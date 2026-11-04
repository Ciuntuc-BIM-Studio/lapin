using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 298: sweeps and reveals on the wall type, given to every wall of it and overridden or switched off on one;
/// the wall sweeps schedule; a sweep moved, copied and mirrored on its own; returns at free ends and at openings.
/// </summary>
public class WallSweepTypeTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-type-sweeps-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    const double D = 0.05, H = 0.1, A = D * H;

    static JsonObject Rect(params (string Key, JsonNode? Value)[] more)
    {
        var o = new JsonObject { ["catalog"] = "rectangle", ["params"] = new JsonObject { ["depth"] = D, ["height"] = H } };
        foreach (var (k, v) in more) o[k] = v;
        return o;
    }

    static void SetSweeps(Fixture f, string type, params JsonObject[] sweeps) =>
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["sweeps"] = new JsonArray([.. sweeps.Select(x => x.DeepClone())]) });

    static JsonArray Defs(Fixture f, string type) => (JsonArray)f.Model.Get(type)!.Attrs[WallSweepTypes.Key]!.DeepClone();

    static List<Entity> SweepsOn(Fixture f, string wall) => [.. f.Model.RelatedOf(wall, IfcClass.RelVoidsElement).Where(x => x.Class == WallSweeps.Class)];
    static Entity One(Fixture f, string wall) => Assert.Single(SweepsOn(f, wall));
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static Dictionary<string, double> Qs(Fixture f, string id) =>
        f.Session.Quantities(id).Single(s => s.Name == "Lapin_WallSweepQuantities").Quantities.ToDictionary(x => x.Name, x => x.Value);
    static void Update(Fixture f, string id, JsonObject attrs) => f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = attrs });

    static Fixture Full() => new(new ArchitectureModule(), new Lapin.Structure.StructureModule());

    [Fact]
    public void A_type_s_sweeps_are_given_to_every_wall_of_it_and_follow_the_type()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        Assert.Empty(SweepsOn(f, a));
        // The type edited: the wall already there gets it, on the type's face (its first layer's: the right as drawn).
        SetSweeps(f, type, Rect(("height", 1.0), ("returns", "none")));
        var sa = One(f, a);
        Assert.NotNull(sa.Str("typeSweep"));
        Assert.Equal("right", sa.Str("side"));
        Assert.Equal(-0.15, f.Bounds(sa.Id).Min.Y, 9);
        Assert.Equal(5 * A, Volume(f, sa.Id), 9);
        // A wall made of the type gets it as it is made.
        var b = f.Wall(0, 3, 5, 3, type);
        Assert.Equal(sa.Str("typeSweep"), One(f, b).Str("typeSweep"));
        // The type changed again: the same sweeps (their ids) at the new height.
        var defs = Defs(f, type);
        ((JsonObject)defs[0]!)["height"] = 1.5;
        SetSweeps(f, type, [.. defs.OfType<JsonObject>()]);
        Assert.Equal(sa.Id, One(f, a).Id);
        Assert.Equal(1.5, f.Bounds(sa.Id).Min.Z, 9);
        Assert.Equal(1.5, f.Bounds(One(f, b).Id).Min.Z, 9);
        // A wall of another type has none; back to the type, it has it again.
        var other = f.Type("Plain", ("Brick", 0.2, true));
        f.Run("architecture.setWallType", new JsonObject { ["ids"] = new JsonArray(b), ["typeId"] = other });
        Assert.Empty(SweepsOn(f, b));
        f.Run("architecture.setWallType", new JsonObject { ["ids"] = new JsonArray(b), ["typeId"] = type });
        Assert.Single(SweepsOn(f, b));
        // Flipped, the wall's type face is its left: the sweep goes with it.
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(a) });
        Assert.Equal("left", One(f, a).Str("side"));
        Assert.Equal(0.15, f.Bounds(sa.Id).Max.Y, 9);
        // The type's sweeps taken away: its walls' go.
        SetSweeps(f, type);
        Assert.Empty(SweepsOn(f, a));
        Assert.Empty(SweepsOn(f, b));
        Assert.Null(f.Model.Get(type)!.Attrs[WallSweepTypes.Key]);
    }

    [Fact]
    public void A_wall_keeps_its_own_fields_of_a_type_sweep_and_one_switched_off_stays_off()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        SetSweeps(f, type, Rect(("height", 1.0), ("returns", "none")));
        var sa = One(f, a).Id;
        Update(f, sa, new JsonObject { ["height"] = 2.0 });
        Assert.Equal(["height"], WallSweeps.OwnOf(f.Model.Get(sa)!.CloneAttrs()));
        // The type's height and offset changed: the wall keeps its own height, takes the offset.
        var defs = Defs(f, type);
        ((JsonObject)defs[0]!)["height"] = 1.5;
        ((JsonObject)defs[0]!)["offset"] = 0.01;
        SetSweeps(f, type, [.. defs.OfType<JsonObject>()]);
        Assert.Equal(2.0, f.Model.Get(sa)!.Num("height"));
        Assert.Equal(0.01, f.Model.Get(sa)!.Num("offset"));
        Assert.Equal(1.5, One(f, b).Num("height"));
        // Back to the type.
        f.Run("architecture.updateWallSweep", new JsonObject { ["id"] = sa, ["reset"] = true });
        Assert.Equal(1.5, f.Model.Get(sa)!.Num("height"));
        Assert.Null(f.Model.Get(sa)!.Attrs["own"]);
        // Deleted, it is switched off on its wall: the type does not bring it back.
        f.Run("element.delete", new JsonObject { ["id"] = sa });
        Assert.Empty(SweepsOn(f, a));
        Assert.Single(WallSweepTypes.OffOf(f.Model.Get(a)!));
        ((JsonObject)defs[0]!)["height"] = 1.2;
        SetSweeps(f, type, [.. defs.OfType<JsonObject>()]);
        Assert.Empty(SweepsOn(f, a));
        Assert.Equal(1.2, One(f, b).Num("height"));
        // Switched on again; and off by the command.
        f.Run("architecture.restoreWallSweeps", new JsonObject { ["wallId"] = a });
        Assert.Equal(1.2, One(f, a).Num("height"));
        f.Run("architecture.updateWallSweep", new JsonObject { ["id"] = One(f, b).Id, ["off"] = true });
        Assert.Empty(SweepsOn(f, b));
        // A wall copied keeps its sweeps as they are (no second one from the type).
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(a), ["op"] = "copy", ["by"] = new JsonArray(0.0, 6.0) });
        Assert.Equal(2, f.Model.OfClass(WallSweeps.Class).Count());
    }

    [Fact]
    public void The_wall_sweeps_schedule_adds_them_up_by_profile_kind_and_material()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        SetSweeps(f, type, Rect(("height", 1.0), ("returns", "none")));
        var a = f.Wall(0, 0, 5, 0, type);
        f.Wall(0, 3, 4, 3, type);
        f.Run("architecture.addWallSweep", new JsonObject { ["wallId"] = a, ["mode"] = "cut", ["catalog"] = "groove", ["params"] = new JsonObject { ["depth"] = 0.02, ["width"] = 0.03 }, ["side"] = "left", ["height"] = 2.0 });
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = WallSweepSchedule.KindId })!["id"]!;
        var grid = new WallSweepSchedule().Grid(f.Session.Reader, f.Model.Get(id)!);
        Assert.Equal(["Profile", "Kind", "Material", "Count", "Pieces", "Length (m)", "Volume (m³)"], grid.Columns);
        var sweep = grid.Rows.Single(r => r[1] == "Sweep" && !r[0].StartsWith("Total", StringComparison.Ordinal));
        Assert.Equal("Plaster", sweep[2]);
        Assert.Equal("2", sweep[3]);
        Assert.Equal("9.00", sweep[5]);
        Assert.Equal((9 * A).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), sweep[6]);
        var reveal = grid.Rows.Single(r => r[1] == "Reveal" && !r[0].StartsWith("Total", StringComparison.Ordinal));
        Assert.Equal("5.00", reveal[5]);
        Assert.Equal("0.0030", reveal[6]);
        Assert.Equal(["Total sweeps", "Total reveals"], grid.Rows.Where(r => r[0].StartsWith("Total", StringComparison.Ordinal)).Select(r => r[0]));
        // By storey, then by wall type: a column before.
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = new JsonArray("type") });
        grid = new WallSweepSchedule().Grid(f.Session.Reader, f.Model.Get(id)!);
        Assert.Equal("Wall type", grid.Columns[0]);
        Assert.All(grid.Rows.Where(r => !r[1].StartsWith("Total", StringComparison.Ordinal)), r => Assert.Equal("Core 20", r[0]));
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = new JsonArray("storey") });
        Assert.Equal("Storey", new WallSweepSchedule().Grid(f.Session.Reader, f.Model.Get(id)!).Columns[0]);
        // The engine lists it among the schedules.
        Assert.Contains(f.Session.Catalog.All<IScheduleSource>(), s => s.Kind == WallSweepSchedule.KindId);
    }

    static string Own(Fixture f, string wall, params (string Key, JsonNode? Value)[] more)
    {
        var a = Rect(more);
        a["wallId"] = wall;
        return (string)f.Run("architecture.addWallSweep", a)!["id"]!;
    }

    static void Move(Fixture f, string id, double dx, double dy, double? rise = null, bool copy = false)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(id), ["op"] = copy ? "copy" : "move", ["by"] = new JsonArray(dx, dy) };
        if (rise is { } r) a["world"] = new JsonArray(dx, dy, r);
        f.Run("element.transform", a);
    }

    [Fact]
    public void A_sweep_moves_along_its_wall_and_up_and_is_copied_on_it_or_onto_another()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var s = Own(f, a, ("side", "right"), ("height", 1.0), ("start", 1.0), ("end", 3.0));
        // Along the wall it slides; the part across it is left out.
        Move(f, s, 1, 0.5);
        Assert.Equal(2.0, f.Model.Get(s)!.Num("start"), 9);
        Assert.Equal(4.0, f.Model.Get(s)!.Num("end"), 9);
        Assert.Equal(a, WallSweeps.HostOf(f.Model, f.Model.Get(s)!)!.Id);
        // Up, in an elevation (the move in the model): its height.
        Move(f, s, 0, 0, rise: 0.2);
        Assert.Equal(1.2, f.Model.Get(s)!.Num("height"), 9);
        Assert.Equal(1.2, f.Bounds(s).Min.Z, 9);
        // Along the whole face: it gets a start and an end, cut by the wall's end.
        var whole = Own(f, a, ("side", "left"), ("height", 2.0), ("returns", "none"));
        Move(f, whole, 1, 0);
        Assert.Equal(1.0, f.Model.Get(whole)!.Num("start"), 9);
        Assert.Equal(6.0, f.Model.Get(whole)!.Num("end"), 9);
        Assert.Equal(4.0, Qs(f, whole)["Length"], 9);
        // Copied along its wall: another sweep, shifted.
        Move(f, s, 0.5, 0, copy: true);
        Assert.Equal(3, SweepsOn(f, a).Count);
        var copy = SweepsOn(f, a).Single(x => x.Id != s && x.Id != whole);
        Assert.Equal(2.5, copy.Num("start"), 9);
        Assert.Equal(1.2, copy.Num("height"), 9);
        // Copied onto another wall: there, on the face it lands on, as long.
        var b = f.Wall(0, 3, 5, 3, type);
        Move(f, s, 0, 3, copy: true);
        var onB = One(f, b);
        Assert.Equal("right", onB.Str("side"));
        Assert.Equal(2.0, onB.Num("start"), 9);
        Assert.Equal(4.0, onB.Num("end"), 9);
        Assert.Equal(2 * A, Volume(f, onB.Id), 9);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, onB.Id)!.Id);
        // Mirrored across its wall's axis: on the other face, where it was along it.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(s), ["op"] = "mirror", ["a"] = new JsonArray(0.0, 0.0), ["b"] = new JsonArray(5.0, 0.0) });
        Assert.Equal("left", f.Model.Get(s)!.Str("side"));
        Assert.Equal(2.0, f.Model.Get(s)!.Num("start"), 9);
        // Mirrored across a line square to the wall at x 2.5: the other way along, the same face.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(s), ["op"] = "mirror", ["a"] = new JsonArray(2.5, -1.0), ["b"] = new JsonArray(2.5, 1.0) });
        Assert.Equal("left", f.Model.Get(s)!.Str("side"));
        Assert.Equal(1.0, f.Model.Get(s)!.Num("start"), 9);
        Assert.Equal(3.0, f.Model.Get(s)!.Num("end"), 9);
    }

    [Fact]
    public void A_type_sweep_moved_becomes_its_wall_s_own_in_what_changed()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        SetSweeps(f, type, Rect(("height", 1.0), ("returns", "none")));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        var s = One(f, a).Id;
        Move(f, s, 1, 0, rise: 0.5);
        var own = WallSweeps.OwnOf(f.Model.Get(s)!.CloneAttrs());
        Assert.Equal(["end", "height", "start"], own.Order(StringComparer.Ordinal));
        // The type changed: the moved one stays where it was put.
        var defs = Defs(f, type);
        ((JsonObject)defs[0]!)["height"] = 0.5;
        SetSweeps(f, type, [.. defs.OfType<JsonObject>()]);
        Assert.Equal(1.5, f.Model.Get(s)!.Num("height"), 9);
        Assert.Equal(0.5, One(f, b).Num("height"), 9);
        // Moved onto the other wall: its own sweep there, the type's switched off on the first.
        Move(f, s, 0, 3);
        Assert.Empty(SweepsOn(f, a));
        Assert.Equal(2, SweepsOn(f, b).Count);
        Assert.Null(f.Model.Get(s)!.Str("typeSweep"));
        Assert.Single(WallSweepTypes.OffOf(f.Model.Get(a)!));
    }

    [Fact]
    public void A_sweep_returns_round_its_wall_s_free_ends_and_not_at_a_corner()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        // By default an added sweep returns at both free ends: run to the end, mitred, and wrapped back to the face's plane.
        var s = Own(f, a, ("side", "left"), ("height", 1.0));
        Assert.Equal((5 + 2 * D) * A, Volume(f, s), 9);
        var box = f.Bounds(s);
        Assert.Equal(-D, box.Min.X, 9);
        Assert.Equal(5 + D, box.Max.X, 9);
        Assert.Equal(0.1, box.Min.Y, 9);
        Assert.Equal(0.1 + D, box.Max.Y, 9);
        // The returns' ends lie in the face's plane, past the wall's ends.
        var solid = f.Session.Geometry(s)!.Parts.Single().Solid;
        Assert.Contains(solid.Vertices, v => Math.Abs(v.X - (5 + D)) < 1e-9 && Math.Abs(v.Y - 0.1) < 1e-9);
        Assert.Equal(5, Qs(f, s)["Length"], 9);
        // At the start only, at the end only, none.
        Update(f, s, new JsonObject { ["returns"] = "start" });
        Assert.Equal((5 + D) * A, Volume(f, s), 9);
        Assert.Equal(5, f.Bounds(s).Max.X, 9);
        Update(f, s, new JsonObject { ["returns"] = "none" });
        Assert.Equal(5 * A, Volume(f, s), 9);
        // Set back from an end, it stops square there.
        Update(f, s, new JsonObject { ["returns"] = "both", ["setbackEnd"] = 1.0 });
        Assert.Equal((4 + D) * A, Volume(f, s), 9);
        // A reveal never returns.
        var r = (string)f.Run("architecture.addWallSweep", new JsonObject { ["wallId"] = a, ["mode"] = "cut", ["catalog"] = "groove", ["params"] = new JsonObject { ["depth"] = 0.02, ["width"] = 0.03 }, ["side"] = "right", ["height"] = 2.0 })!["id"]!;
        Assert.Equal(5 * 0.02 * 0.03, Qs(f, r)["RemovedVolume"], 9);
        Assert.Equal(3.0 - 5 * 0.02 * 0.03, f.Session.Geometry(a)!.Parts.Sum(p => Measure.Volume(p.Solid)), 9);
        // Flipped upside down, a rectangle hangs under its height.
        Update(f, s, new JsonObject { ["flip"] = true });
        Assert.Equal(1.0 - H, f.Bounds(s).Min.Z, 9);
        Assert.Equal(1.0, f.Bounds(s).Max.Z, 9);
    }

    [Fact]
    public void At_an_L_corner_a_sweep_does_not_return_and_at_openings_it_may()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        f.Wall(5, 0, 5, 4, type);
        // On the outer face: to the outer corner (x 5.1), square there; at the free start, the return.
        var s = Own(f, a, ("side", "right"), ("height", 1.0));
        Assert.Equal(5.1, f.Bounds(s).Max.X, 9);
        Assert.Equal(-D, f.Bounds(s).Min.X, 9);
        Assert.Equal((5.1 + D) * A, Volume(f, s), 9);

        var g = new Fixture();
        var w = g.Wall(0, 0, 5, 0, g.Type("Core 20", ("Brick", 0.2, true)));
        g.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 2.5 });
        var low = Own(g, w, ("side", "left"), ("height", 1.0), ("returns", "none"));
        Assert.Equal((5 - 0.9) * A, Volume(g, low), 9);
        Update(g, low, new JsonObject { ["returnsAtOpenings"] = true });
        Assert.Equal((5 - 0.9 + 2 * D) * A, Volume(g, low), 9);
        Update(g, low, new JsonObject { ["returns"] = "both" });
        Assert.Equal((5 - 0.9 + 4 * D) * A, Volume(g, low), 9);
        Assert.Equal(2, Qs(g, low)["Pieces"]);
    }

    static Fixture Typed(out string type, out string a, out string b)
    {
        var f = Full();
        type = f.Type("Core 20", ("Brick", 0.2, true));
        a = f.Wall(0, 0, 5, 0, type);
        b = f.Wall(5, 0, 5, 4, type);
        SetSweeps(f, type, Rect(("height", 1.0)), new JsonObject { ["mode"] = "cut", ["catalog"] = "vGroove", ["side"] = "left", ["height"] = 2.0 });
        Update(f, One2(f, a, "add"), new JsonObject { ["height"] = 1.1 });
        return f;
    }

    static string One2(Fixture f, string wall, string mode) => SweepsOn(f, wall).Single(x => x.Str("mode") == mode).Id;

    [Fact]
    public void Type_sweeps_go_to_IFC_as_occurrences_and_come_back_with_their_type()
    {
        var f = Typed(out var type, out var a, out var b);
        Assert.Equal(4, f.Model.OfClass(WallSweeps.Class).Count());
        var path = Path.Combine(_dir, "type-sweeps.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        Assert.Equal(2, step.OfType("IFCCOVERING").Count());
        Assert.Equal(2, step.OfType("IFCOPENINGELEMENT").Count(o => o[8] is StepEnum { Value: "RECESS" }));

        var g = Full();
        IfcImport.Run(g.Session, path, null, out _);
        Assert.Equal(4, g.Model.OfClass(WallSweeps.Class).Count());
        Assert.True(JsonNode.DeepEquals(f.Model.Get(type)!.Attrs[WallSweepTypes.Key], g.Model.Get(type)!.Attrs[WallSweepTypes.Key]));
        foreach (var s in f.Model.OfClass(WallSweeps.Class))
        {
            var twin = g.Model.Get(s.Id)!;
            Assert.Equal(s.Str("typeSweep"), twin.Str("typeSweep"));
            Assert.Equal(Volume(f, s.Id), Volume(g, twin.Id), 9);
        }
        Assert.Equal(1.1, g.Model.Get(One2(f, a, "add"))!.Num("height"));
        // Still the type's there: a change to it reaches the same sweeps, the wall's own height kept.
        var defs = Defs(g, type);
        ((JsonObject)defs[0]!)["height"] = 0.6;
        SetSweeps(g, type, [.. defs.OfType<JsonObject>()]);
        Assert.Equal(4, g.Model.OfClass(WallSweeps.Class).Count());
        Assert.Equal(0.6, g.Model.Get(One2(f, b, "add"))!.Num("height"));
        Assert.Equal(1.1, g.Model.Get(One2(f, a, "add"))!.Num("height"));
    }

    [Fact]
    public void Type_sweeps_are_kept_in_the_lapin_file_and_in_the_types_file()
    {
        var f = Typed(out var type, out _, out var b);
        var path = Path.Combine(_dir, "type-sweeps.lapin");
        PackageFiles.Save(f.Session, path);
        var g = Full();
        PackageFiles.Open(g.Session, path);
        Assert.True(JsonNode.DeepEquals(f.Model.Get(type)!.Attrs[WallSweepTypes.Key], g.Model.Get(type)!.Attrs[WallSweepTypes.Key]));
        foreach (var s in f.Model.OfClass(WallSweeps.Class)) Assert.Equal(Volume(f, s.Id), Volume(g, s.Id), 9);

        // The types file: the profiles' outlines and the materials by name, brought into another project.
        var text = (string)f.Session.Query("architecture.exportTypes", new JsonObject())!["text"]!;
        var doc = JsonNode.Parse(text)!;
        var sweeps = (JsonArray)doc["types"]!.AsArray().Single(t => (string?)t!["name"] == "Core 20")!["sweeps"]!;
        Assert.Equal(2, sweeps.Count);
        Assert.Equal("Plaster", (string?)sweeps[0]!["material"]);
        Assert.NotNull(sweeps[0]!["profile"]!["points"]);
        var h = Full();
        h.Run("architecture.importTypes", new JsonObject { ["text"] = text });
        var made = h.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "Core 20");
        var defs = WallSweepTypes.Defs(made);
        Assert.Equal(2, defs.Count);
        Assert.All(defs, d => Assert.Equal(Profiles.Class, h.Model.Get((string)d[Profiles.IdKey]!)!.Class));
        Assert.Equal("Plaster", h.Model.Get((string)defs[0]["material"]!)!.Name);
        var w = h.Wall(0, 0, 5, 0, made.Id);
        Assert.Equal(2, SweepsOn(h, w).Count);
        Assert.Equal(Volume(f, One2(f, b, "add")) > 0, Volume(h, One2(h, w, "add")) > 0);
    }
}
