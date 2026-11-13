using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Balconies;

/// <summary>
/// Where a balcony stands: its frame in plan (u along its wall's face, v out from it, the face at v = 0), the level of
/// its floor (its storey plus its base offset) and the wall it hangs on (its thickness, its base, its height). A loggia
/// goes into the wall: its v is negative behind the face.
/// </summary>
public sealed record BalconyFrame(Vec2 At, Vec2 U, Vec2 V, double Top, double Storey, Entity? Wall, double WallThickness, double WallBase, double WallHeight)
{
    public Vec2 Plan(Vec2 local) => At + U * local.X + V * local.Y;
    public Vec3 World(Vec3 local) => Plan(new Vec2(local.X, local.Y)).At(local.Z);
    public Vec2 Local(Vec2 world) { var d = world - At; return new Vec2(Vec2.Dot(d, U), Vec2.Dot(d, V)); }
    public double Rotation => Math.Atan2(U.Y, U.X) * 180 / Math.PI;
}

/// <summary>
/// A balcony built in its own frame (<see cref="BalconyFrame"/>): its outline with what each edge is (against the
/// building, a side, the front), the slab under its finish (the finish falling to the front), the soffit, the drip or
/// nosing, the thermal break, the railing (the railings' designs, <see cref="Railings.Solids"/>, or a solid parapet),
/// the posts, brackets or tie rods, and a loggia's side walls and ceiling. Heights are absolute; u and v local.
/// </summary>
public sealed class BalconyLayout
{
    public required string Kind { get; init; }
    public required JsonObject Fields { get; init; }
    public List<Vec2> Outline { get; } = [];
    /// <summary>For each edge (point i to i + 1): "wall", "side" or "front".</summary>
    public List<string> Roles { get; } = [];
    public List<List<Vec2>> Rails { get; } = [];
    public List<Vec2> Posts { get; } = [];
    public List<double> Brackets { get; } = [];
    public List<Vec2> Hangers { get; } = [];
    public List<string> Notes { get; } = [];
    /// <summary>The parts, local: key, name, material (a name), colour, solid.</summary>
    public List<(string Key, string Name, string? Material, string Color, Solid Solid)> Parts { get; } = [];
    public double Top { get; set; }
    public double SlabTop { get; set; }
    public double SlabBottomAtBack { get; set; }
    public double PostBottom { get; set; }
    public bool HasSlab { get; set; } = true;
    public double Num(string k) => Json.Number(Fields[k]) ?? 0;
    public string Str(string k) => Fields[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    public bool Bool(string k) => Fields[k] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    /// <summary>The walking surface's height at a point: its floor at the back, falling to the front.</summary>
    public Func<Vec2, double> Walk { get; set; } = _ => 0;
    public double Area => Math.Abs(Polygon2.SignedArea(Outline));
    public double RailLength => Rails.Sum(r => r.Zip(r.Skip(1), (a, b) => (b - a).Length).Sum());
    public int SupportCount => Posts.Count + Brackets.Count + Hangers.Count;
}

public static class BalconyBuilder
{
    // ------------------------------------------------------------ the frame

    /// <summary>The balcony's frame: on its wall at <c>along</c> on its <c>side</c>; else at its <c>at</c> turned by <c>rotation</c>; a drawn one by its first edge.</summary>
    public static BalconyFrame? Frame(IModelView m, Entity e)
    {
        var storey = Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
        var top = storey + e.Num("baseOffset");
        if (m.Get(e.Str("wallId") ?? "") is { Class: IfcClass.Wall } wall && Walls.Section(m, wall).Count > 0)
        {
            var along = e.Num("along");
            var f = Openings.FrameOf(m, wall, along);
            var side = e.Num("side", 1) < 0 ? -1 : 1;
            var face = side > 0 ? f.YMax : f.YMin;
            var at = f.Start + f.Dir * along + f.Left * face;
            return new BalconyFrame(at, f.Dir * side, f.Left * side, top, storey, wall, f.YMax - f.YMin, storey + wall.Num("baseOffset"), Walls.HeightOf(m, wall));
        }
        if (Points(e) is { Count: >= 3 } pts)
        {
            var (a, b) = (pts[0], pts[1]);
            var u = (b - a).Normalized();
            var v = u.Perp;
            var centre = new Vec2(pts.Average(p => p.X), pts.Average(p => p.Y));
            if (Vec2.Dot(centre - a, v) < 0) (u, v) = (-u, -v);
            return new BalconyFrame((a + b) * 0.5, u, v, top, storey, null, 0, storey, Walls.DefaultHeight);
        }
        if (e.Attrs["at"] is JsonArray { Count: >= 2 } atj)
        {
            var at = new Vec2(Json.Number(atj[0]) ?? 0, Json.Number(atj[1]) ?? 0);
            var r = e.Num("rotation") * Math.PI / 180;
            var u = new Vec2(Math.Cos(r), Math.Sin(r));
            if (e.Bool("flip")) u = -u;
            return new BalconyFrame(at, u, u.Perp, top, storey, null, 0, storey, Walls.DefaultHeight);
        }
        return null;
    }

    public static List<Vec2> Points(Entity e) =>
        (e.Attrs["contour"] as JsonArray)?.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList() ?? [];

    // ------------------------------------------------------------ the outline

    sealed class Ring
    {
        public readonly List<Vec2> P = [];
        public readonly List<string> R = [];
        public void Add(double u, double v, string role) { P.Add(new Vec2(u, v)); R.Add(role); }
        public void Arc(Vec2 c, double radius, double from, double to, string role)
        {
            var n = Math.Max(2, Chords.Count(radius, Math.Abs(to - from)));
            for (var i = 0; i < n; i++) { var t = from + (to - from) * i / n; Add(c.X + radius * Math.Cos(t), c.Y + radius * Math.Sin(t), role); }
        }
    }

    /// <summary>The outline in the frame, counter-clockwise, with each edge's role.</summary>
    static (List<Vec2> P, List<string> R) Outline(BalconyLayout l, BalconyFrame f, Entity e)
    {
        var w = Math.Max(0.3, l.Num("width"));
        var d = Math.Max(0, l.Num("depth"));
        var ring = new Ring();
        switch (l.Kind)
        {
            case "loggia":
            {
                var p = Math.Max(0, l.Num("projection"));
                ring.Add(-w / 2, -d, "wall"); ring.Add(w / 2, -d, "wall"); ring.Add(w / 2, p, "front"); ring.Add(-w / 2, p, "wall");
                // The sides are its own walls: no railing; the front, at the face (or the slab's edge past it).
                (ring.R[1], ring.R[2], ring.R[3]) = ("wall", "front", "wall");
                break;
            }
            case "mezzanine":
            {
                var depth = Math.Max(0.05, d);
                ring.Add(-w / 2, -depth, "wall"); ring.Add(w / 2, -depth, "wall"); ring.Add(w / 2, 0, "front"); ring.Add(-w / 2, 0, "wall");
                break;
            }
            case "corner":
            {
                var r = Math.Max(0.3, l.Num("returnLength"));
                d = Math.Max(0.3, d);
                ring.Add(-w / 2, 0, "wall"); ring.Add(w / 2, 0, "wall"); ring.Add(w / 2, -r, "side"); ring.Add(w / 2 + d, -r, "front"); ring.Add(w / 2 + d, d, "front"); ring.Add(-w / 2, d, "side");
                if (l.Str("cornerSide") == "left") return Mirror(ring);
                break;
            }
            default:
            {
                if (Points(e) is { Count: >= 3 } pts)
                {
                    var local = pts.Select(f.Local).ToList();
                    var ccw = Polygon2.SignedArea(local) > 0;
                    if (!ccw) local.Reverse();
                    // The first edge drawn lies against the building; reversed, it is the one that ends at the first point.
                    for (var i = 0; i < local.Count; i++) ring.Add(local[i].X, local[i].Y, ccw ? (i == 0 ? "wall" : "front") : (i == local.Count - 2 ? "wall" : "front"));
                    break;
                }
                d = Math.Max(0.05, d);
                switch (l.Str("outline"))
                {
                    case "rounded":
                    {
                        var r = Math.Clamp(l.Num("cornerRadius"), 0.05, Math.Min(w / 2, d) - 1e-3);
                        ring.Add(-w / 2, 0, "wall"); ring.Add(w / 2, 0, "side");
                        ring.Add(w / 2, d - r, "front");
                        ring.Arc(new Vec2(w / 2 - r, d - r), r, 0, Math.PI / 2, "front");
                        ring.Add(w / 2 - r, d, "front");
                        ring.Arc(new Vec2(-w / 2 + r, d - r), r, Math.PI / 2, Math.PI, "front");
                        // The arc's end is where the side starts.
                        ring.Add(-w / 2, d - r, "side");
                        break;
                    }
                    case "curved":
                    {
                        var s = Math.Clamp(l.Num("bulge"), 0.05, Math.Max(0.06, d - 0.1));
                        var radius = (w * w / 4 + s * s) / (2 * s);
                        var c = new Vec2(0, d - radius);
                        var a0 = Math.Atan2(d - s - c.Y, w / 2);
                        ring.Add(-w / 2, 0, "wall"); ring.Add(w / 2, 0, "side");
                        ring.Arc(c, radius, a0, Math.PI - a0, "front");
                        ring.Add(-w / 2, d - s, "side");
                        break;
                    }
                    default:
                        ring.Add(-w / 2, 0, "wall"); ring.Add(w / 2, 0, "side"); ring.Add(w / 2, d, "front"); ring.Add(-w / 2, d, "side");
                        break;
                }
                break;
            }
        }
        return Clean(ring.P, ring.R);
    }

    /// <summary>Mirrored across v (a corner on the left): the points reversed so it stays counter-clockwise, each edge keeping its role.</summary>
    static (List<Vec2>, List<string>) Mirror(Ring ring)
    {
        var n = ring.P.Count;
        var p = new List<Vec2>();
        var r = new List<string>();
        for (var j = 0; j < n; j++)
        {
            var src = n - 1 - j;
            p.Add(new Vec2(-ring.P[src].X, ring.P[src].Y));
            r.Add(ring.R[((n - 2 - j) % n + n) % n]);
        }
        return Clean(p, r);
    }

    static (List<Vec2>, List<string>) Clean(List<Vec2> p, List<string> r)
    {
        var op = new List<Vec2>();
        var or = new List<string>();
        for (var i = 0; i < p.Count; i++)
        {
            if (op.Count > 0 && (p[i] - op[^1]).Length < 1e-6) { or[^1] = r[i]; continue; }
            op.Add(p[i]); or.Add(r[i]);
        }
        if (op.Count > 2 && (op[0] - op[^1]).Length < 1e-6) { op.RemoveAt(op.Count - 1); or.RemoveAt(or.Count - 1); }
        return (op, or);
    }

    // ------------------------------------------------------------ the build

    static string Rgb(IModelView m, string? material, string fallback) =>
        material is { Length: > 0 } && Materials.Find(m, material) is { } mat ? Materials.ColorOf(m, mat.Id) : fallback;

    /// <summary>The balcony built in its frame, from its own fields.</summary>
    public static BalconyLayout? Build(IModelView m, Entity e) => Frame(m, e) is { } f ? Build(m, e, f, BalconySpec.Read(e)) : null;

    public static BalconyLayout Build(IModelView m, Entity e, BalconyFrame f, JsonObject fields)
    {
        var kind = (string?)fields["kind"] is { } k && BalconySpec.Kinds.Contains(k) ? k : "cantilever";
        var l = new BalconyLayout { Kind = kind, Fields = fields, Top = f.Top };
        var (pts, roles) = Outline(l, f, e);
        l.Outline.AddRange(pts);
        l.Roles.AddRange(roles);
        if (pts.Count < 3) { l.Notes.Add("The outline needs three points at least."); return l; }

        double vMin = pts.Min(p => p.Y), vMax = pts.Max(p => p.Y), span = Math.Max(1e-3, vMax - vMin);
        var thickness = Math.Max(0.04, l.Num("thickness"));
        var finish = Math.Max(0, l.Num("finishThickness"));
        var fall = Math.Max(0, l.Num("fall")) / 100;
        if (finish > 0 && finish - fall * span < 0.005)
        {
            var fit = Math.Max(0, (finish - 0.005) / span);
            l.Notes.Add($"The finish is too thin for a fall of {fall * 100:0.#} %: it falls {fit * 100:0.#} %.");
            fall = fit;
        }
        var top = f.Top;
        var slabTopBack = top - finish;
        double SlabTop(Vec2 p) => finish > 0 ? slabTopBack : top - fall * (p.Y - vMin);
        l.Walk = p => top - fall * (p.Y - vMin);
        var tapered = l.Str("edge") == "tapered";
        var edgeThickness = Math.Clamp(l.Num("edgeThickness"), 0.03, thickness);
        double Bottom(Vec2 p) => slabTopBack - (tapered ? thickness + (edgeThickness - thickness) * (p.Y - vMin) / span : thickness);
        l.SlabTop = slabTopBack;
        l.SlabBottomAtBack = slabTopBack - thickness;
        l.HasSlab = kind != "mezzanine" || l.Bool("gallerySlab");

        var slabMat = l.Str("material");
        var steel = l.Str("supportMaterial");
        var railColor = (string?)fields["railColor"] is { Length: 7 } rc ? rc : "#3a3d42";

        // The slab, less the thermal break along the building where it has one.
        if (l.HasSlab)
        {
            var breakWidth = l.Bool("thermalBreak") && kind is not ("mezzanine" or "loggia") ? 0.08 : 0;
            var slabOutline = breakWidth > 0 ? Polygon2.Offset(pts, roles.Select(r => r == "wall" ? -breakWidth : 0.0).ToList()).ToList() : pts;
            if (Slab(slabOutline, SlabTop, Bottom, "slab") is { } slab) l.Parts.Add(("balcony-slab", "Slab", slabMat, Rgb(m, slabMat, "#b8b4ac"), slab));
            if (breakWidth > 0)
                for (var i = 0; i < pts.Count; i++)
                {
                    if (roles[i] != "wall") continue;
                    var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
                    var n = (b - a).Normalized().Perp * breakWidth;
                    if (Slab([a, b, b + n, a + n], SlabTop, Bottom, "break") is { } tb) l.Parts.Add(("balcony-break", "Thermal break", null, "#e3b23c", tb));
                }
            if (finish > 0)
            {
                // A nosing: the finish runs past the front edges.
                var finishOutline = l.Str("edge") == "nosing" ? Polygon2.Offset(pts, roles.Select(r => r == "wall" ? 0.0 : 0.03).ToList()).ToList() : pts;
                var fm = l.Str("finishMaterial");
                if (Slab(finishOutline, p => top - fall * (p.Y - vMin), _ => slabTopBack, "finish") is { } fs) l.Parts.Add(("balcony-finish", "Finish", fm, Rgb(m, fm, "#d8cfc0"), fs));
            }
            var soffit = Math.Max(0, l.Num("soffitThickness"));
            if (soffit > 0 && kind != "loggia")
            {
                var sm = l.Str("soffitMaterial");
                if (Slab(pts, Bottom, p => Bottom(p) - soffit, "soffit") is { } ss) l.Parts.Add(("balcony-soffit", "Soffit", sm, Rgb(m, sm, "#efece6"), ss));
            }
            if (l.Str("edge") == "drip")
            {
                var drips = new List<Solid>();
                for (var i = 0; i < pts.Count; i++)
                {
                    if (roles[i] == "wall") continue;
                    var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
                    var inward = (b - a).Normalized().Perp * 0.035;
                    var (za, zb) = (Bottom(a) - soffit, Bottom(b) - soffit);
                    if (Prism.SweepBox((a + inward).At(za - 0.012), (b + inward).At(zb - 0.012), 0.012, 0.024, "drip") is { } s) drips.Add(s);
                }
                if (drips.Count > 0) l.Parts.Add(("balcony-drip", "Drip", "Aluminium", Rgb(m, "Aluminium", "#a9adb2"), Openings.Combine(drips, "drip")));
            }
        }

        // The railing along the sides and front (a loggia's and a gallery's at its front only).
        var railStyle = l.Str("railStyle");
        var allSides = BalconySpec.Kinds.Contains(kind) && kind is not ("loggia" or "mezzanine") && l.Str("railSides") != "front";
        bool Railed(string r) => r == "front" || r == "side" && allSides;
        if (railStyle != "none")
        {
            var inset = railStyle == "parapet" ? Math.Max(0.05, l.Num("parapetThickness")) / 2 : Math.Max(0, l.Num("railInset"));
            l.Rails.AddRange(Runs(pts, roles, Railed, inset));
            var height = Math.Max(0.3, l.Num("railHeight"));
            if (railStyle == "parapet")
            {
                var pt = Math.Max(0.05, l.Num("parapetThickness"));
                var walls = new List<Solid>();
                foreach (var run in l.Rails)
                    for (var i = 0; i + 1 < run.Count; i++)
                    {
                        var (a, b) = (run[i], run[i + 1]);
                        var z = Math.Min(SlabTop(a), SlabTop(b));
                        // Each piece a little longer, so the corners close.
                        var dir = (b - a).Normalized() * (pt / 2);
                        var (a2, b2) = (i == 0 ? a : a - dir, i + 2 == run.Count ? b : b + dir);
                        if (Prism.Extrude([a2.At(z) + Side(a2, b2, -pt / 2), b2.At(z) + Side(a2, b2, -pt / 2), b2.At(z) + Side(a2, b2, pt / 2), a2.At(z) + Side(a2, b2, pt / 2)], null, new Vec3(0, 0, height + top - z), "parapet") is { } s) walls.Add(s);
                    }
                var pm = l.Str("parapetMaterial");
                if (walls.Count > 0) l.Parts.Add(("balcony-parapet", "Parapet", pm, Rgb(m, pm, "#c9c4bb"), Openings.Combine(walls, "parapet")));
            }
            else
            {
                var rail = new StairRailing("both", height, l.Num("barSize"), l.Num("postSize"), l.Num("postSpacing"),
                    StairSpec.RailingStyles.Contains(railStyle) ? railStyle : "bar", l.Str("railProfile") == "square" ? "square" : "round", l.Num("balusterSpacing"), 0, 0, railColor);
                var metal = new List<Solid>();
                var glass = new List<Solid>();
                foreach (var run in l.Rails)
                {
                    var (mm, gg) = Railings.Solids(run.Select(p => p.At(l.Walk(p))).ToList(), rail);
                    metal.AddRange(mm);
                    glass.AddRange(gg);
                }
                if (metal.Count > 0) l.Parts.Add(("balcony-railing", "Railing", null, railColor, Openings.Combine(metal, "railing")));
                if (glass.Count > 0) l.Parts.Add(("balcony-glass", "Railing glass", null, "#9cc8e8", Openings.Combine(glass, "glass")));
            }
        }

        // What carries it.
        var support = kind is "loggia" or "mezzanine" ? "none" : l.Str("support");
        var size = Math.Max(0.01, l.Num("supportSize"));
        if (l.HasSlab && support == "posts")
        {
            l.PostBottom = PostFoot(m, f);
            var foot = l.PostBottom;
            foreach (var run in Runs(pts, roles, r => r == "front", size / 2 + 0.05))
                l.Posts.AddRange(Resample(run, Math.Max(0.3, l.Num("supportSpacing"))));
            var posts = new List<Solid>();
            foreach (var p in l.Posts)
            {
                var head = Bottom(p) - Math.Max(0, l.Num("soffitThickness"));
                if (head - foot < 0.1) continue;
                var s = l.Str("supportShape") == "square" ? Prism.Box(p.At((foot + head) / 2), size, size, head - foot, "post") : Prism.Cylinder(p, foot, size / 2, head - foot, 0, "post");
                if (s != null) posts.Add(s);
            }
            if (posts.Count == 0) { l.Posts.Clear(); l.Notes.Add("Nothing under the balcony for its posts to stand on: on the lowest storey it needs none."); }
            else l.Parts.Add(("balcony-posts", "Posts", steel, Rgb(m, steel, "#5d6168"), Openings.Combine(posts, "post")));
        }
        else if (l.HasSlab && support == "brackets")
        {
            // Along the edge against the wall (v = 0), evenly.
            var onFace = new List<double>();
            for (var i = 0; i < pts.Count; i++)
                if (roles[i] == "wall" && Math.Abs(pts[i].Y) < 1e-6 && Math.Abs(pts[(i + 1) % pts.Count].Y) < 1e-6) { onFace.Add(pts[i].X); onFace.Add(pts[(i + 1) % pts.Count].X); }
            if (onFace.Count > 0)
            {
                double u0 = onFace.Min(), u1 = onFace.Max(), length = u1 - u0;
                var margin = Math.Min(0.15, length / 4);
                var count = Math.Max(2, (int)Math.Round((length - 2 * margin) / Math.Max(0.3, l.Num("supportSpacing"))) + 1);
                for (var i = 0; i < count; i++) l.Brackets.Add(u0 + margin + (length - 2 * margin) * i / (count - 1));
                var depthAvail = Math.Max(0.05, (pts.Where(p => p.X >= u0 - 1e-6 && p.X <= u1 + 1e-6).Max(p => p.Y)) - 0.02);
                var bd = Math.Min(Math.Max(0.05, l.Num("bracketDepth")), depthAvail);
                var bh = Math.Max(0.05, l.Num("bracketHeight"));
                var soffit = Math.Max(0, l.Num("soffitThickness"));
                var brackets = new List<Solid>();
                foreach (var u in l.Brackets)
                {
                    var zb = Bottom(new Vec2(u, 0)) - soffit;
                    List<Vec3> tri = [new(u - size / 2, 0, zb), new(u - size / 2, bd, zb), new(u - size / 2, 0, zb - bh)];
                    if (Prism.Extrude(tri, null, new Vec3(size, 0, 0), "bracket") is { } s) brackets.Add(s);
                }
                if (brackets.Count > 0) l.Parts.Add(("balcony-brackets", "Brackets", steel, Rgb(m, steel, "#5d6168"), Openings.Combine(brackets, "bracket")));
            }
        }
        else if (l.HasSlab && support == "hangers")
        {
            var rods = new List<Solid>();
            var up = Math.Max(0.3, l.Num("hangerHeight"));
            foreach (var run in Runs(pts, roles, r => r == "front", 0.1))
                foreach (var p in new[] { run[0], run[^1] })
                {
                    l.Hangers.Add(p);
                    if (StairBuilder.RoundBar(new Vec3(p.X, p.Y, top + 0.02), new Vec3(p.X, 0, top + up), size, "rod") is { } s) rods.Add(s);
                }
            if (rods.Count > 0) l.Parts.Add(("balcony-hangers", "Tie rods", steel, Rgb(m, steel, "#5d6168"), Openings.Combine(rods, "rod")));
        }

        // A gallery's fascia over its edge.
        if (kind == "mezzanine" && l.Num("fascia") > 0.01)
        {
            var fascia = l.Num("fascia");
            var bands = new List<Solid>();
            for (var i = 0; i < pts.Count; i++)
            {
                if (roles[i] != "front") continue;
                var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
                var outward = -(b - a).Normalized().Perp * 0.012;
                var zt = top + 0.01;
                if (Prism.SweepBox((a + outward).At(zt - fascia / 2), (b + outward).At(zt - fascia / 2), 0.024, fascia, "fascia") is { } s) bands.Add(s);
            }
            if (bands.Count > 0) l.Parts.Add(("balcony-fascia", "Fascia", "Plasterboard", Rgb(m, "Plasterboard", "#f1efea"), Openings.Combine(bands, "fascia")));
        }

        // A loggia's side walls and ceiling, behind its wall's face.
        if (kind == "loggia")
        {
            var w = Math.Max(0.3, l.Num("width"));
            var d = Math.Max(0, l.Num("depth"));
            var t = f.Wall != null ? f.WallThickness : 0;
            var c = Math.Max(0.05, l.Num("cheekThickness"));
            var h = Math.Min(Math.Max(1.8, l.Num("recessHeight")), f.WallHeight);
            var zb = f.WallBase;
            if (d > t + 0.01)
            {
                var cheeks = new List<Solid>();
                foreach (var x0 in new[] { -w / 2 - c, w / 2 })
                    if (Prism.ExtrudeFootprint([new(x0, -d), new(x0 + c, -d), new(x0 + c, -t), new(x0, -t)], zb, h, "cheek") is { } s) cheeks.Add(s);
                var cm = f.Wall != null && Walls.Section(m, f.Wall).FirstOrDefault(b => b.Core).Material is { } coreMat ? m.Get(coreMat)?.Name : null;
                if (cheeks.Count > 0) l.Parts.Add(("balcony-cheeks", "Side walls", cm, Rgb(m, cm, "#d9d4c9"), Openings.Combine(cheeks, "cheek")));
                if (l.Bool("ceiling"))
                {
                    var ct = Math.Min(thickness, Math.Max(0.05, f.WallHeight - h));
                    if (Prism.ExtrudeFootprint([new(-w / 2 - c, -d), new(w / 2 + c, -d), new(w / 2 + c, -t), new(-w / 2 - c, -t)], zb + h, ct, "ceiling") is { } s)
                        l.Parts.Add(("balcony-ceiling", "Ceiling", slabMat, Rgb(m, slabMat, "#b8b4ac"), s));
                }
            }
            var soffit = Math.Max(0, l.Num("soffitThickness"));
            if (soffit > 0 && Prism.ExtrudeFootprint([new(-w / 2, -d), new(w / 2, -d), new(w / 2, 0), new(-w / 2, 0)], zb + h - soffit, soffit, "soffit") is { } ceilingFinish)
            {
                var sm = l.Str("soffitMaterial");
                l.Parts.Add(("balcony-soffit", "Soffit", sm, Rgb(m, sm, "#efece6"), ceilingFinish));
            }
            if (f.Wall == null) l.Notes.Add("A loggia is cut into a wall: place it on one.");
        }
        return l;
    }

    static Vec3 Side(Vec2 a, Vec2 b, double by) { var n = (b - a).Normalized().Perp * by; return new Vec3(n.X, n.Y, 0); }

    /// <summary>Where a balcony's posts stand: the level of the storey under it (its balcony, stacked), never below ±0.00.</summary>
    static double PostFoot(IModelView m, BalconyFrame f)
    {
        var below = Spatial.Storeys(m).Where(s => s.Num("elevation") < f.Storey - 1e-6).OrderBy(s => s.Num("elevation")).LastOrDefault();
        return Math.Max(below?.Num("elevation") ?? 0, Math.Min(0, f.Storey));
    }

    /// <summary>The runs of consecutive edges <paramref name="take"/> keeps, as polylines on the outline moved in by <paramref name="inset"/>.</summary>
    public static List<List<Vec2>> Runs(IReadOnlyList<Vec2> pts, IReadOnlyList<string> roles, Func<string, bool> take, double inset)
    {
        var n = pts.Count;
        var runs = new List<List<Vec2>>();
        if (n < 3) return runs;
        var inner = inset > 0 ? Polygon2.Offset(pts, [-inset]) : [.. pts];
        var keep = roles.Select(take).ToArray();
        if (keep.All(k => k)) { var all = inner.ToList(); all.Add(inner[0]); runs.Add(all); return runs; }
        if (!keep.Any(k => k)) return runs;
        // Start just after an edge not kept, so no run is split across the ring's seam.
        var start = Enumerable.Range(0, n).First(i => !keep[i]);
        List<Vec2>? run = null;
        for (var s = 1; s <= n; s++)
        {
            var i = (start + s) % n;
            if (keep[i])
            {
                run ??= [inner[i]];
                run.Add(inner[(i + 1) % n]);
            }
            else if (run != null) { runs.Add(run); run = null; }
        }
        if (run != null) runs.Add(run);
        return runs;
    }

    /// <summary>Points along a polyline at most <paramref name="spacing"/> apart, its ends included.</summary>
    static IEnumerable<Vec2> Resample(List<Vec2> run, double spacing)
    {
        var lengths = new List<double> { 0 };
        for (var i = 1; i < run.Count; i++) lengths.Add(lengths[^1] + (run[i] - run[i - 1]).Length);
        var total = lengths[^1];
        if (total < 1e-6) { yield return run[0]; yield break; }
        var count = Math.Max(1, (int)Math.Ceiling(total / spacing - 1e-9));
        for (var k = 0; k <= count; k++)
        {
            var at = total * k / count;
            var j = 1;
            while (j < run.Count - 1 && lengths[j] < at) j++;
            var seg = lengths[j] - lengths[j - 1];
            var t = seg < 1e-9 ? 0 : (at - lengths[j - 1]) / seg;
            yield return run[j - 1] + (run[j] - run[j - 1]) * t;
        }
    }

    /// <summary>A footprint between two planar heights (each linear over it): its bottom, its top and its sides.</summary>
    public static Solid? Slab(IReadOnlyList<Vec2> footprint, Func<Vec2, double> top, Func<Vec2, double> bottom, string tag)
    {
        var ring = Polygon2.EnsureCcw(footprint).ToList();
        if (ring.Count < 3 || Math.Abs(Polygon2.SignedArea(ring)) < 1e-8) return null;
        if (ring.Any(p => top(p) - bottom(p) < 1e-4)) return null;
        var n = ring.Count;
        var verts = new List<Vec3>(2 * n);
        foreach (var p in ring) verts.Add(p.At(bottom(p)));
        foreach (var p in ring) verts.Add(p.At(top(p)));
        var faces = new List<FaceInput>
        {
            new(Enumerable.Range(0, n).Reverse().ToArray(), null, new Vec3(0, 0, -1), "bottom"),
            new(Enumerable.Range(n, n).ToArray(), null, VecMath.NewellNormal(verts.Skip(n).ToList()).Normalized(), "top"),
        };
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var d = ring[j] - ring[i];
            faces.Add(new FaceInput([i, j, n + j, n + i], null, new Vec3(d.Y, -d.X, 0), "side"));
        }
        return SolidBuilder.Make(verts, faces, tag);
    }

    // ------------------------------------------------------------ the element

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        if (Frame(m, e) is not { } f) return null;
        var l = Build(m, e, f, BalconySpec.Read(e));
        if (l.Parts.Count == 0) return new ElementGeometry([], null, null, l.Notes.Count > 0 ? l.Notes : ["The balcony has nothing to build."]);
        Vec3 W(Vec3 p) => f.World(p);
        var parts = l.Parts.Select(p => new GeometryPart(p.Name, p.Color, SolidBuilder.Transform(p.Solid, W), p.Material is { } mn ? Materials.Find(m, mn)?.Id : null, p.Key)).ToList();
        var plan = new List<PlanItem> { new("outline", true, l.Outline.Select(f.Plan).ToList()) };
        foreach (var r in l.Rails) plan.Add(new PlanItem("symbol", false, r.Select(f.Plan).ToList()));
        return new ElementGeometry(parts, plan, l.Outline.Select(f.Plan).ToList(), l.Notes.Count > 0 ? l.Notes : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, IModelView m)
    {
        if (Build(m, e) is not { } l || l.Outline.Count < 3) yield break;
        var slab = l.HasSlab ? l.Parts.FirstOrDefault(p => p.Key == "balcony-slab").Solid : null;
        if (slab != null)
            yield return new QuantitySet("Qto_SlabBaseQuantities",
            [
                new("Width", l.Num("width"), "LENGTH"), new("Length", l.Num("depth"), "LENGTH"), new("Depth", l.Num("thickness"), "LENGTH"),
                new("GrossArea", l.Area, "AREA", "Balcony slab"), new("NetVolume", Measure.Volume(slab), "VOLUME", "Balcony slab"),
            ], "balcony-slab", "Slab");
        if (l.RailLength > 0)
            yield return new QuantitySet("Qto_RailingBaseQuantities", [new("Length", l.RailLength, "LENGTH", $"Railing, {l.Str("railStyle")}")],
                l.Str("railStyle") == "parapet" ? "balcony-parapet" : "balcony-railing", "Railing");
        if (l.SupportCount > 0)
            yield return new QuantitySet("Lapin_BalconySupports", [new("Count", l.SupportCount, "COUNT", l.Posts.Count > 0 ? "Posts" : l.Brackets.Count > 0 ? "Brackets" : "Tie rods")]);
    }

    // ------------------------------------------------------------ the configurator's drawings

    static JsonArray Pts(IEnumerable<Vec2> pts) => new([.. pts.SelectMany(p => new JsonNode[] { Math.Round(p.X, 4), Math.Round(p.Y, 4) })]);

    /// <summary>
    /// The balcony as the configurator draws it: its plan in its own frame (the outline with each edge's role, the
    /// railing, the posts, brackets and rods), a section through its middle (the wall, the slab, the finish, the soffit,
    /// the railing, what carries it; heights from its floor) and a summary.
    /// </summary>
    public static JsonObject Describe(IModelView m, BalconyFrame f, BalconyLayout l)
    {
        static string N(double v, string unit) => v.ToString("0.00", CultureInfo.InvariantCulture) + " " + unit;
        var o = new JsonObject
        {
            ["outline"] = Pts(l.Outline), ["roles"] = new JsonArray([.. l.Roles.Select(r => (JsonNode)r)]),
            ["rails"] = new JsonArray([.. l.Rails.Select(r => (JsonNode)Pts(r))]),
            ["posts"] = Pts(l.Posts), ["hangers"] = Pts(l.Hangers), ["brackets"] = new JsonArray([.. l.Brackets.Select(b => (JsonNode)Math.Round(b, 4))]),
            ["wall"] = f.Wall != null ? Math.Round(f.WallThickness, 4) : null,
            ["notes"] = new JsonArray([.. l.Notes.Select(n => (JsonNode)n)]),
        };
        // The section: across the middle of the front (u = 0 of the main leg).
        var items = new JsonArray();
        void Item(string role, IEnumerable<(double V, double Z)> poly) => items.Add((JsonNode)new JsonObject { ["role"] = role, ["pts"] = new JsonArray([.. poly.SelectMany(p => new JsonNode[] { Math.Round(p.V, 4), Math.Round(p.Z - l.Top, 4) })]) });
        var cut = Cut(l.Outline, 0);
        var t = f.Wall != null ? f.WallThickness : 0.3;
        var railH = l.Str("railStyle") == "none" ? 0 : Math.Max(0.3, l.Num("railHeight"));
        if (l.Kind == "loggia")
        {
            var h = Math.Min(Math.Max(1.8, l.Num("recessHeight")), f.WallHeight);
            var z0 = f.WallBase;
            var d = Math.Max(0, l.Num("depth"));
            Item("wall", [(-t, z0 + h), (0, z0 + h), (0, z0 + f.WallHeight), (-t, z0 + f.WallHeight)]);
            Item("wall", [(-d - t, z0), (-d, z0), (-d, z0 + f.WallHeight), (-d - t, z0 + f.WallHeight)]);
        }
        else if (l.Kind != "mezzanine") Item("wall", [(-t, l.Top - 1.2), (0, l.Top - 1.2), (0, l.Top + Math.Max(2.6, railH + 0.4)), (-t, l.Top + Math.Max(2.6, railH + 0.4))]);
        foreach (var p in l.Parts)
        {
            if (Measure.Bounds(p.Solid) is not { } b) continue;
            var role = p.Key["balcony-".Length..];
            if (role is "railing" or "glass" or "parapet" or "posts" or "hangers" or "cheeks") continue;
            if (b.Min.X > 1e-6 || b.Max.X < -1e-6) continue;
            if (role is "brackets") Item(role, [(0, b.Max.Z), (b.Max.Y, b.Max.Z), (0, b.Min.Z)]);
            else if (role is "slab" or "finish" or "soffit" && cut is (var c0, var c1) && l.Kind != "loggia") Item(role, SectionOf(p.Solid, c0, c1));
            else Item(role, [(b.Min.Y, b.Min.Z), (b.Max.Y, b.Min.Z), (b.Max.Y, b.Max.Z), (b.Min.Y, b.Max.Z)]);
        }
        // The railing at the front, seen cut; the posts and rods beyond.
        if (cut is (_, var front) && railH > 0)
        {
            var inset = l.Str("railStyle") == "parapet" ? Math.Max(0.05, l.Num("parapetThickness")) / 2 : l.Num("railInset");
            var half = l.Str("railStyle") == "parapet" ? Math.Max(0.05, l.Num("parapetThickness")) / 2 : Math.Max(0.02, l.Num("barSize")) / 2;
            var v = front - inset;
            var z = l.Walk(new Vec2(0, front));
            Item(l.Str("railStyle") == "parapet" ? "parapet" : l.Str("railStyle") == "glass" ? "glass" : "railing", [(v - half, z), (v + half, z), (v + half, z + railH), (v - half, z + railH)]);
        }
        if (l.Posts.Count > 0)
        {
            var size = Math.Max(0.01, l.Num("supportSize"));
            var v = l.Posts[0].Y;
            Item("posts", [(v - size / 2, l.PostBottom), (v + size / 2, l.PostBottom), (v + size / 2, l.SlabBottomAtBack), (v - size / 2, l.SlabBottomAtBack)]);
        }
        if (l.Hangers.Count > 0)
        {
            var v = l.Hangers[0].Y;
            var up = Math.Max(0.3, l.Num("hangerHeight"));
            Item("hangers", [(v - 0.01, l.Top), (v + 0.01, l.Top), (0.01, l.Top + up), (-0.01, l.Top + up)]);
        }
        o["section"] = new JsonObject { ["items"] = items, ["railHeight"] = railH };
        var summary = new JsonArray
        {
            (JsonNode)new JsonArray("Area", N(l.Area, "m²")),
            (JsonNode)new JsonArray("Railing", N(l.RailLength, "m")),
        };
        if (l.Parts.FirstOrDefault(p => p.Key == "balcony-slab").Solid is { } slab) summary.Add((JsonNode)new JsonArray("Slab", N(Measure.Volume(slab), "m³")));
        if (l.Posts.Count > 0) summary.Add((JsonNode)new JsonArray("Posts", l.Posts.Count.ToString(CultureInfo.InvariantCulture)));
        if (l.Brackets.Count > 0) summary.Add((JsonNode)new JsonArray("Brackets", l.Brackets.Count.ToString(CultureInfo.InvariantCulture)));
        if (l.Hangers.Count > 0) summary.Add((JsonNode)new JsonArray("Tie rods", l.Hangers.Count.ToString(CultureInfo.InvariantCulture)));
        o["summary"] = summary;
        return o;
    }

    /// <summary>A slab-like part's section between v0 and v1: its bottom and top there (heights vary along v only), from its nearest vertices.</summary>
    static IEnumerable<(double V, double Z)> SectionOf(Solid s, double v0, double v1)
    {
        double ZAt(double v, bool top)
        {
            var best = s.Vertices.Min(p => Math.Abs(p.Y - v));
            var zs = s.Vertices.Where(p => Math.Abs(p.Y - v) <= best + 1e-4).Select(p => p.Z);
            return top ? zs.Max() : zs.Min();
        }
        return [(v0, ZAt(v0, false)), (v1, ZAt(v1, false)), (v1, ZAt(v1, true)), (v0, ZAt(v0, true))];
    }

    /// <summary>Where the line u = <paramref name="u"/> crosses the outline: its lowest and highest v; null when it does not.</summary>
    static (double, double)? Cut(IReadOnlyList<Vec2> pts, double u)
    {
        var vs = new List<double>();
        for (var i = 0; i < pts.Count; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
            if ((a.X - u) * (b.X - u) > 0 || Math.Abs(b.X - a.X) < 1e-12) continue;
            vs.Add(a.Y + (b.Y - a.Y) * (u - a.X) / (b.X - a.X));
        }
        return vs.Count >= 2 ? (vs.Min(), vs.Max()) : null;
    }
}
