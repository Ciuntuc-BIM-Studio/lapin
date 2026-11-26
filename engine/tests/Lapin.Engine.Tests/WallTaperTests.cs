using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Tapered walls (docs/plan-wall-lean-taper.md, stage L2; in-app report #191): a straight wall's <c>thicknessTop</c> (its type's
/// when it has none) makes it thinner or thicker upwards. One face stays plumb (<c>plumbFace</c>: the first layer's face by
/// default, the right face as drawn), the other leans; the layer that tapers (<c>taperLayer</c>, the core by default) takes the
/// whole change and every other layer keeps its thickness measured normal to its face. Joins are worked out at the base and the
/// top, as for a leaning wall. A wall without <c>thicknessTop</c> is built exactly as before. What stage 2 cannot taper keeps its
/// thickness with a problem, never silently.
/// </summary>
public sealed class WallTaperTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-wall-taper").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    const double Length = 5, Height = 3;

    static void Taper(Fixture f, double? top, params string[] ids) =>
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["attrs"] = new JsonObject { [WallTaper.Key] = top } });

    static void Set(Fixture f, string id, JsonObject attrs) =>
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = attrs });

    static List<GeometryPart> Parts(Fixture f, string id) => f.Session.Geometry(id)!.Parts.ToList();
    static double Volume(Fixture f, string id) => Parts(f, id).Sum(p => Measure.Volume(p.Solid));
    static List<Vec3> Vertices(Fixture f, string id) => Parts(f, id).SelectMany(p => p.Solid.Vertices).ToList();
    static List<Vec3> At(IEnumerable<Vec3> v, double z) => v.Where(p => Math.Abs(p.Z - z) < 1e-9).ToList();
    static double Q(Fixture f, string id, string set, string name, string? part = null) =>
        f.Session.Quantities(id).First(q => q.Name == set && (part == null || q.Part == part)).Quantities.Single(x => x.Name == name).Value;
    static ISolidBooleans Booleans(Fixture f) => f.Session.Catalog.Get<ISolidBooleans>()!;
    static string Problem(Fixture f, string id) => string.Join(" ", f.Session.Geometry(id)!.Diagnostics ?? []);

    static string Print(Fixture f, string id) =>
        string.Join(";", Parts(f, id).Select(p => p.Key + ":" + string.Join(",", p.Solid.Vertices.Select(v => $"{v.X:R} {v.Y:R} {v.Z:R}")) + "|" + p.Solid.Faces.Length));

    /// <summary>Simpson's rule, exact for the quadratic areas of these walls' sections.</summary>
    static double Integral(Func<double, double> area, double h) => h / 6 * (area(0) + 4 * area(h / 2) + area(h));

    [Fact]
    public void A_wall_without_a_thickness_at_top_is_built_exactly_as_before()
    {
        var f = new Fixture();
        var type = f.Type("Plastered brick", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 2.5 });
        var (pa, pb) = (Print(f, a), Print(f, b));
        // Its own thickness at the top is no taper; a tapered wall elsewhere, joined to neither, changes nothing of theirs.
        Taper(f, 0.28, a);
        var c = f.Wall(20, 0, 25, 0, type, height: Height);
        Taper(f, 0.2, c);
        Assert.Equal(pa, Print(f, a));
        Assert.Equal(pb, Print(f, b));
        Taper(f, null, a);
        Assert.Equal(pa, Print(f, a));
        Assert.Null(f.Session.Geometry(a)!.Diagnostics);
    }

    [Fact]
    public void A_wall_tapers_with_its_first_layers_face_plumb()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, f.Type("Concrete 500", ("Concrete", 0.5, true)), height: Height);
        Taper(f, 0.3, w);
        var v = Vertices(f, w);
        var (bottom, top) = (At(v, 0), At(v, Height));
        // The right face (the first layer's, y = −0.25) plumb; the left one comes in by 0.2 m over the height.
        Assert.Equal(-0.25, bottom.Min(p => p.Y), 9);
        Assert.Equal(-0.25, top.Min(p => p.Y), 9);
        Assert.Equal(0.25, bottom.Max(p => p.Y), 9);
        Assert.Equal(0.05, top.Max(p => p.Y), 9);
        Assert.Equal(Length * Height * 0.4, Volume(f, w), 9);
        // Its width its mean thickness; its height vertical.
        Assert.Equal(0.4, Q(f, w, "Qto_WallBaseQuantities", "Width"), 9);
        Assert.Equal(Height, Q(f, w, "Qto_WallBaseQuantities", "Height"), 9);
        Assert.Equal(Length * Height * 0.4, Q(f, w, "Qto_WallBaseQuantities", "NetVolume"), 9);
        Assert.Equal(Length * Height * 0.4, Q(f, w, "Qto_WallBaseQuantities", "GrossVolume"), 9);
        Assert.Null(f.Session.Geometry(w)!.Diagnostics);
        // The last layer's face plumb instead: the right face comes in.
        Set(f, w, new JsonObject { [WallTaper.FaceKey] = WallTaper.Last });
        v = Vertices(f, w);
        Assert.Equal(0.25, At(v, Height).Max(p => p.Y), 9);
        Assert.Equal(-0.05, At(v, Height).Min(p => p.Y), 9);
        Assert.Equal(-0.25, At(v, 0).Min(p => p.Y), 9);
        // Thicker at the top works the same way.
        Set(f, w, new JsonObject { [WallTaper.FaceKey] = null, [WallTaper.Key] = 0.6 });
        Assert.Equal(0.35, At(Vertices(f, w), Height).Max(p => p.Y), 9);
        Assert.Equal(Length * Height * 0.55, Volume(f, w), 9);
    }

    [Fact]
    public void The_core_takes_the_change_and_the_finishes_keep_their_thickness()
    {
        var f = new Fixture();
        var type = f.Type("Retaining", ("Plaster", 0.015, false), ("Concrete", 0.4, true), ("Plaster", 0.02, false));
        var w = f.Wall(0, 0, Length, 0, type, height: Height);
        Taper(f, 0.3, w);
        var change = 0.3 - 0.435;
        var cos = Height / Math.Sqrt(Height * Height + change * change);
        var parts = Parts(f, w);
        (double Lo, double Hi) Across(string key, double z)
        {
            var p = At(parts.Single(x => x.Key == key).Solid.Vertices, z);
            return (p.Min(q => q.Y), p.Max(q => q.Y));
        }
        // The plumb face's plaster: plumb, its thickness.
        Assert.Equal((-0.215, -0.2), Across("layer:0", 0), new Close());
        Assert.Equal((-0.215, -0.2), Across("layer:0", Height), new Close());
        // The leaning face's plaster: its thickness normal to its face (horizontally 0.02 / cosφ), on the face at the base and the top.
        var w2 = 0.02 / cos;
        Assert.Equal((0.22 - w2, 0.22), Across("layer:2", 0), new Close());
        Assert.Equal((0.22 + change - w2, 0.22 + change), Across("layer:2", Height), new Close());
        Assert.Equal(Length * Height * 0.02 / cos, Measure.Volume(parts.Single(x => x.Key == "layer:2").Solid), 9);
        // The core between them: all the change.
        Assert.Equal((-0.2, 0.22 - w2), Across("layer:1", 0), new Close());
        Assert.Equal((-0.2, 0.22 + change - w2), Across("layer:1", Height), new Close());
        Assert.Equal(Length * Height * (0.435 + 0.3) / 2, Volume(f, w), 9);
        // Quantities: each finish its own thickness and true area, the core its mean thickness.
        Assert.Equal(0.015, Q(f, w, "Lapin_WallLayerQuantities", "Width", "layer:0"), 9);
        Assert.Equal(0.02, Q(f, w, "Lapin_WallLayerQuantities", "Width", "layer:2"), 9);
        Assert.Equal(Length * Height / cos, Q(f, w, "Lapin_WallLayerQuantities", "NetSideArea", "layer:2"), 6);
        Assert.Equal(Length * Height, Q(f, w, "Lapin_WallLayerQuantities", "NetSideArea", "layer:0"), 6);
        Assert.Equal(0.42 - w2 + change / 2, Q(f, w, "Lapin_WallLayerQuantities", "Width", "layer:1"), 9);
        Assert.Equal((0.435 + 0.3) / 2, Q(f, w, "Qto_WallBaseQuantities", "Width"), 9);
    }

    [Fact]
    public void Another_layer_may_take_the_change_and_the_core_then_keeps_its_thickness()
    {
        var f = new Fixture();
        var type = f.Type("Insulated", ("Insulation", 0.15, false), ("Concrete", 0.2, true));
        var w = f.Wall(0, 0, Length, 0, type, height: Height);
        Set(f, w, new JsonObject { [WallTaper.Key] = 0.3, [WallTaper.LayerKey] = 0 });
        var change = -0.05;
        var cos = Height / Math.Sqrt(Height * Height + change * change);
        var parts = Parts(f, w);
        var core = parts.Single(x => x.Key == "layer:1").Solid;
        var ins = parts.Single(x => x.Key == "layer:0").Solid;
        // The concrete keeps 0.2 normal to its leaning faces; the insulation, on the plumb face, goes from 0.15 to 0.10.
        Assert.Equal(Length * Height * 0.2 / cos, Measure.Volume(core), 9);
        Assert.Equal(0.15 - (0.2 / cos - 0.2), At(ins.Vertices, 0).Max(p => p.Y) - At(ins.Vertices, 0).Min(p => p.Y), 9);
        Assert.Equal(0.10 - (0.2 / cos - 0.2), At(ins.Vertices, Height).Max(p => p.Y) - At(ins.Vertices, Height).Min(p => p.Y), 9);
        Assert.Equal(0.2, Q(f, w, "Lapin_WallLayerQuantities", "Width", "layer:1"), 9);
        Assert.Null(f.Session.Geometry(w)!.Diagnostics);
    }

    [Fact]
    public void A_zoned_core_tapers_with_its_zones()
    {
        var f = new Fixture();
        var type = f.Type("Zoned", ("Plaster", 0.015, false), ("Brick", 0.3, true));
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["zones"] = new JsonArray([.. WallZones.Defaults("Brick").Select(z => (JsonNode)z)]) });
        var w = f.Wall(0, 0, Length, 0, type, height: Height);
        var plumb = Volume(f, w);
        Taper(f, 0.215, w);
        var zones = Parts(f, w).Where(p => p.Key?.StartsWith("zone:") == true).ToList();
        Assert.Equal(3, zones.Count);
        // The plaster (first layer, plumb face) keeps its volume; the zones lose the whole change, the top one the most.
        Assert.Equal(Length * Height * 0.015, Measure.Volume(Parts(f, w).Single(p => p.Key == "layer:0").Solid), 9);
        Assert.Equal(plumb - Length * Height * 0.1 / 2, Volume(f, w), 9);
        var ring = zones.Single(p => p.Key == "zone:ring-beam").Solid;
        Assert.Equal(Length * 0.25 * (0.3 - 0.1 * 2.875 / Height), Measure.Volume(ring), 6);
    }

    [Fact]
    public void An_L_corner_of_a_tapered_wall_and_a_plumb_one_meets_with_no_gap_or_overlap()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 300", ("Concrete", 0.3, true));
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        Taper(f, 0.2, a);
        // At z: A's strip from x 0 to B's outer face (5.15), y from −0.15 (plumb) to 0.15 − z/30; B's 0.3 wide from −0.15 to 4.
        double Area(double z) { var inner = 0.15 - z / 30; return 5.15 * (inner + 0.15) + 0.3 * (4 - inner); }
        var expected = Integral(Area, Height);
        var union = Booleans(f).Union([.. Parts(f, a).Concat(Parts(f, b)).Select(p => p.Solid)], "union")!;
        Assert.Equal(expected, Measure.Volume(union), 6);
        Assert.Equal(expected, Volume(f, a) + Volume(f, b), 6);
        // The plumb wall's end follows the tapered wall's leaning face up.
        var bv = Vertices(f, b);
        Assert.Equal(-0.15, bv.Min(p => p.Y), 9);
        Assert.Contains(At(bv, Height).Concat(At(Vertices(f, a), Height)), p => (p.Xy - new Vec2(4.85, 0.05)).Length < 1e-9);
    }

    [Fact]
    public void An_L_corner_of_a_tapered_wall_and_a_leaning_one_meets_with_no_gap_or_overlap()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 300", ("Concrete", 0.3, true));
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        Taper(f, 0.2, a);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(b), ["attrs"] = new JsonObject { [WallLean.Key] = 5 } });
        // B leans to its left (−x) by tan5° per metre, its strip w = 0.3/cos5° wide; A's inner face comes in by z/30, its outer plumb.
        var (tan, cos) = (Math.Tan(5 * Math.PI / 180), Math.Cos(5 * Math.PI / 180));
        var w = 0.3 / cos;
        double Area(double z)
        {
            var (inner, outerB) = (0.15 - z / 30, Length + w / 2 - z * tan);
            return outerB * (inner + 0.15) + w * (4 - inner);
        }
        var expected = Integral(Area, Height);
        var union = Booleans(f).Union([.. Parts(f, a).Concat(Parts(f, b)).Select(p => p.Solid)], "union")!;
        Assert.Equal(expected, Measure.Volume(union), 6);
        Assert.Equal(expected, Volume(f, a) + Volume(f, b), 6);
        // And two tapered walls: each outer face plumb, each inner face in by z/30.
        var g = new Fixture();
        var type2 = g.Type("Concrete 300", ("Concrete", 0.3, true));
        var c = g.Wall(0, 0, Length, 0, type2, height: Height);
        var d = g.Wall(Length, 0, Length, 4, type2, height: Height);
        Taper(g, 0.2, c, d);
        double Area2(double z) => 5.15 * 4.15 - (4.85 + z / 30) * (4 - (0.15 - z / 30));
        var both = Booleans(g).Union([.. Parts(g, c).Concat(Parts(g, d)).Select(p => p.Solid)], "union")!;
        Assert.Equal(Integral(Area2, Height), Measure.Volume(both), 6);
        Assert.Equal(Integral(Area2, Height), Volume(g, c) + Volume(g, d), 6);
    }

    [Fact]
    public void A_door_goes_right_through_and_stands_on_the_middle_of_the_wall_at_its_height()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, f.Type("Concrete 500", ("Concrete", 0.5, true)), height: Height);
        var d = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 2.5 })!["id"]!;
        var plumbDoor = f.Bounds(d);
        Taper(f, 0.3, w);
        var opening = f.Model.OfClass(IfcClass.OpeningElement).Single();
        var p = Openings.Place(f.Model, opening);
        // The hole the opening's width by its height by the wall's thickness there (0.5 − z/15).
        double Thickness(double z) => 0.5 - 0.2 * z / Height;
        var removed = p.Width * (Integral(Thickness, p.Sill + p.Height) - Integral(Thickness, p.Sill));
        Assert.Equal(Length * Height * 0.4 - removed, Volume(f, w), 6);
        // The door moved across to the middle of the wall at its mid-height (the right face plumb: by −0.1·z/3).
        var door = f.Bounds(d);
        var mid = p.FillSill + p.Height / 2;
        Assert.Equal((plumbDoor.Min.Y + plumbDoor.Max.Y) / 2 - 0.1 * mid / Height, (door.Min.Y + door.Max.Y) / 2, 6);
        Assert.Equal(plumbDoor.Min.Z, door.Min.Z, 9);
        // The opening IFC writes goes right through the wall at its base.
        var body = f.Session.Catalog.All<IVoidShapes>().Select(v => v.Shape(opening, f.Model)).First(x => x != null)!;
        var box = Measure.Bounds(body)!.Value;
        Assert.True(box.Min.Y <= -0.25 && box.Max.Y >= 0.25, $"{box}");
    }

    [Fact]
    public void The_plan_cuts_the_wall_at_the_plans_height_and_shows_its_base_line_below()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, Length, 0, f.Type("Concrete 500", ("Concrete", 0.5, true)), height: Height);
        Taper(f, 0.3, w);
        var plan = f.Session.Geometry(w)!.Plan!;
        var cut = plan.Where(p => p.Kind == "cut").SelectMany(p => p.Points).ToList();
        Assert.Equal(-0.25, cut.Min(p => p.Y), 9);
        Assert.Equal(0.25 - 0.2 * Openings.PlanCut / Height, cut.Max(p => p.Y), 9);
        // Thinner upwards: the leaning face's base line seen below, outside the cut; nothing overhead.
        Assert.Contains(plan, p => p.Kind == "outline" && p.Points.All(q => Math.Abs(q.Y - 0.25) < 1e-9));
        Assert.DoesNotContain(plan, p => p.Kind == "overhead");
    }

    [Fact]
    public void The_type_gives_its_walls_their_taper_and_a_walls_own_wins()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 500", ("Concrete", 0.5, true));
        var w = f.Wall(0, 0, Length, 0, type, height: Height);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, [WallTaper.Key] = 0.3, [WallTaper.FaceKey] = WallTaper.Last });
        Assert.Equal(Length * Height * 0.4, Volume(f, w), 9);
        Assert.Equal(-0.05, At(Vertices(f, w), Height).Min(p => p.Y), 9);
        Taper(f, 0.4, w);
        Assert.Equal(Length * Height * 0.45, Volume(f, w), 9);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, [WallTaper.Key] = null, [WallTaper.FaceKey] = null });
        Assert.Null(f.Model.Get(type)!.Attrs[WallTaper.Key]);
        Assert.Equal(Length * Height * 0.45, Volume(f, w), 9);
        Taper(f, null, w);
        Assert.Equal(Length * Height * 0.5, Volume(f, w), 9);
        // What the commands refuse.
        Assert.Throws<CommandException>(() => Taper(f, 0, w));
        Assert.Throws<CommandException>(() => Set(f, w, new JsonObject { [WallTaper.FaceKey] = "middle" }));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWall", new JsonObject { ["id"] = w, [WallTaper.LayerKey] = -1 }));
        // Drawn tapered.
        var drawn = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 10, ["x2"] = Length, ["y2"] = 10, ["height"] = Height, ["typeId"] = type, [WallTaper.Key] = 0.3 })!["id"]!;
        Assert.Equal(Length * Height * 0.4, Volume(f, drawn), 9);
    }

    [Fact]
    public void Each_taper_the_second_stage_cannot_build_says_its_problem_and_the_wall_keeps_its_thickness()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 300", ("Concrete", 0.3, true));
        void Raw(string id, string key, JsonNode value) => f.Session.Edit("set", e => { var x = e.Model.Get(id)!; var a = x.CloneAttrs(); a[key] = value; e.Replace(x.With(attrs: a)); });
        var curved = f.Wall(0, 0, Length, 0, type, height: Height);
        Raw(curved, PathCurve.Key, 0.8);
        Taper(f, 0.2, curved);
        Assert.Contains("curved", Problem(f, curved));
        Assert.Contains("taper", Problem(f, curved));
        var profiled = f.Wall(0, 10, Length, 10, type, height: Height);
        Raw(profiled, WallTops.TopProfileKey, WallTops.ProfileJson([(0, 2.0), (Length, 3.0)]));
        Taper(f, 0.2, profiled);
        Assert.Contains("profile", Problem(f, profiled));
        var wrapped = f.Wall(0, 20, Length, 20, f.Type("Plastered", ("Plaster", 0.015, false), ("Brick", 0.25, true)), height: Height);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wrapped, ["wrapEnds"] = "first" });
        Taper(f, 0.2, wrapped);
        Assert.Contains("wrap", Problem(f, wrapped));
        Assert.Equal(Length * Height * 0.265, Volume(f, wrapped), 6);
        // A layer left with no thickness.
        var thin = f.Wall(0, 30, Length, 30, f.Type("Thin core", ("Plaster", 0.1, false), ("Brick", 0.05, true)), height: Height);
        Taper(f, 0.1, thin);
        Assert.Contains("less than 10 mm", Problem(f, thin));
        Assert.Equal(Length * Height * 0.15, Volume(f, thin), 6);
        // A layer it has not: its core tapers, and it says so.
        var missing = f.Wall(0, 40, Length, 40, type, height: Height);
        Set(f, missing, new JsonObject { [WallTaper.Key] = 0.2, [WallTaper.LayerKey] = 5 });
        Assert.Contains("no layer 6", Problem(f, missing));
        Assert.Equal(Length * Height * 0.25, Volume(f, missing), 6);
        // Tapered and leaning: tapered, its lean ignored.
        var both = f.Wall(0, 50, Length, 50, type, height: Height);
        Set(f, both, new JsonObject { [WallTaper.Key] = 0.2, [WallLean.Key] = 10 });
        Assert.Contains("lean is ignored", Problem(f, both));
        Assert.Equal(Length * Height * 0.25, Volume(f, both), 6);
        Assert.Null(WallLean.Of(f.Model, f.Model.Get(both)!));
        // A wall with no taper says nothing of it.
        var plain = f.Wall(0, 60, Length, 60, type, height: Height);
        Raw(plain, PathCurve.Key, 0.8);
        Assert.DoesNotContain("taper", Problem(f, plain));
    }

    [Fact]
    public void Tapering_several_walls_at_once_is_one_undo_step()
    {
        var f = new Fixture();
        var type = f.Type("Concrete 300", ("Concrete", 0.3, true));
        var a = f.Wall(0, 0, Length, 0, type, height: Height);
        var b = f.Wall(Length, 0, Length, 4, type, height: Height);
        var before = Volume(f, a) + Volume(f, b);
        Taper(f, 0.2, a, b);
        Assert.Equal(0.2, f.Model.Get(a)!.Num(WallTaper.Key));
        Assert.Equal(0.2, f.Model.Get(b)!.Num(WallTaper.Key));
        Assert.True(Volume(f, a) + Volume(f, b) < before - 0.1);
        f.Session.Undo();
        Assert.Null(f.Model.Get(a)!.Attrs[WallTaper.Key]);
        Assert.Null(f.Model.Get(b)!.Attrs[WallTaper.Key]);
        Assert.Equal(before, Volume(f, a) + Volume(f, b), 9);
    }

    [Fact]
    public void IFC_writes_the_tapered_body_with_its_nominal_layers_and_reads_it_back_as_the_same_wall()
    {
        var f = new Fixture();
        var type = f.Type("Retaining", ("Plaster", 0.015, false), ("Concrete", 0.4, true), ("Plaster", 0.02, false));
        var w = f.Wall(0, 0, Length, 0, type, height: Height);
        Set(f, w, new JsonObject { [WallTaper.Key] = 0.3, [WallTaper.FaceKey] = WallTaper.Last });
        var volume = Volume(f, w);
        var path = Path.Combine(_dir, "taper.ifc");
        IfcExport.Write(f.Session, path, null);
        var text = File.ReadAllText(path);
        // The intent in Lapin_Intent; the layers' usage with their nominal thicknesses (the build-up at the base).
        Assert.Contains("'thicknessTop'", text);
        Assert.Contains("'plumbFace'", text);
        Assert.Contains("IFCMATERIALLAYERSETUSAGE", text);
        Assert.Contains(text.Split('\n'), l => l.StartsWith('#') && l.Contains("IFCMATERIALLAYER(") && l.Contains(",0.4,"));
        // IFC4's IfcMaterialLayerWithOffsets moves a layer's edges (a layer ending short of the top or running past it, AXIS3),
        // it does not taper one: a tapered wall is not written with it (plan-wall-lean-taper, L2).
        Assert.DoesNotContain("IFCMATERIALLAYERWITHOFFSETS", text);
        // No oblique extrusion: a tapered wall is not a prism.
        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        var back = g.Model.Get(w)!;
        Assert.Equal(0.3, WallTaper.TopOf(g.Model, back));
        Assert.Equal(WallTaper.Last, WallTaper.FaceOf(g.Model, back));
        Assert.False(StoredBodies.Has(back));
        Assert.Equal(volume, Volume(g, w), 6);
    }

    sealed class Close : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) a, (double, double) b) => Math.Abs(a.Item1 - b.Item1) < 1e-9 && Math.Abs(a.Item2 - b.Item2) < 1e-9;
        public int GetHashCode((double, double) obj) => 0;
    }
}
