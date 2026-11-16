using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Holes in outlines (audit S4): a floor, a ceiling or a platform keeps islands cut out of its own outline (<c>holes</c>, and
/// <c>holeArcs</c> for curved edges), not separate openings: built round them, measured without them, checked inside the outline
/// and apart, edited by the corner commands with <c>hole</c>, from several closed loops in Edit sketch, carried to IFC as more
/// curves of the footprint and read back. What was saved before (no holes) reads unchanged.
/// </summary>
public sealed class OutlineHolesTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-outline-holes").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static JsonArray Nums(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static double Q(Fixture f, string id, string set, string name) => f.Session.Quantities(id).First(q => q.Name == set).Quantities.Single(x => x.Name == name).Value;
    static Fixture F() => new(new ArchitectureModule(), new CivilModule(), new Lapin.Drafting.DraftingModule(), new StudioModule());
    const double Thick = 0.22;

    static string Floor(Fixture f, JsonArray? holes = null, JsonArray? holeArcs = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (10, 0), (10, 8), (0, 8)) };
        if (holes != null) a[OutlineHoles.Key] = holes;
        if (holeArcs != null) a[OutlineHoles.ArcsKey] = holeArcs;
        return (string)f.Run("architecture.addSlab", a)!["id"]!;
    }

    [Fact]
    public void A_floor_with_a_hole_is_built_round_it_and_measured_without_it()
    {
        var f = F();
        var slab = Floor(f, new JsonArray(Pts((2, 2), (4, 2), (4, 4), (2, 4))));
        Assert.Equal(80 - 4, Q(f, slab, "Qto_SlabBaseQuantities", "GrossArea"), 9);
        Assert.Equal((80 - 4) * Thick, Vol(f, slab), 6);
        // In plan, the hole is a loop of its own.
        Assert.Equal(2, f.Session.Geometry(slab)!.Plan!.Count(p => p.Kind == "outline"));
        // A round hole, its quarter arcs: exactly π r².
        var (c, r) = ArcOutline.Circle(new Vec2(7, 5), 1);
        var round = Floor(f, new JsonArray(new JsonArray([.. c.Select(p => (JsonNode)new JsonArray(p.X, p.Y))])), new JsonArray(Nums(r)));
        Assert.Equal(80 - Math.PI, Q(f, round, "Qto_SlabBaseQuantities", "GrossArea"), 9);
        Assert.True(Math.Abs(Vol(f, round) - (80 - Math.PI) * Thick) / ((80 - Math.PI) * Thick) < 0.001);
        // Clockwise as drawn, kept counter-clockwise.
        var cw = Floor(f, new JsonArray(Pts((2, 2), (2, 4), (4, 4), (4, 2))));
        var held = OutlineHoles.Of(f.Model.Get(cw)!)[0];
        Assert.True(Polygon2.SignedArea(held.Corners) > 0);
    }

    [Fact]
    public void A_hole_outside_the_outline_or_over_another_is_refused_and_a_floor_saved_before_keeps_none()
    {
        var f = F();
        Assert.Throws<CommandException>(() => Floor(f, new JsonArray(Pts((8, 2), (12, 2), (12, 4), (8, 4)))));
        Assert.Throws<CommandException>(() => Floor(f, new JsonArray(Pts((2, 2), (4, 2), (4, 4), (2, 4)), Pts((3, 3), (5, 3), (5, 5), (3, 5)))));
        var plain = Floor(f);
        Assert.Null(f.Model.Get(plain)!.Attrs[OutlineHoles.Key]);
        Assert.Null(f.Model.Get(plain)!.Attrs[OutlineHoles.ArcsKey]);
        // The outline shrunk past a hole is refused; the floor stays as it was.
        var holed = Floor(f, new JsonArray(Pts((6, 2), (8, 2), (8, 4), (6, 4))));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateSlab", new JsonObject { ["id"] = holed, ["points"] = Pts((0, 0), (5, 0), (5, 8), (0, 8)) }));
        Assert.Single(OutlineHoles.Of(f.Model.Get(holed)!));
    }

    [Fact]
    public void Holes_are_added_edited_and_removed_one_undo_step_each()
    {
        var f = F();
        var slab = Floor(f);
        Assert.Equal(slab, (string)f.Run("architecture.addOutlineHole", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((2, 2), (4, 2), (4, 4), (2, 4)) })!["id"]!);
        Assert.Single(OutlineHoles.Of(f.Model.Get(slab)!));
        // Its corner rounded, its edge bent, by the outline's commands with the hole's index.
        f.Run("architecture.roundCorner", new JsonObject { ["id"] = slab, ["hole"] = 0, ["index"] = 0, ["radius"] = 0.5 });
        Assert.Equal(5, OutlineHoles.Of(f.Model.Get(slab)!)[0].Corners.Count);
        Assert.Equal(76 + 0.25 * (1 - Math.PI / 4), Q(f, slab, "Qto_SlabBaseQuantities", "GrossArea"), 9);
        f.Session.Undo();
        Assert.Equal(4, OutlineHoles.Of(f.Model.Get(slab)!)[0].Corners.Count);
        f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = slab, ["hole"] = 0, ["index"] = 0, ["rise"] = -0.5 });
        Assert.NotNull(f.Model.Get(slab)!.Attrs[OutlineHoles.ArcsKey]);
        Assert.Throws<CommandException>(() => f.Run("architecture.roundCorner", new JsonObject { ["id"] = slab, ["hole"] = 3, ["index"] = 0, ["radius"] = 0.5 }));
        f.Run("architecture.removeOutlineHole", new JsonObject { ["id"] = slab, ["hole"] = 0 });
        Assert.Null(f.Model.Get(slab)!.Attrs[OutlineHoles.Key]);
        Assert.Null(f.Model.Get(slab)!.Attrs[OutlineHoles.ArcsKey]);
        f.Session.Undo();
        Assert.Single(OutlineHoles.Of(f.Model.Get(slab)!));
        // Moved, the holes go with the outline.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(slab), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal(3, OutlineHoles.Of(f.Model.Get(slab)!)[0].Corners.Min(p => p.X), 9);
    }

    [Fact]
    public void A_ceiling_and_a_platform_take_holes()
    {
        var f = F();
        var ceiling = (string)f.Run("architecture.addCeiling", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["holes"] = new JsonArray(Pts((1, 1), (2, 1), (2, 2), (1, 2))) })!["id"]!;
        Assert.Equal(23, Q(f, ceiling, "Qto_CoveringBaseQuantities", "GrossArea"), 9);
        Assert.True(f.Session.Geometry(ceiling)!.Parts.Count > 0);
        var t = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -10, ["y1"] = -10, ["x2"] = 30, ["y2"] = 30 })!["id"]!;
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["elevation"] = -1 })!["id"]!;
        f.Run("civil.addOutlineHole", new JsonObject { ["points"] = Pts((1, 1), (3, 1), (3, 3), (1, 3)) });
        Assert.Single(OutlineHoles.Of(f.Model.Get(pad)!));
        // Its ground kept inside the hole: 24 − 4 m² cut a metre down.
        var cut = f.Session.Quantities(t).Single(q => q.Name == "Lapin_EarthworkQuantities").Quantities.Single(x => x.Name == "CutVolume").Value;
        Assert.Equal(20, cut, 4);
    }

    [Fact]
    public void Edit_sketch_takes_several_loops_the_largest_the_outline_the_others_its_holes()
    {
        var f = F();
        var slab = Floor(f, new JsonArray(Pts((2, 2), (4, 2), (4, 4), (2, 4))));
        var r = (JsonObject)f.Run("studio.editOutline", new JsonObject { ["id"] = slab })!;
        var view = (string)r["viewId"]!;
        // Four edges of the outline and four of the hole.
        Assert.Equal(8, r["ids"]!.AsArray().Count);
        // A circle drawn inside: a second hole.
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(7, 5), ["r"] = 1 } });
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        var e = f.Model.Get(slab)!;
        Assert.Equal(4, ArcOutline.CornersOf(e).Count);
        Assert.Equal(2, OutlineHoles.Of(e).Count);
        Assert.Equal(80 - 4 - Math.PI, Q(f, slab, "Qto_SlabBaseQuantities", "GrossArea"), 9);
        // Opened again, the sketch it was finished with.
        var again = (JsonObject)f.Run("studio.editOutline", new JsonObject { ["id"] = slab })!;
        Assert.Equal(9, again["ids"]!.AsArray().Count);
        f.Run("studio.cancelOutline", new JsonObject { ["viewId"] = (string)again["viewId"]! });
    }

    [Fact]
    public void Ifc_carries_the_holes_as_curves_of_the_footprint_and_reads_them_back()
    {
        var f = F();
        var (c, r) = ArcOutline.Circle(new Vec2(7, 5), 1);
        var slab = Floor(f, new JsonArray(Pts((2, 2), (4, 2), (4, 4), (2, 4)), new JsonArray([.. c.Select(p => (JsonNode)new JsonArray(p.X, p.Y))])), new JsonArray(null, Nums(r)));
        var path = Path.Combine(_dir, "holes.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        Assert.Equal(3, step.OfType("IFCINDEXEDPOLYCURVE").Count());
        var g = F();
        IfcImport.Run(g.Session, path, null, out _);
        var back = g.Model.Get(slab)!;
        Assert.Equal(2, OutlineHoles.Of(back).Count);
        Assert.Equal(Q(f, slab, "Qto_SlabBaseQuantities", "GrossArea"), Q(g, slab, "Qto_SlabBaseQuantities", "GrossArea"), 6);
    }
}
