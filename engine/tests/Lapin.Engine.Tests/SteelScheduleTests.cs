using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Steel, stage 5 (decision 276): the Steel members list with its marks, cuts and totals by hall; a hall's anchor plan and frame elevation.</summary>
public class SteelScheduleTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static string Hall(Fixture f, JsonObject? attrs = null)
    {
        var o = attrs ?? [];
        if (!o.ContainsKey("secondary")) o["secondary"] = false;
        return (string)f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = o })!["id"]!;
    }

    static List<SteelMembersSchedule.Row> Rows(Fixture f) => SteelMembersSchedule.Rows(f.Model, f.Session.Geometry);

    [Fact]
    public void The_member_list_marks_members_alike_by_profile_length_and_cuts()
    {
        var f = F();
        Hall(f);
        var rows = Rows(f);
        SteelMembersSchedule.Row Mark(string m) => rows.Single(r => r.Mark == m);
        // Eight frame columns alike: square at the base, their top turned 10° by the roof.
        Assert.Equal(8, Mark("C1").Count);
        Assert.Equal("IPE 400", Mark("C1").Profile);
        Assert.Equal("—", Mark("C1").Start);
        Assert.Equal("10.0°", Mark("C1").End);
        // Eight frame rafters alike (both sides), fitted plumb and mitred plumb: 10° from square each end; four gable rafters of their own profile.
        Assert.Equal(8, Mark("R1").Count);
        Assert.Equal(("10.0°", "10.0°"), (Mark("R1").Start, Mark("R1").End));
        Assert.Equal(("IPE 240", 4), (Mark("R2").Profile, Mark("R2").Count));
        // The gable posts by their length: the middle ones tallest, then the posts at 5 m, then the corners.
        Assert.Equal([2, 4, 4], rows.Where(r => r.Mark.StartsWith("GP", StringComparison.Ordinal)).Select(r => r.Count));
        Assert.True(Mark("GP1").Length > Mark("GP2").Length && Mark("GP2").Length > Mark("GP3").Length);
        Assert.All(rows, r => Assert.Equal("Hall 1", r.Hall));
        // The weights: each its body's, the per metre its section's.
        var col = Halls.Members(f.Model, f.Model.OfClass(Halls.Class).Single().Id)["f1:col:a"];
        Assert.Equal(f.Session.Geometry(col.Id)!.Parts.Where(p => p.Key == "body").Sum(p => Measure.Volume(p.Solid)) * SteelModule.SteelDensity, Mark("C1").Each, 6);
        Assert.Equal(Section.Catalogue["IPE 400"].Area * 7850, Mark("C1").KgPerM, 6);
        // The paint: the section's outline round, along its length.
        Assert.Equal(Polygon2.Perimeter(Section.Catalogue["IPE 400"].Outline()) * Mark("C1").Length * 8, Mark("C1").Paint, 1);
    }

    [Fact]
    public void The_member_list_has_a_total_under_each_hall_and_the_members_of_none_apart()
    {
        var f = F();
        Hall(f, new JsonObject { ["bays"] = new JsonArray(6), ["gable"] = "frame" });
        Hall(f, new JsonObject { ["bays"] = new JsonArray(5), ["gable"] = "frame", ["x"] = 40, ["secondary"] = true, ["flyBraces"] = false, ["bracing"] = new JsonArray() });
        var type = (string)f.Run("steel.createType", new JsonObject { ["section"] = "IPE 200" })!["id"]!;
        var m = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 50, ["x2"] = 4, ["y2"] = 50 })!["id"]!;
        f.Run("steel.setEnds", new JsonObject { ["ids"] = new JsonArray(m), ["end"] = new JsonObject { ["cut"] = "plane", ["planAngle"] = 30, ["copeLength"] = 0.1, ["copeDepth"] = 0.03, ["copeTop"] = true } });
        var rows = Rows(f);
        Assert.Equal(["Hall 1", "Hall 2", ""], rows.Select(r => r.Hall).Distinct());
        // The second hall's purlins and side rails marked as such.
        Assert.Contains(rows, r => r.Hall == "Hall 2" && r.Mark == "P1");
        Assert.Contains(rows, r => r.Hall == "Hall 2" && r.Mark == "SR1");
        // A member of no hall: its cut 30° in plan, and coped.
        var own = rows.Single(r => r.Hall == "");
        Assert.Equal(("M1", "—", "30.0° cope"), (own.Mark, own.Start, own.End));
        var grid = f.Session.Catalog.All<IScheduleSource>().Single(s => s.Kind == "steelMembers").Grid(new Q(f));
        Assert.Equal(["Hall 1", "Total"], grid.Rows.First(r => r[1] == "Total").Take(2));
        Assert.Equal("Total", grid.Rows[^1][0]);
        Assert.Equal(rows.Sum(r => r.Count).ToString(System.Globalization.CultureInfo.InvariantCulture), grid.Rows[^1][6]);
        Assert.Equal(rows.Sum(r => r.Each * r.Count), double.Parse(grid.Rows[^1][9], System.Globalization.CultureInfo.InvariantCulture), 0);
    }

    [Fact]
    public void An_anchor_plan_shows_each_base_its_anchors_dimensioned_to_the_grid_and_the_table_of_base_types()
    {
        var f = F();
        var hall = Hall(f);
        var view = (string)f.Run("steel.anchorPlan", new JsonObject { ["id"] = hall })!["id"]!;
        var v = f.Model.Get(view)!;
        Assert.Equal("plan", v.Str("kind"));
        Assert.Equal(50, v.Num("scale"));
        Assert.Equal(hall, v.Str(SteelDrawings.AnchorKey));
        Assert.Equal("Anchor plan · Hall 1", v.Name);
        // Asked again, from a member of the hall: the same view.
        var col = Halls.Members(f.Model, hall)["f1:col:a"].Id;
        Assert.Equal(view, (string)f.Run("steel.anchorPlan", new JsonObject { ["id"] = col })!["id"]!);
        // The bases: eight frame columns on four anchors (IPE 400, pinned), ten gable posts on two; two types.
        var bases = SteelDrawings.Bases(f.Model, f.Model.Get(hall)!);
        Assert.Equal(18, bases.Count);
        var marks = SteelDrawings.BaseMarks(bases);
        Assert.Equal(2, marks.Count);
        Assert.Equal(8, bases.Count(b => b.Anchors.Count == 4));
        Assert.Equal(10, bases.Count(b => b.Anchors.Count == 2));
        // A frame column's anchors about its axis, inside its flanges: symmetric in the hall's x and y.
        var b1 = bases.Single(b => Math.Abs(b.X) < 1e-6 && Math.Abs(b.Y - 6) < 1e-6);
        Assert.Equal(0, b1.Anchors.Average(p => p.X), 6);
        Assert.Equal(6, b1.Anchors.Average(p => p.Y), 6);
        Assert.True(b1.Anchors.Max(p => p.X) < 0.2, "inside the column's depth");
        Assert.Equal("B1", marks[bases[0].Key]);
        // Drawn: the marks, each anchor's distance to the grid and the grid's bays, the table's rows.
        var drawn = Views.Render(f.Session.Reader, v).Marks.Where(m => m.Owner == hall).ToList();
        var texts = drawn.SelectMany(m => m.Texts).Select(t => t.Text.Text).ToList();
        Assert.Equal(18 + 2, texts.Count(t => t == "B1" || t == "B2"));
        Assert.Contains("6000", texts);
        Assert.Contains("20000", texts);
        var gx = "±" + Math.Round(b1.Anchors.Max(p => p.X) * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(gx, texts);
        Assert.Contains(texts, t => t.StartsWith("4 × M24", StringComparison.Ordinal));
        Assert.Contains("Column bases · Hall 1", texts);
        // A fixed base is another type: three in all when the first frame's bases are set fixed.
        var node = Halls.Nodes(f.Model, hall)["base@f1:col:a@start"].Id;
        f.Run("steel.updateNode", new JsonObject { ["id"] = node, ["params"] = new JsonObject { ["base"] = "fixed" } });
        Assert.Equal(3, SteelDrawings.BaseMarks(SteelDrawings.Bases(f.Model, f.Model.Get(hall)!)).Count);
    }

    [Fact]
    public void A_frame_elevation_is_a_section_across_the_frame_with_its_span_and_heights()
    {
        var f = F();
        var hall = Hall(f);
        var raf = Halls.Members(f.Model, hall)["f3:raf:a"].Id;
        var view = (string)f.Run("steel.frameView", new JsonObject { ["id"] = raf })!["id"]!;
        var v = f.Model.Get(view)!;
        Assert.Equal("section", v.Str("kind"));
        Assert.Equal(3, v.Num(SteelDrawings.FrameIndexKey));
        Assert.Equal("Frame 4 · Hall 1", v.Name);
        // Looking along the hall at the frame (y 18), from half a metre before it.
        Assert.Equal(new Vec2(0, 1), Views.Look(v));
        var texts = Views.Render(f.Session.Reader, v).Marks.Where(m => m.Owner == hall).SelectMany(m => m.Texts).Select(t => t.Text.Text).ToList();
        Assert.Contains("20000", texts);
        Assert.Contains("10000", texts);
        Assert.Contains("6000", texts);
        Assert.Contains(Math.Round((HallSpec.Default.ApexHeight - 6) * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture), texts);
        // The drawing cuts the frame's members.
        Assert.Equal(view, (string)f.Run("steel.frameView", new JsonObject { ["id"] = hall, ["frame"] = 3 })!["id"]!);
        Assert.Throws<CommandException>(() => f.Run("steel.frameView", new JsonObject { ["id"] = hall, ["frame"] = 9 }));
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
