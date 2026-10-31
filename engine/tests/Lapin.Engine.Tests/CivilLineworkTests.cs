using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// CAD linework with heights: a DXF's 3D lines keep their z, show in 3D as lines (hidden until the view shows its
/// linework), and the civil module makes a track, a road or a breakline of a chain of them, with the profile from
/// their heights. The track's parts have materials by name, so sections hatch them.
/// </summary>
public class CivilLineworkTests
{
    static string F(double v) => v.ToString(CultureInfo.InvariantCulture);
    static string Dxf(string entities) => string.Join("\n", "0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015", "9", "$INSUNITS", "70", "6", "0", "ENDSEC", "0", "SECTION", "2", "ENTITIES", entities, "0", "ENDSEC", "0", "EOF");
    static string Line3d(double x1, double y1, double z1, double x2, double y2, double z2, string layer = "AXIS") =>
        string.Join("\n", "0", "LINE", "8", layer, "10", F(x1), "20", F(y1), "30", F(z1), "11", F(x2), "21", F(y2), "31", F(z2));

    /// <summary>Three lines end to end with heights 10 → 13 m, metres, and one apart on another layer.</summary>
    static readonly string Axis = Dxf(string.Join("\n", Line3d(0, 0, 10, 50, 0, 11), Line3d(100, 20, 12, 150, 20, 13), Line3d(50, 0, 11, 100, 20, 12), Line3d(0, 30, 0, 10, 30, 0, "OTHER")));

    static (Fixture F, string Plan, List<Entity> Lines) Imported()
    {
        var f = new Fixture(new DraftingModule(), new CivilModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["name"] = "axis.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Axis)) });
        var lines = f.Model.OfClass(IfcClass.Annotation).Where(e => e.Attrs["shape"] is JsonObject).OrderBy(e => e.Name).ToList();
        return (f, plan, lines);
    }

    static List<(double X, double Y)> PathOf(Entity e) => ((JsonArray)e.Attrs["path"]!).Select(p => (Json.Number(p!.AsArray()[0]) ?? 0, Json.Number(p.AsArray()[1]) ?? 0)).ToList();

    [Fact]
    public void A_DXF_lines_heights_are_kept_and_drawn_in_3D_as_linework()
    {
        var (f, _, lines) = Imported();
        Assert.Equal(4, lines.Count);
        var withZ = lines.Where(e => e.Str("layer") == "AXIS").ToList();
        Assert.Equal(3, withZ.Count);
        Assert.All(withZ, e => Assert.Equal(2, ((JsonArray)e.Attrs["heights"]!).Count));
        var first = withZ.Single(e => Math.Abs((Json.Number(((JsonArray)e.Attrs["heights"]!)[0]) ?? -1) - 10) < 1e-9);
        Assert.Equal(11, Json.Number(((JsonArray)first.Attrs["heights"]!)[1])!.Value, 6);
        // The flat line on the other layer has no heights.
        Assert.Null(lines.Single(e => e.Str("layer") == "OTHER").Attrs["heights"]);
        // In 3D: a line item each, six numbers a segment, no body; the category hidden until shown.
        var render = f.Session.Snapshot().Render.Where(r => withZ.Any(e => e.Id == r.NodeId)).ToList();
        Assert.Equal(3, render.Count);
        Assert.All(render, r => { Assert.NotNull(r.Lines); Assert.Equal(6, r.Lines!.Length); Assert.Empty(r.Parts ?? []); });
        Assert.Contains(render, r => Math.Abs(r.Lines![2] - 10) < 1e-6 && Math.Abs(r.Lines[5] - 11) < 1e-6);
        Assert.Equal("linework", LineStyles.CategoryOf(IfcClass.Annotation));
        Assert.Contains("linework", GraphicsSets.HiddenIn3d);
    }
    [Fact]
    public void The_same_chain_becomes_a_road_or_a_breakline_of_the_terrain()
    {
        var (f, _, lines) = Imported();
        var picked = lines.Last(e => e.Str("layer") == "AXIS");
        var road = f.Run("civil.fromLinework", new JsonObject { ["id"] = picked.Id, ["as"] = "road", ["storeyId"] = f.Storey })!;
        var pavement = f.Model.Get((string)road["id"]!)!;
        Assert.Equal(IfcClass.Pavement, pavement.Class);
        Assert.Equal("Road", pavement.Name);
        Assert.Equal("design", CorridorSpec.Of(pavement).Mode);

        f.Run("civil.addFlatTerrain", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -10, ["y1"] = -10, ["x2"] = 200, ["y2"] = 50, ["elevation"] = 5 });
        var b = f.Run("civil.fromLinework", new JsonObject { ["id"] = picked.Id, ["as"] = "breakline", ["storeyId"] = f.Storey })!;
        Assert.Equal(1, (int)b["breaklines"]!);
        var terrain = f.Model.Get((string)b["id"]!)!;
        var line = ((JsonArray)terrain.Attrs["breaklines"]!)[0]!.AsArray();
        Assert.Equal(4, line.Count);
        // The line's own heights, not the ground's.
        Assert.Equal(10, Json.Number(line[0]!.AsArray()[2])!.Value, 3);

        var ex = Assert.Throws<CommandException>(() => f.Run("civil.fromLinework", new JsonObject { ["id"] = picked.Id, ["as"] = "ditch" }));
        Assert.Contains("a road, a path or a breakline", ex.Message);
    }
}
