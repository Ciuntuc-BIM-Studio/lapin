using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>The openings' voids drawn on plans, sections, elevations and sheets, as a coordination drawing shows them (decision 290).</summary>
public sealed class VoidDrawingTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(x => (JsonNode)new JsonArray(x.X, x.Y)).ToArray());

    /// <summary>A wall along y = 1 from x = −2 to 8 with an opening 400 wide and 1500 high, 500 up, its middle at x = 0; the faces' y (the axis is on the core).</summary>
    static (Fixture F, string Wall, string Opening, (double Y0, double Y1) T) Wall()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(-2, 1, 8, 1);
        var opening = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["width"] = 0.4, ["height"] = 1.5, ["sill"] = 0.5 })!["id"]!;
        var box = Measure.Bounds(OpeningVoids.Of(f.Session.Reader, VoidOptions.All).Single().Solid)!.Value;
        Assert.Equal(Walls.Section(f.Model, f.Model.Get(wall)!).Sum(b => b.To - b.From), box.Max.Y - box.Min.Y, 6);
        return (f, wall, opening, (box.Min.Y, box.Max.Y));
    }

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static Views.Rendered Render(Fixture f, string view) => Views.Render(new Q(f), Views.Find(f.Model, view)!, print: true);

    static List<(Vec2 A, Vec2 B)> Lines(Views.Rendered r) => r.Pens.Where(p => p.Layer == VoidDrawings.Layer).SelectMany(p => p.Segments).ToList();

    static List<string> Texts(Views.Rendered r) => r.Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    static bool Has(List<(Vec2 A, Vec2 B)> lines, Vec2 a, Vec2 b) =>
        lines.Any(l => (l.A - a).Length < 1e-6 && (l.B - b).Length < 1e-6 || (l.A - b).Length < 1e-6 && (l.B - a).Length < 1e-6);

    [Fact]
    public void A_plan_draws_no_voids_until_its_style_says()
    {
        var (f, _, _, _) = Wall();
        var plan = Plan(f);
        Assert.Empty(Lines(Render(f, plan)));
        Assert.DoesNotContain("V1", Texts(Render(f, plan)));
        // Off in every discipline's style; on in the Coordination style.
        Assert.All(ViewStyles.Defaults.Where(s => s.Discipline != null), s => Assert.False(s.Voids));
        Assert.True(ViewStyles.Defaults.Single(s => s.Id == "coordination").Voids);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = "coordination" });
        Assert.NotEmpty(Lines(Render(f, plan)));
        // The view's own choice wins over its style's.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["voids"] = false });
        Assert.Empty(Lines(Render(f, plan)));
        Assert.Equal(["voids"], ((JsonArray)f.Session.Query("view.style", new JsonObject { ["id"] = plan })!["overrides"]!).Select(x => (string)x!).ToArray());
    }

    [Fact]
    public void A_plan_through_a_wall_opening_draws_the_void_across_the_wall_crossed_with_its_mark_and_size()
    {
        var (f, _, opening, t) = Wall();
        var plan = Plan(f);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["voids"] = true });
        var r = Render(f, plan);
        var lines = Lines(r);
        Assert.All(r.Pens.Where(p => p.Layer == VoidDrawings.Layer), p => Assert.Null(p.Dash));
        // The outline through the wall's thickness, 400 wide, and its two diagonals.
        var (y0, y1) = t;
        Assert.True(Has(lines, new Vec2(-0.2, y0), new Vec2(0.2, y0)));
        Assert.True(Has(lines, new Vec2(-0.2, y1), new Vec2(0.2, y1)));
        Assert.True(Has(lines, new Vec2(-0.2, y0), new Vec2(0.2, y1)));
        Assert.True(Has(lines, new Vec2(0.2, y0), new Vec2(-0.2, y1)));
        Assert.All(lines, l => Assert.InRange(Math.Max(l.A.X, l.B.X), -0.2 - 1e-6, 0.2 + 1e-6));
        // Violet by default; a click takes the opening.
        Assert.All(r.Pens.Where(p => p.Layer == VoidDrawings.Layer), p => Assert.Equal(OpeningVoids.Color, p.Stroke));
        Assert.Contains(r.Marks, m => m.Owner == opening && m.Lines.Count > 0);
        var texts = Texts(r);
        Assert.Contains("V1", texts);
        Assert.Contains("400×1500 +0.50", texts);
        // A sleeve low in the wall, under the cut: dashed, as hidden inside the wall.
        f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = f.Model.OfClass(IfcClass.Wall).Single().Id, ["along"] = 5, ["width"] = 0.2, ["height"] = 0.2, ["sill"] = 0.2 });
        r = Render(f, plan);
        var low = r.Pens.Where(p => p.Layer == VoidDrawings.Layer && p.Dash != null).SelectMany(p => p.Segments).ToList();
        Assert.True(Has(low, new Vec2(2.9, y0), new Vec2(3.1, y0)));
        Assert.Contains("200×200 +0.20", Texts(r));
        Assert.True(Has(Lines(r), new Vec2(-0.2, y0), new Vec2(0.2, y0)));
        // Without the sizes: the mark alone.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["voidSizes"] = false });
        texts = Texts(Render(f, plan));
        Assert.Contains("V1", texts);
        Assert.DoesNotContain("400×1500 +0.50", texts);
    }

    [Fact]
    public void A_floor_opening_is_drawn_as_seen_from_above_crossed_corner_to_corner_and_dashed_overhead()
    {
        var f = new Fixture(new ArchitectureModule());
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((-2, 2), (4, 2), (4, 6), (-2, 6)) })!["id"]!;
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = Pts((0, 3), (0.6, 3), (0.6, 3.5), (0, 3.5)) });
        var plan = Plan(f);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["voids"] = true });
        var r = Render(f, plan);
        var lines = Lines(r);
        Assert.True(Has(lines, new Vec2(0, 3), new Vec2(0.6, 3)));
        Assert.True(Has(lines, new Vec2(0.6, 3), new Vec2(0.6, 3.5)));
        Assert.True(Has(lines, new Vec2(0, 3), new Vec2(0.6, 3.5)));
        Assert.True(Has(lines, new Vec2(0.6, 3), new Vec2(0, 3.5)));
        Assert.All(r.Pens.Where(p => p.Layer == VoidDrawings.Layer), p => Assert.Null(p.Dash));
        // A floor's opening has a size, no bottom level.
        Assert.Contains("600×500", Texts(r));

        // The same slab a storey up, over this plan's cut: overhead, dashed.
        var level = (string)f.Run("storey.create", new JsonObject { ["name"] = "Upper", ["elevation"] = 3, ["height"] = 3 })!["id"]!;
        var upper = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = level, ["points"] = Pts((10, 2), (14, 2), (14, 6), (10, 6)) })!["id"]!;
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = upper, ["points"] = Pts((11, 3), (12, 3), (12, 4), (11, 4)) });
        f.Run("view.setRange", new JsonObject { ["id"] = plan, ["top"] = new JsonObject { ["level"] = "unlimited" } });
        f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["mode"] = "all" });
        r = Render(f, plan);
        var overhead = r.Pens.Where(p => p.Layer == VoidDrawings.Layer && p.Dash != null).SelectMany(p => p.Segments).ToList();
        Assert.True(Has(overhead, new Vec2(11, 3), new Vec2(12, 3)));
    }

    [Fact]
    public void A_section_cuts_the_void_through_its_height_and_an_elevation_sees_its_face()
    {
        var (f, _, _, t) = Wall();
        // Across the wall through the opening's middle, looking −x.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, -1), ["end"] = new JsonArray(0, 3), ["name"] = "A-A" })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["voids"] = true });
        var r = Render(f, section);
        var lines = Lines(r);
        // The wall at 2 m along the section's line: the void through its thickness, from 0.50 to 2.00.
        var (x0, x1) = (t.Y0 + 1, t.Y1 + 1);
        Assert.True(Has(lines, new Vec2(x0, 0.5), new Vec2(x0, 2.0)));
        Assert.True(Has(lines, new Vec2(x1, 0.5), new Vec2(x1, 2.0)));
        Assert.True(Has(lines, new Vec2(x0, 0.5), new Vec2(x1, 2.0)));
        Assert.Contains("400×1500 +0.50", Texts(r));

        // An elevation looking at the wall's face: the opening's outline, 400 by 1500, crossed.
        string? seen = null;
        foreach (var dir in new[] { "south", "north" })
        {
            var e = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = dir })!["id"]!;
            f.Run("view.setStyle", new JsonObject { ["id"] = e, ["voids"] = true });
            var el = Lines(Render(f, e));
            Assert.NotEmpty(el);
            var w = el.Max(l => Math.Max(l.A.X, l.B.X)) - el.Min(l => Math.Min(l.A.X, l.B.X));
            var h = el.Max(l => Math.Max(l.A.Y, l.B.Y)) - el.Min(l => Math.Min(l.A.Y, l.B.Y));
            Assert.Equal(0.4, w, 6);
            Assert.Equal(1.5, h, 6);
            seen = e;
        }
        // Seen edge on (an elevation looking along the wall), it is not drawn.
        var west = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "west" })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = west, ["voids"] = true });
        Assert.Empty(Lines(Render(f, west)));
        Assert.NotNull(seen);
    }

    [Fact]
    public void Object_styles_and_view_graphics_restyle_or_hide_them()
    {
        var (f, _, _, _) = Wall();
        var plan = Plan(f);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["voids"] = true });
        f.Run("view.objectStyle", new JsonObject { ["category"] = VoidDrawings.Category, ["role"] = "cut", ["spec"] = new JsonObject { ["color"] = "#000000", ["pen"] = 7 } });
        var outline = Render(f, plan).Pens.Where(p => p.Layer == VoidDrawings.Layer).MaxBy(p => p.Width)!;
        Assert.Equal("#000000", outline.Stroke);
        Assert.Equal(0.7, outline.Width, 6);
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = VoidDrawings.Category, ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Empty(Lines(Render(f, plan)));
    }

    [Fact]
    public void A_sheet_and_its_PDF_and_DXF_carry_them()
    {
        var (f, _, _, _) = Wall();
        var plan = Plan(f);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = "coordination" });
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Coordination", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["x"] = 200, ["y"] = 140 });
        var page = Sheets.Compose(new Q(f), f.Model.Get(sheet)!);
        Assert.Contains(page.Pens, p => p.Layer == VoidDrawings.Layer && p.Segments.Count > 0);
        Assert.Contains("V1", page.Pens.SelectMany(p => p.Texts).Select(t => t.Text));
        Assert.Contains("V1", PdfContent(Sheets.Pdf(new Q(f), [f.Model.Get(sheet)!])));
        var dxf = Encoding.UTF8.GetString(Views.Dxf(new Q(f), Views.Find(f.Model, plan)!));
        Assert.Contains("VOIDS", dxf);
        Assert.Contains("V1", dxf);
    }

    static string PdfContent(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var o = new StringBuilder();
        for (var at = text.IndexOf("/FlateDecode >>\nstream\n", StringComparison.Ordinal); at >= 0; at = text.IndexOf("/FlateDecode >>\nstream\n", at + 1, StringComparison.Ordinal))
        {
            var start = at + "/FlateDecode >>\nstream\n".Length;
            var end = text.IndexOf("\nendstream", start, StringComparison.Ordinal);
            using var z = new System.IO.Compression.ZLibStream(new MemoryStream(pdf[start..end]), System.IO.Compression.CompressionMode.Decompress);
            using var r = new StreamReader(z, Encoding.Latin1);
            o.Append(r.ReadToEnd());
        }
        return o.ToString() + text;
    }
}
