using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The platform's findings of the audit of 22.11.2026 (docs/audit-2026-11-22.md §3.3): outlines with ellipses and arcs, the
/// filter's numbers, deleting a storey with its elements, sheet and revision numbering.
/// </summary>
public class PlatformAudit1122Tests
{
    static JsonArray Pts(IEnumerable<Vec2> p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);

    [Fact]
    public void A_hole_inside_an_elliptical_outline_beyond_its_corners_is_taken()
    {
        // A whole ellipse as the outline: its four corners make a diamond; a hole near the ellipse's edge lies outside the
        // diamond but inside the ellipse.
        var (corners, el) = EllipseEdge.Whole(new Vec2(0, 0), new Vec2(4, 0), 0.5);
        var attrs = new JsonObject { ["contour"] = Pts(corners) };
        EllipseEdge.Write(attrs, el);
        var hole = new[] { new Vec2(2.6, 0.9), new Vec2(3.0, 0.9), new Vec2(3.0, 1.1), new Vec2(2.6, 1.1) };
        Assert.False(Polygon2.Contains(corners, hole[0]));
        OutlineHoles.Apply(attrs, new JsonObject { [OutlineHoles.Key] = new JsonArray(Pts(hole)) });
        Assert.Single(OutlineHoles.Read(attrs[OutlineHoles.Key], null));
        // One outside the ellipse is still refused.
        var outside = new[] { new Vec2(3.6, 1.5), new Vec2(3.9, 1.5), new Vec2(3.9, 1.8), new Vec2(3.6, 1.8) };
        Assert.Throws<CommandException>(() => OutlineHoles.Apply(attrs, new JsonObject { [OutlineHoles.Key] = new JsonArray(Pts(outside)) }));
    }

    [Fact]
    public void A_line_crossing_one_arc_twice_leaves_a_cap_the_check_takes()
    {
        var (corners, rises) = ArcOutline.Circle(new Vec2(0, 0), 1);
        // x + y = 1.35 crosses the first quarter (from (1, 0) to (0, 1)) twice.
        var (left, right) = OutlineCuts.Split(corners, rises, new Vec2(1.35, 0), new Vec2(0, 1.35));
        foreach (var p in new[] { left, right })
        {
            Assert.True(p.Corners.Count >= 3);
            ArcOutline.Check(p.Corners, p.Rises, "A floor");
        }
        var area = Math.Abs(ArcOutline.SignedArea(left.Corners, left.Rises)) + Math.Abs(ArcOutline.SignedArea(right.Corners, right.Rises));
        Assert.Equal(Math.PI, area, 9);
    }

    [Fact]
    public void A_half_ellipse_on_three_corners_in_a_line_is_checked_with_its_ellipse()
    {
        // (-2, 0) → (0, 0) → (2, 0) straight, then back along the upper half of an ellipse: its corners alone enclose nothing.
        var e = new EllipseEdge(new Vec2(0, 0), new Vec2(2, 0), 0.5, true);
        Vec2[] pts = [new(-2, 0), new(0, 0), new(2, 0)];
        EllipseEdge?[] el = [null, null, e];
        Assert.Throws<CommandException>(() => ArcOutline.Check(pts, null));
        var (c, _) = ArcOutline.Check(pts, null, "A floor", el);
        Assert.Equal(3, c.Count);
        var h = OutlineHoles.Checked(pts, null, el);
        Assert.Equal(Math.PI * 2 * 1 / 2, h.Area, 9);
    }

    [Fact]
    public void A_filter_reads_1_200_as_one_point_two_for_every_operator()
    {
        var text = FieldValue.OfText("1,200");
        Assert.True(new FieldCondition("Code", "=", "1.2").Holds(text));
        Assert.True(new FieldCondition("Code", "between", "1..2").Holds(text));
        Assert.True(new FieldCondition("@text:Code", "=", "1.2").HoldsAny(["1,200"]));
        Assert.True(new FieldCondition("@text:Code", "between", "1..2").HoldsAny(["1,200"]));
        Assert.True(new FieldCondition("@text:Code", "<", "2").HoldsAny(["1,200"]));
        Assert.False(new FieldCondition("@text:Code", ">", "1000").HoldsAny(["1,200"]));
        // Grouped by a space, it is a thousand two hundred everywhere.
        Assert.True(new FieldCondition("@text:Code", "between", "1000..1300").HoldsAny(["1 200"]));
    }

    [Fact]
    public void Formulas_and_tables_read_numbers_by_the_one_reader()
    {
        Assert.False(XValue.Of("NaN").IsNumericText);
        Assert.False(XValue.Of("Infinity").IsNumericText);
        Assert.True(XValue.Of("−3").IsNumericText);
        Assert.Equal("-3.0", Expr.Format(XValue.Of("−3"), "0.0"));
        Assert.Equal("NaN", Expr.Format(XValue.Of("NaN"), "0.0"));
        Assert.Null(TableUnits.Parse("NaN"));
        Assert.Equal(-3.2, TableUnits.Parse("−3,2")!.Value, 12);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_storey_deleted_with_its_elements_goes_whatever_the_order(bool storeyFirst)
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Second" });
        var storey = Spatial.Storeys(f.Model).Last().Id;
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0 })!["id"]!;
        var ids = storeyFirst ? new JsonArray(storey, wall) : new JsonArray(wall, storey);
        f.Run("element.delete", new JsonObject { ["ids"] = ids });
        Assert.Null(f.Model.Get(storey));
        Assert.Null(f.Model.Get(wall));
        // One step: undone, both are back.
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(storey));
        Assert.NotNull(f.Model.Get(wall));
        // Alone, a storey with elements on it is still refused.
        Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(storey) }));
    }

    sealed class Failing : Lapin.Platform.Coordination.IProvisionOpenings
    {
        public IEnumerable<Lapin.Platform.Coordination.ProvisionOpening> Of(IModelView m, IServices services, Entity? host, double offset = 0)
        {
            var box = Prism.Box(new Vec3(0, 0, 0), 0.2, 0.2, 0.2)!;
            yield return new("k1", "run", "wall", "duct", "rect", 0.2, 0.2, 0.2, box, box, null);
            throw new InvalidOperationException("no section");
        }
    }

    sealed class With(object service) : IServices
    {
        public T? Get<T>() where T : class => service as T;
        public IReadOnlyList<T> All<T>() where T : class => service is T t ? [t] : [];
    }

    [Fact]
    public void A_provider_failing_part_way_keeps_what_it_gave_and_says_why()
    {
        var problems = new List<string>();
        var got = Lapin.Platform.Coordination.Provisions.Of(new ModelStore(), new With(new Failing()), null, 0, problems);
        Assert.Single(got);
        Assert.Contains(problems, p => p.Contains("no section"));
    }

    sealed class Thrower : ILapinModule, IViewAnnotations
    {
        public string Id => "thrower";
        public string Name => "Thrower";
        public void Register(IModuleRegistry r) => r.Service<IViewAnnotations>(this);
        public IEnumerable<ViewMark> Marks(ViewContext c) => throw new InvalidOperationException("broken marks");
    }

    [Fact]
    public void A_module_that_cannot_draw_on_a_sheet_is_reported_not_swallowed()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Drafting.DraftingModule(), new Thrower());
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        f.Run("drafting.addText", new JsonObject { ["viewId"] = PaperViews.IdOf(s), ["at"] = new JsonArray(0.1, 0.2), ["text"] = "Note" });
        f.Session.TakeWarnings();
        var composed = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!);
        Assert.Contains(composed.Pens.SelectMany(p => p.Texts), t => t.Text == "Note");
        Assert.Contains(f.Session.TakeWarnings(), w => w.Contains("broken marks"));
    }

    [Fact]
    public void A_solid_pattern_without_a_fill_fills_in_ink_everywhere()
    {
        var plain = new Lapin.Drawing.ResolvedMaterial("Steel", "SOLID", 1, null, null, false, false, null, "own");
        // No fill: ink (black without a category; the 3D caps were #2b2b2b, the drawings and legends ink).
        Assert.Equal("#000000", SolidFill.Of(plain, plain.Fill));
        Assert.Equal("#336699", SolidFill.Of(plain, plain.Fill, "#336699"));
        // A fill: the fill; a hatch colour of the material's: over the fill.
        Assert.Equal("#aabbcc", SolidFill.Of(plain with { Fill = "#aabbcc" }, "#aabbcc"));
        var coloured = new Lapin.Drawing.ResolvedMaterial("Steel", "SOLID", 1, "#aabbcc", null, false, false, null, "own") { HatchColor = "#ff0000" };
        Assert.Equal("#ff0000", SolidFill.Of(coloured, coloured.Fill));
    }

    [Fact]
    public void A_new_sheet_takes_one_past_the_highest_number_of_its_series()
    {
        var f = new Fixture();
        foreach (var n in new[] { "A-101", "A-103", "A-102" }) f.Run("sheet.create", new JsonObject { ["number"] = n, ["paper"] = "A3" });
        string Next() => f.Model.Get((string)f.Run("sheet.create", new JsonObject { ["paper"] = "A3" })!["id"]!)!.Str("number")!;
        Assert.Equal("A-104", Next());
        // A number too long for a long: no overflow, and never one already taken.
        f.Run("sheet.create", new JsonObject { ["number"] = "A-99999999999999999999", ["paper"] = "A3" });
        var numbers = f.Model.OfClass(Sheets.Class).Select(s => s.Str("number")).ToList();
        var next = Next();
        Assert.DoesNotContain(next, numbers);
    }

    static JsonObject Issue(string revision) => new()
    {
        ["name"] = "Issue " + revision, ["revision"] = revision, ["status"] = "S2", ["container"] = Lapin.Platform.Cde.CdeRevisions.Shared,
        ["files"] = new JsonArray(new JsonObject { ["name"] = revision + ".pdf", ["kind"] = "pdf", ["sha256"] = "00", ["size"] = 1 }),
    };

    [Fact]
    public void The_next_revision_goes_on_from_the_highest_not_the_last_made()
    {
        var f = new Fixture();
        f.Run("cde.recordIssue", Issue("P03"));
        f.Run("cde.recordIssue", Issue("P01"));
        Assert.Equal("P03", Lapin.Platform.Cde.CdeIssues.LastRevision(f.Model));
        Assert.Equal("P04", (string)f.Session.Query("cde.settings", new JsonObject())!["nextRevision"]!);
    }
    [Fact]
    public void A_lin_type_kept_beside_Lapin_s_own_reads_the_same_the_second_time()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        const string lin = "*DASHED,File dashes\nA,12.7,-6.35\n*MINE,Mine\nA,3,-1";
        f.Run("standards.importLin", new JsonObject { ["text"] = lin });
        var read = (JsonArray)f.Session.Query("standards.readLin", new JsonObject { ["text"] = lin })!["lineTypes"]!;
        var dashed = read.OfType<JsonObject>().Single(x => (string?)x["name"] == "DASHED");
        Assert.Equal("DASHED (file)", (string?)dashed["as"]);
        Assert.True((bool)dashed["exists"]!);
        Assert.True((bool)dashed["same"]!);
        // Imported again: kept, nothing added.
        var again = f.Run("standards.importLin", new JsonObject { ["text"] = lin })!;
        Assert.Equal(0, (int)again["added"]!);
        Assert.Equal(0, (int)again["updated"]!);
    }
}

/// <summary>An edition's naming hook (a static) set for one test, alone.</summary>
[Collection(Scaling.Serial)]
public class CdeNamingHookTests
{
    [Fact]
    public void The_settings_example_name_goes_through_the_edition_s_naming_hook()
    {
        var f = new Fixture();
        Lapin.Platform.Cde.CdeHooks.Naming = n => n with { Project = "HOOK" };
        try
        {
            var example = (string)f.Session.Query("cde.settings", new JsonObject())!["example"]!;
            Assert.StartsWith("HOOK", example);
            // The project's own naming, as saved, stays the project's.
            Assert.Equal("PRJ", (string)f.Session.Query("cde.settings", new JsonObject())!["naming"]!["project"]!);
        }
        finally { Lapin.Platform.Cde.CdeHooks.Naming = null; }
    }
}
