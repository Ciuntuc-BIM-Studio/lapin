using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A session of the full product, with helpers that run its commands as the interface does.</summary>
sealed class Fixture
{
    public Session Session { get; }

    public Fixture(params ILapinModule[] modules)
    {
        ILapinModule[] use = modules.Length == 0 ? [new ArchitectureModule()] : modules;
        Session = new Session(ModuleCatalog.Create(use), new ProductManifest("test", "Test", ["core", .. use.Select(m => m.Id)]));
    }

    public IModelView Model => Session.Model;
    public string Storey => Spatial.Storeys(Model).First().Id;

    public JsonNode? Run(string command, JsonObject args) { Session.Execute(command, args, out var r); return r; }
    public Diff RunDiff(string command, JsonObject args) => Session.Execute(command, args, out _);

    public string Type(string name, params (string Material, double Thickness, bool Core)[] layers) =>
        (string)Run("architecture.createWallType", new JsonObject
        {
            ["name"] = name,
            ["layers"] = new JsonArray(layers.Select(l => (JsonNode)new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness, ["core"] = l.Core }).ToArray()),
        })!["id"]!;

    public string Wall(double x1, double y1, double x2, double y2, string? typeId = null)
    {
        var a = new JsonObject { ["storeyId"] = Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
        if (typeId != null) a["typeId"] = typeId;
        return (string)Run("architecture.addWall", a)!["id"]!;
    }

    /// <summary>The bounds of everything the element's geometry built.</summary>
    public (Vec3 Min, Vec3 Max) Bounds(string id)
    {
        var g = Session.Geometry(id) ?? throw new InvalidOperationException($"{id} has no geometry.");
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)),
                new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    public LayerUsage Usage(string wallId) => Layers.ReadUsage(Walls.UsageOf(Model, wallId)!)!;
}
