using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A material's cut hatch from its name as Revit's default materials draw theirs (decision 460, after 456): brick and
/// masonry 45° lines (MASONRY), concrete blocks a diagonal crosshatch (BLOCK), concrete (cast in place or reinforced) the
/// speckle, rigid insulation a fine square grid (RIGID), batts BATT, gypsum and plaster a stipple (GYPSUM), sand a dense
/// one (SAND), wood, steel, earth and gravel theirs; glass, air, finishes and a name nothing matches none. Brick's courses
/// stay an elevation's. A project saved before keeps the hatches it drew: by decision 325's names when it has no mark,
/// by decision 456's when it has the first.
/// </summary>
public sealed class SectionHatchDefaultTests
{
    static Entity Mat(string name, JsonObject? attrs = null) => new("m", IfcClass.Material, name, attrs ?? []);

    [Theory]
    [InlineData("Brick", "MASONRY")]
    [InlineData("Masonry", "MASONRY")]
    [InlineData("Cărămidă plină", "MASONRY")]
    [InlineData("Natural stone", "MASONRY")]
    [InlineData("Zidărie BCA", "BLOCK")]
    [InlineData("Concrete block", "BLOCK")]
    [InlineData("Concrete", "CONCRETE")]
    [InlineData("Concrete, cast-in-place", "CONCRETE")]
    [InlineData("Beton C25/30", "CONCRETE")]
    [InlineData("Reinforced concrete", "CONCRETE")]
    [InlineData("Beton armat", "CONCRETE")]
    [InlineData("EPS 100", "RIGID")]
    [InlineData("XPS", "RIGID")]
    [InlineData("Mineral wool", "BATT")]
    [InlineData("Gypsum board", "GYPSUM")]
    [InlineData("Tencuială", "GYPSUM")]
    [InlineData("Timber", "WOOD")]
    [InlineData("Plywood", "WOODCROSS")]
    [InlineData("Steel", "STEEL")]
    [InlineData("Earth", "EARTH")]
    [InlineData("Sand", "SAND")]
    [InlineData("Gravel", "GRAVEL")]
    [InlineData("Clay tile", "TILES")]
    public void A_name_suggests_Revit_s_cut_pattern(string name, string hatch)
    {
        Assert.Equal(hatch, MaterialGraphics.Resolve(Mat(name), "section").Hatch);
        Assert.Equal(hatch, MaterialGraphics.Resolve(Mat(name), "plan").Hatch);
        Assert.NotNull(HatchLibrary.Find(hatch));
    }

    [Theory]
    [InlineData("Glass")]
    [InlineData("Air gap")]
    [InlineData("Paint")]
    [InlineData("Vapour barrier")]
    [InlineData("Unobtainium")]
    public void Glass_air_paint_membranes_and_unknown_names_take_none(string name) => Assert.Null(MaterialGraphics.Resolve(Mat(name), "section").Hatch);

    [Fact]
    public void Brick_courses_stay_the_elevation_s_and_an_IFC_hatch_of_no_known_name_stays_none()
    {
        Assert.Equal("BRICK", MaterialGraphics.Resolve(Mat("Facing brick"), "elevation").Hatch);
        Assert.Null(MaterialGraphics.FromIfcHatch("Whatever", null, null, null));
        Assert.Equal("MASONRY", MaterialGraphics.FromIfcHatch("Masonry hatch", null, null, null)!.Hatch);
    }

    static Fixture Old(out Dictionary<string, string> ids)
    {
        var f = new Fixture();
        ids = new Dictionary<string, string>();
        foreach (var name in new[] { "Brick", "Unobtainium", "Concrete", "Own", "Filled brick", "Plaster", "Reinforced concrete", "EPS" })
            ids[name] = (string)f.Run("material.create", new JsonObject { ["name"] = name })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = ids["Own"], ["section"] = new JsonObject { ["hatch"] = "CROSS" } });
        f.Run("material.update", new JsonObject { ["id"] = ids["Filled brick"], ["section"] = new JsonObject { ["fill"] = "#eeeeee" } });
        return f;
    }

    /// <summary>The project as a build with that mark saved it (null: none).</summary>
    static List<Entity> SavedAt(Fixture f, int? rev) => f.Model.Entities.Select(e =>
    {
        if (e.Class != IfcClass.Project) return e;
        var a = e.CloneAttrs();
        if (rev is { } r) a[MaterialHatchUpgrade.RevKey] = r; else a.Remove(MaterialHatchUpgrade.RevKey);
        return e.With(attrs: a);
    }).ToList();

    [Fact]
    public void A_project_saved_before_decision_456_keeps_the_hatches_its_materials_took_from_their_names()
    {
        var f = Old(out var ids);
        var es = SavedAt(f, null);
        var (up, _) = f.Session.Upgraded(es, f.Model.Relations.ToList());
        Entity Of(string name) => up.Single(e => e.Id == ids[name]);
        string? Hatch(string name, string kind = "section") => MaterialGraphics.Resolve(Of(name), kind).Hatch;
        // As decision 325 drew them: brick in courses, an unknown name none, plaster none, EPS as batts.
        Assert.Equal("BRICK", Hatch("Brick"));
        Assert.Equal("BRICK", Hatch("Brick", "plan"));
        Assert.Null(Hatch("Unobtainium"));
        Assert.Null(Hatch("Plaster"));
        Assert.Equal("BATT", Hatch("EPS"));
        Assert.Equal("CONCRETE", Hatch("Reinforced concrete"));
        // Its fill kept with the hatch.
        Assert.Equal(("BRICK", "#eeeeee"), (Hatch("Filled brick"), MaterialGraphics.Resolve(Of("Filled brick"), "section").Fill));
        // What the new suggestions draw the same is left as it was, and what the user chose too.
        Assert.Same(es.Single(e => e.Id == ids["Concrete"]), Of("Concrete"));
        Assert.Same(es.Single(e => e.Id == ids["Unobtainium"]), Of("Unobtainium"));
        Assert.Same(es.Single(e => e.Id == ids["Own"]), Of("Own"));
        Assert.Equal(MaterialHatchUpgrade.Rev, (int)up.Single(e => e.Class == IfcClass.Project).Attrs[MaterialHatchUpgrade.RevKey]!);
        // Upgraded once; a new project has the mark and takes the new suggestions.
        var (again, _) = f.Session.Upgraded(up, f.Model.Relations.ToList());
        Assert.Same(Of("Brick"), again.Single(e => e.Id == ids["Brick"]));
        Assert.Equal(MaterialHatchUpgrade.Rev, (int)Spatial.Project(f.Model)!.Attrs[MaterialHatchUpgrade.RevKey]!);
        Assert.Equal("MASONRY", MaterialGraphics.Resolve(f.Model.Get(ids["Brick"]), "section").Hatch);
    }

    [Fact]
    public void A_project_saved_with_decision_456_keeps_its_hatches_too()
    {
        var f = Old(out var ids);
        var (up, _) = f.Session.Upgraded(SavedAt(f, 1), f.Model.Relations.ToList());
        string? Hatch(string name) => MaterialGraphics.Resolve(up.Single(e => e.Id == ids[name]), "section").Hatch;
        // As decision 456 drew them: brick and unknown names 45° lines, reinforced concrete RCONCRETE, plaster none.
        Assert.Equal("LINES45", Hatch("Brick"));
        Assert.Equal("LINES45", Hatch("Unobtainium"));
        Assert.Equal("RCONCRETE", Hatch("Reinforced concrete"));
        Assert.Null(Hatch("Plaster"));
        Assert.Equal("BATT", Hatch("EPS"));
        Assert.Equal("CONCRETE", Hatch("Concrete"));
    }
}
