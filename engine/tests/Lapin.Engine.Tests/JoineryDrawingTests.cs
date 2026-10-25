using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The joinery's drawings in the configurator, the plans and the elevations
/// (docs/plan-views-and-joinery.md §2, pending decision 126).
/// </summary>
public class JoineryDrawingTests
{
    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static JsonObject Q(Fixture f, string name, JsonObject args) => (JsonObject)f.Session.Query(name, args)!;

    /// <summary>The apex of the first opening triangle of a drawing (an open stroke of three points).</summary>
    static double ApexX(JsonObject drawing) => (double)drawing["strokes"]!.AsArray().First(s => s!["pts"]!.AsArray().Count == 3 && !(bool)s["closed"]!)!["pts"]![1]![0]!;

    static List<(string Layer, double X0, double Y0, double X1, double Y1, bool Dashed)> Segments(JsonObject d, string layer)
    {
        var o = new List<(string, double, double, double, double, bool)>();
        foreach (var p in d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == layer))
        {
            if (p["segments"] is not JsonArray a) continue;
            var dashed = p["dash"] is JsonArray { Count: > 0 };
            for (var i = 0; i + 3 < a.Count; i += 4) o.Add((layer, (double)a[i]!, (double)a[i + 1]!, (double)a[i + 2]!, (double)a[i + 3]!, dashed));
        }
        return o;
    }

    [Fact]
    public void The_preview_draws_the_elevation_the_plan_and_the_section_and_follows_the_projects_convention()
    {
        var f = Full();
        var spec = JoinerySpec.DefaultWindow().ToJson();
        var p = Q(f, "architecture.joineryPreview", new JsonObject { ["spec"] = spec.DeepClone() });
        Assert.Null((string?)p["problem"]);
        var drawings = (JsonObject)p["drawings"]!;
        foreach (var key in new[] { "elevation", "exterior", "plan", "section" }) Assert.NotEmpty(drawings[key]!["strokes"]!.AsArray());
        Assert.Equal("handle", (string?)p["symbols"]!["convention"]);
        var makers = ApexX((JsonObject)drawings["elevation"]!);
        // From outside the drawing is mirrored.
        Assert.Equal(-makers, ApexX((JsonObject)drawings["exterior"]!), 9);
        f.Run("architecture.joinerySymbols", new JsonObject { ["convention"] = "hinge", ["hardware"] = true });
        var din = Q(f, "architecture.joineryPreview", new JsonObject { ["spec"] = spec.DeepClone() });
        Assert.Equal("hinge", (string?)din["symbols"]!["convention"]);
        Assert.True((bool)din["symbols"]!["hardware"]!);
        Assert.NotEqual(makers, ApexX((JsonObject)din["drawings"]!["elevation"]!));
        Assert.Throws<CommandException>(() => f.Run("architecture.joinerySymbols", new JsonObject { ["convention"] = "sideways" }));
    }

    [Fact]
    public void A_door_and_a_window_draw_the_symbol_engines_plan_in_the_model()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        var dp = f.Session.Geometry(door)!.Plan!;
        var wp = f.Session.Geometry(window)!.Plan!;
        // The door: the frame cut, its leaf (heavy) and the arc of its swing (a polyline of many points).
        Assert.Contains(dp, i => i.Kind == "heavy" && i.Closed);
        Assert.Contains(dp, i => i.Kind == "symbol" && i.Points.Count > 8);
        // The window: the frame cut and the glass (thin, drawn as a symbol); the jambs across the wall stay.
        Assert.Contains(wp, i => i.Kind == "heavy" && i.Closed);
        Assert.True(wp.Count(i => i.Kind == "symbol") >= 3);
        Assert.Single(wp, i => i.Kind == "hit");
    }

    [Fact]
    public void An_elevation_marks_the_openings_whose_exterior_faces_it_dashed_and_leaves_the_hidden_ones()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        // The exterior is the wall's right: south. The south elevation (looking north) sees it; the north one does not.
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var north = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })!["id"]!;
        var marks = Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY");
        Assert.NotEmpty(marks);
        Assert.All(marks, s => Assert.True(s.Dashed));
        // They sit on the window: within its width and height in the drawing.
        var spec = Openings.SpecOf(f.Model, window)!;
        Assert.All(marks, s => Assert.True(s.Y0 >= spec.SillHeight - 1e-6 && s.Y1 <= spec.SillHeight + spec.Height + 1e-6, $"a mark at {s.Y0}..{s.Y1}"));
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = north }), "JOINERY"));
        // Switched off on the view.
        f.Run("element.update", new JsonObject { ["id"] = south, ["attrs"] = new JsonObject { ["openingSymbols"] = "hide" } });
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
        f.Run("element.update", new JsonObject { ["id"] = south, ["attrs"] = new JsonObject { ["openingSymbols"] = "show" } });
        Assert.NotEmpty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
        // A wall in front of the window hides its symbol.
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = -3, ["x2"] = 6, ["y2"] = -3, ["height"] = 4 });
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
    }

    [Fact]
    public void A_flipped_window_turns_its_exterior_to_the_other_elevation()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        f.Run("architecture.flipJoinery", new JsonObject { ["id"] = window, ["axis"] = "y" });
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var north = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })!["id"]!;
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
        Assert.NotEmpty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = north }), "JOINERY"));
    }
}
