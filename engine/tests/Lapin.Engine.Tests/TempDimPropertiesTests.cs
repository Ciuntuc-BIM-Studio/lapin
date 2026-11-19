using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 738 (a user: "in a composite wall we always consider dimensions from core face to core face"; Revit's Temporary
/// Dimension Properties): the centre of a wall's core, which <c>view.dimLines</c> gives beside its core faces and stands off the
/// axis when the core is not centred on it; the numbers a door's or window's centreline temporary dimension measures with; and
/// an angular temporary dimension made permanent, held on the two elements so that it follows them and, typed, turns the second.
/// </summary>
public class TempDimPropertiesTests
{
    static JsonObject Lines(Fixture f) => f.Session.Query("view.dimLines", new JsonObject { ["storeyId"] = f.Storey })!.AsObject();
    static JsonObject Of(JsonObject r, string id, string? part = null) =>
        r["lines"]!.AsArray().Select(x => x!.AsObject()).Single(l => (string?)l["id"] == id && (string?)l["part"] == part);
    static double[] Nums(JsonNode? n) => n!.AsArray().Select(x => Math.Round((double)x!, 6)).ToArray();
    static double[] Sorted(JsonNode? n) => [.. Nums(n).OrderBy(x => x)];
    static JsonArray J(double x, double y) => [x, y];

    [Fact]
    public void The_centre_of_core_is_the_middle_of_the_core_layers_off_the_axis_when_the_core_is_not_centred()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        // Plaster 20, brick 250 (the core), insulation 100: a native wall keeps its axis on its core (−0.125…0.125, the middle 0).
        var type = f.Type("Plastered brick", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var centred = f.Wall(0, 4, 6, 4, type);
        var offCentre = f.Wall(0, 0, 6, 0, type);
        // As an imported wall's layers may lie (its IfcMaterialLayerSetUsage's OffsetFromReferenceLine): the whole build-up 0.1 further left.
        f.Session.Edit("offset", e =>
        {
            var u = LayerSets.UsageOf(e.Model, offCentre)!;
            var a = u.CloneAttrs();
            a["offsetFromReferenceLine"] = (double)a["offsetFromReferenceLine"]! + 0.1;
            e.Replace(u.With(attrs: a));
        });
        var r = Lines(f);

        var o = Of(r, offCentre);
        Assert.Equal(new[] { -0.025, 0.225 }, Nums(o["core"]));
        Assert.Equal(0.1, (double)o["coreCentre"]!, 6);
        var c = Of(r, centred);
        Assert.Equal(new[] { -0.125, 0.125 }, Nums(c["core"]));
        Assert.Equal(0, (double)c["coreCentre"]!, 6);
        // Between the two, centre of core to centre of core: 4 − 0.1; axis to axis (Centrelines): 4.
        Assert.Equal(3.9, Nums(c["start"])[1] - (Nums(o["start"])[1] + (double)o["coreCentre"]!), 6);

        // A column's and a beam's too: the middle of the section, on its point and its axis here.
        var col = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.4, ["depth"] = 0.3 }, ["height"] = 3 })!["id"]!;
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = col, ["x"] = 2, ["y"] = 2 })!["id"]!;
        r = Lines(f);
        Assert.Equal(0, (double)Of(r, column, "x")["coreCentre"]!, 6);
        Assert.Equal(0, (double)Of(r, column, "y")["coreCentre"]!, 6);
    }

    [Fact]
    public void A_window_s_centreline_measures_from_its_centre_to_the_other_window_s_centre_and_to_the_meeting_wall_s_core_centre()
    {
        var f = new Fixture();
        var layered = f.Type("Plastered brick", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var host = f.Wall(0, 0, 8, 0, f.Type("Brick 300", ("Brick", 0.3, true)));
        var tee = f.Wall(6.5, 0, 6.5, 4, layered);         // a T on the host's left face, its left toward −x
        var w1 = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = host, ["along"] = 2 })!["id"]!;
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = host, ["along"] = 4.5 });
        var r = f.Session.Query("architecture.openingReferences", new JsonObject { ["id"] = w1 })!.AsObject();

        // Centrelines: the window's centre to the other's, 2.5, whatever their widths.
        var other = r["openings"]!.AsArray().Single()!;
        Assert.Equal(2.5, (double)other["along"]! - (double)r["opening"]!["along"]!, 6);
        // To the T's centre of core: the middle of its core faces along the host, on its axis at 6.5 (a native wall's core is centred).
        var t = r["walls"]!.AsArray().Single(w => (string?)w!["id"] == tee)!["left"]!;
        var core = Sorted(t["core"]);
        Assert.Equal(6.5, (double)t["axis"]!, 6);
        Assert.Equal(new[] { 6.375, 6.625 }, core);
        Assert.Equal(6.5, (core[0] + core[1]) / 2, 6);
        Assert.Equal(4.5, (core[0] + core[1]) / 2 - (double)r["opening"]!["along"]!, 6);
    }

    [Fact]
    public void An_angle_held_on_two_walls_follows_them_and_typed_turns_the_second_about_where_they_meet_in_one_step()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var t = f.Type("Brick 300", ("Brick", 0.3, true));
        var a = f.Wall(0, 0, 6, 0, t);
        // 60° from the first, from its start.
        var b = f.Wall(0, 0, 4 * Math.Cos(Math.PI / 3), 4 * Math.Sin(Math.PI / 3), t);
        var dim = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = plan, ["dimKind"] = "angular", ["c"] = J(0, 0), ["a"] = J(1, 0), ["b"] = J(0.5, 0.866), ["at"] = J(0.87, 0.5),
            ["holds"] = new JsonArray(new JsonObject { ["element"] = a, ["way"] = 1 }, new JsonObject { ["element"] = b, ["way"] = 1 }),
        })!["id"]!;
        JsonObject Info() => f.Session.Query("drafting.dimension", new JsonObject { ["id"] = dim })!.AsObject();
        Assert.Equal(60, (double)Info()["value"]!, 6);
        Assert.True((bool)Info()["driving"]!);

        // The second turned 15° about the corner (as a temporary dimension's typed angle does it): the dimension follows.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(b), ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 15 });
        Assert.Equal(75, (double)Info()["value"]!, 6);
        // Both moved together: the vertex goes with them, the angle stays.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(a, b), ["op"] = "move", ["by"] = J(2, 1) });
        Assert.Equal(75, (double)Info()["value"]!, 6);

        // Typed 90: the second wall turns about the vertex until it is square to the first; one undo step puts it back.
        var before = f.Model.Get(b)!.Attrs["end"]!.ToJsonString();
        f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = dim, ["value"] = 90 });
        Assert.Equal(90, (double)Info()["value"]!, 6);
        var end = Nums(f.Model.Get(b)!.Attrs["end"]);
        var start = Nums(f.Model.Get(b)!.Attrs["start"]);
        Assert.Equal(start[0], end[0], 6);
        f.Session.Undo();
        Assert.Equal(before, f.Model.Get(b)!.Attrs["end"]!.ToJsonString());
        Assert.Throws<CommandException>(() => f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = dim, ["value"] = 190 }));

        // Parallel elements have no angle; one gone leaves the dimension an orphan.
        var c = f.Wall(0, 3, 6, 3, t);
        Assert.Throws<CommandException>(() => f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = plan, ["dimKind"] = "angular", ["c"] = J(0, 0), ["a"] = J(1, 0), ["b"] = J(0, 1), ["at"] = J(1, 1),
            ["holds"] = new JsonArray(new JsonObject { ["element"] = a, ["way"] = 1 }, new JsonObject { ["element"] = c, ["way"] = 1 }),
        }));
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(b) });
        Assert.True((bool)Info()["orphan"]!);
    }
}
