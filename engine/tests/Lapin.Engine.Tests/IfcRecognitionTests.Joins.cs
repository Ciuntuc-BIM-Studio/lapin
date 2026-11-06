using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// Recognised walls joined as the file's walls meet (docs/spec-ifc.md, Recunoașterea, Îmbinările; <see cref="FoundJoins"/>):
/// a join only where it leaves both walls as the file has them, the ends otherwise square where the file's end; openings
/// kept in place while the axes go to the joints; tops the file keeps flat not raised or cut by what is over them.
/// Walls drawn in Lapin keep <see cref="Joins.Auto"/> (JoinModelTests).
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>A wall's body extruded 2.8 m from a footprint in plan (millimetres, world coordinates), of brick.</summary>
    static string FootprintWall(Foreign x, string key, params (double X, double Y)[] pts)
    {
        var f = x.F;
        var loop = f.Add("IFCPOLYLINE", L(pts.Append(pts[0]).Select(p => f.Add("IFCCARTESIANPOINT", $"({R(p.X)},{R(p.Y)})"))));
        var solid = f.Add("IFCEXTRUDEDAREASOLID", f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, loop),
            f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null), f.Add("IFCDIRECTION", "(0.,0.,1.)"), "2800.");
        var w = x.Element("IFCWALL", key, key, x.Place(0, 0, 0), x.Shape(solid), E("STANDARD"));
        x.Material(w, x.Brick);
        return w;
    }

    /// <summary>Imports the file; every wall's gate check (by key), the join between a and b if any.</summary>
    (Fixture F, IfcImportResult R, Relation? Join) Imported(Foreign x, string name)
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, name)), null, out _);
        return (f, r, Joins.Between(f.Model, Guid("foreign-test", "a"), Guid("foreign-test", "b")));
    }

    static void AllFaithful(IfcImportResult r)
    {
        foreach (var c in r.Fidelity!.Checks.Where(c => c.IfcClass == IfcClass.Wall))
        {
            Assert.True(c.Measured, c.Id);
            Assert.True(c.Pass, $"{c.Name}: {c.Reason}");
            Assert.InRange(c.OutlineMm!.Value, 0, 1);
            Assert.InRange(Math.Abs(c.Volume!.Value), 0, 0.005);
        }
    }

    [Fact]
    public void A_corner_the_file_butts_comes_in_as_a_butt_corner_with_the_same_wall_through()
    {
        // Wall a along x runs on to b's outer face (x = -150); b along y stops at a's inner face (y = 150). 300 thick.
        var x = new Foreign();
        FootprintWall(x, "a", (-150, -150), (5000, -150), (5000, 150), (-150, 150));
        FootprintWall(x, "b", (-150, 150), (150, 150), (150, 4000), (-150, 4000));
        var (f, r, join) = Imported(x, "corner-butt.ifc");
        Assert.NotNull(join);
        Assert.Equal(("L", "butt"), ((string?)join!.Attrs!["kind"], (string?)join.Attrs["style"]));
        Assert.Equal(Guid("foreign-test", "a"), join.Relating);
        // Both axes at the joint, the corner where the axes cross.
        Assert.Equal(new Vec2(0, 0), Walls.Start(f.Model.Get(Guid("foreign-test", "a"))!));
        Assert.Equal(new Vec2(0, 0), Walls.Start(f.Model.Get(Guid("foreign-test", "b"))!));
        AllFaithful(r);
    }

    [Fact]
    public void A_mitred_corner_in_the_file_comes_in_mitred()
    {
        var x = new Foreign();
        FootprintWall(x, "a", (-150, -150), (5000, -150), (5000, 150), (150, 150));
        FootprintWall(x, "b", (-150, -150), (150, 150), (150, 4000), (-150, 4000));
        var (_, r, join) = Imported(x, "corner-mitre.ifc");
        Assert.NotNull(join);
        Assert.Equal(("L", "mitre"), ((string?)join!.Attrs!["kind"], (string?)join.Attrs["style"]));
        AllFaithful(r);
    }

    [Fact]
    public void A_wall_the_file_ends_on_another_comes_in_as_a_T_its_window_where_the_file_has_it()
    {
        // a along x from -2 m to 3 m; b along y ends on a's face (y = 150), with a window 1 m wide centred at y = 2 m.
        var x = new Foreign();
        FootprintWall(x, "a", (-2000, -150), (3000, -150), (3000, 150), (-2000, 150));
        var b = FootprintWall(x, "b", (-150, 150), (150, 150), (150, 4000), (-150, 4000));
        var op = x.Element("IFCOPENINGELEMENT", "b-op", "Opening", x.Place(0, 0, 0), x.Shape(x.Box(0, 2000, 900, 500, 1000, 1200)), E("OPENING"));
        var win = x.Element("IFCWINDOW", "b-win", "F1", x.Place(0, 0, 0), x.Shape(x.Box(0, 2000, 900, 100, 1000, 1200)), "1200.", "1000.", E("WINDOW"), E("SINGLE_PANEL"), Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("b-v"), Null, Null, Null, b, op);
        x.F.Add("IFCRELFILLSELEMENT", x.G("b-f"), Null, Null, Null, op, win);
        var (f, r, join) = Imported(x, "tee.ifc");
        Assert.NotNull(join);
        Assert.Equal("T", (string?)join!.Attrs!["kind"]);
        Assert.Equal(Guid("foreign-test", "a"), join.Relating);
        var wb = f.Model.Get(Guid("foreign-test", "b"))!;
        // b's axis goes to a's axis (y = 0), and its window stays at y = 2 m: 2 m along from the new start.
        Assert.Equal(0, Walls.Start(wb).Y, 9);
        var opening = Openings.Of(f.Model, wb.Id).Single();
        Assert.Equal(2.0, opening.Num("along"), 6);
        // a is left whole: from -2 m to 3 m.
        var wa = f.Model.Get(Guid("foreign-test", "a"))!;
        Assert.Equal(-2, Walls.Start(wa).X, 9);
        Assert.Equal(3, Walls.End(wa).X, 9);
        AllFaithful(r);
    }

    [Fact]
    public void A_corner_the_file_overlaps_is_not_joined_and_neither_wall_runs_into_the_other()
    {
        // Both walls run to the outer corner: no join of Lapin's leaves them so; each stays square where the file ends it.
        var x = new Foreign();
        FootprintWall(x, "a", (-150, -150), (5000, -150), (5000, 150), (-150, 150));
        FootprintWall(x, "b", (-150, -150), (150, -150), (150, 4000), (-150, 4000));
        // And one whose end stops 100 mm short of a's face: not run on into it.
        FootprintWall(x, "c", (2000, 250), (2300, 250), (2300, 3000), (2000, 3000));
        var (f, r, join) = Imported(x, "corner-overlap.ifc");
        Assert.Null(join);
        Assert.Empty(Joins.Of(f.Model, Guid("foreign-test", "c")));
        Assert.Equal(-0.15, Walls.Start(f.Model.Get(Guid("foreign-test", "a"))!).X, 9);
        Assert.Equal(0.25, Walls.Start(f.Model.Get(Guid("foreign-test", "c"))!).Y, 9);
        AllFaithful(r);
        // Volumes as the file's, its overlap and all: nothing added or taken.
        var checks = r.Fidelity!.Checks.Where(c => c.IfcClass == IfcClass.Wall).ToList();
        Assert.Equal(checks.Sum(c => c.FileVolume!.Value), checks.Sum(c => c.NativeVolume!.Value), 4);
    }

    [Fact]
    public void A_wall_under_an_imported_roof_keeps_the_top_the_file_gives_it()
    {
        // A wall 2.8 m high, flat; over it a roof whose underside slopes from 2.5 m to 3.5 m along the wall (a B-rep wedge):
        // neither cut by it at one end nor raised to it at the other. A second wall whose top the file cuts (a boolean, 2.4 to
        // 2.8 m, recognition takes its 3 m extrusion) is not raised above that either.
        var x = new Foreign();
        FootprintWall(x, "a", (0, -150), (5000, -150), (5000, 150), (0, 150));
        var f0 = x.F;
        string P(double a, double b, double c) => f0.Add("IFCCARTESIANPOINT", $"({R(a)},{R(b)},{R(c)})");
        var p = new[] { P(-500, -1000, 2500), P(5500, -1000, 3500), P(5500, 5000, 3500), P(-500, 5000, 2500), P(-500, -1000, 2700), P(5500, -1000, 3700), P(5500, 5000, 3700), P(-500, 5000, 2700) };
        string Face(params int[] ix) => f0.Add("IFCFACE", L([f0.Add("IFCFACEOUTERBOUND", f0.Add("IFCPOLYLOOP", L(ix.Select(i => p[i]))), True)]));
        var wedge = f0.Add("IFCFACETEDBREP", f0.Add("IFCCLOSEDSHELL", L([Face(0, 3, 2, 1), Face(4, 5, 6, 7), Face(0, 1, 5, 4), Face(1, 2, 6, 5), Face(2, 3, 7, 6), Face(3, 0, 4, 7)])));
        x.Element("IFCROOF", "roof", "Roof", x.Place(0, 0, 0), x.Shape(wedge, "Brep"), E("NOTDEFINED"));
        var n = new Vec3(400, 0, -5000).Normalized();
        var plane = f0.Add("IFCPLANE", f0.Add("IFCAXIS2PLACEMENT3D", f0.Add("IFCCARTESIANPOINT", "(0.,0.,2400.)"), f0.Add("IFCDIRECTION", $"({R(n.X)},{R(n.Y)},{R(n.Z)})"), Null));
        var clipped = f0.Add("IFCBOOLEANCLIPPINGRESULT", E("DIFFERENCE"), x.Box(2500, 0, 0, 5000, 300, 3000), f0.Add("IFCHALFSPACESOLID", plane, True));
        x.Material(x.Element("IFCWALL", "cut", "Cut", x.Place(0, 3000, 0), x.Shape(clipped, "Clipping"), E("STANDARD")), x.Brick);
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "under-roof.ifc")), null, out _);
        var a = f.Model.Get(Guid("foreign-test", "a"))!;
        Assert.Equal(IfcClass.Wall, a.Class);
        Assert.Equal("none", a.Str("roofTop"));
        var box = f.Bounds(a.Id);
        Assert.Equal(2.8, box.Max.Z, 4);
        // Flat all along: its volume the file's, 5 × 0.3 × 2.8.
        Assert.Equal(4.2, f.Session.Geometry(a.Id)!.Parts.Sum(q => Measure.Volume(q.Solid)), 3);
        Assert.True(CheckOf(r, a.Id).Pass, CheckOf(r, a.Id).Reason);
        // The cut one follows what is over it (auto) but never above its own top.
        var cut = f.Model.Get(Guid("foreign-test", "cut"))!;
        Assert.Null(cut.Str("roofTop"));
        Assert.InRange(f.Bounds(cut.Id).Max.Z, 0, cut.Num("height") + 1e-6);
    }

    [Fact]
    public void The_gate_measures_a_wall_a_slab_cuts_against_the_files_wall_cut_the_same()
    {
        // A wall 2.8 m high whose head the file's slab (2.6 to 2.8 m) overlaps: the project's cut order takes the slab out of
        // the wall, so the wall counts 2.6 m and the slab its whole; the file counts the overlap in both. Compared like with
        // like, the wall is faithful.
        var x = new Foreign();
        FootprintWall(x, "a", (0, -150), (5000, -150), (5000, 150), (0, 150));
        x.Material(x.Element("IFCSLAB", "slab", "Floor", x.Place(0, 0, 0), x.Shape(x.Box(2500, 2000, 2600, 6000, 5000, 200)), E("FLOOR")), x.Brick);
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "wall-slab.ifc")), null, out _);
        Assert.Equal(IfcClass.Slab, f.Model.Get(Guid("foreign-test", "slab"))!.Class);
        var a = CheckOf(r, Guid("foreign-test", "a"));
        output.WriteLine(a.ToJson().ToJsonString());
        Assert.Equal(5 * 0.3 * 2.6, a.NativeVolume!.Value, 3);
        Assert.Equal(5 * 0.3 * 2.6, a.FileVolume!.Value, 3);
        Assert.True(a.Pass, a.Reason);
        Assert.True(CheckOf(r, Guid("foreign-test", "slab")).Pass);
    }
}
