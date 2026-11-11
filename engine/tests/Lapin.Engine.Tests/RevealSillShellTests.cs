using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A window with an outside sill and a reveal in a material of its own leaves its wall a closed solid, with or without
/// finish layers: where the reveal's and the sill's cutters meet the opening's sill on the core's face, the boolean library
/// leaves a triangle with its three corners on one line, which joined no face and left its three edges open.
/// </summary>
public class RevealSillShellTests
{
    static void AllClosed(Fixture f, string id)
    {
        foreach (var p in f.Session.Geometry(id)!.Parts)
            Assert.True(Topology.IsManifold(p.Solid), $"{p.Key ?? p.Name}: {string.Join("; ", Topology.Validate(p.Solid).Select(d => d.Message))}");
    }

    /// <summary>A 375 mm wall (or a bare 350 mm core) running north, a tall window in it (0.75 × 2.25 m, sill 0.15), its frame on the wall's middle, with a stone reveal and a sloped outside sill.</summary>
    static (Fixture F, string Wall, string Window) Scene(string finishes, bool reveal, bool sill)
    {
        var f = new Fixture();
        f.Run("material.create", new JsonObject { ["name"] = "Stone" });
        var type = finishes switch
        {
            "none" => f.Type("Bare", ("Brick", 0.35, true)),
            "outside" => f.Type("Faced", ("Render", 0.025, false), ("Brick", 0.35, true)),
            _ => f.Type("Rendered", ("Render", 0.025, false), ("Brick", 0.35, true), ("Plaster", 0.015, false)),
        };
        var wall = f.Wall(22.475, 4.8375, 22.475, 6.825, type);
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 2.8 } });
        var d = JoinerySpec.DefaultWindow("Tall");
        var spec = d with
        {
            Width = 0.75, Height = 2.25, SillHeight = 0.15, Frame = d.Frame with { Offset = 0 },
            Details = new JoineryDetails(reveal ? new JoineryReveal("material", "Stone", 0.02) : null, SillOutside: sill ? new JoinerySill(true, 0.04, 0.02, 0.03, 5, "Stone") : null),
        };
        var typeId = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 1.262, ["typeId"] = typeId })!["id"]!;
        return (f, wall, window);
    }

    [Theory]
    [InlineData("none", true, true)]
    [InlineData("outside", true, true)]
    [InlineData("both", true, true)]
    [InlineData("none", false, true)]
    [InlineData("none", true, false)]
    [InlineData("both", false, true)]
    public void A_window_with_an_outside_sill_and_a_reveal_of_its_own_material_leaves_its_wall_closed(string finishes, bool reveal, bool sill)
    {
        var (f, wall, window) = Scene(finishes, reveal, sill);
        var parts = f.Session.Geometry(window)!.Parts;
        if (reveal) Assert.Contains(parts, p => p.Key == "reveal:outside" && f.Model.Get(p.Material!)!.Name == "Stone");
        if (sill) Assert.Contains(parts, p => p.Key == "sill:outside");
        Assert.Empty(f.Session.Geometry(wall)!.Diagnostics ?? []);
        AllClosed(f, wall);
        AllClosed(f, window);
    }

    [Fact]
    public void The_wall_still_gives_way_to_the_reveal_and_the_sill()
    {
        // Closing the shell keeps the cut: the wall loses, besides the opening, at least the room of the reveal's four jambs
        // (each 20 mm wide, 140 mm from the frame's face to the wall's, the opening's 2.25 m high) and of the outside sill under the frame.
        var (plain, plainWall, _) = Scene("none", false, false);
        var (dressed, dressedWall, window) = Scene("none", true, true);
        static double Wall(Fixture f, string wall) => f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));
        var jambs = 4 * 0.02 * 0.14 * 2.25;
        var sill = (0.75 + 2 * 0.03) * 0.02 * 0.14;
        Assert.True(Wall(plain, plainWall) - Wall(dressed, dressedWall) > jambs + sill - 1e-9, $"{Wall(plain, plainWall) - Wall(dressed, dressedWall)} m³");
        Assert.True(Topology.IsManifold(dressed.Session.Geometry(window)!.Parts.Single(p => p.Key == "sill:outside").Solid));
    }
}
