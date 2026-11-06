using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A wall's own end cut (<see cref="Joins.OwnEnd"/>, the attributes startAngle and endAngle): a free end turned from square,
/// the cut through the end's point on the axis, each layer taking its share; a join at that end takes it over. Recognition
/// sets it where the file cuts an end so and no join of Lapin's does (docs/spec-ifc.md, Îmbinările).
/// </summary>
public class WallEndCutTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-wall-end-cut").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static (Fixture F, string Wall) Alone(double startAngle)
    {
        var f = new Fixture();
        var type = f.Type("Brick 30", ("Brick", 0.3, true));
        var wall = f.Wall(0, 0, 5, 0, type);
        if (startAngle != 0) f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { [Joins.StartAngle] = startAngle } });
        return (f, wall);
    }

    [Fact]
    public void A_free_end_cut_at_45_turns_its_faces_and_keeps_the_volume()
    {
        var (f, wall) = Alone(45);
        var (square, sq) = Alone(0);
        // Through the axis: the left face (+Y) 150 mm on past the start, the right face 150 mm short of it; the same volume.
        var box = f.Bounds(wall);
        Assert.Equal(-0.15, box.Min.X, 6);
        Assert.Equal(Volume(square, sq), Volume(f, wall), 6);
        var cut = f.Session.Geometry(wall)!.Plan!.Where(p => p.Kind == "cut").SelectMany(p => p.Points).ToList();
        Assert.Contains(cut, p => (p - new Vec2(-0.15, 0.15)).Length < 1e-6);
        Assert.Contains(cut, p => (p - new Vec2(0.15, -0.15)).Length < 1e-6);
        // The rooms' outline follows it.
        var fp = RoomBounds.JoinedFootprint(f.Model.Get(wall)!, f.Model, f.Session.Catalog.Get<IPathElements>()!)!;
        Assert.Contains(fp, p => (p - new Vec2(-0.15, 0.15)).Length < 1e-6);
        // Too steep is held at the most an end may turn.
        Assert.ThrowsAny<Exception>(() => f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { [Joins.EndAngle] = 89 } }));
    }

    [Fact]
    public void A_join_at_that_end_takes_it_over()
    {
        // The same T at the start, with and without the angle: the join decides, the angle waits for the end to be free.
        Fixture Tee(double angle)
        {
            var (f, wall) = Alone(angle);
            f.Wall(0, -2, 0, 2, f.Model.OfClass(IfcClass.WallType).First().Id);
            Assert.NotEmpty(Joins.Of(f.Model, wall));
            return f;
        }
        var (a, b) = (Tee(30), Tee(0));
        var wa = a.Model.OfClass(IfcClass.Wall).First(w => Walls.Start(w).Y == 0 && Walls.End(w).Y == 0);
        var wb = b.Model.OfClass(IfcClass.Wall).First(w => Walls.Start(w).Y == 0 && Walls.End(w).Y == 0);
        Assert.Equal(30, wa.Num(Joins.StartAngle), 6);
        Assert.Equal(Volume(b, wb.Id), Volume(a, wa.Id), 6);
        Assert.Equal(b.Bounds(wb.Id).Min.X, a.Bounds(wa.Id).Min.X, 6);
    }

    [Fact]
    public void A_lintel_by_an_angled_end_stays_inside_the_wall()
    {
        // A door 0.15 m from the start's nearer face: its lintel bears on the masonry up to that face, never past the cut.
        var (f, wall) = Alone(45);
        var (square, sq) = Alone(0);
        foreach (var (g, w) in new[] { (f, wall), (square, sq) })
            g.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 0.65 });
        var lintel = f.Session.Geometry(wall)!.Parts.Single(p => p.Key?.StartsWith("lintel:", StringComparison.Ordinal) == true);
        Assert.InRange(Measure.Bounds(lintel.Solid)!.Value.Min.X, 0.15 - 1e-6, 1);
        Assert.Equal(Volume(square, sq), Volume(f, wall), 6);
    }

    [Fact]
    public void The_cut_goes_to_IFC_and_comes_back()
    {
        var (a, wall) = Alone(-30);
        var path = Path.Combine(_dir, "angled.ifc");
        IfcExport.Write(a.Session, path);
        var b = new Fixture(new ArchitectureModule());
        IfcImport.Run(b.Session, path, null, out _);
        var twin = b.Model.Get(wall)!;
        Assert.Equal(-30, twin.Num(Joins.StartAngle), 6);
        Assert.Equal(Volume(a, wall), Volume(b, wall), 6);
        Assert.Equal(a.Bounds(wall).Min.X, b.Bounds(wall).Min.X, 6);
    }
}
