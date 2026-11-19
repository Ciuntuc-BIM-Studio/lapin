using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A window's sills tagged and scheduled (forum topic 178, decision 727, tester LVK): the interior and exterior sill of a
/// window's type (its detailed sills, else its plain boards) are parts of the window with their own fields (side, material,
/// length, depth, projection, thickness, slope); a field schedule lists them as rows (the shipped "Window sills", for the
/// sills' supplier); a tag points at one (named, or the one clicked) and reads its fields; the window reads them too.
/// </summary>
public class WindowSillTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Rendered(Fixture f) => f.Type("Rendered", ("Render", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
    static readonly JoineryDetails Stone = new(
        SillInside: new JoinerySill(true, 0.04, 0.03, 0.02, 0, "Timber"),
        SillOutside: new JoinerySill(true, 0.05, 0.02, 0.03, 10, "Granite"));

    static string TypeOf(Fixture f, JoinerySpec spec) => (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
    static string Window(Fixture f, string wall, double along, string? type = null)
    {
        var a = new JsonObject { ["wallId"] = wall, ["along"] = along };
        if (type != null) a["typeId"] = type;
        return (string)f.Run("architecture.addWindow", a)!["id"]!;
    }

    static (Vec3 Min, Vec3 Max) Box(IReadOnlyList<Vec3> pts) =>
        (new(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z)), new(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z)));

    [Fact]
    public void A_windows_detailed_sills_are_parts_with_their_own_measures_where_its_body_draws_them()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0, Rendered(f));
        var spec = JoinerySpec.DefaultWindow("Dressed") with { Details = Stone };
        var w = Window(f, wall, 3, TypeOf(f, spec));
        var sills = WindowSills.Of(f.Model, f.Model.Get(w)!);
        Assert.Equal(["sill:inside", "sill:outside"], sills.Select(s => s.Key));
        var (inside, outside) = (sills[0], sills[1]);
        Assert.Equal("interior", inside.Side);
        Assert.Equal("Timber", inside.Material);
        Assert.Equal("Granite", outside.Material);
        Assert.True(inside.Detailed && outside.Detailed);
        Assert.Equal(spec.Width + 0.04, inside.Length, 6);
        Assert.Equal(spec.Width + 0.06, outside.Length, 6);
        Assert.Equal(0.04, inside.Projection, 6);
        Assert.Equal(0.05, outside.Projection, 6);
        Assert.Equal(10, outside.Slope, 6);
        // Across the wall: from the frame's face to past the wall's (285 mm wall, the frame centred).
        var t = 0.285;
        Assert.Equal(t / 2 + 0.04 - spec.Frame.Depth / 2 + spec.Frame.Offset, inside.Depth, 6);
        Assert.Equal(t / 2 + 0.05 - spec.Frame.Depth / 2 - spec.Frame.Offset, outside.Depth, 6);
        // Where the body draws them: the inside sill's corners are its part's box.
        var part = f.Session.Geometry(w)!.Parts.Single(p => p.Key == "sill:inside");
        var (pb, sb) = (Measure.Bounds(part.Solid)!.Value, Box(inside.Points));
        Assert.Equal(pb.Min.X, sb.Min.X, 6); Assert.Equal(pb.Max.Y, sb.Max.Y, 6); Assert.Equal(pb.Min.Z, sb.Min.Z, 6);
        var outPart = Measure.Bounds(f.Session.Geometry(w)!.Parts.Single(p => p.Key == "sill:outside").Solid)!.Value;
        Assert.Equal(outPart.Min.Y, Box(outside.Points).Min.Y, 6);
        // The window's own fields read them (tags, schedules of windows, filters).
        var e = f.Model.Get(w)!;
        Assert.Equal("Granite", TagText.Value(f.Model, e, "sillExteriorMaterial"));
        Assert.Equal(((int)Math.Round((spec.Width + 0.06) * 1000)).ToString(), TagText.Value(f.Model, e, "sillExteriorLength"));
        Assert.Equal("30", TagText.Value(f.Model, e, "sillInteriorThickness"));
    }

    [Fact]
    public void A_plain_board_is_a_sill_too_and_a_door_has_none()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0);
        var spec = JoinerySpec.DefaultWindow("Plain");
        var w = Window(f, wall, 2, TypeOf(f, spec));
        var sills = WindowSills.Of(f.Model, f.Model.Get(w)!);
        Assert.Equal(2, sills.Count);
        Assert.All(sills, s => Assert.False(s.Detailed));
        Assert.Equal(spec.Board.Interior, sills[0].Depth, 6);
        Assert.Equal(spec.Board.Exterior, sills[1].Depth, 6);
        Assert.All(sills, s => Assert.Equal(spec.Width, s.Length, 6));
        // A board switched off is no sill; a door's leaf on the floor has none.
        var bare = Window(f, wall, 4.5, TypeOf(f, spec with { Name = "Bare", Board = new JoineryBoard(0, 0) }));
        Assert.Empty(WindowSills.Of(f.Model, f.Model.Get(bare)!));
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 6.5 })!["id"]!;
        Assert.Empty(WindowSills.Of(f.Model, f.Model.Get(door)!));
    }

    [Fact]
    public void The_window_sills_schedule_lists_each_kind_of_sill_with_its_count_and_windows()
    {
        var f = F();
        var wall = f.Wall(0, 0, 12, 0, Rendered(f));
        var dressed = TypeOf(f, JoinerySpec.DefaultWindow("Dressed") with { Mark = "W1", Details = Stone });
        var plain = TypeOf(f, JoinerySpec.DefaultWindow("Plain") with { Mark = "W2", Width = 0.9 });
        Window(f, wall, 2, dressed);
        Window(f, wall, 5, dressed);
        Window(f, wall, 8, plain);
        var made = f.Run("architecture.importSchedule", new JsonObject { ["builtin"] = "windowSills" })!;
        var schedule = f.Model.Get((string)made["id"]!)!;
        Assert.Equal("Window sills", schedule.Name);
        var grid = new FieldSchedule().Grid(f.Session.Reader, schedule);
        Assert.Equal(["Side", "Material", "Length (mm)", "Depth (mm)", "Thickness (mm)", "Slope (°)", "Windows", "Count", "Total length (m)", "Area (m²)"], grid.Columns);
        // Two dressed windows' sills on one row each side; the plain one's boards on their own: four rows, and the totals.
        var rows = grid.Rows.Where(r => r[0] is "Interior" or "Exterior").ToList();
        Assert.Equal(4, rows.Count);
        var granite = rows.Single(r => r[1] == "Granite");
        Assert.Equal("Exterior", granite[0]);
        Assert.Equal("2", granite[7]);
        Assert.Equal("10", granite[5]);
        Assert.Contains("W1", granite[6]);
        Assert.DoesNotContain("W2", granite[6]);
        var board = rows.Single(r => r[0] == "Interior" && r[1] != "Timber");
        Assert.Equal("900", board[2]);
        Assert.Equal("1", board[7]);
        // The part source is offered with its fields.
        var sources = (JsonArray)f.Session.Query("schedule.partSources", new JsonObject())!;
        Assert.Contains(sources.OfType<JsonObject>(), s => (string)s["source"]! == "sills" && s["fields"]!.AsArray().Any(x => (string)x!["key"]! == "sillMaterial"));
    }

    static JsonObject Draw(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static List<string> Texts(JsonObject d) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null)
        .SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).ToList();
    static JsonObject Mark(JsonObject d, string id) => d["marks"]!.AsArray().OfType<JsonObject>().First(m => (string)m["id"]! == id);

    [Fact]
    public void A_tag_points_at_a_sill_and_reads_its_fields()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0, Rendered(f));
        var w = Window(f, wall, 3, TypeOf(f, JoinerySpec.DefaultWindow("Dressed") with { Details = Stone }));
        var view = "plan:" + f.Storey;
        var outside = WindowSills.Of(f.Model, f.Model.Get(w)!).Single(s => s.Side == "exterior");
        // Named: by default its few words; its leader to the sill, not the window's middle.
        var tag = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["part"] = "sill:outside", ["at"] = new JsonArray(3, -1.5) })!["id"]!;
        Assert.Equal("sill:outside", f.Model.Get(tag)!.Str("part"));
        var d = Draw(f, view);
        Assert.Contains(WindowSills.Label(outside), Texts(d));
        var end = Mark(d, tag)["data"]!["end"]!.AsArray().Select(x => (double)x!).ToArray();
        var (min, max) = Box(outside.Points);
        Assert.Equal((min.Y + max.Y) / 2, end[1], 6);
        // A field of the sill's: its material.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["field"] = "sillMaterial" });
        Assert.Contains("Granite", Texts(Draw(f, view)));
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["template"] = "{mark} {sillSide}|{sillLength}" });
        Assert.Contains("Exterior", Texts(Draw(f, view)).Single(x => x.Contains("Exterior")));
        Assert.ThrowsAny<Exception>(() => f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["part"] = "handle", ["at"] = new JsonArray(3, 2) }));
    }

    [Fact]
    public void A_click_on_the_sill_tags_the_sill_and_a_click_on_the_window_the_window()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0, Rendered(f));
        var w = Window(f, wall, 3, TypeOf(f, JoinerySpec.DefaultWindow("Dressed") with { Details = Stone }));
        var view = "plan:" + f.Storey;
        var inside = WindowSills.Of(f.Model, f.Model.Get(w)!).Single(s => s.Side == "interior");
        var (min, max) = Box(inside.Points);
        var onSill = new JsonArray((min.X + max.X) / 2, (min.Y + max.Y) / 2);
        var t1 = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["pick"] = onSill, ["at"] = new JsonArray(3, 1.5) })!["id"]!;
        Assert.Equal("sill:inside", f.Model.Get(t1)!.Str("part"));
        Assert.Equal("partLabel", f.Model.Get(t1)!.Str("field"));
        // On the frame, in the wall's middle: the window, its mark as before.
        var t2 = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["pick"] = new JsonArray(3.4, 0), ["field"] = "mark", ["at"] = new JsonArray(3, -1.5) })!["id"]!;
        Assert.Null(f.Model.Get(t2)!.Str("part"));
        // The sill gone (the type's sills switched off, its boards too): its tag shows a question mark.
        var type = f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!;
        var spec = JoinerySpec.From(type.Attrs["spec"])! with { Details = null, Board = new JoineryBoard(0, 0) };
        f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = type.Id, ["spec"] = spec.ToJson() });
        Assert.Contains("?", Texts(Draw(f, view)));
    }
}
