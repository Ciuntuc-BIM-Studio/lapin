using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// Foreign columns, beams and roofs where the file has them (docs/spec-ifc.md, Recunoașterea and Poarta de fidelitate): a
/// column keeps its base and its own height; a beam whose profile is no rectangle, and a roof whose slabs slope, stay references.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>
    /// Three columns 300 × 300 of one file type "C30" on the storey at 0: one from −1.50 to −0.30 m (under its storey's level),
    /// one 3 m high, one 2.76 m.
    /// </summary>
    string GateColumns()
    {
        var x = new Foreign();
        var low = x.Element("IFCCOLUMN", "low", "C-low", x.Place(0, 0, 0), x.Shape(x.Box(0, 0, -1500, 300, 300, 1200)), E("COLUMN"));
        var tall = x.Element("IFCCOLUMN", "tall", "C-tall", x.Place(3000, 0, 0), x.Shape(x.Box(0, 0, 0, 300, 300, 3000)), E("COLUMN"));
        var short_ = x.Element("IFCCOLUMN", "short", "C-short", x.Place(6000, 0, 0), x.Shape(x.Box(0, 0, 0, 300, 300, 2760)), E("COLUMN"));
        var type = x.F.Add("IFCCOLUMNTYPE", x.G("c30"), Null, S("C30"), Null, Null, Null, Null, Null, Null, E("COLUMN"));
        x.F.Add("IFCRELDEFINESBYTYPE", x.G("c30-rel"), Null, Null, Null, L([low, tall, short_]), type);
        return x.Write(Path.Combine(_dir, "columns.ifc"));
    }

    [Fact]
    public void A_column_below_its_storeys_level_keeps_its_base()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, GateColumns(), null, out _);
        var id = Guid("foreign-test", "low");
        var c = f.Model.Get(id)!;
        Assert.Equal(IfcClass.Column, c.Class);
        Assert.False(StoredBodies.Has(c));
        Assert.Equal(-1.5, c.Num("baseOffset"), 6);
        var (min, max) = f.Bounds(id);
        Assert.Equal(-1.5, min.Z, 4);
        Assert.Equal(-0.3, max.Z, 4);
        var check = CheckOf(r, id);
        output.WriteLine(check.ToJson().ToJsonString());
        Assert.True(check.Measured);
        Assert.True(check.Pass, check.Reason);
    }

    [Fact]
    public void Columns_of_one_type_with_different_heights_keep_their_own()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, GateColumns(), null, out _);
        var m = f.Model;
        var ids = new[] { "low", "tall", "short" }.Select(k => Guid("foreign-test", k)).ToList();
        // One type for the file's one, each column as high as the file has it.
        var types = ids.Select(id => m.RelatingOf(id, IfcClass.RelDefinesByType)?.Id).Distinct().ToList();
        Assert.Single(types);
        Assert.Equal("C30", m.Get(types[0]!)!.Name);
        Assert.Equal([1.2, 3.0, 2.76], ids.Select(id => Math.Round(Columns.Spec(m, m.Get(id)!).Height, 6)));
        Assert.Equal(2.76, f.Bounds(ids[2]).Max.Z, 4);
        Assert.Equal(3.0, f.Bounds(ids[1]).Max.Z, 4);
        foreach (var id in ids)
        {
            var check = CheckOf(r, id);
            Assert.True(check.Measured);
            Assert.True(check.Pass, $"{m.Get(id)!.Name}: {check.Reason}");
        }
        Assert.Equal(0, r.Fidelity!.Failed);
    }

    [Fact]
    public void A_beam_whose_profile_is_no_rectangle_stays_a_reference()
    {
        var x = new Foreign();
        var f0 = x.F;
        // A T 200 wide, 200 deep, its flange and web 10 thick, swept 4 m along x with its top at 3 m.
        var t = f0.Add("IFCPOLYLINE", L(new[] { "(-100.,0.)", "(100.,0.)", "(100.,-10.)", "(5.,-10.)", "(5.,-200.)", "(-5.,-200.)", "(-5.,-10.)", "(-100.,-10.)", "(-100.,0.)" }.Select(p => f0.Add("IFCCARTESIANPOINT", p))));
        var at = f0.Add("IFCAXIS2PLACEMENT3D", f0.Add("IFCCARTESIANPOINT", "(0.,0.,3000.)"), f0.Add("IFCDIRECTION", "(1.,0.,0.)"), f0.Add("IFCDIRECTION", "(0.,1.,0.)"));
        var solid = f0.Add("IFCEXTRUDEDAREASOLID", f0.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, t), at, f0.Add("IFCDIRECTION", "(0.,0.,1.)"), "4000.");
        x.Element("IFCBEAM", "tee", "T-beam", x.Place(0, 0, 0), x.Shape(solid), E("BEAM"));
        // A rectangle 200 × 300, the same way: native.
        var rect = f0.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, f0.Add("IFCAXIS2PLACEMENT2D", f0.Add("IFCCARTESIANPOINT", "(0.,-150.)"), Null), "200.", "300.");
        var at2 = f0.Add("IFCAXIS2PLACEMENT3D", f0.Add("IFCCARTESIANPOINT", "(0.,2000.,3000.)"), f0.Add("IFCDIRECTION", "(1.,0.,0.)"), f0.Add("IFCDIRECTION", "(0.,1.,0.)"));
        x.Element("IFCBEAM", "plain-beam", "B", x.Place(0, 0, 0), x.Shape(f0.Add("IFCEXTRUDEDAREASOLID", rect, at2, f0.Add("IFCDIRECTION", "(0.,0.,1.)"), "4000.")), E("BEAM"));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "tee.ifc")), null, out _);
        var tee = f.Model.Get(Guid("foreign-test", "tee"))!;
        Assert.True(StoredBodies.Has(tee));
        Assert.Contains(r.Skipped!.Keys, k => k.Contains("profile is not a rectangle"));
        var plain = f.Model.Get(Guid("foreign-test", "plain-beam"))!;
        Assert.Equal(IfcClass.Beam, plain.Class);
        Assert.False(StoredBodies.Has(plain));
        Assert.True(CheckOf(r, plain.Id).Pass, CheckOf(r, plain.Id).Reason);
    }

    [Fact]
    public void A_beam_of_two_B_reps_is_not_read_as_one_of_them()
    {
        // A beam 200 × 300 in two pieces, 0–2 m and 2–5 m along x, each a B-rep of its own (as BURKWIL's split beams are):
        // not the longer piece's prism, but a reference, which recognition from its triangles then makes one 5 m beam of.
        var x = new Foreign();
        var a = BrepBox(x, 0, -100, 2700, 2000, 100, 3000);
        var b = BrepBox(x, 2000, -100, 2700, 5000, 100, 3000);
        var shape = x.F.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([x.F.Add("IFCSHAPEREPRESENTATION", x.Ctx, S("Body"), S("Brep"), L([a, b]))]));
        x.Element("IFCBEAM", "split", "B-split", x.Place(0, 0, 0), shape, E("BEAM"));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "split.ifc")), null, out _);
        Assert.Contains(r.Skipped!.Keys, k => k.Contains("several bodies"));
        var beam = f.Model.Get(Guid("foreign-test", "split"))!;
        Assert.Equal(IfcClass.Beam, beam.Class);
        Assert.Equal(5, (Args.Point(beam.Attrs["end"], "end") - Args.Point(beam.Attrs["start"], "start")).Length, 4);
        var check = CheckOf(r, beam.Id, "triangles");
        Assert.True(check.Pass, check.Reason);
    }

    [Fact]
    public void A_foreign_roof_of_sloping_slabs_stays_as_it_came()
    {
        // Two slabs 10 × 6 m stacked, sloping 10° up along y, each extruded straight up (as BURKWIL's are): its profile in the
        // sloping plane, its extrusion vertical in the world.
        var x = new Foreign();
        var f0 = x.F;
        var (s, c) = (Math.Sin(10 * Math.PI / 180), Math.Cos(10 * Math.PI / 180));
        string Slab(string key, double z0, double h)
        {
            var prof = f0.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, f0.Add("IFCAXIS2PLACEMENT2D", f0.Add("IFCCARTESIANPOINT", "(5000.,3000.)"), Null), "10000.", "6000.");
            var at = f0.Add("IFCAXIS2PLACEMENT3D", f0.Add("IFCCARTESIANPOINT", $"(0.,0.,{R(z0)})"), f0.Add("IFCDIRECTION", $"(0.,{R(-s)},{R(c)})"), f0.Add("IFCDIRECTION", "(1.,0.,0.)"));
            var solid = f0.Add("IFCEXTRUDEDAREASOLID", prof, at, f0.Add("IFCDIRECTION", $"(0.,{R(s)},{R(c)})"), R(h));
            var slab = f0.Add("IFCSLAB", x.G(key), Null, S(key), Null, Null, x.Place(0, 0, 0), x.Shape(solid), Null, E("ROOF"));
            x.Material(slab, f0.Add("IFCMATERIAL", S(key + " material"), Null, Null));
            return slab;
        }
        var roof = x.Element("IFCROOF", "sloped-roof", "Shed", x.Place(0, 0, 0), Null, E("SHED_ROOF"));
        x.F.Add("IFCRELAGGREGATES", x.G("sloped-agg"), Null, Null, Null, roof, L([Slab("deck", 3000, 200), Slab("cover", 3200, 50)]));
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "shed.ifc")), null, out _);
        output.WriteLine(r.ToJson().ToJsonString());
        Assert.NotEqual(IfcClass.Roof, f.Model.Get(Guid("foreign-test", "sloped-roof"))?.Class);
        Assert.DoesNotContain(r.Fidelity?.Checks ?? [], k => k.IfcClass == IfcClass.Roof && k.Measured && !k.Pass);
    }
}
