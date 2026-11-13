using System.Text;
using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// Beams of different depths meeting a column (docs/plan-fem.md §9.9, from §9.8's check): each beam on its own axis' level, the
/// column one node at the deepest beam's level and the other beams joined to it by a rigid vertical arm — no column piece 25 mm
/// long between the beams' axes, no beam tilting 5 cm over 5 m; the frame balanced; the JG export and the view for IFC writing
/// the arms. The regression found by fem-check on space-frame-rect (12.11.2026): the column's head was at the shallow diagonal's
/// level and the deep beams hung 10 cm under it, their own ends on the column's axis, where a reader of the SAV export joins
/// them to the column (and loses a release there). Now no arm-joined end lies on a column, and a bar on an arm with releases
/// at its own ends solves as the same frame with the arm drawn as a very stiff bar.
/// </summary>
public class BeamLevelTests
{
    /// <summary>The cross-check's space-frame-rect: four columns 30 × 30 (3 m), beams 30 × 50 round 5 × 4 m and a diagonal 20 × 30 pinned at both ends, their tops flush, one beam hinged about its strong axis at (5, 4).</summary>
    static (Fixture F, string Diagonal) SpaceFrame()
    {
        var f = LoadCaseTests.F();
        string Type(string kind, string name, double w, double d) => (string)f.Run(kind == "column" ? "structure.createColumnType" : "structure.createBeamType", new JsonObject
        {
            ["name"] = name, ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = w, ["depth"] = d }, ["material"] = AnalysisRunTests.Concrete,
        })!["id"]!;
        var col = Type("column", "C30x30", 0.3, 0.3);
        foreach (var (x, y) in new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 4.0), (0.0, 4.0) })
        {
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = col });
            f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y });
        }
        var bt = Type("beam", "B30x50", 0.3, 0.5);
        string Beam(double x1, double y1, double x2, double y2, string type) =>
            (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type })!["id"]!;
        Beam(0, 0, 5, 0, bt); var side = Beam(5, 0, 5, 4, bt); Beam(5, 4, 0, 4, bt); Beam(0, 4, 0, 0, bt);
        var diagonal = Beam(0, 0, 5, 4, Type("beam", "B20x30", 0.2, 0.3));
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(diagonal), ["i"] = "pinned", ["j"] = "pinned" });
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(side), ["j"] = new JsonArray(false, false, false, false, false, true) });
        f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = f.Storey, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(5, 0), ["z"] = 2.75, ["value"] = 8, ["caseId"] = LoadCaseTests.IdOf(f, "Q-A") });
        return (f, diagonal);
    }

    static double DistToSegment(Vec3 p, Vec3 a, Vec3 b)
    {
        var ab = b - a;
        var t = Math.Clamp(Vec3.Dot(p - a, ab) / Vec3.Dot(ab, ab), 0, 1);
        return (a + ab * t - p).Length;
    }

    [Fact]
    public void Beams_of_different_depths_keep_their_levels_and_meet_the_column_by_an_arm()
    {
        var (f, _) = SpaceFrame();
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        // No sliver of column, every beam level.
        Assert.All(am.Frames, x => Assert.True((x.J.At + x.OffJ - x.I.At - x.OffI).Length > 0.5, $"{x.Source.Name}: {(x.J.At - x.I.At).Length} m"));
        foreach (var b in am.Frames.Where(x => x.Source.Kind == "beam"))
            Assert.Equal((b.I.At + b.OffI).Z, (b.J.At + b.OffJ).Z, 9);
        // Every column's head at the deep beams' axis (3 − 0.25); the shallow diagonal (axis 3 − 0.15) joined to it by an arm 10 cm up.
        var columns = am.Frames.Where(x => x.Source.Section.Name == "C30x30").ToList();
        Assert.Equal(4, columns.Count);
        Assert.All(columns, c => Assert.Equal(2.75, Math.Max(c.I.At.Z, c.J.At.Z), 6));
        Assert.All(am.Frames.Where(x => x.Source.Section.Name == "B30x50"), b => Assert.False(b.Eccentric));
        var diag = am.Frames.Single(x => x.Source.Section.Name == "B20x30");
        Assert.Equal(2.75, diag.I.At.Z, 6);
        Assert.Equal(2.75, diag.J.At.Z, 6);
        foreach (var o in new[] { diag.OffI, diag.OffJ }) { Assert.Equal(0.1, o.Z, 6); Assert.Equal(0, o.X, 9); Assert.Equal(0, o.Y, 9); }
        Assert.Equal(2, am.LevelJoins);
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        Assert.All(run["cases"]!.AsArray(), c => Assert.True((bool)(c!["balanced"] ?? true)));
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("2 beam ends of another depth meet a column at its joint"));
    }

    [Fact]
    public void No_end_joined_by_an_arm_lies_on_a_column()
    {
        // The regression: a bar's own end on a column's axis (between its ends or at one) is read by the SAV export's readers as
        // joined to the column there, whatever the arm and the end's releases say.
        var (f, _) = SpaceFrame();
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        var columns = am.Frames.Where(x => x.Source.Kind == "column").ToList();
        Assert.NotEmpty(columns);
        foreach (var b in am.Frames.Where(x => x.Eccentric))
            foreach (var (n, off) in new[] { (b.I, b.OffI), (b.J, b.OffJ) })
            {
                if (off.Length < 1e-9) continue;
                var end = n.At + off;
                Assert.All(columns, c => Assert.True(DistToSegment(end, c.I.At + c.OffI, c.J.At + c.OffJ) > 0.001,
                    $"{b.Source.Name}'s end {end} lies on {c.Source.Name}"));
            }
    }

    [Fact]
    public void The_diagonal_pinned_on_its_arms_carries_a_central_force_as_a_simply_supported_beam()
    {
        // 15 kN in the middle of the diagonal pinned at both its own ends (on the arms): V = P/2 at each end, no moment at the ends,
        // PL/4 in the middle; the frame takes the 15 kN.
        var (f, diagonal) = SpaceFrame();
        f.Run("analysis.addPointLoad", new JsonObject
        {
            ["storeyId"] = f.Storey, ["at"] = new JsonArray(2.5, 2), ["z"] = 2.85, ["value"] = 15, ["direction"] = "down", ["caseId"] = LoadCaseTests.IdOf(f, "G2"),
        });
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        var r = f.Session.Query("analysis.results", new JsonObject { ["case"] = "G2" })!.AsObject();
        Assert.Equal(15, r["reactions"]!.AsArray().Sum(x => (double)x!["f"]![2]!), 6);
        var d = r["frames"]!.AsArray().Single(x => (string)x!["sourceId"]! == diagonal)!;
        var L = Math.Sqrt(41);
        Assert.Equal(L, (double)d["length"]!, 6);
        var st = d["stations"]!.AsArray();
        Assert.Equal(7.5, Math.Abs((double)st[0]!["Vy"]!), 6);
        Assert.Equal(7.5, Math.Abs((double)st[^1]!["Vy"]!), 6);
        Assert.Equal(0, (double)st[0]!["Mz"]!, 6);
        Assert.Equal(0, (double)st[^1]!["Mz"]!, 6);
        Assert.Equal(15 * L / 4, (double)d["extremes"]!["Mz"]!["max"]!, 4);
    }

    // The frame at the solver: the bars' own E, G, A, I, J (kPa, m², m⁴) of C30/37 rectangles.
    const double Ec = 31e6, Gc = 31e6 / 2.4;
    static FrameElement Rect(int i, int j, double w, double d, Vec3 local2, Vec3 offI = default, Vec3 offJ = default, bool[]? relI = null, bool[]? relJ = null)
    {
        var (a, b) = (Math.Max(w, d), Math.Min(w, d));
        var jt = a * b * b * b * (1.0 / 3 - 0.21 * b / a * (1 - b * b * b * b / (12 * a * a * a * a)));
        return new()
        {
            Id = $"b{i}-{j}", I = i, J = j, Local2 = local2, E = Ec, G = Gc, A = w * d, I22 = d * w * w * w / 12, I33 = w * d * d * d / 12, Jt = jt,
            OffI = offI, OffJ = offJ, ReleaseI = relI ?? new bool[6], ReleaseJ = relJ ?? new bool[6],
        };
    }

    /// <summary>
    /// space-frame-rect at the solver, both ways §9.9's arms ever joined it: <paramref name="deepOnArms"/> the columns' heads at the
    /// diagonal's level (2.85) at its two corners, the deep beams on arms 10 cm down, one of them hinged at its own end there; else
    /// the heads at the deep beams' level (2.75) and the diagonal on arms 10 cm up. <paramref name="explicitArms"/> draws each arm
    /// as a bar 10⁴ times as stiff as the diagonal's section in every way instead.
    /// </summary>
    static (FrameProblem P, int Diagonal, int Side) Frame(bool deepOnArms, bool explicitArms)
    {
        var p = new FrameProblem();
        bool[] fixd = [true, true, true, true, true, true];
        var corners = new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 4.0), (0.0, 4.0) };
        var head = new int[4];
        for (var k = 0; k < 4; k++) p.AddNode(new Vec3(corners[k].Item1, corners[k].Item2, 0), $"F{k}", fixd);
        for (var k = 0; k < 4; k++)
        {
            var z = deepOnArms && (k == 0 || k == 2) ? 2.85 : 2.75;
            head[k] = p.AddNode(new Vec3(corners[k].Item1, corners[k].Item2, z), $"H{k}");
            p.Elements.Add(Rect(k, head[k], 0.3, 0.3, new Vec3(0, 1, 0)));
        }
        var arm = new Dictionary<int, int>();
        // The end of a bar at a corner: its node and offset, or the explicit arm's far node.
        (int Node, Vec3 Off) End(int k, double z)
        {
            var off = new Vec3(0, 0, z - p.Nodes[head[k]].Z);
            if (off.Length < 1e-9) return (head[k], default);
            if (!explicitArms) return (head[k], off);
            if (!arm.TryGetValue(k, out var n))
            {
                n = arm[k] = p.AddNode(p.Nodes[head[k]] + off, $"A{k}");
                var big = Rect(head[k], n, 0.2, 0.3, new Vec3(0, 1, 0));
                p.Elements.Add(new FrameElement
                {
                    Id = $"arm{k}", I = head[k], J = n, Local2 = new Vec3(0, 1, 0), E = Ec, G = Gc, A = big.A * 1e4,
                    I22 = 1e4 * Math.Max(big.I33, big.Jt), I33 = 1e4 * Math.Max(big.I33, big.Jt), Jt = 1e4 * Math.Max(big.I33, big.Jt),
                });
            }
            return (n, default);
        }
        bool[] none = new bool[6], strong = [false, false, false, false, false, true];
        int side = -1, first = -1;
        foreach (var (a, b) in new[] { (0, 1), (1, 2), (2, 3), (3, 0) })
        {
            var (ei, ej) = (End(a, 2.75), End(b, 2.75));
            if ((a, b) == (1, 2)) side = p.Elements.Count;
            if ((a, b) == (0, 1)) first = p.Elements.Count;
            p.Elements.Add(Rect(ei.Node, ej.Node, 0.3, 0.5, Vec3.UnitZ, ei.Off, ej.Off, none, (a, b) == (1, 2) ? strong : none));
        }
        var (di, dj) = (End(0, 2.85), End(2, 2.85));
        var diagonal = p.Elements.Count;
        p.Elements.Add(Rect(di.Node, dj.Node, 0.2, 0.3, Vec3.UnitZ, di.Off, dj.Off, [false, false, false, false, true, true], [false, false, false, true, true, true]));
        var c = new CaseLoads("P");
        c.Span.Add((diagonal, SpanLoad.Force(Math.Sqrt(41) / 2, new Vec3(0, 0, -15))));
        c.Span.Add((first, SpanLoad.Uniform(0, 5, new Vec3(0, 0, -8))));
        c.Span.Add((side, SpanLoad.Uniform(0, 4, new Vec3(0, 2, 0))));
        c.AddNodal(head[2], new Vec3(8, 0, 0));
        c.AddNodal(head[3], new Vec3(0, -6, 0));
        p.Cases.Add(c);
        return (p, diagonal, side);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_bar_on_rigid_arms_with_releases_at_its_own_ends_solves_as_the_frame_with_stiff_arm_bars(bool deepOnArms)
    {
        var (rigid, diagonal, side) = Frame(deepOnArms, explicitArms: false);
        var (stiff, diagonalS, sideS) = Frame(deepOnArms, explicitArms: true);
        var a = FrameSolver.Solve(rigid);
        var b = FrameSolver.Solve(stiff);
        Assert.True(a.Ok, a.Mechanism is { } ma ? $"rigid: mechanism at {ma.Node} {FrameSolver.DofName(ma.Dof)}" : a.Refused);
        Assert.True(b.Ok, b.Mechanism is { } mb ? $"stiff: mechanism at {mb.Node} {FrameSolver.DofName(mb.Dof)}" : b.Refused);
        var (ra, rb) = (a.Cases[0], b.Cases[0]);
        Assert.True(ra.Balance < 1e-9 && rb.Balance < 1e-9, $"balance {ra.Balance} {rb.Balance}");
        // The feet's reactions and the heads' translations (the nodes both problems share, in the same order).
        var rmax = Enumerable.Range(0, 4).Max(k => Math.Abs(ra.Reactions[k].Take(3).Max(Math.Abs)));
        for (var k = 0; k < 4; k++)
            for (var d = 0; d < 6; d++)
                Assert.True(Math.Abs(ra.Reactions[k][d] - rb.Reactions[k][d]) < 1e-5 * rmax, $"foot {k} dof {d}: {ra.Reactions[k][d]} against {rb.Reactions[k][d]}");
        var umax = Enumerable.Range(4, 4).Max(n => Math.Abs(ra.U[n * 6 + 2]) + Math.Abs(ra.U[n * 6]) + Math.Abs(ra.U[n * 6 + 1]));
        for (var n = 4; n < 8; n++)
            for (var d = 0; d < 3; d++)
                Assert.True(Math.Abs(ra.U[n * 6 + d] - rb.U[n * 6 + d]) < 1e-5 * umax, $"head {n - 4} dof {d}");
        // The bars' own end forces: the released ones nothing at their own ends, as with the arms drawn.
        foreach (var (e, es) in new[] { (diagonal, diagonalS), (side, sideS) })
            for (var d = 0; d < 12; d++)
                Assert.True(Math.Abs(ra.Elements[e].Forces[d] - rb.Elements[es].Forces[d]) < 1e-5 * 15, $"bar {e} end force {d}: {ra.Elements[e].Forces[d]} against {rb.Elements[es].Forces[d]}");
        Assert.Equal(0, ra.Elements[diagonal].Forces[4], 9);
        Assert.Equal(0, ra.Elements[diagonal].Forces[5], 9);
        Assert.Equal(0, ra.Elements[diagonal].Forces[11], 9);
        Assert.Equal(0, ra.Elements[side].Forces[11], 9);
    }

    [Fact]
    public void The_jg_export_and_the_view_for_ifc_write_the_arms()
    {
        var (f, _) = SpaceFrame();
        var r = f.Session.Query("jg.export", new JsonObject { ["units"] = "contract" })!;
        var doc = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String((string)r["data"]!)))!.AsObject();
        Assert.Empty(JgValidator.Validate(doc));
        Assert.Contains(doc["frames"]!.AsArray(), x => x!["end_offsets"] is JsonObject or JsonArray);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "frame.ifc", new IfcExportOptions(Structural: true));
        var step = StepFile.Read(ms.ToArray());
        Assert.Equal(2, step.OfType("IFCRELCONNECTSWITHECCENTRICITY").Count());
        // In the member's axes (x along it, z its Axis = local 2, upward for a level beam): 10 cm up.
        Assert.All(step.OfType("IFCCONNECTIONPOINTECCENTRICITY"), e => Assert.Equal(0.1, Convert.ToDouble(e[4]), 6));
    }
}
