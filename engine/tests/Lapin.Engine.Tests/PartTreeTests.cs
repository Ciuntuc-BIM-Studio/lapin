using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Studio's parts, stage P0 of decision 517 (docs/plan-studio-parametric.md §3.1, §9): a <c>LapinSolidType</c> carrying a part
/// definition, placed parts as ordinary Studio solids typed by it, a project saved with the families of decision 515 opened as
/// parts with the same ids and bodies, the <c>.lapinpart</c> file, and the part's tree the edit mode shows for a placed part and
/// for a solid drawn from a sketch.
/// </summary>
public sealed class PartTreeTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    // The angle bracket of FamilyTests: W wide, H high, T thick, L long; a shelf when HasShelf.
    static JsonObject Bracket() => new()
    {
        ["ifcClass"] = "IfcFurnishingElement",
        ["params"] = J(
            new JsonObject { ["key"] = "W", ["default"] = 0.4, ["unit"] = "m", ["group"] = "Size", ["description"] = "The width", ["published"] = true },
            new JsonObject { ["key"] = "H", ["default"] = 0.3 },
            new JsonObject { ["key"] = "T", ["default"] = 0.02, ["instance"] = false, ["published"] = false },
            new JsonObject { ["key"] = "L", ["default"] = 0.1 },
            new JsonObject { ["key"] = "HasShelf", ["kind"] = "bool", ["default"] = 0 }),
        ["bodies"] = J(
            new JsonObject { ["name"] = "Angle", ["plane"] = "xz", ["points"] = J(J(0, 0), J("W", 0), J("W", "T"), J("T", "T"), J("T", "H"), J(0, "H")), ["depth"] = "L" },
            new JsonObject { ["name"] = "Shelf", ["plane"] = "xy", ["when"] = "HasShelf", ["offset"] = J(0, 0, "H"), ["points"] = J(J(0, 0), J("W", 0), J("W", "L"), J(0, "L")), ["depth"] = "T" }),
    };

    static double Volume(Fixture f, string id) => f.Session.Quantities(id).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;

    [Fact]
    public void A_placed_part_is_a_Studio_solid_typed_by_its_part()
    {
        var f = F();
        var part = (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Bracket", ["def"] = Bracket() })!["id"]!;
        Assert.Equal(StudioModule.TypeClass, f.Model.Get(part)!.Class);
        // The parameter table's fields are kept.
        var w = f.Model.Get(part)!.Attrs["def"]!["params"]![0]!;
        Assert.Equal("m", (string)w["unit"]!);
        Assert.Equal("Size", (string)w["group"]!);
        Assert.True((bool)w["published"]!);
        var a = (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(1, 2), ["values"] = new JsonObject { ["W"] = 0.6 } })!["id"]!;
        var e = f.Model.Get(a)!;
        Assert.Equal(IfcClass.BuildingElementProxy, e.Class);
        Assert.Equal("part", e.Str("operation"));
        Assert.Equal("IfcFurnishingElement", e.Str("exportAs"));
        Assert.Equal(part, f.Model.RelatingOf(a, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal((0.6 * 0.02 + 0.28 * 0.02) * 0.1, Volume(f, a), 9);
        // A step of Studio's after its bodies: raised by a 3D move.
        f.Run("studio.addFeature", new JsonObject { ["id"] = a, ["feature"] = new JsonObject { ["op"] = "move", ["by"] = J(0, 0, 0.5) } });
        Assert.Equal(0.5, f.Bounds(a).Min.Z, 6);
        // Copied, the copy keeps its part.
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = J(a), ["op"] = "copy", ["by"] = J(3, 0) })!["ids"]![0]!;
        Assert.Equal(part, f.Model.RelatingOf(copy, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal(4, Args.PointOf(f.Model.Get(copy)!.Attrs["at"]).X, 6);
        // A placed part takes another part, never a plain solid type.
        Assert.Throws<CommandException>(() => f.Run("studio.setType", new JsonObject { ["ids"] = J(a), ["name"] = "Plain" }));
        // A part in use is not deleted.
        Assert.Throws<CommandException>(() => f.Run("studio.deletePart", new JsonObject { ["id"] = part }));
        // Listed with the project's solid types.
        var parts = f.Session.Query("studio.parts", new JsonObject())!["parts"]!.AsArray();
        Assert.Equal(2, (int)parts.Single(p => (string)p!["id"]! == part)!["uses"]!);
    }

    [Fact]
    public void A_solid_with_a_type_without_definition_keeps_its_recipe()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var sq = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = J(J(0, 0), J(1, 0), J(1, 1), J(0, 1)), ["closed"] = true } })!["ids"]![0]!;
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = sq, ["depth"] = 2 })!["id"]!;
        var type = (string)f.Run("studio.setType", new JsonObject { ["ids"] = J(solid), ["name"] = "Pillar" })!["id"]!;
        Assert.Equal(2, Volume(f, solid), 6);
        f.Run("studio.addFeature", new JsonObject { ["id"] = solid, ["feature"] = new JsonObject { ["op"] = "move", ["by"] = J(0, 0, 1), ["off"] = true } });
        Assert.Equal(2, Volume(f, solid), 6);

        // Its tree: the sketch on the plan, the extrusion and its step, no parameters yet.
        var tree = (JsonObject)f.Session.Query("studio.partTree", new JsonObject { ["id"] = solid })!;
        Assert.Equal(type, (string)tree["partTypeId"]!);
        Assert.Equal("Pillar", (string)tree["name"]!);
        Assert.Equal("recipe", (string)tree["kind"]!);
        Assert.Equal(["xy", "xz", "yz"], tree["planes"]!.AsArray().Select(p => (string)p!).ToArray());
        var sketch = tree["sketches"]!.AsArray().Single()!;
        Assert.Equal(sq, (string)sketch["id"]!);
        Assert.Equal(plan, (string)sketch["viewId"]!);
        Assert.Equal("plan", (string)sketch["plane"]!);
        Assert.Equal("profile", (string)sketch["role"]!);
        Assert.Equal([sq], sketch["shapeIds"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.Equal(1, (double)sketch["frame"]!["normal"]![2]!, 9);
        var steps = tree["steps"]!.AsArray();
        Assert.Equal(["extrude", "move"], steps.Select(s => (string)s!["kind"]!).ToArray());
        Assert.Equal([0, 1], steps.Select(s => (int)s!["index"]!).ToArray());
        Assert.Equal("Extrude", (string)steps[0]!["label"]!);
        Assert.True((bool)steps[1]!["suppressed"]!);
        Assert.Empty(tree["params"]!.AsArray());
        // Not a Studio solid: no tree.
        Assert.Throws<CommandException>(() => f.Session.Query("studio.partTree", new JsonObject { ["id"] = f.Storey }));
        // No file for a part drawn from a sketch yet.
        Assert.Throws<CommandException>(() => f.Session.Query("studio.exportPart", new JsonObject { ["id"] = type }));
    }

    [Fact]
    public void A_placed_part_s_tree_shows_its_bodies_as_steps_and_its_parameters_with_its_values()
    {
        var f = F();
        var part = (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Bracket", ["def"] = Bracket() })!["id"]!;
        var a = (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(0, 0), ["values"] = new JsonObject { ["W"] = 0.6 } })!["id"]!;
        var tree = (JsonObject)f.Session.Query("studio.partTree", new JsonObject { ["id"] = a })!;
        Assert.Equal(part, (string)tree["partTypeId"]!);
        Assert.Equal("Bracket", (string)tree["name"]!);
        Assert.Equal("formula", (string)tree["kind"]!);
        var sketches = tree["sketches"]!.AsArray();
        Assert.Equal(["xz", "xy"], sketches.Select(s => (string)s!["plane"]!).ToArray());
        Assert.Equal(["body:1", "body:2"], sketches.Select(s => (string)s!["id"]!).ToArray());
        Assert.Empty(sketches[0]!["shapeIds"]!.AsArray());
        // The shelf's plane lies H up.
        Assert.Equal(0.3, (double)sketches[1]!["frame"]!["origin"]![2]!, 9);
        var steps = tree["steps"]!.AsArray();
        Assert.Equal(["Angle", "Shelf"], steps.Select(s => (string)s!["label"]!).ToArray());
        Assert.Equal("HasShelf", (string)steps[1]!["when"]!);
        Assert.True((bool)steps[1]!["suppressed"]!);
        Assert.Null(steps[0]!["suppressed"]);
        var ps = tree["params"]!.AsArray();
        Assert.Equal(["W", "H", "T", "L", "HasShelf"], ps.Select(p => (string)p!["key"]!).ToArray());
        var w = ps[0]!;
        Assert.Equal(0.6, (double)w["value"]!, 9);
        Assert.Equal(0.4, (double)w["default"]!, 9);
        Assert.Equal("m", (string)w["unit"]!);
        Assert.Equal("Size", (string)w["group"]!);
        Assert.Equal("The width", (string)w["description"]!);
        Assert.True((bool)w["instance"]!);
        var t = ps[2]!;
        Assert.False((bool)t["instance"]!);
        Assert.False((bool)t["published"]!);
        Assert.Equal(0.02, (double)t["value"]!, 9);
        // The shelf asked for: no longer suppressed.
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["HasShelf"] = true } });
        tree = (JsonObject)f.Session.Query("studio.partTree", new JsonObject { ["id"] = a })!;
        Assert.Null(tree["steps"]![1]!["suppressed"]);
        Assert.Equal(1, (double)tree["params"]![4]!["value"]!, 9);
    }

    [Fact]
    public void A_part_goes_to_a_lapinpart_file_and_a_family_file_is_read_as_a_part()
    {
        var f = F();
        var part = (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Bracket", ["def"] = Bracket() })!["id"]!;
        var file = f.Session.Query("studio.exportPart", new JsonObject { ["id"] = part })!;
        Assert.Equal("Bracket.lapinpart", (string)file["file"]!);
        var text = (string)file["text"]!;
        var parsed = JsonNode.Parse(text)!;
        Assert.Equal("lapinpart", (string)parsed["format"]!);
        Assert.Equal(1, (int)parsed["version"]!);
        var g = F();
        var copy = (string)g.Run("studio.importPart", new JsonObject { ["text"] = text })!["id"]!;
        Assert.Equal("Bracket", g.Model.Get(copy)!.Name);
        Assert.Equal("m", (string)g.Model.Get(copy)!.Attrs["def"]!["params"]![0]!["unit"]!);
        // A .lapinfamily's text, as the families' export writes it.
        var family = (string)f.Session.Query("studio.exportFamily", new JsonObject { ["id"] = part })!["text"]!;
        Assert.Equal("lapin-family", (string)JsonNode.Parse(family)!["format"]!);
        var fromFamily = (string)g.Run("studio.importPart", new JsonObject { ["text"] = family })!["id"]!;
        var placed = (string)g.Run("studio.placePart", new JsonObject { ["typeId"] = fromFamily, ["storeyId"] = g.Storey, ["at"] = J(0, 0) })!["id"]!;
        Assert.Equal((0.4 * 0.02 + 0.28 * 0.02) * 0.1, Volume(g, placed), 9);
        Assert.Throws<CommandException>(() => g.Run("studio.importPart", new JsonObject { ["text"] = "{\"format\":\"lapintypes\"}" }));
        Assert.Throws<CommandException>(() => g.Run("studio.importPart", new JsonObject { ["text"] = "{\"format\":\"lapinpart\",\"version\":99,\"def\":{}}" }));
    }

    [Fact]
    public void A_project_saved_with_families_opens_as_parts_with_the_same_ids_and_bodies()
    {
        var f = F();
        // As today's code saved them: a LapinFamily with its checked definition, an object of it standing turned and mirrored with its own values.
        var family = new Entity(GlobalId.New(), FamilyPartUpgrade.OldClass, "Bracket", new JsonObject { ["def"] = Parts.Check(Bracket()) });
        var objectId = GlobalId.New();
        var obj = new Entity(objectId, FamilyPartUpgrade.OldInstanceClass, "Bracket", new JsonObject
        {
            ["familyId"] = family.Id, ["at"] = J(2, 1), ["rotation"] = 90, ["exportAs"] = "IfcFurnishingElement", ["mirror"] = true, ["z"] = 0.25,
            ["values"] = new JsonObject { ["W"] = 0.6, ["HasShelf"] = 1 },
        });
        var contained = new Relation(GlobalId.New(), IfcClass.RelContainedInSpatialStructure, f.Storey, [objectId]);
        f.Session.Load([.. f.Model.Entities, family, obj], [.. f.Model.Relations, contained], null);

        var type = f.Model.Get(family.Id)!;
        Assert.Equal(StudioModule.TypeClass, type.Class);
        Assert.Equal("Bracket", type.Name);
        var part = f.Model.Get(objectId)!;
        Assert.Equal(IfcClass.BuildingElementProxy, part.Class);
        Assert.Equal("part", part.Str("operation"));
        Assert.Null(part.Str("familyId"));
        Assert.Equal("IfcFurnishingElement", part.Str("exportAs"));
        Assert.Equal(family.Id, f.Model.RelatingOf(objectId, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, objectId)!.Id);
        // The same bodies: mirrored (x to −x), turned a quarter (x along y, y along −x), at (2, 1), 0.25 up.
        Assert.Equal(2, f.Session.Geometry(objectId)!.Parts.Count);
        var (min, max) = f.Bounds(objectId);
        Assert.Equal(1.9, min.X, 6); Assert.Equal(2, max.X, 6);
        Assert.Equal(0.4, min.Y, 6); Assert.Equal(1, max.Y, 6);
        Assert.Equal(0.25, min.Z, 6); Assert.Equal(0.57, max.Z, 6);
        Assert.Equal((0.6 * 0.02 + 0.28 * 0.02) * 0.1 + 0.6 * 0.1 * 0.02, Volume(f, objectId), 9);
        // The family commands still work on it.
        f.Run("studio.setFamilyValues", new JsonObject { ["ids"] = J(objectId), ["values"] = new JsonObject { ["HasShelf"] = null } });
        Assert.Single(f.Session.Geometry(objectId)!.Parts);
        Assert.Single(f.Session.Query("studio.families", new JsonObject())!["families"]!.AsArray());

        // Upgraded the same way twice: the type relation's id derived from the part's.
        var es = new List<Entity> { family, obj };
        var (once, r1) = f.Session.Upgraded(es, [contained]);
        var (twice, r2) = f.Session.Upgraded(es, [contained]);
        Assert.Equal(r1.Single(r => r.Class == IfcClass.RelDefinesByType).Id, r2.Single(r => r.Class == IfcClass.RelDefinesByType).Id);
        // Nothing to do on a project of parts.
        var (again, _) = f.Session.Upgraded(once, r1);
        Assert.Same(once.Single(e => e.Id == objectId), again.Single(e => e.Id == objectId));
    }
}
