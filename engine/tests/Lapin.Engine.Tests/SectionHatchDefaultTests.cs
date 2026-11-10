using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A material's cut hatch from its name (decision 456, a tester's request): the section symbols drawings use (masonry
/// and brick as 45° lines, concrete speckled, reinforced concrete speckled over 45° lines, insulation, timber, steel,
/// earth), the 45° lines for a name nothing matches; brick's courses stay an elevation's. A project saved before keeps
/// the hatches it drew: the upgrade writes the old suggestion on each material that took one from its name.
/// </summary>
public sealed class SectionHatchDefaultTests
{
    static Entity Mat(string name, JsonObject? attrs = null) => new("m", IfcClass.Material, name, attrs ?? []);

    [Theory]
    [InlineData("Brick", "LINES45")]
    [InlineData("Masonry", "LINES45")]
    [InlineData("Cărămidă plină", "LINES45")]
    [InlineData("Zidărie BCA", "LINES45")]
    [InlineData("Concrete", "CONCRETE")]
    [InlineData("Beton C25/30", "CONCRETE")]
    [InlineData("Reinforced concrete", "RCONCRETE")]
    [InlineData("Beton armat", "RCONCRETE")]
    [InlineData("EPS 100", "BATT")]
    [InlineData("Timber", "WOOD")]
    [InlineData("Steel", "STEEL")]
    [InlineData("Earth", "EARTH")]
    [InlineData("Gravel", "GRAVEL")]
    [InlineData("Natural stone", "LINES45")]
    [InlineData("Unobtainium", "LINES45")]
    public void A_name_suggests_its_section_symbol(string name, string hatch)
    {
        Assert.Equal(hatch, MaterialGraphics.Resolve(Mat(name), "section").Hatch);
        Assert.Equal(hatch, MaterialGraphics.Resolve(Mat(name), "plan").Hatch);
        Assert.NotNull(HatchLibrary.Find(hatch));
    }

    [Theory]
    [InlineData("Plaster")]
    [InlineData("Gypsum board")]
    [InlineData("Glass")]
    [InlineData("Air gap")]
    [InlineData("Vapour barrier")]
    public void Finishes_glass_air_and_membranes_take_no_hatch(string name) => Assert.Null(MaterialGraphics.Resolve(Mat(name), "section").Hatch);

    [Fact]
    public void Brick_courses_stay_the_elevation_s_and_an_IFC_hatch_of_no_known_name_stays_none()
    {
        Assert.Equal("BRICK", MaterialGraphics.Resolve(Mat("Facing brick"), "elevation").Hatch);
        Assert.Null(MaterialGraphics.FromIfcHatch("Whatever", null, null, null));
        Assert.Equal("LINES45", MaterialGraphics.FromIfcHatch("Masonry hatch", null, null, null)!.Hatch);
    }

    static Fixture Old(out Dictionary<string, string> ids)
    {
        var f = new Fixture();
        ids = new Dictionary<string, string>();
        foreach (var name in new[] { "Brick", "Unobtainium", "Concrete", "Own", "Filled brick", "Plaster", "Reinforced concrete" })
            ids[name] = (string)f.Run("material.create", new JsonObject { ["name"] = name })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = ids["Own"], ["section"] = new JsonObject { ["hatch"] = "CROSS" } });
        f.Run("material.update", new JsonObject { ["id"] = ids["Filled brick"], ["section"] = new JsonObject { ["fill"] = "#eeeeee" } });
        return f;
    }

    [Fact]
    public void A_project_saved_before_keeps_the_hatches_its_materials_took_from_their_names()
    {
        var f = Old(out var ids);
        var es = f.Model.Entities.Select(e =>
        {
            if (e.Class != IfcClass.Project) return e;
            var a = e.CloneAttrs();
            a.Remove(MaterialHatchUpgrade.RevKey);
            return e.With(attrs: a);
        }).ToList();
        var (up, _) = f.Session.Upgraded(es, f.Model.Relations.ToList());
        Entity Of(string name) => up.Single(e => e.Id == ids[name]);
        string? Hatch(string name, string kind = "section") => MaterialGraphics.Resolve(Of(name), kind).Hatch;
        // As decision 325 drew them: brick in courses, an unknown name none, reinforced concrete speckled.
        Assert.Equal("BRICK", Hatch("Brick"));
        Assert.Equal("BRICK", Hatch("Brick", "plan"));
        Assert.Null(Hatch("Unobtainium"));
        Assert.Equal("CONCRETE", Hatch("Reinforced concrete"));
        // Its fill kept with the hatch.
        Assert.Equal(("BRICK", "#eeeeee"), (Hatch("Filled brick"), MaterialGraphics.Resolve(Of("Filled brick"), "section").Fill));
        // What the new suggestions draw the same is left as it was, and what the user chose too.
        Assert.Same(es.Single(e => e.Id == ids["Concrete"]), Of("Concrete"));
        Assert.Same(es.Single(e => e.Id == ids["Plaster"]), Of("Plaster"));
        Assert.Same(es.Single(e => e.Id == ids["Own"]), Of("Own"));
        Assert.NotNull(up.Single(e => e.Class == IfcClass.Project).Attrs[MaterialHatchUpgrade.RevKey]);
        // Upgraded once; a new project has the mark and takes the new suggestions.
        var (again, _) = f.Session.Upgraded(up, f.Model.Relations.ToList());
        Assert.Same(Of("Brick"), again.Single(e => e.Id == ids["Brick"]));
        Assert.NotNull(Spatial.Project(f.Model)!.Attrs[MaterialHatchUpgrade.RevKey]);
        Assert.Equal("LINES45", MaterialGraphics.Resolve(f.Model.Get(ids["Brick"]), "section").Hatch);
    }

    [Fact]
    public void A_project_opened_again_keeps_the_new_suggestions()
    {
        var f = Old(out var ids);
        var (up, _) = f.Session.Upgraded(f.Model.Entities.ToList(), f.Model.Relations.ToList());
        Assert.Equal("LINES45", MaterialGraphics.Resolve(up.Single(e => e.Id == ids["Brick"]), "section").Hatch);
        Assert.Equal("RCONCRETE", MaterialGraphics.Resolve(up.Single(e => e.Id == ids["Reinforced concrete"]), "section").Hatch);
    }
}
