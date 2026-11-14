using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// Shop drawings, stage S0 (docs/plan-shop-drawings.md, decision 579): an element's own frame (a module's axes, the core's,
/// or one from its body), element views read from it on every ask, the view's subject as the first rule of Visibility /
/// Graphics, and the element-anchored views there were before registered as roles, drawing as they did.
/// </summary>
public class ElementViewTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule(), new WoodModule());

    static string Id(JsonNode? n) => (string)n!["id"]!;
    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;

    static string Member(Fixture f, Vec3 a, Vec3 b, double rotation = 0) =>
        Id(f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = a.X, ["y1"] = a.Y, ["z1"] = a.Z, ["x2"] = b.X, ["y2"] = b.Y, ["z2"] = b.Z, ["rotation"] = rotation }));

    static string View(Fixture f, string element, string role, JsonObject? more = null)
    {
        var a = new JsonObject { ["element"] = element, ["role"] = role };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return Id(f.Run("elementView.create", a));
    }

    /// <summary>
    /// The drawing's seen and cut lines (its pens' segments, rounded to a tenth of a millimetre, one way round, sorted). Hidden
    /// lines are left out: where a hidden edge lies under a seen one (a flange's edge behind the other's) which of the two the
    /// hidden-line removal keeps is a matter of rounding.
    /// </summary>
    static List<string> Lines(JsonObject d)
    {
        var o = new List<string>();
        foreach (var pen in ((JsonArray)d["pens"]!).OfType<JsonObject>())
        {
            if (pen["segments"] is not JsonArray segs) continue;
            var layer = (string?)pen["layer"];
            if (layer is "HIDDEN" or null || layer.StartsWith("HATCH", StringComparison.Ordinal)) continue;
            var xs = segs.Select(x => (double)x!).ToArray();
            for (var i = 0; i + 3 < xs.Length; i += 4)
            {
                static string R(double v) => Math.Round(v, 4).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture).Replace("-0.0000", "0.0000");
                var (a, b) = ((R(xs[i]), R(xs[i + 1])), (R(xs[i + 2]), R(xs[i + 3])));
                if (string.CompareOrdinal(a.Item1 + a.Item2, b.Item1 + b.Item2) > 0) (a, b) = (b, a);
                o.Add($"{layer} {a.Item1},{a.Item2} {b.Item1},{b.Item2}");
            }
        }
        o.Sort(StringComparer.Ordinal);
        return o;
    }

    [Fact]
    public void A_member_turned_37_degrees_and_tilted_draws_the_same_front_as_one_along_x()
    {
        var f = F();
        var along = Member(f, new Vec3(0, 0, 1), new Vec3(6, 0, 1));
        var (az, tilt) = (37 * Math.PI / 180, 20 * Math.PI / 180);
        var dir = new Vec3(Math.Cos(az) * Math.Cos(tilt), Math.Sin(az) * Math.Cos(tilt), Math.Sin(tilt));
        var s = new Vec3(10, 5, 1);
        var turned = Member(f, s, s + dir * 6);
        foreach (var role in new[] { "front", "top", "left", "section:x" })
        {
            var (a, b) = (Drawing(f, View(f, along, role)), Drawing(f, View(f, turned, role)));
            Assert.Null(a["problem"]);
            var (la, lb) = (Lines(a), Lines(b));
            Assert.NotEmpty(la);
            Assert.Equal(la, lb);
        }
        // The frame: along the axis, the web up (square to the axis, in the vertical plane through it).
        var fr = (JsonObject)f.Session.Query("element.frame", new JsonObject { ["id"] = turned })!;
        var x = ((JsonArray)fr["x"]!).Select(v => (double)v!).ToArray();
        Assert.Equal(dir.X, x[0], 6);
        Assert.Equal(dir.Z, x[2], 6);
        Assert.Equal(6.0, (double)fr["length"]!, 3);
        Assert.Equal("module", (string?)fr["source"]);
    }

    [Fact]
    public void An_element_view_follows_its_element_when_it_moves_and_turns()
    {
        var f = F();
        var member = Member(f, new Vec3(0, 0, 2), new Vec3(5, 0, 2));
        var view = View(f, member, "front");
        var before = Drawing(f, view);
        var o0 = ((JsonArray)before["frame"]!["origin"]!).Select(v => (double)v!).ToArray();
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(member), ["op"] = "move", ["by"] = new JsonArray(3, 4) });
        Assert.Equal(Lines(before), Lines(Drawing(f, view)));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(member), ["op"] = "rotate", ["angle"] = 50, ["center"] = new JsonArray(3, 4) });
        var after = Drawing(f, view);
        Assert.Null(after["problem"]);
        Assert.Equal(Lines(before), Lines(after));
        var o1 = ((JsonArray)after["frame"]!["origin"]!).Select(v => (double)v!).ToArray();
        Assert.True(Math.Abs(o1[0] - o0[0]) > 1 || Math.Abs(o1[1] - o0[1]) > 1, "the view's plane moved with the member");
        // The element gone: the view says so rather than drawing the rest of the model.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(member) });
        Assert.NotNull(Drawing(f, view)["problem"]);
    }

    [Fact]
    public void A_boxed_proxy_without_axes_of_its_own_takes_its_box_as_its_frame()
    {
        var f = F();
        var box = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(3, 4), ["width"] = 0.5, ["length"] = 2, ["height"] = 1, ["rotation"] = 30 }));
        var fr = (JsonObject)f.Session.Query("element.frame", new JsonObject { ["id"] = box })!;
        Assert.Equal("body", (string?)fr["source"]);
        Assert.Equal(2.0, (double)fr["length"]!, 6);
        Assert.Equal(0.5, (double)fr["width"]!, 6);
        Assert.Equal(1.0, (double)fr["height"]!, 6);
        var z = ((JsonArray)fr["z"]!).Select(v => (double)v!).ToArray();
        Assert.Equal(1.0, z[2], 9);
        // X along the 2 m side: the box's x or y turned by 30°, level.
        var x = ((JsonArray)fr["x"]!).Select(v => (double)v!).ToArray();
        Assert.Equal(0.0, x[2], 9);
        var a30 = new Vec2(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));
        var crossA = Math.Abs(Vec2.Cross(new Vec2(x[0], x[1]), a30));
        var crossB = Math.Abs(Vec2.Cross(new Vec2(x[0], x[1]), a30.Perp));
        Assert.True(Math.Min(crossA, crossB) < 1e-6, $"x {x[0]}, {x[1]}");
        // Every point of the body at positive x, y and z of the frame: the origin is the box's first corner.
        var min = ((JsonArray)fr["min"]!).Select(v => (double)v!).ToArray();
        Assert.All(min, v => Assert.Equal(0.0, v, 9));
        // Its front view draws the 2 × 1 m face, with the margin round it.
        var d = Drawing(f, View(f, box, "front"));
        var b = ((JsonArray)d["bounds"]!).Select(v => (double)v!).ToArray();
        Assert.Equal(2.0, b[2] - b[0], 2);
        Assert.Equal(1.0, b[3] - b[1], 2);
    }

    [Fact]
    public void A_plate_from_a_body_alone_has_its_thickness_as_its_z()
    {
        var f = F();
        var box = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(0, 0), ["width"] = 0.4, ["length"] = 0.02, ["height"] = 0.6 }));
        f.Run("studio.setClass", new JsonObject { ["ids"] = new JsonArray(box), ["class"] = "IfcPlate" });
        Assert.Equal("IfcPlate", f.Model.Get(box)!.Class);
        var fr = (JsonObject)f.Session.Query("element.frame", new JsonObject { ["id"] = box })!;
        Assert.Equal("plate", (string?)fr["source"]);
        Assert.Equal(0.6, (double)fr["length"]!, 6);
        Assert.Equal(0.4, (double)fr["width"]!, 6);
        Assert.Equal(0.02, (double)fr["height"]!, 6);
    }

    /// <summary>The elements a drawing picks (its pick marks' owners).</summary>
    static HashSet<string> Picked(JsonObject d) => ((JsonArray)d["marks"]!).OfType<JsonObject>().Where(m => m["pick"] != null).Select(m => (string)m["id"]!).ToHashSet();

    [Fact]
    public void A_views_subject_hides_everything_else_in_plan_section_sheet_pdf_and_dxf()
    {
        var f = F();
        var a = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(0, 0), ["width"] = 1, ["length"] = 1, ["height"] = 2 }));
        var b = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(3, 0), ["width"] = 1, ["length"] = 1, ["height"] = 2 }));
        var plan = Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "P" }));
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-2, 0), ["end"] = new JsonArray(6, 0) }));
        var sheet = Id(f.Run("sheet.create", new JsonObject { ["paper"] = "A3" }));
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan });
        Assert.Equal(new HashSet<string> { a, b }, Picked(Drawing(f, plan)));
        Assert.Equal(new HashSet<string> { a, b }, Picked(Drawing(f, section)));
        var dxfAll = Views.Dxf(f.Session.Reader, f.Model.Get(plan)!).Length;
        var pdfAll = Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]).Length;
        foreach (var v in new[] { plan, section }) f.Run("view.setSubject", new JsonObject { ["viewId"] = v, ["subject"] = new JsonArray(a) });
        Assert.Equal(new HashSet<string> { a }, Picked(Drawing(f, plan)));
        Assert.Equal(new HashSet<string> { a }, Picked(Drawing(f, section)));
        // The sheet places the view as it draws: only the subject's lines.
        var placed = Sheets.Place(f.Session.Reader, f.Model.Get(sheet)!).Single();
        Assert.All(placed.Drawing.Generated!.Lines, l => Assert.Equal(a, l.Element));
        Assert.True(Views.Dxf(f.Session.Reader, f.Model.Get(plan)!).Length < dxfAll);
        Assert.True(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]).Length < pdfAll);
        // Faded: the rest drawn again, halftone; the subject cleared: everything as before.
        f.Run("view.setSubject", new JsonObject { ["viewId"] = plan, ["context"] = "faded" });
        Assert.Equal(new HashSet<string> { a, b }, Picked(Drawing(f, plan)));
        f.Run("view.setSubject", new JsonObject { ["viewId"] = plan, ["subject"] = new JsonArray(), ["context"] = "none" });
        Assert.Null(f.Model.Get(plan)!.Attrs[ElementViews.SubjectKey]);
        Assert.Equal(dxfAll, Views.Dxf(f.Session.Reader, f.Model.Get(plan)!).Length);
    }

    [Fact]
    public void An_element_view_shows_only_its_element_and_its_drafting_stays()
    {
        var f = F();
        var a = Member(f, new Vec3(0, 0, 1), new Vec3(4, 0, 1));
        Member(f, new Vec3(0, 0.3, 1), new Vec3(4, 0.3, 1));
        var view = View(f, a, "top");
        Assert.Equal(new HashSet<string> { a }, Picked(Drawing(f, view)));
        // A line drafted on the view is the view's own: drawn whatever the subject.
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(-1, -1), ["b"] = new JsonArray(1, 1) }) })!["ids"]![0]!;
        Assert.Contains(((JsonArray)Drawing(f, view)["marks"]!).OfType<JsonObject>(), m => (string?)m["id"] == line);
    }

    [Fact]
    public void An_iso_view_looks_from_the_frames_south_west_and_draws_on_a_sheet()
    {
        var f = F();
        var member = Member(f, new Vec3(0, 0, 1), new Vec3(3, 3, 1));
        var iso = View(f, member, "iso");
        Assert.Equal("3d", f.Model.Get(iso)!.Str("kind"));
        var d = Drawing(f, iso);
        Assert.Null(d["problem"]);
        Assert.NotEmpty(Lines(d));
        // From the frame's −x, −y, +z: looking along its +x +y −z.
        var dir = ((JsonArray)d["frame"]!["dir"]!).Select(v => (double)v!).ToArray();
        Assert.True(dir[2] < -0.5);
        var sheet = Id(f.Run("sheet.create", new JsonObject { ["paper"] = "A3" }));
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = iso });
        Assert.NotEmpty(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]));
    }

    [Fact]
    public void A_section_at_a_station_cuts_the_member_square_to_its_axis()
    {
        var f = F();
        var member = Member(f, new Vec3(0, 0, 1), new Vec3(6, 0, 1));
        var view = View(f, member, "section:x", new JsonObject { ["station"] = 2.5 });
        var d = Drawing(f, view);
        Assert.Null(d["problem"]);
        var dir = ((JsonArray)d["frame"]!["dir"]!).Select(v => (double)v!).ToArray();
        Assert.Equal(1.0, dir[0], 9);
        var origin = ((JsonArray)d["frame"]!["origin"]!).Select(v => (double)v!).ToArray();
        Assert.Equal(2.5, origin[0], 6);
        // The IPE 200's section: 100 wide, 200 high.
        var b = ((JsonArray)d["bounds"]!).Select(v => (double)v!).ToArray();
        Assert.Equal(0.1, b[2] - b[0], 3);
        Assert.Equal(0.2, b[3] - b[1], 3);
    }

    [Fact]
    public void The_element_anchored_views_there_were_are_roles_and_draw_as_before()
    {
        var f = F();
        var beam = Id(f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 }));
        // A section across made the old way, and made through its role: the same drawing.
        var old = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 2 }));
        var byRole = View(f, beam, "across", new JsonObject { ["station"] = 2 });
        Assert.Equal(Lines(Drawing(f, old)), Lines(Drawing(f, byRole)));
        var roles = (JsonObject)f.Session.Query("elementView.roles", new JsonObject { ["id"] = beam })!;
        var views = ((JsonArray)roles["views"]!).OfType<JsonObject>().ToList();
        Assert.Contains(views, v => (string?)v["id"] == old && (string?)v["role"] == "across");
        Assert.Contains(((JsonArray)roles["roles"]!).OfType<JsonObject>(), r => (string?)r["role"] == "front");
        // A timber-frame wall's frame elevation through its role, as wood.frameView makes it.
        var type = Id(f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Frame", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Timber", ["thickness"] = 0.14, ["core"] = true }),
        }));
        f.Run("wood.setWallFrame", new JsonObject { ["id"] = type, ["spec"] = new JsonObject() });
        var wall = f.Wall(0, 3, 4, 3, type);
        var frame = Id(f.Run("wood.frameView", new JsonObject { ["id"] = wall }));
        var frame2 = View(f, wall, "wood.frame");
        Assert.Equal(Lines(Drawing(f, frame)), Lines(Drawing(f, frame2)));
        Assert.Contains(((JsonArray)((JsonObject)f.Session.Query("elementView.roles", new JsonObject { ["id"] = wall })!)["roles"]!).OfType<JsonObject>(), r => (string?)r["role"] == "wood.frame");
        // A wall built of layers has no frame elevation.
        var plain = f.Wall(0, 6, 4, 6, f.Type("Brick", ("Brick", 0.25, true)));
        Assert.Throws<CommandException>(() => f.Run("elementView.create", new JsonObject { ["element"] = plain, ["role"] = "wood.frame" }));
    }
}
