using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Leaning walls (docs/plan-wall-lean-taper.md, stage L1; in-app report #191): a straight wall's <c>lean</c> (degrees from
/// vertical, + towards its left face; its type's when it has none) tilts it in its own plane: every horizontal section is its
/// base footprint moved across by z·tanθ, each layer keeps its normal thickness, the height stays vertical and the base stays
/// put (<c>leanFrom</c> base). Joins are worked out at the base and at the top and the layers lofted between, so two walls meet
/// on the line where their faces' planes cross. Openings follow the wall (<c>openingCut</c> wall) or stand plumb (vertical).
/// A wall without a lean is built exactly as before. What stage 1 does not lean yet is refused with a problem, never silently.
/// </summary>
public sealed class WallLeanTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-wall-lean").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    const double Length = 5, Height = 3, T = 0.3, Deg = 10;
    static readonly double Tan = Math.Tan(Deg * Math.PI / 180), Cos = Math.Cos(Deg * Math.PI / 180);

    static string Solid300(Fixture f) => f.Type("Concrete 300", ("Concrete", T, true));

    static void Lean(Fixture f, double deg, params string[] ids) =>
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["attrs"] = new JsonObject { [WallLean.Key] = deg } });

    static List<GeometryPart> Parts(Fixture f, string id) => f.Session.Geometry(id)!.Parts.ToList();
    static double Volume(Fixture f, string id) => Parts(f, id).Sum(p => Measure.Volume(p.Solid));
    static List<Vec3> Vertices(Fixture f, string id) => Parts(f, id).SelectMany(p => p.Solid.Vertices).ToList();
    static double Q(Fixture f, string id, string set, string name, string? part = null) =>
        f.Session.Quantities(id).First(q => q.Name == set && (part == null || q.Part == part)).Quantities.Single(x => x.Name == name).Value;

    static ISolidBooleans Booleans(Fixture f) => f.Session.Catalog.Get<ISolidBooleans>()!;

    /// <summary>Every vertex of every part, in order, as text: two bodies built the same way print the same.</summary>
    static string Print(Fixture f, string id) =>
        string.Join(";", Parts(f, id).Select(p => p.Key + ":" + string.Join(",", p.Solid.Vertices.Select(v => $"{v.X:R} {v.Y:R} {v.Z:R}")) + "|" + p.Solid.Faces.Length));

    [Fact]
    public void A_vertical_wall_is_built_exactly_as_before_vertex_for_vertex()
    {
        var f = new Fixture();
        var type = f.Type("Plastered brick", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 2.5 });
        var (pa, pb) = (Print(f, a), Print(f, b));
        var plan = string.Join(";", f.Session.Geometry(a)!.Plan!.Select(p => p.Kind + string.Join(",", p.Points.Select(q => $"{q.X:R} {q.Y:R}"))));
        // A lean of 0 is no lean; and a leaning wall elsewhere, joined to neither, changes nothing of theirs.
        Lean(f, 0, a, b);
        var c = f.Wall(20, 0, 25, 0, type, height: Height);
        Lean(f, Deg, c);
        Assert.Equal(pa, Print(f, a));
        Assert.Equal(pb, Print(f, b));
        Assert.Equal(plan, string.Join(";", f.Session.Geometry(a)!.Plan!.Select(p => p.Kind + string.Join(",", p.Points.Select(q => $"{q.X:R} {q.Y:R}")))));
        // The plumb wall's faces straight over its base.
        var v = Vertices(f, a);
        Assert.Equal(v.Where(p => p.Z < 1e-9).Min(p => p.Y), v.Where(p => p.Z > Height - 1e-9).Min(p => p.Y), 9);
        Assert.Equal(v.Where(p => p.Z < 1e-9).Max(p => p.Y), v.Where(p => p.Z > Height - 1e-9).Max(p => p.Y), 9);
    }

    [Fact]
    public void A_wall_leaning_ten_degrees_keeps_its_thickness_its_height_and_its_base()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, Solid300(f), height: Height);
        Lean(f, Deg, w);
        var v = Vertices(f, w);
        // The base stays where it was, the top is moved towards the left face by H·tanθ, the height stays vertical.
        var (bottom, top) = (v.Where(p => Math.Abs(p.Z) < 1e-9).ToList(), v.Where(p => Math.Abs(p.Z - Height) < 1e-9).ToList());
        Assert.Equal(-T / 2 / Cos, bottom.Min(p => p.Y), 9);
        Assert.Equal(T / 2 / Cos, bottom.Max(p => p.Y), 9);
        Assert.Equal(-T / 2 / Cos + Height * Tan, top.Min(p => p.Y), 9);
        Assert.Equal(T / 2 / Cos + Height * Tan, top.Max(p => p.Y), 9);
        Assert.Equal(Height, v.Max(p => p.Z), 9);
        Assert.Equal(0, v.Min(p => p.X), 9);
        Assert.Equal(Length, v.Max(p => p.X), 9);
        // Its volume and face as an oblique prism has them; the quantities say so, the thickness the normal one.
        Assert.Equal(Length * Height * T / Cos, Volume(f, w), 9);
        Assert.Equal(T, Q(f, w, "Qto_WallBaseQuantities", "Width"), 9);
        Assert.Equal(Height, Q(f, w, "Qto_WallBaseQuantities", "Height"), 9);
        Assert.Equal(Length * Height / Cos, Q(f, w, "Qto_WallBaseQuantities", "GrossSideArea"), 9);
        Assert.Equal(Length * Height / Cos, Q(f, w, "Qto_WallBaseQuantities", "NetSideArea"), 9);
        Assert.Equal(Length * Height * T / Cos, Q(f, w, "Qto_WallBaseQuantities", "NetVolume"), 9);
        // No problem: a straight wall leans.
        Assert.Null(f.Session.Geometry(w)!.Diagnostics);
    }

    [Fact]
    public void An_L_corner_of_two_leaning_walls_meets_on_their_faces_planes_with_no_gap_or_overlap()
    {
        var f = new Fixture();
        var type = Solid300(f);
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        Assert.NotEmpty(Joins.Of(f.Model, a));
        Lean(f, Deg, a, b);
        // Both lean into the corner (each towards its left). At each height the section is the L of the two strips, w = T/cosθ wide,
        // their outer faces at -w/2 + z·tanθ: its area w·(9 − 2z·tanθ), so the volume is w·(9H − tanθ·H²).
        var w = T / Cos;
        var expected = w * (9 * Height - Tan * Height * Height);
        var union = Booleans(f).Union([.. Parts(f, a).Concat(Parts(f, b)).Select(p => p.Solid)], "union")!;
        Assert.Equal(expected, Measure.Volume(union), 6);
        Assert.Equal(expected, Volume(f, a) + Volume(f, b), 6);
        // The corner's trace is where the faces' planes cross: the outer corner at the top is moved by H·tanθ along both.
        var top = Vertices(f, a).Concat(Vertices(f, b)).Where(p => Math.Abs(p.Z - Height) < 1e-9).ToList();
        var corner = new Vec2(Length - (-w / 2 + Height * Tan), -w / 2 + Height * Tan);
        Assert.Contains(top, p => (p.Xy - corner).Length < 1e-9);
    }

    [Fact]
    public void An_L_corner_of_a_leaning_wall_and_a_plumb_one_meets_with_no_gap_or_overlap()
    {
        var f = new Fixture();
        var type = Solid300(f);
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        Lean(f, Deg, a);
        // At z: A's strip w wide from x 0 to B's outer face (5 + T/2), B's T wide from A's outer face (-w/2 + z·tanθ) to y 4.
        var w = T / Cos;
        var expected = Height * ((Length + T / 2) * w + T * (4 + w / 2) - T * w) - T * Tan * Height * Height / 2;
        var union = Booleans(f).Union([.. Parts(f, a).Concat(Parts(f, b)).Select(p => p.Solid)], "union")!;
        Assert.Equal(expected, Measure.Volume(union), 6);
        Assert.Equal(expected, Volume(f, a) + Volume(f, b), 6);
        // The plumb wall's free end is still plumb; its corner end follows the leaning face.
        var bv = Vertices(f, b);
        Assert.Equal(4, bv.Max(p => p.Y), 9);
        Assert.Equal(-w / 2, bv.Where(p => p.Z < 1e-9).Min(p => p.Y), 9);
        Assert.Equal(-w / 2 + Height * Tan, bv.Where(p => p.Z > Height - 1e-9).Min(p => p.Y), 9);
    }

    [Fact]
    public void A_wall_ending_on_a_leaning_walls_face_follows_it_up()
    {
        var f = new Fixture();
        var type = Solid300(f);
        var a = f.Wall(0, 0, 6, 0, type, height: Height);
        var b = f.Wall(3, 0, 3, 4, type, height: Height);
        Lean(f, Deg, a);
        var w = T / Cos;
        var bv = Vertices(f, b);
        // Its end on the leaning wall's left face, at the base and at the top.
        Assert.Equal(w / 2, bv.Where(p => p.Z < 1e-9).Min(p => p.Y), 9);
        Assert.Equal(w / 2 + Height * Tan, bv.Where(p => p.Z > Height - 1e-9).Min(p => p.Y), 9);
        var union = Booleans(f).Union([.. Parts(f, a).Concat(Parts(f, b)).Select(p => p.Solid)], "union")!;
        Assert.Equal(Volume(f, a) + Volume(f, b), Measure.Volume(union), 6);
        Assert.Equal(6 * Height * w, Volume(f, a), 9);
        Assert.Equal(T * (4 * Height - (w / 2 * Height + Tan * Height * Height / 2)), Volume(f, b), 6);
    }

    [Fact]
    public void A_layered_wall_keeps_each_layers_normal_thickness()
    {
        var f = new Fixture();
        var type = f.Type("Plastered brick", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.02, false));
        var w = f.Wall(0, 0, Length, 0, type, height: Height);
        Lean(f, Deg, w);
        foreach (var (key, t) in new[] { ("layer:0", 0.015), ("layer:1", 0.25), ("layer:2", 0.02) })
        {
            var part = Parts(f, w).Single(p => p.Key == key);
            var bottom = part.Solid.Vertices.Where(p => p.Z < 1e-9).ToList();
            // Across a face's normal (0, cosθ, -sinθ): the layer's own thickness.
            Assert.Equal(t, (bottom.Max(p => p.Y) - bottom.Min(p => p.Y)) * Cos, 9);
            Assert.Equal(Length * Height * t / Cos, Measure.Volume(part.Solid), 9);
            Assert.Equal(t, Q(f, w, "Lapin_WallLayerQuantities", "Width", key), 9);
            Assert.Equal(Length * Height / Cos, Q(f, w, "Lapin_WallLayerQuantities", "NetSideArea", key), 6);
        }
        Assert.Equal(0.285, Q(f, w, "Qto_WallBaseQuantities", "Width"), 9);
    }

    [Fact]
    public void The_type_gives_its_walls_their_lean_and_a_walls_own_wins()
    {
        var f = new Fixture();
        var type = Solid300(f);
        var w = f.Wall(0, 0, Length, 0, type, height: Height);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, [WallLean.Key] = Deg });
        Assert.Equal(Length * Height * T / Cos, Volume(f, w), 9);
        Lean(f, -Deg, w);
        Assert.Equal(-T / 2 / Cos - Height * Tan, Vertices(f, w).Where(p => p.Z > Height - 1e-9).Min(p => p.Y), 9);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, [WallLean.Key] = null });
        Assert.Null(f.Model.Get(type)!.Attrs[WallLean.Key]);
        // Its own stays; given back to the type, it stands plumb.
        Assert.Equal(-T / 2 / Cos - Height * Tan, Vertices(f, w).Where(p => p.Z > Height - 1e-9).Min(p => p.Y), 9);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(w), ["attrs"] = new JsonObject { [WallLean.Key] = null } });
        Assert.Equal(Length * Height * T, Volume(f, w), 9);
        Assert.Throws<CommandException>(() => Lean(f, 80, w));
    }

    [Fact]
    public void Leaning_from_the_top_keeps_the_top_where_it_was()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, Solid300(f), height: Height);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(w), ["attrs"] = new JsonObject { [WallLean.Key] = Deg, [WallLean.FromKey] = "top" } });
        var v = Vertices(f, w);
        Assert.Equal(-T / 2 / Cos, v.Where(p => p.Z > Height - 1e-9).Min(p => p.Y), 9);
        Assert.Equal(-T / 2 / Cos - Height * Tan, v.Where(p => p.Z < 1e-9).Min(p => p.Y), 9);
    }

    [Fact]
    public void A_door_follows_the_wall_or_stands_plumb_and_its_hole_is_the_same()
    {
        double Removed(string? cut, out (Vec3 Min, Vec3 Max) door, out double sill, double lean = Deg)
        {
            var f = new Fixture();
            var w = f.Wall(0, 0, Length, 0, Solid300(f), height: Height);
            var whole = Volume(f, w);
            var d = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 2.5 })!["id"]!;
            if (lean != 0) Lean(f, lean, w);
            whole = lean != 0 ? whole / Cos : whole;
            if (cut != null) f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(w), ["attrs"] = new JsonObject { [WallLean.CutKey] = cut } });
            door = f.Bounds(d);
            // The hole's bottom: the lowest face the wall's body has between the door's jambs, over the base.
            var hole = Vertices(f, w).Where(p => p.X > 2.5 - 0.3 && p.X < 2.5 + 0.3 && p.Z > 1e-6).ToList();
            sill = hole.Count > 0 ? hole.Min(p => p.Z) : double.NaN;
            return whole - Volume(f, w);
        }
        var plumbWall = Removed(null, out var plumbDoor, out _, lean: 0);
        var along = Removed(null, out var leaningDoor, out var sillAlong);
        var vertical = Removed("vertical", out var plumbInLeaning, out var sillVertical);
        // The hole: the plumb wall's, across the wall's horizontal thickness (T / cosθ), whichever way the door stands; its head and sill level.
        Assert.Equal(plumbWall / Cos, along, 6);
        Assert.Equal(along, vertical, 6);
        Assert.Equal(sillAlong, sillVertical, 9);
        // Following the wall, the door leans with it (its top moved by its height·tanθ); plumb, it is the plumb wall's door moved across.
        var h = plumbDoor.Max.Z - plumbDoor.Min.Z;
        Assert.Equal(plumbDoor.Max.Y - plumbDoor.Min.Y, plumbInLeaning.Max.Y - plumbInLeaning.Min.Y, 6);
        Assert.True(leaningDoor.Max.Y - leaningDoor.Min.Y > plumbDoor.Max.Y - plumbDoor.Min.Y + h * Tan * 0.75);
        Assert.Equal(plumbDoor.Min.Z, leaningDoor.Min.Z, 9);
        Assert.Equal(plumbDoor.Max.Z, leaningDoor.Max.Z, 9);
        Assert.Throws<CommandException>(() =>
        {
            var f = new Fixture();
            var w = f.Wall(0, 0, Length, 0, Solid300(f), height: Height);
            f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(w), ["attrs"] = new JsonObject { [WallLean.CutKey] = "sideways" } });
        });
    }

    [Fact]
    public void The_plan_cuts_the_wall_where_the_cut_meets_it_and_shows_its_base_and_its_top()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, Solid300(f), height: Height);
        Lean(f, Deg, w);
        var plan = f.Session.Geometry(w)!.Plan!;
        var cut = plan.Where(p => p.Kind == "cut").SelectMany(p => p.Points).ToList();
        var at = Openings.PlanCut * Tan;
        Assert.Equal(-T / 2 / Cos + at, cut.Min(p => p.Y), 9);
        Assert.Equal(T / 2 / Cos + at, cut.Max(p => p.Y), 9);
        // Leaning to the left: its base line seen below on the right face, its top edge overhead on the left one.
        Assert.Contains(plan, p => p.Kind == "outline" && p.Points.All(q => Math.Abs(q.Y + T / 2 / Cos) < 1e-9));
        Assert.Contains(plan, p => p.Kind == "overhead" && p.Points.All(q => Math.Abs(q.Y - (T / 2 / Cos + Height * Tan)) < 1e-9));
    }

    [Fact]
    public void Each_lean_the_first_stage_cannot_build_says_its_problem_and_the_wall_stands_plumb()
    {
        var f = new Fixture();
        var type = Solid300(f);
        string Problem(string id) => string.Join(" ", f.Session.Geometry(id)!.Diagnostics ?? []);
        void Set(string id, string key, JsonNode value) => f.Session.Edit("set", e => { var x = e.Model.Get(id)!; var a = x.CloneAttrs(); a[key] = value; e.Replace(x.With(attrs: a)); });
        var curved = f.Wall(0, 0, Length, 0, type, height: Height);
        Set(curved, PathCurve.Key, 0.8);
        Lean(f, Deg, curved);
        Assert.Contains("curved", Problem(curved));
        var profiled = f.Wall(0, 10, Length, 10, type, height: Height);
        Set(profiled, WallTops.TopProfileKey, WallTops.ProfileJson([(0, 2.0), (Length, 3.0)]));
        Lean(f, Deg, profiled);
        Assert.Contains("profile", Problem(profiled));
        var wrapped = f.Wall(0, 20, Length, 20, f.Type("Plastered", ("Plaster", 0.015, false), ("Brick", 0.25, true)), height: Height);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wrapped, ["wrapEnds"] = "first" });
        Lean(f, Deg, wrapped);
        Assert.Contains("wrap", Problem(wrapped));
        // Each stands plumb, as it was.
        Assert.Equal(Length * Height * 0.265, Volume(f, wrapped), 6);
        Assert.Equal(Vertices(f, profiled).Where(p => p.Z < 1e-9).Min(p => p.Y), Vertices(f, profiled).Min(p => p.Y), 9);
        // A wall with no lean says nothing of it.
        var plain = f.Wall(0, 30, Length, 30, type, height: Height);
        Set(plain, PathCurve.Key, 0.8);
        Assert.DoesNotContain("lean", Problem(plain));
    }

    [Fact]
    public void Leaning_several_walls_at_once_is_one_undo_step()
    {
        var f = new Fixture();
        var type = Solid300(f);
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        var before = Volume(f, a) + Volume(f, b);
        Lean(f, Deg, a, b);
        Assert.Equal(Deg, f.Model.Get(a)!.Num(WallLean.Key));
        Assert.Equal(Deg, f.Model.Get(b)!.Num(WallLean.Key));
        f.Session.Undo();
        Assert.Null(f.Model.Get(a)!.Attrs[WallLean.Key]);
        Assert.Null(f.Model.Get(b)!.Attrs[WallLean.Key]);
        Assert.Equal(before, Volume(f, a) + Volume(f, b), 9);
    }

    [Fact]
    public void IFC_writes_an_oblique_extrusion_and_reads_it_back_as_the_same_leaning_wall()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, Solid300(f), height: Height);
        Lean(f, Deg, w);
        var volume = Volume(f, w);
        var path = Path.Combine(_dir, "lean.ifc");
        IfcExport.Write(f.Session, path, null);
        var text = File.ReadAllText(path);
        Assert.Contains("'lean'", text);
        // The body an oblique sweep: its direction across the wall (local y), tilted by θ = (0, sin θ, cos θ); its layers' usage kept.
        Assert.Contains("IFCEXTRUDEDAREASOLID", text);
        Assert.Contains(text.Split('\n'), l => l.Contains("IFCDIRECTION((0.,0.17364") && l.Contains(",0.98480"));
        Assert.Contains("IFCMATERIALLAYERSETUSAGE", text);
        // Back natively (Lapin's intent): the same wall, its GlobalId, its lean, its body.
        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        var back = g.Model.Get(w)!;
        Assert.Equal(Deg, WallLean.Angle(g.Model, back), 9);
        Assert.Equal(volume, Volume(g, w), 6);
    }

    [Fact]
    public void Another_tools_oblique_wall_is_recognised_as_a_leaning_wall_through_the_gate()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, Solid300(f), height: Height);
        Lean(f, Deg, w);
        var path = Path.Combine(_dir, "lean.ifc");
        IfcExport.Write(f.Session, path, null);
        var foreign = Path.Combine(_dir, "foreign.ifc");
        File.WriteAllText(foreign, File.ReadAllText(path).Replace("Lapin_Intent", "Other_Set"));
        var g = new Fixture();
        var report = IfcImport.Run(g.Session, foreign, null, out _);
        var back = g.Model.Get(w)!;
        Assert.False(StoredBodies.Has(back));
        Assert.Equal(Deg, WallLean.Angle(g.Model, back), 6);
        Assert.Contains(report.Fidelity!.Checks, x => x.Id == w && x.Measured && x.Pass);
        Assert.Equal(Volume(f, w), Volume(g, w), 6);
        Assert.Equal(T, Q(g, w, "Qto_WallBaseQuantities", "Width"), 6);
        // A prism swept along the wall (a parallelogram in elevation) is no lean: it stays the file's.
        var h = new Fixture();
        var skew = Path.Combine(_dir, "skew.ifc");
        File.WriteAllText(skew, File.ReadAllText(foreign).Replace("IFCDIRECTION((0.,0.173648", "IFCDIRECTION((0.173648,0."));
        Assert.NotEqual(File.ReadAllText(foreign), File.ReadAllText(skew));
        IfcImport.Run(h.Session, skew, null, out _);
        Assert.True(h.Model.Get(w) is not { } s || StoredBodies.Has(s) || WallLean.Angle(h.Model, s) == 0);
    }
}
