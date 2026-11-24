using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// Columns and beams whose IFC body has no profile (a faceted B-rep, a triangulated face set) recognised with their body's own
/// outline as their section, as a Speckle receive's are (decision 826): the type's catalogue profile when it is the outline to
/// 2 mm, a parametric shape when the outline is one, else the polygon it is; the fidelity gate judging each, enforced.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>A polygon (mm) in the plane across <paramref name="sweep"/> swept by it, as a closed faceted B-rep: the polygon is wound about the sweep.</summary>
    static string BrepPrism(Foreign x, IReadOnlyList<(double X, double Y, double Z)> ring, (double X, double Y, double Z) sweep)
    {
        var n = ring.Count;
        var pts = ring.Concat(ring.Select(p => (p.X + sweep.X, p.Y + sweep.Y, p.Z + sweep.Z))).ToArray();
        var faces = new List<int[]> { Enumerable.Range(0, n).Reverse().ToArray(), Enumerable.Range(n, n).ToArray() };
        for (var i = 0; i < n; i++) faces.Add([i, (i + 1) % n, n + (i + 1) % n, n + i]);
        return Brep(x, pts, [.. faces]);
    }

    /// <summary>The same prism as an IfcTriangulatedFaceSet (the caps fanned from their first corner: convex rings only).</summary>
    static string TriangulatedPrism(Foreign x, IReadOnlyList<(double X, double Y, double Z)> ring, (double X, double Y, double Z) sweep)
    {
        var n = ring.Count;
        var pts = ring.Concat(ring.Select(p => (X: p.X + sweep.X, Y: p.Y + sweep.Y, Z: p.Z + sweep.Z))).ToList();
        var tris = new List<(int, int, int)>();
        for (var i = 1; i + 1 < n; i++) { tris.Add((0, i + 1, i)); tris.Add((n, n + i, n + i + 1)); }
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            tris.Add((i, j, n + j));
            tris.Add((i, n + j, n + i));
        }
        var list = x.F.Add("IFCCARTESIANPOINTLIST3D", "(" + string.Join(",", pts.Select(p => $"({R(p.X)},{R(p.Y)},{R(p.Z)})")) + ")");
        return x.F.Add("IFCTRIANGULATEDFACESET", list, Null, ".T.", "(" + string.Join(",", tris.Select(t => $"({t.Item1 + 1},{t.Item2 + 1},{t.Item3 + 1})")) + ")", Null);
    }

    static List<(double X, double Y, double Z)> Plan(IEnumerable<(double X, double Y)> pts, double z) => [.. pts.Select(p => (p.X, p.Y, z))];

    [Fact]
    public void A_column_or_beam_body_with_no_profile_comes_in_with_its_own_outline_as_its_section()
    {
        var x = new Foreign();
        // An L-shaped corner column 600 × 600, its arms 250 thick, 3 m high, as a faceted B-rep.
        var corner = new (double X, double Y)[] { (0, 0), (600, 0), (600, 250), (250, 250), (250, 600), (0, 600) };
        x.Element("IFCCOLUMN", "corner", "Corner column", x.Place(0, 0, 0), x.Shape(BrepPrism(x, Plan(corner, 0), (0, 0, 3000)), "Brep"), E("COLUMN"));
        // A column 300 × 500 as a triangulated face set.
        var rect = new (double X, double Y)[] { (0, 0), (300, 0), (300, 500), (0, 500) };
        x.Element("IFCCOLUMN", "tris", "Triangulated column", x.Place(3000, 0, 0), x.Shape(TriangulatedPrism(x, Plan(rect, 0), (0, 0, 3000)), "Tessellation"), E("COLUMN"));
        // A beam of the catalogue's HEA 200, its type named so, 4 m along x as a faceted B-rep, its top at 3 m.
        var hea = Section.Parse(Lapin.Platform.ProfileLibraries.BuiltInProfiles.Find("HEA200")!.SectionJson(null));
        var ring = hea.Outline().Select(p => (X: 0.0, Y: p.X * 1000 + 2000, Z: p.Y * 1000 + 3000 - hea.Depth * 500)).ToList();
        var beam = x.Element("IFCBEAM", "hea", "HEA beam", x.Place(0, 0, 0), x.Shape(BrepPrism(x, ring, (4000, 0, 0)), "Brep"), E("BEAM"));
        Typed(x, "hea-type", "HEA200", "IFCBEAMTYPE", "BEAM", beam);
        // A tapered column: no prism, whatever its section; it stays a reference.
        var tapered = Brep(x, [(0, 0, 0), (400, 0, 0), (400, 400, 0), (0, 400, 0), (50, 50, 3000), (350, 50, 3000), (350, 350, 3000), (50, 350, 3000)],
            [0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]);
        x.Element("IFCCOLUMN", "tapered", "Tapered column", x.Place(6000, 0, 0), x.Shape(tapered, "Brep"), E("COLUMN"));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "outlines.ifc")), null, out _);
        var m = f.Model;

        var cornerColumn = m.Get(x.Gid("corner"))!;
        Assert.Equal(IfcClass.Column, cornerColumn.Class);
        Assert.False(StoredBodies.Has(cornerColumn));
        var section = Columns.Spec(m, cornerColumn).Section;
        Assert.Equal("outline", section.Kind);
        Assert.Equal(0.6, section.Width, 4);
        Assert.Equal(0.6, section.Depth, 4);
        Assert.Equal(0.6 * 0.6 - 0.35 * 0.35, section.Area, 6);
        var check = CheckOf(r, cornerColumn.Id, "triangles");
        Assert.True(check.Pass, check.Reason);

        var tri = m.Get(x.Gid("tris"))!;
        Assert.False(StoredBodies.Has(tri));
        var triSection = Columns.Spec(m, tri).Section;
        Assert.Equal("rectangle", triSection.Kind);
        Assert.Equal([0.3, 0.5], new[] { triSection.Width, triSection.Depth }.Order().Select(v => Math.Round(v, 4)));

        var heaBeam = m.Get(x.Gid("hea"))!;
        Assert.Equal(IfcClass.Beam, heaBeam.Class);
        Assert.False(StoredBodies.Has(heaBeam));
        var type = m.RelatingOf(heaBeam.Id, IfcClass.RelDefinesByType)!;
        Assert.Equal("HEA200", type.Name);
        var beamSection = Section.From(type.Attrs["section"])!;
        Assert.True(beamSection.IsProfile, beamSection.Kind);
        Assert.Equal(hea.Label, beamSection.Label);
        Assert.True(CheckOf(r, heaBeam.Id, "triangles").Pass);
        Assert.Equal(4, Math.Abs((double)heaBeam.Attrs["end"]![0]! - (double)heaBeam.Attrs["start"]![0]!), 4);

        // Not a prism: the reference it was, its body the file's.
        var taper = m.Get(x.Gid("tapered"))!;
        Assert.True(StoredBodies.Has(taper));
        // Every measured check within the gate: nothing native differs from the file.
        Assert.All(r.Fidelity!.Checks.Where(c => !c.Reverted && c.Measured), c => Assert.True(c.Pass, $"{c.Name}: {c.Reason}"));
    }

    [Fact]
    public void A_shape_off_its_catalogue_profile_keeps_its_own_outline_and_a_sloped_end_is_never_squared()
    {
        var x = new Foreign();
        // Named HEA200, its flanges 5 mm wider than the catalogue's (more than the 2 mm a profile is matched to): its own outline.
        var hea = Section.Parse(Lapin.Platform.ProfileLibraries.BuiltInProfiles.Find("HEA200")!.SectionJson(null));
        var wide = hea.Outline().Select(p => (X: 0.0, Y: (p.X * 1000) * (hea.Width * 1000 + 5) / (hea.Width * 1000) + 2000, Z: p.Y * 1000 + 2900)).ToList();
        var beam = x.Element("IFCBEAM", "wide", "Wide HEA", x.Place(0, 0, 0), x.Shape(BrepPrism(x, wide, (4000, 0, 0)), "Brep"), E("BEAM"));
        Typed(x, "wide-type", "HEA200", "IFCBEAMTYPE", "BEAM", beam);
        // A beam 200 × 400 whose far end is cut at a slope: a native beam's ends are square, so it is no beam Lapin makes as it is.
        var sloped = Brep(x,
            [(0, 0, 2600), (0, 200, 2600), (0, 200, 3000), (0, 0, 3000), (4000, 0, 2600), (4000, 200, 2600), (3800, 200, 3000), (3800, 0, 3000)],
            [0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]);
        x.Element("IFCBEAM", "sloped", "Sloped end", x.Place(0, 4000, 0), x.Shape(sloped, "Brep"), E("BEAM"));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "outline-refs.ifc")), null, out _);
        var m = f.Model;
        var made = m.Get(x.Gid("wide"))!;
        Assert.False(StoredBodies.Has(made));
        Assert.Equal("outline", Section.From(m.RelatingOf(made.Id, IfcClass.RelDefinesByType)!.Attrs["section"])!.Kind);
        Assert.True(CheckOf(r, made.Id, "triangles").Pass);
        Assert.True(StoredBodies.Has(m.Get(x.Gid("sloped"))!));
        Assert.All(r.Fidelity!.Checks.Where(c => !c.Reverted && c.Measured), c => Assert.True(c.Pass, $"{c.Name}: {c.Reason}"));
    }

    /// <summary>
    /// The sample files' recognition (LAPIN_IFC_COUNTS=a.ifc;b.ifc, the files docs/spec-ifc.md measures): never fewer recognised than
    /// before outline sections (24.11.2026), nothing native that differs from the file under the enforced gate.
    /// </summary>
    [Fact]
    public void Sample_files_recognise_no_fewer_when_given()
    {
        var files = Environment.GetEnvironmentVariable("LAPIN_IFC_COUNTS");
        if (string.IsNullOrEmpty(files)) return;
        // Measured before decision 826 (enforced, 1 mm / 0.5 %): recognised elements, native columns and beams.
        var before = new Dictionary<string, (int Recognised, int Columns, int Beams)>
        {
            ["Bloc-P-4.ifc"] = (696, 0, 0),
            ["BURKWIL_DX_B.ifc"] = (905, 250, 27),
            ["Casa_Stil_Brancovenesc.ifc"] = (18, 0, 0),
        };
        foreach (var path in files.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = Full();
            var r = IfcImport.Run(f.Session, File.ReadAllBytes(path), Path.GetFileName(path), null, out _);
            int Native(string cls) => f.Model.OfClass(cls).Count(e => !StoredBodies.Has(e));
            output.WriteLine($"{Path.GetFileName(path)}: recognised {r.Recognized}, columns {Native(IfcClass.Column)}, beams {Native(IfcClass.Beam)}, reverted {r.Fidelity?.Reverted}");
            if (before.TryGetValue(Path.GetFileName(path), out var b))
            {
                Assert.True(r.Recognized >= b.Recognised, $"{path}: {r.Recognized} recognised, {b.Recognised} before");
                Assert.True(Native(IfcClass.Column) >= b.Columns);
                Assert.True(Native(IfcClass.Beam) >= b.Beams);
            }
            Assert.Equal(FidelityOptions.Enforce, r.Fidelity!.Options.Mode);
            Assert.All(r.Fidelity.Checks.Where(c => !c.Reverted && c.Measured), c => Assert.True(c.Pass, $"{c.Name}: {c.Reason}"));
        }
    }
}
