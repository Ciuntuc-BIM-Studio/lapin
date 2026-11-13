using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Garage doors (a tester's request, forum topic #48: "a steel sectional panel lift door", decision 530): door types whose one
/// panel opens as a sectional overhead, roller, up-and-over or side-sliding sectional door, hung behind the opening; each a
/// closed body, its plan drawn as CAD libraries draw it (the tracks dashed back to the backroom), its OperationType the best
/// each IFC schema has, read back from IFC, scheduled with the room it needs; the presets at common sizes (indicative).
/// </summary>
public class GarageDoorTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-garage").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static readonly JoinerySpec[] Presets =
    [
        JoinerySpecs.SectionalGarageDoor, JoinerySpecs.RollerGarageDoor, JoinerySpecs.UpAndOverGarageDoor,
        JoinerySpecs.SideSlidingGarageDoor, JoinerySpecs.SideHingedGarageDoors,
    ];

    static (Fixture F, string Wall, string Door) Placed(JoinerySpec spec)
    {
        var f = new Fixture();
        var type = f.Type("Block 200", ("Plaster", 0.015, false), ("Block", 0.2, true), ("Render", 0.02, false));
        var wall = f.Wall(0, 0, 8, 0, type);
        var typeId = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 4, ["typeId"] = typeId })!["id"]!;
        return (f, wall, door);
    }

    [Fact]
    public void Each_kind_builds_a_closed_body_and_leaves_its_wall_closed()
    {
        foreach (var spec in Presets)
        {
            Assert.Null(Joinery.Problem(spec));
            var (f, wall, door) = Placed(spec);
            var parts = f.Session.Geometry(door)!.Parts;
            Assert.Contains(parts, p => p.Key == "leaf");
            foreach (var p in parts.Concat(f.Session.Geometry(wall)!.Parts))
                Assert.True(Topology.IsManifold(p.Solid), $"{spec.Name} {p.Key ?? p.Name}: {string.Join("; ", Topology.Validate(p.Solid).Select(d => d.Message))}");
        }
        // Every finish and track of a sectional door, a wicket door, a roller's box outside, a retractable up-and-over, the other hand.
        var s = JoinerySpecs.SectionalGarageDoor;
        foreach (var fin in GarageDoors.Finishes)
            foreach (var track in GarageDoors.Tracks)
            {
                var g = s with { Garage = new JoineryGarage(Finish: fin, Track: track, Wicket: fin != "glazed-top") };
                Assert.Null(Joinery.Problem(g));
                Assert.All(Joinery.Boxes(g), b => Assert.True(b.Max.X > b.Min.X && b.Max.Y > b.Min.Y && b.Max.Z > b.Min.Z));
            }
        Assert.Null(Joinery.Problem(JoinerySpecs.RollerGarageDoor with { Garage = new JoineryGarage(Box: "outside") }));
        Assert.Null(Joinery.Problem(JoinerySpecs.UpAndOverGarageDoor with { Garage = new JoineryGarage(Mechanism: "retractable") }));
        Assert.Null(Joinery.Problem(Joinery.Mirrored(JoinerySpecs.SideSlidingGarageDoor)));
        Assert.Equal("sectional-side-right", GarageDoors.KindOf(Joinery.Mirrored(JoinerySpecs.SideSlidingGarageDoor)));
    }

    [Fact]
    public void A_sectional_door_has_panels_from_its_height_its_finish_and_its_tracks_to_the_backroom()
    {
        var s = JoinerySpecs.SectionalGarageDoor;
        // 2100 high: four panels of about 525 mm; 2400 → four, 2700 → five; the type's own count wins.
        Assert.Equal(4, GarageDoors.SectionCount(s));
        Assert.Equal(5, GarageDoors.SectionCount(s with { Height = 3.0 }));
        Assert.Equal(6, GarageDoors.SectionCount(s with { Garage = new JoineryGarage(Sections: 6) }));
        var boxes = Joinery.Boxes(s);
        var curtain = GarageDoors.Curtain(s);
        // The panels, joint to joint, cover the curtain; the ribs stand on the outer face.
        var (yc0, yc1) = GarageDoors.Plane(s);
        var panels = boxes.Where(b => b.Role == "leaf" && b.Min.Y < yc1 - 1e-9).ToList();
        Assert.Equal(4, panels.Count);
        Assert.Equal(curtain.Z0, panels.Min(b => b.Min.Z), 9);
        Assert.Equal(curtain.Z1, panels.Max(b => b.Max.Z), 9);
        Assert.Equal(12, boxes.Count(b => b.Role == "leaf" && b.Min.Y >= yc1 - 1e-9));
        // Hung behind the frame: the curtain's outer face on the frame's inner face.
        Assert.Equal(s.Frame.Offset - s.Frame.Depth / 2, yc1, 9);
        // The tracks: up the sides, back under the ceiling to the backroom (the door's height and 500 mm, from the frame's inner face).
        var backroom = GarageDoors.BackroomOf(s);
        Assert.Equal(2.6, backroom, 9);
        Assert.Equal(0.3, GarageDoors.HeadroomOf(s), 9);
        var tracks = boxes.Where(b => b.Role == "hardware").ToList();
        Assert.Equal(s.Frame.Offset - s.Frame.Depth / 2 - backroom, tracks.Min(b => b.Min.Y), 9);
        Assert.True(tracks.Max(b => b.Max.Z) <= s.Height + GarageDoors.HeadroomOf(s) + 1e-9, "the tracks within the headroom");
        Assert.True(tracks.Max(b => b.Max.Z) > s.Height, "the tracks run over the opening");
        // Low headroom and high lift: the horizontal run lower, or higher by the lift; the backroom shorter by it.
        var low = s with { Garage = new JoineryGarage(Track: "low-headroom") };
        var high = s with { Garage = new JoineryGarage(Track: "high-lift", Lift: 0.8) };
        Assert.Equal(0.15, GarageDoors.HeadroomOf(low), 9);
        Assert.Equal(1.1, GarageDoors.HeadroomOf(high), 9);
        Assert.Equal(2.1 - 0.8 + 0.5, GarageDoors.BackroomOf(high), 9);
        Assert.True(GarageDoors.TrackLine(high).Zh > GarageDoors.TrackLine(s).Zh && GarageDoors.TrackLine(low).Zh < GarageDoors.TrackLine(s).Zh);
        // No tracks asked for in 3D: none.
        Assert.DoesNotContain(Joinery.Boxes(s with { Garage = new JoineryGarage(Tracks: false) }), b => b.Role == "hardware");
        // Glazed top: glass in the top panel only.
        var glazed = Joinery.Boxes(s with { Garage = new JoineryGarage(Finish: "glazed-top") });
        Assert.True(glazed.Count(b => b.Role == "glass") >= 2);
        Assert.All(glazed.Where(b => b.Role == "glass"), b => Assert.True(b.Min.Z >= GarageDoors.SectionBands(s)[^1].Z0 - 1e-9));
        // A wicket door: its frame on the outer face, a handle each side; too narrow a door has no room for one.
        var wicket = s with { Garage = new JoineryGarage(Wicket: true, WicketWidth: 0.9) };
        var w = GarageDoors.WicketRect(wicket)!.Value;
        Assert.Equal(0.9, w.X1 - w.X0, 9);
        Assert.Equal(GarageDoors.Curtain(s).X1 - 0.3, w.X1, 9);
        Assert.True(w.X0 + w.X1 > 0, "the left seen from inside: the type frame's +X");
        Assert.Equal(2, Joinery.Boxes(wicket).Count(b => b.Role == "handle"));
        Assert.NotNull(Joinery.Problem(wicket with { Width = 1.3 }));
    }

    [Fact]
    public void The_plan_draws_the_curtain_and_the_tracks_dashed_to_the_backroom()
    {
        var s = JoinerySpecs.SectionalGarageDoor;
        var plan = JoinerySymbols.Plan(s);
        var inside = s.Frame.Offset - s.Frame.Depth / 2;
        var back = inside - GarageDoors.BackroomOf(s);
        var dashed = plan.Where(x => x.Dashed).ToList();
        // Two tracks from the curtain to the backroom, the cross line there, the door open overhead.
        var tracks = dashed.Where(x => !x.Closed && x.Pts.Count == 2 && Math.Abs(x.Pts[0].X - x.Pts[1].X) < 1e-9).ToList();
        Assert.Equal(2, tracks.Count);
        Assert.All(tracks, t => Assert.Equal(back, t.Pts.Min(p => p.Y), 9));
        Assert.Contains(dashed, x => !x.Closed && x.Pts.All(p => Math.Abs(p.Y - back) < 1e-9));
        Assert.Contains(dashed, x => x.Closed);
        // The curtain cut at the opening; at LOD 100 a line, the tracks still dashed.
        var (yc0, yc1) = GarageDoors.Plane(s);
        Assert.Contains(plan, x => x.Weight == "cut" && x.Closed && x.Pts.Min(p => p.Y) == yc0 && x.Pts.Max(p => p.Y) == yc1);
        Assert.Equal(2, JoinerySymbols.Plan(s, level: PlanDetail.Coarse).Count(x => x.Dashed && x.Pts.Count == 2 && Math.Abs(x.Pts[0].X - x.Pts[1].X) < 1e-9));
        // No leaf open at 90° and no swing arc.
        Assert.DoesNotContain(plan, x => !x.Closed && x.Pts.Count > 4 && !x.Dashed);

        // A roller's box overhead, an up-and-over door open, a side-sliding door's track round the corner to its backroom.
        Assert.Single(JoinerySymbols.Plan(JoinerySpecs.RollerGarageDoor), x => x.Dashed && x.Closed);
        Assert.Single(JoinerySymbols.Plan(JoinerySpecs.UpAndOverGarageDoor), x => x.Dashed && x.Closed);
        var side = JoinerySpecs.SideSlidingGarageDoor;
        var run = Assert.Single(JoinerySymbols.Plan(side), x => x.Dashed && !x.Closed);
        Assert.Equal(side.Frame.Offset - side.Frame.Depth / 2 - GarageDoors.BackroomOf(side), run.Pts.Min(p => p.Y), 9);
        Assert.True(run.Pts.Min(p => p.X) < GarageDoors.Curtain(side).X0, "it bends to the left, beside the opening");

        // Elevation: the panel joints; section: the curtain cut, the tracks seen.
        Assert.Equal(3, JoinerySymbols.Elevation(s).Count(x => x.Weight == "outline" && !x.Closed && x.Pts.Count == 2 && Math.Abs(x.Pts[0].Y - x.Pts[1].Y) < 1e-9));
        Assert.Contains(JoinerySymbols.Section(s), x => x.Weight == "cut");
    }

    [Fact]
    public void Plans_of_the_project_draw_it_dashed_overhead()
    {
        var (f, _, door) = Placed(JoinerySpecs.SectionalGarageDoor);
        var plan = f.Session.Geometry(door)!.Plan!;
        Assert.True(plan.Count(p => p.Kind == "dashed") >= 3);
        Assert.Contains(plan, p => p.Kind == "heavy");
    }

    [Fact]
    public void Ifc_names_the_operation_each_schema_has_and_reads_it_back()
    {
        (string Op, string? User, string Op43) Terms(JoinerySpec s) { var t = JoineryIfc.Of(s); return (t.In(false).Operation, t.In(false).UserDefined, t.In(true).Operation); }
        Assert.Equal(("USERDEFINED", "SECTIONAL_OVERHEAD", "LIFTING_HORIZONTAL"), Terms(JoinerySpecs.SectionalGarageDoor));
        Assert.Equal(("ROLLINGUP", null, "ROLLINGUP"), Terms(JoinerySpecs.RollerGarageDoor));
        Assert.Equal(("USERDEFINED", "UP_AND_OVER", "LIFTING_HORIZONTAL"), Terms(JoinerySpecs.UpAndOverGarageDoor));
        Assert.Equal(("USERDEFINED", "SECTIONAL_SIDE_LEFT", "USERDEFINED"), Terms(JoinerySpecs.SideSlidingGarageDoor));
        Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", Terms(JoinerySpecs.SideHingedGarageDoors).Op);
        Assert.Equal("ROLLINGUP", Assert.Single(JoineryIfc.Of(JoinerySpecs.RollerGarageDoor).Panels).Operation);
        // A side-sliding door's mirror image runs to the right.
        Assert.Equal("SECTIONAL_SIDE_RIGHT", JoineryIfc.Of(JoinerySpecs.SideSlidingGarageDoor, mirrored: true).UserDefined);

        // Read back: Lapin's names, IFC4X3's lifting door, a foreign file's rolling door or its only panel rolling up.
        var basis = JoinerySpec.DefaultDoor() with { Width = 2.4, Height = 2.1 };
        string? Kind(string? op, string? user, IReadOnlyList<IfcJoineryPanel>? panels = null) => JoineryIfc.FromIfc(basis, op, user, panels) is { } x ? GarageDoors.KindOf(x) : null;
        Assert.Equal("sectional-overhead", Kind("USERDEFINED", "SECTIONAL_OVERHEAD"));
        Assert.Equal("sectional-overhead", Kind("LIFTING_HORIZONTAL", null));
        Assert.Equal("up-and-over", Kind("USERDEFINED", "UP_AND_OVER"));
        Assert.Equal("roller", Kind("ROLLINGUP", null));
        Assert.Equal("roller", Kind("NOTDEFINED", null, [new IfcJoineryPanel("ROLLINGUP", "MIDDLE", 1)]));
        Assert.Equal("sectional-side-right", Kind("USERDEFINED", "SECTIONAL_SIDE_RIGHT"));
        Assert.Null(Kind("SINGLE_SWING_LEFT", null));
        Assert.Null(Joinery.Problem(JoineryIfc.FromIfc(basis, "ROLLINGUP", null, null)!));
    }

    [Fact]
    public void An_ifc4_and_an_ifc4x3_file_say_it_and_lapin_reads_its_own_back()
    {
        var (f, _, door) = Placed(JoinerySpecs.SectionalGarageDoor with { Garage = new JoineryGarage(Track: "low-headroom", Wicket: true) });
        var p4 = Path.Combine(_dir, "garage4.ifc");
        var p43 = Path.Combine(_dir, "garage43.ifc");
        IfcExport.Write(f.Session, p4);
        IfcExport.Write(f.Session, p43, new IfcExportOptions("IFC4X3"));
        var t4 = File.ReadAllText(p4);
        var t43 = File.ReadAllText(p43);
        Assert.Contains(".USERDEFINED.,.F.,'SECTIONAL_OVERHEAD'", t4);
        Assert.DoesNotContain("LIFTING_HORIZONTAL", t4);
        Assert.Contains(".LIFTING_HORIZONTAL.", t43);
        Assert.DoesNotContain("'SECTIONAL_OVERHEAD'", t43);
        Assert.Contains("'Lapin_GarageDoor'", t4);

        foreach (var path in new[] { p4, p43 })
        {
            var g = new Fixture();
            IfcImport.Run(g.Session, path, null, out _);
            var type = Assert.Single(g.Session.Model.OfClass(IfcClass.DoorType));
            var spec = JoinerySpec.From(type.Attrs["spec"])!;
            Assert.Equal("sectional-overhead", GarageDoors.KindOf(spec));
            Assert.Equal("low-headroom", spec.Garage!.Track);
            Assert.True(spec.Garage.Wicket);
        }
        Assert.NotNull(door);
    }

    [Fact]
    public void The_schedule_names_the_operation_and_the_room_it_needs_and_quantities_measure_it()
    {
        var s = JoinerySpecs.SectionalGarageDoor;
        Assert.Equal("sectional overhead", ScheduleLayout.OperationText(s));
        Assert.Equal("4 panels, ribbed, standard track, headroom 300 mm, backroom 2600 mm, sideroom 100 mm", ScheduleLayout.HangingText(s));
        Assert.Equal("roller", ScheduleLayout.OperationText(JoinerySpecs.RollerGarageDoor));
        Assert.StartsWith("box inside, headroom", ScheduleLayout.HangingText(JoinerySpecs.RollerGarageDoor));

        var (f, _, door) = Placed(s);
        var data = Schedules.Joinery(f.Model, "door");
        var entry = Assert.Single(data.Entries);
        var rows = ScheduleLayout.Rows(data, new ScheduleTable(null, [], new Dictionary<string, Dictionary<string, string>>(), null));
        Assert.Equal("sectional overhead", rows.Single(x => x.Id == "operation").Value(entry));
        Assert.StartsWith("4 panels", rows.Single(x => x.Id == "hanging").Value(entry));
        var set = f.Session.Quantities(door).Single(q => q.Name == "Lapin_GarageDoor");
        var c = GarageDoors.Curtain(s);
        Assert.Equal((c.X1 - c.X0) * (c.Z1 - c.Z0), set.Quantities.Single(q => q.Name == "CurtainArea").Value, 9);
        Assert.Equal(4, set.Quantities.Single(q => q.Name == "Panels").Value);
        Assert.Equal(2.6, set.Quantities.Single(q => q.Name == "Backroom").Value, 9);
    }

    [Fact]
    public void The_presets_are_at_common_sizes_and_hung_from_the_interior_face()
    {
        var f = new Fixture();
        var catalog = (JsonObject)f.Session.Catalog.Catalogs["architecture.joinery"]!;
        foreach (var (key, kind) in new[] { ("garage-sectional", "sectional-overhead"), ("garage-roller", "roller"), ("garage-up-and-over", "up-and-over"), ("garage-side-sliding", "sectional-side-left") })
        {
            var spec = JoinerySpec.From(catalog["doorPresets"]![key])!;
            Assert.Null(Joinery.Problem(spec));
            Assert.Equal(kind, GarageDoors.KindOf(spec));
            Assert.Equal("interior", spec.Position!.From);
        }
        var pair = JoinerySpec.From(catalog["doorPresets"]!["garage-side-hinged"])!;
        Assert.Equal(["swing-left", "swing-right"], Joinery.Normalised(pair).OrderBy(p => p.Col).Select(p => p.Operation));
        Assert.Equal(2.4, pair.Width, 9);
        // Australia's 2400, 2700, 4800, 5400 × 2100 and Europe's 2500, 3000, 5000 × 2125 among the sizes, which are indicative.
        var sizes = catalog["garage"]!["sizes"]!.AsArray().Select(x => ((double)x!["width"]!, (double)x["height"]!)).ToList();
        foreach (var size in new[] { (2.4, 2.1), (2.7, 2.1), (4.8, 2.1), (5.4, 2.1), (2.5, 2.125), (3.0, 2.125), (5.0, 2.125) }) Assert.Contains(size, sizes);
        Assert.True((bool)catalog["garage"]!["indicative"]!);
        Assert.Contains("sectional-overhead", catalog["doorOperations"]!.AsArray().Select(x => (string)x!));

        // Hung behind the opening: the frame's inner face on the wall's interior face, whichever side the door is flipped to.
        var (g, wall, door) = Placed(JoinerySpecs.SectionalGarageDoor);
        var m = g.Session.Model;
        var spec2 = JoinerySpecs.SectionalGarageDoor.Positioned(m, m.Get(wall)!, null, false);
        var bands = Walls.Section(m, m.Get(wall)!).ToList();
        var half = (bands.Max(b => b.To) - bands.Min(b => b.From)) / 2;
        Assert.Equal(-half, spec2.Frame.Offset - spec2.Frame.Depth / 2, 9);
        Assert.NotNull(door);
    }
}
