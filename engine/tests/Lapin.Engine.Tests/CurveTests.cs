using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>Curved walls and beams: an axis that is an arc through its ends, rising off the chord.</summary>
public class CurveTests
{
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void An_arc_through_its_ends_is_measured_and_walked_along()
    {
        // A half circle of radius 2 over the chord (0,0)–(4,0), bulging to the left (+Y).
        var c = new PathCurve(new Vec2(0, 0), new Vec2(4, 0), 2);
        Assert.Equal(2, c.Radius, 9);
        Assert.Equal(Math.PI * 2, c.Length, 9);
        Assert.Equal(new Vec2(2, 0), c.Centre);
        var top = c.PointAt(c.Length / 2);
        Assert.Equal(2, top.X, 9);
        Assert.Equal(2, top.Y, 9);
        // It turns clockwise: up at its start, down at its end; to its left is outward here.
        Assert.Equal(1, c.TangentAt(0).Y, 9);
        Assert.Equal(-1, c.TangentAt(c.Length).Y, 9);
        Assert.Equal(2.5, c.At(c.Length / 2, 0.5).Y, 9);
        // Past its ends it goes on straight along its tangents.
        Assert.Equal(-1, c.PointAt(-1).Y, 9);
        Assert.Equal(Math.PI, c.Project(new Vec2(2, 5)), 9);
        Assert.Equal(-0.5, c.Project(new Vec2(0, -0.5)), 9);
        Assert.Equal(0.5, c.Distance(new Vec2(2, 2.5)), 9);
        // The arc through a third point: here, the top.
        Assert.Equal(2, PathCurve.RiseThrough(new Vec2(0, 0), new Vec2(4, 0), new Vec2(2, 2)), 9);
        Assert.Equal(-0.5, PathCurve.RiseThrough(new Vec2(0, 0), new Vec2(4, 0), new Vec2(2, -0.5)), 9);
        Assert.Equal(0, PathCurve.RiseThrough(new Vec2(0, 0), new Vec2(4, 0), new Vec2(7, 0)), 9);
    }

    [Fact]
    public void A_curved_wall_is_its_layers_along_the_arc_and_measures_its_arc()
    {
        var f = new Fixture();
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["x3"] = 2, ["y3"] = 2, ["height"] = 3 })!["id"]!;
        Assert.Equal(2, f.Model.Get(wall)!.Num(PathCurve.Key), 9);
        var g = f.Session.Geometry(wall)!;
        Assert.Null(g.Diagnostics);
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        var t = bands.Sum(b => b.To - b.From);
        // A ring of the wall's thickness along a half circle of R = 2: π (R + mid) t h, drawn in 5° steps (within 0.5 %).
        var mid = (bands.Min(b => b.From) + bands.Max(b => b.To)) / 2;
        var exact = Math.PI * (2 + mid) * t * 3;
        Assert.InRange(Volume(f, wall), exact * 0.995, exact * 1.0001);
        // Its left face (outward here) at the top of the arc.
        Assert.Equal(2 + bands.Max(b => b.To), f.Bounds(wall).Max.Y, 3);
        var q = f.Session.Quantities(wall).First(x => x.Name == "Qto_WallBaseQuantities");
        Assert.Equal(Math.PI * 2, q.Quantities.Single(x => x.Name == "Length").Value, 9);
        // Straightened again: its rise goes.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["arc"] = 0 });
        Assert.Null(f.Model.Get(wall)!.NumOrNull(PathCurve.Key));
        Assert.Equal(4 * t * 3, Volume(f, wall), 6);
    }

    [Fact]
    public void A_door_in_a_curved_wall_stands_on_its_tangent_and_cuts_it_radially()
    {
        var f = new Fixture();
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0, ["arc"] = 4, ["height"] = 3 })!["id"]!;
        var before = Volume(f, wall);
        // Clicked at the top of the arc: half way along it.
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["x"] = 4, ["y"] = 4.1 })!["id"]!;
        var opening = Openings.OpeningOf(f.Model, door)!;
        Assert.Equal(Math.PI * 2, opening.Num("along"), 6);
        var spec = Openings.SpecOf(f.Model, door)!;
        var t = Walls.Section(f.Model, f.Model.Get(wall)!).Sum(b => b.To - b.From);
        Assert.InRange(before - Volume(f, wall), spec.Width * spec.Height * t * 0.98, spec.Width * spec.Height * t * 1.02);
        // The door's body runs along the tangent there (along X at the top), centred on the axis.
        var b = f.Bounds(door);
        Assert.Equal(4, (b.Min.X + b.Max.X) / 2, 3);
        Assert.InRange((b.Min.Y + b.Max.Y) / 2, 3.9, 4.1);
        // Moved along the arc past its end, it is refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.moveOpening", new JsonObject { ["id"] = opening.Id, ["along"] = 4 * Math.PI }));
    }

    [Fact]
    public void A_curved_wall_joins_a_straight_one_by_its_tangent_at_the_corner()
    {
        var f = new Fixture();
        var arc = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["arc"] = 2 })!["id"]!;
        // At (4, 0) the arc runs down (−Y): a wall going on along +X makes a square corner with it.
        var straight = f.Wall(4, 0, 8, 0);
        var join = Assert.Single(f.Model.Relations, r => r.Class == Joins.Class);
        Assert.True(join.Touches(arc) && join.Touches(straight));
        Assert.Equal("L", (string?)join.Attrs?["kind"]);
        // The arc's ends stay where they were drawn; both build cleanly.
        var e = f.Model.Get(arc)!;
        Assert.Equal(new Vec2(0, 0), Walls.Start(e));
        Assert.Equal(4, Walls.End(e).X, 9);
        Assert.Null(f.Session.Geometry(arc)!.Diagnostics);
        Assert.Null(f.Session.Geometry(straight)!.Diagnostics);
        // The corner is closed: the arc runs through, on along its tangent (down) to the straight wall's far face (its right one)…
        var straightRight = Walls.Section(f.Model, f.Model.Get(straight)!).Min(b => b.From);
        var near = f.Session.Geometry(arc)!.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.X > 3.5).Min(v => v.Y);
        Assert.Equal(straightRight, near, 6);
        // …and the straight wall stops at the arc's face towards it (its left one, outward here).
        var outer = Walls.Section(f.Model, e).Max(b => b.To);
        Assert.Equal(4 + outer, f.Bounds(straight).Min.X, 6);
    }

    [Fact]
    public void A_mirrored_curved_wall_bulges_the_other_way()
    {
        var f = new Fixture();
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["arc"] = 1 })!["id"]!;
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wall), ["op"] = "mirror", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(1, 0) });
        Assert.Equal(-1, f.Model.Get(wall)!.Num(PathCurve.Key), 9);
        Assert.True(f.Bounds(wall).Min.Y < -1);
    }

    [Fact]
    public void A_curved_beam_and_a_curved_steel_profile_follow_their_arc_and_a_beam_flips()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["arc"] = 2 })!["id"]!;
        // 250 × 400 along a half circle of radius 2.
        var exact = Math.PI * 2 * 0.25 * 0.4;
        Assert.InRange(Volume(f, beam), exact * 0.995, exact * 1.0001);
        Assert.Equal(Math.PI * 2, f.Session.Quantities(beam)[0].Quantities.Single(x => x.Name == "Length").Value, 9);
        var ipe = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = "IPE 300" })!["id"]!;
        var steel = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ipe, ["x1"] = 0, ["y1"] = 10, ["x2"] = 6, ["y2"] = 10, ["x3"] = 3, ["y3"] = 11 })!["id"]!;
        var length = PathCurve.Of(f.Model.Get(steel)!).Length;
        var area = Section.Catalogue["IPE 300"].Area;
        Assert.InRange(Volume(f, steel), area * length * 0.99, area * length * 1.01);
        Assert.Equal(3, f.Bounds(steel).Max.Z, 6);
        // A channel flipped faces the other way across its axis.
        var upn = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "U", ["depth"] = 0.2, ["width"] = 0.075, ["web"] = 0.0085, ["flange"] = 0.0115 } })!["id"]!;
        var channel = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = upn, ["x1"] = 0, ["y1"] = 20, ["x2"] = 5, ["y2"] = 20 })!["id"]!;
        var centre = (f.Bounds(channel).Min.Y + f.Bounds(channel).Max.Y) / 2;
        var mass = f.Session.Geometry(channel)!.Parts[0].Solid.Vertices.Average(v => v.Y);
        f.Run("structure.flip", new JsonObject { ["id"] = channel });
        Assert.True(f.Model.Get(channel)!.Bool("flipped"));
        var flipped = f.Session.Geometry(channel)!.Parts[0].Solid.Vertices.Average(v => v.Y);
        Assert.Equal(centre - (mass - centre), flipped, 6);
    }

    [Fact]
    public void A_window_in_a_curved_wall_has_its_lintel_along_the_arc()
    {
        var f = new Fixture();
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0, ["arc"] = 3 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["x"] = 4, ["y"] = 3.1 })!["id"]!;
        var g = f.Session.Geometry(wall)!;
        Assert.Null(g.Diagnostics);
        var lintels = g.Parts.Where(p => p.Key!.StartsWith("lintel:")).ToList();
        Assert.NotEmpty(lintels);
        // Over the window, wider than it, centred on the top of the arc.
        var spec = Openings.SpecOf(f.Model, window)!;
        var all = lintels.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        var (x0, x1) = (all.Min(b => b.Min.X), all.Max(b => b.Max.X));
        Assert.True(x1 - x0 > spec.Width, $"the lintel spans {x1 - x0} over a {spec.Width} window");
        Assert.Equal(4, (x0 + x1) / 2, 2);
        var sill = Openings.OpeningOf(f.Model, window)!.Num("sill");
        Assert.Equal(sill + spec.Height, all.Min(b => b.Min.Z), 6);
        Assert.Contains(f.Session.Quantities(wall), q => q.Name == "Qto_BeamBaseQuantities");
    }

    [Fact]
    public void A_curved_wall_attached_to_a_roof_rises_to_its_slopes_along_the_arc()
    {
        var f = new Fixture();
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)), ["style"] = "traditional_campie" });
        // An arc inside the house, from near the south eave up towards the ridge and back.
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 3, ["y1"] = 0.8, ["x2"] = 7, ["y2"] = 0.8, ["arc"] = 2, ["height"] = 2.4 })!["id"]!;
        Assert.Equal(2.4, f.Bounds(wall).Max.Z, 6);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall) });
        var g = f.Session.Geometry(wall)!;
        Assert.Null(g.Diagnostics);
        var top = f.Bounds(wall).Max.Z;
        Assert.True(top > 3.5, $"attached, the arc reaches the slopes at its crown, not {top}");
        // Near its ends (close to the eave) it is lower: the top follows the slope along the arc.
        var nearEnds = g.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.Y < 1.0).Max(v => v.Z);
        Assert.InRange(nearEnds, 2.4, top - 0.5);
    }

    [Fact]
    public void A_curved_strip_foundation_follows_its_arc_and_joins_a_straight_one()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var arc = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["arc"] = 3 })!["id"]!;
        var straight = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 6, ["y1"] = 0, ["x2"] = 6, ["y2"] = -4 })!["id"]!;
        Assert.Single(f.Model.Relations, r => r.Class == Joins.Class && r.Touches(arc) && r.Touches(straight));
        var g = f.Session.Geometry(arc)!;
        Assert.Null(g.Diagnostics);
        Assert.Equal(Math.PI * 3, f.Session.Quantities(arc)[0].Quantities.Single(x => x.Name == "Length").Value, 9);
        // The footing 600 × 400 under the stem, along the half circle (its end runs on along its tangent into the corner).
        var footing = Measure.Volume(g.Parts.Single(p => p.Key == "footing").Solid);
        Assert.InRange(footing, Math.PI * 3 * 0.6 * 0.4 * 0.99, Math.PI * 3 * 0.6 * 0.4 * 1.1);
        Assert.Equal(3, f.Bounds(arc).Max.Y - 0.3, 1);
    }
}
