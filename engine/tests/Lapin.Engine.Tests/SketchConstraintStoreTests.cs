using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Sketch constraints, stage C0 of decision 520 (docs/plan-studio-parametric.md §4.2, §7.1): kept per sketch on a
/// <c>LapinSketch</c> carrier, checked, resolved for drawing, saved in the package and never in IFC, and carried through the
/// drafting commands that replace shapes (trim, extend, fillet, join, explode, offset, delete, move/rotate/mirror/copy, a face's
/// region pushed), or dropped with the reason in the command's result.
/// </summary>
public sealed class SketchConstraintStoreTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(Vec3 p) => [p.X, p.Y, p.Z];
    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static string Line(Fixture f, string view, double x1, double y1, double x2, double y2) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) } })!["ids"]![0]!;

    static string Poly(Fixture f, string view, bool closed, params (double X, double Y)[] pts) =>
        (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = view,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(pts.Select(p => (JsonNode)J(p.X, p.Y)).ToArray()), ["closed"] = closed },
        })!["ids"]![0]!;

    static JsonObject R(string shape, string part = "whole") => new() { ["shape"] = shape, ["part"] = part };

    static JsonObject Args(string kind, params JsonObject[] refs) => new() { ["kind"] = kind, ["refs"] = new JsonArray(refs.Select(r => (JsonNode)r).ToArray()) };

    static string Add(Fixture f, string kind, params JsonObject[] refs) => (string)f.Run("studio.addConstraint", Args(kind, refs))!["id"]!;

    static string Add(Fixture f, string kind, double value, params JsonObject[] refs)
    {
        var a = Args(kind, refs);
        a["value"] = value;
        return (string)f.Run("studio.addConstraint", a)!["id"]!;
    }

    static List<JsonObject> All(Fixture f, string view) =>
        f.Session.Query("studio.constraints", new JsonObject { ["viewId"] = view })!["constraints"]!.AsArray().Select(c => (JsonObject)c!).ToList();

    static JsonObject One(Fixture f, string view, string id) => All(f, view).Single(c => (string)c["id"]! == id);

    static (string Shape, string Part)[] Refs(JsonObject c) => c["refs"]!.AsArray().Select(r => ((string)r!["shape"]!, (string)r["part"]!)).ToArray();

    static string Stored(Fixture f) => f.Model.OfClass(SketchConstraints.Class).Select(e => e.Attrs["constraints"]!.ToJsonString()).SingleOrDefault() ?? "";

    static void Refused(Fixture f, string command, JsonObject a, string? says = null)
    {
        var ex = Assert.Throws<CommandException>(() => f.Run(command, a));
        if (says != null) Assert.Contains(says, ex.Message);
    }

    static string[] DroppedIds(JsonNode? result) => result?["dropped"]?.AsArray().Select(d => (string)d!["id"]!).ToArray() ?? [];

    [Fact]
    public void Constraints_are_added_changed_and_removed_one_undo_step_each_and_resolved_for_drawing()
    {
        var f = F();
        var plan = Plan(f);
        var l = Line(f, plan, 0, 0, 2, 0);
        var p = Line(f, plan, 1, 1, 1, 2);
        var h = Add(f, "horizontal", R(l, "edge:0"));
        // Kept on the sketch's carrier, a Lapin resource of the view.
        var carrier = f.Model.OfClass(SketchConstraints.Class).Single();
        Assert.Equal(plan, carrier.Str("viewId"));
        var c = One(f, plan, h);
        Assert.Equal("horizontal", (string)c["kind"]!);
        Assert.Equal([(l, "whole")], Refs(c));          // a line's edge:0 written as whole
        Assert.True((bool)c["driving"]!);
        Assert.Equal([0.0, 0.0], c["refs"]![0]!["a"]!.AsArray().Select(x => (double)x!).ToArray());
        Assert.Equal([2.0, 0.0], c["refs"]![0]!["b"]!.AsArray().Select(x => (double)x!).ToArray());

        // A dimension: its value, what it measures now.
        var d = Add(f, "distance", 3, R(l));
        Assert.Equal(3, (double)One(f, plan, d)["value"]!);
        Assert.Equal(2, (double)One(f, plan, d)["measured"]!, 9);
        // Point on object given line first: written point first.
        var on = Add(f, "pointOn", R(l), R(p, "start"));
        Assert.Equal([(p, "start"), (l, "whole")], Refs(One(f, plan, on)));
        Assert.Equal([1.0, 1.0], One(f, plan, on)["refs"]![0]!["at"]!.AsArray().Select(x => (double)x!).ToArray());

        // Changed: a value, a formula of numbers alone (the sketch belongs to no part), driven.
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = d, ["value"] = 2.5 });
        Assert.Equal(2.5, (double)One(f, plan, d)["value"]!);
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = d, ["formula"] = "1 + 1.5" });
        Assert.Equal(2.5, (double)One(f, plan, d)["value"]!);
        Assert.Equal("1 + 1.5", (string)One(f, plan, d)["formula"]!);
        Assert.Null(f.Model.OfClass(SketchConstraints.Class).Single().Attrs["constraints"]![1]!["value"]);   // a formula's value is worked out, never stored
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = d, ["driving"] = false });
        Assert.False((bool)One(f, plan, d)["driving"]!);
        Refused(f, "studio.updateConstraint", new JsonObject { ["id"] = h, ["value"] = 1 }, "takes no value");

        f.Run("studio.removeConstraint", new JsonObject { ["ids"] = Ids(h, on) });
        Assert.Equal([d], All(f, plan).Select(x => (string)x["id"]!).ToArray());

        // One undo step each.
        f.Session.Undo();
        Assert.Equal(3, All(f, plan).Count);
        f.Session.Undo(); f.Session.Undo(); f.Session.Undo();
        Assert.Equal(3, (double)One(f, plan, d)["value"]!);
        Assert.True((bool)One(f, plan, d)["driving"]!);
        f.Session.Undo(); f.Session.Undo(); f.Session.Undo();
        Assert.Empty(All(f, plan));
        Assert.Empty(f.Model.OfClass(SketchConstraints.Class));

        // Asked by shapes: those touching them.
        Add(f, "vertical", R(p));
        Add(f, "horizontal", R(l));
        Assert.Single(f.Session.Query("studio.constraints", new JsonObject { ["shapeIds"] = Ids(p) })!["constraints"]!.AsArray());
    }

    [Fact]
    public void A_constraint_is_refused_when_its_references_or_value_do_not_fit_its_kind()
    {
        var f = F();
        var plan = Plan(f);
        var other = Plan(f);
        var l = Line(f, plan, 0, 0, 2, 0);
        var m = Line(f, plan, 0, 1, 2, 1);
        var far = Line(f, other, 0, 0, 1, 0);
        var circle = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = J(5, 5), ["r"] = 1 } })!["ids"]![0]!;

        Refused(f, "studio.addConstraint", Args("horizontal", R(l), R(m), R(l, "start")), "Horizontal takes");
        Refused(f, "studio.addConstraint", Args("radius", R(l)), "Radius takes a circle or an arc");
        Refused(f, "studio.addConstraint", Args("tangent", R(l), R(m)), "Tangent takes");
        Refused(f, "studio.addConstraint", Args("coincident", R(l, "vertex:5"), R(m, "start")), "has no part");
        Refused(f, "studio.addConstraint", Args("pointOn", R(circle, "start"), R(l)), "has no part");
        Refused(f, "studio.addConstraint", Args("coincident", R(l, "start"), R(l, "start")), "once");
        Refused(f, "studio.addConstraint", Args("glued", R(l)), "is not a constraint");
        Refused(f, "studio.addConstraint", Args("horizontal", R(f.Storey)), "not a drafted");
        Refused(f, "studio.addConstraint", Args("parallel", R(l), R(far)), "one sketch");
        Refused(f, "studio.addConstraint", Args("distance", R(l)), "needs its value");
        var negative = Args("distance", R(l)); negative["value"] = -1;
        Refused(f, "studio.addConstraint", negative, "not negative");
        var zero = Args("radius", R(circle)); zero["value"] = 0;
        Refused(f, "studio.addConstraint", zero, "more than nothing");
        var valued = Args("coincident", R(l, "start"), R(m, "start")); valued["value"] = 1;
        Refused(f, "studio.addConstraint", valued, "takes no value");
        var driven = Args("parallel", R(l), R(m)); driven["driving"] = false;
        Refused(f, "studio.addConstraint", driven, "always driving");
        var named = Args("distance", R(l)); named["formula"] = "W / 2";
        Refused(f, "studio.addConstraint", named, "„W” is not known");
        var both = Args("distance", R(l)); both["formula"] = "2"; both["value"] = 2;
        Refused(f, "studio.addConstraint", both, "not both");
        // Nothing was kept of the refusals.
        Assert.Empty(f.Model.OfClass(SketchConstraints.Class));

        // A driven dimension needs no value; an angle is stored in (-180, 180].
        var reference = Args("distance", R(l)); reference["driving"] = false;
        f.Run("studio.addConstraint", reference);
        var angle = Args("angle", R(l), R(m)); angle["value"] = 270;
        var a = (string)f.Run("studio.addConstraint", angle)!["id"]!;
        Assert.Equal(-90, (double)One(f, plan, a)["value"]!, 9);
    }

    [Fact]
    public void A_formula_names_the_parameters_of_the_part_whose_recipe_uses_the_sketch()
    {
        var f = F();
        var plan = Plan(f);
        var sq = Poly(f, plan, true, (0, 0), (1, 0), (1, 1), (0, 1));
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = sq, ["depth"] = 2 })!["id"]!;
        var type = (string)f.Run("studio.setType", new JsonObject { ["ids"] = Ids(solid), ["name"] = "Pillar" })!["id"]!;
        var a = Args("distance", R(sq, "edge:0")); a["formula"] = "W / 2";
        Refused(f, "studio.addConstraint", a, "„W” is not known");
        f.Run("studio.updatePart", new JsonObject
        {
            ["id"] = type,
            ["def"] = new JsonObject
            {
                ["params"] = new JsonArray(new JsonObject { ["key"] = "W", ["default"] = 0.4 }, new JsonObject { ["key"] = "T", ["default"] = 0.02 }),
                ["bodies"] = new JsonArray(new JsonObject { ["plane"] = "xy", ["points"] = new JsonArray(J(0, 0), J(1, 0), J(1, 1)), ["depth"] = "T" }),
            },
        });
        var id = (string)f.Run("studio.addConstraint", a)!["id"]!;
        Assert.Equal(0.2, (double)One(f, plan, id)["value"]!, 9);
        a["formula"] = "Q * 2";
        Refused(f, "studio.addConstraint", a, "not a parameter of the part (it has W, T)");

        // The part's tree shows the sketch's constraints.
        var tree = (JsonObject)f.Session.Query("studio.partTree", new JsonObject { ["id"] = solid })!;
        var sketch = tree["sketches"]!.AsArray().Single()!;
        Assert.Equal(1, (int)sketch["constraintCount"]!);
        Assert.Equal(id, (string)sketch["constraints"]![0]!["id"]!);
        Assert.Single(f.Session.Query("studio.constraints", new JsonObject { ["elementId"] = solid })!["constraints"]!.AsArray());
    }

    [Fact]
    public void Constraints_are_kept_in_the_package_and_never_written_to_IFC()
    {
        var f = F();
        var plan = Plan(f);
        var l = Line(f, plan, 0, 0, 2, 0);
        var h = Add(f, "horizontal", R(l));
        Add(f, "distance", 2, R(l));
        var before = Stored(f);
        using (var ms = new MemoryStream())
        {
            ProjectPackage.Write(f.Session, ms);
            ms.Position = 0;
            var content = LapinPackage.Read(ms);
            Assert.Equal("studio", content.Classes[SketchConstraints.Class]);
            var g = F();
            g.Session.Load(content.Entities, content.Relations, null, content.Classes);
            Assert.Equal(before, Stored(g));
            Assert.Equal(2, All(g, plan).Count);
        }
        using (var ms = new MemoryStream())
        {
            IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
            var text = Encoding.UTF8.GetString(ms.ToArray());
            Assert.DoesNotContain(h, text);
            Assert.DoesNotContain(SketchConstraints.Class, text);
            Assert.DoesNotContain("Constraints on", text);
            Assert.DoesNotContain("horizontal", text);
        }
    }

    [Fact]
    public void Trim_keeps_the_kept_end_s_constraints_moves_a_piece_s_to_it_and_drops_what_lost_its_point()
    {
        var f = F();
        var plan = Plan(f);
        var h = Line(f, plan, 0, 0, 6, 0);
        Line(f, plan, 2, -1, 2, 1);
        Line(f, plan, 4, -1, 4, 1);
        var e = Line(f, plan, 6, 0, 6, 2);
        var w = Line(f, plan, 0, 0, 0, 2);
        var fix = Add(f, "fix", R(h, "start"));
        var startMeets = Add(f, "coincident", R(h, "start"), R(w, "start"));
        var endMeets = Add(f, "coincident", R(h, "end"), R(e, "start"));
        var level = Add(f, "horizontal", R(h));
        var length = Add(f, "distance", 6, R(h));
        // The middle goes: the first piece keeps the line's id, the last is a new line.
        var r = f.Run("drafting.trim", new JsonObject { ["id"] = h, ["at"] = J(3, 0) })!;
        var pieces = r["ids"]!.AsArray().Select(x => (string)x!).ToArray();
        Assert.Equal(h, pieces[0]);
        var last = pieces[1];
        Assert.Equal([(h, "start")], Refs(One(f, plan, fix)));
        Assert.Equal([(h, "start"), (w, "start")], Refs(One(f, plan, startMeets)));
        Assert.Equal([(last, "end"), (e, "start")], Refs(One(f, plan, endMeets)));   // the piece holding the point
        Assert.Equal([(h, "whole")], Refs(One(f, plan, level)));
        // Its length changed: the dimension goes, and the command says so.
        Assert.Equal([length], DroppedIds(r));
        Assert.Equal("its edge changed length", (string)r["dropped"]![0]!["reason"]!);
        Assert.Equal("distance", (string)r["dropped"]![0]!["kind"]!);
        Assert.Equal(4, All(f, plan).Count);
        // Undone with the trim, in one step.
        f.Session.Undo();
        Assert.Equal(5, All(f, plan).Count);
        Assert.Equal([(h, "end"), (e, "start")], Refs(One(f, plan, endMeets)));

        // Trimmed at its end: the end that went drops what held it.
        var r2 = f.Run("drafting.trim", new JsonObject { ["id"] = h, ["at"] = J(5, 0) })!;
        Assert.Contains(endMeets, DroppedIds(r2));
        Assert.Equal("the point it held is gone", (string)r2["dropped"]!.AsArray().Single(d => (string)d!["id"]! == endMeets)!["reason"]!);
        Assert.Equal([(h, "start")], Refs(One(f, plan, fix)));
    }

    [Fact]
    public void Extend_and_fillet_keep_what_still_holds_and_drop_the_corner_that_moved()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 2, 0);
        var b = Line(f, plan, 2, 0.5, 2, 2);
        Line(f, plan, 3, -1, 3, 3);
        var level = Add(f, "horizontal", R(a));
        var corner = Add(f, "coincident", R(a, "end"), R(b, "start"));
        var square = Add(f, "perpendicular", R(a), R(b));
        var start = Add(f, "fix", R(a, "start"));
        var r = f.Run("drafting.extend", new JsonObject { ["id"] = a, ["at"] = J(1.9, 0) })!;
        Assert.Equal([corner], DroppedIds(r));
        Assert.Equal([(a, "whole")], Refs(One(f, plan, level)));
        Assert.Equal([(a, "whole"), (b, "whole")], Refs(One(f, plan, square)));
        Assert.Equal([(a, "start")], Refs(One(f, plan, start)));
        f.Session.Undo();

        // A fillet: the lines shortened to the arc, still square; the corner they met at is gone.
        var fr = f.Run("drafting.fillet", new JsonObject { ["a"] = a, ["b"] = b, ["atA"] = J(1, 0), ["atB"] = J(2, 1), ["radius"] = 0.5 })!;
        Assert.Equal([corner], DroppedIds(fr));
        Assert.Equal([(a, "whole"), (b, "whole")], Refs(One(f, plan, square)));
        Assert.Equal([(a, "start")], Refs(One(f, plan, start)));
        Assert.Equal(3, All(f, plan).Count);
    }

    [Fact]
    public void Explode_maps_a_polyline_s_vertices_and_edges_onto_its_lines_and_keeps_its_corners_together()
    {
        var f = F();
        var plan = Plan(f);
        var rect = Poly(f, plan, true, (0, 0), (2, 0), (2, 1), (0, 1));
        var x = Line(f, plan, 2, 1, 3, 2);
        var bottom = Add(f, "horizontal", R(rect, "edge:0"));
        var side = Add(f, "vertical", R(rect, "edge:1"));
        var meets = Add(f, "coincident", R(rect, "vertex:2"), R(x, "start"));
        var origin = Add(f, "fix", R(rect, "vertex:0"));
        var r = f.Run("drafting.explode", new JsonObject { ["ids"] = Ids(rect) })!;
        var lines = r["ids"]!.AsArray().Select(i => (string)i!).ToArray();
        Assert.Equal(4, lines.Length);
        Assert.Empty(DroppedIds(r));
        Assert.Equal([(lines[0], "whole")], Refs(One(f, plan, bottom)));
        Assert.Equal([(lines[1], "whole")], Refs(One(f, plan, side)));
        Assert.Equal([(lines[0], "start")], Refs(One(f, plan, origin)));
        var held = Refs(One(f, plan, meets));
        Assert.Equal((x, "start"), held[1]);
        Assert.Contains(held[0], new[] { (lines[1], "end"), (lines[2], "start") });
        // The four corners, a coincidence each, so a solve cannot pull them apart.
        var added = r["constraintsAdded"]!.AsArray().Select(i => (string)i!).ToArray();
        Assert.Equal(4, added.Length);
        Assert.Equal([(lines[0], "end"), (lines[1], "start")], Refs(One(f, plan, added.Single(id => Refs(One(f, plan, id))[0].Shape == lines[0]))));
        Assert.Equal(8, All(f, plan).Count);
        // An unconstrained polyline exploded gets nothing.
        var free = Poly(f, plan, false, (5, 5), (6, 5), (6, 6));
        Assert.Null(f.Run("drafting.explode", new JsonObject { ["ids"] = Ids(free) })!["constraintsAdded"]);
    }

    [Fact]
    public void Join_maps_lines_onto_the_polyline_s_edges_and_drops_the_coincidence_it_welded()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 1, 0);
        var b = Line(f, plan, 1, 0, 1, 1);
        var level = Add(f, "horizontal", R(a));
        var upright = Add(f, "vertical", R(b));
        var welded = Add(f, "coincident", R(a, "end"), R(b, "start"));
        var top = Add(f, "fix", R(b, "end"));
        var r = f.Run("drafting.join", new JsonObject { ["ids"] = Ids(a, b) })!;
        Assert.Equal([welded], DroppedIds(r));
        Assert.Equal("the edit made its points one", (string)r["dropped"]![0]!["reason"]!);
        Assert.Equal([(a, "edge:0")], Refs(One(f, plan, level)));
        Assert.Equal([(a, "edge:1")], Refs(One(f, plan, upright)));
        Assert.Equal([(a, "vertex:2")], Refs(One(f, plan, top)));
    }

    [Fact]
    public void Deleting_a_shape_drops_its_constraints_with_a_report_and_undo_brings_them_back()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 1, 0);
        var b = Line(f, plan, 1, 0, 1, 1);
        var c1 = Add(f, "perpendicular", R(a), R(b));
        var c2 = Add(f, "horizontal", R(a));
        var r = f.Run("element.delete", new JsonObject { ["ids"] = Ids(b) })!;
        Assert.Equal([c1], DroppedIds(r));
        Assert.Equal("its shape was deleted", (string)r["dropped"]![0]!["reason"]!);
        Assert.Equal([c2], All(f, plan).Select(c => (string)c["id"]!).ToArray());
        f.Session.Undo();
        Assert.Equal(2, All(f, plan).Count);
        // The last one gone, the carrier goes too.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(a, b) });
        Assert.Empty(f.Model.OfClass(SketchConstraints.Class));
    }

    [Fact]
    public void Copies_take_the_constraints_among_the_copied_shapes_only()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 1, 0);
        var b = Line(f, plan, 1, 0, 1, 1);
        var c = Line(f, plan, 5, 5, 6, 5);
        Add(f, "coincident", R(a, "end"), R(b, "start"));
        Add(f, "distance", 1, R(a));
        Add(f, "parallel", R(a), R(c));      // reaches a shape not copied: stays the original's alone
        var r = f.Run("drafting.transform", new JsonObject { ["ids"] = Ids(a, b), ["op"] = "copy", ["by"] = J(0, 3) })!;
        var copies = r["ids"]!.AsArray().Select(x => (string)x!).ToArray();
        Assert.Equal(2, (int)r["constraintsCopied"]!);
        Assert.Equal(5, All(f, plan).Count);
        Assert.Contains(All(f, plan), x => (string)x["kind"]! == "coincident" && Refs(x).SequenceEqual([(copies[0], "end"), (copies[1], "start")]));
        Assert.Contains(All(f, plan), x => (string)x["kind"]! == "distance" && Refs(x).SequenceEqual([(copies[0], "whole")]) && (double)x["value"]! == 1);
        // The same through element.transform (the Move tool's copy).
        var r2 = f.Run("element.transform", new JsonObject { ["ids"] = Ids(a, b), ["op"] = "copy", ["by"] = J(0, 6) })!;
        Assert.Equal(2, (int)r2["constraintsCopied"]!);
        Assert.Equal(7, All(f, plan).Count);
    }

    [Fact]
    public void A_moved_sketch_keeps_its_constraints_and_a_turn_carries_what_depends_on_the_axes()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 2, 0);
        var b = Line(f, plan, 2, 0, 2, 1);
        var arc = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "arc", ["c"] = J(0, 1), ["r"] = 1, ["start"] = 0, ["end"] = Math.PI / 2 } })!["ids"]![0]!;
        var level = Add(f, "horizontal", R(a));
        var dx = Add(f, "distanceX", 2, R(a));
        var meets = Add(f, "coincident", R(b, "end"), R(arc, "start"));
        var radius = Add(f, "radius", 1, R(arc));
        var before = Stored(f);
        // Moved whole, with element.transform and with drafting.transform: untouched.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(a, b, arc), ["op"] = "move", ["by"] = J(5, 5) });
        Assert.Equal(before, Stored(f));
        f.Run("drafting.transform", new JsonObject { ["ids"] = Ids(a, b, arc), ["op"] = "move", ["by"] = J(-5, -5) });
        Assert.Equal(before, Stored(f));
        // A quarter turn: horizontal becomes vertical, the run along x one along y.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(a, b, arc), ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 90 });
        Assert.Equal("vertical", (string)One(f, plan, level)["kind"]!);
        Assert.Equal("distanceY", (string)One(f, plan, dx)["kind"]!);
        Assert.Equal(2, (double)One(f, plan, dx)["value"]!, 9);
        Assert.Equal(2, (double)One(f, plan, dx)["measured"]!, 9);
        f.Session.Undo();
        // A mirror: an arc's start and end swap (Lapin keeps arcs counter-clockwise).
        f.Run("drafting.transform", new JsonObject { ["ids"] = Ids(a, b, arc), ["op"] = "mirror", ["a"] = J(0, -3), ["b"] = J(0, 3) });
        Assert.Equal([(b, "end"), (arc, "end")], Refs(One(f, plan, meets)));
        Assert.Equal(-2, (double)One(f, plan, dx)["value"]!, 9);
        Assert.Equal(-2, (double)One(f, plan, dx)["measured"]!, 9);
        f.Session.Undo();
        // Scaled: lengths follow.
        f.Run("drafting.transform", new JsonObject { ["ids"] = Ids(a, b, arc), ["op"] = "scale", ["center"] = J(0, 0), ["factor"] = 2 });
        Assert.Equal(2, (double)One(f, plan, radius)["value"]!, 9);
        Assert.Equal(4, (double)One(f, plan, dx)["value"]!, 9);
        f.Session.Undo();
        // Turned 30°: horizontal and the run along x no longer hold, and the command says so.
        var r = f.Run("drafting.transform", new JsonObject { ["ids"] = Ids(a, b, arc), ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 30 })!;
        Assert.Equal(new[] { level, dx }.Order(), DroppedIds(r).Order());
        Assert.Equal("turned off the view's axes", (string)r["dropped"]![0]!["reason"]!);
        Assert.Equal(2, All(f, plan).Count);
    }

    [Fact]
    public void Offset_leaves_the_original_s_constraints_and_makes_a_free_copy()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 2, 0);
        Add(f, "horizontal", R(a));
        Add(f, "distance", 2, R(a));
        var before = Stored(f);
        var o = (string)f.Run("drafting.offset", new JsonObject { ["id"] = a, ["side"] = J(1, 1), ["distance"] = 0.5 })!["id"]!;
        Assert.Equal(before, Stored(f));
        Assert.Empty(f.Session.Query("studio.constraints", new JsonObject { ["shapeIds"] = Ids(o) })!["constraints"]!.AsArray());
    }

    [Fact]
    public void A_face_s_region_pushed_is_tied_to_the_constrained_sketch_that_split_it()
    {
        var f = F();
        var plan = Plan(f);
        var sq = Poly(f, plan, true, (0, 0), (2, 0), (2, 2), (0, 2));
        var cube = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = sq, ["depth"] = 2 })!["id"]!;
        var east = new Vec3(1, 0, 0);
        var drawn = f.Run("studio.onFace", new JsonObject
        {
            ["id"] = cube, ["point"] = J3(new Vec3(2, 1, 1)), ["normal"] = J3(east), ["open"] = true,
            ["outline"] = new JsonArray(J3(new(2, 0, 0.5)), J3(new(2, 2, 0.5))),
        })!;
        var face = (string)drawn["planeId"]!;
        var line = (string)drawn["sketchId"]!;
        var level = Add(f, "horizontal", R(line));
        var r = f.Run("studio.pushPull", new JsonObject { ["id"] = cube, ["point"] = J3(new(2, 1, 0.2)), ["normal"] = J3(east), ["distance"] = 0.3 })!;
        Assert.True((bool)r["region"]!);
        var outline = (string)r["sketchId"]!;
        // The line's constraint as it was; the region's corners on the line's ends, coincident with them.
        Assert.Equal([(line, "whole")], Refs(One(f, face, level)));
        var added = r["constraintsAdded"]!.AsArray().Select(x => (string)x!).ToArray();
        Assert.Equal(2, added.Length);
        foreach (var id in added)
        {
            var c = One(f, face, id);
            Assert.Equal("coincident", (string)c["kind"]!);
            Assert.Equal(outline, Refs(c)[0].Shape);
            Assert.Equal(line, Refs(c)[1].Shape);
        }
        Assert.Equal(new[] { "end", "start" }, added.Select(id => Refs(One(f, face, id))[1].Part).Order().ToArray());
    }

    [Fact]
    public void Any_other_command_that_takes_a_constrained_shape_away_leaves_no_constraint_pointing_at_it()
    {
        var f = F();
        var plan = Plan(f);
        var poly = Poly(f, plan, false, (0, 0), (1, 0), (1, 1));
        var c = Add(f, "fix", R(poly, "vertex:2"));
        var keep = Add(f, "horizontal", R(poly, "edge:0"));
        // A grip edit that keeps its points keeps them; one that takes a point away finds the rest again by geometry.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = poly, ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(1, 0), J(1.5, 1)), ["closed"] = false } });
        Assert.Equal([(poly, "vertex:2")], Refs(One(f, plan, c)));
        var r = f.Run("drafting.updateDraft", new JsonObject { ["id"] = poly, ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(1, 0)), ["closed"] = false } })!;
        Assert.Equal([c], DroppedIds(r));
        Assert.Equal([(poly, "edge:0")], Refs(One(f, plan, keep)));
    }
}
