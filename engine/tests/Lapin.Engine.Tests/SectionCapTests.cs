using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Host;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>A section seen in 3D closes what it cuts: each part of a cut element gets its caps, in its own material.</summary>
public class SectionCapTests
{
    static double Area(CapPart c)
    {
        double a = 0;
        for (var i = 0; i + 2 < c.Indices.Length; i += 3)
        {
            Vec3 P(int k) => new(c.Positions[3 * k], c.Positions[3 * k + 1], c.Positions[3 * k + 2]);
            a += Vec3.Cross(P(c.Indices[i + 1]) - P(c.Indices[i]), P(c.Indices[i + 2]) - P(c.Indices[i])).Length / 2;
        }
        return a;
    }

    [Fact]
    public void A_cut_layered_wall_is_capped_layer_by_layer_in_each_layers_material()
    {
        var f = new Fixture();
        var type = f.Type("Brick and plaster", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.02, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        var g = f.Session.Geometry(wall)!;
        var (min, max) = f.Bounds(wall);
        // Across the wall at x = 2.5, the half towards the start kept.
        var caps = f.Session.Caps([ClipPlane.Through(new Vec3(2.5, 0, 0), new Vec3(-1, 0, 0))], lift: 0);
        var item = Assert.Single(caps, c => c.NodeId == wall);
        Assert.Equal(g.Parts.Count, item.Parts.Count);
        foreach (var c in item.Parts) Assert.Equal(g.Parts[c.Part].Material, c.Material);
        Assert.Equal((max.Y - min.Y) * (max.Z - min.Z), item.Parts.Sum(Area), 4);
    }

    [Fact]
    public void What_the_planes_do_not_cross_has_no_caps()
    {
        var f = new Fixture();
        var near = f.Wall(0, 0, 5, 0);
        var far = f.Wall(20, 0, 25, 0);
        var caps = f.Session.Caps([ClipPlane.Through(new Vec3(2.5, 0, 0), new Vec3(-1, 0, 0))]);
        Assert.Contains(caps, c => c.NodeId == near);
        Assert.DoesNotContain(caps, c => c.NodeId == far);
    }

    [Fact]
    public async Task The_caps_come_over_the_wire_as_buffers_and_a_guest_may_ask_for_them()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var hub = new EngineHub(f.Session, PackageFiles.Create());
        var (header, buffers) = BinaryFrame.Decode((await hub.Handle("""{"id":1,"method":"view.caps","params":{"planes":[{"normal":[-1,0,0],"constant":2.5}]}}"""))!);
        var item = header.RootElement.GetProperty("result").EnumerateArray().Single();
        Assert.Equal(wall, item.GetProperty("nodeId").GetString());
        var part = item.GetProperty("parts").EnumerateArray().First();
        Assert.NotEmpty(BinaryFrame.Ints(part.GetProperty("indices"), buffers));
        Assert.Null(EngineDispatcher.AllowedRemote("view.caps", new JsonObject(), "viewer"));
    }

    [Fact]
    public void Drawn_as_a_sheet_a_cap_takes_its_materials_section_fill_and_hatch()
    {
        var f = new Fixture();
        var type = f.Type("Brick and plaster", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.02, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        var item = f.Session.Caps([ClipPlane.Through(new Vec3(2.5, 0, 0), new Vec3(-1, 0, 0))], drawing: true).Single(c => c.NodeId == wall);
        Assert.All(item.Parts, p => Assert.NotNull(p.Fill));
        // The brick core is hatched (its name says brick), its segments on the cap's plane, a hair towards the viewer.
        var core = item.Parts.Single(p => f.Model.Get(p.Material!)!.Name == "Brick");
        Assert.NotEmpty(core.Hatch!);
        for (var i = 0; i < core.Hatch!.Length; i += 3) Assert.Equal(2.5 - 0.0005 + 0.0002, core.Hatch[i], 4);
        // In the materials' look, none of that.
        Assert.All(f.Session.Caps([ClipPlane.Through(new Vec3(2.5, 0, 0), new Vec3(-1, 0, 0))]).Single(c => c.NodeId == wall).Parts, p => Assert.Null(p.Hatch));
    }

    [Fact]
    public void A_saved_3D_view_keeps_its_section_box()
    {
        var f = new Fixture();
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "3d", ["name"] = "Cut" })!["id"]!;
        f.Run("view.setSectionBox", new JsonObject { ["id"] = view, ["box"] = new JsonObject { ["min"] = new JsonArray(0, 0, 0), ["max"] = new JsonArray(5, 4, 1.2), ["rot"] = 0.5 } });
        var box = f.Model.Get(view)!.Attrs["sectionBox"]!.AsObject();
        Assert.Equal(1.2, (double)box["max"]![2]!, 9);
        Assert.Equal(0.5, (double)box["rot"]!, 9);
        f.Session.Undo();
        Assert.Null(f.Model.Get(view)!.Attrs["sectionBox"]);
        f.Run("view.setSectionBox", new JsonObject { ["id"] = view, ["box"] = new JsonObject { ["min"] = new JsonArray(0, 0, 0), ["max"] = new JsonArray(5, 4, 1.2) } });
        f.Run("view.setSectionBox", new JsonObject { ["id"] = view, ["box"] = null });
        Assert.Null(f.Model.Get(view)!.Attrs["sectionBox"]);
    }

    [Fact]
    public void On_a_level_cut_the_hatch_runs_along_the_layer_however_the_wall_is_turned()
    {
        // A brick wall turned 30°, cut at 1.2 m: its courses (the pattern's lines) run the wall's way.
        var f = new Fixture();
        var type = f.Type("Brick", ("Brick", 0.25, true));
        double c = Math.Cos(Math.PI / 6), s = Math.Sin(Math.PI / 6);
        var wall = f.Wall(0, 0, 6 * c, 6 * s, type);
        var core = f.Session.Caps([ClipPlane.Through(new Vec3(0, 0, 1.2), new Vec3(0, 0, -1))], drawing: true).Single(x => x.NodeId == wall).Parts.Single();
        var h = core.Hatch!;
        Assert.NotEmpty(h);
        // The longest hatch segments are the courses: parallel to the wall.
        var longest = Enumerable.Range(0, h.Length / 6).Select(k => (dx: h[6 * k + 3] - h[6 * k], dy: h[6 * k + 4] - h[6 * k + 1])).OrderByDescending(d => d.dx * d.dx + d.dy * d.dy).First();
        var len = Math.Sqrt(longest.dx * longest.dx + longest.dy * longest.dy);
        Assert.Equal(1, Math.Abs(longest.dx / len * c + longest.dy / len * s), 3);
    }
}
