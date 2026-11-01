using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Architecture;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>A railing of its own (testers' request, decision 238): drawn along a balcony's edge, in the stair railings' designs, an IfcRailing.</summary>
public class RailingTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);

    [Fact]
    public void A_railing_along_a_balcony_s_edge_stands_on_its_storey_in_its_design()
    {
        // With Studio too, which owns IfcRailing for its solids: the two live side by side.
        var f = new Fixture(new ArchitectureModule(), new StudioModule());
        var id = (string)f.Run("architecture.addRailing", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (4, 0), (4, 1.5)) })!["id"]!;
        var e = f.Model.Get(id)!;
        Assert.Equal(("LapinRailing", "IfcRailing", "GUARDRAIL", "balusters"), (e.Class, e.Str("exportAs"), e.PredefinedType, e.Str("style")));
        var g = f.Session.Geometry(id)!;
        var v = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        // 1 m high (the handrail's top a little over), from the floor, along the two edges.
        Assert.InRange(v.Max(p => p.Z), 1.0, 1.06);
        Assert.Equal(0, v.Min(p => p.Z), 6);
        Assert.InRange(v.Max(p => p.X), 4.0, 4.06);
        Assert.Equal(2, g.Plan!.Count);
        Assert.Equal(5.5, f.Session.Quantities(id).SelectMany(s => s.Quantities).Single(q => q.Name == "Length").Value, 9);
        // Glass between posts: its panes a part of their own; a panel, one solid.
        f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["style"] = "glass", ["height"] = 1.1, ["baseOffset"] = 0.2 } });
        g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "railing-glass");
        Assert.InRange(g.Parts.SelectMany(p => p.Solid.Vertices).Max(p => p.Z), 1.3, 1.36);
        // Its line moved.
        f.Run("architecture.updateRailing", new JsonObject { ["id"] = id, ["points"] = Pts((0, 0), (6, 0)) });
        Assert.Equal(6, f.Session.Quantities(id).SelectMany(s => s.Quantities).Single(q => q.Name == "Length").Value, 9);
        Assert.Throws<CommandException>(() => f.Run("architecture.addRailing", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((1, 1)) }));
    }

    [Fact]
    public void A_railing_goes_to_IFC_as_a_guardrail()
    {
        var f = new Fixture();
        f.Run("architecture.addRailing", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (3, 0)), ["style"] = "bar" });
        var path = Path.Combine(Path.GetTempPath(), $"railing-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var r = Assert.Single(StepFile.Read(path).OfType("IFCRAILING"));
            Assert.Equal(new StepEnum("GUARDRAIL"), r[8]);
        }
        finally { File.Delete(path); }
    }
}
