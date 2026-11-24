using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Speckle;
using Lapin.Structure;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using SMesh = Speckle.Objects.Geometry.Mesh;

namespace Lapin.Engine.Tests;

/// <summary>
/// Receiving from Speckle with recognition (docs/plan-speckle.md, S3), no network: walls and slabs as Revit's v3 connector
/// writes them (a <c>RevitObject</c> with its location line and its parameters, in millimetres) made native where the
/// native element is the received body within the fidelity gate (1 mm or 0.5 %), else kept the references they were; a
/// parameter contradicting the body keeps it a reference; newer versions updating recognised walls in place (moved: native
/// again; made irregular: a reference; removed: gone), their GlobalIds kept; and a Lapin model received as a copy coming
/// back as native walls.
/// </summary>
// One at a time: Speckle's SDK keeps one type loader, which setting it up again resets.
[Collection("Speckle SDK")]
public sealed class SpeckleRecognitionTests
{
    const string Server = "https://speckle.test";

    static (Fixture F, SpeckleModule Module) Project()
    {
        var module = new SpeckleModule();
        return (new Fixture(new ArchitectureModule(), new StructureModule(), module), module);
    }

    /// <summary>A Speckle mesh in millimetres of a prism: its outline at the bottom (<paramref name="z0"/>), and at the top (<paramref name="z1"/>) the same outline moved by (<paramref name="dx"/>, <paramref name="dy"/>) or the one given.</summary>
    static SMesh Prism(string appId, (double X, double Y)[] bottom, double z0, double z1, (double X, double Y)[]? top = null, (int, int, int)[]? caps = null)
    {
        top ??= bottom;
        var n = bottom.Length;
        var v = new List<double>();
        foreach (var (x, y) in bottom) v.AddRange([x, y, z0]);
        foreach (var (x, y) in top) v.AddRange([x, y, z1]);
        var f = new List<int>();
        if (caps == null)
        {
            // Convex: each cap one face.
            f.Add(n);
            for (var i = n - 1; i >= 0; i--) f.Add(i);
            f.Add(n);
            for (var i = 0; i < n; i++) f.Add(n + i);
        }
        else foreach (var (a, b, c) in caps) f.AddRange([3, a, c, b, 3, n + a, n + b, n + c]);
        for (var i = 0; i < n; i++) f.AddRange([4, i, (i + 1) % n, n + (i + 1) % n, n + i]);
        return new SMesh { applicationId = appId + "/mesh", units = "mm", vertices = v, faces = f };
    }

    static (double, double)[] Rect(double x, double y, double dx, double dy) => [(x, y), (x + dx, y), (x + dx, y + dy), (x, y + dy)];

    static Dictionary<string, object?> Param(string name, double value, string id) =>
        new() { ["name"] = name, ["value"] = value, ["units"] = "mm", ["internalDefinitionName"] = id };

    /// <summary>A Revit wall: its body, its location line along its middle, its width and unconnected height.</summary>
    static RevitObject Wall(string appId, double x, double y, double length = 4000, double width = 200, double height = 2800, SMesh? body = null, double? widthParam = null) => new()
    {
        applicationId = appId,
        name = "Basic Wall",
        type = "Generic - 200mm",
        family = "Basic Wall",
        category = "Walls",
        level = "Level 1",
        location = new Line { start = new Point(x, y + width / 2, 0, "mm"), end = new Point(x + length, y + width / 2, 0, "mm"), units = "mm" },
        elements = [],
        units = "mm",
        displayValue = [body ?? Prism(appId, Rect(x, y, length, width), 0, height)],
        properties = new Dictionary<string, object?>
        {
            ["category"] = "Walls",
            ["Parameters"] = new Dictionary<string, object?>
            {
                ["Type Parameters"] = new Dictionary<string, object?> { ["Construction"] = new Dictionary<string, object?> { ["Width"] = Param("Width", widthParam ?? width, "WALL_ATTR_WIDTH_PARAM") } },
                ["Instance Parameters"] = new Dictionary<string, object?> { ["Constraints"] = new Dictionary<string, object?> { ["Unconnected Height"] = Param("Unconnected Height", height, "WALL_USER_HEIGHT_PARAM") } },
            },
        },
    };

    static RevitObject Floor(string appId, double thickness = 200) => new()
    {
        applicationId = appId,
        name = "Floor",
        type = "Generic 200mm",
        family = "Floor",
        category = "Floors",
        level = "Level 1",
        location = null,
        elements = [],
        units = "mm",
        displayValue = [Prism(appId, Rect(0, 0, 6000, 4000), -thickness, 0)],
        properties = new Dictionary<string, object?>
        {
            ["Parameters"] = new Dictionary<string, object?>
            {
                ["Instance Parameters"] = new Dictionary<string, object?> { ["Dimensions"] = new Dictionary<string, object?> { ["Thickness"] = Param("Thickness", thickness, "FLOOR_ATTR_THICKNESS_PARAM") } },
            },
        },
    };

    static RootCollection Version(params Base[] objects)
    {
        SpeckleSdk.Ensure();
        var root = new RootCollection { name = "Revit model", applicationId = "revit-root", elements = [new Collection { name = "Level 1", elements = [.. objects] }] };
        root["levelProxies"] = new List<LevelProxy>
        {
            new() { value = new DataObject { name = "Level 1", displayValue = [], properties = new() { ["elevation"] = 0.0, ["units"] = "mm" } }, objects = [.. objects.Select(o => o.applicationId!)] },
        };
        return root;
    }

#pragma warning disable CS0618
    static async Task<Base> RoundTrip(Base root)
    {
        var services = new ServiceCollection();
        services.AddSpeckleSdk(new Application("Lapin", SpeckleConvert.SourceApplication), "test", typeof(SMesh).Assembly);
        var ops = services.BuildServiceProvider().GetRequiredService<IOperations>();
        return await ops.DeserializeAsync(ops.Serialize(root));
    }
#pragma warning restore CS0618

    static JsonObject Receive(Fixture f, SpeckleModule module, Base root, string version, bool recognize = true, bool asCopy = false, string source = "revit")
    {
        module.Prime(new SpeckleFetched(new SpeckleSource(Server, "p1", "m1", "Revit model", version, source), root));
        return f.Run("speckle.receive", new JsonObject { ["projectId"] = "p1", ["modelId"] = "m1", ["versionId"] = version, ["recognize"] = recognize, ["asCopy"] = asCopy })!.AsObject();
    }

    static List<Entity> Received(Fixture f) => f.Model.Entities.Where(e => e.Attrs[SpeckleReceive.Key] is JsonObject && e.Class != StoredBodies.SharedClass).ToList();
    static Entity ByApp(Fixture f, string app) => Received(f).Single(e => (string?)e.Attrs[SpeckleReceive.Key]!["applicationId"] == app);
    static bool Native(Entity e) => !StoredBodies.Has(e);

    [Fact]
    public async Task A_straight_wall_comes_in_as_a_native_wall_keeping_its_id_its_provenance_and_its_group()
    {
        var (f, module) = Project();
        var r = Receive(f, module, await RoundTrip(Version(Wall("w1", 0, 0))), "v1");
        Assert.Equal(1, (int)r["recognized"]!);
        Assert.Equal(1, (int)r["native"]!);
        Assert.Equal(0, (int)r["references"]!);
        var w = ByApp(f, "w1");
        Assert.Equal(IfcClass.Wall, w.Class);
        Assert.True(Native(w));
        Assert.Equal("native", (string?)w.Attrs[SpeckleReceive.Key]!["recognition"]);
        Assert.Equal("w1", (string?)((JsonObject)w.Attrs["properties"]!)["Speckle"]!["applicationId"]);
        // As long, thick and high as its body, on its storey, in the model's group.
        var (lo, hi) = f.Bounds(w.Id);
        Assert.Equal(4.0, hi.X - lo.X, 3);
        Assert.Equal(0.2, hi.Y - lo.Y, 3);
        Assert.Equal(2.8, hi.Z - lo.Z, 3);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, w.Id)!.Id);
        Assert.NotNull(Groups.Of(f.Model, w.Id));
        // No lintel, reveal or join of Lapin's own: the wall alone.
        Assert.DoesNotContain(f.Model.Relations, x => x.Class == IfcClass.RelConnectsPathElements);
        // The GlobalId the reference would have had (derived from the model and the applicationId).
        var (g, module2) = Project();
        Receive(g, module2, await RoundTrip(Version(Wall("w1", 0, 0))), "v1", recognize: false);
        Assert.Equal(ByApp(g, "w1").Id, w.Id);
        Assert.True(StoredBodies.Has(ByApp(g, "w1")));
        // One undo step takes it all away.
        f.Session.Undo();
        Assert.Empty(Received(f));
    }

    /// <summary>
    /// As Revit's connector sends a wall: one mesh per material of its faces (its painted sides in one, its core's ends, top and
    /// bottom in another), neither a solid alone; and its head running up into the floor over it, as Revit's meshes overlap.
    /// Read as one solid, and the recognised floor cutting no wall: both native, the wall its full height.
    /// </summary>
    [Fact]
    public async Task A_wall_in_two_open_meshes_running_up_into_the_floor_over_it_comes_in_whole_as_does_the_floor()
    {
        var (f, module) = Project();
        var v = new List<double> { 0, 0, 0, 4000, 0, 0, 4000, 200, 0, 0, 200, 0, 0, 0, 3000, 4000, 0, 3000, 4000, 200, 3000, 0, 200, 3000 };
        var sides = new SMesh { applicationId = "w1/paint", units = "mm", vertices = [.. v], faces = [4, 0, 1, 5, 4, 4, 2, 3, 7, 6] };
        var core = new SMesh { applicationId = "w1/core", units = "mm", vertices = [.. v], faces = [4, 0, 3, 2, 1, 4, 4, 5, 6, 7, 4, 1, 2, 6, 5, 4, 3, 0, 4, 7] };
        var wall = Wall("w1", 0, 0, height: 3000);
        wall.displayValue = [sides, core];
        var floor = Floor("f1", thickness: 470);
        floor.displayValue = [Prism("f1", Rect(-1000, -1000, 6000, 4000), 2530, 3000)];
        var r = Receive(f, module, await RoundTrip(Version(wall, floor)), "v1");
        Assert.Equal(2, (int)r["recognized"]!);
        var w = ByApp(f, "w1");
        Assert.True(Native(w));
        Assert.Equal(3.0, f.Bounds(w.Id).Item2.Z, 3);
        Assert.True(Native(ByApp(f, "f1")));
    }

    [Fact]
    public async Task A_floor_comes_in_as_a_native_slab()
    {
        var (f, module) = Project();
        var r = Receive(f, module, await RoundTrip(Version(Floor("f1"))), "v1");
        Assert.Equal(1, (int)r["recognized"]!);
        var s = ByApp(f, "f1");
        Assert.Equal(IfcClass.Slab, s.Class);
        Assert.True(Native(s));
        var (lo, hi) = f.Bounds(s.Id);
        Assert.Equal(6.0, hi.X - lo.X, 3);
        Assert.Equal(4.0, hi.Y - lo.Y, 3);
        Assert.Equal(0.2, hi.Z - lo.Z, 3);
    }

    [Fact]
    public async Task A_skewed_wall_fails_the_gate_and_stays_the_reference_it_was()
    {
        var (f, module) = Project();
        // Its top 300 mm along from its bottom: leaning, no upright prism; no recogniser reads it.
        var leaning = Prism("w1", Rect(0, 0, 4000, 200), 0, 2800, Rect(300, 0, 4000, 200));
        var r = Receive(f, module, await RoundTrip(Version(Wall("w1", 0, 0, body: leaning), Floor("f1"))), "v1");
        var w = ByApp(f, "w1");
        Assert.True(StoredBodies.Has(w));
        Assert.Equal("reference", (string?)w.Attrs[SpeckleReceive.Key]!["recognition"]);
        Assert.True(Native(ByApp(f, "f1")));
        Assert.Equal(1, (int)r["recognized"]!);
        Assert.Equal(1, (int)r["references"]!);
        Assert.NotEmpty(r["recognition"]!["kept"]!.AsObject());
        // In plan, a 20 mm recess 1 m long in one face: read as a wall (an upright prism), its native body is a plain wall, 20 mm off
        // the received one in plan; the gate keeps it the reference it was.
        var (g, module2) = Project();
        var recess = Prism("w2", [(0, 0), (1000, 0), (1000, 20), (2000, 20), (2000, 0), (4000, 0), (4000, 200), (0, 200)], 0, 2800,
            caps: [(0, 1, 2), (0, 2, 7), (2, 3, 7), (3, 6, 7), (3, 4, 6), (4, 5, 6)]);
        var r2 = Receive(g, module2, await RoundTrip(Version(Wall("w2", 0, 0, body: recess))), "v1");
        Assert.Equal(0, (int)r2["recognized"]!);
        Assert.Equal(1, (int)r2["recognition"]!["reverted"]!);
        var differs = Assert.Single(r2["recognition"]!["kept"]!.AsObject());
        Assert.Equal($"{SpeckleRecognition.Differs}: volume, base outline", differs.Key);
        var w2 = ByApp(g, "w2");
        Assert.True(StoredBodies.Has(w2));
        Assert.Equal(IfcClass.Wall, w2.Class);
        Assert.Equal(g.Storey, Spatial.StoreyOf(g.Model, w2.Id)!.Id);
        Assert.NotNull(Groups.Of(f.Model, w.Id));
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, w.Id)!.Id);
    }

    [Fact]
    public async Task A_parameter_that_contradicts_the_body_keeps_it_a_reference()
    {
        var (f, module) = Project();
        // Revit says 300 mm; its body is 200 mm thick. The mesh wins: no wall is made from the parameter, and it stays a reference.
        var r = Receive(f, module, await RoundTrip(Version(Wall("w1", 0, 0, widthParam: 300))), "v1");
        var w = ByApp(f, "w1");
        Assert.True(StoredBodies.Has(w));
        Assert.Equal(0, (int)r["recognized"]!);
        var kept = r["recognition"]!["kept"]!.AsObject();
        Assert.Equal(1, (int)kept[$"{SpeckleRecognition.Contradicted}: its Width is not its body's thickness"]!);
        // A location line across the body contradicts it too.
        var across = Wall("w2", 0, 0);
        across.location = new Line { start = new Point(2000, -1000, 0, "mm"), end = new Point(2000, 1000, 0, "mm"), units = "mm" };
        var (g, module2) = Project();
        Receive(g, module2, await RoundTrip(Version(across)), "v1");
        Assert.True(StoredBodies.Has(ByApp(g, "w2")));
        // A curtain wall (Revit's family) as a box: panels and mullions, never a layered wall; it stays a reference.
        var curtain = Wall("w4", 0, 0);
        curtain.family = "Curtain Wall";
        var (c, module4) = Project();
        var rc = Receive(c, module4, await RoundTrip(Version(curtain)), "v1");
        Assert.True(StoredBodies.Has(ByApp(c, "w4")));
        Assert.Equal(1, (int)rc["recognition"]!["kept"]![SpeckleRecognition.Curtain]!);
        // The same wall with no parameters at all: the mesh alone, recognised.
        var bare = new DataObject { applicationId = "w3", name = "Wall", displayValue = [Prism("w3", Rect(0, 0, 4000, 200), 0, 2800)], properties = new() { ["category"] = "Walls" } };
        var (h, module3) = Project();
        Receive(h, module3, await RoundTrip(Version(bare)), "v1");
        Assert.True(Native(ByApp(h, "w3")));
    }

    [Fact]
    public async Task Updates_re_recognise_a_moved_wall_turn_an_irregular_one_back_into_a_reference_and_remove_a_gone_one_keeping_GlobalIds()
    {
        var (f, module) = Project();
        Receive(f, module, await RoundTrip(Version(Wall("a", 0, 0), Wall("b", 0, 3000))), "v1");
        var a = ByApp(f, "a").Id;
        var b = ByApp(f, "b").Id;
        Assert.True(Native(f.Model.Get(a)!) && Native(f.Model.Get(b)!));
        var group = Groups.Of(f.Model, a)!.Id;
        var bAttrs = f.Model.Get(b)!.Attrs.ToJsonString();

        // v2: a moved 1 m along: native again, the same GlobalId, where it now is; b left as it was.
        var r2 = Receive(f, module, await RoundTrip(Version(Wall("a", 1000, 0), Wall("b", 0, 3000))), "v2");
        Assert.Equal(1, (int)r2["updated"]!);
        Assert.Equal(1, (int)r2["unchanged"]!);
        Assert.Equal(1, (int)r2["recognized"]!);
        var moved = f.Model.Get(a)!;
        Assert.True(Native(moved));
        Assert.Equal(IfcClass.Wall, moved.Class);
        Assert.Equal(1.0, f.Bounds(a).Item1.X, 3);
        Assert.Equal("v2", (string?)moved.Attrs[SpeckleReceive.Key]!["versionId"]);
        Assert.Equal(group, Groups.Of(f.Model, a)!.Id);
        Assert.Equal(bAttrs, f.Model.Get(b)!.Attrs.ToJsonString());

        // v3: a made irregular (a 20 mm recess in one face): read as a wall, the gate turns it back into a reference, the same
        // GlobalId, still in the group.
        var recess = Prism("a", [(1000, 0), (2000, 0), (2000, 20), (3000, 20), (3000, 0), (5000, 0), (5000, 200), (1000, 200)], 0, 2800,
            caps: [(0, 1, 2), (0, 2, 7), (2, 3, 7), (3, 6, 7), (3, 4, 6), (4, 5, 6)]);
        var r3 = Receive(f, module, await RoundTrip(Version(Wall("a", 1000, 0, body: recess), Wall("b", 0, 3000))), "v3");
        Assert.Equal(0, (int)r3["recognized"]!);
        Assert.Equal(1, (int)r3["recognition"]!["reverted"]!);
        var irregular = f.Model.Get(a)!;
        Assert.True(StoredBodies.Has(irregular));
        Assert.Equal(group, Groups.Of(f.Model, a)!.Id);
        Assert.Equal(1, (int)r3["native"]!);
        Assert.Equal(1, (int)r3["references"]!);

        // v4: a gone from the version: removed; b still native.
        var r4 = Receive(f, module, await RoundTrip(Version(Wall("b", 0, 3000))), "v4");
        Assert.Equal(1, (int)r4["removed"]!);
        Assert.Null(f.Model.Get(a));
        Assert.True(Native(f.Model.Get(b)!));
        // And back: a comes in again under the GlobalId it had.
        Receive(f, module, await RoundTrip(Version(Wall("a", 0, 0), Wall("b", 0, 3000))), "v5");
        Assert.True(Native(f.Model.Get(a)!));
        // Each receive one undo step: undone, a is the reference v3 left.
        f.Session.Undo();
        Assert.Null(f.Model.Get(a));
        f.Session.Undo();
        Assert.True(StoredBodies.Has(f.Model.Get(a)!));
    }

    [Fact]
    public async Task A_lapin_model_received_as_a_copy_comes_back_as_native_walls()
    {
        var (f, module) = Project();
        var w1 = f.Wall(0, 0, 6, 0, height: 2.8);
        var w2 = f.Wall(0, 4, 6, 4, height: 2.8);
        var root = await RoundTrip(SpeckleConvert.Convert(f.Session.Reader, SpeckleSendScope.From([])).Root);
        var natives = new[] { w1, w2 }.Select(id => f.Model.Get(id)!.Attrs.ToJsonString()).ToList();
        var r = Receive(f, module, root, "v1", asCopy: true, source: "lapin");
        Assert.Equal(2, (int)r["recognized"]!);
        var copies = Received(f).Where(e => e.Class == IfcClass.Wall).ToList();
        Assert.Equal(2, copies.Count);
        Assert.All(copies, c => Assert.True(Native(c)));
        Assert.All(copies, c => Assert.DoesNotContain(c.Id, new[] { w1, w2 }));
        // As long and thick as the walls they copy; the originals untouched.
        foreach (var (copy, original) in copies.OrderBy(c => f.Bounds(c.Id).Item1.Y).Zip(new[] { w1, w2 }))
        {
            var (lo, hi) = f.Bounds(copy.Id);
            var (lo0, hi0) = f.Bounds(original);
            Assert.Equal(hi0.X - lo0.X, hi.X - lo.X, 3);
            Assert.Equal(hi0.Y - lo0.Y, hi.Y - lo.Y, 3);
            Assert.Equal(hi0.Z - lo0.Z, hi.Z - lo.Z, 3);
        }
        Assert.Equal(natives, new[] { w1, w2 }.Select(id => f.Model.Get(id)!.Attrs.ToJsonString()).ToList());
    }
}
