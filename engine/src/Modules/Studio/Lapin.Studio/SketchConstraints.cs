using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Sketch constraints, stage C0 of decision 520 (docs/plan-studio-parametric.md §4): stored, checked, resolved for drawing, and
/// carried through the drafting commands that replace shapes. The solve comes with stage C1 (<see cref="AfterChange"/>).
///
/// <para><b>The carrier.</b> A sketch is the drafted shapes on one view (a plan, a section, an elevation, a reference plane, a
/// storey's implicit plan <c>plan:&lt;storey&gt;</c>); its constraints are kept by one <see cref="Class"/> entity per view,
/// <c>{viewId, constraints: [...]}</c>, a Lapin resource: saved in the <c>.lapin</c> package as any entity, never written to IFC
/// (IFC has no sketch constraints; the solved shapes go as they do today). A separate entity rather than the view's own attributes
/// because a storey's implicit plan has no entity, and so a view's other edits never touch its constraints. The carrier goes when
/// its last constraint does.</para>
///
/// <para><b>A constraint</b> is <c>{id, kind, refs: [{shape, part}], value?, formula?, driving}</c>: <c>shape</c> a drafted shape's id
/// on the carrier's view, <c>part</c> one of <c>start</c>, <c>end</c>, <c>center</c> (points), <c>vertex:i</c> (a polyline's point),
/// <c>edge:i</c> (a polyline's side), <c>whole</c> (a line's edge, a circle or an arc, or a polyline as a whole for <c>fix</c>). Lengths
/// are metres; angles degrees, signed, counter-clockwise (one line: from the view's x axis; two: from the first to the second);
/// <c>distanceX</c>/<c>distanceY</c> signed (a point from the sketch's origin, the second point from the first, a line's end from its
/// start). A dimension (<see cref="ValueKinds"/>) is driving (its <c>value</c>, or a <c>formula</c> of the parameters of the part
/// whose recipe uses the sketch, evaluated on every ask, never stored) or driven (a reference, measured).</para>
/// </summary>
public static partial class SketchConstraints
{
    public const string Class = "LapinSketch";

    /// <summary>The kinds, FreeCAD Sketcher's (§4.1).</summary>
    public static readonly string[] Kinds =
    [
        "coincident", "pointOn", "horizontal", "vertical", "parallel", "perpendicular", "tangent", "equal", "symmetric", "fix",
        "distanceX", "distanceY", "distance", "radius", "diameter", "angle", "midpoint",
    ];

    /// <summary>The kinds carrying a value: dimensions, driving or driven.</summary>
    public static readonly string[] ValueKinds = ["distanceX", "distanceY", "distance", "radius", "diameter", "angle"];

    /// <summary>What a reference is: a point, a straight edge, a circle or an arc, or a polyline whole.</summary>
    public enum RefKind { Point, Line, Curve, Shape }

    const char P = 'P', L = 'L', C = 'C', S = 'S';

    /// <summary>Each kind's allowed references, in order: P a point, L a straight edge, C a circle or arc, S a polyline whole.</summary>
    static readonly Dictionary<string, string[][]> Patterns = new()
    {
        ["coincident"] = [["P", "P"]],
        ["pointOn"] = [["P", "LC"]],
        ["horizontal"] = [["L"], ["P", "P"]],
        ["vertical"] = [["L"], ["P", "P"]],
        ["parallel"] = [["L", "L"]],
        ["perpendicular"] = [["L", "L"]],
        ["tangent"] = [["L", "C"], ["C", "L"], ["C", "C"], ["P", "P"]],
        ["equal"] = [["L", "L"], ["C", "C"]],
        ["symmetric"] = [["P", "P", "L"], ["P", "P", "P"]],
        ["fix"] = [["P"], ["L"], ["C"], ["S"]],
        ["distanceX"] = [["P"], ["P", "P"], ["L"]],
        ["distanceY"] = [["P"], ["P", "P"], ["L"]],
        ["distance"] = [["P", "P"], ["L"], ["P", "L"]],
        ["radius"] = [["C"]],
        ["diameter"] = [["C"]],
        ["angle"] = [["L"], ["L", "L"]],
        ["midpoint"] = [["P", "L"]],
    };

    static char Letter(RefKind k) => k switch { RefKind.Point => P, RefKind.Line => L, RefKind.Curve => C, _ => S };

    // ------------------------------------------------------------ references

    /// <summary>A reference on a shape: what it is and its part written the one way (a line's <c>edge:0</c> is <c>whole</c>, an open polyline's <c>start</c> is <c>vertex:0</c>); null when the shape has no such part.</summary>
    public static (RefKind Kind, string Part)? Classify(Shape s, string? part)
    {
        if (part == null) return null;
        int? Index(string prefix) => part.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(part[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : null;
        switch (s)
        {
            case Line:
                return part switch
                {
                    "start" or "end" => (RefKind.Point, part),
                    "whole" or "edge:0" => (RefKind.Line, "whole"),
                    _ => null,
                };
            case Polyline p:
            {
                var n = p.Pts.Count;
                var edges = p.Closed ? n : n - 1;
                if (part == "start" && !p.Closed) return (RefKind.Point, "vertex:0");
                if (part == "end" && !p.Closed) return (RefKind.Point, $"vertex:{n - 1}");
                if (part == "whole") return (RefKind.Shape, "whole");
                if (Index("vertex:") is { } v && v < n) return (RefKind.Point, $"vertex:{v}");
                if (Index("edge:") is { } e && e < edges) return (RefKind.Line, $"edge:{e}");
                return null;
            }
            case Circle:
                return part switch { "center" => (RefKind.Point, part), "whole" => (RefKind.Curve, part), _ => null };
            case Arc:
                return part switch { "start" or "end" or "center" => (RefKind.Point, part), "whole" => (RefKind.Curve, part), _ => null };
        }
        return null;
    }

    /// <summary>A point reference's place on the view.</summary>
    public static Vec2? PointOf(Shape s, string part) => (s, part) switch
    {
        (Line l, "start") => l.A,
        (Line l, "end") => l.B,
        (Arc a, "start") => a.At(a.Start),
        (Arc a, "end") => a.At(a.End),
        (Arc a, "center") => a.C,
        (Circle c, "center") => c.C,
        (Polyline p, _) when part.StartsWith("vertex:", StringComparison.Ordinal) && int.TryParse(part[7..], out var i) && i < p.Pts.Count => p.Pts[i],
        _ => null,
    };

    /// <summary>A straight edge's ends.</summary>
    public static (Vec2 A, Vec2 B)? SegmentOf(Shape s, string part) => (s, part) switch
    {
        (Line l, "whole") => (l.A, l.B),
        (Polyline p, _) when part.StartsWith("edge:", StringComparison.Ordinal) && int.TryParse(part[5..], out var i) && i < (p.Closed ? p.Pts.Count : p.Pts.Count - 1)
            => (p.Pts[i], p.Pts[(i + 1) % p.Pts.Count]),
        _ => null,
    };

    /// <summary>The point references a shape has, in the order a mapping prefers them.</summary>
    static IEnumerable<string> PointParts(Shape s) => s switch
    {
        Line => ["start", "end"],
        Arc => ["start", "end", "center"],
        Circle => ["center"],
        Polyline p => Enumerable.Range(0, p.Pts.Count).Select(i => $"vertex:{i}"),
        _ => [],
    };

    /// <summary>The straight edges a shape has.</summary>
    static IEnumerable<string> EdgeParts(Shape s) => s switch
    {
        Line => ["whole"],
        Polyline p => Enumerable.Range(0, p.Closed ? p.Pts.Count : p.Pts.Count - 1).Select(i => $"edge:{i}"),
        _ => [],
    };

    // ------------------------------------------------------------ storage

    /// <summary>The carrier of a view's constraints; null when it has none.</summary>
    public static Entity? CarrierOf(IModelView m, string viewId) => m.OfClass(Class).FirstOrDefault(e => e.Str("viewId") == viewId);

    /// <summary>A carrier's constraints, as stored.</summary>
    public static List<JsonObject> ListOf(Entity? carrier) =>
        (carrier?.Attrs["constraints"] as JsonArray)?.OfType<JsonObject>().Select(c => (JsonObject)c.DeepClone()).ToList() ?? [];

    /// <summary>A view's constraints written back: the carrier made, changed, or removed when none is left.</summary>
    static void Write(ICommandContext ctx, string viewId, IReadOnlyList<JsonObject> list)
    {
        var carrier = CarrierOf(ctx.Model, viewId);
        if (list.Count == 0)
        {
            if (carrier != null) ctx.Remove(carrier.Id);
            return;
        }
        var arr = new JsonArray(list.Select(c => (JsonNode)c.DeepClone()).ToArray());
        if (carrier == null)
        {
            var name = Views.Find(ctx.Model, viewId)?.Name is { Length: > 0 } vn ? $"Constraints on {vn}" : "Sketch constraints";
            ctx.Add(new Entity(ctx.NewId(), Class, name, new JsonObject { ["viewId"] = viewId, ["constraints"] = arr }));
            return;
        }
        if (JsonNode.DeepEquals(carrier.Attrs["constraints"], arr)) return;
        var attrs = carrier.CloneAttrs();
        attrs["constraints"] = arr;
        ctx.Replace(carrier.With(attrs: attrs));
    }

    /// <summary>The carrier and the constraint of that id.</summary>
    static (Entity Carrier, List<JsonObject> List, int Index) Find(IModelView m, string? id)
    {
        if (id != null)
            foreach (var carrier in m.OfClass(Class))
            {
                var list = ListOf(carrier);
                var i = list.FindIndex(c => (string?)c["id"] == id);
                if (i >= 0) return (carrier, list, i);
            }
        throw new CommandException("There is no such constraint.");
    }

    static IEnumerable<(string Shape, string Part)> Refs(JsonObject c) =>
        (c["refs"] as JsonArray)?.OfType<JsonObject>().Select(r => ((string?)r["shape"] ?? "", (string?)r["part"] ?? "")) ?? [];

    static JsonArray RefsJson(IEnumerable<(string Shape, string Part)> refs) =>
        new(refs.Select(r => (JsonNode)new JsonObject { ["shape"] = r.Shape, ["part"] = r.Part }).ToArray());

    // ------------------------------------------------------------ checking

    /// <summary>The parameters a sketch's formulas may name, with their defaults: those of the parts whose recipe uses one of the view's shapes.</summary>
    public static Dictionary<string, double> ParamsOf(IModelView m, string viewId)
    {
        var o = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var drafts = Drafts.On(m, viewId).Select(d => d.Id).ToHashSet();
        if (drafts.Count == 0) return o;
        foreach (var solid in StudioModule.Classes.SelectMany(m.OfClass))
        {
            if (solid.Str("operation") == null) continue;
            var used = new[] { "profile", "guide", "endProfile", "rail" }.Select(solid.Str)
                .Concat((solid.Attrs["profiles"] as JsonArray)?.Select(x => (string?)x) ?? []);
            if (!used.Any(id => id != null && drafts.Contains(id))) continue;
            if (Parts.DefOf(Parts.TypeOf(m, solid)) is { } def)
                foreach (var (k, v) in Parts.Values(def, null)) o.TryAdd(k, v);
        }
        return o;
    }

    static double Value(JsonObject c, IReadOnlyDictionary<string, double> ps)
    {
        if ((string?)c["formula"] is { } f)
        {
            var (v, error) = Expr.TryEval(f, k => ps.TryGetValue(k, out var x) ? x : null);
            return v ?? throw new CommandException($"The formula „{f}”: {error}");
        }
        return Json.Number(c["value"]) ?? double.NaN;
    }

    /// <summary>
    /// A constraint checked against the model and written the one way (its refs' parts, its kind's order): its refs on one view's
    /// drafted shapes, as many and of the sorts its kind takes, none twice; a value or formula only on a dimension, a driving one
    /// needing one; lengths not negative (a radius or a diameter more than nothing); formulas naming only the part's parameters.
    /// Returns the view the constraint belongs to.
    /// </summary>
    static string Check(IModelView m, JsonObject c, string? viewId)
    {
        var kind = (string?)c["kind"] ?? throw new CommandException("A constraint needs its kind.");
        if (!Patterns.TryGetValue(kind, out var patterns)) throw new CommandException($"„{kind}” is not a constraint ({string.Join(", ", Kinds)}).");
        var refs = c["refs"] as JsonArray ?? throw new CommandException("A constraint needs its references: [{shape, part}].");
        var found = new List<(string Shape, string Part, RefKind Kind)>();
        string? view = viewId;
        foreach (var r in refs)
        {
            if (r is not JsonObject ro || (string?)ro["shape"] is not { } sid) throw new CommandException("A reference is {shape, part}.");
            var e = m.Get(sid) is { Class: IfcClass.Annotation, PredefinedType: Drafts.Predefined } d && d.Attrs["hatch"] == null ? d
                : throw new CommandException($"„{sid}” is not a drafted line, polyline, circle or arc.");
            var shape = Drafts.ShapeOf(e) ?? throw new CommandException($"„{e.Name}” has no shape.");
            var part = (string?)ro["part"] ?? "whole";
            var cl = Classify(shape, part) ?? throw new CommandException($"„{e.Name}” has no part „{part}”.");
            var v = e.Str("viewId") ?? "";
            if (view == null) view = v;
            else if (view != v) throw new CommandException("A constraint's references lie on one sketch: its shapes are on another view.");
            found.Add((sid, cl.Part, cl.Kind));
        }
        if (found.Count == 0) throw new CommandException("A constraint needs its references.");
        if (found.Select(f => (f.Shape, f.Part)).Distinct().Count() < found.Count) throw new CommandException("A constraint takes a reference once.");
        // The refs in the order the kind takes them (two may come the other way round).
        var letters = found.Select(f => Letter(f.Kind)).ToArray();
        bool Fits(char[] ls) => patterns.Any(p => p.Length == ls.Length && p.Select((set, i) => set.Contains(ls[i])).All(x => x));
        if (!Fits(letters))
        {
            if (found.Count == 2 && Fits([letters[1], letters[0]])) found.Reverse();
            else throw new CommandException($"{Label(kind)} takes {Takes(kind)}.");
        }
        // Tangent end to end: the ends of lines, arcs or polylines, never a centre.
        if (kind == "tangent" && found[0].Kind == RefKind.Point && found.Any(f => f.Part == "center")) throw new CommandException("Tangent end to end takes the ends of two shapes, not a centre.");
        if (kind == "tangent" && found[0].Kind == RefKind.Point && found[0].Shape == found[1].Shape) throw new CommandException("Tangent end to end takes the ends of two shapes.");
        c["kind"] = kind;
        c["refs"] = RefsJson(found.Select(f => (f.Shape, f.Part)));

        var dimension = ValueKinds.Contains(kind);
        var driving = c["driving"] is JsonValue dv && dv.TryGetValue<bool>(out var db) ? db : true;
        if (!dimension)
        {
            if (c["value"] != null || c["formula"] != null) throw new CommandException($"{Label(kind)} takes no value.");
            if (!driving) throw new CommandException($"{Label(kind)} is always driving; only a dimension may be driven.");
            c.Remove("driving");
            c["driving"] = true;
            return view!;
        }
        c["driving"] = driving;
        var formula = ((string?)c["formula"])?.Trim();
        if (formula is { Length: 0 }) formula = null;
        if (formula != null && c["value"] != null) throw new CommandException("A dimension takes a value or a formula, not both.");
        if (formula != null)
        {
            var ps = ParamsOf(m, view!);
            var unknown = Expr.Names(formula).Where(n => !ps.ContainsKey(n)).ToList();
            if (unknown.Count > 0)
                throw new CommandException(ps.Count == 0
                    ? $"„{string.Join("”, „", unknown)}” is not known: the sketch belongs to no part with parameters."
                    : $"„{string.Join("”, „", unknown)}” is not a parameter of the part (it has {string.Join(", ", ps.Keys)}).");
            c["formula"] = formula;
            c.Remove("value");
        }
        else if (c["value"] != null)
        {
            c["value"] = Json.Number(c["value"]) is { } x && double.IsFinite(x) ? x : throw new CommandException("A dimension's value is a number.");
        }
        else if (driving) throw new CommandException($"A driving {Label(kind).ToLowerInvariant()} needs its value or a formula.");
        if (c["value"] != null || formula != null)
        {
            var v = Value(c, ParamsOf(m, view!));
            switch (kind)
            {
                case "distance" when v < 0: throw new CommandException("A distance is not negative.");
                case "radius" or "diameter" when v <= 0: throw new CommandException($"A {kind} is more than nothing.");
                case "angle" when !double.IsFinite(v): throw new CommandException("An angle is a number of degrees.");
            }
            if (kind == "angle" && formula == null) c["value"] = Angle(v);
        }
        return view!;
    }

    /// <summary>An angle in degrees, in (-180, 180].</summary>
    static double Angle(double deg)
    {
        var a = deg % 360;
        if (a <= -180) a += 360;
        if (a > 180) a -= 360;
        return Math.Round(a, 9);
    }

    static string Label(string kind) => kind switch
    {
        "pointOn" => "Point on object",
        "distanceX" => "Horizontal distance",
        "distanceY" => "Vertical distance",
        _ => char.ToUpperInvariant(kind[0]) + kind[1..],
    };

    static string Takes(string kind)
    {
        static string Word(char ch) => ch switch { P => "a point", L => "a line", C => "a circle or an arc", _ => "a polyline whole" };
        return string.Join("; or ", Patterns[kind].Select(p => string.Join(" and ", p.Select(set => string.Join(" or ", set.Select(Word))))));
    }

    // ------------------------------------------------------------ resolved for drawing

    /// <summary>A constraint with each ref's place now (a point's <c>at</c>, an edge's <c>a</c> and <c>b</c>, a curve's centre, radius and ends), its value (a formula's worked out) and, for a dimension, what it measures now.</summary>
    public static JsonObject Resolve(IModelView m, string viewId, JsonObject c, IReadOnlyDictionary<string, double>? ps = null)
    {
        var o = (JsonObject)c.DeepClone();
        o["viewId"] = viewId;
        var refs = new JsonArray();
        foreach (var (sid, part) in Refs(c))
        {
            var r = new JsonObject { ["shape"] = sid, ["part"] = part };
            if (m.Get(sid) is { } e && Drafts.ShapeOf(e) is { } s)
            {
                if (PointOf(s, part) is { } p) r["at"] = J(p);
                else if (SegmentOf(s, part) is (var a, var b)) { r["a"] = J(a); r["b"] = J(b); }
                else if (part == "whole" && s is Circle ci) { r["c"] = J(ci.C); r["r"] = ci.R; }
                else if (part == "whole" && s is Arc ar) { r["c"] = J(ar.C); r["r"] = ar.R; r["start"] = J(ar.At(ar.Start)); r["end"] = J(ar.At(ar.End)); }
                else if (part == "whole" && s is Polyline pl) r["pts"] = new JsonArray(pl.Pts.Select(q => (JsonNode)J(q)).ToArray());
            }
            else r["missing"] = true;
            refs.Add((JsonNode)r);
        }
        o["refs"] = refs;
        if (ValueKinds.Contains((string?)c["kind"] ?? ""))
        {
            if ((string?)c["formula"] != null)
            {
                ps ??= ParamsOf(m, viewId);
                var (v, error) = Expr.TryEval((string)c["formula"]!, k => ps.TryGetValue(k, out var x) ? x : null);
                if (v is { } x) o["value"] = Math.Round(x, 9); else o["problem"] = error;
            }
            if (Measure(m, c) is { } mv) o["measured"] = Math.Round(mv, 9);
        }
        return o;
    }

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    /// <summary>What a dimension measures on the shapes as they are (metres, degrees); null when a ref is gone.</summary>
    public static double? Measure(IModelView m, JsonObject c)
    {
        var shapes = new List<(Shape S, string Part)>();
        foreach (var (sid, part) in Refs(c))
        {
            if (m.Get(sid) is not { } e || Drafts.ShapeOf(e) is not { } s) return null;
            shapes.Add((s, part));
        }
        Vec2? Pt(int i) => PointOf(shapes[i].S, shapes[i].Part);
        (Vec2 A, Vec2 B)? Seg(int i) => SegmentOf(shapes[i].S, shapes[i].Part);
        double Deg(Vec2 d) => Math.Atan2(d.Y, d.X) * 180 / Math.PI;
        switch ((string?)c["kind"])
        {
            case "distanceX" or "distanceY":
            {
                var y = (string?)c["kind"] == "distanceY";
                Vec2 d;
                if (shapes.Count == 1 && Pt(0) is { } p) d = p;
                else if (shapes.Count == 1 && Seg(0) is (var a, var b)) d = b - a;
                else if (shapes.Count == 2 && Pt(0) is { } p0 && Pt(1) is { } p1) d = p1 - p0;
                else return null;
                return y ? d.Y : d.X;
            }
            case "distance":
                if (shapes.Count == 1 && Seg(0) is (var sa, var sb)) return (sb - sa).Length;
                if (Pt(0) is { } q && Pt(1) is { } q1) return (q1 - q).Length;
                if (Pt(0) is { } q2 && Seg(1) is (var la, var lb))
                {
                    var dir = lb - la;
                    return dir.Length < 1e-12 ? (q2 - la).Length : Math.Abs((dir.X * (q2.Y - la.Y) - dir.Y * (q2.X - la.X)) / dir.Length);
                }
                return null;
            case "radius" or "diameter":
            {
                var r = shapes[0].S switch { Circle ci => ci.R, Arc ar => ar.R, _ => double.NaN };
                return double.IsNaN(r) ? null : (string?)c["kind"] == "diameter" ? 2 * r : r;
            }
            case "angle":
                if (shapes.Count == 1 && Seg(0) is (var a1, var b1)) return Angle(Deg(b1 - a1));
                if (shapes.Count == 2 && Seg(0) is (var a2, var b2) && Seg(1) is (var a3, var b3)) return Angle(Deg(b3 - a3) - Deg(b2 - a2));
                return null;
        }
        return null;
    }

    // ------------------------------------------------------------ the seam for the solver

    /// <summary>
    /// After a command changed a sketch or its constraints, in the same undo step: today the constraints are checked against the
    /// shapes as they are (one whose shape or part is gone is dropped, with the reason in the command's report). Stage C1 solves
    /// here: the sketch's shapes and constraints to the solver (<c>Lapin.Geometry/Constraints</c>), the shapes written back with
    /// <see cref="Drafts.Set"/>, so the solids built from them rebuild as they already do.
    /// </summary>
    internal static void AfterChange(ICommandContext ctx, string viewId)
    {
        var list = ListOf(CarrierOf(ctx.Model, viewId));
        if (list.Count == 0) return;
        var kept = new List<JsonObject>();
        foreach (var c in list)
        {
            if (Invalid(ctx.Model, c, viewId) is { } reason) Report(ctx).Dropped.Add(Dropped(c, reason));
            else kept.Add(c);
        }
        if (kept.Count != list.Count) Write(ctx, viewId, kept);
    }

    /// <summary>Why a stored constraint no longer holds together (a shape gone, a part it no longer has, its refs no longer of its kind's sorts); null when it does.</summary>
    static string? Invalid(IModelView m, JsonObject c, string viewId)
    {
        foreach (var (sid, part) in Refs(c))
        {
            if (m.Get(sid) is not { Class: IfcClass.Annotation, PredefinedType: Drafts.Predefined } e || Drafts.ShapeOf(e) is not { } s) return "its shape was deleted";
            if (e.Str("viewId") != viewId) return "its shape left the sketch";
            if (Classify(s, part) is not { } cl || cl.Part != part) return $"its shape no longer has the part „{part}”";
        }
        try { Check(m, (JsonObject)c.DeepClone(), viewId); }
        catch (CommandException ex) { return ex.Message; }
        return null;
    }

    static JsonObject Dropped(JsonObject c, string reason) => new() { ["id"] = (string?)c["id"], ["kind"] = (string?)c["kind"], ["reason"] = reason };

    // ------------------------------------------------------------ what a command did to constraints, for its result

    internal sealed class Outcome
    {
        public List<JsonObject> Dropped { get; } = [];
        public List<string> Added { get; } = [];
        public int Copied { get; set; }
        /// <summary>element.transform's own placement and copies, told by <see cref="TransformWatch"/>.</summary>
        public Placement2D? Transform { get; set; }
        public IReadOnlyDictionary<string, string>? Copies { get; set; }
    }

    static readonly ConditionalWeakTable<ICommandContext, Outcome> Outcomes = new();

    internal static Outcome Report(ICommandContext ctx) => Outcomes.GetValue(ctx, _ => new Outcome());

    /// <summary>The command's result with what happened to constraints: <c>dropped: [{id, kind, reason}]</c>, <c>constraintsCopied</c>, <c>constraintsAdded</c>.</summary>
    internal static JsonNode? Attach(ICommandContext ctx, JsonNode? result)
    {
        if (!Outcomes.TryGetValue(ctx, out var o) || o.Dropped.Count == 0 && o.Added.Count == 0 && o.Copied == 0) return result;
        if (result is not JsonObject r)
        {
            if (result != null) return result;
            r = new JsonObject();
        }
        if (o.Dropped.Count > 0) r["dropped"] = new JsonArray(o.Dropped.Select(d => (JsonNode)d.DeepClone()).ToArray());
        if (o.Copied > 0) r["constraintsCopied"] = o.Copied;
        if (o.Added.Count > 0) r["constraintsAdded"] = new JsonArray(o.Added.Select(x => (JsonNode)x).ToArray());
        return r;
    }

    // ------------------------------------------------------------ commands and queries

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Sketch constraints", [], "#8a94a6", Kind: "resource", Template: false));

        // {viewId?, kind, refs: [{shape, part}], value?, formula?, driving?} → {id}.
        r.Command("addConstraint", "Constraint", (ctx, a) =>
        {
            var c = new JsonObject { ["kind"] = (string?)a["kind"], ["refs"] = (a["refs"] as JsonArray)?.DeepClone() };
            if (a["value"] is { } value) c["value"] = Json.Number(value) is { } v ? v : value.DeepClone();
            if ((string?)a["formula"] is { } formula) c["formula"] = formula;
            if (a["driving"] is JsonValue dv && dv.TryGetValue<bool>(out var driving)) c["driving"] = driving;
            var viewId = (string?)a["viewId"] ?? (string?)a["sketch"];
            var view = Check(ctx.Model, c, viewId);
            var id = ctx.NewId();
            var stored = new JsonObject { ["id"] = id };
            foreach (var (k, v) in c) stored[k] = v?.DeepClone();
            var list = ListOf(CarrierOf(ctx.Model, view));
            list.Add(stored);
            Write(ctx, view, list);
            AfterChange(ctx, view);
            return Attach(ctx, new JsonObject { ["id"] = id });
        });

        // {id, value?, formula?, driving?}: a value takes the place of a formula and the other way round; null clears.
        r.Command("updateConstraint", "Change constraint", (ctx, a) =>
        {
            var (carrier, list, i) = Find(ctx.Model, (string?)a["id"]);
            var view = carrier.Str("viewId")!;
            var c = (JsonObject)list[i].DeepClone();
            if (a.ContainsKey("value")) { c.Remove("value"); if (a["value"] != null) { c["value"] = a["value"]!.DeepClone(); c.Remove("formula"); } }
            if (a.ContainsKey("formula")) { c.Remove("formula"); if (a["formula"] != null) { c["formula"] = a["formula"]!.DeepClone(); c.Remove("value"); } }
            if (a["driving"] != null) c["driving"] = a["driving"]!.DeepClone();
            Check(ctx.Model, c, view);
            list[i] = c;
            Write(ctx, view, list);
            AfterChange(ctx, view);
            return Attach(ctx, null);
        });

        // {ids} or {id}.
        r.Command("removeConstraint", "Remove constraint", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Which constraints?");
            foreach (var id in ids)
            {
                var (carrier, list, i) = Find(ctx.Model, id);
                list.RemoveAt(i);
                Write(ctx, carrier.Str("viewId")!, list);
            }
            return null;
        });

        // {viewId} (a sketch's), {shapeIds} (those touching them) or {elementId} (a Studio solid's sketches') → {constraints}.
        r.Query("constraints", (q, a) => new JsonObject { ["constraints"] = Query(q.Model, (string?)a["viewId"], a["shapeIds"] as JsonArray, (string?)a["elementId"]) });

        RegisterMapping(r);
    }

    static JsonArray Query(IModelView m, string? viewId, JsonArray? shapeIds, string? elementId)
    {
        var o = new JsonArray();
        if (viewId != null)
        {
            var ps = ParamsOf(m, viewId);
            foreach (var c in ListOf(CarrierOf(m, viewId))) o.Add((JsonNode)Resolve(m, viewId, c, ps));
            return o;
        }
        HashSet<string> shapes;
        if (shapeIds is { } sa) shapes = sa.Select(x => (string?)x).OfType<string>().ToHashSet();
        else if (elementId is { } eid)
        {
            var e = m.Get(eid) is { } x && x.Str("operation") != null ? x : throw new CommandException("Not a Studio solid or a placed part.");
            shapes = Parts.Tree(m, null, e)["sketches"]!.AsArray().SelectMany(s => s!["shapeIds"]!.AsArray().Select(i => (string)i!)).ToHashSet();
        }
        else throw new CommandException("Whose constraints: {viewId}, {shapeIds} or {elementId}?");
        foreach (var carrier in m.OfClass(Class))
        {
            var view = carrier.Str("viewId")!;
            var mine = ListOf(carrier).Where(c => Refs(c).Any(r => shapes.Contains(r.Shape))).ToList();
            if (mine.Count == 0) continue;
            var ps = ParamsOf(m, view);
            foreach (var c in mine) o.Add((JsonNode)Resolve(m, view, c, ps));
        }
        return o;
    }

    /// <summary>The constraints touching some shapes of a view, resolved: what the part tree shows of a sketch.</summary>
    public static JsonArray Touching(IModelView m, string viewId, IReadOnlyCollection<string> shapeIds)
    {
        var list = ListOf(CarrierOf(m, viewId)).Where(c => Refs(c).Any(r => shapeIds.Contains(r.Shape))).ToList();
        if (list.Count == 0) return [];
        var ps = ParamsOf(m, viewId);
        return new JsonArray(list.Select(c => (JsonNode)Resolve(m, viewId, c, ps)).ToArray());
    }
}
