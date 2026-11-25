using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// A foreign file's concrete as the analysis reads it (issue #121, finding 2): a material that names no C class is concrete when
/// its name is a Spanish designation (HA-25/B/20/IIa: EHE-08, Código Estructural) or says hormigón, or when it carries
/// Pset_MaterialConcrete; its fck from the file's set or its name, its Ecm by EN 1992-1-1 Table 3.1.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>A material with Pset_MaterialConcrete.CompressiveStrength (Pa, IFC's unit), as IfcOpenShell writes it.</summary>
    static string ConcreteMaterial(Foreign x, string name, double? fckPa)
    {
        var mat = x.F.Add("IFCMATERIAL", S(name), Null, Null);
        if (fckPa is { } pa)
            x.F.Add("IFCMATERIALPROPERTIES", S("Pset_MaterialConcrete"), Null,
                L([x.F.Add("IFCPROPERTYSINGLEVALUE", S("CompressiveStrength"), Null, $"IFCPRESSUREMEASURE({R(pa)})", Null)]), mat);
        return mat;
    }

    [Fact]
    public void A_spanish_concrete_and_one_known_by_its_pset_come_in_as_concrete_with_their_E()
    {
        var x = new Foreign();
        var ha = ConcreteMaterial(x, "HA-25/B/20/IIa", 25e6);
        var plain = ConcreteMaterial(x, "M-07", 30e6);

        var wall = x.Element("IFCWALL", "wall", "W1", x.Place(2500, 0, 0), x.Shape(x.Box(0, 0, 0, 5000, 200, 2800)), E("STANDARD"));
        x.Material(wall, ha);
        var slab = x.Element("IFCSLAB", "slab", "S1", x.Place(0, 0, 2800), x.Shape(x.Box(2500, 2000, 0, 5000, 4000, 200)), E("FLOOR"));
        x.Material(slab, ha);
        var f0 = x.F;
        var rect = f0.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, f0.Add("IFCAXIS2PLACEMENT2D", f0.Add("IFCCARTESIANPOINT", "(0.,-250.)"), Null), "300.", "500.");
        var at = f0.Add("IFCAXIS2PLACEMENT3D", f0.Add("IFCCARTESIANPOINT", "(0.,4000.,2800.)"), f0.Add("IFCDIRECTION", "(1.,0.,0.)"), f0.Add("IFCDIRECTION", "(0.,1.,0.)"));
        var beam = x.Element("IFCBEAM", "beam", "B1", x.Place(0, 0, 0), x.Shape(f0.Add("IFCEXTRUDEDAREASOLID", rect, at, f0.Add("IFCDIRECTION", "(0.,0.,1.)"), "5000.")), E("BEAM"));
        x.Material(beam, ha);
        var beam2 = x.Element("IFCBEAM", "beam2", "B2", x.Place(0, 0, 0),
            x.Shape(f0.Add("IFCEXTRUDEDAREASOLID", rect, f0.Add("IFCAXIS2PLACEMENT3D", f0.Add("IFCCARTESIANPOINT", "(0.,8000.,2800.)"), f0.Add("IFCDIRECTION", "(1.,0.,0.)"), f0.Add("IFCDIRECTION", "(0.,1.,0.)")),
                f0.Add("IFCDIRECTION", "(0.,0.,1.)"), "5000.")), E("BEAM"));
        x.Material(beam2, plain);

        var f = Full();
        IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "concrete-kind.ifc")), null, out _);
        var m = f.Session.Model;

        // The wall, the slab and the beam are of the Spanish concrete: fck from the file's set, Ecm of C25/30 (Table 3.1).
        foreach (var key in new[] { "wall", "slab", "beam" })
        {
            var e = m.Get(x.Gid(key))!;
            var id = CutOrder.MainMaterial(e, m);
            Assert.NotNull(id);
            var mat = StructuralMaterials.Of(m, m.Get(id!)!);
            Assert.Equal("HA-25/B/20/IIa", mat.Name);
            Assert.Equal("concrete", mat.Kind);
            Assert.Equal(25e6, mat.Fc!.Value, 1e-3);
            Assert.Contains("Pset_MaterialConcrete.CompressiveStrength", mat.Fc.Source);
            Assert.Equal(31e9, mat.E!.Value, 1e-3);
            Assert.Contains("Table 3.1", mat.E.Source);
            Assert.Equal(0.2, mat.Poisson!.Value, 9);
        }

        // A name that says nothing, but the file's Pset_MaterialConcrete: concrete, its Ecm from its fck by Table 3.1's formula.
        var other = StructuralMaterials.Of(m, m.Get(CutOrder.MainMaterial(m.Get(x.Gid("beam2"))!, m)!)!);
        Assert.Equal("M-07", other.Name);
        Assert.Equal("concrete", other.Kind);
        Assert.Equal(30e6, other.Fc!.Value, 1e-3);
        Assert.Equal(22e9 * Math.Pow((30 + 8) / 10.0, 0.3), other.E!.Value, 1e3);
    }
}
