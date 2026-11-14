using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Cable trays, ladders, trunking and conduit (report #147): drawn as runs are, their fittings worked out, exported, cables laid in them.</summary>
public class MepCarrierTests
{
    static string Carrier(MepFixture f, JsonArray points, JsonObject? more = null)
    {
        var a = new JsonObject { ["points"] = points };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return f.Id("mep.drawCarrier", a);
    }

    static StepFile Export(MepFixture f)
    {
        var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "carriers.ifc");
        return StepFile.Read(ms.ToArray());
    }

    [Fact]
    public void A_tray_drawn_with_a_bend_a_rise_and_a_tee_has_its_size_and_fittings()
    {
        var f = new MepFixture();
        // A 200×60 tray at 2.4 m: 4 m east, turning north for 3 m, rising to 2.8 m at its end's last point.
        var tray = Carrier(f, MepFixture.Pts((0, 0), (4, 0), (4, 3), (4, 5)), new JsonObject { ["preset"] = "tray200x60", ["levels"] = new JsonArray(null, null, 2.8, 2.8) });
        var e = f.Model.Get(tray)!;
        Assert.Equal("CABLETRAYSEGMENT", e.PredefinedType);
        var s = CableCarriers.SpecOf(f.Model, e);
        Assert.Equal(0.2, s.Width, 6);
        Assert.Equal(0.06, s.Height, 6);
        Assert.Equal("200×60", s.SizeLabel);
        // A branch from the first leg's middle, south: a tee on the tray.
        var branch = Carrier(f, MepFixture.Pts((2, 0), (2, -2)), new JsonObject { ["preset"] = "tray100x60", ["start"] = new JsonObject { ["carrierId"] = tray } });
        var fit = CarrierBodies.FittingsOf(f.Model, f.Model.Get(tray)!);
        // The plan turn, and the rise at (4, 3): up, then on north — two bends there.
        Assert.Equal(3, fit.Bends);
        Assert.Equal(1, fit.Tees);
        Assert.Equal(0, fit.Crosses);

        var g = f.Session.Geometry(tray)!;
        Assert.Contains(g.Parts, p => p.Key == "fitting:tee:" + branch);
        Assert.Equal(3, g.Parts.Count(p => p.Key?.StartsWith("fitting:bend:", StringComparison.Ordinal) == true));
        // The tray's body: 200 mm across the first leg, its floor at 2.4 − 30 mm and its sides' tops at 2.4 + 30 mm, the rise to 2.8 + 30 mm.
        var (min, max) = f.Bounds(tray, "carrier");
        Assert.Equal(-0.1, min.Y, 3);
        Assert.Equal(2.37, min.Z, 3);
        Assert.Equal(2.83, max.Z, 3);
        // The branch leaves the trunk's side: its own body starts past the trunk's half width and the mouth.
        var (bmin, bmax) = f.Bounds(branch, "carrier");
        Assert.True(bmax.Y <= -0.1 - 0.05 + 1e-6, $"branch reaches {bmax.Y}");
        Assert.Equal(-2, bmin.Y, 3);
        // Its plan: outline and centreline, the centreline dash-dotted.
        Assert.Contains(g.Plan!, i => i.Style?.Pattern == "center");
        Assert.True(g.Plan!.Count(i => i.Kind == "symbol") >= 3);
        // Length: 4 + 3 + 2 + 0.4 up.
        var length = f.Session.Quantities(tray).Single(q => q.Name == "Lapin_CableCarrier").Quantities.Single(q => q.Name == "Length").Value;
        Assert.Equal(9.4, length, 6);
    }

    [Fact]
    public void A_carrier_is_laid_at_45_degree_steps()
    {
        var f = new MepFixture();
        var id = Carrier(f, MepFixture.Pts((0, 0), (3, 0.4), (5, 2.3)));
        var pts = CableCarriers.Points(f.Model.Get(id)!);
        Assert.Equal(0, pts[1].Y, 6);
        Assert.Equal(3, pts[1].X, 6);
        // The second leg at 45°.
        Assert.Equal(pts[2].X - pts[1].X, pts[2].Y - pts[1].Y, 6);
    }

    [Fact]
    public void Kinds_export_as_cable_carrier_segments_with_their_fittings_types_and_ports()
    {
        var f = new MepFixture();
        var tray = Carrier(f, MepFixture.Pts((0, 0), (4, 0), (4, 3)), new JsonObject { ["preset"] = "ladder300x100" });
        var branch = Carrier(f, MepFixture.Pts((2, 0), (2, 2)), new JsonObject { ["preset"] = "tray100x60", ["start"] = new JsonObject { ["carrierId"] = tray } });
        Carrier(f, MepFixture.Pts((0, 5), (3, 5)), new JsonObject { ["preset"] = "trunking60x40" });
        Carrier(f, MepFixture.Pts((0, 7), (3, 7)), new JsonObject { ["preset"] = "conduit20" });
        // A smaller tray going on from the ladder's end: a reducer.
        var smaller = Carrier(f, MepFixture.Pts((4, 3), (4, 6)), new JsonObject { ["preset"] = "tray200x60", ["start"] = new JsonObject { ["carrierId"] = tray } });
        Assert.Equal(1, CarrierBodies.FittingsOf(f.Model, f.Model.Get(smaller)!).Reducers);
        var ifc = Export(f);

        var segments = ifc.OfType("IFCCABLECARRIERSEGMENT").ToList();
        Assert.Equal(5, segments.Count);
        Assert.Equal(["CABLELADDERSEGMENT", "CABLETRAYSEGMENT", "CABLETRAYSEGMENT", "CABLETRUNKINGSEGMENT", "CONDUITSEGMENT"],
            segments.Select(s => ((StepEnum)s[8]!).Value).Order().ToArray());
        var fittings = ifc.OfType("IFCCABLECARRIERFITTING").Select(x => ((StepEnum)x[8]!).Value).ToList();
        Assert.Contains("BEND", fittings);
        Assert.Contains("TEE", fittings);
        Assert.Contains("REDUCER", fittings);
        Assert.Empty(ifc.OfType("IFCBUILDINGELEMENTPROXY"));
        Assert.Equal(5, ifc.OfType("IFCCABLECARRIERSEGMENTTYPE").Count());
        // Ports: CABLECARRIER, the branch's start on the trunk's tee port.
        var ports = ifc.OfType("IFCDISTRIBUTIONPORT").ToList();
        Assert.All(ports, p => Assert.Equal("CABLECARRIER", ((StepEnum)p[8]!).Value));
        Assert.Contains(ports, p => StepFile.Decode((string)p[2]!) == "tee:" + branch);
        Assert.Equal(2, ifc.OfType("IFCRELCONNECTSPORTS").Count());
    }

    [Fact]
    public void A_cable_run_laid_in_a_carrier_fills_it()
    {
        var f = new MepFixture();
        var trunking = Carrier(f, MepFixture.Pts((0, 0), (4, 0)), new JsonObject { ["preset"] = "trunking40x40" });
        var system = f.System(Services.Electrical);
        var cables = Enumerable.Range(0, 3).Select(i => f.Draw(system, MepFixture.Pts((0, 0.01 * i), (4, 0.01 * i)), more: new JsonObject { ["size"] = 0.01 })).ToList();
        f.Run("mep.setCableCarrier", new JsonObject { ["ids"] = new JsonArray([.. cables.Select(c => (JsonNode)c)]), ["carrierId"] = trunking });
        var fill = (JsonObject)f.Session.Query("mep.carrierFill", new JsonObject { ["id"] = trunking })!;
        // Three Ø10 cables in 40×40 trunking of 1.2 mm walls: 235.6 mm² over 37.6 × 37.6 mm².
        var expected = 100 * 3 * Math.PI * 0.005 * 0.005 / ((0.04 - 0.0024) * (0.04 - 0.0024));
        Assert.Equal(Math.Round(expected, 1), (double)fill["percent"]!, 6);
        Assert.Equal(45, (double)fill["limit"]!);
        Assert.False((bool)fill["over"]!);
        Assert.Equal(3, (int)fill["count"]!);
        // A heating run is no cable: refused.
        var heating = f.System();
        var pipe = f.Draw(heating, MepFixture.Pts((0, 1), (4, 1)));
        Assert.Throws<CommandException>(() => f.Run("mep.setCableCarrier", new JsonObject { ["ids"] = new JsonArray(pipe), ["carrierId"] = trunking }));
        // The schedule counts it, with its fill.
        var grid = new CarrierSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, r => r[3] == "40×40" && r[10] == Math.Round(expected, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Each_carrier_command_is_one_undo_step()
    {
        var f = new MepFixture();
        var tray = Carrier(f, MepFixture.Pts((0, 0), (4, 0)), new JsonObject { ["preset"] = "tray300x60" });
        f.Run("mep.setCarrierPoints", new JsonObject { ["id"] = tray, ["points"] = MepFixture.Pts((0, 0), (4, 0), (4, 2)) });
        Assert.Equal(3, CableCarriers.Points(f.Model.Get(tray)!).Count);
        var type = f.Id("mep.createCarrierType", new JsonObject { ["kind"] = "solid", ["width"] = 0.5, ["height"] = 0.1, ["name"] = "My tray" });
        f.Run("mep.setCarrierType", new JsonObject { ["ids"] = new JsonArray(tray), ["typeId"] = type });
        Assert.Equal(0.5, CableCarriers.SpecOf(f.Model, f.Model.Get(tray)!).Width, 6);
        f.Session.Undo();
        Assert.Equal(0.3, CableCarriers.SpecOf(f.Model, f.Model.Get(tray)!).Width, 6);
        f.Session.Undo();
        Assert.Null(f.Model.Get(type));
        f.Session.Undo();
        Assert.Equal(2, CableCarriers.Points(f.Model.Get(tray)!).Count);
        f.Session.Undo();
        Assert.Null(f.Model.Get(tray));
    }
}
