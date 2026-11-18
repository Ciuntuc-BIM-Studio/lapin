using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A wall's reference line kept with it (forum topic 159, GitHub #63, decision 685): the axis stays on the core's centre, and a
/// wall may also keep the line it is placed, moved and grown by: a face (of its core, its finishes, a layer) or a signed offset
/// from the core's centre. Absent, it is the core's centre and nothing changes. The wall here: 100 mm of insulation outside
/// (its first layer, the right as drawn), a 250 mm core and 15 mm of plaster inside; drawn along +x, left of the axis is +y, so
/// across the axis the insulation runs −0.225 … −0.125, the core −0.125 … 0.125, the plaster 0.125 … 0.140.
/// </summary>
public class WallReferenceLineTests
{
    static Vec2 P(JsonNode? n) => new((double)n![0]!, (double)n[1]!);

    static string ThreeLayers(Fixture f) => f.Type("Insulated 365", ("Insulation", 0.1, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));

    static string Draw(Fixture f, string type, JsonNode? reference, bool remember, double x1 = 0, double y1 = 0, double x2 = 10, double y2 = 0, JsonObject? more = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
        if (reference != null) a["reference"] = reference;
        if (remember) a["rememberReference"] = true;
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addWall", a)!["id"]!;
    }

    static double AxisY(Fixture f, string id) => P(f.Model.Get(id)!.Attrs["start"]).Y;

    [Theory]
    [InlineData("coreCentre", 0)]
    [InlineData("coreExterior", -0.125)]
    [InlineData("coreInterior", 0.125)]
    [InlineData("exteriorFace", -0.225)]
    [InlineData("interiorFace", 0.140)]
    public void Each_face_lies_where_the_layers_put_it(string kind, double left)
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), null, false);
        var bands = Walls.Section(f.Model, f.Model.Get(id)!);
        Assert.Equal(left, WallReference.LeftOf(bands, new WallReference.Line(kind)), 9);
    }

    [Fact]
    public void A_layer_face_is_counted_from_the_exterior_and_an_offset_is_its_own_number()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), null, false);
        var bands = Walls.Section(f.Model, f.Model.Get(id)!);
        // Faces 0 … 3 from the exterior: the insulation's outer face, insulation | core, core | plaster, the plaster's inner face.
        double[] faces = [-0.225, -0.125, 0.125, 0.140];
        for (var i = 0; i < faces.Length; i++) Assert.Equal(faces[i], WallReference.LeftOf(bands, new WallReference.Line("layer", Layer: i)), 9);
        Assert.Throws<CommandException>(() => WallReference.LeftOf(bands, new WallReference.Line("layer", Layer: 4)));
        Assert.Equal(0.3, WallReference.LeftOf(bands, new WallReference.Line("offset", 0.3)), 9);
        Assert.Equal(-0.15, WallReference.LeftOf(bands, new WallReference.Line("offset", -0.15)), 9);
    }

    [Fact]
    public void Drawn_on_the_exterior_face_without_remembering_it_the_wall_keeps_no_reference_as_before()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), "exterior", false);
        // The face clicked at y = 0, the axis 0.225 left of it: today's behaviour, nothing stored.
        Assert.Equal(0.225, AxisY(f, id), 9);
        Assert.Null(f.Model.Get(id)!.Attrs[WallReference.Key]);
        Assert.Null(WallReference.LineOf(f.Model, f.Model.Get(id)!));
    }

    [Fact]
    public void Drawn_on_the_exterior_face_and_remembered_the_wall_keeps_that_line()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), "exteriorFace", true);
        var wall = f.Model.Get(id)!;
        Assert.Equal(0.225, AxisY(f, id), 9);
        Assert.Equal("exteriorFace", (string?)wall.Attrs[WallReference.Key]!["kind"]);
        var line = WallReference.LineOf(f.Model, wall)!.Value;
        Assert.Equal(0, line.Start.Y, 9);
        Assert.Equal(0, line.End.Y, 9);
        Assert.Equal(0, line.Start.X, 9);
        Assert.Equal(10, line.End.X, 9);
    }

    [Fact]
    public void Drawn_by_an_offset_the_axis_lies_that_far_right_of_the_line_drawn()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), new JsonObject { ["kind"] = "offset", ["offset"] = 0.15 }, true);
        // The line drawn is 150 mm left of the core's centre: the axis at y = −0.15.
        Assert.Equal(-0.15, AxisY(f, id), 9);
        Assert.Equal(0, WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value.Start.Y, 9);
    }

    [Fact]
    public void Setting_the_reference_on_an_existing_wall_moves_the_line_not_the_wall_in_one_undo_step()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), null, false);
        f.Run("architecture.setWallReference", new JsonObject { ["ids"] = new JsonArray(id), ["kind"] = "offset", ["offset"] = 0.3 });
        Assert.Equal(0, AxisY(f, id), 9);
        Assert.Equal(0.3, WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value.Start.Y, 9);
        f.Run("architecture.setWallReference", new JsonObject { ["ids"] = new JsonArray(id), ["kind"] = "layer", ["layer"] = 1 });
        Assert.Equal(-0.125, WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value.Start.Y, 9);
        f.Session.Undo();
        Assert.Equal(0.3, WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value.Start.Y, 9);
        f.Session.Undo();
        Assert.Null(f.Model.Get(id)!.Attrs[WallReference.Key]);
        // Back to the core's centre: nothing kept.
        f.Run("architecture.setWallReference", new JsonObject { ["ids"] = new JsonArray(id), ["kind"] = "exteriorFace" });
        f.Run("architecture.setWallReference", new JsonObject { ["ids"] = new JsonArray(id), ["kind"] = "coreCentre" });
        Assert.Null(f.Model.Get(id)!.Attrs[WallReference.Key]);
        Assert.Equal(0, AxisY(f, id), 9);
        Assert.Throws<CommandException>(() => f.Run("architecture.setWallReference", new JsonObject { ["ids"] = new JsonArray(id), ["kind"] = "middle" }));
        Assert.Throws<CommandException>(() => f.Run("architecture.setWallReference", new JsonObject { ["ids"] = new JsonArray(id), ["kind"] = "layer", ["layer"] = 7 }));
    }

    [Fact]
    public void A_thicker_core_grows_the_wall_round_its_reference_line()
    {
        var f = new Fixture();
        var type = ThreeLayers(f);
        var kept = Draw(f, type, "exteriorFace", true);
        var plain = Draw(f, type, "exterior", false, 0, 5, 10, 5);
        // 365 → 465 mm: the core 250 → 350. The exterior face is then 0.175 + 0.1 = 0.275 right of the axis.
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(kept, plain), ["thickness"] = 0.465 });
        Assert.Equal(0.275, AxisY(f, kept), 9);
        Assert.Equal(0, WallReference.LineOf(f.Model, f.Model.Get(kept)!)!.Value.Start.Y, 9);
        Assert.Equal(-0.275, Walls.Section(f.Model, f.Model.Get(kept)!).Min(b => b.From), 9);
        // A wall that keeps no reference grows round its core's centre, as before.
        Assert.Equal(5.225, AxisY(f, plain), 9);
    }

    [Fact]
    public void Another_type_keeps_the_interior_face_where_it_was()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), "interiorFace", true);
        Assert.Equal(-0.140, AxisY(f, id), 9);
        var thin = f.Type("Brick 115", ("Brick", 0.115, true));
        f.Run("architecture.setWallType", new JsonObject { ["ids"] = new JsonArray(id), ["typeId"] = thin });
        // The new interior face 0.0575 left of the axis, still at y = 0.
        Assert.Equal(-0.0575, AxisY(f, id), 9);
        Assert.Equal(0, WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value.Start.Y, 9);
    }

    [Fact]
    public void An_offset_reference_does_not_move_the_wall_when_its_layers_change()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), null, false);
        f.Run("architecture.setWallReference", new JsonObject { ["ids"] = new JsonArray(id), ["kind"] = "offset", ["offset"] = 0.3 });
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(id), ["thickness"] = 0.465 });
        Assert.Equal(0, AxisY(f, id), 9);
    }

    [Fact]
    public void A_flipped_wall_turns_round_its_reference_line()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), "exteriorFace", true);
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(id) });
        // The exterior goes left: 0.225 left of the axis, the axis 0.225 right of the face, still at y = 0.
        Assert.Equal(-0.225, AxisY(f, id), 9);
        Assert.Equal(0, WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value.Start.Y, 9);
        // A wall that keeps no reference flips round its axis, as before.
        var plain = Draw(f, ThreeLayers(f), null, false, 0, 5, 10, 5);
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(plain) });
        Assert.Equal(5, AxisY(f, plain), 9);
    }

    [Fact]
    public void A_curved_wall_drawn_on_its_exterior_face_has_a_concentric_axis()
    {
        var f = new Fixture();
        // A half circle of radius 5 round the origin, anticlockwise from (5, 0) through (0, 5): the left, towards the centre, is the
        // inside, so the exterior (the right) is outside and the axis is 0.225 in from the arc drawn.
        var id = Draw(f, ThreeLayers(f), "exteriorFace", true, 5, 0, -5, 0, new JsonObject { ["x3"] = 0, ["y3"] = 5 });
        var c = PathCurve.Of(f.Model.Get(id)!);
        Assert.True(c.IsArc);
        Assert.Equal(4.775, c.Radius, 9);
        Assert.Equal(0, c.Centre.X, 9);
        Assert.Equal(0, c.Centre.Y, 9);
        var line = WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value;
        Assert.Equal(5, line.Radius, 9);
        Assert.Equal(5, line.Start.X, 9);
        // Not remembered, an arc is drawn on the face too now (#63), its axis as far in.
        var plain = Draw(f, ThreeLayers(f), "interior", false, 5, 0, -5, 0, new JsonObject { ["x3"] = 0, ["y3"] = 5 });
        Assert.Equal(5.14, PathCurve.Of(f.Model.Get(plain)!).Radius, 9);
        Assert.Null(f.Model.Get(plain)!.Attrs[WallReference.Key]);
    }

    [Fact]
    public void A_curved_wall_keeps_its_reference_radius_when_its_core_grows()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), "exteriorFace", true, 5, 0, -5, 0, new JsonObject { ["x3"] = 0, ["y3"] = 5 });
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(id), ["thickness"] = 0.465 });
        Assert.Equal(4.725, PathCurve.Of(f.Model.Get(id)!).Radius, 9);
        Assert.Equal(5, WallReference.LineOf(f.Model, f.Model.Get(id)!)!.Value.Radius, 9);
    }

    [Fact]
    public void Two_walls_joined_on_their_exterior_faces_stay_joined_when_both_grow()
    {
        var f = new Fixture();
        var type = ThreeLayers(f);
        var south = Draw(f, type, "exteriorFace", true, 0, 0, 10, 0);
        var east = Draw(f, type, "exteriorFace", true, 10, 0, 10, 8);
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(south, east), ["thickness"] = 0.465 });
        var (s, e) = (f.Model.Get(south)!, f.Model.Get(east)!);
        // Each axis 0.275 in from its face; the corner where the axes meet, (9.725, 0.275).
        Assert.Equal(0.275, P(s.Attrs["start"]).Y, 9);
        Assert.Equal(9.725, P(e.Attrs["end"]).X, 9);
        Assert.Equal(0, (P(s.Attrs["end"]) - P(e.Attrs["start"])).Length, 9);
        Assert.Equal(9.725, P(s.Attrs["end"]).X, 9);
        Assert.Equal(0.275, P(s.Attrs["end"]).Y, 9);
        Assert.Single(Joins.Of(f.Model, south));
    }

    [Fact]
    public void An_old_wall_without_the_attribute_is_unchanged()
    {
        var f = new Fixture();
        var id = f.Wall(0, 0, 10, 0);
        var before = f.Model.Get(id)!.Attrs.ToJsonString();
        f.Run("architecture.setWallThickness", new JsonObject { ["ids"] = new JsonArray(id), ["thickness"] = 0.4 });
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(id) });
        var w = f.Model.Get(id)!;
        Assert.Null(w.Attrs[WallReference.Key]);
        Assert.Null(WallReference.LineOf(f.Model, w));
        Assert.Equal(P(JsonNode.Parse(before)!["start"]), P(w.Attrs["start"]));
        Assert.Equal(P(JsonNode.Parse(before)!["end"]), P(w.Attrs["end"]));
    }

    [Fact]
    public void The_interface_is_told_the_reference_line()
    {
        var f = new Fixture();
        var id = Draw(f, ThreeLayers(f), "exteriorFace", true);
        var props = new JsonObject();
        new WallReference.Props().Add(f.Model.Get(id)!, f.Model, props);
        var r = props["referenceAxis"]!;
        Assert.Equal(0, (double)r["start"]![1]!, 9);
        Assert.Equal(-0.225, (double)r["left"]!, 9);
        var none = new JsonObject();
        new WallReference.Props().Add(f.Model.Get(f.Wall(0, 5, 10, 5))!, f.Model, none);
        Assert.Null(none["referenceAxis"]);
    }
}
