using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 714: a material's cut hatch turns with the element it fills (a wall's along its axis in a plan, as Revit's model
/// patterns) unless the material fixes it to the view; a project saved before keeps its hatches fixed to the sheet; the batt
/// runs along each wall's own axis, round a curved one too.
/// </summary>
public class WallHatchDirectionTests
{
    /// <summary>A layered wall's type: mineral wool 100 (outside), brick 250 (core, MASONRY: 45° lines), plaster 15.</summary>
    static string Type(Fixture f) => f.Type("Insulated brick", ("Mineral wool", 0.10, false), ("Solid brick", 0.25, true), ("Plaster", 0.015, false));

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;

    static Views.Rendered Render(Fixture f, string plan) => Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!);

    static double Dir(Vec2 a, Vec2 b) { var d = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI; return ((d % 180) + 180) % 180; }

    /// <summary>
    /// The directions (0–180°) of the brick's hatch lines inside the box: the hatch strokes longer than 6 cm (not the batt's,
    /// whose pieces are at most half its 100 mm layer long).
    /// </summary>
    static List<double> BrickLines(Views.Rendered r, double x0, double y0, double x1, double y1) =>
        r.Pens.Where(p => p.Layer == PenLayer.Hatch).SelectMany(p => p.Segments)
            .Where(s => (s.B - s.A).Length > 0.06 && Inside(s.A, x0, y0, x1, y1) && Inside(s.B, x0, y0, x1, y1))
            .Select(s => Dir(s.A, s.B)).ToList();

    static bool Inside(Vec2 p, double x0, double y0, double x1, double y1) => p.X >= x0 && p.X <= x1 && p.Y >= y0 && p.Y <= y1;

    static void AllAt(List<double> dirs, double expected, string what)
    {
        Assert.NotEmpty(dirs);
        Assert.All(dirs, d => Assert.True(Math.Abs(((d - expected) % 180 + 270) % 180 - 90) < 0.01, $"{what}: a hatch line at {d:0.###}°, not {expected}°"));
    }

    [Fact]
    public void A_new_projects_wall_hatch_turns_with_the_wall_at_any_angle()
    {
        var f = new Fixture();
        var type = Type(f);
        Assert.Equal(HatchAlign.Element, MaterialHatchUpgrade.AlignDefault(f.Model));
        // Four free walls: along x, at 30°, along y, at 147°; each 4 m, far enough apart.
        foreach (var (deg, x) in new[] { (0.0, 0.0), (30, 10), (90, 20), (147, 30) })
        {
            var a = deg * Math.PI / 180;
            f.Wall(x, 0, x + 4 * Math.Cos(a), 4 * Math.Sin(a), type);
        }
        var r = Render(f, Plan(f));
        // MASONRY's 45° lines turned by the wall's angle: 45°, 75°, 135°, 192° = 12°.
        foreach (var (deg, x) in new[] { (0.0, 0.0), (30, 10), (90, 20), (147, 30) })
            AllAt(BrickLines(r, x - 4, -1, x + 4.5, 4.5), deg + 45, $"a wall at {deg}°");
        // The material's own angle on top: 15° more.
        var brick = f.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Solid brick");
        f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["section"] = new JsonObject { ["hatchAngle"] = 15 } });
        AllAt(BrickLines(Render(f, Plan(f)), 6, -1, 14.5, 4.5), 30 + 45 + 15, "a wall at 30°, the material at 15°");
    }

    [Fact]
    public void A_material_fixed_to_the_view_keeps_its_hatch_square_to_the_sheet()
    {
        var f = new Fixture();
        var type = Type(f);
        f.Wall(0, 0, 4 * Math.Cos(Math.PI / 6), 4 * Math.Sin(Math.PI / 6), type);
        var brick = f.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Solid brick");
        f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["section"] = new JsonObject { ["hatchAlign"] = "view" } });
        AllAt(BrickLines(Render(f, Plan(f)), -2, -2, 6, 4), 45, "fixed to the view");
        // A wall's own choice still wins over its material's (decision 369).
        var wall = f.Model.OfClass(IfcClass.Wall).Single().Id;
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["hatchAlign"] = "axis" } });
        AllAt(BrickLines(Render(f, Plan(f)), -2, -2, 6, 4), 75, "the wall along its axis");
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["section"] = new JsonObject { ["hatchAlign"] = "wall" } }));
    }

    [Fact]
    public void A_project_saved_before_keeps_its_hatches_fixed_to_the_sheet_and_a_new_one_aligns_them()
    {
        var f = new Fixture();
        var type = Type(f);
        f.Wall(0, 0, 4 * Math.Cos(Math.PI / 6), 4 * Math.Sin(Math.PI / 6), type);
        var plan = Plan(f);
        AllAt(BrickLines(Render(f, plan), -2, -2, 6, 4), 75, "a new project");
        Assert.Equal(MaterialHatchUpgrade.Rev, (int)Spatial.Project(f.Model)!.Attrs[MaterialHatchUpgrade.RevKey]!);
        // The same project as a build of hatchRev 3 saved it, opened again: marked, its materials untouched, its hatch as it drew it.
        var saved = f.Model.Entities.Select(e =>
        {
            if (e.Class != IfcClass.Project) return e;
            var a = e.CloneAttrs();
            a[MaterialHatchUpgrade.RevKey] = 3;
            return e.With(attrs: a);
        }).ToList();
        var materials = saved.Where(e => e.Class == IfcClass.Material).ToList();
        f.Session.Load(saved, f.Model.Relations.ToList(), null);
        Assert.Equal(HatchAlign.View, MaterialHatchUpgrade.AlignDefault(f.Model));
        Assert.Equal(MaterialHatchUpgrade.Rev, (int)Spatial.Project(f.Model)!.Attrs[MaterialHatchUpgrade.RevKey]!);
        Assert.All(materials, m => Assert.True(JsonNode.DeepEquals(m.Attrs, f.Model.Get(m.Id)!.Attrs)));
        AllAt(BrickLines(Render(f, plan), -2, -2, 6, 4), 45, "a project saved before");
        // Its material list shows the project's default; a material set to the element aligns as a new project's.
        var listed = (JsonArray)f.Session.Query("material.list", new JsonObject())!;
        Assert.Equal("view", (string)listed.Single(m => (string?)m!["name"] == "Solid brick")!["section"]!["hatchAlign"]!);
        var brick = f.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Solid brick");
        f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["section"] = new JsonObject { ["hatchAlign"] = "element" } });
        AllAt(BrickLines(Render(f, plan), -2, -2, 6, 4), 75, "a project saved before, its brick set to the element");
    }

    [Fact]
    public void A_curved_walls_hatch_runs_along_its_chord_and_its_batt_round_the_arc_between_the_layers_faces()
    {
        var f = new Fixture();
        var type = Type(f);
        // An arc from (0, 0) to (6, 0) sagging 1.2 m (to the right of start → end).
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["arc"] = -1.2, ["typeId"] = type });
        var wall = f.Model.OfClass(IfcClass.Wall).Single();
        // The wool's batt in a colour of its own, a pen of its own.
        var woolMaterial = f.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Mineral wool");
        f.Run("material.update", new JsonObject { ["id"] = woolMaterial.Id, ["section"] = new JsonObject { ["hatchColor"] = "#ff0000" } });
        var r = Render(f, Plan(f));
        AllAt(BrickLines(r, -1, -3, 7, 1), 45, "a curved wall's brick");
        // The batt: every point between the wool's faces, round the arc, and both faces touched along it.
        var curve = PathCurve.Of(wall);
        var bands = LayerSets.BandsOf(f.Model, wall.Id)!;
        var wool = bands.Single(b => f.Model.Get(b.Material)?.Name == "Mineral wool");
        var (y0, y1) = (Math.Min(wool.From, wool.To), Math.Max(wool.From, wool.To));
        var batt = BattPoints(r, curve);
        Assert.True(batt.Count > 200, $"{batt.Count} batt points");
        Assert.All(batt, p => Assert.InRange(p.Y, y0 - Chords.Tolerance - 1e-6, y1 + Chords.Tolerance + 1e-6));
        Assert.Contains(batt, p => p.Y <= y0 + 0.003);
        Assert.Contains(batt, p => p.Y >= y1 - 0.003);
        Assert.True(batt.Max(p => p.S) - batt.Min(p => p.S) > 0.9 * curve.Length, "the batt runs the whole arc");
    }

    /// <summary>The points of the batt (its red hatch pen's, in the curve's frame: along, across).</summary>
    static List<(double S, double Y)> BattPoints(Views.Rendered r, PathCurve curve)
    {
        var frame = curve.BandAxis();
        var pen = r.Pens.Single(p => p.Layer == PenLayer.Hatch && p.Stroke == "#ff0000");
        return pen.Segments.SelectMany(s => new[] { s.A, s.B }).Select(frame.ToBand).ToList();
    }

    [Fact]
    public void The_dxf_of_a_plan_carries_the_turned_hatch_and_ifc_names_the_batt()
    {
        var f = new Fixture();
        var type = Type(f);
        f.Wall(0, 0, 4 * Math.Cos(Math.PI / 6), 4 * Math.Sin(Math.PI / 6), type);
        var plan = Plan(f);
        var dxf = Encoding.UTF8.GetString(Views.Dxf(f.Session.Reader, Views.Find(f.Model, plan)!)).Replace("\r\n", "\n");
        // The DXF's lines on the hatch layer longer than 2 cm on paper scale (the brick's, not the batt's short pieces).
        var lines = DxfLines(dxf).Where(l => l.Layer.Contains("HATCH", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(lines);
        var longest = lines.Max(l => (l.B - l.A).Length);
        var brick = lines.Where(l => (l.B - l.A).Length > longest / 4).Select(l => Dir(l.A, l.B)).ToList();
        AllAt(brick, 75, "the DXF's brick hatch");
        // IFC: the wool's batt written as a hatching (the zig-zag), its curve style named BATT.
        var wool = f.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Mineral wool");
        f.Run("material.update", new JsonObject { ["id"] = wool.Id, ["section"] = new JsonObject { ["hatch"] = "BATT" } });
        using var ms = new MemoryStream();
        Lapin.Ifc.IfcExport.Write(f.Session, ms, "batt.ifc", new Lapin.Ifc.IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains(ifc.Split('\n'), l => l.Contains("IFCCURVESTYLE('BATT'"));
    }

    /// <summary>The LINE entities of a DXF: layer and ends.</summary>
    static List<(string Layer, Vec2 A, Vec2 B)> DxfLines(string dxf)
    {
        var t = dxf.Split('\n');
        var o = new List<(string, Vec2, Vec2)>();
        for (var i = 0; i + 1 < t.Length; i += 2)
        {
            if (t[i].Trim() != "0" || t[i + 1].Trim() != "LINE") continue;
            string layer = ""; double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            for (var j = i + 2; j + 1 < t.Length && t[j].Trim() != "0"; j += 2)
            {
                var v = t[j + 1].Trim();
                double N() => double.Parse(v, CultureInfo.InvariantCulture);
                switch (t[j].Trim())
                {
                    case "8": layer = v; break;
                    case "10": x1 = N(); break;
                    case "20": y1 = N(); break;
                    case "11": x2 = N(); break;
                    case "21": y2 = N(); break;
                }
            }
            o.Add((layer, new Vec2(x1, y1), new Vec2(x2, y2)));
        }
        return o;
    }
}
