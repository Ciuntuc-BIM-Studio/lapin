using System.Text.Json.Nodes;

namespace Lapin.Model;

/// <summary>
/// Where an element along an axis (its <c>start</c> and <c>end</c> attributes, a wall's) lies in plan, as cells of a grid: a build that
/// looks at such elements near it (the walls an opening passes through, forum topic 64) reads the cells round it instead of the whole
/// class, so it is built again only when an element of the class comes into, changes in or leaves those cells. The store keeps a stamp
/// per cell (<see cref="ModelStore.ReadStamp"/>: a sum over the elements in it, back to what it was on an undo) and an edit touches the
/// cells of where an element was and is. An element over too many cells is in <see cref="Wide"/>, which every reader reads too.
/// </summary>
public static class PlaceCells
{
    /// <summary>The side of a cell (m).</summary>
    public const double Size = 2;
    /// <summary>A segment whose box spans more cells than this is <see cref="Wide"/>.</summary>
    const int MaxCells = 4096;
    const string Prefix = "place:";

    /// <summary>The classes laid in cells: those some build reads so (the walls). Others are not, so a section's or a dimension's edit costs nothing here.</summary>
    static readonly HashSet<string> Classes = [IfcClass.Wall];

    /// <summary>
    /// What says where such an element is, beyond its axis, so its cells' stamp changes with them and nothing else (a name, a property
    /// set): its arc, its base, height and top, its own thickness, and whether it was read from a file.
    /// </summary>
    static readonly string[] PlaceKeys = ["arc", "baseOffset", "height", "top", "topOffset", "thickness"];

    public static bool IsKey(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>The key of the class's elements too large for cells.</summary>
    public static string Wide(string ifcClass) => $"{Prefix}{ifcClass}:*";

    /// <summary>The cells an element along an axis lies in (those its axis crosses, an arc's rise round it); none for an element with no axis, or of a class not laid in cells.</summary>
    public static IEnumerable<string> Of(Entity e)
    {
        if (!Classes.Contains(e.Class) || Axis(e) is not var (x0, y0, x1, y1)) return [];
        return Along(e.Class, x0, y0, x1, y1, Math.Abs(Json.Number(e.Attrs["arc"]) ?? 0), reader: false);
    }

    static (double, double, double, double)? Axis(Entity e) =>
        e.Attrs["start"] is JsonArray { Count: >= 2 } s && e.Attrs["end"] is JsonArray { Count: >= 2 } t
        && Json.Number(s[0]) is { } x0 && Json.Number(s[1]) is { } y0 && Json.Number(t[0]) is { } x1 && Json.Number(t[1]) is { } y1 ? (x0, y0, x1, y1) : null;

    /// <summary>
    /// A hash of where the element is (its id, axis and <see cref="PlaceKeys"/>, whether it was imported): what its cells' stamps sum,
    /// the same for the same place whatever else changed (its version does with its name).
    /// </summary>
    public static ulong Hash(Entity e)
    {
        var h = 14695981039346656037UL;
        void Add(ulong v) => h = ModelStore.Mix(h ^ v);
        void Text(string t) { foreach (var c in t) h = (h ^ c) * 1099511628211UL; Add((ulong)t.Length); }
        Text(e.Id);
        Text(e.Class);
        if (Axis(e) is var (x0, y0, x1, y1)) foreach (var v in new[] { x0, y0, x1, y1 }) Add((ulong)BitConverter.DoubleToInt64Bits(v));
        foreach (var k in PlaceKeys) Text(e.Attrs[k]?.ToJsonString() ?? "");
        Add(e.Attrs["imported"] != null ? 1UL : 0UL);
        return h;
    }

    /// <summary>Whether an edit from <paramref name="before"/> to <paramref name="after"/> moves the element in its cells (and so touches them).</summary>
    public static bool Moved(Entity? before, Entity? after) =>
        before == null || after == null || before.Class != after.Class || Hash(before) != Hash(after);

    /// <summary>The keys a build looking within <paramref name="reach"/> of a segment in plan reads: the cells round it and the class's <see cref="Wide"/>.</summary>
    public static IEnumerable<string> Round(string ifcClass, double x0, double y0, double x1, double y1, double reach) => Along(ifcClass, x0, y0, x1, y1, reach, reader: true);

    /// <summary>The cells within <paramref name="margin"/> of a segment: those it crosses, and those as near as that.</summary>
    static IEnumerable<string> Along(string cls, double x0, double y0, double x1, double y1, double margin, bool reader)
    {
        double cx0 = Math.Floor((Math.Min(x0, x1) - margin) / Size), cy0 = Math.Floor((Math.Min(y0, y1) - margin) / Size);
        double cx1 = Math.Floor((Math.Max(x0, x1) + margin) / Size), cy1 = Math.Floor((Math.Max(y0, y1) + margin) / Size);
        if (!double.IsFinite(cx0 + cy0 + cx1 + cy1) || (cx1 - cx0 + 1) * (cy1 - cy0 + 1) > MaxCells)
        {
            // A reader over so many cells reads them all: the class's every element, which an edit of any of them touches.
            yield return reader ? "class:" + cls : Wide(cls);
            yield break;
        }
        for (var x = (long)cx0; x <= (long)cx1; x++)
            for (var y = (long)cy0; y <= (long)cy1; y++)
                if (Distance(x0, y0, x1, y1, x * Size, y * Size, (x + 1) * Size, (y + 1) * Size) <= margin + 1e-9) yield return $"{Prefix}{cls}:{x},{y}";
        if (reader) yield return Wide(cls);
    }

    /// <summary>How far a segment is from a rectangle: 0 when it crosses it, else the least of its ends' from the rectangle and the corners' from it.</summary>
    static double Distance(double x0, double y0, double x1, double y1, double rx0, double ry0, double rx1, double ry1)
    {
        // Clipped to the rectangle (Liang–Barsky): anything left, it crosses it.
        double t0 = 0, t1 = 1, dx = x1 - x0, dy = y1 - y0;
        bool Clip(double p, double q)
        {
            if (Math.Abs(p) < 1e-15) return q >= 0;
            var r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
        if (Clip(-dx, x0 - rx0) && Clip(dx, rx1 - x0) && Clip(-dy, y0 - ry0) && Clip(dy, ry1 - y0)) return 0;
        static double ToRect(double px, double py, double ax, double ay, double bx, double by) =>
            Math.Sqrt(Math.Pow(Math.Max(0, Math.Max(ax - px, px - bx)), 2) + Math.Pow(Math.Max(0, Math.Max(ay - py, py - by)), 2));
        double ToSegment(double px, double py)
        {
            var len2 = dx * dx + dy * dy;
            var u = len2 > 0 ? Math.Clamp(((px - x0) * dx + (py - y0) * dy) / len2, 0, 1) : 0;
            return Math.Sqrt(Math.Pow(x0 + dx * u - px, 2) + Math.Pow(y0 + dy * u - py, 2));
        }
        return Math.Min(Math.Min(ToRect(x0, y0, rx0, ry0, rx1, ry1), ToRect(x1, y1, rx0, ry0, rx1, ry1)),
            Math.Min(Math.Min(ToSegment(rx0, ry0), ToSegment(rx1, ry0)), Math.Min(ToSegment(rx0, ry1), ToSegment(rx1, ry1))));
    }

}
