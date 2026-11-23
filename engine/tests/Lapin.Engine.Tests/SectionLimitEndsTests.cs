using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Drawing.Import;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A plain section limited to its ends (decision 802, Î10 of docs/plan-site-grading.md): with <c>limitEnds</c> it cuts and
/// sees only the strip between its two ends, across its whole depth, so a 120 m terrain no longer dwarfs a 10 m section of a
/// wall; a new section has it, one saved before has none and draws as it did; a jogged section's pieces see only their
/// stretches either way; the sheet and the DXF print what the view shows.
/// </summary>
public sealed class SectionLimitEndsTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new CivilModule());

    /// <summary>A flat terrain 120 m long (x −60…60, y −20…20) and a 10 m wall along x at y = 3 (x 0…10), seen beyond a section along y = 0.</summary>
    static (string Terrain, string Wall) Site(Fixture f)
    {
        var terrain = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -60, ["y1"] = -20, ["x2"] = 60, ["y2"] = 20, ["elevation"] = -0.2 })!["id"]!;
        var wall = f.Wall(0, 3, 10, 3);
        return (terrain, wall);
    }

    /// <summary>A section from (−1, 0) to (11, 0), looking +y (12 m long: the drawing's x 0…12).</summary>
    static string Section(Fixture f, JsonObject? extra = null)
    {
        var args = new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, 0), ["end"] = new JsonArray(11, 0) };
        foreach (var (k, v) in extra ?? []) args[k] = v?.DeepClone();
        return (string)f.Run("view.create", args)!["id"]!;
    }

    static ViewDrawing Drawn(Fixture f, string id)
    {
        var (d, _, problem) = Views.Generate(f.Session.Reader, f.Model.Get(id)!);
        Assert.True(d != null, problem);
        return d!;
    }

    static (double Min, double Max) Xs(IEnumerable<Vec2> pts)
    {
        var l = pts.ToList();
        return (l.Min(p => p.X), l.Max(p => p.X));
    }

    static (double Min, double Max) CutXs(ViewDrawing d, string element) => Xs(d.Cuts.Where(c => c.Element == element).SelectMany(c => c.Loops).SelectMany(l => l));
    static (double Min, double Max) LineXs(ViewDrawing d) => Xs(d.Lines.SelectMany(l => new[] { l.A, l.B }));

    /// <summary>The x extent of a drawing's pens on the model's layers (cut, seen, beyond, hidden), from <c>view.drawing</c> or a sheet's viewport.</summary>
    static (double Min, double Max) PenXs(IEnumerable<JsonObject> pens)
    {
        var xs = new List<double>();
        foreach (var p in pens.Where(p => (string?)p["layer"] is "CUT" or "SEEN" or "BEYOND" or "HIDDEN"))
            if (p["segments"] is JsonArray s)
                for (var i = 0; i + 1 < s.Count; i += 2) xs.Add((double)s[i]!);
        Assert.NotEmpty(xs);
        return (xs.Min(), xs.Max());
    }

    [Fact]
    public void A_new_section_limits_its_ends_and_nothing_beyond_them_is_drawn()
    {
        var f = F();
        var (terrain, wall) = Site(f);
        var s = Section(f);
        Assert.True(f.Model.Get(s)!.Bool(SectionPaths.LimitEndsKey));
        var d = Drawn(f, s);
        // The terrain is cut only between the ends (x 0…12), not over its 120 m.
        var t = CutXs(d, terrain);
        Assert.Equal(0, t.Min, 3);
        Assert.Equal(12, t.Max, 3);
        // Every line lies between the ends; the wall seen beyond is whole (x 1…11).
        var all = LineXs(d);
        Assert.True(all.Min >= -1e-6 && all.Max <= 12 + 1e-6, $"{all}");
        var w = Xs(d.Lines.Where(l => l.Element == wall).SelectMany(l => new[] { l.A, l.B }));
        Assert.Equal(1, w.Min, 3);
        Assert.Equal(11, w.Max, 3);
    }

    [Fact]
    public void Without_the_option_the_section_draws_across_its_plane_as_before()
    {
        var f = F();
        var (terrain, _) = Site(f);
        var s = Section(f, new JsonObject { ["limitEnds"] = false });
        Assert.False(f.Model.Get(s)!.Attrs.ContainsKey(SectionPaths.LimitEndsKey));
        var t = CutXs(Drawn(f, s), terrain);
        // The whole terrain: x −60…60 is the drawing's −59…61.
        Assert.Equal(-59, t.Min, 3);
        Assert.Equal(61, t.Max, 3);
    }

    [Fact]
    public void A_section_saved_before_has_no_field_and_draws_exactly_as_one_without_the_option()
    {
        var f = F();
        var (terrain, _) = Site(f);
        var unlimited = Section(f, new JsonObject { ["limitEnds"] = false, ["name"] = "Old" });
        // As a project saved before the option holds it: the same section, no limitEnds at all.
        var old = new JsonObject { ["kind"] = "section", ["scale"] = 100, ["start"] = new JsonArray(-1, 0), ["end"] = new JsonArray(11, 0), ["pts"] = new JsonArray(new JsonArray(-1, 0), new JsonArray(11, 0)) };
        f.Session.Edit("Old section", e => e.Add(new Entity("old-section", Views.Class, "Old", old)));
        var view = f.Model.Get("old-section")!;
        Assert.False(SectionPaths.LimitsEnds(view));
        Assert.Equal(Drawn(f, unlimited).Cuts.Count, Drawn(f, "old-section").Cuts.Count);
        Assert.Equal(CutXs(Drawn(f, unlimited), terrain), CutXs(Drawn(f, "old-section"), terrain));
        var a = Views.Drawing(f.Session.Reader, f.Model.Get(unlimited)!);
        var b = Views.Drawing(f.Session.Reader, view);
        Assert.Equal(((JsonArray)a["pens"]!).ToJsonString(), ((JsonArray)b["pens"]!).ToJsonString());
        // Turned on in its settings: limited, one undo step; undone: as it was.
        var before = ((JsonArray)b["pens"]!).ToJsonString();
        f.Run("view.setLine", new JsonObject { ["id"] = "old-section", ["limitEnds"] = true });
        Assert.Equal(12, CutXs(Drawn(f, "old-section"), terrain).Max, 3);
        f.Session.Undo();
        Assert.False(SectionPaths.LimitsEnds(f.Model.Get("old-section")!));
        Assert.Equal(before, ((JsonArray)Views.Drawing(f.Session.Reader, f.Model.Get("old-section")!)["pens"]!).ToJsonString());
    }

    [Fact]
    public void The_grid_axes_and_other_sections_traces_stop_at_the_ends()
    {
        var f = F();
        Site(f);
        // Axis 1 at x = 5 (between the ends), axis 2 at x = 30 (beyond them); a cross section at x = 30 too.
        string Axis(double x) => (string)f.Run("drafting.addAxis", new JsonObject { ["x1"] = x, ["y1"] = -5, ["x2"] = x, ["y2"] = 12 })!["tag"]!;
        var one = Axis(5);
        var two = Axis(30);
        var s = Section(f);
        var cross = Section(f, new JsonObject { ["start"] = new JsonArray(30, -5), ["end"] = new JsonArray(30, 5), ["name"] = "Cross", ["limitEnds"] = false });
        double? GridX(string tag) => Views.Render(f.Session.Reader, f.Model.Get(s)!).Marks.FirstOrDefault(m => m.Owner?.EndsWith("#" + tag) == true) is { } m ? m.Lines[0].A.X : null;
        bool Trace() => Views.Render(f.Session.Reader, f.Model.Get(s)!).Marks.Any(m => m.Owner == cross);
        Assert.Equal(6, GridX(one)!.Value, 6);
        Assert.Null(GridX(two));
        Assert.False(Trace());
        // Without the option: the axis and the trace beyond the end are drawn, where the plane crosses them.
        f.Run("view.setLine", new JsonObject { ["id"] = s, ["limitEnds"] = false });
        Assert.Equal(31, GridX(two)!.Value, 6);
        Assert.True(Trace());
    }

    [Fact]
    public void A_jogged_section_sees_each_piece_between_its_ends_whatever_the_option()
    {
        var f = F();
        var (terrain, _) = Site(f);
        // Projected: along y = 0 from x 0 to 4, a jog to y = 1, on to x 10.
        var pts = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 1), new JsonArray(10, 1));
        var on = Section(f, new JsonObject { ["pts"] = pts.DeepClone() });
        var off = Section(f, new JsonObject { ["pts"] = pts.DeepClone(), ["limitEnds"] = false });
        var a = CutXs(Drawn(f, on), terrain);
        Assert.Equal(0, a.Min, 3);
        Assert.Equal(10, a.Max, 3);
        Assert.Equal(a, CutXs(Drawn(f, off), terrain));
        Assert.Equal(Drawn(f, on).Lines.Count, Drawn(f, off).Lines.Count);
    }

    [Fact]
    public void The_sheet_and_the_dxf_print_the_section_clipped_to_its_ends()
    {
        var f = F();
        Site(f);
        var s = Section(f);
        var open = Section(f, new JsonObject { ["limitEnds"] = false });
        // The view's drawing: model x 0…12 against the terrain's 120 m.
        var limited = PenXs(((JsonArray)Views.Drawing(f.Session.Reader, f.Model.Get(s)!)["pens"]!).OfType<JsonObject>());
        Assert.True(limited.Min >= -1e-6 && limited.Max <= 12 + 1e-6, $"{limited}");
        Assert.True(PenXs(((JsonArray)Views.Drawing(f.Session.Reader, f.Model.Get(open)!)["pens"]!).OfType<JsonObject>()) is var w && w.Max - w.Min > 100);
        // The DXF (in millimetres): as wide as the strip (plus the annotations by it), not the terrain.
        double Width(byte[] dxf) { var l = DxfReader.Read(dxf).Entities.OfType<DxfLine>().SelectMany(e => new[] { e.A.X, e.B.X }).ToList(); return l.Max() - l.Min(); }
        var dxfLimited = Width(Views.Dxf(f.Session.Reader, f.Model.Get(s)!));
        var dxfOpen = Width(Views.Dxf(f.Session.Reader, f.Model.Get(open)!));
        Assert.True(dxfLimited < 20_000, $"{dxfLimited}");
        Assert.True(dxfOpen > 100_000, $"{dxfOpen}");
        // On a sheet at 1:100: the viewport's model lines are 12 m = 120 mm wide, not 1200 mm.
        double Sheet(string view)
        {
            var sheet = (string)f.Run("sheet.create", new JsonObject { ["paper"] = "A1" })!["id"]!;
            var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = view, ["x"] = 400, ["y"] = 300 })!["viewportId"]!;
            var pens = ((JsonObject)f.Session.Query("sheet.drawing", new JsonObject { ["id"] = sheet })!)["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string?)p["viewport"] == vp);
            var x = PenXs(pens);
            return x.Max - x.Min;
        }
        Assert.Equal(120, Sheet(s), 0);
        Assert.True(Sheet(open) > 1000);
    }
}
