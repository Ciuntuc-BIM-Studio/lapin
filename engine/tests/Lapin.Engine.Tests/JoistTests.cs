using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>Joisted floors (the wood module, stage 3, decision 244): joists the short way, rims, blocking, a stairwell's trimmers and headers.</summary>
public class JoistTests
{
    static readonly Vec2[] Floor = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)];
    static readonly Vec2[] Well = [new(0.6, 0.6), new(3.4, 0.6), new(3.4, 1.6), new(0.6, 1.6)];

    [Fact]
    public void Joists_span_the_short_way_and_a_stairwell_gets_trimmers_and_headers()
    {
        var r = Joists.Layout(JoistSpec.Default, Floor, [Well], -0.22, 0);
        int N(string role) => r.Members.Count(m => m.Role == role);
        // 6 × 4 m: the joists span the 4 m, along y.
        Assert.Equal(new Vec2(0, 1), r.Along);
        Assert.Equal(4, N("rim"));
        // Two trimmers each side of the well, two plies of header at each end.
        Assert.Equal(4, N("trimmer"));
        Assert.Equal(4, N("header"));
        Assert.True(N("tail") > 0);
        Assert.True(N("joist") > 0);
        Assert.True(N("blocking") > 0);
        // Nothing in the well; nothing past the floor.
        bool InWell(Joists.Member m) => m.Outline.Min(p => p.X) < 3.4 - 1e-3 && m.Outline.Max(p => p.X) > 0.6 + 1e-3 && m.Outline.Min(p => p.Y) < 1.6 - 1e-3 && m.Outline.Max(p => p.Y) > 0.6 + 1e-3;
        Assert.DoesNotContain(r.Members.Where(m => m.Role is "joist" or "tail" or "blocking"), InWell);
        Assert.All(r.Members, m => Assert.All(m.Outline, p => Assert.True(p.X >= -1e-6 && p.X <= 6 + 1e-6 && p.Y >= -1e-6 && p.Y <= 4 + 1e-6)));
        // The joists at 600 mm: no two further apart.
        var xs = r.Members.Where(m => m.Role is "joist" or "tail" or "trimmer").Select(m => Math.Round(m.Outline.Average(p => p.X), 4)).Distinct().OrderBy(x => x).ToList();
        Assert.All(xs.Zip(xs.Skip(1)), p => Assert.True(p.Second - p.First <= 0.6 + 1e-6, string.Join(" ", xs)));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void A_floor_of_a_joisted_type_is_built_of_its_joists()
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray([.. Floor.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]) })!["id"]!;
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = new JsonArray([.. Well.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]) });
        var type = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id;
        f.Run("wood.setFloorJoists", new JsonObject { ["id"] = type, ["spec"] = JoistSpec.Default.ToJson() });
        var parts = f.Session.Geometry(slab)!.Parts;
        Assert.Contains(parts, p => p.Key!.StartsWith("wood:joist:"));
        Assert.Contains(parts, p => p.Key!.StartsWith("wood:trimmer:"));
        Assert.Contains(TimberSchedule.Rows(new Q(f)), r => r.Role == "Joist" && r.Section == "45x220 mm");
        var p = (JsonObject)f.Session.Query("wood.joistPreview", new JsonObject { ["spec"] = JoistSpec.Default.ToJson() })!;
        Assert.True((int)p["counts"]!["header"]! == 4);
        var path = Path.Combine(Path.GetTempPath(), $"joists-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            Assert.Contains(StepFile.Read(path).OfType("IFCBEAM"), b => b[8] is StepEnum { Value: "JOIST" });
        }
        finally { File.Delete(path); }
        Assert.Throws<CommandException>(() => f.Run("wood.setFloorJoists", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["spacing"] = 2 } }));
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
