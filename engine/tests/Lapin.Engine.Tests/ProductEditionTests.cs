using System.Text;
using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// An edition of Lapin's modules under its own name (products/&lt;id&gt;.json): a civil one without Architecture,
/// Structure and MEP starts a new project on one site level, groups its views as civil and writes its own name
/// into the files it exports; a product that says nothing starts with the building storeys.
/// </summary>
public class ProductEditionTests
{
    static readonly ProductManifest Civil = new("civil-edition", "Acme Civil", ["core", "drafting", "studio", "phases", "civil"],
        Storeys: [new ProductStorey("Site", 0, 3)], Disciplines: ["Civil"]);

    [Fact]
    public void A_civil_edition_starts_on_its_site_without_the_building_modules_and_names_itself_in_IFC()
    {
        var s = new Session(ModuleCatalog.Create([new DraftingModule(), new StudioModule(), new PhasesModule(), new CivilModule()]), Civil);
        s.New();
        Assert.Equal(["Site"], Spatial.Storeys(s.Model).Select(x => x.Name));
        Assert.DoesNotContain(s.Info().Modules, m => m.Id is "architecture" or "structure" or "mep");
        s.Execute("civil.addFlatTerrain", new JsonObject { ["x1"] = -50, ["y1"] = -50, ["x2"] = 50, ["y2"] = 50, ["elevation"] = 10 }, out _);

        using var ms = new MemoryStream();
        IfcExport.Write(s, ms, "civil.ifc", new IfcExportOptions("IFC4X3_ADD2"));
        // The file says which application wrote it: the edition's name, not Lapin's.
        Assert.Contains("'Acme Civil','Acme Civil'", Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public void A_product_that_says_nothing_starts_with_the_building_storeys()
    {
        var s = new Session(ModuleCatalog.Create([new CivilModule()]), new ProductManifest("lapin", "Lapin", ["core", "civil"]));
        s.New();
        Assert.Equal(["Foundations", "Ground floor", "First floor"], Spatial.Storeys(s.Model).OrderBy(x => x.Num("elevation")).Select(x => x.Name));
    }
}
