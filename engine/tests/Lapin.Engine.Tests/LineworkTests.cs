using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Linework on generated lines: ported from the old Lapin's linework.test.ts (same cases and numbers), then the LW commands on a view.</summary>
public class LineworkTests
{
    static DLine L(double x0, double y0, double x1, double y1, int el = 1, string role = "projection") => new(new(x0, y0), new(x1, y1), role, $"G{el}", "IfcWall");
    static ViewDrawing D(params DLine[] lines) => new([], [.. lines]);
    static LineEdit E(double ax, double ay, double bx, double by, string action, LineStyleSpec? style = null, string id = "") =>
        new(id == "" ? Guid.NewGuid().ToString() : id, "G1", new(ax, ay), new(bx, by), action, style);
    static List<(double, double, string)> Spans(ViewDrawing d) => d.Lines.Select(l => (Math.Round(l.A.X, 9), Math.Round(l.B.X, 9), l.Role == "hidden" ? "hidden" : "visible")).ToList();

    [Fact]
    public void No_edits_the_drawing_itself_untouched()
    {
        var d = D(L(0, 0, 4, 0));
        Assert.Same(d, Linework.Apply(d, []).Drawing);
    }

    [Fact]
    public void Hides_a_piece_across_the_pieces_the_drawing_cut_the_edge_into_and_again_once_regenerated()
    {
        var hide = E(1, 0, 3, 0, "hide");
        // Hidden-line removal cut the edge at 2 this time.
        var first = Linework.Apply(D(L(0, 0, 2, 0), L(2, 0, 4, 0, role: "hidden")), [hide]);
        Assert.Equal([(0, 1, "visible"), (3, 4, "hidden")], Spans(first.Drawing));
        Assert.Empty(first.Orphans);
        var again = Linework.Apply(D(L(0, 0, 4, 0)), [hide]);
        Assert.Equal([(0, 1, "visible"), (3, 4, "visible")], Spans(again.Drawing));
        // Another element's line along the same place is not touched.
        Assert.Single(Linework.Apply(D(L(0, 0, 4, 0, 2)), [hide]).Drawing.Lines);
    }

    [Fact]
    public void Shows_a_hidden_edge_the_later_of_hide_and_show_wins_a_style_is_kept_on_its_piece()
    {
        var d = D(L(0, 0, 4, 0, role: "hidden"));
        Assert.Equal([(0, 4, "visible")], Spans(Linework.Apply(d, [E(0, 0, 4, 0, "show")]).Drawing));
        Assert.Empty(Linework.Apply(d, [E(0, 0, 4, 0, "show"), E(0, 0, 4, 0, "hide")]).Drawing.Lines);
        var styled = Linework.Apply(D(L(0, 0, 4, 0)), [E(2, 0, 4, 0, "style", new LineStyleSpec(Pen: 9))]).Drawing;
        Assert.Equal([(0.0, 2.0, (int?)null), (2.0, 4.0, 9)], styled.Lines.Select(l => (l.A.X, l.B.X, l.Style?.Pen)));
        // The restyled piece is drawn with the edit's pen at the view's scale.
        var pens = Pens.Of(styled, null, new Pens.Options(Scale: 50));
        Assert.Contains(pens, p => Math.Abs(p.Width - LineStyles.PenWidth(LineStyles.DefaultPenTable(), 9, 50)) < 1e-9);
    }

    [Fact]
    public void An_edit_whose_edge_is_gone_is_orphaned_and_draws_nothing()
    {
        var hide = E(0, 0, 4, 0, "hide", id: "h");
        var moved = Linework.Apply(D(L(0, 1, 4, 1)), [hide]);
        Assert.Equal(["h"], moved.Orphans);
        Assert.Single(moved.Drawing.Lines);
        Assert.Single(Linework.Apply(D(L(0, 0, 4, 0, 2)), [E(4, 0, 5, 0, "add")]).Orphans);
    }

    [Fact]
    public void An_edge_is_picked_whole_the_touching_pieces_of_its_element_along_it()
    {
        var d = D(L(0, 0, 2, 0), L(2, 0, 3, 0, role: "hidden"), L(3, 0, 5, 0), L(6, 0, 8, 0), L(0, 0, 9, 0, 2));
        var edge = Linework.EdgeAt(d, new Vec2(2.5, 0.01), 0.05)!;
        Assert.Equal("G1", edge.Element);
        Assert.Equal((0.0, 5.0), (edge.A.X, edge.B.X));
        Assert.Null(Linework.EdgeAt(d, new Vec2(2.5, 1), 0.05));
    }

    [Fact]
    public void JOIN_extends_a_short_edge_to_the_corner_and_cuts_back_an_overshooting_one()
    {
        var d = D(L(0, 0, 3, 0), L(4, -1, 4, 2, 2));
        var a = Linework.EdgeAt(d, new Vec2(1, 0), 0.05)!;
        var b = Linework.EdgeAt(d, new Vec2(4, 1.5), 0.05)!;
        var x = Linework.Crossing(a.A, a.B, b.A, b.B)!.Value;
        Assert.Equal(new Vec2(4, 0), x);
        Assert.Equal([("add", new Vec2(3, 0), new Vec2(4, 0))], Linework.ToCorner(a, x, "picked"));
        // B was clicked above the corner: its piece below is cut away.
        Assert.Equal([("hide", new Vec2(4, -1), new Vec2(4, 0))], Linework.ToCorner(b, x, "picked"));
        Assert.Equal([("hide", new Vec2(4, 0), new Vec2(4, 2))], Linework.ToCorner(b, x, "other"));
    }

    [Fact]
    public void A_new_edit_drops_the_earlier_ones_of_its_kind_it_covers()
    {
        var small = E(1, 0, 2, 0, "hide");
        var style = E(1, 0, 2, 0, "style", new LineStyleSpec(Pen: 3));
        var next = Linework.Add([small, style], E(0, 0, 4, 0, "show"));
        Assert.Equal(["style", "show"], next.Select(o => o.Action));
    }

    // ------------------------------------------------------------ on a view

    [Fact]
    public void LW_on_a_plan_hides_restyles_and_resets_one_undo_step_each()
    {
        var f = new Fixture();
        f.Wall(0, 0, 6, 0);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        int Cut() => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray().OfType<JsonObject>()
            .Where(p => (string)p["layer"]! == "CUT").Sum(p => p["segments"]!.AsArray().Count / 4);
        var all = Cut();
        // The wall's long face (y = −0.125 for the default 250 wall) hidden.
        f.Run("view.linework", new JsonObject { ["id"] = plan, ["at"] = new JsonArray(3, -0.125), ["action"] = "hide" });
        Assert.Equal(all - 1, Cut());
        f.Run("view.linework", new JsonObject { ["id"] = plan, ["at"] = new JsonArray(3, 0.125), ["action"] = "style", ["style"] = new JsonObject { ["pen"] = 9, ["pattern"] = "dashed" } });
        var pens = ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(pens, p => p["dash"] != null && (string)p["layer"]! == "CUT");
        f.Session.Undo();
        Assert.Single(Linework.Apply(new ViewDrawing([], []), ViewLinework.Edits(f.Model.Get(plan)!)).Orphans);
        f.Run("view.lineworkReset", new JsonObject { ["id"] = plan, ["all"] = true });
        Assert.Equal(all, Cut());
        // The storey's own plan, not yet a view, keeps no edits.
        Assert.Throws<CommandException>(() => f.Run("view.linework", new JsonObject { ["id"] = "plan:" + f.Storey, ["at"] = new JsonArray(3, -0.125) }));
    }

    [Fact]
    public void A_category_takes_any_pen_of_the_iso_series_and_its_seen_lines_two_pens_lighter()
    {
        var cut = new DLine(new(0, 0), new(4, 0), LineRole.Cut, "G1", "IfcWall");
        var seen = new DLine(new(0, 1), new(4, 1), LineRole.Projection, "G1", "IfcWall");
        double[] Widths(string weight) =>
            Pens.Of(D(cut, seen), null, new Pens.Options(Categories: new Dictionary<string, CategoryGraphics> { ["walls"] = new(LineWeight: weight) }))
                .Where(p => p.Layer is PenLayer.Cut or PenLayer.Seen).OrderBy(p => p.Layer == PenLayer.Cut ? 0 : 1).Select(p => p.Width).ToArray();
        Assert.Equal([1.0, 0.5], Widths("1.0"));
        Assert.Equal([0.7, 0.35], Widths("0.7"));
        Assert.Equal([0.35, 0.18], Widths("0.35"));
        Assert.Equal([0.13, 0.13], Widths("0.13"));
        Assert.Equal([0.18, 0.13], Widths("0.2"));      // a number in between takes the nearest pen
        Assert.Equal([0.7, 0.35], Widths("heavy"));     // the older names still read
        Assert.Equal(0.5, Pens.WeightMm(CategoryGraphics.From(new JsonObject { ["lineWeight"] = 0.5 })!.LineWeight));
        Assert.Null(Pens.WeightMm("thick"));
        Assert.Equal(100, Lapin.Drawing.Export.DxfWriter.LineWeight(1.0));
    }
}
