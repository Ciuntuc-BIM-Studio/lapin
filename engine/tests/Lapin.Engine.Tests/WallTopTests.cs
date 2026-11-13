using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A wall's top (the forum's topic #45, a tester's report): a wall goes up to the storey above (the default for a new
/// one), to a storey named, or stands at a height of its own (unconnected); its height is worked out on every ask, so a
/// storey raised, lowered or deleted, a copy to other storeys and an IFC round trip all keep it where it should be.
/// </summary>
public class WallTopTests
{
    /// <summary>A ground floor at ±0.00 (3 m) and a first floor at 3.00 (3 m).</summary>
    static (Fixture F, string Ground, string First) TwoStoreys()
    {
        var f = new Fixture(new ArchitectureModule());
        var ground = f.Storey;
        f.Run("storey.update", new JsonObject { ["id"] = ground, ["elevation"] = 0.0, ["height"] = 3.0 });
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        return (f, ground, first);
    }

    static string Wall(Fixture f, string storey, JsonObject? extra = null)
    {
        var a = new JsonObject { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addWall", a)!["id"]!;
    }

    static double Height(Fixture f, string wall) => Walls.HeightOf(f.Model, f.Model.Get(wall)!);
    static double Top(Fixture f, string wall) => f.Bounds(wall).Max.Z;

    [Fact]
    public void A_new_wall_goes_up_to_the_storey_above_and_follows_it_when_it_moves()
    {
        var (f, ground, first) = TwoStoreys();
        var w = Wall(f, ground);
        Assert.Equal(WallTop.Above, f.Model.Get(w)!.Str(WallTop.Key));
        Assert.Equal(3.0, Height(f, w), 9);
        Assert.Equal(3.0, Top(f, w), 6);

        // The first floor raised: the wall rises with it; lowered, it comes down.
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3.4, ["keepTop"] = true });
        Assert.Equal(3.4, Height(f, w), 9);
        Assert.Equal(3.4, Top(f, w), 6);
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 2.8, ["keepTop"] = true });
        Assert.Equal(2.8, Top(f, w), 6);
        // Undone, it is where it was.
        f.Session.Undo();
        Assert.Equal(3.4, Top(f, w), 6);

        // The ground floor lowered: the wall's base goes down with it, its top stays at the first floor.
        f.Run("storey.update", new JsonObject { ["id"] = ground, ["elevation"] = -0.5, ["keepTop"] = true });
        Assert.Equal(3.9, Height(f, w), 9);
        var b = f.Bounds(w);
        Assert.Equal(-0.5, b.Min.Z, 6);
        Assert.Equal(3.4, b.Max.Z, 6);
        // Its quantities measure the height it stands to.
        var qto = f.Session.Quantities(w).First(q => q.Name == "Qto_WallBaseQuantities");
        Assert.Equal(3.9, qto.Quantities.First(q => q.Name == "Height").Value, 6);
    }

    [Fact]
    public void On_the_topmost_storey_a_wall_goes_to_the_storeys_own_top()
    {
        var (f, _, first) = TwoStoreys();
        var w = Wall(f, first);
        Assert.Equal(3.0, Height(f, w), 9);
        f.Run("storey.update", new JsonObject { ["id"] = first, ["height"] = 2.7 });
        Assert.Equal(2.7, Height(f, w), 9);
        Assert.Equal(3.0 + 2.7, Top(f, w), 6);
        // A storey added above: it goes to that one's level.
        f.Run("storey.create", new JsonObject { ["name"] = "Attic", ["elevation"] = 6.2, ["height"] = 2.5 });
        Assert.Equal(3.2, Height(f, w), 9);
    }

    [Fact]
    public void A_wall_to_a_storey_two_up_with_an_offset()
    {
        var (f, ground, first) = TwoStoreys();
        var second = (string)f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6.0, ["height"] = 3.0 })!["id"]!;
        var w = Wall(f, ground, new JsonObject { ["top"] = second, ["topOffset"] = -0.3 });
        Assert.Equal(5.7, Height(f, w), 9);
        Assert.Equal(5.7, Top(f, w), 6);
        // The offset changed and the base raised: from the base to the second floor less the offset.
        f.Run("architecture.setWallTop", new JsonObject { ["ids"] = new JsonArray(w), ["topOffset"] = 0.2 });
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(w), ["attrs"] = new JsonObject { ["baseOffset"] = 0.5 } });
        Assert.Equal(5.7, Height(f, w), 9);
        Assert.Equal(6.2, Top(f, w), 6);
        // Its first floor moved means nothing to it.
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3.5 });
        Assert.Equal(6.2, Top(f, w), 6);
    }

    [Fact]
    public void A_top_at_or_below_the_base_is_refused_and_one_that_comes_so_is_flagged()
    {
        var (f, ground, first) = TwoStoreys();
        var w = Wall(f, first);
        // To the ground floor from the first: refused, nothing changed.
        Assert.Throws<CommandException>(() => f.Run("architecture.setWallTop", new JsonObject { ["ids"] = new JsonArray(w), ["top"] = ground }));
        Assert.Equal(WallTop.Above, f.Model.Get(w)!.Str(WallTop.Key));
        var low = Wall(f, ground, new JsonObject { ["top"] = first, ["topOffset"] = -0.1 });
        // The first floor brought down to the ground floor's level: never a negative body, its own height, said so.
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 0.05 });
        Assert.Equal(2.9, Height(f, low), 9);
        Assert.Contains(f.Session.Geometry(low)!.Diagnostics!, d => d.Contains("at or below its base", StringComparison.Ordinal));
    }

    [Fact]
    public void Unconnected_walls_keep_their_height_and_a_height_given_makes_one()
    {
        var (f, ground, first) = TwoStoreys();
        var w = Wall(f, ground, new JsonObject { ["height"] = 2.5 });
        Assert.Null(f.Model.Get(w)!.Str(WallTop.Key));
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3.6 });
        Assert.Equal(2.5, Height(f, w), 9);
        // A wall disconnected keeps the height it had then.
        var c = Wall(f, ground);
        Assert.Equal(3.6, Height(f, c), 9);
        f.Run("architecture.setWallTop", new JsonObject { ["ids"] = new JsonArray(c), ["top"] = null });
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3.0 });
        Assert.Equal(3.6, Height(f, c), 9);
        // Connected again, then a height typed on it: unconnected at that height.
        f.Run("architecture.setWallTop", new JsonObject { ["ids"] = new JsonArray(c), ["top"] = WallTop.Above });
        Assert.Equal(3.0, Height(f, c), 9);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = c, ["height"] = 2.2 });
        Assert.Null(f.Model.Get(c)!.Str(WallTop.Key));
        Assert.Equal(2.2, Height(f, c), 9);
    }

    [Fact]
    public void A_type_gives_its_walls_its_top()
    {
        var (f, ground, _) = TwoStoreys();
        var parapet = f.Type("Parapet", ("Brick", 0.25, true));
        f.Run("architecture.setWallTypeTop", new JsonObject { ["id"] = parapet, ["top"] = WallTop.Unconnected, ["height"] = 1.1 });
        var w = Wall(f, ground, new JsonObject { ["typeId"] = parapet });
        Assert.Null(f.Model.Get(w)!.Str(WallTop.Key));
        Assert.Equal(1.1, Height(f, w), 9);
        var under = f.Type("Under the beams", ("Brick", 0.25, true));
        f.Run("architecture.setWallTypeTop", new JsonObject { ["id"] = under, ["top"] = WallTop.Above, ["topOffset"] = -0.45 });
        var u = Wall(f, ground, new JsonObject { ["typeId"] = under });
        Assert.Equal(WallTop.Above, f.Model.Get(u)!.Str(WallTop.Key));
        Assert.Equal(2.55, Height(f, u), 9);
        // What the command asks wins over the type's.
        var v = Wall(f, ground, new JsonObject { ["typeId"] = under, ["topOffset"] = 0.0 });
        Assert.Equal(3.0, Height(f, v), 9);
    }

    [Fact]
    public void Several_walls_take_a_top_in_one_step()
    {
        var (f, ground, first) = TwoStoreys();
        var a = Wall(f, ground, new JsonObject { ["height"] = 2.0 });
        var b = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = 0, ["y1"] = 3, ["x2"] = 5, ["y2"] = 3, ["height"] = 2.4 })!["id"]!;
        f.Run("architecture.setWallTop", new JsonObject { ["ids"] = new JsonArray(a, b), ["top"] = first, ["topOffset"] = -0.25 });
        Assert.All(new[] { a, b }, w => Assert.Equal(2.75, Height(f, w), 9));
        f.Session.Undo();
        Assert.Equal(2.0, Height(f, a), 9);
        Assert.Equal(2.4, Height(f, b), 9);
    }

    [Fact]
    public void A_storey_deleted_lets_the_walls_going_to_it_stand_at_their_height()
    {
        var (f, ground, first) = TwoStoreys();
        var second = (string)f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6.0, ["height"] = 3.0 })!["id"]!;
        var named = Wall(f, ground, new JsonObject { ["top"] = second });
        var above = Wall(f, ground);
        // The first floor goes: the wall to the storey above (it) stands at 3 m, unconnected, rather than jump to the second.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(first) });
        Assert.Null(f.Model.Get(above)!.Str(WallTop.Key));
        Assert.Equal(3.0, Height(f, above), 9);
        Assert.Equal(second, f.Model.Get(named)!.Str(WallTop.Key));
        // The second floor goes: the wall to it is unconnected at 6 m.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(second) });
        Assert.Null(f.Model.Get(named)!.Str(WallTop.Key));
        Assert.Equal(6.0, Height(f, named), 9);
        // One step: undone, it goes to it again.
        f.Session.Undo();
        Assert.Equal(second, f.Model.Get(named)!.Str(WallTop.Key));
        f.Session.Undo();
        Assert.Equal(WallTop.Above, f.Model.Get(above)!.Str(WallTop.Key));
    }

    [Fact]
    public void A_copy_to_another_storey_goes_as_many_storeys_up()
    {
        var (f, ground, first) = TwoStoreys();
        var second = (string)f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6.0, ["height"] = 3.2 })!["id"]!;
        var above = Wall(f, ground);
        var named = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = 0, ["y1"] = 4, ["x2"] = 5, ["y2"] = 4, ["top"] = first, ["topOffset"] = -0.2 })!["id"]!;
        f.Run("element.copyToStoreys", new JsonObject { ["ids"] = new JsonArray(above, named), ["storeyIds"] = new JsonArray(first, second) });
        Entity On(string storey, double y) => f.Model.OfClass(IfcClass.Wall).Single(w => Spatial.StoreyOf(f.Model, w.Id)?.Id == storey && Math.Abs(Walls.Start(w).Y - y) < 1e-6);
        // On the first floor: to the storey above (the second), and the named one to the second (one up).
        Assert.Equal(WallTop.Above, On(first, 0).Str(WallTop.Key));
        Assert.Equal(3.0, Walls.HeightOf(f.Model, On(first, 0)), 9);
        Assert.Equal(second, On(first, 4).Str(WallTop.Key));
        Assert.Equal(2.8, Walls.HeightOf(f.Model, On(first, 4)), 9);
        // On the second floor (the topmost): its own top; the named one has no storey one up, so it stands at the original's height.
        Assert.Equal(3.2, Walls.HeightOf(f.Model, On(second, 0)), 9);
        Assert.Null(On(second, 4).Str(WallTop.Key));
        Assert.Equal(2.8, Walls.HeightOf(f.Model, On(second, 4)), 9);
    }

    [Fact]
    public void Lapins_own_IFC_brings_the_top_back_and_writes_the_height_worked_out()
    {
        var (f, ground, first) = TwoStoreys();
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3.3, ["keepTop"] = true });
        var w = Wall(f, ground, new JsonObject { ["top"] = first, ["topOffset"] = -0.1 });
        var free = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = 0, ["y1"] = 4, ["x2"] = 5, ["y2"] = 4, ["height"] = 2.1 })!["id"]!;
        var path = Path.Combine(Path.GetTempPath(), $"lapin-wall-top-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var text = File.ReadAllText(path);
            Assert.Contains("'top',$,IFCLABEL('" + first + "')", text);
            // The quantities say the height it stands to.
            Assert.Contains("IFCQUANTITYLENGTH('Height',$,$,3.2,$)", text);

            var g = new Fixture(new ArchitectureModule());
            IfcImport.Run(g.Session, path, null, out _);
            var back = g.Model.Get(w)!;
            Assert.Equal(first, back.Str(WallTop.Key));
            Assert.Equal(-0.1, back.Num(WallTop.OffsetKey), 9);
            Assert.Equal(3.2, Walls.HeightOf(g.Model, back), 6);
            Assert.Null(g.Model.Get(free)!.Str(WallTop.Key));
            Assert.Equal(2.1, Walls.HeightOf(g.Model, g.Model.Get(free)!), 6);
            // Still a constraint: the storey moved, the wall follows.
            g.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3.6, ["keepTop"] = true });
            Assert.Equal(3.5, Walls.HeightOf(g.Model, g.Model.Get(w)!), 6);
        }
        finally { File.Delete(path); }
    }
}
