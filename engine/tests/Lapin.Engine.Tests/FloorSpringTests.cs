using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Ifc;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A frameless glass door on a floor spring (a forum user's request, topic #28): the spring's box let into the floor under the
/// leaf's pivot, its cover plate drawn in plan, a top pivot and patch fittings (or a heel strap at the bottom), the pivot set in
/// from the leaf's edge, single or double action, a hold-open angle; the plan swings the leaf about its pivot, the schedule
/// names the hanging, IFC carries it in a set of Lapin's.
/// </summary>
public class FloorSpringTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-floorspring").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JoinerySpec Door(string op = "double-acting-left", double width = 0.9, double height = 2.1, JoineryFrameless? fl = null) =>
        JoinerySpec.DefaultDoor("Glass door") with
        {
            Width = width,
            Height = height,
            Columns = [1],
            Panels = [new(0, 0, op)],
            Frameless = fl ?? new JoineryFrameless("floor-spring"),
        };

    static double Len(JoineryBox b) => b.Max.X - b.Min.X;

    [Fact]
    public void The_spring_lies_in_the_floor_under_the_pivot_set_in_from_the_leafs_edge()
    {
        var s = Door();
        Assert.Null(Joinery.Problem(s));
        var boxes = Joinery.Boxes(s);
        var glass = Assert.Single(boxes, b => b.Role == "glass");
        var plane = (glass.Min.Y + glass.Max.Y) / 2;
        // 900 × 2100 in 12 mm glass: 0.892 × 2.088 × 0.012 × 2500 = 55.9 kg, the 60 kg model.
        Assert.Equal(0.892 * 2.088 * 0.012 * 2500, Frameless.LeafMass(s, Joinery.Normalised(s)[0]), 9);
        var model = Frameless.SpringOf(s)!;
        Assert.Equal("60", model.Id);
        var pivot = glass.Min.X + 0.055;
        Assert.Equal(pivot, Frameless.PivotX(s, Joinery.Normalised(s)[0]), 9);
        // The body under the floor finish, below its cover plate, centred on the glass, the pivot over it near its hinge end.
        var under = boxes.Where(b => b.Underfloor).ToList();
        Assert.Equal(2, under.Count);
        var cover = under.Single(b => Math.Abs(b.Max.Z) < 1e-9);
        var body = under.Single(b => b != cover);
        Assert.Equal(-Frameless.CoverPlate, cover.Min.Z, 9);
        Assert.Equal(-Frameless.CoverPlate, body.Max.Z, 9);
        Assert.Equal(-Frameless.CoverPlate - model.Depth, body.Min.Z, 9);
        Assert.Equal(model.Length, Len(body), 9);
        Assert.Equal(model.Width, body.Max.Y - body.Min.Y, 9);
        Assert.Equal(plane, (body.Min.Y + body.Max.Y) / 2, 9);
        Assert.True(body.Min.X < pivot && pivot < body.Min.X + 0.06, "the spindle near the box's hinge end");
        Assert.True(Len(cover) > Len(body) && cover.Max.Y - cover.Min.Y > body.Max.Y - body.Min.Y);
        Assert.All(under, b => Assert.False(b.Leaf));
        // A top pivot in the gap over the leaf, on the same axis.
        var top = Assert.Single(boxes, b => b.Role == "hardware" && b.Min.Z >= glass.Max.Z - 1e-9 && !b.Leaf);
        Assert.Equal(pivot, (top.Min.X + top.Max.X) / 2, 9);
        Assert.Equal(2.1, top.Max.Z, 9);
        // Patch fittings at the top and bottom of the hinge side, a lock patch opposite.
        Assert.Equal(3, boxes.Count(b => b.Role == "hardware" && b.Leaf && Math.Abs(Len(b) - 0.18) < 1e-9));
    }

    [Fact]
    public void A_heel_strap_takes_the_bottom_patch_fittings_place()
    {
        var boxes = Joinery.Boxes(Door(fl: new JoineryFrameless("floor-spring", Bottom: "strap")));
        Assert.Equal(2, boxes.Count(b => b.Role == "hardware" && b.Leaf && Math.Abs(Len(b) - 0.18) < 1e-9));
        var strap = Assert.Single(boxes, b => b.Role == "hardware" && b.Leaf && Math.Abs(Len(b) - Frameless.StrapLength) < 1e-9);
        Assert.Equal(0.008, strap.Min.Z, 9);
    }

    [Fact]
    public void The_model_is_the_lightest_that_carries_the_leaf_or_the_one_chosen()
    {
        // 1000 × 2400: 71 kg, the 100 kg model; 1200 × 2700: 96 kg but 1192 mm wide, the 150 kg model.
        Assert.Equal("100", Frameless.SpringOf(Door(width: 1.0, height: 2.4))!.Id);
        Assert.Equal("150", Frameless.SpringOf(Door(width: 1.2, height: 2.7))!.Id);
        var light = Door(width: 1.0, height: 2.4, fl: new JoineryFrameless("floor-spring", Spring: "60"));
        Assert.Contains("60 kg", Joinery.Problem(light));
        Assert.Contains("not a floor spring", Joinery.Problem(Door(fl: new JoineryFrameless("floor-spring", Spring: "900"))));
        Assert.Contains("30 to 150 mm", Joinery.Problem(Door(fl: new JoineryFrameless("floor-spring", Pivot: 0.02))));
        Assert.Contains("30 to 150 mm", Joinery.Problem(Door(fl: new JoineryFrameless("floor-spring", Pivot: 0.2))));
        // On a narrow leaf the pivot stays within a third of its width.
        Assert.Contains("third", Joinery.Problem(Door(width: 0.4, fl: new JoineryFrameless("floor-spring", Pivot: 0.14))));
        Assert.Contains("hold", Joinery.Problem(Door(fl: new JoineryFrameless("floor-spring", HoldOpen: 30))));
        Assert.Null(Joinery.Problem(Door(fl: new JoineryFrameless("floor-spring", HoldOpen: 90))));
        Assert.Contains("bottom", Joinery.Problem(Door(fl: new JoineryFrameless("floor-spring", Bottom: "clamp"))));
        // A leaf too heavy for any.
        Assert.Contains("heavier", Joinery.Problem(Door(width: 1.6, height: 3.5, fl: new JoineryFrameless("floor-spring", Glass: 0.025))));
    }

    [Fact]
    public void The_plan_swings_the_leaf_about_its_pivot_both_ways_and_draws_the_cover_plate()
    {
        var s = Door();
        var p = Joinery.Normalised(s)[0];
        var plan = JoinerySymbols.Plan(s);
        var r = Frameless.PaneRect(s, p);
        var w = r.X1 - r.X0;
        var pivot = Frameless.PivotX(s, p);
        var y = s.Frame.Offset;
        (double X0, double X1, double Y0, double Y1) Box(JStroke k) => (k.Pts.Min(q => q.X), k.Pts.Max(q => q.X), k.Pts.Min(q => q.Y), k.Pts.Max(q => q.Y));
        // The open leaf: the glass across the pivot, its short end 55 mm past it, the rest into the room.
        var leaf = Assert.Single(plan, k => k.Weight == "cut" && Math.Abs(Box(k).X1 - Box(k).X0 - 0.012) < 1e-9);
        Assert.Equal(pivot, (Box(leaf).X0 + Box(leaf).X1) / 2, 9);
        Assert.Equal(y + 0.055, Box(leaf).Y1, 9);
        Assert.Equal(y - (w - 0.055), Box(leaf).Y0, 9);
        // Two swings of the leaf's long side about the pivot, the other way dashed.
        var arcs = plan.Where(k => !k.Closed && k.Pts.Count > 4).ToList();
        Assert.Equal(2, arcs.Count);
        Assert.All(arcs, a => Assert.All(a.Pts, q => Assert.Equal(w - 0.055, Math.Sqrt((q.X - pivot) * (q.X - pivot) + (q.Y - y) * (q.Y - y)), 6)));
        Assert.Single(arcs, a => a.Dashed);
        Assert.Contains(arcs, a => a.Pts.All(q => q.Y >= y - 1e-9));
        // The cover plate seen on the floor.
        var model = Frameless.SpringOf(s)!;
        Assert.Contains(plan, k => k.Closed && k.Weight == "thin" && Math.Abs(Box(k).X1 - Box(k).X0 - (model.Length + 2 * Frameless.CoverMargin)) < 1e-9);
        // Single action: one swing.
        Assert.Single(JoinerySymbols.Plan(Door("swing-left")), k => !k.Closed && k.Pts.Count > 4);
        // The elevation: the spring dashed under the floor, the pivot's axis dashed up the leaf.
        var elevation = JoinerySymbols.Elevation(s);
        Assert.Contains(elevation, k => k.Dashed && k.Closed && k.Pts.Max(q => q.Y) <= 1e-9);
        Assert.Contains(elevation, k => k.Dashed && !k.Closed && k.Pts.All(q => Math.Abs(q.X - pivot) < 1e-9));
    }

    [Fact]
    public void The_schedule_names_the_hanging_and_IFC_carries_it()
    {
        var s = Door() with { Frameless = new JoineryFrameless("floor-spring", HoldOpen: 90, Bottom: "strap") };
        Assert.Equal("floor spring 60 kg (EN 1–3), double action, pivot 55 mm, heel strap, hold-open 90°", ScheduleLayout.HangingText(s));
        Assert.Equal("pivots and patch fittings", ScheduleLayout.HangingText(Door("swing-left", fl: new JoineryFrameless())));
        Assert.Null(ScheduleLayout.HangingText(JoinerySpec.DefaultDoor()));
        Assert.Equal("DOUBLE_SWING_LEFT", JoineryIfc.Of(s).Operation);
        Assert.Equal("SINGLE_SWING_RIGHT", JoineryIfc.Of(Door("swing-right")).Operation);

        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = s.ToJson() })!["id"]!;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["typeId"] = type })!["id"]!;
        Assert.Equal(s.Frameless, Openings.SpecOf(f.Model, door)?.Frameless);

        var data = Schedules.Joinery(f.Model, "door");
        var entry = data.Entries.Single(e => e.Spec?.Frameless != null);
        var rows = ScheduleLayout.Rows(data, new ScheduleTable(null, [], new Dictionary<string, Dictionary<string, string>>(), null));
        Assert.StartsWith("floor spring 60 kg", rows.Single(x => x.Id == "hanging").Value(entry));

        var path = Path.Combine(_dir, "spring.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Contains("DOUBLE_SWING_LEFT", text);
        Assert.Contains("'Lapin_FloorSpring'", text);
        Assert.Contains("'PivotOffset'", text);
        Assert.Contains("IFCMASSMEASURE(60.)", text);
        Assert.Contains("'HoldOpenAngle'", text);
    }

    [Fact]
    public void A_door_without_a_floor_spring_has_no_set_and_no_hanging_row()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2 });
        var data = Schedules.Joinery(f.Model, "door");
        var rows = ScheduleLayout.Rows(data, new ScheduleTable(null, [], new Dictionary<string, Dictionary<string, string>>(), null));
        Assert.DoesNotContain(rows, x => x.Id == "hanging");
        var path = Path.Combine(_dir, "plain.ifc");
        IfcExport.Write(f.Session, path);
        Assert.DoesNotContain("Lapin_FloorSpring", File.ReadAllText(path));
    }

    [Fact]
    public void The_preset_is_a_frameless_door_on_a_floor_spring()
    {
        var f = new Fixture(new ArchitectureModule());
        var catalog = (JsonObject)f.Session.Catalog.Catalogs["architecture.joinery"]!;
        var spec = JoinerySpec.From(catalog["doorPresets"]!["floor-spring"])!;
        Assert.Null(Joinery.Problem(spec));
        Assert.Equal("floor-spring", spec.Frameless!.Fixing);
        Assert.Equal("frameless-glass", spec.Style);
        Assert.StartsWith("double-acting", Joinery.Normalised(spec)[0].Operation);
        Assert.Equal(["auto", "60", "100", "150", "300"], catalog["floorSprings"]!.AsArray().Select(x => (string)x!["id"]!).Prepend("auto"));
    }
}
