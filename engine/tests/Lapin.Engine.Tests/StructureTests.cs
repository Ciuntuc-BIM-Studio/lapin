using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The structure module. Foundations ported from the old Lapin's
/// foundation.e2e.test.ts (same defaults and numbers), cut priorities from
/// cut-priority.e2e.test.ts (a column through a wall cuts it).
/// </summary>
public class StructureTests
{
    static Fixture Both() => new(new ArchitectureModule(), new StructureModule());
    static double Vol(Fixture f, string id, string? key = null) => f.Session.Geometry(id)!.Parts.Where(p => key == null || p.Key == key).Sum(p => Measure.Volume(p.Solid));
    static (Vec3 Min, Vec3 Max) Bounds(Fixture f, string id, string key) => Measure.Bounds(f.Session.Geometry(id)!.Parts.Single(p => p.Key == key).Solid)!.Value;

    static string Strip(Fixture f, double x1, double y1, double x2, double y2, string? style = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
        if (style != null) a["joinStyle"] = style;
        return (string)f.Run("structure.addStripFooting", a)!["id"]!;
    }

    static double Q(Fixture f, string id, string name, string? part = null) =>
        f.Session.Quantities(id).Single(s => s.Part == part).Quantities.Single(q => q.Name == name).Value;

    // ------------------------------------------------------------ strip foundations

    [Fact]
    public void A_strip_foundation_is_its_stem_core_on_the_axis_on_a_footing_centred_under_it()
    {
        var f = Both();
        var sf = Strip(f, 0, 0, 5, 0);
        Assert.Equal("SF 30", f.Model.Get(sf)!.Name);
        // Drawn along +X: the right face is −Y. The stem stands on the footing (bottom −1.8 + 0.4).
        var stem = f.Session.Geometry(sf)!.Parts.Where(p => p.Key!.StartsWith("layer:")).Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        Assert.Equal(-0.275, stem.Min(b => b.Min.Y), 9);
        Assert.Equal(0.15, stem.Max(b => b.Max.Y), 9);
        Assert.Equal(-1.4, stem.Min(b => b.Min.Z), 9);
        Assert.Equal(0, stem.Max(b => b.Max.Z), 9);
        var footing = Bounds(f, sf, "footing");
        Assert.Equal((-0.3, 0.3, -1.8, -1.4), (footing.Min.Y, footing.Max.Y, footing.Min.Z, footing.Max.Z));
        // Measured as the old test: the footing 5 × 0.6 × 0.4, the core 5 × 0.3 × 1.4.
        Assert.Equal(5 * 0.6 * 0.4, Q(f, sf, "NetVolume", "footing"), 9);
        Assert.Equal(5 * 0.3 * 1.4, Q(f, sf, "NetVolume", "layer:3"), 9);
    }

    [Fact]
    public void A_butt_corner_the_through_footing_reaches_the_outer_corner_the_other_stops_at_its_face()
    {
        var f = Both();
        var a = Strip(f, 0, 0, 5, 0);
        var c = Strip(f, 5, 0, 5, 4);
        Assert.Single(f.Model.Relations, r => r.Class == Joins.Class);
        var fa = Bounds(f, a, "footing");
        var fc = Bounds(f, c, "footing");
        // Drawn last, c runs through (the old test joined a first, so a did; the rule is the same):
        // the through footing runs 0.3 past the corner, the other stops 0.3 short of it.
        Assert.Equal(-0.3, fc.Min.Y, 9);
        Assert.Equal(4.7, fa.Max.X, 9);
        Assert.Equal(0.6 * 4.3, Q(f, c, "Area", "footing"), 9);
        // Swapped, a runs through: the old test's own numbers.
        f.Run("structure.restyleJoins", new JsonObject { ["id"] = a, ["swap"] = true });
        Assert.Equal(5.3, Bounds(f, a, "footing").Max.X, 9);
        Assert.Equal(0.3, Bounds(f, c, "footing").Min.Y, 9);
        Assert.Equal(0.6 * 5.3, Q(f, a, "Area", "footing"), 9);
    }

    [Fact]
    public void A_mitred_corner_cuts_both_footings_on_the_diagonal_and_a_T_lets_the_through_one_run_on()
    {
        var f = Both();
        var a = Strip(f, 0, 0, 5, 0, "mitre");
        var c = Strip(f, 5, 0, 5, 4, "mitre");
        // A mitred end adds a triangle 0.3 × 0.3 outside and takes one inside: each footing keeps its axis length × 0.6.
        Assert.Equal(5 * 0.6 * 0.4, Vol(f, a, "footing"), 9);
        Assert.Equal(4 * 0.6 * 0.4, Vol(f, c, "footing"), 9);
        Assert.Equal(5.3, Bounds(f, a, "footing").Max.X, 9);   // the outer face runs to the outer corner

        var g = Both();
        var through = Strip(g, 0, 0, 10, 0);
        var ending = Strip(g, 5, 4, 5, 0);
        Assert.Equal((0.0, 10.0), (Bounds(g, through, "footing").Min.X, Bounds(g, through, "footing").Max.X));
        Assert.Equal(0.3, Bounds(g, ending, "footing").Min.Y, 9);
    }

    // ------------------------------------------------------------ pad footings

    [Fact]
    public void A_stepped_pad_stands_centred_where_it_was_put_and_is_measured()
    {
        var f = Both();
        var pad = (string)f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        var parts = f.Session.Geometry(pad)!.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        Assert.Equal((1.1, 2.1, -1.8), (Math.Round(parts.Min(b => b.Min.X), 9), Math.Round(parts.Min(b => b.Min.Y), 9), Math.Round(parts.Min(b => b.Min.Z), 9)));
        Assert.Equal((2.9, 3.9, -0.9), (Math.Round(parts.Max(b => b.Max.X), 9), Math.Round(parts.Max(b => b.Max.Y), 9), Math.Round(parts.Max(b => b.Max.Z), 9)));
        Assert.Equal(1.8 * 1.8 * 0.5 + 1 * 1 * 0.4, Q(f, pad, "NetVolume"), 9);
        Assert.Equal(0.9, Q(f, pad, "Height"), 9);
        Assert.Equal(4 * 1.8 * 0.5 + 4 * 1 * 0.4 + (1.8 * 1.8 - 1) + 1, Q(f, pad, "OuterSurfaceArea"), 9);
    }

    // ------------------------------------------------------------ columns, beams, cut priorities

    [Fact]
    public void A_column_through_a_wall_cuts_it_the_one_beside_it_does_not()
    {
        var f = Both();
        var wall = f.Wall(0, 0, 6, 0);
        var full = Vol(f, wall);
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 3, ["y"] = 0 })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 3, ["y"] = 2 });
        // A 300 × 300 column the storey's height, the wall 285 mm thick at it: 0.3 × 0.285 × 3 out of the wall.
        Assert.Equal(full - 0.3 * 0.285 * 3, Vol(f, wall), 6);
        Assert.Equal(0.3 * 0.3 * 3, Vol(f, column), 9);
        var q = f.Session.Quantities(column)[0];
        Assert.Equal("Qto_ColumnBaseQuantities", q.Name);
        Assert.Equal(3, q.Quantities.Single(x => x.Name == "Length").Value, 9);
        // Moved off the wall, it cuts it no more.
        f.Run("structure.updateColumn", new JsonObject { ["id"] = column, ["x"] = 3, ["y"] = -2 });
        Assert.Equal(full, Vol(f, wall), 6);
    }

    [Fact]
    public void Beams_stand_with_their_top_at_the_level_above_and_join_at_corners()
    {
        var f = Both();
        var a = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 })!["id"]!;
        var b = Measure.Bounds(f.Session.Geometry(a)!.Parts[0].Solid)!.Value;
        // 250 × 400, its top at +3.00 (the storey's height).
        Assert.Equal((3.0, 2.6), (b.Max.Z, Math.Round(b.Min.Z, 9)));
        Assert.Equal(0.25, b.Max.Y - b.Min.Y, 9);
        var c = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 4 })!["id"]!;
        Assert.Single(f.Model.Relations, r => r.Class == Joins.Class && r.Touches(a) && r.Touches(c));
        // A beam over a wall cuts its head (priority 80 over 50): one wider than the wall takes it all.
        var wide = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.4 } })!["id"]!;
        var wall = f.Wall(0, 10, 5, 10);
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -1, ["y1"] = 10, ["x2"] = 6, ["y2"] = 10, ["typeId"] = wide });
        Assert.Equal(2.6, f.Bounds(wall).Max.Z, 9);
    }

    [Fact]
    public void An_edition_with_structure_alone_has_no_walls_but_its_own_elements()
    {
        var f = new Fixture(new StructureModule());
        Assert.Throws<CommandException>(() => f.Run("architecture.addWall", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 1, ["y2"] = 0 }));
        var sf = Strip(f, 0, 0, 5, 0);
        Assert.NotNull(f.Session.Geometry(sf));
    }
}
