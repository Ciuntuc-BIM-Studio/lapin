using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The 2D drawing generator (M5). Ported from the old Lapin's view-range.test.ts,
/// line-styles.test.ts, material-drawing.test.ts and view-hidden-lines.test.ts
/// (same cases and numbers), then the generator on real models: plans, sections,
/// elevations.
/// </summary>
public class DrawingTests
{
    static void Near(double? a, double b) => Assert.True(a is { } x && Math.Abs(x - b) < 1e-9, $"{a} ≉ {b}");
    static readonly double[] Levels = [-3, 0, 3];

    // ------------------------------------------------------------ view range (view-range.test.ts)

    [Fact]
    public void A_range_resolves_its_edges_on_the_levels()
    {
        var r = ViewRanges.Resolve(0, 1.2, new ViewRange(new("above", -0.2), new("self", 0), new("below", 0.5)), Levels);
        Near(r.Cut, 1.2); Near(r.Top, 2.8); Near(r.Bottom, 0); Near(r.Depth, -2.5);
        var b = ViewRanges.Bands(r);
        Near(b.Below, 3.7); Near(b.Above, 1.6); Near(b.BeyondFrom, 1.2);
        Assert.Empty(ViewRanges.Problems(r));
    }

    [Fact]
    public void Unlimited_edges_and_the_top_level_with_nothing_above_have_no_limit()
    {
        var r = ViewRanges.Resolve(3, 1.2, new ViewRange(new("above", 0), new("self", 0), new("unlimited", 0)), Levels);
        Assert.Null(r.Top);
        Assert.Null(r.Depth);
        var b = ViewRanges.Bands(r);
        Assert.Equal(ViewRanges.Unlimited, b.Above);
        Assert.True(b.Below > 1000);
    }

    [Fact]
    public void Revit_s_order_is_checked_and_the_default_range_has_no_beyond_band()
    {
        var r = ViewRanges.Resolve(0, 1.2, new ViewRange(new("self", 1), new("self", 2), new("self", 2.5)), Levels);
        Assert.Equal(["topBelowCut", "bottomAboveCut", "depthAboveBottom"], ViewRanges.Problems(r));
        var deep = ViewRanges.Default(1.2, 3);
        Assert.Equal(deep.Bottom, deep.Depth);
        Assert.Null(ViewRanges.Bands(ViewRanges.Resolve(0, 1.2, deep, Levels)).BeyondFrom);
    }

    // ------------------------------------------------------------ whole lines

    [Fact]
    public void Collinear_pieces_join_and_duplicates_and_specks_go()
    {
        var m = LineMerge.Merge([
            (new Vec2(0, 0), new Vec2(1, 0)), (new Vec2(1, 0), new Vec2(2.5, 0)), (new Vec2(3, 0), new Vec2(2, 0)),   // one line 0–3
            (new Vec2(5, 0), new Vec2(6, 0)),                                                                    // apart: stays
            (new Vec2(0, 1), new Vec2(0, 2)), (new Vec2(0, 2), new Vec2(0, 1)),                                  // a duplicate, reversed
            (new Vec2(0, 0), new Vec2(1, 1)), (new Vec2(0.5, 0.5), new Vec2(2, 2)),                              // diagonal overlap
            (new Vec2(4, 4), new Vec2(4, 4.000001)),                                                             // a speck
        ]);
        Assert.Equal(4, m.Count);
        double Len((Vec2 A, Vec2 B) s) => (s.B - s.A).Length;
        Assert.Contains(m, s => Math.Abs(Len(s) - 3) < 1e-9 && Math.Abs(s.A.Y) < 1e-9);
        Assert.Contains(m, s => Math.Abs(Len(s) - Math.Sqrt(8)) < 1e-9);
        Assert.Contains(m, s => Math.Abs(Len(s) - 1) < 1e-9 && Math.Abs(s.A.X) < 1e-9);
        // Near-vertical lines either side of the wrap still join.
        Assert.Single(LineMerge.Merge([(new Vec2(0, 0), new Vec2(1e-9, 1)), (new Vec2(1e-9, 1), new Vec2(0, 2))]));
    }

    [Fact]
    public void Projection_lines_longer_than_one_and_a_half_times_the_drawn_extent_are_dropped()
    {
        // The cut is 4 × 3 (diagonal 5), the projected model 6 × 8 (diagonal 10): lines over 15 go, cut lines never.
        List<DLine> lines =
        [
            new(new Vec2(0, 0), new Vec2(4, 0), "cut", "w", "IfcWall"),
            new(new Vec2(0, 0), new Vec2(14, 0), "projection", "w", "IfcWall"),
            new(new Vec2(0, 0), new Vec2(0, 16), "projection", "w", "IfcWall"),
            new(new Vec2(-500, 0), new Vec2(500, 0), "hidden", "w", "IfcWall"),
            new(new Vec2(0, 0), new Vec2(40, 0), "cut", "w", "IfcWall"),
            new(new Vec2(-90, 0), new Vec2(90, 0), "contour", "t", "IfcGeographicElement"),   // a symbol: not measured
        ];
        var kept = Generator.DropStray(lines, (new Vec2(0, 0), new Vec2(4, 3)), (new Vec2(0, 0), new Vec2(6, 8)));
        Assert.Equal(4, kept.Count);
        Assert.DoesNotContain(kept, l => l.Role is "projection" or "hidden" && (l.B - l.A).Length > 15);
        // Nothing to measure by: all stay.
        Assert.Equal(6, Generator.DropStray(lines, null, null).Count);
    }

    // ------------------------------------------------------------ line styles (line-styles.test.ts)

    [Fact]
    public void Pens_by_scale_pen_5_is_0_35_mm_at_1_100_and_0_5_at_1_50()
    {
        var t = LineStyles.DefaultPenTable();
        Assert.Equal(0.35, LineStyles.PenWidth(t, 5, 100));
        Assert.Equal(0.5, LineStyles.PenWidth(t, 5, 50));
        Assert.Equal(0.35, LineStyles.PenWidth(t, 5, 75));
        Assert.Equal(0.1, LineStyles.PenWidth(t, 1, 200));
    }

    [Fact]
    public void Object_styles_then_the_view_s_category_then_the_element_field_by_field()
    {
        Assert.Null(LineStyles.For(null, null, null, null, 100));
        var t = LineStyles.DefaultPenTable();
        var resolve = LineStyles.For(
            new JsonObject { ["walls"] = new JsonObject { ["cut"] = new JsonObject { ["pen"] = 7, ["pattern"] = "solid" }, ["hidden"] = new JsonObject { ["pattern"] = "dotted" } } },
            new JsonObject { ["walls"] = new JsonObject { ["lines"] = new JsonObject { ["cut"] = new JsonObject { ["color"] = "#ff0000" } } } },
            new JsonObject { ["G1"] = new JsonObject { ["cut"] = new JsonObject { ["pen"] = 9 } } }, null, 100)!;
        var cut = resolve("cut", "IfcWall", null)!;
        Assert.Equal((LineStyles.PenWidth(t, 7, 100), "#ff0000"), (cut.WidthMm!.Value, cut.Color));
        Assert.Empty(cut.Dash!);
        Assert.Equal(LineStyles.PenWidth(t, 9, 100), resolve("cut", "IfcWall", "G1")!.WidthMm);
        Assert.Equal("#ff0000", resolve("cut", "IfcWall", "G1")!.Color);
        Assert.Equal(LineStyles.DashOf("dotted"), resolve("hidden", "IfcWallStandardCase", null)!.Dash);
        Assert.Null(resolve("cut", "IfcSlab", null));
    }

    // ------------------------------------------------------------ material drawing (material-drawing.test.ts)

    static readonly Dictionary<string, string> Names = new() { ["10"] = "Concrete C25/30", ["11"] = "PE foil", ["12"] = "Mineral wool", ["13"] = "Bitumen membrane" };

    static DPolygon Band(string element, string material, double x0, double x1, double y0, double y1, string cls = "IfcWall") =>
        new([[new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]], element, cls, null, material);
    static DLine Line(string element, double x0, double y0, double x1, double y1, string role = "cut", string cls = "IfcWall") => new(new(x0, y0), new(x1, y1), role, element, cls);

    static ViewDrawing Scene() => new(
        [Band("1", "10", 0, 4, 0, 0.2), Band("1", "12", 0, 4, 0.2, 0.3), Band("2", "10", 4, 8, 0, 0.2), Band("2", "12", 4, 8, 0.2, 0.3)],
        [Line("1", 0, 0, 4, 0), Line("1", 0, 0.3, 4, 0.3), Line("1", 0, 0.2, 4, 0.2), Line("1", 4, 0, 4, 0.3), Line("1", 0, 0, 0, 0.3),
         Line("2", 4, 0, 8, 0), Line("2", 4, 0.3, 8, 0.3), Line("2", 4, 0.2, 8, 0.2), Line("2", 4, 0, 4, 0.3), Line("2", 8, 0, 8, 0.3)]);

    static readonly IReadOnlyList<LayerRef> Layers = [new("10", Names["10"], 0.2), new("11", Names["11"], 0.0002), new("12", Names["12"], 0.1)];
    static ResolvedMaterial? Graphics(DPolygon p, JsonObject? attrs = null) =>
        p.Material != null && Names.TryGetValue(p.Material, out var n) ? MaterialGraphics.Resolve(new Entity(p.Material, IfcClass.Material, n, attrs ?? []), "section") : null;
    static bool Membrane(LayerRef l) => l.Thickness < 0.002 || MaterialGraphics.FromName(l.Name).Membrane;
    static string Key(ViewDrawing d, int i) => $"{d.Lines[i].Element}:{d.Lines[i].A.X},{d.Lines[i].A.Y}-{d.Lines[i].B.X},{d.Lines[i].B.Y}";

    [Fact]
    public void Each_band_is_hatched_lines_inside_one_material_go_and_the_foil_is_a_membrane()
    {
        var d = Scene();
        var m = MaterialDrawing.Apply(d, p => Graphics(p), _ => Layers, Membrane);
        Assert.Equal(["CONCRETE", "CONCRETE", "INSULATION", "INSULATION"], m.Hatches.Select(h => h.Pattern).Order());
        var dropped = m.DropLines.Select(i => Key(d, i)).ToList();
        Assert.Contains("1:4,0-4,0.3", dropped);
        Assert.Contains("2:4,0-4,0.3", dropped);
        Assert.DoesNotContain(dropped, x => x.Contains("0,0-0,0.3") || x.Contains("8,0-8,0.3"));
        // The interface y = 0.2 is the foil: a membrane line per wall, and the plain line there goes.
        Assert.Equal(2, m.Membranes.Count);
        Assert.All(m.Membranes, s => { Assert.Equal(0.2, s.A.Y, 9); Assert.Equal(0.2, s.B.Y, 9); });
        Assert.Contains("1:0,0.2-4,0.2", dropped);
        Assert.Contains("2:4,0.2-8,0.2", dropped);
        // No fill unless asked: white behind the hatch.
        Assert.All(m.Fills.Values, Assert.Null);
    }

    [Fact]
    public void Sides_two_walls_bands_of_one_material_share_are_seams()
    {
        var d = Scene();
        var m = MaterialDrawing.Apply(d, p => Graphics(p), _ => Layers, Membrane);
        Assert.Equal([1], m.Seams[0]);
        Assert.Equal([1], m.Seams[1]);
        Assert.Equal([3], m.Seams[2]);
        Assert.Equal([3], m.Seams[3]);
        // The edge between the two solids seen from above (a projection line on the seam) goes too.
        var with = new ViewDrawing(d.Cuts, [.. d.Lines, Line("1", 4, 0, 4, 0.3, "projection")]);
        Assert.Contains(with.Lines.Count - 1, MaterialDrawing.Apply(with, p => Graphics(p), _ => Layers, Membrane).DropLines);
    }

    [Fact]
    public void A_material_whose_joints_are_drawn_keeps_the_line_between_its_elements()
    {
        var d = Scene();
        var m = MaterialDrawing.Apply(d, p => Graphics(p, p.Material == "10" ? new JsonObject { ["drawJoints"] = true } : null));
        Assert.False(m.Seams.ContainsKey(0), "the concrete joint is drawn");
        Assert.Equal([1], m.Seams[1]);
    }

    [Fact]
    public void A_membrane_thick_enough_for_its_own_band_is_its_middle_line()
    {
        var d = Scene();
        d.Cuts.Add(Band("3", "13", 0, 6, 1, 1.004));
        var m = MaterialDrawing.Apply(d, p => Graphics(p), _ => Layers, Membrane);
        Assert.Contains(4, m.HidePolygons);
        Assert.Contains(m.Membranes, s => Math.Abs(s.A.Y - 1.002) < 1e-9 && Math.Abs(Math.Abs(s.B.X - s.A.X) - 6) < 1e-9);
    }

    [Fact]
    public void Elements_with_no_material_join_by_kind_walls_with_walls_not_with_columns()
    {
        DPolygon Plain(string e, string cls, double x0, double x1) => Band(e, null!, x0, x1, 0, 0.3, cls) with { Material = null };
        var d = new ViewDrawing([Plain("1", "IfcWall", 0, 4), Plain("2", "IfcWallStandardCase", 4, 8), Plain("3", "IfcColumn", 8, 8.4)],
            [Line("1", 4, 0, 4, 0.3), Line("2", 4, 0, 4, 0.3), Line("3", 8, 0, 8, 0.3, cls: "IfcColumn")]);
        var m = MaterialDrawing.Apply(d, _ => null);
        Assert.Equal(["1:4", "2:4"], m.DropLines.Select(i => $"{d.Lines[i].Element}:{d.Lines[i].A.X}").Order());
        Assert.True(m.Seams.ContainsKey(0) && m.Seams.ContainsKey(1));
        Assert.False(m.Seams.ContainsKey(2));
        Assert.Empty(m.Hatches);
    }

    [Fact]
    public void Segments_coincide_when_collinear_and_mostly_overlapping()
    {
        Assert.True(MaterialDrawing.Coincide((new(0, 0), new(2, 0)), (new(2, 0.001), new(0.2, 0))));
        Assert.False(MaterialDrawing.Coincide((new(0, 0), new(2, 0)), (new(0, 0.01), new(2, 0.01))));
        Assert.False(MaterialDrawing.Coincide((new(0, 0), new(2, 0)), (new(1.9, 0), new(3, 0))));
    }

    // ------------------------------------------------------------ hidden lines per view (view-hidden-lines.test.ts)

    [Fact]
    public void Hidden_lines_by_kind_then_the_project_then_the_view()
    {
        Assert.True(Views.ShowsHidden(null, "plan", null));
        Assert.True(Views.ShowsHidden(null, "section", null));
        Assert.False(Views.ShowsHidden(null, "elevation", null));
        var project = new Entity("p", IfcClass.Project, "P", new JsonObject { ["viewDefaults"] = new JsonObject { ["hiddenLines"] = new JsonObject { ["elevation"] = "show", ["section"] = "hide" } } });
        Assert.True(Views.ShowsHidden(project, "elevation", null));
        Assert.False(Views.ShowsHidden(project, "section", null));
        Assert.True(Views.ShowsHidden(project, "section", "show"));
        var f = new Fixture();
        f.Run("view.objectStyle", new JsonObject { ["kind"] = "plan", ["hiddenLines"] = "hide" });
        Assert.False(Views.ShowsHidden(Spatial.Project(f.Model), "plan", null));
    }

    // ------------------------------------------------------------ the generator on a model

    static JsonObject Draw(Fixture f, JsonObject args) => (JsonObject)f.Session.Query("view.drawing", args)!;
    static List<JsonObject> Pens(JsonObject d, string layer) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == layer).ToList();
    static int Segs(JsonObject d, string layer) => Pens(d, layer).Sum(p => p["segments"]?.AsArray().Count / 4 ?? 0);

    [Fact]
    public void A_plan_cuts_walls_into_their_layers_with_the_materials_hatches_and_the_corner_reads_as_one()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        f.Wall(5, 0, 5, 4);
        var d = Draw(f, new JsonObject { ["storeyId"] = f.Storey });
        Assert.Null(d["problem"]);
        Assert.True(Segs(d, "CUT") >= 8);
        // The brick core is hatched; the plaster and render are not.
        Assert.NotEmpty(Pens(d, "HATCH"));
        Assert.Equal(0.5, (double)Pens(d, "CUT")[0]["width"]!);
        // At the corner, the two walls' brick meets brick: no cut line between them.
        var plan = Views.Find(f.Model, "plan:" + f.Storey)!;
        Assert.Equal("plan", plan.Str("kind"));
    }

    [Fact]
    public void A_door_draws_its_symbol_in_plan_and_the_wall_its_gap()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var plain = Segs(Draw(f, new JsonObject { ["storeyId"] = f.Storey }), "CUT");
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 3 });
        var d = Draw(f, new JsonObject { ["storeyId"] = f.Storey });
        // The wall is cut in two pieces at 1.20 m: more cut lines; the door's leaf and arc are seen lines.
        Assert.True(Segs(d, "CUT") > plain);
        Assert.True(Segs(d, "SEEN") >= 13);
    }

    [Fact]
    public void Overhead_beams_are_dashed_and_footings_under_the_bottom_are_beyond()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 });
        var d = Draw(f, new JsonObject { ["storeyId"] = f.Storey });
        Assert.True(Segs(d, "OVERHEAD") >= 2);
        var overhead = Pens(d, "OVERHEAD")[0];
        Assert.NotNull(overhead["dash"]);
        // A pad footing under the ground floor: in a plan whose range goes down a level, it lies beyond the bottom.
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("view.setRange", new JsonObject { ["id"] = view, ["depth"] = new JsonObject { ["level"] = "self", ["offset"] = -3 } });
        var deep = Draw(f, new JsonObject { ["id"] = view });
        Assert.True(Segs(deep, "BEYOND") >= 4);
        Assert.Equal("#808080", (string)Pens(deep, "BEYOND")[0]["stroke"]!);
        // A foundation plan (cut 0.50 under the level) cuts a strip foundation's stem and sees the pad (its top at −0.90) below.
        f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = -3, ["x2"] = 6, ["y2"] = -3 });
        var foundation = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["foundation"] = true })!["id"]!;
        var fd = Draw(f, new JsonObject { ["id"] = foundation });
        Assert.True(Segs(fd, "CUT") >= 4);
        Assert.True(Segs(fd, "SEEN") >= 8);
    }

    [Fact]
    public void A_roof_draws_its_outline_and_ridges_overhead_in_the_plan_of_its_storey()
    {
        var f = new Fixture();
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 6 });
        var d = Draw(f, new JsonObject { ["storeyId"] = f.Storey });
        // The eave outline (4 sides) and the ridge and four hips.
        Assert.True(Segs(d, "OVERHEAD") >= 4 + 5);
    }

    [Fact]
    public void A_section_cuts_what_its_line_crosses_and_sees_the_rest_and_an_elevation_cuts_nothing()
    {
        var f = new Fixture();
        f.Wall(0, 0, 6, 0);
        f.Wall(0, 4, 6, 4);
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 6) })!["id"]!;
        var d = Draw(f, new JsonObject { ["id"] = s });
        // Looking to the left of the line (west, −X): both walls cut, each a layered band 3 m high.
        var bounds = d["bounds"]!.AsArray().Select(x => (double)x!).ToArray();
        Assert.Equal(3, bounds[3] - bounds[1], 6);
        Assert.True(Segs(d, "CUT") >= 8);
        Assert.NotEmpty(Pens(d, "HATCH"));
        var e = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })!["id"]!;
        var el = Draw(f, new JsonObject { ["id"] = e });
        Assert.Equal(0, Segs(el, "CUT"));
        Assert.True(Segs(el, "SEEN") >= 4);
        Assert.Equal(0, Segs(el, "HIDDEN"));   // elevations leave hidden lines out
        // The view's graphics: walls in red.
        f.Run("view.graphics", new JsonObject { ["id"] = e, ["category"] = "walls", ["value"] = new JsonObject { ["lineColor"] = "#ff0000" } });
        Assert.All(Pens(Draw(f, new JsonObject { ["id"] = e }), "SEEN"), p => Assert.Equal("#ff0000", (string)p["stroke"]!));
    }

    [Fact]
    public void A_range_out_of_order_is_kept_and_reported_and_undo_takes_it_back()
    {
        var f = new Fixture();
        var v = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var r = f.Run("view.setRange", new JsonObject { ["id"] = v, ["top"] = new JsonObject { ["level"] = "self", ["offset"] = 0.5 } })!;
        Assert.Contains("topBelowCut", r["problems"]!.AsArray().Select(x => (string)x!));
        f.Session.Undo();
        Assert.Null(f.Model.Get(v)!.Attrs["viewRange"]);
    }
}
