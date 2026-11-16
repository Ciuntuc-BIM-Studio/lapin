using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A wall opening of its own outline (audit S4, the Opening tool's missing kind): a closed outline on the wall's face (along it,
/// up it), its arcs too, cut through the wall as an IfcOpeningElement of that profile; resized, it stretches; another head given,
/// the outline goes; drafted on a section or an elevation, Convert to… makes it in the wall the view looks at.
/// </summary>
public sealed class OutlinedWallOpeningTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-outlined-openings").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void An_opening_of_its_own_outline_cuts_its_shape_through_the_wall()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0, height: 3);
        var whole = Vol(f, wall);
        var thick = Walls.Section(f.Model, f.Model.Get(wall)!).Sum(b => b.To - b.From);
        // A triangle 2 m wide, 1.5 m high, from 1 m up: its area 1.5 m².
        var id = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["profile"] = Pts((3, 1), (5, 1), (4, 2.5)) })!["id"]!;
        var o = f.Model.Get(id)!;
        Assert.Equal("outline", o.Str("shape"));
        Assert.Equal(4, o.Num("along"), 9);
        Assert.Equal(1, o.Num("sill"), 9);
        Assert.Equal(2, o.Num("width"), 9);
        Assert.Equal(1.5, o.Num("height"), 9);
        Assert.Equal(whole - 1.5 * thick, Vol(f, wall), 6);
        // Resized, it stretches with its box.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["width"] = 3 });
        Assert.Equal(whole - 2.25 * thick, Vol(f, wall), 6);
        // A rectangle given, its outline goes.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["shape"] = "rect" });
        Assert.Null(f.Model.Get(id)!.Attrs["profile"]);
        Assert.Equal(whole - 3 * 1.5 * thick, Vol(f, wall), 6);
    }

    [Fact]
    public void Its_arcs_are_cut_along_the_curve()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0, height: 3);
        var whole = Vol(f, wall);
        var thick = Walls.Section(f.Model, f.Model.Get(wall)!).Sum(b => b.To - b.From);
        // A round opening, radius 0.5, its centre 1.5 m up at 2 m along: four quarter arcs.
        var (c, r) = ArcOutline.Circle(new Vec2(2, 1.5), 0.5);
        f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["profile"] = new JsonArray([.. c.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]), ["arcs"] = new JsonArray([.. r.Select(x => (JsonNode)x)]) });
        var cut = whole - Vol(f, wall);
        Assert.True(Math.Abs(cut - Math.PI * 0.25 * thick) / (Math.PI * 0.25 * thick) < 0.003, $"{cut}");
        // Past the wall, refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["profile"] = Pts((7, 1), (9, 1), (8, 2)) }));
    }

    [Fact]
    public void Ifc_writes_it_as_an_opening_element_of_its_shape_and_reads_it_back()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0, height: 3);
        var id = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["profile"] = Pts((3, 1), (5, 1), (4, 2.5)) })!["id"]!;
        var path = Path.Combine(_dir, "opening.ifc");
        IfcExport.Write(f.Session, path);
        Assert.Contains(StepFile.Read(path).OfType("IFCOPENINGELEMENT"), e => e.ToString()!.Length > 0);
        var g = F();
        IfcImport.Run(g.Session, path, null, out _);
        var back = g.Model.Get(id)!;
        Assert.Equal("outline", back.Str("shape"));
        Assert.Equal(Vol(f, wall), Vol(g, wall), 4);
    }

    [Fact]
    public void Drafted_on_a_section_it_becomes_an_opening_in_the_wall_it_looks_at()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0, height: 3);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(11, -2) })!["id"]!;
        var shape = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = section, ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(3, 1.5), ["r"] = 0.5 } })!["ids"]![0]!;
        f.Run("drafting.convert", new JsonObject { ["viewId"] = section, ["ids"] = new JsonArray(shape), ["converter"] = "wallOpening", ["options"] = new JsonObject() });
        var o = f.Model.OfClass(IfcClass.OpeningElement).Single(x => x.Str("shape") == "outline");
        Assert.Equal(1, o.Num("width"), 6);
        Assert.Equal(1, o.Num("sill"), 6);
        // The section runs from x = −1 (station 0) whichever way it looks: the circle's middle is 3 m along it.
        var along = o.Num("along");
        Assert.True(Math.Abs(along - 2) < 1e-6 || Math.Abs(along - 6) < 1e-6, $"{along}");
    }
}
