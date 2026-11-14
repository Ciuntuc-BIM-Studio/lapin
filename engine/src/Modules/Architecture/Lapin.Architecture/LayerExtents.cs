using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A layered element's layers reaching past its edges, or stopping short of them (forum topic 89, docs/plan-layer-extents.md):
/// the element keeps its type's layer set (IfcMaterialLayerSetUsage); each extent is Lapin's own design intent on the element
/// (<c>layerExtents</c>, carried in Lapin_Intent): a layer (its index in the set), an edge, an offset (metres; positive past the
/// edge, negative short of it) and whether the piece past the edge cuts the walls and floors it meets (<see cref="Extent.Cuts"/>;
/// off, it overlaps them and both keep their volumes). A wall's edges are its start and end (along its axis, whatever ends
/// there) and its bottom and top; a floor's are its outline, all round, or one edge of it (its contour's edge
/// <see cref="Extent.Index"/>, from corner i to the next), an edge's own over the outline's. What is short is taken off the
/// layer where it is built; what is past is a piece of its own, added after the cut priorities (which would take it back), one
/// body with the layer it goes on from.
/// </summary>
public static class LayerExtents
{
    public const string Key = LayerExtentReads.AttrKey;
    public const string Start = "start", End = "end", Bottom = "bottom", Top = "top", Outline = "outline", Edge = "edge";
    public static readonly string[] WallEdges = [Start, End, Bottom, Top];
    /// <summary>The longest extent a command takes (m): a layer's edge, not a new element.</summary>
    public const double MaxOffset = 1.0;

    /// <summary><paramref name="ByOrder"/> (with <paramref name="Cuts"/>): it cuts only what its element outranks by the cut order (stage 4), stored as <c>cuts: "order"</c>.</summary>
    public sealed record Extent(int Layer, string Edge, int? Index, double Offset, bool Cuts, bool ByOrder = false)
    {
        public JsonObject ToJson()
        {
            var o = new JsonObject { ["layer"] = Layer, ["edge"] = Edge };
            if (Index is { } i) o["index"] = i;
            o["offset"] = Offset;
            if (Cuts) o["cuts"] = ByOrder ? JsonValue.Create(LayerExtents.ByOrder) : true;
            return o;
        }
    }

    /// <summary>The <c>cuts</c> value of an extent that follows the cut order (<see cref="Extent.ByOrder"/>).</summary>
    public const string ByOrder = "order";

    /// <summary>A <c>cuts</c> argument or attribute: true, false, or <c>"order"</c> (cutting, by the order); null when it is none of them.</summary>
    public static bool? CutsArg(JsonNode? n) => n is JsonValue v ? v.TryGetValue<bool>(out var b) ? b : v.TryGetValue<string>(out var s) && s == ByOrder ? true : null : null;

    /// <summary>The element's extents, as stored; none for an element without them (every element made before).</summary>
    public static IReadOnlyList<Extent> Of(Entity e)
    {
        if (e.Attrs[Key] is not JsonArray arr || arr.Count == 0) return [];
        var o = new List<Extent>();
        foreach (var n in arr.OfType<JsonObject>())
        {
            if (Json.Number(n["layer"]) is not { } layer || (string?)n["edge"] is not { Length: > 0 } edge || Json.Number(n["offset"]) is not { } offset || !double.IsFinite(offset)) continue;
            var index = Json.Number(n["index"]) is { } i ? (int?)(int)i : null;
            var cuts = CutsArg(n["cuts"]) == true;
            o.Add(new Extent((int)layer, edge, index, offset, cuts, cuts && n["cuts"] is JsonValue c && c.TryGetValue<string>(out var by) && by == ByOrder));
        }
        return o;
    }

    /// <summary>The element's type (a wall's or a floor's), whose extents are the element's defaults.</summary>
    public static Entity? TypeOf(IModelView m, Entity e) =>
        e.Class is IfcClass.Wall or IfcClass.Slab && m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: IfcClass.WallType or IfcClass.SlabType } t ? t : null;

    /// <summary>
    /// The extents an element is built with: its type's (the defaults every element of the type takes, stage 4), each one the
    /// element sets itself at the same place (layer, edge, edge index) replacing it — an own offset of 0 keeps the layer at
    /// that edge against its type's — and its own. Those of offset 0 are left out.
    /// </summary>
    public static IReadOnlyList<Extent> Effective(Entity e, IModelView m)
    {
        var own = Of(e);
        var type = TypeOf(m, e) is { } t ? Of(t) : [];
        if (type.Count == 0) return own.Where(x => Math.Abs(x.Offset) > 1e-9).ToList();
        var o = type.Where(x => !own.Any(y => SamePlace(x, y))).Concat(own).Where(x => Math.Abs(x.Offset) > 1e-9).ToList();
        return [.. o.OrderBy(y => y.Layer).ThenBy(y => y.Edge, StringComparer.Ordinal).ThenBy(y => y.Index ?? -1)];
    }

    /// <summary>Whether two extents are at the same place: the same layer, edge and edge index.</summary>
    public static bool SamePlace(Extent a, Extent b) => a.Layer == b.Layer && a.Edge == b.Edge && a.Index == b.Index;

    /// <summary>A layer's extent at a wall's edge (0: none).</summary>
    public static double Offset(IReadOnlyList<Extent> xs, int layer, string edge)
    {
        foreach (var x in xs) if (x.Layer == layer && x.Edge == edge) return x.Offset;
        return 0;
    }

    /// <summary>A layer's extent at an edge when it stops short of it (negative), else 0: what is taken off where the layer is built.</summary>
    public static double Short(IReadOnlyList<Extent> xs, int layer, string edge) => Math.Min(0, Offset(xs, layer, edge));

    /// <summary>
    /// The extents with one set: the one of the same layer and edge replaced, dropped when its offset is 0 — unless
    /// <paramref name="keepZero"/> (the type has an extent there, which an own 0 overrides).
    /// </summary>
    public static List<Extent> With(IReadOnlyList<Extent> xs, Extent x, bool keepZero = false)
    {
        var o = xs.Where(y => !SamePlace(x, y)).ToList();
        if (keepZero || Math.Abs(x.Offset) > 1e-9) o.Add(x);
        return [.. o.OrderBy(y => y.Layer).ThenBy(y => y.Edge, StringComparer.Ordinal).ThenBy(y => y.Index ?? -1)];
    }

    /// <summary>The extents as stored; null when there are none (the attribute goes).</summary>
    public static JsonArray? ToJson(IReadOnlyList<Extent> xs) => xs.Count == 0 ? null : new JsonArray([.. xs.Select(x => (JsonNode)x.ToJson())]);

    // ─── Floors ───────────────────────────────────────────────────────────

    /// <summary>
    /// The distance each edge of a floor's contour moves for a layer: the edge's own extent, else the outline's, else 0;
    /// <paramref name="keep"/> says which extents count (all, the short ones, the cutting ones…). Null when none moves.
    /// </summary>
    public static double[]? SlabDistances(IReadOnlyList<Extent> xs, int layer, int edges, Func<Extent, double> keep)
    {
        var mine = xs.Where(x => x.Layer == layer).ToList();
        if (mine.Count == 0) return null;
        var all = mine.FirstOrDefault(x => x.Edge == Outline);
        var d = new double[edges];
        var any = false;
        for (var i = 0; i < edges; i++)
        {
            var x = mine.FirstOrDefault(y => y.Edge == Edge && y.Index == i) ?? all;
            d[i] = x != null ? keep(x) : 0;
            any |= Math.Abs(d[i]) > 1e-12;
        }
        return any ? d : null;
    }

    /// <summary>A floor's contour for a layer, each edge moved as <see cref="SlabDistances"/> says; the contour itself when none moves; null when it no longer encloses anything.</summary>
    public static IReadOnlyList<Vec2>? SlabContour(IReadOnlyList<Vec2> contour, IReadOnlyList<Extent> xs, int layer, Func<Extent, double> keep)
    {
        if (SlabDistances(xs, layer, contour.Count, keep) is not { } d) return contour;
        var moved = Polygon2.Offset(contour, d);
        return Polygon2.SignedArea(moved) > 1e-6 && Simple(moved) ? moved : null;
    }

    /// <summary>Whether a closed outline crosses itself nowhere (edges that are not neighbours do not touch).</summary>
    public static bool Simple(IReadOnlyList<Vec2> p)
    {
        var n = p.Count;
        for (var i = 0; i < n; i++)
            for (var j = i + 1; j < n; j++)
            {
                if (j == i + 1 || (i == 0 && j == n - 1)) continue;
                if (SegmentsMeet(p[i], p[(i + 1) % n], p[j], p[(j + 1) % n])) return false;
            }
        return true;
    }

    static bool SegmentsMeet(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double O(Vec2 p, Vec2 q, Vec2 r) => Vec2.Cross(q - p, r - p);
        var (d1, d2, d3, d4) = (O(c, d, a), O(c, d, b), O(a, b, c), O(a, b, d));
        return ((d1 > 1e-12 && d2 < -1e-12) || (d1 < -1e-12 && d2 > 1e-12)) && ((d3 > 1e-12 && d4 < -1e-12) || (d3 < -1e-12 && d4 > 1e-12));
    }

    /// <summary>
    /// A floor's edge extents after its contour changed (a corner moved, added or taken away): each goes to the new edge lying
    /// along the old one (the same direction, on its line within 5 mm), its own index first; one whose edge is gone is dropped.
    /// </summary>
    public static List<Extent> RemapEdges(IReadOnlyList<Extent> xs, IReadOnlyList<Vec2> was, IReadOnlyList<Vec2> now)
    {
        if (!xs.Any(x => x.Edge == Edge)) return [.. xs];
        bool Same(int oi, int ni)
        {
            if (oi >= was.Count || ni >= now.Count) return false;
            var (a, b) = (was[oi], was[(oi + 1) % was.Count]);
            var (c, d) = (now[ni], now[(ni + 1) % now.Count]);
            if ((b - a).Length < 1e-9 || (d - c).Length < 1e-9) return false;
            var u = (b - a).Normalized();
            return Vec2.Dot(u, (d - c).Normalized()) > 0.999 && Math.Abs(Vec2.Cross(u, c - a)) < 0.005 && Math.Abs(Vec2.Cross(u, d - a)) < 0.005;
        }
        var o = new List<Extent>();
        foreach (var x in xs)
        {
            if (x.Edge != Edge || x.Index is not { } i) { o.Add(x); continue; }
            var to = Same(i, i) ? i : Enumerable.Range(0, now.Count).Cast<int?>().FirstOrDefault(ni => Same(i, ni!.Value));
            if (to is { } t && !o.Any(y => y.Layer == x.Layer && y.Edge == Edge && y.Index == t)) o.Add(x with { Index = t });
        }
        return o;
    }

    // ─── Building ─────────────────────────────────────────────────────────

    /// <summary>A layer's piece past an element's edge: its body, material, name, part key, whether it is of the core, its heights.</summary>
    public sealed record Piece(Solid Solid, string? Material, string Name, string Key, bool Core, double Z0, double Z1);

    /// <summary>
    /// A piece put with the parts: one body with the part of the same key and material it touches (no seam drawn between
    /// them in a section), else a part of its own.
    /// </summary>
    public static void AddJoined(List<GeometryPart> parts, GeometryPart piece, ISolidBooleans? booleans)
    {
        if (Measure.Bounds(piece.Solid) is not { } xb) return;
        const double t = 1e-6;
        var at = parts.FindIndex(p => p.Key == piece.Key && p.Material == piece.Material && Measure.Bounds(p.Solid) is { } pb
            && pb.Min.X <= xb.Max.X + t && pb.Max.X >= xb.Min.X - t && pb.Min.Y <= xb.Max.Y + t && pb.Max.Y >= xb.Min.Y - t && pb.Min.Z <= xb.Max.Z + t && pb.Max.Z >= xb.Min.Z - t);
        Solid? joined = null;
        if (at >= 0 && booleans != null)
        {
            try { joined = booleans.Union([parts[at].Solid, piece.Solid], parts[at].Solid.Tag); }
            catch (InvalidOperationException) { joined = null; }
        }
        if (joined != null && Measure.Volume(joined) > 1e-12) parts[at] = parts[at] with { Solid = joined };
        else parts.Add(piece);
    }

    /// <summary>
    /// The pieces past their edges of an element's layers set to cut (<see cref="Extent.Cuts"/>), of the extents
    /// <paramref name="which"/> keeps: what it takes out of the elements it meets.
    /// </summary>
    public static List<Piece> CuttingPieces(Entity e, IModelView m, IServices? services, Func<Extent, bool>? which = null) => e.Class switch
    {
        IfcClass.Wall => Walls.ExtentPieces(e, m, services, cuttingOnly: true, which),
        IfcClass.Slab => Slabs.ExtentPieces(e, m, services, cuttingOnly: true, which),
        _ => [],
    };

    /// <summary>The classes whose layers may reach past their edges.</summary>
    public static readonly string[] Classes = [IfcClass.Wall, IfcClass.Slab];

    /// <summary>What the platform's <see cref="ILayerExtentCuts"/> asks for: the elements of other modules (columns, beams, footings) cut too.</summary>
    public sealed class Cuts : ILayerExtentCuts
    {
        public List<GeometryPart> Apply(Entity target, IModelView m, IServices? services, List<GeometryPart> parts, List<string> problems) => CutBy(target, m, services, parts, problems);
    }

    /// <summary>
    /// An element's parts less what the layers of the walls and floors round take, where they reach past their own edges and
    /// are set to cut (forum topic 89): every one so set, or (<see cref="Extent.ByOrder"/>) only when its element outranks this
    /// one by the cut order. Read under a narrow key (<see cref="LayerExtentReads"/>): only the elements with extents (their own
    /// or their type's) are looked at, by a box round their attributes first. Walls, floors, columns, beams and footings are cut.
    /// </summary>
    public static List<GeometryPart> CutBy(Entity target, IModelView m, IServices? services, List<GeometryPart> parts, List<string> problems)
    {
        if (parts.Count == 0 || services?.Get<ISolidBooleans>() is not { } booleans) return parts;
        (Vec3 Min, Vec3 Max)? box = null;
        foreach (var p in parts)
            if (Measure.Bounds(p.Solid) is { } b)
                box = box is { } o ? (new Vec3(Math.Min(o.Min.X, b.Min.X), Math.Min(o.Min.Y, b.Min.Y), Math.Min(o.Min.Z, b.Min.Z)), new Vec3(Math.Max(o.Max.X, b.Max.X), Math.Max(o.Max.Y, b.Max.Y), Math.Max(o.Max.Z, b.Max.Z))) : b;
        if (box is not { } tb) return parts;
        var order = services.Get<ICutters>();
        int? mine = null;
        var mineRead = false;
        var cutters = new List<Solid>();
        foreach (var cls in Classes)
            foreach (var id in LayerExtentReads.Holders(m, cls))
            {
                if (id == target.Id) continue;
                // Its place in plan from its attributes, before anything is built: most holders are far.
                if (m.Get(id) is not { } e || Effective(e, m).Where(x => x.Cuts && x.Offset > 1e-9).ToList() is not { Count: > 0 } cutting || PlanBox(e, m) is not { } pb) continue;
                if (pb.X0 > tb.Max.X || pb.X1 < tb.Min.X || pb.Y0 > tb.Max.Y || pb.Y1 < tb.Min.Y) continue;
                // Those following the cut order: only when the holder outranks the target.
                var outranks = false;
                if (cutting.Any(x => x.ByOrder))
                {
                    if (!mineRead) { mine = order?.PriorityOf(target, m); mineRead = true; }
                    outranks = mine is { } t && order?.PriorityOf(e, m) is { } h && h > t;
                    if (!outranks && cutting.All(x => x.ByOrder)) continue;
                }
                foreach (var piece in CuttingPieces(e, m, services, x => !x.ByOrder || outranks))
                    if (Measure.Bounds(piece.Solid) is { } sb && sb.Min.X < tb.Max.X - 1e-6 && sb.Max.X > tb.Min.X + 1e-6 && sb.Min.Y < tb.Max.Y - 1e-6 && sb.Max.Y > tb.Min.Y + 1e-6 && sb.Min.Z < tb.Max.Z - 1e-6 && sb.Max.Z > tb.Min.Z + 1e-6)
                        cutters.Add(piece.Solid);
            }
        if (cutters.Count == 0) return parts;
        return parts.Select(p =>
        {
            var b = Measure.Bounds(p.Solid);
            var reach = cutters.Where(c => b is { } pb && Measure.Bounds(c) is { } cb && cb.Min.X < pb.Max.X - 1e-6 && cb.Max.X > pb.Min.X + 1e-6 && cb.Min.Y < pb.Max.Y - 1e-6 && cb.Max.Y > pb.Min.Y + 1e-6 && cb.Min.Z < pb.Max.Z - 1e-6 && cb.Max.Z > pb.Min.Z + 1e-6).ToList();
            if (reach.Count == 0) return p;
            try { return booleans.Difference(p.Solid, reach, p.Solid.Tag) is { } s && Measure.Volume(s) > 1e-9 ? p with { Solid = s } : null; }
            catch (InvalidOperationException ex) { problems.Add($"{p.Name}: a layer reaching into it could not be taken out ({ex.Message})."); return p; }
        }).OfType<GeometryPart>().ToList();
    }

    // ─── Grips ────────────────────────────────────────────────────────────

    static JsonArray P3(Vec3 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(p.Z, 6));
    static JsonArray P2(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    /// <summary>
    /// A wall's or floor's layers' edges as they are built now, for the grips of "Edit layers" (forum topic 89, stage 2):
    /// <c>{layers: [{layer, name, footprint: [[x, y]…], z0, z1}], grips: [{layer, name, edge, index?, line: [[x, y, z]…], dir:
    /// [x, y, z], offset, own}]}</c>, model coordinates. Each layer is its footprint between two heights (a wall's under a roof
    /// up to its highest point); each grip lies along its edge (<c>line</c>: a wall's end up its height, its base and top along
    /// it, the roof's line under a roof; a floor's edge at the layer's middle height) and moves the edge along <c>dir</c>, out of
    /// the element; <c>offset</c> is the extent it has there now (its own, else its type's; a floor's edge's own, else its
    /// outline's), <c>own</c> whether the element sets it itself. Null for anything without layers.
    /// </summary>
    public static JsonObject? Edges(Entity e, IModelView m, IServices? services)
    {
        var xs = Effective(e, m);
        var own = Of(e);
        var layers = new JsonArray();
        var grips = new JsonArray();
        void Grip(int layer, string name, string edge, int? index, IEnumerable<Vec3> line, Vec3 dir, Extent? at)
        {
            var g = new JsonObject { ["layer"] = layer, ["name"] = name, ["edge"] = edge };
            if (index is { } i) g["index"] = i;
            g["line"] = new JsonArray([.. line.Select(p => (JsonNode)P3(p))]);
            g["dir"] = P3(dir);
            g["offset"] = Math.Round(at?.Offset ?? 0, 6);
            g["own"] = at != null && own.Any(y => SamePlace(y, at));
            grips.Add((JsonNode)g);
        }
        if (e.Class == IfcClass.Slab)
        {
            var contour = Slabs.Contour(e);
            if (contour.Count < 3 || LayerSets.BandsOf(m, e.Id) is not { Count: > 0 }) return null;
            var top = Slabs.Top(m, e);
            foreach (var (from, to, _, name, index) in Slabs.Section(m, e))
            {
                if (!(to - from > 1e-9)) continue;
                var c = SlabContour(contour, xs, index, x => x.Offset) ?? contour;
                layers.Add((JsonNode)new JsonObject { ["layer"] = index, ["name"] = name, ["footprint"] = new JsonArray([.. c.Select(p => (JsonNode)P2(p))]), ["z0"] = Math.Round(top + from, 6), ["z1"] = Math.Round(top + to, 6) });
                var zm = top + (from + to) / 2;
                var outline = xs.FirstOrDefault(x => x.Layer == index && x.Edge == Outline);
                for (var i = 0; i < contour.Count && i < c.Count; i++)
                {
                    var (a, b) = (c[i], c[(i + 1) % c.Count]);
                    var d = (contour[(i + 1) % contour.Count] - contour[i]).Normalized();
                    if (d.Length < 1e-9) continue;
                    // Out of a counter-clockwise outline: on its right.
                    Grip(index, name, Edge, i, [a.At(zm), b.At(zm)], new Vec3(d.Y, -d.X, 0), xs.FirstOrDefault(x => x.Layer == index && x.Edge == Edge && x.Index == i) ?? outline);
                }
            }
            return new JsonObject { ["layers"] = layers, ["grips"] = grips };
        }
        if (e.Class != IfcClass.Wall || LayerSets.BandsOf(m, e.Id) is not { Count: > 0 } || Walls.ExtentFrameOf(e, m, services) is not { } f) return null;
        var roofed = f.AboveRoof != null;
        foreach (var band in f.Bands)
        {
            if (!(band.To - band.From > 1e-9)) continue;
            Extent? At(string edge) => xs.FirstOrDefault(x => x.Layer == band.Index && x.Edge == edge);
            double Off(string edge) => At(edge)?.Offset ?? 0;
            var follows = roofed && Walls.FollowsRoof(band, f.Height);
            var (below, above) = (Off(Bottom), Off(Top));
            // Its height as built: its own bottom and top, else the wall's (under a roof, up to its highest point), with its extents.
            var z0 = Math.Clamp(band.Z0 ?? 0, 0, f.Built) - below;
            var z1 = (follows ? f.Built : Math.Clamp(band.Z1 ?? f.Built, 0, f.Built)) + above;
            if (z1 - z0 < 1e-6) continue;
            double EndAt(int end, bool atFrom) => f.EndAt(end, band, atFrom) + (end == 0 ? -Off(Start) : Off(End));
            var footprint = f.Axis.Band(y => EndAt(0, Math.Abs(y - band.From) < Math.Abs(y - band.To)), y => EndAt(1, Math.Abs(y - band.From) < Math.Abs(y - band.To)), band.From, band.To);
            layers.Add((JsonNode)new JsonObject { ["layer"] = band.Index, ["name"] = band.Name, ["footprint"] = new JsonArray([.. footprint.Select(p => (JsonNode)P2(p))]), ["z0"] = Math.Round(f.Z + z0, 6), ["z1"] = Math.Round(f.Z + z1, 6) });
            var ym = (band.From + band.To) / 2;
            var (s0, s1) = ((EndAt(0, true) + EndAt(0, false)) / 2, (EndAt(1, true) + EndAt(1, false)) / 2);
            var (p0, p1) = (f.Axis.At(s0, ym), f.Axis.At(s1, ym));
            var (t0, t1) = (f.Axis.TangentAt(s0), f.Axis.TangentAt(s1));
            // Its ends up its height (under a roof, to the roof there); its base along it.
            var roofLine = follows && f.RoofLine is { Count: > 1 } r ? r : null;
            double TopAt(double s) => roofLine != null ? Math.Min(z1, Under(roofLine, s) + above) : z1;
            Grip(band.Index, band.Name, Start, null, [p0.At(f.Z + z0), p0.At(f.Z + TopAt(s0))], new Vec3(-t0.X, -t0.Y, 0), At(Start));
            Grip(band.Index, band.Name, End, null, [p1.At(f.Z + z0), p1.At(f.Z + TopAt(s1))], new Vec3(t1.X, t1.Y, 0), At(End));
            var along = f.Axis.Stations(s0, s1);
            Grip(band.Index, band.Name, Bottom, null, along.Select(s => f.Axis.At(s, ym).At(f.Z + z0)), new Vec3(0, 0, -1), At(Bottom));
            // Its top along it: under a roof, the roof's line moved by its extent.
            if (roofLine != null)
                along = [.. along.Concat(roofLine.Select(p => p.S).Where(x => x > s0 + 1e-9 && x < s1 - 1e-9)).Order()];
            var topLine = along.Select(s => f.Axis.At(s, ym).At(f.Z + TopAt(s))).ToList();
            Grip(band.Index, band.Name, Top, null, topLine, new Vec3(0, 0, 1), At(Top));
        }
        return new JsonObject { ["layers"] = layers, ["grips"] = grips };
    }

    /// <summary>A roof's line (stations along the axis, heights) read at a station, on in line past its ends.</summary>
    static double Under(List<(double S, double V)> line, double s)
    {
        if (s <= line[0].S) return line[0].V;
        for (var i = 0; i + 1 < line.Count; i++)
            if (s <= line[i + 1].S) { var (a, b) = (line[i], line[i + 1]); return b.S - a.S < 1e-12 ? a.V : a.V + (b.V - a.V) * (s - a.S) / (b.S - a.S); }
        return line[^1].V;
    }

    /// <summary>A holder's box in plan from its attributes, as far as its layers may reach.</summary>
    static (double X0, double Y0, double X1, double Y1)? PlanBox(Entity e, IModelView m)
    {
        var pts = e.Class == IfcClass.Wall ? [Walls.Start(e), Walls.End(e)] : Slabs.Contour(e);
        if (pts.Count == 0) return null;
        // A wall's half thickness is not known without its type: a generous margin, the extents' and a metre (and a curved
        // wall's rise, its arc bulging past its ends' box).
        var margin = Effective(e, m).Select(x => Math.Abs(x.Offset)).DefaultIfEmpty(0).Max() + (e.Class == IfcClass.Wall ? 1.0 + Math.Abs(PathCurve.Of(e).Rise) : 0.01);
        return (pts.Min(p => p.X) - margin, pts.Min(p => p.Y) - margin, pts.Max(p => p.X) + margin, pts.Max(p => p.Y) + margin);
    }
}
