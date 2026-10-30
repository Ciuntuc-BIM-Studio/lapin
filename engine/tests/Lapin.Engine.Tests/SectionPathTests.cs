using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Sections along a polyline, an arc or an element (docs/sections.md, decision
/// 176): each straight piece a planar cut seen only between its ends, the
/// pieces laid side by side at their lengths (a developed section); the arc
/// sampled; the element's line read live, with its stations; what read a
/// section as one line (start, end, its look) still does.
/// </summary>
public sealed class SectionPathTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new CivilModule());

    static string Section(Fixture f, JsonObject args)
    {
        args["kind"] = "section";
        return (string)f.Run("view.create", args)!["id"]!;
    }

    static ViewDrawing Drawn(Fixture f, string id)
    {
        var (d, _, problem) = Views.Generate(f.Session.Reader, f.Model.Get(id)!);
        Assert.True(d != null, problem);
        return d!;
    }

    static (double Min, double Max) Xs(IEnumerable<Vec2> pts) => (pts.Min(p => p.X), pts.Max(p => p.X));

    [Fact]
    public void A_jogged_section_cuts_each_piece_between_its_ends_and_lays_them_side_by_side()
    {
        var f = F();
        // Two walls along y: one at x = 2 (y −1…3), one at x = 8 (y 5…9).
        var a = f.Wall(2, -1, 2, 3);
        var b = f.Wall(8, 5, 8, 9);
        // Three pieces: along +x at y = 1 (looking +y, through wall a), along +y at x = 4 (looking −x, wall a beyond), along +x at y = 7 (through wall b).
        var s = Section(f, new JsonObject { ["pts"] = new JsonArray(new JsonArray(0, 1), new JsonArray(4, 1), new JsonArray(4, 7), new JsonArray(10, 7)) });
        var v = f.Model.Get(s)!;
        Assert.Equal(new JsonArray(0, 1).ToJsonString(), v.Attrs["start"]!.ToJsonString());
        Assert.Equal(new JsonArray(10, 7).ToJsonString(), v.Attrs["end"]!.ToJsonString());
        Assert.True(SectionPaths.Developed(v));
        var d = Drawn(f, s);
        // Wall a cut by the first piece at x = 2, wall b by the third at 4 + 6 + (8 − 4) = 14; nothing by the second (each wall's layers as one).
        var cuts = d.Cuts.GroupBy(c => c.Element).ToDictionary(g => g.Key, g => Xs(g.SelectMany(c => c.Loops[0])));
        Assert.Equal(2, cuts.Count);
        Assert.Equal(2, (cuts[a].Min + cuts[a].Max) / 2, 1);
        Assert.Equal(14, (cuts[b].Min + cuts[b].Max) / 2, 1);
        // The second piece sees wall a beyond it, only along its own stretch (y 1…3 → x 4…6), never past its ends.
        var seen = d.Lines.Where(l => l.Element == a && l.Role == LineRole.Projection).ToList();
        Assert.NotEmpty(seen);
        Assert.All(seen, l => { Assert.InRange(l.A.X, 4 - 1e-6, 6 + 1e-6); Assert.InRange(l.B.X, 4 - 1e-6, 6 + 1e-6); });
        // Wall b lies behind the first piece and wall a behind the third: neither is drawn there.
        Assert.DoesNotContain(d.Lines, l => l.Element == b && l.A.X < 10 - 1e-6);
        Assert.DoesNotContain(d.Lines, l => l.Element == a && l.A.X > 10 + 1e-6);
        // The drawing tells where each piece lies: three frames over x 0…4, 4…10, 10…16.
        var json = Views.Drawing(f.Session.Reader, v);
        var frames = json["frames"]!.AsArray();
        Assert.Equal(3, frames.Count);
        Assert.Equal([0.0, 4, 10], frames.Select(x => (double)x!["x0"]!));
        Assert.Equal([4.0, 10, 16], frames.Select(x => (double)x!["x1"]!));
        Assert.Equal(new JsonArray(4, 1, 0).ToJsonString(), frames[1]!["origin"]!.ToJsonString());
        // The section looks left of its first piece.
        Assert.Equal(new Vec2(0, 1), Views.Look(v));
        // A plain two-point section through the same first piece draws wall a alone, cut the same.
        var plain = Section(f, new JsonObject { ["start"] = new JsonArray(0, 1), ["end"] = new JsonArray(4, 1) });
        var pd = Drawn(f, plain);
        var px = Xs(pd.Cuts.SelectMany(c => c.Loops[0]));
        Assert.All(pd.Cuts, c => Assert.Equal(a, c.Element));
        Assert.Equal(2, (px.Min + px.Max) / 2, 1);
        Assert.Null(Views.Drawing(f.Session.Reader, f.Model.Get(plain)!)["frames"]);
    }

    [Fact]
    public void The_plan_marks_a_jogged_section_along_its_whole_line_with_a_jog_line_in_the_section()
    {
        var f = F();
        f.Wall(2, -1, 2, 3);
        var s = Section(f, new JsonObject { ["pts"] = new JsonArray(new JsonArray(0, 1), new JsonArray(4, 1), new JsonArray(4, 7)), ["name"] = "J-J" });
        var plan = Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!);
        var mark = Assert.Single(plan.Marks, m => m.Owner == s);
        Assert.Equal(3, mark.Snaps.Count);
        Assert.Contains(mark.Lines, l => l.A == new Vec2(0, 1) && l.B == new Vec2(4, 1));
        Assert.Contains(mark.Lines, l => l.A == new Vec2(4, 1) && l.B == new Vec2(4, 7));
        Assert.Equal(2, mark.Texts.Count(t => t.Text.Text == "J-J"));
        // In the section itself, a dashed line down the jog at x = 4.
        var section = Views.Render(f.Session.Reader, f.Model.Get(s)!);
        Assert.Contains(section.Marks, m => m.Owner == null && m.Lines.Any(l => l.Layer == "SECTION" && Math.Abs(l.A.X - 4) < 1e-9 && Math.Abs(l.B.X - 4) < 1e-9 && l.Dash != null));
    }

    [Fact]
    public void A_curved_section_is_the_arc_through_its_three_points_sampled_to_pieces()
    {
        var f = F();
        f.Wall(5, -1, 5, 10);
        // A half circle of radius 5, from (0, 0) through (5, 5) to (10, 0): its length π·5, seen from inside (looking left: towards the centre).
        var s = Section(f, new JsonObject { ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(5, 5), new JsonArray(10, 0)), ["curve"] = "arc" });
        var v = f.Model.Get(s)!;
        var (path, _, _) = SectionPaths.Resolve(f.Model, f.Session.Reader.Services, v);
        Assert.NotNull(path);
        Assert.Equal(25, path!.Count);
        Assert.Equal(Math.PI * 5, SectionPaths.Length(path), 0.05);
        Assert.All(path, p => Assert.Equal(5, (p - new Vec2(5, 0)).Length, 6));
        // The wall at x = 5 is cut where the arc crosses it, at the top of the arc: a quarter of the way round, x ≈ π·5/2.
        var d = Drawn(f, s);
        var cx = Xs(d.Cuts.SelectMany(c => c.Loops[0]));
        Assert.Equal(Math.PI * 5 / 2, (cx.Min + cx.Max) / 2, 0.1);
        Assert.Throws<CommandException>(() => Section(f, new JsonObject { ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["curve"] = "arc" }));
    }

    [Fact]
    public void A_section_along_an_element_follows_its_line_as_it_is_now_and_writes_the_stations()
    {
        var f = F();
        var wall = f.Wall(0, 0, 12, 0);
        f.Wall(6, -3, 6, 3);
        var s = Section(f, new JsonObject { ["along"] = wall });
        var v = f.Model.Get(s)!;
        Assert.Equal([new Vec2(0, 0), new Vec2(12, 0)], SectionPaths.Points(v));
        // The wall moved: the section goes with it.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["x2"] = 20 });
        var (path, _, _) = SectionPaths.Resolve(f.Model, f.Session.Reader.Services, f.Model.Get(s)!);
        Assert.Equal(20, path![^1].X, 6);
        // Stations every 10 m at 1:100 (a tenth of the scale), from 0+000 to the line's end.
        var texts = Views.Render(f.Session.Reader, f.Model.Get(s)!).Marks.Where(m => m.Owner == null).SelectMany(m => m.Texts).Select(t => t.Text.Text).ToList();
        Assert.Contains("0+000", texts);
        Assert.Contains("0+010", texts);
        Assert.Contains("0+020", texts);
        Assert.DoesNotContain("0+030", texts);
        // Set to 5 m, then none.
        f.Run("view.setLine", new JsonObject { ["id"] = s, ["stations"] = 5 });
        Assert.Contains("0+015", Views.Render(f.Session.Reader, f.Model.Get(s)!).Marks.SelectMany(m => m.Texts).Select(t => t.Text.Text));
        f.Run("view.setLine", new JsonObject { ["id"] = s, ["stations"] = 0, ["along"] = null });
        Assert.Null(SectionPaths.Along(f.Model.Get(s)!));
        Assert.DoesNotContain("0+010", Views.Render(f.Session.Reader, f.Model.Get(s)!).Marks.SelectMany(m => m.Texts).Select(t => t.Text.Text));
        // A road's axis from the site: followed from the station it starts at.
        var road = (string)f.Run("civil.importGeoJson", new JsonObject
        {
            ["storeyId"] = f.Storey, ["crs"] = "EPSG:3844",
            ["text"] = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"name":"DJ 1"},"geometry":{"type":"LineString","coordinates":[[0,0],[30,0],[30,40]]}}]}""",
        })!["ids"]![0]!;
        var along = Section(f, new JsonObject { ["along"] = road });
        var (rp, _, _) = SectionPaths.Resolve(f.Model, f.Session.Reader.Services, f.Model.Get(along)!);
        Assert.Equal(3, rp!.Count);
        Assert.Equal(70, SectionPaths.Length(rp), 6);
        Assert.Throws<CommandException>(() => Section(f, new JsonObject { ["along"] = f.Storey }));
    }

    [Fact]
    public void A_section_moved_as_one_line_still_works_and_its_stations_read_as_a_road_writes_them()
    {
        var f = F();
        var s = Section(f, new JsonObject { ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(4, 0) });
        f.Run("view.setLine", new JsonObject { ["id"] = s, ["end"] = new JsonArray(0, 4) });
        var v = f.Model.Get(s)!;
        Assert.Equal([new Vec2(0, 0), new Vec2(0, 4)], SectionPaths.Points(v));
        Assert.False(SectionPaths.Developed(v));
        Assert.Equal(new Vec2(-1, 0), Views.Look(v));
        Assert.Equal("0+000", SectionPaths.Station(0));
        Assert.Equal("0+020", SectionPaths.Station(20));
        Assert.Equal("1+250.5", SectionPaths.Station(1250.5));
        Assert.Equal("12+000", SectionPaths.Station(12000));
    }

    [Fact]
    public void A_piece_sees_only_between_its_ends()
    {
        // A box 4 wide (x 0…4) beyond a section plane at y = −1 looking +y; the piece reaches x 1…3 only.
        var box = Prism.Box(new Vec3(2, 1, 1), 4, 2, 2)!;
        var frame = ViewFrame.Horizontal(new Vec3(0, -1, 0), new Vec2(0, 1));
        var (lines, _, _) = HiddenLines.Draw([(box, 0)], frame, cut: true, span: (1, 3));
        Assert.NotEmpty(lines);
        Assert.All(lines, l => { Assert.InRange(l.A.X, 1 - 1e-9, 3 + 1e-9); Assert.InRange(l.B.X, 1 - 1e-9, 3 + 1e-9); });
        // Cut through it (the plane at y = 1): the cut area clipped to the piece.
        var through = ViewFrame.Horizontal(new Vec3(0, 1, 0), new Vec2(0, 1));
        var (_, cuts, _) = HiddenLines.Draw([(box, 0)], through, cut: true, span: (1, 3));
        var loop = Assert.Single(Assert.Single(cuts).Loops);
        Assert.Equal((1.0, 3.0), Xs(loop));
        // A solid wholly outside the piece is left out.
        Assert.Empty(HiddenLines.Draw([(box, 0)], through, cut: true, span: (5, 8)).Cuts);
    }
}
