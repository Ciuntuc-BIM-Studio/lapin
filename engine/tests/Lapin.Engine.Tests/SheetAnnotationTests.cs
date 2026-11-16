using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Annotations drawn on a sheet itself (audit §6, decision 620): its paper as a view (<c>paper:&lt;sheet&gt;</c>, metres from
/// the lower-left corner, y up, at 1:1), what the Draft tools draw there printed on the sheet, its PDF and DXF in paper mm,
/// copied with the sheet, deleted with it, one undo step each.
/// </summary>
public class SheetAnnotationTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static (Fixture F, string Sheet, string Paper) A3()
    {
        var f = F();
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        return (f, s, PaperViews.IdOf(s));
    }

    static JsonArray J(double x, double y) => new(x, y);
    static JsonObject Line(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };
    static JsonObject Rect(double x0, double y0, double x1, double y1) => new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static List<Pen> Pens(Fixture f, string sheet) => [.. Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens];
    static bool Near(Vec2 a, Vec2 b) => (a - b).Length < 1e-6;

    [Fact]
    public void The_paper_is_a_view_at_1_1_in_paper_metres_and_a_line_drawn_on_it_prints_in_paper_mm()
    {
        var (f, s, paper) = A3();
        var view = Views.Find(f.Model, paper)!;
        Assert.Equal("paper", view.Str("kind"));
        Assert.Equal(1, view.Num("scale"));
        Assert.Null(Views.Find(f.Model, "paper:nothing"));
        // From (0.05, 0.05) to (0.15, 0.05) m on the paper's view: 50 mm in from the left, 50 mm up from the bottom of 297.
        f.Run("drafting.draw", new JsonObject { ["viewId"] = paper, ["shape"] = Line(0.05, 0.05, 0.15, 0.05) });
        var pens = Pens(f, s);
        Assert.Contains(pens.SelectMany(p => p.Segments), sg => Near(sg.A, new Vec2(50, 247)) && Near(sg.B, new Vec2(150, 247)));
        // The view's own drawing shows the sheet under it (its frame), the paper's edge and the line; the paper fits the screen.
        var drawing = Views.Drawing(f.Session.Reader, view);
        var layers = ((JsonArray)drawing["pens"]!).Select(p => (string)p!["layer"]!).ToList();
        Assert.Contains("FRAME", layers);
        Assert.Contains(PaperViews.PaperLayer, layers);
        Assert.Equal([0.0, 0.0, 0.42, 0.297], ((JsonArray)drawing["bounds"]!).Select(x => Math.Round((double)x!, 6)).ToArray());
        // The print never holds the screen's own layers.
        Assert.DoesNotContain(pens, p => p.Layer is PaperViews.PaperLayer or PaperViews.ViewportLayer);
    }

    [Fact]
    public void A_sheet_with_nothing_on_its_paper_composes_as_before()
    {
        var (f, s, _) = A3();
        f.Wall(0, 0, 8, 0);
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 150, ["y"] = 140 });
        var sheet = f.Model.Get(s)!;
        var with = Sheets.Compose(f.Session.Reader, sheet).Pens.Select(p => p.ToJson().ToJsonString());
        var without = Sheets.Compose(f.Session.Reader, sheet, annotations: false).Pens.Select(p => p.ToJson().ToJsonString());
        Assert.Equal(without, with);
    }

    [Fact]
    public void Texts_on_the_paper_read_the_sheet_s_tokens_and_a_leader_note_draws_its_arrow()
    {
        var (f, s, paper) = A3();
        f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.03, 0.1), ["text"] = "Sheet {sheet.number} of {sheet.count} · {nothing.here}" });
        var note = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.1, 0.2), ["text"] = "Check on site", ["leader"] = new JsonArray(J(0.06, 0.15)) })!["id"]!;
        var pens = Pens(f, s);
        var texts = pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("Sheet A-101 of 1 · <nothing.here>", texts);
        Assert.Contains("Check on site", texts);
        // The leader: from its arrow at (60, 147) mm, with a filled head there.
        Assert.Contains(pens.Where(p => p.Layer == "LEADER").SelectMany(p => p.Segments), sg => Near(sg.A, new Vec2(60, 147)));
        Assert.Contains(pens.Where(p => p.Fill != null).SelectMany(p => p.Loops), l => l.Any(q => Near(q, new Vec2(60, 147))));
        // On any other view a text reads as typed.
        f.Run("drafting.addText", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["at"] = J(0, 0), ["text"] = "{sheet.number}" });
        var plan = Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!);
        Assert.Contains(plan.Pens.SelectMany(p => p.Texts), t => t.Text == "{sheet.number}");
        // Moved, the note takes its leader with it.
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(note), ["op"] = "move", ["by"] = J(0.01, 0) });
        Assert.True(Near(Texts.Leader(f.Model.Get(note)!)[0], new Vec2(0.07, 0.15)));
    }

    [Fact]
    public void A_revision_cloud_scallops_a_closed_shape_and_can_be_made_plain_again()
    {
        var (f, s, paper) = A3();
        var id = (string)((JsonArray)f.Run("drafting.draw", new JsonObject { ["viewId"] = paper, ["shape"] = Rect(0.1, 0.1, 0.14, 0.12), ["cloud"] = 10 })!["ids"]!)[0]!;
        Assert.Equal(10, f.Model.Get(id)!.Num("cloud"));
        Assert.StartsWith("Cloud", f.Model.Get(id)!.Name);
        // 40 + 20 + 40 + 20 mm of sides in 10 mm scallops of 8 chords: 96 segments, every point outside the rectangle or on it.
        var segs = Pens(f, s).Where(p => p.Layer == "0").SelectMany(p => p.Segments).ToList();
        Assert.Equal(96, segs.Count);
        Assert.All(segs, sg => Assert.False(sg.A.X > 100 + 1e-6 && sg.A.X < 140 - 1e-6 && sg.A.Y > 177 + 1e-6 && sg.A.Y < 197 - 1e-6));
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = id, ["cloud"] = null });
        Assert.Equal(4, Pens(f, s).Where(p => p.Layer == "0").SelectMany(p => p.Segments).Count());
        Assert.Throws<CommandException>(() => f.Run("drafting.updateDraft", new JsonObject { ["id"] = id, ["cloud"] = 500 }));
    }

    [Fact]
    public void A_sheet_s_PDF_and_DXF_hold_what_is_drawn_on_its_paper()
    {
        var (f, s, paper) = A3();
        f.Run("drafting.draw", new JsonObject { ["viewId"] = paper, ["shape"] = Rect(0.02, 0.02, 0.06, 0.04) });
        f.Run("drafting.hatch", new JsonObject { ["viewId"] = paper, ["at"] = J(0.04, 0.03), ["pattern"] = "LINES45" });
        f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.03, 0.05), ["text"] = "Approved {project.name}" });
        var sheet = f.Model.Get(s)!;
        var pdf = PdfText(Sheets.Pdf(f.Session.Reader, [sheet]));
        Assert.Contains("Approved", pdf);
        var dxf = Encoding.UTF8.GetString(Sheets.Dxf(f.Session.Reader, sheet));
        Assert.Contains("Approved", dxf);
        Assert.Contains("HATCH", dxf);
    }

    [Fact]
    public void Duplicate_sheet_copies_its_paper_s_drawing_and_symbols_and_delete_takes_them()
    {
        var (f, s, paper) = A3();
        f.Run("drafting.draw", new JsonObject { ["viewId"] = paper, ["shape"] = Line(0.01, 0.01, 0.2, 0.01) });
        f.Run("drafting.addText", new JsonObject { ["viewId"] = paper, ["at"] = J(0.05, 0.05), ["text"] = "Note" });
        f.Run("drafting.placeSymbol", new JsonObject { ["sheetId"] = s, ["symbolId"] = BuiltinSymbols.Prefix + "north-arrow", ["at"] = J(30, 30) });
        var copy = (string)f.Run("sheet.duplicate", new JsonObject { ["id"] = s })!["id"]!;
        var mine = PaperViews.Owned(f.Model, s).ToList();
        var theirs = PaperViews.Owned(f.Model, copy).ToList();
        Assert.Equal(3, mine.Count);
        Assert.Equal(3, theirs.Count);
        Assert.Empty(mine.Select(e => e.Id).Intersect(theirs.Select(e => e.Id)));
        Assert.All(theirs.Where(e => e.PredefinedType != Symbols.Predefined), e => Assert.Equal(PaperViews.IdOf(copy), e.Str("viewId")));
        Assert.Contains(Pens(f, copy).SelectMany(p => p.Texts), t => t.Text == "Note");
        // Deleted, the sheet takes them; the copy keeps its own; undo brings them all back in one step.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(s) });
        Assert.Empty(PaperViews.Owned(f.Model, s));
        Assert.Equal(3, PaperViews.Owned(f.Model, copy).Count());
        f.Session.Undo();
        Assert.Equal(3, PaperViews.Owned(f.Model, s).Count());
    }

    [Fact]
    public void Each_drawing_on_the_paper_is_one_undo_step()
    {
        var (f, s, paper) = A3();
        f.Run("drafting.draw", new JsonObject { ["viewId"] = paper, ["shapes"] = new JsonArray(Line(0, 0, 0.1, 0), Line(0, 0, 0, 0.1)) });
        Assert.Equal(2, PaperViews.Owned(f.Model, s).Count());
        f.Session.Undo();
        Assert.Empty(PaperViews.Owned(f.Model, s));
        Assert.False(PaperViews.Any(f.Model, s));
    }

    [Fact]
    public void A_material_legend_lists_what_the_sheet_s_views_cut_live()
    {
        var (f, s, paper) = A3();
        f.Wall(0, 0, 8, 0);
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 150, ["y"] = 140 });
        var legend = (string)f.Run("sheet.addLegend", new JsonObject { ["viewId"] = paper, ["at"] = J(0.3, 0.25) })!["id"]!;
        var rows = DrawingKeys.Entries(f.Session.Reader, Views.Find(f.Model, paper)!, "materials");
        Assert.NotEmpty(rows);
        var texts = Pens(f, s).SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("LEGEND", texts);
        Assert.All(rows, r => Assert.Contains(r.Name, texts));
        // The lines legend: the views' lines by role, type and width.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["kind"] = "lines", ["title"] = "Lines of {sheet.number}" });
        texts = [.. Pens(f, s).SelectMany(p => p.Texts).Select(t => t.Text)];
        Assert.Contains("Lines of A-101", texts);
        Assert.Contains(texts, t => t.StartsWith("Cut · continuous · ", StringComparison.Ordinal));
        // A legend belongs on a sheet's paper.
        Assert.Throws<CommandException>(() => f.Run("sheet.addLegend", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["at"] = J(0, 0) }));
        // And the paper itself is not a view to place.
        Assert.Throws<CommandException>(() => f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = paper }));
    }

    [Fact]
    public void A_scale_bar_on_the_paper_takes_its_view_s_scale_and_a_round_length()
    {
        Assert.Equal(5, DrawingKeys.NiceLength(100));
        Assert.Equal(2, DrawingKeys.NiceLength(50));
        Assert.Equal(20, DrawingKeys.NiceLength(250));
        var (f, s, paper) = A3();
        f.Wall(0, 0, 8, 0);
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = "plan:" + f.Storey, ["x"] = 150, ["y"] = 140 })!["viewportId"]!;
        f.Run("sheet.addScaleBar", new JsonObject { ["viewId"] = paper, ["at"] = J(0.03, 0.03), ["viewportId"] = vp, ["divisions"] = 5 });
        var texts = Pens(f, s).SelectMany(p => p.Texts).ToList();
        Assert.Contains(texts, t => t.Text == "1:100");
        // 5 m at 1:100 is 50 mm: from x 30 to x 80 on paper.
        Assert.Contains(texts, t => t.Text == "5 m" && Math.Abs(t.At.X - 80) < 1e-6);
        Assert.Contains(texts, t => t.Text == "1" && Math.Abs(t.At.X - 40) < 1e-6);
        // On a plan, a bar is at the plan's scale, in metres of the model.
        var bar = (string)f.Run("sheet.addScaleBar", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["at"] = J(0, -2), ["length"] = 10 })!["id"]!;
        var plan = Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!);
        Assert.Contains(plan.Pens.SelectMany(p => p.Texts), t => t.Text == "10 m" && Math.Abs(t.At.X - 10) < 1e-6);
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(bar), ["op"] = "move", ["by"] = J(1, 0) });
        Assert.Equal(1, Args.PointOf(f.Model.Get(bar)!.Attrs["at"]).X, 6);
    }

    static string PdfText(byte[] pdf)
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
        return o.ToString();
    }
}
