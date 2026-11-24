using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Speckle;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Other;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Speckle connector (docs/plan-speckle.md, S1): the model as Speckle's v3 objects, with no network (the tree
/// Project → Building → Storey → Category, each element a data object keyed by its GlobalId, its meshes from the GLB
/// export's scene in metres, its materials and properties), written and read back by Speckle's own serializer; and,
/// when a server is given (LAPIN_SPECKLE_TEST_URL and LAPIN_SPECKLE_TEST_TOKEN), sent to it twice and received back.
/// </summary>
// One at a time: Speckle's SDK keeps one type loader, which setting it up again resets.
[Collection("Speckle SDK")]
public sealed class SpeckleTests
{
    /// <summary>Two storeys: a layered wall with a door and a window, a plain wall and a slab on the ground floor, a wall upstairs; glass-like insulation.</summary>
    static (Fixture F, Dictionary<string, string> Ids) Project()
    {
        var f = new Fixture(new ArchitectureModule(), new SpeckleModule());
        var ground = f.Storey;
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor" })!["id"]!;
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var ids = new Dictionary<string, string>
        {
            ["layered"] = f.Wall(0, 0, 6, 0, type),
            ["plain"] = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = 0, ["y1"] = 5, ["x2"] = 6, ["y2"] = 5 })!["id"]!,
            ["upper"] = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 5 })!["id"]!,
        };
        ids["door"] = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = ids["layered"], ["x"] = 1.5, ["y"] = 0 })!["id"]!;
        ids["window"] = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = ids["layered"], ["x"] = 4.5, ["y"] = 0 })!["id"]!;
        ids["slab"] = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = ground,
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 5), new JsonArray(0, 5)),
        })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = Materials.Find(f.Model, "Insulation")!.Id, ["transparency"] = 0.5 });
        return (f, ids);
    }

    static SpeckleConversion Convert(Fixture f, JsonObject? scope = null) => SpeckleConvert.Convert(f.Session.Reader, SpeckleSendScope.From(scope ?? []));

    static IEnumerable<Base> Walk(Base b)
    {
        yield return b;
        IEnumerable<Base> children = b switch
        {
            Collection c => c.elements,
            DataObject d => d["@elements"] is IEnumerable<object> nested ? nested.OfType<Base>() : [],
            _ => [],
        };
        foreach (var c in children)
            foreach (var x in Walk(c)) yield return x;
    }

    static List<DataObject> Objects(Base root) => Walk(root).OfType<DataObject>().ToList();

    static List<T> Proxies<T>(Base root, string name) => ((System.Collections.IEnumerable?)root[name])?.OfType<T>().ToList() ?? [];

    [Fact]
    public void The_tree_is_project_building_storey_category_with_every_element_keyed_by_its_GlobalId()
    {
        var (f, ids) = Project();
        var c = Convert(f);
        var root = c.Root;
        Assert.Equal(Spatial.Project(f.Model)!.Id, root.applicationId);
        var building = Assert.IsType<Collection>(root.elements[0]);
        Assert.Equal(Spatial.Building(f.Model)!.Id, building.applicationId);
        // The storeys in the building, by their names and GlobalIds; on each, a collection per IFC class.
        var storeys = building.elements.Cast<Collection>().ToList();
        Assert.Equal(Spatial.Storeys(f.Model).Select(s => s.Id), storeys.Select(s => s.applicationId));
        var ground = storeys[0];
        Assert.Equal(["IfcDoor", "IfcSlab", "IfcWall", "IfcWindow"], ground.elements.Cast<Collection>().Select(x => x.name));
        Assert.Equal(["IfcWall"], storeys[1].elements.Cast<Collection>().Select(x => x.name));
        var walls = ground.elements.Cast<Collection>().Single(x => x.name == "IfcWall").elements.Cast<DataObject>().Select(o => o.applicationId).ToHashSet();
        Assert.Equal(new HashSet<string?> { ids["layered"], ids["plain"] }, walls);

        // Each element once, its applicationId its GlobalId: every element of the model with a body, nothing else.
        var objects = Objects(root);
        Assert.Equal(ids.Values.Order(), objects.Select(o => o.applicationId!).Order());
        Assert.Equal(6, c.Elements);
        foreach (var o in objects)
        {
            Assert.True(GlobalId.IsValid(o.applicationId!));
            Assert.Equal(f.Model.Get(o.applicationId!)!.Name is { Length: > 0 } n ? n : o.properties["IFC class"], o.name);
            Assert.NotEmpty(o.displayValue);
        }
    }

    [Fact]
    public void The_meshes_are_the_glb_scenes_in_metres_and_each_material_colours_its_meshes()
    {
        var (f, ids) = Project();
        var c = Convert(f);
        var scene = MeshScene.Build(f.Session, new MeshExportOptions());
        var meshes = Objects(c.Root).SelectMany(o => o.displayValue).Cast<global::Speckle.Objects.Geometry.Mesh>().ToList();
        Assert.Equal(c.Meshes, meshes.Count);
        Assert.All(meshes, m => Assert.Equal("m", m.units));
        // The same triangles as the GLB export's scene, a face of three corners each.
        Assert.Equal(scene.TriangleCount, meshes.Sum(m => m.faces.Count / 4));
        Assert.All(meshes, m => Assert.Equal(m.faces.Count / 4, m.faces.Where((_, i) => i % 4 == 0).Count(x => x == 3)));
        // The layered wall: a mesh per material (plaster, brick, insulation), as its GLB node has a primitive per material.
        var layered = Objects(c.Root).Single(o => o.applicationId == ids["layered"]);
        var glbLayered = scene.Groups.SelectMany(g => g.Elements).Single(e => e.GlobalId == ids["layered"]);
        Assert.Equal(glbLayered.Parts.Count, layered.displayValue.Count);
        // Its extent: 6 m along x, in metres from the base point (the first point at the origin).
        var xs = layered.displayValue.Cast<global::Speckle.Objects.Geometry.Mesh>().SelectMany(m => m.vertices.Where((_, i) => i % 3 == 0)).ToList();
        Assert.InRange(xs.Max() - xs.Min(), 6 - 0.6, 6 + 0.6);

        // Every mesh coloured by one render material proxy, named as the project's material; the insulation half see-through.
        var proxies = Proxies<RenderMaterialProxy>(c.Root, "renderMaterialProxies");
        Assert.Equal(c.Materials, proxies.Count);
        var coloured = proxies.SelectMany(p => p.objects).ToList();
        Assert.Equal(meshes.Select(m => m.applicationId).Order(), coloured.Order());
        Assert.Equal(coloured.Count, coloured.Distinct().Count());
        var insulation = proxies.Single(p => p.value.name == "Insulation").value;
        Assert.Equal(0.5, insulation.opacity, 3);
        Assert.Equal(1.0, proxies.Single(p => p.value.name == "Brick").value.opacity, 3);
        Assert.Equal(0xFF, (insulation.diffuse >> 24) & 0xFF);

        // The storeys as levels, each naming its elements.
        var levels = Proxies<LevelProxy>(c.Root, "levelProxies");
        Assert.Equal(2, levels.Count);
        Assert.Contains(ids["upper"], levels[1].objects);
        Assert.Equal("First floor", levels[1].value.name);
    }

    [Fact]
    public void An_element_says_its_class_type_storey_layers_sets_and_quantities()
    {
        var (f, ids) = Project();
        var c = Convert(f);
        var wall = Objects(c.Root).Single(o => o.applicationId == ids["layered"]).properties;
        Assert.Equal("IfcWall", wall["IFC class"]);
        Assert.Equal(ids["layered"], wall["GlobalId"]);
        Assert.Equal("Brick 37", wall["Type"]);
        Assert.Equal(Spatial.Storeys(f.Model).First().Name, wall["Storey"]);
        var layers = Assert.IsAssignableFrom<IList<object?>>(wall["Layers"]).Cast<Dictionary<string, object?>>().ToList();
        Assert.Equal(["Plaster", "Brick", "Insulation"], layers.Select(l => (string)l["Material"]!));
        Assert.Equal([0.02, 0.25, 0.1], layers.Select(l => (double)l["Thickness"]!));
        Assert.Equal([false, true, false], layers.Select(l => (bool)l["Core"]!));
        Assert.Contains("Brick", Assert.IsAssignableFrom<IList<object?>>(wall["Materials"]).Cast<string>());
        // The quantities the module measures, each with its unit.
        var q = Assert.IsType<Dictionary<string, object?>>(wall["Quantities"]);
        var baseQ = Assert.IsType<Dictionary<string, object?>>(q["Qto_WallBaseQuantities"]);
        var length = Assert.IsType<Dictionary<string, object?>>(baseQ["Length"]);
        Assert.Equal("m", length["units"]);
        Assert.InRange((double)length["value"]!, 5.5, 6.5);
        Assert.Contains(baseQ.Values.Cast<Dictionary<string, object?>>(), x => (string?)x["units"] == "m³");
        // A door: its class, its storey, its host's storey.
        var door = Objects(c.Root).Single(o => o.applicationId == ids["door"]).properties;
        Assert.Equal("IfcDoor", door["IFC class"]);
        Assert.Equal(Spatial.Storeys(f.Model).First().Name, door["Storey"]);
    }

    [Fact]
    public void The_applicationIds_stay_the_same_when_the_model_is_rebuilt_and_opened_again()
    {
        var (f, ids) = Project();
        var first = Objects(Convert(f).Root).Select(o => o.applicationId).Order().ToList();
        var firstMeshes = Objects(Convert(f).Root).SelectMany(o => o.displayValue).Select(m => m.applicationId).Order().ToList();
        // An edit rebuilds the walls and what they host: the same objects again, with their new shapes.
        f.Run("element.update", new JsonObject { ["id"] = ids["plain"], ["attrs"] = new JsonObject { ["height"] = 3.4 } });
        Assert.Equal(first, Objects(Convert(f).Root).Select(o => o.applicationId).Order());

        // Saved, then opened by another engine: the same GlobalIds, the same meshes' ids.
        var dir = Directory.CreateTempSubdirectory("lapin-speckle");
        try
        {
            var path = Path.Combine(dir.FullName, "house.lapin");
            Call(new EngineDispatcher(f.Session, PackageFiles.Create()), "project.save", new JsonObject { ["path"] = path });
            var g = new Fixture(new ArchitectureModule(), new SpeckleModule());
            Call(new EngineDispatcher(g.Session, PackageFiles.Create()), "project.open", new JsonObject { ["path"] = path });
            var again = Convert(g);
            Assert.Equal(first, Objects(again.Root).Select(o => o.applicationId).Order());
            Assert.Equal(firstMeshes, Objects(again.Root).SelectMany(o => o.displayValue).Select(m => m.applicationId).Order());
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void A_selection_or_a_view_sends_only_what_it_holds()
    {
        var (f, ids) = Project();
        var c = Convert(f, new JsonObject { ["what"] = "selection", ["ids"] = new JsonArray(ids["upper"], ids["slab"]) });
        Assert.Equal(new[] { ids["slab"], ids["upper"] }.Order(), Objects(c.Root).Select(o => o.applicationId!).Order());
        // As the 3D view shows it: what the interface does not draw left out.
        var v = Convert(f, new JsonObject { ["what"] = "view", ["hidden"] = new JsonArray(ids["plain"]) });
        Assert.DoesNotContain(ids["plain"], Objects(v.Root).Select(o => o.applicationId));
        Assert.Contains(ids["layered"], Objects(v.Root).Select(o => o.applicationId));
        Assert.Throws<CommandException>(() => Convert(f, new JsonObject { ["what"] = "selection", ["ids"] = new JsonArray() }));
        Assert.Throws<CommandException>(() => Convert(f, new JsonObject { ["what"] = "everything" }));
    }

    // The SDK's JSON serializer and Receive2, which the connector's Send2 matches (marked legacy by the SDK for servers from 2026.9 on).
#pragma warning disable CS0618
    [Fact]
    public async Task Speckles_own_serializer_writes_it_and_reads_it_back()
    {
        var (f, ids) = Project();
        var c = Convert(f);
        var services = new ServiceCollection();
        services.AddSpeckleSdk(new Application("Lapin", SpeckleConvert.SourceApplication), "test", typeof(global::Speckle.Objects.Geometry.Mesh).Assembly);
        var ops = services.BuildServiceProvider().GetRequiredService<IOperations>();
        var json = ops.Serialize(c.Root);
        Assert.Contains("Objects.Data.DataObject", json);
        Assert.Contains("Speckle.Core.Models.Collections.Collection", json);
        var back = await ops.DeserializeAsync(json);
        Assert.Equal(ids.Values.Order(), Objects(back).Select(o => o.applicationId!).Order());
        Assert.Equal(c.Materials, Proxies<RenderMaterialProxy>(back, "renderMaterialProxies").Count);
    }

    [Fact]
    public void Its_calls_are_the_hosts_alone_and_a_server_is_an_address()
    {
        foreach (var q in new[] { "speckle.send", "speckle.setAccount", "speckle.projects", "speckle.accounts" })
            foreach (var role in new[] { "editor", "owner", "viewer" })
                Assert.NotNull(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = q }, role));
        Assert.Equal("https://app.speckle.systems", SpeckleServer.ServerOf(""));
        Assert.Equal("https://speckle.firm.ro", SpeckleServer.ServerOf(" speckle.firm.ro/ "));
        Assert.Equal("http://127.0.0.1:8090", SpeckleServer.ServerOf("http://127.0.0.1:8090/projects"));
        Assert.Throws<CommandException>(() => SpeckleServer.ServerOf("ftp://x"));
        Assert.Equal("https://app.speckle.systems/projects/p1/models/m2@v3", SpeckleServer.VersionUrl("https://app.speckle.systems/", "p1", "m2", "v3"));
        // Not signed in: nothing is opened, nothing is sent.
        var f = new Fixture(new ArchitectureModule(), new SpeckleModule());
        Assert.Throws<CommandException>(() => f.Session.Query("speckle.openVersion", new JsonObject { ["url"] = "https://app.speckle.systems/projects/p1" }));
        Assert.Throws<CommandException>(() => f.Session.Query("speckle.send", new JsonObject { ["projectId"] = "p1", ["modelName"] = "x" }));
        Assert.Empty(f.Session.Query("speckle.accounts", [])!.AsArray());
    }

    /// <summary>
    /// A real server (deploy/speckle-dev for development): signed in, the model sent twice to a new model, each version
    /// received back with Speckle's SDK, the same applicationIds in both. Runs only when LAPIN_SPECKLE_TEST_URL and
    /// LAPIN_SPECKLE_TEST_TOKEN are set (never in CI); LAPIN_SPECKLE_TEST_PROJECT names the project, else the first one.
    /// </summary>
    [Fact]
    public async Task Sent_to_a_server_twice_the_versions_hold_the_same_objects()
    {
        var url = Environment.GetEnvironmentVariable("LAPIN_SPECKLE_TEST_URL");
        var token = Environment.GetEnvironmentVariable("LAPIN_SPECKLE_TEST_TOKEN");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token)) return;
        var (f, ids) = Project();
        var account = f.Session.Query("speckle.setAccount", new JsonObject { ["server"] = url, ["token"] = token })!;
        Assert.Equal(SpeckleServer.ServerOf(url), (string?)account["server"]);
        var projects = f.Session.Query("speckle.projects", [])!.AsArray();
        var project = Environment.GetEnvironmentVariable("LAPIN_SPECKLE_TEST_PROJECT") is { Length: > 0 } p ? p : (string)projects[0]!["id"]!;
        var stages = new List<string>();
        f.Session.Progress = (stage, _, _) => { lock (stages) stages.Add(stage); };
        var first = f.Session.Query("speckle.send", new JsonObject { ["projectId"] = project, ["modelName"] = $"lapin-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}", ["message"] = "first" })!;
        var model = (string)first["modelId"]!;
        Assert.Contains(model, f.Session.Query("speckle.models", new JsonObject { ["projectId"] = project })!.AsArray().Select(x => (string?)x!["id"]));
        f.Run("element.update", new JsonObject { ["id"] = ids["plain"], ["attrs"] = new JsonObject { ["height"] = 3.4 } });
        var second = f.Session.Query("speckle.send", new JsonObject { ["projectId"] = project, ["modelId"] = model, ["message"] = "second" })!;
        Assert.NotEqual((string?)first["versionId"], (string?)second["versionId"]);
        Assert.Contains("Uploading to Speckle", stages);
        Assert.StartsWith(SpeckleServer.ServerOf(url) + "/projects/", (string)second["url"]!);

        var services = new ServiceCollection();
        services.AddSpeckleSdk(new Application("Lapin", SpeckleConvert.SourceApplication), "test", typeof(global::Speckle.Objects.Geometry.Mesh).Assembly);
        var ops = services.BuildServiceProvider().GetRequiredService<IOperations>();
        async Task<List<string?>> Received(JsonNode sent) => Objects(await ops.Receive2(new Uri(url), project, (string)sent["rootId"]!, token, null, default)).Select(o => o.applicationId).Order().ToList();
        var a = await Received(first);
        Assert.Equal(ids.Values.Order(), a);
        Assert.Equal(a, await Received(second));
    }
#pragma warning restore CS0618

    /// <summary>A call as the interface makes it (File → Save, File → Open).</summary>
    static void Call(EngineDispatcher d, string method, JsonObject p)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        Assert.DoesNotContain("\"error\"", Encoding.UTF8.GetString(reply));
    }
}
