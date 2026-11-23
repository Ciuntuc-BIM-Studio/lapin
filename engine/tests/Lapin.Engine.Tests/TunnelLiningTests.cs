using System.Text;
using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The tunnel lining (docs/plan-wing-walls.md, S4): every preset a closed body, exact; the volume on a straight and on a curve against
/// the analytic one (Pappus for a torus segment); a drawn section; its blocks and rings, their GlobalIds kept when it is changed; the
/// red line followed, a vertical curve exact; a lining taken from a road between two stations, a clothoid skinned within the chord
/// rule; a portal at its end with the lining's opening through its face; the IFC assembly read back; one undo step each.
/// </summary>
public class TunnelLiningTests(ITestOutputHelper output)
{
    static Fixture Site() => new(new CivilModule());

    static string Lining(Fixture f, JsonObject spec, JsonArray points, JsonObject? more = null)
    {
        var a = new JsonObject { ["spec"] = spec, ["points"] = points };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("civil.addTunnelLining", a)!["id"]!;
    }

    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);

    static JsonObject Spec(string preset, JsonObject? more = null)
    {
        var o = new JsonObject { ["preset"] = preset };
        foreach (var (k, v) in more ?? []) o[k] = v?.DeepClone();
        return o;
    }

    static IReadOnlyList<GeometryPart> Parts(Fixture f, string id) => f.Session.Geometry(id)!.Parts;

    static double Q(Fixture f, string id, string name) =>
        f.Session.Quantities(id).Single(x => x.Name == Tunnels.SetName && x.Part == null).Quantities.Single(x => x.Name == name).Value;

    static void Closed(Solid s)
    {
        var uses = new Dictionary<(int, int), int>();
        foreach (var face in s.Faces)
            foreach (var loop in face.Holes.Prepend(face.Outer))
                for (var k = 0; k < loop.Length; k++) { var key = (loop[k], loop[(k + 1) % loop.Length]); uses[key] = uses.GetValueOrDefault(key) + 1; }
        Assert.All(uses, u => { Assert.Equal(1, u.Value); Assert.True(uses.ContainsKey((u.Key.Item2, u.Key.Item1)), $"edge {u.Key} has no twin"); });
    }

    static LiningSection SectionOf(Fixture f, string id) => Tunnels.SectionOf(f.Model, Tunnels.ValuesOf(f.Model, f.Model.Get(id)!));

    static double LayerArea(LiningSection s) => s.Outer.Area - s.Inner.Area;

    [Theory]
    [InlineData("horseshoe", 4)]
    [InlineData("circular", 28)]
    [InlineData("box", 5)]
    [InlineData("vault", 5)]
    public void Each_preset_is_closed_exact_and_as_big_as_its_section_along_a_straight(string preset, int blocks)
    {
        var f = Site();
        var id = Lining(f, Spec(preset), Pts((0, 0), (50, 0)));
        var parts = Parts(f, id);
        var section = SectionOf(f, id);
        var lining = parts.Where(p => !p.Key!.StartsWith("primary", StringComparison.Ordinal)).ToList();
        Assert.Equal(blocks, lining.Count);
        Assert.Equal(preset == "circular" ? "ring:0" : "block:0", lining[0].Key);
        foreach (var p in parts) Closed(p.Solid);
        // The concrete is the section's area along the axis; the facets hold it to within the chord rule.
        var area = LayerArea(section);
        Assert.Equal(area * 50, Q(f, id, "ConcreteVolume"), 6);
        var mesh = lining.Sum(p => Measure.Volume(p.Solid));
        Assert.InRange(mesh / (area * 50), 0.997, 1.0001);
        // The inner face: the clear outline's length along the axis (a straight's ruled faces).
        Assert.Equal(section.Inner.Perimeter * 50, Q(f, id, "FormworkInner"), 4);
        // Each block exact: its faces on planes and cylinders, its ends planar.
        var built = Tunnels.Of(lining[0].Solid)!;
        var exact = built.ExactOf(built.Parts.First(p => p.Key == lining[0].Key));
        Assert.NotNull(exact);
        var kinds = exact!.SurfaceKinds();
        output.WriteLine($"{preset}: {area:0.000} m², {Q(f, id, "ConcreteVolume"):0.00} m³, {string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}"))}");
        if (preset is "horseshoe" or "circular") Assert.False(kinds.ContainsKey("plane") && kinds["plane"] > 2);
        Assert.True(kinds.GetValueOrDefault("cylinder") > 0 || preset == "box");
        if (preset == "horseshoe") { Assert.NotNull(section.Primary); Assert.Contains(parts, p => p.Key == "primary:0"); }
    }

    [Fact]
    public void The_horseshoe_arcs_follow_round_tangent_and_the_lining_is_as_thick_as_asked()
    {
        var v = TunnelSections.Evaluate(Spec("horseshoe"), null);
        var s = TunnelSections.Build(v);
        Assert.Empty(s.Problems);
        foreach (var ring in new[] { s.Inner, s.Outer })
            for (var i = 0; i < ring.Pieces.Count; i++)
            {
                var (p, q) = (ring.Pieces[i], ring.Pieces[(i + 1) % ring.Pieces.Count]);
                Assert.True((p.B - q.A).Length < 1e-9, $"pieces {i} and {i + 1} meet");
                // Tangent: no corner anywhere round a horseshoe.
                Assert.True(Math.Abs(Vec2.Dot(p.Tangent(1).Normalized(), q.Tangent(0).Normalized()) - 1) < 1e-9, $"pieces {i} and {i + 1} are tangent");
            }
        var (inner, outer) = (s.Inner.Box(), s.Outer.Box());
        // Clear: 10.6 wide, 7.0 over the road, the invert 1.6 under it; the crown 0.35 thick, the walls 0.35, the invert 0.5.
        Assert.Equal(10.6, inner.MaxX - inner.MinX, 6);
        Assert.Equal(7.0, inner.MaxY, 9);
        Assert.Equal(-1.6, inner.MinY, 9);
        Assert.Equal(7.35, outer.MaxY, 9);
        Assert.Equal(-2.1, outer.MinY, 9);
        Assert.Equal(11.3, outer.MaxX - outer.MinX, 6);
        // The outer shell 0.25 round it.
        Assert.Equal(7.6, s.Primary!.Box().MaxY, 9);
        // Symmetric: no first moment.
        Assert.Equal(0, s.Outer.Qx - s.Inner.Qx, 9);
    }

    [Fact]
    public void On_a_level_arc_a_TBM_ring_is_a_torus_segment_its_volume_by_Pappus()
    {
        var f = Site();
        var road = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (120, 0), (120 + 120 * Math.Cos(Math.PI / 3), 120 * Math.Sin(Math.PI / 3))) })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = road, ["curves"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(150, 0, 0), new JsonArray(0, 0, 0)), ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(300, 0, 0)) } });
        var c = Corridors.ChainOf(f.Model.Get(road)!);
        // The arc of the road: its second segment.
        var (a, b) = (c.StationOf(1), c.StationOf(2));
        var id = (string)f.Run("civil.addTunnelLining", new JsonObject { ["spec"] = Spec("circular", new JsonObject { ["ringWidth"] = 2.0 }), ["axis"] = new JsonObject { ["id"] = road, ["from"] = a, ["to"] = b } })!["id"]!;
        var e = f.Model.Get(id)!;
        var lc = Corridors.ChainOf(e);
        Assert.Equal(a, lc.Start, 6);
        Assert.Equal(b - a, lc.Length, 6);
        Assert.Equal(150 * Math.PI / 3, lc.Length, 3);
        var parts = Parts(f, id);
        foreach (var p in parts) Closed(p.Solid);
        // Pappus: the ring's area (a 4.75 m circle 0.4 thick) times the path of its centre, the axis (the section is symmetric about it).
        var area = Math.PI * (5.15 * 5.15 - 4.75 * 4.75);
        var pappus = area * 150 * Math.PI / 3;
        Assert.Equal(pappus, Q(f, id, "ConcreteVolume"), 4);
        Assert.InRange(parts.Sum(p => Measure.Volume(p.Solid)) / pappus, 0.997, 1.0001);
        // The inner face, a torus's: the circle's length times the path of its centre (Pappus's first theorem).
        Assert.Equal(2 * Math.PI * 4.75 * 150 * Math.PI / 3, Q(f, id, "FormworkInner"), 3);
        // Revolved, exact: the ring's faces B-spline tori.
        var built = Tunnels.Of(parts[0].Solid)!;
        Assert.All(built.Blocks.SelectMany(x => x.Spans), s => Assert.Equal(Tunnels.SpanKind.Revolve, s.Kind));
        var exact = built.ExactOf(built.Parts[3]);
        Assert.NotNull(exact);
        Assert.Equal(8, exact!.SurfaceKinds()["bspline"]);
        Assert.Equal(2, exact.SurfaceKinds()["plane"]);
    }

    [Fact]
    public void A_drawn_section_is_its_outside_less_its_opening()
    {
        var f = Site();
        // An outside 8 × 7 with an opening 6 × 5.5 in it, 0.5 over its bottom: drawn, saved as a profile.
        var profile = ProjectProfiles.FromOutlines([[new(0, 0), new(8, 0), new(8, 7), new(0, 7)], [new(1, 0.5), new(7, 0.5), new(7, 6), new(1, 6)]], "Culvert", "concrete", "drawn");
        var pid = (string)f.Run("profiles.use", new JsonObject { ["profile"] = profile.ToJson(), ["library"] = "Mine" })!["id"]!;
        var id = Lining(f, Spec("box", new JsonObject { ["profileId"] = pid, ["invertDepth"] = 0.2 }), Pts((0, 0), (0, 20)));
        var s = SectionOf(f, id);
        Assert.Equal(6 * 5.5, s.Inner.Area, 9);
        Assert.Equal(8 * 7 - 6 * 5.5, LayerArea(s), 9);
        // The opening's floor 0.2 under the road, the outside centred on the axis.
        Assert.Equal(-0.2, s.Inner.Box().MinY, 9);
        Assert.Equal(-4, s.Outer.Box().MinX, 9);
        var parts = Parts(f, id);
        Assert.Equal(2, parts.Count);
        foreach (var p in parts) Closed(p.Solid);
        Assert.Equal(23 * 20, parts.Sum(p => Measure.Volume(p.Solid)), 6);
        var built = Tunnels.Of(parts[0].Solid)!;
        Assert.NotNull(built.ExactOf(built.Parts[0]));
        // Cut across, the section is the drawn one, bridged to its hole.
        var (sect, left, right) = Corridors.Section(f.Model, f.Model.Get(id)!, 10);
        Assert.Equal(4, left, 9);
        Assert.Equal(-4, right, 9);
        Assert.Equal(23, Math.Abs(Polygon2.SignedArea(sect.Single().Polygon)), 6);
    }

    [Fact]
    public void Its_blocks_keep_their_GlobalIds_when_it_is_changed_in_one_undo_step()
    {
        var f = Site();
        var id = Lining(f, Spec("horseshoe"), Pts((0, 0), (50, 0)));
        Assert.Equal(["block:0", "primary:0", "block:1", "primary:1", "block:2", "primary:2", "block:3", "primary:3"], Parts(f, id).Select(p => p.Key));
        // 12.5 m blocks: four, nothing left over.
        Assert.Equal(4, Q(f, id, "Blocks"));
        var (before, _) = IfcExport.Bytes(f.Session, "a.ifc");
        var gids = Gids(before);
        Assert.Equal(1 + 4 * 2, gids.Count);
        f.Run("civil.updateTunnelLining", new JsonObject { ["id"] = f.Model.Get(id)!.Id, ["spec"] = Spec("horseshoe", new JsonObject { ["thickness"] = 0.45, ["clearWidth"] = 11.0 }) });
        var (after, _) = IfcExport.Bytes(f.Session, "b.ifc");
        Assert.Equal(gids, Gids(after));
        Assert.Equal(0.45, SectionOf(f, id).Outer.Box().MaxY - SectionOf(f, id).Inner.Box().MaxY, 9);
        // Shorter blocks: the first ones keep theirs, the new ones come after.
        f.Run("civil.updateTunnelLining", new JsonObject { ["id"] = id, ["spec"] = Spec("horseshoe", new JsonObject { ["blockLength"] = 10.0 }) });
        var (more, _) = IfcExport.Bytes(f.Session, "c.ifc");
        Assert.Equal(1 + 5 * 2, Gids(more).Count);
        Assert.Subset(Gids(more).ToHashSet(), gids.ToHashSet());
        f.Session.Undo();
        Assert.Equal(12.5, Tunnels.ValuesOf(f.Model, f.Model.Get(id)!)["blockLength"], 9);
        f.Session.Undo();
        Assert.Equal(10.6, Tunnels.ValuesOf(f.Model, f.Model.Get(id)!)["clearWidth"], 9);
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
    }

    /// <summary>The GlobalIds of a file's assemblies and proxies.</summary>
    static List<string> Gids(byte[] bytes) =>
        [.. Encoding.UTF8.GetString(bytes).Split('\n').Where(l => l.Contains("=IFCBUILDINGELEMENTPROXY(") || l.Contains("=IFCELEMENTASSEMBLY(")).Select(l => l.Split('\'')[1]).Order()];

    [Fact]
    public void It_follows_the_red_line_its_vertical_curve_exact()
    {
        var f = Site();
        var id = Lining(f, Spec("vault"), Pts((0, 0), (100, 0)));
        // A crest: up 3 % to 50 m, down 2 % after, rounded over 40 m.
        f.Run("civil.setCorridor", new JsonObject { ["id"] = id, ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 10, 0), new JsonArray(50, 11.5, 40), new JsonArray(100, 10.5, 0)) } });
        var parts = Parts(f, id);
        var built = Tunnels.Of(parts[0].Solid)!;
        var spans = built.Blocks.SelectMany(b => b.Spans).ToList();
        Assert.Contains(spans, s => s.Kind == Tunnels.SpanKind.Translational);
        Assert.All(spans, s => Assert.True(s.Kind is Tunnels.SpanKind.Ruled or Tunnels.SpanKind.Translational));
        // The road's level on the axis is the red line: at the crest's PVI, 11.5 less the curve's middle ordinate (0.05 × 40 / 8 = 0.25).
        var z = built.Grade.Z(50);
        Assert.Equal(11.25, z, 9);
        var inner = SectionOf(f, id).Inner.Box();
        var floor = parts.Where(p => !p.Key!.StartsWith("primary")).SelectMany(p => p.Solid.Vertices).Where(v => Math.Abs(v.X - 50) < 1e-6 && Math.Abs(v.Y) < 1e-6).Select(v => v.Z).ToList();
        Assert.Contains(floor, v => Math.Abs(v - (z + inner.MinY)) < 1e-9);
        foreach (var p in parts) Closed(p.Solid);
        // The volume: the section along the axis in plan (the grade shears it).
        Assert.Equal(LayerArea(SectionOf(f, id)) * 100, Q(f, id, "ConcreteVolume"), 6);
        // The block over the crest exact: its parabola a quadratic B-spline.
        var crest = built.Parts.First(p => p.Key == "block:4");
        var exact = built.ExactOf(crest);
        Assert.NotNull(exact);
        Assert.True(exact!.SurfaceKinds()["bspline"] > 0);
    }

    [Fact]
    public void Taken_from_a_road_between_two_stations_it_follows_its_curves_and_clothoids()
    {
        var f = Site();
        var road = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (200, 0), (300, 150)), ["template"] = "road" })!["id"]!;
        // A 250 m curve with 40 m clothoids either side; the road rising 2 %.
        f.Run("civil.setCorridor", new JsonObject { ["id"] = road, ["curves"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(250, 40, 40), new JsonArray(0, 0, 0)), ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 100, 0), new JsonArray(400, 108, 0)) } });
        var rc = Corridors.ChainOf(f.Model.Get(road)!);
        var id = (string)f.Run("civil.addTunnelLining", new JsonObject
        {
            ["spec"] = Spec("horseshoe", new JsonObject { ["primary"] = 0.0 }),
            ["axis"] = new JsonObject { ["id"] = road, ["from"] = new JsonArray(120, 2), ["to"] = 290.0 },
        })!["id"]!;
        var e = f.Model.Get(id)!;
        var lc = Corridors.ChainOf(e);
        // Its stations the road's: from where the click is nearest the axis (on the first clothoid).
        Assert.Equal(rc.Project(new Vec2(120, 2)).Station, lc.Start, 6);
        Assert.Equal(290, lc.End, 6);
        foreach (var s in new[] { lc.Start + 0.1, 170, 205.5, 250, 289.9 })
        {
            Assert.True((lc.PointAt(s) - rc.PointAt(s)).Length < 1e-6, $"the axis at {s}");
            Assert.True((lc.DirectionAt(s) - rc.DirectionAt(s)).Length < 1e-6, $"the direction at {s}");
        }
        var built = Tunnels.Of(Parts(f, id)[0].Solid)!;
        Assert.Equal(100 + 0.02 * 200, built.Grade.Z(200), 9);
        var spans = built.Blocks.SelectMany(b => b.Spans).ToList();
        Assert.Contains(spans, s => s.Kind == Tunnels.SpanKind.Skin);
        foreach (var p in Parts(f, id)) Closed(p.Solid);
        // On the curve the section's area along the axis still (it is symmetric); the facets within the chord rule.
        var area = LayerArea(SectionOf(f, id));
        Assert.Equal(area * lc.Length, Q(f, id, "ConcreteVolume"), 4);
        Assert.InRange(Parts(f, id).Sum(p => Measure.Volume(p.Solid)) / (area * lc.Length), 0.997, 1.0001);
        // A block on the clothoid is skinned within the chord rule: exact (to 2 mm) as B-splines.
        var onSpiral = built.Blocks.First(b => b.Spans.Any(s => s.Kind == Tunnels.SpanKind.Skin));
        var part = built.Parts.First(p => p.Block == onSpiral.Index && p.Layer == "lining");
        var exact = built.ExactOf(part);
        output.WriteLine(built.ExactProblems.GetValueOrDefault(part.Key) ?? "exact");
        Assert.NotNull(exact);
        // The skinned surface strays from the true sweep by less than the chord rule between its sections.
        var skin = onSpiral.Spans.First(s => s.Kind == Tunnels.SpanKind.Skin);
        var piece = SectionOf(f, id).Inner.Pieces[2];
        var sections = skin.Us.Select(u => piece.Curve().Transformed(skin.At(u).Map)).ToList();
        var surface = BSplineSurface.Skin(sections, 3, skin.Us);
        var worst = 0.0;
        for (var k = 0; k < 200; k++)
        {
            var u = (k + 0.5) / 200;
            var t = 0.37;
            var truth = skin.At(u).Map(piece.At(t));
            worst = Math.Max(worst, (new SplineSurface(surface).Nearest(truth) is var (su, sv) ? surface.At(su, sv) - truth : Vec3.Zero).Length);
        }
        output.WriteLine($"skin strays {worst * 1000:0.000} mm");
        Assert.True(worst < Chords.Tolerance);
    }

    [Fact]
    public void A_portal_at_its_end_stands_across_it_the_lining_through_its_face()
    {
        var f = Site();
        var id = Lining(f, Spec("horseshoe"), Pts((0, 0), (0, 40)));
        var wall = (string)f.Run("civil.tunnelPortal", new JsonObject { ["id"] = id, ["end"] = "start" })!["id"]!;
        var w = f.Model.Get(wall)!;
        Assert.Equal(id, (string?)w.Attrs["tunnelOpening"]!["id"]);
        var spec = (JsonObject)w.Attrs["portal"]!;
        // Its face across the axis at the start, the wings going out of the tunnel (−y), wider and higher than the lining.
        var path = RetainingWalls.Path(w);
        Assert.True(((path[1] + path[2]) * 0.5).Length < 1e-9);
        Assert.True((path[3] - path[2]).Y < 0);
        var box = SectionOf(f, id).Outside.Box();
        Assert.True((double)spec["faceLength"]! > box.MaxX - box.MinX + 3.9);
        Assert.True((double)spec["crown"]! > box.MaxY + 0.9);
        // The opening through it: less concrete than the same portal without it.
        var plain = (string)f.Run("civil.addPortal", new JsonObject { ["spec"] = spec.DeepClone(), ["origin"] = new JsonArray(100, 0), ["direction"] = new JsonArray(0, -1) })!["id"]!;
        var cut = Parts(f, wall).Sum(p => Measure.Volume(p.Solid));
        var whole = Parts(f, plain).Sum(p => Measure.Volume(p.Solid));
        output.WriteLine($"portal {cut:0.00} m³, without the opening {whole:0.00} m³");
        Assert.True(cut < whole - 10);
        // A point on the axis, in the road's clear opening just outside the end: no concrete; one in the lining's thickness behind it: none either.
        var s = Parts(f, wall)[1].Solid;
        Assert.False(Inside(s, new Vec3(0, -0.2, 3)));
        Assert.False(Inside(s, new Vec3(0, 0.2, box.MaxY - 0.1)));
        // The lining changed, the portal's opening with it.
        f.Run("civil.updateTunnelLining", new JsonObject { ["id"] = id, ["spec"] = Spec("horseshoe", new JsonObject { ["clearWidth"] = 9.0, ["crownRadius"] = 4.5 }) });
        Assert.NotEqual(cut, Parts(f, wall).Sum(p => Measure.Volume(p.Solid)), 3);
        // Skewed: the lining's end turned, the face wall with it.
        var skewed = (string)f.Run("civil.tunnelPortal", new JsonObject { ["id"] = id, ["end"] = "end", ["skew"] = 20.0 })!["id"]!;
        Assert.Equal(20, f.Model.Get(id)!.Num("skewEnd"), 9);
        var sp = RetainingWalls.Path(f.Model.Get(skewed)!);
        var face = (sp[2] - sp[1]).Normalized();
        Assert.Equal(Math.Cos(20 * Math.PI / 180), Math.Abs(face.X), 6);
        foreach (var p in Parts(f, id)) Closed(p.Solid);
    }

    /// <summary>Whether a point is inside a closed solid: a ray's crossings, odd.</summary>
    static bool Inside(Solid s, Vec3 p)
    {
        var hits = 0;
        var dir = new Vec3(0.5773, 0.5774, 0.5775).Normalized();
        foreach (var (a, b, c) in Measure.Triangles(s))
        {
            var (v0, v1, v2) = (s.Vertices[a], s.Vertices[b], s.Vertices[c]);
            var e1 = v1 - v0; var e2 = v2 - v0;
            var h = Vec3.Cross(dir, e2);
            var det = Vec3.Dot(e1, h);
            if (Math.Abs(det) < 1e-12) continue;
            var inv = 1 / det;
            var sv = p - v0;
            var u = Vec3.Dot(sv, h) * inv;
            if (u < 0 || u > 1) continue;
            var q = Vec3.Cross(sv, e1);
            var v = Vec3.Dot(dir, q) * inv;
            if (v < 0 || u + v > 1) continue;
            if (Vec3.Dot(e2, q) * inv > 1e-9) hits++;
        }
        return hits % 2 == 1;
    }

    [Fact]
    public void As_an_assembly_its_blocks_are_proxies_under_an_element_assembly_exact_and_read_back()
    {
        var f = Site();
        var id = Lining(f, Spec("horseshoe", new JsonObject { ["primary"] = 0.0, ["blockLength"] = 10.0 }), Pts((0, 0), (30, 0)));
        var (bytes, exported) = IfcExport.Bytes(f.Session, "tunnel.ifc", new IfcExportOptions(AdvancedBrep: true));
        output.WriteLine(string.Join("\n", exported.Notes));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("'Tunnel lining'", text);
        Assert.Contains("=IFCELEMENTASSEMBLY(", text);
        Assert.Equal(3, text.Split("'Lining block'").Length - 1);
        Assert.Equal(3, text.Split("=IFCADVANCEDBREP(").Length - 1);
        var model = new IfcModel(StepFile.Read(bytes));
        var blocks = model.Products.Where(p => p.Class.Equals("IfcBuildingElementProxy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(["Block 1", "Block 2", "Block 3"], blocks.Select(b => b.Name).Order());
        var total = blocks.Sum(b => MeshVolume(model, b.Id));
        Assert.InRange(total / Q(f, id, "ConcreteVolume"), 0.995, 1.005);
        // IFC 4.3 the same (it has no tunnel lining either).
        var (b43, _) = IfcExport.Bytes(f.Session, "tunnel43.ifc", new IfcExportOptions(Schema: "IFC4X3_ADD2"));
        Assert.Equal(3, new IfcModel(StepFile.Read(b43)).Products.Count(p => p.Class.Equals("IfcBuildingElementProxy", StringComparison.OrdinalIgnoreCase)));
        // As one body: one proxy, no assembly; the whole lining exact.
        f.Run("civil.updateTunnelLining", new JsonObject { ["id"] = id, ["spec"] = Spec("horseshoe", new JsonObject { ["primary"] = 0.0, ["ifc"] = "body" }) });
        Assert.Single(Parts(f, id));
        var (plain, _) = IfcExport.Bytes(f.Session, "plain.ifc", new IfcExportOptions(AdvancedBrep: true));
        var pt = Encoding.UTF8.GetString(plain);
        Assert.DoesNotContain("=IFCELEMENTASSEMBLY(", pt);
        Assert.Equal(1, pt.Split("=IFCADVANCEDBREP(").Length - 1);
        var back = new IfcModel(StepFile.Read(plain));
        var one = back.Products.Single(p => p.Class.Equals("IfcBuildingElementProxy", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(MeshVolume(back, one.Id) / Q(f, id, "ConcreteVolume"), 0.995, 1.005);
    }

    static double MeshVolume(IfcModel model, int productId) =>
        model.Shape(productId).Body.Select(x => x.Mesh).Sum(m => { var x = m.Welded(); var o = x.V[0]; return Math.Abs(x.T.Sum(t => Vec3.Dot(x.V[t.A] - o, Vec3.Cross(x.V[t.B] - o, x.V[t.C] - o))) / 6); });

    [Fact]
    public void Its_long_section_follows_its_curves()
    {
        var f = Site();
        var road = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts((0, 0), (120, 0), (220, 110)), ["template"] = "road" })!["id"]!;
        f.Run("civil.setCorridor", new JsonObject { ["id"] = road, ["curves"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(160, 30, 30), new JsonArray(0, 0, 0)), ["profile"] = new JsonObject { ["mode"] = "design", ["pvis"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(300, 0, 0)) } });
        var id = (string)f.Run("civil.addTunnelLining", new JsonObject { ["spec"] = Spec("horseshoe"), ["axis"] = new JsonObject { ["id"] = road, ["from"] = 40.0, ["to"] = 200.0 } })!["id"]!;
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["along"] = id, ["depth"] = 0.2, ["scale"] = 500 })!["id"]!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, f.Model.Get(along)!);
        // Its line the axis round the curve, by chords of 2 cm at most: some tens of pieces.
        Assert.InRange(SectionPaths.Points(f.Model.Get(along)!).Count, 10, 80);
        Assert.Null(problem);
        Assert.True(d!.Cuts.Count >= 2);
    }

    [Fact]
    public void Its_section_at_a_station_and_its_plan_show_its_layers_axis_and_joints()
    {
        var f = Site();
        var id = Lining(f, Spec("horseshoe"), Pts((0, 0), (25, 0)), new JsonObject { ["level"] = 50.0 });
        var (parts, left, right) = Corridors.Section(f.Model, f.Model.Get(id)!, 12);
        // Two layers, each in two halves; their areas the section's.
        Assert.Equal(["lining", "lining", "primary", "primary"], parts.Select(p => p.Key));
        var s = SectionOf(f, id);
        Assert.InRange(parts.Where(p => p.Key == "lining").Sum(p => Math.Abs(Polygon2.SignedArea(p.Polygon))) / LayerArea(s), 0.999, 1.0);
        Assert.Equal(s.Primary!.Box().MaxX, left, 9);
        Assert.Equal(50 + 7.6, parts.SelectMany(p => p.Polygon).Max(p => p.Y), 6);
        var cross = f.Session.Query("civil.crossSection", new JsonObject { ["id"] = id, ["station"] = 12.0 })!;
        Assert.Equal(4, ((JsonArray)cross["parts"]!).Count);
        var plan = f.Session.Geometry(id)!.Plan!;
        // The outside's two edges and the opening's (hidden), the axis, the two ends, one joint between the two 12.5 m blocks.
        Assert.Equal(5, plan.Count(p => p.Kind == "dashed"));
        Assert.Single(plan, p => p.Style?.Pattern == "center");
        Assert.Equal(2, plan.Count(p => p.Kind == "symbol" && p.Style == null));
        // A section along its axis (its long section) cuts its crown and its invert; one across it at a station cuts both layers.
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["along"] = id, ["depth"] = 0.2 })!["id"]!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, f.Model.Get(along)!);
        output.WriteLine(problem ?? "drawn");
        Assert.Null(problem);
        Assert.True(d!.Cuts.Count >= 2, $"the long section cuts the lining ({d.Cuts.Count} cut areas, {d.Lines.Count} lines)");
        // The corridor's long section: the red line along it.
        var corridor = f.Session.Query("civil.corridor", new JsonObject { ["id"] = id })!;
        Assert.Equal("tunnelLining", (string?)corridor["kind"]);
    }
}
