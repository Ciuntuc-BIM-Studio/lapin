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

    [Theory]
    [InlineData("json")]
    [InlineData("csv")]
    public void Types_come_into_a_project_that_has_no_wall_or_floor_type(string format)
    {
        var from = F();
        Project(from);
        var text = Export(from, format);
        var to = F();
        // A tester's report: the list of wall types empty, the import did nothing.
        to.Session.Edit("No types", e =>
        {
            foreach (var t in to.Model.OfClass(IfcClass.WallType).Concat(to.Model.OfClass(IfcClass.SlabType)).ToList()) e.Remove(t.Id);
        });
        Assert.Empty(to.Model.OfClass(IfcClass.WallType));
        Assert.Empty(to.Model.OfClass(IfcClass.SlabType));
        var r = Import(to, text);
        Assert.Equal(2, (int)r["created"]!);
        Assert.Equal(["WT1 Exterior"], to.Model.OfClass(IfcClass.WallType).Select(t => t.Name));
        Assert.Equal(["F1 Ground floor"], to.Model.OfClass(IfcClass.SlabType).Select(t => t.Name));
        Assert.Equal(4, Layers(to, to.Model.OfClass(IfcClass.WallType).Single()).Count);
    }

    static JsonObject Choose(Fixture f, string text, JsonObject? materials = null, JsonObject? types = null)
    {
        var a = new JsonObject { ["text"] = text };
        if (materials != null) a["materials"] = materials;
        if (types != null) a["types"] = types;
        return (JsonObject)f.Run("architecture.importTypes", a)!;
    }

    static Entity Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).Single(m => m.Name == name);

    /// <summary>A project whose WT1 has a clinker brick hatched at twice the scale, a fire rating and an instance default.</summary>
    static string Library(Fixture from)
    {
        var type = (string)from.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "WT1 Exterior", ["mark"] = "WT1",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Clinker brick", ["thickness"] = 0.25, ["core"] = true, ["category"] = "Structure", ["extendDown"] = true },
                new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
        })!["id"]!;
        from.Run("material.update", new JsonObject { ["id"] = Material(from, "Clinker brick").Id, ["section"] = new JsonObject { ["hatchScale"] = 2.0, ["hatchAngle"] = 30.0, ["hatchPen"] = 3 } });
        from.Run("material.update", new JsonObject { ["id"] = Material(from, "Plaster").Id, ["color"] = "#fafafa" });
        from.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = true });
        from.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Pset_WallCommon", ["name"] = "FireRating", ["value"] = "EI 60" });
        from.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "RAVA_Wall", ["name"] = "Renovation", ["kind"] = "text", ["value"] = "new", ["instance"] = true });
        return Export(from, "json");
    }

    [Fact]
    public void The_json_carries_the_materials_graphics_and_the_type_s_parameters_and_a_project_makes_them_as_they_were()
    {
        var text = Library(F());
        var doc = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(2, (int)doc["version"]!);
        var to = F();
        to.Session.Edit("Plaster", e => Materials.Ensure(e, "Plaster"));
        var r = Import(to, text);
        Assert.Equal(1, (int)r["created"]!);
        var clinker = Material(to, "Clinker brick");
        Assert.Equal(2.0, (double)clinker.Attrs["section"]!["hatchScale"]!);
        Assert.Equal(30.0, (double)clinker.Attrs["section"]!["hatchAngle"]!);
        Assert.Equal(3, (int)clinker.Attrs["section"]!["hatchPen"]!);
        var type = to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior");
        var ps = type.Attrs["params"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(ps, p => (string?)p["name"] == "FireRating" && (string?)p["value"] == "EI 60" && (string?)p["pset"] == "Pset_WallCommon");
        Assert.Contains(ps, p => (string?)p["name"] == "IsExternal" && (bool)p["value"]!);
        Assert.Contains(ps, p => (string?)p["name"] == "Renovation" && (bool)p["instance"]!);
        var core = LayerSets.LayersOf(to.Model, type.Id)[0];
        Assert.Equal("Structure", core.Category);
        Assert.True(core.ExtendDown);
        // The project's Plaster differs (its colour): kept as it was, said so.
        Assert.NotEqual("#fafafa", Material(to, "Plaster").Str("color"));
        Assert.Equal(1, (int)r["materials"]!["kept"]!);
    }

    [Fact]
    public void Before_importing_the_json_says_what_differs_and_the_choices_update_keep_both_or_skip()
    {
        var text = Library(F());
        var to = F();
        Project(to);   // WT1 Exterior of other layers
        var plaster = Material(to, "Plaster").Id;
        var preview = (JsonObject)to.Session.Query("architecture.readTypes", new JsonObject { ["text"] = text })!;
        var wt = preview["types"]!.AsArray().Single()!;
        Assert.True((bool)wt["exists"]!);
        Assert.Contains("layers", wt["differs"]!.AsArray().Select(x => (string?)x));
        Assert.Contains("parameters", wt["differs"]!.AsArray().Select(x => (string?)x));
        var m = Assert.Single(preview["materials"]!.AsArray())!;
        Assert.Equal("Plaster", (string?)m["name"]);
        Assert.Equal(["colour"], m["differs"]!.AsArray().Select(x => (string?)x));

        // Plaster updated in place, the type kept beside.
        var r = Choose(to, text, new JsonObject { ["Plaster"] = "update" }, new JsonObject { ["WT1 Exterior"] = "both" });
        Assert.Equal("#fafafa", to.Model.Get(plaster)!.Str("color"));
        Assert.Equal(["WT1 Exterior (2)"], r["renamed"]!.AsArray().Select(x => (string?)x));
        Assert.Equal(4, LayerSets.LayersOf(to.Model, to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior").Id).Count);
        to.Session.Undo();

        // Plaster kept beside as „Plaster (2)”, which the file's type takes; the project's type skipped.
        r = Choose(to, text, new JsonObject { ["Plaster"] = "both" }, new JsonObject { ["WT1 Exterior"] = "skip" });
        Assert.Equal(1, (int)r["skipped"]!);
        Assert.Equal(["Plaster (2)"], r["materials"]!["renamed"]!.AsArray().Select(x => (string?)x));
        Assert.Equal(4, LayerSets.LayersOf(to.Model, to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior").Id).Count);
        to.Session.Undo();

        // Plaster (2) taken by the type updated in place.
        r = Choose(to, text, new JsonObject { ["Plaster"] = "both" });
        Assert.Equal(1, (int)r["updated"]!);
        var layers = LayerSets.LayersOf(to.Model, to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior").Id);
        Assert.Equal(["Clinker brick", "Plaster (2)"], layers.Select(l => to.Model.Get(l.Material)!.Name));
    }

    [Fact]
    public void A_json_of_the_first_version_without_materials_or_parameters_still_imports()
    {
        var f = F();
        var r = Import(f, "{\"format\": \"lapin-types\", \"version\": 1, \"types\": [{\"kind\": \"wall\", \"name\": \"Old\", \"layers\": [{\"material\": \"Brick\", \"thickness\": 0.25, \"core\": true}]}]}");
        Assert.Equal(1, (int)r["created"]!);
        Assert.Null(f.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "Old").Attrs["params"]);
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
