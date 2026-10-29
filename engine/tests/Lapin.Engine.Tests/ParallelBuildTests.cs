using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Elements built on every core (Session.BuildMany) give what one core gives: the same items, in the same order, with the same triangles and edges.</summary>
public class ParallelBuildTests
{
    static Fixture Walls()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = f.Type("Brick", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.02, false));
        // A grid of rooms: walls joined at every crossing, so their seams and joins depend on their neighbours.
        for (var i = 0; i <= 12; i++)
        {
            f.Wall(i * 4, 0, i * 4, 48, type);
            f.Wall(0, i * 4, 48, i * 4, type);
        }
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(24, 0), new JsonArray(24, 24), new JsonArray(0, 24)) });
        return f;
    }

    static string Fingerprint(Diff d) => string.Join("\n", d.Render.Select(r =>
        $"{r.NodeId}|{string.Join(";", (r.Parts ?? []).Select(p => $"{p.Name}:{p.Positions.Length}:{p.Positions.Sum(v => (double)v):0.###}:{p.Edges.Length}:{p.Edges.Sum(v => (double)v):0.###}"))}"));

    [Fact]
    public void A_snapshot_on_every_core_is_the_snapshot_on_one()
    {
        var f = Walls();
        f.Session.OneCore = true;
        var one = Fingerprint(f.Session.Snapshot());
        f.Session.OneCore = false;
        var all = Fingerprint(f.Session.Snapshot());
        Assert.True(f.Session.Snapshot().Render.Count > 20);
        Assert.Equal(one, all);
    }

    [Fact]
    public void An_edit_rebuilding_many_elements_on_every_core_gives_what_one_core_gives()
    {
        var f = Walls();
        // A storey's height changed: every wall on it built again; on one core, then undone and made again on every core.
        Diff Raise() => f.RunDiff("element.update", new JsonObject { ["id"] = f.Storey, ["attrs"] = new JsonObject { ["height"] = 3.4 } });
        f.Session.OneCore = true;
        var one = Raise();
        f.Session.Undo();
        f.Session.OneCore = false;
        var all = Raise();
        Assert.True(one.Render.Count > 20);
        Assert.Equal(Fingerprint(one), Fingerprint(all));
    }
}
