using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The joinery's drawings in the configurator, the plans and the elevations
/// (docs/plan-views-and-joinery.md §2, pending decision 126).
/// </summary>
public class JoineryDrawingTests
{
    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static JsonObject Q(Fixture f, string name, JsonObject args) => (JsonObject)f.Session.Query(name, args)!;

    /// <summary>The apex of the first opening triangle of a drawing (an open stroke of three points).</summary>
    static double ApexX(JsonObject drawing) => (double)drawing["strokes"]!.AsArray().First(s => s!["pts"]!.AsArray().Count == 3 && !(bool)s["closed"]!)!["pts"]![1]![0]!;

    static List<(string Layer, double X0, double Y0, double X1, double Y1, bool Dashed)> Segments(JsonObject d, string layer)
    {
        var o = new List<(string, double, double, double, double, bool)>();
        foreach (var p in d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == layer))
        {
            if (p["segments"] is not JsonArray a) continue;
            var dashed = p["dash"] is JsonArray { Count: > 0 };
            for (var i = 0; i + 3 < a.Count; i += 4) o.Add((layer, (double)a[i]!, (double)a[i + 1]!, (double)a[i + 2]!, (double)a[i + 3]!, dashed));
        }
        return o;
    }

    [Fact]
    public void The_preview_draws_the_elevation_the_plan_and_the_section_and_follows_the_projects_convention()
    {
        var f = Full();
        var spec = JoinerySpec.DefaultWindow().ToJson();
        var p = Q(f, "architecture.joineryPreview", new JsonObject { ["spec"] = spec.DeepClone() });
        Assert.Null((string?)p["problem"]);
        var drawings = (JsonObject)p["drawings"]!;
        foreach (var key in new[] { "elevation", "exterior", "plan", "section" }) Assert.NotEmpty(drawings[key]!["strokes"]!.AsArray());
        Assert.Equal("handle", (string?)p["symbols"]!["convention"]);
        var makers = ApexX((JsonObject)drawings["elevation"]!);
        // From outside the drawing is mirrored.
        Assert.Equal(-makers, ApexX((JsonObject)drawings["exterior"]!), 9);
        f.Run("architecture.joinerySymbols", new JsonObject { ["convention"] = "hinge", ["hardware"] = true });
        var din = Q(f, "architecture.joineryPreview", new JsonObject { ["spec"] = spec.DeepClone() });
        Assert.Equal("hinge", (string?)din["symbols"]!["convention"]);
        Assert.True((bool)din["symbols"]!["hardware"]!);
        Assert.NotEqual(makers, ApexX((JsonObject)din["drawings"]!["elevation"]!));
        Assert.Throws<CommandException>(() => f.Run("architecture.joinerySymbols", new JsonObject { ["convention"] = "sideways" }));
    }

    [Fact]
    public void A_door_and_a_window_draw_the_symbol_engines_plan_in_the_model()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        var dp = f.Session.Geometry(door)!.Plan!;
        var wp = f.Session.Geometry(window)!.Plan!;
        // The door: the frame cut, its leaf (heavy) and the arc of its swing (a polyline of many points).
        Assert.Contains(dp, i => i.Kind == "heavy" && i.Closed);
        Assert.Contains(dp, i => i.Kind == "symbol" && i.Points.Count > 8);
        // The window: the frame cut and the glass (thin, drawn as a symbol); the jambs across the wall stay.
        Assert.Contains(wp, i => i.Kind == "heavy" && i.Closed);
        Assert.True(wp.Count(i => i.Kind == "symbol") >= 3);
        Assert.Single(wp, i => i.Kind == "hit");
    }

    [Fact]
    public void An_elevation_marks_the_openings_whose_exterior_faces_it_dashed_and_leaves_the_hidden_ones()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        // The exterior is the wall's right: south. The south elevation (looking north) sees it; the north one does not.
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var north = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })!["id"]!;
        var marks = Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY");
        Assert.NotEmpty(marks);
        Assert.All(marks, s => Assert.True(s.Dashed));
        // They sit on the window: within its width and height in the drawing.
        var spec = Openings.SpecOf(f.Model, window)!;
        Assert.All(marks, s => Assert.True(s.Y0 >= spec.SillHeight - 1e-6 && s.Y1 <= spec.SillHeight + spec.Height + 1e-6, $"a mark at {s.Y0}..{s.Y1}"));
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = north }), "JOINERY"));
        // Switched off on the view.
        f.Run("element.update", new JsonObject { ["id"] = south, ["attrs"] = new JsonObject { ["openingSymbols"] = "hide" } });
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
        f.Run("element.update", new JsonObject { ["id"] = south, ["attrs"] = new JsonObject { ["openingSymbols"] = "show" } });
        Assert.NotEmpty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
        // A wall in front of the window hides its symbol.
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = -3, ["x2"] = 6, ["y2"] = -3, ["height"] = 4 });
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
    }

    [Fact]
    public void A_flipped_window_turns_its_exterior_to_the_other_elevation()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        f.Run("architecture.flipJoinery", new JsonObject { ["id"] = window, ["axis"] = "y" });
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var north = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })!["id"]!;
        Assert.Empty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = south }), "JOINERY"));
        Assert.NotEmpty(Segments(Q(f, "view.drawing", new JsonObject { ["id"] = north }), "JOINERY"));
    }

    [Fact]
    public void A_two_leaf_sliding_window_places_its_leaves_on_distinct_planes()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        var spec = Openings.SpecOf(f.Model, window)! with
        {
            Columns = [0.5, 0.5],
            Rows = [1.0],
            Panels = [new(0, 0, "sliding-left"), new(0, 1, "sliding-right")],
        };
        var glassY = Joinery.Boxes(spec).Where(b => b.Role == "glass").Select(b => Math.Round((b.Min.Y + b.Max.Y) / 2, 4)).Distinct().OrderBy(y => y).ToList();
        Assert.Equal(2, glassY.Count);
        Assert.True(glassY[1] - glassY[0] > 0.02);
        var plan = JoinerySymbols.Plan(spec);
        Assert.NotEmpty(plan);
    }

    [Fact]
    public void Joinery_can_be_shown_open_or_closed_in_plan_and_3d()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;

        double LeafDepth3d()
        {
            var g = f.Session.Geometry(door)!;
            var leaf = g.Parts.First(p => p.Key == "leaf");
            var ys = leaf.Solid.Vertices.Select(v => v.Y).ToList();
            return ys.Max() - ys.Min();
        }

        // Default: closed in 3D (thin leaf across wall), open 90° in plan (leaf box + swing).
        Assert.True(LeafDepth3d() < 0.15);

        // Open: 3D leaf swings 90° into the room (> 0.5 m depth).
        f.Run("architecture.setJoineryOpen", new JsonObject { ["ids"] = new JsonArray(door), ["mode"] = "open" });
        Assert.True(LeafDepth3d() > 0.5);

        // Closed: 3D leaf is back in the frame, and plan has no swing arc (no open leaf stroke extending past wall).
        f.Run("architecture.setJoineryOpen", new JsonObject { ["ids"] = new JsonArray(door), ["mode"] = "closed" });
        Assert.True(LeafDepth3d() < 0.15);
        var closedPlan = f.Session.Geometry(door)!.Plan!;
        Assert.All(closedPlan.Where(p => p.Kind != "hit"), p => Assert.All(p.Points, pt => Assert.True(Math.Abs(pt.Y) < 0.25)));
    }

    // ─── The sills in plan (GitHub issue #153, the tester's request 18) ───

    static readonly JoineryDetails Sills = new(
        SillInside: new JoinerySill(true, 0.04, 0.03, 0.02, 0, "Timber"),
        SillOutside: new JoinerySill(true, 0.05, 0.02, 0.03, 10, "Granite"));

    static string WindowOf(Fixture f, string wall, double along, JoinerySpec spec)
    {
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along, ["typeId"] = type })!["id"]!;
    }

    /// <summary>The plan's items a view at <paramref name="level"/> draws, but the hit area.</summary>
    static List<PlanItem> At(IReadOnlyList<PlanItem> plan, int level) => plan.Where(i => i.Kind != "hit" && i.MinDetail <= level && i.MaxDetail >= level).ToList();

    static string Print(IEnumerable<PlanItem> plan) => string.Join("\n", plan.Select(i =>
        $"{i.Kind} {i.Closed} {i.MinDetail}-{i.MaxDetail} " + string.Join(" ", i.Points.Select(p => FormattableString.Invariant($"{p.X:0.####},{p.Y:0.####}")))));

    [Fact]
    public void A_windows_detailed_sills_are_drawn_in_plan_seen_under_the_cut_past_the_walls_faces()
    {
        var f = Full();
        // A wall along X, 300 thick (its faces at y ±0.15), the window 1200 wide at x 3.
        var wall = f.Wall(0, 0, 6, 0);
        var spec = JoinerySpec.DefaultWindow("Dressed") with { Details = Sills };
        var w = WindowOf(f, wall, 3, spec);
        var plan = f.Session.Geometry(w)!.Plan!;
        var sills = WindowSills.Of(f.Model, f.Model.Get(w)!);
        static (double X0, double X1, double Y0, double Y1) Bounds(IEnumerable<Lapin.Geometry.Vec2> pts) =>
            (pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.Y));
        bool Is(PlanItem i, WindowSills.Sill s)
        {
            var (a, b) = (Bounds(i.Points), Bounds(s.Points.Select(p => p.Xy)));
            return Math.Abs(a.X0 - b.X0) < 1e-6 && Math.Abs(a.X1 - b.X1) < 1e-6 && Math.Abs(a.Y0 - b.Y0) < 1e-6 && Math.Abs(a.Y1 - b.Y1) < 1e-6;
        }
        var fine = At(plan, PlanDetail.Fine);
        // Each sill an outline seen (the symbol pen) where its body stands: the interior one 40 mm into the room with 20 mm
        // past each jamb, the exterior one 50 mm past the facade with 30 mm past each jamb.
        var inside = Assert.Single(fine, i => i.Closed && i.Kind == "symbol" && Is(i, sills[0]));
        var outside = Assert.Single(fine, i => i.Closed && i.Kind == "symbol" && Is(i, sills[1]));
        Assert.Equal(1.2 + 0.04, Bounds(inside.Points).X1 - Bounds(inside.Points).X0, 6);
        Assert.Equal(1.2 + 0.06, Bounds(outside.Points).X1 - Bounds(outside.Points).X0, 6);
        // Within the wall it is the opening's width (the overhang is let into the jambs, under the wall's cut): the outline steps
        // in at the wall's face, so only its front edge is the sill's whole length.
        Assert.Equal(8, inside.Points.Count);
        Assert.Equal(8, outside.Points.Count);
        Assert.Equal(4, inside.Points.Count(p => Math.Abs(Math.Abs(p.X - 3) - 0.6) < 1e-6));
        // The plain boards they replace are not drawn (the old plan drew the boards under the detailed sills).
        Assert.DoesNotContain(fine, i => i.Closed && i.Kind == "symbol" && Math.Abs(Bounds(i.Points).X1 - Bounds(i.Points).X0 - 1.2) < 1e-6);
        // LOD 200 leaves the sills out (decisions 404, 405: sills are details from LOD 300) …
        Assert.DoesNotContain(At(plan, PlanDetail.Medium), i => sills.Any(s => Is(i, s)));
        // … unless the type asks for them (Sills drawn in plan): then from LOD 200 up, LOD 100 still the opening alone.
        var shown = WindowOf(f, wall, 1, spec with { Name = "Shown", SillsInPlan = true });
        var ss = WindowSills.Of(f.Model, f.Model.Get(shown)!);
        var sp = f.Session.Geometry(shown)!.Plan!;
        Assert.Equal(2, At(sp, PlanDetail.Medium).Count(i => ss.Any(s => Is(i, s))));
        Assert.Equal(2, At(sp, PlanDetail.Fine).Count(i => ss.Any(s => Is(i, s))));
        Assert.DoesNotContain(At(sp, PlanDetail.Coarse), i => ss.Any(s => Is(i, s)));
        // The configurator's plan draws them too: the interior sill 40 mm past a 300 mm wall's face, the exterior 50 mm.
        var preview = Q(f, "architecture.joineryPreview", new JsonObject { ["spec"] = spec.ToJson(), ["wall"] = 0.3 });
        var ys = preview["drawings"]!["plan"]!["strokes"]!.AsArray().SelectMany(st => st!["pts"]!.AsArray().Select(q => (double)q![1]!)).ToList();
        Assert.Contains(ys, y => Math.Abs(y + 0.19) < 1e-6);
        Assert.Contains(ys, y => Math.Abs(y - 0.2) < 1e-6);
    }

    [Fact]
    public void A_window_with_plain_boards_keeps_its_plan_and_one_without_sills_draws_none()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 6, 0);
        var plain = WindowOf(f, wall, 2, JoinerySpec.DefaultWindow("Plain"));
        // The plan of a window with the type's plain boards, as before (fingerprint taken before issue #153).
        Assert.Equal(PlainPlan, Print(f.Session.Geometry(plain)!.Plan!));
        // Asked for, the plain boards are drawn at LOD 200 too.
        var shown = WindowOf(f, wall, 4.5, JoinerySpec.DefaultWindow("Plain shown") with { SillsInPlan = true });
        var boards = At(f.Session.Geometry(shown)!.Plan!, PlanDetail.Medium).Where(i => i.Closed && i.Kind == "symbol" && i.Points.Max(p => p.X) - i.Points.Min(p => p.X) > 1.19).ToList();
        Assert.Equal(2, boards.Count);
        // No boards, no sills: nothing of a sill in plan, asked for or not.
        var f2 = Full();
        var wall2 = f2.Wall(0, 0, 6, 0);
        var bare = WindowOf(f2, wall2, 3, JoinerySpec.DefaultWindow("Bare") with { Board = new JoineryBoard(0, 0), SillsInPlan = true });
        Assert.DoesNotContain(f2.Session.Geometry(bare)!.Plan!, i => i.Kind != "hit" && i.Closed && i.Points.Max(q => q.X) - i.Points.Min(q => q.X) > 1.19);
    }

    const string PlainPlan = """
        hit True 0-2 1.4,0.14 2.6,0.14 2.6,-0.145 1.4,-0.145
        symbol False 0-2 1.4,0.14 1.4,-0.145
        symbol False 0-2 2.6,0.14 2.6,-0.145
        symbol False 0-0 1.4,-0.0025 2.6,-0.0025
        heavy True 1-1 1.4,0.0325 1.46,0.0325 1.46,-0.0375 1.4,-0.0375
        heavy True 1-1 2.54,0.0325 2.6,0.0325 2.6,-0.0375 2.54,-0.0375
        heavy True 1-1 1.96,0.0325 2.04,0.0325 2.04,-0.0375 1.96,-0.0375
        symbol False 1-1 1.53,-0.0025 1.89,-0.0025
        symbol False 1-1 2.11,-0.0025 2.47,-0.0025
        heavy True 2-2 1.4,0.0325 1.46,0.0325 1.46,-0.0375 1.4,-0.0375
        heavy True 2-2 2.54,0.0325 2.6,0.0325 2.6,-0.0375 2.54,-0.0375
        heavy True 2-2 1.96,0.0325 2.04,0.0325 2.04,-0.0375 1.96,-0.0375
        heavy True 2-2 1.46,0.0375 1.53,0.0375 1.53,-0.0425 1.46,-0.0425
        heavy True 2-2 1.89,0.0375 1.96,0.0375 1.96,-0.0425 1.89,-0.0425
        symbol True 2-2 1.53,0.0095 1.89,0.0095 1.89,-0.0145 1.53,-0.0145
        heavy True 2-2 2.04,0.0375 2.11,0.0375 2.11,-0.0425 2.04,-0.0425
        heavy True 2-2 2.47,0.0375 2.54,0.0375 2.54,-0.0425 2.47,-0.0425
        symbol True 2-2 2.11,0.0095 2.47,0.0095 2.47,-0.0145 2.11,-0.0145
        symbol True 2-2 1.4,0.2325 2.6,0.2325 2.6,0.0325 1.4,0.0325
        symbol True 2-2 1.4,-0.0375 2.6,-0.0375 2.6,-0.0875 1.4,-0.0875
        """;
}
