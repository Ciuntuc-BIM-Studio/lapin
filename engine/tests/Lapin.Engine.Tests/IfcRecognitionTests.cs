using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;
using Xunit.Abstractions;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// A foreign IFC file (no Lapin intent) read into native elements
/// (docs/spec-ifc.md, Recunoașterea): walls with their axes, openings, doors
/// and windows from extrusions and B-rep boxes; floors with their holes;
/// columns, beams and pad footings; what is not a prism stays a reference.
/// </summary>
public class IfcRecognitionTests(ITestOutputHelper output) : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-recognition").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());

    /// <summary>A foreign file as another program would write it: millimetres, a storey, bodies as extrusions and B-reps, no intent.</summary>
    sealed class Foreign
    {
        public readonly Step F = new();
        public readonly string Ctx, StoreyPlacement, Storey;
        readonly List<string> _contained = [];
        public readonly string Brick;

        public Foreign()
        {
            var origin = F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
            var world = F.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
            Ctx = F.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
            var mm = F.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
            var units = F.Add("IFCUNITASSIGNMENT", L([mm]));
            var project = F.Add("IFCPROJECT", G("project"), Null, S("Foreign"), Null, Null, Null, Null, L([Ctx]), units);
            var sitePl = F.Add("IFCLOCALPLACEMENT", Null, world);
            var site = F.Add("IFCSITE", G("site"), Null, S("Site"), Null, Null, sitePl, Null, Null, E("ELEMENT"), Null, Null, Null, Null, Null);
            var bPl = F.Add("IFCLOCALPLACEMENT", sitePl, world);
            var building = F.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, bPl, Null, Null, E("ELEMENT"), Null, Null, Null);
            StoreyPlacement = F.Add("IFCLOCALPLACEMENT", bPl, world);
            Storey = F.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Ground"), Null, Null, StoreyPlacement, Null, Null, E("ELEMENT"), "0.");
            F.Add("IFCRELAGGREGATES", G("agg1"), Null, Null, Null, project, L([site]));
            F.Add("IFCRELAGGREGATES", G("agg2"), Null, Null, Null, site, L([building]));
            F.Add("IFCRELAGGREGATES", G("agg3"), Null, Null, Null, building, L([Storey]));
            Brick = F.Add("IFCMATERIAL", S("Brick"), Null, Null);
        }

        public string G(string key) => S(Guid("foreign-test", key));
        public string Gid(string key) => Guid("foreign-test", key);

        public string Place(double x, double y, double z, double angleDeg = 0, string? relTo = null)
        {
            var p = F.Add("IFCCARTESIANPOINT", $"({R(x)},{R(y)},{R(z)})");
            var zDir = F.Add("IFCDIRECTION", "(0.,0.,1.)");
            var xDir = F.Add("IFCDIRECTION", $"({R(Math.Cos(angleDeg * Math.PI / 180))},{R(Math.Sin(angleDeg * Math.PI / 180))},0.)");
            return F.Add("IFCLOCALPLACEMENT", relTo ?? StoreyPlacement, F.Add("IFCAXIS2PLACEMENT3D", p, zDir, xDir));
        }

        /// <summary>A rectangle extruded up from (cx, cy, z0), the rectangle's x side at <paramref name="angleDeg"/>.</summary>
        public string Box(double cx, double cy, double z0, double dx, double dy, double h, double angleDeg = 0)
        {
            var p2 = F.Add("IFCCARTESIANPOINT", $"({R(cx)},{R(cy)})");
            var d2 = F.Add("IFCDIRECTION", $"({R(Math.Cos(angleDeg * Math.PI / 180))},{R(Math.Sin(angleDeg * Math.PI / 180))})");
            var prof = F.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, F.Add("IFCAXIS2PLACEMENT2D", p2, d2), R(dx), R(dy));
            var at = F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", $"(0.,0.,{R(z0)})"), Null, Null);
            return F.Add("IFCEXTRUDEDAREASOLID", prof, at, F.Add("IFCDIRECTION", "(0.,0.,1.)"), R(h));
        }

        public string Shape(string item, string kind = "SweptSolid") =>
            F.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([F.Add("IFCSHAPEREPRESENTATION", Ctx, S("Body"), S(kind), L([item]))]));

        public string Element(string type, string key, string name, string placement, string shape, params string[] tail)
        {
            var id = F.Add(type, [G(key), Null, S(name), Null, Null, placement, shape, Null, .. tail]);
            _contained.Add(id);
            return id;
        }

        public void Material(string element, string material) => F.Add("IFCRELASSOCIATESMATERIAL", G("mat-" + element), Null, Null, Null, L([element]), material);

        public string Write(string path)
        {
            F.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", G("contained"), Null, Null, Null, L(_contained), Storey);
            F.Write(path, Path.GetFileName(path), "ViewDefinition [ReferenceView_V1.2]");
            return path;
        }
    }

    /// <summary>A B-rep box: eight points, six faces, a closed shell.</summary>
    static string BrepBox(Foreign x, double x0, double y0, double z0, double x1, double y1, double z1)
    {
        var f = x.F;
        string P(double a, double b, double c) => f.Add("IFCCARTESIANPOINT", $"({R(a)},{R(b)},{R(c)})");
        var p = new[] { P(x0, y0, z0), P(x1, y0, z0), P(x1, y1, z0), P(x0, y1, z0), P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1) };
        string Face(params int[] ix) => f.Add("IFCFACE", L([f.Add("IFCFACEOUTERBOUND", f.Add("IFCPOLYLOOP", L(ix.Select(i => p[i]))), True)]));
        var faces = new[] { Face(0, 3, 2, 1), Face(4, 5, 6, 7), Face(0, 1, 5, 4), Face(1, 2, 6, 5), Face(2, 3, 7, 6), Face(3, 0, 4, 7) };
        return f.Add("IFCFACETEDBREP", f.Add("IFCCLOSEDSHELL", L(faces)));
    }

    string House()
    {
        var x = new Foreign();
        // A wall 5 m long, 300 thick, 2.8 high, from (1, 2) along x, in millimetres.
        var wallPl = x.Place(1000, 2000, 0);
        var wall = x.Element("IFCWALL", "wall", "W1", wallPl, x.Shape(x.Box(2500, 0, 0, 5000, 300, 2800)), E("STANDARD"));
        x.Material(wall, x.Brick);
        // A door opening 900 × 2100 at 2 m along, a window opening 1200 × 1500 at 4 m along, sill 900.
        var doorOp = x.Element("IFCOPENINGELEMENT", "door-op", "Door opening", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(2000, 0, 0, 900, 500, 2100)), E("OPENING"));
        var door = x.Element("IFCDOOR", "door", "D1", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(2000, 0, 0, 900, 100, 2100)), "2100.", "900.", E("DOOR"), E("SINGLE_SWING_RIGHT"), Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("v1"), Null, Null, Null, wall, doorOp);
        x.F.Add("IFCRELFILLSELEMENT", x.G("f1"), Null, Null, Null, doorOp, door);
        var winOp = x.Element("IFCOPENINGELEMENT", "win-op", "Window opening", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(4000, 0, 900, 1200, 500, 1500)), E("OPENING"));
        var win = x.Element("IFCWINDOW", "win", "F1", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(4000, 0, 900, 1200, 100, 1500)), "1500.", "1200.", E("WINDOW"), E("SINGLE_PANEL"), Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("v2"), Null, Null, Null, wall, winOp);
        x.F.Add("IFCRELFILLSELEMENT", x.G("f2"), Null, Null, Null, winOp, win);
        // A B-rep wall 4 m long, 250 thick, along x at y = -3.
        x.Element("IFCWALL", "brep-wall", "W2", x.Place(0, 0, 0), x.Shape(BrepBox(x, 0, -3125, 0, 4000, -2875, 2800), "Brep"), E("STANDARD"));
        // A floor 6 × 4, 200 thick, its top at the storey, with a 1 × 1 hole.
        var outer = x.F.Add("IFCPOLYLINE", L(new[] { "(0.,0.)", "(6000.,0.)", "(6000.,4000.)", "(0.,4000.)", "(0.,0.)" }.Select(p => x.F.Add("IFCCARTESIANPOINT", p))));
        var hole = x.F.Add("IFCPOLYLINE", L(new[] { "(2000.,1000.)", "(3000.,1000.)", "(3000.,2000.)", "(2000.,2000.)", "(2000.,1000.)" }.Select(p => x.F.Add("IFCCARTESIANPOINT", p))));
        var prof = x.F.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", E("AREA"), Null, outer, L([hole]));
        var slabSolid = x.F.Add("IFCEXTRUDEDAREASOLID", prof, x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,-200.)"), Null, Null), x.F.Add("IFCDIRECTION", "(0.,0.,1.)"), "200.");
        x.Element("IFCSLAB", "slab", "Floor", x.Place(0, 0, 0), x.Shape(slabSolid), E("FLOOR"));
        // A column 300 × 400 turned 30° at (3, 2), 3 m high.
        x.Element("IFCCOLUMN", "column", "C1", x.Place(3000, 2000, 0), x.Shape(x.Box(0, 0, 0, 300, 400, 3000, 30)), E("COLUMN"));
        // A beam 250 wide, 400 deep, from (0, 0) to (5, 0) with its top at 3 m: the profile in a vertical plane, swept along x.
        var bp = x.F.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, x.F.Add("IFCAXIS2PLACEMENT2D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.)"), Null), "250.", "400.");
        var bAt = x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,2800.)"), x.F.Add("IFCDIRECTION", "(1.,0.,0.)"), x.F.Add("IFCDIRECTION", "(0.,1.,0.)"));
        var beamSolid = x.F.Add("IFCEXTRUDEDAREASOLID", bp, bAt, x.F.Add("IFCDIRECTION", "(0.,0.,1.)"), "5000.");
        x.Element("IFCBEAM", "beam", "B1", x.Place(0, 0, 0), x.Shape(beamSolid), E("BEAM"));
        // A pad footing 1.5 × 1.5 × 0.5 at (8, 0), its bottom 1.5 m down.
        x.Element("IFCFOOTING", "footing", "F1", x.Place(8000, 0, 0), x.Shape(x.Box(0, 0, -1500, 1500, 1500, 500)), E("PAD_FOOTING"));
        // A railing as a pyramid: not a prism, a reference.
        var f = x.F;
        string P(string c) => f.Add("IFCCARTESIANPOINT", c);
        var apex = P("(500.,500.,1000.)"); var b0 = P("(0.,0.,0.)"); var b1 = P("(1000.,0.,0.)"); var b2 = P("(1000.,1000.,0.)"); var b3 = P("(0.,1000.,0.)");
        string Tri(params string[] pts) => f.Add("IFCFACE", L([f.Add("IFCFACEOUTERBOUND", f.Add("IFCPOLYLOOP", L(pts)), True)]));
        var pyramid = f.Add("IFCFACETEDBREP", f.Add("IFCCLOSEDSHELL", L([Tri(b0, b3, b2, b1), Tri(b0, b1, apex), Tri(b1, b2, apex), Tri(b2, b3, apex), Tri(b3, b0, apex)])));
        x.Element("IFCRAILING", "railing", "Rail", x.Place(10000, 10000, 0), x.Shape(pyramid, "Brep"), E("HANDRAIL"));
        return x.Write(Path.Combine(_dir, "foreign.ifc"));
    }

    [Fact]
    public void Walls_floors_columns_beams_and_footings_come_in_native_and_the_rest_as_references()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, House(), null, out _);
        output.WriteLine(r.ToJson().ToJsonString());
        var m = f.Session.Model;
        Assert.Equal(6, r.Recognized);
        Assert.Equal(1, r.References);
        Assert.Equal(2, r.RecognizedByClass!["IfcWall"]);

        // The extruded wall: its axis, thickness, height, type and material; the file's GlobalId kept.
        var w1 = m.Get(Guid("foreign-test", "wall"));
        Assert.NotNull(w1);
        Assert.Equal(IfcClass.Wall, w1!.Class);
        var axis = PathCurve.Of(w1);
        Assert.Equal(1, Math.Min(axis.Start.X, axis.End.X), 3);
        Assert.Equal(6, Math.Max(axis.Start.X, axis.End.X), 3);
        Assert.Equal(2, axis.Start.Y, 3);
        Assert.Equal(2.8, w1.Num("height"), 6);
        Assert.Equal(0, w1.Num("baseOffset"), 6);
        var type = m.RelatingOf(w1.Id, IfcClass.RelDefinesByType);
        Assert.NotNull(type);
        Assert.Equal("Brick 300", type!.Name);
        Assert.Equal(0.3, LayerSets.BandsOf(m, w1.Id)!.Sum(b => b.To - b.From), 6);
        Assert.Equal("IfcWall", (string?)w1.Attrs["imported"]?["ifcClass"]);

        // The door and the window: on the wall, where and as big as the file said; the door's GlobalId kept; the hand from the operation.
        var door = m.Get(Guid("foreign-test", "door"));
        Assert.NotNull(door);
        var dSpec = Openings.SpecOf(m, door!.Id)!;
        Assert.Equal((0.9, 2.1), (dSpec.Width, dSpec.Height));
        var dOpening = Openings.OpeningOf(m, door.Id)!;
        Assert.Equal(2.0, dOpening.Num("along"), 3);
        Assert.Equal(0, dOpening.Num("sill"), 3);
        Assert.True((bool)door.Attrs["flipHand"]!);
        Assert.Equal(w1.Id, Openings.WallOf(m, dOpening.Id)!.Id);
        var win = m.Get(Guid("foreign-test", "win"))!;
        var wSpec = Openings.SpecOf(m, win.Id)!;
        Assert.Equal((1.2, 1.5), (wSpec.Width, wSpec.Height));
        Assert.Equal(4.0, Openings.OpeningOf(m, win.Id)!.Num("along"), 3);
        Assert.Equal(0.9, Openings.OpeningOf(m, win.Id)!.Num("sill"), 3);
        Assert.Equal(2, Openings.Of(m, w1.Id).Count());

        // The B-rep wall: the same from six faces.
        var w2 = m.Get(Guid("foreign-test", "brep-wall"))!;
        var a2 = PathCurve.Of(w2);
        Assert.Equal(4, (a2.End - a2.Start).Length, 3);
        Assert.Equal(-3, a2.Start.Y, 3);
        Assert.Equal(0.25, LayerSets.BandsOf(m, w2.Id)!.Sum(b => b.To - b.From), 6);

        // The floor: its contour, its hole, its thickness, its top at the storey.
        var slab = m.Get(Guid("foreign-test", "slab"))!;
        Assert.Equal(IfcClass.Slab, slab.Class);
        Assert.Equal(4, slab.Attrs["contour"]!.AsArray().Count);
        Assert.Equal(0, slab.Num("offset"), 6);
        Assert.Single(m.RelatedOf(slab.Id, IfcClass.RelVoidsElement));
        Assert.Equal(0.2, LayerSets.BandsOf(m, slab.Id)!.Sum(b => b.To - b.From), 6);

        // The column, the beam, the footing.
        var col = m.Get(Guid("foreign-test", "column"))!;
        Assert.Equal((3.0, 2.0), (col.Attrs["at"]![0]!.GetValue<double>(), col.Attrs["at"]![1]!.GetValue<double>()));
        Assert.Equal(30, col.Num("rotation"), 3);
        var cs = Columns.Spec(m, col);
        Assert.Equal((0.3, 0.4, 3.0), (cs.Section.Width, cs.Section.Depth, cs.Height));
        var beam = m.Get(Guid("foreign-test", "beam"))!;
        var bs = Beams.Spec(m, beam);
        Assert.Equal((0.25, 0.4), (bs.Section.Width, bs.Section.Depth));
        Assert.Equal(5, (Args.Point(beam.Attrs["end"], "end") - Args.Point(beam.Attrs["start"], "start")).Length, 3);
        Assert.Equal(0, beam.Num("topOffset"), 6);
        var footing = m.Get(Guid("foreign-test", "footing"))!;
        Assert.Equal("PAD_FOOTING", footing.PredefinedType);
        var ft = m.RelatingOf(footing.Id, IfcClass.RelDefinesByType)!;
        Assert.Equal(-1.5, ft.Num("bottom"), 6);

        // The railing: a reference, with the reason counted.
        var rail = m.Get(Guid("foreign-test", "railing"))!;
        Assert.True((bool?)rail.Attrs["reference"]);
        Assert.Equal(1, r.ReferencesByClass!["IfcRailing"]);

        // Exported again, the wall keeps the file's GlobalId.
        var path = Path.Combine(_dir, "again.ifc");
        IfcExport.Write(f.Session, path, null);
        Assert.Contains($"IFCWALL('{Guid("foreign-test", "wall")}'", File.ReadAllText(path));
    }

    [Fact]
    public void A_second_import_of_the_same_file_does_not_double_the_elements()
    {
        var f = Full();
        var path = House();
        IfcImport.Run(f.Session, path, null, out _);
        var walls = f.Session.Model.OfClass(IfcClass.Wall).Count();
        var doors = f.Session.Model.OfClass(IfcClass.Door).Count();
        var r = IfcImport.Run(f.Session, path, null, out _);
        output.WriteLine(r.ToJson().ToJsonString());
        Assert.Equal(walls, f.Session.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(doors, f.Session.Model.OfClass(IfcClass.Door).Count());
    }

    [Fact]
    public void In_reference_mode_nothing_is_recognised()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, House(), new IfcImportOptions("reference"), out _);
        Assert.Equal(0, r.Recognized);
        Assert.True(r.References >= 8);
    }

    /// <summary>A real file, when one is given (LAPIN_IFC_SAMPLE=/path/to/file.ifc): the report, for the eye.</summary>
    [Fact]
    public void A_sample_file_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_SAMPLE");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var f = Full();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = IfcImport.Run(f.Session, path, null, out _);
        output.WriteLine($"{Path.GetFileName(path)} in {sw.ElapsedMilliseconds} ms");
        output.WriteLine(r.ToJson().ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Assert.True(r.Recognized > 0);
    }
}

/// <summary>A real file's plans and a section on a sheet, as a PDF, for the eye (LAPIN_IFC_SAMPLE and LAPIN_IFC_PDF=/path/out.pdf).</summary>
public class IfcRecognitionSheetTests
{
    [Fact]
    public void A_sample_file_drawn_when_asked()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_SAMPLE");
        var outPath = Environment.GetEnvironmentVariable("LAPIN_IFC_PDF");
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(outPath) || !File.Exists(path)) return;
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());
        IfcImport.Run(f.Session, path, Environment.GetEnvironmentVariable("LAPIN_IFC_MODE") is { } mode ? new IfcImportOptions(mode) : null, out _);
        var storeys = Spatial.Storeys(f.Model).OrderBy(s => s.Num("elevation")).ToList();
        var storey = storeys.Skip(Math.Min(1, storeys.Count - 1)).First();
        // The plan framed on what the storey holds (walls' axes, references' bodies).
        var pts = new List<Vec2>();
        foreach (var e in f.Model.OfClass(IfcClass.Wall).Where(w => Spatial.StoreyOf(f.Model, w.Id)?.Id == storey.Id)) { var c = PathCurve.Of(e); pts.Add(c.Start); pts.Add(c.End); }
        foreach (var e in f.Model.OfClass(IfcClass.Wall).Where(w => StoredBodies.Has(w) && Spatial.StoreyOf(f.Model, w.Id)?.Id == storey.Id))
            if (StoredBodies.Geometry(e, f.Model, f.Session.Catalog) is { } g) foreach (var part in g.Parts) foreach (var v in part.Solid.Vertices) pts.Add(new Vec2(v.X, v.Y));
        var cx = pts.Count > 0 ? (pts.Min(p => p.X) + pts.Max(p => p.X)) / 2 : 0;
        var cy = pts.Count > 0 ? (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2 : 0;
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plan", ["paper"] = "A1" })!["id"]!;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = "plan:" + storey.Id, ["x"] = 30, ["y"] = 30 })!["viewportId"]!;
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vp, ["width"] = 780, ["height"] = 520, ["center"] = new JsonArray(cx, cy) });
        var pdf = Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]);
        File.WriteAllBytes(outPath, pdf);
        Assert.True(pdf.Length > 1000);
    }
}
