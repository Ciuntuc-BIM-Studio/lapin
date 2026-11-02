using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// Prefabricated timber-frame panels (the wood module, docs/plan-wood.md, decision 245): a wall cut into panels no longer
/// than the type's panel length, the joints on the studs' module and never through an opening's framing, each panel
/// framed with its own end studs; the panels' parts, their schedule and their IFC assemblies.
/// </summary>
public class WoodPanelTests
{
    static readonly FrameSpec Panels = FrameSpec.Nordic with { PanelMax = 4 };

    [Fact]
    public void A_wall_is_cut_into_panels_on_the_module_clear_of_its_openings()
    {
        Framing.Hole[] holes = [new("W", 3.6, 0.9, 1.2, 1.3, false), new("D", 7.5, 0, 0.9, 2.1, true)];
        var r = Framing.Panelize(Panels, 0, 10, _ => 2.7, [], -0.0725, 0.0725, holes, 0);
        Assert.NotNull(r.Panels);
        Assert.True(r.Panels!.Count >= 3);
        Assert.Equal(0, r.Panels[0].U0, 9);
        Assert.Equal(10, r.Panels[^1].U1, 9);
        Assert.All(r.Panels, p => Assert.True(p.U1 - p.U0 <= 4 + 1e-6, $"{p.U0}–{p.U1}"));
        for (var i = 0; i + 1 < r.Panels.Count; i++) Assert.Equal(r.Panels[i].U1, r.Panels[i + 1].U0, 9);
        // Every joint clear of the openings' framing: an opening's members all in one panel, within it.
        foreach (var id in new[] { "W", "D" })
        {
            var mine = r.Members.Where(m => m.Opening == id).ToList();
            Assert.Single(mine.Select(m => m.Panel).Distinct());
            var p = r.Panels[mine[0].Panel - 1];
            Assert.All(mine, m => Assert.True(m.U0 >= p.U0 + 2 * 0.045 - 1e-6 && m.U1 <= p.U1 - 2 * 0.045 + 1e-6));
        }
        Assert.True(r.Openings.ContainsKey("W") && r.Openings.ContainsKey("D"));
        // Each panel its own end studs at both ends, its own plates.
        for (var i = 1; i <= r.Panels.Count; i++)
        {
            Assert.Equal(2 * Panels.EndStuds, r.Members.Count(m => m.Panel == i && m.Role == "end"));
            Assert.Contains(r.Members, m => m.Panel == i && m.Role == "plate");
        }
        // The joints that are not at an opening's edge fall on the module.
        Assert.Contains(r.Panels.Skip(1), p => Math.Abs(p.U0 / 0.6 - Math.Round(p.U0 / 0.6)) < 1e-6);
        Assert.Empty(r.Warnings);
        // A frame built on site has no panels.
        Assert.Null(Framing.Panelize(FrameSpec.Nordic, 0, 10, _ => 2.7, [], -0.0725, 0.0725, holes, 0).Panels);
        // A spec that cannot be cut.
        Assert.NotNull((FrameSpec.Nordic with { PanelMax = 0.5 }).Problem());
        Assert.Equal(4, FrameSpec.From(Panels.ToJson()).PanelMax);
    }

    [Fact]
    public void A_panel_too_wide_for_its_length_is_kept_whole_and_warned()
    {
        // A 3.4 m opening's framing does not fit in a 2.4 m panel: the panel takes it whole, and says so.
        var r = Framing.Panelize(FrameSpec.Nordic with { PanelMax = 2.4 }, 0, 8, _ => 2.7, [], -0.0725, 0.0725, [new("W", 4, 0.9, 3.4, 1.3, false)], 0);
        Assert.Contains(r.Warnings, w => w.Contains("no joint within 2400 mm"));
        var mine = r.Members.Where(m => m.Opening == "W").ToList();
        Assert.Single(mine.Select(m => m.Panel).Distinct());
    }

    [Fact]
    public void A_prefabricated_wall_s_parts_its_schedule_and_IFC()
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 0 })!["id"]!;
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3.6 });
        var type = Walls.TypeOf(f.Model, wall)!.Id;
        f.Run("wood.setWallFrame", new JsonObject { ["id"] = type, ["spec"] = Panels.ToJson() });
        var parts = f.Session.Geometry(wall)!.Parts;
        var mark = WoodModule.PanelMark(f.Model.Get(wall)!, 1);
        Assert.Contains(parts, p => p.Key!.StartsWith("wood:end:") && p.Properties?.Contains(("Panel", mark)) == true);
        var w = (JsonObject)f.Session.Query("wood.wall", new JsonObject { ["id"] = wall })!;
        Assert.True(((JsonArray)w["panels"]!).Count >= 3);
        var rows = PanelSchedule.Rows(new Q(f));
        Assert.Equal(((JsonArray)w["panels"]!).Count, rows.Count);
        Assert.Equal(mark, rows[0].Mark);
        Assert.All(rows, r => Assert.True(r.Length <= 4 + 1e-6 && r.Members > 0 && r.Mass > 0));
        Assert.Equal(1, rows.Sum(r => r.Openings));
        Assert.Equal(10, rows.Sum(r => r.Length), 3);
        var path = Path.Combine(Path.GetTempPath(), $"wood-panels-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var file = StepFile.Read(path);
            var assemblies = file.OfType("IFCELEMENTASSEMBLY").Where(a => (string?)a[4] == "Timber frame panel").ToList();
            Assert.Equal(rows.Count, assemblies.Count);
            Assert.Contains(assemblies, a => (string?)a[2] == mark);
        }
        finally { File.Delete(path); }
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
