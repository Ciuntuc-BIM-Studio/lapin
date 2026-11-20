using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A terrain's control cage (issue #113): a few of its survey points chosen as controls, kept in <c>cage</c> as their
/// places in plan ([[x, y], …], a survey point matching within a millimetre), so adding, removing or merging points never
/// shifts them onto others and no GlobalId or TIN changes. The other points are free. Moving controls up or down moves
/// the free points by the cage's interpolation of the controls' moves:
/// <code>
///   shift(x) = Σc wc(x)·Δc / (Σc wc(x) + 1/R²),   wc(x) = 1/|x − xc|²
/// </code>
/// inverse-distance weights (Shepard, power 2) over every control, Δc the control's move (0 for one that stays), with
/// one more weight, 1/R², for "stay where it is". At a control the shift is that control's move exactly (its weight is
/// infinite); between two controls it goes monotonically from one's move to the other's; a free point far from every
/// control (beyond the reach R) hardly moves, so one control alone lifts a smooth bump (half its move at R), not the
/// whole terrain. The weights depend on the controls' places only, so the shift is linear in the moves: applying each
/// move's shift to the points as they are is the same as deriving every free point from its height when the cage was
/// set plus the interpolated offsets of the controls since, and moving a control back puts every point back. A free
/// point moved on its own simply keeps its own move. R: the cage's <c>cageReach</c> when set, else twice the mean
/// distance from a control to its nearest neighbour (half the survey's shorter side with one control).
/// </summary>
public static class Cage
{
    public const string Key = "cage";
    public const string ReachKey = "cageReach";
    const double Match = 1e-3;

    /// <summary>The controls' places in plan.</summary>
    public static List<Vec2> Of(Entity t) => PointLists.Xy(t.Attrs[Key]);

    /// <summary>The survey points that are controls, by index (one point per control, ascending).</summary>
    public static List<int> Indices(Entity t) => IndicesOf(Terrains.Points(t), Of(t));

    public static List<int> IndicesOf(IReadOnlyList<Vec3> pts, IReadOnlyList<Vec2> cage)
    {
        if (cage.Count == 0) return [];
        var byKey = new Dictionary<(long, long), int>();
        for (var i = 0; i < pts.Count; i++) byKey.TryAdd(KeyOf(pts[i].Xy), i);
        var found = new SortedSet<int>();
        foreach (var c in cage)
        {
            if (byKey.TryGetValue(KeyOf(c), out var i)) { found.Add(i); continue; }
            // Not on the millimetre grid's same cell: the nearest point within a millimetre.
            var best = -1; var bd = Match;
            for (var j = 0; j < pts.Count; j++) { var d = (pts[j].Xy - c).Length; if (d <= bd) { bd = d; best = j; } }
            if (best >= 0) found.Add(best);
        }
        return [.. found];
    }

    static (long, long) KeyOf(Vec2 p) => ((long)Math.Round(p.X / Match), (long)Math.Round(p.Y / Match));

    /// <summary>Writes the controls (the points at <paramref name="indices"/>) into <paramref name="attrs"/>; none: no cage.</summary>
    public static void Write(JsonObject attrs, IReadOnlyList<Vec3> pts, IEnumerable<int> indices)
    {
        var list = indices.Where(i => i >= 0 && i < pts.Count).Distinct().Order().Select(i => pts[i].Xy).ToList();
        if (list.Count > 0) attrs[Key] = Terrains.ContourJson(list); else attrs.Remove(Key);
    }

    /// <summary>Keeps only the controls that still stand on a point of <paramref name="pts"/> (and, given, inside <paramref name="within"/>).</summary>
    public static void Prune(JsonObject attrs, IReadOnlyList<Vec3> pts, IReadOnlyList<Vec2>? within = null)
    {
        if (attrs[Key] == null) return;
        var kept = IndicesOf(pts, PointLists.Xy(attrs[Key])).Where(i => within == null || Polygon2.Contains(within, pts[i].Xy));
        Write(attrs, pts, kept);
    }

    /// <summary>The cage's reach R (see the class).</summary>
    public static double Reach(IReadOnlyList<Vec2> controls, IReadOnlyList<Vec3> pts, double? set)
    {
        if (set is > 0) return set.Value;
        if (controls.Count >= 2)
        {
            var sum = 0.0;
            for (var i = 0; i < controls.Count; i++) sum += controls.Where((_, j) => j != i).Min(o => (o - controls[i]).Length);
            var mean = sum / controls.Count;
            if (mean > 1e-6) return 2 * mean;
        }
        if (pts.Count == 0) return 10;
        double w = pts.Max(p => p.X) - pts.Min(p => p.X), h = pts.Max(p => p.Y) - pts.Min(p => p.Y);
        return Math.Max(1, Math.Min(w, h) / 2);
    }

    /// <summary>The shift at <paramref name="at"/> of the controls' moves <paramref name="moves"/> (see the class).</summary>
    public static double Shift(IReadOnlyList<Vec2> controls, IReadOnlyList<double> moves, Vec2 at, double reach)
    {
        double num = 0, den = 1 / (reach * reach);
        for (var c = 0; c < controls.Count; c++)
        {
            var v = controls[c] - at;
            var d2 = Vec2.Dot(v, v);
            if (d2 < 1e-12) return moves[c];
            var w = 1 / d2;
            num += w * moves[c];
            den += w;
        }
        return num / den;
    }

    /// <summary>
    /// Moves the free points of <paramref name="pts"/> by the shift of the controls' moves (index → dz) on terrain
    /// <paramref name="t"/>; the points in <paramref name="fixedOnes"/> (those the command set itself) stay as given.
    /// Nothing happens when none of the moved points is a control.
    /// </summary>
    public static void Follow(Entity t, List<Vec3> pts, IReadOnlyDictionary<int, double> moves, ISet<int> fixedOnes)
    {
        var ctrl = IndicesOf(pts, Of(t));
        if (ctrl.Count == 0 || !moves.Keys.Any(ctrl.Contains)) return;
        var places = ctrl.Select(i => pts[i].Xy).ToList();
        var dz = ctrl.Select(i => moves.TryGetValue(i, out var m) ? m : 0).ToList();
        var reach = Reach(places, pts, t.Attrs[ReachKey] is { } r ? Json.Number(r) : null);
        var isCtrl = ctrl.ToHashSet();
        for (var i = 0; i < pts.Count; i++)
        {
            if (isCtrl.Contains(i) || fixedOnes.Contains(i)) continue;
            var s = Shift(places, dz, pts[i].Xy, reach);
            if (s != 0) pts[i] = pts[i] with { Z = pts[i].Z + s };
        }
    }

    /// <summary>One point per node of a plan grid of <paramref name="spacing"/> from the survey's lower-left corner: the point nearest the node (the lower index on a tie), so the corners of a regular survey are controls.</summary>
    public static List<int> Grid(IReadOnlyList<Vec3> pts, double spacing)
    {
        if (pts.Count == 0) return [];
        double x0 = pts.Min(p => p.X), y0 = pts.Min(p => p.Y);
        var best = new Dictionary<(long, long), (int I, double D)>();
        for (var i = 0; i < pts.Count; i++)
        {
            var (cx, cy) = ((long)Math.Round((pts[i].X - x0) / spacing), (long)Math.Round((pts[i].Y - y0) / spacing));
            var mid = new Vec2(x0 + cx * spacing, y0 + cy * spacing);
            var d = (pts[i].Xy - mid).Length;
            if (!best.TryGetValue((cx, cy), out var b) || d < b.D - 1e-9) best[(cx, cy)] = (i, d);
        }
        return [.. best.Values.Select(b => b.I).Order()];
    }

    /// <summary>The survey points on the terrain's outline (its triangulation's boundary, or the drawn boundary's corners' nearest points).</summary>
    public static List<int> Rim(Entity t, IReadOnlyList<Vec3> pts)
    {
        var loop = Terrains.BoundaryOutline(t) is { Count: >= 3 } drawn ? drawn.ToList() : Terrains.BoundaryLoop(Terrains.Of(t));
        var found = new SortedSet<int>();
        foreach (var v in loop)
        {
            var best = -1; var bd = double.MaxValue;
            for (var j = 0; j < pts.Count; j++) { var d = (pts[j].Xy - v).Length; if (d < bd) { bd = d; best = j; } }
            if (best >= 0) found.Add(best);
        }
        return [.. found];
    }
}
