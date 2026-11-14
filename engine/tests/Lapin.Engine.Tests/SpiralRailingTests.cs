using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 86 (Casa Curutchet's spiral stair): a spiral that turns more than once passes over itself, and its first
/// turn's balusters stood on the treads a turn up (the highest surface over their footprint), cut to 5 cm stubs under the
/// handrail, which was left floating. Every post now stands on the tread under it and reaches the handrail, and the railing
/// keeps to its radius, on either side, turning either way, through one turn, one and a half or two.
/// </summary>
public class SpiralRailingTests
{
    // The tester's type: open treads on steel, 18 risers of 167, tread 285, width 780; balusters 0.11 apart under a square handrail 0.9 high, 0.05 in from the edge.
    static StairSpec Curutchet(string sides) => StairSpec.Library["spiral"]() with
    {
        Width = 0.78,
        Railing = new StairRailing(sides, 0.9, 0.04, 0.02, 1.2, "balusters", "square", 0.11, 0.05),
    };

    static (Resolved R, StairPlacement Place, ElementGeometry G) Build(string sides, string turn, double? sweep, bool rotated)
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = Curutchet(sides).ToJson() })!["id"]!;
        var args = new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 2, ["y"] = 1, ["x2"] = 3, ["y2"] = rotated ? 2 : 1, ["turn"] = turn };
        if (sweep is { } s) args["sweep"] = s;
        var id = (string)f.Run("architecture.addStair", args)!["id"]!;
        var (r, place, _) = StairBuilder.Resolve(f.Session.Model, f.Session.Model.Get(id)!)!.Value;
        return (r, place, f.Session.Geometry(id)!);
    }

    /// <summary>The upright boxes of a railing part (posts, balusters): its solids are joined eight vertices each, a box's.</summary>
    static IEnumerable<Vec3[]> Uprights(Solid rail, double size)
    {
        for (var i = 0; i + 8 <= rail.Vertices.Length; i += 8)
        {
            var box = rail.Vertices[i..(i + 8)];
            if (box.Max(v => v.X) - box.Min(v => v.X) < size * 1.5 && box.Max(v => v.Y) - box.Min(v => v.Y) < size * 1.5) yield return box;
        }
    }

    [Theory]
    [InlineData("right", "left", null)]   // the tester's: 18 risers turn about 1.5 times, anticlockwise, the railing outside
    [InlineData("right", "right", null)]  // clockwise: the right side is the pole's
    [InlineData("left", "left", null)]
    [InlineData("left", "right", null)]
    [InlineData("both", "left", 360.0)]   // one turn: the top stands over the foot
    [InlineData("both", "right", 540.0)]
    [InlineData("both", "left", 720.0)]   // two turns
    public void A_spiral_railing_follows_its_treads_all_the_way_round(string sides, string turn, double? sweep)
    {
        foreach (var rotated in new[] { false, true })
        {
            var (r, place, g) = Build(sides, turn, sweep, rotated);
            var arc = r.Arc!;
            var centre = place.World(arc.Centre);
            var rail = Curutchet(sides).Railing;
            if (sweep is { } sw) Assert.Equal(sw, Math.Abs(arc.Step * arc.Steps) * 180 / Math.PI, 6);
            else Assert.True(Math.Abs(arc.Step * arc.Steps) > 2 * Math.PI, "the tester's stair turns more than once");
            foreach (var side in new[] { "left", "right" })
            {
                var part = g.Parts.SingleOrDefault(p => p.Key == $"railing:{side}");
                if (sides != side && sides != "both") { Assert.Null(part); continue; }
                Assert.NotNull(part);
                // Left as one climbs is the pole's side when it turns left.
                var inward = side == "left" == (turn == "left");
                var radius = inward ? arc.Inner + rail.Inset : arc.Outer - rail.Inset;
                // Every point of it (handrail, bottom rail, balusters) keeps to that radius, within the bar's half diagonal and the chords' sag.
                foreach (var v in part!.Solid.Vertices)
                    Assert.InRange((new Vec2(v.X, v.Y) - centre).Length, radius - 0.04, radius + 0.04);
                var posts = Uprights(part.Solid, Math.Max(0.012, rail.PostSize * 0.6)).ToList();
                Assert.True(posts.Count > 2 * arc.Steps, $"{side}: {posts.Count} balusters");
                foreach (var post in posts)
                {
                    var (foot, top) = (post.Min(v => v.Z) - place.Z0, post.Max(v => v.Z) - place.Z0);
                    // It stands on a tread (or a floor) and reaches the handrail, the railing's height over the nosings' line: no stub.
                    Assert.InRange(top - foot, rail.Height - 1e-6, rail.Height + r.RiserHeight + 1e-6);
                    Assert.InRange(foot, -1e-6, r.Height + 1e-6);
                    Assert.True(Math.Abs(foot / r.RiserHeight - Math.Round(foot / r.RiserHeight)) < 1e-6, $"{side}: a baluster's foot at {foot:0.000} is not on a tread");
                }
                // Round the stair the handrail climbs: its posts' tops go from a riser over the floor to the railing's height over the top.
                Assert.Equal(r.RiserHeight + rail.Height, posts.Min(p => p.Max(v => v.Z)) - place.Z0, 6);
                Assert.Equal(r.Height + rail.Height, posts.Max(p => p.Max(v => v.Z)) - place.Z0, 6);
            }
        }
    }
}
