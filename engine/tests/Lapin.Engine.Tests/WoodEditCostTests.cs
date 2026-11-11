using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #125: with timber-frame walls every edit of any wall built all of them again and sent them to the interface, and a
/// plan's separation lines were found by trying every cut line against every cut edge. A frame now reads only the walls near it
/// (<see cref="NearReads"/>), and still follows those; the separations are the same, found on a grid.
/// </summary>
public class WoodEditCostTests
{
    static (Fixture F, List<string> Walls) Framed(int rooms)
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var walls = new List<string>();
        for (var i = 0; i < rooms; i++)
        {
            var (x, y) = (i % 4 * 10.0, i / 4 * 8.0);
            foreach (var (x1, y1, x2, y2) in new[] { (x, y, x + 8, y), (x + 8, y, x + 8, y + 6), (x + 8, y + 6, x, y + 6), (x, y + 6, x, y) })
                walls.Add(AddWall(f, x1, y1, x2, y2));
        }
        f.Run("wood.setWallFrame", new JsonObject { ["id"] = Walls.TypeOf(f.Model, walls[0])!.Id, ["spec"] = FrameSpec.Nordic.ToJson() });
        return (f, walls);
    }

    static string AddWall(Fixture f, double x1, double y1, double x2, double y2) =>
        (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["joinStyle"] = "butt" })!["id"]!;

    static JsonObject AddArgs(Fixture f, double x1, double y1, double x2, double y2) =>
        new() { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["joinStyle"] = "butt" };

    static JsonObject Counts(Fixture f, string wall) => (JsonObject)((JsonObject)f.Session.Query("wood.wall", new JsonObject { ["id"] = wall })!)["counts"]!;

    static int Backers(Fixture f, string wall) => Counts(f, wall)["backer"] is { } n ? (int)n : 0;

    [Fact]
    public void A_wall_added_or_deleted_far_away_builds_no_framed_wall_again()
    {
        var (f, walls) = Framed(4);
        var add = f.RunDiff("architecture.addWall", AddArgs(f, -20, 0, -20, 5));
        var added = Assert.Single(add.Render).NodeId;
        Assert.DoesNotContain(added, walls);
        var delete = f.RunDiff("element.delete", new JsonObject { ["ids"] = new JsonArray(added) });
        Assert.Empty(delete.Render);
        Assert.Equal(added, Assert.Single(delete.RenderRemoved));
    }

    [Fact]
    public void A_wall_stopping_against_a_framed_wall_frames_it_again_and_its_going_too()
    {
        var (f, walls) = Framed(1);
        var bottom = walls[0];
        var before = Backers(f, bottom);
        var d = f.RunDiff("architecture.addWall", AddArgs(f, 4, 0, 4, 6));
        var inner = d.Render.Select(r => r.NodeId).First(id => !walls.Contains(id));
        // The wall it stops against is built again, with the studs of the T; the frame reads as a new wall in the same type would.
        Assert.Contains(d.Render, r => r.NodeId == bottom);
        Assert.True(Backers(f, bottom) > before);
        var withT = Backers(f, bottom);

        f.Session.Undo();
        Assert.Equal(before, Backers(f, bottom));
        f.Session.Redo();
        Assert.Equal(withT, Backers(f, bottom));

        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(inner) });
        Assert.Equal(before, Backers(f, bottom));
    }

    [Fact]
    public void A_wall_moved_away_and_moved_in_frames_again_the_walls_it_left_and_reached()
    {
        var (f, walls) = Framed(1);
        var bottom = walls[0];
        var before = Backers(f, bottom);
        // Far off first: nothing here sees it.
        var far = AddWall(f, -20, 0, -20, 6);
        Assert.Equal(before, Backers(f, bottom));
        // Moved to stop against the wall: the wall sees it, from where it arrived.
        var moved = f.RunDiff("architecture.updateWall", new JsonObject { ["id"] = far, ["x1"] = 4, ["y1"] = 0, ["x2"] = 4, ["y2"] = 6 });
        Assert.Contains(moved.Render, r => r.NodeId == bottom);
        Assert.True(Backers(f, bottom) > before);
        // And away again: it sees that too, from where it left.
        var back = f.RunDiff("architecture.updateWall", new JsonObject { ["id"] = far, ["x1"] = -20, ["y1"] = 0, ["x2"] = -20, ["y2"] = 6 });
        Assert.Contains(back.Render, r => r.NodeId == bottom);
        Assert.Equal(before, Backers(f, bottom));
    }

    [Fact]
    public void The_separation_lines_are_those_every_line_against_every_edge_finds()
    {
        // Rectangles side by side, long sides shared in part, a slanted and a long edge among them.
        var loops = new List<Vec2[]>
        {
            new Vec2[] { new(0, 0), new(40, 0), new(40, 0.2), new(0, 0.2) },
            new Vec2[] { new(3, 0.2), new(9, 0.2), new(9, 3), new(3, 3) },
            new Vec2[] { new(9, 0.2), new(9.3, 0.2), new(9.3, 3), new(9, 3) },
            new Vec2[] { new(20, 0.2), new(35, 15.2), new(34.8, 15.4), new(19.8, 0.4) },
            new Vec2[] { new(35, 15.2), new(36, 16.2), new(35.8, 16.4), new(34.8, 15.4) },
        };
        var cuts = loops.Select((l, i) => new DPolygon([l], "e" + i, IfcClass.Wall, null, null)).ToList();
        var lines = new List<DLine>();
        foreach (var l in loops)
            for (var k = 0; k < l.Length; k++) lines.Add(new DLine(l[k], l[(k + 1) % l.Length], LineRole.Cut, "e", IfcClass.Wall));
        // A piece of a long side, of another class, and a seen line: only the first may count.
        lines.Add(new DLine(new Vec2(4, 0.2), new Vec2(8, 0.2), LineRole.Cut, "e", IfcClass.Wall));
        lines.Add(new DLine(new Vec2(4, 0.2), new Vec2(8, 0.2), LineRole.Cut, "e", IfcClass.Slab));
        lines.Add(new DLine(new Vec2(4, 0.2), new Vec2(8, 0.2), LineRole.Projection, "e", IfcClass.Wall));
        var d = new ViewDrawing(cuts, lines);

        var brute = new HashSet<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Role != LineRole.Cut || lines[i].Class != IfcClass.Wall) continue;
            var on = new HashSet<int>();
            for (var c = 0; c < loops.Count; c++)
                for (var k = 0; k < loops[c].Length; k++)
                    if (Holds(lines[i].A, lines[i].B, loops[c][k], loops[c][(k + 1) % loops[c].Length])) on.Add(c);
            if (on.Count >= 2) brute.Add(i);
        }
        Assert.NotEmpty(brute);
        Assert.Equal(brute.Order(), Pens.Separations(d).Order());
    }

    static bool Holds(Vec2 p, Vec2 q, Vec2 a, Vec2 b)
    {
        var u = (b - a).Normalized();
        foreach (var x in new[] { p, q })
        {
            var t = Vec2.Dot(u, x - a);
            if (Math.Abs(Vec2.Cross(u, x - a)) > 1e-4 || t < -1e-4 || t > (b - a).Length + 1e-4) return false;
        }
        return true;
    }
}
