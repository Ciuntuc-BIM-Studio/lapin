using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Speckle;
using Microsoft.Extensions.DependencyInjection;
using Speckle.DoubleNumerics;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using SMesh = Speckle.Objects.Geometry.Mesh;

namespace Lapin.Engine.Tests;

/// <summary>
/// Receiving from Speckle (docs/plan-speckle.md, S2), with no network: versions built in code (as Revit's or Rhino's connectors
/// write them, in millimetres, with levels, render materials, a block's instances, a line and a point) received as references
/// through the module's command (one undo step): storeys by level, units, properties, instances sharing a body; a newer version
/// updating them by applicationId with their GlobalIds kept, adding and removing; a version Lapin sent from this project
/// refused unless received as a copy. And, when a server is given (LAPIN_SPECKLE_TEST_URL, LAPIN_SPECKLE_TEST_TOKEN), a model
/// sent to it and received back.
/// </summary>
public sealed class SpeckleReceiveTests
{
    const string Server = "https://speckle.test";

    static (Fixture F, SpeckleModule Module, string Ground, string First) Project()
    {
        var module = new SpeckleModule();
        var f = new Fixture(new ArchitectureModule(), module);
        var ground = f.Storey;
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0 })!["id"]!;
        return (f, module, ground, first);
    }

    /// <summary>A box in millimetres from (x, y, z), the given size, as a Speckle mesh of six quads.</summary>
    static SMesh Box(string appId, double x, double y, double z, double dx, double dy, double dz) => new()
    {
        applicationId = appId,
        units = "mm",
        vertices = [x, y, z, x + dx, y, z, x + dx, y + dy, z, x, y + dy, z, x, y, z + dz, x + dx, y, z + dz, x + dx, y + dy, z + dz, x, y + dy, z + dz],
        faces = [4, 0, 3, 2, 1, 4, 4, 5, 6, 7, 4, 0, 1, 5, 4, 4, 1, 2, 6, 5, 4, 2, 3, 7, 6, 4, 3, 0, 4, 7],
    };

    static DataObject Wall(string appId, double x, double z, double length = 4000) => new()
    {
        applicationId = appId,
        name = $"Basic Wall {appId}",
        displayValue = [Box(appId + "/mesh", x, 0, z, length, 200, 2800)],
        properties = new Dictionary<string, object?>
        {
            ["category"] = "Walls",
            ["Parameters"] = new Dictionary<string, object?>
            {
                ["Instance Parameters"] = new Dictionary<string, object?>
                {
                    ["Dimensions"] = new Dictionary<string, object?> { ["Length"] = new Dictionary<string, object?> { ["name"] = "Length", ["value"] = length, ["units"] = "mm" } },
                },
            },
            ["Pset_WallCommon"] = new Dictionary<string, object?> { ["IsExternal"] = true },
        },
    };

    /// <summary>
    /// A version as another program sends it: two walls on Level 1 (one see-through), one on Level 2, two instances of a chair
    /// (the block's mesh beside the collections), a line, a point; levels and materials as proxies.
    /// </summary>
    static RootCollection Other(bool second = false)
    {
        SpeckleSdk.Ensure();
        var chairMesh = Box("chair-mesh", 0, 0, 0, 500, 500, 900);
        var level1 = new Collection { name = "Level 1", elements = [Wall("w1", 0, 0), Wall("w2", 0, 0, second ? 6000 : 4000)] };
        if (!second) level1.elements.Add(Wall("w-gone", 8000, 0));
        else level1.elements.Add(Wall("w-new", 9000, 0));
        Matrix4x4 At(double x, double y) => new(1, 0, 0, x, 0, 1, 0, y, 0, 0, 1, 0, 0, 0, 0, 1);
        var furniture = new Collection
        {
            name = "Furniture",
            elements =
            [
                new InstanceProxy { applicationId = "chair-1", definitionId = "chair-def", transform = At(1000, 1000), units = "mm", maxDepth = 0 },
                new InstanceProxy { applicationId = "chair-2", definitionId = "chair-def", transform = At(2000, 1000), units = "mm", maxDepth = 0 },
            ],
        };
        var line = new Line { applicationId = "line-1", start = new Point(0, -1000, 0, "mm"), end = new Point(5000, -1000, 0, "mm"), units = "mm" };
        var point = new Point(1, 2, 3, "mm") { applicationId = "pt-1" };
        var level2 = new Collection { name = "Level 2", elements = [Wall("w3", 0, 3000)] };
        var root = new RootCollection
        {
            name = "Other model",
            applicationId = "root-of-another",
            elements = [new Collection { name = "Building", elements = [level1, level2, furniture, new Collection { name = "Annotations", elements = [line, point] }] },
                new Collection { name = "definitionGeometry", elements = [chairMesh] }],
        };
        root["instanceDefinitionProxies"] = new List<InstanceDefinitionProxy> { new() { applicationId = "chair-def", name = "Chair", objects = ["chair-mesh"], maxDepth = 0 } };
        root["renderMaterialProxies"] = new List<RenderMaterialProxy>
        {
            new() { value = new RenderMaterial { name = "Glass", diffuse = unchecked((int)0xFF88CCEE), opacity = 0.4 }, objects = ["w2/mesh"] },
            new() { value = new RenderMaterial { name = "Brick", diffuse = unchecked((int)0xFFAA5533), opacity = 1 }, objects = ["w1"] },
        };
        root["levelProxies"] = new List<LevelProxy>
        {
            new() { value = new DataObject { name = "Level 1", displayValue = [], properties = new() { ["elevation"] = 0.0, ["units"] = "mm" } }, objects = ["w1", "w2", "w-gone", "w-new"] },
            new() { value = new DataObject { name = "Level 2", displayValue = [], properties = new() { ["elevation"] = 3000.0, ["units"] = "mm" } }, objects = ["w3"] },
        };
        return root;
    }

    /// <summary>The version written by Speckle's serializer and read back, as a server would hand it (the types as the SDK reads them).</summary>
#pragma warning disable CS0618
    static async Task<Base> RoundTrip(Base root)
    {
        var services = new ServiceCollection();
        services.AddSpeckleSdk(new Application("Lapin", SpeckleConvert.SourceApplication), "test", typeof(SMesh).Assembly);
        var ops = services.BuildServiceProvider().GetRequiredService<IOperations>();
        return await ops.DeserializeAsync(ops.Serialize(root));
    }
#pragma warning restore CS0618

    static JsonObject Receive(Fixture f, SpeckleModule module, Base root, string version, JsonObject? extra = null, string model = "m1", string modelName = "Other model")
    {
        module.Prime(new SpeckleFetched(new SpeckleSource(Server, "p1", model, modelName, version, "revit"), root));
        var a = new JsonObject { ["projectId"] = "p1", ["modelId"] = model, ["versionId"] = version };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return f.Run("speckle.receive", a)!.AsObject();
    }

    static List<Entity> References(Fixture f) => f.Model.Entities.Where(e => e.Attrs[SpeckleReceive.Key] is JsonObject && e.Class != StoredBodies.SharedClass).ToList();
    static Entity ByApp(Fixture f, string app) => References(f).Single(e => (string?)e.Attrs[SpeckleReceive.Key]!["applicationId"] == app);

    [Fact]
    public async Task Another_programs_version_comes_in_as_references_on_its_levels_in_metres_with_its_properties()
    {
        var (f, module, ground, first) = Project();
        var root = await RoundTrip(Other());
        var r = Receive(f, module, root, "v1");
        Assert.True(7 == (int)r["added"]!, r.ToJsonString());   // three walls on level 1 and one on level 2, two chairs, a line
        Assert.Equal(1, (int)r["skipped"]!["Point"]!);
        var w1 = ByApp(f, "w1");
        Assert.Equal(IfcClass.Wall, w1.Class);
        Assert.Equal("Basic Wall w1", w1.Name);
        Assert.Equal(ground, Spatial.StoreyOf(f.Model, w1.Id)!.Id);
        Assert.Equal(first, Spatial.StoreyOf(f.Model, ByApp(f, "w3").Id)!.Id);
        // Millimetres to metres: the wall is 4 m long and 2.8 m high.
        var geometry = f.Session.Reader.Geometry(w1.Id)!;
        var bounds = geometry.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Equal(4.0, bounds.Max(v => v.X) - bounds.Min(v => v.X), 6);
        Assert.Equal(2.8, bounds.Max(v => v.Z) - bounds.Min(v => v.Z), 6);
        // Its render material's colour; the see-through one a material of its own.
        Assert.Equal("#aa5533", (string?)((JsonArray)w1.Attrs["body"]!["parts"]!)[0]!["color"]);
        var glassPart = (JsonObject)((JsonArray)ByApp(f, "w2").Attrs["body"]!["parts"]!)[0]!;
        var glass = f.Model.Get((string)glassPart["material"]!)!;
        Assert.Equal(0.6, glass.Num("transparency"), 3);
        // Its properties as sets, none named as IFC's standard ones; the parameter's value with its unit beside its name.
        var props = (JsonObject)w1.Attrs["properties"]!;
        Assert.All(props.Select(kv => kv.Key), k => Assert.False(k.StartsWith("Pset_", StringComparison.Ordinal)));
        Assert.Equal("w1", (string?)props["Speckle"]!["applicationId"]);
        Assert.Equal("Building / Level 1", (string?)props["Speckle"]!["Collection"]);
        Assert.Equal(4000, (double)props["Speckle · Parameters"]!["Instance Parameters › Dimensions › Length (mm)"]!);
        Assert.True((bool)props["Speckle · Pset_WallCommon"]!["IsExternal"]!);
        // Where it came from.
        var meta = (JsonObject)w1.Attrs[SpeckleReceive.Key]!;
        Assert.Equal(Server, (string?)meta["server"]);
        Assert.Equal("v1", (string?)meta["versionId"]);
        Assert.Equal("m1", (string?)meta["modelId"]);
        // The chairs share one body, each standing where its transform puts it (in metres).
        var chairs = new[] { ByApp(f, "chair-1"), ByApp(f, "chair-2") };
        Assert.Equal(StoredBodies.InstanceOf(chairs[0])!.Value.Body, StoredBodies.InstanceOf(chairs[1])!.Value.Body);
        Assert.Equal(1.0, StoredBodies.InstanceOf(chairs[0])!.Value.Xf[9], 9);
        Assert.Equal(2.0, StoredBodies.InstanceOf(chairs[1])!.Value.Xf[9], 9);
        Assert.Equal("Chair", chairs[0].Name);
        // The line drawn in plan.
        Assert.True(Plan2d.Has(ByApp(f, "line-1")));
        // One group named after the model, holding them all.
        var group = Groups.Of(f.Model, w1.Id)!;
        Assert.Equal("Other model", group.Name);
        Assert.Equal(7, Groups.MembersOf(f.Model, group.Id).Count());
        // One undo step: nothing of it is left.
        f.Session.Undo();
        Assert.Empty(References(f));
        Assert.DoesNotContain(f.Model.Entities, e => e.Class == StoredBodies.SharedClass);
    }

    [Fact]
    public async Task A_newer_version_updates_the_references_in_place_keeping_their_GlobalIds()
    {
        var (f, module, _, _) = Project();
        Receive(f, module, await RoundTrip(Other()), "v1");
        var before = References(f).ToDictionary(e => (string)e.Attrs[SpeckleReceive.Key]!["applicationId"]!, e => e.Id);
        var group = Groups.Of(f.Model, before["w1"])!.Id;
        var r = Receive(f, module, await RoundTrip(Other(second: true)), "v2");
        Assert.Equal(1, (int)r["added"]!);       // w-new
        Assert.Equal(1, (int)r["removed"]!);     // w-gone
        Assert.Equal(1, (int)r["updated"]!);     // w2, longer
        Assert.Equal(5, (int)r["unchanged"]!);   // w1, w3, the chairs, the line
        var after = References(f).ToDictionary(e => (string)e.Attrs[SpeckleReceive.Key]!["applicationId"]!, e => e.Id);
        foreach (var app in new[] { "w1", "w2", "w3", "chair-1", "chair-2", "line-1" }) Assert.Equal(before[app], after[app]);
        Assert.DoesNotContain("w-gone", after.Keys);
        Assert.Equal("v2", (string?)f.Model.Get(after["w2"])!.Attrs[SpeckleReceive.Key]!["versionId"]);
        var w2 = f.Session.Reader.Geometry(after["w2"])!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Equal(6.0, w2.Max(v => v.X) - w2.Min(v => v.X), 6);
        // The new one joins the model's group.
        Assert.Equal(group, Groups.Of(f.Model, after["w-new"])!.Id);
        // One undo step back to the first version, as it was.
        f.Session.Undo();
        var undone = References(f).ToDictionary(e => (string)e.Attrs[SpeckleReceive.Key]!["applicationId"]!, e => e.Id);
        Assert.Equal(before.OrderBy(kv => kv.Key), undone.OrderBy(kv => kv.Key));
        Assert.Equal("v1", (string?)f.Model.Get(after["w2"])!.Attrs[SpeckleReceive.Key]!["versionId"]);
    }

    [Fact]
    public async Task Received_again_with_nothing_changed_nothing_moves_and_the_ids_are_derived_the_same_way()
    {
        var (f, module, _, _) = Project();
        var root = await RoundTrip(Other());
        Receive(f, module, root, "v1");
        var ids = References(f).Select(e => e.Id).Order().ToList();
        var r = Receive(f, module, await RoundTrip(Other()), "v1b");
        Assert.Equal(0, (int)r["added"]! + (int)r["updated"]! + (int)r["removed"]!);
        Assert.Equal(ids, References(f).Select(e => e.Id).Order().ToList());
        // The same model received into another project gets the same ids (derived from the model and the applicationIds).
        var (g, module2, _, _) = Project();
        Receive(g, module2, await RoundTrip(Other()), "v1");
        Assert.Equal(ids, References(g).Select(e => e.Id).Order().ToList());
        // Another model of the same objects is another set, with ids of its own.
        Receive(g, module2, await RoundTrip(Other()), "v1", model: "m2", modelName: "Copy");
        Assert.Equal(14, References(g).Count);
    }

    [Fact]
    public async Task Everything_goes_on_the_storey_asked_for_and_the_internal_origin_leaves_the_base_point_out()
    {
        var (f, module, ground, first) = Project();
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["x"] = 10, ["y"] = 0, ["elevation"] = 0 } });
        Receive(f, module, await RoundTrip(Other()), "v1", new JsonObject { ["storeyId"] = first });
        Assert.All(References(f), e => Assert.Equal(first, Spatial.StoreyOf(f.Model, e.Id)!.Id));
        var w1 = f.Session.Reader.Geometry(ByApp(f, "w1").Id)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        var basePoint = Location.Base(f.Model).At.X;
        Assert.Equal(basePoint, w1.Min(v => v.X), 6);
        f.Session.Undo();
        Receive(f, module, await RoundTrip(Other()), "v1", new JsonObject { ["origin"] = "internal" });
        w1 = f.Session.Reader.Geometry(ByApp(f, "w1").Id)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Equal(0, w1.Min(v => v.X), 6);
        Assert.Equal(ground, Spatial.StoreyOf(f.Model, ByApp(f, "w1").Id)!.Id);
    }

    /// <summary>Two storeys and a few walls, a door and a slab, as SpeckleTests' project.</summary>
    static (Fixture F, SpeckleModule Module, List<string> Ids) LapinProject()
    {
        var module = new SpeckleModule();
        var f = new Fixture(new ArchitectureModule(), module);
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0 })!["id"]!;
        var ids = new List<string>
        {
            (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!,
            (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 5 })!["id"]!,
        };
        ids.Add((string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = ids[0], ["x"] = 1.5, ["y"] = 0 })!["id"]!);
        return (f, module, ids);
    }

    [Fact]
    public async Task A_version_sent_from_this_project_is_refused_unless_received_as_a_copy_and_never_touches_its_elements()
    {
        var (f, module, ids) = LapinProject();
        var root = await RoundTrip(SpeckleConvert.Convert(f.Session.Reader, SpeckleSendScope.From([])).Root);
        module.Prime(new SpeckleFetched(new SpeckleSource(Server, "p1", "m1", "house", "v1", "lapin"), root));
        var survey = (JsonObject)f.Session.Query("speckle.survey", new JsonObject { ["projectId"] = "p1", ["modelId"] = "m1", ["versionId"] = "v1" })!;
        Assert.True((bool)survey["fromThisProject"]!);
        Assert.Equal(ids.Count, (int)survey["native"]!);
        var natives = ids.Select(id => f.Model.Get(id)!.Attrs.ToJsonString()).ToList();
        var refused = Assert.Throws<CommandException>(() => f.Run("speckle.receive", new JsonObject { ["projectId"] = "p1", ["modelId"] = "m1", ["versionId"] = "v1" }));
        Assert.Contains("sent from this project", refused.Message);
        var r = Receive(f, module, root, "v1", new JsonObject { ["asCopy"] = true }, modelName: "house");
        Assert.Equal(ids.Count, (int)r["added"]!);
        Assert.All(References(f), e => Assert.DoesNotContain(e.Id, ids));
        Assert.Equal(natives, ids.Select(id => f.Model.Get(id)!.Attrs.ToJsonString()).ToList());
        Assert.Equal(IfcClass.Wall, ByApp(f, ids[0]).Class);
        // Received again as a copy: the copy is updated, not doubled.
        Receive(f, module, root, "v2", new JsonObject { ["asCopy"] = true }, modelName: "house");
        Assert.Equal(ids.Count, References(f).Count);

        // Into another project: its elements' GlobalIds, on the storeys at their levels.
        var (g, module2, _, _) = Project();
        var into = Receive(g, module2, root, "v1", modelName: "house");
        Assert.Equal(ids.Count, (int)into["added"]!);
        Assert.Equal(ids.Order(), References(g).Select(e => e.Id).Order());
        var upper = Spatial.Storeys(g.Model).Single(s => s.Name == "First floor").Id;
        Assert.Equal(upper, Spatial.StoreyOf(g.Model, ids[1])!.Id);
    }

    [Fact]
    public void Receiving_is_the_hosts_alone()
    {
        foreach (var role in new[] { "editor", "owner", "viewer" })
        {
            Assert.NotNull(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "speckle.receive", ["args"] = new JsonObject() }, role));
            Assert.NotNull(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "speckle.survey" }, role));
            Assert.NotNull(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "speckle.versions" }, role));
        }
        // Not signed in and nothing fetched: nothing is received.
        var (f, _, _, _) = Project();
        Assert.Throws<CommandException>(() => f.Run("speckle.receive", new JsonObject { ["projectId"] = "p1", ["modelId"] = "m1" }));
    }

    [Fact]
    public void Units_and_transforms_read_as_Speckle_writes_them()
    {
        Assert.Equal(0.001, SpeckleRead.Metres("mm"), 12);
        Assert.Equal(0.3048, SpeckleRead.Metres("ft"), 12);
        Assert.Equal(1, SpeckleRead.Metres(null));
        // Translation in the last column (Speckle's), or in the last row: both read the same.
        var a = SpeckleRead.Xf(new Matrix4x4(0, -1, 0, 1000, 1, 0, 0, 2000, 0, 0, 1, 0, 0, 0, 0, 1), 0.001);
        Assert.Equal(new double[] { 0, 1, 0, -1, 0, 0, 0, 0, 1, 1, 2, 0 }, a);
        var b = SpeckleRead.Xf(new Matrix4x4(0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, 1000, 2000, 0, 1), 0.001);
        Assert.Equal(a, b);
        var composed = SpeckleRead.Compose(a, [1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0]);
        Assert.Equal(1, composed[9], 12);
        Assert.Equal(3, composed[10], 12);
    }

    /// <summary>
    /// A real server (deploy/speckle-dev): a project sent to a new model, received into another project as references (its
    /// GlobalIds kept), then sent again changed and received again, updated in place. Only with LAPIN_SPECKLE_TEST_URL and
    /// LAPIN_SPECKLE_TEST_TOKEN (never in CI); LAPIN_SPECKLE_TEST_PROJECT names the project, else the first one.
    /// </summary>
    [Fact]
    public void Sent_to_a_server_and_received_back_into_another_project()
    {
        var url = Environment.GetEnvironmentVariable("LAPIN_SPECKLE_TEST_URL");
        var token = Environment.GetEnvironmentVariable("LAPIN_SPECKLE_TEST_TOKEN");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token)) return;
        var (f, _, ids) = LapinProject();
        f.Session.Query("speckle.setAccount", new JsonObject { ["server"] = url, ["token"] = token });
        var project = Environment.GetEnvironmentVariable("LAPIN_SPECKLE_TEST_PROJECT") is { Length: > 0 } p ? p : (string)f.Session.Query("speckle.projects", [])!.AsArray()[0]!["id"]!;
        var sent = f.Session.Query("speckle.send", new JsonObject { ["projectId"] = project, ["modelName"] = $"lapin-receive-{DateTime.UtcNow:yyyyMMdd-HHmmss}", ["message"] = "first" })!;
        var model = (string)sent["modelId"]!;

        var (g, _, _, _) = Project();
        g.Session.Query("speckle.setAccount", new JsonObject { ["server"] = url, ["token"] = token });
        var versions = g.Session.Query("speckle.versions", new JsonObject { ["projectId"] = project, ["modelId"] = model })!.AsArray();
        Assert.Equal((string?)sent["versionId"], (string?)versions[0]!["id"]);
        Assert.Equal("lapin", (string?)versions[0]!["sourceApplication"]);
        var survey = g.Session.Query("speckle.survey", new JsonObject { ["projectId"] = project, ["modelId"] = model })!;
        Assert.Equal(0, (int)survey["native"]!);
        var stages = new List<string>();
        g.Session.Progress = (stage, _, _) => { lock (stages) stages.Add(stage); };
        var first = g.Run("speckle.receive", new JsonObject { ["projectId"] = project, ["modelId"] = model })!;
        Assert.Equal(ids.Count, (int)first["added"]!);
        Assert.Equal(ids.Order(), References(g).Select(e => e.Id).Order());
        // The sending project refuses it (its own elements).
        Assert.Throws<CommandException>(() => f.Run("speckle.receive", new JsonObject { ["projectId"] = project, ["modelId"] = model }));

        f.Run("element.update", new JsonObject { ["id"] = ids[1], ["attrs"] = new JsonObject { ["height"] = 3.4 } });
        f.Session.Query("speckle.send", new JsonObject { ["projectId"] = project, ["modelId"] = model, ["message"] = "second" });
        var second = g.Run("speckle.receive", new JsonObject { ["projectId"] = project, ["modelId"] = model })!;
        Assert.Equal(0, (int)second["added"]!);
        Assert.True((int)second["updated"]! >= 1);
        Assert.Equal(ids.Order(), References(g).Select(e => e.Id).Order());
        var upper = g.Session.Reader.Geometry(ids[1])!.Parts.SelectMany(x => x.Solid.Vertices).ToList();
        Assert.Equal(3.4, upper.Max(v => v.Z) - upper.Min(v => v.Z), 3);
    }
}
