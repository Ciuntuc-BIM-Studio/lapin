using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A session with Architecture and MEP, and helpers that run the MEP commands as the interface does.</summary>
sealed class MepFixture
{
    public Session Session { get; }

    public MepFixture()
    {
        ILapinModule[] use = [new ArchitectureModule(), new MepModule()];
        Session = new Session(ModuleCatalog.Create(use), new ProductManifest("test", "Test", ["core", .. use.Select(m => m.Id)]));
    }

    public IModelView Model => Session.Model;
    public string Storey => Spatial.Storeys(Model).First().Id;

    public JsonNode? Run(string command, JsonObject args) { Session.Execute(command, args, out var r); return r; }
    public string Id(string command, JsonObject args) => (string)Run(command, args)!["id"]!;

    public string System(string service = Services.Heating, JsonObject? defaults = null)
    {
        var a = new JsonObject { ["service"] = service };
        if (defaults != null) a["defaults"] = defaults;
        return Id("mep.createSystem", a);
    }

    public string Node(string system, string role, double x, double y, JsonObject? more = null)
    {
        var a = new JsonObject { ["systemId"] = system, ["role"] = role, ["x"] = x, ["y"] = y };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return Id("mep.placeNode", a);
    }

    public static JsonArray Pts(params (double X, double Y)[] pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    public string Draw(string system, JsonArray points, JsonObject? start = null, JsonObject? end = null, JsonObject? more = null)
    {
        var a = new JsonObject { ["systemId"] = system, ["points"] = points };
        if (start != null) a["start"] = start;
        if (end != null) a["end"] = end;
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return Id("mep.drawRun", a);
    }

    /// <summary>The bounds of the element's geometry, or of one of its parts by key.</summary>
    public (Vec3 Min, Vec3 Max) Bounds(string id, string? key = null)
    {
        var g = Session.Geometry(id) ?? throw new InvalidOperationException($"{id} has no geometry.");
        var parts = g.Parts.Where(p => key == null || p.Key == key).Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        if (parts.Count == 0) throw new InvalidOperationException($"{id} has no part {key}.");
        return (new Vec3(parts.Min(b => b.Min.X), parts.Min(b => b.Min.Y), parts.Min(b => b.Min.Z)), new Vec3(parts.Max(b => b.Max.X), parts.Max(b => b.Max.Y), parts.Max(b => b.Max.Z)));
    }

    public JsonArray Open(string system) => (JsonArray)Session.Query("mep.openPorts", new JsonObject { ["systemId"] = system })!;
}
