using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// Foreign columns' and beams' sections as the file has them (docs/spec-ifc.md, Recunoașterea, Secțiunile stâlpilor și
/// grinzilor): a regular polygon read as that polygon (a faceted round as a circle only when Lapin's has its area), one
/// file type's elements of another section on a type of their own, a box's prism chosen by its file type's section, a
/// column whose top is of several planes left a reference, and beams joined as the file has them.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>A polygon (mm, in the element's placement) extruded up from <paramref name="z0"/> by <paramref name="h"/>.</summary>
    static string Extruded(Foreign x, IEnumerable<(double X, double Y)> pts, double z0, double h)
    {
        var f = x.F;
        var list = pts.ToList();
        var line = f.Add("IFCPOLYLINE", L(list.Append(list[0]).Select(p => f.Add("IFCCARTESIANPOINT", $"({R(p.X)},{R(p.Y)})"))));
        var at = f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", $"(0.,0.,{R(z0)})"), Null, Null);
        return f.Add("IFCEXTRUDEDAREASOLID", f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, line), at, f.Add("IFCDIRECTION", "(0.,0.,1.)"), R(h));
    }

    /// <summary>A closed B-rep from its points (mm) and its faces (each a loop of indices, wound outwards).</summary>
    static string Brep(Foreign x, (double X, double Y, double Z)[] pts, params int[][] faces)
    {
        var f = x.F;
        var p = pts.Select(q => f.Add("IFCCARTESIANPOINT", $"({R(q.X)},{R(q.Y)},{R(q.Z)})")).ToArray();
        string Face(int[] ix) => f.Add("IFCFACE", L([f.Add("IFCFACEOUTERBOUND", f.Add("IFCPOLYLOOP", L(ix.Select(i => p[i]))), True)]));
        return f.Add("IFCFACETEDBREP", f.Add("IFCCLOSEDSHELL", L(faces.Select(Face))));
    }

    /// <summary>A regular polygon's corners (mm): <paramref name="n"/> on a circle of radius <paramref name="r"/>, the first at <paramref name="startDeg"/>.</summary>
    static IEnumerable<(double X, double Y)> Ngon(int n, double r, double startDeg = 0) =>
        Enumerable.Range(0, n).Select(i => (r * Math.Cos((startDeg + 360.0 * i / n) * Math.PI / 180), r * Math.Sin((startDeg + 360.0 * i / n) * Math.PI / 180)));

    /// <summary>An octagonal prism as a B-rep (as BURKWIL's 25 mm posts are): the bottom, the top and the eight sides.</summary>
    static string BrepNgon(Foreign x, int n, double r, double z0, double z1)
    {
        var ring = Ngon(n, r).ToList();
        var pts = ring.Select(p => (p.X, p.Y, z0)).Concat(ring.Select(p => (p.X, p.Y, z1))).ToArray();
        var faces = new List<int[]> { Enumerable.Range(0, n).Reverse().ToArray(), Enumerable.Range(n, n).ToArray() };
        for (var i = 0; i < n; i++) faces.Add([i, (i + 1) % n, n + (i + 1) % n, n + i]);
        return Brep(x, pts, [.. faces]);
    }

    static void Typed(Foreign x, string key, string name, string ifcType, string predefined, params string[] elements)
    {
        var type = x.F.Add(ifcType, x.G(key), Null, S(name), Null, Null, Null, Null, Null, Null, E(predefined));
        x.F.Add("IFCRELDEFINESBYTYPE", x.G(key + "-rel"), Null, Null, Null, L(elements), type);
    }

    [Fact]
    public void An_octagonal_post_comes_in_as_an_octagon_and_a_faceted_round_column_as_a_circle()
    {
        var x = new Foreign();
        // A post 25 mm across its corners, 1 m high, as a B-rep; one extruded from an octagon turned 10°; a round column
        // 400 mm across drawn with 48 sides.
        x.Element("IFCCOLUMN", "post", "Post", x.Place(0, 0, 0), x.Shape(BrepNgon(x, 8, 12.5, 0, 1000), "Brep"), E("COLUMN"));
        x.Element("IFCCOLUMN", "turned", "Turned", x.Place(1000, 0, 0), x.Shape(Extruded(x, Ngon(8, 150, 10), 0, 3000)), E("COLUMN"));
        x.Element("IFCCOLUMN", "round", "Round", x.Place(3000, 0, 0), x.Shape(Extruded(x, Ngon(48, 200), 0, 3000)), E("COLUMN"));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "posts.ifc")), null, out _);
        var m = f.Model;
        Section SectionOf(string key) => Columns.Spec(m, m.Get(x.Gid(key))!).Section;
        foreach (var key in new[] { "post", "turned", "round" })
        {
            var c = m.Get(x.Gid(key))!;
            Assert.Equal(IfcClass.Column, c.Class);
            Assert.False(StoredBodies.Has(c));
            var check = CheckOf(r, c.Id);
            output.WriteLine($"{key}: {SectionOf(key).Label} {check.ToJson().ToJsonString()}");
            Assert.True(check.Pass, $"{key}: {check.Reason}");
            Assert.InRange(Math.Abs(check.Volume!.Value), 0, 0.005);
        }
        Assert.Equal(new Section("polygon", 0.025, 0.025, Sides: 8), SectionOf("post"));
        Assert.Equal("8-gon 25", SectionOf("post").Label);
        // The turned one: its corner where the file has it (10°, within its side's 45°).
        Assert.Equal(new Section("polygon", 0.3, 0.3, Sides: 8), SectionOf("turned"));
        Assert.Equal(10, m.Get(x.Gid("turned"))!.Num("rotation"), 3);
        Assert.Equal("circle", SectionOf("round").Kind);
        Assert.Equal(0.4, SectionOf("round").Width, 4);
        // The octagon's area is its own (not a circle's, 11 % more), and its type says so as the dialog reads it back.
        Assert.Equal(2 * Math.Sqrt(2) * 0.15 * 0.15, SectionOf("turned").Area, 6);
        var type = m.RelatingOf(x.Gid("post"), IfcClass.RelDefinesByType)!;
        Assert.Equal(8, (int)type.Attrs["section"]!["sides"]!);
        Assert.Equal(SectionOf("post"), Section.Parse(type.Attrs["section"]!.DeepClone()));
    }

    [Fact]
    public void A_regular_polygon_section_is_made_drawn_and_checked_like_any_other()
    {
        var f = Full();
        var s = Section.Parse(new JsonObject { ["kind"] = "polygon", ["width"] = 0.4, ["sides"] = 6 });
        Assert.Equal(0.4, s.Depth);
        Assert.Equal(6, s.Outline().Count);
        Assert.Equal(new Vec2(0.2, 0), s.Outline()[0]);
        Assert.Equal(3 * Math.Sqrt(3) / 2 * 0.04, s.Area, 9);
        Assert.Throws<CommandException>(() => Section.Parse(new JsonObject { ["kind"] = "polygon", ["width"] = 0.4, ["sides"] = 2 }));
        var storey = Spatial.Storeys(f.Model).First().Id;
        var typeId = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "Hex", ["section"] = s.ToJson() })!["id"]!;
        var id = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = storey, ["typeId"] = typeId, ["x"] = 1, ["y"] = 2 })!["id"]!;
        var g = f.Session.Geometry(id)!;
        Assert.Equal(6, g.Plan!.Single(p => p.Kind == "cut").Points.Count);
        Assert.Equal(s.Area * Columns.Spec(f.Model, f.Model.Get(id)!).Height, g.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
    }

    [Fact]
    public void One_file_types_columns_of_another_section_take_a_type_of_their_own_and_a_quarter_turn_keeps_it()
    {
        var x = new Foreign();
        // "S5": 260 × 260, then a piece 45 × 260 of the same type (cut down where a wall runs past it).
        var a = x.Element("IFCCOLUMN", "s5-full", "S5", x.Place(0, 0, 0), x.Shape(x.Box(0, 0, 0, 260, 260, 2960)), E("COLUMN"));
        var b = x.Element("IFCCOLUMN", "s5-part", "S5", x.Place(1000, 0, 0), x.Shape(x.Box(0, 0, 0, 45, 260, 2960)), E("COLUMN"));
        Typed(x, "s5", "S5", "IFCCOLUMNTYPE", "COLUMN", a, b);
        // "S6": 50 × 25, and one drawn 25 × 50 (the same post turned a quarter).
        var c = x.Element("IFCCOLUMN", "s6-a", "S6", x.Place(0, 2000, 0), x.Shape(x.Box(0, 0, 0, 50, 25, 2065)), E("COLUMN"));
        var d = x.Element("IFCCOLUMN", "s6-b", "S6", x.Place(1000, 2000, 0), x.Shape(x.Box(0, 0, 0, 25, 50, 2065)), E("COLUMN"));
        Typed(x, "s6", "S6", "IFCCOLUMNTYPE", "COLUMN", c, d);
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "sections.ifc")), null, out _);
        var m = f.Model;
        Entity TypeOf(string key) => m.RelatingOf(x.Gid(key), IfcClass.RelDefinesByType)!;
        Assert.NotEqual(TypeOf("s5-full").Id, TypeOf("s5-part").Id);
        Assert.Equal("S5", TypeOf("s5-full").Name);
        Assert.StartsWith("S5 ", TypeOf("s5-part").Name);
        Assert.Equal(TypeOf("s6-a").Id, TypeOf("s6-b").Id);
        Assert.Equal(90, Math.Abs(m.Get(x.Gid("s6-b"))!.Num("rotation") - m.Get(x.Gid("s6-a"))!.Num("rotation")), 3);
        foreach (var key in new[] { "s5-full", "s5-part", "s6-a", "s6-b" })
        {
            var check = CheckOf(r, x.Gid(key));
            Assert.True(check.Pass, $"{key}: {check.Reason}");
        }
        // Three types: S5, S5 for its other section, S6.
        Assert.Equal(3, m.OfClass(IfcClass.ColumnType).Count());
    }

    [Fact]
    public void A_beam_stub_is_read_along_its_types_section_and_a_beam_of_another_depth_takes_its_own_type()
    {
        var x = new Foreign();
        // "B1": 150 wide, 200 deep, top at 3 m; a beam 4 m along x, a stub 120 long along y (as a B-rep box, so it is three
        // prisms, the longest of them 150 along x), and one 80 deep (a lintel strip of the same type).
        var a = x.Element("IFCBEAM", "long", "B1", x.Place(0, 0, 0), x.Shape(x.Box(2000, 0, 2800, 4000, 150, 200)), E("BEAM"));
        var b = x.Element("IFCBEAM", "stub", "B1", x.Place(0, 2000, 0), x.Shape(BrepBox(x, -75, 0, 2800, 75, 120, 3000), "Brep"), E("BEAM"));
        var c = x.Element("IFCBEAM", "flat", "B1", x.Place(0, 4000, 0), x.Shape(x.Box(2000, 0, 2920, 4000, 150, 80)), E("BEAM"));
        Typed(x, "b1", "B1", "IFCBEAMTYPE", "BEAM", a, b, c);
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "beams.ifc")), null, out _);
        var m = f.Model;
        Entity TypeOf(string key) => m.RelatingOf(x.Gid(key), IfcClass.RelDefinesByType)!;
        Assert.Equal(TypeOf("long").Id, TypeOf("stub").Id);
        Assert.NotEqual(TypeOf("long").Id, TypeOf("flat").Id);
        Assert.StartsWith("B1 ", TypeOf("flat").Name);
        var stub = m.Get(x.Gid("stub"))!;
        var axis = Args.Point(stub.Attrs["end"], "end") - Args.Point(stub.Attrs["start"], "start");
        Assert.Equal(0.12, axis.Length, 4);
        Assert.Equal(0, axis.X, 4);
        foreach (var key in new[] { "long", "stub", "flat" })
        {
            var check = CheckOf(r, x.Gid(key));
            Assert.True(check.Pass, $"{key}: {check.Reason}");
        }
    }

    [Fact]
    public void A_column_whose_top_is_of_several_planes_stays_a_reference()
    {
        var x = new Foreign();
        // 300 × 300: a ridge along y at 3 m over eaves at 2.8 m; and a notch 260 down its top half (a beam's seat), as BURKWIL's.
        var ridge = Brep(x,
            [(0, 0, 0), (300, 0, 0), (300, 300, 0), (0, 300, 0), (0, 0, 2800), (300, 0, 2800), (300, 300, 2800), (0, 300, 2800), (150, 0, 3000), (150, 300, 3000)],
            [0, 3, 2, 1], [0, 1, 5, 8, 4], [2, 3, 7, 9, 6], [1, 2, 6, 5], [3, 0, 4, 7], [4, 8, 9, 7], [5, 6, 9, 8]);
        x.Element("IFCCOLUMN", "ridge", "Ridge", x.Place(0, 0, 0), x.Shape(ridge, "Brep"), E("COLUMN"));
        var notch = Brep(x,
            [(0, 0, 0), (300, 0, 0), (300, 300, 0), (0, 300, 0), (0, 0, 2700), (300, 0, 2700), (300, 150, 2700), (0, 150, 2700), (300, 150, 2960), (0, 150, 2960), (300, 300, 2960), (0, 300, 2960)],
            [0, 3, 2, 1], [0, 1, 5, 4], [1, 2, 10, 8, 6, 5], [3, 0, 4, 7, 9, 11], [2, 3, 11, 10], [4, 5, 6, 7], [7, 6, 8, 9], [9, 8, 10, 11]);
        x.Element("IFCCOLUMN", "notch", "Notch", x.Place(1000, 0, 0), x.Shape(notch, "Brep"), E("COLUMN"));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "tops.ifc")), null, out _);
        foreach (var key in new[] { "ridge", "notch" })
        {
            var c = f.Model.Get(x.Gid(key))!;
            Assert.True(StoredBodies.Has(c), key);
            Assert.Equal("IfcColumn", c.Str("exportAs") ?? c.Str("ifcClass") ?? c.Class);
            Assert.DoesNotContain(r.Fidelity?.Checks ?? [], k => k.Id == c.Id);
        }
    }

    [Fact]
    public void Beams_meeting_at_a_corner_come_in_joined_as_the_file_has_them()
    {
        var x = new Foreign();
        // Two beams 200 wide, 400 deep, top at 3 m, mitred at the corner (0, 0): one along +x to 4 m, one along +y to 3 m;
        // and a third butting into the first from below, its end on the first's face.
        x.Element("IFCBEAM", "bx", "BX", x.Place(0, 0, 0), x.Shape(Extruded(x, [(-100, -100), (4000, -100), (4000, 100), (100, 100)], 2600, 400)), E("BEAM"));
        x.Element("IFCBEAM", "by", "BY", x.Place(0, 0, 0), x.Shape(Extruded(x, [(-100, -100), (100, 100), (100, 3000), (-100, 3000)], 2600, 400)), E("BEAM"));
        x.Element("IFCBEAM", "bt", "BT", x.Place(0, 0, 0), x.Shape(Extruded(x, [(1900, -2000), (2100, -2000), (2100, -100), (1900, -100)], 2600, 400)), E("BEAM"));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "corner.ifc")), null, out _);
        var m = f.Model;
        foreach (var key in new[] { "bx", "by", "bt" })
        {
            var b = m.Get(x.Gid(key))!;
            Assert.Equal(IfcClass.Beam, b.Class);
            var check = CheckOf(r, b.Id);
            output.WriteLine($"{key}: {check.ToJson().ToJsonString()}");
            Assert.True(check.Pass, $"{key}: {check.Reason}");
        }
        Assert.NotNull(Joins.Between(m, x.Gid("bx"), x.Gid("by")));
        // The butting beam stops on the first's face (a T: its axis to the other's, its body to the face), the first whole.
        Assert.Equal(-0.1, f.Bounds(x.Gid("bt")).Max.Y, 4);
        Assert.Equal(-0.1, f.Bounds(x.Gid("bx")).Min.Y, 4);
        Assert.Equal(4, f.Bounds(x.Gid("bx")).Max.X, 4);
    }
}
