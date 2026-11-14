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
/// The circular pneumatic vacuum lift (tester's report #149, docs/plan-elevators.md): a transparent tube on a base ring,
/// no shaft and no pit, ring beams in the floors it passes, a head unit on top, frame columns, landing doors of three
/// kinds each facing its own angle, a cabin with or without its sliding door; round openings in the floors; the
/// configurator's preview made by the same code; the schedule, styles, mirror, IFC and undo.
/// </summary>
public sealed class VacuumLiftTests
{
    static Fixture F() => new(new ArchitectureModule(), new TransportModule());

    /// <summary>Storeys 3 m apart, each with a floor of 10 × 8 m; their ids, lowest first.</summary>
    static List<string> Building(Fixture f, int storeys = 3, bool floors = true)
    {
        for (var i = Spatial.Storeys(f.Model).Count(); i < storeys; i++) f.Run("storey.create", new JsonObject { ["height"] = 3.0 });
        var ids = Spatial.Storeys(f.Model).Select(s => s.Id).ToList();
        if (floors) foreach (var id in ids) Slab(f, id);
        return ids;
    }

    static void Slab(Fixture f, string storey) =>
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = storey, ["points"] = new JsonArray(new JsonArray(-5, -4), new JsonArray(5, -4), new JsonArray(5, 4), new JsonArray(-5, 4)) });

    static string Place(Fixture f, string style = "vacuum-933", JsonObject? extra = null)
    {
        var a = new JsonObject { ["storeyId"] = Spatial.Storeys(f.Model).First().Id, ["x"] = 0, ["y"] = 0, ["style"] = style };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("transport.place", a)!["id"]!;
    }

    static ElevatorLayout Layout(Fixture f, string id) => ElevatorLayout.Resolve(f.Model, f.Model.Get(id)!, out _)!;
    static void Update(Fixture f, string id, JsonObject attrs) => f.Run("transport.update", new JsonObject { ["id"] = id, ["attrs"] = attrs });

    [Fact]
    public void Each_tube_builds_a_closed_lift_over_three_storeys_with_no_pit()
    {
        foreach (var style in ElevatorStyles.All.Where(x => (string?)x.Values["drive"] == "vacuum"))
        {
            var f = F();
            var storeys = Building(f);
            var id = Place(f, style.Id);
            var e = f.Model.Get(id)!;
            Assert.Equal(TransportModule.Predefined, e.PredefinedType);
            var l = Layout(f, id);
            var s = l.Spec;
            Assert.True(s.Vacuum);
            Assert.Equal((double)style.Values["tubeDiameter"]!, s.TubeDiameter, 6);
            Assert.Equal(ElevatorSpec.DerivedCabinDiameter(s.TubeDiameter), s.CabinDiameter, 6);
            Assert.Equal(3, l.Stops.Count);
            Assert.Empty(s.Checks(l.Travel));
            Assert.Empty(l.VacuumNotes(f.Model));
            // Every piece is a closed solid (the tube's arcs, rings, columns, door frames, cabin, doors).
            foreach (var (key, _, solids, _) in l.Bodies())
                Assert.All(solids, x => Assert.True(Topology.IsManifold(x) && Measure.Volume(x) > 0, $"{style.Id}: a piece of {key} is closed"));
            var g = f.Session.Geometry(id)!;
            string[] keys = ["shaft", "frame", "headUnit", "cabin", "cabinGlass", "carDoors", "landingDoors"];
            Assert.Equal(keys.Order(), g.Parts.Select(p => p.Key).Order());
            Assert.True(g.Parts.Single(p => p.Key == "shaft").CutInPlan);
            Assert.All(g.Parts, p => Assert.NotNull(p.Material));
            Assert.Equal("#2b2f33", g.Parts.Single(p => p.Key == "frame").Color);
            // It stands on the floor (no pit) and rises its cabin, a gap and its head unit over the top stop.
            var (min, max) = f.Bounds(id);
            Assert.Equal(0, min.Z, 6);
            Assert.Equal(6 + s.CabinHeight + ElevatorSpec.CabinRoofGap + s.HeadUnitHeight, max.Z, 6);
            Assert.InRange(max.X - min.X, s.TubeDiameter, s.TubeDiameter + 2 * (s.ColumnSize + 0.01));
            // Its type and name say its kind and tube.
            Assert.Equal($"{style.Label}, Ø {Math.Round(s.TubeDiameter * 1000)}", e.Name);
            Assert.Equal(e.Name, TransportModule.TypeOf(f.Model, e)!.Name);
        }
    }

    [Fact]
    public void Its_round_openings_are_in_the_floors_above_and_follow_it()
    {
        var f = F();
        var storeys = Building(f, 4);
        var id = Place(f, extra: new JsonObject { ["topStoreyId"] = storeys[2] });
        var l = Layout(f, id);
        // The lowest floor carries it; the two above open round the tube and its ring beams; the one over its head unit stays whole.
        var openings = TransportModule.OpeningsOf(f.Model, id).ToList();
        Assert.Equal(2, openings.Count);
        var r = l.Spec.TubeDiameter / 2 + l.Spec.OpeningMargin;
        Assert.Equal(Math.PI * r * r, Math.Abs(Polygon2.SignedArea(Slabs.HoleContour(f.Model, openings[0]))), 2);
        Assert.Equal(Chords.Circle(r), Slabs.HoleContour(f.Model, openings[0]).Count);
        // A head unit higher than the storey above opens that floor too, and says so.
        Update(f, id, new JsonObject { ["headUnitHeight"] = 1.2 });
        Assert.Equal(3, TransportModule.OpeningsOf(f.Model, id).Count());
        Assert.Contains(Layout(f, id).VacuumNotes(f.Model), n => n.Contains("head unit"));
        Update(f, id, new JsonObject { ["headUnitHeight"] = 0.55 });
        Assert.Equal(2, TransportModule.OpeningsOf(f.Model, id).Count());
        // Moved, they go with it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(2, 1) });
        var c = Slabs.HoleContour(f.Model, TransportModule.OpeningsOf(f.Model, id).First());
        Assert.Equal(2, c.Average(p => p.X), 4);
        Assert.Equal(1, c.Average(p => p.Y), 4);
        // A wider tube widens them.
        Update(f, id, new JsonObject { ["tubeDiameter"] = 1.3 });
        var wide = Slabs.HoleContour(f.Model, TransportModule.OpeningsOf(f.Model, id).First());
        Assert.Equal(Math.PI * Math.Pow(0.65 + 0.06, 2), Math.Abs(Polygon2.SignedArea(wide)), 2);
    }

    [Fact]
    public void A_floor_drawn_after_it_opens_with_shaft_openings()
    {
        var f = F();
        var storeys = Building(f, 3, floors: false);
        var id = Place(f);
        Assert.Empty(TransportModule.OpeningsOf(f.Model, id));
        Slab(f, storeys[1]);
        f.Run("transport.syncOpenings", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Single(TransportModule.OpeningsOf(f.Model, id));
    }

    [Fact]
    public void Doors_slide_swing_or_are_open_and_each_landing_faces_its_own_way()
    {
        var f = F();
        var storeys = Building(f);
        var id = Place(f, extra: new JsonObject { ["attrs"] = new JsonObject { ["doorAngles"] = new JsonObject { [storeys[1]] = 90 } } });
        var l = Layout(f, id);
        Assert.Equal(0, l.Spec.LandingAngle(storeys[0]));
        Assert.Equal(90, l.Spec.LandingAngle(storeys[1]));
        Assert.Equal(0, l.Spec.LandingAngle(storeys[2]));
        // The landing doors: at the ground floor in front (−y), at the first floor to the right (+x).
        var doors = f.Session.Geometry(id)!.Parts.Single(p => p.Key == "landingDoors").Solid.Vertices;
        var ground = doors.Where(v => v.Z < 2.5).ToList();
        var first = doors.Where(v => v.Z > 3.1 && v.Z < 5.5).ToList();
        Assert.True(ground.Average(v => v.Y) < -0.4 && Math.Abs(ground.Average(v => v.X)) < 0.05);
        Assert.True(first.Average(v => v.X) > 0.4 && Math.Abs(first.Average(v => v.Y)) < 0.05);
        // The tube is open where each door is: at +1 m over the ground floor no tube in front, at +4 m none to the right.
        var tube = l.Bodies().Single(b => b.Key == "shaft").Solids;
        bool TubeAt(double x, double y, double z) => tube.Any(s => Measure.Bounds(s) is { } b && b.Min.X <= x && b.Max.X >= x && b.Min.Y <= y && b.Max.Y >= y && b.Min.Z <= z && b.Max.Z >= z);
        var r = l.TubeR - 0.005;
        Assert.False(TubeAt(0, -r, 1.0));
        Assert.True(TubeAt(r, 0, 1.0));
        Assert.False(TubeAt(r, 0, 4.0));
        Assert.True(TubeAt(0, -r, 4.0));

        // Sliding: a panel and, dashed, where it slides; the cabin's door too.
        var plan = l.PlanOn(storeys[0]);
        Assert.Equal(2, plan.Count(i => i.Kind == "dashed"));
        Assert.Contains(f.Session.Geometry(id)!.Parts, p => p.Key == "carDoors");
        // Swing: the leaf and its swing, no slide; no cabin door.
        Update(f, id, new JsonObject { ["doorKind"] = "swing", ["cabinDoor"] = "none" });
        l = Layout(f, id);
        plan = l.PlanOn(storeys[0]);
        Assert.DoesNotContain(plan, i => i.Kind == "dashed");
        Assert.Equal(4, plan.Count(i => !i.Closed));
        Assert.Contains(f.Session.Geometry(id)!.Parts, p => p.Key == "landingDoors");
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts, p => p.Key == "carDoors");
        // Open: no landing doors at all, the opening and its frame stay.
        Update(f, id, new JsonObject { ["doorKind"] = "none" });
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts, p => p.Key == "landingDoors");
        Assert.Equal(3, Layout(f, id).PlanOn(storeys[0]).Count);
        Assert.False(TubeAt(0, -r, 1.0));
        // A stop it skips has no door and a whole tube.
        Update(f, id, new JsonObject { ["doorKind"] = "sliding", ["unserved"] = new JsonArray(storeys[1]) });
        l = Layout(f, id);
        Assert.Equal(3, l.PlanOn(storeys[1]).Count);
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts.Single(p => p.Key == "landingDoors").Solid.Vertices, v => v.Z > 3.1 && v.Z < 5.5);
        // A door wider than the tube allows is said.
        Assert.Contains(new ElevatorSpec(new JsonObject { ["drive"] = "vacuum", ["tubeDiameter"] = 0.75, ["doorWidth"] = 0.8 }).Checks(3), c => c.Contains("wider than the tube"));
    }

    [Fact]
    public void The_columns_stand_clear_of_the_doors_and_the_cabin_follows_the_tube_until_set()
    {
        var f = F();
        Building(f, 2);
        var id = Place(f);
        var l = Layout(f, id);
        Assert.Equal((4, 0), (l.ColumnAngles().Kept.Count, l.ColumnAngles().Skipped));
        // Eight columns and the upper door at the back: those in its way are left out, and said.
        Update(f, id, new JsonObject { ["columns"] = 8, ["doorAngles"] = new JsonObject { [Spatial.Storeys(f.Model).Last().Id] = 180 } });
        l = Layout(f, id);
        Assert.True(l.ColumnAngles().Skipped > 0);
        Assert.Contains(l.VacuumNotes(f.Model), n => n.Contains("frame column"));
        // The cabin follows a new tube; set by hand, it stays.
        Update(f, id, new JsonObject { ["tubeDiameter"] = 1.3 });
        Assert.Equal(1.18, f.Model.Get(id)!.Num("cabinDiameter"), 6);
        Update(f, id, new JsonObject { ["cabinDiameter"] = 1.1 });
        Update(f, id, new JsonObject { ["tubeDiameter"] = 1.25 });
        Assert.Equal(1.1, f.Model.Get(id)!.Num("cabinDiameter"), 6);
        // Both at once: as given.
        Update(f, id, new JsonObject { ["tubeDiameter"] = 0.75, ["cabinDiameter"] = 0.6 });
        Assert.Equal(0.6, f.Model.Get(id)!.Num("cabinDiameter"), 6);
        // Its clear size is the cabin's, as Pset_TransportElementElevator says it.
        Assert.Equal(0.6, f.Model.Get(id)!.Num("cabinWidth"), 6);
    }

    [Fact]
    public void The_configurators_preview_is_the_element_it_applies()
    {
        var f = F();
        var storeys = Building(f);
        var id = Place(f);
        var draft = new JsonObject { ["tubeDiameter"] = 1.3, ["doorKind"] = "swing", ["doorAngles"] = new JsonObject { [storeys[2]] = 180 }, ["frameColor"] = "#ffffff" };
        var before = f.Session.Query("transport.preview", new JsonObject { ["id"] = id, ["attrs"] = draft.DeepClone() })!;
        Assert.Null(before["problem"]);
        Assert.Equal(1.18, (double)before["attrs"]!["cabinDiameter"]!, 6);
        Assert.Contains(before["plan"]!.AsArray(), i => (string?)i!["kind"] == "wall");
        Assert.Contains(before["section"]!["items"]!.AsArray(), i => (string?)i!["role"] == "tube");
        Update(f, id, draft);
        var after = f.Session.Query("transport.preview", new JsonObject { ["id"] = id })!;
        Assert.Equal(before["plan"]!.ToJsonString(), after["plan"]!.ToJsonString());
        Assert.Equal(before["section"]!.ToJsonString(), after["section"]!.ToJsonString());
        Assert.Equal(before["summary"]!.ToJsonString(), after["summary"]!.ToJsonString());
        foreach (var (k, v) in before["attrs"]!.AsObject()) Assert.Equal(v?.ToJsonString(), f.Model.Get(id)!.Attrs[k]?.ToJsonString());
        // The plan's symbols are the element's at its lowest stop.
        var l = Layout(f, id);
        var symbols = after["plan"]!.AsArray().Where(i => (string?)i!["kind"] != "wall").Count();
        Assert.Equal(l.PlanOn(storeys[0]).Count + 1, symbols);
        Assert.Equal("#ffffff", f.Session.Geometry(id)!.Parts.Single(p => p.Key == "frame").Color);
    }

    [Fact]
    public void The_schedule_lists_it_with_its_tube_cabin_and_door()
    {
        var f = F();
        Building(f);
        Place(f);
        Place(f, "passenger-medium", new JsonObject { ["x"] = 4 });
        var grid = new ElevatorSchedule().Grid(f.Session.Reader);
        var row = grid.Rows.Single(r => r[1] == "Vacuum lift, two persons");
        Assert.Equal("vacuum", row[2]);
        Assert.Equal("205", row[3]);
        Assert.Equal("2", row[4]);
        Assert.Equal("Ø 813 × 2050", row[5]);
        Assert.Equal("600 × 1950 sliding", row[6]);
        Assert.Equal("3/3", row[7]);
        Assert.Equal("Ø 933", row[11]);
        Assert.Equal("2", grid.Rows[^1][1]);
    }

    [Fact]
    public void A_project_style_keeps_its_tube_and_a_mirror_turns_its_doors()
    {
        var f = F();
        var storeys = Building(f, 2);
        var a = Place(f, extra: new JsonObject { ["attrs"] = new JsonObject { ["doorAngle"] = 90 } });
        var sid = (string)f.Run("transport.saveStyle", new JsonObject { ["id"] = a, ["name"] = "Villa lift", ["attrs"] = new JsonObject { ["tubeDiameter"] = 1.1, ["tubeMaterial"] = "glass" } })!["styleId"]!;
        var b = Place(f, sid, new JsonObject { ["x"] = 3 });
        Assert.Equal(1.1, f.Model.Get(b)!.Num("tubeDiameter"), 6);
        Assert.Equal("glass", f.Model.Get(b)!.Str("tubeMaterial"));
        Assert.Equal("Glass", f.Model.Get(f.Session.Geometry(b)!.Parts.Single(p => p.Key == "shaft").Material!)!.Name);
        // Mirrored across a north–south line, a door facing east faces west.
        var r = f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(a), ["op"] = "mirror", ["a"] = new JsonArray(-2, -10), ["b"] = new JsonArray(-2, 10), ["copy"] = true })!;
        var copy = f.Model.Get((string)((JsonArray)r["ids"]!)[0]!)!;
        Assert.Equal(270, copy.Num("doorAngle"), 6);
        var doors = f.Session.Geometry(copy.Id)!.Parts.Single(p => p.Key == "landingDoors").Solid.Vertices.Where(v => v.Z < 2.5).ToList();
        Assert.True(doors.Average(v => v.X) < -4 - 0.4);
        // Another drive drops the tube's fields and takes its pit.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(b), ["attrs"] = new JsonObject { ["drive"] = "mrl" } });
        var e = f.Model.Get(b)!;
        Assert.Null(e.Attrs["tubeDiameter"]);
        Assert.Equal(ElevatorSpec.SuggestedPit("mrl", 0.15), e.Num("pit"), 6);
        Assert.Equal(ElevatorSpec.SuggestedHeadroom("mrl", 0.15), e.Num("headroom"), 6);
        _ = storeys;
    }

    [Fact]
    public void IFC_writes_it_as_an_elevator_with_its_capacity_under_a_stable_GlobalId()
    {
        var f = F();
        var storeys = Building(f);
        var id = Place(f);
        var gid = f.Model.Get(id)!.Id;
        Update(f, id, new JsonObject { ["tubeDiameter"] = 1.3, ["doorKind"] = "swing" });
        Assert.Equal(gid, f.Model.Get(id)!.Id);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains($"IFCTRANSPORTELEMENT('{gid}'", ifc);
        Assert.Contains(".ELEVATOR.)", ifc);
        Assert.Contains("'Pset_TransportElementCommon'", ifc);
        Assert.Contains("IFCCOUNTMEASURE(2)", ifc);
        Assert.Contains("IFCMASSMEASURE(205.)", ifc);
        Assert.Contains("'ClearWidth'", ifc);
        Assert.Equal(2, ifc.Split("'Elevator shaft'").Length - 1);
        var path = Path.Combine(Path.GetTempPath(), $"vacuum-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var lift = g.Model.OfClass(TransportModule.Class).Single();
            Assert.Equal(gid, lift.Id);
            Assert.Equal("vacuum", lift.Str("drive"));
            Assert.Equal(1.3, lift.Num("tubeDiameter"), 6);
            Assert.Equal("swing", lift.Str("doorKind"));
            Assert.NotEmpty(g.Session.Geometry(lift.Id)!.Parts);
        }
        finally { File.Delete(path); }
        _ = storeys;
    }

    [Fact]
    public void Each_change_is_one_undo_step()
    {
        var f = F();
        Building(f);
        var id = Place(f);
        var area = Math.Abs(Polygon2.SignedArea(Slabs.HoleContour(f.Model, TransportModule.OpeningsOf(f.Model, id).First())));
        Update(f, id, new JsonObject { ["tubeDiameter"] = 1.3 });
        f.Session.Undo();
        Assert.Equal(0.933, f.Model.Get(id)!.Num("tubeDiameter"), 6);
        Assert.Equal(area, Math.Abs(Polygon2.SignedArea(Slabs.HoleContour(f.Model, TransportModule.OpeningsOf(f.Model, id).First()))), 6);
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Empty(f.Model.OfClass(IfcClass.OpeningElement));
    }
}
