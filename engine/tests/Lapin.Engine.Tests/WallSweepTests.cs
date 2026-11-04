using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Wall sweeps and reveals (decision 297, a user's request): a profile swept along a wall's face, added (a cornice, a
/// band) or taken out of the wall (a reveal); mitred round a corner into a like sweep, stopped at openings, along curved
/// walls; IFC as IfcCovering MOLDING and IfcOpeningElement RECESS, and back.
/// </summary>
public class WallSweepTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-sweeps-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    // Square ends: these tests measure the sweeps without their returns at free ends (decision 298: WallSweepTypeTests).
    static JsonObject Rect(double depth, double height) => new() { ["catalog"] = "rectangle", ["params"] = new JsonObject { ["depth"] = depth, ["height"] = height }, ["returns"] = "none" };

    static string Sweep(Fixture f, string wall, JsonObject args)
    {
        args["wallId"] = wall;
        return (string)f.Run("architecture.addWallSweep", args)!["id"]!;
    }

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static Dictionary<string, double> Qs(Fixture f, string id) =>
        f.Session.Quantities(id).Single(s => s.Name == "Lapin_WallSweepQuantities").Quantities.ToDictionary(x => x.Name, x => x.Value);

    static JsonObject With(JsonObject a, params (string Key, JsonNode? Value)[] more)
    {
        foreach (var (k, v) in more) a[k] = v;
        return a;
    }

    [Fact]
    public void A_sweep_stands_out_of_the_face_at_its_height_along_the_whole_wall()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, f.Type("Core 20", ("Brick", 0.2, true)));
        var id = Sweep(f, wall, With(Rect(0.05, 0.1), ("side", "left"), ("height", 1.0)));
        var box = f.Bounds(id);
        Assert.Equal(0, box.Min.X, 9);
        Assert.Equal(5, box.Max.X, 9);
        Assert.Equal(0.1, box.Min.Y, 9);
        Assert.Equal(0.15, box.Max.Y, 9);
        Assert.Equal(1.0, box.Min.Z, 9);
        Assert.Equal(1.1, box.Max.Z, 9);
        Assert.Equal(5 * 0.05 * 0.1, Volume(f, id), 9);
        var q = Qs(f, id);
        Assert.Equal(5, q["Length"], 9);
        Assert.Equal(0.005, q["CrossSectionArea"], 9);
        Assert.Equal(0.025, q["Volume"], 9);
        // The wall is left whole; a profile of the project was made for it.
        Assert.Equal(3.0, Volume(f, wall), 9);
        Assert.Single(f.Model.OfClass(Profiles.Class));
        // From the top, and flipped to the other face.
        f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["fromTop"] = true, ["height"] = 0.1 } });
        f.Run("architecture.updateWallSweep", new JsonObject { ["id"] = id, ["side"] = "flip" });
        box = f.Bounds(id);
        Assert.Equal(2.9, box.Min.Z, 9);
        Assert.Equal(-0.15, box.Min.Y, 9);
    }

    [Fact]
    public void A_reveal_takes_its_profile_out_of_the_wall()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, f.Type("Core 20", ("Brick", 0.2, true)));
        // Clicked on the right of the wall: the right face.
        var id = Sweep(f, wall, new JsonObject { ["mode"] = "cut", ["catalog"] = "groove", ["params"] = new JsonObject { ["depth"] = 0.02, ["width"] = 0.03 }, ["point"] = new JsonArray(2.0, -1.0), ["height"] = 1.5 });
        var e = f.Model.Get(id)!;
        Assert.Equal("right", e.Str("side"));
        Assert.Equal("RECESS", e.PredefinedType);
        Assert.Empty(f.Session.Geometry(id)!.Parts);
        Assert.NotEmpty(f.Session.Geometry(id)!.Lines!);
        Assert.Equal(3.0 - 5 * 0.02 * 0.03, Volume(f, wall), 9);
        Assert.Equal(5 * 0.02 * 0.03, Qs(f, id)["RemovedVolume"], 9);
        // The groove is in the right face: nothing of the wall between y −0.1 and −0.08 from 1.5 to 1.53 m.
        var wallSolid = f.Session.Geometry(wall)!.Parts.Single().Solid;
        Assert.DoesNotContain(wallSolid.Vertices, v => v.Y < -0.0801 && v.Z > 1.5001 && v.Z < 1.5299);
        // Its mode turned: a sweep of its own material, the wall whole again.
        f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["mode"] = "add", ["returns"] = "none" } });
        Assert.Equal("MOLDING", f.Model.Get(id)!.PredefinedType);
        Assert.Equal("IfcCovering", f.Model.Get(id)!.Str("exportAs"));
        Assert.Equal(3.0, Volume(f, wall), 9);
        Assert.Equal(5 * 0.02 * 0.03, Volume(f, id), 9);
    }

    [Fact]
    public void Like_sweeps_on_the_faces_meeting_at_a_corner_are_mitred_into_one_another()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(5, 0, 5, 4, type);
        var sa = Sweep(f, a, With(Rect(0.05, 0.1), ("point", new JsonArray(2.5, -1.0)), ("height", 1.0)));
        // Alone, it runs to the corner of the outer faces (x 5.1) and stops square there.
        Assert.Equal(5.1, f.Bounds(sa).Max.X, 9);
        Assert.Equal(5.1 * 0.005, Volume(f, sa), 9);
        var sb = Sweep(f, b, With(Rect(0.05, 0.1), ("point", new JsonArray(6.0, 2.0)), ("height", 1.0)));
        Assert.Equal("right", f.Model.Get(sb)!.Str("side"));
        // Mitred: each reaches the corner of the sweeps' outer faces (5.15, −0.15); together, the band round the corner.
        Assert.Equal(5.15, f.Bounds(sa).Max.X, 9);
        Assert.Equal(-0.15, f.Bounds(sb).Min.Y, 9);
        Assert.Equal((5.1 + 5.15) / 2 * 0.05 * 0.1, Volume(f, sa), 9);
        Assert.Equal((4.1 + 4.15) / 2 * 0.05 * 0.1, Volume(f, sb), 9);
        // At another height they are not alike: each stops square.
        f.Run("element.update", new JsonObject { ["id"] = sb, ["attrs"] = new JsonObject { ["height"] = 1.2 } });
        Assert.Equal(5.1, f.Bounds(sa).Max.X, 9);
        Assert.Equal(-0.1, f.Bounds(sb).Min.Y, 9);
    }

    [Fact]
    public void A_sweep_stops_at_the_openings_it_would_cross_and_runs_on_between_them()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, f.Type("Core 20", ("Brick", 0.2, true)));
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2.5 });
        var low = Sweep(f, wall, With(Rect(0.05, 0.1), ("side", "left"), ("height", 1.0)));
        var q = Qs(f, low);
        Assert.Equal(2, q["Pieces"]);
        Assert.Equal(5 - 0.9, q["Length"], 9);
        Assert.Equal((5 - 0.9) * 0.005, Volume(f, low), 9);
        // Above the door it runs the whole way; and it can be told to run through.
        var high = Sweep(f, wall, With(Rect(0.05, 0.1), ("side", "left"), ("height", 2.5)));
        Assert.Equal(1, Qs(f, high)["Pieces"]);
        f.Run("element.update", new JsonObject { ["id"] = low, ["attrs"] = new JsonObject { ["gaps"] = false } });
        Assert.Equal(5, Qs(f, low)["Length"], 9);
        // From start to end along the wall, only that stretch.
        f.Run("element.update", new JsonObject { ["id"] = high, ["attrs"] = new JsonObject { ["start"] = 1.0, ["end"] = 3.0 } });
        Assert.Equal(2, Qs(f, high)["Length"], 9);
        Assert.Equal(1.0, f.Bounds(high).Min.X, 9);
    }

    [Fact]
    public void A_sweep_follows_a_curved_wall()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0, ["arc"] = 2.0, ["typeId"] = type })!["id"]!;
        var c = PathCurve.Of(f.Model.Get(wall)!);
        var id = Sweep(f, wall, With(Rect(0.05, 0.1), ("side", "left"), ("height", 1.0)));
        // Along the left face (radius r − 0.1 for an arc bulging to the left… or + 0.1): the band's middle at 0.125 from the axis.
        var sign = c.Rise > 0 ? 1 : -1;
        var expected = 0.005 * c.Sweep * (c.Radius + sign * 0.125);
        Assert.InRange(Volume(f, id) / expected, 0.999, 1.001);
        Assert.InRange(Qs(f, id)["Length"] / (c.Sweep * (c.Radius + sign * 0.1)), 0.999, 1.001);
    }

    [Fact]
    public void Sweeps_go_with_their_wall_when_it_is_copied_or_deleted()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, f.Type("Core 20", ("Brick", 0.2, true)));
        Sweep(f, wall, With(Rect(0.05, 0.1), ("side", "left"), ("height", 1.0)));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wall), ["op"] = "copy", ["by"] = new JsonArray(0.0, 3.0) });
        Assert.Equal(2, f.Model.OfClass(WallSweeps.Class).Count());
        var copy = f.Model.OfClass(WallSweeps.Class).Select(s => WallSweeps.HostOf(f.Model, s)!.Id).Single(h => h != wall);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, f.Model.OfClass(WallSweeps.Class).Single(s => WallSweeps.HostOf(f.Model, s)!.Id == copy).Id)!.Id);
        f.Run("element.delete", new JsonObject { ["id"] = wall });
        Assert.Single(f.Model.OfClass(WallSweeps.Class));
        // Mirrored across y 4, the copy's sweep faces the mirror line as the original's did.
        var before = f.Model.OfClass(WallSweeps.Class).Select(x => x.Id).ToHashSet();
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(copy), ["op"] = "mirror", ["a"] = new JsonArray(0.0, 4.0), ["b"] = new JsonArray(5.0, 4.0), ["copy"] = true });
        var mirrored = f.Model.OfClass(WallSweeps.Class).Single(x => !before.Contains(x.Id));
        var box = f.Bounds(mirrored.Id);
        Assert.Equal(4.85, box.Min.Y, 9);
        Assert.Equal(4.9, box.Max.Y, 9);
    }

    [Fact]
    public void Several_walls_take_a_sweep_at_once()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        var r = f.Run("architecture.addWallSweep", With(Rect(0.05, 0.1), ("wallIds", new JsonArray(a, b)), ("side", "left"), ("height", 0.5)))!;
        Assert.Equal(2, ((JsonArray)r["ids"]!).Count);
        // One profile for both, which a change rewrites for both.
        var profile = Assert.Single(f.Model.OfClass(Profiles.Class));
        f.Run("profile.update", new JsonObject { ["id"] = profile.Id, ["params"] = new JsonObject { ["depth"] = 0.1, ["height"] = 0.1 } });
        foreach (var s in f.Model.OfClass(WallSweeps.Class)) Assert.Equal(0.05, Volume(f, s.Id), 9);
        Assert.All(f.Model.OfClass(WallSweeps.Class), s => Assert.Contains("0.1", s.Attrs[Profiles.Key]!.ToJsonString()));
    }

    static Fixture Full() => new(new ArchitectureModule(), new Lapin.Structure.StructureModule());

    static Fixture Box(out string a, out string b)
    {
        var f = Full();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        a = f.Wall(0, 0, 5, 0, type);
        b = f.Wall(5, 0, 5, 4, type);
        Sweep(f, a, With(Rect(0.05, 0.1), ("point", new JsonArray(2.5, -1.0)), ("height", 1.0)));
        Sweep(f, b, With(Rect(0.05, 0.1), ("point", new JsonArray(6.0, 2.0)), ("height", 1.0)));
        Sweep(f, a, new JsonObject { ["mode"] = "cut", ["catalog"] = "vGroove", ["side"] = "left", ["height"] = 2.0 });
        return f;
    }

    [Fact]
    public void Sweeps_go_to_IFC_as_mouldings_and_reveals_as_recesses_and_come_back()
    {
        var f = Box(out var a, out _);
        var path = Path.Combine(_dir, "sweeps.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        var coverings = step.OfType("IFCCOVERING").ToList();
        Assert.Equal(2, coverings.Count);
        Assert.All(coverings, c => Assert.Equal(new StepEnum("MOLDING"), c[8]));
        var recess = Assert.Single(step.OfType("IFCOPENINGELEMENT"));
        Assert.Equal(new StepEnum("RECESS"), recess[8]);
        // The wall goes whole, the recess voiding it.
        Assert.Contains(step.OfType("IFCRELVOIDSELEMENT"), r => r[5] is StepRef rr && rr.Id == recess.Id);

        var g = Full();
        var r = IfcImport.Run(g.Session, path, null, out _);
        Assert.Equal(0, r.References);
        Assert.Equal(3, g.Model.OfClass(WallSweeps.Class).Count());
        foreach (var s in f.Model.OfClass(WallSweeps.Class))
        {
            var twin = g.Model.Get(s.Id)!;
            Assert.Equal(WallSweeps.HostOf(f.Model, s)!.Id, WallSweeps.HostOf(g.Model, twin)!.Id);
            Assert.Equal(s.Str("mode"), twin.Str("mode"));
            Assert.Equal(Volume(f, s.Id), Volume(g, twin.Id), 9);
            Assert.Equal(Qs(f, s.Id)["Length"], Qs(g, twin.Id)["Length"], 9);
        }
        Assert.Equal(Volume(f, a), Volume(g, a), 9);
    }

    [Fact]
    public void Sweeps_are_kept_in_the_lapin_file()
    {
        var f = Box(out var a, out _);
        var path = Path.Combine(_dir, "sweeps.lapin");
        PackageFiles.Save(f.Session, path);
        var g = Full();
        PackageFiles.Open(g.Session, path);
        Assert.Equal(3, g.Model.OfClass(WallSweeps.Class).Count());
        foreach (var s in f.Model.OfClass(WallSweeps.Class)) Assert.Equal(Volume(f, s.Id), Volume(g, s.Id), 9);
        Assert.Equal(Volume(f, a), Volume(g, a), 9);
    }

    [Fact]
    public void Catalogue_profiles_are_closed_and_turn_anticlockwise()
    {
        foreach (var c in Profiles.Catalogue)
        {
            var shape = c.Shape();
            var o = shape.Outline();
            Assert.True(o.Count >= 3, c.Id);
            Assert.True(Polygon2.SignedArea(o) > 0, c.Id);
            Assert.True(shape.Area > 0, c.Id);
            // Sweeps stand out of the face and reveals go into the wall from it: none crosses to the other side.
            if (c.Use is "sweep" or "reveal") Assert.True(o.Min(p => p.X) > -1e-9, c.Id);
        }
        // A half-round of radius r is half a circle.
        var half = Profiles.CatalogueEntry("halfRound")!.Shape(new JsonObject { ["radius"] = 0.05 });
        // Its arc as chords no more than 1 mm off it.
        Assert.InRange(half.Area / (Math.PI * 0.05 * 0.05 / 2), 0.98, 1.0);
        Assert.Equal(0.05, half.Bounds.U1, 9);
    }
}
