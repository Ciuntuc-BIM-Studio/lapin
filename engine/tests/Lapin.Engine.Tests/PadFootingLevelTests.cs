using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A pad footing under a column rises to it (decision 507): its top on the column's base (its storey's elevation and its base
/// offset), its bottom its steps' heights under that, worked out on every ask, so it follows the column; a pad with no column
/// over it keeps its storey's elevation with its type's bottom under it; a pad may have its own top instead. Pad footings
/// under columns puts one under each column that has none, in the infrastructure storey.
/// </summary>
public class PadFootingLevelTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());

    static string Column(Fixture f, string storey, double x, double y, string? type = null, double rotation = 0)
    {
        var a = new JsonObject { ["storeyId"] = storey, ["x"] = x, ["y"] = y, ["rotation"] = rotation };
        if (type != null) a["typeId"] = type;
        return (string)f.Run("structure.addColumn", a)!["id"]!;
    }

    static string Pad(Fixture f, string storey, double x, double y) =>
        (string)f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = storey, ["x"] = x, ["y"] = y })!["id"]!;

    static (double Bottom, double Top) Z(Fixture f, string id)
    {
        var (min, max) = f.Bounds(id);
        return (Math.Round(min.Z, 6), Math.Round(max.Z, 6));
    }

    static string Storey(Fixture f, string name, double elevation) =>
        (string)f.Run("storey.create", new JsonObject { ["name"] = name, ["elevation"] = elevation, ["height"] = 3.0 })!["id"]!;

    [Fact]
    public void A_pad_under_a_column_has_its_top_on_the_columns_base()
    {
        var f = F();
        var ground = f.Storey;
        var c = Column(f, ground, 2, 3);
        var pad = Pad(f, ground, 2, 3);
        // The default steps 0.5 + 0.4 under the column standing on ±0.00: no gap.
        Assert.Equal((-0.9, 0.0), Z(f, pad));
        var level = PadFootings.Level(f.Model.Get(pad)!, f.Model)!;
        Assert.Equal(("column", c), (level.Rule, level.Column!.Id));

        // Its base offset, then moved off the pad: the pad follows, then keeps its storey's rule (the type's bottom, −1.8).
        f.Run("element.update", new JsonObject { ["id"] = c, ["attrs"] = new JsonObject { ["baseOffset"] = -0.3 } });
        Assert.Equal((-1.2, -0.3), Z(f, pad));
        f.Run("structure.updateColumn", new JsonObject { ["id"] = c, ["x"] = 5 });
        Assert.Equal((-1.8, -0.9), Z(f, pad));
        Assert.Equal("storey", PadFootings.Level(f.Model.Get(pad)!, f.Model)!.Rule);
        f.Run("structure.updateColumn", new JsonObject { ["id"] = c, ["x"] = 2 });
        Assert.Equal((-1.2, -0.3), Z(f, pad));
    }

    [Fact]
    public void A_pad_in_a_foundation_plan_rises_to_the_column_above_and_follows_its_storey()
    {
        var f = F();
        var ground = f.Storey;
        var foundations = Storey(f, "Foundations", -1.5);
        var first = Storey(f, "First floor", 3);
        var c = Column(f, ground, 0, 0);
        // Drawn on the Foundations level (−1.50): not −3.30 under it, but under the column standing on ±0.00.
        var pad = Pad(f, foundations, 0, 0);
        Assert.Equal((-0.9, 0.0), Z(f, pad));
        // The column moved to the first floor: the pad goes up with it, whatever storey holds the pad.
        f.Run("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(c), ["storeyId"] = first });
        Assert.Equal((2.1, 3.0), Z(f, pad));
        // The storey's elevation changed: the pad follows the column's base again.
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3.2 });
        Assert.Equal(3.2, Z(f, pad).Top, 6);
    }

    [Fact]
    public void Of_columns_stacked_over_a_pad_it_takes_the_lowest_and_of_two_as_low_the_nearest()
    {
        var f = F();
        var ground = f.Storey;
        var first = Storey(f, "First floor", 3);
        Column(f, first, 0, 0);
        var low = Column(f, ground, 0, 0);
        var pad = Pad(f, first, 0, 0);
        Assert.Equal(low, PadFootings.Level(f.Model.Get(pad)!, f.Model)!.Column!.Id);
        Assert.Equal(0, Z(f, pad).Top, 6);

        // Two columns of 60 × 60 on the same level overlapping over the pad: the one whose point is nearer the pad's.
        var wide = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.6, ["depth"] = 0.6 } })!["id"]!;
        var a = Column(f, ground, 10, 0, wide);
        var b = Column(f, ground, 10.4, 0, wide);
        var between = Pad(f, ground, 10.25, 0);
        Assert.Equal(b, PadFootings.Level(f.Model.Get(between)!, f.Model)!.Column!.Id);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Under_a_column_is_within_its_section_turned_with_it_to_a_millimetre()
    {
        var f = F();
        var ground = f.Storey;
        // A blade column 30 × 100 turned a quarter: it reaches 0.5 m along y, 0.15 along x.
        var blade = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.3, ["depth"] = 1.0 } })!["id"]!;
        Column(f, ground, 0, 0, blade, 90);
        var outline = PadFootings.ColumnFootprint(f.Model, f.Model.OfClass(IfcClass.Column).Single());
        var reach = (outline.Max(p => p.X), outline.Max(p => p.Y));
        var (along, across) = reach.Item1 > reach.Item2 ? (new Vec2(reach.Item1, 0), new Vec2(0, reach.Item2)) : (new Vec2(0, reach.Item2), new Vec2(reach.Item1, 0));
        Assert.NotNull(PadFootings.ColumnAt(f.Model, along * 0.9));
        Assert.Null(PadFootings.ColumnAt(f.Model, across * 2));
        // On the outline, or within a millimetre of it, is under; two millimetres out is not.
        Assert.NotNull(PadFootings.ColumnAt(f.Model, along + along.Normalized() * 0.0009));
        Assert.Null(PadFootings.ColumnAt(f.Model, along + along.Normalized() * 0.002));
    }

    [Fact]
    public void A_pads_own_top_unlinks_it_and_following_again_brings_it_back_each_one_step()
    {
        var f = F();
        var ground = f.Storey;
        var c = Column(f, ground, 0, 0);
        var pad = Pad(f, ground, 0, 0);
        f.Run("structure.setPadLevel", new JsonObject { ["ids"] = new JsonArray(pad), ["top"] = -0.5 });
        Assert.Equal((-1.4, -0.5), Z(f, pad));
        Assert.Equal("own", PadFootings.Level(f.Model.Get(pad)!, f.Model)!.Rule);
        // The column's base moving leaves it.
        f.Run("element.update", new JsonObject { ["id"] = c, ["attrs"] = new JsonObject { ["baseOffset"] = -0.2 } });
        Assert.Equal(-0.5, Z(f, pad).Top, 6);
        f.Run("structure.setPadLevel", new JsonObject { ["id"] = pad, ["follow"] = true });
        Assert.Equal((-1.1, -0.2), Z(f, pad));
        f.Session.Undo();
        Assert.Equal(-0.5, Z(f, pad).Top, 6);
        Assert.Throws<CommandException>(() => f.Run("structure.setPadLevel", new JsonObject { ["id"] = c, ["top"] = 0 }));
    }

    [Fact]
    public void Pad_footings_under_columns_puts_one_under_each_column_without_one_in_the_infrastructure_storey()
    {
        var f = F();
        var ground = f.Storey;
        var foundations = Storey(f, "Foundations", -1.5);
        var cols = new[] { Column(f, ground, 0, 0), Column(f, ground, 6, 0, rotation: 30), Column(f, ground, 6, 5) };
        // One has its pad already.
        Pad(f, ground, 6, 5);
        var made = ((JsonArray)f.Run("structure.padsUnderColumns", new JsonObject { ["storeyId"] = ground })!["ids"]!).Select(n => (string)n!).ToList();
        Assert.Equal(2, made.Count);
        foreach (var id in made)
        {
            var pad = f.Model.Get(id)!;
            Assert.Equal("PAD_FOOTING", pad.PredefinedType);
            // The highest storey at or under its bottom (−0.90): the Foundations at −1.50.
            Assert.Equal(foundations, Spatial.StoreyOf(f.Model, id)!.Id);
            Assert.Equal((-0.9, 0.0), Z(f, id));
        }
        var turned = made.Select(f.Model.Get).Single(p => Place(p!) == new Vec2(6, 0))!;
        Assert.Equal(30, turned.Num("rotation"), 9);
        // Again: every column has one.
        Assert.Throws<CommandException>(() => f.Run("structure.padsUnderColumns", new JsonObject { ["storeyId"] = ground }));
        // One undo step takes them all.
        f.Session.Undo();
        Assert.All(made, id => Assert.Null(f.Model.Get(id)));

        // Selected columns only; and from the foundation plan (a storey without columns), the columns of the storey above.
        var one = ((JsonArray)f.Run("structure.padsUnderColumns", new JsonObject { ["ids"] = new JsonArray(cols[0]) })!["ids"]!).Single();
        Assert.Equal(new Vec2(0, 0), Place(f.Model.Get((string)one!)!));
        var rest = (JsonArray)f.Run("structure.padsUnderColumns", new JsonObject { ["storeyId"] = foundations })!["ids"]!;
        Assert.Single(rest);
    }

    [Fact]
    public void Without_a_storey_under_the_pads_bottom_they_go_in_the_lowest()
    {
        var f = F();
        var ground = f.Storey;
        Storey(f, "First floor", 3);
        Column(f, ground, 0, 0);
        var id = (string)((JsonArray)f.Run("structure.padsUnderColumns", new JsonObject { ["storeyId"] = ground })!["ids"]!)[0]!;
        Assert.Equal(ground, Spatial.StoreyOf(f.Model, id)!.Id);
        Assert.Equal((-0.9, 0.0), Z(f, id));
    }

    [Fact]
    public void The_analytical_support_of_a_pad_under_a_column_is_at_the_columns_foot()
    {
        var f = F();
        var ground = f.Storey;
        Column(f, ground, 0, 0);
        Pad(f, ground, 0, 0);
        var into = new AnalyticalContribution();
        new StructureAnalytical().Contribute(f.Model, into);
        var support = Assert.Single(into.Supports);
        Assert.Equal((-0.9, 0.0), (Math.Round(support.Bottom, 6), Math.Round(support.Top, 6)));
    }

    static Vec2 Place(Entity e) => new(Json.Number(e.Attrs["at"]![0]) ?? 0, Json.Number(e.Attrs["at"]![1]) ?? 0);
}
