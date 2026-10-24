using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Platform;
using Lapin.Architecture;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>Lintels. Ported from the old Lapin's wall-lintels.test.ts, same cases and numbers.</summary>
public class LintelTests
{
    static readonly IReadOnlyList<ZoneBand> Bands = Zones3m();   // the ring beam from 2.75
    static readonly (double X0, double X1, double Height) Wall = (0, 8, 3);
    static readonly LintelRule Precast = new("precast", 0.25, 0.25);
    static readonly LintelRule Cast = Precast with { Mode = "cast" };

    static IReadOnlyList<ZoneBand> Zones3m()
    {
        var zones = WallZones.Defaults("Brick").Select(z => new WallZone((string)z["key"]!, (string)z["name"]!, (string)z["role"]!,
            z["height"] is JsonValue v && v.TryGetValue<double>(out var h) ? h : null, "m")).ToList();
        return WallZones.Bands(zones, 3);
    }

    static LintelOpening O(string key, double x0, double x1, double z0, double z1, LintelRule? rule = null, bool none = false) => new(key, x0, x1, z0, z1, rule, none);

    [Fact]
    public void Takes_the_shortest_catalogue_length_over_the_opening_and_both_bearings()
    {
        Assert.Equal(1.5, Lintels.PrecastLength(1.4), 9);
        Assert.Equal(1.5, Lintels.PrecastLength(1.5), 9);
        Assert.Equal(4.25, Lintels.PrecastLength(4.1), 9);
        var l = Assert.Single(Lintels.ForWall([O("w", 2, 2.9, 0.9, 2.1)], Bands, Precast, Wall));
        // 0.9 + 2 × 0.25 = 1.4 → 1.5, centred over the window.
        Assert.Equal(("lintel", true), (l.Kind, l.Precast));
        Assert.Equal(1.5, l.Length, 9);
        Assert.Equal(2.1, l.Z0, 9);
        Assert.Equal(2.35, l.Z1, 9);
        Assert.Equal(1.7, l.X0, 9);
        Assert.Equal(3.2, l.X1, 9);
        // Cast in place: exactly the opening and its bearings.
        var c = Lintels.ForWall([O("w", 2, 2.9, 0.9, 2.1)], Bands, Cast, Wall)[0];
        Assert.Equal(1.75, c.X0, 9);
        Assert.Equal(3.15, c.X1, 9);
        Assert.False(c.Precast);
    }

    [Fact]
    public void Shares_one_lintel_over_openings_whose_bearings_touch_at_the_same_head_height()
    {
        var o = Lintels.ForWall([O("a", 1, 1.9, 0.9, 2.1), O("b", 2.3, 3.2, 0.9, 2.1), O("c", 5, 5.9, 0, 2.1)], Bands, Cast, Wall);
        Assert.Equal(["a+b", "c"], o.Select(l => l.Key));
        Assert.Equal(0.75, o[0].X0, 9);
        Assert.Equal(3.45, o[0].X1, 9);
    }

    [Fact]
    public void Lets_the_ring_beam_come_down_over_an_opening_close_under_it_and_keeps_lintels_inside_the_wall()
    {
        var down = Assert.Single(Lintels.ForWall([O("d", 1, 2, 0, 2.6)], Bands, Precast, Wall));
        Assert.Equal(("downstand", false), (down.Kind, down.Precast));
        Assert.Equal(2.6, down.Z0, 9);
        Assert.Equal(2.75, down.Z1, 9);
        // An opening up to the ring beam needs nothing.
        Assert.Empty(Lintels.ForWall([O("e", 1, 2, 0, 2.75)], Bands, Precast, Wall));
        var edge = Lintels.ForWall([O("f", 0.1, 1, 0, 2.1)], Bands, Cast, Wall)[0];
        Assert.Equal(0, edge.X0);
    }

    [Fact]
    public void A_window_types_own_rule_wins_over_the_walls_and_a_type_with_none_goes_without()
    {
        var o = Lintels.ForWall([O("own", 1, 1.9, 0.9, 2.1, Cast with { Bearing = 0.15 }), O("none", 3, 3.9, 0.9, 2.1, none: true), O("wall", 5, 5.9, 0.9, 2.1)], Bands, Precast, Wall);
        Assert.Equal([("own", false), ("wall", true)], o.Select(l => (l.Key, l.Precast)));
        Assert.Equal(0.85, o[0].X0, 9);
        Assert.Equal(2.05, o[0].X1, 9);
        // A wall with no rule still gets the lintels its windows' types ask for.
        Assert.Single(Lintels.ForWall([O("own", 1, 1.9, 0.9, 2.1, Cast)], Bands, null, Wall));
    }

    [Fact]
    public void A_precast_product_its_height_and_its_shortest_length_that_covers_the_span_marked_when_none_does()
    {
        var product = new LintelProduct("P11", 0.115, 0.19, [1.25, 1.5, 2]);
        var rule = new LintelRule("precast", 0.2, 0.25, Product: product);
        var l = Lintels.ForWall([O("w", 2, 3, 0.9, 2.1)], Bands, rule, Wall)[0];
        Assert.Equal(1.5, l.Length, 9);
        Assert.Equal(2.29, l.Z1, 9);
        Assert.True(l.Fits);
        var longer = Lintels.ForWall([O("w", 2, 4, 0.9, 2.1)], Bands, rule, Wall)[0];
        Assert.False(longer.Fits);
        Assert.Equal(2.4, longer.Length, 9);
    }

    [Fact]
    public void Bearings_can_differ_by_side()
    {
        var l = Lintels.ForWall([O("w", 2, 3, 0.9, 2.1)], Bands, Cast with { Bearing = 0.1, BearingEnd = 0.4 }, Wall)[0];
        Assert.Equal(1.9, l.X0, 9);
        Assert.Equal(3.4, l.X1, 9);
    }

    [Fact]
    public void Across_the_wall_the_core_or_a_width_at_the_outer_face_centred_or_inner_in_pieces()
    {
        Assert.Equal([(0.0, 0.3)], Lintels.Slices(null, 0, 0.3, true));
        Assert.Equal(0, Lintels.Slices(Cast with { Width = 0.2, Placement = "outer" }, 0, 0.3, true)[0].From, 9);
        Assert.Equal(0.3, Lintels.Slices(Cast with { Width = 0.2, Placement = "inner" }, 0, 0.3, true)[0].To, 9);
        Assert.Equal(0.3, Lintels.Slices(Cast with { Width = 0.2, Placement = "outer" }, 0, 0.3, false)[0].To, 9);
        var two = Lintels.Slices(new LintelRule("precast", 0.2, 0.19, Product: new LintelProduct("P", 0.115, 0.19, [1]), Pieces: 2), 0, 0.3, true);
        Assert.Equal(2, two.Count);
        Assert.Equal(0.23, two[1].To - two[0].From, 9);
    }

    [Fact]
    public void Reinforcing_steel_of_a_cast_lintel_kg()
    {
        var r = new LintelReinforcement(Bottom: new LintelBars(2, 0.012), Top: new LintelBars(2, 0.01), Stirrups: new LintelStirrups(0.006, 0.15), Cover: 0.025);
        var kg = Lintels.ReinforcementMass(r, 1.5, 0.25, 0.25);
        // Bars: (2 × 0.000113 + 2 × 0.0000785) × 1.5 m × 7850 ≈ 4.5 kg; 11 stirrups of 0.8 m, Ø6 ≈ 2.0 kg.
        Assert.InRange(kg, 6, 7.2);
        Assert.Equal(0, Lintels.ReinforcementMass(null, 1.5, 0.25, 0.25));
    }

    [Fact]
    public void A_manufacturer_s_lintels_come_from_a_CSV_into_the_project_s_catalogue()
    {
        var (products, skipped) = LintelProducts.Parse("code;width;height;lengths;manufacturer\nL1;115;71;1000|1250|1500;Porotherm\nL2,175,71,1000,1250,Wienerberger\nbad line\n# a note\n");
        Assert.Equal(["L1", "L2"], products.Select(p => p.Code));
        Assert.Equal([1.0, 1.25, 1.5], products[0].Lengths);
        Assert.Equal(0.115, products[0].Width, 9);
        Assert.Equal("Wienerberger", products[1].Manufacturer);
        Assert.Equal([1.0, 1.25], products[1].Lengths);
        Assert.Equal(["line 4"], skipped);
        var f = new Fixture();
        f.Run("architecture.importLintelProducts", new System.Text.Json.Nodes.JsonObject { ["text"] = "L1;115;71;1000|1250;Porotherm" });
        f.Run("architecture.importLintelProducts", new System.Text.Json.Nodes.JsonObject { ["text"] = "L1;115;71;1500;Porotherm\nL3;250;71;2000;X" });
        var catalogue = LintelProducts.Of(f.Model);
        Assert.Equal(["L1", "L3"], catalogue.Select(p => p.Code).Order());
        Assert.Equal([1.5], catalogue.Single(p => p.Code == "L1").Lengths);
    }

    [Fact]
    public void The_lintels_of_two_walls_that_meet_at_a_corner_join_the_longer_runs_through()
    {
        // The old Lapin's wall-zones.e2e case: windows close to the corner, both lintels bear into it.
        var f = new Fixture();
        var a = f.Wall(0, 0, 5, 0);
        var b = f.Wall(0, 0, 0, 5);
        string Type(string name, double width) => (string)f.Run("architecture.createJoineryType", new JsonObject
        {
            ["spec"] = (JoinerySpec.DefaultWindow(name) with { Width = width, Height = 1.2, Lintel = new LintelRule("cast", 0.4, 0.2) }).ToJson(),
        })!["id"]!;
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = a, ["along"] = 0.9, ["typeId"] = Type("W1", 1.2), ["sill"] = 0.9 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = b, ["along"] = 0.8, ["typeId"] = Type("W2", 0.9), ["sill"] = 0.9 });
        GeometryPart L(string wall) => Assert.Single(f.Session.Geometry(wall)!.Parts, p => p.Key!.StartsWith("lintel:"));
        var (la, lb) = (Measure.Bounds(L(a).Solid)!.Value, Measure.Bounds(L(b).Solid)!.Value);
        // A's lintel is the longer (2.0 against 1.7): it runs through the corner to the far face of B's core…
        var t = Walls.DefaultThickness;
        Assert.Equal(-t / 2, la.Min.X, 6);
        // …and B's stops at A's lintel.
        Assert.Equal(la.Max.Y, lb.Min.Y, 6);
        var booleans = new BubbleBim.Platform.Booleans.ManifoldBooleans();
        Assert.True(booleans.Intersection(L(a).Solid, L(b).Solid) is not { } both || Measure.Volume(both) < 1e-9);
        // Neither core holds what a lintel does (the brick, layer 1 of the default type).
        foreach (var (wall, other) in new[] { (a, b), (b, a) })
        {
            var core = f.Session.Geometry(wall)!.Parts.Single(p => p.Key == "layer:1").Solid;
            Assert.True(booleans.Intersection(core, L(other).Solid) is not { } x || Measure.Volume(x) < 1e-9);
            Assert.True(booleans.Intersection(core, L(wall).Solid) is not { } y || Measure.Volume(y) < 1e-9);
        }
    }
}
