using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What stands on a roof's curved edges (decision 741): the roof of the tutorial film (a 10 × 7 m house, its front eave
/// bulging 1.5 m out, the corner at the back right filleted at 1.5 m). The solver gives each chord a plane; the gutters,
/// downpipes, plates, purlins, rafters and hips follow the curve, not the chords.
/// </summary>
public class RoofCurvedEdgeTests
{
    static (Fixture F, string Id) FilmRoof(JsonObject? attrs = null)
    {
        var f = new Fixture(new ArchitectureModule());
        var id = (string)f.Run("architecture.addRoof", new JsonObject
        {
            ["points"] = new JsonArray(new JsonArray(-0.125, -0.125), new JsonArray(10.125, -0.125), new JsonArray(10.125, 7.125), new JsonArray(-0.125, 7.125)),
            ["arcs"] = new JsonArray(-1.5, 0, 0, 0),
        })!["id"]!;
        f.Run("architecture.roundCorner", new JsonObject { ["id"] = id, ["index"] = 2, ["radius"] = 1.5 });
        if (attrs != null) f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = attrs });
        return (f, id);
    }

    static (Roof.Resolved R, RoofShape S) Solved(Fixture f, string id)
    {
        var r = Roof.Resolve(f.Model.Get(id)!, f.Model)!;
        return (r, Roof.Solve(r).Shape!);
    }

    [Fact]
    public void A_curved_eave_takes_downpipes_by_its_length_not_one_per_chord()
    {
        var (f, id) = FilmRoof();
        var (r, s) = Solved(f, id);
        var gutters = RoofParts.Gutters(r, s);
        Assert.True(gutters.Count > 40);   // the chords' gutters, as built
        // Before: one downpipe per chord, 65. The runs: the bulge (11.0 m: 2, one at each end), the right side round the
        // fillet to the back (5.75 + 2.36 + 8.75 m: 2, one at each end), the left side (7.25 m: 1, at its end). Where a
        // curved run starts at a corner whose other gutter already has its outlet there, the two share it: 3 downpipes,
        // one at each corner the water runs to.
        var runs = Drainage.Runs(gutters, r);
        Assert.Equal(3, runs.Count);
        var outlets = Drainage.Outlets(f.Model, r, gutters);
        Assert.Equal(3, outlets.Count);
        var geom = f.Session.Geometry(id)!;
        Assert.Equal(3, geom.Parts.Count(p => p.Key!.StartsWith("downpipe:")));
        Assert.Equal(3, geom.Plan!.Count(p => p.Kind == "symbol"));
        // Each run is one gutter part, not one per chord.
        Assert.Equal(3, geom.Parts.Count(p => p.Key!.StartsWith("gutter:")));
        // Every run drains at both its ends (its own outlet or the one at the corner it shares), no two outlets at one corner.
        Vec2 At(int gi, double st) { var g = gutters[gi]; return (g.A + (g.B - g.A) * (st / g.Length)).Xy; }
        var points = outlets.Select(o => At(o.Gutter, o.Station)).ToList();
        foreach (var run in runs)
            foreach (var end in new[] { gutters[run[0]].A.Xy, gutters[run[^1]].B.Xy })
                Assert.Contains(points, p => (p - end).Length < 1);
        for (var i = 0; i < points.Count; i++)
            for (var j = i + 1; j < points.Count; j++) Assert.True((points[i] - points[j]).Length > 1);
    }

    [Fact]
    public void A_curved_gutter_falls_to_its_runs_outlets_without_a_step_at_its_chords()
    {
        var (f, id) = FilmRoof(new JsonObject { ["gutterFall"] = 5 });
        var (r, s) = Solved(f, id);
        var gutters = RoofParts.Gutters(r, s);
        var spec = Drainage.Spec.Of(f.Model.Get(id)!);
        var places = Drainage.RunPlaces(gutters, r, Drainage.Outlets(f.Model, r, gutters).Select(x => (x.Gutter, x.Station)));
        var falls = 0;
        foreach (var run in Drainage.Runs(gutters, r))
            for (var k = 0; k + 1 < run.Count; k++)
            {
                var (a, b) = (gutters[run[k]], gutters[run[k + 1]]);
                var end = Drainage.OutletAt(a, a.Length, places[run[k]], spec);
                var start = Drainage.OutletAt(b, 0, places[run[k + 1]], spec);
                Assert.Equal(end.Z, start.Z, 9);
                if (Math.Abs(Drainage.OutletAt(a, 0, places[run[k]], spec).Z - end.Z) > 1e-6) falls++;
            }
        // Chords with no outlet of their own still fall (before, each chord was level unless it had an outlet).
        Assert.True(falls > 20);
    }

    [Fact]
    public void The_creases_between_a_curves_facets_are_not_hips()
    {
        var (f, id) = FilmRoof();
        var (r, s) = Solved(f, id);
        // Before: 107 hip lines (one between every two chords' planes) and 104 hip rafters.
        Assert.True(s.Lines.Count(l => l.Kind == LineKind.Hip) < 50);
        var members = RoofParts.Framing(r, s);
        // The two corners where the bulge meets its sides, the hip off the fillet's apex, the curved hips where the
        // bulge's facets meet the sides (one member each), and the long hip at the back left: 5.
        Assert.Equal(5, members.Count(m => m.Role == "hip"));
        // A hip between two neighbouring chords of one curve is gone; the plan has no line between them either.
        var geom = f.Session.Geometry(id)!;
        Assert.True(geom.Plan!.Count(p => p.Kind == "hip") < 50);
    }

    [Fact]
    public void Rafters_stand_at_the_spacing_along_a_curve_and_round_a_fillet()
    {
        var (f, id) = FilmRoof();
        var (r, s) = Solved(f, id);
        var rafters = RoofParts.Framing(r, s).Where(m => m.Role == "rafter").ToList();
        // Before: two per chord, 155 (the fillet's 22 chords alone gave 44, crowding its apex).
        Assert.InRange(rafters.Count, 40, 52);
        // Away from the sharp corners (where jack rafters from both slopes meet their hip, as on any hip roof).
        Vec2[] sharp = [new(-0.125, -0.125), new(10.125, -0.125), new(-0.125, 7.125)];
        var feet = rafters.Select(m => (m.A.Z < m.B.Z ? m.A : m.B).Xy).Where(p => sharp.All(c => (p - c).Length > 1)).ToList();
        Assert.True(feet.Count > 30);
        var spacing = 0.8;
        for (var i = 0; i < feet.Count; i++)
            for (var j = i + 1; j < feet.Count; j++)
                Assert.True((feet[i] - feet[j]).Length > spacing / 2, $"rafters {i} and {j} stand {(feet[i] - feet[j]).Length:0.000} m apart");
        // Round the fillet (corner [10.125, 7.125] rounded at 1.5 m: its arc from [10.125, 5.625] to [8.625, 7.125]) they are
        // spread along the arc, a spacing apart, not crowded at its apex.
        var round = feet.Where(p => p.X > 8.6 && p.Y > 5.6 && (p - new Vec2(8.625, 5.625)).Length > 1.5).ToList();
        Assert.InRange(round.Count, 2, 4);
    }

    [Fact]
    public void A_curved_wall_plate_and_the_purlins_over_it_are_one_member_each()
    {
        var (f, id) = FilmRoof();
        var (r, s) = Solved(f, id);
        var members = RoofParts.Framing(r, s);
        // Before: 65 plates and 65 purlin pieces, one per chord.
        var plates = members.Where(m => m.Role == "plate").ToList();
        Assert.Equal(5, plates.Count);
        var roof = f.Model.Get(id)!;
        var corners = Roof.Contour(roof);
        var rises = Slabs.Rises(roof);
        // The bulge's plate runs the whole arc, the fillet's plate its quarter circle: as long as their chords.
        var bulge = plates.MaxBy(m => m.Length)!;
        Assert.NotNull(bulge.Path);
        var chordsLength = Enumerable.Range(0, r.Outline.Count).Where(k => r.LogicalEdge(k) == 0).Sum(k => (r.Outline[(k + 1) % r.Outline.Count] - r.Outline[k]).Length);
        Assert.Equal(chordsLength, bulge.Length, 6);
        Assert.True(ArcOutline.Edge(corners, rises, 0).IsArc);
        Assert.Single(plates, m => Math.Abs(m.Length - Math.PI * 1.5 / 2) < 0.01);
        Assert.NotNull(bulge.Body("plate"));
        // Each purlin row over the bulge one member, level all along.
        var purlins = members.Where(m => m.Role == "purlin").ToList();
        Assert.InRange(purlins.Count, 4, 6);
        Assert.All(purlins.Where(m => m.Path != null), m => Assert.All(m.Path!, p => Assert.Equal(m.A.Z, p.Z, 9)));
        Assert.Contains(purlins, m => m.Path != null);
        // The parts in 3D: one per member.
        var geom = f.Session.Geometry(id)!;
        Assert.Equal(5, geom.Parts.Count(p => p.Key!.StartsWith("timber:plate")));
    }

    [Fact]
    public void A_straight_roofs_members_and_drainage_are_unchanged()
    {
        var f = new Fixture(new ArchitectureModule());
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 6 })!["id"]!;
        var (r, s) = Solved(f, id);
        var gutters = RoofParts.Gutters(r, s);
        Assert.Equal(gutters.Count, Drainage.Runs(gutters, r).Count);
        // The long eaves (11 m with their overhang) two each, the short ones one: as before.
        Assert.Equal(6, Drainage.Outlets(f.Model, r, gutters).Count);
        var members = RoofParts.Framing(r, s);
        Assert.Equal(4, members.Count(m => m.Role == "plate"));
        Assert.All(members, m => Assert.Null(m.Path));
        Assert.Same(s, RoofCurves.WithoutCreases(r, s));
        Assert.Empty(RoofCurves.SmoothRuns(r, s));
    }

    [Fact]
    public void A_layers_edge_extent_on_a_curved_roof_moves_the_edge_it_names()
    {
        var f = new Fixture(new ArchitectureModule());
        var id = (string)f.Run("architecture.addRoof", new JsonObject
        {
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)),
            ["arcs"] = new JsonArray(-1.2, 0, 0, 0),
        })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = id,
            ["attrs"] = new JsonObject
            {
                ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.5,
                ["layers"] = new JsonArray(
                    new JsonObject { ["material"] = "OSB", ["thickness"] = 0.02 },
                    new JsonObject { ["material"] = "Clay tile", ["thickness"] = 0.04 }),
                // Edge 2 is the back eave (y = 6), straight; the arc is edge 0.
                ["layerExtents"] = new JsonArray(new JsonObject { ["layer"] = 0, ["edge"] = "edge", ["index"] = 2, ["offset"] = -0.2 }),
            },
        });
        var r = Roof.Resolve(f.Model.Get(id)!, f.Model)!;
        var d = Roof.LayerOffsets(r, 0)!;
        // Every chord that lays the back eave, and only it, takes the offset (before: chord 2, a piece of the arc).
        var (pts, _) = RoofSolver.Orient(r.Outline, r.Rules);
        for (var k = 0; k < d.Length; k++)
        {
            var (a, b) = (pts[k], pts[(k + 1) % pts.Count]);
            var back = Math.Abs(a.Y - 6) < 1e-9 && Math.Abs(b.Y - 6) < 1e-9;
            Assert.Equal(back ? -0.2 : 0, d[k], 9);
        }
        Assert.Null(f.Session.Geometry(id)!.Diagnostics);
        // Layer 0's back slope stops 0.2 m short (its eave at 6.3, the roof's at 6.5); its curved front is the roof's.
        var shape = Roof.Solve(r).Shape!;
        var layer = Roof.LayerShape(r, shape, 0);
        var backEdge = Enumerable.Range(0, pts.Count).Single(k => Math.Abs(d[k]) > 1e-9);
        Assert.Equal(6.5, shape.Planes.Single(p => p.Edge == backEdge).Points.Max(q => q.Y), 6);
        Assert.Equal(6.3, layer.Planes.Single(p => p.Edge == backEdge).Points.Max(q => q.Y), 6);
        Assert.Equal(shape.Planes.Min(p => p.Points.Min(q => q.Y)), layer.Planes.Min(p => p.Points.Min(q => q.Y)), 6);
    }
    [Theory]
    [InlineData("hip", "rafters", -1.5)]
    [InlineData("hip", "rafters", 1.0)]
    [InlineData("hip", "collar", -1.5)]
    [InlineData("gable", "trusses", -1.5)]
    [InlineData("gable", "rafters", 1.0)]
    [InlineData("shed", "rafters", -1.5)]
    public void Curved_roofs_of_every_shape_and_framing_build_members_that_stay_in_the_roof(string shape, string structure, double rise)
    {
        var f = new Fixture(new ArchitectureModule());
        var id = (string)f.Run("architecture.addRoof", new JsonObject
        {
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 7), new JsonArray(0, 7)),
            ["arcs"] = new JsonArray(rise, 0, 0, 0),
        })!["id"]!;
        f.Run("architecture.roundCorner", new JsonObject { ["id"] = id, ["index"] = 2, ["radius"] = 1.5 });
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["shape"] = shape, ["structure"] = structure } });
        var (r, s) = Solved(f, id);
        var members = RoofParts.Framing(r, s);
        Assert.NotEmpty(members);
        var box = Measure.Bounds(f.Session.Geometry(id)!.Parts.Single(p => p.Key == "covering").Solid)!.Value;
        foreach (var m in members)
        {
            Assert.NotNull(m.Body(m.Role));
            Assert.True(m.Length > 0.05 && m.Length < 16, $"{m.Role} {m.Length}");
            foreach (var p in m.Path ?? [m.A, m.B])
                Assert.True(p.X > box.Min.X - 0.2 && p.X < box.Max.X + 0.2 && p.Y > box.Min.Y - 0.2 && p.Y < box.Max.Y + 0.2 && p.Z < box.Max.Z, $"{m.Role} at {p}");
        }
        Assert.InRange(Drainage.Outlets(f.Model, r, RoofParts.Gutters(r, s)).Count, 1, 8);
    }
}
