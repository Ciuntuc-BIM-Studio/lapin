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
/// A plan's orientation (decision 526, a tester's question: "how do I rotate the view along my screen?"): the view keeps
/// the model direction it shows horizontal, and its sheets, PDF and DXF print it as the screen shows it, texts readable.
/// </summary>
public class PlanOrientationTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static readonly double C30 = Math.Cos(Math.PI / 6), S30 = Math.Sin(Math.PI / 6);

    /// <summary>A plan turned to <paramref name="angle"/>° from the level's default plan: the view it became.</summary>
    static string Turned(Fixture f, double angle) =>
        (string)f.Run("view.setOrientation", new JsonObject { ["id"] = "plan:" + f.Storey, ["angle"] = angle })!["id"]!;

    static (string Sheet, string Viewport) OnSheet(Fixture f, string view)
    {
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans" })!["id"]!;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = view, ["x"] = 300, ["y"] = 250 })!["viewportId"]!;
        return (s, vp);
    }

    static List<(Vec2 A, Vec2 B)> Cuts(IEnumerable<Pen> pens) => pens.Where(p => p.Layer == "CUT").SelectMany(p => p.Segments).ToList();

    [Fact]
    public void A_wall_along_30_degrees_comes_out_horizontal_on_the_sheet_of_a_plan_turned_to_30()
    {
        var f = F();
        f.Wall(0, 0, 10 * C30, 10 * S30);
        var view = Turned(f, 30);
        Assert.Equal(30, Views.Orientation(f.Model, f.Model.Get(view)!), 9);
        var (s, vp) = OnSheet(f, view);
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!);
        var cuts = Cuts(page.Pens.Where(p => p.Viewport == vp));
        Assert.NotEmpty(cuts);
        // The wall's long sides: 10 m at 1:100 is about 100 mm, now level on the paper.
        var longest = cuts.OrderByDescending(c => (c.B - c.A).Length).First();
        Assert.True((longest.B - longest.A).Length > 95);
        Assert.True(Math.Abs(longest.B.Y - longest.A.Y) < 1e-6, $"not horizontal: {longest}");
        // Every cut line is level or upright: the wall's sides and its ends.
        Assert.All(cuts, c => Assert.True(Math.Abs(c.B.Y - c.A.Y) < 1e-6 || Math.Abs(c.B.X - c.A.X) < 1e-6, $"{c}"));
        // The box fits the turned drawing: a wall level on paper makes a wide, low box.
        var pl = Sheets.Place(f.Session.Reader, f.Model.Get(s)!).Single();
        Assert.True(pl.X1 - pl.X0 > 100 && pl.Y1 - pl.Y0 < 60, $"{pl.X1 - pl.X0} × {pl.Y1 - pl.Y0}");
    }

    [Fact]
    public void Not_turned_the_sheet_prints_as_before()
    {
        var f = F();
        f.Wall(0, 0, 10 * C30, 10 * S30);
        var (s, vp) = OnSheet(f, "plan:" + f.Storey);
        var longest = Cuts(Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens.Where(p => p.Viewport == vp)).OrderByDescending(c => (c.B - c.A).Length).First();
        // On paper y runs down: the wall rises to the right at 30° (its line at 150° in the paper's y-down terms).
        var deg = Math.Atan2(longest.B.Y - longest.A.Y, longest.B.X - longest.A.X) * 180 / Math.PI;
        Assert.Equal(150, (deg % 180 + 180) % 180, 6);
    }

    [Fact]
    public void Texts_stay_readable_upright_within_90_degrees_on_a_turned_sheet()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        // A dimension along the wall and a level text, in the plan turned by 150° (which would put them upside down).
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(8, 0), ["at"] = new JsonArray(0, 1) });
        f.Run("drafting.addText", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["at"] = new JsonArray(4, 3), ["text"] = "Living room" });
        var view = Turned(f, -150);
        var (s, vp) = OnSheet(f, view);
        var texts = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens.Where(p => p.Viewport == vp).SelectMany(p => p.Texts).ToList();
        Assert.Contains(texts, t => t.Text == "Living room");
        Assert.Contains(texts, t => t.Text.Contains("8000"));
        Assert.All(texts, t => Assert.True(Turning.Readable(t.Angle), $"{t.Text} at {t.Angle}°"));
        // Oriented to −150°, the drawing turns by 150°: a level text would read at 150°, upside down; turned over, it reads at −30°.
        Assert.Equal(-30,texts.First(t => t.Text == "Living room").Angle, 6);
        // The annotations drawn on the level's default plan went with it onto the plan view it became (decision 386).
        Assert.All(f.Model.OfClass(IfcClass.Annotation), a => Assert.Equal(view, a.Str("viewId")));
    }

    [Fact]
    public void A_text_turned_over_keeps_its_footprint()
    {
        // Along +X, anchored at its start, sitting on its point: turned by 180° it reads left to right again, ending where it began, above the same line.
        var t = new PenText(new Vec2(0, 0), "A", 2.5, 0, "start", "bottom");
        var u = Turning.Upright(t, 180, 0.1);
        Assert.Equal(0, u.Angle, 9);
        Assert.Equal("end", u.Anchor);
        Assert.Equal(0.25, u.At.Y, 9);
        Assert.Same(t, Turning.Upright(t, 0, 0.1));
        Assert.True(Turning.Readable(90));
        Assert.False(Turning.Readable(-90));
    }

    [Fact]
    public void Setting_it_is_one_step_on_a_plan_only_and_follows_the_UCS_when_asked()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var view = Turned(f, 30);
        // The level's default plan became its plan view, turned: undone in one step, as it was.
        Assert.Equal(view, StoreyPlans.Of(f.Model, f.Storey)!.Id);
        f.Session.Undo();
        Assert.Null(StoreyPlans.Of(f.Model, f.Storey));
        view = Turned(f, 30);
        f.Run("view.setOrientation", new JsonObject { ["id"] = view, ["angle"] = 390 });
        Assert.Equal(30.0, f.Model.Get(view)!.Num("orientation"), 9);
        f.Run("view.setOrientation", new JsonObject { ["id"] = view, ["angle"] = null });
        Assert.Null(f.Model.Get(view)!.Attrs["orientation"]);
        Assert.Equal(0, Views.Turn(f.Model, f.Model.Get(view)!));
        // Following the UCS: its X axis, as it changes.
        f.Run("view.setOrientation", new JsonObject { ["id"] = view, ["ucs"] = true });
        Assert.Equal(0, Views.Orientation(f.Model, f.Model.Get(view)!));
        f.Run("project.location", new JsonObject { ["ucs"] = new JsonObject { ["x"] = 1, ["y"] = 2, ["angle"] = 25 } });
        Assert.Equal(25, Views.Orientation(f.Model, f.Model.Get(view)!), 9);
        Assert.Equal(-25 * Math.PI / 180, Views.Turn(f.Model, f.Model.Get(view)!), 9);
        // A section is not turned.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, -1), ["end"] = new JsonArray(0, 1) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("view.setOrientation", new JsonObject { ["id"] = section, ["angle"] = 30 }));
        Assert.Throws<CommandException>(() => f.Run("view.setOrientation", new JsonObject { ["id"] = view, ["angle"] = "east" }));
        // A new plan view may be made turned.
        var made = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Turned", ["orientation"] = new JsonObject { ["angle"] = -45 } })!["id"]!;
        Assert.Equal(-45, Views.Orientation(f.Model, f.Model.Get(made)!), 9);
    }

    [Fact]
    public void Moving_what_a_turned_viewport_shows_follows_the_paper()
    {
        var f = F();
        f.Wall(0, 0, 10, 0);
        var view = Turned(f, 90);
        var (s, vp) = OnSheet(f, view);
        var before = Sheets.Place(f.Session.Reader, f.Model.Get(s)!).Single();
        // Turned to 90°, the paper's right is the plan's +y: sliding the drawing 10 mm right (1 m at 1:100) brings into the
        // middle what was 1 m to its left, 1 m along the plan's −y.
        f.Run("sheet.panViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["dx"] = 10, ["dy"] = 0 });
        var after = Sheets.Place(f.Session.Reader, f.Model.Get(s)!).Single();
        var moved = after.Middle - before.Middle;
        Assert.Equal(0, moved.X, 9);
        Assert.Equal(-1, moved.Y, 9);
        // A point of the plan stays put on the paper as the box is cropped.
        var p = new Vec2(10, 0);
        var at = after.ToPaper(p);
        f.Run("sheet.cropViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["x0"] = after.X0 + 5, ["y0"] = after.Y0, ["x1"] = after.X1, ["y1"] = after.Y1 - 7 });
        var cropped = Sheets.Place(f.Session.Reader, f.Model.Get(s)!).Single();
        var again = cropped.ToPaper(p);
        Assert.Equal(at.X, again.X, 6);
        Assert.Equal(at.Y, again.Y, 6);
    }

    [Fact]
    public void Its_DXF_in_its_own_coordinates_shows_it_turned_and_the_map_s_stays_the_map_s()
    {
        var f = F();
        f.Wall(0, 0, 10 * C30, 10 * S30);
        var view = Turned(f, 30);
        var dxf = Encoding.UTF8.GetString(Views.Dxf(f.Session.Reader, f.Model.Get(view)!));
        // Its LINE entities: the wall's long sides now level (10 000 mm along X, the same Y at both ends).
        var lines = Lines(dxf);
        Assert.Contains(lines, l => Math.Abs(l.B.X - l.A.X) > 9000 && Math.Abs(l.B.Y - l.A.Y) < 1e-3);
        // From the project base point: as the model lies, not turned.
        var project = Lines(Encoding.UTF8.GetString(Views.Dxf(f.Session.Reader, f.Model.Get(view)!, "project")));
        Assert.DoesNotContain(project, l => Math.Abs(l.B.X - l.A.X) > 9000 && Math.Abs(l.B.Y - l.A.Y) < 1e-3);
    }

    /// <summary>A DXF's LINE entities (group codes 10, 20, 11, 21).</summary>
    static List<(Vec2 A, Vec2 B)> Lines(string dxf)
    {
        var rows = dxf.Replace("\r", "").Split('\n').Select(r => r.Trim()).ToList();
        var o = new List<(Vec2, Vec2)>();
        for (var i = 0; i + 1 < rows.Count; i += 2)
        {
            if (rows[i] != "0" || rows[i + 1] != "LINE") continue;
            var v = new Dictionary<string, double>();
            for (var j = i + 2; j + 1 < rows.Count && rows[j] != "0"; j += 2)
                if (double.TryParse(rows[j + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) v[rows[j]] = d;
            if (v.TryGetValue("10", out var x1) && v.TryGetValue("20", out var y1) && v.TryGetValue("11", out var x2) && v.TryGetValue("21", out var y2))
                o.Add((new Vec2(x1, y1), new Vec2(x2, y2)));
        }
        return o;
    }
}
