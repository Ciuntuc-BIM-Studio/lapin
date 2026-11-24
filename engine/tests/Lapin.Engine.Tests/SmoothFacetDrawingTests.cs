using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Curves made of flats draw as curves in elevations, sections and plans (forum topic 255, decision 844): no line on a seam between
/// two chords of a curve that both face the eye (<see cref="HiddenLines.ChordSeams"/>, under <see cref="Tessellator.CreaseDeg"/>);
/// the outline, folds someone drew however gentle (a low ridge, a hip, a slab's fall), open edges and the cut still draw. A member's
/// fillets at LOD 400 keep their own rule, every gentle fold (decision 415).
/// </summary>
public class SmoothFacetDrawingTests
{
    static bool Vertical(Seg2 s) => Math.Abs(s.B.X - s.A.X) < 1e-6 && Math.Abs(s.B.Y - s.A.Y) > 0.1;
    static bool Vertical(DLine l) => Math.Abs(l.B.X - l.A.X) < 1e-6 && Math.Abs(l.B.Y - l.A.Y) > 0.1;

    static Solid Extrude(IEnumerable<(double X, double Y)> ring, Vec3 d) =>
        Prism.Extrude(ring.Select(p => new Vec3(p.X, p.Y, 0)).ToList(), null, d)!;

    static List<(double, double)> Circle(double r, int n) =>
        Enumerable.Range(0, n).Select(i => (r * Math.Cos(2 * Math.PI * i / n), r * Math.Sin(2 * Math.PI * i / n))).ToList();

    [Fact]
    public void A_round_column_seen_from_the_side_draws_its_two_outlines_and_no_line_per_flat()
    {
        var column = Extrude(Circle(0.5, 24), new Vec3(0, 0, 3));
        var view = ViewFrame.Horizontal(new Vec3(0, -5, 0), new Vec2(0, 1));
        var (smooth, _, _) = HiddenLines.Draw([(column, 0)], view, chords: true);
        var (faceted, _, _) = HiddenLines.Draw([(column, 0)], view);
        // The two outlines, where the flats turn from the eye; the twelve flats facing it would add eleven lines between them.
        Assert.Equal(2, smooth.Count(Vertical));
        Assert.Equal(13, faceted.Count(Vertical));
        // The top and the bottom still draw across the whole width.
        foreach (var y in new[] { 0.0, 3.0 })
        {
            var at = smooth.Where(s => Math.Abs(s.A.Y - y) < 1e-6 && Math.Abs(s.B.Y - y) < 1e-6).ToList();
            Assert.Equal(1.0, at.Max(s => Math.Max(s.A.X, s.B.X)) - at.Min(s => Math.Min(s.A.X, s.B.X)), 6);
        }
        // Its hidden back draws no facet either.
        var (_, _, hidden) = HiddenLines.Draw([(column, 0)], view, hidden: true, chords: true);
        Assert.DoesNotContain(hidden, s => Vertical(s) && Math.Abs(s.A.X) < 0.49);
    }

    [Fact]
    public void A_box_still_draws_every_edge_it_shows()
    {
        var box = Extrude([(0, 0), (2, 0), (2, 1), (0, 1)], new Vec3(0, 0, 1.5));
        var dir = new Vec3(1, 2, -1.5).Normalized();
        var right = Vec3.Cross(dir, Vec3.UnitZ).Normalized();
        var view = new ViewFrame(new Vec3(-5, -10, 8), right, Vec3.Cross(right, dir), dir);
        var (lines, _, _) = HiddenLines.Draw([(box, 0)], view, chords: true);
        // Seen from a corner: three faces, nine edges.
        Assert.Equal(9, lines.Count);
    }

    static readonly ViewFrame Down = new(new Vec3(0, 0, 10), new Vec3(1, 0, 0), new Vec3(0, 1, 0), -Vec3.UnitZ);

    /// <summary>A gable prism along y (one solid across its ridge at x = 3), its profile drawn in x–z.</summary>
    static Solid Gable(double pitchDeg) =>
        SolidBuilder.Transform(Extrude([(0, 0), (6, 0), (6, 0.5), (3, 0.5 + 3 * Math.Tan(pitchDeg * Math.PI / 180)), (0, 0.5)], new Vec3(0, 0, 4)), p => new Vec3(p.X, p.Z, p.Y));

    static bool Ridge(List<Seg2> lines) => lines.Any(s => Math.Abs(s.A.X - 3) < 1e-6 && Math.Abs(s.B.X - 3) < 1e-6);

    [Fact]
    public void A_gentle_fold_someone_drew_keeps_its_line_however_low()
    {
        // Seen from above, both slopes face the eye: 2 × 30° and 2 × 5° (10°, under the 20° crease) both draw their ridge.
        Assert.True(Ridge(HiddenLines.Draw([(Gable(30), 0)], Down, chords: true).Lines));
        Assert.True(Ridge(HiddenLines.Draw([(Gable(5), 0)], Down, chords: true).Lines));
        Assert.True(Ridge(HiddenLines.Draw([(Gable(2), 0)], Down, chords: true).Lines));
        // A member's fillets at LOD 400 keep their own rule (decision 415): every gentle fold smooth.
        Assert.False(Ridge(HiddenLines.Draw([(Gable(5), 0)], Down, smooth: [true]).Lines));
    }

    [Fact]
    public void A_low_hipped_roof_draws_its_ridge_and_hips_and_a_slab_its_fall()
    {
        // A hipped block of 8 × 6 at 5°: its ridge from (3, 3) to (5, 3) and four hips, all under the crease.
        var t = Math.Tan(5 * Math.PI / 180);
        var v = new List<Vec3>
        {
            new(0, 0, 0), new(8, 0, 0), new(8, 6, 0), new(0, 6, 0),
            new(0, 0, 0.3), new(8, 0, 0.3), new(8, 6, 0.3), new(0, 6, 0.3),
            new(3, 3, 0.3 + 3 * t), new(5, 3, 0.3 + 3 * t),
        };
        var faces = new List<FaceInput>
        {
            new([0, 3, 2, 1]), new([0, 1, 5, 4]), new([1, 2, 6, 5]), new([2, 3, 7, 6]), new([3, 0, 4, 7]),
            new([4, 5, 9, 8]), new([5, 6, 9]), new([6, 7, 8, 9]), new([7, 4, 8]),
        };
        var hip = SolidBuilder.Make(v, faces);
        var lines = HiddenLines.Draw([(hip, 0)], Down, chords: true).Lines;
        // The outline's four sides, the ridge and the four hips.
        Assert.Equal(9, lines.Count);
        // A slab falling 2 % towards a gutter line in its middle (a fold of 2.3°, concave): drawn.
        var fall = SolidBuilder.Transform(Extrude([(0, 0), (4, 0), (4, 0.2), (2, 0.16), (0, 0.2)], new Vec3(0, 0, 3)), p => new Vec3(p.X, p.Z, p.Y));
        Assert.Contains(HiddenLines.Draw([(fall, 0)], Down, chords: true).Lines, s => Math.Abs(s.A.X - 2) < 1e-6 && Math.Abs(s.B.X - 2) < 1e-6);
    }

    [Fact]
    public void A_curve_s_first_chord_off_a_tangent_flat_and_a_three_piece_arch_draw_smooth()
    {
        // A slab with a rounded corner: flats tangent to a quarter circle of 1 m by the chord rule.
        var ring = new List<(double, double)> { (0, 0), (4, 0), (4, 3) };
        var n = Chords.Count(1, Math.PI / 2);
        for (var k = 0; k <= n; k++) ring.Add((3 + Math.Cos(Math.PI / 2 * k / n), 3 + Math.Sin(Math.PI / 2 * k / n)));
        ring.Add((0, 4));
        var slab = Extrude(ring, new Vec3(0, 0, 0.2));
        var side = ViewFrame.Horizontal(new Vec3(10, -10, 0), new Vec2(-1, 1));
        var lines = HiddenLines.Draw([(slab, 0)], side, chords: true).Lines;
        var faceted = HiddenLines.Draw([(slab, 0)], side).Lines;
        // Seen from the corner's diagonal: the near corner and the two outlines (the far end, the arc where it turns away half way),
        // none where the tangent flat meets the first chord, nor between the chords in front.
        Assert.Equal(3, lines.Count(Vertical));
        Assert.True(faceted.Count(Vertical) >= lines.Count(Vertical) + 2, $"{faceted.Count(Vertical)} against {lines.Count(Vertical)}");
    }

    [Fact]
    public void A_flat_face_split_into_coplanar_pieces_draws_no_line_inside()
    {
        // A slab whose top is two triangles: no diagonal in a plan seen from above.
        var v = new List<Vec3> { new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(0, 1, 0), new(0, 0, 0.2), new(2, 0, 0.2), new(2, 1, 0.2), new(0, 1, 0.2) };
        var faces = new List<FaceInput>
        {
            new([0, 3, 2, 1]), new([4, 5, 6]), new([4, 6, 7]),
            new([0, 1, 5, 4]), new([1, 2, 6, 5]), new([2, 3, 7, 6]), new([3, 0, 4, 7]),
        };
        var slab = SolidBuilder.Make(v, faces);
        var (lines, _, _) = HiddenLines.Draw([(slab, 0)], Down, chords: true);
        Assert.Equal(4, lines.Count);
    }

    static ViewDrawing Elevation(Fixture f, string direction)
    {
        var id = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = direction })!["id"]!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, Views.Find(f.Model, id)!);
        Assert.True(d != null, problem);
        return d!;
    }

    [Fact]
    public void A_curved_wall_in_elevation_draws_its_ends_top_and_bottom_and_no_line_per_flat()
    {
        var f = new Fixture();
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0, ["arc"] = 2, ["height"] = 3 })!["id"]!;
        foreach (var direction in new[] { "south", "north" })
        {
            var lines = Elevation(f, direction).Lines.Where(l => l.Element == wall).ToList();
            var xs = lines.SelectMany(l => new[] { l.A.X, l.B.X }).ToList();
            double lo = xs.Min(), hi = xs.Max();
            Assert.InRange(hi - lo, 7.9, 8.6);
            var vertical = lines.Where(Vertical).ToList();
            // Its ends (and the end faces' edges beside them), never a line where two flats of the arc meet.
            Assert.Contains(vertical, l => l.A.X - lo < 0.4);
            Assert.Contains(vertical, l => hi - l.A.X < 0.4);
            Assert.DoesNotContain(vertical, l => l.A.X - lo > 0.4 && hi - l.A.X > 0.4);
            // Its top and bottom run the whole way.
            var horizontal = lines.Where(l => Math.Abs(l.A.Y - l.B.Y) < 1e-6).ToList();
            Assert.True(horizontal.Sum(l => Math.Abs(l.B.X - l.A.X)) > 2 * 7.5, $"{direction}: {horizontal.Count} horizontal lines");
        }
    }

    [Fact]
    public void A_barrel_roof_made_of_strips_draws_no_line_between_them()
    {
        // The vault is one strip per chord, each its own piece of the covering and the deck, meeting face to face: one body for the drawing.
        var f = new Fixture(new ArchitectureModule());
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(8, 0), new JsonArray(8, 7), new JsonArray(0, 7)), ["style"] = "modern_barrel" })!["id"]!;
        var lines = Elevation(f, "south").Lines.Where(l => l.Element == roof && Math.Abs(l.A.Y - l.B.Y) < 1e-6 && Math.Abs(l.B.X - l.A.X) > 1).ToList();
        var top = lines.Max(l => l.A.Y);
        // The eaves, the gutters, the snow bar and the crown's outline; between the snow bar and the crown, where the strips meet, nothing.
        Assert.Contains(lines, l => Math.Abs(l.A.Y - top) < 1e-6);
        Assert.DoesNotContain(lines, l => l.A.Y > 3.9 && l.A.Y < top - 0.01);
        Assert.InRange(lines.Select(l => Math.Round(l.A.Y, 3)).Distinct().Count(), 4, 10);
    }

    [Fact]
    public void A_cylinder_in_elevation_draws_its_outline_only()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var id = (string)f.Run("studio.primitive", new JsonObject { ["shape"] = "cylinder", ["at"] = new JsonArray(0, 0), ["radius"] = 0.5, ["height"] = 2 })!["id"]!;
        var lines = Elevation(f, "south").Lines.Where(l => l.Element == id).ToList();
        var vertical = lines.Where(Vertical).ToList();
        Assert.Equal(2, vertical.Select(l => Math.Round(l.A.X, 4)).Distinct().Count());
    }
}
