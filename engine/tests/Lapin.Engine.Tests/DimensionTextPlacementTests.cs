using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 78: a dimension value that does not fit between its witness lines is moved by the engine, just outside its
/// segment on the free side, else staggered above and below the line, clear of the other values and lines of the view, with
/// a leader once moved beyond the set distance (paper mm); a value moved by hand keeps its place; values that fit, and so
/// every drawing whose values all fit, are drawn exactly as before. At 1:100 a paper mm is 0.1 m and a value 0.25 m high.
/// </summary>
public class DimensionTextPlacementTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => "plan:" + f.Storey;

    /// <summary>A string along x through the points at x = <paramref name="xs"/> (y = 0), its line at y = <paramref name="at"/>.</summary>
    static string String(Fixture f, double at, params double[] xs) => Styled(f, null, at, xs);

    static string Styled(Fixture f, string? style, double at, params double[] xs)
    {
        var a = new JsonObject
        {
            ["viewId"] = Plan(f), ["pts"] = new JsonArray(xs.Select(x => (JsonNode)new JsonArray(x, 0)).ToArray()),
            ["at"] = new JsonArray(0, at), ["dir"] = new JsonArray(1, 0),
        };
        if (style != null) a["dimStyle"] = style;
        return (string)f.Run("drafting.addDimension", a)!["id"]!;
    }

    static ViewMark Mark(Fixture f, string dim) => Views.Render(f.Session.Reader, Views.Find(f.Model, Plan(f))!, false).Marks.Single(m => m.Owner == dim);
    static List<PenText> Texts(Fixture f, string dim) => Mark(f, dim).Texts.Select(t => t.Text).ToList();
    static string Drawing(Fixture f) => f.Session.Query("view.drawing", new JsonObject { ["id"] = Plan(f) })!.ToJsonString();
    static void Options(Fixture f, JsonObject o) => f.Run("standards.options", o);

    /// <summary>Where a value stands when nothing moves it: over its segment's middle, the style's gap (0.8 mm) above the line.</summary>
    static Vec2 Natural(double x0, double x1, double at) => new((x0 + x1) / 2, at + 0.08);

    [Fact]
    public void A_short_segments_value_moves_just_outside_it_on_the_free_side()
    {
        var f = F();
        // A 10 cm reveal next to a 2.40 m window: "100" (some 0.58 m wide) cannot stand between witness lines 0.1 m apart.
        var dim = String(f, 2, 0, 0.1, 2.5);
        var t = Texts(f, dim);
        Assert.Equal(["100", "2400"], t.Select(x => x.Text));
        // Outside the string's end, on the same row, clear of the segment; the 2.40 m value where it always was.
        Assert.True(t[0].At.X < -0.29, $"the value stands left of the segment (x = {t[0].At.X})");
        Assert.Equal(2.08, t[0].At.Y, 6);
        Assert.Equal(Natural(0.1, 2.5, 2), t[1].At);
        // No leader at this distance (under 5 mm on paper); the grips learn how far it went.
        Assert.Equal(Mark(f, dim).Lines.Count, LinesOff(f, dim));
        Assert.NotNull(Mark(f, dim).Data!["textAuto"]![0]);
        Assert.Null(Mark(f, dim).Data!["textAuto"]![1]);
    }

    /// <summary>The lines the dimension draws with the placement off.</summary>
    static int LinesOff(Fixture f, string dim)
    {
        Options(f, new JsonObject { ["dimAutoText"] = false });
        var n = Mark(f, dim).Lines.Count;
        f.Session.Undo();
        return n;
    }

    [Fact]
    public void Two_short_neighbours_are_staggered_above_and_below_the_line()
    {
        var f = F();
        // Four 10 cm segments: the outer two go outside the string's ends, the inner two above and below the line.
        var dim = String(f, 2, 0, 0.1, 0.2, 0.3, 0.4);
        var t = Texts(f, dim);
        Assert.True(t[0].At.X < 0, "the first outside the string's start");
        Assert.True(t[3].At.X > 0.4, "the last outside its end");
        Assert.Equal(0.15, t[1].At.X, 6);
        Assert.Equal(0.25, t[2].At.X, 6);
        Assert.True(t[1].At.Y > 2.08 + 0.25, "the second a row higher");
        Assert.True(t[2].At.Y + 0.25 < 2, "the third under the line");
    }

    [Fact]
    public void A_value_moved_beyond_the_set_distance_takes_a_leader_to_its_segment()
    {
        var f = F();
        var dim = String(f, 2, 0, 0.1, 2.5);
        var off = LinesOff(f, dim);
        Assert.Equal(off, Mark(f, dim).Lines.Count);
        // Moved some 3.8 mm on paper: with a leader beyond 2 mm, one line from the segment's middle to the value.
        Options(f, new JsonObject { ["dimTextLeader"] = 2 });
        var mark = Mark(f, dim);
        Assert.Equal(off + 1, mark.Lines.Count);
        var leader = mark.Lines[^1];
        Assert.Equal(new Vec2(0.05, 2), leader.A);
        // It ends at the value's side nearest the segment, a gap short of it (forum topic 227).
        var value = mark.Texts[0].Text;
        Assert.Equal(value.At.X + BuildupText.Measure(value.Text, value.Height, false) * 0.1 / 2 + DimensionTextPlacement.LeaderGap * 0.1, leader.B.X, 6);
        Assert.True(leader.B.X < leader.A.X);
        // A dimension style may say its own: off, or a leader distance of its own.
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = Standards.Standard, ["textLeader"] = 10 } });
        Assert.Equal(off, Mark(f, dim).Lines.Count);
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = Standards.Standard, ["autoText"] = "off" } });
        Assert.Equal(Natural(0, 0.1, 2), Texts(f, dim)[0].At);
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["dimTextLeader"] = -1 }));
    }

    [Fact]
    public void A_value_the_user_moved_keeps_its_place()
    {
        var f = F();
        var dim = String(f, 2, 0, 0.1, 2.5);
        f.Run("drafting.moveDimensionText", new JsonObject { ["id"] = dim, ["segment"] = 0, ["by"] = new JsonArray(0.3, 0.5) });
        var t = Texts(f, dim);
        Assert.Equal(Natural(0, 0.1, 2) + new Vec2(0.3, 0.5), t[0].At);
        Assert.Null(Mark(f, dim).Data!["textAuto"]);
    }

    [Fact]
    public void Values_of_a_neighbouring_string_are_kept_clear_of()
    {
        // Alone, the second of four short segments goes a row up; with another string 5.5 mm above (as an exterior
        // dimension's strings stand), that row would cross its line, so it goes under its own line instead.
        var f = F();
        var dim = String(f, 2, 0, 0.1, 0.2, 0.3, 0.4);
        Assert.True(Texts(f, dim)[1].At.Y > 2);
        String(f, 2.55, 0, 3);
        var t = Texts(f, dim);
        Assert.True(t[1].At.Y + 0.25 < 2, $"under its own line (y = {t[1].At.Y})");
        Assert.All(t, x => Assert.True(x.At.Y + 0.25 < 2.55 - 1e-9, $"no value over the other string's line ({string.Join("; ", t.Select(q => $"{q.Text} {q.At.X:0.###},{q.At.Y:0.###}"))})"));
    }

    [Fact]
    public void A_view_whose_values_all_fit_is_drawn_exactly_as_with_the_placement_off()
    {
        // Walls, an opening (its height under the line), strings with values above, on and below the line, a manual text:
        // the drawing with the placement on (a new project's and an older one's default) is the one drawn without it.
        var f = F();
        f.Session.New();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var wall = f.Wall(0, 0, 8, 0, type, height: 3.0);
        f.Wall(8, 0, 8, 6, type, height: 3.0);
        f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["width"] = 1.2, ["height"] = 1.5, ["sill"] = 0.9 });
        String(f, -1, 0, 2.4, 3.6, 8);
        String(f, -1.7, 0, 8);
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = "centred", ["name"] = "Centred", ["placement"] = "centered" } });
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = "under", ["name"] = "Under", ["placement"] = "below" } });
        Styled(f, "centred", 1, 0, 3, 8);
        Styled(f, "under", 2, 0, 3, 8);
        var vertical = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = Plan(f), ["pts"] = new JsonArray(new JsonArray(8, 0), new JsonArray(8, 2.5), new JsonArray(8, 6)), ["at"] = new JsonArray(9, 0),
        })!["id"]!;
        f.Run("drafting.setDimensionText", new JsonObject { ["id"] = vertical, ["segment"] = 1, ["text"] = "3500 (approx.)" });
        f.Run("drafting.moveDimensionText", new JsonObject { ["id"] = vertical, ["segment"] = 0, ["by"] = new JsonArray(0.2, 0) });
        Assert.True(DraftingResources.Book(f.Model).DimAutoText);
        var on = Drawing(f);
        Options(f, new JsonObject { ["dimAutoText"] = false });
        Assert.False(DraftingResources.Book(f.Model).DimAutoText);
        Assert.Equal(Drawing(f), on);
        // Turned on again it says so; given null, the default.
        Options(f, new JsonObject { ["dimAutoText"] = null });
        Assert.True(DraftingResources.Book(f.Model).DimAutoText);
        Assert.DoesNotContain("textAuto", on);
    }

    [Fact]
    public void Standards_saved_before_read_the_placement_on_with_a_5_mm_leader()
    {
        var b = Standards.Read(new JsonObject { ["dimStyles"] = new JsonArray(new JsonObject { ["id"] = "standard", ["name"] = "Standard" }) });
        Assert.True(b.DimAutoText);
        Assert.Equal(5, b.DimTextLeader);
        var s = Standards.ResolveDim(b, new JsonObject());
        Assert.True(s.PlacesTexts);
        Assert.Equal(5, s.TextLeader);
        Assert.False(Standards.ResolveDim(b, new JsonObject { ["o.autoText"] = "off" }).PlacesTexts);
    }
}
