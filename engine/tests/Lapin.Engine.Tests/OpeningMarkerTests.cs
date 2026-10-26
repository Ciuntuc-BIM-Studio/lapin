using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Door and window markers in plan: a line across the wall, perpendicular to
/// its axis, through the opening's middle and past both faces; the mark at the
/// exterior end, width/height (and a window's sill height) at the interior
/// end; all of it from the drafting standards' tag types, which can be changed.
/// </summary>
public sealed class OpeningMarkerTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    /// <summary>A wall along X from (0, 0) to (6, 0) with a door at 1.5 m and a window at 4 m, and its storey's plan.</summary>
    static (Fixture F, string Wall, string Door, string Window, string Plan) Project()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (f, wall, door, window, plan);
    }

    static Views.Rendered Render(Fixture f, string view) => Views.Render(new Q(f), Views.Find(f.Model, view)!, print: true);

    static List<PenText> Texts(Views.Rendered r) => r.Pens.SelectMany(p => p.Texts).ToList();
    static List<(Vec2 A, Vec2 B)> TagLines(Views.Rendered r) => r.Pens.Where(p => p.Layer == "TAG").SelectMany(p => p.Segments).ToList();

    [Fact]
    public void Every_door_and_window_gets_a_marker_across_its_wall()
    {
        var (f, wall, door, window, plan) = Project();
        Assert.Equal(2, (int)f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan })!["tagged"]!);
        // Tagged once: again tags nothing.
        Assert.Equal(0, (int)f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan })!["tagged"]!);
        var r = Render(f, plan);
        var lines = TagLines(r);
        Assert.Equal(2, lines.Count);
        var thickness = f.Session.Geometry(wall)!.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).Max(b => b.Max.Y) - f.Session.Geometry(wall)!.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).Min(b => b.Min.Y);
        foreach (var (along, (a, b)) in new[] { 1.5, 4.0 }.Zip(lines.OrderBy(l => l.A.X)))
        {
            // Perpendicular to the wall (along Y), through the opening's middle, past both faces by 6 mm on paper (0.6 m at 1:100).
            Assert.Equal(along, a.X, 6);
            Assert.Equal(along, b.X, 6);
            Assert.Equal(thickness + 2 * 0.6, Math.Abs(b.Y - a.Y), 3);
        }
        // The door: its mark outside, width/height inside (cm); the window adds its sill.
        var texts = Texts(r);
        var doorMark = TagText.Mark(f.Model, f.Model.Get(door)!);
        var windowMark = TagText.Mark(f.Model, f.Model.Get(window)!);
        Assert.Contains(texts, t => t.Text == doorMark);
        Assert.Contains(texts, t => t.Text == windowMark);
        var p = Lapin.Architecture.Openings.Place(f.Model, Lapin.Architecture.Openings.OpeningOf(f.Model, window)!);
        Assert.Contains(texts, t => t.Text == $"{TagText.Length(p.Width, "cm")}/{TagText.Length(p.Height, "cm")}");
        Assert.Contains(texts, t => t.Text == $"sill {TagText.Length(p.Sill, "cm")}");
        // The texts run along the wall (0°) and stand on either side of it.
        var outer = texts.Single(t => t.Text == windowMark);
        var inner = texts.Single(t => t.Text.StartsWith("sill "));
        Assert.Equal(0, outer.Angle, 6);
        Assert.True(Math.Sign(outer.At.Y) != Math.Sign(inner.At.Y), $"{outer.At} {inner.At}");
    }

    [Fact]
    public void A_flipped_window_turns_its_marker_and_the_standards_change_what_it_says()
    {
        var (f, _, _, window, plan) = Project();
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan });
        var mark = TagText.Mark(f.Model, f.Model.Get(window)!);
        double MarkY() => Texts(Render(f, plan)).Single(t => t.Text == mark).At.Y;
        var before = MarkY();
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(window), ["axis"] = "y" });
        // The mark goes to the other face (the wall's axis is not its middle, so not by the same distance).
        var after = MarkY();
        Assert.True(before * after < 0, $"{before} → {after}");

        // The window marker's type, changed in the standards: the name outside, the size in mm inside, no sill, a shorter line.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "window", ["template"] = "{name}", ["inner"] = "{width}x{height}", ["unit"] = "mm", ["reach"] = 3 } });
        var texts = Texts(Render(f, plan));
        var p = Lapin.Architecture.Openings.Place(f.Model, Lapin.Architecture.Openings.OpeningOf(f.Model, window)!);
        Assert.Contains(texts, t => t.Text == f.Model.Get(window)!.Name);
        Assert.Contains(texts, t => t.Text == $"{p.Width * 1000:0}x{p.Height * 1000:0}");
        Assert.DoesNotContain(texts, t => t.Text.StartsWith("sill "));
    }

    [Fact]
    public void A_tag_placed_by_hand_on_a_door_is_its_marker_and_a_box_elsewhere()
    {
        var (f, wall, door, _, plan) = Project();
        var tag = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = plan, ["element"] = door, ["at"] = new JsonArray(1.5, 2) })!["id"]!;
        Assert.Equal("door", f.Model.Get(tag)!.Str("tagType"));
        Assert.Single(TagLines(Render(f, plan)));
        // A field asked for: the old box with its leader.
        f.Run("drafting.addTag", new JsonObject { ["viewId"] = plan, ["element"] = wall, ["field"] = "name", ["at"] = new JsonArray(3, 2) });
        Assert.Contains(Texts(Render(f, plan)), t => t.Text == f.Model.Get(wall)!.Name);
        // In a section the door's marker is a box.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(1.5, -2), ["end"] = new JsonArray(1.5, 2) })!["id"]!;
        f.Run("drafting.addTag", new JsonObject { ["viewId"] = section, ["element"] = door, ["at"] = new JsonArray(0, 3) });
        Assert.Contains(Texts(Render(f, section)), t => t.Text == TagText.Mark(f.Model, f.Model.Get(door)!));
        Assert.Throws<CommandException>(() => f.Run("drafting.addTag", new JsonObject { ["viewId"] = plan, ["element"] = door, ["tagType"] = "nope", ["at"] = new JsonArray(0, 0) }));
        Assert.Throws<CommandException>(() => f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = section }));
    }

    [Fact]
    public void A_project_starts_with_the_door_and_window_markers_and_reads_its_own()
    {
        Assert.Equal(["door", "window"], Standards.Read(null).TagTypes.Select(t => t.Id));
        var own = Standards.Read(new JsonObject { ["tagTypes"] = new JsonArray(new JsonObject { ["id"] = "w", ["template"] = "{mark}", ["shape"] = "opening", ["unit"] = "km", ["classes"] = "IfcWindow, IfcDoor" }) }).TagTypes.Single();
        Assert.Equal(("opening", "cm", 6.0), (own.Shape, own.Unit, own.Reach));
        Assert.Equal(["IfcWindow", "IfcDoor"], own.Classes);
        Assert.Equal("120", TagText.Length(1.2, "cm"));
        Assert.Equal("1200", TagText.Length(1.2, "mm"));
        Assert.Equal("1.20", TagText.Length(1.2, "m"));
        Assert.Equal("87.5", TagText.Length(0.875, "cm"));
    }
}
