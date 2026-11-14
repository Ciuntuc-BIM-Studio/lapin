using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Shop drawings, stage S2 (docs/plan-shop-drawings.md, decision 582): a steel member's features (holes in runs, copes, cut ends)
/// in its frame, its rule's dimensions of them ("4×80 = 320", ordinates from its start, a notch's length and depth), a section
/// through each notch, dimensions that follow the part and a moved string that keeps its move.
/// </summary>
public class ShopFeatureTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static string Id(JsonNode? n) => (string)n!["id"]!;

    static string Member(Fixture f) =>
        Id(f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 1, ["y1"] = 2, ["z1"] = 1, ["x2"] = 5, ["y2"] = 5, ["z2"] = 1 }));

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;

    static List<string> Texts(JsonObject d) =>
        ((JsonArray)d["pens"]!).OfType<JsonObject>().Where(p => p["texts"] is JsonArray).SelectMany(p => ((JsonArray)p["texts"]!).OfType<JsonObject>()).Select(t => (string)t["text"]!).ToList();

    static void Holes(Fixture f, string member, double station, int count, double pitch) =>
        f.Run("steel.addHoles", new JsonObject { ["id"] = member, ["station"] = station, ["count"] = count, ["pitch"] = pitch, ["diameter"] = 0.022 });

    [Fact]
    public void Two_runs_of_holes_are_dimensioned_as_runs_and_by_ordinates_from_the_start()
    {
        var f = F();
        var member = Member(f);
        Holes(f, member, 0.3, 5, 0.08);
        Holes(f, member, 3.0, 3, 0.1);
        var features = (JsonArray)f.Session.Query("element.features", new JsonObject { ["id"] = member })!;
        Assert.Equal(8, features.Count);
        // Their stations along the member, as drilled.
        var stations = features.Select(x => Math.Round((double)x!["at"]![0]! * 1000)).ToList();
        Assert.Equal([300, 380, 460, 540, 620, 3000, 3100, 3200], stations);
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = member })!;
        Assert.Equal("steel.member", f.Model.Get((string)made["id"]!)!.Str("rule"));
        var front = Drawing(f, (string)made["views"]!["front"]!);
        var texts = Texts(front);
        Assert.Contains("4×80 = 320", texts);
        Assert.Contains("2×100 = 200", texts);
        foreach (var s in new[] { "300", "380", "460", "540", "620", "3000", "3100", "3200" }) Assert.Contains(s, texts);
        Assert.Contains("5× Ø22", texts);
        Assert.Contains("3× Ø22", texts);
        // The rows across: the holes on the web's middle, 100 mm up the IPE 200.
        Assert.Contains("100", texts);
    }

    [Fact]
    public void A_cope_gets_its_length_and_depth_and_a_section_through_it()
    {
        var f = F();
        var member = Member(f);
        f.Run("steel.setEnds", new JsonObject { ["ids"] = new JsonArray(member), ["start"] = new JsonObject { ["cut"] = "square", ["copeLength"] = 0.15, ["copeDepth"] = 0.04, ["copeTop"] = true } });
        var features = (JsonArray)f.Session.Query("element.features", new JsonObject { ["id"] = member })!;
        var notch = Assert.Single(features.OfType<JsonObject>(), x => (string?)x["kind"] == "notch");
        Assert.Equal("notch:start:top", (string?)notch["key"]);
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = member })!;
        var texts = Texts(Drawing(f, (string)made["views"]!["front"]!));
        Assert.Contains("150", texts);
        Assert.Contains("40", texts);
        // The section at the notch: through its middle, cutting the member square to its axis.
        var sectionId = (string)made["views"]!["feature:notch:start:top"]!;
        var view = f.Model.Get(sectionId)!;
        Assert.Equal("section:x", ElementViews.RoleOf(view));
        var frame = ElementFrames.Of(f.Session.Reader, f.Model.Get(member)!)!;
        Assert.Equal(frame.Min.X + 0.075, (double)view.Attrs["of"]!["station"]!, 6);
        var d = Drawing(f, sectionId);
        Assert.Null(d["problem"]);
        // Cut through the cope, the section is 40 mm lower than the IPE's 200.
        var cuts = Views.Render(f.Session.Reader, view).Generated!.Cuts.SelectMany(c => c.Loops.SelectMany(l => l)).ToList();
        Assert.Equal(0.16, cuts.Max(p => p.Y) - cuts.Min(p => p.Y), 3);
    }

    [Fact]
    public void The_dimensions_follow_a_hole_moved_and_a_moved_string_keeps_its_move()
    {
        var f = F();
        var member = Member(f);
        Holes(f, member, 0.3, 5, 0.08);
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = member })!;
        var front = (string)made["views"]!["front"]!;
        double ChainY()
        {
            var mk = ((JsonArray)Drawing(f, front)["marks"]!).OfType<JsonObject>().Single(m => ((string)m["id"]!).EndsWith(":partdim:holes:chain", StringComparison.Ordinal));
            return (double)((JsonArray)mk["snaps"]!)[1]!;
        }
        var y0 = ChainY();
        f.Run("drawing.moveDimension", new JsonObject { ["viewId"] = front, ["key"] = "holes:chain", ["by"] = new JsonArray(0, 0.25) });
        Assert.Equal(y0 + 0.25, ChainY(), 6);
        // The run drilled again 100 mm further: the ordinates say so, the string stays where it was moved.
        f.Run("steel.clearHoles", new JsonObject { ["ids"] = new JsonArray(member) });
        Holes(f, member, 0.4, 5, 0.08);
        var texts = Texts(Drawing(f, front));
        Assert.Contains("400", texts);
        Assert.DoesNotContain("300", texts);
        Assert.Equal(y0 + 0.25, ChainY(), 6);
        // Put back.
        f.Run("drawing.moveDimension", new JsonObject { ["viewId"] = front, ["key"] = "holes:chain", ["reset"] = true });
        Assert.Equal(y0, ChainY(), 6);
    }

    [Fact]
    public void A_member_cut_off_square_shows_its_angle_from_the_front()
    {
        var f = F();
        var member = Member(f);
        f.Run("steel.setEnds", new JsonObject { ["ids"] = new JsonArray(member), ["end"] = new JsonObject { ["cut"] = "plane", ["elevAngle"] = 30 } });
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = member })!;
        Assert.Contains("30°", Texts(Drawing(f, (string)made["views"]!["front"]!)));
        Assert.DoesNotContain("30°", Texts(Drawing(f, (string)made["views"]!["top"]!)));
    }

    [Fact]
    public void Holes_read_from_the_body_alone_are_the_holes_drilled()
    {
        var f = F();
        var member = Member(f);
        Holes(f, member, 0.5, 3, 0.1);
        var e = f.Model.Get(member)!;
        var frame = ElementFrames.Of(f.Session.Reader, e)!;
        var own = ElementFeatures.Of(f.Session.Reader, e);
        var body = ElementFeatures.FromBody(frame, f.Session.Geometry(member)!.Parts.Select(p => p.Solid).ToList());
        Assert.Equal(3, body.Count);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(own[i].At.X, body[i].At.X, 3);
            Assert.Equal(own[i].At.Z, body[i].At.Z, 3);
            Assert.Equal(0.022, body[i].Diameter, 3);
            Assert.Equal(1.0, Math.Abs(body[i].Axis.Y), 6);
        }
    }
}
