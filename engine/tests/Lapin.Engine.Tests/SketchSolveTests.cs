using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Xunit.Abstractions;
using Cad = Lapin.Drafting.Cad;

namespace Lapin.Engine.Tests;

/// <summary>
/// Constrained sketches solve, stage C1 of decision 520 (docs/plan-studio-parametric.md §4.3): a sketch's stored constraints
/// (C0) become the system of Lapin's own solver (<c>Lapin.Geometry/Constraints</c>) inside every command that changes them or
/// their shapes, in the same undo step; a user's edit is a drag goal; conflicts are refused; the status is derived on every ask.
/// </summary>
public sealed class SketchSolveTests(ITestOutputHelper output)
{
    const double Eps = 1e-7;

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static string Draw(Fixture f, string view, JsonObject shape) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;

    static string Line(Fixture f, string view, double x1, double y1, double x2, double y2) =>
        Draw(f, view, new JsonObject { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) });

    static string Poly(Fixture f, string view, bool closed, params (double X, double Y)[] pts) =>
        Draw(f, view, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(pts.Select(p => (JsonNode)J(p.X, p.Y)).ToArray()), ["closed"] = closed });

    static string CircleAt(Fixture f, string view, double x, double y, double r) =>
        Draw(f, view, new JsonObject { ["type"] = "circle", ["c"] = J(x, y), ["r"] = r });

    static string ArcAt(Fixture f, string view, double x, double y, double r, double start, double end) =>
        Draw(f, view, new JsonObject { ["type"] = "arc", ["c"] = J(x, y), ["r"] = r, ["start"] = start, ["end"] = end });

    static JsonObject R(string shape, string part = "whole") => new() { ["shape"] = shape, ["part"] = part };

    static JsonObject C(string kind, params JsonObject[] refs) => new() { ["kind"] = kind, ["refs"] = new JsonArray(refs.Select(r => (JsonNode)r).ToArray()) };

    static JsonObject C(string kind, double value, params JsonObject[] refs)
    {
        var c = C(kind, refs);
        c["value"] = value;
        return c;
    }

    static string Add(Fixture f, JsonObject c) => (string)f.Run("studio.addConstraint", (JsonObject)c.DeepClone())!["id"]!;

    /// <summary>Several constraints in one command (one undo step, one solve).</summary>
    static string[] AddAll(Fixture f, params JsonObject[] cs) =>
        f.Run("studio.addConstraint", new JsonObject { ["constraints"] = new JsonArray(cs.Select(c => (JsonNode)c.DeepClone()).ToArray()) })!["ids"]!
            .AsArray().Select(x => (string)x!).ToArray();

    static Shape S(Fixture f, string id) => Drafts.ShapeOf(f.Model.Get(id)!)!;
    static Cad.Line L(Fixture f, string id) => (Cad.Line)S(f, id);

    static void Near(Vec2 expected, Vec2 actual, double eps = Eps) =>
        Assert.True((expected - actual).Length <= eps, $"expected ({expected.X}, {expected.Y}), got ({actual.X}, {actual.Y})");

    static void NearLine(Cad.Line l, double x1, double y1, double x2, double y2)
    {
        Near(new Vec2(x1, y1), l.A);
        Near(new Vec2(x2, y2), l.B);
    }

    static JsonObject Status(Fixture f, string view) => (JsonObject)f.Session.Query("studio.sketchStatus", new JsonObject { ["viewId"] = view })!;

    static int Count(Fixture f, string view) =>
        f.Session.Query("studio.constraints", new JsonObject { ["viewId"] = view })!["constraints"]!.AsArray().Count;

    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    /// <summary>Four lines, roughly a rectangle, their corners not quite meeting.</summary>
    static (string B, string Rt, string T, string Lf) Rough(Fixture f, string plan) =>
        (Line(f, plan, 0, 0, 2.1, 0.05), Line(f, plan, 2, 0.1, 1.9, 1), Line(f, plan, 2, 1.1, 0.1, 0.9), Line(f, plan, 0, 1, 0.05, 0.1));

    static JsonObject[] Corners(string b, string r, string t, string l) =>
    [
        C("coincident", R(b, "end"), R(r, "start")), C("coincident", R(r, "end"), R(t, "start")),
        C("coincident", R(t, "end"), R(l, "start")), C("coincident", R(l, "end"), R(b, "start")),
    ];

    static JsonObject[] Square(string b, string r, string t, string l) =>
        [C("horizontal", R(b)), C("vertical", R(r)), C("horizontal", R(t)), C("vertical", R(l))];

    // ------------------------------------------------------------ translation

    [Fact]
    public void A_rough_rectangle_solves_to_its_dimensions_and_only_coincidences_join_shapes()
    {
        var f = F();
        var plan = Plan(f);
        var (b, r, t, l) = Rough(f, plan);
        // A line starting where the top starts, with no coincidence: not part of the sketch's system, so it stays.
        var loose = Line(f, plan, 2, 1.1, 3, 1.1);
        AddAll(f, [.. Corners(b, r, t, l), .. Square(b, r, t, l), C("fix", R(b, "start")), C("distanceX", 3, R(b)), C("distanceY", 2, R(r))]);
        NearLine(L(f, b), 0, 0, 3, 0);
        NearLine(L(f, r), 3, 0, 3, 2);
        NearLine(L(f, t), 3, 2, 0, 2);
        NearLine(L(f, l), 0, 2, 0, 0);
        NearLine(L(f, loose), 2, 1.1, 3, 1.1);

        // The status: the rectangle fully constrained; the loose line's four degrees of freedom are the sketch's only ones.
        var st = Status(f, plan);
        Assert.Equal("underconstrained", (string)st["status"]!);
        Assert.Equal(4, (int)st["dof"]!);
        Assert.All(st["freeShapes"]!.AsArray(), x => Assert.Equal(loose, (string)x!["shape"]!));
        Assert.Empty(st["redundant"]!.AsArray());
        Assert.Empty(st["conflicting"]!.AsArray());
        var dims = st["dimensions"]!.AsArray();
        Assert.Equal(2, dims.Count);
        Assert.All(dims, d => Assert.Equal((double)d!["value"]!, (double)d["measured"]!, 9));

        // Deleting the loose line: the rectangle alone, solved.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(loose) });
        st = Status(f, plan);
        Assert.Equal("solved", (string)st["status"]!);
        Assert.Equal(0, (int)st["dof"]!);
        Assert.Empty(st["freeShapes"]!.AsArray());
    }

    [Fact]
    public void Every_stored_kind_and_pattern_reaches_the_solver()
    {
        var f = F();
        var plan = Plan(f);
        var p = Poly(f, plan, false, (0, 0), (2, 0), (2, 1));
        var a = ArcAt(f, plan, 3.1, 1.6, 0.5, Math.PI, 2 * Math.PI);
        var c = CircleAt(f, plan, 6, 0, 1);
        var tl = Line(f, plan, 4, -1, 8, -1.1);
        var m1 = Line(f, plan, 4, 3, 4.2, 4);
        var m2 = Line(f, plan, 5, -3, 5.5, -4);
        var g = Line(f, plan, 0, 5, 1, 5.2);
        var h = Line(f, plan, 3, 5, 3.1, 6);
        var k = Line(f, plan, 6.2, 1.5, 8, 2);
        var driven = C("diameter", R(c));
        driven["driving"] = false;
        var ids = AddAll(f,
            C("fix", R(p, "vertex:0")), C("horizontal", R(p, "edge:0")), C("perpendicular", R(p, "edge:0"), R(p, "edge:1")),
            C("distance", 2.5, R(p, "edge:0")), C("distance", 1.5, R(p, "edge:1")),
            C("tangent", R(p, "end"), R(a, "start")), C("radius", 0.5, R(a)),
            C("fix", R(c, "center")), C("equal", R(a), R(c)), driven,
            C("tangent", R(tl), R(c)), C("parallel", R(tl), R(p, "edge:0")),
            C("symmetric", R(m1, "start"), R(m2, "start"), R(p, "edge:0")), C("midpoint", R(m2, "end"), R(p, "edge:0")), C("vertical", R(m1, "start"), R(m1, "end")),
            C("angle", 30, R(g)), C("distanceX", 0, R(g, "start")), C("distanceY", 5, R(g, "start")),
            C("angle", 60, R(g), R(h)), C("distance", 5, R(h, "start"), R(p, "edge:0")),
            C("pointOn", R(k, "start"), R(c)), C("horizontal", R(k, "start"), R(k, "end")), C("distance", 2, R(k, "start"), R(k, "end")));

        var pl = (Cad.Polyline)S(f, p);
        Near(new(0, 0), pl.Pts[0]); Near(new(2.5, 0), pl.Pts[1]); Near(new(2.5, 1.5), pl.Pts[2]);
        // Tangent where they join: the arc starts at the polyline's end, its centre square off the last edge; still counter-clockwise.
        var arc = (Cad.Arc)S(f, a);
        Near(new(2.5, 1.5), arc.At(arc.Start));
        Near(new(3, 1.5), arc.C);
        Assert.Equal(0.5, arc.R, 7);
        Assert.InRange(arc.Sweep, 0.5, 2 * Math.PI - 0.5);
        var ci = (Cad.Circle)S(f, c);
        Near(new(6, 0), ci.C);
        Assert.Equal(0.5, ci.R, 7);
        // A line tangent to the circle, parallel to the base: on the side it started.
        Assert.Equal(-0.5, L(f, tl).A.Y, 7);
        Assert.Equal(-0.5, L(f, tl).B.Y, 7);
        var (s1, s2) = (L(f, m1), L(f, m2));
        Near(new(s1.A.X, -s1.A.Y), s2.A);
        Near(new(1.25, 0), s2.B);
        Assert.Equal(s1.A.X, s1.B.X, 7);
        var gl = L(f, g);
        Near(new(0, 5), gl.A);
        Assert.Equal(30, Math.Atan2(gl.B.Y - gl.A.Y, gl.B.X - gl.A.X) * 180 / Math.PI, 6);
        var hl = L(f, h);
        Assert.Equal(90, Math.Atan2(hl.B.Y - hl.A.Y, hl.B.X - hl.A.X) * 180 / Math.PI, 6);
        Assert.Equal(5, hl.A.Y, 7);
        var kl = L(f, k);
        Assert.Equal(0.5, (kl.A - new Vec2(6, 0)).Length, 7);
        Assert.Equal(kl.A.Y, kl.B.Y, 7);
        Assert.Equal(2, (kl.B - kl.A).Length, 7);

        // Every dimension measures its value; the driven one what the shapes make (a diameter of 1).
        var st = Status(f, plan);
        Assert.NotEqual("conflicting", (string)st["status"]!);
        foreach (var d in st["dimensions"]!.AsArray())
        {
            if ((bool)d!["driving"]!) Assert.Equal((double)d["value"]!, (double)d["measured"]!, 6);
            else Assert.Equal(1, (double)d["measured"]!, 6);
        }
        Assert.Contains(st["dimensions"]!.AsArray(), d => (string)d!["id"]! == ids[9] && !(bool)d["driving"]!);
    }

    [Fact]
    public void A_formula_is_worked_out_with_the_part_s_parameters()
    {
        var f = F();
        var plan = Plan(f);
        var sq = Poly(f, plan, true, (0, 0), (1, 0), (1, 1), (0, 1));
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = sq, ["depth"] = 2 })!["id"]!;
        var type = (string)f.Run("studio.setType", new JsonObject { ["ids"] = Ids(solid), ["name"] = "Pillar" })!["id"]!;
        f.Run("studio.updatePart", new JsonObject
        {
            ["id"] = type,
            ["def"] = new JsonObject
            {
                ["params"] = new JsonArray(new JsonObject { ["key"] = "W", ["default"] = 0.4 }),
                ["bodies"] = new JsonArray(new JsonObject { ["plane"] = "xy", ["points"] = new JsonArray(J(0, 0), J(1, 0), J(1, 1)), ["depth"] = 1 }),
            },
        });
        var w = C("distanceX", R(sq, "edge:0"));
        w["formula"] = "W / 2";
        AddAll(f, C("fix", R(sq, "vertex:0")), C("horizontal", R(sq, "edge:0")), w);
        var pl = (Cad.Polyline)S(f, sq);
        Near(new(0, 0), pl.Pts[0]);
        Near(new(0.2, 0), pl.Pts[1]);
    }

    // ------------------------------------------------------------ refusals

    [Fact]
    public void A_pattern_the_solver_has_no_equation_for_is_refused_when_added()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 1, 0);
        var b = Line(f, plan, 1, 0, 2, 0);
        var sq = Poly(f, plan, true, (5, 5), (6, 5), (6, 6), (5, 6));
        var open = Poly(f, plan, false, (3, 2), (2, 2), (2, 1));
        var arc = ArcAt(f, plan, 1, 1, 1, -Math.PI / 2, 0);   // from (1, 0) to (2, 1)
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.addConstraint", C("tangent", R(a, "end"), R(b, "start"))));
        Assert.Contains("needs an arc", ex.Message);
        ex = Assert.Throws<CommandException>(() => f.Run("studio.addConstraint", C("tangent", R(sq, "vertex:1"), R(arc, "start"))));
        Assert.Contains("corner", ex.Message);
        ex = Assert.Throws<CommandException>(() => f.Run("studio.addConstraint", C("tangent", R(open, "vertex:1"), R(arc, "start"))));
        Assert.Contains("corner", ex.Message);
        Assert.Empty(f.Model.OfClass(SketchConstraints.Class));
        // An open polyline's end and a line's end to an arc's are fine.
        Add(f, C("tangent", R(a, "end"), R(arc, "start")));
        Add(f, C("tangent", R(open, "end"), R(arc, "end")));
    }

    [Fact]
    public void A_conflicting_constraint_is_refused_naming_what_it_conflicts_with_and_nothing_is_kept()
    {
        var f = F();
        var plan = Plan(f);
        var (b, r, t, l) = Rough(f, plan);
        var ids = AddAll(f, [.. Corners(b, r, t, l), .. Square(b, r, t, l), C("fix", R(b, "start")), C("distance", 2, R(b))]);
        var width = ids[^1];
        var before = f.Model.OfClass(SketchConstraints.Class).Single().Attrs["constraints"]!.ToJsonString();
        var shapes = new[] { b, r, t, l }.Select(id => S(f, id)).ToArray();
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.addConstraint", C("distance", 3, R(t))));
        Assert.Contains("conflict", ex.Message);
        Assert.Contains($"distance {width}", ex.Message);
        Assert.Equal(before, f.Model.OfClass(SketchConstraints.Class).Single().Attrs["constraints"]!.ToJsonString());
        Assert.Equal(shapes, new[] { b, r, t, l }.Select(id => S(f, id)).ToArray());
        // A change that would conflict is refused too.
        Add(f, C("distance", 1, R(r)));
        Add(f, C("equal", R(r), R(l)));
        var redundant = Add(f, C("parallel", R(b), R(t)));
        Assert.Equal("redundant", (string)Status(f, plan)["status"]!);
        Assert.Contains(redundant, Status(f, plan)["redundant"]!.AsArray().Select(x => (string)x!));
        var top = C("distance", R(t));
        top["driving"] = false;
        var reference = Add(f, top);
        Assert.Equal(2, (double)Status(f, plan)["dimensions"]!.AsArray().Single(d => (string)d!["id"]! == reference)!["measured"]!, 9);
        ex = Assert.Throws<CommandException>(() => f.Run("studio.updateConstraint", new JsonObject { ["id"] = reference, ["value"] = 3, ["driving"] = true }));
        Assert.Contains("conflict", ex.Message);
        Assert.False((bool)Status(f, plan)["dimensions"]!.AsArray().Single(d => (string)d!["id"]! == reference)!["driving"]!);
        NearLine(L(f, r), 2, 0, 2, 1);
    }

    [Fact]
    public void A_sketch_saved_in_conflict_refuses_edits_but_deletes_and_removals_always_go_through()
    {
        var f = F();
        var plan = Plan(f);
        var (b, r, t, l) = Rough(f, plan);
        var extra = Line(f, plan, 5, 5, 6, 5);
        var ids = AddAll(f, [.. Corners(b, r, t, l), .. Square(b, r, t, l), C("fix", R(b, "start")), C("distance", 2, R(b)), C("horizontal", R(extra))]);
        // As an older build (stage C0, no solve) could have saved it: the top 3 m long while the base is 2.
        var carrier = f.Model.OfClass(SketchConstraints.Class).Single();
        var attrs = carrier.CloneAttrs();
        attrs["constraints"]!.AsArray().Add((JsonNode)new JsonObject { ["id"] = "old", ["kind"] = "distance", ["refs"] = new JsonArray(R(t)), ["value"] = 3, ["driving"] = true });
        f.Session.Edit("Older build", e => e.Replace(carrier.With(attrs: attrs)));
        Assert.Equal("conflicting", (string)Status(f, plan)["status"]!);
        Assert.Contains("old", Status(f, plan)["conflicting"]!.AsArray().Select(x => (string)x!));

        // An edit of its shapes is refused.
        Assert.Throws<CommandException>(() => f.Run("drafting.updateDraft", new JsonObject { ["id"] = extra, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(5, 6), ["b"] = J(6, 6) } }));
        // A delete goes through and says the sketch is still in conflict.
        var res = f.Run("element.delete", new JsonObject { ["ids"] = Ids(extra) })!;
        Assert.Equal("conflicting", (string)res["sketch"]!["status"]!);
        Assert.Null(f.Model.Get(extra));
        // Removing the constraint at fault always works; the sketch solves again on the next change.
        f.Run("studio.removeConstraint", new JsonObject { ["id"] = "old" });
        Assert.NotEqual("conflicting", (string)Status(f, plan)["status"]!);
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = ids[^2], ["value"] = 2.5 });
        NearLine(L(f, b), 0, 0, 2.5, 0);
    }

    // ------------------------------------------------------------ solids follow, one undo step

    [Fact]
    public void An_extrude_of_a_constrained_rectangle_follows_a_driving_distance_in_one_undo_step()
    {
        var f = F();
        var plan = Plan(f);
        var rect = Poly(f, plan, true, (0, 0), (2, 0), (2, 1), (0, 1));
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = rect, ["depth"] = 1 })!["id"]!;
        var ids = AddAll(f, C("fix", R(rect, "vertex:0")), C("horizontal", R(rect, "edge:0")), C("vertical", R(rect, "edge:1")),
            C("horizontal", R(rect, "edge:2")), C("vertical", R(rect, "edge:3")), C("distanceX", 2, R(rect, "edge:0")), C("distanceY", 1, R(rect, "edge:1")));
        Assert.Equal(2, Vol(f, solid), 6);
        var width = ids[5];
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = width, ["value"] = 3 });
        Assert.Equal(3, Vol(f, solid), 6);
        var pl = (Cad.Polyline)S(f, rect);
        Near(new(3, 0), pl.Pts[1]); Near(new(3, 1), pl.Pts[2]);
        // The part's tree: the sketch fully constrained.
        var sketch = f.Session.Query("studio.partTree", new JsonObject { ["id"] = solid })!["sketches"]!.AsArray().Single()!;
        Assert.Equal("solved", (string)sketch["status"]!);
        Assert.Equal(0, (int)sketch["dof"]!);
        // One undo step brings back the value and the body.
        f.Session.Undo();
        Assert.Equal(2, Vol(f, solid), 6);
        Assert.Equal(2, (double)f.Session.Query("studio.constraints", new JsonObject { ["viewId"] = plan })!["constraints"]!.AsArray().Single(c => (string)c!["id"]! == width)!["value"]!);
        Near(new(2, 1), ((Cad.Polyline)S(f, rect)).Pts[2]);
        f.Session.Redo();
        Assert.Equal(3, Vol(f, solid), 6);
    }

    // ------------------------------------------------------------ user edits are drag goals

    /// <summary>A 2 × 1 rectangle of four lines, its bottom-left corner fixed and its width held at 2 m; its height free.</summary>
    static (string B, string Rt, string T, string Lf, string Width) Held(Fixture f, string plan)
    {
        var b = Line(f, plan, 0, 0, 2, 0);
        var r = Line(f, plan, 2, 0, 2, 1);
        var t = Line(f, plan, 2, 1, 0, 1);
        var l = Line(f, plan, 0, 1, 0, 0);
        var ids = AddAll(f, [.. Corners(b, r, t, l), .. Square(b, r, t, l), C("fix", R(b, "start")), C("distance", 2, R(b))]);
        return (b, r, t, l, ids[^1]);
    }

    [Fact]
    public void A_grip_edit_wins_where_the_constraints_allow_and_the_rest_follows_else_it_is_refused()
    {
        var f = F();
        var plan = Plan(f);
        var (b, r, t, l, width) = Held(f, plan);
        Assert.Equal(1, (int)Status(f, plan)["dof"]!);
        // The right side's top dragged up: the height is free, so the top and the left side follow.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = r, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(2, 0), ["b"] = J(2, 1.5) } });
        NearLine(L(f, r), 2, 0, 2, 1.5);
        NearLine(L(f, t), 2, 1.5, 0, 1.5);
        NearLine(L(f, l), 0, 1.5, 0, 0);
        NearLine(L(f, b), 0, 0, 2, 0);
        // One undo step for the edit and what followed.
        f.Session.Undo();
        NearLine(L(f, t), 2, 1, 0, 1);

        // Dragged sideways: the width is held, so the edit is refused and nothing changes.
        var ex = Assert.Throws<CommandException>(() => f.Run("drafting.updateDraft", new JsonObject { ["id"] = r, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(2, 0), ["b"] = J(2.5, 1) } }));
        Assert.Contains("cannot", ex.Message);
        Assert.Contains(width, ex.Message);
        NearLine(L(f, r), 2, 0, 2, 1);
        // Moving the right side alone sideways: refused the same way; up and down it may go.
        Assert.Throws<CommandException>(() => f.Run("element.transform", new JsonObject { ["ids"] = Ids(r), ["op"] = "move", ["by"] = J(0.5, 0) }));
        NearLine(L(f, r), 2, 0, 2, 1);
        // Moving the whole sketch: no solve, its constraints carried; the fixed corner is held where it now is.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(b, r, t, l), ["op"] = "move", ["by"] = J(5, 5) });
        NearLine(L(f, b), 5, 5, 7, 5);
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = r, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(7, 5), ["b"] = J(7, 7) } });
        NearLine(L(f, l), 5, 7, 5, 5);
        NearLine(L(f, b), 5, 5, 7, 5);
    }

    [Fact]
    public void DragSketch_moves_a_point_in_one_undo_step_and_its_preview_commits_nothing()
    {
        var f = F();
        var plan = Plan(f);
        var (b, r, t, l, width) = Held(f, plan);
        f.Run("studio.dragSketch", new JsonObject { ["viewId"] = plan, ["shape"] = r, ["part"] = "end", ["to"] = J(2, 1.8) });
        NearLine(L(f, r), 2, 0, 2, 1.8);
        NearLine(L(f, l), 0, 1.8, 0, 0);
        f.Session.Undo();
        NearLine(L(f, r), 2, 0, 2, 1);

        var preview = (JsonObject)f.Session.Query("studio.dragPreview", new JsonObject { ["viewId"] = plan, ["shape"] = r, ["part"] = "end", ["to"] = J(2, 2.2) })!;
        Assert.True((bool)preview["ok"]!);
        var moved = preview["shapes"]!.AsArray().ToDictionary(x => (string)x!["id"]!, x => Shape.From(x!["shape"])!);
        Assert.Equal(new[] { l, r, t }.Order(), moved.Keys.Order());
        NearLine((Cad.Line)moved[t], 2, 2.2, 0, 2.2);
        NearLine(L(f, r), 2, 0, 2, 1);       // nothing committed
        var no = (JsonObject)f.Session.Query("studio.dragPreview", new JsonObject { ["viewId"] = plan, ["shape"] = r, ["part"] = "end", ["to"] = J(2.5, 1) })!;
        Assert.False((bool)no["ok"]!);
        Assert.Contains(width, (string)no["reason"]!);
        Assert.Throws<CommandException>(() => f.Run("studio.dragSketch", new JsonObject { ["viewId"] = plan, ["shape"] = r, ["part"] = "end", ["to"] = J(2.5, 1) }));
        Assert.Throws<CommandException>(() => f.Run("studio.dragSketch", new JsonObject { ["viewId"] = plan, ["shape"] = r, ["part"] = "whole", ["to"] = J(2, 1) }));
        // A shape with no constraints is dragged as it is.
        var free = Line(f, plan, 10, 10, 11, 10);
        f.Run("studio.dragSketch", new JsonObject { ["viewId"] = plan, ["shape"] = free, ["part"] = "end", ["to"] = J(12, 12) });
        NearLine(L(f, free), 10, 10, 12, 12);
    }

    // ------------------------------------------------------------ the status

    [Fact]
    public void The_status_counts_degrees_of_freedom_as_constraints_come_and_names_the_free_shapes()
    {
        var f = F();
        var plan = Plan(f);
        var (b, r, t, l) = Rough(f, plan);
        Assert.Equal(16, (int)Status(f, plan)["dof"]!);
        AddAll(f, Corners(b, r, t, l));
        Assert.Equal(8, (int)Status(f, plan)["dof"]!);
        AddAll(f, Square(b, r, t, l));
        Assert.Equal(4, (int)Status(f, plan)["dof"]!);
        Add(f, C("fix", R(b, "start")));
        var st = Status(f, plan);
        Assert.Equal("underconstrained", (string)st["status"]!);
        Assert.Equal(2, (int)st["dof"]!);
        var free = st["freeShapes"]!.AsArray().Select(x => ((string)x!["shape"]!, (string)x["part"]!)).ToList();
        Assert.Contains((r, "end"), free);
        Assert.Contains((t, "whole"), free);
        Assert.DoesNotContain((b, "start"), free);
        Assert.DoesNotContain((l, "end"), free);
        AddAll(f, C("distance", 3, R(b)), C("distance", 2, R(l)));
        st = Status(f, plan);
        Assert.Equal("solved", (string)st["status"]!);
        Assert.Equal(0, (int)st["dof"]!);
    }

    // ------------------------------------------------------------ speed

    [Fact]
    public void A_sketch_of_a_hundred_constrained_lines_solves_within_its_command_quickly()
    {
        var f = F();
        var plan = Plan(f);
        // A staircase: one hundred lines, each horizontal or vertical, 1 m long, end to end.
        var shapes = new JsonArray();
        double x = 0, y = 0;
        for (var i = 0; i < 100; i++)
        {
            var (nx, ny) = i % 2 == 0 ? (x + 1, y) : (x, y + 1);
            shapes.Add((JsonNode)new JsonObject { ["type"] = "line", ["a"] = J(x, y), ["b"] = J(nx, ny) });
            (x, y) = (nx, ny);
        }
        var ids = f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shapes"] = shapes })!["ids"]!.AsArray().Select(v => (string)v!).ToArray();
        var cs = new List<JsonObject> { C("fix", R(ids[0], "start")) };
        for (var i = 0; i < 100; i++)
        {
            cs.Add(C(i % 2 == 0 ? "horizontal" : "vertical", R(ids[i])));
            cs.Add(C("distance", 1, R(ids[i])));
            if (i > 0) cs.Add(C("coincident", R(ids[i - 1], "end"), R(ids[i], "start")));
        }
        var added = AddAll(f, [.. cs]);
        Assert.Equal("solved", (string)Status(f, plan)["status"]!);
        var first = added[2];
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = first, ["value"] = 1.2 });   // warmed up
        var sw = Stopwatch.StartNew();
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = first, ["value"] = 1.5 });
        sw.Stop();
        output.WriteLine($"100 lines, 300 constraints: {sw.Elapsed.TotalMilliseconds:F1} ms");
        NearLine(L(f, ids[99]), 50.5, 49, 50.5, 50);
        Assert.True(sw.ElapsedMilliseconds < 600, $"took {sw.ElapsedMilliseconds} ms");
    }
}
