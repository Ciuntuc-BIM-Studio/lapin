using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// A foreign file's materials as its renderer would see them (report #76): a material or a body styled see-through comes in
/// see-through, and a window's material per part (its IfcMaterialConstituentSet) becomes its type's materials.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>A surface style: its colour (0–1) and transparency, as a rendering (IFC4's nine attributes) or a shading.</summary>
    static string SurfaceStyle(Foreign x, string name, double r, double g, double b, double transparency, bool rendering)
    {
        var colour = x.F.Add("IFCCOLOURRGB", Null, R(r), R(g), R(b));
        var shading = rendering
            ? x.F.Add("IFCSURFACESTYLERENDERING", colour, R(transparency), Null, Null, Null, Null, Null, Null, E("NOTDEFINED"))
            : x.F.Add("IFCSURFACESTYLESHADING", colour, R(transparency));
        return x.F.Add("IFCSURFACESTYLE", S(name), E("BOTH"), L([shading]));
    }

    [Fact]
    public void A_see_through_style_and_a_windows_constituents_come_in_with_the_file()
    {
        var x = new Foreign();
        // A material styled as a blue glass, 70 % see-through (its IfcMaterialDefinitionRepresentation), and an aluminium.
        var glass = x.F.Add("IFCMATERIAL", S("Blue glass"), Null, Null);
        var glassStyle = SurfaceStyle(x, "Blue glass", 0.4, 0.6, 0.8, 0.7, rendering: true);
        x.F.Add("IFCMATERIALDEFINITIONREPRESENTATION", Null, Null,
            L([x.F.Add("IFCSTYLEDREPRESENTATION", x.Ctx, Null, Null, L([x.F.Add("IFCSTYLEDITEM", Null, L([glassStyle]), Null)]))]), glass);
        var alu = x.F.Add("IFCMATERIAL", S("Alu RAL 7016"), Null, Null);

        var wallPl = x.Place(1000, 2000, 0);
        var wall = x.Element("IFCWALL", "wall", "W1", wallPl, x.Shape(x.Box(2500, 0, 0, 5000, 300, 2800)), E("STANDARD"));
        x.Material(wall, x.Brick);
        var op = x.Element("IFCOPENINGELEMENT", "win-op", "Window opening", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(2500, 0, 900, 1200, 500, 1500)), E("OPENING"));
        var win = x.Element("IFCWINDOW", "win", "F1", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(2500, 0, 900, 1200, 100, 1500)), "1500.", "1200.", E("WINDOW"), E("SINGLE_PANEL"), Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("v"), Null, Null, Null, wall, op);
        x.F.Add("IFCRELFILLSELEMENT", x.G("f"), Null, Null, Null, op, win);
        var set = x.F.Add("IFCMATERIALCONSTITUENTSET", S("Window"), Null, L([
            x.F.Add("IFCMATERIALCONSTITUENT", S("Lining"), Null, alu, Null, Null),
            x.F.Add("IFCMATERIALCONSTITUENT", S("Glazing"), Null, glass, Null, Null)]));
        x.Material(win, set);

        // A body styled see-through on its own (a glass balustrade a recogniser does not take: a reference), its style 60 %.
        var f0 = x.F;
        string P(string c) => f0.Add("IFCCARTESIANPOINT", c);
        var apex = P("(500.,500.,1000.)"); var b0 = P("(0.,0.,0.)"); var b1 = P("(1000.,0.,0.)"); var b2 = P("(1000.,1000.,0.)"); var b3 = P("(0.,1000.,0.)");
        string Tri(params string[] pts) => f0.Add("IFCFACE", L([f0.Add("IFCFACEOUTERBOUND", f0.Add("IFCPOLYLOOP", L(pts)), True)]));
        var pyramid = f0.Add("IFCFACETEDBREP", f0.Add("IFCCLOSEDSHELL", L([Tri(b0, b3, b2, b1), Tri(b0, b1, apex), Tri(b1, b2, apex), Tri(b2, b3, apex), Tri(b3, b0, apex)])));
        f0.Add("IFCSTYLEDITEM", pyramid, L([SurfaceStyle(x, "Railing glass", 0.5, 0.7, 0.9, 0.6, rendering: false)]), Null);
        x.Element("IFCRAILING", "railing", "Rail", x.Place(10000, 10000, 0), x.Shape(pyramid, "Brep"), E("HANDRAIL"));

        var f = Full();
        IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "materials.ifc")), null, out _);
        var m = f.Session.Model;

        // The material: its file's transparency, so its glass renders see-through.
        var blue = Materials.Find(m, "Blue glass")!;
        Assert.Equal(0.7, MaterialLook.Of(blue).Transparency, 3);
        Assert.Equal("#6699cc", blue.Str("color"));

        // The reference: its colour as the file has it, see-through through a material named after its style.
        var railing = m.Get(x.Gid("railing"))!;
        Assert.True(railing.Bool("reference"));
        var part = Assert.Single(f.Session.Geometry(railing.Id)!.Parts);
        Assert.Equal("#80b2e6", part.Color);
        var railGlass = m.Get(part.Material!)!;
        Assert.Equal("Railing glass", railGlass.Name);
        Assert.Equal(0.6, MaterialLook.Of(railGlass).Transparency, 3);

        // The window: its frame and its glass of the file's materials (Lining is IFC4's name for the frame).
        var window = m.Get(x.Gid("win"))!;
        Assert.Equal(IfcClass.Window, window.Class);
        var spec = Openings.SpecOf(m, window.Id)!;
        Assert.Equal("Alu RAL 7016", spec.Materials!.Frame);
        Assert.Equal("Blue glass", spec.Materials.Glass);
        Assert.Equal(blue.Id, f.Session.Geometry(window.Id)!.Parts.First(p => p.Key == "glass").Material);
    }
}
