using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// Walls whose layers are parts (docs/spec-ifc.md, Pereții din piese; <see cref="ForeignWallParts"/>): each
/// IfcBuildingElementPart read as a layer from its own triangles (its faces across, its ends, its top and bottom seen from
/// the side), the wall made layered with the file's gable top (<c>topProfile</c>) and the layers' stepped ends
/// (<c>layerEnds</c>), or left a reference with the reason when a part is no layer; and the file's own connections
/// (IfcRelConnectsPathElements) tried first by the joins.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>A gable's side outline (mm): flat at the bottom, eaves at <paramref name="eaves"/> at u = 0 and 6000, the ridge at <paramref name="ridge"/> at u = 3000, cut at <paramref name="u0"/> and <paramref name="u1"/>.</summary>
    static (double U, double Z)[] Gable(double u0, double u1, double eaves = 2500, double ridge = 4000)
    {
        double Top(double u) => u <= 3000 ? eaves + (ridge - eaves) * u / 3000 : ridge - (ridge - eaves) * (u - 3000) / 3000;
        var pts = new List<(double, double)> { (u0, 0), (u1, 0), (u1, Top(u1)) };
        if (u0 < 3000 && u1 > 3000) pts.Add((3000, ridge));
        pts.Add((u0, Top(u0)));
        return [.. pts];
    }

    /// <summary>
    /// A part: its side outline (u along the wall, z up, mm) extruded across the wall from <paramref name="v0"/> to
    /// <paramref name="v1"/> (left of the axis positive), placed with the wall; of its own material.
    /// </summary>
    static string Part(Foreign x, string key, string material, string wallPlacement, (double U, double Z)[] outline, double v0, double v1)
    {
        var f = x.F;
        var loop = f.Add("IFCPOLYLINE", L(outline.Append(outline[0]).Select(p => f.Add("IFCCARTESIANPOINT", $"({R(p.U)},{R(p.Z)})"))));
        // The outline in the wall's vertical plane: local x along the wall, local y up, extruded along local z (the wall's −y) from v1 to v0.
        var at = f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", $"(0.,{R(v1)},0.)"), f.Add("IFCDIRECTION", "(0.,-1.,0.)"), f.Add("IFCDIRECTION", "(1.,0.,0.)"));
        var solid = f.Add("IFCEXTRUDEDAREASOLID", f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, loop), at, f.Add("IFCDIRECTION", "(0.,0.,1.)"), R(v1 - v0));
        var part = f.Add("IFCBUILDINGELEMENTPART", x.G(key), Null, S(material), Null, Null, x.Place(0, 0, 0, 0, wallPlacement), x.Shape(solid, "SweptSolid"), Null, E("NOTDEFINED"));
        x.Material(part, f.Add("IFCMATERIAL", S(material), Null, Null));
        return part;
    }

    /// <summary>A wall with its 'Axis' (from (0, 0) along x, <paramref name="length"/> mm) and no body of its own, its parts aggregated under it.</summary>
    static string PartsWall(Foreign x, string key, double ox, double oy, double angleDeg, double length, Func<string, string[]> parts)
    {
        var f = x.F;
        var pl = x.Place(ox, oy, 0, angleDeg);
        var axis = f.Add("IFCSHAPEREPRESENTATION", x.Ctx, S("Axis"), S("Curve2D"), L([f.Add("IFCPOLYLINE", L(new[] { "(0.,0.)", $"({R(length)},0.)" }.Select(p => f.Add("IFCCARTESIANPOINT", p))))]));
        var wall = x.Element("IFCWALL", key, key, pl, f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([axis])), E("STANDARD"));
        f.Add("IFCRELAGGREGATES", x.G(key + "-parts"), Null, Null, Null, wall, L(parts(pl)));
        return wall;
    }

    /// <summary>
    /// A wall 6 m long under a gable (eaves 2.5 m, ridge 4.0 m) of three parts: a 20 mm plaster stopping 100 mm short of
    /// both ends, a 200 mm masonry core, a 100 mm insulation running 100 mm on past both.
    /// </summary>
    string GableWallFile(string name, bool flatTopPlaster = false)
    {
        var x = new Foreign();
        PartsWall(x, "gable", 0, 0, 0, 6000, pl =>
        [
            Part(x, "gable-plaster", "Plaster", pl, flatTopPlaster ? [(100, 0), (5900, 0), (5900, 2400), (100, 2400)] : Gable(100, 5900), 0, 20),
            Part(x, "gable-core", "Masonry", pl, Gable(0, 6000), 20, 220),
            Part(x, "gable-insulation", "Insulation", pl, Gable(-100, 6100), 220, 320),
        ]);
        return x.Write(Path.Combine(_dir, name));
    }

    [Fact]
    public void A_wall_of_three_parts_with_a_gable_top_and_stepped_ends_comes_in_layered_as_the_file_has_it()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, GableWallFile("gable.ifc"), null, out _);
        var w = f.Model.Get(Guid("foreign-test", "gable"))!;
        Assert.Equal(IfcClass.Wall, w.Class);
        Assert.False(StoredBodies.Has(w));
        // Three layers, the core the thickest, 320 mm in all, as the parts lie across the axis.
        var bands = Walls.Section(f.Model, w);
        Assert.Equal(3, bands.Count);
        Assert.Equal([0.02, 0.2, 0.1], bands.Select(b => Math.Round(b.To - b.From, 4)));
        Assert.True(bands[1].Core);
        Assert.Equal(0, bands[0].From, 6);
        // The wall runs the core's length; the plaster stops 100 mm short of each end, the insulation runs 100 mm on.
        Assert.Equal(new Vec2(0, 0), Walls.Start(w));
        Assert.Equal(new Vec2(6, 0), Walls.End(w));
        var ends = (JsonArray)w.Attrs[Walls.LayerEndsKey]!;
        Assert.Equal([-0.1, -0.1, -0.1, -0.1], ((JsonArray)ends[0]!).Select(v => (double)v!));
        Assert.Equal([0.1, 0.1, 0.1, 0.1], ((JsonArray)ends[2]!).Select(v => (double)v!));
        // The gable: its top along the axis from the start, the ridge 4.0 m at 3 m.
        var top = ((JsonArray)w.Attrs[WallTops.TopProfileKey]!).Select(p => ((double)p![0]!, (double)p[1]!)).ToList();
        output.WriteLine(string.Join(" ", top));
        Assert.Contains(top, p => Math.Abs(p.Item1 - 3) < 1e-6 && Math.Abs(p.Item2 - 4) < 1e-6);
        Assert.Equal(4.0, w.Num("height"), 6);
        // The body is the file's: the ridge's height, its volume and its plan within the gate.
        var b = f.Bounds(w.Id);
        Assert.Equal(4.0, b.Max.Z, 3);
        Assert.Equal(-0.1, b.Min.X, 3);
        Assert.Equal(6.1, b.Max.X, 3);
        var c = CheckOf(r, w.Id);
        output.WriteLine(c.ToJson().ToJsonString());
        Assert.True(c.Pass, c.Reason);
        // Each layer's volume as its part's: its side outline's area across its thickness.
        static double Area((double U, double Z)[] p) => Math.Abs(Enumerable.Range(0, p.Length).Sum(i => p[i].U * p[(i + 1) % p.Length].Z - p[(i + 1) % p.Length].U * p[i].Z) / 2) / 1e6;
        var geometry = f.Session.Geometry(w.Id)!;
        Assert.Equal(Area(Gable(100, 5900)) * 0.02 + Area(Gable(0, 6000)) * 0.2 + Area(Gable(-100, 6100)) * 0.1, geometry.Parts.Sum(p => Measure.Volume(p.Solid)), 4);
    }

    [Fact]
    public void A_gable_wall_of_parts_keeps_its_top_and_its_layers_ends_in_its_section_and_its_IFC()
    {
        var f = Full();
        IfcImport.Run(f.Session, GableWallFile("gable-export.ifc"), null, out _);
        var id = Guid("foreign-test", "gable");
        // Through the ridge, across the wall: the section's cut reaches 4.0 m; at the eaves 2.5 m.
        var tops = new List<double>();
        foreach (var u in new[] { 3.0, 0.5 })
        {
            var g = f.Session.Geometry(id)!;
            var top = g.Parts.SelectMany(p => p.Solid.Vertices).Where(v => Math.Abs(v.X - u) < 1e-6).Select(v => v.Z).DefaultIfEmpty(double.NaN).Max();
            tops.Add(top);
        }
        Assert.Equal(4.0, tops[0], 3);
        // Exported and read back as a reference: the same body, its ridge and its ends where the file had them.
        var exported = Path.Combine(_dir, "gable-out.ifc");
        IfcExport.Write(f.Session, exported);
        var g2 = Full();
        IfcImport.Run(g2.Session, exported, new IfcImportOptions(Mode: "reference"), out _);
        var back = g2.Model.Entities.Where(e => StoredBodies.Has(e) && e.Str("ifcClass") == "IfcWall").ToList();
        Assert.NotEmpty(back);
        var bb = back.Select(e => g2.Bounds(e.Id)).ToList();
        Assert.Equal(4.0, bb.Max(x => x.Max.Z), 3);
        Assert.Equal(-0.1, bb.Min(x => x.Min.X), 3);
        Assert.Equal(6.1, bb.Max(x => x.Max.X), 3);
        // Its record carries the top and the ends for Lapin to read again.
        Assert.Contains("topProfile", File.ReadAllText(exported));
    }

    [Fact]
    public void A_layer_flat_under_the_gable_is_a_layer_of_its_own_height()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, GableWallFile("gable-flat.ifc", flatTopPlaster: true), null, out _);
        var w = f.Model.Get(Guid("foreign-test", "gable"))!;
        Assert.Equal(IfcClass.Wall, w.Class);
        var bands = Walls.Section(f.Model, w);
        Assert.Equal(2.4, bands[0].Z1!.Value, 4);
        Assert.Null(bands[1].Z1);
        Assert.True(CheckOf(r, w.Id).Pass, CheckOf(r, w.Id).Reason);
    }

    [Fact]
    public void A_wall_whose_part_is_notched_stays_a_reference_with_the_reason()
    {
        var x = new Foreign();
        PartsWall(x, "notched", 0, 0, 0, 6000, pl =>
        [
            Part(x, "notched-core", "Masonry", pl, Gable(0, 6000), 0, 200),
            // A second layer whose bottom steps up half way: no layer's bottom is so.
            Part(x, "notched-finish", "Plaster", pl, [(0, 0), (3000, 0), (3000, 500), (6000, 500), (6000, 2500), (3000, 4000), (0, 2500)], 200, 220),
        ]);
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "notched.ifc")), null, out _);
        // The wall has no body of its own: its parts come as the references they are.
        Assert.Null(f.Model.Get(Guid("foreign-test", "notched")));
        Assert.Equal(2, r.ReferencesByClass!.GetValueOrDefault("IfcBuildingElementPart"));
        Assert.NotEqual(0, r.Skipped!.GetValueOrDefault("a wall whose layers' bottoms are not flat (stepped or sloped)"));
        Assert.Equal(0, r.RecognizedByClass?.GetValueOrDefault("IfcWall") ?? 0);
    }

    /// <summary>
    /// Two walls lying one over the other (b and c, as an export may write a wall twice) and a third, a, ending on their
    /// face: a T with either leaves all three as the file has them; the file says a joins c, and so it does. Without
    /// the file's word the nearest first, b.
    /// </summary>
    [Fact]
    public void The_file_s_connection_is_the_join_tried_first()
    {
        Relation? Join(bool told, string other)
        {
            var x = new Foreign();
            var a = FootprintWall(x, "a", (2900, 150), (3100, 150), (3100, 4000), (2900, 4000));
            FootprintWall(x, "b", (0, -150), (6000, -150), (6000, 150), (0, 150));
            var c = FootprintWall(x, "c", (0, -150), (6000, -150), (6000, 150), (0, 150));
            if (told)
                foreach (var at in new[] { "ATSTART", "ATEND" })
                    x.F.Add("IFCRELCONNECTSPATHELEMENTS", x.G("conn-" + at), Null, Null, Null, Null, c, a, "()", "()", E(at), E("ATPATH"));
            var f = Full();
            IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, $"told-{told}.ifc")), null, out _);
            return Joins.Between(f.Model, Guid("foreign-test", "a"), Guid("foreign-test", other));
        }
        Assert.NotNull(Join(told: true, "c"));
        Assert.Null(Join(told: true, "b"));
        Assert.NotNull(Join(told: false, "b"));
    }
}
