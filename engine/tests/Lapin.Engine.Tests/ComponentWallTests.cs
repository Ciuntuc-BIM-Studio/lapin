using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Components that take walls (forum #224, docs/components.md): a plain component, not an apartment type, of walls with
/// their doors and a room. Walls join among themselves inside an instance; where one meets a wall outside every instance it
/// stops there, per instance, the outer wall left as it was; an edit in one instance reaches the others under their ids.
/// </summary>
public class ComponentWallTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-component-walls").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);
    static double[] Pt(JsonNode? n) => [.. n!.AsArray().Select(v => Json.Number(v) ?? 0)];
    static string TwinOf(Fixture f, JsonNode made) => made["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
    static List<Entity> Members(Fixture f, string group) => Groups.MembersOf(f.Model, group).Select(f.Model.Get).OfType<Entity>().ToList();
    static List<Entity> WallsOf(Fixture f, string group) => Members(f, group).Where(e => e.Class == IfcClass.Wall).ToList();
    static List<Entity> DoorsOf(Fixture f, string group) => WallsOf(f, group).SelectMany(w => Openings.Of(f.Model, w.Id)).Select(o => Openings.FillOf(f.Model, o.Id)).OfType<Entity>().ToList();
    static int InnerJoins(Fixture f, string group)
    {
        var ids = Groups.MembersOf(f.Model, group).ToHashSet();
        return f.Model.Relations.Count(r => r.Class == IfcClass.RelConnectsPathElements && ids.Contains(r.Relating) && r.Related.All(ids.Contains));
    }
    static List<Relation> Stops(Fixture f, string wall) => [.. Joins.Of(f.Model, wall).Where(r => r.Attrs?[Joins.StopsOn] != null)];

    /// <summary>A 5 × 4 box of four joined walls, a door in the south one and a room, made a plain component, and a second instance 20 m along.</summary>
    static (Fixture F, string Group, string Twin, string Component) Two()
    {
        var f = new Fixture(new ArchitectureModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(5, 0, 5, 4), f.Wall(5, 4, 0, 4), f.Wall(0, 4, 0, 0) };
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 1 });
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids([.. walls, room]), ["name"] = "Bay" })!;
        var group = (string)made["id"]!;
        var twin = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(20, 0) })!);
        return (f, group, twin, (string)made["component"]!);
    }

    [Fact]
    public void A_plain_component_takes_walls_with_their_doors_and_a_room()
    {
        var (f, group, twin, component) = Two();
        Assert.Null(f.Model.Get(component)!.Str(Components.KindKey));
        Assert.Equal("Bay", f.Model.Get(component)!.Name);
        Assert.Equal(component, Components.Of(f.Model.Get(twin)!));
        Assert.Equal(4, WallsOf(f, twin).Count);
        Assert.Single(DoorsOf(f, twin));
        Assert.Contains(Members(f, twin), e => e.Class == Rooms.Class);
        // The walls join among themselves in each instance, as in the first.
        Assert.Equal(4, InnerJoins(f, group));
        Assert.Equal(InnerJoins(f, group), InnerJoins(f, twin));
        // The door has a key of its own, the same in both.
        Assert.Equal(DoorsOf(f, group).Single().Str(Components.MemberKey), DoorsOf(f, twin).Single().Str(Components.MemberKey));
        Assert.NotNull(DoorsOf(f, twin).Single().Str(Components.MemberKey));
    }

    [Fact]
    public void A_wall_edited_in_one_instance_follows_in_the_other_under_its_ids_in_one_undo()
    {
        var (f, group, twin, _) = Two();
        var wallIds = WallsOf(f, twin).Select(w => w.Id).ToHashSet();
        var doorId = DoorsOf(f, twin).Single().Id;
        var edited = WallsOf(f, group).First(w => Openings.Of(f.Model, w.Id).Any());
        f.Run("element.update", new JsonObject { ["id"] = edited.Id, ["attrs"] = new JsonObject { ["height"] = 3.3 } });
        Assert.Equal(wallIds, WallsOf(f, twin).Select(w => w.Id).ToHashSet());
        Assert.Equal(doorId, DoorsOf(f, twin).Single().Id);
        Assert.Equal(1, WallsOf(f, twin).Count(w => Math.Abs(w.Num("height") - 3.3) < 1e-9));
        Assert.Equal(InnerJoins(f, group), InnerJoins(f, twin));
        f.Session.Undo();
        Assert.Equal(0, WallsOf(f, twin).Count(w => Math.Abs(w.Num("height") - 3.3) < 1e-9));
        Assert.Equal(wallIds, WallsOf(f, twin).Select(w => w.Id).ToHashSet());
    }

    [Fact]
    public void A_wall_moved_in_the_other_instance_reaches_the_first_and_its_joins_follow()
    {
        var (f, group, twin, _) = Two();
        var east = WallsOf(f, twin).First(w => Math.Abs(Pt(w.Attrs["start"])[0] - 25) < 1e-6 && Math.Abs(Pt(w.Attrs["end"])[0] - 25) < 1e-6);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(east.Id), ["op"] = "move", ["by"] = P(1, 0) });
        var xs = WallsOf(f, group).SelectMany(w => new[] { Pt(w.Attrs["start"])[0], Pt(w.Attrs["end"])[0] }).ToList();
        Assert.Equal(6, xs.Max(), 6);
        // The corners still meet: the south and north walls were drawn out to the moved wall.
        Assert.Equal(4, InnerJoins(f, group));
        Assert.Equal(4, InnerJoins(f, twin));
    }

    [Fact]
    public void A_turned_and_a_mirrored_instance_follow_an_edit()
    {
        var (f, group, twin, _) = Two();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "rotate", ["center"] = P(20, 0), ["angle"] = 90 });
        var mirrored = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "mirror", ["a"] = P(-1, 0), ["b"] = P(-1, 1), ["copy"] = true })!);
        Assert.NotEqual(DoorsOf(f, group).Single().Bool("flipSide"), DoorsOf(f, mirrored).Single().Bool("flipSide"));
        var east = WallsOf(f, group).First(w => Math.Abs(Pt(w.Attrs["start"])[0] - 5) < 1e-6 && Math.Abs(Pt(w.Attrs["end"])[0] - 5) < 1e-6);
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(east.Id), ["op"] = "move", ["by"] = P(1, 0) });
        // Turned 90° about (20, 0): x along the first is y along it.
        var ys = WallsOf(f, twin).SelectMany(w => new[] { Pt(w.Attrs["start"])[1], Pt(w.Attrs["end"])[1] }).ToList();
        Assert.Equal(0, ys.Min(), 6);
        Assert.Equal(6, ys.Max(), 6);
        // Mirrored about x = -1: further west.
        var xs = WallsOf(f, mirrored).SelectMany(w => new[] { Pt(w.Attrs["start"])[0], Pt(w.Attrs["end"])[0] }).ToList();
        Assert.Equal(-8, xs.Min(), 6);
        Assert.Equal(-2, xs.Max(), 6);
        Assert.Single(DoorsOf(f, mirrored));
    }

    [Fact]
    public void A_wall_meeting_one_outside_the_instance_stops_in_it_per_instance_and_leaves_it_alone()
    {
        var f = new Fixture(new ArchitectureModule());
        var facade = f.Wall(0, 0, 30, 0);
        // A bay of two walls: a fin from the facade out to y = 3, and a return along y = 3, a door in the fin.
        var fin = f.Wall(4, 0, 4, 3);
        var back = f.Wall(4, 3, 7, 3);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = fin, ["along"] = 1.5 });
        var before = f.Model.Get(facade)!.Attrs.ToJsonString();
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(fin, back), ["name"] = "Fin" })!;
        var group = (string)made["id"]!;
        Assert.Contains(Stops(f, fin), r => r.Relating == facade);
        var twin = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(10, 0) })!);
        var twinFin = WallsOf(f, twin).Single(w => Math.Abs(Pt(w.Attrs["start"])[0] - Pt(w.Attrs["end"])[0]) < 1e-6);
        // The copy's fin stops in the facade where it lands; the facade is as it was, joined to neither as a corner.
        Assert.Contains(Stops(f, twinFin.Id), r => r.Relating == facade);
        Assert.Equal(before, f.Model.Get(facade)!.Attrs.ToJsonString());
        Assert.Equal(1, InnerJoins(f, twin));
        // An edit of the type keeps each instance's stop.
        f.Run("element.update", new JsonObject { ["id"] = back, ["attrs"] = new JsonObject { ["height"] = 2.4 } });
        Assert.Contains(Stops(f, twinFin.Id), r => r.Relating == facade);
        // Moved away from the facade, the copy lets go of it and does not drag it.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "move", ["by"] = P(0, 5) });
        Assert.Empty(Stops(f, twinFin.Id));
        Assert.Equal(before, f.Model.Get(facade)!.Attrs.ToJsonString());
    }

    [Fact]
    public void A_door_without_its_wall_is_refused()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 4, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1 })!["id"]!;
        var ex = Assert.Throws<CommandException>(() => f.Run("component.make", new JsonObject { ["ids"] = Ids(door) }));
        Assert.Contains("wall that is not selected", ex.Message);
        Assert.Empty(f.Model.OfClass(Components.Class));
    }

    [Fact]
    public void Saved_and_opened_again_the_instances_still_follow()
    {
        var (f, group, twin, _) = Two();
        var path = Path.Combine(_dir, "bays.lapin");
        PackageFiles.Save(f.Session, path);
        var g = new Fixture(new ArchitectureModule());
        PackageFiles.Open(g.Session, path);
        Assert.Equal(4, WallsOf(g, twin).Count);
        var edited = WallsOf(g, group).First(w => !Openings.Of(g.Model, w.Id).Any());
        g.Run("element.update", new JsonObject { ["id"] = edited.Id, ["attrs"] = new JsonObject { ["height"] = 2.9 } });
        Assert.Equal(1, WallsOf(g, twin).Count(w => Math.Abs(w.Num("height") - 2.9) < 1e-9));
    }

    static List<string> Lines(string text, string step) => [.. text.Split('\n').Where(l => Regex.IsMatch(l, $@"=\s*{step}\("))];

    [Fact]
    public void In_IFC_the_instances_are_groups_of_ordinary_walls_and_come_back_instances()
    {
        var (f, group, _, component) = Two();
        var path = Path.Combine(_dir, "bays.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Equal(8, Lines(text, "IFCWALL(?:STANDARDCASE)?").Count);
        Assert.Equal(2, Lines(text, "IFCDOOR").Count);
        Assert.Equal(2, Lines(text, "IFCGROUP").Count);
        Assert.Equal(2, Regex.Matches(text, @"IFCPROPERTYSET\([^\n]*'Lapin_Component'").Count);
        Assert.DoesNotContain("'Lapin_Unit'", text);
        Assert.Equal(2, Regex.Matches(text, $@"IFCPROPERTYSINGLEVALUE\('TypeId',[^\n]*'{Regex.Escape(component)}'").Count);

        var g = new Fixture(new ArchitectureModule());
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, path, null, out _);
        foreach (var cls in new[] { IfcClass.Wall, IfcClass.Door, Rooms.Class })
            Assert.Equal(f.Model.OfClass(cls).Select(e => e.Id).OrderBy(x => x), g.Model.OfClass(cls).Select(e => e.Id).OrderBy(x => x));
        var back = Assert.Single(g.Model.OfClass(Components.Class));
        Assert.Equal("Bay", back.Name);
        Assert.Null(back.Str(Components.KindKey));
        var instances = Components.Instances(g.Model, back.Id).ToList();
        Assert.Equal(2, instances.Count);
        var edited = WallsOf(g, group).First(w => !Openings.Of(g.Model, w.Id).Any());
        g.Run("element.update", new JsonObject { ["id"] = edited.Id, ["attrs"] = new JsonObject { ["height"] = 3.1 } });
        Assert.All(instances, i => Assert.Equal(1, WallsOf(g, i.Id).Count(w => Math.Abs(w.Num("height") - 3.1) < 1e-9)));
    }
}
