using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A 3D view's layers (docs/view-styles.md, decision 162): every layer, without the
/// finishes, or the cores only, by its style (a Structural 3D view: cores only) or its
/// own choice. The render says, part by part, which modes leave it out; the caps, the
/// export as the view shows it follow; plans are not touched.
/// </summary>
public sealed class View3dLayersTests
{
    /// <summary>Plaster 20, brick 250 (the core), mineral wool 100, render 10: 380 mm, along X for 5 m.</summary>
    static (Fixture F, string Wall) PlasteredWall()
    {
        var f = new Fixture();
        var type = f.Type("Plastered brick", ("Lime plaster", 0.02, false), ("Brick", 0.25, true), ("Mineral wool", 0.1, false), ("Silicate render", 0.01, false));
        return (f, f.Wall(0, 0, 5, 0, type));
    }

    static RenderItem Item(Fixture f, string id) => f.Session.Snapshot().Render.Single(r => r.NodeId == id);

    /// <summary>The volume a closed triangle mesh holds (divergence theorem): only a closed one gives the solid's.</summary>
    static double MeshVolume(float[] p, int[] ix)
    {
        double v = 0;
        for (var t = 0; t + 2 < ix.Length; t += 3)
        {
            var (a, b, c) = (ix[t] * 3, ix[t + 1] * 3, ix[t + 2] * 3);
            v += (p[a] * (p[b + 1] * p[c + 2] - p[b + 2] * p[c + 1]) - p[a + 1] * (p[b] * p[c + 2] - p[b + 2] * p[c]) + p[a + 2] * (p[b] * p[c + 1] - p[b + 1] * p[c])) / 6.0;
        }
        return Math.Abs(v);
    }

    /// <summary>What a 3D view in a layers mode shows of an element's render: its parts' names and their meshes' volume.</summary>
    static (List<string> Names, double Volume) Shown(RenderItem item, string mode)
    {
        var kept = (item.Parts ?? []).Where(p => mode == "all" || !(p.HiddenBy ?? []).Contains(mode)).ToList();
        return (kept.Select(p => p.Name).ToList(), kept.Sum(p => MeshVolume(p.Positions, p.Indices)));
    }

    [Fact]
    public void A_wall_in_3D_keeps_brick_and_wool_without_finishes_and_the_brick_alone_as_its_core()
    {
        var (f, wall) = PlasteredWall();
        var item = Item(f, wall);
        var h = f.Bounds(wall).Max.Z - f.Bounds(wall).Min.Z;
        Assert.Equal(["Lime plaster", "Brick", "Mineral wool", "Silicate render"], Shown(item, "all").Names);
        Assert.Equal(["Brick", "Mineral wool"], Shown(item, "noFinishes").Names);
        Assert.Equal(["Brick"], Shown(item, "core").Names);
        // Each kept layer a closed solid of its own: the meshes hold exactly the layers' volumes.
        Assert.Equal(5 * h * 0.38, Shown(item, "all").Volume, 4);
        Assert.Equal(5 * h * 0.35, Shown(item, "noFinishes").Volume, 4);
        Assert.Equal(5 * h * 0.25, Shown(item, "core").Volume, 4);
        // The plaster goes in both modes, the wool with the cores only, the brick never.
        Assert.Equal(["noFinishes", "core"], item.Parts![0].HiddenBy!);
        Assert.Null(item.Parts[1].HiddenBy);
        Assert.Equal(["core"], item.Parts[2].HiddenBy!);
    }

    [Fact]
    public void A_materials_category_changed_builds_the_render_again()
    {
        var (f, wall) = PlasteredWall();
        // The render becomes structure: a 3D view without finishes keeps it now.
        var diff = f.RunDiff("material.update", new JsonObject { ["id"] = Materials.Find(f.Model, "Silicate render")!.Id, ["category"] = "Structure" });
        var item = diff.Render.Single(r => r.NodeId == wall);
        Assert.Equal(["Brick", "Mineral wool", "Silicate render"], Shown(item, "noFinishes").Names);
    }

    [Fact]
    public void A_floor_in_3D_keeps_its_screed_and_slab_without_finishes_and_the_slab_as_its_core()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Tiled floor",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Ceramic tiles", ["thickness"] = 0.02, ["core"] = false },
                new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05, ["core"] = false },
                new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = 0.15, ["core"] = true },
                new JsonObject { ["material"] = "Lime plaster", ["thickness"] = 0.015, ["core"] = false }),
        })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 4, ["typeId"] = type })!["id"]!;
        var item = Item(f, slab);
        Assert.Equal(4, item.Parts!.Count);
        Assert.Equal(24 * 0.235, Shown(item, "all").Volume, 4);
        Assert.Equal(2, Shown(item, "noFinishes").Names.Count);
        Assert.Equal(24 * 0.20, Shown(item, "noFinishes").Volume, 4);
        Assert.Single(Shown(item, "core").Names);
        Assert.Equal(24 * 0.15, Shown(item, "core").Volume, 4);
    }

    [Fact]
    public void A_3D_views_layers_come_from_its_discipline_and_its_own_choice()
    {
        var (f, _) = PlasteredWall();
        Assert.Equal("all", ViewStyles.Of(f.Model, Views.Find(f.Model, "3d")!).Layers);
        // {3D}'s discipline and choices live on the project, as its storeys shown.
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["discipline"] = "Structure" });
        Assert.Equal("Structure", (string?)Spatial.Project(f.Model)!.Attrs["view3d"]!["discipline"]);
        Assert.Null(Spatial.Project(f.Model)!.Attrs["view3d"]!["kind"]);
        Assert.Equal("core", ViewStyles.Of(f.Model, Views.Find(f.Model, "3d")!).Layers);
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["layers"] = "noFinishes" });
        Assert.Equal("noFinishes", ViewStyles.Of(f.Model, Views.Find(f.Model, "3d")!).Layers);
        var state = f.Session.Query("view.style", new JsonObject { ["id"] = "3d" })!;
        Assert.Equal("structural", (string?)state["style"]!["id"]);
        Assert.Equal(["layers"], state["overrides"]!.AsArray().Select(x => (string?)x));
        // A saved 3D view takes {3D}'s settings when saved, and keeps its own after.
        var saved = (string)f.Run("view.duplicate", new JsonObject { ["id"] = "3d" })!["id"]!;
        Assert.Equal("noFinishes", ViewStyles.Of(f.Model, f.Model.Get(saved)!).Layers);
        f.Run("view.setStyle", new JsonObject { ["id"] = saved, ["layers"] = null });
        Assert.Equal("core", ViewStyles.Of(f.Model, f.Model.Get(saved)!).Layers);
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["layers"] = null, ["discipline"] = null });
        Assert.Equal("all", ViewStyles.Of(f.Model, Views.Find(f.Model, "3d")!).Layers);
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["layers"] = "some" }));
    }

    [Fact]
    public void The_caps_close_only_the_layers_kept()
    {
        var (f, wall) = PlasteredWall();
        // A section across the wall at x = 2.5, keeping x ≤ 2.5.
        ClipPlane[] planes = [new(new Vec3(-1, 0, 0), 2.5)];
        static int[] Parts(List<CapItem> caps, string id) => caps.Single(c => c.NodeId == id).Parts.Select(p => p.Part).Order().ToArray();
        Assert.Equal([0, 1, 2, 3], Parts(f.Session.Caps(planes), wall));
        Assert.Equal([1, 2], Parts(f.Session.Caps(planes, layers: "noFinishes"), wall));
        Assert.Equal([1], Parts(f.Session.Caps(planes, layers: "core"), wall));
    }

    [Fact]
    public void Export_as_the_view_shows_it_takes_the_views_layers()
    {
        var (f, wall) = PlasteredWall();
        var h = f.Bounds(wall).Max.Z - f.Bounds(wall).Min.Z;
        double Volume(MeshScene s) => s.Groups.SelectMany(g => g.Elements).Single(e => e.GlobalId == wall).Parts.Sum(p => MeshVolume(p.Positions, p.Indices));
        int Materials(MeshScene s) => s.Groups.SelectMany(g => g.Elements).Single(e => e.GlobalId == wall).Parts.Count;
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["layers"] = "core" });
        var view = MeshScene.Build(f.Session, new MeshExportOptions(ViewId: "3d"));
        Assert.Equal(1, Materials(view));
        Assert.Equal(5 * h * 0.25, Volume(view), 4);
        Assert.Contains(view.Notes, n => n.Contains("cores only"));
        // Everything: every layer, whatever the view shows.
        var all = MeshScene.Build(f.Session, new MeshExportOptions());
        Assert.Equal(4, Materials(all));
        Assert.Equal(5 * h * 0.38, Volume(all), 4);
        // Asked for by name (the protocol's layers), over the view's.
        var o = MeshExportOptions.From(new JsonObject { ["viewId"] = "3d", ["layers"] = "noFinishes" });
        Assert.Equal("noFinishes", o.Layers);
        Assert.Equal(5 * h * 0.35, Volume(MeshScene.Build(f.Session, o)), 4);
        Assert.Null(MeshExportOptions.From(new JsonObject()).Layers);
        Assert.Throws<CommandException>(() => MeshExportOptions.From(new JsonObject { ["layers"] = "half" }));
        // Cut and capped as the view is: the core alone, closed at the cut (x ≤ 2.5).
        var cut = MeshScene.Build(f.Session, new MeshExportOptions(ViewId: "3d", Planes: [new ClipPlane(new Vec3(-1, 0, 0), 2.5)]));
        Assert.Equal(2.5 * h * 0.25, Volume(cut), 4);
    }

    [Fact]
    public void A_plans_drawing_is_not_touched_by_the_3D_views_layers()
    {
        var (f, wall) = PlasteredWall();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["layers"] = "core" });
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, plan)!);
        Assert.True(d != null, problem);
        Assert.Equal(4, d!.Cuts.Where(c => c.Element == wall).Select(c => c.Key).Distinct().Count());
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
