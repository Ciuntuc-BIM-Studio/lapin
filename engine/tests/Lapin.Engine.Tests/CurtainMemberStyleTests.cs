using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #128, a tester's request: custom aluminium profiles for chosen mullions and transoms, with orientation, alignment and offset,
/// reusable across curtain walls and following the library profile when it is updated.
/// </summary>
public class CurtainMemberStyleTests
{
    /// <summary>A section 50 wide and 100 deep with a nose: not symmetric, so a turn, a mirror and an alignment all show.</summary>
    static SectionProfile Alu(string name = "ALU 50x100") => new()
    {
        Name = name, Kind = "outline", Material = "aluminium", B = 0.05, H = 0.1,
        Rings = [new Vec2[] { new(-0.025, -0.05), new(0.025, -0.05), new(0.025, 0.05), new(0.005, 0.05), new(0.005, 0.03), new(-0.025, 0.03) }],
    };

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Used(Fixture f, SectionProfile p, string library = "Alu") => (JsonObject)f.Run("profiles.use", new JsonObject { ["profile"] = p.ToJson(), ["library"] = library })!["section"]!.DeepClone();

    static string Curtain(Fixture f, double x1 = 0, double y1 = 0, string? type = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x1 + 6, ["y2"] = y1, ["height"] = 3 };
        if (type != null) a["typeId"] = type;
        return (string)f.Run("architecture.addCurtainWall", a)!["id"]!;
    }

    static (Vec3 A, Vec3 B) Box(Fixture f, string id, string key) => Measure.Bounds(f.Session.Geometry(id)!.Parts.Single(p => p.Key == key).Solid)!.Value;
    static void Members(Fixture f, string id, string[] keys, JsonObject? extra = null)
    {
        var a = new JsonObject { ["id"] = id, ["keys"] = new JsonArray(keys.Select(k => (JsonNode)k).ToArray()) };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        f.Run("architecture.setCurtainMembers", a);
    }

    [Fact]
    public void A_chosen_mullion_takes_a_profile_of_its_own_and_the_rest_keep_the_type_s()
    {
        var f = F();
        var id = Curtain(f);
        var (plainA, plainB) = Box(f, id, "mullion:2");
        Members(f, id, ["mullion:1"], new JsonObject { ["profile"] = Used(f, Alu()) });
        var (a, b) = Box(f, id, "mullion:1");
        Assert.Equal(0.05, b.X - a.X, 6);
        Assert.Equal(0.1, b.Y - a.Y, 6);
        // The others are as they were; only the one carries a style.
        Assert.Equal((plainA, plainB), Box(f, id, "mullion:2"));
        var q = (JsonObject)f.Session.Query("architecture.curtainMembers", new JsonObject { ["id"] = id })!;
        var pieces = q["pieces"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal("ALU 50x100", (string?)pieces.Single(p => (string)p["key"]! == "mullion:1")["profile"]);
        Assert.True((bool)pieces.Single(p => (string)p["key"]! == "mullion:1")["own"]!);
        Assert.False((bool)pieces.Single(p => (string)p["key"]! == "mullion:2")["own"]!);
        // By role: every transom of the wall; then back to the type's.
        Members(f, id, ["transom"], new JsonObject { ["profile"] = Used(f, Alu()) });
        var (t0, t1) = Box(f, id, "transom:0:1");
        Assert.Equal(0.1, t1.Y - t0.Y, 6);
        Members(f, id, ["mullion:1", "transom"], new JsonObject { ["clear"] = true });
        Assert.Null(f.Model.Get(id)!.Attrs["memberStyles"]);
        Assert.Equal(Box(f, id, "mullion:2").B.Y - Box(f, id, "mullion:2").A.Y, Box(f, id, "mullion:1").B.Y - Box(f, id, "mullion:1").A.Y, 9);
    }

    [Fact]
    public void A_turn_a_mirror_an_alignment_and_an_offset_place_the_profile_on_its_member()
    {
        var f = F();
        var id = Curtain(f);
        var section = Used(f, Alu());
        Members(f, id, ["mullion:1"], new JsonObject { ["profile"] = section });
        var (a0, b0) = Box(f, id, "mullion:1");
        // Turned a quarter: width and depth exchange.
        Members(f, id, ["mullion:1"], new JsonObject { ["place"] = new JsonObject { ["rotate"] = 90 } });
        var (a1, b1) = Box(f, id, "mullion:1");
        Assert.Equal(0.1, b1.X - a1.X, 6);
        Assert.Equal(0.05, b1.Y - a1.Y, 6);
        // Mirrored: the nose goes to the other side of the member.
        Members(f, id, ["mullion:1"], new JsonObject { ["place"] = new JsonObject { ["mirror"] = true } });
        var g = f.Session.Geometry(id)!.Parts.Single(p => p.Key == "mullion:1").Solid;
        Assert.Contains(g.Vertices, v => Math.Abs(v.X - (1.5 - 0.005)) < 1e-6 && Math.Abs(v.Y - -0.03) < 1e-6);
        // Aligned: the front face on the member's plane and the left edge on its grid line.
        Members(f, id, ["mullion:1"], new JsonObject { ["place"] = new JsonObject { ["across"] = "front", ["along"] = "start" } });
        var (a2, b2) = Box(f, id, "mullion:1");
        Assert.Equal(1.5, a2.X, 6);
        Assert.Equal(0.05, b2.X - a2.X, 6);
        // y is out of the building, the wall's right: along +x, −y. The profile's front (+y) face is on the plane.
        Assert.Equal(0, a2.Y, 6);
        Assert.Equal(0.1, b2.Y - a2.Y, 6);
        // Offset 20 mm along, 10 mm out.
        Members(f, id, ["mullion:1"], new JsonObject { ["place"] = new JsonObject { ["dx"] = 0.02, ["dy"] = 0.01 } });
        var (a3, b3) = Box(f, id, "mullion:1");
        Assert.Equal((a0.X + b0.X) / 2 + 0.02, (a3.X + b3.X) / 2, 6);
        Assert.Equal((a0.Y + b0.Y) / 2 - 0.01, (a3.Y + b3.Y) / 2, 6);
        // The same volume throughout: a place moves a profile, it does not change it.
        Assert.Equal(Alu().Rings!.Sum(r => Polygon2.SignedArea(r)) * 3, Measure.Volume(f.Session.Geometry(id)!.Parts.Single(p => p.Key == "mullion:1").Solid), 6);
    }

    [Fact]
    public void A_plain_member_can_be_moved_and_a_profile_left_off_with_plain()
    {
        var f = F();
        var id = Curtain(f);
        var (a0, b0) = Box(f, id, "mullion:1");
        Members(f, id, ["mullion:1"], new JsonObject { ["place"] = new JsonObject { ["dx"] = 0.1 } });
        var (a1, b1) = Box(f, id, "mullion:1");
        Assert.Equal(a0.X + 0.1, a1.X, 6);
        Assert.Equal(b0.X - a0.X, b1.X - a1.X, 6);
        // A profile on the type and plain on one member.
        var section = Used(f, Alu());
        var type = f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Id;
        var spec = CurtainWalls.SpecOf(f.Model, f.Model.Get(id)!) with { Mullion = new CurtainSection(0.05, 0.1, section) };
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = type, ["spec"] = spec.ToJson() });
        Assert.Equal(0.1, Box(f, id, "mullion:2").B.Y - Box(f, id, "mullion:2").A.Y, 6);
        Members(f, id, ["mullion:2"], new JsonObject { ["plain"] = true });
        Assert.Equal(0.1, Box(f, id, "mullion:2").B.Y - Box(f, id, "mullion:2").A.Y, 6);
        Assert.Equal(8, f.Session.Geometry(id)!.Parts.Single(p => p.Key == "mullion:2").Solid.Vertices.Length);
    }

    [Fact]
    public void A_type_keeps_the_place_of_its_profile_for_every_curtain_wall_of_it_and_an_unknown_member_is_refused()
    {
        var f = F();
        var section = Used(f, Alu());
        var first = Curtain(f);
        var type = f.Model.RelatingOf(first, IfcClass.RelDefinesByType)!.Id;
        var json = CurtainWalls.SpecOf(f.Model, f.Model.Get(first)!).ToJson();
        var mullion = new JsonObject { ["width"] = 0.05, ["depth"] = 0.1, ["profile"] = section.DeepClone(), ["place"] = new JsonObject { ["rotate"] = 90, ["across"] = "back" } };
        json["mullion"] = mullion;
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = type, ["spec"] = json });
        // Turned a quarter turn the profile's size is the other way: the spec's width follows.
        var spec = CurtainWalls.SpecOf(f.Model, f.Model.Get(first)!);
        Assert.Equal((0.1, 0.05), (spec.Mullion.Width, spec.Mullion.Depth));
        var second = Curtain(f, 0, 10, type);
        foreach (var id in new[] { first, second })
        {
            var (a, b) = Box(f, id, "mullion:1");
            Assert.Equal(0.1, b.X - a.X, 6);
            Assert.Equal(0.05, b.Y - a.Y, 6);
        }
        Assert.Throws<CommandException>(() => Members(f, first, ["mullion:99"]));
        Assert.Throws<CommandException>(() => Members(f, first, []));
    }

    [Fact]
    public void A_member_s_own_profile_follows_the_library_when_it_is_updated()
    {
        var f = F();
        var id = Curtain(f);
        var section = Used(f, Alu());
        Members(f, id, ["mullion:1", "transom"], new JsonObject { ["profile"] = section, ["place"] = new JsonObject { ["dx"] = 0.01 } });
        var copy = f.Model.OfClass(ProjectProfiles.Class).Single();
        var wider = Alu() with { B = 0.06, Rings = [new Vec2[] { new(-0.03, -0.05), new(0.03, -0.05), new(0.03, 0.05), new(-0.03, 0.05) }] };
        var used = f.Run("profiles.update", new JsonObject { ["id"] = copy.Id, ["profile"] = wider.ToJson() })!;
        // The wall is one of the things that held the profile: the chosen mullion is now 60 wide, its place kept.
        Assert.True((int)used["types"]! >= 1);
        var (a, b) = Box(f, id, "mullion:1");
        Assert.Equal(0.06, b.X - a.X, 6);
        Assert.Equal(1.5 + 0.01, (a.X + b.X) / 2, 6);
        // A profile that cannot be taken out while a wall uses it.
        Assert.Throws<CommandException>(() => f.Run("profiles.remove", new JsonObject { ["id"] = copy.Id }));
    }

    static JsonArray Ring(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    [Fact]
    public void A_profile_drawn_in_the_editor_is_made_from_its_rings_in_millimetres_and_a_crossing_one_is_refused()
    {
        var f = F();
        // 50 × 100 with a 30 × 60 chamber: the second ring is inside the first, so a hole, whatever way it was drawn.
        var made = (JsonObject)f.Session.Query("profiles.fromRings", new JsonObject
        {
            ["name"] = "Drawn 50x100", ["material"] = "aluminium",
            ["rings"] = new JsonArray(Ring((10, 20), (60, 20), (60, 120), (10, 120)), Ring((20, 30), (50, 30), (50, 90), (20, 90))),
        })!;
        Assert.Equal("outline", (string)made["kind"]!);
        Assert.Equal("aluminium", (string)made["material"]!);
        var profile = SectionProfile.From(made)!;
        Assert.Equal(2, profile.Rings!.Count);
        Assert.Equal(0.05 * 0.1 - 0.03 * 0.06, profile.Rings!.Sum(r => Polygon2.SignedArea(r)), 9);
        Assert.Equal(0.05, profile.B, 9);
        Assert.Equal(0.1, profile.H, 9);
        // Centred on its box, the hole clockwise, and it goes into a member as any library profile does.
        Assert.All(profile.Rings!, r => Assert.InRange(r.Min(p => p.X), -0.0251, 0.0251));
        Assert.True(Polygon2.SignedArea(profile.Rings![1]) < 0);
        var id = Curtain(f);
        Members(f, id, ["mullion:1"], new JsonObject { ["profile"] = Used(f, profile, "My profiles") });
        Assert.Equal(profile.Rings!.Sum(r => Polygon2.SignedArea(r)) * 3, Measure.Volume(f.Session.Geometry(id)!.Parts.Single(p => p.Key == "mullion:1").Solid), 6);
        // A bow tie crosses itself; two outlines crossing are refused; so is a ring of two points.
        Assert.Throws<CommandException>(() => f.Session.Query("profiles.fromRings", new JsonObject { ["rings"] = new JsonArray(Ring((0, 0), (10, 10), (10, 0), (0, 10))) }));
        Assert.Throws<CommandException>(() => f.Session.Query("profiles.fromRings", new JsonObject { ["rings"] = new JsonArray(Ring((0, 0), (40, 0), (40, 40), (0, 40)), Ring((20, 20), (60, 20), (60, 60), (20, 60))) }));
        Assert.Throws<CommandException>(() => f.Session.Query("profiles.fromRings", new JsonObject { ["rings"] = new JsonArray(Ring((0, 0), (1, 1))) }));
    }
}
