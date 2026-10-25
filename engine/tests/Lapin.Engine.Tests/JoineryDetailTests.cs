using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The details around a door or window (docs/plan-views-and-joinery.md §3,
/// pending decision 127): the reveal's finish from the wall's face layers, the
/// sills, the surround, the threshold; the wall cut to make room; measured.
/// </summary>
public class JoineryDetailTests
{
    /// <summary>Layers run from the wall's right face (the exterior) to its left: render outside, plaster inside.</summary>
    static string Rendered(Fixture f) => f.Type("Rendered", ("Render", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
    static GeometryPart? Part(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.FirstOrDefault(p => p.Key == key);
    static (Vec3 Min, Vec3 Max) Bounds(GeometryPart p) => Measure.Bounds(p.Solid)!.Value;

    /// <summary>
    /// The hole a wall part has at height z, from its vertices there, either
    /// side of the opening's middle (x = 3): the narrowest and the widest gap
    /// (a part lined by a skin on one side only shows both).
    /// </summary>
    static (double Narrowest, double Widest) Hole(Fixture f, string wall, string partName, double z)
    {
        var part = f.Session.Geometry(wall)!.Parts.Single(p => p.Name == partName);
        var xs = part.Solid.Vertices.Where(v => Math.Abs(v.Z - z) < 1e-6 && Math.Abs(v.X - 3) < 1).Select(v => Math.Round(v.X, 6)).Distinct().ToList();
        var left = xs.Where(x => x < 3).ToList();
        var right = xs.Where(x => x > 3).ToList();
        return (right.Min() - left.Max(), right.Max() - left.Min());
    }

    static string Window(Fixture f, string wall, JoinerySpec? spec = null)
    {
        var args = new JsonObject { ["wallId"] = wall, ["along"] = 3 };
        if (spec != null) args["typeId"] = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (string)f.Run("architecture.addWindow", args)!["id"]!;
    }

    [Fact]
    public void The_reveal_takes_the_walls_face_layers_each_side_its_own_and_the_wall_is_cut_wider_only_there()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0, Rendered(f));
        var window = Window(f, wall);
        var spec = Openings.SpecOf(f.Model, window)!;
        var outside = Part(f, window, "reveal:outside")!;
        var inside = Part(f, window, "reveal:inside")!;
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        // The exterior is the wall's right, its first layer (render); the interior the last (plaster).
        Assert.Equal(bands[0].Material, outside.Material);
        Assert.Equal(bands[^1].Material, inside.Material);
        Assert.Equal("Render", f.Model.Get(bands[0].Material!)!.Name);
        // The skins stand beside the frame, as thick as their layer, up over the head.
        var ob = Bounds(outside);
        Assert.Equal(spec.Width + 2 * 0.02, ob.Max.X - ob.Min.X, 6);
        Assert.Equal(spec.Height + 0.02, ob.Max.Z - ob.Min.Z, 6);
        var ib = Bounds(inside);
        Assert.Equal(spec.Width + 2 * 0.015, ib.Max.X - ib.Min.X, 6);
        // Each finish layer's hole is wider by its own skin; the core keeps the opening where the frame sits and is cut back where a skin lines it.
        var z = spec.SillHeight; // the hole's bottom corners: the jambs have no vertices between their corners
        // (the booleans leave vertices where the plain opening's edges met the skin's, so the widest gap is the hole)
        Assert.Equal(spec.Width + 2 * 0.02, Hole(f, wall, "Render", z).Widest, 6);
        Assert.Equal(spec.Width + 2 * 0.015, Hole(f, wall, "Plaster", z).Widest, 6);
        var brick = Hole(f, wall, "Brick", z);
        Assert.Equal(spec.Width, brick.Narrowest, 6);
        Assert.Equal(spec.Width + 2 * 0.02, brick.Widest, 6);
    }

    [Fact]
    public void A_wall_of_one_layer_gives_no_reveal_and_the_mode_none_or_a_named_material_decide_otherwise()
    {
        var f = new Fixture();
        var plain = f.Wall(0, 0, 6, 0, f.Type("Block 30", ("Block", 0.3, true)));
        Assert.Null(Part(f, Window(f, plain), "reveal:outside"));
        var wall = f.Wall(0, 10, 6, 10, Rendered(f));
        f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultWindow("None") with { Details = new JoineryDetails(new JoineryReveal("none")) }).ToJson() });
        var none = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 1.5, ["typeId"] = f.Model.OfClass(IfcClass.WindowType).Single(t => t.Name == "None").Id })!["id"]!;
        Assert.Null(Part(f, none, "reveal:outside"));
        f.Run("material.create", new JsonObject { ["name"] = "Stone" });
        var stoneId = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultWindow("Stone-lined") with { Details = new JoineryDetails(new JoineryReveal("material", "Stone", 0.03)) }).ToJson() })!["id"]!;
        var stone = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4.5, ["typeId"] = stoneId })!["id"]!;
        var part = Part(f, stone, "reveal:outside")!;
        Assert.Equal("Stone", f.Model.Get(part.Material!)!.Name);
        Assert.Equal(1.2 + 2 * 0.03, Bounds(part).Max.X - Bounds(part).Min.X, 6);
        Assert.Equal(part.Material, Part(f, stone, "reveal:inside")!.Material);
    }

    [Fact]
    public void The_sills_the_surround_and_the_threshold_are_parts_with_their_materials_and_their_measures()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0, Rendered(f));
        f.Run("material.create", new JsonObject { ["name"] = "Granite" });
        var details = new JoineryDetails(
            SillInside: new JoinerySill(true, 0.04, 0.03, 0.02, 0, "Timber"),
            SillOutside: new JoinerySill(true, 0.05, 0.02, 0.03, 10, "Granite"),
            Surround: new JoinerySurround(true, 0.1, 0.02, true, "Render"));
        var window = Window(f, wall, JoinerySpec.DefaultWindow("Dressed") with { Details = details });
        var spec = Openings.SpecOf(f.Model, window)!;
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        var t = bands.Max(b => b.To) - bands.Min(b => b.From);
        var outside = Part(f, window, "sill:outside")!;
        Assert.Equal("Granite", f.Model.Get(outside.Material!)!.Name);
        var ob = Bounds(outside);
        // Past the jambs each side, out past the exterior face (the wall's right: its lowest Y), dropping with its slope.
        Assert.Equal(spec.Width + 2 * 0.03, ob.Max.X - ob.Min.X, 6);
        Assert.Equal(bands.Min(b => b.From) - 0.05, ob.Min.Y, 6);
        Assert.True(ob.Min.Z < spec.SillHeight - 0.02 - 1e-6);
        var inside = Part(f, window, "sill:inside")!;
        Assert.Null(inside.Material); // no such material in the project: the board keeps its colour
        Assert.Equal(bands.Max(b => b.To) + 0.04, Bounds(inside).Max.Y, 6);
        // No boards of the plain kind remain.
        Assert.DoesNotContain(f.Session.Geometry(window)!.Parts, p => p.Key == "board");
        var surround = Part(f, window, "surround")!;
        Assert.Equal(spec.Width + 2 * 0.1, Bounds(surround).Max.X - Bounds(surround).Min.X, 6);
        Assert.Equal(bands.Min(b => b.From) - 0.02, Bounds(surround).Min.Y, 6);
        var q = f.Session.Quantities(window).Single(s => s.Name == "Lapin_Details").Quantities.ToDictionary(x => x.Name, x => x.Value);
        Assert.Equal(spec.Width + 0.06, q["SillOutsideLength"], 6);
        Assert.Equal(spec.Width + 0.04, q["SillInsideLength"], 6);
        Assert.True(q["SurroundArea"] > 0 && q["RevealAreaOutside"] > 0);

        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1, ["typeId"] = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultDoor("Stepped") with { Details = new JoineryDetails(Threshold: new JoineryThreshold(true, 0.03, "Granite")) }).ToJson() })!["id"]! })!["id"]!;
        var threshold = Part(f, door, "threshold")!;
        Assert.Equal(t, Bounds(threshold).Max.Y - Bounds(threshold).Min.Y, 6);
        Assert.Equal(0.03, Bounds(threshold).Max.Z - Bounds(threshold).Min.Z, 6);
    }
}
