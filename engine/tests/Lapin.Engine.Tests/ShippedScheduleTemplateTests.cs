using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Gates;
using Lapin.Architecture.Schedules;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Host;

namespace Lapin.Engine.Tests;

/// <summary>
/// The fixed schedules as shipped field-schedule templates (audit S1, decision 611): on a varied model of each kind, the
/// fixed schedule's table as it is today is the characterization, and the field schedule made from the template that stands
/// for it gives the same headings, rows, order and totals. Differences, where a template cannot give the same, are asserted
/// and written beside the test.
/// </summary>
public class ShippedScheduleTemplateTests
{
    /// <summary>The fixed schedule's table and the template's, side by side.</summary>
    static (ScheduleGrid Fixed, ScheduleGrid Template) Both(Fixture f, string kind) => Both(f.Session, (c, a) => f.Run(c, a), kind);

    static (ScheduleGrid Fixed, ScheduleGrid Template) Both(Session session, Func<string, JsonObject, JsonNode?> run, string kind)
    {
        var source = session.Reader.Services.All<IScheduleSource>().Single(s => s.Kind == kind);
        var made = run("architecture.importSchedule", new JsonObject { ["builtin"] = kind })!;
        var schedule = session.Model.Get((string)made["id"]!)!;
        Assert.Equal(FieldSchedule.KindId, schedule.Str("kind"));
        Assert.Equal(source.Label, schedule.Name);
        return (source.Grid(session.Reader), new FieldSchedule().Grid(session.Reader, schedule));
    }

    static string Show(ScheduleGrid g) => string.Join("\n", new[] { string.Join(" | ", g.Columns) }.Concat(g.Rows.Select(r => string.Join(" | ", r))));

    /// <summary>The same table: headings, then each row's cells, in order.</summary>
    static void Same(ScheduleGrid expected, ScheduleGrid actual)
    {
        Assert.True(expected.Rows.Count > 1, "The characterization model gives rows:\n" + Show(expected));
        Assert.Equal(Show(expected), Show(actual));
    }

    [Fact]
    public void Sunshades_template_gives_the_sunshade_schedule()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 9, 0);
        string Window(double along) => (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;
        string On(string host, string kind) => (string)f.Run("architecture.addSunshade", new JsonObject { ["hostId"] = host, ["kind"] = kind })!["id"]!;
        On(Window(1.5), "canopy");
        On(Window(4), "canopy");
        On(Window(6.5), "louvres");
        f.Run("architecture.addSunshade", new JsonObject { ["kind"] = "louvres", ["wallId"] = wall, ["x1"] = 7.2, ["y1"] = 0, ["x2"] = 8.8, ["y2"] = 0 });
        f.Run("architecture.addSunshade", new JsonObject { ["kind"] = "fins", ["wallId"] = wall, ["x1"] = 0.2, ["y1"] = 0, ["x2"] = 1.0, ["y2"] = 0 });
        var (fixedGrid, template) = Both(f, "sunshades");
        Same(fixedGrid, template);
    }
    [Fact]
    public void Gates_template_gives_the_gate_schedule()
    {
        var f = new Fixture(new ArchitectureModule());
        string Place(JsonObject a) { a["storeyId"] ??= f.Storey; return (string)f.Run("architecture.placeGate", a)!["id"]!; }
        Place(new JsonObject { ["kind"] = "sliding", ["x"] = 0, ["y"] = 0 });
        Place(new JsonObject { ["kind"] = "pedestrian", ["x"] = 0, ["y"] = 5 });
        Place(new JsonObject { ["kind"] = "pedestrian", ["x"] = 0, ["y"] = 9 });
        Place(new JsonObject { ["kind"] = "swingWicket", ["x"] = 0, ["y"] = 14 });
        var b = Place(new JsonObject { ["kind"] = "doubleSwing", ["x"] = 0, ["y"] = 20 });
        var style = (string)f.Run("architecture.createGateType", new JsonObject { ["style"] = "wroughtIron" })!["id"]!;
        f.Run("architecture.setGateType", new JsonObject { ["ids"] = new JsonArray(b), ["typeId"] = style });
        var (fixedGrid, template) = Both(f, "gates");
        Same(fixedGrid, template);
    }

    [Fact]
    public void Balconies_template_gives_the_balcony_schedule()
    {
        var f = new Fixture(new ArchitectureModule());
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var type = f.Type("Brick 25", ("Brick", 0.25, true));
        f.Wall(0, 0, 10, 0, type);
        string Add(string style, double x, string? storey = null) =>
            (string)f.Run("architecture.addBalcony", new JsonObject { ["storeyId"] = storey ?? f.Storey, ["style"] = style, ["x"] = x, ["y"] = -0.5 })!["id"]!;
        Add("cantilever", 2);
        Add("stacked", 7, first);
        Add("brackets", 5, first);
        var (fixedGrid, template) = Both(f, "balconies");
        Same(fixedGrid, template);
    }

    [Fact]
    public void Elevators_template_gives_the_elevator_schedule()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Transport.TransportModule());
        for (var i = Spatial.Storeys(f.Model).Count(); i < 4; i++) f.Run("storey.create", new JsonObject { ["height"] = 3.0 });
        var ids = Spatial.Storeys(f.Model).Select(s => s.Id).ToList();
        foreach (var id in ids)
            f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = id, ["points"] = new JsonArray(new JsonArray(-5, -4), new JsonArray(5, -4), new JsonArray(5, 4), new JsonArray(-5, 4)) });
        string Place(string style, JsonObject? extra = null)
        {
            var a = new JsonObject { ["storeyId"] = ids[0], ["x"] = 0, ["y"] = 0, ["style"] = style };
            foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
            return (string)f.Run("transport.place", a)!["id"]!;
        }
        Place("accessible");
        Place("goods", new JsonObject { ["x"] = 4, ["topStoreyId"] = ids[1] });
        var (fixedGrid, template) = Both(f, "elevators");
        Same(fixedGrid, template);
    }

    [Fact]
    public void Skirtings_template_gives_the_skirting_schedule()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        string[] walls = [f.Wall(0, 0, 4, 0, type), f.Wall(4, 0, 4, 5, type), f.Wall(4, 5, 0, 5, type), f.Wall(0, 5, 0, 0, type)];
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 2 });
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 2.5 })!["id"]!;
        f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room });
        f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(3.9, 0.1), new JsonArray(3.9, 4.9)) });
        var oak = (string)f.Run("architecture.skirtingTypeFromLibrary", new JsonObject { ["name"] = "Oak 70 moulded" })!["id"]!;
        f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0.1, 0.1), new JsonArray(0.1, 4.9)), ["typeId"] = oak });
        var (fixedGrid, template) = Both(f, "skirtings");
        Same(fixedGrid, template);
    }

    [Fact]
    public void Furniture_template_gives_the_furniture_schedule()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Furniture.FurnitureModule());
        f.Wall(0, 0, 5, 0); f.Wall(5, 0, 5, 4); f.Wall(5, 4, 0, 4); f.Wall(0, 4, 0, 0);
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2, ["name"] = "Living" });
        f.Wall(5, 0, 9, 0); f.Wall(9, 0, 9, 4); f.Wall(9, 4, 5, 4);
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 7, ["y"] = 2, ["name"] = "Bedroom" });
        string Place(JsonObject a) { a["storeyId"] ??= f.Storey; return (string)f.Run("furniture.place", a)!["id"]!; }
        Place(new JsonObject { ["kind"] = "sofa", ["x"] = 2.5, ["y"] = 0.8 });
        Place(new JsonObject { ["kind"] = "armchair", ["x"] = 1, ["y"] = 2, ["against"] = false });
        Place(new JsonObject { ["kind"] = "armchair", ["x"] = 4, ["y"] = 2, ["against"] = false });
        Place(new JsonObject { ["kind"] = "armchair", ["x"] = 7, ["y"] = 2, ["against"] = false });
        Place(new JsonObject { ["kind"] = "armchair", ["x"] = 12, ["y"] = 2, ["against"] = false });
        Place(new JsonObject { ["kind"] = "bed", ["x"] = 7, ["y"] = 2.5, ["against"] = false });
        var (fixedGrid, template) = Both(f, "furniture");
        Same(fixedGrid, template);
    }

    [Fact]
    public void Mep_equipment_template_gives_the_equipment_schedule_but_for_the_pieces_unit()
    {
        var f = new MepFixture();
        var heating = f.System();
        var water = f.System(Lapin.Mep.Services.ColdWater);
        f.Node(heating, "boiler", 0, 0);
        f.Node(heating, "radiator", 3, 0);
        f.Node(heating, "radiator", 6, 0);
        f.Node(water, "waterMeter", 0, 4);
        f.Node(water, "basin", 3, 4);
        var (fixedGrid, template) = Both(f.Session, f.Run, "mep-equipment");
        // The same table, but the total's „pcs” under the units: a field schedule's totals line adds a column up, it writes no word of its own.
        var expected = fixedGrid with { Rows = [.. fixedGrid.Rows.SkipLast(1), [.. fixedGrid.Rows[^1].SkipLast(1), ""]] };
        Assert.Equal("pcs", fixedGrid.Rows[^1][^1]);
        Same(expected, template);
    }

    [Fact]
    public void Planting_template_lists_the_plants_as_parts_and_gives_the_planting_schedule()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Civil.CivilModule());
        f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 40, ["y2"] = 40, ["elevation"] = 0 });
        f.Run("civil.addPlanting", new JsonObject { ["points"] = new JsonArray(new JsonArray(5, 5), new JsonArray(35, 5), new JsonArray(35, 35), new JsonArray(5, 35)), ["density"] = 2, ["seed"] = 7 });
        f.Run("civil.addPlanting", new JsonObject { ["x"] = 2, ["y"] = 2 });
        var (fixedGrid, template) = Both(f, "planting");
        Same(fixedGrid, template);
    }

    static JsonArray Outline(double x, double y, double w, double d) => new(new JsonArray(x, y), new JsonArray(x + w, y), new JsonArray(x + w, y + d), new JsonArray(x, y + d));

    [Fact]
    public void Roof_timber_template_lists_the_members_as_parts_and_gives_the_cutting_list()
    {
        var f = new Fixture();
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline(0, 0, 10, 6), ["style"] = "se_truss", ["name"] = "Roof B" });
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline(20, 0, 8, 5), ["style"] = "traditional_campie" });
        var (fixedGrid, template) = Both(f, "roofTimber");
        Same(fixedGrid, template);
        // Kept as the user's own template (.lapinschedule) and made again: its parts, order, subtotals and formats go with it.
        var made = f.Model.OfClass(ScheduleEntity.Class).Single(x => x.Str("kind") == FieldSchedule.KindId);
        var text = (string)f.Session.Query("schedule.exportTemplate", new JsonObject { ["id"] = made.Id })!["text"]!;
        var again = (string)f.Run("architecture.importSchedule", new JsonObject { ["text"] = text, ["name"] = "Mine" })!["id"]!;
        Same(template, new FieldSchedule().Grid(f.Session.Reader, f.Model.Get(again)!));
    }

    [Fact]
    public void Timber_template_lists_walls_floors_and_roofs_members_and_gives_the_timber_schedule()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Studio.StudioModule(), new Lapin.Wood.WoodModule());
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 });
        f.Run("wood.setWallFrame", new JsonObject { ["id"] = Walls.TypeOf(f.Model, wall)!.Id, ["spec"] = Lapin.Wood.FrameSpec.Nordic.ToJson() });
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline(0, 10, 6, 4) })!["id"]!;
        f.Run("wood.setFloorJoists", new JsonObject { ["id"] = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id, ["spec"] = Lapin.Wood.JoistSpec.Default.ToJson() });
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline(20, 0, 10, 6), ["style"] = "se_truss" });
        var (fixedGrid, template) = Both(f, "timber");
        Same(fixedGrid, template);
    }

    [Fact]
    public void Scaffold_template_gives_the_scaffold_schedule()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Phases.PhasesModule(), new Lapin.Civil.CivilModule(), new Lapin.SiteWorks.SiteWorksModule());
        f.Run("sitework.addScaffold", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["height"] = 6 });
        f.Run("sitework.addScaffold", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 5), new JsonArray(7.3, 5), new JsonArray(7.3, 9)), ["height"] = 4.5 });
        var (fixedGrid, template) = Both(f, "scaffold");
        Same(fixedGrid, template);
    }

    [Fact]
    public void A_fixed_schedule_saved_by_this_build_opens_and_shows_the_same()
    {
        // No upgrade turns saved fixed schedules into field schedules: their kind stays, so they open as they were, with what
        // was typed in their cells (keyed by the type, which a field schedule's grouped row is not).
        var f = new Fixture(new ArchitectureModule());
        string Place(JsonObject a) { a["storeyId"] ??= f.Storey; return (string)f.Run("architecture.placeGate", a)!["id"]!; }
        Place(new JsonObject { ["kind"] = "sliding", ["x"] = 0, ["y"] = 0 });
        Place(new JsonObject { ["kind"] = "pedestrian", ["x"] = 0, ["y"] = 5 });
        var old = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "gates" })!["id"]!;
        var shown = ScheduleEntity.SourceGrid(f.Session.Reader, f.Model.Get(old)!, new GateSchedule()).ToGrid();
        var path = Path.Combine(Path.GetTempPath(), $"fixed-schedule-{Guid.NewGuid():N}.lapin");
        try
        {
            PackageFiles.Save(f.Session, path);
            var g = new Fixture(new ArchitectureModule());
            PackageFiles.Open(g.Session, path);
            var again = g.Model.Get(old)!;
            Assert.Equal("gates", again.Str("kind"));
            var reopened = ScheduleEntity.SourceGrid(g.Session.Reader, again, new GateSchedule()).ToGrid();
            Assert.Equal(shown.Columns, reopened.Columns);
            Assert.Equal(shown.Rows, reopened.Rows);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_ribbon_kind_is_an_alias_and_the_fixed_kind_still_makes_its_table()
    {
        var f = new Fixture();
        var shipped = (JsonArray)f.Session.Query("schedule.shippedTemplates", new JsonObject())!;
        Assert.Contains(shipped, t => (string?)t!["id"] == "sunshades" && (string?)t["kind"] == "sunshades");
        // The fixed kind stays: a script or a saved schedule of it shows its table as before.
        var old = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "sunshades" })!["id"]!;
        Assert.Equal("sunshades", f.Model.Get(old)!.Str("kind"));
        Assert.Throws<CommandException>(() => f.Run("architecture.importSchedule", new JsonObject { ["builtin"] = "nothing" }));
        // Parts of a module the edition lacks are refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "fields", ["fields"] = new JsonObject { ["source"] = "plants" } }));
    }
}
