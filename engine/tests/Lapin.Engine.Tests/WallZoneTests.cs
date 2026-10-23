using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Walls composed in their height. Ported from the old Lapin's
/// wall-zones.test.ts, same cases and numbers (the cases with a door or a
/// window come with openings, M3).
/// </summary>
public class WallZoneTests
{
    static IReadOnlyList<WallZone> DefaultZones()
    {
        var f = new Fixture();
        IReadOnlyList<WallZone> zones = [];
        f.Session.History.Execute("zones", e =>
        {
            var parsed = WallZones.Parse(e, new JsonArray(WallZones.Defaults("Brick").Select(z => (JsonNode)z.DeepClone()).ToArray()));
            zones = WallZones.Of(new Entity("t", IfcClass.WallType, "t", new JsonObject { ["zones"] = parsed }));
        });
        return zones;
    }

    [Fact]
    public void Zones_stack_on_the_wall_fixed_heights_the_rest_to_the_rest_zone_nothing_past_the_top()
    {
        var zones = DefaultZones();
        Assert.Equal([("first-course", 0.0, 0.25), ("masonry", 0.25, 2.75), ("ring-beam", 2.75, 3.0)],
            WallZones.Bands(zones, 3).Select(b => (b.Zone.Key, Math.Round(b.Z0, 9), Math.Round(b.Z1, 9))));
        // No rest zone: the topmost masonry zone takes what is left.
        var fixedZones = zones.Select(z => z.Rest ? z with { Height = 1 } : z).ToList();
        Assert.Equal([250, 2500, 250], WallZones.Bands(fixedZones, 3).Select(b => (int)Math.Round((b.Z1 - b.Z0) * 1000)));
        // A low wall: the ring beam is cut at the top.
        Assert.Equal([("first-course", 0.25), ("ring-beam", 0.4)], WallZones.Bands(zones, 0.4).Select(b => (b.Zone.Key, Math.Round(b.Z1, 9))));
    }

    [Fact]
    public void Only_one_zone_takes_the_rest()
    {
        var f = new Fixture();
        var zones = WallZones.Defaults("Brick").Select(z => (JsonNode)z.DeepClone()).ToList();
        var again = (JsonObject)zones[1].DeepClone();
        again["key"] = "again";
        again["name"] = "Again";
        zones.Add(again);
        var e = Assert.Throws<CommandException>(() => f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Zoned", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3 }), ["zones"] = new JsonArray(zones.ToArray()),
        }));
        Assert.Contains("Only one zone can take the rest", e.Message);
    }

    [Fact]
    public void Each_zone_is_measured_blocks_and_mortar_the_ring_beams_concrete_and_steel()
    {
        var zones = DefaultZones();
        var (first, field, ring) = (zones[0], zones[1], zones[2]);
        static double Value(QuantitySet q, string name) => q.Quantities.Single(x => x.Name == name).Value;

        var masonry = WallZones.Quantities(field, 0.25, 2.75, 5, 0.3, 5 * 2.5 * 0.3);
        Assert.Equal("Lapin_WallZoneQuantities", masonry.Name);
        Assert.Equal(0.3 * 5 * 2.5, Value(masonry, "NetVolume"), 9);
        Assert.Equal(5 * 2.5, Value(masonry, "NetSideArea"), 9);
        // 12.5 m² of 375 × 238 blocks with 10 mm joints.
        Assert.Equal(Math.Ceiling(12.5 / (0.385 * 0.248)), Value(masonry, "BlockCount"));
        Assert.True(Value(masonry, "MortarVolume") > 0);

        var course = WallZones.Quantities(first, 0, 0.25, 5, 0.3, 5 * 0.25 * 0.3);
        Assert.Equal(Math.Ceiling(5 * 0.25 / (0.603 * 0.253)), Value(course, "BlockCount"));

        var beam = WallZones.Quantities(ring, 2.75, 3, 5, 0.3, 5 * 0.25 * 0.3);
        Assert.Equal("Qto_BeamBaseQuantities", beam.Name);
        Assert.Equal(5 * 0.25 * 0.3, Value(beam, "NetVolume"), 9);
        Assert.Equal(0.075, Value(beam, "CrossSectionArea"), 9);
        Assert.Equal(5 * 0.25 * 0.3 * 100, Value(beam, "ReinforcementWeight"), 9);
    }

    static string ZonedType(Fixture f) => (string)f.Run("architecture.createWallType", new JsonObject
    {
        ["name"] = "Brick 30 zoned",
        ["layers"] = new JsonArray(
            new JsonObject { ["material"] = "Render", ["thickness"] = 0.02 },
            new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true },
            new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
        ["zones"] = new JsonArray(WallZones.Defaults("Brick").Select(z => (JsonNode)z.DeepClone()).ToArray()),
    })!["id"]!;

    [Fact]
    public void A_zoned_walls_core_is_its_zones_its_finishes_run_the_whole_height()
    {
        var f = new Fixture();
        var type = ZonedType(f);
        var wall = f.Wall(0, 0, 5, 0, type);
        var parts = f.Session.Geometry(wall)!.Parts;
        Assert.Equal(["layer:0", "zone:first-course", "zone:masonry", "zone:ring-beam", "layer:2"], parts.Select(p => p.Key));
        double Vol(string key) => Measure.Volume(parts.Single(p => p.Key == key).Solid);
        Assert.Equal(5 * 0.3 * 0.25, Vol("zone:ring-beam"), 9);
        Assert.Equal(5 * 0.3 * 2.5, Vol("zone:masonry"), 9);
        Assert.Equal(5 * 0.02 * 3, Vol("layer:0"), 9);
        Assert.Equal(Materials.Find(f.Model, "Reinforced concrete")!.Id, parts.Single(p => p.Key == "zone:ring-beam").Material);

        var sets = f.Session.Quantities(wall);
        Assert.Equal(["Qto_WallBaseQuantities", "Lapin_WallZoneQuantities", "Lapin_WallZoneQuantities", "Qto_BeamBaseQuantities", "Lapin_WallLayerQuantities", "Lapin_WallLayerQuantities"], sets.Select(s => s.Name));
        Assert.Equal(5 * 3 * 0.335, sets[0].Quantities.Single(q => q.Name == "NetVolume").Value, 9);
        Assert.Equal(5 * 0.25 * 0.3 * 100, sets.Single(s => s.Part == "zone:ring-beam").Quantities.Single(q => q.Name == "ReinforcementWeight").Value, 9);
    }

    [Fact]
    public void Changing_the_types_zones_changes_every_wall_of_the_type_and_an_empty_list_takes_them_away()
    {
        var f = new Fixture();
        var type = ZonedType(f);
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        var zones = WallZones.Defaults("Brick").Select(z => (JsonNode)z.DeepClone()).ToArray();
        ((JsonObject)zones[2]!)["height"] = 0.3;
        var diff = f.RunDiff("architecture.updateWallType", new JsonObject { ["id"] = type, ["zones"] = new JsonArray(zones) });
        Assert.Equal(new[] { a, b }.Order(), diff.Render.Select(r => r.NodeId).Order());
        Assert.Equal(5 * 0.3 * 0.3, Measure.Volume(f.Session.Geometry(a)!.Parts.Single(p => p.Key == "zone:ring-beam").Solid), 9);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["zones"] = new JsonArray() });
        Assert.Equal(["layer:0", "layer:1", "layer:2"], f.Session.Geometry(a)!.Parts.Select(p => p.Key));
    }

    [Fact]
    public void In_a_joined_room_the_ring_beams_never_overlap_and_run_along_the_whole_core()
    {
        var f = new Fixture();
        var type = ZonedType(f);
        Vec2[] p = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)];
        var ids = Enumerable.Range(0, 4).Select(i => f.Wall(p[i].X, p[i].Y, p[(i + 1) % 4].X, p[(i + 1) % 4].Y, type)).ToList();
        // The ring beams' footprints: the core's plan outline of each wall (its second band).
        var footprints = ids.Select(id => f.Session.Geometry(id)!.Plan![1].Points.ToArray()).ToList();
        static bool Inside(Vec2[] poly, Vec2 q)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y) && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
            return inside;
        }
        // The core is centred on the axis, 300 mm: the ring between the rectangle 150 mm out and 150 mm in.
        // Near a corner the ending wall's core stops at the through wall's inner finish (15 mm short of its
        // core): layers joined core to core come with the material drawing (M5, decisions-pending #21).
        bool NearCorner(Vec2 q) => p.Any(c => Math.Abs(q.X - c.X) < 0.2 && Math.Abs(q.Y - c.Y) < 0.2);
        bool InRing(Vec2 q) => !NearCorner(q) && q.X > -0.15 && q.X < 6.15 && q.Y > -0.15 && q.Y < 4.15 && !(q.X > 0.15 && q.X < 5.85 && q.Y > 0.15 && q.Y < 3.85);
        var failures = new List<string>();
        for (var x = -0.4 + 0.00731; x < 6.4; x += 0.0173)
            for (var y = -0.4 + 0.00419; y < 4.4; y += 0.0173)
            {
                var q = new Vec2(x, y);
                var n = footprints.Count(fp => Inside(fp, q));
                if (n > 1 || (InRing(q) && n != 1)) { if (failures.Count < 5) failures.Add($"({x:0.000},{y:0.000}): {n}"); }
            }
        Assert.Empty(failures);
    }
}
