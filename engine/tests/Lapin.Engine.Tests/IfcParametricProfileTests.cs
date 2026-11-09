using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Beams, columns and steel members in IFC by their parametric profile (decision 416), read back by Lapin's own import with the
/// same body: the profile's subtype and name, its mechanical set; L, U, C and Z where the section stands, flipped or not.
/// </summary>
public class IfcParametricProfileTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-profiles").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static JsonObject Sec(string kind, double w, double d, double t = 0, double f = 0, double lip = 0, string? name = null)
    {
        var o = new JsonObject { ["kind"] = kind, ["width"] = w, ["depth"] = d };
        if (t > 0) { o["web"] = t; o["flange"] = f > 0 ? f : t; }
        if (lip > 0) o["lip"] = lip;
        if (name != null) o["name"] = name;
        return o;
    }

    public static readonly TheoryData<string, string, JsonObject> Sections = new()
    {
        { "IFCISHAPEPROFILEDEF", "IPE 300", new JsonObject { ["name"] = "IPE 300" } },
        { "IFCLSHAPEPROFILEDEF", "L 100×100×10", new JsonObject { ["name"] = "L 100×100×10" } },
        { "IFCLSHAPEPROFILEDEF", "L 150×90×10", Sec("L", 0.09, 0.15, 0.01, 0.01, name: "L 150×90×10") },
        { "IFCUSHAPEPROFILEDEF", "UPN 200", new JsonObject { ["name"] = "UPN 200" } },
        { "IFCTSHAPEPROFILEDEF", "T 100×100", Sec("T", 0.1, 0.1, 0.008, 0.011, name: "T 100×100") },
        { "IFCCSHAPEPROFILEDEF", "C 200×65×2", new JsonObject { ["name"] = "C 200×65×2" } },
        { "IFCZSHAPEPROFILEDEF", "Z 200×130×3", Sec("Z", 0.13, 0.2, 0.003, 0.003, name: "Z 200×130×3") },
        { "IFCRECTANGLEHOLLOWPROFILEDEF", "RHS 150×100×6", new JsonObject { ["name"] = "RHS 150×100×6" } },
        { "IFCCIRCLEHOLLOWPROFILEDEF", "CHS 114.3×5", new JsonObject { ["name"] = "CHS 114.3×5" } },
        { "IFCRECTANGLEPROFILEDEF", "30×40", Sec("rectangle", 0.3, 0.4) },
        { "IFCCIRCLEPROFILEDEF", "Ø400", Sec("circle", 0.4, 0.4) },
    };

    string Export(Fixture f)
    {
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".ifc");
        IfcExport.Write(f.Session, path);
        return path;
    }

    static string Column(Fixture f, JsonObject section, double rotation = 0)
    {
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "Col", ["section"] = section.DeepClone(), ["material"] = "Steel" })!["id"]!;
        return (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 2, ["y"] = 3, ["rotation"] = rotation })!["id"]!;
    }

    static string Beam(Fixture f, JsonObject section, bool flipped = false)
    {
        var type = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "Bm", ["section"] = section.DeepClone(), ["material"] = "Steel" })!["id"]!;
        var id = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 1, ["y1"] = 1, ["x2"] = 6, ["y2"] = 4 })!["id"]!;
        if (flipped) f.Run("structure.flip", new JsonObject { ["ids"] = new JsonArray(id) });
        return id;
    }

    /// <summary>The file read as references (Lapin's own intent left out): each element by its GlobalId with the body the file gives it.</summary>
    static Fixture ImportedAsReferences(string path)
    {
        var g = F();
        IfcImport.Run(g.Session, path, new IfcImportOptions("reference"), out _);
        return g;
    }

    /// <summary>
    /// Two bodies the same, as a stored body keeps them (its coordinates to a tenth of a millimetre, StoredBodies): volume, box,
    /// and every corner of each on the other's within that, so a section mirrored or moved off its place fails.
    /// </summary>
    static void SameBody(Solid a, Solid b)
    {
        const double Tol = 1.5e-4;
        Assert.InRange(Measure.Volume(b), Measure.Volume(a) * 0.98, Measure.Volume(a) * 1.02);
        var (x, y) = (Measure.Bounds(a)!.Value, Measure.Bounds(b)!.Value);
        Assert.InRange((x.Min - y.Min).Length, 0, Tol);
        Assert.InRange((x.Max - y.Max).Length, 0, Tol);
        foreach (var v in a.Vertices) Assert.True(b.Vertices.Min(q => (q - v).Length) < Tol, $"corner {v} is not on the file's body");
        foreach (var v in b.Vertices) Assert.True(a.Vertices.Min(q => (q - v).Length) < Tol, $"the file's corner {v} is not on the element");
    }

    static StepEntity ProfileOf(StepFile s, string globalId)
    {
        var product = s.Rooted.First(e => e[0] as string == globalId);
        var shape = (List<object?>)s.Get(product[6])![2]!;
        foreach (var r in shape.OfType<StepRef>())
        {
            var rep = s.Get(r)!;
            foreach (var item in ((List<object?>)rep[3]!).OfType<StepRef>())
            {
                var it = s.Get(item)!;
                while (it.Type == "IFCBOOLEANCLIPPINGRESULT") it = s.Get(it[1])!;
                if (it.Type == "IFCEXTRUDEDAREASOLID") return s.Get(it[0])!;
            }
        }
        throw new InvalidOperationException($"No extrusion on {globalId}: {string.Join(", ", shape.OfType<StepRef>().Select(r => s.Get(r)?[2]))}");
    }

    [Theory, MemberData(nameof(Sections))]
    public void A_column_is_written_by_its_parametric_profile_and_read_back_with_the_same_body(string type, string name, JsonObject section)
    {
        var f = F();
        var id = Column(f, section, 30);
        var path = Export(f);
        var s = StepFile.Read(path);
        var profile = ProfileOf(s, id);
        Assert.Equal(type, profile.Type);
        Assert.Equal(name, profile[1] as string);
        var g = ImportedAsReferences(path);
        SameBody(f.Session.Geometry(id)!.Parts[0].Solid, g.Session.Geometry(id)!.Parts[0].Solid);
    }

    [Theory, MemberData(nameof(Sections))]
    public void A_beam_keeps_where_it_stands_flipped_or_not(string type, string name, JsonObject section)
    {
        // A round beam's body is its bounding band (as it always drew): not the circle, so its B-rep stays.
        if (type == "IFCCIRCLEPROFILEDEF") return;
        foreach (var flipped in new[] { false, true })
        {
            var f = F();
            var id = Beam(f, section, flipped);
            var path = Export(f);
            var profile = ProfileOf(StepFile.Read(path), id);
            Assert.Equal(type, profile.Type);
            Assert.Equal(name, profile[1] as string);
            var g = ImportedAsReferences(path);
            SameBody(f.Session.Geometry(id)!.Parts[0].Solid, g.Session.Geometry(id)!.Parts[0].Solid);
        }
    }

    [Fact]
    public void Steel_members_with_cut_ends_write_it_too_and_stay_the_same_body()
    {
        var f = F();
        var type = (string)f.Run("steel.createType", new JsonObject { ["name"] = "Angle", ["section"] = new JsonObject { ["name"] = "L 100×100×10" } })!["id"]!;
        var id = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["z1"] = 1, ["x2"] = 4, ["y2"] = 1, ["z2"] = 2 })!["id"]!;
        f.Run("steel.flip", new JsonObject { ["ids"] = new JsonArray(id) });
        f.Run("steel.setEnds", new JsonObject { ["id"] = id, ["start"] = new JsonObject { ["cut"] = "plane", ["planAngle"] = 20 } });
        var path = Export(f);
        Assert.Equal("IFCLSHAPEPROFILEDEF", ProfileOf(StepFile.Read(path), id).Type);
        var g = ImportedAsReferences(path);
        var (a, b) = (f.Session.Geometry(id)!.Parts[0].Solid, g.Session.Geometry(id)!.Parts[0].Solid);
        SameBody(a, b);
    }

    [Fact]
    public void A_hall_writes_its_frames_by_their_profiles_each_once_with_its_mechanical_set()
    {
        var f = F();
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject() });
        var s = StepFile.Read(Export(f));
        var ipe = s.OfType("IFCISHAPEPROFILEDEF").ToList();
        Assert.NotEmpty(ipe);
        Assert.Equal(ipe.Count, ipe.Select(p => p[1] as string).Distinct().Count());
        Assert.All(ipe, p => Assert.Contains(s.OfType("IFCPROFILEPROPERTIES"), pp => pp[0] as string == "Pset_ProfileMechanical" && pp[3] is StepRef r && r.Id == p.Id));
        // The default's Z purlins carry lips: IFC has no form for them, so they stay an arbitrary profile, named.
        Assert.Contains(s.OfType("IFCARBITRARYCLOSEDPROFILEDEF"), p => (p[1] as string)?.StartsWith('Z') == true);
    }

    [Fact]
    public void Its_values_are_in_SI_read_back_as_the_library_profile_with_its_radii()
    {
        var f = F();
        var column = Column(f, new JsonObject { ["name"] = "IPE 300" });
        var read = new IfcModel(StepFile.Read(Export(f))).ReadProfiles();
        var p = read.Profiles.Single(x => x.Name == "IPE 300");
        Assert.Equal(("I", 0.3, 0.15, 0.0071, 0.0107, 0.015), (p.Kind, Math.Round(p.H, 6), Math.Round(p.B, 6), Math.Round(p.Tw, 6), Math.Round(p.Tf, 6), Math.Round(p.R1, 6)));
        // 53.81 cm², 8356 cm⁴ (EN 10365), 42.2 kg/m, given back in their own units.
        Assert.InRange(p.Given["A"], 53.7e-4, 53.9e-4);
        Assert.InRange(p.Given["Iy"], 8300e-8, 8400e-8);
        Assert.InRange(p.Given["mass"], 42, 42.5);
        Assert.Empty(read.Checks);
        _ = column;
    }

    [Fact]
    public void A_library_asymmetric_I_and_an_ellipse_go_by_their_subtypes_a_polygon_and_an_outline_by_their_rings()
    {
        var f = F();
        var asym = new SectionProfile { Name = "ASB 300", Kind = "asymI", H = 0.3, B = 0.15, B2 = 0.2, Tw = 0.01, Tf = 0.012, Tf2 = 0.015, R1 = 0.012 };
        var ellipse = new SectionProfile { Name = "EHS 200×100", Kind = "ellipse", H = 0.2, B = 0.1 };
        string Typed(SectionProfile p)
        {
            var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = p.Name, ["profile"] = p.ToJson(), ["library"] = "Test" })!["id"]!;
            return (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 2, ["y"] = 3 })!["id"]!;
        }
        var (a, e) = (Typed(asym), Typed(ellipse));
        var polygon = Column(f, new JsonObject { ["kind"] = "polygon", ["diameter"] = 0.4, ["sides"] = 8 });
        var outline = Column(f, new JsonObject { ["kind"] = "outline", ["rings"] = new JsonArray(new JsonArray(new JsonArray(-0.1, -0.1), new JsonArray(0.1, -0.1), new JsonArray(0.1, 0.1), new JsonArray(0, 0.1), new JsonArray(0, 0), new JsonArray(-0.1, 0))) });
        var path = Export(f);
        var s = StepFile.Read(path);
        Assert.Equal("IFCASYMMETRICISHAPEPROFILEDEF", ProfileOf(s, a).Type);
        Assert.Equal("IFCELLIPSEPROFILEDEF", ProfileOf(s, e).Type);
        Assert.Equal("IFCARBITRARYCLOSEDPROFILEDEF", ProfileOf(s, polygon).Type);
        Assert.Equal("IFCARBITRARYCLOSEDPROFILEDEF", ProfileOf(s, outline).Type);
        var g = ImportedAsReferences(path);
        foreach (var id in new[] { a, e, polygon, outline }) SameBody(f.Session.Geometry(id)!.Parts[0].Solid, g.Session.Geometry(id)!.Parts[0].Solid);
    }

    [Fact]
    public void A_beam_cut_by_a_column_passing_through_stays_the_brep_it_is()
    {
        var f = F();
        var rect = Sec("rectangle", 0.25, 0.4);
        var beam = Beam(f, rect);
        // A column in the beam's way, not at its ends: the beam runs round it, in two pieces of one body.
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 3.5, ["y"] = 2.5 })!["id"]!;
        Assert.NotNull(column);
        var path = Export(f);
        var s = StepFile.Read(path);
        Assert.Throws<InvalidOperationException>(() => ProfileOf(s, beam));
        // The body in the file is the element's, cuts and all.
        var g = ImportedAsReferences(path);
        Assert.Equal(Measure.Volume(f.Session.Geometry(beam)!.Parts[0].Solid), Measure.Volume(g.Session.Geometry(beam)!.Parts[0].Solid), 3);
    }

    [Fact]
    public void Lapin_reading_its_own_file_rebuilds_the_type_and_the_recognised_rectangle_passes_the_gate()
    {
        var f = F();
        var i = Column(f, new JsonObject { ["name"] = "IPE 300" }, 15);
        var r = Column(f, Sec("rectangle", 0.3, 0.4), 15);
        var c = Column(f, Sec("circle", 0.4, 0.4));
        var path = Export(f);
        // Natively, by Lapin's intent.
        var own = F();
        IfcImport.Run(own.Session, path, null, out _);
        Assert.Equal("I", Section.From(own.Model.RelatingOf(i, IfcClass.RelDefinesByType)!.Attrs["section"])!.Kind);
        SameBody(f.Session.Geometry(i)!.Parts[0].Solid, own.Session.Geometry(i)!.Parts[0].Solid);
        // As another tool's file: the intent's name changed, so what recognition reads is the parametric profile.
        var foreign = Path.Combine(_dir, "foreign.ifc");
        File.WriteAllText(foreign, File.ReadAllText(path).Replace("Lapin_Intent", "Other_Set").Replace("Pset_Lapin_Intent", "Other_Set"));
        var g = F();
        var report = IfcImport.Run(g.Session, foreign, null, out _);
        var gate = report.Fidelity!;
        Assert.Equal(0, gate.Failed);
        Assert.Equal(0, gate.Reverted);
        Assert.Contains(gate.Checks, x => x.Id == r && x.Measured && x.Pass);
        Assert.Contains(gate.Checks, x => x.Id == c && x.Measured && x.Pass);
        SameBody(f.Session.Geometry(r)!.Parts[0].Solid, g.Session.Geometry(r)!.Parts[0].Solid);
        // The I is no section recognition makes: a reference, with the file's same body.
        Assert.True(StoredBodies.Has(g.Model.Get(i)!));
        SameBody(f.Session.Geometry(i)!.Parts[0].Solid, g.Session.Geometry(i)!.Parts[0].Solid);
    }

    [Fact]
    public void Steel_sections_stay_put_over_ten_cycles()
    {
        var f = F();
        var ids = new[] { Column(f, new JsonObject { ["name"] = "HEA 200" }, 20), Beam(f, new JsonObject { ["name"] = "UPN 200" }, true), Beam(f, new JsonObject { ["name"] = "L 100×100×10" }, false) };
        var bodies = ids.ToDictionary(x => x, x => f.Session.Geometry(x)!.Parts[0].Solid);
        var path = Export(f);
        var count = StepFile.Read(path).Entities.Count;
        for (var cycle = 0; cycle < 10; cycle++)
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            foreach (var x in ids) SameBody(bodies[x], g.Session.Geometry(x)!.Parts[0].Solid);
            path = Export(g);
            Assert.Equal(count, StepFile.Read(path).Entities.Count);
        }
    }
}
