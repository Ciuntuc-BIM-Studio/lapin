using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A linked IFC's walls and floors split by their layer sets (decision 400): "engineers don't want to see wall and floor
/// finishings" — the finishes of a link's walls and slabs hidden by the Finishes subcategory, in a plan and in 3D, the
/// elements staying references.
/// </summary>
public class LinkLayersTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    /// <summary>An architect's model written by Lapin: a wall at an angle, plaster inside, brick, wool and render outside; a floor with parquet and screed on concrete.</summary>
    static byte[] Architect()
    {
        var f = F();
        var type = f.Type("Insulated brick", ("Lime plaster", 0.015, false), ("Brick", 0.25, true), ("Mineral wool", 0.1, false), ("Silicate render", 0.01, false));
        f.Wall(0, 0, 4, 3, type);
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Parquet", ["thickness"] = 0.015 },
                new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 },
                new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.15, ["core"] = true }),
        })!["id"]!;
        f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["typeId"] = slabType,
            ["points"] = new JsonArray(new JsonArray(10, 0), new JsonArray(14, 0), new JsonArray(14, 4), new JsonArray(10, 4)),
        });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "architect.ifc", new IfcExportOptions("IFC4", "group", null));
        return ms.ToArray();
    }

    static (Fixture F, Entity Wall, Entity Slab, string Link) Linked()
    {
        var f = F();
        var r = IfcImport.Link(f.Session, Architect(), "architect.ifc", null, new IfcImportOptions("reference"), out _);
        var wall = f.Model.OfClass(IfcClass.Wall).Single(w => w.Str(Links.Key) == r.LinkId);
        var slab = f.Model.OfClass(IfcClass.Slab).Single(w => w.Str(Links.Key) == r.LinkId);
        return (f, wall, slab, r.LinkId!);
    }

    static double Thickness(GeometryPart p, Vec3 axis)
    {
        var d = p.Solid.Vertices.Select(v => Vec3.Dot(v, axis)).ToList();
        return d.Max() - d.Min();
    }

    [Fact]
    public void A_linked_wall_and_slab_come_in_their_layers_and_stay_references()
    {
        var (f, wall, slab, _) = Linked();
        Assert.True(StoredBodies.Has(wall));
        Assert.True(ReferenceLayers.IsReference(wall));
        var set = ReferenceLayers.Of(f.Model, wall)!;
        Assert.Equal(["finishes", "core", "insulation", "finishes"], set.Layers.Select(l => l.Sub));
        // Its parts, one a layer, as thick as the file's layers, across the wall's axis.
        var g = f.Session.Geometry(wall.Id)!;
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], g.Parts.Select(p => p.Key));
        var axis = new Vec3(-3, 4, 0).Normalized();
        Assert.Equal([0.015, 0.25, 0.1, 0.01], g.Parts.Select(p => Math.Round(Thickness(p, axis), 4)));
        Assert.Equal("Brick", g.Parts[1].Material);
        // The slab: parquet on top of the screed on the concrete.
        var gs = f.Session.Geometry(slab.Id)!;
        Assert.Equal(3, gs.Parts.Count);
        var tops = gs.Parts.Select(p => p.Solid.Vertices.Max(v => v.Z)).ToList();
        Assert.True(tops[0] > tops[1] && tops[1] > tops[2]);
        Assert.Equal(["finishes", "otherLayers", "core"], ReferenceLayers.Of(f.Model, slab)!.Layers.Select(l => l.Sub));
        // The export writes the file's body back whole, not the layers.
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "again.ifc", new IfcExportOptions("IFC4", "group", null));
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.DoesNotContain("layerAxis", text);
    }

    [Fact]
    public void Engineers_hide_a_link_s_finishes_in_a_plan_and_in_3D()
    {
        var (f, wall, slab, link) = Linked();
        var storey = Spatial.StoreyOf(f.Model, wall.Id)!.Id;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey, ["scale"] = 50 })!["id"]!;
        List<string> Cut(string id)
        {
            var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, plan)!);
            Assert.True(d != null, problem);
            return d!.Cuts.Where(c => c.Element == id).Select(c => c.Key ?? "").Distinct().Order().ToList();
        }
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Cut(wall.Id));
        // The link's own categories for this plan: walls ▸ finishes off.
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["link"] = link, ["value"] = new JsonObject { ["display"] = "custom" } });
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["link"] = link, ["category"] = "walls", ["sub"] = "finishes", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal(["layer:1", "layer:2"], Cut(wall.Id));
        // In 3D, by host view: the floor's parquet and the walls' plaster and render hidden as parts.
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["category"] = "walls", ["sub"] = "finishes", ["value"] = new JsonObject { ["hidden"] = true } });
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["category"] = "slabs", ["sub"] = "finishes", ["value"] = new JsonObject { ["hidden"] = true } });
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.Equal(["layer:0", "layer:3"], looks[wall.Id]!["parts"]!.AsObject().Select(p => p.Key).Order());
        Assert.Equal(["layer:0"], looks[slab.Id]!["parts"]!.AsObject().Select(p => p.Key));
        // A structural style (cores only) draws the link's cores alone, as a native wall's.
        var styles = f.Session.Query("viewStyle.list", new JsonObject())!.AsArray().OfType<JsonObject>();
        var structural = (string)styles.First(s => (string?)s["layers"] == "core")["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = structural });
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["link"] = link, ["value"] = null });
        Assert.Equal(["layer:1"], Cut(wall.Id));
    }

    [Fact]
    public void A_reference_of_one_finish_hides_with_the_finishes_and_a_part_takes_its_wall_s_category()
    {
        var f = F();
        var record = new Entity("rec", IfcSource.RecordClass, "IFC record", new JsonObject { ["snap"] = new JsonObject { ["t"] = "IFCMATERIAL", ["a"] = new JsonArray("'Gypsum plaster'", "$", "$") } });
        var part = new Entity("part", "IfcBuildingElementPart", "Plaster", new JsonObject
        {
            ["reference"] = true, [ReferenceLayers.PartOfKey] = IfcClass.Wall,
            [IfcSource.Key] = new JsonObject { ["material"] = new JsonObject { ["record"] = "rec" } },
        });
        f.Session.Edit("put", e => { e.Add(record); e.Add(part); });
        Assert.Equal("walls", ReferenceLayers.CategoryOf(part));
        Assert.Equal("finishes", ReferenceLayers.WholeSub(f.Model, part));
        var g = new JsonObject { ["categories"] = new JsonObject { ["walls"] = new JsonObject { ["sub"] = new JsonObject { ["finishes"] = new JsonObject { ["hidden"] = true } } } } };
        Assert.True(VisibilityGraphics.From(f.Model, g, false).Hides(part));
        Assert.False(VisibilityGraphics.From(f.Model, null, false).Hides(part));
        // By the IFC Category, by the name, in Romanian too.
        Assert.Equal("insulation", ReferenceLayers.SubOf("Insulation", "Whatever"));
        Assert.Equal("finishes", ReferenceLayers.SubOf(null, "Tencuială var-ciment"));
        Assert.Equal("insulation", ReferenceLayers.SubOf(null, "Polistiren expandat EPS"));
        Assert.Equal("membranes", ReferenceLayers.SubOf(null, "Vapour barrier foil"));
        Assert.Equal("otherLayers", ReferenceLayers.SubOf(null, "Concrete C25/30"));
        Assert.Equal("finishes", ReferenceLayers.SubOf(null, "Putz Aussen"));
        Assert.Equal("insulation", ReferenceLayers.SubOf(null, "Dämmung Allgemein"));
    }

    /// <summary>
    /// A real file linked as references, its layered walls and slabs split (opt-in: <c>LAPIN_LINK_LAYERS=&lt;file.ifc&gt;</c>):
    /// how many have a layer set, how many split, and the time it takes.
    /// </summary>
    [Fact]
    public void A_real_file_s_layers_when_asked()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_LINK_LAYERS") is not { Length: > 0 } path || !File.Exists(path)) return;
        var f = F();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = IfcImport.Link(f.Session, File.ReadAllBytes(path), Path.GetFileName(path), path, new IfcImportOptions("reference"), out _);
        var linked = sw.Elapsed.TotalSeconds;
        var layered = f.Model.Entities.Where(e => e.Str(Links.Key) == r.LinkId && ReferenceLayers.Of(f.Model, e) is { Layers.Count: > 1 }).ToList();
        sw.Restart();
        var split = layered.Count(e => f.Session.Geometry(e.Id)?.Parts.Any(p => p.Key?.StartsWith("layer:") == true) == true);
        var withAxis = layered.Count(e => e.Attrs[ReferenceLayers.AxisKey] != null);
        Console.WriteLine($"LINK-LAYERS {Path.GetFileName(path)}: linked in {linked:F1} s; {layered.Count} layered references ({withAxis} with their axis), {split} split; geometry {sw.Elapsed.TotalSeconds:F2} s");
        var whole = f.Model.Entities.Where(e => e.Str(Links.Key) == r.LinkId && ReferenceLayers.WholeSub(f.Model, e) != null)
            .GroupBy(e => (ReferenceLayers.CategoryOf(e) ?? e.Class) + " ▸ " + ReferenceLayers.WholeSub(f.Model, e));
        foreach (var g in whole) Console.WriteLine($"  whole: {g.Count()} × {g.Key}");
        foreach (var g in layered.Where(e => f.Session.Geometry(e.Id)?.Parts.Any(p => p.Key?.StartsWith("layer:") == true) != true).GroupBy(e => e.Class))
            Console.WriteLine($"  not split: {g.Count()} × {g.Key}");
    }
}
