using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 682's leftovers of stage 3 (docs/plan-material-graphics.md): Lapin's standard at version 1.1 for a project made since
/// (tones balanced: rigid insulation 1.5, blocks 1.25, concrete 0.75; floor coverings' surfaces), a project made before drawing
/// by 1.0 exactly; rooms' floor finishes hatched by the project's standard in a new project, by their own table in an old one.
/// </summary>
public class MaterialLookRevisionTests
{
    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    /// <summary>A project made before decision 682: its mark taken off.</summary>
    static void MakeOld(Fixture f) => f.Session.Edit("old", e =>
    {
        var p = Spatial.Project(e.Model)!;
        var a = p.CloneAttrs();
        a.Remove(MaterialStandards.LookRevKey);
        e.Replace(p.With(attrs: a));
    });

    [Fact]
    public void A_new_project_draws_by_Lapin_1_1_and_an_old_one_by_1_0_exactly()
    {
        var f = new Fixture();
        Assert.Equal(MaterialStandards.LookRev, (int)(double)Json.Number(Spatial.Project(f.Model)!.Attrs[MaterialStandards.LookRevKey])!);
        Assert.Same(MaterialStandard.LapinCurrent, MaterialStandards.Of(f.Model));
        Assert.Equal("1.2.0", MaterialStandards.Of(f.Model).Version);
        f.Type("W", ("Concrete", 0.2, true), ("EPS board", 0.1, false), ("Concrete block", 0.2, false), ("Brick masonry", 0.25, false));
        double Scale(Fixture x, string name) => MaterialGraphics.Resolve(x.Model.Get(Mat(x, name)), "section", standard: MaterialStandards.Of(x.Model)).HatchScale;
        Assert.Equal((0.75, 1.5, 1.25, 1.0), (Scale(f, "Concrete"), Scale(f, "EPS board"), Scale(f, "Concrete block"), Scale(f, "Brick masonry")));
        // The same names, the same patterns: only their sizes changed.
        foreach (var name in new[] { "Concrete", "EPS board", "Concrete block", "Brick masonry", "Mineral wool", "Gypsum plaster", "Oak", "Steel" })
            Assert.Equal(MaterialStandard.Lapin.CutRule(name)?.Section?.Hatch, MaterialStandard.LapinCurrent.CutRule(name)?.Section?.Hatch);
        // The lists show the version the project draws by.
        Assert.Equal("1.2.0", (string?)f.Session.Query("standards.materialList", [])!["standards"]![0]!["version"]);

        MakeOld(f);
        Assert.Same(MaterialStandard.Lapin, MaterialStandards.Of(f.Model));
        Assert.Equal((1.0, 1.0, 1.0, 1.0), (Scale(f, "Concrete"), Scale(f, "EPS board"), Scale(f, "Concrete block"), Scale(f, "Brick masonry")));
        Assert.Equal("1.0.0", (string?)f.Session.Query("standards.materialList", [])!["standards"]![0]!["version"]);
        Assert.Equal("1.0.0", MaterialStandard.Lapin.Version);
        // 1.1 from the store's pack: kept beside as a copy (a built-in is never changed), which the old project may apply.
        var text = (string)new Fixture().Session.Query("standards.export", new JsonObject { ["materialStandards"] = new JsonArray("lapin"), ["only"] = "materialGraphics" })!["text"]!;
        Assert.False((bool)f.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!["materialStandards"]![0]!["same"]!);
        f.Run("standards.importFrom", new JsonObject { ["text"] = text });
        var copy = MaterialStandards.Own(Spatial.Project(f.Model)).Single();
        Assert.Equal("1.2.0", copy.Version);
        f.Run("standards.applyMaterials", new JsonObject { ["id"] = copy.Id });
        Assert.Equal(0.75, Scale(f, "Concrete"));
    }

    [Fact]
    public void Lapin_1_1_names_floor_coverings_seen_from_above()
    {
        string? Surface(string name) => MaterialStandard.LapinCurrent.ElevationRule(name)?.Elevation?.Hatch is { Length: > 0 } h ? h : null;
        Assert.Equal("BOARDS_150", Surface("Oak parquet"));
        Assert.Equal("BOARDS_150", Surface("Laminate"));
        Assert.Equal("CROSS", Surface("Carpet"));
        Assert.Equal("HORIZONTAL", Surface("Vinyl flooring"));
        Assert.Equal("CONCRETE", Surface("Epoxy coating"));
        Assert.Equal("CONCRETE", Surface("Screed"));
        Assert.Equal("TILE_600X600", Surface("Gresie porțelanată"));
        Assert.Equal("TILE_300X300", Surface("Faianță"));
        // A facade's concrete stays plain, as 1.0 has it.
        Assert.Null(Surface("Concrete C25/30"));
        Assert.Null(MaterialStandard.Lapin.ElevationRule("Carpet"));
    }

    static RoomFinishSpec Floor(params string[] materials) =>
        RoomFinishSpec.Default() with { Floor = [.. materials.Select(m => new FinishLayer(m, 0.01, null))] };

    [Fact]
    public void A_new_project_s_floor_finishes_take_the_project_s_standard_and_an_old_one_s_their_table()
    {
        var f = new Fixture();
        // Lapin's own: what its table said for the usual floors.
        Assert.Equal("BOARDS_150", RoomFinishes.HatchOf(f.Model, Floor("Laminate", "Screed"), "floor")!.Pattern);
        Assert.Equal("CROSS", RoomFinishes.HatchOf(f.Model, Floor("Carpet"), "floor")!.Pattern);
        Assert.Equal("CONCRETE", RoomFinishes.HatchOf(f.Model, Floor("Epoxy coating", "Screed"), "floor")!.Pattern);
        // A standard of the project's own: its surface rule for the carpet, at its scale.
        f.Run("standards.materialSet", new JsonObject
        {
            ["standard"] = new JsonObject
            {
                ["id"] = "office", ["name"] = "Office",
                ["rules"] = new JsonArray(new JsonObject { ["match"] = new JsonObject { ["names"] = new JsonArray("carpet") }, ["elevation"] = new JsonObject { ["hatch"] = "STEEL", ["hatchScale"] = 2 } }),
            },
        });
        f.Run("standards.applyMaterials", new JsonObject { ["id"] = "office" });
        var h = RoomFinishes.HatchOf(f.Model, Floor("Carpet"), "floor")!;
        Assert.Equal(("STEEL", 2.0), (h.Pattern, h.Scale));
        // A floor nothing names: the 45° lines, as before.
        Assert.Equal("LINES45", RoomFinishes.HatchOf(f.Model, Floor("Odd stuff"), "floor")!.Pattern);
        // A project made before: its own table, whatever the standard.
        MakeOld(f);
        Assert.Equal("CROSS", RoomFinishes.HatchOf(f.Model, Floor("Carpet"), "floor")!.Pattern);
    }
}
