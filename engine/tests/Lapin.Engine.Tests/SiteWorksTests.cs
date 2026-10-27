using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.SiteWorks;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Site works (docs/plan-site-works.md): the named faces elements offer,
/// facade scaffolding along a line or the walls' faces, formwork on floors and
/// beams, each temporary in the execution phase, and what they count, schedule
/// and export. The numbers are worked out by hand in each test.
/// </summary>
public class SiteWorksTests
{
    /// <summary>The full set of modules, on a ground floor at ±0,00 and a first floor at +3,00 (both 3 m high).</summary>
    static Fixture Site()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new CivilModule(), new SiteWorksModule());
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Ground floor", ["elevation"] = 0, ["height"] = 3 });
        f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3, ["height"] = 3 });
        return f;
    }

    static string StoreyNamed(Fixture f, string name) => Spatial.Storeys(f.Model).First(s => s.Name == name).Id;

    static IReadOnlyDictionary<string, double> Q(Fixture f, string id) =>
        f.Session.Quantities(id).SelectMany(s => s.Quantities).GroupBy(q => q.Name).ToDictionary(g => g.Key, g => g.First().Value);

    /// <summary>A 6 × 4 m floor, 0,20 m of concrete, its top at the first floor's level (+3,00), so its soffit at +2,80.</summary>
    static string Floor(Fixture f, double x0 = 0, double y0 = 0, double x1 = 6, double y1 = 4)
    {
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "RC 200",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = 0.20, ["core"] = true }),
        })!["id"]!;
        return (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = StoreyNamed(f, "First floor"), ["typeId"] = type,
            ["points"] = new JsonArray(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1)),
        })!["id"]!;
    }

    /// <summary>A 0,25 × 0,40 m beam on the ground floor: its top at +3,00 (the storey's top), its bottom at +2,60.</summary>
    static string Beam(Fixture f, double x1, double y1, double x2, double y2) =>
        (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = StoreyNamed(f, "Ground floor"), ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 })!["id"]!;

    static void Near(double expected, double actual) => Assert.InRange(actual, expected * 0.999, expected * 1.001);

    static string[] Ids(JsonNode? r) => ((JsonArray)r!["ids"]!).Select(x => (string)x!).ToArray();

    [Fact]
    public void Floors_and_beams_name_their_faces_and_a_pick_finds_the_face_it_lands_on()
    {
        var f = Site();
        var slab = Floor(f);
        var faces = (JsonArray)f.Session.Query("core.faces", new JsonObject { ["id"] = slab })!;
        var roles = faces.Select(x => (string)x!["role"]!).ToList();
        Assert.Equal(["Soffit", "Top", "Edge[0]", "Edge[1]", "Edge[2]", "Edge[3]"], roles);
        Assert.Equal(24, (double)faces[0]!["size"]!, 6);
        Assert.Equal(2.80, (double)faces[0]!["outline"]![0]![2]!, 6);
        Assert.Equal(6 * 0.20, (double)faces[2]!["size"]!, 6);                    // the edge along y = 0: 6 m × 0,20 m
        // In 3D: a point on the soffit, looking up at it.
        var soffit = f.Session.Query("core.faceAt", new JsonObject { ["id"] = slab, ["point"] = new JsonArray(3, 2, 2.8), ["normal"] = new JsonArray(0, 0, -1) })!;
        Assert.Equal("Soffit", (string)soffit["role"]!);
        // In plan: a click just outside the edge along y = 0.
        var edge = f.Session.Query("core.faceAt", new JsonObject { ["id"] = slab, ["point"] = new JsonArray(3, -0.05) })!;
        Assert.Equal("Edge[0]", (string)edge["role"]!);

        var beam = Beam(f, 0, 0, 5, 0);
        var b = ((JsonArray)f.Session.Query("core.faces", new JsonObject { ["id"] = beam })!).ToDictionary(x => (string)x!["role"]!, x => (double)x!["size"]!);
        Assert.Equal(5 * 0.25, b["Bottom"], 6);
        Assert.Equal(5 * 0.40, b["Side.Left"], 6);
        Assert.Equal(5 * 0.40, b["Side.Right"], 6);
        Assert.Equal(5, b["Edge.BottomLeft"], 6);
        var side = f.Session.Query("core.faceAt", new JsonObject { ["id"] = beam, ["point"] = new JsonArray(2, 0.125, 2.8), ["normal"] = new JsonArray(0, 1, 0) })!;
        Assert.Equal("Side.Left", (string)side["role"]!);
    }

    [Fact]
    public void A_floor_is_formed_on_beams_and_props_by_its_types_spacings_and_measured_by_its_contact()
    {
        var f = Site();
        var slab = Floor(f);
        var fw = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab) }))[0];
        var q = Q(f, fw);
        Assert.Equal(24, q["SoffitArea"], 6);
        Assert.Equal(20 * 0.20, q["EdgeArea"], 6);                                  // four free edges, the perimeter × the thickness
        Assert.Equal(28, q["ContactArea"], 6);
        // 0,20 m thick: secondary beams at 0,50, primary at 2,00, props at 1,50; the first 0,15 m in.
        // Primary along x (the longest side) at y = 0,15 / 2,00 / 3,85, each 6 m on five props; 13 secondary beams 4 m long.
        Assert.Equal(15, q["Props"]);
        Assert.Equal(18, q["PrimaryLength"], 6);
        Assert.Equal(6, q["PrimaryPieces"]);                                         // 6 m = 3,60 + 2,45 on each line
        Assert.Equal(13, q["SecondaryPieces"]);                                      // each 4 m on one 4,50 m beam
        Assert.Equal(13 * 4, q["SecondaryLength"], 6);
        // The props stand on the ground floor (±0,00): 2,80 − 0,021 − 0,20 − 0,20 = 2,379 m, the 1,5–2,5 m prop.
        Assert.Equal(15, q["Props:Prop 1.5–2.5 m"]);
        // It is temporary: created and struck in the execution phase, which became the active one.
        var e = f.Model.Get(fw)!;
        var phases = PhasesModule.Settings(f.Model);
        Assert.Equal("execution", phases.ActivePhase!.Kind);
        Assert.Equal(phases.ActivePhase.Id, e.Str("phaseCreated"));
        Assert.Equal(phases.ActivePhase.Id, e.Str("phaseDemolished"));
        Assert.Equal("temporary", phases.State(e.Str("phaseCreated"), e.Str("phaseDemolished"), phases.ActivePhase.Id));
    }

    [Fact]
    public void A_beam_under_a_floor_is_formed_below_it_and_the_floor_stops_at_it()
    {
        var f = Site();
        var slab = Floor(f);
        var beam = Beam(f, 3, 0, 3, 4);
        var ids = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab, beam) }));
        var floor = Q(f, ids[0]);
        Assert.Equal(24 - 0.25 * 4, floor["SoffitArea"], 6);                       // the beam's strip is not formed from the floor
        var b = Q(f, ids[1]);
        Assert.Equal(4 * 0.25, b["BottomArea"], 6);
        // Each side as high as the beam below the floor: 2,80 − 2,60 = 0,20 m, over 4 m.
        Assert.Equal(2 * 4 * 0.20, b["SideArea"], 6);
        Assert.Equal(8, b["ChamferLength"], 6);                                      // both bottom edges
        Assert.Equal(4, b["Props"]);                                                  // 4 m at no more than 1,20 m, 0,30 m in
        Assert.Equal(8, b["Yokes"]);                                                  // 0,40 m deep: every 0,60 m, 0,15 m in

        // An edge beam: the floor on its left only, its right side formed full height (0,40 m); the first beam meets it at x = 3, its 0,25 m not formed.
        var edge = Beam(f, 0, 0, 6, 0);
        var e = Q(f, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(edge) }))[0]);
        Assert.Equal(5.75, e["FormedLength"], 6);
        Assert.Equal(5.75 * 0.20 + 5.75 * 0.40, e["SideArea"], 6);
    }

    [Fact]
    public void A_column_takes_its_share_out_of_the_floor_and_breaks_the_beam_it_carries()
    {
        var f = Site();
        var slab = Floor(f);
        var beam = Beam(f, 3, 0, 3, 4);
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = StoreyNamed(f, "Ground floor"), ["x"] = 3, ["y"] = 2 });
        var ids = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab, beam) }));
        // The beam's strip and the column's 0,30 × 0,30, less where they overlap (0,25 × 0,30).
        Assert.Equal(24 - (1.0 + 0.09 - 0.075), Q(f, ids[0])["SoffitArea"], 6);
        var b = Q(f, ids[1]);
        Assert.Equal(3.7, b["FormedLength"], 6);                                     // 4 m less the column's 0,30 m
        Assert.Equal(3.7 * 0.25, b["BottomArea"], 6);
        Assert.Equal(2 * 3.7 * 0.20, b["SideArea"], 6);
    }

    [Fact]
    public void Formwork_follows_its_floor_and_forms_only_the_faces_picked()
    {
        var f = Site();
        var slab = Floor(f);
        var fw = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab), ["faces"] = new JsonArray("Soffit") }))[0];
        Assert.Equal(0, Q(f, fw)["EdgeArea"], 9);
        // An edge picked later goes on the same formwork.
        Assert.Equal(fw, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab), ["faces"] = new JsonArray("Edge[0]") }))[0]);
        Assert.Equal(6 * 0.20, Q(f, fw)["EdgeArea"], 6);
        // The floor made 8 m long: the soffit follows.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(8, 0), new JsonArray(8, 4), new JsonArray(0, 4)) });
        Assert.Equal(32, Q(f, fw)["SoffitArea"], 6);
        Assert.Throws<CommandException>(() => f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab), ["faces"] = new JsonArray("Side.Left") }));
    }

    [Fact]
    public void A_floors_formwork_stands_on_the_storey_under_it_and_its_formwork_plan_draws_it()
    {
        var f = Site();
        var slab = Floor(f);
        var fw = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab) }))[0];
        Assert.Equal(StoreyNamed(f, "Ground floor"), Spatial.StoreyOf(f.Model, fw)!.Id);
        var view = (string)f.Run("sitework.formworkPlan", new JsonObject { ["storeyId"] = StoreyNamed(f, "Ground floor") })!["id"]!;
        Assert.Equal(PhasesModule.Settings(f.Model).ActivePhase!.Id, f.Model.Get(view)!.Str("phaseId"));
        var drawing = f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!.ToJsonString();
        Assert.Contains(fw, drawing);                                                 // the props cut, the beams over them
        Assert.Equal("up", f.Model.Get(view)!.Str("look"));                          // from below by default
    }

    [Fact]
    public void A_plan_is_seen_from_above_or_from_below_and_from_below_it_lies_as_the_plan_does()
    {
        var f = Site();
        var beam = Beam(f, 1, 2, 5, 2);                                              // its bottom at +2,60, over the cut (+1,20)
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = StoreyNamed(f, "Ground floor") })!["id"]!;
        var spec = Views.Spec(f.Model, f.Model.Get(plan)!, []).Spec!;
        Assert.False(spec.Up);
        f.Run("view.setRange", new JsonObject { ["id"] = plan, ["look"] = "up" });
        Assert.True(Views.Spec(f.Model, f.Model.Get(plan)!, []).Spec!.Up);
        var up = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        var pens = up["pens"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(pens, p => (string)p["layer"]! == "SEEN");                    // seen from below, not dashed overhead
        Assert.DoesNotContain(pens, p => (string)p["layer"]! == "OVERHEAD");
        // Turned back: the beam's lines lie where it is in plan (y = 2 ± 0,125), not mirrored to y = −2.
        var ys = pens.SelectMany(p => p["segments"]?.AsArray().Select(x => (double)x!).Where((_, i) => i % 2 == 1) ?? []).ToList();
        Assert.NotEmpty(ys);
        Assert.All(ys, y => Assert.InRange(y, 1.8, 2.2));
        f.Run("view.setRange", new JsonObject { ["id"] = plan, ["look"] = "down" });
        Assert.Null(f.Model.Get(plan)!.Str("look"));
        Assert.Throws<CommandException>(() => f.Run("view.setRange", new JsonObject { ["id"] = plan, ["look"] = "sideways" }));
    }


    [Fact]
    public void A_curved_beam_is_formed_along_its_arc_its_outer_side_longer_than_its_inner()
    {
        var f = Site();
        // From (0, 0) to (6, 0) through (3, 1): a radius of 5 m, a sweep of 4·atan(1/3), the axis 6,435 m long.
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = StoreyNamed(f, "Ground floor"), ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["x3"] = 3, ["y3"] = 1 })!["id"]!;
        var sweep = 4 * Math.Atan(1.0 / 3);
        var length = 5 * sweep;
        var fw = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(beam) }))[0];
        var q = Q(f, fw);
        // The arc is followed in chords of at most 5°: within a thousandth of it.
        Near(length, q["FormedLength"]);
        Near(length * 0.25, q["BottomArea"]);
        Near(2 * length * 0.40, q["SideArea"]);                                       // (R + w/2)·θ + (R − w/2)·θ = 2·R·θ
        var l = Formwork.BeamLayoutOf(f.Model.Get(fw)!, f.Model, f.Session.Catalog)!;
        var bySide = l.Sides.GroupBy(x => x.Side).ToDictionary(g => g.Key, g => g.Sum(x => x.Length));
        Near(0.25 * sweep, Math.Abs(bySide["Side.Left"] - bySide["Side.Right"]));   // the outer side longer by w·θ
        Near(2 * length, q["ChamferLength"]);
        Assert.Equal(6, q["Props"]);                                                  // 6,435 m at no more than 1,20 m, 0,30 m in
    }

    [Fact]
    public void A_scaffold_follows_a_curved_wall_in_chords_and_stays_outside_it()
    {
        var f = Site();
        var t = f.Type("Masonry 300", ("Masonry", 0.30, true));
        f.Wall(0, 0, 10, 0, t); f.Wall(10, 0, 10, 6, t); f.Wall(0, 6, 0, 0, t);
        // The fourth side bulges out to y = 8.
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 10, ["y1"] = 6, ["x2"] = 0, ["y2"] = 6, ["x3"] = 5, ["y3"] = 8, ["typeId"] = t });
        var walls = f.Model.OfClass(IfcClass.Wall).Select(w => (JsonNode)w.Id).ToArray();
        var r = f.Run("sitework.scaffoldFromWalls", new JsonObject { ["wallIds"] = new JsonArray(walls) })!;
        Assert.True((bool)r["closed"]!);
        var layout = Scaffolds.LayoutOf(f.Model.Get((string)r["id"]!)!, f.Model, f.Session.Catalog)!;
        Assert.Empty(layout.Problems);
        // The arc's outer face is about 12 m long: four chords of at most 3 m, and the three straight sides.
        Assert.Equal(7, layout.Runs.Count);
        // The chords: about 3,1 m at the scaffold's radius, those at the corners going on to close them.
        Assert.Equal(4, layout.Runs.Count(x => x.Length < 5));
        Assert.All(layout.Runs.Where(x => x.Length < 5), x => Assert.InRange(x.Length, 2.8, 4.0));
        // Every standard outside the walls: below y = −0,15, past x = 10,15 or before −0,15, or beyond the arc.
        foreach (var run in layout.Runs)
            foreach (var x in run.Stations)
            {
                var p = run.A + run.Dir * x;
                var c = new Vec2(5, 6 + (25 - 4) / 4.0 - 2);                           // the arc's centre: radius 7,25, through (0, 6), (5, 8), (10, 6)
                var inside = p.X > -0.15 && p.X < 10.15 && p.Y > -0.15 && (p.Y < 6 || (p - new Vec2(5, 8 - 7.25)).Length < 7.25 + 0.15);
                Assert.False(inside, $"{p} is inside the walls");
            }
    }

    [Fact]
    public void A_column_is_formed_up_to_the_floor_on_it_with_its_clamps_and_props()
    {
        var f = Site();
        Floor(f);
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = StoreyNamed(f, "Ground floor"), ["x"] = 3, ["y"] = 2 })!["id"]!;
        var q = Q(f, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(column) }))[0]);
        // 0,30 × 0,30 from ±0,00 up to the floor's soffit at +2,80.
        Assert.Equal(4 * 0.30 * 2.80, q["ContactArea"], 6);
        Assert.Equal(5, q["Clamps"]);                                                 // every 0,60 m over 2,80 m
        Assert.Equal(2, q["Braces"]);
        Assert.Equal(4 * 1 * 2, q["Panels"]);                                         // each side one 0,30 m panel, 2,70 + 0,60 high
    }

    [Fact]
    public void A_concrete_wall_is_formed_both_faces_and_its_free_ends_less_its_openings_with_ties_through_it()
    {
        var f = Site();
        var t = f.Type("RC 250", ("Reinforced concrete", 0.25, true));
        var wall = f.Wall(0, 0, 6, 0, t);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 3 });
        var q = Q(f, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(wall) }))[0]);
        // Both faces 6 × 3 m, both ends 0,25 × 3 m, less the door through both faces.
        Assert.True(q["OpeningsArea"] > 3);
        Assert.Equal(2 * 6 * 3 + 2 * 0.25 * 3 - q["OpeningsArea"], q["ContactArea"], 6);
        Assert.Equal(5 * 3, q["Ties"]);                                               // ceil(6 / 1,20) × ceil(3 / 1,35)
        Assert.Equal(3, q["Braces"]);                                                 // a push-pull prop every 2,50 m along it

        // Another wall standing against its end: that end is not formed.
        f.Wall(6, -2, 6, 2, t);
        var again = Q(f, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(wall) }))[0]);
        Assert.True(again["ContactArea"] < q["ContactArea"] - 0.7);
    }

    [Fact]
    public void A_masonry_walls_ring_beam_is_formed_on_its_own()
    {
        var f = Site();
        var zones = new JsonArray(WallZones.Defaults("Brick").Select(z => (JsonNode)z.DeepClone()).ToArray());
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Brick 300 with ring beam", ["zones"] = zones,
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.30, ["core"] = true }),
        })!["id"]!;
        var wall = f.Wall(0, 0, 6, 0, type);
        var q = Q(f, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(wall) }))[0]);
        // The ring beam, 0,25 m high at the top of the 3 m wall, both sides.
        Assert.Equal(2 * 6 * 0.25, q["ContactArea"], 6);
        Assert.Equal(2 * 6 * 0.25, q["Area:Zones"], 6);
        Assert.Equal(5, q["Ties"]);
    }

    [Fact]
    public void Footings_are_formed_round_their_steps_and_along_their_sole_and_stem()
    {
        var f = Site();
        var pad = (string)f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 20, ["y"] = 20 })!["id"]!;
        // The default pad: 1,80 × 1,80 × 0,50 under 1,00 × 1,00 × 0,40.
        Assert.Equal(4 * 1.8 * 0.5 + 4 * 1.0 * 0.4, Q(f, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(pad) }))[0])["ContactArea"], 6);
        var strip = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 30, ["x2"] = 6, ["y2"] = 30 })!["id"]!;
        // The default strip: a sole 0,40 m high, a stem 1,40 m high, 6 m long, both sides.
        Assert.Equal(2 * 6 * 0.4 + 2 * 6 * 1.4, Q(f, Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(strip) }))[0])["ContactArea"], 6);
    }

    [Fact]
    public void A_straight_scaffold_takes_the_fewest_bays_and_climbs_until_the_work_top_is_within_reach()
    {
        var f = Site();
        var id = (string)f.Run("sitework.addScaffold", new JsonObject
        {
            ["storeyId"] = StoreyNamed(f, "Ground floor"), ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["height"] = 6,
        })!["id"]!;
        var q = Q(f, id);
        // 10 m: four bays reach 10,28 m (five would end 1 cm out, but one piece more weighs more than 28 cm).
        Assert.Equal(4, q["Bays"]);
        Assert.Equal(10.28, q["Length"], 6);
        // Feet at ±0,00, work top +6,00, reach 1,50: decks at 2, 4 and 6 m, the guardrail to 7 m.
        Assert.Equal(3, q["Lifts"]);
        Assert.Equal(7, q["Height"], 6);
        Assert.Equal(10.28 * 7, q["Area"], 6);
        Assert.Equal(10, q["Standards"]);                                            // five frames' worth, inner and outer
        Assert.Equal(70, q["StandardLength"], 6);
        Assert.Equal(2 * 4 * (3 + 1), q["Ledgers"]);                                 // each deck level and the base, both faces
        Assert.Equal(5 * 4, q["Transoms"]);
        Assert.Equal(2 * 4 * 3 + 2 * 2 * 3, q["Guardrails"]);                        // two rails per bay and level, and across both ends
        Assert.Equal(4 * 3 * 3, q["DeckBoards"]);                                    // 0,73 m wide: three 0,32 m boards
        Assert.Equal(4 * 3 + 2 * 3, q["ToeBoards"]);
        Assert.Equal(3, q["Diagonals"]);                                             // every fifth bay, every lift
        Assert.Equal(10, q["BaseJacks"]);
        Assert.Equal(1, q["AccessBays"]);
        Assert.InRange(q["Anchors"], 3, 4);                                          // on the second lift, no further apart than 4 m
        var g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "decks");
        Assert.Equal(-0.0, f.Bounds(id).Min.Z, 6);
        Assert.Equal(7.0, f.Bounds(id).Max.Z, 2);
    }

    [Fact]
    public void A_scaffold_on_walls_goes_round_their_outer_faces_and_closes_its_corners()
    {
        var f = Site();
        var t = f.Type("Masonry 300", ("Masonry", 0.30, true));
        f.Wall(0, 0, 10, 0, t); f.Wall(10, 0, 10, 6, t); f.Wall(10, 6, 0, 6, t); f.Wall(0, 6, 0, 0, t);
        var walls = f.Model.OfClass(IfcClass.Wall).Select(w => (JsonNode)w.Id).ToArray();
        var r = f.Run("sitework.scaffoldFromWalls", new JsonObject { ["wallIds"] = new JsonArray(walls) })!;
        Assert.True((bool)r["closed"]!);
        var id = (string)r["id"]!;
        var e = f.Model.Get(id)!;
        var layout = Scaffolds.LayoutOf(e, f.Model, f.Session.Catalog)!;
        Assert.Equal(4, layout.Runs.Count);
        // The outer faces: 10,30 × 6,30 m; the inner line 0,25 m out: 10,80 × 6,80; every corner outside, so each run goes on one width (0,73 m).
        Assert.Equal([11.53, 11.53, 7.53, 7.53], layout.Runs.Select(x => Math.Round(x.Length, 6)).OrderByDescending(x => x).ToArray());
        // Every standard stands outside the walls.
        foreach (var run in layout.Runs)
        {
            var p = run.A + run.Dir * 0.1;
            Assert.False(p.X > -0.15 && p.X < 10.15 && p.Y > -0.15 && p.Y < 6.15, $"{p} is inside the walls");
        }
        // Up to the walls' top (+3,00): one lift, the deck at 2 m, the guardrail to 3 m.
        Assert.Equal(3, layout.Top, 6);
        Assert.Equal(1, layout.Lifts);
        // The walls moved: the scaffold follows.
        var w0 = f.Model.OfClass(IfcClass.Wall).First();
        f.Run("architecture.updateWall", new JsonObject { ["id"] = w0.Id, ["height"] = 6 });
        Assert.Equal(6, Scaffolds.LayoutOf(f.Model.Get(id)!, f.Model, f.Session.Catalog)!.Top, 6);
    }

    [Fact]
    public void A_scaffold_stands_on_the_terrain_and_says_when_the_ground_falls_beyond_its_jacks()
    {
        var f = Site();
        f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -20, ["y1"] = -20, ["x2"] = 40, ["y2"] = 40, ["elevation"] = -0.30 });
        var id = (string)f.Run("sitework.addScaffold", new JsonObject
        {
            ["storeyId"] = StoreyNamed(f, "Ground floor"), ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["height"] = 6,
        })!["id"]!;
        var q = Q(f, id);
        // Feet at −0,30, work top +6,00: ceil((6 − 1,5 + 0,3) / 2) = 3 lifts, the guardrail at −0,30 + 6 + 1 = 6,70.
        Assert.Equal(3, q["Lifts"]);
        Assert.Equal(-0.30, f.Bounds(id).Min.Z, 6);
        Assert.Equal(7.0, q["Height"], 6);

        var g = Site();
        var lines = new List<string>();
        for (var i = 0; i <= 8; i++) for (var j = 0; j <= 8; j++) lines.Add($"{i * 5} {j * 5} {100 + 0.1 * i * 5}");
        g.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100 });
        var s = (string)g.Run("sitework.addScaffold", new JsonObject
        {
            ["storeyId"] = StoreyNamed(g, "Ground floor"), ["points"] = new JsonArray(new JsonArray(5, 5), new JsonArray(15, 5)), ["height"] = 6,
        })!["id"]!;
        Assert.Contains(g.Session.Geometry(s)!.Diagnostics!, d => d.Contains("compensating"));
    }

    [Fact]
    public void The_configurators_preview_and_types_change_every_element_of_theirs()
    {
        var f = Site();
        var p = (JsonObject)f.Session.Query("sitework.scaffoldPreview", new JsonObject { ["length"] = 10, ["height"] = 6 })!;
        Assert.Equal(4, ((JsonArray)p["bays"]!).Count);
        Assert.Equal(3, (int)p["lifts"]!);
        var fp = (JsonObject)f.Session.Query("sitework.formworkPreview", new JsonObject { ["kind"] = "slab", ["thickness"] = 0.28, ["height"] = 2.8 })!;
        Assert.Equal(0.40, (double)fp["secondary"]!, 6);                            // the 0,30 m row
        Assert.Equal(1.00, (double)fp["prop"]!, 6);

        var id = (string)f.Run("sitework.addScaffold", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["height"] = 6 })!["id"]!;
        var type = Scaffolds.TypeOf(f.Model, f.Model.Get(id)!)!;
        // Only 2,07 m bays and 1,5 m lifts: 10 m takes five (10,35 m); 6 m up takes ceil(4,5 / 1,5) = 3 lifts.
        f.Run("sitework.updateScaffoldType", new JsonObject { ["id"] = type.Id, ["spec"] = new JsonObject { ["bays"] = new JsonArray(2.07), ["lift"] = 1.5, ["unknown"] = 1 } });
        var q = Q(f, id);
        Assert.Equal(5, q["Bays"]);
        Assert.Equal(10.35, q["Length"], 6);
        Assert.Equal(3, q["Lifts"]);
        Assert.Null(((JsonObject)f.Model.Get(type.Id)!.Attrs["spec"]!)["unknown"]);
    }

    [Fact]
    public void Schedules_list_the_temporary_works_and_IFC_writes_them_as_temporary_assemblies_with_their_resource()
    {
        var f = Site();
        var slab = Floor(f);
        var fw = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab) }))[0];
        f.Run("sitework.addScaffold", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, -1), new JsonArray(6, -1)), ["height"] = 4 });
        var q = new Query(f);
        var formwork = new FormworkSchedule().Grid(q);
        Assert.Equal(3, formwork.Rows.Count);                                        // the floor's, the total, its type's set
        Assert.Equal("28", formwork.Rows[0][6]);
        var scaffold = new ScaffoldSchedule().Grid(q);
        Assert.Equal(2, scaffold.Rows.Count);
        Assert.NotNull(f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "formwork" }));

        var path = Path.Combine(Directory.CreateTempSubdirectory("lapin-sitework").FullName, "site.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Contains("IFCELEMENTASSEMBLY(", text);
        Assert.Contains("'FORMWORK'", text);
        Assert.Contains("'SCAFFOLD'", text);
        Assert.Contains("IFCCONSTRUCTIONPRODUCTRESOURCE(", text);
        Assert.Contains(".FORMWORK.", text);
        Assert.Contains("IFCRELASSIGNSTOPRODUCT(", text);
        Assert.Contains("IFCRELCONNECTSELEMENTS(", text);
        Assert.Contains("'TEMPORARY'", text);
        Assert.Contains("IFCMEMBER(", text);                                          // the props and beams as members of the assembly
        Assert.Contains(fw, f.Model.Entities.Select(e => e.Id));
    }

    [Fact]
    public void A_types_set_is_the_most_cast_at_once_and_what_to_buy_its_set_or_its_total_over_its_reuses()
    {
        var f = Site();
        var a = Floor(f);
        // A second floor, the same, a storey up.
        f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6, ["height"] = 3 });
        var type = f.Model.OfClass(IfcClass.SlabType).First().Id;
        var b = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = StoreyNamed(f, "Second floor"), ["typeId"] = type,
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)),
        })!["id"]!;
        var ids = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(a, b), ["faces"] = new JsonArray("Soffit") }));
        var fwType = Formwork.TypeOf(f.Model, f.Model.Get(ids[0])!)!.Id;
        f.Run("sitework.updateFormworkType", new JsonObject { ["id"] = fwType, ["spec"] = new JsonObject { ["article"] = "CB01", ["articleText"] = "Cofraje pentru plăci", ["reuses"] = 4 } });
        var grid = new FormworkSchedule().Grid(new Query(f));
        Assert.Equal("CB01 Cofraje pentru plăci", grid.Rows[0][3]);
        var sum = grid.Rows[^1];
        Assert.Equal("set by storey", sum[1]);
        Assert.Equal("48", sum[6]);                                                    // 24 + 24
        Assert.Equal("24", sum[8]);                                                    // one storey at a time
        Assert.Equal("24", sum[9]);                                                    // the set, more than 48 / 4
    }

    sealed class Query(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
