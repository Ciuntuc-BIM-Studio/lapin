using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit S6 (docs/plan-studio-parametric.md §10): a part holds native elements. A wall (with its door) taken into a placed part is
/// kept in the part's definition and built again in every placed part: moved, turned and mirrored with it, edited from any one of
/// them, its length driven by a parameter, gone with its part, copied with new ids, written to IFC as a wall that is a part of the
/// placed part (IfcRelAggregates), counted once. Saved parts of formula corners open drawn from sketches (the part editor edits them).
/// </summary>
public sealed class PartMemberTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    static JsonObject Plinth() => new()
    {
        ["params"] = J(new JsonObject { ["key"] = "W", ["default"] = 4.0 }),
        ["bodies"] = J(new JsonObject { ["name"] = "Plinth", ["plane"] = "xy", ["points"] = J(J(0, -0.5), J(1, -0.5), J(1, -0.2), J(0, -0.2)), ["depth"] = 0.1 }),
    };

    /// <summary>A part, placed once at the origin, holding a 4 m wall along x with a door in it.</summary>
    static (string Part, string Placed, string Wall) Setup(Fixture f)
    {
        var part = (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Bay", ["def"] = Plinth() })!["id"]!;
        var placed = (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(0, 0) })!["id"]!;
        var wall = f.Wall(0, 0, 4, 0, height: 2.5);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2.0 });
        f.Run("studio.addPartMembers", new JsonObject { ["partId"] = placed, ["ids"] = J(wall) });
        return (part, placed, wall);
    }

    static string Place(Fixture f, string part, double x, double y, double rotation = 0, bool mirror = false) =>
        (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(x, y), ["rotation"] = rotation, ["mirror"] = mirror })!["id"]!;

    static List<Entity> Members(Fixture f, string placed) => [.. f.Model.RelatedOf(placed, IfcClass.RelAggregates)];
    static Entity WallOf(Fixture f, string placed) => Members(f, placed).Single(e => e.Class == IfcClass.Wall);
    static Vec2 Start(Entity w) => Args.PointOf(w.Attrs["start"]);
    static Vec2 End(Entity w) => Args.PointOf(w.Attrs["end"]);
    static void Near(Vec2 want, Vec2 got) { Assert.Equal(want.X, got.X, 6); Assert.Equal(want.Y, got.Y, 6); }
    static int Doors(Fixture f) => f.Model.OfClass(IfcClass.Door).Count();

    [Fact]
    public void A_wall_taken_into_a_part_is_built_in_every_placed_part_and_keeps_its_id()
    {
        var f = F();
        var (part, placed, wall) = Setup(f);
        var def = Parts.DefOf(f.Model.Get(part))!;
        Assert.Single(PartMembers.MembersOf(def));
        // The wall is the placed part's member, under its own id, with its door.
        Assert.Equal(wall, WallOf(f, placed).Id);
        Assert.Equal(1, Doors(f));
        // A second placed part, turned a quarter at (10, 0): its own wall, from (10, 0) to (10, 4), with its own door.
        var second = Place(f, part, 10, 0, 90);
        var w2 = WallOf(f, second);
        Assert.NotEqual(wall, w2.Id);
        Near(new Vec2(10, 0), Start(w2));
        Near(new Vec2(10, 4), End(w2));
        Assert.Equal(2, Doors(f));
        Assert.Equal(f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid)), f.Session.Geometry(w2.Id)!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
        // One undo step took the wall in: undone, the part holds nothing and the wall is a plain wall again.
        var f2 = F();
        var (p2, placed2, wall2) = Setup(f2);
        f2.Session.Undo();
        Assert.False(PartMembers.HasMembers(Parts.DefOf(f2.Model.Get(p2))));
        Assert.Null(f2.Model.Get(wall2)!.Str(PartMembers.KeyAttr));
        Assert.Empty(Members(f2, placed2));
    }

    [Fact]
    public void Members_move_turn_and_mirror_with_their_part_keeping_their_ids()
    {
        var f = F();
        var (part, placed, wall) = Setup(f);
        var second = Place(f, part, 10, 0);
        var id2 = WallOf(f, second).Id;
        f.Run("element.transform", new JsonObject { ["ids"] = J(second), ["op"] = "move", ["by"] = J(0, 5) });
        Assert.Equal(id2, WallOf(f, second).Id);
        Near(new Vec2(10, 5), Start(WallOf(f, second)));
        Near(new Vec2(14, 5), End(WallOf(f, second)));
        f.Run("element.transform", new JsonObject { ["ids"] = J(second), ["op"] = "rotate", ["center"] = J(10, 5), ["angle"] = 90 });
        Assert.Equal(id2, WallOf(f, second).Id);
        Near(new Vec2(10, 5), Start(WallOf(f, second)));
        Near(new Vec2(10, 9), End(WallOf(f, second)));
        // Mirrored about x = 10: the wall runs the same line (it lay on it), its part turned over.
        f.Run("element.transform", new JsonObject { ["ids"] = J(second), ["op"] = "mirror", ["a"] = J(12, 0), ["b"] = J(12, 1) });
        Assert.True(f.Model.Get(second)!.Bool("mirror"));
        var w = WallOf(f, second);
        Assert.Equal(id2, w.Id);
        Near(new Vec2(14, 5), Start(w));
        Near(new Vec2(14, 9), End(w));
        // The first placed part never moved.
        Near(new Vec2(0, 0), Start(f.Model.Get(wall)!));
        Assert.Equal(2, Doors(f));
    }

    [Fact]
    public void A_member_edited_in_one_placed_part_reaches_the_others()
    {
        var f = F();
        var (part, placed, wall) = Setup(f);
        var second = Place(f, part, 10, 0, 90);
        // The first placed part's wall made longer: the second's too, in its own frame.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["x2"] = 5.0, ["y2"] = 0.0 });
        Near(new Vec2(10, 5), End(WallOf(f, second)));
        Near(new Vec2(5, 0), End(f.Model.Get(wall)!));
        // Its member in the definition says so, in the frame it was taken in.
        var mo = PartMembers.MembersOf(Parts.DefOf(f.Model.Get(part))).OfType<JsonObject>().Single();
        Near(new Vec2(5, 0), Args.PointOf(mo["attrs"]!["end"]));
        // A member deleted in one: gone from the part and from every placed part.
        f.Run("element.delete", new JsonObject { ["ids"] = J(WallOf(f, second).Id) });
        Assert.False(PartMembers.HasMembers(Parts.DefOf(f.Model.Get(part))));
        Assert.Null(f.Model.Get(wall));
        Assert.Equal(0, Doors(f));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(wall));
        Assert.Equal(2, Doors(f));
    }

    [Fact]
    public void A_parameter_drives_a_member_s_length()
    {
        var f = F();
        var (part, placed, wall) = Setup(f);
        var second = Place(f, part, 0, 10);
        f.Run("studio.setPartMemberDrives", new JsonObject { ["partId"] = placed, ["id"] = wall, ["drives"] = new JsonObject { ["length"] = "W" } });
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(second), ["values"] = new JsonObject { ["W"] = 6.0 } });
        Near(new Vec2(6, 10), End(WallOf(f, second)));
        Near(new Vec2(4, 0), End(f.Model.Get(wall)!));
        // The default changed: the first follows.
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = placed, ["key"] = "W", ["changes"] = new JsonObject { ["default"] = 3.0 } });
        Near(new Vec2(3, 0), End(f.Model.Get(wall)!));
        Near(new Vec2(6, 10), End(WallOf(f, second)));
        // A drive that does not read is refused.
        Assert.Throws<CommandException>(() => f.Run("studio.setPartMemberDrives", new JsonObject { ["partId"] = placed, ["id"] = wall, ["drives"] = new JsonObject { ["length"] = "Nope + 1" } }));
        Assert.Throws<CommandException>(() => f.Run("studio.setPartMemberDrives", new JsonObject { ["partId"] = placed, ["id"] = wall, ["drives"] = new JsonObject { ["colour"] = "W" } }));
        // The tree lists the member, its drive and what could be driven.
        var tree = (JsonObject)f.Session.Query("studio.partTree", new JsonObject { ["id"] = placed })!;
        var member = tree["members"]!.AsArray().Single()!.AsObject();
        Assert.Equal("W", (string?)member["drives"]!["length"]);
        Assert.Contains("length", member["drivable"]!.AsArray().Select(x => (string?)x));
    }

    [Fact]
    public void Members_go_with_their_part_and_a_copy_gets_its_own()
    {
        var f = F();
        var (part, placed, wall) = Setup(f);
        var r = f.Run("element.transform", new JsonObject { ["ids"] = J(placed), ["op"] = "copy", ["by"] = J(0, 6) })!;
        var copy = f.Model.OfClass(Parts.InstanceClass).Single(e => Parts.IsInstance(e) && e.Id != placed).Id;
        var w2 = WallOf(f, copy);
        Assert.NotEqual(wall, w2.Id);
        Assert.Equal(wall, WallOf(f, placed).Id);
        Near(new Vec2(0, 6), Start(w2));
        Assert.Equal(2, Doors(f));
        // Deleted, a placed part takes its members with it.
        f.Run("element.delete", new JsonObject { ["ids"] = J(copy) });
        Assert.Null(f.Model.Get(w2.Id));
        Assert.Equal(1, Doors(f));
        Assert.Equal(wall, WallOf(f, placed).Id);
        // A member can be let go: it stays, a plain wall.
        f.Run("studio.removePartMembers", new JsonObject { ["partId"] = placed, ["ids"] = J(wall) });
        Assert.Null(f.Model.Get(wall)!.Str(PartMembers.KeyAttr));
        Assert.False(PartMembers.HasMembers(Parts.DefOf(f.Model.Get(part))));
        Assert.Empty(Members(f, placed));
        _ = r;
    }

    [Fact]
    public void A_member_cannot_be_grouped_on_its_own_nor_be_a_part_or_hosted()
    {
        var f = F();
        var (part, placed, wall) = Setup(f);
        Assert.Throws<CommandException>(() => f.Run("group.create", new JsonObject { ["ids"] = J(wall) }));
        var door = f.Model.OfClass(IfcClass.Door).Single().Id;
        var other = Place(f, part, 20, 0);
        Assert.Throws<CommandException>(() => f.Run("studio.addPartMembers", new JsonObject { ["partId"] = other, ["ids"] = J(door) }));
        Assert.Throws<CommandException>(() => f.Run("studio.addPartMembers", new JsonObject { ["partId"] = other, ["ids"] = J(placed) }));
    }

    [Fact]
    public void Members_go_to_IFC_as_their_own_classes_parts_of_the_placed_part_counted_once()
    {
        var f = F();
        var (part, placed, wall) = Setup(f);
        var second = Place(f, part, 10, 0, 90);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "parts.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(2, Regex.Matches(ifc, @"=IFCWALL(STANDARDCASE)?\(").Count);
        Assert.Equal(2, Regex.Matches(ifc, @"=IFCDOOR\(").Count);
        // Each placed part aggregates its wall: the wall's line named in an IFCRELAGGREGATES whose whole is the placed part.
        foreach (var (p, w) in new[] { (placed, wall), (second, WallOf(f, second).Id) })
        {
            var pRef = Regex.Match(ifc, $@"(#\d+)=IFC[A-Z]+\('{Regex.Escape(p)}'").Groups[1].Value;
            var wRef = Regex.Match(ifc, $@"(#\d+)=IFCWALL[A-Z]*\('{Regex.Escape(w)}'").Groups[1].Value;
            Assert.NotEmpty(pRef);
            Assert.NotEmpty(wRef);
            Assert.Matches($@"IFCRELAGGREGATES\([^;]*,{pRef},\([^)]*{wRef}\b", ifc);
        }
        // Counted once: the walls' volumes are the walls', the part's body its own plinth.
        var walls = f.Model.OfClass(IfcClass.Wall).Sum(w => f.Session.Quantities(w.Id).SelectMany(q => q.Quantities).Where(q => q.Name == "NetVolume").Sum(q => q.Value));
        var one = f.Session.Quantities(wall).SelectMany(q => q.Quantities).Where(q => q.Name == "NetVolume").Sum(q => q.Value);
        Assert.True(one > 0);
        Assert.Equal(2 * one, walls, 6);
        var plinth = f.Session.Quantities(placed).SelectMany(q => q.Quantities).Where(q => q.Name == "NetVolume").Sum(q => q.Value);
        Assert.Equal(1 * 0.3 * 0.1, plinth, 6);
    }

    [Fact]
    public void A_saved_part_of_formula_corners_opens_drawn_from_sketches_with_the_same_ids_values_and_volumes()
    {
        var f = F();
        var bracket = new JsonObject
        {
            ["params"] = J(new JsonObject { ["key"] = "W", ["default"] = 0.4 }, new JsonObject { ["key"] = "H", ["default"] = 0.3 }, new JsonObject { ["key"] = "T", ["default"] = 0.02 }),
            ["bodies"] = J(new JsonObject { ["name"] = "Angle", ["plane"] = "xz", ["points"] = J(J(0, 0), J("W", 0), J("W", "T"), J("T", "T"), J("T", "H"), J(0, "H")), ["depth"] = 0.1 }),
        };
        // Made by the families' commands, as a project saved by the build before this one holds them.
        var family = (string)f.Run("studio.createFamily", new JsonObject { ["name"] = "Bracket", ["def"] = bracket })!["id"]!;
        var a = (string)f.Run("studio.placeFamily", new JsonObject { ["familyId"] = family, ["storeyId"] = f.Storey, ["at"] = J(1, 2), ["rotation"] = 30 })!["id"]!;
        var b = (string)f.Run("studio.placeFamily", new JsonObject { ["familyId"] = family, ["storeyId"] = f.Storey, ["at"] = J(3, 2), ["values"] = new JsonObject { ["W"] = 0.6 } })!["id"]!;
        Assert.NotNull((Parts.DefOf(f.Model.Get(family))!["bodies"]![0]!)["points"]);
        double Vol(Fixture x, string id) => x.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var g = F();
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        var def = Parts.DefOf(g.Model.Get(family))!;
        var body = def["bodies"]![0]!.AsObject();
        Assert.Null(body["points"]);
        Assert.NotNull((string?)body["sketch"]);
        Assert.Single(PartSketches.SketchesOf(def));
        Assert.Equal(3, def["params"]!.AsArray().Count);
        foreach (var id in new[] { a, b }) Assert.Equal(Vol(f, id), Vol(g, id), 9);
        Assert.Equal(0.6, (double)g.Model.Get(b)!.Attrs["values"]!["W"]!);
        Assert.Equal(family, Parts.TypeOf(g.Model, g.Model.Get(a)!)!.Id);
        // The tree shows it as a sketch of its own, edited in place; no corners left to constrain.
        var tree = (JsonObject)g.Session.Query("studio.partTree", new JsonObject { ["id"] = a })!;
        Assert.All(tree["sketches"]!.AsArray(), s => Assert.True((bool?)s!["local"] == true));
        // The families' file still comes in, as a part drawn from sketches through the one import.
        var file = (string)f.Session.Query("studio.exportFamily", new JsonObject { ["id"] = family })!["text"]!;
        var h = F();
        var imported = (string)h.Run("studio.importFamily", new JsonObject { ["text"] = file, ["all"] = "both" })!["id"]!;
        Assert.NotNull((string?)Parts.DefOf(h.Model.Get(imported))!["bodies"]![0]!["sketch"]);
    }
}
