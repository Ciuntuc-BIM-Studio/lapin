using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>
/// A path built of a hardscape type (docs/plan-site-grading.md, T4, the user's answer of 23.11.2026): switched on, its body's
/// layers are the type's, top down, in their materials, with each layer's quantities; the type edited, the path follows in the
/// same step; switched off, it is its own courses again, exactly as before; a path that never had one is as it was; IFC 4.3 and
/// back with its type.
/// </summary>
public sealed class PathHardscapeTypeTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule());

    /// <summary>A 2 m alley 20 m long, level (an alley's crossfall is none unless asked): 40 m² paved.</summary>
    static string Alley(Fixture f) =>
        (string)f.Run("civil.addPath", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(20, 0)), ["width"] = 2.0, ["storeyId"] = f.Storey })!["id"]!;

    static double Vol(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.Where(p => p.Key == key).Sum(p => Measure.Volume(p.Solid));

    static string[] Keys(Fixture f, string id) => [.. f.Session.Geometry(id)!.Parts.Select(p => p.Key!)];

    static double Q(Fixture f, string id, string set, string name, string? part = null) =>
        f.Session.Quantities(id).First(s => s.Name == set && s.Part == part).Quantities.First(q => q.Name == name).Value;

    static string Print(Fixture f, string id)
    {
        var e = f.Model.Get(id)!;
        var g = f.Session.Geometry(id)!;
        return e.Attrs.ToJsonString() + "|" + e.PredefinedType + "|" + string.Join(",", g.Parts.Select(p => $"{p.Key}:{p.Material}:{Measure.Volume(p.Solid):0.000000}"));
    }

    [Fact]
    public void With_a_type_its_body_is_the_type_s_layers_with_their_quantities()
    {
        var f = F();
        var id = Alley(f);
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["hardscapeType"] = "pavers" });
        var e = f.Model.Get(id)!;
        var type = PathTypes.TypeOf(f.Model, e)!;
        Assert.Equal("Concrete pavers", type.Name);
        Assert.Equal("RIGID", e.PredefinedType);
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(f, id));
        Assert.Equal(40 * 0.06, Vol(f, id, "layer:0"), 6);
        Assert.Equal(40 * 0.04, Vol(f, id, "layer:1"), 6);
        Assert.Equal(40 * 0.20, Vol(f, id, "layer:2"), 6);
        var g = f.Session.Geometry(id)!;
        Assert.Equal("Concrete pavers", f.Model.Get(g.Parts[0].Material!)!.Name);
        Assert.Equal("Crushed stone", f.Model.Get(g.Parts[2].Material!)!.Name);
        // The top of the build-up where its own surface course's top was; the bottom 30 cm under it.
        var top = Measure.Bounds(g.Parts[0].Solid)!.Value.Max.Z;
        Assert.Equal(top - 0.30, Measure.Bounds(g.Parts[2].Solid)!.Value.Min.Z, 9);
        // Its quantities: the top layer the surface course, the ones under it the base; each layer's as a hardscape's.
        Assert.Equal(40 * 0.06, Q(f, id, "Lapin_PavementQuantities", "SurfaceVolume"), 6);
        Assert.Equal(40 * 0.24, Q(f, id, "Lapin_PavementQuantities", "BaseVolume"), 6);
        Assert.Equal(0.04, Q(f, id, PathTypes.LayerSet, "Thickness", "layer:1"), 9);
        Assert.Equal(40, Q(f, id, PathTypes.LayerSet, "Area", "layer:1"), 6);
        Assert.Equal(40 * 0.20, Q(f, id, PathTypes.LayerSet, "Volume", "layer:2"), 6);
        // Its cross-section in the corridor configurator draws the layers in their materials.
        Assert.Equal("Sand bedding", SectionLooks.Of(f.Model, e, "layer:1").Material);
        // The interface's fields.
        var props = new JsonObject();
        foreach (var p in f.Session.Catalog.All<Lapin.Platform.INodeProps>()) p.Add(e, f.Model, props);
        Assert.Equal(type.Id, (string?)props["hardscapeTypeId"]);
        Assert.Equal(3, props["hardscapeLayers"]!.AsArray().Count);
    }

    [Fact]
    public void The_type_edited_the_path_follows_in_one_undo_step()
    {
        var f = F();
        var id = Alley(f);
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["useHardscapeType"] = true });
        var type = PathTypes.TypeOf(f.Model, f.Model.Get(id)!)!;
        // A hardscape of the same type: both follow the one edit.
        var plaza = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = new JsonArray(new JsonArray(30, 0), new JsonArray(40, 0), new JsonArray(40, 8), new JsonArray(30, 8)), ["typeId"] = type.Id, ["standalone"] = true, ["storeyId"] = f.Storey })!["id"]!;
        f.Run("civil.updateHardscapeType", new JsonObject
        {
            ["id"] = type.Id, ["pavement"] = "FLEXIBLE",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Asphalt wearing course", ["thickness"] = 0.05 }, new JsonObject { ["material"] = "Crushed stone", ["thickness"] = 0.3 }),
        });
        Assert.Equal(["layer:0", "layer:1"], Keys(f, id));
        Assert.Equal(40 * 0.05, Vol(f, id, "layer:0"), 6);
        Assert.Equal(40 * 0.30, Vol(f, id, "layer:1"), 6);
        Assert.Equal("FLEXIBLE", f.Model.Get(id)!.PredefinedType);
        Assert.Equal(80 * 0.35, f.Session.Geometry(plaza)!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
        f.Session.Undo();
        Assert.Equal(["layer:0", "layer:1", "layer:2"], Keys(f, id));
        Assert.Equal(40 * 0.06, Vol(f, id, "layer:0"), 6);
        Assert.Equal("RIGID", f.Model.Get(id)!.PredefinedType);
    }

    [Fact]
    public void Switched_off_it_is_its_own_courses_again_and_a_path_without_one_is_as_it_was()
    {
        var f = F();
        var id = Alley(f);
        var before = Print(f, id);
        Assert.Equal(["surface", "base"], Keys(f, id));
        Assert.Equal("FLEXIBLE", f.Model.Get(id)!.PredefinedType);
        Assert.DoesNotContain(f.Session.Quantities(id), s => s.Name == PathTypes.LayerSet);
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["hardscapeType"] = "stoneSlabs" });
        Assert.Equal(4, Keys(f, id).Length);
        // Its own courses changed meanwhile are kept for when it is switched off.
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["width"] = 2.0 });
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["useHardscapeType"] = false });
        Assert.Null(PathTypes.TypeOf(f.Model, f.Model.Get(id)!));
        Assert.Equal(before, Print(f, id));
        // Switched on again with no type named: the project's first.
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["useHardscapeType"] = true });
        Assert.Equal("Stone slabs", PathTypes.TypeOf(f.Model, f.Model.Get(id)!)!.Name);
        // Made with one.
        var other = (string)f.Run("civil.addPath", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 10), new JsonArray(20, 10)), ["width"] = 2.0, ["storeyId"] = f.Storey, ["hardscapeType"] = "gravel" })!["id"]!;
        Assert.Equal(["layer:0", "layer:1"], Keys(f, other));
        f.Session.Undo();
        Assert.Null(f.Model.Get(other));
    }

    [Fact]
    public void It_goes_to_IFC_4_3_with_its_type_and_comes_back()
    {
        var f = F();
        var id = Alley(f);
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["hardscapeType"] = "asphalt" });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "path.ifc", new IfcExportOptions(Schema: "IFC4X3"));
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCPAVEMENT(", text);
        Assert.Contains("IFCPAVEMENTTYPE(", text);
        Assert.Contains("'Asphalt'", text);
        Assert.Contains("'Asphalt wearing course'", text);
        Assert.DoesNotContain("'Pset_Lapin", text);
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "path.ifc", null, out _);
        var back = g.Model.Get(id);
        Assert.NotNull(back);
        Assert.Equal(IfcClass.Pavement, back!.Class);
        Assert.Equal("Asphalt", PathTypes.TypeOf(g.Model, back)?.Name);
        Assert.Equal(3, PathTypes.LayersOf(g.Model, back)!.Count);
        Assert.Equal(Keys(f, id), Keys(g, id));
        foreach (var k in Keys(f, id)) Assert.Equal(Vol(f, id, k), Vol(g, id, k), 6);
    }
}
