using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Real arcs in railings (a user's request): a railing's segment may rise off its chord (<c>arcs</c>, as a floor's edges), built
/// along the arc by the one rule for curves, its posts spaced along it, measured exactly; its corners filleted or chamfered, two
/// railings that meet made one round a fillet; drafting converted keeps a fillet's arc; IFC carries the true curve as its Axis.
/// </summary>
public sealed class RailingArcTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static JsonArray Nums(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static double Length(Fixture f, string id) => f.Session.Quantities(id).SelectMany(s => s.Quantities).Single(q => q.Name == "Length").Value;
    static string Add(Fixture f, JsonArray pts, JsonArray? arcs = null, string style = "bar")
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["style"] = style };
        if (arcs != null) a["arcs"] = arcs;
        return (string)f.Run("architecture.addRailing", a)!["id"]!;
    }
    static void Near((double X, double Y) want, Vec2 got) { Assert.Equal(want.X, got.X, 5); Assert.Equal(want.Y, got.Y, 5); }

    [Fact]
    public void A_railing_s_segment_may_be_an_arc_built_along_it_and_measured_exactly_and_a_straight_one_reads_as_before()
    {
        var f = new Fixture();
        var straight = Add(f, Pts((0, 5), (4, 5)));
        Assert.Null(f.Model.Get(straight)!.Attrs["arcs"]);
        // A half circle of radius 2 from (0, 0) to (4, 0), bulging to −y: to the right of its way, so a negative rise.
        var id = Add(f, Pts((0, 0), (4, 0)), Nums(-2));
        Assert.Equal(2 * Math.PI, Length(f, id), 6);
        var v = f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.InRange(v.Min(p => p.Y), -2.06, -1.97);
        // In plan: one band round the arc, its chords no more than a millimetre off it.
        var plan = Assert.Single(f.Session.Geometry(id)!.Plan!);
        Assert.True(plan.Points.Count > 20);
        // Its posts along the arc, the bar style's 1.2 m apart at most: ⌈2π / 1.2⌉ + 1 = 7, none at the chords' points between.
        var bar = Railings.Of(f.Model.Get(id)!);
        var path = Railings.Chorded(Railings.Points(f.Model.Get(id)!), Railings.Rises(f.Model.Get(id)!, 2));
        var (metal, _) = Railings.Solids(path.Points.Select(p => p.At(0)).ToList(), bar, null, path.PieceOf);
        var (straightMetal, _) = Railings.Solids(path.Points.Select(p => p.At(0)).ToList(), bar);
        Assert.Equal(7 + path.Points.Count - 1, metal.Count);
        Assert.True(straightMetal.Count > metal.Count);
    }

    [Fact]
    public void A_railing_s_corner_is_filleted_or_chamfered_in_one_step_and_its_segments_bent_or_straightened()
    {
        var f = new Fixture();
        var id = Add(f, Pts((0, 0), (4, 0), (4, 3)));
        f.Run("architecture.roundRailingCorner", new JsonObject { ["id"] = id, ["index"] = 1, ["radius"] = 1.0 });
        var e = f.Model.Get(id)!;
        var pts = Railings.Points(e);
        Assert.Equal(4, pts.Count);
        Near((3, 0), pts[1]);
        Near((4, 1), pts[2]);
        var c = Railings.Segment(pts, Railings.Rises(e, 4), 1);
        Assert.Equal(1, c.Radius, 5);
        Assert.Equal(3 + Math.PI / 2 + 2, Length(f, id), 5);
        f.Session.Undo();
        Assert.Equal(3, Railings.Points(f.Model.Get(id)!).Count);
        // FILLET on the railing picked twice, on the segments either side of the corner: the same.
        f.Run("architecture.filletRailings", new JsonObject { ["a"] = id, ["b"] = id, ["atA"] = new JsonArray(2, 0), ["atB"] = new JsonArray(4, 2), ["radius"] = 1.0 });
        Assert.Equal(3 + Math.PI / 2 + 2, Length(f, id), 5);
        f.Session.Undo();
        f.Run("architecture.chamferRailingCorner", new JsonObject { ["id"] = id, ["index"] = 1, ["distance"] = 1.0 });
        Assert.Equal(3 + Math.Sqrt(2) + 2, Length(f, id), 5);
        // A segment bent, then straight again; its points moved without arcs keep them.
        f.Run("architecture.setRailingArc", new JsonObject { ["id"] = id, ["index"] = 0, ["rise"] = 0.5 });
        Assert.Equal(0.5, Railings.Rises(f.Model.Get(id)!, 4)[0], 9);
        f.Run("architecture.updateRailing", new JsonObject { ["id"] = id, ["points"] = Pts((0, -1), (3, 0), (4, 1), (4, 3)) });
        Assert.Equal(0.5, Railings.Rises(f.Model.Get(id)!, 4)[0], 9);
        f.Run("architecture.updateRailing", new JsonObject { ["id"] = id, ["points"] = Pts((0, -1), (3, 0), (4, 1), (4, 3)), ["arcs"] = Nums(0, 0, 0) });
        Assert.Null(f.Model.Get(id)!.Attrs["arcs"]);
        // Too large, or at an end: refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.roundRailingCorner", new JsonObject { ["id"] = id, ["index"] = 0, ["radius"] = 0.2 }));
        Assert.Throws<CommandException>(() => f.Run("architecture.roundRailingCorner", new JsonObject { ["id"] = id, ["index"] = 2, ["radius"] = 50.0 }));
    }

    [Fact]
    public void Two_railings_that_meet_or_would_meet_are_made_one_round_a_fillet()
    {
        var f = new Fixture();
        var a = Add(f, Pts((0, 0), (3, 0)));
        var b = Add(f, Pts((5, 4), (5, 1)));
        f.Run("architecture.filletRailings", new JsonObject { ["a"] = a, ["b"] = b, ["radius"] = 1.0 });
        Assert.Null(f.Model.Get(b));
        var e = f.Model.Get(a)!;
        var pts = Railings.Points(e);
        Assert.Equal(4, pts.Count);
        Near((0, 0), pts[0]);
        Near((4, 0), pts[1]);
        Near((5, 1), pts[2]);
        Near((5, 4), pts[3]);
        Assert.Equal(4 + Math.PI / 2 + 3, Length(f, a), 5);
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(b));
        // No radius: one railing with a sharp corner.
        f.Run("architecture.filletRailings", new JsonObject { ["a"] = a, ["b"] = b, ["radius"] = 0.0 });
        Assert.Equal(5 + 4, Length(f, a), 5);
        // Of another design: refused unless the first one's is taken.
        var c = Add(f, Pts((10, 0), (12, 0)), style: "glass");
        var d = Add(f, Pts((12, 0), (12, 2)));
        Assert.Contains("differ", Assert.Throws<CommandException>(() => f.Run("architecture.filletRailings", new JsonObject { ["a"] = c, ["b"] = d, ["radius"] = 0.5 })).Message);
        f.Run("architecture.filletRailings", new JsonObject { ["a"] = c, ["b"] = d, ["radius"] = 0.5, ["matchFirst"] = true });
        Assert.Equal("glass", f.Model.Get(c)!.Str("style"));
    }

    [Fact]
    public void Drafting_filleted_and_converted_makes_one_railing_with_the_true_arc_and_IFC_carries_it_as_its_axis()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;
        var ids = f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan, ["layer"] = "0",
            ["shapes"] = new JsonArray(
                new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(4, 0) },
                new JsonObject { ["type"] = "line", ["a"] = new JsonArray(4, 0), ["b"] = new JsonArray(4, 3) }),
        })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var arc = (string)f.Run("drafting.fillet", new JsonObject { ["a"] = ids[0], ["atA"] = new JsonArray(1, 0), ["b"] = ids[1], ["atB"] = new JsonArray(4, 2), ["radius"] = 1.0 })!["id"]!;
        f.Run("drafting.convert", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray(ids[0], arc, ids[1]), ["converter"] = "railing", ["options"] = new JsonObject() });
        var railing = Assert.Single(f.Model.OfClass(Railings.Class));
        Assert.Equal(4, Railings.Points(railing).Count);
        Assert.Equal(3 + Math.PI / 2 + 2, Length(f, railing.Id), 5);
        var path = Path.Combine(Path.GetTempPath(), $"railing-arc-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var file = StepFile.Read(path);
            Assert.Single(file.OfType("IFCRAILING"));
            var curve = Assert.Single(file.OfType("IFCINDEXEDPOLYCURVE"));
            Assert.Contains("IFCARCINDEX", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
}
