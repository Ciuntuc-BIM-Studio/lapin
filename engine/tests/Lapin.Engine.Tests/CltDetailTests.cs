using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// CLT, decision 246: the joints modelled (half-lap, spline with its strip), the openings' corners as the router leaves
/// them, the CLT wall's panel elevation and CLT roofs.
/// </summary>
public class CltDetailTests
{
    [Fact]
    public void A_half_lap_overlaps_and_a_spline_leaves_a_rebate_for_its_strip()
    {
        // A joint at 5 between a panel from 0 and one to 10, 120 mm thick.
        var lap = CltSpec.Wall with { Joint = "halfLap", LapWidth = 0.1 };
        var left = Clt.Slices(lap, null, 5, 0, 10, 0, 0.12);
        var right = Clt.Slices(lap, 5, null, 0, 10, 0, 0.12);
        Assert.Equal([(0, 5.05, 0, 0.06), (0, 4.95, 0.06, 0.12)], left.Select(s => (Math.Round(s.A, 6), Math.Round(s.B, 6), Math.Round(s.T0, 6), Math.Round(s.T1, 6))));
        Assert.Equal([(5.05, 10, 0, 0.06), (4.95, 10, 0.06, 0.12)], right.Select(s => (Math.Round(s.A, 6), Math.Round(s.B, 6), Math.Round(s.T0, 6), Math.Round(s.T1, 6))));
        var spline = CltSpec.Wall with { Joint = "spline", SplineWidth = 0.12, SplineDepth = 0.027 };
        var l2 = Clt.Slices(spline, null, 5, 0, 10, 0, 0.12);
        Assert.Equal(4.94, l2[0].B, 9);
        Assert.Equal(5, l2[1].B, 9);
        Assert.Equal((4.94, 5.06, 0, 0.027), Clt.Splines(spline, 5, 0)!.Value);
        Assert.Single(Clt.Slices(CltSpec.Wall with { Joint = "butt" }, null, 5, 0, 10, 0, 0.12));
    }

    [Fact]
    public void The_router_rounds_an_opening_s_corners_or_overcuts_them()
    {
        var round = Clt.Cutters(CltSpec.Wall with { Corners = "round", CornerRadius = 0.02 }, 1, 2, 0.9, 2.2);
        var loop = Assert.Single(round);
        Assert.All(loop, p => Assert.True(p.U >= 1 - 1e-9 && p.U <= 2 + 1e-9 && p.Z >= 0.9 - 1e-9 && p.Z <= 2.2 + 1e-9));
        Assert.DoesNotContain(loop, p => Math.Abs(p.U - 1) < 1e-9 && Math.Abs(p.Z - 0.9) < 1e-9);
        var dog = Clt.Cutters(CltSpec.Wall with { Corners = "dogbone", CornerRadius = 0.02 }, 1, 2, 0.9, 2.2);
        Assert.Equal(5, dog.Count);
        // Each circle passes through its corner and reaches past the rectangle's sides.
        Assert.Contains(dog.Skip(1), c => c.Min(p => p.U) < 1 - 0.003);
        Assert.Single(Clt.Cutters(CltSpec.Wall, 1, 2, 0.9, 2.2));
    }

    [Fact]
    public void Joints_are_modelled_in_the_wall_and_floor_and_the_wall_has_a_panel_elevation()
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 12, ["y2"] = 0 })!["id"]!;
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 });
        var type = Walls.TypeOf(f.Model, wall)!.Id;
        var core = Walls.Section(f.Model, f.Model.Get(wall)!).First(b => b.Core);
        var coreVolume = Measure.Volume(f.Session.Geometry(wall)!.Parts.First(p => p.Key == $"layer:{core.Index}").Solid);
        double Panels(CltSpec spec)
        {
            f.Run("wood.setClt", new JsonObject { ["id"] = type, ["spec"] = (spec with { MaxLength = 5 }).ToJson() });
            return f.Session.Geometry(wall)!.Parts.Where(p => p.Key!.StartsWith("clt")).Sum(p => Measure.Volume(p.Solid));
        }
        var butt = Panels(CltSpec.Wall with { Joint = "butt" });
        // The joints only move material between the panels: the lap's halves and the spline's strip fill what was cut.
        Assert.Equal(butt, Panels(CltSpec.Wall with { Joint = "halfLap" }), 6);
        Assert.Equal(butt, Panels(CltSpec.Wall with { Joint = "spline" }), 6);
        var parts = f.Session.Geometry(wall)!.Parts;
        Assert.Equal(2, parts.Count(p => p.Key!.StartsWith("clt-spline:")));
        Assert.True(butt < coreVolume);
        // Round corners leave material in the window's corners; a dogbone takes more.
        var round = Panels(CltSpec.Wall with { Joint = "butt", Corners = "round" });
        var dogbone = Panels(CltSpec.Wall with { Joint = "butt", Corners = "dogbone" });
        Assert.True(round > butt && dogbone < butt, $"{round} {butt} {dogbone}");
        // The panel elevation: the joints dimensioned, each panel's mark.
        f.Run("wood.setClt", new JsonObject { ["id"] = type, ["spec"] = (CltSpec.Wall with { MaxLength = 5 }).ToJson() });
        var view = (string)f.Run("wood.frameView", new JsonObject { ["id"] = wall })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var texts = ((JsonArray)d["pens"]!).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []).ToList();
        var name = f.Model.Get(wall)!.Name;
        Assert.Contains($"{name}-1", texts);
        Assert.Contains($"{name}-3", texts);

        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)) })!["id"]!;
        var slabType = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id;
        f.Run("wood.setClt", new JsonObject { ["id"] = slabType, ["spec"] = (CltSpec.Floor with { Joint = "spline" }).ToJson() });
        var floor = f.Session.Geometry(slab)!.Parts;
        Assert.Equal(3, floor.Count(p => p.Key!.StartsWith("clt:")));
        var strips = floor.Where(p => p.Key!.StartsWith("clt-spline:")).ToList();
        Assert.Equal(2, strips.Count);
        // The strips lie in a rebate on the top face.
        var top = floor.Where(p => p.Key!.StartsWith("clt:")).Max(p => p.Solid.Vertices.Max(v => v.Z));
        Assert.All(strips, s => Assert.Equal(top, s.Solid.Vertices.Max(v => v.Z), 6));
    }

    [Fact]
    public void A_roof_of_clt_has_panels_on_its_planes_in_place_of_its_rafters()
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)), ["style"] = "traditional_campie" })!["id"]!;
        Assert.Contains(f.Session.Geometry(roof)!.Parts, p => p.Key!.StartsWith("timber:rafter"));
        f.Run("wood.setRoofClt", new JsonObject { ["id"] = roof, ["spec"] = CltSpec.Floor.ToJson() });
        var parts = f.Session.Geometry(roof)!.Parts;
        Assert.DoesNotContain(parts, p => p.Key!.StartsWith("timber:rafter") || p.Key!.StartsWith("truss:"));
        Assert.Contains(parts, p => p.Key == "covering");
        var panels = parts.Where(p => p.Key!.StartsWith("clt:")).ToList();
        // Two slopes along a 10 m eave (and its overhangs) in strips at most 3.5 m wide: at least four panels, none wider.
        Assert.True(panels.Count >= 4, $"{panels.Count}");
        Assert.All(panels, p => Assert.True(double.Parse(p.Properties!.First(x => x.Name == "Width").Value, System.Globalization.CultureInfo.InvariantCulture) <= 3.5 + 1e-6));
        Assert.Contains(new CltSchedule().Grid(new Q(f)).Rows, r => r[1] == f.Model.Get(roof)!.Name);
        Assert.DoesNotContain(TimberSchedule.Rows(new Q(f)), r => r.Roof == f.Model.Get(roof)!.Name && r.Role == "Rafter");
        var path = Path.Combine(Path.GetTempPath(), $"clt-roof-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            Assert.Equal(panels.Count, StepFile.Read(path).OfType("IFCBUILDINGELEMENTPART").Count(p => (string?)p[4] == "CLT panel"));
        }
        finally { File.Delete(path); }
        f.Run("wood.setRoofClt", new JsonObject { ["id"] = roof, ["spec"] = null });
        Assert.Contains(f.Session.Geometry(roof)!.Parts, p => p.Key!.StartsWith("timber:rafter"));
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
