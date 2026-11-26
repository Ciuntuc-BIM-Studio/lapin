using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// A visible Structural usage on elements (docs/plan-structural-usage.md, decision 866): one resolver of what is said (the
/// element's tick, its Pset value, its file's, its type's), each consumer's rule when nothing is (the views', the analysis'),
/// nothing changed until the user ticks; the command element.setStructural, one undo step; the analysis leaving out a floor,
/// a roof or a member said not to bear load; the IFC export writing Pset_…Common.LoadBearing from it, the derived value only
/// when asked; the filter field <c>structural</c> and the Structure discipline's halftone.
/// </summary>
public sealed class StructuralUsageTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-structural-usage").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new AnalysisModule());

    static void Tick(Fixture f, JsonNode? value, params string[] ids) =>
        f.Run("element.setStructural", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["value"] = value?.DeepClone() });

    static void Pset(Fixture f, string id, bool value, string set = "Pset_WallCommon") =>
        f.Run("element.setParam", new JsonObject { ["id"] = id, ["pset"] = set, ["name"] = "LoadBearing", ["value"] = value });

    static StructuralUsage.Usage Views(Fixture f, string id) => StructuralUsage.ForViews(f.Model, f.Model.Get(id)!);

    [Fact]
    public void What_is_said_wins_in_its_order_each_source_reported()
    {
        var f = Full();
        var type = f.Type("Brick 25", ("Brick", 0.25, true));
        var wall = f.Wall(0, 0, 5, 0, type);
        // Nothing said: the views' rule (a wall bears load), the analysis' (by its material and core).
        Assert.Equal(new StructuralUsage.Usage(null, true, "rule:class"), Views(f, wall));
        Assert.Equal("rule:thickness", StructuralUsage.ForAnalysis(f.Model, f.Model.Get(wall)!).Source);
        // Its type's Pset value, then its type's tick over it.
        Pset(f, type, false);
        Assert.Equal(new StructuralUsage.Usage(false, false, "typePset"), Views(f, wall));
        f.Run("element.setStructural", new JsonObject { ["ids"] = new JsonArray(wall), ["value"] = true, ["onType"] = true });
        Assert.True(StructuralUsage.Own(f.Model.Get(type)!));
        Assert.Null(StructuralUsage.Own(f.Model.Get(wall)!));
        var byType = Views(f, wall);
        Assert.Equal((true, true, "type", true), (byType.Said, byType.Effective, byType.Source, byType.Conflict));
        // Its own Pset value over its type's.
        Pset(f, wall, false);
        Assert.Equal(new StructuralUsage.Usage(false, false, "ownPset"), Views(f, wall));
        // Its own tick over everything, the disagreement with its Pset value said.
        Tick(f, true, wall);
        Assert.Equal(new StructuralUsage.Usage(true, true, "own", Conflict: true), Views(f, wall));
        Assert.Equal("own", StructuralUsage.ForAnalysis(f.Model, f.Model.Get(wall)!).Source);
        // The tick taken off: the Pset value decides again.
        Tick(f, null, wall);
        Assert.Equal("ownPset", Views(f, wall).Source);
        // A partition by IFC's word, a floor, a covering.
        Assert.Equal(new StructuralUsage.Usage(null, false, "rule:partition"), StructuralUsage.ForViews(f.Model, new Entity("p", IfcClass.Wall, "P", [], "PARTITIONING")));
        Assert.Equal(new StructuralUsage.Usage(null, true, "rule:class"), StructuralUsage.ForViews(f.Model, new Entity("s", IfcClass.Slab, "S", [])));
        Assert.Equal(new StructuralUsage.Usage(null, false, "rule:class"), StructuralUsage.ForViews(f.Model, new Entity("k", "IfcCovering", "K", [])));
        // A file's Pset_…Common value (an imported element's), read where nothing of Lapin's says.
        var imported = new Entity("i", IfcClass.Wall, "I", new JsonObject { ["imported"] = new JsonObject { ["properties"] = new JsonObject { ["Pset_WallCommon"] = new JsonObject { ["LoadBearing"] = false } } } });
        Assert.Equal(new StructuralUsage.Usage(false, false, "file"), StructuralUsage.ForViews(f.Model, imported));
        // The query the properties read.
        var row = f.Session.Query("element.structural", new JsonObject { ["ids"] = new JsonArray(wall) })!.AsArray().Single()!.AsObject();
        Assert.Equal("ownPset", (string?)row["source"]);
        Assert.True((bool)row["applies"]!);
        Assert.Equal("ownPset", (string?)row["analysis"]!["source"]);
    }

    [Fact]
    public void Absent_the_views_answer_as_they_did()
    {
        // The cases of decisions 500 and 502 (PartialStructureTests), through the resolver.
        var f = Full();
        Assert.True(ViewStyles.BearsLoad(f.Model, new Entity("c", IfcClass.Column, "C", [])));
        Assert.True(ViewStyles.BearsLoad(f.Model, new Entity("s", IfcClass.Slab, "S", [])));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("s2", IfcClass.Slab, "S", new JsonObject { ["loadBearing"] = false })));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("k", "IfcCovering", "K", [])));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("k2", "IfcCovering", "K", new JsonObject { ["loadBearing"] = true })));
        Assert.True(ViewStyles.BearsLoad(f.Model, new Entity("d", IfcClass.Door, "D", [])));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("p", IfcClass.Wall, "P", [], "PARTITIONING")));
        Assert.True(ViewStyles.BearsLoad(f.Model, new Entity("p2", IfcClass.Wall, "P", new JsonObject { ["loadBearing"] = true }, "PARTITIONING")));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("cw", "IfcCurtainWall", "CW", [])));
        Assert.False(ViewStyles.BearsLoad(f.Model, new Entity("m", "IfcMember", "M", [])));
        var wall = f.Wall(0, 0, 5, 0);
        Assert.True(ViewStyles.BearsLoad(f.Model, f.Model.Get(wall)!));
        Assert.Null(Lapin.Platform.Coordination.Provisions.LoadBearingOrNull(f.Model, f.Model.Get(wall)!));
        Assert.False(Lapin.Platform.Coordination.Provisions.LoadBearing(f.Model, f.Model.Get(wall)!));
    }

    [Fact]
    public void The_tick_on_several_elements_is_one_undo_step_and_keeps_their_ids()
    {
        var f = Full();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(5, 0, 5, 4);
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = AnalysisRunTests.Rect(0, 0, 5, 4) })!["id"]!;
        var gid = f.Model.Get(a)!.Str("globalId");
        var r = f.Run("element.setStructural", new JsonObject { ["ids"] = new JsonArray(a, b, slab), ["value"] = false })!;
        Assert.Equal(3, (int)r["changed"]!);
        Assert.All(new[] { a, b, slab }, id => Assert.False(StructuralUsage.Own(f.Model.Get(id)!)));
        Assert.Equal(gid, f.Model.Get(a)!.Str("globalId"));
        f.Session.Undo();
        Assert.All(new[] { a, b, slab }, id => Assert.Null(StructuralUsage.Own(f.Model.Get(id)!)));
        // Only what may bear load: a door is refused alone, left out with others.
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["x"] = 2, ["y"] = 0 });
        var door = f.Model.OfClass(IfcClass.Door).Single().Id;
        Assert.Throws<CommandException>(() => Tick(f, true, door));
        Tick(f, true, door, a);
        Assert.True(StructuralUsage.Own(f.Model.Get(a)!));
        Assert.Null(f.Model.Get(door)!.Attrs[StructuralUsage.Key]);
        // The Structure tab's tools: drawn ticked, in the same step.
        var drawn = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 4, ["x2"] = 5, ["y2"] = 4, ["loadBearing"] = true })!["id"]!;
        Assert.True(StructuralUsage.Own(f.Model.Get(drawn)!));
        var floor = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = AnalysisRunTests.Rect(0, 0, 2, 2), ["loadBearing"] = true })!["id"]!;
        Assert.True(StructuralUsage.Own(f.Model.Get(floor)!));
        f.Session.Undo();
        Assert.Null(f.Model.Get(floor));
    }

    [Fact]
    public void The_type_dialogs_tick_sets_the_types_attribute_in_one_step_and_saving_the_type_keeps_it()
    {
        // What the Wall types, Floor types and Structure types dialogs send: {ids: [the type], value, onType: true}.
        var f = Full();
        var wallType = f.Type("Brick 25", ("Brick", 0.25, true));
        var wall = f.Wall(0, 0, 5, 0, wallType);
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject { ["name"] = "Slab 20", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.2, ["core"] = true }) })!["id"]!;
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 } })!["id"]!;
        var gid = f.Model.Get(wallType)!.Str("globalId");
        void OnType(string type, bool? value) =>
            f.Run("element.setStructural", new JsonObject { ["ids"] = new JsonArray(type), ["value"] = value is { } v ? JsonValue.Create(v) : null, ["onType"] = true });
        OnType(wallType, false);
        Assert.False(StructuralUsage.Own(f.Model.Get(wallType)!));
        Assert.Equal(new StructuralUsage.Usage(false, false, "type"), Views(f, wall));
        Assert.Equal(gid, f.Model.Get(wallType)!.Str("globalId"));
        f.Session.Undo();
        Assert.Null(StructuralUsage.Own(f.Model.Get(wallType)!));
        Assert.Equal(new StructuralUsage.Usage(null, true, "rule:class"), Views(f, wall));
        // Each type ticked, then saved by its dialog: the tick stays.
        foreach (var t in new[] { wallType, slabType, colType, beamType }) OnType(t, t == colType);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = wallType, ["name"] = "Brick 25", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true }), ["wrapEnds"] = null });
        f.Run("architecture.updateSlabType", new JsonObject { ["id"] = slabType, ["name"] = "Slab 20", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.22, ["core"] = true }), ["beamBlock"] = null });
        f.Run("structure.updateType", new JsonObject { ["id"] = colType, ["name"] = "C35", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.35, ["depth"] = 0.35 } });
        f.Run("structure.updateType", new JsonObject { ["id"] = beamType, ["name"] = "B35", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.35, ["depth"] = 0.5 } });
        Assert.False(StructuralUsage.Own(f.Model.Get(wallType)!));
        Assert.False(StructuralUsage.Own(f.Model.Get(slabType)!));
        Assert.True(StructuralUsage.Own(f.Model.Get(colType)!));
        Assert.False(StructuralUsage.Own(f.Model.Get(beamType)!));
        // Back to the rule.
        OnType(colType, null);
        Assert.Null(f.Model.Get(colType)!.Attrs[StructuralUsage.Key]);
    }

    /// <summary>
    /// U4 (one default rule for every consumer) is left: no rule keeps both consumers' answers for unsaid walls. A thin wall
    /// that is no partition bears load in the views (decisions 500/502, the cores of the load-bearing, the Structure discipline)
    /// and stays out of the analysis (a core under 10 cm): a single rule changes one of them for models that say nothing.
    /// </summary>
    [Fact]
    public void U4_the_views_and_the_analysis_disagree_on_an_unsaid_thin_wall_so_one_rule_would_change_a_result()
    {
        var f = Full();
        var board = f.Type("Board 7.5", ("Plasterboard", 0.075, true));
        var wall = f.Wall(0, 0, 6, 0, board);
        Assert.True(Views(f, wall).Effective);
        Assert.True(ViewStyles.BearsLoad(f.Model, f.Model.Get(wall)!));
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        Assert.DoesNotContain(am.Shells, s => s.Source.SourceId == wall);
        Assert.Equal("rule:thickness", StructuralUsage.ForAnalysis(f.Model, f.Model.Get(wall)!).Source);
    }

    [Fact]
    public void A_column_a_beam_or_a_floor_said_not_to_bear_load_leaves_the_analysis_with_a_note()
    {
        var frame = AnalysisRunTests.Frame6x4();
        var f = frame.F;
        // Absent: everything in, as before.
        var before = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        var frames = before.Frames.Select(x => x.Source.SourceId).ToHashSet();
        Assert.All(frame.Columns.Concat(frame.Beams), id => Assert.Contains(id, frames));
        Assert.Contains(before.Shells, s => s.Source.SourceId == frame.Slab);
        Assert.DoesNotContain(before.Notes, n => n.StartsWith("Floors, roofs and members marked not load-bearing"));
        // The tick cleared on a beam and the floor, a Pset value false on a column.
        Tick(f, false, frame.Beams[0], frame.Slab);
        Pset(f, frame.Columns[0], false, "Pset_ColumnCommon");
        var after = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        var left = after.Frames.Select(x => x.Source.SourceId).ToHashSet();
        Assert.DoesNotContain(frame.Beams[0], left);
        Assert.DoesNotContain(frame.Columns[0], left);
        Assert.Contains(frame.Beams[1], left);
        Assert.DoesNotContain(after.Shells, s => s.Source.SourceId == frame.Slab);
        string Why(string id) => after.LeftOut.First(l => l.SourceId == id).Reason;
        Assert.Equal(AnalyticalModel.NotStructural, Why(frame.Beams[0]));
        Assert.Equal(AnalyticalModel.NotStructural, Why(frame.Slab));
        Assert.Equal("not load-bearing (Pset_ColumnCommon.LoadBearing false)", Why(frame.Columns[0]));
        var note = Assert.Single(after.Notes, n => n.StartsWith("Floors, roofs and members marked not load-bearing, left out of the analysis: "));
        Assert.Contains(f.Model.Get(frame.Beams[0])!.Name, note);
        // Ticked again: back in.
        Tick(f, true, frame.Beams[0], frame.Slab);
        var again = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        Assert.Contains(again.Frames, x => x.Source.SourceId == frame.Beams[0]);
        Assert.Contains(again.Shells, s => s.Source.SourceId == frame.Slab);
    }

    [Fact]
    public void A_wall_whose_tick_is_cleared_stays_out_of_the_analysis_saying_so()
    {
        var f = Full();
        var brick = f.Type("Brick 25", ("Brick", 0.25, true));
        var wall = f.Wall(0, 0, 6, 0, brick);
        Assert.Contains(AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions()).Shells, s => s.Source.SourceId == wall);
        Tick(f, false, wall);
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        Assert.DoesNotContain(am.Shells, s => s.Source.SourceId == wall);
        Assert.Equal(AnalyticalModel.NotStructural, am.LeftOut.Single(l => l.SourceId == wall).Reason);
    }

    static string Ifc(Fixture f, string path, IfcExportOptions? o = null) { IfcExport.Write(f.Session, path, o); return File.ReadAllText(path); }

    /// <summary>The LoadBearing values the file writes, in its order.</summary>
    static List<string> LoadBearings(string text) =>
        [.. text.Split('\n').Where(l => l.Contains("IFCPROPERTYSINGLEVALUE('LoadBearing'")).Select(l => l.Contains(".T.") ? "T" : "F")];

    [Fact]
    public void The_export_writes_LoadBearing_from_the_tick_and_the_rules_value_only_when_asked()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 5, 0);
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = AnalysisRunTests.Rect(0, 0, 5, 4) });
        // Nothing said: no LoadBearing on a wall or a floor, as before.
        Assert.Empty(LoadBearings(Ifc(f, Path.Combine(_dir, "a.ifc"))));
        // Derived, asked for: both, from the views' rule.
        Assert.Equal(["T", "T"], LoadBearings(Ifc(f, Path.Combine(_dir, "b.ifc"), new IfcExportOptions(DerivedLoadBearing: true))));
        // The tick: in Pset_WallCommon, and in Lapin_Intent, from which a Lapin import makes the tick again.
        Tick(f, false, wall);
        var text = Ifc(f, Path.Combine(_dir, "c.ifc"));
        Assert.Equal(["F"], LoadBearings(text));
        var back = Full();
        IfcImport.Run(back.Session, Path.Combine(_dir, "c.ifc"), null, out _);
        var w = back.Model.OfClass(IfcClass.Wall).Single();
        Assert.False(StructuralUsage.Own(w));
        Assert.Equal(f.Model.Get(wall)!.Str("globalId"), w.Str("globalId"));
        // A column keeps its true; cleared, false.
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 2 })!["id"]!;
        Assert.Equal(["F", "T"], LoadBearings(Ifc(f, Path.Combine(_dir, "d.ifc"))));
        Tick(f, false, column);
        Assert.Equal(["F", "F"], LoadBearings(Ifc(f, Path.Combine(_dir, "e.ifc"))));
    }

    [Fact]
    public void A_foreign_files_LoadBearing_is_read_and_its_import_adds_no_tick()
    {
        var f = Full();
        var x = new Step();
        var origin = x.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
        var world = x.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
        var ctx = x.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
        var mm = x.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
        var project = x.Add("IFCPROJECT", S(Guid("su", "project")), Null, S("Foreign"), Null, Null, Null, Null, L([ctx]), x.Add("IFCUNITASSIGNMENT", L([mm])));
        var sitePl = x.Add("IFCLOCALPLACEMENT", Null, world);
        var site = x.Add("IFCSITE", S(Guid("su", "site")), Null, S("Site"), Null, Null, sitePl, Null, Null, E("ELEMENT"), Null, Null, Null, Null, Null);
        var bPl = x.Add("IFCLOCALPLACEMENT", sitePl, world);
        var building = x.Add("IFCBUILDING", S(Guid("su", "building")), Null, S("Building"), Null, Null, bPl, Null, Null, E("ELEMENT"), Null, Null, Null);
        var sPl = x.Add("IFCLOCALPLACEMENT", bPl, world);
        var storey = x.Add("IFCBUILDINGSTOREY", S(Guid("su", "storey")), Null, S("Ground"), Null, Null, sPl, Null, Null, E("ELEMENT"), "0.");
        x.Add("IFCRELAGGREGATES", S(Guid("su", "a1")), Null, Null, Null, project, L([site]));
        x.Add("IFCRELAGGREGATES", S(Guid("su", "a2")), Null, Null, Null, site, L([building]));
        x.Add("IFCRELAGGREGATES", S(Guid("su", "a3")), Null, Null, Null, building, L([storey]));
        var p2 = x.Add("IFCCARTESIANPOINT", "(2000.,150.)");
        var prof = x.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, x.Add("IFCAXIS2PLACEMENT2D", p2, x.Add("IFCDIRECTION", "(1.,0.)")), "4000.", "300.");
        var solid = x.Add("IFCEXTRUDEDAREASOLID", prof, x.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null), x.Add("IFCDIRECTION", "(0.,0.,1.)"), "2800.");
        var shape = x.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([x.Add("IFCSHAPEREPRESENTATION", ctx, S("Body"), S("SweptSolid"), L([solid]))]));
        var wall = x.Add("IFCWALL", S(Guid("su", "wall")), Null, S("W"), Null, Null, x.Add("IFCLOCALPLACEMENT", sPl, world), shape, Null, E("STANDARD"));
        x.Add("IFCRELASSOCIATESMATERIAL", S(Guid("su", "mat")), Null, Null, Null, L([wall]), x.Add("IFCMATERIAL", S("Brick"), Null, Null));
        var prop = x.Add("IFCPROPERTYSINGLEVALUE", S("LoadBearing"), Null, "IFCBOOLEAN(.F.)", Null);
        var pset = x.Add("IFCPROPERTYSET", S(Guid("su", "pset")), Null, S("Pset_WallCommon"), Null, L([prop]));
        x.Add("IFCRELDEFINESBYPROPERTIES", S(Guid("su", "defp")), Null, Null, Null, L([wall]), pset);
        x.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", S(Guid("su", "contained")), Null, Null, Null, L([wall]), storey);
        var path = Path.Combine(_dir, "foreign.ifc");
        x.Write(path, "foreign.ifc", "ViewDefinition [ReferenceView_V1.2]");
        IfcImport.Run(f.Session, path, null, out _);
        var w = Assert.Single(f.Model.Entities, e => e.Class == IfcClass.Wall || e.Str("ifcClass") == "IfcWall");
        Assert.Null(w.Attrs[StructuralUsage.Key]);
        var u = StructuralUsage.ForViews(f.Model, w);
        Assert.Equal((false, "file"), (u.Said, u.Source));
    }

    [Fact]
    public void Filters_read_structural_and_a_structural_view_draws_the_rest_halftone()
    {
        var f = Full();
        var bearing = f.Wall(0, 0, 5, 0);
        var partition = f.Wall(0, 2, 5, 2);
        Tick(f, false, partition);
        var notStructural = new ViewFilters.Filter("f", "Not structural", false, ViewFilters.Rules(new JsonArray(new JsonObject { ["field"] = "structural", ["op"] = "equals", ["value"] = "no" })));
        Assert.True(ViewFilters.Matches(f.Model, f.Model.Get(partition)!, notStructural));
        Assert.False(ViewFilters.Matches(f.Model, f.Model.Get(bearing)!, notStructural));
        // A schedule's, a tag's field.
        Assert.Equal("yes", new ElementFields(new ModelQuery(f.Model), f.Model.Get(bearing)!).Value(StructuralUsage.Field).Text);
        Assert.Equal("no", new ElementFields(new ModelQuery(f.Model), f.Model.Get(partition)!).Value(StructuralUsage.Field).Text);
        // A Structure plan drawing the others halftone: the partition with them, the bearing wall in full.
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["discipline"] = "Structure", ["name"] = "S" })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["halftoneOthers"] = true });
        var vg = VisibilityGraphics.Of(f.Model, Lapin.Platform.Views.Find(f.Model, plan)!);
        Assert.True(vg.OutsideDiscipline(f.Model.Get(partition)!));
        Assert.False(vg.OutsideDiscipline(f.Model.Get(bearing)!));
        // An architectural plan: nothing halftone, as before.
        var arch = VisibilityGraphics.Of(f.Model, Lapin.Platform.Views.Find(f.Model, "plan:" + f.Storey)!);
        Assert.False(arch.OutsideDiscipline(f.Model.Get(partition)!));
    }
}
