using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// Timber-frame walls (the wood module, docs/plan-wood.md, decision 239), stage 1: the members round door and window openings
/// by their rough openings and width bands, the studs on the module, shared headers, partitions, an opening's own rule; the
/// wall's body, the configurator's preview, the Timber schedule and IFC.
/// </summary>
public class WoodFramingTests
{
    static readonly FrameSpec Nordic = FrameSpec.Nordic;

    static Framing.Result Sample(FrameSpec spec, params Framing.Hole[] holes) =>
        Framing.Layout(spec, 0, 6, _ => 2.7, [], -0.0725, 0.0725, holes, 0);

    static int Count(Framing.Result r, string role) => r.Members.Count(m => m.Role == role);

    [Fact]
    public void Round_a_window_and_a_door_the_frame_takes_the_band_s_kings_jacks_header_and_sill()
    {
        var r = Sample(Nordic, new Framing.Hole("W", 1.7, 0.9, 1.2, 1.3, false), new Framing.Hole("D", 4.3, 0, 0.9, 2.1, true));
        // Both rough openings (1.22 and 0.92 m) fall in the band up to 1.8 m: 2 kings and 2 jacks a side, a 2 × 45 × 245 header, a double sill.
        Assert.Equal(8, Count(r, "king"));
        Assert.Equal(8, Count(r, "jack"));
        Assert.Equal(4, Count(r, "header"));
        Assert.All(r.Members.Where(m => m.Role == "header"), m => Assert.Equal("45x245 mm", m.Section));
        Assert.Equal(2, Count(r, "sill"));
        Assert.Equal(4, Count(r, "end"));
        // The bottom plate stops at the door: two pieces; the top plate doubled.
        Assert.Equal(4, Count(r, "plate"));
        // The rough openings: the window 10 mm round, the door down to the floor.
        Assert.Equal((1.09, 2.31, 0.89, 2.21), (Math.Round(r.Openings["W"].U0, 4), Math.Round(r.Openings["W"].U1, 4), Math.Round(r.Openings["W"].Z0, 4), Math.Round(r.Openings["W"].Z1, 4)));
        Assert.Equal(0, r.Openings["D"].Z0, 9);
        // Nothing stands in a rough opening.
        foreach (var (rule, u0, u1, z0, z1) in r.Openings.Values)
            Assert.DoesNotContain(r.Members, m => m.U0 < u1 - 1e-6 && m.U1 > u0 + 1e-6 && m.Z0 < z1 - 1e-6 && m.Z1 > z0 + 1e-6);
        // The studs and the cripples stand on the 600 mm module.
        Assert.All(r.Members.Where(m => m.Role is "stud" or "cripple"), m => Assert.Equal(0, Math.Round(((m.U0 + m.U1) / 2) / 0.6 % 1, 6) % 1, 6));
        Assert.Equal(3, Count(r, "stud"));
        Assert.True(Count(r, "cripple") >= 4);
        Assert.Empty(r.Warnings);
        // The insulation fills what is left, never where a member stands.
        Assert.NotEmpty(r.Insulation);
        Assert.DoesNotContain(r.Insulation, c => r.Members.Any(m => m.Role is "stud" or "king" && m.U0 < c.U1 - 1e-6 && m.U1 > c.U0 + 1e-6 && m.Z0 < c.Z1Left - 1e-6 && m.Z1 > c.Z0 + 1e-6));
    }

    [Fact]
    public void Two_windows_closer_than_the_narrowest_pier_share_one_header_and_their_outer_kings()
    {
        var r = Sample(Nordic, new Framing.Hole("A", 1.5, 0.9, 1.0, 1.2, false), new Framing.Hole("B", 2.7, 0.9, 1.0, 1.2, false));
        Assert.Equal(4, Count(r, "king"));
        // One header over both, by the rule of the span it covers (2.2 m: the glulam band).
        var header = Assert.Single(r.Members, m => m.Role == "header");
        Assert.Equal("90x360 mm", header.Section);
        Assert.True(header.U0 < r.Openings["A"].U0 && header.U1 > r.Openings["B"].U1);
        // The jacks between them meet in one post.
        var between = r.Members.Where(m => m.Role == "jack" && m.U0 >= r.Openings["A"].U1 - 1e-6 && m.U1 <= r.Openings["B"].U0 + 1e-6).ToList();
        Assert.NotEmpty(between);
    }

    [Fact]
    public void A_partition_takes_a_flat_member_over_its_openings_and_an_opening_its_own_rule()
    {
        var r = Sample(Nordic with { LoadBearing = false, Openings = FrameSpec.Partition }, new Framing.Hole("D", 3, 0, 0.9, 2.1, true));
        var header = Assert.Single(r.Members, m => m.Role == "header");
        Assert.Equal(0.045, header.Z1 - header.Z0, 9);
        Assert.Equal(2, Count(r, "king"));
        var own = Sample(Nordic, new Framing.Hole("W", 3, 0.9, 0.6, 1.0, false, new OpeningRule(9, 3, 1, new HeaderRule("lvl", "45x300 mm", 1), 0)));
        Assert.Equal(6, Count(own, "king"));
        Assert.Equal(0, Count(own, "sill"));
        Assert.Equal("45x300 mm", Assert.Single(own.Members, m => m.Role == "header").Section);
        // A window too close to the wall's end: told, not moved.
        var near = Sample(Nordic, new Framing.Hole("W", 0.5, 0.9, 0.6, 1.0, false));
        Assert.Contains(near.Warnings, w => w.StartsWith("W:"));
    }

    static (Fixture F, string Wall, string Window, string Type) Wall()
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        var type = Walls.TypeOf(f.Model, wall)!.Id;
        f.Run("wood.setWallFrame", new JsonObject { ["id"] = type, ["spec"] = FrameSpec.Nordic.ToJson() });
        return (f, wall, window, type);
    }

    [Fact]
    public void A_wall_of_a_timber_frame_type_is_built_of_its_members_in_place_of_its_core()
    {
        var (f, wall, window, type) = Wall();
        var parts = f.Session.Geometry(wall)!.Parts;
        var core = Walls.Section(f.Model, f.Model.Get(wall)!).First(b => b.Core);
        Assert.DoesNotContain(parts, p => p.Key == $"layer:{core.Index}");
        Assert.Contains(parts, p => p.Key!.StartsWith("wood:stud:"));
        Assert.Contains(parts, p => p.Key!.StartsWith("wood:header:"));
        Assert.Contains(parts, p => p.Key == "wood-insulation");
        Assert.Contains(parts, p => p.Properties?.Contains(("Grade", "C24")) == true);
        // The opening's framing, by its band; then its own rule.
        var o = (JsonObject)f.Session.Query("wood.opening", new JsonObject { ["id"] = window })!;
        Assert.True((bool)o["framed"]!);
        Assert.False((bool)o["own"]!);
        var kings = (int)((JsonObject)f.Session.Query("wood.wall", new JsonObject { ["id"] = wall })!)["counts"]!["king"]!;
        f.Run("wood.setOpeningFrame", new JsonObject { ["id"] = window, ["rule"] = new JsonObject { ["kings"] = 3 } });
        Assert.Equal(kings + 2 * (3 - (int)o["rule"]!["kings"]!), (int)((JsonObject)f.Session.Query("wood.wall", new JsonObject { ["id"] = wall })!)["counts"]!["king"]!);
        Assert.True((bool)((JsonObject)f.Session.Query("wood.opening", new JsonObject { ["id"] = window })!)["own"]!);
        // The window moved: its frame follows.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = window, ["along"] = 2 });
        var w = (JsonObject)f.Session.Query("wood.wall", new JsonObject { ["id"] = wall })!;
        Assert.Contains(((JsonArray)w["members"]!).OfType<JsonObject>(), m => (string?)m["role"] == "header" && (double)m["face"]![0]! < 2 - 0.3);
        // Not a timber frame any more: the core again.
        f.Run("wood.setWallFrame", new JsonObject { ["id"] = type, ["spec"] = null });
        Assert.Contains(f.Session.Geometry(wall)!.Parts, p => p.Key == $"layer:{core.Index}");
    }

    [Fact]
    public void The_preview_the_timber_schedule_and_IFC()
    {
        var (f, wall, _, _) = Wall();
        var p = (JsonObject)f.Session.Query("wood.preview", new JsonObject { ["spec"] = FrameSpec.Romanian.ToJson() })!;
        Assert.Null(p["problem"]);
        Assert.True((int)p["counts"]!["noggin"]! > 0);
        Assert.NotNull(((JsonObject)f.Session.Query("wood.preview", new JsonObject { ["spec"] = new JsonObject { ["spacing"] = 3 } })!)["problem"]);
        var rows = TimberSchedule.Rows(new Q(f));
        Assert.Contains(rows, r => r.Role == "Stud" && r.Section == "45x145 mm" && r.Grade == "C24");
        Assert.Contains(rows, r => r.Role == "Header");
        var path = Path.Combine(Path.GetTempPath(), $"wood-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var file = StepFile.Read(path);
            Assert.Contains(file.OfType("IFCMEMBER"), m => m[8] is StepEnum { Value: "STUD" });
            Assert.Contains(file.OfType("IFCMEMBER"), m => m[8] is StepEnum { Value: "USERDEFINED" } && (string?)m[4] == "Header");
            Assert.Contains(file.OfType("IFCCOVERING"), c => c[8] is StepEnum { Value: "INSULATION" });
        }
        finally { File.Delete(path); }
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
