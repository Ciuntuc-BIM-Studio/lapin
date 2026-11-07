using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Bay, bow and box windows (report #60): a window type whose spec projects; each built-in style builds closed parts
/// out from the wall's exterior face, the wall is cut by the opening only, the faces stand at their angles (a bow's on its
/// arc), the parts follow the configurator (seat, sill, base, head, flashing), schedules list one row per type with its
/// projection, IFC writes one IfcWindow of a user-defined partitioning and reads the spec back.
/// </summary>
public sealed class BayWindowTests
{
    const double Along = 3;

    static (Fixture F, string Wall) Setup()
    {
        var f = new Fixture();
        return (f, f.Wall(0, 0, 6, 0));
    }

    static JoinerySpec StyleSpec(string id) => BayWindows.Styles.Single(s => s.Id == id).Spec;

    static string Bay(Fixture f, string wall, JoinerySpec spec, double along = Along)
    {
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along, ["typeId"] = type })!["id"]!;
    }

    static (double YMin, double YMax) Faces(Fixture f, string wall)
    {
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        return (bands.Min(b => b.From), bands.Max(b => b.To));
    }

    [Fact]
    public void Every_style_builds_closed_parts_out_from_the_walls_exterior_face()
    {
        foreach (var (id, _, _, spec) in BayWindows.Styles)
        {
            var (f, wall) = Setup();
            Assert.Null(Joinery.Problem(spec));
            var w = Bay(f, wall, spec);
            var g = f.Session.Geometry(w)!;
            Assert.Contains(g.Parts, p => p.Key == "frame");
            Assert.Contains(g.Parts, p => p.Key == "glass");
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{id}: {p.Name} is closed"));
            // The wall runs along +x; its exterior is its right face (−y): the bay projects that way by its projection.
            var (yMin, _) = Faces(f, wall);
            var min = g.Parts.Where(p => p.Key is "frame" or "glass" or "sash" or "bay:side").Min(p => Measure.Bounds(p.Solid)!.Value.Min.Y);
            Assert.InRange(yMin - min, spec.Bay!.Projection - 0.01, spec.Bay.Projection + 0.01);
            if (spec.Bay.Head is { Kind: not "none" }) Assert.Contains(g.Parts, p => p.Key == "bay:head");
            if (spec.Bay.Head is { Kind: not "none", Flashing: true }) Assert.Contains(g.Parts, p => p.Key == "bay:flashing");
            if (spec.Bay.Seat is { On: true }) Assert.Contains(g.Parts, p => p.Key == "bay:seat");
            if (spec.Bay.Base is { Kind: "corbel" }) Assert.Contains(g.Parts, p => p.Key == "bay:corbels");
            if (spec.Bay.Base is { Kind: "slab" or "corbel" or "floor" }) Assert.Contains(g.Parts, p => p.Key == "bay:base");
            if (spec.Bay.Sides == "solid") Assert.Contains(g.Parts, p => p.Key == "bay:side");
            // The materials it names are the project's.
            if (spec.Bay.Head?.Material is { } roof) Assert.NotNull(Materials.Find(f.Model, roof));
            Assert.NotEmpty(g.Plan!);
        }
    }

    [Fact]
    public void The_wall_is_cut_by_the_opening_only_and_the_knee_walls_stand_on_the_floor()
    {
        var (f, wall) = Setup();
        var spec = StyleSpec("bay-60-floor");
        var w = Bay(f, wall, spec);
        var opening = Openings.OpeningOf(f.Model, w)!;
        var cutter = Openings.CutterIn(f.Model, f.Model.Get(wall)!, Openings.Place(f.Model, opening))!;
        var cb = Measure.Bounds(cutter)!.Value;
        Assert.Equal(spec.Width, cb.Max.X - cb.Min.X, 6);
        Assert.Equal(spec.Height, cb.Max.Z - cb.Min.Z, 6);
        Assert.Equal(spec.SillHeight, cb.Min.Z, 6);
        // The wall's body has the opening's jambs at its edges.
        var body = f.Session.Geometry(wall)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Contains(body, v => Math.Abs(v.X - (Along - spec.Width / 2)) < 1e-6 && Math.Abs(v.Z - spec.SillHeight) < 1e-6);
        Assert.Contains(body, v => Math.Abs(v.X - (Along + spec.Width / 2)) < 1e-6 && Math.Abs(v.Z - (spec.SillHeight + spec.Height)) < 1e-6);
        // Knee walls from the floor up under the sill.
        var base_ = Measure.Bounds(f.Session.Geometry(w)!.Parts.Single(p => p.Key == "bay:base").Solid)!.Value;
        Assert.Equal(0, base_.Min.Z, 6);
        Assert.InRange(base_.Max.Z, spec.SillHeight - 0.06, spec.SillHeight);
    }

    [Fact]
    public void A_bays_sides_stand_at_its_angle_a_bows_faces_on_its_arc_and_a_boxs_square()
    {
        var bay = StyleSpec("bay-45");
        var faces = BayWindows.Faces(bay, 0.15);
        Assert.Equal(3, faces.Count);
        Assert.Equal(45, Math.Atan2(faces[0].U.Y, faces[0].U.X) * 180 / Math.PI, 6);
        Assert.Equal(-45, Math.Atan2(faces[2].U.Y, faces[2].U.X) * 180 / Math.PI, 6);
        Assert.Equal(0, faces[1].U.Y, 9);
        // The centre takes the columns between the sides: two panels; the sides one each.
        Assert.Equal(2, faces[1].Spec.Columns.Length);
        Assert.Single(faces[0].Spec.Columns);
        var pc = bay.Bay!.Projection - bay.Frame.Depth / 2;
        Assert.Equal(pc * Math.Sqrt(2), faces[0].Length, 6);
        Assert.Equal(bay.Width - bay.Frame.Depth - 2 * pc, faces[1].Length, 6);
        // Unfolded: four panels side by side, as wide as the faces.
        var dev = BayWindows.Developed(bay);
        Assert.Equal(4, dev.Columns.Length);
        Assert.Equal(faces.Sum(x => x.Length), dev.Width, 5);

        var bow = StyleSpec("bow-5");
        var arc = BayWindows.Line(bow, 0.15);
        Assert.Equal(6, arc.Count);
        var bowFaces = BayWindows.Faces(bow, 0.15);
        Assert.Equal(5, bowFaces.Count);
        Assert.All(bowFaces, x => Assert.Equal(bowFaces[0].Length, x.Length, 6));
        // Every point on one circle about the middle, the outermost face (the middle one) the projection out.
        var cy = (arc[0].Y * arc[0].Y + arc[0].X * arc[0].X - arc[2].Y * arc[2].Y - arc[2].X * arc[2].X) / (2 * (arc[0].Y - arc[2].Y));
        var r = (arc[0] - new Vec2(0, cy)).Length;
        Assert.All(arc, p => Assert.Equal(r, (p - new Vec2(0, cy)).Length, 6));
        Assert.Equal(bow.Bay!.Projection - bow.Frame.Depth / 2, (arc[2].Y + arc[3].Y) / 2 - 0.15, 4);

        var box = StyleSpec("box-glazed");
        var boxFaces = BayWindows.Faces(box, 0.15);
        Assert.Equal(90, Math.Atan2(boxFaces[0].U.Y, boxFaces[0].U.X) * 180 / Math.PI, 6);
        Assert.Equal(box.Width - box.Frame.Depth, boxFaces[1].Length, 6);
        // What cannot be built is said.
        Assert.NotNull(Joinery.Problem(bow with { Bay = bow.Bay with { Projection = 2 } }));
        Assert.NotNull(Joinery.Problem(bay with { Columns = [1, 1], Panels = [] }));
        Assert.NotNull(Joinery.Problem(bay with { Bay = bay.Bay with { Angle = 5 } }));
        Assert.NotNull(Joinery.Problem(JoinerySpec.DefaultDoor() with { Bay = new JoineryBay() }));
    }

    [Fact]
    public void A_hipped_roof_rises_from_its_eaves_to_the_wall_and_a_flashing_follows_it()
    {
        var (f, wall) = Setup();
        var spec = StyleSpec("bay-45");
        var w = Bay(f, wall, spec);
        var g = f.Session.Geometry(w)!;
        var head = Measure.Bounds(g.Parts.Single(p => p.Key == "bay:head").Solid)!.Value;
        var top = spec.SillHeight + spec.Height;
        Assert.Equal(top, head.Min.Z, 6);
        var h = spec.Bay!.Head!;
        // The eaves are its thickness over the frames; it rises towards the wall at its pitch.
        Assert.True(head.Max.Z > top + h.Thickness + 0.1);
        Assert.True(head.Max.Z < top + h.Thickness + Math.Tan(h.Pitch * Math.PI / 180) * (spec.Bay.Projection + h.Overhang) + 0.01);
        var flashing = Measure.Bounds(g.Parts.Single(p => p.Key == "bay:flashing").Solid)!.Value;
        Assert.InRange(flashing.Max.Z - head.Max.Z, 0.14, 0.16);
    }

    [Fact]
    public void Plans_draw_the_projection_and_the_roof_overhead_and_the_schedule_lists_one_row_per_type()
    {
        var (f, wall) = Setup();
        var spec = StyleSpec("bay-45");
        var w = Bay(f, wall, spec);
        var typeId = f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id;
        var g = f.Session.Geometry(w)!;
        Assert.Contains(g.Plan!, i => i.Kind == "overhead");
        Assert.Contains(g.Plan!, i => i.Kind == "heavy");
        var plan = Views.Find(f.Model, "plan:" + f.Storey)!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, plan);
        Assert.True(d != null, problem);
        Assert.Contains(d!.Lines, l => l.Element == w);
        var elevation = f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!)!;
        var (ed, _, eproblem) = Views.Generate(f.Session.Reader, elevation);
        Assert.True(ed != null, eproblem);
        Assert.Contains(ed!.Lines, l => l.Element == w);

        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = f.Wall(0, 5, 6, 5), ["along"] = Along, ["typeId"] = typeId });
        var data = Schedules.Joinery(f.Model, "window");
        var entry = data.Entries.Single(e => e.Spec?.Bay != null);
        Assert.Equal(1, data.Entries.Count(e => e.Spec?.Bay != null));
        var rows = ScheduleLayout.Rows(data, new ScheduleTable(null, [], new Dictionary<string, Dictionary<string, string>>(), null));
        var projecting = rows.Single(r => r.Id == "bay");
        Assert.StartsWith("Bay 45°, projection 600, 3 faces", projecting.Value(entry));
        Assert.Contains("tilt & turn", ScheduleLayout.OperationText(spec));
        // Measured: its unfolded glazed area, its roof and seat.
        var sets = f.Session.Quantities(w);
        var area = sets.Single(s => s.Name == "Qto_WindowBaseQuantities").Quantities.Single(q => q.Name == "Area").Value;
        Assert.Equal(BayWindows.Developed(spec).Width * spec.Height, area, 6);
        var details = sets.Single(s => s.Name == "Lapin_Details").Quantities;
        Assert.Contains(details, q => q.Name == "BayRoofArea" && q.Value > 0);
        Assert.Contains(details, q => q.Name == "BayProjection" && Math.Abs(q.Value - 0.6) < 1e-9);
    }

    [Fact]
    public void IFC_writes_one_window_of_a_user_defined_partitioning_and_reads_the_bay_back()
    {
        var (f, wall) = Setup();
        Bay(f, wall, StyleSpec("bow-5"));
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(1, ifc.Split("IFCWINDOW(").Length - 1);
        Assert.Contains(".USERDEFINED.", ifc);
        Assert.Contains("'Bow, projection 500, 5 faces, 5 panels'", ifc);
        Assert.Equal(5, ifc.Split("IFCWINDOWPANELPROPERTIES(").Length - 1);
        var path = Path.Combine(Path.GetTempPath(), $"bay-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = new Fixture();
            IfcImport.Run(g.Session, path, null, out _);
            var window = g.Model.OfClass(IfcClass.Window).Single();
            var spec = Openings.SpecOf(g.Model, window.Id)!;
            Assert.Equal("bow", spec.Bay!.Kind);
            Assert.Equal(0.5, spec.Bay.Projection, 9);
            Assert.Equal("corbel", spec.Bay.Base!.Kind);
            Assert.Contains(g.Session.Geometry(window.Id)!.Parts, p => p.Key == "bay:head");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_projects_own_bay_styles_are_saved_renamed_and_deleted()
    {
        var (f, _) = Setup();
        var spec = StyleSpec("oriel") with { Bay = StyleSpec("oriel").Bay! with { Projection = 0.75 } };
        var id = (string)f.Run("architecture.saveBayStyle", new JsonObject { ["name"] = "Our oriel", ["spec"] = spec.ToJson() })!["styleId"]!;
        var saved = ProjectStyles.All(f.Model, "bayStyles").Single();
        Assert.Equal("Our oriel", (string?)saved["label"]);
        Assert.Equal(0.75, JoinerySpec.From(saved["spec"])!.Bay!.Projection, 9);
        // Saved again under its name: the same style, changed.
        f.Run("architecture.saveBayStyle", new JsonObject { ["name"] = "our oriel", ["spec"] = (spec with { Bay = spec.Bay! with { Projection = 0.8 } }).ToJson() });
        Assert.Single(ProjectStyles.All(f.Model, "bayStyles"));
        f.Run("architecture.renameBayStyle", new JsonObject { ["styleId"] = id, ["name"] = "Oriel, street side" });
        Assert.Equal("Oriel, street side", (string?)ProjectStyles.All(f.Model, "bayStyles").Single()["label"]);
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.saveBayStyle", new JsonObject { ["name"] = "Flat", ["spec"] = JoinerySpec.DefaultWindow().ToJson() }));
        f.Run("architecture.deleteBayStyle", new JsonObject { ["styleId"] = id });
        Assert.Empty(ProjectStyles.All(f.Model, "bayStyles"));
    }

    [Fact]
    public void A_type_keeps_the_styles_applied_to_it_so_the_configurator_shows_them()
    {
        // Report #111: the configurator's style and bay style stay on the type's spec, read back as they were written.
        var (f, _) = Setup();
        var spec = StyleSpec("oriel") with { Style = "georgian", Bay = StyleSpec("oriel").Bay! with { Style = "oriel" } };
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var stored = JoinerySpec.From(f.Model.Get(type)!.Attrs["spec"])!;
        Assert.Equal("georgian", stored.Style);
        Assert.Equal("oriel", stored.Bay!.Style);
        f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = type, ["spec"] = (stored with { Style = null, Bay = stored.Bay with { Style = null } }).ToJson() });
        stored = JoinerySpec.From(f.Model.Get(type)!.Attrs["spec"])!;
        Assert.Null(stored.Style);
        Assert.Null(stored.Bay!.Style);
    }
}
