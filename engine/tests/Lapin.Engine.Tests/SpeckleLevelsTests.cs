using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Speckle;
using Lapin.Structure;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Other;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using SMesh = Speckle.Objects.Geometry.Mesh;

namespace Lapin.Engine.Tests;

/// <summary>
/// The version's levels as the project's storeys (docs/plan-speckle.md), no network: 18 Revit levels into a project with two
/// storeys, the two matched by elevation and 16 made, every object on its level's storey, in one undo step; a later version
/// renaming and moving a level (its storey renamed and moved, unless something else stands on it), and dropping levels (the
/// storey removed when empty, kept otherwise); a version in feet; levels from another datum than the geometry; and the old
/// way, only the project's storeys.
/// </summary>
// One at a time: Speckle's SDK keeps one type loader, which setting it up again resets.
[Collection("Speckle SDK")]
public sealed class SpeckleLevelsTests
{
    const string Server = "https://speckle.test";

    /// <summary>A project with a storey at ±0.00 (the fixture's ground floor) and one at +4.00.</summary>
    static (Fixture F, SpeckleModule Module, string Ground, string First) Project()
    {
        var module = new SpeckleModule();
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), module);
        var ground = f.Storey;
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 4.0 })!["id"]!;
        return (f, module, ground, first);
    }

    static SMesh Box(string appId, double x, double z, double dx, double dz, string units) => new()
    {
        applicationId = appId + "/mesh",
        units = units,
        vertices = [x, 0, z, x + dx, 0, z, x + dx, 1, z, x, 1, z, x, 0, z + dz, x + dx, 0, z + dz, x + dx, 1, z + dz, x, 1, z + dz],
        faces = [4, 0, 3, 2, 1, 4, 4, 5, 6, 7, 4, 0, 1, 5, 4, 4, 1, 2, 6, 5, 4, 2, 3, 7, 6, 4, 3, 0, 4, 7],
    };

    /// <summary>A level as Revit's connector writes its proxy: its applicationId (Revit's), its name, and its elevation and unit on the level itself.</summary>
    static LevelProxy Level(string appId, string name, double elevation, string units, IEnumerable<string> objects)
    {
        var value = new DataObject { name = name, displayValue = [], properties = [] };
        value["elevation"] = elevation;
        value["units"] = units;
        return new LevelProxy { applicationId = appId, value = value, objects = [.. objects] };
    }

    /// <summary>
    /// A version of <paramref name="levels"/> (name, elevation in <paramref name="units"/>), one object on each, standing
    /// <paramref name="datum"/> above its level; <paramref name="skip"/>: the levels left out with their objects.
    /// </summary>
    static RootCollection Version(IReadOnlyList<(string Id, string Name, double Elevation)> levels, string units = "mm", double datum = 0, ISet<string>? skip = null)
    {
        SpeckleSdk.Ensure();
        var keep = levels.Where(l => skip?.Contains(l.Id) != true).ToList();
        var objects = keep.Select(l => (Base)new DataObject
        {
            applicationId = "obj-" + l.Id,
            name = "Column " + l.Name,
            displayValue = [Box("obj-" + l.Id, 0, l.Elevation + datum, 1, 2, units)],
            properties = new() { ["category"] = "Furniture" },
        }).ToList();
        var root = new RootCollection { name = "Tower", applicationId = "tower-root", elements = [new Collection { name = "Objects", elements = objects }] };
        root["levelProxies"] = keep.Select(l => Level(l.Id, l.Name, l.Elevation, units, ["obj-" + l.Id])).ToList();
        return root;
    }

    static List<(string Id, string Name, double Elevation)> Levels(int n) => [.. Enumerable.Range(0, n).Select(i => ($"lvl-{i}", $"Level {i}", i * 4000.0))];

#pragma warning disable CS0618
    static async Task<Base> RoundTrip(Base root)
    {
        var ops = SpeckleSdk.Services.GetRequiredService<IOperations>();
        return await ops.DeserializeAsync(ops.Serialize(root));
    }
#pragma warning restore CS0618

    static JsonObject Receive(Fixture f, SpeckleModule module, Base root, string version, JsonObject? extra = null)
    {
        module.Prime(new SpeckleFetched(new SpeckleSource(Server, "p1", "m1", "Tower", version, "revit"), root));
        var a = new JsonObject { ["projectId"] = "p1", ["modelId"] = "m1", ["versionId"] = version };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return f.Run("speckle.receive", a)!.AsObject();
    }

    static Entity Obj(Fixture f, string level) => f.Model.Entities.Single(e => (string?)(e.Attrs[SpeckleReceive.Key] as JsonObject)?["applicationId"] == "obj-" + level);
    static Entity StoreyOf(Fixture f, string level) => Spatial.StoreyOf(f.Model, Obj(f, level).Id)!;
    static List<Entity> Storeys(Fixture f) => [.. Spatial.Storeys(f.Model)];

    [Fact]
    public async Task Eighteen_levels_into_a_two_storey_project_match_the_two_and_make_sixteen_with_every_object_on_its_own()
    {
        var (f, module, ground, first) = Project();
        var levels = Levels(18);
        var r = Receive(f, module, await RoundTrip(Version(levels)), "v1");
        Assert.Equal(2, (int)r["levels"]!["matched"]!);
        Assert.Equal(16, (int)r["levels"]!["made"]!);
        Assert.Equal(18, Storeys(f).Count);
        // The two the project had, by elevation (their names kept); the rest made with the levels' names and elevations.
        Assert.Equal(ground, StoreyOf(f, "lvl-0").Id);
        Assert.Equal(first, StoreyOf(f, "lvl-1").Id);
        Assert.Equal("First floor", f.Model.Get(first)!.Name);
        for (var i = 2; i < 18; i++)
        {
            var s = StoreyOf(f, $"lvl-{i}");
            Assert.Equal($"Level {i}", s.Name);
            Assert.Equal(i * 4.0, s.Num("elevation"), 6);
            Assert.Equal(i < 17 ? 4.0 : 3.0, s.Num("height"), 6);
        }
        Assert.Equal(18, ((JsonObject)r["storeys"]!).Count);
        // Received again, nothing new.
        var again = Receive(f, module, await RoundTrip(Version(levels)), "v1b");
        Assert.Equal(0, (int)again["levels"]!["made"]!);
        Assert.Equal(16, (int)again["levels"]!["matched"]! - 2);
        Assert.Equal(18, Storeys(f).Count);
        // One undo step: the objects and the storeys made go together.
        var (g, module2, _, _) = Project();
        Receive(g, module2, await RoundTrip(Version(levels)), "v1");
        g.Session.Undo();
        Assert.Equal(2, Storeys(g).Count);
        Assert.DoesNotContain(g.Model.Entities, e => e.Attrs[SpeckleReceive.Key] is JsonObject);
    }

    [Fact]
    public async Task A_later_version_renames_and_moves_the_storeys_it_made_unless_something_else_stands_on_them()
    {
        var (f, module, _, _) = Project();
        var levels = Levels(8);
        Receive(f, module, await RoundTrip(Version(levels)), "v1");
        var five = StoreyOf(f, "lvl-5").Id;
        var six = StoreyOf(f, "lvl-6").Id;
        var seven = StoreyOf(f, "lvl-7").Id;
        // A wall of the project's own on Level 7's storey.
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = seven, ["x1"] = 10, ["y1"] = 0, ["x2"] = 14, ["y2"] = 0 });
        var next = levels.Select(l => l.Id switch
        {
            "lvl-5" => (l.Id, "Roof terrace", l.Elevation),
            "lvl-6" => (l.Id, l.Name, l.Elevation + 500),
            "lvl-7" => (l.Id, "Plant room", l.Elevation + 500),
            _ => l,
        }).ToList();
        var r = Receive(f, module, await RoundTrip(Version(next)), "v2");
        Assert.Equal(1, (int)r["levels"]!["renamed"]!);
        Assert.Equal(1, (int)r["levels"]!["moved"]!);
        // Renamed in place: the same storey, the same objects on it.
        Assert.Equal("Roof terrace", f.Model.Get(five)!.Name);
        Assert.Equal(five, StoreyOf(f, "lvl-5").Id);
        // Moved with its level, and the storey under it as high as the gap.
        Assert.Equal(24.5, f.Model.Get(six)!.Num("elevation"), 6);
        Assert.Equal(4.5, StoreyOf(f, "lvl-5").Num("height"), 6);
        Assert.Equal(six, StoreyOf(f, "lvl-6").Id);
        // The project's wall stands on Level 7's storey: neither renamed nor moved, and said so.
        Assert.Equal("Level 7", f.Model.Get(seven)!.Name);
        Assert.Equal(28.0, f.Model.Get(seven)!.Num("elevation"), 6);
        var notes = r["notes"]!.AsArray().Select(n => (string)n!).ToList();
        Assert.Contains(notes, n => n.StartsWith("Not renamed") && n.Contains("Level 7"));
        Assert.Contains(notes, n => n.StartsWith("Not moved") && n.Contains("Level 7"));
    }

    [Fact]
    public async Task A_level_gone_from_the_version_takes_its_storey_unless_something_else_stands_on_it()
    {
        var (f, module, _, _) = Project();
        var levels = Levels(6);
        Receive(f, module, await RoundTrip(Version(levels)), "v1");
        var four = StoreyOf(f, "lvl-4").Id;
        var five = StoreyOf(f, "lvl-5").Id;
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = four, ["x1"] = 10, ["y1"] = 0, ["x2"] = 14, ["y2"] = 0 })!["id"]!;
        var r = Receive(f, module, await RoundTrip(Version(levels, skip: new HashSet<string> { "lvl-4", "lvl-5" })), "v2");
        Assert.Equal(2, (int)r["removed"]!);
        Assert.Equal(1, (int)r["levels"]!["removed"]!);
        Assert.Equal(1, (int)r["levels"]!["kept"]!);
        Assert.Null(f.Model.Get(five));
        Assert.NotNull(f.Model.Get(four));
        Assert.Equal(four, Spatial.StoreyOf(f.Model, wall)!.Id);
        Assert.Contains(r["notes"]!.AsArray().Select(n => (string)n!), n => n.StartsWith("Kept") && n.Contains("Level 4"));
        // The storey under the gone one is as high as up to the next there is (none: 3 m).
        Assert.Equal(4.0, StoreyOf(f, "lvl-3").Num("height"), 6);
        // A storey the project had is never removed, whatever the version holds.
        Receive(f, module, await RoundTrip(Version(levels, skip: new HashSet<string> { "lvl-0", "lvl-1" })), "v3");
        Assert.Equal(2, Storeys(f).Count(s => s.Attrs[SpeckleLevels.Key] == null));
    }

    [Fact]
    public async Task A_recognised_wall_stands_on_the_storey_made_for_its_level()
    {
        var (f, module, _, _) = Project();
        var root = Version(Levels(4));
        var wall = new DataObject
        {
            applicationId = "wall-3", name = "Wall", properties = new() { ["category"] = "Walls" },
            displayValue = [new SMesh
            {
                applicationId = "wall-3/mesh", units = "mm",
                vertices = [0, 5000, 12000, 4000, 5000, 12000, 4000, 5200, 12000, 0, 5200, 12000, 0, 5000, 15000, 4000, 5000, 15000, 4000, 5200, 15000, 0, 5200, 15000],
                faces = [4, 0, 3, 2, 1, 4, 4, 5, 6, 7, 4, 0, 1, 5, 4, 4, 1, 2, 6, 5, 4, 2, 3, 7, 6, 4, 3, 0, 4, 7],
            }],
        };
        ((Collection)root.elements[0]).elements.Add(wall);
        ((List<LevelProxy>)root["levelProxies"]!).Single(p => p.applicationId == "lvl-3").objects.Add("wall-3");
        var r = Receive(f, module, await RoundTrip(root), "v1", new JsonObject { ["recognize"] = true });
        Assert.Equal(1, (int)r["recognized"]!);
        var w = f.Model.Entities.Single(e => (string?)(e.Attrs[SpeckleReceive.Key] as JsonObject)?["applicationId"] == "wall-3");
        Assert.Equal(IfcClass.Wall, w.Class);
        Assert.Equal(StoreyOf(f, "lvl-3").Id, Spatial.StoreyOf(f.Model, w.Id)!.Id);
        Assert.Equal(12.0, f.Bounds(w.Id).Item1.Z, 4);
        Assert.Equal(15.0, f.Bounds(w.Id).Item2.Z, 4);
    }

    [Fact]
    public async Task A_version_in_feet_makes_its_storeys_in_metres()
    {
        var (f, module, ground, _) = Project();
        var levels = new List<(string, string, double)> { ("a", "L1", 0), ("b", "L2", 10), ("c", "L3", 20.5) };
        var r = Receive(f, module, await RoundTrip(Version(levels, units: "ft")), "v1");
        Assert.Equal(1, (int)r["levels"]!["matched"]!);
        Assert.Equal(ground, StoreyOf(f, "a").Id);
        Assert.Equal(3.048, StoreyOf(f, "b").Num("elevation"), 6);
        Assert.Equal(6.2484, StoreyOf(f, "c").Num("elevation"), 6);
        Assert.Equal("L3", StoreyOf(f, "c").Name);
        // The objects where the version has them: 10 ft up.
        Assert.Equal(3.048, f.Bounds(Obj(f, "b").Id).Item1.Z, 6);
    }

    [Fact]
    public async Task Levels_given_from_another_datum_than_the_geometry_are_placed_by_the_objects()
    {
        var (f, module, _, _) = Project();
        // Every object 10 m over its level's elevation: the levels are from another point than the geometry.
        var r = Receive(f, module, await RoundTrip(Version(Levels(4), datum: 10000)), "v1");
        Assert.Equal(10.0, (double)r["levels"]!["datum"]!, 6);
        for (var i = 0; i < 4; i++)
        {
            var s = StoreyOf(f, $"lvl-{i}");
            Assert.Equal(10 + i * 4.0, s.Num("elevation"), 6);
            Assert.Equal(s.Num("elevation"), f.Bounds(Obj(f, $"lvl-{i}").Id).Item1.Z, 6);
        }
        Assert.Contains(r["notes"]!.AsArray().Select(n => (string)n!), n => n.Contains("10000 mm off"));
    }

    [Fact]
    public async Task Only_the_projects_storeys_when_asked_and_the_project_base_points_elevation_taken_as_the_geometrys()
    {
        var (f, module, ground, first) = Project();
        var r = Receive(f, module, await RoundTrip(Version(Levels(4))), "v1", new JsonObject { ["levels"] = "existing" });
        Assert.Equal(0, (int)r["levels"]!["made"]!);
        Assert.Equal(2, Storeys(f).Count);
        Assert.Equal(first, StoreyOf(f, "lvl-3").Id);
        Assert.Contains(r["notes"]!.AsArray().Select(n => (string)n!), n => n.Contains("Level 3"));

        // The base point 2 m up: the version from it, its levels too; from the internal origin, as they are.
        var (g, module2, _, _) = Project();
        g.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["elevation"] = 2.0 } });
        Receive(g, module2, await RoundTrip(Version(Levels(3))), "v1");
        Assert.Equal(10.0, StoreyOf(g, "lvl-2").Num("elevation"), 6);
        var (h, module3, _, _) = Project();
        h.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["elevation"] = 2.0 } });
        Receive(h, module3, await RoundTrip(Version(Levels(3))), "v1", new JsonObject { ["origin"] = "internal" });
        Assert.Equal(8.0, StoreyOf(h, "lvl-2").Num("elevation"), 6);
    }
}
