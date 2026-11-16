using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The audit of 15.11.2026, root cause c on the site: a contour interval is 5 cm at least and the levels stop where
/// an interval no longer moves the height (a flat terrain high up with a tiny one looped for ever); a planting's density
/// is the class's 0.1–500 per 100 m², a zone grows <see cref="Plantings.MaxPlants"/> at most, and its spacing check
/// looks only at the plants around (not quadratic), keeping the same plants as before.
/// </summary>
public class CivilLimitsTests
{
    static Fixture Site() => new(new CivilModule());

    [Fact]
    public void A_contour_interval_asked_under_5_cm_is_taken_as_5_cm()
    {
        var f = Site();
        var flat = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 10, ["elevation"] = 1e6, ["interval"] = 1e-30 })!["id"]!;
        Assert.Equal(Terrains.MinInterval, f.Model.Get(flat)!.Num("interval"));
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = flat, ["interval"] = 0.001 });
        Assert.Equal(Terrains.MinInterval, f.Model.Get(flat)!.Num("interval"));
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = flat, ["interval"] = 0.25 });
        Assert.Equal(0.25, f.Model.Get(flat)!.Num("interval"));
        var text = "0 0 100\n10 0 100\n10 10 100\n0 10 100";
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = text, ["interval"] = 0 })!["id"]!;
        Assert.Equal(Terrains.MinInterval, f.Model.Get(t)!.Num("interval"));
    }

    [Fact]
    public void A_flat_terrain_high_up_with_a_stored_tiny_interval_ends_its_levels_and_draws()
    {
        var f = Site();
        var flat = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 10, ["elevation"] = 1e6 })!["id"]!;
        var e = f.Model.Get(flat)!;
        var terrain = Terrains.Of(e);
        var watch = Stopwatch.StartNew();
        Assert.InRange(terrain.Levels(1e-20).Take(10_000).Count(), 1, 9_999);
        Assert.InRange(terrain.Levels(1e-12).Take(1_000_000).Count(), 1, 999_999);
        // An older model's interval, never clamped: its plan is still drawn.
        var attrs = e.CloneAttrs();
        attrs["interval"] = 1e-20;
        var stale = e.With(attrs: attrs);
        Assert.NotNull(Terrains.Geometry(stale, f.Model, null));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20));
        // Sane intervals as before.
        Assert.Equal(new[] { 0.0, 0.5, 1.0 }, Terrains.Of(f.Model.Get((string)f.Run("civil.addTerrain", new JsonObject { ["text"] = "0 0 0\n10 0 1\n10 10 1\n0 10 0" })!["id"]!)!).Levels(0.5));
    }

    [Fact]
    public void A_planting_s_density_is_the_class_s_range()
    {
        var f = Site();
        JsonArray Square(double s) => new(new JsonArray(0, 0), new JsonArray(s, 0), new JsonArray(s, s), new JsonArray(0, s));
        // A small zone, so its 500 per 100 m² stay few to build.
        Assert.Throws<CommandException>(() => f.Run("civil.addPlanting", new JsonObject { ["points"] = Square(30), ["density"] = 1e9 }));
        Assert.Throws<CommandException>(() => f.Run("civil.addPlanting", new JsonObject { ["points"] = Square(30), ["density"] = 0 }));
        var zone = (string)f.Run("civil.addPlanting", new JsonObject { ["points"] = Square(30), ["density"] = "NaN", ["seed"] = 7 })!["id"]!;
        Assert.Equal(3, f.Model.Get(zone)!.Num("density"));
        Assert.Throws<CommandException>(() => f.Run("civil.updatePlanting", new JsonObject { ["id"] = zone, ["density"] = 501 }));
        f.Run("civil.updatePlanting", new JsonObject { ["id"] = zone, ["density"] = 50 });
        Assert.Equal(50, f.Model.Get(zone)!.Num("density"));
    }

    [Fact]
    public void A_vast_dense_zone_grows_a_bounded_number_of_plants_quickly_and_kept_apart()
    {
        // The zone read as it stands in the model, its plants laid without building their bodies.
        var f = Site();
        var square = new JsonArray(new JsonArray(0, 0), new JsonArray(2000, 0), new JsonArray(2000, 2000), new JsonArray(0, 2000));
        var e = new Entity("zone", Plantings.Class, "Planting", new JsonObject { ["contour"] = Terrains.ContourJson(Args.Points(new JsonObject { ["points"] = square })), ["density"] = 500, ["seed"] = 3 }, "VEGETATION");
        var watch = Stopwatch.StartNew();
        var plants = Plantings.Plants(f.Model, e);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"{watch.Elapsed}");
        Assert.Equal(Plantings.MaxPlants, plants.Count);
        // The spacing kept, as the check against every plant would.
        var trees = plants.Where(p => p.Species.Type != "grass").ToList();
        var byCell = trees.GroupBy(p => ((int)Math.Floor(p.At.X / 10), (int)Math.Floor(p.At.Y / 10))).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var p in trees)
        {
            var (cx, cy) = ((int)Math.Floor(p.At.X / 10), (int)Math.Floor(p.At.Y / 10));
            for (var i = cx - 1; i <= cx + 1; i++)
                for (var j = cy - 1; j <= cy + 1; j++)
                    foreach (var q in byCell.GetValueOrDefault((i, j)) ?? [])
                        if (!ReferenceEquals(p, q)) Assert.True((q.At - p.At).Length >= Math.Min(q.Species.Crown, p.Species.Crown) * 0.4 - 1e-9);
        }
    }
}
