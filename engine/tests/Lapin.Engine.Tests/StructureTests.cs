using System.Text.Json.Nodes;
using Lapin.Geometry;
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

    [Fact]
    public void A_columns_rotation_is_in_degrees_like_every_other_angle()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 30x60", ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.6 } })!["id"]!;
        var flat = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 2, ["y"] = 1 })!["id"]!;
        var turned = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 6, ["y"] = 1, ["rotation"] = 90 })!["id"]!;
        var (a0, a1) = f.Bounds(flat);
        var (b0, b1) = f.Bounds(turned);
        Assert.Equal(a1.Y - a0.Y, b1.X - b0.X, 6);
        Assert.Equal(a1.X - a0.X, b1.Y - b0.Y, 6);
        // A rigid turn adds degrees.
        f.Run("element.transform", new JsonObject { ["id"] = turned, ["op"] = "rotate", ["center"] = new JsonArray(6, 1), ["angle"] = 45 });
        Assert.Equal(135, f.Model.Get(turned)!.Num("rotation"), 9);
        // Columns on a slanted grid take the axis direction in degrees.
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 10 * Math.Cos(Math.PI / 6), ["y2"] = 10 * Math.Sin(Math.PI / 6) });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 4, ["y1"] = -3, ["x2"] = 4, ["y2"] = 8 });
        var id = (string)f.Run("structure.addColumnsAtGrid", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type })!["ids"]![0]!;
        Assert.Contains(Math.Round(f.Model.Get(id)!.Num("rotation"), 6), new[] { 30.0, 90.0 });
    }

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
        var a = Strip(f, 0, 0, 5, 0, "butt");
        var c = Strip(f, 5, 0, 5, 4, "butt");
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

    [Fact]
    public void Columns_go_on_every_grid_crossing_once_with_a_type_made_later()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());
        foreach (var x in new[] { 0.0, 5, 10 }) f.Run("drafting.addAxis", new JsonObject { ["x1"] = x, ["y1"] = -1, ["x2"] = x, ["y2"] = 9 });
        foreach (var y in new[] { 0.0, 8 }) f.Run("drafting.addAxis", new JsonObject { ["x1"] = -1, ["y1"] = y, ["x2"] = 11, ["y2"] = y });
        var later = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 40", ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.4, ["depth"] = 0.4 } })!["id"]!;
        var ids = f.Run("structure.addColumnsAtGrid", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = later })!["ids"]!.AsArray();
        Assert.Equal(6, ids.Count);
        Assert.All(ids, id => Assert.Equal(later, f.Model.RelatingOf((string)id!, IfcClass.RelDefinesByType)!.Id));
        Assert.Throws<CommandException>(() => f.Run("structure.addColumnsAtGrid", new JsonObject { ["storeyId"] = f.Storey }));
    }

    [Fact]
    public void Steel_profiles_from_the_catalogue_or_by_size_for_columns_and_beams()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var ipe = Section.Catalogue["IPE 300"];
        // I: two flanges and the web between them.
        Assert.Equal(2 * 0.150 * 0.0107 + (0.300 - 2 * 0.0107) * 0.0071, ipe.Area, 9);
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "IPE 300", ["height"] = 3 })!["id"]!;
        Assert.Equal("Column IPE 300", f.Model.Get(colType)!.Name);
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = colType, ["x"] = 0, ["y"] = 0 })!["id"]!;
        Assert.True(Math.Abs(ipe.Area * 3 - f.Session.Geometry(col)!.Parts.Sum(p => Measure.Volume(p.Solid))) < 1e-9);
        Assert.Equal("Steel", f.Model.Get(f.Model.Get(colType)!.Str("material")!)!.Name);
        // A beam of a profile by its size: swept along its axis, its top at the beam's top.
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "U", ["depth"] = 0.2, ["width"] = 0.075, ["web"] = 0.0085, ["flange"] = 0.0115 } })!["id"]!;
        Assert.Equal("Beam U 200×75", f.Model.Get(beamType)!.Name);
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = beamType, ["x1"] = 1, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var upn = Section.From(f.Model.Get(beamType)!.Attrs["section"])!;
        Assert.True(Math.Abs(upn.Area * 5 - f.Session.Geometry(beam)!.Parts.Sum(p => Measure.Volume(p.Solid))) < 1e-9);
        Assert.Throws<CommandException>(() => f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "I", ["depth"] = 0.2, ["width"] = 0.1, ["web"] = 0.2, ["flange"] = 0.01 } }));
    }
}
