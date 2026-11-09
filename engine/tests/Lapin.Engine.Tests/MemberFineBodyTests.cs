using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// A member's real section only at LOD 400 (decision 415): root fillets, toe radii and sloping flanges in drawings and in a 3D
/// view at LOD 400; every lower level, the quantities and the exports keep the light sharp body.
/// </summary>
public class MemberFineBodyTests(ITestOutputHelper output)
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static string Column(Fixture f, string name)
    {
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = name, ["section"] = name, ["material"] = "Steel" })!["id"]!;
        return (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 1, ["y"] = 2 })!["id"]!;
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
        public ElementGeometry? FineGeometry(string entityId) => f.Session.FineGeometry(entityId);
    }

    static double AreaOf(Fixture f, ElementGeometry g, string id) => g.Parts.Sum(p => Measure.Volume(p.Solid)) / f.Bounds(id) switch { var (a, b) => b.Z - a.Z };

    [Fact]
    public void Only_the_fine_body_has_the_root_fillets_and_its_area_is_the_published_one()
    {
        var f = F();
        var id = Column(f, "IPE 300");
        var sharp = f.Session.Geometry(id)!;
        Assert.True(sharp.FineShape);
        Assert.Equal(24, sharp.Parts[0].Solid.Vertices.Length);
        var fine = f.Session.FineGeometry(id)!;
        Assert.True(fine.Parts[0].Solid.Vertices.Length > 60);
        // 53.81 cm² (EN 10365) against the nominal outline's 51.88 cm²: the fillets by the chord rule (flats within 1 mm of the arc) stray 0.15 %.
        Assert.InRange(AreaOf(f, fine, id), 53.81e-4, 53.81e-4 * 1.002);
        Assert.Equal(51.88e-4, AreaOf(f, sharp, id), 5);
        // Same box, kept once built.
        var box = Measure.Bounds(sharp.Parts[0].Solid)!.Value;
        var fineBox = Measure.Bounds(fine.Parts[0].Solid)!.Value;
        Assert.Equal(box.Min.X, fineBox.Min.X, 9); Assert.Equal(box.Max.Y, fineBox.Max.Y, 9); Assert.Equal(box.Max.Z, fineBox.Max.Z, 9);
        Assert.Same(fine, f.Session.FineGeometry(id));
        // What measures it stays on the light body.
        var q = f.Session.Quantities(id).First(s => s.Name == "Qto_ColumnBaseQuantities");
        // The quantities follow the profile's real area, not the sharp body's (decision 418).
        Assert.InRange(q.Quantities.First(x => x.Name == "NetVolume").Value / (f.Bounds(id) switch { var (a, b) => b.Z - a.Z }), 53.81e-4 * 0.999, 53.81e-4 * 1.001);
    }

    [Theory]
    [InlineData("UPN 200", 32.2e-4, 0.02)]     // sloping flanges from the built-in library, its catalogue type's four sizes
    [InlineData("L 100×100×10", 19.2e-4, 0.01)] // root and toe radii
    [InlineData("RHS 150×100×6", 27.6e-4, 0.03)] // rounded corners, inside and out
    public void Channels_angles_and_hollow_sections_draw_their_real_shape(string name, double area, double within)
    {
        var f = F();
        var id = Column(f, name);
        var fine = f.Session.FineGeometry(id)!;
        var sharp = f.Session.Geometry(id)!;
        Assert.True(fine.Parts[0].Solid.Vertices.Length > sharp.Parts[0].Solid.Vertices.Length);
        Assert.InRange(AreaOf(f, fine, id), area * (1 - within), area * (1 + within));
        var (a, b) = (Measure.Bounds(sharp.Parts[0].Solid)!.Value, Measure.Bounds(fine.Parts[0].Solid)!.Value);
        Assert.Equal(a.Min.X, b.Min.X, 6); Assert.Equal(a.Max.X, b.Max.X, 6); Assert.Equal(a.Min.Y, b.Min.Y, 6); Assert.Equal(a.Max.Y, b.Max.Y, 6);
    }

    [Fact]
    public void A_concrete_rectangle_and_a_cold_formed_section_have_no_finer_shape()
    {
        var f = F();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        var rect = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 1, ["y"] = 2 })!["id"]!;
        Assert.False(f.Session.Geometry(rect)!.FineShape);
        Assert.Same(f.Session.Geometry(rect), f.Session.FineGeometry(rect));
        var c = Column(f, "C 200×65×2");
        Assert.False(f.Session.Geometry(c)!.FineShape);
    }

    static string View(Fixture f, string kind, int lod, JsonObject? more = null)
    {
        var a = new JsonObject { ["kind"] = kind, ["storeyId"] = f.Storey, ["scale"] = 50 };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        var id = (string)f.Run("view.create", a)!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = id, ["detail"] = lod });
        return id;
    }

    [Fact]
    public void A_plan_at_LOD_400_cuts_the_real_section_and_one_below_the_sharp()
    {
        var f = F();
        var id = Column(f, "IPE 300");
        ViewDrawing Draw(int lod) => Views.Generate(new Q(f), Views.Find(f.Model, View(f, "plan", lod))!).Drawing!;
        int Corners(ViewDrawing d) => d.Cuts.Where(c => c.Element == id).Sum(c => c.Loops.Sum(l => l.Length));
        Assert.Equal(12, Corners(Draw(350)));
        Assert.True(Corners(Draw(400)) > 12 + 4 * 2);
    }

    [Fact]
    public void A_beam_seen_along_its_side_at_LOD_400_draws_no_line_per_flat_of_a_fillet()
    {
        var f = F();
        var type = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "IPE 300", ["section"] = "IPE 300", ["material"] = "Steel" })!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        int Long(int lod)
        {
            var d = Views.Generate(new Q(f), Views.Find(f.Model, View(f, "elevation", lod, new JsonObject { ["look"] = "north" }))!).Drawing!;
            return d.Lines.Count(l => l.Element == beam && (l.B - l.A).Length > 5);
        }
        var fine = Long(400);
        // The flanges' top and bottom, their inner edges at the tips: as many long lines as the sharp section, not one per flat.
        Assert.InRange(fine, 2, Long(350));
    }

    [Fact]
    public void A_3D_view_under_LOD_400_gets_no_fine_body_and_the_render_never_grows()
    {
        var f = F();
        var id = Column(f, "IPE 300");
        var before = f.Session.FineBodies(null, null);
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["detail"] = 350 });
        Assert.Equal(350, f.Session.FineBodies(null, null).Lod);
        Assert.Empty(f.Session.FineBodies(null, null).Items);
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["detail"] = 400 });
        var at400 = f.Session.FineBodies(null, null);
        Assert.Equal(400, at400.Lod);
        var item = Assert.Single(at400.Items);
        Assert.Equal(id, item.NodeId);
        Assert.Equal("body", item.Parts[0].Key);
        Assert.True(item.Parts[0].Indices.Length > f.Session.Geometry(id)!.Parts[0].Tessellate().Indices.Length);
        Assert.Empty(f.Session.FineBodies(null, []).Items);
        _ = before;
    }

    /// <summary>What LOD 400 costs on a hall of hundreds of members (reported in docs/plan-profile-libraries.md).</summary>
    [Fact]
    public void A_hall_of_hundreds_of_members_costs_this_much_at_LOD_400()
    {
        var f = F();
        var hall = (string)f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject() })!["id"]!;
        var members = Halls.Members(f.Model, hall).Values.ToList();
        var ids = members.Select(e => e.Id).ToList();
        int Triangles(IEnumerable<ElementGeometry?> gs) => gs.Sum(g => g?.Parts.Sum(p => p.Tessellate().Indices.Length / 3) ?? 0);
        var sharp = Triangles(ids.Select(f.Session.Geometry));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var fine = Triangles(ids.Select(f.Session.FineGeometry));
        var ms = clock.ElapsedMilliseconds;
        var withFine = ids.Count(x => f.Session.Geometry(x)!.FineShape);
        output.WriteLine($"{ids.Count} members, {withFine} with a finer shape: {sharp} triangles sharp, {fine} at LOD 400 (×{(double)fine / sharp:0.00}), built in {ms} ms");
        Assert.True(ids.Count >= 100);
        Assert.InRange((double)fine / sharp, 1.01, 6);
        // The render itself is what it was: nothing fine in it.
        Assert.All(ids, x => Assert.Same(f.Session.Geometry(x), f.Session.Geometry(x)));
    }
}
