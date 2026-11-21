using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Ready-made door and window markers after national conventions (forum topic 205). The Polish door: a line from the frame's
/// middle 60 cm out to a bubble 30 cm across holding the mark, the clear width over the line and the clear height under it,
/// the fire rating at the frame's middle when the door has one. The Polish window: the line run inside to 10 cm past the
/// wall's interior face, the wall opening over and under it, the sill over the finished floor at its interior end. The Czech
/// door: the classic marker with the clear size in mm. Added from the standards as the project's own; a new project's list
/// and every marker it draws stay as they were.
/// </summary>
public sealed class BubbleMarkerTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static Views.Rendered Render(Fixture f, string view) => Views.Render(new Q(f), Views.Find(f.Model, view)!, print: true);
    static List<PenText> Texts(Views.Rendered r) => r.Pens.SelectMany(p => p.Texts).ToList();
    static List<(Vec2 A, Vec2 B)> TagLines(Views.Rendered r) => r.Pens.Where(p => p.Layer == "TAG").SelectMany(p => p.Segments).ToList();

    /// <summary>A wall from (0, 0) to <paramref name="end"/>, a door at 1.5 m and a window at 4 m along it, its storey's plan, the Polish markers added.</summary>
    static (Fixture F, string Wall, string Door, string Window, string Plan) Project(Vec2? end = null)
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var e = end ?? new Vec2(6, 0);
        var wall = f.Wall(0, 0, e.X, e.Y);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Equal("door-pl", (string?)f.Run("standards.addTagPreset", new JsonObject { ["preset"] = "door-pl" })!["id"]);
        Assert.Equal("window-pl", (string?)f.Run("standards.addTagPreset", new JsonObject { ["preset"] = "window-pl" })!["id"]);
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan, ["doorType"] = "door-pl", ["windowType"] = "window-pl" });
        return (f, wall, door, window, plan);
    }

    /// <summary>The wall's two faces and its centre plane at <paramref name="along"/>, in the plan (the wall's axis need not be its middle).</summary>
    static (Vec2 A, Vec2 B, Vec2 Centre) Faces(Fixture f, string wall, double along)
    {
        var fr = Openings.FrameOf(f.Model, f.Model.Get(wall)!, along);
        Vec2 P(double y) { var p = fr.At(along, y, 0); return new Vec2(p.X, p.Y); }
        return (P(fr.YMin), P(fr.YMax), P((fr.YMin + fr.YMax) / 2));
    }

    /// <summary>The TAG segments within 0.5 m of <paramref name="x"/> along the wall: the marker's line (the longest) and its bubble's chords.</summary>
    static List<(Vec2 A, Vec2 B)> Near(Views.Rendered r, double x) => TagLines(r).Where(s => Math.Abs((s.A.X + s.B.X) / 2 - x) < 0.5).ToList();

    [Fact]
    public void The_presets_are_offered_and_added_as_the_projects_own_and_a_new_project_is_as_before()
    {
        Assert.Equal(["door", "window", "room", "fire", "acoustic", "thermal", "buildup"], Standards.Read(null).TagTypes.Select(t => t.Id));
        // The defaults written as before: none of the new fields in their JSON.
        var written = Standards.ToJson(Standards.Read(null))["tagTypes"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.All(written, t => Assert.False(t.ContainsKey("layout") || t.ContainsKey("lineLength") || t.ContainsKey("above")));
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var offered = f.Session.Query("standards.tagPresets", new JsonObject())!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(["door-pl", "window-pl", "door-cz"], offered.Select(o => (string)o["id"]!));
        Assert.All(offered, o => Assert.False((bool)o["added"]!));
        f.Run("standards.addTagPreset", new JsonObject { ["preset"] = "door-pl" });
        // Again: a copy beside it, numbered.
        Assert.Equal("door-pl-2", (string?)f.Run("standards.addTagPreset", new JsonObject { ["preset"] = "door-pl" })!["id"]);
        var book = DraftingResources.Book(f.Model);
        var pl = Standards.TagTypeOf(book, "door-pl")!;
        Assert.Equal(("bubble", "{clearWidth}", "{clearHeight}", "{FireRating}", 0.6, 0.3), (pl.Layout, pl.Above, pl.Below, pl.Centre, pl.LineLengthOrDefault, pl.BubbleSizeOrDefault));
        Assert.Equal("Door marker (Polish: bubble, clear opening) 2", Standards.TagTypeOf(book, "door-pl-2")!.Name);
        Assert.True((bool)f.Session.Query("standards.tagPresets", new JsonObject())!.AsArray().OfType<JsonObject>().First()["added"]!);
        // The door and window markers a project starts with are still the first of their classes.
        Assert.Equal("door", Standards.TagTypeOf(book, null, IfcClass.Door)!.Id);
        Assert.Throws<CommandException>(() => f.Run("standards.addTagPreset", new JsonObject { ["preset"] = "nope" }));
    }

    [Fact]
    public void A_polish_door_marker_runs_from_the_frames_middle_to_a_bubble_with_the_clear_size_over_and_under_its_line()
    {
        var (f, wall, door, _, plan) = Project();
        var r = Render(f, plan);
        var segs = Near(r, 1.5);
        // The line: across the wall at the door's middle, from the frame's middle (the wall's centre plane here) 60 cm out.
        var line = segs.OrderByDescending(s => (s.B - s.A).Length).First();
        Assert.Equal(0.6, (line.B - line.A).Length, 6);
        Assert.Equal(1.5, line.A.X, 6);
        Assert.Equal(1.5, line.B.X, 6);
        var (fa, fb, c) = Faces(f, wall, 1.5);
        var (inner, outer) = Math.Abs(line.A.Y - c.Y) < Math.Abs(line.B.Y - c.Y) ? (line.A, line.B) : (line.B, line.A);
        Assert.Equal(c.Y, inner.Y, 6);
        var outY = Math.Sign(outer.Y - c.Y);
        // The bubble: 30 cm across, just past the line's end, the mark in its middle along the wall.
        var ring = segs.Where(s => s != line).SelectMany(s => new[] { s.A, s.B }).ToList();
        Assert.True(ring.Count > 20);
        var centre = new Vec2(1.5, c.Y + outY * 0.75);
        Assert.All(ring, p => Assert.Equal(0.15, (p - centre).Length, 2));
        var texts = Texts(r);
        var mark = texts.Single(t => t.Text == TagText.Mark(f.Model, f.Model.Get(door)!));
        Assert.Equal(1.5, mark.At.X, 6);
        Assert.Equal(c.Y + outY * 0.75, mark.At.Y, 6);
        Assert.Equal(0, mark.Angle, 6);
        // The clear opening of the default door (900 × 2100, 50 mm frame, 20 mm threshold) in cm: 80 over the line, 203 under it,
        // both along the line, read upright: rising to the left of the way they read, over is the reader's up.
        var width = texts.Single(t => t.Text == "80");
        var height = texts.Single(t => t.Text == "203");
        Assert.Equal(90, width.Angle, 6);
        Assert.Equal(90, height.Angle, 6);
        Assert.True(width.At.X < 1.5 && height.At.X > 1.5, $"{width.At} {height.At}");
        // Between the wall's face and the bubble.
        var face = outY > 0 ? Math.Max(fa.Y, fb.Y) : Math.Min(fa.Y, fb.Y);
        Assert.Equal((face + c.Y + outY * 0.6) / 2, width.At.Y, 6);
        // No fire rating, nothing at the frame's middle; given one, it stands there on the room's side of the line.
        Assert.Equal(3, texts.Count(t => Math.Abs(t.At.X - 1.5) < 0.5));
        f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI30" });
        var fr = Texts(Render(f, plan)).Single(t => t.Text == "EI30");
        Assert.Equal(1.5, fr.At.X, 6);
        Assert.True(Math.Sign(fr.At.Y - c.Y) == -outY, $"{fr.At}");
        Assert.Equal(0, fr.Angle, 6);
    }

    [Fact]
    public void A_polish_window_marker_runs_inside_past_the_interior_face_with_the_wall_opening_and_the_sill()
    {
        var (f, wall, _, window, plan) = Project();
        var r = Render(f, plan);
        var line = Near(r, 4).OrderByDescending(s => (s.B - s.A).Length).First();
        var (fa, fb, c) = Faces(f, wall, 4);
        var half = Math.Abs(fa.Y - fb.Y) / 2;
        // 10 cm past the interior face, the wall's half thickness to the middle, 60 cm out of it.
        Assert.Equal(half + 0.1 + 0.6, (line.B - line.A).Length, 6);
        var texts = Texts(r);
        // The default window, 1200 × 1500 at 90: its wall opening over and under, its sill at the interior end.
        var w = texts.Single(t => t.Text == "120");
        var h = texts.Single(t => t.Text == "150");
        var sill = texts.Single(t => t.Text == "90");
        Assert.True(w.At.X < 4 && h.At.X > 4);
        var outY = Math.Sign(texts.Single(t => t.Text == TagText.Mark(f.Model, f.Model.Get(window)!)).At.Y - c.Y);
        var interiorEnd = outY > 0 ? Math.Min(line.A.Y, line.B.Y) : Math.Max(line.A.Y, line.B.Y);
        Assert.Equal(c.Y - outY * (half + 0.1), interiorEnd, 6);
        Assert.True(Math.Sign(sill.At.Y - c.Y) == -outY && Math.Abs(sill.At.Y - c.Y) > half + 0.1, $"{sill.At}");
        Assert.Equal(0, sill.Angle, 6);
    }

    [Fact]
    public void Its_texts_read_the_same_in_a_wall_along_y_and_its_sizes_are_the_types()
    {
        // A wall up the drawing: the line lies along x, its texts along it (0°), the width over (higher) the height; the mark along the wall (90°).
        var (f, wall, door, _, plan) = Project(new Vec2(0, 6));
        var c = Faces(f, wall, 1.5).Centre;
        var texts = Texts(Render(f, plan));
        var width = texts.Single(t => t.Text == "80");
        var height = texts.Single(t => t.Text == "203");
        Assert.Equal(0, width.Angle, 6);
        Assert.Equal(0, height.Angle, 6);
        Assert.True(width.At.Y > height.At.Y, $"{width.At} {height.At}");
        Assert.Equal(90, texts.Single(t => t.Text == TagText.Mark(f.Model, f.Model.Get(door)!)).Angle, 6);

        // The type's line 80 cm, its bubble 40 cm, its origin 5 cm out: the line from 0.05 to 0.85, the ring 0.2 round 1.05.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door-pl", ["lineLength"] = 0.8, ["bubbleSize"] = 0.4, ["lineOffset"] = 0.05 } });
        var segs = TagLines(Render(f, plan)).Where(s => Math.Abs((s.A.Y + s.B.Y) / 2 - 1.5) < 0.5).ToList();
        var line = segs.OrderByDescending(s => (s.B - s.A).Length).First();
        Assert.Equal(0.8, (line.B - line.A).Length, 6);
        var near = Math.Abs(line.A.X - c.X) < Math.Abs(line.B.X - c.X) ? line.A : line.B;
        Assert.Equal(0.05, Math.Abs(near.X - c.X), 6);
        var sign = Math.Sign(near.X - c.X);
        Assert.All(segs.Where(s => s != line).SelectMany(s => new[] { s.A, s.B }), p => Assert.Equal(0.2, (p - new Vec2(c.X + sign * 1.05, 1.5)).Length, 2));
    }

    [Fact]
    public void A_czech_door_marker_writes_the_clear_size_in_millimetres()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("standards.addTagPreset", new JsonObject { ["preset"] = "door-cz" });
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan, ["doorType"] = "door-cz" });
        // The default door: 900 − 2·50 wide, 2100 − 50 − 20 high inside its frame.
        Assert.Contains(Texts(Render(f, plan)), t => t.Text == "800/2030");
    }
}
