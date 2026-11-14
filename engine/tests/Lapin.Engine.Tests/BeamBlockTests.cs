using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Beam-and-block floors (a tester's report, #134): POT joists at a spacing, MIAKO blocks between them, a cast topping.</summary>
public class BeamBlockTests
{
    static readonly Vec2[] Rect = [new(0, 0), new(5, 0), new(5, 4), new(0, 4)];
    static readonly Vec2[] L = [new(0, 0), new(8, 0), new(8, 4), new(4, 4), new(4, 7), new(0, 7)];
    static readonly Vec2[] Hole = [new(1, 4.5), new(2.2, 4.5), new(2.2, 5.5), new(1, 5.5)];
    static readonly BeamBlockSpec Spec = BeamBlockSpec.Default;
    /// <summary>A POT joist's section: its foot and its web.</summary>
    static readonly double JoistArea = 0.16 * 0.06 + 0.06 * (0.175 - 0.06);

    [Fact]
    public void On_a_rectangle_the_joists_span_the_short_way_at_the_spacing_and_go_into_the_supports()
    {
        var r = BeamBlock.Layout(Spec, Rect, [], BeamBlock.Direction(Spec, Rect));
        // 5 × 4 m: the joists span the 4 m (along y), 625 mm apart across the 5 m, centred.
        Assert.Equal(new Vec2(0, 1), r.Along);
        Assert.Equal(8, r.Joists.Count);
        var xs = r.Joists.Select(j => -j.V).OrderBy(x => x).ToList();
        Assert.Equal(0.3125, xs[0], 6);
        Assert.Equal(5 - 0.3125, xs[^1], 6);
        Assert.All(xs.Zip(xs.Skip(1)), p => Assert.Equal(0.625, p.Second - p.First, 6));
        // Each from edge to edge and 125 mm into the supports at both ends: 4.25 m, ordered as such.
        Assert.All(r.Joists, j => { Assert.Equal(-0.125, j.U0, 6); Assert.Equal(4.125, j.U1, 6); Assert.Equal(4.25, j.Ordered, 6); });
        // Seven rows of 16 whole blocks (4 m / 250 mm), none cut.
        Assert.Equal(7 * 16, r.WholeBlocks);
        Assert.Equal(0, r.CutBlocks);
        Assert.All(r.Blocks, b => Assert.Equal(0.625 - 0.16, b.V1 - b.V0, 6));
    }

    [Fact]
    public void A_row_not_a_multiple_of_the_block_ends_in_a_cut_block()
    {
        Vec2[] floor = [new(0, 0), new(3.1, 0), new(3.1, 4.1), new(0, 4.1)];
        var r = BeamBlock.Layout(Spec with { Direction = "y" }, floor, [], new Vec2(0, 1));
        // 4.1 m: 16 whole blocks and one of 100 mm in each row.
        var rows = r.Joists.Count - 1;
        Assert.Equal(16 * rows, r.WholeBlocks);
        Assert.Equal(rows, r.CutBlocks);
        Assert.All(r.Blocks.Where(b => !b.Whole), b => Assert.Equal(0.1, b.U1 - b.U0, 6));
    }

    [Fact]
    public void An_L_shape_with_an_opening_keeps_every_piece_in_the_floor_and_out_of_the_opening()
    {
        var spec = Spec with { Direction = "x" };
        var r = BeamBlock.Layout(spec, L, [Hole], new Vec2(1, 0));
        Assert.Equal(new Vec2(1, 0), r.Along);
        // Across the 7 m: (7 - 0.16) / 0.625 → 11 joists.
        Assert.Equal(11 + 2, r.Joists.Count); // the two joists through the opening's band are each cut in two
        bool InHole(double x0, double x1, double y0, double y1) => x0 < 2.2 - 1e-6 && x1 > 1 + 1e-6 && y0 < 5.5 - 1e-6 && y1 > 4.5 + 1e-6;
        Assert.DoesNotContain(r.Blocks, b => InHole(Math.Min(r.W(b.U0, b.V0).X, r.W(b.U1, b.V1).X), Math.Max(r.W(b.U0, b.V0).X, r.W(b.U1, b.V1).X),
            Math.Min(r.W(b.U0, b.V0).Y, r.W(b.U1, b.V1).Y), Math.Max(r.W(b.U0, b.V0).Y, r.W(b.U1, b.V1).Y)));
        Assert.DoesNotContain(r.Joists, j => InHole(j.U0, j.U1, j.V - 0.08, j.V + 0.08));
        // The blocks lie inside the L; the joists run past its edges only by the bearing.
        Assert.All(r.Blocks, b => Assert.All(new[] { r.W(b.U0, b.V0), r.W(b.U1, b.V1), r.W(b.U0, b.V1), r.W(b.U1, b.V0) },
            p => Assert.True(p.X >= -1e-6 && p.Y >= -1e-6 && p.X <= 8 + 1e-6 && p.Y <= 7 + 1e-6 && !(p.X > 4 + 1e-6 && p.Y > 4 + 1e-6), p.ToString())));
        Assert.All(r.Joists, j => Assert.True(j.U0 >= -0.125 - 1e-6 && j.U1 <= 8.125 + 1e-6));
        // The upper leg's joists are 4 m long and into the supports: 4.25 m.
        Assert.Contains(r.Joists, j => Math.Abs(j.V - 6.5) < 0.4 && Math.Abs(j.Length - 4.25) < 1e-6);
    }

    static (Fixture F, string Slab, string Type) Floor(Vec2[] outline, Vec2[]? hole = null, BeamBlockSpec? spec = null)
    {
        var f = new Fixture(new ArchitectureModule());
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts(outline) })!["id"]!;
        if (hole != null) f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = Pts(hole) });
        var type = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id;
        f.Run("architecture.setBeamBlock", new JsonObject { ["id"] = type, ["spec"] = (spec ?? Spec).ToJson() });
        return (f, slab, type);
    }

    static JsonArray Pts(IEnumerable<Vec2> ps) => new([.. ps.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]);

    [Fact]
    public void A_floor_of_a_beam_and_block_type_is_built_of_joists_blocks_and_cast_concrete()
    {
        var (f, slab, type) = Floor(Rect);
        // The type's core is the structure: 190 + 60 mm; the finish and the screed stay over it.
        var layers = LayerSets.LayersOf(f.Model, type);
        Assert.Equal(0.25, layers.Single(l => l.Core).Thickness, 6);
        Assert.Equal(3, layers.Count);
        Assert.NotNull(Materials.Find(f.Model, "Ceramic infill block"));
        var g = f.Session.Geometry(slab)!;
        Assert.Equal(8, g.Parts.Count(p => p.Key!.StartsWith("bb-joist:")));
        Assert.DoesNotContain(g.Parts, p => p.Key == "layer:2");
        var blocks = g.Parts.Single(p => p.Key == "bb-blocks");
        Assert.Equal(112, blocks.Instances!.Count);
        Assert.Equal(112 * 0.25 * 0.465 * 0.19, Measure.Volume(blocks.Solid), 6);
        // The concrete: the structure's volume less the joists (inside the floor) and the blocks.
        var concrete = Measure.Volume(g.Parts.Single(p => p.Key == "bb-concrete").Solid);
        Assert.Equal(20 * 0.25 - 8 * 4 * JoistArea - 112 * 0.25 * 0.465 * 0.19, concrete, 4);
        // Each joist's body: its T swept 4.25 m.
        Assert.Equal(4.25 * JoistArea, Measure.Volume(g.Parts.First(p => p.Key == "bb-joist:1").Solid), 6);
        // The plan: the span arrow and the joists' axes.
        Assert.True(g.Plan!.Count(p => p.Kind == "symbol") >= 8 + 3);

        var q = f.Session.Reader.Quantities(slab).Single(s => s.Name == "Lapin_BeamBlockQuantities").Quantities;
        double Q(string n) => q.Single(x => x.Name == n).Value;
        Assert.Equal(8, Q("JoistCount"));
        Assert.Equal(8 * 4.25, Q("JoistLength"), 6);
        Assert.Equal(112, Q("BlockCount"));
        Assert.Equal(0, Q("CutBlockCount"));
        Assert.Equal(concrete, Q("ConcreteVolume"), 6);
        Assert.Equal(20, Q("Area"), 6);
        Assert.Equal(20, Q("MeshArea"), 6);

        var rows = BeamBlockSchedule.Rows(new Qc(f));
        Assert.Contains(rows, r => r.Item == "Joist" && r.Count == 8 && Math.Abs(r.Length - 4.25) < 1e-9);
        Assert.Contains(rows, r => r.Item == "Infill block" && r.Count == 112);
    }

    [Fact]
    public void An_opening_and_an_edge_set_span_are_respected()
    {
        var (f, slab, _) = Floor(L, Hole);
        var g = f.Session.Geometry(slab)!;
        Assert.DoesNotContain(g.Diagnostics ?? [], d => d.Contains("core"));
        var b = Measure.Bounds(g.Parts.Single(p => p.Key == "bb-concrete").Solid)!.Value;
        Assert.Equal(-0.07 - 0.25, b.Min.Z, 6); // under the finish and the screed
        // The joists across the outline's first edge (along x): the joists run along y.
        f.Run("architecture.setFloorSpan", new JsonObject { ["ids"] = new JsonArray(slab), ["edge"] = 0 });
        Assert.Equal(90, f.Model.Get(slab)!.Num(BeamBlock.SpanKey), 6);
        var along = BeamBlock.Of(f.Model, f.Model.Get(slab)!)!.Result.Along;
        Assert.Equal(0, along.X, 6);
        f.Run("architecture.setFloorSpan", new JsonObject { ["ids"] = new JsonArray(slab) });
        Assert.Null(f.Model.Get(slab)!.NumOrNull(BeamBlock.SpanKey));
    }

    [Fact]
    public void Ifc_writes_the_joists_and_the_blocks_as_parts_whose_ids_stay_when_the_floor_is_rebuilt()
    {
        var (f, slab, _) = Floor(Rect);
        HashSet<string> Ids()
        {
            var path = Path.Combine(Path.GetTempPath(), $"bb-{Guid.NewGuid():N}.ifc");
            try
            {
                IfcExport.Write(f.Session, path);
                var file = StepFile.Read(path);
                Assert.Equal(8, file.OfType("IFCBEAM").Count(b => b[8] is StepEnum { Value: "JOIST" }));
                Assert.Contains(file.OfType("IFCBUILDINGELEMENTPART"), p => p[4] as string == "Infill block");
                return file.OfType("IFCBEAM").Concat(file.OfType("IFCBUILDINGELEMENTPART")).Select(e => e[0]!.ToString()!).ToHashSet();
            }
            finally { File.Delete(path); }
        }
        var before = Ids();
        // Rebuilt (moved up): the same parts, the same GlobalIds.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["offset"] = 0.1 });
        Assert.Equal(before, Ids());
    }

    [Fact]
    public void Setting_the_type_is_one_undo_step_and_a_bad_spec_is_refused()
    {
        var f = new Fixture(new ArchitectureModule());
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts(Rect) })!["id"]!;
        var type = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id;
        var core = LayerSets.LayersOf(f.Model, type).Single(l => l.Core).Thickness;
        f.Run("architecture.setBeamBlock", new JsonObject { ["id"] = type, ["spec"] = Spec.ToJson() });
        Assert.NotNull(BeamBlockSpec.Of(f.Model.Get(type)));
        f.Session.Undo();
        Assert.Null(BeamBlockSpec.Of(f.Model.Get(type)));
        Assert.Equal(core, LayerSets.LayersOf(f.Model, type).Single(l => l.Core).Thickness, 9);
        Assert.Contains(f.Session.Geometry(slab)!.Parts, p => p.Key == "layer:2");
        Assert.Throws<CommandException>(() => f.Run("architecture.setBeamBlock", new JsonObject { ["id"] = type, ["spec"] = (Spec with { Spacing = 0.2 }).ToJson() }));
        // Back to layers.
        f.Run("architecture.setBeamBlock", new JsonObject { ["id"] = type, ["spec"] = Spec.ToJson() });
        f.Run("architecture.setBeamBlock", new JsonObject { ["id"] = type });
        Assert.Contains(f.Session.Geometry(slab)!.Parts, p => p.Key == "layer:2");
    }

    [Fact]
    public void The_users_catalogue_comes_in_from_a_csv_as_floor_types()
    {
        var f = new Fixture(new ArchitectureModule());
        var csv = "Name;Joist width (mm);Joist height (mm);Spacing (mm);Block height (mm);Topping (mm);Block material\n" +
                  "My 23/62,5;160;175;625;230;60;MIAKO 23\nMy 19/50;160;175;500;190;50;MIAKO 19\n";
        var names = (JsonArray)f.Run("architecture.importBeamBlockTypes", new JsonObject { ["csv"] = csv })!["names"]!;
        Assert.Equal(2, names.Count);
        var t = f.Model.OfClass(IfcClass.SlabType).Single(x => x.Name == "My 23/62,5");
        var spec = BeamBlockSpec.Of(t)!;
        Assert.Equal(0.23, spec.BlockHeight, 9);
        Assert.Equal("MIAKO 23", spec.BlockMaterial);
        Assert.Equal(0.29, LayerSets.LayersOf(f.Model, t.Id).Single(l => l.Core).Thickness, 9);
        Assert.Throws<CommandException>(() => f.Run("architecture.importBeamBlockTypes", new JsonObject { ["csv"] = "Name;Spacing (mm)\nBad;100\n" }));
        // The preview is the layout's own.
        var p = (JsonObject)f.Session.Query("architecture.beamBlockPreview", new JsonObject { ["spec"] = Spec.ToJson() })!;
        Assert.Equal(0.25, (double)p["depth"]!, 9);
        Assert.True((int)p["counts"]!["joists"]! > 0);
    }

    sealed class Qc(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
