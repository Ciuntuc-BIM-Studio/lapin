using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A finish wrapped round a wall's free ends (in-app report #192, decision 862): a wall type's, or a wall's own, <c>wrapEnds</c>
/// says which face's outer layer turns each free end (<c>first</c>: layer 1's, the R face; <c>last</c>: layer n's, the L face;
/// <c>none</c>). That layer then caps the end from its own face to the other one, and every other layer stops its thickness short,
/// so the end shows the finish, not the core; the wall's overall outline does not move. Joined ends are the joins'. Without
/// the setting (every wall made before, every IFC import) the wall builds exactly as it did: each layer's own end face.
/// </summary>
public sealed class WallEndWrapTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-wrap-ends").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    const double Length = 5, Height = 3, Plaster = 0.015, Brick = 0.25, Width = Plaster + Brick + Plaster;

    static JsonObject L(string material, double thickness, bool core = false) => new() { ["material"] = material, ["thickness"] = thickness, ["core"] = core };

    static string WallType(Fixture f, string? wrap = null)
    {
        var a = new JsonObject { ["name"] = "Plastered brick", ["layers"] = new JsonArray(L("Plaster", Plaster), L("Brick", Brick, true), L("Plaster", Plaster)) };
        if (wrap != null) a["wrapEnds"] = wrap;
        return (string)f.Run("architecture.createWallType", a)!["id"]!;
    }

    static List<GeometryPart> Parts(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.Where(p => p.Key == key).ToList();
    static double Volume(Fixture f, string id, string key) => Parts(f, id, key).Sum(p => Measure.Volume(p.Solid));
    static double All(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static List<Vec3> Vertices(Fixture f, string id, string key) => Parts(f, id, key).SelectMany(p => p.Solid.Vertices).ToList();
    static double Measured(Fixture f, string id, string key) => f.Session.Quantities(id).Single(q => q.Part == key).Quantities.Single(x => x.Name == "NetVolume").Value;

    [Fact]
    public void Without_the_setting_a_free_wall_shows_each_layers_end_as_before()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, Length, 0, WallType(f), height: Height);
        Assert.Null(f.Model.Get(wall)!.Attrs[WallEndWraps.Key]);
        foreach (var key in new[] { "layer:0", "layer:1", "layer:2" })
        {
            Assert.Equal(0, Vertices(f, wall, key).Min(v => v.X), 6);
            Assert.Equal(Length, Vertices(f, wall, key).Max(v => v.X), 6);
        }
        Assert.Equal(Length * Height * Plaster, Volume(f, wall, "layer:0"), 6);
        Assert.Equal(Length * Height * Brick, Volume(f, wall, "layer:1"), 6);
    }

    [Fact]
    public void The_first_faces_plaster_caps_both_free_ends_and_the_rest_stops_short()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, Length, 0, WallType(f, "first"), height: Height);
        // The brick and the other plaster stop 15 mm short of each end.
        foreach (var key in new[] { "layer:1", "layer:2" })
        {
            Assert.Equal(Plaster, Vertices(f, wall, key).Min(v => v.X), 6);
            Assert.Equal(Length - Plaster, Vertices(f, wall, key).Max(v => v.X), 6);
        }
        Assert.Equal((Length - 2 * Plaster) * Height * Brick, Volume(f, wall, "layer:1"), 6);
        Assert.Equal((Length - 2 * Plaster) * Height * Plaster, Volume(f, wall, "layer:2"), 6);
        // The first plaster runs its face and turns each end across the whole wall, one body.
        var plaster = Parts(f, wall, "layer:0");
        Assert.Single(plaster);
        var v = Vertices(f, wall, "layer:0");
        Assert.Equal(0, v.Min(p => p.X), 6);
        Assert.Equal(Length, v.Max(p => p.X), 6);
        Assert.Equal(Width, v.Max(p => p.Y) - v.Min(p => p.Y), 6);
        var capped = Length * Height * Plaster + 2 * (Brick + Plaster) * Plaster * Height;
        Assert.Equal(capped, Volume(f, wall, "layer:0"), 6);
        Assert.Equal(capped, Measured(f, wall, "layer:0"), 6);
        // The wall's outline and volume as before.
        Assert.Equal(Length * Height * Width, All(f, wall), 6);
        // In plan the plaster's cut turns the ends too: one outline across the whole width.
        var cut = f.Session.Geometry(wall)!.Plan!.Where(p => p.Kind == "cut").ToList();
        Assert.Contains(cut, p => p.Points.Max(q => q.Y) - p.Points.Min(q => q.Y) > Width - 1e-6 && p.Points.Max(q => q.X) > Length - 1e-6);
    }

    [Fact]
    public void A_joined_end_is_the_joins_and_only_the_free_one_is_wrapped()
    {
        var f = new Fixture();
        var type = WallType(f, "first");
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        f.Wall(Length, 0, Length, 4, type, height: Height);
        Assert.NotEmpty(Joins.Of(f.Model, a));
        // At the free start the brick stops 15 mm in; at the corner it goes on as the join says (past 5 - 0.015).
        var brick = Vertices(f, a, "layer:1");
        Assert.Equal(Plaster, brick.Min(p => p.X), 6);
        Assert.True(brick.Max(p => p.X) > Length - 1e-6);
    }

    [Fact]
    public void A_walls_own_setting_wins_over_its_types_and_each_change_is_one_undo_step()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, Length, 0, WallType(f, "first"), height: Height);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["wrapEnds"] = "none" });
        Assert.Equal(0, Vertices(f, wall, "layer:1").Min(p => p.X), 6);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["wrapEnds"] = "last" });
        Assert.Equal(Plaster, Vertices(f, wall, "layer:0").Min(p => p.X), 6);
        Assert.Equal(0, Vertices(f, wall, "layer:2").Min(p => p.X), 6);
        Assert.Equal(Length * Height * Plaster + 2 * (Brick + Plaster) * Plaster * Height, Volume(f, wall, "layer:2"), 6);
        f.Session.Undo();
        Assert.Equal("none", f.Model.Get(wall)!.Str(WallEndWraps.Key));
        // Null gives the place back to the type.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["wrapEnds"] = null });
        Assert.Null(f.Model.Get(wall)!.Attrs[WallEndWraps.Key]);
        Assert.Equal(Plaster, Vertices(f, wall, "layer:1").Min(p => p.X), 6);
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["wrapEnds"] = "sideways" }));
    }

    [Fact]
    public void A_types_setting_changed_rebuilds_its_walls_and_a_flipped_wall_wraps_the_same_layer()
    {
        var f = new Fixture();
        var type = WallType(f);
        var wall = f.Wall(0, 0, Length, 0, type, height: Height);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["wrapEnds"] = "first" });
        Assert.Equal("first", f.Model.Get(type)!.Str(WallEndWraps.Key));
        Assert.Equal(Plaster, Vertices(f, wall, "layer:1").Min(p => p.X), 6);
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(wall) });
        // Layer 1 (now on the left face) still turns the ends.
        Assert.Equal(0, Vertices(f, wall, "layer:0").Min(p => p.X), 6);
        Assert.Equal(Plaster, Vertices(f, wall, "layer:2").Min(p => p.X), 6);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["wrapEnds"] = null });
        Assert.Null(f.Model.Get(type)!.Attrs[WallEndWraps.Key]);
        Assert.Equal(0, Vertices(f, wall, "layer:1").Min(p => p.X), 6);
    }

    /// <summary>
    /// A lower wall (a parapet, 1 m) ending on the wrapped wall's face (forum topic 268's gaps, report #192's wrap): its core
    /// goes through the wrapping plaster only up to its own top, the free ends stay wrapped, the plan's cut above it sees the
    /// plaster whole and still turned round the ends.
    /// </summary>
    [Fact]
    public void A_lower_wall_meeting_the_wrapped_face_cuts_the_wrapping_finish_only_up_to_its_top_and_the_ends_stay_wrapped()
    {
        var f = new Fixture();
        var type = WallType(f, "first");
        var wall = f.Wall(0, 0, Length, 0, type, height: Height);
        // Layer 1 is the R face, right of the drawn direction: y < 0 here.
        var low = f.Wall(2.5, -3, 2.5, 0, type, height: 1);
        Assert.NotEmpty(Joins.Of(f.Model, low));
        foreach (var key in new[] { "layer:1", "layer:2" })
        {
            Assert.Equal(Plaster, Vertices(f, wall, key).Min(v => v.X), 6);
            Assert.Equal(Length - Plaster, Vertices(f, wall, key).Max(v => v.X), 6);
        }
        var v0 = Vertices(f, wall, "layer:0");
        Assert.Equal(0, v0.Min(p => p.X), 6);
        Assert.Equal(Length, v0.Max(p => p.X), 6);
        Assert.Equal(Width, v0.Max(p => p.Y) - v0.Min(p => p.Y), 6);
        Assert.Equal(Height, v0.Max(p => p.Z) - v0.Min(p => p.Z), 6);
        // The lower wall's brick goes through the plaster under its top: the plaster less a gap of brick × plaster × 1 m.
        var capped = Length * Height * Plaster + 2 * (Brick + Plaster) * Plaster * Height;
        Assert.Equal(capped - Brick * Plaster * 1, Volume(f, wall, "layer:0"), 6);
        Assert.Equal(Volume(f, wall, "layer:0"), Measured(f, wall, "layer:0"), 6);
        // The other layers as wrapped, untouched by the lower wall.
        Assert.Equal((Length - 2 * Plaster) * Height * Brick, Volume(f, wall, "layer:1"), 6);
        // The lower wall's brick reaches the wrapped wall's brick, its plaster stops at the plaster.
        Assert.Equal(-Brick / 2, Vertices(f, low, "layer:1").Max(p => p.Y), 6);
        // The plan's cut (above the lower wall): the plaster one outline across the whole width, round both ends.
        var cut = f.Session.Geometry(wall)!.Plan!.Where(p => p.Kind == "cut").ToList();
        Assert.Contains(cut, p => p.Points.Max(q => q.Y) - p.Points.Min(q => q.Y) > Width - 1e-6 && p.Points.Min(q => q.X) < 1e-6 && p.Points.Max(q => q.X) > Length - 1e-6);
    }

    /// <summary>
    /// A lower wall at the wrapped wall's end, turning a corner: the end is the join's (not wrapped), the other end still is,
    /// and the lower wall's top does not change what the join makes of the end.
    /// </summary>
    [Fact]
    public void A_lower_wall_at_a_wrapped_end_takes_that_end_and_the_free_one_stays_wrapped()
    {
        var f = new Fixture();
        var type = WallType(f, "first");
        var wall = f.Wall(0, 0, Length, 0, type, height: Height);
        var tall = new Fixture();
        var reference = tall.Wall(0, 0, Length, 0, WallType(tall, "first"), height: Height);
        tall.Wall(Length, 0, Length, -4, tall.Model.OfClass(IfcClass.WallType).Single().Id, height: Height);
        f.Wall(Length, 0, Length, -4, type, height: 1);
        Assert.NotEmpty(Joins.Of(f.Model, wall));
        var brick = Vertices(f, wall, "layer:1");
        Assert.Equal(Plaster, brick.Min(p => p.X), 6);
        // The joined end as a storey-high wall's corner makes it.
        Assert.Equal(Vertices(tall, reference, "layer:1").Max(p => p.X), brick.Max(p => p.X), 6);
        Assert.Equal(Vertices(tall, reference, "layer:0").Max(p => p.X), Vertices(f, wall, "layer:0").Max(p => p.X), 6);
    }

    [Fact]
    public void A_core_on_the_face_wraps_nothing_and_IFC_keeps_the_setting_without_adding_it_to_imports()
    {
        var f = new Fixture();
        var bare = (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Fair-faced", ["layers"] = new JsonArray(L("Brick", Brick, true), L("Plaster", Plaster)), ["wrapEnds"] = "first" })!["id"]!;
        var wall = f.Wall(0, 0, Length, 0, bare, height: Height);
        Assert.Equal(0, Vertices(f, wall, "layer:1").Min(p => p.X), 6);
        Assert.Equal(Length * Height * Brick, Volume(f, wall, "layer:0"), 6);
        // A Lapin IFC file carries the type's setting (Lapin_Intent) and brings it back.
        var g = new Fixture();
        var w = g.Wall(0, 0, Length, 0, WallType(g, "first"), height: Height);
        var ifc = Path.Combine(_dir, "wrap.ifc");
        IfcExport.Write(g.Session, ifc, null);
        Assert.Contains("'wrapEnds'", File.ReadAllText(ifc));
        var b = new Fixture();
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass(IfcClass.Wall).Single();
        Assert.Equal(All(g, w), b.Session.Geometry(back.Id)!.Parts.Sum(p => Measure.Volume(p.Solid)), 4);
    }
}
