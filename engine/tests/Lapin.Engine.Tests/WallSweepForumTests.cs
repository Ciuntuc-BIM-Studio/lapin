using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 268 ("Wall sweep issues"): several sweeps changed at once; a coping's return at a free end; a sweep ending
/// against another wall; a lower wall ending on a taller one cutting its plaster only up to its own top; a wall type's sweeps
/// kept on the type when it is saved with its name, and a project whose type lost them given them back.
/// </summary>
public class WallSweepForumTests
{
    static Fixture Full() => new(new ArchitectureModule(), new Lapin.Structure.StructureModule());

    static JsonObject Rect(double depth, double height, params (string Key, JsonNode? Value)[] more)
    {
        var o = new JsonObject { ["catalog"] = "rectangle", ["params"] = new JsonObject { ["depth"] = depth, ["height"] = height } };
        foreach (var (k, v) in more) o[k] = v;
        return o;
    }

    static string Sweep(Fixture f, string wall, JsonObject args)
    {
        args["wallId"] = wall;
        return (string)f.Run("architecture.addWallSweep", args)!["id"]!;
    }

    static List<Entity> SweepsOn(Fixture f, string wall) => [.. f.Model.RelatedOf(wall, IfcClass.RelVoidsElement).Where(x => x.Class == WallSweeps.Class)];
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    // ---------------------------------------------------------------- point 1

    [Fact]
    public void Several_sweeps_selected_all_take_a_change()
    {
        var f = Full();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        var ids = ((JsonArray)f.Run("architecture.addWallSweep", Rect(0.05, 0.1, ("wallIds", new JsonArray(a, b)), ("side", "left"), ("height", 0.5)))!["ids"]!)
            .Select(x => (string)x!).ToList();
        // The inspector's fields: one element.update for the selection.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["attrs"] = new JsonObject { ["offset"] = 0.02, ["flip"] = true } });
        Assert.All(ids, id => Assert.Equal(0.02, f.Model.Get(id)!.Num("offset")));
        Assert.All(ids, id => Assert.True(f.Model.Get(id)!.Bool("flip")));
        // Its own section's: the profile, the material, the face.
        f.Run("architecture.updateWallSweep", new JsonObject
        {
            ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["catalog"] = "halfRound", ["params"] = new JsonObject { ["radius"] = 0.04 },
            ["material"] = "Stone", ["side"] = "flip",
        });
        Assert.All(ids, id =>
        {
            var s = f.Model.Get(id)!;
            Assert.Equal("right", s.Str("side"));
            Assert.Equal("Stone", f.Model.Get(s.Str("material")!)!.Name);
            Assert.Equal(0.04, Profiles.Of(f.Model, s)!.Bounds.U1, 9);
        });
    }

    // ---------------------------------------------------------------- point 2

    [Fact]
    public void A_coping_over_the_wall_s_top_ends_square_at_a_free_end()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 5, 0, f.Type("Core 20", ("Brick", 0.2, true)), height: 1.0);
        // 30 cm wide over a 20 cm wall: 5 cm out of the right face, 25 cm into the wall (over its top), returns as by default.
        var id = Sweep(f, wall, Rect(0.3, 0.05, ("side", "right"), ("fromTop", true), ("height", 0.0), ("offset", -0.25)));
        var box = f.Bounds(id);
        Assert.Equal(0, box.Min.X, 9);
        Assert.Equal(5, box.Max.X, 9);
        Assert.Equal(-0.15, box.Min.Y, 9);
        Assert.Equal(0.15, box.Max.Y, 9);
        Assert.Equal(5 * 0.3 * 0.05, Volume(f, id), 9);
        // A sweep standing out of the face still returns there.
        var band = Sweep(f, wall, Rect(0.05, 0.1, ("side", "right"), ("height", 0.5)));
        Assert.Equal(5.05, f.Bounds(band).Max.X, 9);
    }

    [Fact]
    public void A_sweep_ending_against_another_wall_does_not_return_into_it()
    {
        var f = Full();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        f.Wall(0, 0, 6, 0, type);
        // A parapet drawn to the face of the tall wall without a join (another storey's wall, or one left unjoined).
        var low = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 2, ["y1"] = 0.1, ["x2"] = 2, ["y2"] = 3, ["height"] = 1.0, ["typeId"] = type, ["join"] = false })!["id"]!;
        Assert.Empty(Joins.Of(f.Model, low));
        var id = Sweep(f, low, Rect(0.05, 0.1, ("side", "left"), ("height", 0.5)));
        var box = f.Bounds(id);
        // Square against the tall wall's face; returned round the parapet's free end.
        Assert.Equal(0.1, box.Min.Y, 9);
        Assert.Equal(3.05, box.Max.Y, 9);
    }

    // ---------------------------------------------------------------- point 3

    [Fact]
    public void A_lower_wall_ending_on_a_taller_one_cuts_its_finish_only_up_to_its_own_top()
    {
        var f = Full();
        var type = f.Type("Plastered", ("Plaster", 0.02, false), ("Brick", 0.2, true), ("Plaster", 0.02, false));
        var tall = f.Wall(0, 0, 6, 0, type);
        var low = f.Wall(2, 0, 2, 3, type, height: 1.0);
        Assert.Equal("T", Joins.Of(f.Model, low).Single().Attrs?["kind"]?.ToString());
        var height = Walls.HeightOf(f.Model, f.Model.Get(tall)!);
        Assert.True(height > 2);
        // The plaster on the parapet's side: one body, with the parapet's core (20 cm) let into it up to 1 m only.
        var plaster = Assert.Single(f.Session.Geometry(tall)!.Parts, p => p.Key == "layer:2");
        var b = Measure.Bounds(plaster.Solid)!.Value;
        Assert.Equal(0, b.Min.X, 9);
        Assert.Equal(6, b.Max.X, 9);
        Assert.Equal(height, b.Max.Z, 9);
        Assert.Equal(6 * 0.02 * height - 0.2 * 0.02 * 1.0, Measure.Volume(plaster.Solid), 9);
        // Over the parapet the plaster is whole: nothing of it is missing at x 2, 1.5 m up.
        Assert.Contains(plaster.Solid.Vertices, v => Math.Abs(v.Z - 1.0) < 1e-9 && Math.Abs(v.X - 1.9) < 1e-9);
        // Walls of the same height keep the plaster cut the whole way, as before.
        f.Run("element.update", new JsonObject { ["id"] = low, ["attrs"] = new JsonObject { ["height"] = height } });
        Assert.Equal(2, f.Session.Geometry(tall)!.Parts.Count(p => p.Key == "layer:2"));
    }

    // ---------------------------------------------------------------- point 4

    [Fact]
    public void A_wall_type_saved_with_its_name_keeps_its_sweeps_and_takes_them_off_its_walls()
    {
        var f = Full();
        var type = f.Type("Plastered", ("Plaster", 0.02, false), ("Brick", 0.2, true), ("Plaster", 0.02, false));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        // As the wall types dialog saves: its name, its layers and its sweeps together.
        JsonObject Save(params JsonObject[] sweeps) => new()
        {
            ["id"] = type, ["name"] = "Plastered",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.02 }, new JsonObject { ["material"] = "Brick", ["thickness"] = 0.2, ["core"] = true }, new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.02 }),
            ["sweeps"] = new JsonArray([.. sweeps.Select(x => (JsonNode)x)]),
        };
        f.Run("architecture.updateWallType", Save(Rect(0.05, 0.1, ("height", 0.3))));
        Assert.Single(WallSweepTypes.Defs(f.Model.Get(type)));
        Assert.Single(SweepsOn(f, a));
        Assert.Single(SweepsOn(f, b));
        // Taken out of the type: gone from every wall at once.
        f.Run("architecture.updateWallType", Save());
        Assert.Empty(WallSweepTypes.Defs(f.Model.Get(type)));
        Assert.Empty(SweepsOn(f, a));
        Assert.Empty(SweepsOn(f, b));
    }

    [Fact]
    public void Type_sweeps_a_wall_switches_off_or_on_go_for_several_walls_at_once()
    {
        var f = Full();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["sweeps"] = new JsonArray(Rect(0.05, 0.1, ("height", 0.3))) });
        var ids = new JsonArray(SweepsOn(f, a).Single().Id, SweepsOn(f, b).Single().Id);
        f.Run("architecture.updateWallSweep", new JsonObject { ["ids"] = ids, ["off"] = true });
        Assert.Empty(SweepsOn(f, a));
        Assert.Empty(SweepsOn(f, b));
        f.Run("architecture.restoreWallSweeps", new JsonObject { ["ids"] = new JsonArray(a, b) });
        Assert.Single(SweepsOn(f, a));
        Assert.Single(SweepsOn(f, b));
    }

    [Fact]
    public void A_project_whose_wall_type_lost_its_sweeps_gets_them_back_when_it_opens()
    {
        var f = Full();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["sweeps"] = new JsonArray(Rect(0.05, 0.1, ("height", 0.3), ("offset", 0.01), ("flip", true))) });
        var def = (JsonObject)WallSweepTypes.Defs(f.Model.Get(type)).Single().DeepClone();
        // b flipped (its type face on its left), a's sweep its own in height.
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(b) });
        f.Run("element.update", new JsonObject { ["id"] = SweepsOn(f, a).Single().Id, ["attrs"] = new JsonObject { ["height"] = 0.8 } });
        // As a build before the fix saved it: the walls with their type's sweeps, the type without them.
        var lost = f.Model.Entities.Select(e =>
        {
            if (e.Id != type) return e;
            var attrs = e.CloneAttrs();
            attrs.Remove(WallSweepTypes.Key);
            return e.With(attrs: attrs);
        }).ToList();
        f.Session.Load(lost, f.Model.Relations.ToList(), null);
        var back = WallSweepTypes.Defs(f.Model.Get(type)).Single();
        Assert.True(JsonNode.DeepEquals(def, back), $"{def.ToJsonString()} ≠ {back.ToJsonString()}");
        // So the type's dialog lists it again, and taking it out there takes it off both walls.
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["name"] = "Core 20", ["sweeps"] = new JsonArray() });
        Assert.Empty(SweepsOn(f, a));
        Assert.Empty(SweepsOn(f, b));
    }
}
