using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// SubD solids in Studio (docs/plan-studio-freeform.md §8, decision 772, stage F1b): made from a box, a cylinder, a sheet or a
/// solid's faces; their cage edited, shaped and set, each one undo step keeping the GlobalId; the cage read in the world for the
/// edit mode; booleans and steps on the body; quantities; IFC and .lapin keeping the cage.
/// </summary>
public class StudioSubDTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J3(double x, double y, double z) => [x, y, z];
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static string Id(JsonNode? r) => (string)r!["id"]!;
    static JsonArray Ints(params int[] xs) => new(xs.Select(x => (JsonNode)x).ToArray());

    static string Box(Fixture f, JsonObject? more = null)
    {
        var a = new JsonObject { ["shape"] = "box", ["at"] = J3(2, 3, 0) };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return Id(f.Run("studio.subdCreate", a));
    }

    static JsonObject Cage(Fixture f, string id) => (JsonObject)f.Session.Query("studio.subdCage", new JsonObject { ["id"] = id })!;
    static List<Vec3> Points(JsonObject cage) => cage["points"]!.AsArray().Select(p => Features.P(p)!.Value).ToList();
    static List<int[]> Faces(JsonObject cage) => cage["faces"]!.AsArray().Select(f => f!.AsArray().Select(k => (int)k!).ToArray()).ToList();

    /// <summary>The cage face whose points all stand at the top (largest z).</summary>
    static int TopFace(JsonObject cage)
    {
        var pts = Points(cage);
        var top = pts.Max(p => p.Z);
        return Faces(cage).FindIndex(f => f.All(k => Math.Abs(pts[k].Z - top) < 1e-9));
    }

    // ------------------------------------------------------------ shapes

    [Fact]
    public void Each_shape_makes_a_subd_solid_at_its_point()
    {
        var f = F();
        // A box 1 × 1 × 1 on (2, 3): its smooth body round its middle, 34/81 out from it (SubDTests' cube).
        var box = Box(f);
        var e = f.Model.Get(box)!;
        Assert.Equal("subd", e.Str("operation"));
        Assert.Equal(IfcClass.BuildingElementProxy, e.Class);
        var b = f.Bounds(box);
        Assert.Equal(2 + 34.0 / 81, b.Max.X, 6); Assert.Equal(3 - 34.0 / 81, b.Min.Y, 6);
        Assert.Equal(0.5 + 34.0 / 81, b.Max.Z, 6);
        Assert.Equal(0.32618, Vol(f, box), 4);
        Assert.Equal(Spatial.Storeys(f.Model).First().Id, Spatial.StoreyOf(f.Model, box)!.Id);

        // A cylinder 0.5 × 2 with creased rims: its ends flat at 0 and 2.
        var cyl = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "cylinder", ["at"] = J3(10, 0, 0), ["radius"] = 0.5, ["height"] = 2, ["sides"] = 12 }));
        var cb = f.Bounds(cyl);
        Assert.Equal(0, cb.Min.Z, 9); Assert.Equal(2, cb.Max.Z, 9);
        Assert.InRange(Vol(f, cyl), 0.9 * Math.PI * 0.25 * 2 * 0.9, Math.PI * 0.25 * 2);

        // A sheet 4 × 2: an open surface, drawn, measured by its area only, cutting nothing; thickened, a solid.
        var sheet = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "plane", ["at"] = J3(0, 10, 3), ["sizes"] = new JsonArray(4, 2), ["segments"] = new JsonArray(4, 2) }));
        Assert.True(StudioModule.IsOpenSurface(f.Model.Get(sheet)!));
        var sb = f.Bounds(sheet);
        Assert.Equal(3, sb.Min.Z, 9); Assert.Equal(3, sb.Max.Z, 9);
        var q = f.Session.Quantities(sheet).Single();
        Assert.DoesNotContain(q.Quantities, x => x.Name == "NetVolume");
        // Flat: its limit is the sheet less what the border's B-spline pulls in from the sides between its corners (they stay).
        var area = q.Quantities.Single(x => x.Name == "GrossSurfaceArea").Value;
        Assert.InRange(area, 7, 8);
        f.Run("studio.subdSet", new JsonObject { ["id"] = sheet, ["thickness"] = 0.2, ["side"] = "centre" });
        Assert.False(StudioModule.IsOpenSurface(f.Model.Get(sheet)!));
        Assert.Equal(area * 0.2, Vol(f, sheet), 6);
        sb = f.Bounds(sheet);
        Assert.Equal(2.9, sb.Min.Z, 9); Assert.Equal(3.1, sb.Max.Z, 9);

        // A solid's faces as a cage: a 1 m primitive box made smooth, its frame at the middle of its foot.
        var prim = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(20, 0) }));
        var from = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "fromSolid", ["sourceId"] = prim }));
        Assert.NotNull(f.Model.Get(prim));
        Assert.Equal(new Vec3(20, 0, 0), Features.P(f.Model.Get(from)!.Attrs["at"]));
        Assert.Equal(0.32618, Vol(f, from), 4);
        // Made in one undo step.
        f.Session.Undo();
        Assert.Null(f.Model.Get(from));
        Assert.NotNull(f.Model.Get(prim));

        Assert.Throws<CommandException>(() => f.Run("studio.subdCreate", new JsonObject { ["shape"] = "torus", ["at"] = J3(0, 0, 0) }));
        Assert.Throws<CommandException>(() => f.Run("studio.subdCreate", new JsonObject { ["shape"] = "box", ["at"] = J3(0, 0, 0), ["width"] = -1 }));
    }

    // ------------------------------------------------------------ edits, ops, settings: one undo step each, the id kept

    [Fact]
    public void Editing_the_cage_moves_its_points_in_the_world_as_one_undo_step_keeping_the_id()
    {
        var f = F();
        var box = Box(f, new JsonObject { ["rotation"] = 90 });
        var cage = Cage(f, box);
        // The query gives the cage in the world: turned 90° about (2, 3).
        Assert.Equal(8, Points(cage).Count);
        Assert.All(Points(cage), p => Assert.True(Math.Abs(Math.Abs(p.X - 2) - 0.5) < 1e-9 && Math.Abs(Math.Abs(p.Y - 3) - 0.5) < 1e-9));
        Assert.Equal(12, cage["edges"]!.AsArray().Count);
        Assert.True((bool)cage["closed"]!);
        Assert.Equal(4, (int)cage["level"]!);
        var before = Vol(f, box);
        var top = TopFace(cage);
        var diff = f.RunDiff("studio.subdEdit", new JsonObject { ["id"] = box, ["faces"] = Ints(top), ["translate"] = J3(0.5, 0, 1) });
        Assert.NotNull(diff);
        Assert.Equal(box, f.Model.Get(box)!.Id);
        var moved = Points(Cage(f, box));
        Assert.Equal(4, moved.Count(p => Math.Abs(p.Z - 2) < 1e-9));
        Assert.All(moved.Where(p => p.Z > 1), p => Assert.True(Math.Abs(Math.Abs(p.X - 2.5) - 0.5) < 1e-9));
        Assert.True(Vol(f, box) > before);
        // One undo step brings the cage back.
        f.Session.Undo();
        Assert.Equal(before, Vol(f, box), 12);

        // Scaled about their middle: the top face twice as wide, its middle where it was.
        f.Run("studio.subdEdit", new JsonObject { ["id"] = box, ["faces"] = Ints(top), ["scale"] = 2 });
        var scaled = Points(Cage(f, box)).Where(p => p.Z > 0.5).ToList();
        Assert.All(scaled, p => Assert.Equal(1, Math.Abs(p.X - 2), 9));
        // Turned 45° about the upright through their middle: the corners on the axes.
        f.Session.Undo();
        f.Run("studio.subdEdit", new JsonObject { ["id"] = box, ["faces"] = Ints(top), ["rotate"] = 45 });
        var turned = Points(Cage(f, box)).Where(p => p.Z > 0.5).ToList();
        Assert.All(turned, p => Assert.Equal(Math.Sqrt(0.5), (p.Xy - new Vec2(2, 3)).Length, 9));
        Assert.Contains(turned, p => Math.Abs(p.Y - 3) < 1e-9);

        Assert.Throws<CommandException>(() => f.Run("studio.subdEdit", new JsonObject { ["id"] = box, ["translate"] = J3(0, 0, 1) }));
        Assert.Throws<CommandException>(() => f.Run("studio.subdEdit", new JsonObject { ["id"] = box, ["vertices"] = Ints(99), ["translate"] = J3(0, 0, 1) }));
    }

    [Fact]
    public void Shaping_the_cage_extrudes_loops_deletes_and_creases_each_as_one_undo_step()
    {
        var f = F();
        var box = Box(f);
        var smooth = Vol(f, box);
        var cage = Cage(f, box);
        // Crease every edge: the cube itself.
        f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "crease", ["edges"] = cage["edges"]!.DeepClone(), ["sharpness"] = 10 });
        Assert.Equal(1, Vol(f, box), 9);
        Assert.Equal(1, (int)Cage(f, box)["level"]!);
        // Extrude its top 0.5 up: the top 0.5 higher, flat (its creased rim goes up with it and stays on the old rim too; the band's
        // uprights are smooth, so the sides bulge a little).
        var r = f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "extrude", ["faces"] = Ints(TopFace(Cage(f, box))), ["distance"] = 0.5 })!;
        Assert.Equal(10, (int)r["faces"]!);
        Assert.Equal(1.5, f.Bounds(box).Max.Z, 9);
        f.Session.Undo();
        Assert.Equal(1, Vol(f, box), 9);
        f.Session.Undo();
        Assert.Equal(smooth, Vol(f, box), 12);

        // A loop round the sides, a quarter up from the bottom.
        cage = Cage(f, box);
        var pts = Points(cage);
        var upright = cage["edges"]!.AsArray().Select(x => ((int)x![0]!, (int)x[1]!)).First(x => pts[x.Item1].Z != pts[x.Item2].Z);
        var low = pts[upright.Item1].Z < pts[upright.Item2].Z ? upright : (upright.Item2, upright.Item1);
        f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "loop", ["edge"] = Ints(low.Item1, low.Item2), ["fraction"] = 0.25 });
        cage = Cage(f, box);
        Assert.Equal(12, Points(cage).Count);
        Assert.Equal(4, Points(cage).Count(p => Math.Abs(p.Z - 0.25) < 1e-9));
        Assert.True(Vol(f, box) > smooth);
        f.Session.Undo();

        // Delete the top: an open surface, area only; given a thickness, a cup.
        f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "delete", ["faces"] = Ints(TopFace(Cage(f, box))) });
        Assert.False((bool)Cage(f, box)["closed"]!);
        Assert.Equal(4, Cage(f, box)["borders"]!.AsArray().Count);
        Assert.True(StudioModule.IsOpenSurface(f.Model.Get(box)!));
        f.Run("studio.subdSet", new JsonObject { ["id"] = box, ["thickness"] = 0.05 });
        Assert.True(Vol(f, box) > 0);
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal(smooth, Vol(f, box), 12);

        Assert.Throws<CommandException>(() => f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "bevel" }));
        Assert.Throws<CommandException>(() => f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "delete", ["faces"] = Ints(0, 1, 2, 3, 4, 5) }));
        var f0 = Faces(Cage(f, box))[0];
        Assert.Throws<CommandException>(() => f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "crease", ["edges"] = new JsonArray(Ints(f0[0], f0[2])) }));
    }

    [Fact]
    public void Settings_cap_the_levels_and_symmetry_keeps_both_halves_alike()
    {
        var f = F();
        var box = Box(f, new JsonObject { ["segments"] = new JsonArray(2, 1, 1) });
        f.Run("studio.subdSet", new JsonObject { ["id"] = box, ["levels"] = 2 });
        Assert.Equal(2, (int)Cage(f, box)["level"]!);
        var two = Vol(f, box);
        f.Session.Undo();
        Assert.Equal(4, (int)Cage(f, box)["level"]!);
        Assert.True(Vol(f, box) > two);

        // Mirrored across its own x plane (x = 2 in the world): the half on +x kept, the partners each other's images.
        f.Run("studio.subdSet", new JsonObject { ["id"] = box, ["symmetry"] = "x" });
        var cage = Cage(f, box);
        Assert.Equal(1, (double)cage["symmetry"]!["normal"]![0]!, 9);
        Assert.Equal(2, (double)cage["symmetry"]!["point"]![0]!, 9);
        var pts = Points(cage);
        var partners = cage["partners"]!.AsArray().Select(x => (int)x!).ToList();
        Assert.All(Enumerable.Range(0, pts.Count), i => Assert.Equal(4 - pts[i].X, pts[partners[i]].X, 9));
        // A corner on the −x side pulled up: its image on +x follows.
        var corner = Enumerable.Range(0, pts.Count).First(i => pts[i].X < 1.6 && pts[i].Z > 0.5);
        f.Run("studio.subdEdit", new JsonObject { ["id"] = box, ["vertices"] = Ints(corner), ["translate"] = J3(0, 0, 0.4) });
        var after = Points(Cage(f, box));
        Assert.Equal(1.4, after[corner].Z, 9);
        Assert.Equal(1.4, after[partners[corner]].Z, 9);
        var body = f.Session.Geometry(box)!.Parts[0].Solid;
        Assert.All(body.Vertices.Where((_, i) => i % 5 == 0), v => Assert.Contains(body.Vertices, w => (w - new Vec3(4 - v.X, v.Y, v.Z)).Length < 1e-6));
        // A face extruded on one side is extruded on the other too.
        var side = Faces(Cage(f, box)).FindIndex(fc => fc.All(k => after[k].X > 2.4));
        f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "extrude", ["faces"] = Ints(side), ["distance"] = 0.3 });
        var grown = f.Bounds(box);
        Assert.Equal(4 - grown.Max.X, grown.Min.X, 6);
        // Symmetry off: the cage stays as it is.
        f.Run("studio.subdSet", new JsonObject { ["id"] = box, ["symmetry"] = null });
        Assert.Null(Cage(f, box)["symmetry"]);
        Assert.Throws<CommandException>(() => f.Run("studio.subdSet", new JsonObject { ["id"] = box, ["levels"] = 9 }));
        Assert.Throws<CommandException>(() => f.Run("studio.subdSet", new JsonObject { ["id"] = box, ["side"] = "up" }));
        // A box of one segment has no loop on its middle: symmetry across it is refused with the reason.
        var plain = Box(f);
        Assert.Contains("crosses", Assert.Throws<CommandException>(() => f.Run("studio.subdSet", new JsonObject { ["id"] = plain, ["symmetry"] = "x" })).Message);
    }

    // ------------------------------------------------------------ the body as any Studio solid's

    [Fact]
    public void Booleans_steps_and_quantities_work_on_its_body()
    {
        var f = F();
        var box = Box(f, new JsonObject { ["class"] = "SLAB" });
        var whole = Vol(f, box);
        // Sliced through its middle (x = 2): half is left.
        f.Run("studio.addFeature", new JsonObject { ["id"] = box, ["feature"] = new JsonObject { ["op"] = "slice", ["point"] = J3(2, 3, 0), ["normal"] = J3(1, 0, 0) } });
        Assert.Equal(whole / 2, Vol(f, box), 4);
        f.Session.Undo();
        // United with a primitive box beside it: one body holding both.
        var other = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = J3(4, 3, 0) }));
        f.Run("studio.boolean", new JsonObject { ["target"] = box, ["tools"] = new JsonArray(other), ["op"] = "union" });
        Assert.Equal(whole + 1, Vol(f, box), 4);
        Assert.Empty(f.Session.Geometry(other)!.Parts);
        // A slab's quantities: its volume, and with a thickness its area.
        var q = f.Session.Quantities(box).Single();
        Assert.Contains(q.Quantities, x => x.Name == "NetVolume");
        var sheet = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "plane", ["at"] = J3(0, 20, 0), ["thickness"] = 0.25, ["class"] = "SLAB" }));
        var sq = f.Session.Quantities(sheet).Single();
        Assert.Equal(0.25, sq.Quantities.Single(x => x.Name == "Width").Value, 9);
        // Moved and placed as a primitive is.
        f.Run("studio.place", new JsonObject { ["id"] = sheet, ["at"] = J3(5, 20, 1) });
        Assert.Equal(1, f.Bounds(sheet).Min.Z, 9);
    }

    // ------------------------------------------------------------ IFC and .lapin

    [Fact]
    public void Ifc_keeps_the_cage_in_lapin_subd_and_reads_it_back_editable()
    {
        Assert.Equal(IfcExport.SubDSetName, SubDIfc.SetName);
        var f = F();
        var box = Box(f, new JsonObject { ["rotation"] = 30 });
        f.Run("studio.subdOp", new JsonObject { ["id"] = box, ["op"] = "crease", ["edges"] = new JsonArray(Cage(f, box)["edges"]![0]!.DeepClone()), ["sharpness"] = 2.5 });
        f.Run("studio.subdSet", new JsonObject { ["id"] = box, ["levels"] = 3 });
        var volume = Vol(f, box);
        var cage = f.Model.Get(box)!.Attrs["cage"]!.ToJsonString();
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "subd.ifc", new IfcExportOptions());
        var text = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("'Lapin_SubD'", text);
        Assert.Contains("'Cage'", text);
        Assert.DoesNotContain("Pset_", text.Split('\n').Where(l => l.Contains("Lapin_SubD")).FirstOrDefault() ?? "");
        // Once: in its own set, not again in the intent.
        Assert.Single(text.Split('\n'), l => l.Contains("\"points\""));
        var g = F();
        var path = Path.Combine(Directory.CreateTempSubdirectory("lapin-subd").FullName, "subd.ifc");
        try
        {
            File.WriteAllBytes(path, ms.ToArray());
            IfcImport.Run(g.Session, path, null, out _);
            var e = g.Model.Get(box)!;
            Assert.Equal("subd", e.Str("operation"));
            Assert.Equal(cage, e.Attrs["cage"]!.ToJsonString());
            Assert.Equal(3, e.Num("levels"));
            Assert.Equal(volume, Vol(g, box), 9);
            // Editable again.
            g.Run("studio.subdEdit", new JsonObject { ["id"] = box, ["vertices"] = Ints(0), ["translate"] = J3(0, 0, -0.2) });
            Assert.NotEqual(volume, Vol(g, box), 6);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void A_lapin_package_keeps_the_cage()
    {
        var f = F();
        var sheet = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "plane", ["at"] = J3(1, 1, 0), ["thickness"] = 0.1, ["side"] = "in" }));
        f.Run("studio.subdEdit", new JsonObject { ["id"] = sheet, ["vertices"] = Ints(12), ["translate"] = J3(0, 0, 0.5) });
        var volume = Vol(f, sheet);
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var g = F();
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        var e = g.Model.Get(sheet)!;
        Assert.Equal(f.Model.Get(sheet)!.Attrs["cage"]!.ToJsonString(), e.Attrs["cage"]!.ToJsonString());
        Assert.Equal("in", e.Str("side"));
        Assert.Equal(volume, Vol(g, sheet), 9);
    }
}
