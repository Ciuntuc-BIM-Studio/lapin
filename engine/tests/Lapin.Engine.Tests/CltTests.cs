using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>CLT walls and floors (the wood module, stage 5, decision 241): the core in panels, joints clear of the openings, the schedule and IFC.</summary>
public class CltTests
{
    [Fact]
    public void Joints_keep_clear_of_the_openings_by_the_edge_distance()
    {
        var spec = CltSpec.Wall with { MaxLength = 5 };
        // A 12 m run: 5 m panels would join at 5 and 10; a window from 4.5 to 6 moves the first joint back to 4.2 (its edge less 0.3).
        var (joints, warnings) = Clt.Joints(spec, 0, 12, [("W", 4.5, 6)]);
        Assert.Equal(4.2, joints[0], 9);
        Assert.All(joints, j => Assert.False(j > 4.2 && j < 6.3));
        Assert.Empty(warnings);
        // Three panels: 4.2, 5 and 2.8 m.
        Assert.Equal(new[] { 4.2, 9.2 }, joints.Select(j => Math.Round(j, 9)));
        // Too wide an opening for a panel: told.
        Assert.NotEmpty(Clt.Joints(spec, 0, 12, [("Wide", 1, 6)]).Warnings);
    }

    [Fact]
    public void A_clt_wall_and_floor_are_built_of_panels()
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 20, ["y2"] = 0 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 16 })!["id"]!;
        var type = Walls.TypeOf(f.Model, wall)!.Id;
        f.Run("wood.setClt", new JsonObject { ["id"] = type, ["spec"] = CltSpec.Wall.ToJson() });
        var g = f.Session.Geometry(wall)!;
        var core = Walls.Section(f.Model, f.Model.Get(wall)!).First(b => b.Core);
        Assert.DoesNotContain(g.Parts, p => p.Key == $"layer:{core.Index}");
        var panels = g.Parts.Where(p => p.Key!.StartsWith("clt:")).ToList();
        // 20 m (and the ends) in panels of at most 16 m: two, the joint clear of the window at 16 m.
        Assert.Equal(2, panels.Count);
        Assert.All(panels, p => Assert.True(p.Solid.Vertices.Max(v => v.X) - p.Solid.Vertices.Min(v => v.X) <= 16 + 1e-6));
        Assert.Contains(panels, p => p.Properties!.Contains(("Plies", "3 plies 40/40/40 mm")));
        // The window's rough opening is cut from its panel: the panel holding it is lighter than its box.
        Assert.Contains(g.Diagnostics ?? [], d => d.StartsWith("The core is"));

        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)) })!["id"]!;
        var slabType = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id;
        f.Run("wood.setClt", new JsonObject { ["id"] = slabType, ["spec"] = CltSpec.Floor.ToJson() });
        // 10 × 6 m: spanning the 6 m way, 3.5 m wide panels across the 10 m: three.
        Assert.Equal(3, f.Session.Geometry(slab)!.Parts.Count(p => p.Key!.StartsWith("clt:")));
        var grid = new CltSchedule().Grid(new Q(f));
        Assert.Equal(5 + 1, grid.Rows.Count);
        var path = Path.Combine(Path.GetTempPath(), $"clt-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            Assert.Equal(5, StepFile.Read(path).OfType("IFCBUILDINGELEMENTPART").Count(p => (string?)p[4] == "CLT panel"));
        }
        finally { File.Delete(path); }
        Assert.Throws<CommandException>(() => f.Run("wood.setClt", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["plies"] = new JsonArray(40, 40) } }));
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
