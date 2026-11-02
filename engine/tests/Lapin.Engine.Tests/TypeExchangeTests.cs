using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Layered types taken out of one project and brought into another (report #52): JSON and spreadsheet CSV, a type of
/// the same kind and name changed, any other made, materials made by name; and the type assemblies schedule.
/// </summary>
public sealed class TypeExchangeTests
{
    static Fixture F() => new(new ArchitectureModule());

    static string Export(Fixture f, string format) => (string)f.Session.Query("architecture.exportTypes", new JsonObject { ["format"] = format })!["text"]!;
    static JsonObject Import(Fixture f, string text) => (JsonObject)f.Run("architecture.importTypes", new JsonObject { ["text"] = text })!;

    static List<(string Material, double Thickness, bool Core)> Layers(Fixture f, Entity type) =>
        LayerSets.LayersOf(f.Model, type.Id).Select(l => (f.Model.Get(l.Material)!.Name, Math.Round(l.Thickness, 6), l.Core)).ToList();

    static string Project(Fixture f)
    {
        f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "WT1 Exterior", ["mark"] = "WT1",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Render", ["thickness"] = 0.015 },
                new JsonObject { ["material"] = "EPS insulation", ["thickness"] = 0.1, ["name"] = "Thermal insulation" },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
                new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
        });
        return (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "F1 Ground floor",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 }, new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.15, ["core"] = true }),
        })!["id"]!;
    }

    [Theory]
    [InlineData("json")]
    [InlineData("csv")]
    public void A_project_s_wall_and_floor_types_go_into_another_with_their_layers(string format)
    {
        var from = F();
        Project(from);
        var text = Export(from, format);
        var to = F();
        var wallTypes = to.Model.OfClass(IfcClass.WallType).Count();
        var r = Import(to, text);
        Assert.Equal(2, (int)r["created"]!);
        var wall = to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior");
        Assert.Equal("WT1", wall.Str("mark"));
        Assert.Equal([("Render", 0.015, false), ("EPS insulation", 0.1, false), ("Brick", 0.25, true), ("Plaster", 0.015, false)], Layers(to, wall));
        Assert.Equal("Thermal insulation", LayerSets.LayersOf(to.Model, wall.Id)[1].Name);
        var floor = to.Model.OfClass(IfcClass.SlabType).Single(t => t.Name == "F1 Ground floor");
        Assert.Equal([("Screed", 0.05, false), ("Concrete", 0.15, true)], Layers(to, floor));
        Assert.Equal(wallTypes + 1, to.Model.OfClass(IfcClass.WallType).Count());

        // Brought in again: the same types changed, none made; one undo step took them all.
        r = Import(to, text);
        Assert.Equal(0, (int)r["created"]!);
        Assert.Equal(2, (int)r["updated"]!);
    }

    [Fact]
    public void A_spreadsheet_s_csv_with_semicolons_and_decimal_commas_makes_its_types_and_changes_a_project_s_own()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var typeId = f.Model.RelatingOf(wall, IfcClass.RelDefinesByType)!.Id;
        var name = f.Model.Get(typeId)!.Name;
        // As Excel saves it where the decimal mark is a comma: semicolons, a quoted name, "x" for the core, no kind (a wall).
        var csv = $"Type;Mark;Material;Thickness (mm);Core\r\n\"{name}\";W0;Plaster;12,5;\r\n\"{name}\";W0;Concrete;200;x\r\n\"{name}\";W0;Plaster;12,5;\r\nWT9 \"Party\" wall;;Brick;240;x\r\n";
        var r = Import(f, csv);
        Assert.Equal(1, (int)r["created"]!);
        Assert.Equal(1, (int)r["updated"]!);
        Assert.Equal([("Plaster", 0.0125, false), ("Concrete", 0.2, true), ("Plaster", 0.0125, false)], Layers(f, f.Model.Get(typeId)!));
        Assert.Equal("W0", f.Model.Get(typeId)!.Str("mark"));
        Assert.Single(f.Model.OfClass(IfcClass.WallType), t => t.Name == "WT9 \"Party\" wall");
        // The wall of the changed type took its new thickness: 225 mm across.
        var g = f.Session.Geometry(wall)!;
        var bounds = g.Parts.Select(p => Lapin.Geometry.Measure.Bounds(p.Solid)!.Value).ToList();
        Assert.Equal(0.225, bounds.Max(b => b.Max.Y) - bounds.Min(b => b.Min.Y), 6);
    }

    [Fact]
    public void A_file_without_what_a_type_needs_is_refused_whole()
    {
        var f = F();
        var before = f.Model.OfClass(IfcClass.WallType).Count();
        Assert.Throws<CommandException>(() => Import(f, "Type,Material\nA,Brick\n"));
        Assert.Throws<CommandException>(() => Import(f, "Type,Material,Thickness (mm)\nA,Brick,abc\n"));
        Assert.Throws<CommandException>(() => Import(f, "Kind,Type,Material,Thickness (mm)\nroof,A,Tiles,30\n"));
        Assert.Throws<CommandException>(() => Import(f, "{\"types\": [{\"name\": \"A\", \"layers\": []}]}"));
        Assert.Throws<CommandException>(() => Import(f, "{ not json"));
        Assert.Equal(before, f.Model.OfClass(IfcClass.WallType).Count());
    }

    [Fact]
    public void The_type_assemblies_schedule_lists_each_type_its_thickness_its_elements_and_its_layers()
    {
        var f = F();
        Project(f);
        var type = f.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior").Id;
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0, ["typeId"] = type });
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 4, ["typeId"] = type });
        var grid = new TypeAssemblies().Grid(new Q(f));
        Assert.Equal(["Kind", "Type", "Mark", "Layer", "Material", "Thickness (mm)", "Core", "Elements"], grid.Columns);
        var at = grid.Rows.ToList().FindIndex(r => r[1] == "WT1 Exterior");
        Assert.Equal(["Wall", "WT1 Exterior", "WT1", "", "", "380", "", "2"], grid.Rows[at]);
        Assert.Equal(["", "", "", "Thermal insulation", "EPS insulation", "100", "", ""], grid.Rows[at + 2]);
        Assert.Equal(["", "", "", "3", "Brick", "250", "core", ""], grid.Rows[at + 3]);
        Assert.Contains(grid.Rows, r => r[0] == "Floor" && r[1] == "F1 Ground floor" && r[5] == "200" && r[7] == "0");
        // A schedule of this kind is made from it.
        Assert.NotNull(f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "types" })!["id"]);
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
