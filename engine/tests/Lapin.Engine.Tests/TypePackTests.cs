using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Type packs (docs/plan-extensions.md, §9.3; a .lapintypes file): chosen types taken out of a project with what they are
/// made of (their layer sets, the materials they name, the resources they point at) and brought into another by
/// types.importFrom: materials matched by name or made, an identical type reused, a type of the same name that differs
/// made beside it under a new name, the chosen ones only, all in one undo step; a bad pack refused whole.
/// </summary>
public sealed class TypePackTests
{
    static Fixture F() => new(new ArchitectureModule());

    static string WallType(Fixture f, string brick = "Brick") => (string)f.Run("architecture.createWallType", new JsonObject
    {
        ["name"] = "WT1 Exterior", ["mark"] = "WT1",
        ["layers"] = new JsonArray(
            new JsonObject { ["material"] = "Render", ["thickness"] = 0.015 },
            new JsonObject { ["material"] = "EPS insulation", ["thickness"] = 0.1, ["name"] = "Thermal insulation" },
            new JsonObject { ["material"] = brick, ["thickness"] = 0.25, ["core"] = true },
            new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
    })!["id"]!;

    static string DoorType(Fixture f) => (string)f.Run("architecture.createJoineryType", new JsonObject
    {
        ["spec"] = (JoinerySpec.DefaultDoor("D1 Oak") with { Width = 1.0 }).ToJson(),
    })!["id"]!;

    static string Export(Fixture f, params string[] ids) =>
        (string)f.Session.Query("types.export", new JsonObject { ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()), ["name"] = "Test pack" })!["text"]!;

    static JsonObject Import(Fixture f, string text, params string[] refs)
    {
        var a = new JsonObject { ["text"] = text };
        if (refs.Length > 0) a["refs"] = new JsonArray(refs.Select(r => (JsonNode)r).ToArray());
        return (JsonObject)f.Run("types.importFrom", a)!;
    }

    static List<(string Material, double Thickness, bool Core)> Layers(Fixture f, Entity type) =>
        LayerSets.LayersOf(f.Model, type.Id).Select(l => (f.Model.Get(l.Material)!.Name, Math.Round(l.Thickness, 6), l.Core)).ToList();

    static Entity Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).Single(m => m.Name == name);

    [Fact]
    public void A_wall_and_a_door_type_go_into_another_project_with_their_materials_matched_by_name_or_made()
    {
        var from = F();
        var wall = WallType(from);
        var door = DoorType(from);
        from.Run("material.update", new JsonObject { ["id"] = Material(from, "EPS insulation").Id, ["color"] = "#f0e68c" });
        from.Run("material.update", new JsonObject { ["id"] = Material(from, "Door leaf").Id, ["color"] = "#8b5a2b" });
        var text = Export(from, wall, door);
        var pack = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(TypePacks.Format, (string?)pack["format"]);
        Assert.Equal(2, pack["types"]!.AsArray().Count);

        var to = F();
        // The project has its own Brick: matched by name, not made again, its colour left as it is.
        to.Run("architecture.createWallType", new JsonObject { ["name"] = "Own", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true }) });
        var brick = Material(to, "Brick");
        var materials = to.Model.OfClass(IfcClass.Material).Count();

        var r = Import(to, text);
        Assert.Equal(2, (int)r["created"]!);
        Assert.Equal(0, (int)r["reused"]!);
        var w = to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior");
        Assert.Equal("WT1", w.Str("mark"));
        Assert.Equal([("Render", 0.015, false), ("EPS insulation", 0.1, false), ("Brick", 0.25, true), ("Plaster", 0.015, false)], Layers(to, w));
        Assert.Equal("Thermal insulation", LayerSets.LayersOf(to.Model, w.Id)[1].Name);
        Assert.Same(brick, Material(to, "Brick"));
        Assert.Single(to.Model.OfClass(IfcClass.Material), m => m.Name == "Brick");
        // A material the project lacked comes with what it was in the pack (its colour).
        Assert.Equal("#f0e68c", Material(to, "EPS insulation").Str("color"));
        // The door's materials are named in its spec: they come too, as the pack has them, so the door is drawn with them.
        var d = to.Model.OfClass(IfcClass.DoorType).Single(t => t.Name == "D1 Oak");
        Assert.Equal("Door leaf", JoinerySpec.From(d.Attrs["spec"])!.Materials!.Leaf);
        Assert.Equal("#8b5a2b", Material(to, "Door leaf").Str("color"));
        Assert.Single(to.Model.OfClass(IfcClass.Material), m => m.Name == "Door frame");
        Assert.True(to.Model.OfClass(IfcClass.Material).Count() > materials);

        // A wall of the imported type is placed and built, a door of the other in it.
        var placed = to.Wall(0, 0, 5, 0, w.Id);
        Assert.Equal(w.Id, to.Model.RelatingOf(placed, IfcClass.RelDefinesByType)!.Id);
        var (min, max) = to.Bounds(placed);
        Assert.Equal(0.38, Math.Round(max.Y - min.Y, 3));
        var fill = (string)to.Run("architecture.addDoor", new JsonObject { ["wallId"] = placed, ["along"] = 2.5, ["typeId"] = d.Id })!["id"]!;
        Assert.Equal(d.Id, to.Model.RelatingOf(fill, IfcClass.RelDefinesByType)!.Id);
    }

    [Fact]
    public void An_identical_type_is_reused_and_nothing_is_added_the_second_time()
    {
        var from = F();
        var text = Export(from, WallType(from), DoorType(from));
        var to = F();
        Import(to, text);
        var entities = to.Model.Entities.Count();
        var relations = to.Model.Relations.Count();
        var r = Import(to, text);
        Assert.Equal(0, (int)r["created"]!);
        Assert.Equal(2, (int)r["reused"]!);
        Assert.Equal(entities, to.Model.Entities.Count());
        Assert.Equal(relations, to.Model.Relations.Count());
        Assert.All(r["types"]!.AsArray(), t => Assert.Equal("reused", (string?)t!["status"]));

        // Into the project it came from: the very types, reused.
        r = Import(from, text);
        Assert.Equal(2, (int)r["reused"]!);
        Assert.Single(from.Model.OfClass(IfcClass.WallType), t => t.Name == "WT1 Exterior");
    }

    [Fact]
    public void A_type_of_the_same_name_that_differs_is_kept_and_the_pack_s_comes_beside_it_renamed()
    {
        var from = F();
        var text = Export(from, WallType(from));
        var to = F();
        var own = WallType(to, brick: "Concrete");
        var r = Import(to, text);
        Assert.Equal(1, (int)r["created"]!);
        Assert.Equal("renamed", (string?)r["types"]![0]!["status"]);
        Assert.Equal("Concrete", Layers(to, to.Model.Get(own)!)[2].Material);
        var made = to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior (2)");
        Assert.Equal("Brick", Layers(to, made)[2].Material);
        Assert.Equal("WT1 Exterior (2)", (string?)r["types"]![0]!["name"]);
    }

    [Fact]
    public void Only_the_chosen_types_come_and_only_what_they_need()
    {
        var from = F();
        var wall = WallType(from);
        var door = DoorType(from);
        var text = Export(from, wall, door);
        var refs = (JsonObject)from.Session.Query("types.readPack", new JsonObject { ["text"] = text })!;
        Assert.Equal("Test pack", (string?)refs["name"]);
        var listed = refs["types"]!.AsArray().Select(t => ((string)t!["name"]!, (string)t["class"]!, (string)t["ref"]!)).ToList();
        Assert.Contains(listed, t => t.Item1 == "D1 Oak" && t.Item2 == IfcClass.DoorType);
        var to = F();
        Import(to, text, listed.Single(t => t.Item1 == "WT1 Exterior").Item3);
        Assert.Single(to.Model.OfClass(IfcClass.WallType), t => t.Name == "WT1 Exterior");
        Assert.DoesNotContain(to.Model.OfClass(IfcClass.DoorType), t => t.Name == "D1 Oak");
        Assert.DoesNotContain(to.Model.OfClass(IfcClass.Material), m => m.Name == "Door leaf");
    }

    [Fact]
    public void One_undo_takes_back_the_types_their_layer_sets_and_the_materials_made()
    {
        var from = F();
        var text = Export(from, WallType(from), DoorType(from));
        var to = F();
        var entities = to.Model.Entities.Select(e => e.Id).ToHashSet();
        var relations = to.Model.Relations.Count();
        Import(to, text);
        Assert.Contains(to.Model.OfClass(IfcClass.Material), m => m.Name == "EPS insulation");
        to.Session.Undo();
        Assert.Equal(entities, to.Model.Entities.Select(e => e.Id).ToHashSet());
        Assert.Equal(relations, to.Model.Relations.Count());
        to.Session.Redo();
        Assert.Single(to.Model.OfClass(IfcClass.WallType), t => t.Name == "WT1 Exterior");
    }

    [Theory]
    [InlineData("{\"format\":\"lapin-types\",\"version\":1,\"types\":[]}", "not a Lapin type pack")]
    [InlineData("{\"format\":\"lapin-type-pack\",\"version\":2,\"types\":[]}", "newer Lapin")]
    [InlineData("{\"format\":\"lapin-type-pack\",\"version\":1,\"types\":[]}", "no type")]
    [InlineData("{\"format\":\"lapin-type-pack\",\"version\":1,\"types\":[{\"ref\":\"a\",\"class\":\"IfcWall\",\"name\":\"W\",\"attrs\":{}}]}", "not a kind of type")]
    [InlineData("{\"format\":\"lapin-type-pack\",\"version\":1,\"types\":[{\"ref\":\"a\",\"class\":\"IfcWallType\",\"name\":\"W\",\"attrs\":{}}],\"resources\":[{\"ref\":\"b\",\"class\":\"IfcBuildingStorey\",\"name\":\"S\",\"attrs\":{}}]}", "cannot come with types")]
    public void A_bad_pack_is_refused_whole(string text, string why)
    {
        var f = F();
        var before = f.Model.Entities.Count();
        var e = Assert.ThrowsAny<Exception>(() => Import(f, text));
        Assert.Contains(why, e.Message);
        Assert.Equal(before, f.Model.Entities.Count());
    }

    [Fact]
    public void A_steel_beam_type_brings_its_profile_copy_and_its_material_and_a_project_s_identical_copy_is_reused()
    {
        static Fixture S() => new(new Lapin.Structure.StructureModule(), new Lapin.Steel.SteelModule());
        var p = Lapin.Platform.ProfileLibraries.BuiltInProfiles.Find("HEB 240")!;
        var from = S();
        var beam = (string)from.Run("steel.createType", new JsonObject { ["profile"] = p.ToJson(), ["library"] = Lapin.Platform.ProfileLibraries.BuiltInProfiles.Name })!["id"]!;
        var text = Export(from, beam);

        var to = S();
        var r = Import(to, text);
        var made = to.Model.Get((string)r["types"]![0]!["id"]!)!;
        var copy = to.Model.OfClass(Lapin.Platform.ProfileLibraries.ProjectProfiles.Class).Single();
        Assert.Equal(copy.Id, (string?)made.Attrs["section"]!["profileId"]);
        Assert.Equal("Steel", to.Model.Get((string)made.Attrs["material"]!)!.Name);

        // A project that copied the same profile from the same library for its own type: that copy is the one the pack's type takes.
        var other = S();
        other.Run("structure.createBeamType", new JsonObject { ["profile"] = p.ToJson(), ["library"] = Lapin.Platform.ProfileLibraries.BuiltInProfiles.Name });
        var own = other.Model.OfClass(Lapin.Platform.ProfileLibraries.ProjectProfiles.Class).Single();
        r = Import(other, text);
        Assert.Single(other.Model.OfClass(Lapin.Platform.ProfileLibraries.ProjectProfiles.Class));
        Assert.Equal(own.Id, (string?)other.Model.Get((string)r["types"]![0]!["id"]!)!.Attrs["section"]!["profileId"]);
    }

    static JsonObject Read(Fixture f, string text) => (JsonObject)f.Session.Query("types.readPack", new JsonObject { ["text"] = text })!;

    static JsonObject Choose(Fixture f, string text, JsonObject? materials = null, JsonObject? types = null)
    {
        var a = new JsonObject { ["text"] = text };
        if (materials != null) a["materials"] = materials;
        if (types != null) a["types"] = types;
        return (JsonObject)f.Run("types.importFrom", a)!;
    }

    /// <summary>A pack of WT1 whose brick is hatched at twice the scale, and a project with WT1 on concrete and a brick of its own hatch.</summary>
    static (string Text, Fixture To, string OwnType, string Wall) Library()
    {
        var from = F();
        var type = WallType(from);
        from.Run("material.update", new JsonObject { ["id"] = Material(from, "Brick").Id, ["section"] = new JsonObject { ["hatchScale"] = 2.0 } });
        var text = Export(from, type);
        var to = F();
        to.Session.Edit("Brick", e => Materials.Ensure(e, "Brick"));
        var own = WallType(to, brick: "Concrete");
        var wall = to.Wall(0, 0, 5, 0, own);
        return (text, to, own, wall);
    }

    [Fact]
    public void Before_importing_the_pack_says_which_materials_and_types_of_the_same_name_differ_and_how()
    {
        var (text, to, _, _) = Library();
        var r = Read(to, text);
        var brick = Assert.Single(r["materials"]!.AsArray(), m => (string?)m!["name"] == "Brick")!;
        Assert.Contains("section hatch", brick["differs"]!.AsArray().Select(x => (string?)x));
        var wt = r["types"]!.AsArray().Single()!;
        Assert.True((bool)wt["exists"]!);
        Assert.Equal(["layers"], wt["differs"]!.AsArray().Select(x => (string?)x));
        // Its own project: nothing differs.
        var from = F();
        var same = Export(from, WallType(from));
        var again = Read(from, same);
        Assert.Empty(again["materials"]!.AsArray());
        Assert.Null(again["types"]![0]!["differs"]);
    }

    [Fact]
    public void Without_choices_the_project_s_material_is_kept_and_the_import_says_so()
    {
        var (text, to, _, _) = Library();
        var before = Material(to, "Brick").Attrs.ToJsonString();
        var r = Import(to, text);
        Assert.Equal(1, (int)r["materialsKept"]!);
        Assert.Equal(before, Material(to, "Brick").Attrs.ToJsonString());
        Assert.Equal("renamed", (string?)r["types"]![0]!["status"]);
    }

    [Fact]
    public void A_material_updated_from_the_pack_keeps_its_id_and_one_kept_beside_is_the_new_type_s()
    {
        var (text, to, _, _) = Library();
        var id = Material(to, "Brick").Id;
        var r = Choose(to, text, new JsonObject { ["Brick"] = "update" });
        Assert.Equal(1, (int)r["materialsUpdated"]!);
        Assert.Equal(id, Material(to, "Brick").Id);
        Assert.Equal(2.0, (double)Material(to, "Brick").Attrs["section"]!["hatchScale"]!);
        to.Session.Undo();
        Assert.Null(Material(to, "Brick").Attrs["section"]?["hatchScale"]);

        r = Choose(to, text, new JsonObject { ["brick"] = "both" });
        Assert.Equal(["Brick (2)"], r["materialsRenamed"]!.AsArray().Select(x => (string?)x));
        var made = to.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "WT1 Exterior (2)");
        Assert.Equal("Brick (2)", Layers(to, made)[2].Material);
        Assert.Null(Material(to, "Brick").Attrs["section"]?["hatchScale"]);
    }

    [Fact]
    public void A_type_updated_from_the_pack_keeps_its_id_and_its_walls_follow_and_one_skipped_stays()
    {
        var (text, to, own, wall) = Library();
        var setBefore = to.Model.RelatingOf(own, IfcClass.RelAssociatesMaterial)!.Id;
        var r = Choose(to, text, types: new JsonObject { ["WT1 Exterior"] = "update" });
        Assert.Equal(1, (int)r["updated"]!);
        Assert.Equal(own, (string?)r["types"]![0]!["id"]);
        Assert.Equal("Brick", Layers(to, to.Model.Get(own)!)[2].Material);
        Assert.Equal(setBefore, to.Model.RelatingOf(own, IfcClass.RelAssociatesMaterial)!.Id);
        Assert.Single(to.Model.OfClass(IfcClass.WallType), t => t.Name.StartsWith("WT1 Exterior", StringComparison.Ordinal));
        // The wall on the new layers: its usage still points at the type's set.
        Assert.Equal(setBefore, to.Usage(wall).ForLayerSet);
        to.Session.Undo();
        Assert.Equal("Concrete", Layers(to, to.Model.Get(own)!)[2].Material);

        r = Choose(to, text, types: new JsonObject { ["WT1 Exterior"] = "skip" });
        Assert.Equal(1, (int)r["skipped"]!);
        Assert.Equal("Concrete", Layers(to, to.Model.Get(own)!)[2].Material);
        Assert.Single(to.Model.OfClass(IfcClass.WallType), t => t.Name.StartsWith("WT1 Exterior", StringComparison.Ordinal));
        Assert.Throws<CommandException>(() => Choose(to, text, types: new JsonObject { ["WT1 Exterior"] = "merge" }));
    }

    [Fact]
    public void A_type_s_parameters_and_instance_defaults_travel_in_the_pack()
    {
        var from = F();
        var type = WallType(from);
        from.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Pset_WallCommon", ["name"] = "FireRating", ["value"] = "EI 60" });
        from.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "RAVA_Wall", ["name"] = "Renovation", ["kind"] = "text", ["value"] = "new", ["instance"] = true });
        var to = F();
        var made = (string)Import(to, Export(from, type))["types"]![0]!["id"]!;
        var ps = to.Model.Get(made)!.Attrs["params"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(ps, p => (string?)p["pset"] == "Pset_WallCommon" && (string?)p["value"] == "EI 60");
        Assert.Contains(ps, p => (string?)p["name"] == "Renovation" && p["instance"] is not null && (bool)p["instance"]!);
        var wall = to.Wall(0, 0, 4, 0, made);
        Assert.Equal("new", (string?)to.Model.Get(wall)!.Attrs["params"]![0]!["value"]);
    }

    [Fact]
    public void Only_types_are_exported()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        Assert.ThrowsAny<Exception>(() => Export(f, wall));
    }
}
