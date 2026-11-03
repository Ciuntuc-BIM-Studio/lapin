using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// Steel connections (docs/plan-steel.md stage 3, decision 273). A <c>LapinSteelNode</c> keeps the members it joins by
/// their ids (each with the end of it that comes to the connection, or none for the member it is fixed to along its side),
/// its kind and its parameters (<see cref="NodeParams"/>); its plates, bolts and anchors are derived from them and from
/// the members as they are, so they follow a member moved or a hall built again. A connection writes the members' ends it
/// needs as their ends' rules (<see cref="SteelEnd.Extension"/>: a rafter set back by its end plate, a column by its base
/// plate, a gable post by the gap under its rafter), as the joins of walls write theirs.
/// <list type="bullet">
/// <item><c>base</c>: a column on its base plate, its anchors down into the foundation (into a footing of the structure
/// under it, when there is one), pinned (2 or 4 anchors) or fixed (4–8 anchors outside the flanges, ribs);</item>
/// <item><c>eaves</c>: a rafter's extended end plate bolted to a column's flange, the column stiffened in line with the
/// rafter's flanges (its haunch's);</item>
/// <item><c>apex</c> and <c>splice</c>: two members' end plates bolted together, at a mitre or in line;</item>
/// <item><c>post</c>: a gable post under a rafter, a fin plate welded under the rafter bolted to its web through slotted holes.</item>
/// </list>
/// In IFC a connection is an IfcElementAssembly of IfcPlate, IfcMechanicalFastener (BOLT, ANCHORBOLT) and
/// IfcDiscreteAccessory (ANCHORPLATE), realizing an IfcRelConnectsWithRealizingElements between the members; the welds are
/// data (the throat and length of each), not bodies.
/// </summary>
public static class SteelNodes
{
    public const string Class = "LapinSteelNode";
    /// <summary>The key a hall's connection keeps its place in the hall under ("eaves@f1:raf:a@start"): its members' roles.</summary>
    public const string RoleKey = "hallNode";
    public static readonly string[] Kinds = ["base", "eaves", "apex", "splice", "post"];

    public static string Label(string kind) => kind switch
    {
        "base" => "Column base", "eaves" => "Eaves connection", "apex" => "Apex connection", "splice" => "Splice", _ => "Gable post connection",
    };

    /// <summary>A member a connection joins: its id, and the end of it that comes there (null: joined along its side).</summary>
    public sealed record Joined(string Id, string? End)
    {
        public JsonObject ToJson() { var o = new JsonObject { ["id"] = Id }; if (End != null) o["end"] = End; return o; }
    }

    /// <summary>A weld a connection asks for, as data: where, its throat, its length (metres).</summary>
    public sealed record Weld(string Where, double Throat, double Length);

    public static string KindOf(Entity n) => n.Str("kind") is { } k && Kinds.Contains(k) ? k : "base";

    public static List<Joined> MembersOf(Entity n) =>
        (n.Attrs["members"] as JsonArray)?.OfType<JsonObject>()
            .Select(o => new Joined((string?)o["id"] ?? "", (string?)o["end"] is "start" or "end" ? (string)o["end"]! : null))
            .Where(j => j.Id.Length > 0).ToList() ?? [];

    public static NodeParams ParamsOf(Entity n) => NodeParams.From(n.Attrs["params"]);

    static JsonArray Json(IEnumerable<Joined> js) => new([.. js.Select(j => (JsonNode)j.ToJson())]);

    /// <summary>The member whose section the connection's bolts are chosen by: the one that comes to it by its end (the column at a base).</summary>
    static Joined? Bolted(IReadOnlyList<Joined> js) => js.FirstOrDefault(j => j.End != null) ?? js.FirstOrDefault();

    /// <summary>A connection's parameters resolved over the members it joins as they are now.</summary>
    public static NodeSpec Spec(IModelView m, Entity node)
    {
        var kind = KindOf(node);
        var section = Bolted(MembersOf(node)) is { } j && m.Get(j.Id) is { } e && MemberAxes.Of(m, e) is { } ax ? ax.Section : Section.Catalogue["IPE 300"];
        return NodeSpec.Resolve(kind, section, ParamsOf(node));
    }

    /// <summary>A new connection, its members' ends set back as it needs; contained in its first member's storey.</summary>
    public static Entity Add(IEdit edit, string kind, IReadOnlyList<Joined> members, NodeParams p, string? role = null, bool own = false)
    {
        if (!Kinds.Contains(kind)) throw new CommandException($"A connection is a column base, an eaves, an apex, a splice or a gable post's, not „{kind}”.");
        if (members.Count == 0 || members.Any(j => edit.Model.Get(j.Id) is not { } e || !MemberAxes.IsSteel(edit.Model, e)))
            throw new CommandException("A connection joins steel members (members, or straight beams and columns of a steel profile).");
        var storey = Spatial.StoreyOf(edit.Model, members[0].Id) ?? Spatial.Storeys(edit.Model).First();
        var attrs = new JsonObject
        {
            ["exportAs"] = "IfcElementAssembly", ["objectType"] = Label(kind), ["kind"] = kind, ["members"] = Json(members), ["params"] = p.ToJson(),
        };
        if (role != null) attrs[RoleKey] = role;
        if (own) attrs["own"] = true;
        var node = new Entity(edit.NewId(), Class, Label(kind), attrs, "USERDEFINED");
        edit.Add(node);
        Spatial.Contain(edit, storey.Id, node.Id);
        Apply(edit, node);
        return node;
    }

    /// <summary>The connection's parameters changed by a patch (each field given; null: back to its default), set on its own (a hall built again keeps them).</summary>
    public static void Update(IEdit edit, Entity node, JsonObject patch)
    {
        var p = NodeParams.Patch(ParamsOf(node), patch);
        var attrs = node.CloneAttrs();
        attrs["params"] = p.ToJson();
        attrs["own"] = true;
        var next = node.With(attrs: attrs);
        edit.Replace(next);
        Apply(edit, next);
    }

    /// <summary>
    /// Writes the setbacks the connection needs on its members' ends: an end plate's or a base plate's thickness, a gable
    /// post's gap. The rest of each end (its cut, its cope) stays as it was.
    /// </summary>
    public static void Apply(IEdit edit, Entity node)
    {
        var spec = Spec(edit.Model, node);
        var ext = KindOf(node) == "post" ? -spec.Gap : -spec.Plate;
        foreach (var j in MembersOf(node)) if (j.End != null) SetExtension(edit, j.Id, j.End, ext);
    }

    /// <summary>The setbacks the connection wrote taken back (it is going).</summary>
    public static void Release(IEdit edit, Entity node)
    {
        foreach (var j in MembersOf(node)) if (j.End != null) SetExtension(edit, j.Id, j.End, 0);
    }

    static void SetExtension(IEdit edit, string id, string which, double ext)
    {
        if (edit.Model.Get(id) is not { } e || !MemberAxes.IsSteel(edit.Model, e)) return;
        var (s, t) = SteelEnd.Of(e);
        var end = which == "start" ? s : t;
        if (Math.Abs(end.Extension - ext) < 1e-12) return;
        end = end with { Extension = ext };
        var attrs = e.CloneAttrs();
        var (ns, nt) = which == "start" ? (end, t) : (s, end);
        if (ns == SteelEnd.Square && nt == SteelEnd.Square && e.Class != SteelModule.Member) attrs.Remove(SteelEnd.Key);
        else attrs[SteelEnd.Key] = SteelEnd.Json2(ns, nt);
        edit.Replace(e.With(attrs: attrs));
    }

    /// <summary>The connections that join a member.</summary>
    public static IEnumerable<Entity> Of(IModelView m, string memberId) =>
        m.OfClass(Class).Where(n => MembersOf(n).Any(j => j.Id == memberId));

    // ------------------------------------------------------------ topology

    /// <summary>
    /// The connections the members' topology asks for, that the model does not have yet: a base under each vertical member;
    /// an eaves where a member is fitted to a vertical one's face, a gable post's where a vertical one is fitted under a
    /// member; an apex where two members are mitred with each other; a splice where two run in line, end to end. The
    /// members looked at are <paramref name="scope"/> (the others are found as what they meet).
    /// </summary>
    public static List<(string Kind, List<Joined> Members)> Find(IModelView m, IEnumerable<Entity> scope)
    {
        var steel = scope.Where(e => MemberAxes.IsSteel(m, e) && MemberAxes.Of(m, e) != null).DistinctBy(e => e.Id).ToList();
        static string Key(string kind, IEnumerable<Joined> js) => kind + "|" + string.Join(",", js.Select(j => j.Id).Order(StringComparer.Ordinal));
        var have = m.OfClass(Class).Select(n => Key(KindOf(n), MembersOf(n))).ToHashSet();
        var o = new List<(string, List<Joined>)>();
        void Take(string kind, List<Joined> js) { if (have.Add(Key(kind, js))) o.Add((kind, js)); }
        bool Vertical(MemberAxis ax) => Math.Abs(ax.U.Z) > 0.99;
        foreach (var e in steel)
        {
            var ax = MemberAxes.Of(m, e)!;
            var vertical = Vertical(ax);
            if (vertical && ax.U.Z > 0) Take("base", [new(e.Id, "start")]);
            var (s, t) = SteelEnd.Of(e);
            foreach (var (end, which) in new[] { (s, "start"), (t, "end") })
            {
                if (end.Ref == null || m.Get(end.Ref) is not { } other || !MemberAxes.IsSteel(m, other) || MemberAxes.Of(m, other) is not { } oax) continue;
                if (end.Cut == "fit")
                {
                    if (vertical && !Vertical(oax)) Take("post", [new(other.Id, null), new(e.Id, which)]);
                    else if (!vertical && Vertical(oax)) Take("eaves", [new(other.Id, null), new(e.Id, which)]);
                }
                else if (end.Cut == "mitre")
                {
                    var (os, ot) = SteelEnd.Of(other);
                    var back = ot is { Cut: "mitre" } && ot.Ref == e.Id ? "end" : os is { Cut: "mitre" } && os.Ref == e.Id ? "start" : null;
                    if (back != null) Take("apex", string.CompareOrdinal(e.Id, other.Id) < 0 ? [new(e.Id, which), new(other.Id, back)] : [new(other.Id, back), new(e.Id, which)]);
                }
            }
        }
        // Splices: two members in line (within half a degree), one's end on the other's start (within 5 mm), cut square there.
        var all = m.OfClass(SteelModule.Member).Concat(m.OfClass(IfcClass.Beam)).Where(e => MemberAxes.IsSteel(m, e)).Select(e => (E: e, Ax: MemberAxes.Of(m, e))).Where(x => x.Ax != null).ToList();
        foreach (var e in steel)
        {
            if (MemberAxes.Of(m, e) is not { } ax || Vertical(ax)) continue;
            foreach (var (f, fax) in all)
            {
                if (f.Id == e.Id || Math.Abs(Vec3.Dot(ax.U, fax!.U)) < Math.Cos(0.5 * Math.PI / 180)) continue;
                foreach (var (pe, we) in new[] { (ax.Start, "start"), (ax.End, "end") })
                    foreach (var (pf, wf) in new[] { (fax.Start, "start"), (fax.End, "end") })
                    {
                        if ((pe - pf).Length > 0.005 || we == wf && Vec3.Dot(ax.U, fax.U) > 0) continue;
                        var (es, et) = SteelEnd.Of(e);
                        var (fs, ft) = SteelEnd.Of(f);
                        if ((we == "start" ? es : et).Cut != "square" || (wf == "start" ? fs : ft).Cut != "square") continue;
                        Take("splice", string.CompareOrdinal(e.Id, f.Id) < 0 ? [new(e.Id, we), new(f.Id, wf)] : [new(f.Id, wf), new(e.Id, we)]);
                    }
            }
        }
        return o;
    }

    // ------------------------------------------------------------ parts

    /// <summary>What a connection is made of: its parts (plates, bolts, anchors), its welds, what could not be, the footing its anchors go into, and the rows of bolts it has.</summary>
    public sealed record Built(List<GeometryPart> Parts, List<Weld> Welds, List<string> Problems, Entity? Footing, NodeSpec Spec, int Rows, List<PlanItem> Plan);

    const string BoltColor = "#3c4550";
    static double Up5(double m) => Math.Ceiling(m * 1000 / 5 - 1e-9) * 5 / 1000;
    static double Up10(double m) => Math.Ceiling(m * 100 - 1e-9) / 100;
    static string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);
    static double Throat(double t) => Math.Max(0.003, Math.Round(0.5 * t * 1000) / 1000);

    sealed class Parts(string color, string? material, NodeSpec spec)
    {
        public readonly List<GeometryPart> List = [];
        public readonly List<PlanItem> Plan = [];
        int bolts, anchors, plates;

        public void Plate(string name, Solid? s, double t, double w, double l, bool plan = true)
        {
            if (s == null) return;
            var (a, b) = (Math.Min(w, l), Math.Max(w, l));
            List.Add(new GeometryPart(name, color, s, material, $"steel-plate:{++plates}")
            {
                Properties = [("Part", "Plate"), ("Size", $"PL {Mm(t)}×{Mm(a)}×{Mm(b)}"), ("Grade", "S235")], PropertySet = "Lapin_SteelPart",
            });
            if (plan) Plan.Add(new PlanItem("outline", true, Polygon2.ConvexHull(s.Vertices.Select(v => v.Xy))));
        }

        public void Bolt(Vec3 headFace, Vec3 dir, double grip)
        {
            if (Fasteners.Bolt(headFace, dir, spec.Bolt, grip) is not { } s) return;
            var size = $"M{Mm(spec.Bolt)}×{Mm(Fasteners.Length(spec.Bolt, grip))}";
            List.Add(new GeometryPart($"Bolt {size}", BoltColor, s, material, $"steel-bolt:{++bolts}")
            {
                Properties = [("Part", "Bolt"), ("Size", size), ("Grade", spec.Grade)], PropertySet = "Lapin_SteelPart",
            });
        }

        public void Anchor(Solid? s, double length, Entity? footing)
        {
            if (s == null) return;
            var size = $"M{Mm(spec.Bolt)}×{Mm(Up5(length))}";
            var props = new List<(string, string)> { ("Part", "Anchor"), ("Size", size), ("Grade", spec.Grade), ("Shape", spec.Anchor) };
            if (footing != null) props.Add(("Footing", footing.Name));
            List.Add(new GeometryPart($"Anchor {size}", BoltColor, s, material, $"steel-anchor:{++anchors}") { Properties = props, PropertySet = "Lapin_SteelPart" });
        }

        public void AnchorPlate(Solid? s, double t, double w)
        {
            if (s == null) return;
            List.Add(new GeometryPart("Anchor plate", color, s, material, $"steel-anchorplate:{anchors}")
            {
                Properties = [("Part", "Anchor plate"), ("Size", $"PL {Mm(t)}×{Mm(w)}×{Mm(w)}"), ("Grade", "S235")], PropertySet = "Lapin_SteelPart",
            });
        }
    }

    /// <summary>A member's axis and body as it is now (its ends with the setbacks the connection wrote).</summary>
    static (MemberAxis Ax, SteelBody.Built Body, Entity E)? Member(IModelView m, string id)
    {
        if (m.Get(id) is not { } e || MemberAxes.Of(m, e) is not { } ax) return null;
        var (s, t) = SteelEnd.Of(e);
        return (ax, SteelBody.Build(m, ax, s, t, null), e);
    }

    /// <summary>The connection's parts from its kind, its parameters and its members as they are.</summary>
    public static Built Build(IModelView m, Entity node)
    {
        var kind = KindOf(node);
        var js = MembersOf(node);
        var spec = Spec(m, node);
        var problems = new List<string>();
        var welds = new List<Weld>();
        var first = Bolted(js) is { } bj ? m.Get(bj.Id) : null;
        var material = first != null ? m.RelatingOf(first.Id, IfcClass.RelDefinesByType)?.Str("material") : null;
        var parts = new Parts(material != null ? Materials.ColorOf(m, material) : "#8a96a3", material, spec);
        Entity? footing = null;
        var rows = 0;
        var missing = js.Where(j => m.Get(j.Id) == null).ToList();
        if (missing.Count > 0 || js.Count == 0)
            problems.Add("A member the connection joined is gone: it has no parts.");
        else
            switch (kind)
            {
                case "base": footing = Base(m, js[0], spec, parts, welds, problems); break;
                case "eaves": rows = Eaves(m, js, spec, parts, welds, problems); break;
                case "apex" or "splice": rows = EndToEnd(m, js, spec, parts, welds, problems); break;
                case "post": rows = Post(m, js, spec, parts, welds, problems); break;
            }
        return new Built(parts.List, welds, problems, footing, spec, rows, parts.Plan);
    }

    // ------------------------------------------------------------ column base

    /// <summary>
    /// A column base: the column set back by the plate; the plate under it, centred, its anchors through it — pinned: two
    /// either side of the web, or four inside the flanges; fixed: in rows outside the flanges (and two inside for six),
    /// with ribs in the web's plane from the flanges to the plate's edge. The anchors go down the grout and their length
    /// into the concrete: into a footing of the structure under the column (no deeper than its bottom less 50 mm), else
    /// from the grout's underside.
    /// </summary>
    static Entity? Base(IModelView m, Joined j, NodeSpec spec, Parts parts, List<Weld> welds, List<string> problems)
    {
        if (Member(m, j.Id) is not var (ax, _, col)) return null;
        if (Math.Abs(ax.U.Z) < 0.99) { problems.Add("A column base is under a vertical member."); return null; }
        var s = ax.Section;
        double h = s.Depth, b = s.Width, tf = s.Flange > 0 ? s.Flange : s.Web, tw = s.Web > 0 ? s.Web : 0.01;
        var (tp, d, e) = (spec.Plate, spec.Bolt, spec.Edge);
        var fixedBase = spec.Base == "fixed";
        var gx = Math.Max(spec.Gauge / 2, tw / 2 + 0.75 * d + 0.012);
        var inside = Math.Max(0.04, h / 2 - tf - (d + 0.015));
        var outside = h / 2 + Up5(d + 0.03);
        var at = new List<Vec2>();
        switch (fixedBase, spec.Anchors)
        {
            case (false, 2): at.AddRange([new(-gx, 0), new(gx, 0)]); break;
            case (false, _): at.AddRange([new(-gx, -inside), new(gx, -inside), new(-gx, inside), new(gx, inside)]); break;
            case (true, 8):
                foreach (var y in new[] { outside, outside + spec.Pitch }) at.AddRange([new(-gx, -y), new(gx, -y), new(-gx, y), new(gx, y)]);
                break;
            default:
                at.AddRange([new(-gx, -outside), new(gx, -outside), new(-gx, outside), new(gx, outside)]);
                if (spec.Anchors == 6) at.AddRange([new(-gx, 0), new(gx, 0)]);
                break;
        }
        var length = Up10(fixedBase ? 2 * (at.Max(p => Math.Abs(p.Y)) + e) : Math.Max(h + 0.04, 2 * (at.Max(p => Math.Abs(p.Y)) + e)));
        var width = Up10(Math.Max(b + 0.04, 2 * (gx + e)));
        // The column's start is the plate's underside; the plate from there up its thickness.
        Vec3 P(double x, double y, double st) => ax.At(new Vec2(x, y), st);
        var plate = Prism.Extrude([P(-width / 2, -length / 2, 0), P(width / 2, -length / 2, 0), P(width / 2, length / 2, 0), P(-width / 2, length / 2, 0)], null, ax.U * tp, "plate");
        parts.Plate("Base plate", plate, tp, width, length);
        var outline = s.Outline();
        welds.Add(new Weld("Column to base plate", Throat(tf), Polygon2.Perimeter(outline)));
        if (fixedBase && spec.Stiffeners && length / 2 - h / 2 > 0.03)
        {
            var run = length / 2 - h / 2 - 0.01;
            var hs = Math.Min(0.3, Math.Max(0.1, 1.5 * run));
            var ts = Math.Max(0.01, Up5(tw * 1.2));
            foreach (var side in new[] { 1.0, -1.0 })
            {
                IReadOnlyList<Vec2> yz = [new(h / 2, tp), new(h / 2 + run, tp), new(h / 2 + run, tp + 0.03), new(h / 2, tp + hs)];
                var rib = Prism.Extrude(yz.Select(q => P(-ts / 2, side * q.X, q.Y)).ToList(), null, ax.Frame.V * ts, "rib");
                parts.Plate("Stiffener", rib, ts, run, hs, plan: false);
                welds.Add(new Weld("Base stiffener", Throat(ts), 2 * (run + hs)));
            }
        }
        // The footing under the column: the highest whose box holds the column's point, its top no higher than the base.
        var z0 = ax.Start.Z;
        var where = ax.Start.Xy;
        Entity? footing = null;
        (Vec3 Min, Vec3 Max)? box = null;
        foreach (var f in m.OfClass(IfcClass.Footing))
        {
            var env = PadFootings.Envelope(f, m) ?? StripFootings.Envelope(f, m);
            if (env == null || Measure.Bounds(env) is not { } bb) continue;
            if (where.X < bb.Min.X - 1e-6 || where.X > bb.Max.X + 1e-6 || where.Y < bb.Min.Y - 1e-6 || where.Y > bb.Max.Y + 1e-6) continue;
            if (bb.Max.Z > z0 + 0.01 || bb.Max.Z < z0 - 3) continue;
            if (box == null || bb.Max.Z > box.Value.Max.Z) { box = bb; footing = f; }
        }
        var concrete = box?.Max.Z ?? z0 - spec.Grout;
        var bottom = concrete - spec.AnchorLength;
        if (box is { } fb && bottom < fb.Min.Z + 0.05)
        {
            bottom = fb.Min.Z + 0.05;
            problems.Add("The anchors are longer than the footing is deep: they stop 50 mm above its bottom.");
        }
        var hook = spec.Anchor == "hooked" ? Up10(Math.Max(0.1, 5 * d)) : 0;
        var plateSide = Up10(Math.Max(0.06, 4 * d));
        var plateT = Math.Max(0.01, Up5(0.5 * d));
        foreach (var p in at)
        {
            var w = P(p.X, p.Y, 0);
            var outward = (w.Xy - where).Length > 1e-6 ? (w.Xy - where) : ax.Frame.V.Xy;
            var (rod, len) = Fasteners.Anchor(w.Xy, z0 + tp, bottom, d, hook, outward);
            parts.Anchor(rod, len, footing);
            if (spec.Anchor == "plate")
                parts.AnchorPlate(Prism.Box(new Vec3(w.X, w.Y, bottom + plateT / 2), plateSide, plateSide, plateT, "anchorplate"), plateT, plateSide);
        }
        return footing;
    }

    // ------------------------------------------------------------ end plates

    /// <summary>
    /// An end plate's frame on a member's cut plane: the plane's point on the section's centre line, its up (the section's
    /// up laid on the plane), across, and how far up the member's top and its bottom (its haunch's, when it has one) are,
    /// with the flanges' thicknesses there.
    /// </summary>
    sealed record PlateFrame(Vec3 O, Vec3 Across, Vec3 Up, Vec3 N, double UTop, double UBot, double TfTop, double TfBot, double Width)
    {
        public Vec3 At(double a, double u) => O + Across * a + Up * u;
    }

    static PlateFrame FrameOn(MemberAxis ax, CutPlane plane, Haunch? haunch)
    {
        var u = ax.U;
        var n = plane.Normal;
        double Station(Vec2 p) => Vec3.Dot(n, plane.Point - ax.At(p, 0)) / Vec3.Dot(n, u);
        Vec3 On(Vec2 p) => ax.At(p, Station(p));
        var (outline, _) = ax.Profile;
        double y0 = outline.Min(p => p.Y), y1 = outline.Max(p => p.Y);
        var (v, w) = ax.Frame;
        var up = w - n * Vec3.Dot(n, w);
        up = up.Length > 1e-6 ? up.Normalized() : v;
        var across = Vec3.Cross(n, up).Normalized();
        var o = On(default);
        var tf = ax.Section.Flange > 0 ? ax.Section.Flange : ax.Section.Web;
        var depth = haunch?.Depth ?? 0;
        var tfb = haunch != null ? haunch.Plates(ax.Section).Flange : tf;
        return new PlateFrame(o, across, up, n, Vec3.Dot(up, On(new Vec2(0, y1)) - o), Vec3.Dot(up, On(new Vec2(0, y0 - depth)) - o), tf, tfb, ax.Section.Width);
    }

    /// <summary>The rows of bolts up the plate: one in its extension above the member, then <c>rows</c> between the flanges (as many as fit two pitches apart when not given).</summary>
    static (List<double> Us, int Inner) Rows(PlateFrame f, NodeSpec spec)
    {
        var o = new List<double>();
        if (spec.Extension >= 2 * spec.Edge - 1e-9) o.Add(f.UTop + spec.Extension / 2);
        var hi = f.UTop - f.TfTop - spec.Edge;
        var lo = f.UBot + f.TfBot + spec.Edge;
        var n = spec.Rows > 0 ? spec.Rows : Math.Max(2, (int)Math.Floor((hi - lo) / (2 * spec.Pitch) + 1e-9) + 1);
        if (hi - lo < 1e-3) { o.Add((hi + lo) / 2); return (o, 1); }
        if (n == 1) o.Add((hi + lo) / 2);
        else for (var k = 0; k < n; k++) o.Add(hi - (hi - lo) * k / (n - 1));
        return (o, n);
    }

    static List<double> Cols(NodeSpec spec) =>
        spec.Cols == 4 ? [-spec.Gauge / 2 - spec.Pitch, -spec.Gauge / 2, spec.Gauge / 2, spec.Gauge / 2 + spec.Pitch] : [-spec.Gauge / 2, spec.Gauge / 2];

    /// <summary>An end plate on a frame, from its bottom below the member (its haunch) to its top above it, as wide as the member's flange or its bolts need.</summary>
    static (Solid? Solid, double Width, double Height) EndPlate(PlateFrame f, NodeSpec spec, List<double> cols)
    {
        var width = Up10(Math.Max(f.Width, 2 * cols.Max(Math.Abs) + 2 * spec.Edge));
        var (u0, u1) = (f.UBot - 0.015, f.UTop + spec.Extension);
        var s = Prism.Extrude([f.At(-width / 2, u0), f.At(width / 2, u0), f.At(width / 2, u1), f.At(-width / 2, u1)], null, f.N * spec.Plate, "plate");
        return (s, width, u1 - u0);
    }

    static (Haunch? Start, Haunch? End) Haunches(Entity e) => e.Class == SteelModule.Member ? Haunch.Of(e) : (null, null);

    /// <summary>The welds of a member onto its end plate: round its section, and its haunch's web and flange.</summary>
    static void EndWelds(MemberAxis ax, Haunch? h, List<Weld> welds, string where)
    {
        var s = ax.Section;
        welds.Add(new Weld(where, Throat(s.Web > 0 ? s.Web : 0.01), Polygon2.Perimeter(s.Outline())));
        if (h != null) { var (w, _, b) = h.Plates(s); welds.Add(new Weld("Haunch to end plate", Throat(w), 2 * h.Depth + 2 * b)); }
    }

    /// <summary>
    /// An eaves: the rafter's extended end plate on the column's face, bolted through the column's flange (the heads inside
    /// it, the nuts on the plate); the column's stiffeners, each side of its web, in line with the rafter's top flange and
    /// its haunch's (its own bottom flange's) flange.
    /// </summary>
    static int Eaves(IModelView m, List<Joined> js, NodeSpec spec, Parts parts, List<Weld> welds, List<string> problems)
    {
        var (main, raf) = (js.FirstOrDefault(j => j.End == null), js.FirstOrDefault(j => j.End != null));
        if (main == null || raf == null || Member(m, main.Id) is not var (cax, cb, _) || Member(m, raf.Id) is not var (rax, rb, re))
        { problems.Add("An eaves joins a column and a rafter fitted to it."); return 0; }
        var atEnd = raf.End == "end";
        var haunch = atEnd ? Haunches(re).End : Haunches(re).Start;
        var f = FrameOn(rax, atEnd ? rb.End : rb.Start, haunch);
        var cols = Cols(spec);
        var (plate, width, height) = EndPlate(f, spec, cols);
        parts.Plate("End plate", plate, spec.Plate, width, height);
        EndWelds(rax, haunch, welds, "Rafter to end plate");
        var (us, inner) = Rows(f, spec);
        var tfc = cax.Section.Flange > 0 ? cax.Section.Flange : cax.Section.Web;
        foreach (var u in us)
            foreach (var a in cols)
                parts.Bolt(f.At(a, u) + f.N * (spec.Plate + tfc), -f.N, spec.Plate + tfc);
        if (spec.Stiffeners && Math.Abs(cax.U.Z) > 0.99 && cax.Section.Kind == "I")
        {
            var s = cax.Section;
            var ts = Math.Max(0.008, Up5(f.TfTop));
            double h = s.Depth, b = s.Width, tf = s.Flange, tw = s.Web;
            // As high as the column's top lets (a rafter's top flange level with it: the stiffener under the top).
            var topSt = cb.Solid is { } cs && Measure.Bounds(cs) is { } cbb ? cbb.Max.Z - cax.Start.Z : cax.Length;
            foreach (var z in new[] { f.At(0, f.UTop - f.TfTop / 2).Z, f.At(0, f.UBot + f.TfBot / 2).Z })
            {
                var st = Math.Min(z - cax.Start.Z, topSt - ts / 2);
                if (st < ts) continue;
                foreach (var side in new[] { 1.0, -1.0 })
                {
                    IReadOnlyList<Vec2> q = [new(side * tw / 2, -h / 2 + tf), new(side * b / 2, -h / 2 + tf), new(side * b / 2, h / 2 - tf), new(side * tw / 2, h / 2 - tf)];
                    parts.Plate("Stiffener", Prism.Extrude(q.Select(p => cax.At(p, st - ts / 2)).ToList(), null, cax.U * ts, "stiffener"), ts, (b - tw) / 2, h - 2 * tf, plan: false);
                    welds.Add(new Weld("Column stiffener", Throat(ts), 2 * (h - 2 * tf + b - tw)));
                }
            }
        }
        return inner;
    }

    /// <summary>
    /// An apex or a splice: each member's end plate on its cut, the two bolted together through both (the heads on the
    /// second's plate, the nuts on the first's).
    /// </summary>
    static int EndToEnd(IModelView m, List<Joined> js, NodeSpec spec, Parts parts, List<Weld> welds, List<string> problems)
    {
        if (js.Count < 2 || js[0].End == null || js[1].End == null || Member(m, js[0].Id) is not var (ax0, b0, e0) || Member(m, js[1].Id) is not var (ax1, b1, e1))
        { problems.Add("An apex or a splice joins two members, end to end."); return 0; }
        var cols = Cols(spec);
        PlateFrame? first = null;
        var inner = 0;
        foreach (var (j, ax, built, e) in new[] { (js[0], ax0, b0, e0), (js[1], ax1, b1, e1) })
        {
            var atEnd = j.End == "end";
            var haunch = atEnd ? Haunches(e).End : Haunches(e).Start;
            var f = FrameOn(ax, atEnd ? built.End : built.Start, haunch);
            var (plate, width, height) = EndPlate(f, spec, cols);
            parts.Plate("End plate", plate, spec.Plate, width, height);
            EndWelds(ax, haunch, welds, "Member to end plate");
            first ??= f;
        }
        var (us, n) = Rows(first!, spec);
        inner = n;
        foreach (var u in us)
            foreach (var a in cols)
                parts.Bolt(first!.At(a, u) + first.N * (2 * spec.Plate), -first.N, 2 * spec.Plate);
        return inner;
    }

    /// <summary>
    /// A gable post under its rafter: the post's top set back by the gap; a fin plate welded under the rafter, beside the
    /// post's web, bolted through it in a column of bolts (slotted holes in the fin, so the rafter may move).
    /// </summary>
    static int Post(IModelView m, List<Joined> js, NodeSpec spec, Parts parts, List<Weld> welds, List<string> problems)
    {
        var post = js.FirstOrDefault(j => j.End != null);
        if (post == null || Member(m, post.Id) is not var (ax, _, e)) { problems.Add("A gable post's connection joins a post and the rafter over it."); return 0; }
        var (s, t) = SteelEnd.Of(e);
        var atEnd = post.End == "end";
        var rule = (atEnd ? t : s) with { Extension = 0 };
        var plane = SteelBody.PlaneOf(m, ax, rule, atEnd, []);
        var top = Vec3.Dot(plane.Normal, plane.Point - ax.At(default, 0)) / Vec3.Dot(plane.Normal, ax.U);
        var sec = ax.Section;
        var tw = sec.Web > 0 ? sec.Web : 0.01;
        var rows = spec.Rows > 0 ? spec.Rows : 2;
        var fin = spec.Plate;
        var wf = Up10(Math.Max(2 * spec.Edge, 0.06));
        var hf = spec.Gap + 2 * spec.Edge + spec.Pitch * (rows - 1);
        if (!atEnd) { problems.Add("A gable post's top is its end."); return 0; }
        IReadOnlyList<Vec2> q = [new(tw / 2, -wf / 2), new(tw / 2 + fin, -wf / 2), new(tw / 2 + fin, wf / 2), new(tw / 2, wf / 2)];
        parts.Plate("Fin plate", Prism.Extrude(q.Select(p => ax.At(p, top - hf)).ToList(), null, ax.U * hf, "fin"), fin, wf, hf);
        welds.Add(new Weld("Fin plate to rafter", Throat(fin), 2 * wf));
        for (var k = 0; k < rows; k++)
            parts.Bolt(ax.At(new Vec2(tw / 2 + fin, 0), top - spec.Gap - spec.Edge - k * spec.Pitch), -ax.Frame.V, fin + tw);
        return rows;
    }

    // ------------------------------------------------------------ info

    /// <summary>The connection as the properties panel shows it: kind, members, parameters (given and resolved), parts counted, welds, footing, problems.</summary>
    public static JsonObject Info(IModelView m, Entity node)
    {
        var b = Build(m, node);
        var spec = b.Spec.ToJson();
        spec["rows"] = b.Rows;
        var parts = new JsonArray();
        foreach (var g in b.Parts.Select(p => p.Properties ?? []).GroupBy(p => (P(p, "Part"), P(p, "Size"), P(p, "Grade"))).OrderBy(g => g.Key.Item1, StringComparer.Ordinal))
            parts.Add((JsonNode)new JsonObject { ["part"] = g.Key.Item1, ["size"] = g.Key.Item2, ["grade"] = g.Key.Item3, ["count"] = g.Count() });
        var welds = new JsonArray();
        foreach (var g in b.Welds.GroupBy(w => w.Throat).OrderBy(g => g.Key))
            welds.Add((JsonNode)new JsonObject { ["throat"] = g.Key, ["length"] = Math.Round(g.Sum(w => w.Length), 3), ["count"] = g.Count() });
        var o = new JsonObject
        {
            ["kind"] = KindOf(node), ["label"] = Label(KindOf(node)),
            ["members"] = new JsonArray([.. MembersOf(node).Select(j => (JsonNode)new JsonObject { ["id"] = j.Id, ["end"] = j.End, ["name"] = m.Get(j.Id)?.Name })]),
            ["params"] = ParamsOf(node).ToJson(), ["spec"] = spec, ["parts"] = parts, ["welds"] = welds,
            ["weight"] = Math.Round(b.Parts.Sum(p => Measure.Volume(p.Solid)) * SteelModule.SteelDensity, 2),
            ["own"] = node.Bool("own"),
            ["problems"] = new JsonArray([.. b.Problems.Select(p => (JsonNode)p)]),
        };
        if (b.Footing != null) o["footing"] = new JsonObject { ["id"] = b.Footing.Id, ["name"] = b.Footing.Name };
        return o;
    }

    public static string P(IReadOnlyList<(string Name, string Value)> props, string name) => props.FirstOrDefault(x => x.Name == name).Value ?? "";

    /// <summary>The connection's geometry: its parts, its plates' outlines in plan, what could not be.</summary>
    public static ElementGeometry Geometry(Entity node, IModelView m)
    {
        var b = Build(m, node);
        return new ElementGeometry(b.Parts, b.Plan, null, b.Problems.Count > 0 ? b.Problems : null);
    }
}
