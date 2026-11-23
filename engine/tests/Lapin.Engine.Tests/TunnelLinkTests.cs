using System.Text;
using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tunnel lining linked to its axis's source (docs/plan-wing-walls.md, S4, the user's answer of 23.11.2026): the road edited, the
/// lining takes its axis and red line again in the same undo step, its blocks keeping their GlobalIds; its stations held at the
/// road's ends with a note when it gets shorter, back when it grows; unlinked, it stays as it is, and linked again it follows; its
/// source deleted, it stays, unlinked, with a note; its own axis edited, it is unlinked; a copy made before links (no flag) stays.
/// </summary>
public class TunnelLinkTests
{
    static Fixture Site() => new(new CivilModule());

    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);

    static string Road(Fixture f, JsonArray points)
    {
        var road = (string)f.Run("civil.addPath", new JsonObject { ["points"] = points, ["template"] = "road" })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = road, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 100, 0), new JsonArray(200, 104, 0)) } });
        return road;
    }

    static string Lining(Fixture f, string road, double from, double to, bool? linked = null)
    {
        var a = new JsonObject { ["spec"] = new JsonObject { ["preset"] = "horseshoe" }, ["axis"] = new JsonObject { ["id"] = road, ["from"] = from, ["to"] = to } };
        if (linked is { } l) a["linked"] = l;
        return (string)f.Run("civil.addTunnelLining", a)!["id"]!;
    }

    static Chain Axis(Fixture f, string id) => Corridors.ChainOf(f.Model.Get(id)!);

    static List<string> Gids(Fixture f) =>
        [.. Encoding.UTF8.GetString(IfcExport.Bytes(f.Session, "t.ifc").Item1).Split('\n').Where(l => l.Contains("=IFCBUILDINGELEMENTPROXY(") || l.Contains("=IFCELEMENTASSEMBLY(")).Select(l => l.Split('\'')[1]).Order()];

    [Fact]
    public void The_road_edited_the_lining_follows_in_one_undo_step_its_blocks_keeping_their_GlobalIds()
    {
        var f = Site();
        var road = Road(f, Pts((0, 0), (100, 0)));
        var id = Lining(f, road, 10, 60);
        Assert.True(Tunnels.IsLinked(f.Model.Get(id)!));
        var gids = Gids(f);
        // Four 12.5 m blocks and their shells, and the assembly.
        Assert.Equal(1 + 4 * 2, gids.Count);
        // The road turned about its start: the lining's axis is the road's between the same stations.
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (80, 60)) });
        var lc = Axis(f, id);
        var rc = Axis(f, road);
        Assert.Equal(10, lc.Start, 6);
        Assert.Equal(60, lc.End, 6);
        foreach (var s in new[] { 10.0, 33, 60 }) Assert.True((lc.PointAt(s) - rc.PointAt(s)).Length < 1e-6, $"the axis at {s}");
        Assert.True((lc.PointAt(60) - new Vec2(48, 36)).Length < 1e-6);
        Assert.Equal(gids, Gids(f));
        // Its red line the road's: 100 at 0 rising 2 %, so 101 at 50.
        var built = Tunnels.Build(f.Model, f.Model.Get(id)!, bodies: false);
        Assert.Equal(101, built.Grade.Z(50), 6);
        f.Run("civil.setCorridor", new JsonObject { ["id"] = road, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 90, 0), new JsonArray(100, 90, 0)) } });
        Assert.Equal(90, Tunnels.Build(f.Model, f.Model.Get(id)!, bodies: false).Grade.Z(50), 6);
        Assert.True(Tunnels.IsLinked(f.Model.Get(id)!));
        // One undo puts the red line back on both; another the road's points and the lining's axis.
        f.Session.Undo();
        Assert.Equal(101, Tunnels.Build(f.Model, f.Model.Get(id)!, bodies: false).Grade.Z(50), 6);
        f.Session.Undo();
        Assert.True((Axis(f, id).PointAt(60) - new Vec2(60, 0)).Length < 1e-6);
        Assert.True((Axis(f, road).PointAt(100) - new Vec2(100, 0)).Length < 1e-6);
    }

    [Fact]
    public void Its_stations_are_held_at_the_road_s_ends_with_a_note_and_come_back_when_it_grows()
    {
        var f = Site();
        var road = Road(f, Pts((0, 0), (100, 0)));
        var id = Lining(f, road, 10, 60);
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (40, 0)) });
        var lc = Axis(f, id);
        Assert.Equal(10, lc.Start, 6);
        Assert.Equal(40, lc.End, 6);
        var note = Tunnels.AxisNote(f.Model.Get(id)!);
        Assert.NotNull(note);
        Assert.Contains("from 10 to 40 m, not 10 to 60 m", note);
        Assert.Contains(note!, f.Session.Geometry(id)!.Diagnostics ?? []);
        Assert.True(Tunnels.IsLinked(f.Model.Get(id)!));
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (120, 0)) });
        Assert.Equal(60, Axis(f, id).End, 6);
        Assert.Null(Tunnels.AxisNote(f.Model.Get(id)!));
        // Shorter than a metre between its stations: unlinked, kept as it was.
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (10.5, 0)) });
        var e = f.Model.Get(id)!;
        Assert.False(Tunnels.IsLinked(e));
        Assert.Contains("no longer runs a metre", Tunnels.AxisNote(e));
        Assert.Equal(60, Axis(f, id).End, 6);
    }

    [Fact]
    public void Unlinked_it_stays_as_it_is_and_linked_again_it_follows()
    {
        var f = Site();
        var road = Road(f, Pts((0, 0), (100, 0)));
        var id = Lining(f, road, 10, 60);
        f.Run("civil.unlinkTunnelLining", new JsonObject { ["id"] = id });
        Assert.False(Tunnels.IsLinked(f.Model.Get(id)!));
        Assert.Throws<Lapin.Platform.CommandException>(() => f.Run("civil.unlinkTunnelLining", new JsonObject { ["id"] = id }));
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (0, 100)) });
        Assert.True((Axis(f, id).PointAt(60) - new Vec2(60, 0)).Length < 1e-6);
        // Linked again (the configurator's axis taken again): on the road as it is now, following it from then on.
        f.Run("civil.updateTunnelLining", new JsonObject { ["id"] = id, ["axis"] = new JsonObject { ["id"] = road, ["from"] = 10.0, ["to"] = 60.0 } });
        Assert.True(Tunnels.IsLinked(f.Model.Get(id)!));
        Assert.True((Axis(f, id).PointAt(60) - new Vec2(0, 60)).Length < 1e-6);
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (-100, 0)) });
        Assert.True((Axis(f, id).PointAt(60) - new Vec2(-60, 0)).Length < 1e-6);
    }

    [Fact]
    public void Its_source_deleted_it_stays_unlinked_with_a_note()
    {
        var f = Site();
        var road = Road(f, Pts((0, 0), (100, 0)));
        var id = Lining(f, road, 10, 60);
        var gids = Gids(f);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(road) });
        var e = f.Model.Get(id);
        Assert.NotNull(e);
        Assert.False(Tunnels.IsLinked(e!));
        Assert.Contains("was deleted", Tunnels.AxisNote(e!));
        Assert.Equal(50, Axis(f, id).Length, 6);
        Assert.Equal(gids, Gids(f));
        f.Session.Undo();
        Assert.True(Tunnels.IsLinked(f.Model.Get(id)!));
        Assert.NotNull(f.Model.Get(road));
    }

    [Fact]
    public void Its_own_axis_edited_it_is_unlinked_and_a_change_to_its_section_keeps_the_link()
    {
        var f = Site();
        var road = Road(f, Pts((0, 0), (100, 0)));
        var id = Lining(f, road, 10, 60);
        f.Run("civil.updateTunnelLining", new JsonObject { ["id"] = id, ["spec"] = new JsonObject { ["preset"] = "horseshoe", ["thickness"] = 0.45 } });
        Assert.True(Tunnels.IsLinked(f.Model.Get(id)!));
        // Its red line set on the lining itself.
        f.Run("civil.setCorridor", new JsonObject { ["id"] = id, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(10, 95, 0), new JsonArray(60, 95, 0)) } });
        var e = f.Model.Get(id)!;
        Assert.False(Tunnels.IsLinked(e));
        Assert.Contains("edited on the lining itself", Tunnels.AxisNote(e));
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (0, 100)) });
        Assert.Equal(95, Tunnels.Build(f.Model, f.Model.Get(id)!, bodies: false).Grade.Z(30), 6);
        Assert.True((Axis(f, id).PointAt(60) - new Vec2(60, 0)).Length < 1e-6);
    }

    [Fact]
    public void A_copy_made_before_links_stays_a_copy()
    {
        var f = Site();
        var road = Road(f, Pts((0, 0), (100, 0)));
        // As every lining taken from an axis was before (its source named, no link flag).
        var id = Lining(f, road, 10, 60, linked: false);
        var e = f.Model.Get(id)!;
        Assert.False(((JsonObject)e.Attrs["axisSource"]!).ContainsKey("linked"));
        var before = e.Attrs.ToJsonString();
        f.Run("civil.updatePath", new JsonObject { ["id"] = road, ["points"] = Pts((0, 0), (0, 100)) });
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(road) });
        Assert.Equal(before, f.Model.Get(id)!.Attrs.ToJsonString());
    }
}
