using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Geometry.Constraints;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// The store's constraints on shapes that are not drafted entities: a part's own sketches (§9 point 2 of
/// docs/plan-studio-parametric.md, <see cref="PartSketches"/>), whose shapes live in the part's definition in the part's coordinates.
/// The same schema (<c>{id, kind, refs: [{shape, part}], value?, formula?, driving}</c>, refs naming the sketch's own shape ids), the
/// same checks and the same translation to the solver as a view's sketch; only where the shapes come from differs. Kept apart from the
/// view store so the two grow without touching each other.
/// </summary>
public static partial class SketchConstraints
{
    /// <summary>
    /// A part sketch's constraint checked against its shapes and written the one way, as <see cref="Check"/> does a view's: its refs
    /// on the sketch's shapes, of the sorts its kind takes, none twice; a value or formula only on a dimension; formulas naming only
    /// <paramref name="ps"/> (the part's parameters).
    /// </summary>
    internal static void CheckLocal(IReadOnlyDictionary<string, Shape> shapes, JsonObject c, IReadOnlyDictionary<string, double> ps)
    {
        var kind = (string?)c["kind"] ?? throw new CommandException("A constraint needs its kind.");
        if (!Patterns.TryGetValue(kind, out var patterns)) throw new CommandException($"„{kind}” is not a constraint ({string.Join(", ", Kinds)}).");
        var refs = c["refs"] as JsonArray ?? throw new CommandException("A constraint needs its references: [{shape, part}].");
        var found = new List<(string Shape, string Part, RefKind Kind)>();
        foreach (var r in refs)
        {
            if (r is not JsonObject ro || (string?)ro["shape"] is not { } sid) throw new CommandException("A reference is {shape, part}.");
            var shape = shapes.TryGetValue(sid, out var sh) ? sh : throw new CommandException($"The sketch has no shape „{sid}”.");
            var part = (string?)ro["part"] ?? "whole";
            var cl = Classify(shape, part) ?? throw new CommandException($"„{sid}” has no part „{part}”.");
            found.Add((sid, cl.Part, cl.Kind));
        }
        if (found.Count == 0) throw new CommandException("A constraint needs its references.");
        if (found.Select(f => (f.Shape, f.Part)).Distinct().Count() < found.Count) throw new CommandException("A constraint takes a reference once.");
        var letters = found.Select(f => Letter(f.Kind)).ToArray();
        bool Fits(char[] ls) => patterns.Any(p => p.Length == ls.Length && p.Select((set, i) => set.Contains(ls[i])).All(x => x));
        if (!Fits(letters))
        {
            if (found.Count == 2 && Fits([letters[1], letters[0]])) found.Reverse();
            else throw new CommandException($"{Label(kind)} takes {Takes(kind)}.");
        }
        if (kind == "tangent" && found[0].Kind == RefKind.Point)
        {
            if (found.Any(f => f.Part == "center") || found[0].Shape == found[1].Shape) throw new CommandException("Tangent end to end takes the ends of two shapes, not a centre.");
            var sorts = found.Select(f => EndSort(shapes[f.Shape], f.Part)).ToList();
            if (sorts.Any(x => x == null)) throw new CommandException(TangentCorner);
            if (sorts.All(x => x == 'L')) throw new CommandException("Tangent end to end needs an arc: two straight ends meeting tangent make one straight line (use coincident and parallel).");
        }
        c["kind"] = kind;
        c["refs"] = RefsJson(found.Select(f => (f.Shape, f.Part)));

        var driving = c["driving"] is JsonValue dv && dv.TryGetValue<bool>(out var db) ? db : true;
        if (!ValueKinds.Contains(kind))
        {
            if (c["value"] != null || c["formula"] != null) throw new CommandException($"{Label(kind)} takes no value.");
            if (!driving) throw new CommandException($"{Label(kind)} is always driving; only a dimension may be driven.");
            c.Remove("driving");
            c["driving"] = true;
            return;
        }
        c.Remove("driving");
        c["driving"] = driving;
        var formula = ((string?)c["formula"])?.Trim();
        if (formula is { Length: 0 }) formula = null;
        if (formula != null && c["value"] != null) throw new CommandException("A dimension takes a value or a formula, not both.");
        if (formula != null)
        {
            var unknown = Expr.Names(formula).Where(n => !ps.ContainsKey(n)).ToList();
            if (unknown.Count > 0)
                throw new CommandException(ps.Count == 0
                    ? $"„{string.Join("”, „", unknown)}” is not known: the part has no parameters."
                    : $"„{string.Join("”, „", unknown)}” is not a parameter of the part (it has {string.Join(", ", ps.Keys)}).");
            c["formula"] = formula;
            c.Remove("value");
        }
        else if (c["value"] != null)
            c["value"] = Json.Number(c["value"]) is { } x && double.IsFinite(x) ? x : throw new CommandException("A dimension's value is a number.");
        else if (driving) throw new CommandException($"A driving {Label(kind).ToLowerInvariant()} needs its value or a formula.");
        if (c["value"] != null || formula != null)
        {
            var v = Value(c, ps);
            switch (kind)
            {
                case "distance" when v < 0: throw new CommandException("A distance is not negative.");
                case "radius" or "diameter" when v <= 0: throw new CommandException($"A {kind} is more than nothing.");
                case "angle" when !double.IsFinite(v): throw new CommandException("An angle is a number of degrees.");
            }
            if (kind == "angle" && formula == null) c["value"] = Angle(v);
        }
    }

    /// <summary>Why a part sketch's constraint no longer holds together with these shapes; null when it does.</summary>
    internal static string? InvalidLocal(IReadOnlyDictionary<string, Shape> shapes, JsonObject c, IReadOnlyDictionary<string, double> ps)
    {
        try { CheckLocal(shapes, (JsonObject)c.DeepClone(), ps); return null; }
        catch (CommandException ex) { return ex.Message; }
    }

    /// <summary>A part sketch's solve: the solver's picture, its result, and why it failed (null when it did not).</summary>
    internal sealed record LocalSolve(SolverSketch S, SolveResult R, string? Error)
    {
        /// <summary>Each shape as the solve leaves it (as it was when the solve failed).</summary>
        public Dictionary<string, Shape> Shapes() => S.Order.ToDictionary(id => id, id => Error == null ? After(S, R, id) : S.Now[id]);

        /// <summary>What a dimension (by its index in the list solved) measures on the solved geometry.</summary>
        public double? Measured(int stored)
        {
            var i = S.Owner.IndexOf(stored);
            return i >= 0 && i < R.Values.Count && double.IsFinite(R.Values[i]) ? R.Values[i] : null;
        }

        /// <summary>How the sketch stands: <c>{status, dof, freeShapes, redundant, conflicting}</c>.</summary>
        public JsonObject Status()
        {
            var free = new JsonArray();
            for (var p = 0; p < S.Points.Count; p++)
                if (R.PointIsFree[p]) free.Add((JsonNode)new JsonObject { ["shape"] = S.PointParts[p].Shape, ["part"] = S.PointParts[p].Part });
            for (var e = 0; e < S.Entities.Count; e++)
                if (R.EntityIsFree[e]) free.Add((JsonNode)new JsonObject { ["shape"] = S.EntityParts[e].Shape, ["part"] = S.EntityParts[e].Part });
            JsonArray IdsOf(IEnumerable<int> ix) => new(OwnersOf(S, ix).Select(c => (JsonNode)(string)c["id"]!).ToArray());
            var o = new JsonObject
            {
                ["status"] = StatusName(R.Status), ["dof"] = R.DegreesOfFreedom, ["freeShapes"] = free,
                ["redundant"] = IdsOf(R.Redundant), ["conflicting"] = IdsOf(R.Conflicting),
            };
            if (Error != null) o["problem"] = Error;
            return o;
        }
    }

    /// <summary>
    /// A part sketch solved: every shape (<c>Start</c> where an edit began, the shape as it is otherwise) an unknown, its constraints
    /// translated as a view's are with <paramref name="values"/> for their formulas. With <paramref name="edited"/>, the points an edit
    /// moved are drag goals, as a user's edit of a view's shapes. A formula that does not read throws; a solve that fails or an edit the
    /// constraints cannot have comes back with its <see cref="LocalSolve.Error"/>.
    /// </summary>
    internal static LocalSolve SolveLocal(IReadOnlyList<(string Id, Shape Shape)> shapes, IReadOnlyList<JsonObject> constraints, IReadOnlyDictionary<string, double> values, IReadOnlyDictionary<string, Shape>? edited = null)
    {
        var s = BuildLocal(shapes, constraints, values, edited);
        var goals = edited != null ? GoalsOf(s, edited) : [];
        var r = Run(s, goals);
        if (!r.Succeeded) return new LocalSolve(s, r, Failure(s, r));
        if (!Reached(goals, r)) return new LocalSolve(s, r, Blocked(s, goals, r));
        return new LocalSolve(s, r, null);
    }

    /// <summary>
    /// A part sketch with one point (<paramref name="shape"/>'s <paramref name="part"/>, a point part written the one way) dragged
    /// to <paramref name="to"/> (the sketch's plane coordinates), the rest following its constraints solved with
    /// <paramref name="values"/>; as the view's <c>dragSketch</c> does. A goal the constraints cannot reach comes back with its
    /// <see cref="LocalSolve.Error"/>, naming the constraints that hold the point.
    /// </summary>
    internal static LocalSolve DragLocal(IReadOnlyList<(string Id, Shape Shape)> shapes, IReadOnlyList<JsonObject> constraints, IReadOnlyDictionary<string, double> values, string shape, string part, Vec2 to)
    {
        var s = BuildLocal(shapes, constraints, values, null);
        var p = s.PointAt(shape, part) ?? throw new CommandException($"„{part}” is not a point of „{shape}”.");
        var goals = new List<DragGoal> { new(p, to.X, to.Y) };
        var r = Run(s, goals);
        if (!r.Succeeded) return new LocalSolve(s, r, Failure(s, r));
        if (!Reached(goals, r)) return new LocalSolve(s, r, Blocked(s, goals, r));
        return new LocalSolve(s, r, null);
    }

    /// <summary>A part sketch's solver system: every shape an unknown, its constraints translated with <paramref name="values"/>.</summary>
    static SolverSketch BuildLocal(IReadOnlyList<(string Id, Shape Shape)> shapes, IReadOnlyList<JsonObject> constraints, IReadOnlyDictionary<string, double> values, IReadOnlyDictionary<string, Shape>? edited)
    {
        var s = new SolverSketch();
        foreach (var (id, now) in shapes)
            s.AddShape(id, id, now, edited != null && edited.TryGetValue(id, out var was) && Pairs(was, now) ? was : now);
        var ps = values as Dictionary<string, double> ?? new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase);
        foreach (var c in constraints)
        {
            if (Refs(c).Any(r => !s.Now.ContainsKey(r.Shape))) continue;
            var index = s.Stored.Count;
            s.Stored.Add(c);
            foreach (var sc in Translate(s, c, () => ps))
            {
                s.Constraints.Add(sc);
                s.Owner.Add(index);
            }
        }
        return s;
    }

    /// <summary>
    /// A part sketch's constraint as <see cref="Resolve"/> gives a view's: each ref's place on <paramref name="shapes"/> (the solved
    /// ones), a formula's value worked out with <paramref name="values"/>, and a dimension's <c>measured</c> (degrees for an angle).
    /// </summary>
    internal static JsonObject ResolveLocal(JsonObject c, IReadOnlyDictionary<string, Shape> shapes, IReadOnlyDictionary<string, double> values, double? measured)
    {
        var o = (JsonObject)c.DeepClone();
        var refs = new JsonArray();
        foreach (var (sid, part) in Refs(c))
        {
            var r = new JsonObject { ["shape"] = sid, ["part"] = part };
            if (shapes.TryGetValue(sid, out var s))
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
        var kind = (string?)c["kind"] ?? "";
        if (ValueKinds.Contains(kind))
        {
            if ((string?)c["formula"] is { } f)
            {
                var (v, error) = Expr.TryEval(f, k => values.TryGetValue(k, out var x) ? x : null);
                if (v is { } x) o["value"] = Math.Round(x, 9); else o["problem"] = error;
            }
            if (measured is { } mv) o["measured"] = Math.Round(kind == "angle" ? Angle(mv * 180 / Math.PI) : mv, 9);
        }
        return o;
    }

    /// <summary>A part sketch's dimension's value worked out with some values (a formula's, or its number).</summary>
    internal static double? LocalValue(JsonObject c, IReadOnlyDictionary<string, double> values)
    {
        if ((string?)c["formula"] == null && c["value"] == null) return null;
        try { return Value(c, values); } catch (CommandException) { return null; }
    }
}
