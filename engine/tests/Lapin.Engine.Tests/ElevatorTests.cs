using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Transport;

namespace Lapin.Engine.Tests;

/// <summary>
/// Lifts (docs/plan-elevators.md, testers' reports #54 and #97): every catalogue style builds a closed lift that meets
/// EN 81-20 (and EN 81-70 where it says so); the shaft opens the floors it passes through and follows the lift; landing
/// doors only where it stops; the EN 81 tables; project styles; plans of every storey it reaches; the Lifts schedule;
/// IFC as IfcTransportElement ELEVATOR with Pset_TransportElementCommon, read back native.
/// </summary>
public sealed class ElevatorTests
{
    static Fixture F() => new(new ArchitectureModule(), new TransportModule());

    /// <summary>Four storeys 3 m apart, each with a floor of 10 × 8 m; their ids, lowest first.</summary>
    static List<string> Building(Fixture f, int storeys = 4)
    {
        for (var i = Spatial.Storeys(f.Model).Count(); i < storeys; i++) f.Run("storey.create", new JsonObject { ["height"] = 3.0 });
        var ids = Spatial.Storeys(f.Model).Select(s => s.Id).ToList();
        foreach (var id in ids)
            f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = id, ["points"] = new JsonArray(new JsonArray(-5, -4), new JsonArray(5, -4), new JsonArray(5, 4), new JsonArray(-5, 4)) });
        return ids;
    }

    static string Place(Fixture f, string style, JsonObject? extra = null)
    {
        var a = new JsonObject { ["storeyId"] = Spatial.Storeys(f.Model).First().Id, ["x"] = 0, ["y"] = 0, ["style"] = style };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("transport.place", a)!["id"]!;
    }

    static ElevatorLayout Layout(Fixture f, string id) => ElevatorLayout.Resolve(f.Model, f.Model.Get(id)!, out _)!;

    [Fact]
    public void Every_style_builds_a_closed_lift_that_meets_EN_81()
    {
        foreach (var style in ElevatorStyles.All)
        {
            var f = F();
            var storeys = Building(f, style.Id == "home" ? 2 : 4);
            var id = Place(f, style.Id);
            var e = f.Model.Get(id)!;
            Assert.Equal(TransportModule.Predefined, e.PredefinedType);
            var g = f.Session.Geometry(id)!;
            Assert.Contains(g.Parts, p => p.Key == "shaft" && p.CutInPlan);
            Assert.Contains(g.Parts, p => p.Key == "cabin");
            Assert.Contains(g.Parts, p => p.Key == "landingDoors");
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{style.Id}: {p.Name} is closed"));
            Assert.All(g.Parts, p => Assert.NotNull(p.Material));
            var l = Layout(f, id);
            var s = l.Spec;
            // The style's own numbers, and EN 81-20's: its car no larger than its load allows, its persons within load and area.
            Assert.Equal((double)style.Values["load"]!, s.Load);
            Assert.True(s.CabinArea <= En81.AreaForLoad(s.Load) + 1e-6, $"{style.Id}: car {s.CabinArea} m² for {s.Load} kg");
            Assert.True(s.Persons <= En81.Persons(s.Load, s.CabinArea), $"{style.Id}: {s.Persons} persons");
            Assert.Empty(s.Checks(l.Travel));
            // The shaft holds the car with its clearances; the pit under the lowest stop, the headroom over the top one.
            Assert.True(l.Wi > s.CabinWidth && l.Di > s.CabinDepth);
            Assert.Equal(storeys.Count, l.Stops.Count);
            var (min, max) = f.Bounds(id);
            Assert.InRange(min.Z, l.ZB - s.Pit - 0.31, l.ZB - s.Pit - 0.19);
            Assert.True(max.Z >= l.ZT + s.Headroom, $"{style.Id}: the cap over the headroom");
            // Its type names its kind, load and car.
            var type = TransportModule.TypeOf(f.Model, e)!;
            Assert.StartsWith(style.Label, type.Name);
            Assert.Contains($"{s.Load} kg", type.Name);
            Assert.Equal(type.Name, e.Name);
        }
    }

    [Fact]
    public void The_twelve_kinds_of_the_reports_are_there()
    {
        Assert.Equal(12, ElevatorStyles.All.Count);
        string[] ids = ["residential-small", "passenger-standard", "passenger-medium", "passenger-large", "heavy-duty", "accessible", "hospital", "goods", "hydraulic", "mrl", "panoramic", "home"];
        Assert.Equal(ids, ElevatorStyles.All.Select(s => s.Id));
        Assert.Equal("type2", (string?)ElevatorStyles.Find("accessible")!.Values["accessible"]);
        Assert.Equal("hydraulic", (string?)ElevatorStyles.Find("hydraulic")!.Values["drive"]);
        Assert.True((bool)ElevatorStyles.Find("panoramic")!.Values["glass"]!);
    }

    [Fact]
    public void EN_81_relates_load_persons_and_car_area()
    {
        Assert.Equal(1.66, En81.AreaForLoad(630), 6);
        Assert.Equal(2.40, En81.AreaForLoad(1000), 6);
        Assert.Equal(5.16, En81.AreaForLoad(2600), 6);
        // A car of 1.54 m² (1100 × 1400) needs 570 kg at least; 630 kg carries 8 persons (8 need 1.45 m², 9 need 1.59).
        Assert.Equal(570, En81.LoadForArea(1.1 * 1.4));
        Assert.Equal(8, En81.Persons(630, 1.54));
        Assert.Equal(13, En81.Persons(1000, 1.6 * 1.4));
        Assert.Equal(1.45, En81.AreaForPersons(8), 6);
        // Report #97's standard passenger lift, 450 kg in 1100 × 1400: too large a car for its load.
        var big = new ElevatorSpec(new JsonObject { ["load"] = 450, ["cabinWidth"] = 1.1, ["cabinDepth"] = 1.4, ["persons"] = 6 });
        Assert.Contains(big.Checks(6), c => c.Contains("570 kg"));
        // Report #54's hospital car of 1500 × 2300 (3.45 m²) needs 1535 kg: EN 81-20 interpolates between 1500 and 1600 kg.
        Assert.Equal(1535, En81.LoadForArea(1.5 * 2.3));
        // EN 81-70 type 2 in a type 1 car: too small, and its door.
        var small = new ElevatorSpec(new JsonObject { ["load"] = 450, ["cabinWidth"] = 1.0, ["cabinDepth"] = 1.25, ["accessible"] = "type2", ["doorWidth"] = 0.7 });
        Assert.Equal(2, small.Checks(6).Count(c => c.StartsWith("EN 81-70")));
    }

    [Fact]
    public void The_shaft_opens_the_floors_it_passes_through_and_follows_the_lift()
    {
        var f = F();
        var storeys = Building(f);
        // A floor high above the cap is not opened.
        var high = (string)f.Run("storey.create", new JsonObject { ["elevation"] = 30.0, ["height"] = 3.0, ["name"] = "Roof deck" })!["id"]!;
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = high, ["points"] = new JsonArray(new JsonArray(-5, -4), new JsonArray(5, -4), new JsonArray(5, 4), new JsonArray(-5, 4)) });
        var id = Place(f, "passenger-medium", new JsonObject { ["topStoreyId"] = storeys[^1] });
        var openings = TransportModule.OpeningsOf(f.Model, id).ToList();
        Assert.Equal(storeys.Count, openings.Count);
        var hosts = openings.Select(o => Spatial.StoreyOf(f.Model, f.Model.RelatingOf(o.Id, IfcClass.RelVoidsElement)!.Id)!.Id).ToHashSet();
        Assert.True(storeys.All(hosts.Contains));
        Assert.DoesNotContain(high, hosts);
        // Each floor's body has the hole: its volume less by the shaft's footprint.
        var l = Layout(f, id);
        var slab = f.Model.RelatingOf(openings[0].Id, IfcClass.RelVoidsElement)!;
        Assert.Single(Slabs.Holes(f.Model, slab.Id));
        var area = Slabs.HoleContour(f.Model, openings[0]);
        Assert.Equal(l.OuterW * l.OuterD, Math.Abs(Polygon2.SignedArea(area)), 4);

        // Moved, its openings go with it; with a stop fewer, the top floor closes.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(2, 1) });
        var moved = TransportModule.OpeningsOf(f.Model, id).First();
        var c = Slabs.HoleContour(f.Model, moved);
        Assert.Equal(2, c.Average(p => p.X), 4);
        Assert.Equal(1, c.Average(p => p.Y), 4);
        f.Run("transport.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["topStoreyId"] = storeys[1], ["headroom"] = 2.5 } });
        Assert.Equal(2, TransportModule.OpeningsOf(f.Model, id).Count());
        // Deleted, it takes them with it, and its type.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Empty(TransportModule.OpeningsOf(f.Model, id));
        Assert.Empty(f.Model.OfClass(TransportModule.TypeClass));
    }

    [Fact]
    public void Landing_doors_are_only_where_the_lift_stops()
    {
        var f = F();
        var storeys = Building(f);
        var id = Place(f, "passenger-medium", new JsonObject { ["attrs"] = new JsonObject { ["unserved"] = new JsonArray(storeys[1], storeys[2]) } });
        var l = Layout(f, id);
        Assert.Equal([true, false, false, true], l.Stops.Select(s => s.Served));
        var doors = f.Session.Geometry(id)!.Parts.Single(p => p.Key == "landingDoors");
        var levels = Measure.Bounds(doors.Solid)!.Value;
        // Panels at the ground floor and at the top: none between.
        var zs = doors.Solid.Vertices.Select(v => Math.Round(v.Z, 3)).Distinct().Order().ToList();
        Assert.Contains(0.0, zs);
        Assert.Contains(9.0, zs);
        Assert.DoesNotContain(zs, z => z > 2.5 && z < 8.5);
        Assert.Equal(0, levels.Min.Z, 6);
        // The shaft's front wall is whole where it does not stop: a ray through the door's middle at +3.5 m meets it.
        var shaft = f.Session.Geometry(id)!.Parts.Single(p => p.Key == "shaft").Solid;
        double FrontAt(double z) => shaft.Vertices.Count(v => Math.Abs(v.Z - z) < 1e-6);
        Assert.True(FrontAt(2.0) > 0, "the opening's head at the ground floor");
        Assert.Equal(0, FrontAt(5.0));
        // The plans: the car on every stop, the doors only where it stops.
        var served = l.PlanOn(storeys[0]);
        var passed = l.PlanOn(storeys[1]);
        Assert.True(served.Count > passed.Count);
        Assert.Contains(served, i => i.Kind == "dashed");
        Assert.DoesNotContain(passed, i => i.Kind == "dashed");
        Assert.Equal(3, passed.Count(i => i.Kind == "symbol" && !i.Closed));
        // Served again: the doors come back.
        f.Run("transport.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["unserved"] = null } });
        Assert.All(Layout(f, id).Stops, s => Assert.True(s.Served));
    }

    [Fact]
    public void Plans_of_every_storey_it_reaches_draw_its_shaft_cut_and_its_symbols()
    {
        var f = F();
        var storeys = Building(f, 3);
        var id = Place(f, "passenger-medium");
        foreach (var s in storeys)
        {
            var plan = Views.Find(f.Model, "plan:" + s)!;
            var (d, _, problem) = Views.Generate(f.Session.Reader, plan);
            Assert.True(d != null, problem);
            Assert.Contains(d!.Cuts, c => c.Element == id);
            Assert.Contains(d.Lines, x => x.Element == id && x.Role == Lapin.Drawing.LineRole.Projection);
        }
        var section = f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!)!;
        var (ed, _, eproblem) = Views.Generate(f.Session.Reader, section);
        Assert.True(ed != null, eproblem);
        Assert.Contains(ed!.Lines, x => x.Element == id);
    }

    [Fact]
    public void A_mirrored_lift_keeps_its_doors_in_front_and_takes_its_counterweight_across()
    {
        var f = F();
        Building(f, 2);
        var id = Place(f, "passenger-medium");
        Assert.Equal("left", f.Model.Get(id)!.Str("counterweight"));
        var r = f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "mirror", ["a"] = new JsonArray(1.5, -10), ["b"] = new JsonArray(1.5, 10), ["copy"] = true })!;
        var copy = (string)((JsonArray)r["ids"]!)[0]!;
        var c = f.Model.Get(copy)!;
        Assert.Equal("right", c.Str("counterweight"));
        // Mirrored across a north–south line it still faces south.
        Assert.Equal(0, c.Num("rotation"), 6);
        // Its front still faces −y: the door's middle south of the shaft's.
        var l = Layout(f, copy);
        Assert.Equal(3, l.At.X, 6);
        var front = l.W(0, -l.Di / 2);
        Assert.True(front.Y < l.At.Y);
        Assert.Equal(4, TransportModule.OpeningsOf(f.Model, id).Count() + TransportModule.OpeningsOf(f.Model, copy).Count());
    }

    [Fact]
    public void Project_styles_are_saved_followed_renamed_and_deleted()
    {
        var f = F();
        Building(f, 3);
        var a = Place(f, "passenger-medium");
        var b = Place(f, "passenger-medium");
        var r = f.Run("transport.saveStyle", new JsonObject { ["id"] = a, ["name"] = "Block A lift", ["attrs"] = new JsonObject { ["cabinWidth"] = 1.2, ["load"] = 675 } })!;
        var sid = (string)r["styleId"]!;
        Assert.StartsWith(TransportModule.OwnPrefix, sid);
        Assert.Equal(sid, f.Model.Get(a)!.Str("style"));
        Assert.StartsWith("Block A lift 675 kg", f.Model.Get(a)!.Name);
        // Another lift takes it; the style saved again, both follow.
        f.Run("transport.update", new JsonObject { ["id"] = b, ["style"] = sid });
        Assert.Equal(1.2, f.Model.Get(b)!.Num("cabinWidth"), 6);
        f.Run("transport.saveStyle", new JsonObject { ["id"] = a, ["name"] = "Block A lift", ["styleId"] = sid, ["attrs"] = new JsonObject { ["doorWidth"] = 0.9 } });
        Assert.Equal(0.9, f.Model.Get(b)!.Num("doorWidth"), 6);
        Assert.Single(TransportModule.OwnStyles(f.Model));
        f.Run("transport.renameStyle", new JsonObject { ["styleId"] = sid, ["name"] = "Core lift" });
        Assert.StartsWith("Core lift", f.Model.Get(b)!.Name);
        f.Run("transport.deleteStyle", new JsonObject { ["styleId"] = sid });
        Assert.Empty(TransportModule.OwnStyles(f.Model));
        Assert.Null(f.Model.Get(b)!.Str("style"));
        Assert.Equal(0.9, f.Model.Get(b)!.Num("doorWidth"), 6);
        Assert.StartsWith("Medium passenger 675 kg", f.Model.Get(b)!.Name);
    }

    [Fact]
    public void Properties_change_the_drive_and_its_machine_room_pit_and_headroom_follow()
    {
        var f = F();
        Building(f, 3);
        var id = Place(f, "passenger-medium");
        Assert.Equal("top", f.Model.Get(id)!.Str("machineRoom"));
        Assert.Contains(f.Session.Geometry(id)!.Parts, p => p.Key == "machineRoom");
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = new JsonObject { ["drive"] = "hydraulic" } });
        var e = f.Model.Get(id)!;
        Assert.Equal("beside", e.Str("machineRoom"));
        Assert.Equal(ElevatorSpec.SuggestedPit("hydraulic", 1.0), e.Num("pit"), 6);
        Assert.Contains(f.Session.Geometry(id)!.Parts, p => p.Name == "Jack");
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = new JsonObject { ["drive"] = "mrl" } });
        Assert.Equal("none", f.Model.Get(id)!.Str("machineRoom"));
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts, p => p.Key == "machineRoom");
        // The type follows the change.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = new JsonObject { ["load"] = 800, ["cabinWidth"] = 1.35 } });
        Assert.Contains("800 kg, 1350 × 1400", f.Model.Get(id)!.Name);
        Assert.Single(f.Model.OfClass(TransportModule.TypeClass));
        // A shaft of a wall type: its build-up's thickness.
        var wt = f.Type("Shaft 250", ("Reinforced concrete", 0.25, true));
        f.Run("transport.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["shaftWallTypeId"] = wt } });
        Assert.Equal(0.25, Layout(f, id).T, 6);
    }

    [Fact]
    public void The_schedule_lists_each_lift_with_its_load_persons_car_stops_and_travel()
    {
        var f = F();
        var storeys = Building(f);
        Place(f, "accessible");
        Place(f, "goods", new JsonObject { ["x"] = 4, ["topStoreyId"] = storeys[1] });
        var grid = new ElevatorSchedule().Grid(f.Session.Reader);
        Assert.Equal(3, grid.Rows.Count);
        var acc = grid.Rows.Single(r => r[1] == "Wheelchair-accessible");
        Assert.Equal("630", acc[3]);
        Assert.Equal("8", acc[4]);
        Assert.Equal("1100 × 1400 × 2200", acc[5]);
        Assert.Equal("4/4", acc[7]);
        Assert.Equal("9.00", acc[9]);
        var goods = grid.Rows.Single(r => r[1] == "Goods lift");
        Assert.Equal("2000", goods[3]);
        Assert.Equal("3.00", goods[9]);
        Assert.Equal("2", grid.Rows[^1][1]);
        // Its quantities: a lift each, for the estimate.
        var q = f.Session.Quantities(f.Model.OfClass(TransportModule.Class).First().Id).Single(x => x.Name == "Lapin_LiftQuantities");
        Assert.Equal(1, q.Quantities.Single(x => x.Name == "Count").Value);
    }

    [Fact]
    public void IFC_writes_a_transport_element_of_its_type_with_its_capacity_and_reads_it_back_native()
    {
        var f = F();
        var storeys = Building(f, 3);
        var id = Place(f, "passenger-medium", new JsonObject { ["attrs"] = new JsonObject { ["unserved"] = new JsonArray(storeys[1]), ["fireExit"] = true } });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(1, ifc.Split("IFCTRANSPORTELEMENT(").Length - 1);
        Assert.Contains(".ELEVATOR.)", ifc);
        Assert.Equal(1, ifc.Split("IFCTRANSPORTELEMENTTYPE(").Length - 1);
        Assert.Contains("'Pset_TransportElementCommon'", ifc);
        Assert.Contains("'CapacityPeople'", ifc);
        Assert.Contains("IFCCOUNTMEASURE(8)", ifc);
        Assert.Contains("IFCMASSMEASURE(630.)", ifc);
        Assert.Contains("'FireExit'", ifc);
        Assert.Contains("'Pset_TransportElementElevator'", ifc);
        Assert.Contains("'ClearWidth'", ifc);
        // The floors' openings as IfcOpeningElements of the slabs.
        Assert.Equal(3, ifc.Split("'Elevator shaft'").Length - 1);
        var path = Path.Combine(Path.GetTempPath(), $"lift-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var lift = g.Model.OfClass(TransportModule.Class).Single();
            Assert.Equal(TransportModule.Predefined, lift.PredefinedType);
            Assert.NotNull(TransportModule.TypeOf(g.Model, lift));
            Assert.Equal(630, lift.Num("load"));
            Assert.True(lift.Bool("fireExit"));
            var l = ElevatorLayout.Resolve(g.Model, lift, out var problem);
            Assert.True(l != null, problem);
            Assert.Equal(3, l!.Stops.Count);
            Assert.Equal([true, false, true], l.Stops.Select(s => s.Served));
            Assert.Equal(3, TransportModule.OpeningsOf(g.Model, lift.Id).Count());
            Assert.NotEmpty(g.Session.Geometry(lift.Id)!.Parts);
        }
        finally { File.Delete(path); }
    }
}
