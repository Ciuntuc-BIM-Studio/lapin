using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Roofs, ported from BubbleBIM Pro's RoofTests.cs (same cases and numbers):
/// rules per edge solved as a weighted straight skeleton, one IfcRoof with
/// everything else derived; walls rising to gables and stopping under it.
/// </summary>
public class RoofTests
{
    static readonly Vec2[] Rect = [new(0, 0), new(10, 0), new(10, 6), new(0, 6)];
    static EdgeRule Eave(double pitch = 30, double overhang = 0) => new(EdgeKind.Eave, pitch, overhang);
    static EdgeRule Gable(double rake = 0) => new(EdgeKind.Gable, 0, rake);
    static double Tan(double deg) => Math.Tan(deg * Math.PI / 180);
    static double Len(RoofShape s, LineKind k) => s.Lines.Where(l => l.Kind == k).Sum(l => l.Length);

    static void Planar(RoofShape s)
    {
        foreach (var p in s.Planes)
        {
            var z = RoofSolver.PlaneZ(p.Points);
            Assert.All(p.Points, q => Assert.Equal(q.Z, z(q.Xy), 9));
        }
    }

    // ------------------------------------------------------------ the solver

    [Fact]
    public void A_hip_roof_on_a_rectangle()
    {
        var s = RoofSolver.Solve(Rect, [Eave(), Eave(), Eave(), Eave()]);
        Assert.Equal(4, s.Planes.Count);
        Assert.Equal(4, Len(s, LineKind.Ridge), 9);                     // 10 − 6
        Assert.Equal(4, s.Lines.Count(l => l.Kind == LineKind.Hip));
        Assert.Equal(3 * Tan(30), s.Height, 9);
        Assert.Equal(60 / Math.Cos(Math.PI / 6), s.Planes.Sum(p => VecMath.NewellNormal(p.Points).Length), 9);
        Planar(s);
    }

    [Fact]
    public void A_gable_roof_has_two_planes_and_two_vertical_ends()
    {
        var s = RoofSolver.Solve(Rect, [Eave(), Gable(), Eave(), Gable()]);
        Assert.Equal(2, s.Planes.Count);
        Assert.Equal(2, s.Gables.Count);
        Assert.Equal(10, Len(s, LineKind.Ridge), 9);
        Assert.Equal(0, s.Lines.Count(l => l.Kind is LineKind.Hip or LineKind.Valley));
        Assert.All(s.Gables, g => Assert.Equal(3 * Tan(30), g.Points.Max(p => p.Z), 9));
    }

    [Fact]
    public void Overhangs_drop_the_eaves_and_rakes_lengthen_the_ridge()
    {
        var s = RoofSolver.Solve(Rect, [Eave(30, 0.5), Gable(0.3), Eave(30, 0.5), Gable(0.3)]);
        Assert.Equal(10.6, Len(s, LineKind.Ridge), 9);
        Assert.Equal(-0.5 * Tan(30), s.Planes.Min(p => p.Points.Min(q => q.Z)), 9);
        Assert.Equal(3 * Tan(30), s.Height, 9);
        Assert.Equal(10.6 * 7, s.Planes.Sum(p => Math.Abs(Polygon2.SignedArea(p.Points.Select(q => q.Xy).ToList()))), 9);
    }

    [Fact]
    public void Different_pitches_meet_on_true_plane_intersections()
    {
        var s = RoofSolver.Solve(Rect, [Eave(45, 0.4), Eave(30, 0.6), Eave(45, 0.4), Eave(30, 0.6)]);
        Assert.Equal(4, s.Planes.Count);
        Planar(s);
        foreach (var hip in s.Lines.Where(l => l.Kind == LineKind.Hip))
        {
            var owners = s.Planes.Where(p => p.Points.Any(q => (q - hip.A).Length < 1e-6) && p.Points.Any(q => (q - hip.B).Length < 1e-6)).ToList();
            Assert.Equal(2, owners.Count);
        }
        Assert.Equal(5 * Tan(30), s.Height, 9);
    }

    [Fact]
    public void A_shed_roof_rises_from_its_low_edge()
    {
        var kinds = Roof.Kinds("shed", Rect);
        Assert.Equal([EdgeKind.Eave, EdgeKind.Gable, EdgeKind.Gable, EdgeKind.Gable], kinds);
        var s = RoofSolver.Solve(Rect, kinds.Select(k => k == EdgeKind.Eave ? Eave(10) : Gable()).ToList());
        Assert.Single(s.Planes);
        Assert.Equal(6 * Tan(10), s.Height, 9);
    }

    [Fact]
    public void An_L_gets_gables_at_its_arm_ends_and_a_valley_inside()
    {
        Vec2[] l = [new(0, 0), new(12, 0), new(12, 4), new(4, 4), new(4, 10), new(0, 10)];
        var kinds = Roof.Kinds("gable", l);
        Assert.Equal([EdgeKind.Eave, EdgeKind.Gable, EdgeKind.Eave, EdgeKind.Eave, EdgeKind.Gable, EdgeKind.Eave], kinds);
        var s = RoofSolver.Solve(l, kinds.Select(k => k == EdgeKind.Eave ? Eave(35, 0.5) : Gable(0.3)).ToList());
        Assert.Single(s.Lines, x => x.Kind == LineKind.Valley);
        Assert.Single(s.Lines, x => x.Kind == LineKind.Hip);
        Planar(s);
        var hip = RoofSolver.Solve(l, l.Select(_ => Eave(35)).ToList());
        Assert.Single(hip.Lines, x => x.Kind == LineKind.Valley);
        Assert.Equal(6, hip.Planes.Count);
    }

    [Fact]
    public void A_mansard_is_a_steep_roof_to_the_break_and_a_low_one_over_it()
    {
        Vec2[] rect = [new(0, 0), new(10, 0), new(10, 8), new(0, 8)];
        var hb = 1.0 * Tan(70);
        var s = RoofSolver.Solve(rect, rect.Select(_ => Eave(70)).ToList(), new Mansard(25, hb));
        Assert.Equal(8, s.Planes.Count);
        Assert.Equal(2 * (8 + 6), Len(s, LineKind.Break), 6);
        Assert.Equal(2, Len(s, LineKind.Ridge), 6);
        Assert.Equal(hb + 3 * Tan(25), s.Height, 6);
        Planar(s);
        var g = RoofSolver.Solve(rect, [Eave(60), Gable(), Eave(60), Gable()], new Mansard(30, 1.2 * Tan(60)));
        Assert.Equal(4, g.Planes.Count);
        Assert.Equal(20, Len(g, LineKind.Break), 6);
        Assert.Equal(10, Len(g, LineKind.Ridge), 6);
        Assert.Equal(4, g.Gables.Count);
        Assert.Throws<ArgumentException>(() => RoofSolver.Solve(rect, rect.Select(_ => Eave(70)).ToList(), new Mansard(25, 5 * Tan(70))));
    }

    // ------------------------------------------------------------ the IfcRoof

    static JsonArray Corners(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());
    static JsonArray House10x6 => Corners((0, 0), (10, 0), (10, 6), (0, 6));

    static string AddRoof(Fixture f, string style) =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style })!["id"]!;

    static double Q(Fixture f, string id, string name, string? part = null) =>
        f.Session.Quantities(id).Where(s => s.Part == part).SelectMany(s => s.Quantities).Where(q => q.Name == name).Sum(q => q.Value);

    [Fact]
    public void Another_style_keeps_the_roof_windows_and_dormers()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_barn");
        f.Run("architecture.addRoofOpening", new JsonObject { ["x"] = 3, ["y"] = 1.5 });
        var dormer = (string)f.Run("architecture.addRoofOpening", new JsonObject { ["x"] = 7, ["y"] = 0.6, ["kind"] = "dormer", ["width"] = 1.4, ["wallHeight"] = 0.9 })!["id"]!;
        // The dormer would not sit on the hip roof's slope: refused by name, nothing changed.
        var refused = Assert.Throws<CommandException>(() => f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = "traditional_campie" }));
        Assert.Contains("Dormer 1", refused.Message);
        Assert.Equal("gable", f.Model.Get(roof)!.Str("shape"));
        Assert.Equal(2, (f.Model.Get(roof)!.Attrs["openings"] as JsonArray)?.Count);
        // Without it, the new style keeps the roof window.
        f.Run("architecture.removeRoofOpening", new JsonObject { ["roofId"] = roof, ["id"] = dormer });
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = "traditional_campie" });
        Assert.Equal("hip", f.Model.Get(roof)!.Str("shape"));
        Assert.Equal(1, (f.Model.Get(roof)!.Attrs["openings"] as JsonArray)?.Count);
        Assert.Equal(1, Q(f, roof, "RoofWindows", "openings"));
    }

    [Fact]
    public void A_roof_is_one_IfcRoof_over_its_outline_and_its_style_decides_the_rest()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_barn");
        Assert.Single(f.Model.OfClass(IfcClass.Roof));
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, roof)!.Id);
        Assert.Equal("gable", f.Model.Get(roof)!.Str("shape"));
        // Barn house: 42°, no overhangs, a ridge the length of the house.
        Assert.Equal(10, Q(f, roof, "RidgeLength"), 9);
        Assert.Equal(60 / Math.Cos(42 * Math.PI / 180), Q(f, roof, "GrossArea"), 6);
        Assert.Equal(20, Q(f, roof, "EaveLength"), 9);
        // Another style: the same roof, other rules — hip, clay tile, 600 mm eaves, snow guards.
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = "traditional_campie" });
        Assert.Equal(4, Q(f, roof, "RidgeLength"), 9);
        Assert.Equal("True", f.Model.Get(roof)!.Str("snowGuards"));
        // One edge overridden: a gable on the east side.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["edges"] = new JsonArray(null, new JsonObject { ["kind"] = "gable" }) });
        Assert.Null(f.Model.Get(roof)!.Str("style"));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = roof, ["edges"] = new JsonArray(Enumerable.Range(0, 4).Select(_ => (JsonNode)new JsonObject { ["kind"] = "gable" }).ToArray()),
        }));
        var shape = Roof.Solve(Roof.Resolve(f.Model.Get(roof)!, f.Model)!).Shape!;
        Assert.Equal(Len(shape, LineKind.Ridge), Q(f, roof, "RidgeLength"), 9);
        Assert.Single(shape.Gables);
        // The plan shows where it stops (overhead) and its ridges and hips.
        var plan = f.Session.Geometry(roof)!.Plan!;
        Assert.Contains(plan, p => p.Kind == "overhead");
        Assert.Contains(plan, p => p.Kind == "ridge");
    }

    [Fact]
    public void A_roof_carries_its_structure_gutters_downpipes_and_snow_guards_as_parts()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "traditional_campie");                      // hip 28°, 600 mm eaves, clay tile
        var entities = f.Model.Entities.Count();
        Assert.Equal(32, Q(f, roof, "WallPlateLength", "timber"), 6);
        Assert.Equal(4, Q(f, roof, "RidgeBeamLength", "timber"), 6);
        Assert.True(Q(f, roof, "RafterCount", "timber") >= 40);
        Assert.True(Q(f, roof, "TimberVolume", "timber") > 1);
        // Gutters round the eave outline (11.2 × 7.2 m); a downpipe per 10 m of gutter.
        Assert.Equal(2 * (11.2 + 7.2), Q(f, roof, "GutterLength", "drainage"), 6);
        Assert.Equal(6, Q(f, roof, "DownpipeCount", "drainage"), 9);
        Assert.True(Q(f, roof, "Length", "snow") > 20);
        Assert.Equal(["Covering", "Timber structure", "Gutters and downpipes", "Snow guards"], f.Session.Geometry(roof)!.Parts.Select(p => p.Name));
        Assert.Equal(entities, f.Model.Entities.Count());

        // A gable roof: plates only under its eaves; no snow guards, no gutters asked.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["shape"] = "gable", ["snowGuards"] = "False", ["gutterProfile"] = "none" } });
        Assert.Equal(20, Q(f, roof, "WallPlateLength", "timber"), 6);
        Assert.Equal(0, Q(f, roof, "GutterLength", "drainage"), 9);
        Assert.Equal(0, Q(f, roof, "Length", "snow"), 9);
        // No structure: a deck under the covering, nothing to count.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["structure"] = "none" } });
        Assert.Equal(0, Q(f, roof, "TimberVolume", "timber"), 9);
    }

    [Fact]
    public void The_french_mansard_style_builds_one()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "classic_mansard");
        Assert.Equal("mansard", f.Model.Get(roof)!.Str("shape"));
        Assert.True(Q(f, roof, "BreakLength") > 20);
    }

    [Fact]
    public void An_outline_that_cannot_build_is_refused_and_undo_takes_the_roof_away()
    {
        var f = new Fixture();
        Assert.Throws<CommandException>(() => f.Run("architecture.addRoof", new JsonObject { ["points"] = Corners((0, 0), (1, 0)) }));
        var roof = AddRoof(f, "fr_provence");
        f.Session.Undo();
        Assert.Null(f.Model.Get(roof));
    }

    // ------------------------------------------------------------ walls under the roof

    /// <summary>A 10 × 6 m house: four walls on the axes, south, east, north, west, and a roof over the same corners.</summary>
    static (Fixture F, string Roof, string[] Walls) House(string style)
    {
        var f = new Fixture();
        string[] walls = [f.Wall(0, 0, 10, 0), f.Wall(10, 0, 10, 6), f.Wall(10, 6, 0, 6), f.Wall(0, 6, 0, 0)];
        return (f, AddRoof(f, style), walls);
    }

    [Fact]
    public void Gable_walls_rise_to_the_roof_and_eave_walls_stop_under_the_wall_plate()
    {
        var (f, roof, walls) = House("fr_provence");                     // gable, 24°
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["ridgeDirection"] = "x" } });
        var tops = walls.Select(w => f.Bounds(w).Max.Z).ToArray();
        Assert.Equal(3, tops[0], 9);
        Assert.Equal(3, tops[2], 9);
        // On the wall plates (0.14 m) the rafters' underside rises 3 · tan 24° to the ridge.
        Assert.Equal(3 + 0.14 + 3 * Tan(24), tops[1], 6);
        Assert.Equal(3 + 0.14 + 3 * Tan(24), tops[3], 6);
        // The gable is the wall's own masonry over the storey: the wall grows by about the triangle.
        var east = f.Session.Geometry(walls[1])!;
        var width = f.Bounds(walls[1]).Max.X - f.Bounds(walls[1]).Min.X;
        var volume = east.Parts.Sum(p => Measure.Volume(p.Solid));
        Assert.True(volume > 6 * width * 3 + 0.5 * 6 * 3 * Tan(24) * width * 0.9);
        // The roof goes: the walls are back to their height.
        f.Run("element.delete", new JsonObject { ["id"] = roof });
        Assert.Equal(3, f.Bounds(walls[1]).Max.Z, 9);
    }

    [Fact]
    public void A_roof_seated_low_cuts_the_walls_under_its_slopes()
    {
        var (f, roof, walls) = House("traditional_campie");              // hip, 28°
        var inner = f.Wall(5, 0, 5, 6);
        Assert.Equal(3, f.Bounds(inner).Max.Z, 9);
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["eaveOffset"] = -0.5 } });
        // An eave wall on the roof's outline: it stops under its wall plate, 0.5 m lower.
        Assert.Equal(2.5, f.Bounds(walls[0]).Max.Z, 6);
        // The inner wall: cut under both slopes near the eaves, its full height where the roof is above it.
        Assert.Equal(3, f.Bounds(inner).Max.Z, 6);
        var section = f.Session.Geometry(inner)!.Parts.Select(p => p.Solid).ToList();
        var nearEnd = section.SelectMany(s => s.Vertices).Where(v => v.Y < 0.3).Max(v => v.Z);
        Assert.True(nearEnd < 2.8, $"near the eave the wall stops under the roof, not at {nearEnd}");
    }

    [Fact]
    public void A_wall_attached_to_the_roof_rises_to_the_slopes_underside_and_a_detached_one_ignores_it()
    {
        var (f, roof, walls) = House("traditional_campie");              // hip, 28°
        // An inner wall across the house, 2.4 m (joined to the south and north walls, so it runs face to face): left alone it keeps its height under the high roof.
        var inner = f.Wall(5, 0.5, 5, 5.5);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = inner, ["height"] = 2.4 });
        Assert.Equal(2.4, f.Bounds(inner).Max.Z, 6);
        // Attached, it rises to the underside of the slopes: higher in the middle, cut along them.
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(inner) });
        var top = f.Bounds(inner).Max.Z;
        Assert.True(top > 3.5, $"attached, the wall reaches the slopes, not {top}");
        // Near the south eave it follows the slope down: above its own 2.4 m, well under the middle.
        var near = f.Session.Geometry(inner)!.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.Y < 0.3).Max(v => v.Z);
        Assert.InRange(near, 2.4, top - 1);
        // Along the wall the top rises by tan 28° a metre, as the slope does.
        var at1 = f.Session.Geometry(inner)!.Parts.SelectMany(p => p.Solid.Vertices).Where(v => Math.Abs(v.Y - 1.5) < 1e-6).Select(v => v.Z).DefaultIfEmpty(double.NaN).Max();
        if (!double.IsNaN(at1)) Assert.Equal(near + Tan(28), at1, 2);
        // The roof raised 0.5 m: the attached wall follows (it read the roof).
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["eaveOffset"] = 0.5 } });
        Assert.Equal(top + 0.5, f.Bounds(inner).Max.Z, 6);
        // Detached: its own 2.4 m, whatever the roof; back to auto: the storey's rule again.
        f.Run("architecture.detachFromRoof", new JsonObject { ["ids"] = new JsonArray(inner) });
        Assert.Equal("none", f.Model.Get(inner)!.Str("roofTop"));
        Assert.Equal(2.4, f.Bounds(inner).Max.Z, 6);
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["eaveOffset"] = -1.0 } });
        Assert.Equal(2.4, f.Bounds(inner).Max.Z, 6);
        Assert.True(f.Bounds(walls[0]).Max.Z < 2.4, "an eave wall in auto still stops under its lowered plate");
        f.Run("architecture.detachFromRoof", new JsonObject { ["ids"] = new JsonArray(inner), ["mode"] = "auto" });
        var cut = f.Session.Geometry(inner)!.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.Y < 0.3).Max(v => v.Z);
        Assert.True(cut < 2.4, $"auto again: cut under the lowered slope near the eave, not {cut}");
    }

    [Fact]
    public void A_wall_on_the_storey_below_attaches_to_the_roof_above()
    {
        var f = new Fixture();
        var attic = (string)f.Run("storey.create", new JsonObject { ["name"] = "Attic" })!["id"]!;   // +3.00
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = attic, ["points"] = House10x6, ["style"] = "traditional_campie" })!["id"]!;
        // A ground-floor wall, 3 m: the attic's roof is not its storey's, so in auto it is left alone.
        var wall = f.Wall(5, 0.5, 5, 5.5);
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);
        // Attached to that roof by name: it rises through the attic to the slopes.
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(roof) });
        Assert.True(f.Bounds(wall).Max.Z > 6.2);
        Assert.Throws<CommandException>(() => f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(wall) }));
    }

    [Fact]
    public void Roof_windows_and_dormers_cut_the_covering_and_are_built_with_the_roof()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_barn");
        var net = Q(f, roof, "NetArea");
        var gross = Q(f, roof, "GrossArea");
        Assert.Equal(gross, net, 6);
        // A 78 × 118 roof window on the south slope: its area on the slope comes off the covering.
        var w = (string)f.Run("architecture.addRoofOpening", new JsonObject { ["x"] = 3, ["y"] = 1.5 })!["id"]!;
        Assert.Equal(gross - 0.78 * 1.18, Q(f, roof, "NetArea"), 6);
        var parts = f.Session.Geometry(roof)!.Parts.Select(p => p.Key).ToList();
        Assert.Contains("roof-windows", parts);
        Assert.Contains("glass", parts);
        Assert.Equal(1, Q(f, roof, "RoofWindows", "openings"));
        // A gable dormer beside it: its walls, its own roof, its notch out of the covering.
        f.Run("architecture.addRoofOpening", new JsonObject { ["x"] = 7, ["y"] = 0.6, ["kind"] = "dormer", ["width"] = 1.4, ["wallHeight"] = 0.9 });
        parts = f.Session.Geometry(roof)!.Parts.Select(p => p.Key).ToList();
        Assert.Contains("dormer-walls", parts);
        Assert.Contains("dormer-roofs", parts);
        Assert.True(Q(f, roof, "DormerWallArea", "openings") > 1);
        Assert.True(Q(f, roof, "NetArea") < gross - 0.78 * 1.18 - 1);
        // Off the roof, over another opening, or past its edges: refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.addRoofOpening", new JsonObject { ["roofId"] = roof, ["x"] = 30, ["y"] = 30 }));
        Assert.Throws<CommandException>(() => f.Run("architecture.addRoofOpening", new JsonObject { ["roofId"] = roof, ["x"] = 3.2, ["y"] = 1.6 }));
        Assert.Throws<CommandException>(() => f.Run("architecture.addRoofOpening", new JsonObject { ["roofId"] = roof, ["x"] = 0.1, ["y"] = 1.5 }));
        f.Run("architecture.removeRoofOpening", new JsonObject { ["roofId"] = roof, ["id"] = w });
        Assert.Equal(0, Q(f, roof, "RoofWindows", "openings"));
    }

    // ------------------------------------------------------------ a barrel vault

    [Fact]
    public void A_barrel_roof_is_a_vault_between_two_gable_ends()
    {
        // A 6 m span rising 1.5 m: a circle of R = (3² + 1.5²) / 3 = 3.75, its centre 2.25 m under the wall plates.
        EdgeRule[] rules = [Eave(), Gable(), Eave(), Gable()];
        var s = RoofSolver.SolveBarrel(Rect, rules, 1.5);
        Planar(s);
        Assert.Equal(1.5, s.Height, 9);
        Assert.Equal(2, s.Gables.Count);
        Assert.Equal(20, Len(s, LineKind.Eave), 9);
        Assert.Equal(10, Len(s, LineKind.Ridge), 9);
        Assert.Equal(1.5, s.Lines.Single(l => l.Kind == LineKind.Ridge).A.Z, 9);
        Assert.Equal(0, s.HeightAt(new Vec2(5, 0))!.Value, 6);
        Assert.Equal(1.5, s.HeightAt(new Vec2(5, 3))!.Value, 9);
        // Between, the vault in facets: a chord within a centimetre of the circle, never above it.
        var arc = -2.25 + Math.Sqrt(3.75 * 3.75 - 1.5 * 1.5);
        Assert.InRange(s.HeightAt(new Vec2(5, 1.5))!.Value, arc - 0.01, arc + 1e-9);
        Assert.All(s.Planes, p => Assert.Equal(4, p.Points.Count));
        Assert.True(s.Planes.Count >= 12);
        Assert.Equal(0, Len(s, LineKind.Hip) + Len(s, LineKind.Break), 9);
        // Overhanging eaves go on round the circle, under the plate; the rakes lengthen the ridge.
        var o = RoofSolver.SolveBarrel(Rect, [Eave(30, 0.5), Gable(0.3), Eave(30, 0.5), Gable(0.3)], 1.5);
        Assert.Equal(10.6, Len(o, LineKind.Ridge), 9);
        var eaves = o.Lines.Where(l => l.Kind == LineKind.Eave).ToList();
        Assert.Equal(2, eaves.Count);
        Assert.All(eaves, l => Assert.True(l.A.Z < -0.5 && (Math.Abs(l.A.Y + 0.5) < 1e-9 || Math.Abs(l.A.Y - 6.5) < 1e-9)));
        // Higher than half the span it is no vault; an L has no two parallel eaves to span between.
        Assert.Throws<ArgumentException>(() => RoofSolver.SolveBarrel(Rect, rules, 3.2));
        Assert.Throws<ArgumentException>(() => RoofSolver.SolveBarrel([new(0, 0), new(10, 0), new(10, 6), new(5, 6), new(5, 3), new(0, 3)], [Eave(), Gable(), Eave(), Gable(), Eave(), Gable()], 1));
    }

    [Fact]
    public void A_barrel_roof_over_the_house_and_its_gable_walls_rise_to_the_curve()
    {
        var (f, roof, walls) = House("modern_barrel");                  // a vault across the 6 m, rising 0.3 · 6 = 1.8 m
        Assert.Equal("barrel", f.Model.Get(roof)!.Str("shape"));
        Assert.Equal(1.8, f.Model.Get(roof)!.Num("rise"), 9);
        // The eave walls keep their 3 m; the gable walls rise to the crown of the vault (a deck on the walls, no wall plate).
        Assert.Equal(3, f.Bounds(walls[0]).Max.Z, 6);
        Assert.Equal(3, f.Bounds(walls[2]).Max.Z, 6);
        Assert.Equal(4.8, f.Bounds(walls[1]).Max.Z, 3);
        Assert.Equal(4.8, f.Bounds(walls[3]).Max.Z, 3);
        // The gable's top follows the circle: low near the eaves, and over the storey about the circular segment's worth of masonry.
        var east = f.Session.Geometry(walls[1])!;
        Assert.True(east.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.Y < 0.3).Max(v => v.Z) < 3.5);
        var width = f.Bounds(walls[1]).Max.X - f.Bounds(walls[1]).Min.X;
        var R = (9 + 1.8 * 1.8) / 3.6;
        var segment = R * R / 2 * (2 * Math.Asin(3 / R) - Math.Sin(2 * Math.Asin(3 / R)));
        Assert.InRange(east.Parts.Sum(p => Measure.Volume(p.Solid)), width * (6 * 3 + segment * 0.9), width * (6.5 * 3 + segment * 1.02));
        // One ridge in plan and no hips; the covering's area is the vault's, over the rakes; a deck, so no timber; gutters on both eaves.
        var g = f.Session.Geometry(roof)!;
        Assert.Contains(g.Plan!, p => p.Kind == "ridge");
        Assert.DoesNotContain(g.Plan!, p => p.Kind is "hip" or "break");
        // (the 0.15 m overhangs go on round the circle, so the covering is the arc over 3.15 m each side of the crown)
        Assert.InRange(Q(f, roof, "GrossArea"), 2 * R * Math.Asin(3.15 / R) * 10.6 * 0.995, 2 * R * Math.Asin(3.15 / R) * 10.6);
        Assert.DoesNotContain("timber", g.Parts.Select(p => p.Key));
        Assert.Contains("drainage", g.Parts.Select(p => p.Key));
        Assert.Contains("covering", g.Parts.Select(p => p.Key));
        // Its rise is a field of its own; past half the span the change is refused.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["rise"] = 3 } });
        Assert.Equal(6, f.Bounds(walls[1]).Max.Z, 3);
        Assert.Throws<CommandException>(() => f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["rise"] = 3.5 } }));
    }

    // ------------------------------------------------------------ a wall under any body

    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void A_wall_attached_to_any_body_rises_to_its_underside()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var upper = (string)f.Run("storey.create", [])!["id"]!;            // +3.00
        var top = (string)f.Run("storey.create", [])!["id"]!;              // +6.00
        string Plan(string storey) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey })!["id"]!;
        string Draw(string view, double x0, double x1) => (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = view,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(new JsonArray(x0, 0), new JsonArray(x1, 0), new JsonArray(x1, 1), new JsonArray(x0, 1)), ["closed"] = true },
        })!["ids"]![0]!;
        // A Studio loft from a 2 × 1 square at +3.00 to the same square 8 m on at +6.00: its underside climbs 3 m over 8.
        var a = Draw(Plan(upper), 0, 2);
        var b = Draw(Plan(top), 8, 10);
        var body = (string)f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b) })!["id"]!;
        // A ground-floor wall along it, 3 m: attached to the body it rises under the slope, to 6 m where the body ends, and is its own 3 m past it.
        var wall = f.Wall(-1, 0.5, 12, 0.5);
        var plain = Vol(f, wall);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(body) });
        Assert.Equal(6, f.Bounds(wall).Max.Z, 2);
        var vs = f.Session.Geometry(wall)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Equal(3, vs.Where(v => v.X > 10.05).Max(v => v.Z), 2);
        Assert.Equal(3, vs.Where(v => v.X < 1.95).Max(v => v.Z), 2);
        // The slope's triangle, 8 · 3 / 2, on top of the plain wall's 13 · 3.
        Assert.Equal(plain * (39 + 12) / 39, Vol(f, wall), 1);
        // The sketch moved: the body changed, the wall follows (its underside now climbs from 2 to 8).
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = b, ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(new JsonArray(6, 0), new JsonArray(8, 0), new JsonArray(8, 1), new JsonArray(6, 1)), ["closed"] = true } });
        vs = f.Session.Geometry(wall)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Equal(3, vs.Where(v => v.X > 8.05).Max(v => v.Z), 2);
        Assert.Equal(6, f.Bounds(wall).Max.Z, 2);
        // Detached, its own 3 m again; a wall is no body to rise to.
        f.Run("architecture.detachFromRoof", new JsonObject { ["ids"] = new JsonArray(wall) });
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);
        Assert.Throws<CommandException>(() => f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(f.Wall(0, 5, 5, 5)) }));
    }
}
