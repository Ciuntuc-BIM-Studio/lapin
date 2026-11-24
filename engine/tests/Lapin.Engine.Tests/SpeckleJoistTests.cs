using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Speckle;
using Lapin.Steel;
using Lapin.Structure;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Other;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using SLine = Speckle.Objects.Geometry.Line;
using SMesh = Speckle.Objects.Geometry.Mesh;
using SPoint = Speckle.Objects.Geometry.Point;

namespace Lapin.Engine.Tests;

/// <summary>
/// A Speckle version's bar joists (decision 827), as Revit's v3 connector sends a K-Series joist: its chords as one mesh, its
/// webs as lines beside it. Recognised as native open-web joists under the fidelity gate, and kept by later versions: moved,
/// it is the same joist (its GlobalId); deformed, the reference it then is, with that id; gone, it goes.
/// </summary>
[Collection("Speckle SDK")]
public sealed class SpeckleJoistTests
{
    const string Server = "https://speckle.test";

    static (Fixture F, SpeckleModule Module) Project()
    {
        var module = new SpeckleModule();
        return (new Fixture(new ArchitectureModule(), new StructureModule(), new SteelModule(), module), module);
    }

    /// <summary>The joist as Revit sends it: a DataObject of the K-Series family, its chords one mesh (metres), its webs lines.</summary>
    static DataObject Joist(string appId, double x0, double y, double top, double sag = 0)
    {
        var (v, t, lines) = OpenWebJoistTests.RevitJoist(x0, y, top, sag: sag);
        var mesh = new SMesh
        {
            applicationId = appId + "/mesh", units = "m",
            vertices = [.. v.SelectMany(p => new[] { p.X, p.Y, p.Z })],
            faces = [.. t.SelectMany(x => new[] { 3, x.Item1, x.Item2, x.Item3 })],
        };
        var display = new List<Base> { mesh };
        display.AddRange(lines.Select(l => (Base)new SLine { start = new SPoint(l.Item1.X, l.Item1.Y, l.Item1.Z, "m"), end = new SPoint(l.Item2.X, l.Item2.Y, l.Item2.Z, "m"), units = "m" }));
        return new DataObject
        {
            applicationId = appId, name = "Structural Framing - 16K2", displayValue = display,
            properties = new() { ["category"] = "Structural Framing", ["family"] = "K-Series Bar Joist-Angle Web", ["type"] = "16K2" },
        };
    }

    static RootCollection Version(params Base[] objects)
    {
        SpeckleSdk.Ensure();
        var root = new RootCollection { name = "Structure", applicationId = "structure-root", elements = [new Collection { name = "Level 1", elements = [.. objects] }] };
        var level = new DataObject { name = "Level 1", displayValue = [], properties = [] };
        level["elevation"] = 0.0;
        level["units"] = "m";
        root["levelProxies"] = new List<LevelProxy> { new() { applicationId = "level-1", value = level, objects = [.. objects.Select(o => o.applicationId!)] } };
        return root;
    }

#pragma warning disable CS0618
    static async Task<Base> RoundTrip(Base root)
    {
        var ops = SpeckleSdk.Services.GetRequiredService<IOperations>();
        return await ops.DeserializeAsync(ops.Serialize(root));
    }
#pragma warning restore CS0618

    static JsonObject Receive(Fixture f, SpeckleModule module, Base root, string version)
    {
        module.Prime(new SpeckleFetched(new SpeckleSource(Server, "p1", "m1", "Structure", version, "revit"), root));
        return f.Run("speckle.receive", new JsonObject { ["projectId"] = "p1", ["modelId"] = "m1", ["versionId"] = version, ["recognize"] = true })!.AsObject();
    }

    static Entity? ByApp(Fixture f, string app) => f.Model.Entities.SingleOrDefault(e => (string?)(e.Attrs[SpeckleReceive.Key] as JsonObject)?["applicationId"] == app && e.Class != IfcClass.BuildingStorey);

    [Fact]
    public async Task A_bar_joist_comes_in_native_and_later_versions_keep_its_id()
    {
        var (f, module) = Project();
        var r = Receive(f, module, await RoundTrip(Version(Joist("j1", 1, 2, 3), Joist("j2", 1, 4, 3))), "v1");
        Assert.Equal(2, (int)r["recognized"]!);
        Assert.Equal(2, (int)r["recognition"]!["byClass"]![OpenWebJoists.Class]!);
        var j1 = ByApp(f, "j1")!;
        Assert.Equal(OpenWebJoists.Class, j1.Class);
        Assert.False(StoredBodies.Has(j1));
        Assert.Equal("native", (string?)j1.Attrs[SpeckleReceive.Key]!["recognition"]);
        // Its webs the lines Revit sent with it; one type for both, named after Revit's.
        Assert.True(j1.Attrs[OpenWebJoists.WebAxesKey] is JsonArray { Count: > 10 });
        Assert.Single(f.Model.OfClass(OpenWebJoists.TypeClass));
        Assert.Equal("16K2", f.Model.RelatingOf(j1.Id, IfcClass.RelDefinesByType)!.Name);
        var id = j1.Id;
        var (lo, hi) = f.Bounds(id);
        Assert.Equal(3, hi.Z, 4);
        Assert.Equal(1, lo.X, 4);

        // Moved 1 m along y: updated, native again, the same joist.
        r = Receive(f, module, await RoundTrip(Version(Joist("j1", 1, 3, 3), Joist("j2", 1, 4, 3))), "v2");
        Assert.Equal(1, (int)r["updated"]!);
        var moved = ByApp(f, "j1")!;
        Assert.Equal(id, moved.Id);
        Assert.Equal(OpenWebJoists.Class, moved.Class);
        var (lo2, _) = f.Bounds(id);
        Assert.Equal(lo.Y + 1, lo2.Y, 4);

        // Its bottom chord sagging 20 mm: the reference it now is, with the same id, in the model's group.
        Receive(f, module, await RoundTrip(Version(Joist("j1", 1, 3, 3, sag: 0.02), Joist("j2", 1, 4, 3))), "v3");
        var sagging = ByApp(f, "j1")!;
        Assert.Equal(id, sagging.Id);
        Assert.Equal(IfcClass.Beam, sagging.Class);
        Assert.True(StoredBodies.Has(sagging));
        Assert.Equal("reference", (string?)sagging.Attrs[SpeckleReceive.Key]!["recognition"]);
        Assert.NotNull(Groups.Of(f.Model, id));

        // Straight again: native again, still the same id; then gone from the version, gone from the project.
        Receive(f, module, await RoundTrip(Version(Joist("j1", 1, 3, 3), Joist("j2", 1, 4, 3))), "v4");
        Assert.Equal(OpenWebJoists.Class, f.Model.Get(id)!.Class);
        Receive(f, module, await RoundTrip(Version(Joist("j2", 1, 4, 3))), "v5");
        Assert.Null(f.Model.Get(id));
        Assert.Single(f.Model.OfClass(OpenWebJoists.Class));
    }
}
