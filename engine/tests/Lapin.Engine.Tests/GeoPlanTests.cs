using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Plans of a georeferenced project (decision 447): a plan's DXF in map or base point coordinates, and the spot coordinate.</summary>
public class GeoPlanTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => new(x, y);
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static void Locate(Fixture f, double north = 0) => f.Run("project.location", new JsonObject
    {
        ["base"] = new JsonObject { ["x"] = 2, ["y"] = 1, ["northAngle"] = north },
        ["survey"] = new JsonObject { ["x"] = 10, ["y"] = 5, ["easting"] = 500000, ["northing"] = 4500000 },
    });

    /// <summary>The DXF's group codes and values, paired.</summary>
    static List<(string Code, string Value)> Pairs(byte[] dxf)
    {
        var lines = System.Text.Encoding.Latin1.GetString(dxf).Split('\n');
        return Enumerable.Range(0, lines.Length / 2).Select(i => (lines[2 * i].Trim(), lines[2 * i + 1].Trim())).ToList();
    }

    /// <summary>Each LINE's two ends.</summary>
    static List<(double X1, double Y1, double X2, double Y2)> Lines(byte[] dxf)
    {
        var p = Pairs(dxf);
        var r = new List<(double, double, double, double)>();
        for (var i = 0; i < p.Count; i++)
        {
            if (p[i] != ("0", "LINE")) continue;
            double V(string code) => double.Parse(p.Skip(i + 1).TakeWhile(x => x.Code != "0").First(x => x.Code == code).Value, System.Globalization.CultureInfo.InvariantCulture);
            r.Add((V("10"), V("20"), V("11"), V("21")));
        }
        return r;
    }

    static string Units(byte[] dxf)
    {
        var p = Pairs(dxf);
        var i = p.FindIndex(x => x.Value == "$INSUNITS");
        return p[i + 1].Value;
    }

    [Fact]
    public void A_plan_goes_to_DXF_in_map_coordinates_in_metres_turned_to_true_north()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(10, 5), ["b"] = J(20, 5) } });
        var view = Views.Find(f.Model, plan)!;
        // Without a survey point there is no map to write in.
        Assert.Throws<CommandException>(() => Views.Dxf(f.Session.Reader, view, "map"));
        Locate(f);
        var own = Views.Dxf(f.Session.Reader, view);
        Assert.Equal("4", Units(own));
        Assert.Contains(Lines(own), l => l == (10000, 5000, 20000, 5000));
        // From the base point (2, 1): millimetres from it.
        Assert.Contains(Lines(Views.Dxf(f.Session.Reader, view, "project")), l => l == (8000, 4000, 18000, 4000));
        // The map: metres through the survey point, the drawing lands on the cadastre's coordinates.
        var map = Views.Dxf(f.Session.Reader, view, "map");
        Assert.Equal("6", Units(map));
        Assert.Contains(Lines(map), l => l == (500000, 4500000, 500010, 4500000));
        // True north 90° to the plan's left: the plan's X axis runs south on the map.
        Locate(f, 90);
        Assert.Contains(Lines(Views.Dxf(f.Session.Reader, view, "map")), l => Math.Abs(l.X1 - 500000) < 1e-3 && Math.Abs(l.Y1 - 4500000) < 1e-3 && Math.Abs(l.X2 - 500000) < 1e-3 && Math.Abs(l.Y2 - 4499990) < 1e-3);
        Assert.Throws<CommandException>(() => Views.Dxf(f.Session.Reader, view, "lambert"));
    }

    [Fact]
    public void A_spot_coordinate_reads_the_map_and_follows_the_survey_point()
    {
        var f = F();
        var plan = Plan(f);
        // The coordinates' lines the plan draws (each spot coordinate's two), the earlier one removed when a second is checked.
        List<string> Texts(string id) => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray().OfType<JsonObject>()
            .Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray().Select(t => (string)t!["text"]!))
            .Where(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^[XYEN] -?\d")).ToList();
        // Not tied to the map: the base point's X and Y; a map one refused.
        var local = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "coordinate", ["a"] = J(20, 5), ["at"] = J(22, 7) })!["id"]!;
        Assert.Equal("project", f.Model.Get(local)!.Str("system"));
        Assert.Equal(["X 20.000", "Y 5.000"], Texts(local));
        Assert.Throws<CommandException>(() => f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "coordinate", ["a"] = J(20, 5), ["at"] = J(22, 7), ["system"] = "map" }));
        Locate(f);
        Assert.Equal(["X 18.000", "Y 4.000"], Texts(local));
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(local) });
        var spot = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "coordinate", ["a"] = J(20, 5), ["at"] = J(22, 7) })!["id"]!;
        Assert.Equal("map", f.Model.Get(spot)!.Str("system"));
        Assert.Equal(["E 500010.000", "N 4500000.000"], Texts(spot));
        // The survey point moved on the map: the coordinate is written again, nothing stored to go stale.
        f.Run("project.location", new JsonObject { ["survey"] = new JsonObject { ["x"] = 10, ["y"] = 5, ["easting"] = 600000, ["northing"] = 400000 } });
        Assert.Equal(["E 600010.000", "N 400000.000"], Texts(spot));
        // Only on a plan.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(0, 0), ["end"] = J(10, 0) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("drafting.addDimension", new JsonObject { ["viewId"] = section, ["dimKind"] = "coordinate", ["a"] = J(1, 1), ["at"] = J(2, 2) }));
    }
}
