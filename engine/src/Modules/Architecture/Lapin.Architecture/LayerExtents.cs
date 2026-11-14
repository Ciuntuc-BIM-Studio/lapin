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

    public sealed record Extent(int Layer, string Edge, int? Index, double Offset, bool Cuts)
    {
        public JsonObject ToJson()
        {
            var o = new JsonObject { ["layer"] = Layer, ["edge"] = Edge };
            if (Index is { } i) o["index"] = i;
            o["offset"] = Offset;
            if (Cuts) o["cuts"] = true;
            return o;
        }
    }

    /// <summary>The element's extents, as stored; none for an element without them (every element made before).</summary>
    public static IReadOnlyList<Extent> Of(Entity e)
    {
        if (e.Attrs[Key] is not JsonArray arr || arr.Count == 0) return [];
        var o = new List<Extent>();
        foreach (var n in arr.OfType<JsonObject>())
        {
            if (Json.Number(n["layer"]) is not { } layer || (string?)n["edge"] is not { Length: > 0 } edge || Json.Number(n["offset"]) is not { } offset || !double.IsFinite(offset)) continue;
            var index = Json.Number(n["index"]) is { } i ? (int?)(int)i : null;
            o.Add(new Extent((int)layer, edge, index, offset, n["cuts"] is JsonValue c && c.TryGetValue<bool>(out var cuts) && cuts));
        }
        return o;
    }

    /// <summary>A layer's extent at a wall's edge (0: none).</summary>
    public static double Offset(IReadOnlyList<Extent> xs, int layer, string edge)
    {
        foreach (var x in xs) if (x.Layer == layer && x.Edge == edge) return x.Offset;
        return 0;
    }

    /// <summary>A layer's extent at an edge when it stops short of it (negative), else 0: what is taken off where the layer is built.</summary>
    public static double Short(IReadOnlyList<Extent> xs, int layer, string edge) => Math.Min(0, Offset(xs, layer, edge));

    /// <summary>The extents with one set: the one of the same layer and edge replaced, dropped when its offset is 0.</summary>
    public static List<Extent> With(IReadOnlyList<Extent> xs, Extent x)
    {
        var o = xs.Where(y => !(y.Layer == x.Layer && y.Edge == x.Edge && y.Index == x.Index)).ToList();
        if (Math.Abs(x.Offset) > 1e-9) o.Add(x);
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

    /// <summary>The pieces past their edges of an element's layers set to cut (<see cref="Extent.Cuts"/>): what it takes out of the walls and floors it meets.</summary>
    public static List<Piece> CuttingPieces(Entity e, IModelView m, IServices? services) => e.Class switch
    {
        IfcClass.Wall => Walls.ExtentPieces(e, m, services, cuttingOnly: true),
        IfcClass.Slab => Slabs.ExtentPieces(e, m, services, cuttingOnly: true),
        _ => [],
    };

    /// <summary>The classes whose layers may reach past their edges.</summary>
    public static readonly string[] Classes = [IfcClass.Wall, IfcClass.Slab];

    /// <summary>
    /// An element's parts less what the layers of the walls and floors round it take, where they reach past their own edges and
    /// are set to cut (forum topic 89). Read under a narrow key (<see cref="LayerExtentReads"/>): only the elements with extents
    /// are looked at, by a box round their attributes first.
    /// </summary>
    public static List<GeometryPart> CutBy(Entity target, IModelView m, IServices? services, List<GeometryPart> parts, List<string> problems)
    {
        if (parts.Count == 0 || services?.Get<ISolidBooleans>() is not { } booleans) return parts;
        (Vec3 Min, Vec3 Max)? box = null;
        foreach (var p in parts)
            if (Measure.Bounds(p.Solid) is { } b)
                box = box is { } o ? (new Vec3(Math.Min(o.Min.X, b.Min.X), Math.Min(o.Min.Y, b.Min.Y), Math.Min(o.Min.Z, b.Min.Z)), new Vec3(Math.Max(o.Max.X, b.Max.X), Math.Max(o.Max.Y, b.Max.Y), Math.Max(o.Max.Z, b.Max.Z))) : b;
        if (box is not { } tb) return parts;
        var cutters = new List<Solid>();
        foreach (var cls in Classes)
            foreach (var id in LayerExtentReads.Holders(m, cls))
            {
                if (id == target.Id) continue;
                // Its place in plan from its attributes, before anything is built: most holders are far.
                if (m.Get(id) is not { } e || !Of(e).Any(x => x.Cuts && x.Offset > 1e-9) || PlanBox(e) is not { } pb) continue;
                if (pb.X0 > tb.Max.X || pb.X1 < tb.Min.X || pb.Y0 > tb.Max.Y || pb.Y1 < tb.Min.Y) continue;
                foreach (var piece in CuttingPieces(e, m, services))
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

    /// <summary>A holder's box in plan from its attributes, as far as its layers may reach.</summary>
    static (double X0, double Y0, double X1, double Y1)? PlanBox(Entity e)
    {
        var pts = e.Class == IfcClass.Wall ? [Walls.Start(e), Walls.End(e)] : Slabs.Contour(e);
        if (pts.Count == 0) return null;
        // A wall's half thickness is not known without its type: a generous margin, the extents' and a metre.
        var margin = Of(e).Select(x => Math.Abs(x.Offset)).DefaultIfEmpty(0).Max() + (e.Class == IfcClass.Wall ? 1.0 : 0.01);
        return (pts.Min(p => p.X) - margin, pts.Min(p => p.Y) - margin, pts.Max(p => p.X) + margin, pts.Max(p => p.Y) + margin);
    }
}
