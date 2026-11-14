using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a part's dimensions and sections hold on to (docs/plan-shop-drawings.md §9, decision 582), in the element's own frame
/// (<see cref="ElementFrame"/>), each with a key stable while the element's attributes are:
/// <list type="bullet">
/// <item><c>hole</c>: <see cref="At"/> its centre, <see cref="Axis"/> the way through it, <see cref="Diameter"/>;</item>
/// <item><c>notch</c>: a cope at an end, <see cref="At"/> its corner nearest the frame's origin, <see cref="Size"/> its length along x,
/// width along y and depth along z; <see cref="End"/> start or end;</item>
/// <item><c>cut</c>: an end cut off square, <see cref="End"/>, <see cref="PlanAngle"/> (about z, seen from above) and
/// <see cref="ElevAngle"/> (about y, seen from the front), degrees from square.</item>
/// </list>
/// </summary>
public sealed record ElementFeature(string Key, string Kind, Vec3 At, Vec3 Axis, double Diameter = 0, Vec3 Size = default, string? End = null,
    double PlanAngle = 0, double ElevAngle = 0);

/// <summary>A module's features of its elements (a steel member's holes, copes and cut ends); null for an element it does not know.</summary>
public interface IElementFeatures
{
    IReadOnlyList<ElementFeature>? FeaturesOf(IModelView m, Entity e, ElementFrame f);
}

/// <summary>A run of holes along one line at one pitch: its first and last centres, how many, the pitch, the diameter.</summary>
public sealed record HoleGroup(string Key, List<ElementFeature> Holes, double Pitch)
{
    public ElementFeature First => Holes[0];
    public ElementFeature Last => Holes[^1];
    public int Spaces => Holes.Count - 1;
}

/// <summary>
/// An element's features (decision 582): its module's, else from its body: round holes as the inner loops of its faces whose
/// vertices stand on a circle (a plate's bolt holes, a Studio part's drilled holes), one per hole however many faces it goes through.
/// </summary>
public static class ElementFeatures
{
    public static IReadOnlyList<ElementFeature> Of(IModelView m, IServices? services, Entity e, ElementFrame f, IReadOnlyList<Solid> body)
    {
        if (services != null)
            foreach (var s in services.All<IElementFeatures>())
                if (s.FeaturesOf(m, e, f) is { } own) return own;
        return FromBody(f, body);
    }

    public static IReadOnlyList<ElementFeature> Of(IQueryContext q, Entity e) =>
        ElementFrames.Of(q, e) is { } f ? Of(q.Model, q.Services, e, f, q.Geometry(e.Id)?.Parts.Select(p => p.Solid).ToList() ?? []) : [];

    /// <summary>The round holes of a body: each face's inner loop of eight vertices or more on a circle (within 2 %), its axis the face's normal.</summary>
    public static List<ElementFeature> FromBody(ElementFrame f, IReadOnlyList<Solid> body)
    {
        var o = new List<ElementFeature>();
        foreach (var s in body)
            foreach (var face in s.Faces)
                foreach (var hole in face.Holes)
                {
                    if (hole.Length < 8) continue;
                    var pts = hole.Select(i => f.Local(s.Vertices[i])).ToList();
                    var c = pts.Aggregate(Vec3.Zero, (a, p) => a + p) * (1.0 / pts.Count);
                    var radii = pts.Select(p => (p - c).Length).ToList();
                    var r = radii.Average();
                    if (r < 1e-4 || radii.Any(x => Math.Abs(x - r) > r * 0.02)) continue;
                    var n = f.Local(f.Origin + face.Normal) ;
                    var axis = Snap(n.Normalized());
                    // The same hole seen on its other face: one, its centre between them.
                    var twin = o.FindIndex(h => Math.Abs(Math.Abs(Vec3.Dot(h.Axis, axis)) - 1) < 1e-6 && Math.Abs(h.Diameter - 2 * r) < 1e-4 && Across(h.At - c, axis) < 1e-4);
                    if (twin >= 0) { o[twin] = o[twin] with { At = (o[twin].At + c) * 0.5 }; continue; }
                    o.Add(new ElementFeature("", "hole", c, axis, 2 * r));
                }
        // Keyed by where they are (mm in the frame), in order along x, then z, then y.
        return [.. o.OrderBy(h => Math.Round(h.At.X, 4)).ThenBy(h => Math.Round(h.At.Z, 4)).ThenBy(h => Math.Round(h.At.Y, 4))
            .Select(h => h with { Key = $"hole:{Math.Round(h.At.X * 1000)}:{Math.Round(h.At.Y * 1000)}:{Math.Round(h.At.Z * 1000)}" })];
    }

    static double Across(Vec3 d, Vec3 axis) => (d - axis * Vec3.Dot(d, axis)).Length;

    static Vec3 Snap(Vec3 v)
    {
        static double C(double x) => Math.Abs(x) < 1e-9 ? 0 : Math.Abs(Math.Abs(x) - 1) < 1e-9 ? Math.Sign(x) : x;
        return new Vec3(C(v.X), C(v.Y), C(v.Z));
    }

    /// <summary>
    /// The holes seen face on along <paramref name="axis"/> (frame), in runs: holes on one line across (the same other coordinate within
    /// 0.5 mm), at one pitch along <paramref name="along"/> (within 0.5 mm), each run its first and last and how many.
    /// </summary>
    public static List<HoleGroup> Groups(IEnumerable<ElementFeature> features, Vec3 axis, Vec3 along)
    {
        var across = Vec3.Cross(axis, along);
        var holes = features.Where(h => h.Kind == "hole" && Math.Abs(Math.Abs(Vec3.Dot(h.Axis, axis)) - 1) < 1e-3).ToList();
        var o = new List<HoleGroup>();
        foreach (var line in holes.GroupBy(h => Math.Round(Vec3.Dot(h.At, across) * 2000)).OrderBy(g => g.Key))
        {
            var row = line.OrderBy(h => Vec3.Dot(h.At, along)).ToList();
            var run = new List<ElementFeature> { row[0] };
            double? pitch = null;
            void Close()
            {
                o.Add(new HoleGroup(run[0].Key, [.. run], pitch ?? 0));
                run = [];
                pitch = null;
            }
            for (var i = 1; i < row.Count; i++)
            {
                var d = Vec3.Dot(row[i].At - row[i - 1].At, along);
                if (pitch is { } p && Math.Abs(d - p) > 5e-4 || Math.Abs(row[i].Diameter - row[i - 1].Diameter) > 1e-4) { Close(); run.Add(row[i]); continue; }
                pitch ??= d;
                run.Add(row[i]);
            }
            Close();
        }
        return o;
    }

    public static JsonObject ToJson(ElementFeature h)
    {
        static JsonArray V(Vec3 v) => new(Math.Round(v.X, 6), Math.Round(v.Y, 6), Math.Round(v.Z, 6));
        var o = new JsonObject { ["key"] = h.Key, ["kind"] = h.Kind, ["at"] = V(h.At), ["axis"] = V(h.Axis) };
        if (h.Diameter > 0) o["diameter"] = h.Diameter;
        if (h.Kind == "notch") o["size"] = V(h.Size);
        if (h.End != null) o["end"] = h.End;
        if (h.PlanAngle != 0) o["planAngle"] = h.PlanAngle;
        if (h.ElevAngle != 0) o["elevAngle"] = h.ElevAngle;
        return o;
    }
}
