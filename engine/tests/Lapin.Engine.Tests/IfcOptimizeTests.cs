using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Structure;
using Xunit.Abstractions;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// An IFC file optimised into a copy (docs/ifc-optimize.md): everything but geometry the copy keeps as the file wrote
/// it (the header, every IfcRoot with its GlobalId, names, property and quantity sets), its bodies stay within the
/// tolerance (measured from triangle to triangle), identical bodies are shared, unused resources go, and the original
/// is never written. Files of IFC2X3, IFC4 and IFC4X3 written here; LAPIN_IFC_OPTIMIZE=file(s) measures real ones.
/// </summary>
public class IfcOptimizeTests(ITestOutputHelper output) : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-optimize").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>A small file of products in millimetres, each with its body, a style, a property set and a quantity set.</summary>
    sealed class Model
    {
        public readonly Step F = new();
        public readonly string Ctx, Storey, StoreyPl, Style;
        readonly string _schema;
        readonly List<string> _contained = [];
        int _n;

        public Model(string schema)
        {
            _schema = schema;
            var origin = F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
            var world = F.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
            Ctx = F.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
            var mm = F.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
            var project = F.Add("IFCPROJECT", G("project"), Null, S("Optimise me"), Null, Null, Null, Null, L([Ctx]), F.Add("IFCUNITASSIGNMENT", L([mm])));
            var pl = F.Add("IFCLOCALPLACEMENT", Null, world);
            var building = F.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, pl, Null, Null, E("ELEMENT"), Null, Null, Null);
            StoreyPl = F.Add("IFCLOCALPLACEMENT", pl, world);
            Storey = F.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Ground"), Null, Null, StoreyPl, Null, Null, E("ELEMENT"), "0.");
            F.Add("IFCRELAGGREGATES", G("agg1"), Null, Null, Null, project, L([building]));
            F.Add("IFCRELAGGREGATES", G("agg2"), Null, Null, Null, building, L([Storey]));
            var colour = F.Add("IFCCOLOURRGB", Null, "0.8", "0.2", "0.2");
            var rendering = F.Add("IFCSURFACESTYLERENDERING", colour, "0.", Null, Null, Null, Null, Null, Null, E("NOTDEFINED"));
            Style = F.Add("IFCSURFACESTYLE", S("Red"), E("BOTH"), L([rendering]));
            if (schema == "IFC2X3") Style = F.Add("IFCPRESENTATIONSTYLEASSIGNMENT", L([Style]));
        }

        public static string Gid(string key) => Guid("optimize-test", key);
        static string G(string key) => S(Gid(key));

        /// <summary>A product with its body items (styled), placed at x; its property and quantity sets.</summary>
        public string Product(string cls, string name, string repType, double x, params string[] items)
        {
            _n++;
            foreach (var it in items) F.Add("IFCSTYLEDITEM", it, L([Style]), Null);
            var rep = F.Add("IFCSHAPEREPRESENTATION", Ctx, S("Body"), S(repType), L(items));
            var shape = F.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([rep]));
            var at = F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", $"({R(x)},0.,0.)"), Null, Null);
            var placement = F.Add("IFCLOCALPLACEMENT", StoreyPl, at);
            var product = cls is "IFCWALL" or "IFCBEAM" or "IFCCOLUMN" && _schema == "IFC2X3"
                ? F.Add(cls, G(name), Null, S(name), S("A wall"), Null, placement, shape, S($"T{_n}"))
                : F.Add(cls, G(name), Null, S(name), S("Something"), Null, placement, shape, S($"T{_n}"), Null);
            var prop = F.Add("IFCPROPERTYSINGLEVALUE", S("Mark"), Null, Typed("IFCLABEL", S($"M-{_n} ăș")), Null);
            var len = F.Add("IFCPROPERTYSINGLEVALUE", S("Width"), Null, Typed("IFCLENGTHMEASURE", R(250.5)), Null);
            var pset = F.Add("IFCPROPERTYSET", G(name + "-pset"), Null, S("Pset_Custom"), Null, L([prop, len]));
            F.Add("IFCRELDEFINESBYPROPERTIES", G(name + "-rel"), Null, Null, Null, L([product]), pset);
            var q = _schema == "IFC2X3" ? F.Add("IFCQUANTITYLENGTH", S("Length"), Null, Null, R(1234.5)) : F.Add("IFCQUANTITYLENGTH", S("Length"), Null, Null, R(1234.5), Null);
            var qset = F.Add("IFCELEMENTQUANTITY", G(name + "-qto"), Null, S("Qto_Base"), Null, Null, L([q]));
            F.Add("IFCRELDEFINESBYPROPERTIES", G(name + "-qrel"), Null, Null, Null, L([product]), qset);
            _contained.Add(product);
            return product;
        }

        /// <summary>A triangulated face set of the schema's attribute order; normals when given.</summary>
        public string FaceSet(IReadOnlyList<Vec3> pts, IReadOnlyList<(int, int, int)> tris, bool closed, string? normals = null)
        {
            var list = _schema.StartsWith("IFC4X3") ? F.Add("IFCCARTESIANPOINTLIST3D", Coords(pts), Null) : F.Add("IFCCARTESIANPOINTLIST3D", Coords(pts));
            var idx = L(tris.Select(t => $"({t.Item1 + 1},{t.Item2 + 1},{t.Item3 + 1})"));
            var c = closed ? True : False;
            return _schema.StartsWith("IFC4X3")
                ? F.Add("IFCTRIANGULATEDFACESET", list, c, normals ?? Null, idx, Null)
                : F.Add("IFCTRIANGULATEDFACESET", list, normals ?? Null, c, idx, Null);
        }

        /// <summary>A faceted B-rep: each triangle a face; with <paramref name="ownPoints"/> every face its own points (as some programs write them).</summary>
        public string Brep(IReadOnlyList<Vec3> pts, IReadOnlyList<(int, int, int)> tris, bool ownPoints)
        {
            var shared = ownPoints ? null : pts.Select(P).ToArray();
            var faces = tris.Select(t =>
            {
                var ring = ownPoints ? new[] { P(pts[t.Item1]), P(pts[t.Item2]), P(pts[t.Item3]) } : [shared![t.Item1], shared[t.Item2], shared[t.Item3]];
                var loop = F.Add("IFCPOLYLOOP", L(ring));
                return F.Add("IFCFACE", L([F.Add("IFCFACEOUTERBOUND", loop, True)]));
            }).ToList();
            return F.Add("IFCFACETEDBREP", F.Add("IFCCLOSEDSHELL", L(faces)));
        }

        /// <summary>A faceted B-rep of quads, each face its own four points (a box written face by face).</summary>
        public string QuadBrep(IReadOnlyList<Vec3> pts, IReadOnlyList<int[]> quads)
        {
            var faces = quads.Select(q => F.Add("IFCFACE", L([F.Add("IFCFACEOUTERBOUND", F.Add("IFCPOLYLOOP", L(q.Select(i => P(pts[i])))), True)]))).ToList();
            return F.Add("IFCFACETEDBREP", F.Add("IFCCLOSEDSHELL", L(faces)));
        }

        string P(Vec3 p) => F.Add("IFCCARTESIANPOINT", $"({R(p.X)},{R(p.Y)},{R(p.Z)})");

        public string Extrusion(double w, double d, double h)
        {
            var profile = F.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, F.Add("IFCAXIS2PLACEMENT2D", F.Add("IFCCARTESIANPOINT", "(0.,0.)"), Null), R(w), R(d));
            var at = F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null);
            return F.Add("IFCEXTRUDEDAREASOLID", profile, at, F.Add("IFCDIRECTION", "(0.,0.,1.)"), R(h));
        }

        static string Coords(IReadOnlyList<Vec3> pts) => L(pts.Select(p => $"({R(p.X)},{R(p.Y)},{R(p.Z)})"));

        public byte[] Bytes()
        {
            F.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", G("contained"), Null, Null, Null, L(_contained), Storey);
            using var ms = new MemoryStream();
            F.Write(ms, "optimize.ifc", "ViewDefinition [CoordinationView]", _schema);
            return ms.ToArray();
        }
    }

    // ─── Meshes ─────────────────────────────────────────────────────────────

    /// <summary>A grid of n × n quads over size × size (mm), its height z(x, y).</summary>
    static (List<Vec3>, List<(int, int, int)>) Grid(int n, double size, Func<double, double, double> z)
    {
        var p = new List<Vec3>();
        for (var j = 0; j <= n; j++) for (var i = 0; i <= n; i++) { var (x, y) = (size * i / n, size * j / n); p.Add(new Vec3(x, y, z(x, y))); }
        var t = new List<(int, int, int)>();
        for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                t.Add((a, b, d)); t.Add((a, d, c));
            }
        return (p, t);
    }

    /// <summary>A closed cylinder of <paramref name="sides"/> sides, radius r, height h (mm), moved by o.</summary>
    static (List<Vec3>, List<(int, int, int)>) Cylinder(int sides, double r, double h, Vec3 o)
    {
        var p = new List<Vec3>();
        for (var k = 0; k < sides; k++) { var a = 2 * Math.PI * k / sides; p.Add(o + new Vec3(r * Math.Cos(a), r * Math.Sin(a), 0)); p.Add(o + new Vec3(r * Math.Cos(a), r * Math.Sin(a), h)); }
        p.Add(o); p.Add(o + new Vec3(0, 0, h));
        var t = new List<(int, int, int)>();
        int bc = 2 * sides, tc = bc + 1;
        for (var k = 0; k < sides; k++)
        {
            int a0 = 2 * k, a1 = a0 + 1, b0 = 2 * ((k + 1) % sides), b1 = b0 + 1;
            t.Add((a0, b0, b1)); t.Add((a0, b1, a1)); t.Add((bc, b0, a0)); t.Add((tc, a1, b1));
        }
        return (p, t);
    }

    static readonly Vec3[] BoxPts = [new(0, 0, 0), new(1000, 0, 0), new(1000, 500, 0), new(0, 500, 0), new(0, 0, 300), new(1000, 0, 300), new(1000, 500, 300), new(0, 500, 300)];
    static readonly int[][] BoxQuads = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];

    static byte[] Ifc4(string schema = "IFC4")
    {
        var m = new Model(schema);
        // A flat plate of 800 triangles, a wavy roof, a cylinder three times (twice moved), a box with every corner repeated and bad triangles.
        var (gp, gt) = Grid(20, 1000, (_, _) => 0);
        m.Product("IFCSLAB", "plate", "Tessellation", 0, m.FaceSet(gp, gt, false));
        var (wp, wt) = Grid(30, 3000, (x, y) => 200 * Math.Sin(x / 600) * Math.Cos(y / 900));
        m.Product("IFCROOF", "wavy", "Tessellation", 0, m.FaceSet(wp, wt, false));
        var (cp, ct) = Cylinder(48, 150, 3000, Vec3.Zero);
        m.Product("IFCCOLUMN", "column-1", "Tessellation", 0, m.FaceSet(cp, ct, true));
        m.Product("IFCCOLUMN", "column-2", "Tessellation", 4000, m.FaceSet(cp, ct, true));
        var (mp, mt) = Cylinder(48, 150, 3000, new Vec3(250, 1300, 0));
        m.Product("IFCCOLUMN", "column-3", "Tessellation", 0, m.FaceSet(mp, mt, true));
        var soup = new List<Vec3>(); var st = new List<(int, int, int)>();
        foreach (var q in BoxQuads)
        {
            var b = soup.Count;
            foreach (var i in q) soup.Add(BoxPts[i]);
            st.Add((b, b + 1, b + 2)); st.Add((b, b + 2, b + 3));
        }
        st.Add(st[0]);                                   // a triangle repeated
        st.Add((0, 0, 1));                               // no area
        soup.Add(new Vec3(9999, 9999, 9999));            // a corner no triangle uses
        m.Product("IFCBEAM", "box", "Tessellation", 0, m.FaceSet(soup, st, true));
        // With its own normals: kept as it is.
        var (np, nt) = Grid(6, 500, (_, _) => 0);
        m.Product("IFCPLATE", "normals", "Tessellation", 0, m.FaceSet(np, nt, false, L(np.Select(_ => "(0.,0.,1.)"))));
        m.Product("IFCWALL", "extruded", "SweptSolid", 0, m.Extrusion(5000, 250, 3000));
        // Orphans: points and a direction nothing uses.
        m.F.Add("IFCCARTESIANPOINT", "(1.,2.,3.)"); m.F.Add("IFCCARTESIANPOINT", "(4.,5.,6.)"); m.F.Add("IFCDIRECTION", "(1.,0.,0.)");
        return m.Bytes();
    }

    static byte[] Ifc2x3()
    {
        var m = new Model("IFC2X3");
        var (gp, gt) = Grid(10, 1000, (_, _) => 0);
        m.Product("IFCSLAB", "plate", "Brep", 0, m.Brep(gp, gt, false));
        m.Product("IFCBEAM", "box", "Brep", 0, m.QuadBrep(BoxPts, BoxQuads));
        var (cp, ct) = Cylinder(32, 150, 3000, Vec3.Zero);
        m.Product("IFCCOLUMN", "column-1", "Brep", 0, m.Brep(cp, ct, false));
        var (mp, mt) = Cylinder(32, 150, 3000, new Vec3(800, 0, 0));
        m.Product("IFCCOLUMN", "column-2", "Brep", 0, m.Brep(mp, mt, false));
        m.Product("IFCWALL", "extruded", "SweptSolid", 0, m.Extrusion(5000, 250, 3000));
        m.F.Add("IFCCARTESIANPOINT", "(1.,2.,3.)");
        return m.Bytes();
    }

    static (byte[] Bytes, IfcOptimizeReport Report) Optimize(byte[] input, IfcOptimizeOptions? o = null)
    {
        using var ms = new MemoryStream();
        var r = IfcOptimize.Run(input, ms, o ?? new IfcOptimizeOptions());
        return (ms.ToArray(), r);
    }

    /// <summary>Geometry and its presentation: what the pass may change. Everything else must come out byte for byte.</summary>
    static readonly HashSet<string> Geometry = [.. IfcOptimize.Resources, "IFCSTYLEDITEM", "IFCPRESENTATIONLAYERASSIGNMENT", "IFCPRESENTATIONLAYERWITHSTYLE"];

    /// <summary>Every entity that is not geometry identical in the copy; the header too; every GlobalId and property the same.</summary>
    static void AssertKept(byte[] before, byte[] after)
    {
        var a = IfcOptimize.Entities(before);
        var b = IfcOptimize.Entities(after);
        foreach (var (id, (type, text)) in a)
        {
            if (Geometry.Contains(type)) continue;
            Assert.True(b.TryGetValue(id, out var o), $"#{id} {type} is gone");
            Assert.Equal(text, o.Text);
        }
        int Header(byte[] x) => x.AsSpan().IndexOf("DATA;"u8);
        Assert.True(before.AsSpan(0, Header(before)).SequenceEqual(after.AsSpan(0, Header(after))), "the header changed");
        var fa = new IfcModel(StepFile.Read(before));
        var fb = new IfcModel(StepFile.Read(after));
        Assert.Equal(fa.Products.Select(p => (p.GlobalId, p.Name, p.Class, p.Tag)), fb.Products.Select(p => (p.GlobalId, p.Name, p.Class, p.Tag)));
        Assert.Equal(StepFile.Read(before).Rooted.Select(e => e.Id), StepFile.Read(after).Rooted.Select(e => e.Id));
        foreach (var p in fa.Products)
        {
            var pa = fa.Properties(p.Id);
            var pb = fb.Properties(p.Id);
            Assert.Equal(pa.Keys.Order(), pb.Keys.Order());
            foreach (var (set, props) in pa) Assert.Equal(props.OrderBy(k => k.Key), pb[set].OrderBy(k => k.Key));
        }
        // Nothing Lapin's own added (a file Lapin wrote has its own sets, kept as they are).
        static int Lapin(byte[] x) => System.Text.RegularExpressions.Regex.Count(Encoding.Latin1.GetString(x), "Lapin_");
        Assert.Equal(Lapin(before), Lapin(after));
    }

    /// <summary>The most a product's body strays between the two files, measured both ways from corners, edge middles and centres (m).</summary>
    static double Deviation(byte[] before, byte[] after)
    {
        var fa = new IfcModel(StepFile.Read(before));
        var fb = new IfcModel(StepFile.Read(after));
        var worst = 0.0;
        foreach (var p in fa.Products)
        {
            var ma = Mesh(fa, p.Id);
            var mb = Mesh(fb, p.Id);
            Assert.Equal(ma.T.Count == 0, mb.T.Count == 0);
            if (ma.T.Count == 0) continue;
            worst = Math.Max(worst, Math.Max(Stray(ma, mb), Stray(mb, ma)));
        }
        return worst;
    }

    static MeshBuilder Mesh(IfcModel m, int product)
    {
        var o = new MeshBuilder();
        foreach (var (mesh, _) in m.Shape(product).Body) o.Append(mesh);
        // Without the triangles that have no area (the original's bad ones): they hold no surface to measure.
        var kept = o.T.Where(t => Vec3.Cross(o.V[t.B] - o.V[t.A], o.V[t.C] - o.V[t.A]).Length > 1e-12).ToList();
        o.T.Clear(); o.T.AddRange(kept);
        return o;
    }

    static double Stray(MeshBuilder from, MeshBuilder to)
    {
        var worst = 0.0;
        foreach (var (a, b, c) in from.T)
        {
            var (pa, pb, pc) = (from.V[a], from.V[b], from.V[c]);
            foreach (var s in new[] { pa, pb, pc, (pa + pb) * 0.5, (pb + pc) * 0.5, (pc + pa) * 0.5, (pa + pb + pc) * (1.0 / 3) })
                worst = Math.Max(worst, to.T.Min(t => PointTriangle(s, to.V[t.A], to.V[t.B], to.V[t.C])));
        }
        return worst;
    }

    static double PointTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        Vec3 ab = b - a, ac = c - a, ap = p - a;
        double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return (p - a).Length;
        var bp = p - b; double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return (p - b).Length;
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return (p - (a + ab * (d1 / (d1 - d3)))).Length;
        var cp = p - c; double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return (p - c).Length;
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return (p - (a + ac * (d2 / (d2 - d6)))).Length;
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return (p - (b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6))))).Length;
        var den = 1 / (va + vb + vc);
        return (p - (a + ab * (vb * den) + ac * (vc * den))).Length;
    }

    static int CountOf(byte[] bytes, string type) => IfcOptimize.Entities(bytes).Values.Count(e => e.Type == type);

    // ─── The tests ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC4X3_ADD2")]
    public void An_ifc4_copy_keeps_everything_but_its_geometry_which_stays_within_the_tolerance(string schema)
    {
        var input = Ifc4(schema);
        var (copy, r) = Optimize(input);
        output.WriteLine(r.ToJson().ToJsonString());
        AssertKept(input, copy);
        Assert.Equal(schema, StepFile.Read(copy).Schema);
        Assert.True(copy.Length < input.Length * 0.7, $"{copy.Length} of {input.Length} bytes");
        Assert.True(r.TrianglesAfter < r.TrianglesBefore * 0.7, $"{r.TrianglesAfter} of {r.TrianglesBefore} triangles");
        Assert.True(r.Simplified >= 2 && r.Cleaned >= 1);
        // The cylinder three times: one body, three mapped items (one moved within its product's coordinates).
        Assert.Equal(1, r.SharedBodies);
        Assert.Equal(3, r.Shared);
        Assert.Equal(1, CountOf(copy, "IFCREPRESENTATIONMAP"));
        Assert.Equal(3, CountOf(copy, "IFCMAPPEDITEM"));
        // Orphans: the two points and the direction, the cylinders' face sets left unused and their styled items.
        Assert.True(r.Orphans >= 3);
        Assert.DoesNotContain(IfcOptimize.Entities(copy).Values, e => e.Text.Contains("(1.,2.,3.)") || e.Text.Contains("(9999.,9999.,9999.)"));
        // Measured both ways, within the tolerance (1 mm) and as the report says.
        var dev = Deviation(input, copy);
        output.WriteLine($"deviation {dev * 1000:0.0000} mm, reported {r.DeviationMm:0.0000} mm");
        Assert.True(dev <= 0.001 + 1e-7, $"{dev * 1000} mm");
        Assert.True(r.DeviationMm <= 1 + 1e-9);
        Assert.Equal(r.EntitiesAfter, IfcOptimize.Entities(copy).Count);
        Assert.Equal(r.BytesAfter, copy.Length);
    }

    [Fact]
    public void A_face_set_with_its_own_normals_and_an_extrusion_come_out_as_written()
    {
        var input = Ifc4();
        var (copy, _) = Optimize(input);
        var a = IfcOptimize.Entities(input);
        var b = IfcOptimize.Entities(copy);
        var withNormals = a.First(e => e.Value.Type == "IFCTRIANGULATEDFACESET" && e.Value.Text.Contains("(0.,0.,1.)"));
        Assert.Equal(withNormals.Value.Text, b[withNormals.Key].Text);
        foreach (var e in a.Where(e => e.Value.Type is "IFCEXTRUDEDAREASOLID" or "IFCRECTANGLEPROFILEDEF")) Assert.Equal(e.Value.Text, b[e.Key].Text);
    }

    [Fact]
    public void A_file_without_meshes_or_orphans_comes_out_byte_for_byte()
    {
        var m = new Model("IFC4");
        m.Product("IFCWALL", "w1", "SweptSolid", 0, m.Extrusion(5000, 250, 3000));
        m.Product("IFCWALL", "w2", "SweptSolid", 6000, m.Extrusion(3000, 250, 3000));
        var input = m.Bytes();
        var (copy, r) = Optimize(input);
        Assert.Equal(input, copy);
        Assert.Equal(0, r.Orphans);
        // And in IFC2X3 with its windows' line ends.
        var crlf = Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(Ifc2x3()).Replace("\n", "\r\n"));
        var (c2, _) = Optimize(crlf, new IfcOptimizeOptions { Simplify = false, Clean = false, Share = false, Orphans = false });
        Assert.Equal(crlf, c2);
    }

    [Fact]
    public void An_ifc2x3_copy_keeps_its_breps_welded_simplified_and_shared()
    {
        var input = Ifc2x3();
        var (copy, r) = Optimize(input);
        output.WriteLine(r.ToJson().ToJsonString());
        AssertKept(input, copy);
        Assert.Equal("IFC2X3", StepFile.Read(copy).Schema);
        Assert.Equal(0, CountOf(copy, "IFCTRIANGULATEDFACESET"));
        // Four products, the second column's B-rep gone into the first's map.
        Assert.Equal(3, CountOf(copy, "IFCFACETEDBREP"));
        // The box's 24 points (each face its own) welded into 8; the plate's 200 faces down to a handful.
        Assert.True(r.Cleaned >= 1);
        Assert.True(r.Simplified >= 1);
        Assert.Equal(1, r.SharedBodies);
        Assert.True(CountOf(copy, "IFCCARTESIANPOINT") < CountOf(input, "IFCCARTESIANPOINT") / 2);
        Assert.True(Deviation(input, copy) <= 0.001 + 1e-7);
        // Read back as Lapin reads it: every product has its body.
        var model = new IfcModel(StepFile.Read(copy));
        Assert.All(model.Products, p => Assert.NotEmpty(Mesh(model, p.Id).T));
    }

    [Fact]
    public void Faceted_breps_become_face_sets_in_ifc4_only_when_asked()
    {
        var m = new Model("IFC4");
        var (cp, ct) = Cylinder(32, 150, 3000, Vec3.Zero);
        m.Product("IFCCOLUMN", "c", "Brep", 0, m.Brep(cp, ct, false));
        var input = m.Bytes();
        var (kept, _) = Optimize(input);
        Assert.Equal(1, CountOf(kept, "IFCFACETEDBREP"));
        var (copy, r) = Optimize(input, new IfcOptimizeOptions { BrepsToFaceSets = true });
        Assert.Equal(1, r.Converted);
        Assert.Equal(0, CountOf(copy, "IFCFACETEDBREP"));
        Assert.Equal(1, CountOf(copy, "IFCTRIANGULATEDFACESET"));
        Assert.Contains(IfcOptimize.Entities(copy).Values, e => e.Type == "IFCSHAPEREPRESENTATION" && e.Text.Contains("'Tessellation'"));
        AssertKept(input, copy);
        Assert.True(Deviation(input, copy) <= 0.001 + 1e-7);
        // IFC2X3 keeps its B-reps whatever is asked.
        var (old, r2) = Optimize(Ifc2x3(), new IfcOptimizeOptions { BrepsToFaceSets = true });
        Assert.Equal(0, r2.Converted);
        Assert.Equal(0, CountOf(old, "IFCTRIANGULATEDFACESET"));
    }

    [Fact]
    public void Orphans_go_only_when_asked_and_never_what_is_referenced()
    {
        var input = Ifc4();
        var (copy, _) = Optimize(input, new IfcOptimizeOptions { Simplify = false, Clean = false, Share = false, Orphans = false });
        Assert.Equal(input, copy);
        var (clean, r) = Optimize(input, new IfcOptimizeOptions { Simplify = false, Clean = false, Share = false });
        Assert.Equal(3, r.Orphans);
        // Whatever is left references only what is there.
        var e = IfcOptimize.Entities(clean);
        foreach (var (_, text) in e.Values)
            foreach (System.Text.RegularExpressions.Match ref_ in System.Text.RegularExpressions.Regex.Matches(text[(text.IndexOf('=') + 1)..], @"#(\d+)"))
                Assert.True(e.ContainsKey(int.Parse(ref_.Groups[1].Value)), $"{text} points at nothing");
    }

    [Fact]
    public void Every_copy_references_only_what_it_holds()
    {
        foreach (var input in new[] { Ifc4(), Ifc4("IFC4X3_ADD2"), Ifc2x3() })
            foreach (var o in new[] { new IfcOptimizeOptions(), new IfcOptimizeOptions { BrepsToFaceSets = true, RoundMm = 0.01 }, new IfcOptimizeOptions { ToleranceMm = 5 } })
            {
                var (copy, _) = Optimize(input, o);
                var e = IfcOptimize.Entities(copy);
                foreach (var (_, text) in e.Values)
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text[(text.IndexOf('=') + 1)..], @"#(\d+)"))
                        Assert.True(e.ContainsKey(int.Parse(m.Groups[1].Value)), $"{text} points at nothing");
                AssertKept(input, copy);
            }
    }

    [Fact]
    public void The_copy_is_named_with_its_ending_and_never_the_original()
    {
        var source = Path.Combine(_dir, "house.ifc");
        File.WriteAllBytes(source, Ifc4());
        var before = File.ReadAllBytes(source);
        Assert.Equal(Path.Combine(_dir, "house.optimized.ifc"), IfcOptimize.Target(source, IfcOptimize.DefaultSuffix, null, false));
        Assert.Equal(Path.Combine(_dir, "out", "house-lite.ifczip"), IfcOptimize.Target(source, "-lite", Path.Combine(_dir, "out"), true));
        Assert.Null(IfcOptimize.CleanSuffix("  "));
        Assert.Equal("_v2", IfcOptimize.CleanSuffix("_v2.ifc"));
        Assert.Throws<InvalidOperationException>(() => IfcOptimize.RunFile(source, source, new IfcOptimizeOptions()));
        var target = IfcOptimize.Target(source, ".optimized", null, false);
        var r = IfcOptimize.RunFile(source, target, new IfcOptimizeOptions());
        Assert.Equal(before, File.ReadAllBytes(source));
        Assert.Equal(new FileInfo(target).Length, r.BytesAfter);
        Assert.False(File.Exists(target + ".part"));
        // Zipped: the .ifc inside, read back the same.
        var zipped = IfcOptimize.Target(source, ".optimized", null, true);
        IfcOptimize.RunFile(source, zipped, new IfcOptimizeOptions { Zip = true });
        using (var zip = ZipFile.OpenRead(zipped)) Assert.Equal("house.ifc", Assert.Single(zip.Entries).Name);
        Assert.Equal(File.ReadAllBytes(target), IfcOptimize.ReadSource(zipped));
        // A cancelled pass leaves nothing behind.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var other = Path.Combine(_dir, "cancelled.ifc");
        Assert.ThrowsAny<OperationCanceledException>(() => IfcOptimize.RunFile(source, other, new IfcOptimizeOptions(), null, cts.Token));
        Assert.False(File.Exists(other));
    }

    [Fact]
    public void The_copy_imports_into_lapin_with_the_same_globalids()
    {
        var input = Ifc4();
        var (copy, _) = Optimize(input);
        HashSet<string> Ids(byte[] bytes) => ImportedIds(bytes, input);
        var a = Ids(input);
        Assert.Contains(Model.Gid("column-2"), a);
        Assert.Equal(a.Order(), Ids(copy).Order());
    }

    /// <summary>The file's own GlobalIds (of <paramref name="of"/>) an import of <paramref name="bytes"/> into Lapin keeps (Lapin makes its own for what it adds).</summary>
    static HashSet<string> ImportedIds(byte[] bytes, byte[] of)
    {
        var own = StepFile.Read(of).Rooted.Select(e => e[0] as string).OfType<string>().ToHashSet();
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        IfcImport.Run(f.Session, bytes, "x.ifc", new IfcImportOptions("reference"), out _);
        return [.. f.Model.Entities.Select(e => e.Id).Concat(f.Model.Relations.Select(r => r.Id)).Where(own.Contains)];
    }

    /// <summary>Real files, when given (LAPIN_IFC_OPTIMIZE=a.ifc;b.ifc; the copies into LAPIN_IFC_OPTIMIZE_OUT): the numbers for docs/ifc-optimize.md.</summary>
    [Fact]
    public void Measure_when_given()
    {
        var files = Environment.GetEnvironmentVariable("LAPIN_IFC_OPTIMIZE");
        if (string.IsNullOrEmpty(files)) return;
        var outDir = Environment.GetEnvironmentVariable("LAPIN_IFC_OPTIMIZE_OUT") ?? _dir;
        foreach (var path in files.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var target = IfcOptimize.Target(path, IfcOptimize.DefaultSuffix, outDir, false);
            var sw = Stopwatch.StartNew();
            var last = ""; var lap = Stopwatch.StartNew();
            var r = IfcOptimize.RunFile(path, target, IfcOptimizeOptions.FromJson(null), (stage, _, _) =>
            {
                if (stage == last) return;
                Console.WriteLine($"  {last,-28} {lap.Elapsed.TotalSeconds,6:0.00} s");
                last = stage; lap.Restart();
            });
            Console.WriteLine($"  {last,-28} {lap.Elapsed.TotalSeconds,6:0.00} s");
            var line = $"{Path.GetFileName(path)}: {r.ToJson().ToJsonString()} ({sw.Elapsed.TotalSeconds:0.0} s)";
            output.WriteLine(line); Console.WriteLine(line);
            var before = File.ReadAllBytes(path);
            var after = File.ReadAllBytes(target);
            AssertKept(before, after);
            if (before.Length < 20 << 20)
            {
                var dev = Deviation(before, after);
                Console.WriteLine($"  deviation measured product by product: {dev * 1000:0.000} mm");
                var ia = ImportedIds(before, before).Order().ToList();
                var ib = ImportedIds(after, before).Order().ToList();
                Console.WriteLine($"  imported into Lapin: {ia.Count} and {ib.Count} of the file's GlobalIds");
                Assert.Equal(ia, ib);
            }
        }
    }
}
