using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// Where a run rises or drops inside itself (its plane changed while it was
/// drawn: a point with two levels, <see cref="Runs.PointLevel"/>): the plan
/// shows the rise / drop node's sign there, so the vertical leg is read in
/// plan as one placed by hand is. Its jogs to the ports at its ends are not
/// marked (the node there is).
/// </summary>
public static partial class Runs
{
    /// <summary>The vertical legs of a centreline between its ends: the point in plan, and whether the run goes up there (in its direction).</summary>
    public static IEnumerable<(Vec2 At, bool Up)> Rises(IReadOnlyList<Vec3> line)
    {
        for (var i = 1; i + 2 < line.Count; i++)
            if ((line[i + 1].Xy - line[i].Xy).Length < 1e-9 && Math.Abs(line[i + 1].Z - line[i].Z) > 1e-6)
                yield return (line[i].Xy, line[i + 1].Z > line[i].Z);
    }

    /// <summary>ISO 4067, as the rise node draws it: a ring; a dot where the run rises, a cross where it drops.</summary>
    internal static IEnumerable<PlanItem> RiseMarks(IReadOnlyList<Vec3> line)
    {
        foreach (var (at, up) in Rises(line))
        {
            // Sized on paper, as the rise node's sign is.
            var paper = new PaperAnchor(at, Nodes.SymbolScale);
            yield return new PlanItem("symbol", true, [.. Chords.Ring(at, 0.06)]) { Paper = paper };
            if (up) yield return new PlanItem("heavy", true, [.. Chords.Ring(at, 0.015)]) { Paper = paper };
            else
            {
                const double c = 0.042;
                yield return new PlanItem("symbol", false, [at + new Vec2(-c, -c), at + new Vec2(c, c)]) { Paper = paper };
                yield return new PlanItem("symbol", false, [at + new Vec2(-c, c), at + new Vec2(c, -c)]) { Paper = paper };
            }
        }
    }

    /// <summary>
    /// Points laid by the material with their levels: piece by piece between
    /// the points where the run rises, each piece starting where the one
    /// before was laid to, so a corner the material cuts (PVC turning 45° at
    /// most) is cut before or after a rise, never across it, and the vertical
    /// leg stays vertical. The levels of each piece carried onto its laid
    /// points. No rises: laid whole, as before.
    /// </summary>
    public static (List<Vec2> Points, JsonArray? Levels) LayWithLevels(string service, string? material, IReadOnlyList<Vec2> pts, IReadOnlyList<PointLevel>? levels, double baseLevel)
    {
        if (levels == null || levels.Count != pts.Count)
        {
            var whole = Lay(service, material, pts);
            return (whole, null);
        }
        var cuts = Enumerable.Range(1, Math.Max(0, pts.Count - 2)).Where(i => levels[i].Rises).ToList();
        if (cuts.Count == 0)
        {
            var whole = Lay(service, material, pts);
            return (whole, whole.Count == pts.Count ? LevelsJson(levels) : RemapLevels(pts, levels, whole, baseLevel));
        }
        var outPts = new List<Vec2>();
        var outLv = new List<PointLevel>();
        var bounds = new List<int> { 0 };
        bounds.AddRange(cuts);
        bounds.Add(pts.Count - 1);
        for (var k = 0; k + 1 < bounds.Count; k++)
        {
            int a = bounds[k], b = bounds[k + 1];
            var piece = pts.Skip(a).Take(b - a + 1).ToList();
            if (outPts.Count > 0) piece[0] = outPts[^1];
            // The piece's own levels: at a rise it leaves with the rise's out and arrives with its in.
            var pl = levels.Skip(a).Take(b - a + 1).ToList();
            if (k > 0) pl[0] = PointLevel.At(levels[a].Out);
            if (k + 2 < bounds.Count) pl[^1] = PointLevel.At(levels[b].In);
            var laid = Lay(service, material, piece);
            if (laid.Count < 2) laid = piece;
            var lv = laid.Count == piece.Count ? pl : (RemapLevels(piece, pl, laid, baseLevel) is { } j ? [.. j.Select(LevelOf)] : [.. laid.Select(_ => PointLevel.At(null))]);
            if (outPts.Count == 0) { outPts.AddRange(laid); outLv.AddRange(lv); continue; }
            // The rise: the point the piece before ended on, arriving with its level and leaving with this piece's.
            outLv[^1] = new PointLevel(outLv[^1].In, lv[0].Out);
            outPts.AddRange(laid.Skip(1));
            outLv.AddRange(lv.Skip(1));
        }
        return (outPts, LevelsJson(outLv));
    }

    /// <summary>
    /// A path drawn again with the heights of its legs (Redraw path: an
    /// elevation where its plane was changed while drawing, null before any
    /// change): the old run's levels carried onto the new points where they
    /// are not changed, and where a changed leg starts the run leaves at its
    /// elevation from the level it arrives with there (a rise).
    /// </summary>
    public static JsonArray? LegLevels(IModelView m, Entity run, IReadOnlyList<Vec2> pts, JsonArray legs)
    {
        if (legs.Count != pts.Count - 1) throw new CommandException($"One elevation (or null) per leg ({pts.Count - 1}).");
        var s = SettingOf(m, run);
        var carried = RemapLevels(Points(run), PointLevelsOf(run), pts, s.Elevation) is { } c ? c.Select(LevelOf).ToList() : [.. pts.Select(_ => PointLevel.At(null))];
        for (var i = 0; i < legs.Count; i++)
        {
            if (Json.Number(legs[i]) is not { } z) continue;
            carried[i] = new PointLevel(i == 0 ? z : carried[i].In, z);
            // It arrives at the leg's end at its elevation; it leaves there as the next leg says (its old level when that one is not changed).
            carried[i + 1] = new PointLevel(z, i + 1 == pts.Count - 1 ? z : carried[i + 1].Out);
        }
        // A pair of one height is one level (LevelsJson): no rise where the run leaves at the level it arrives with.
        return LevelsJson(carried);
    }

    static void RegisterRises(IModuleRegistry r)
    {
        // One point's level set (a handle of the profile dragged): its in, its out, or both (side null); null gives it back the run's.
        r.Command("setPointLevel", "Run elevation", (ctx, a) =>
        {
            var run = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which run?");
            if (!IsRun(run)) throw new CommandException("Not a run.");
            var n = Points(run).Count;
            var i = (int?)Json.Number(a["index"]) ?? throw new CommandException("Which point?");
            if (i < 0 || i >= n) throw new CommandException($"The run has {n} points.");
            var side = (string?)a["side"];
            if (side is not (null or "in" or "out")) throw new CommandException("The side is in, out or none.");
            var levels = PointLevelsOf(run) ?? [.. Enumerable.Range(0, n).Select(_ => PointLevel.At(null))];
            var z = Json.Number(a["level"]);
            var was = levels[i];
            levels[i] = side switch { "in" => was with { In = z }, "out" => was with { Out = z }, _ => PointLevel.At(z) };
            var attrs = run.CloneAttrs();
            if (LevelsJson(levels) is { } lv) attrs["levels"] = lv; else attrs.Remove("levels");
            ctx.Replace(run.With(attrs: attrs));
            return null;
        });
    }
}
