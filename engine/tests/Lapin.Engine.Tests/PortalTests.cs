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
/// The tunnel-portal and bridge-abutment configurator (docs/plan-wing-walls.md, S3): every preset a closed body (exact where its wings
/// warp), the wings mirrored, the free end's options (square, leaning with the embankment, turned parallel to the road), the twist along
/// the whole segment, formulas, the IFC mapping (an assembly of walls, an abutment in IFC 4.3's bridge part; or one wall), its parts'
/// quantities, the GlobalIds kept when the configurator rebuilds it, one undo step, a path drawn and a section drawn.
/// </summary>
public class PortalTests(ITestOutputHelper output)
{
    static Fixture Site() => new(new CivilModule());

    static string Portal(Fixture f, JsonObject spec, double ox = 0, double oy = 0, double dx = 0, double dy = 1) =>
        (string)f.Run("civil.addPortal", new JsonObject { ["spec"] = spec, ["origin"] = new JsonArray(ox, oy), ["direction"] = new JsonArray(dx, dy) })!["id"]!;

    static JsonObject Spec(string preset, JsonObject? more = null)
    {
        var o = new JsonObject { ["preset"] = preset };
        foreach (var (k, v) in more ?? []) o[k] = v?.DeepClone();
        return o;
    }

    static IReadOnlyList<GeometryPart> Parts(Fixture f, string id) => f.Session.Geometry(id)!.Parts;

    static QuantitySet MainSet(Fixture f, string id) => f.Session.Quantities(id).Single(x => x.Name == "Lapin_RetainingWallQuantities" && x.Part == null);
    static double Q(Fixture f, string id, string name) => MainSet(f, id).Quantities.Single(x => x.Name == name).Value;

    static void Closed(Solid s)
    {
        var uses = new Dictionary<(int, int), int>();
        foreach (var face in s.Faces)
            foreach (var loop in face.Holes.Prepend(face.Outer))
                for (var k = 0; k < loop.Length; k++) { var key = (loop[k], loop[(k + 1) % loop.Length]); uses[key] = uses.GetValueOrDefault(key) + 1; }
        Assert.All(uses, u => { Assert.Equal(1, u.Value); Assert.True(uses.ContainsKey((u.Key.Item2, u.Key.Item1)), $"edge {u.Key} has no twin"); });
    }

    /// <summary>Every edge used as often one way as the other: a closed, oriented surface (an edge may be shared by four faces).</summary>
    static void Oriented(Solid s)
    {
        var uses = new Dictionary<(int, int), int>();
        foreach (var face in s.Faces)
            foreach (var loop in face.Holes.Prepend(face.Outer))
                for (var k = 0; k < loop.Length; k++) { var key = (loop[k], loop[(k + 1) % loop.Length]); uses[key] = uses.GetValueOrDefault(key) + 1; }
        Assert.All(uses, u => Assert.Equal(u.Value, uses.GetValueOrDefault((u.Key.Item2, u.Key.Item1))));
    }

    static double MeshVolume(IfcModel model, int productId) =>
        model.Shape(productId).Body.Select(x => x.Mesh).Sum(m => { var x = m.Welded(); var o = x.V[0]; return Math.Abs(x.T.Sum(t => Vec3.Dot(x.V[t.A] - o, Vec3.Cross(x.V[t.B] - o, x.V[t.C] - o))) / 6); });

    [Theory]
    [InlineData("straight")]
    [InlineData("u")]
    [InlineData("flared")]
    [InlineData("warped")]
    [InlineData("abutment")]
    public void Each_preset_builds_a_closed_body_of_three_parts_exact_where_it_warps(string preset)
    {
        var f = Site();
        var id = Portal(f, Spec(preset));
        var e = f.Model.Get(id)!;
        Assert.True(BatteredWalls.Is(e));
        var parts = Parts(f, id);
        Assert.Equal(["leg:0", "leg:1", "leg:2"], parts.Select(p => p.Key));
        Assert.Equal(["Right wing", "Face wall", "Left wing"], parts.Select(p => p.Name));
        foreach (var p in parts) Closed(p.Solid);
        var built = BatteredWalls.Of(parts[0].Solid)!;
        Closed(built.Solid!);
        // The parts are the wall: their volumes add up to it, nothing counted twice where a wing meets the face wall.
        Assert.Equal(Measure.Volume(built.Solid!), parts.Sum(p => Measure.Volume(p.Solid)), 4);
        // The wings drop from the face wall's crown (7.5, 6.0 for the abutment) to the embankment's 1.0 + 0.5 at their free ends.
        var crown = preset == "abutment" ? 6.0 : 7.5;
        Assert.Equal(crown, built.Legs[1].Z0, 9);
        Assert.Equal(1.5, built.Legs[0].Z0, 9);
        Assert.Equal(1.5, built.Legs[2].Z1, 9);
        // The wings are as long as the slope 1:1.5 reaches from the crown to the embankment.
        Assert.Equal((crown - 1.0) * 1.5, built.Legs[2].L, 9);
        var exact = BatteredWalls.Exact(built, built.Solid!, 0.003);
        if (preset == "warped")
        {
            Assert.Equal(2, built.Pieces.Warps.Count);
            Assert.NotNull(exact);
            Assert.Equal(2, exact!.SurfaceKinds()["bspline"]);
        }
        else Assert.Null(exact);
        // The abutment holds the embankment between its wings; a portal the hill behind its face.
        Assert.Equal(preset == "abutment" ? "right" : "left", e.Str("side"));
        output.WriteLine($"{preset}: {Q(f, id, "ConcreteVolume"):0.000} m³, formwork {Q(f, id, "FormworkArea"):0.00} m²");
    }

    [Fact]
    public void The_wings_lie_where_the_preset_puts_them_and_mirror_each_other()
    {
        var f = Site();
        // Straight wings in line with the face; U wings along the road; flared at 30°.
        foreach (var (preset, angle) in new[] { ("straight", 90.0), ("u", 0.0), ("flared", 30.0) })
        {
            var id = Portal(f, Spec(preset));
            var path = RetainingWalls.Path(f.Model.Get(id)!);
            // The road along +y from the origin: the face wall across it, 10 m.
            Assert.True((path[1] - new Vec2(5, 0)).Length < 1e-9);
            Assert.True((path[2] - new Vec2(-5, 0)).Length < 1e-9);
            var left = (path[3] - path[2]).Normalized();
            var right = (path[0] - path[1]).Normalized();
            Assert.Equal(Math.Cos(angle * Math.PI / 180), left.Y, 9);
            Assert.Equal(-Math.Sin(angle * Math.PI / 180), left.X, 9);
            // Mirrored across the road's axis.
            Assert.Equal(left.Y, right.Y, 9);
            Assert.Equal(-left.X, right.X, 9);
            var parts = Parts(f, id);
            Assert.Equal(Measure.Volume(parts[0].Solid), Measure.Volume(parts[2].Solid), 4);
        }
        // Not mirrored: the right wing its own length and angle.
        var own = Portal(f, Spec("flared", new JsonObject { ["mirror"] = false, ["wingLengthRight"] = 6.0, ["wingAngleRight"] = 45.0, ["endCrownRight"] = 2.5 }), 40, 0);
        var b = BatteredWalls.Of(Parts(f, own)[0].Solid)!;
        Assert.Equal(6.0, b.Legs[0].L, 9);
        Assert.Equal(2.5, b.Legs[0].Z0, 9);
        Assert.Equal(9.75, b.Legs[2].L, 9);
    }

    [Fact]
    public void A_free_end_leans_with_the_embankment_or_turns_parallel_to_the_road()
    {
        var f = Site();
        var square = Portal(f, Spec("flared"));
        var lean = Portal(f, Spec("flared", new JsonObject { ["endStyle"] = "battered", ["endBatter"] = 1.0 }), 40, 0);
        var turned = Portal(f, Spec("flared", new JsonObject { ["endStyle"] = "parallel" }), 80, 0);
        var vSquare = Q(f, square, "ConcreteVolume");
        var vLean = Q(f, lean, "ConcreteVolume");
        var vTurned = Q(f, turned, "ConcreteVolume");
        output.WriteLine($"square {vSquare:0.000}, leaning {vLean:0.000}, turned {vTurned:0.000}");
        // Leaning 1:1 the stem reaches past the end at its foot, (1.5 − −0.5) = 2.0 m out at the footing's top: more concrete.
        Assert.True(vLean > vSquare + 0.5);
        var bl = BatteredWalls.Of(Parts(f, lean)[2].Solid)!;
        Closed(bl.Solid!);
        var leg = bl.Legs[2];
        var (n, d) = BatteredWalls.EndPlane(leg, true, false);
        // The stem's end is on the plane: every vertex of the body past the crown's end at the footing's top or above lies on or inside it.
        Assert.All(bl.Solid!.Vertices.Where(p => p.Z > leg.Zf + 1e-6), p => Assert.True(Vec3.Dot(n, p) <= d + 1e-6, $"{p}"));
        Assert.Contains(bl.Solid!.Vertices, p => Math.Abs(Vec3.Dot(n, p) - d) < 1e-6 && Math.Abs(p.Z - leg.Zf) < 1e-6);
        // Turned parallel to the road (along y): the end's face of the left wing stands square to x.
        var bt = BatteredWalls.Of(Parts(f, turned)[2].Solid)!;
        Closed(bt.Solid!);
        var tl = bt.Legs[2];
        var (tn, _) = BatteredWalls.EndPlane(tl, true, false);
        Assert.Equal(0, Vec3.Dot(tn, new Vec3(0, 1, 0)), 9);
        Assert.Equal(0, tn.Z, 9);
        // Its formwork at the ends is the end's face, turned: wider than the square end's.
        Assert.True(Q(f, turned, "FormworkEnds") > Q(f, square, "FormworkEnds"));
        // A U wing is parallel to the road already: its end stays square, with a note.
        var r = f.Session.Query("civil.portalValues", new JsonObject { ["spec"] = Spec("u", new JsonObject { ["endStyle"] = "parallel" }) })!;
        Assert.Contains(((JsonArray)r["problems"]!).Select(x => (string)x!), p => p.Contains("parallel to the road"));
    }

    [Fact]
    public void A_warped_wing_twists_along_its_slope_or_along_its_whole_segment()
    {
        var f = Site();
        var slope = Portal(f, Spec("warped"));
        var whole = Portal(f, Spec("warped", new JsonObject { ["twist"] = "segment" }), 40, 0);
        var bs = BatteredWalls.Of(Parts(f, slope)[0].Solid)!;
        var bw = BatteredWalls.Of(Parts(f, whole)[0].Solid)!;
        // Closed and oriented; its wings' wide footings (the foot of a face turning to 2:3 is far out) meet the face wall's where
        // four faces may share an edge.
        Oriented(bw.Solid!);
        Assert.All(bw.Parts, p => Oriented(p.Solid));
        // Along the slope: one patch a wing, from its slope's start. Along the segment: a patch over the corner's level crown as well.
        Assert.Equal(2, bs.Pieces.Warps.Count);
        Assert.Equal(4, bw.Pieces.Warps.Count);
        var left = bw.Legs[2];
        Assert.True(left.TwistAll);
        Assert.True(left.S0 > 0.5);
        // Its batter at the corner is the face wall's (10:1, a run of 0.1), and already turning where its slope starts.
        Assert.Equal(0.1, left.Kf(0), 12);
        Assert.True(left.Kf(left.S0) > 0.1 + 1e-6);
        Assert.Equal(0.1, bs.Legs[2].Kf(bs.Legs[2].S0), 12);
        // The patch over the corner is the face: w = −T/2 − (top − z)·k(x), the crown level there.
        var corner = bw.Pieces.Warps.Single(w => w.Leg == 2 && w.X0 == 0);
        var surface = new SplineSurface(corner.Patch);
        for (var x = 0.0; x <= left.S0; x += left.S0 / 4)
            for (var t = 0.0; t <= 1; t += 0.25)
            {
                var z = left.Zf + t * (left.Top(x) - left.Zf);
                Assert.True(surface.Distance(left.P(x, left.WF(x, z), z)) < 1e-9);
            }
        // Each wing exactly: its warped face one B-spline face, its cut where it meets the face wall along its facets' edges.
        foreach (var p in new[] { bw.Parts[0], bw.Parts[2] })
        {
            var exact = BatteredWalls.Exact(bw, p.Solid, 0.003);
            Assert.NotNull(exact);
            Assert.Equal(1, exact!.SurfaceKinds()["bspline"]);
            output.WriteLine($"{p.Name}: {string.Join(", ", exact.SurfaceKinds().Select(k => $"{k.Key} {k.Value}"))}");
        }
        // Its volume: the facets' and what they miss, within the chord rule's millimetre.
        Assert.True(Math.Abs(bw.Correction) < 0.002 * Q(f, whole, "ConcreteVolume"));
    }

    [Fact]
    public void Its_numbers_are_formulas_reading_each_other_and_the_projects_parameters()
    {
        var f = Site();
        f.Run("project.setParameter", new JsonObject { ["key"] = "roadLevel", ["label"] = "Road level", ["type"] = "number", ["value"] = "2.0" });
        var spec = Spec("flared", new JsonObject
        {
            ["embankment"] = "{= roadLevel + 0.25}", ["endCrown"] = "=embankment + 0.75", ["crown"] = 8.0, ["slope"] = 2.0,
            ["wingLength"] = "{= (crown - embankment) * slope / 2}",
        });
        var v = Portals.Evaluate(spec, f.Model);
        Assert.Empty(v.Problems);
        Assert.Equal(2.25, v["embankment"], 12);
        Assert.Equal(3.0, v["endCrown"], 12);
        Assert.Equal(5.75, v["wingLength"], 12);
        Assert.Equal(9.5, Portals.Evaluate(new JsonObject { ["crown"] = 8.0, ["footingLevel"] = -1.5, ["faceLength"] = "{= height}" }, null)["faceLength"], 12);
        var id = Portal(f, spec);
        var b = BatteredWalls.Of(Parts(f, id)[0].Solid)!;
        Assert.Equal(5.75, b.Legs[2].L, 9);
        Assert.Equal(3.0, b.Legs[2].Z1, 9);
        // The formulas are kept as written, so the configurator opens on them again.
        Assert.Equal("{= roadLevel + 0.25}", (string?)f.Model.Get(id)!.Attrs["portal"]!["embankment"]);
        // A formula reading itself, or nothing that is, says so (and its default stands).
        var bad = Portals.Evaluate(new JsonObject { ["wingLength"] = "{= wingLength + 1}", ["crown"] = "{= nothing * 2}" }, f.Model);
        Assert.Contains(bad.Problems, p => p.StartsWith("Wing length"));
        Assert.Contains(bad.Problems, p => p.StartsWith("Crown elevation"));
    }

    [Fact]
    public void As_an_assembly_it_writes_its_walls_under_an_IfcElementAssembly_and_an_abutment_in_a_bridge_part()
    {
        var f = Site();
        var portal = Portal(f, Spec("warped"));
        var abutment = Portal(f, Spec("abutment"), 60, 0);
        // IFC4: each an IfcElementAssembly (user-defined: a tunnel portal, an abutment) over three IfcWall RETAININGWALL.
        var (bytes, exported) = IfcExport.Bytes(f.Session, "portal.ifc", new IfcExportOptions(AdvancedBrep: true));
        output.WriteLine(string.Join("\n", exported.Notes));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("'Tunnel portal'", text);
        Assert.Contains("'Abutment'", text);
        Assert.DoesNotContain("IFCBRIDGEPART", text);
        // The warped wings exactly, each its own IfcAdvancedBrep.
        Assert.Equal(2, text.Split("=IFCADVANCEDBREP(").Length - 1);
        var model = new IfcModel(StepFile.Read(bytes));
        var walls = model.Products.Where(p => p.Class.Equals("IfcWall", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(6, walls.Count);
        Assert.Contains(".RETAININGWALL.", text);
        Assert.Equal(6, walls.Count(w => w.Name is "Right wing" or "Face wall" or "Left wing"));
        // Read back, its parts' bodies are its concrete.
        var total = walls.Sum(w => MeshVolume(model, w.Id));
        Assert.InRange(total / (Q(f, portal, "ConcreteVolume") + Q(f, abutment, "ConcreteVolume")), 0.995, 1.005);
        // IFC 4.3: the abutment in an IfcBridgePart ABUTMENT of the project's IfcBridge, an IfcElementAssembly ABUTMENT itself.
        var (b43, _) = IfcExport.Bytes(f.Session, "portal43.ifc", new IfcExportOptions(Schema: "IFC4X3_ADD2"));
        var t43 = Encoding.UTF8.GetString(b43);
        Assert.Contains("=IFCBRIDGE(", t43);
        Assert.Contains("=IFCBRIDGEPART(", t43);
        Assert.Contains(".ABUTMENT.", t43);
        var m43 = new IfcModel(StepFile.Read(b43));
        Assert.Equal(6, m43.Products.Count(p => p.Class.Equals("IfcWall", StringComparison.OrdinalIgnoreCase)));
        // As one wall: one IfcWall RETAININGWALL, no assembly.
        f.Run("civil.updatePortal", new JsonObject { ["id"] = portal, ["spec"] = Spec("warped", new JsonObject { ["ifc"] = "wall" }) });
        Assert.Single(Parts(f, portal));
        Assert.Null(f.Model.Get(portal)!.Str("ifcMapping"));
        Assert.Equal("RETAININGWALL", f.Model.Get(portal)!.PredefinedType);
        var (plain, _) = IfcExport.Bytes(f.Session, "plain.ifc");
        var back = new IfcModel(StepFile.Read(plain));
        var one = back.Products.Single(p => p.Name == f.Model.Get(portal)!.Name);
        Assert.Equal("IfcWall", one.Class, ignoreCase: true);
        Assert.DoesNotContain("'Tunnel portal'", Encoding.UTF8.GetString(plain));
    }

    [Fact]
    public void Each_part_has_its_quantities_and_where_two_meet_is_no_formwork()
    {
        var f = Site();
        var id = Portal(f, Spec("flared"));
        var sets = f.Session.Quantities(id).Where(x => x.Part != null).ToList();
        double S(string name) => sets.Sum(x => x.Quantities.Single(q => q.Name == name).Value);
        Assert.Equal(Q(f, id, "ConcreteVolume"), S("ConcreteVolume"), 4);
        // The parts' formwork is the wall's: the faces where a wing meets the face wall are inside it.
        foreach (var k in new[] { "FormworkFront", "FormworkBack", "FormworkEnds", "FormworkFooting", "CrownArea" })
            Assert.Equal(Q(f, id, k), S(k), 3);
        Assert.Equal(Q(f, id, "ConcreteVolume"), Q(f, id, "FaceWallVolume") + Q(f, id, "WingsVolume"), 4);
        Assert.Equal(Q(f, id, "FormworkArea"), Q(f, id, "FaceWallFormwork") + Q(f, id, "WingsFormwork"), 3);
        // The face wall keeps the corners: its volume is its length between the wings' outsides, at least.
        Assert.True(Q(f, id, "FaceWallVolume") > 0.3 * Q(f, id, "ConcreteVolume"));
    }

    [Fact]
    public void The_configurator_rebuilds_the_same_element_its_GlobalIds_kept_in_one_undo_step()
    {
        var f = Site();
        var id = Portal(f, Spec("flared"), 3, 4, 1, 0);
        var (before, _) = IfcExport.Bytes(f.Session, "a.ifc");
        var gids = Gids(before);
        var count = f.Model.Entities.Count();
        f.Run("civil.updatePortal", new JsonObject { ["id"] = id, ["spec"] = Spec("warped", new JsonObject { ["wingAngle"] = 20.0, ["faceLength"] = 12.0 }) });
        Assert.Equal(count, f.Model.Entities.Count());
        var e = f.Model.Get(id)!;
        Assert.Equal("warped", (string?)e.Attrs["portal"]!["preset"]);
        // Its placement kept: the face wall's middle still at (3, 4), the road along +x.
        var path = RetainingWalls.Path(e);
        Assert.True(((path[1] + path[2]) * 0.5 - new Vec2(3, 4)).Length < 1e-9);
        Assert.Equal(12, (path[2] - path[1]).Length, 9);
        var (after, _) = IfcExport.Bytes(f.Session, "b.ifc");
        Assert.Equal(gids, Gids(after));
        // One step back is the flared portal again.
        f.Session.Undo();
        Assert.Equal("flared", (string?)f.Model.Get(id)!.Attrs["portal"]!["preset"]);
        Assert.Equal(10, (RetainingWalls.Path(f.Model.Get(id)!)[2] - RetainingWalls.Path(f.Model.Get(id)!)[1]).Length, 9);
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
    }

    /// <summary>The GlobalIds of a file's walls and assemblies, by name.</summary>
    static List<string> Gids(byte[] bytes) =>
        [.. Encoding.UTF8.GetString(bytes).Split('\n').Where(l => l.Contains("=IFCWALL(") || l.Contains("=IFCELEMENTASSEMBLY(")).Select(l => l.Split('\'')[1]).Order()];

    [Fact]
    public void Placed_on_a_road_it_stands_across_its_axis_and_a_drawn_path_or_section_replaces_the_coded_one()
    {
        var f = Site();
        var road = (string)f.Run("civil.addPath", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(50, 0)) })!["id"]!;
        var id = (string)f.Run("civil.addPortal", new JsonObject { ["spec"] = Spec("u"), ["axis"] = new JsonObject { ["id"] = road, ["at"] = new JsonArray(20, 1.5) } })!["id"]!;
        var path = RetainingWalls.Path(f.Model.Get(id)!);
        Assert.True(((path[1] + path[2]) * 0.5 - new Vec2(20, 0)).Length < 1e-9);
        // U wings along the road, +x.
        Assert.Equal(1, (path[3] - path[2]).Normalized().X, 9);
        // A path drawn: its first and last segments the wings, the crown dropping along them as the configurator says.
        var drawn = Portal(f, Spec("flared", new JsonObject { ["path"] = new JsonArray(new JsonArray(-8, 10), new JsonArray(-5, 0), new JsonArray(5, 0), new JsonArray(9, 9)) }));
        var b = BatteredWalls.Of(Parts(f, drawn)[0].Solid)!;
        Assert.Equal(3, b.Legs.Count);
        Assert.Equal(7.5, b.Legs[1].Z0, 9);
        Assert.Equal(1.5, b.Legs[2].Z1, 9);
        // A section drawn and saved as a profile: a trapezoid 0.5 at the top, 1.5 at the base, 4 high, stretched to the wall's height.
        var profile = ProjectProfiles.FromOutlines([[new(0, 0), new(1.5, 0), new(1.0, 4), new(0.5, 4)]], "Wing section", "concrete", "drawn");
        var pid = (string)f.Run("profiles.use", new JsonObject { ["profile"] = profile.ToJson(), ["library"] = "Mine" })!["id"]!;
        var withProfile = Portal(f, Spec("straight", new JsonObject { ["profileId"] = pid, ["footingThickness"] = 0.0 }), 0, 40);
        var g = f.Session.Geometry(withProfile)!;
        var solid = Assert.Single(g.Parts).Solid;
        // Its face wall: the trapezoid (area 4 at 4 high) stretched to 7.5 + 1.5 = 9 high: 9 m², along 10 m less the mitres' nothing (in line).
        var v = Measure.Volume(solid);
        // The wings 9.75 long, their height from 9 down to 3: 6 m² on average.
        output.WriteLine($"profile wall {v:0.000} m³");
        Assert.Equal(9.0 * 10 + 2 * 1.0 * 6.0 * 9.75, v, 3);
        Assert.True(Q(f, withProfile, "FormworkArea") > 0);
    }
}
