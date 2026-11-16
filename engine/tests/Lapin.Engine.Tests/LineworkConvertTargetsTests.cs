using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Balconies;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Convert to… as the one place that makes elements of drawn shapes (audit S12): the targets it lacked (roof, ceiling, balcony,
/// skirting, terrain, platform, road and breakline, steel members, columns and outlines), arcs kept where the target takes them
/// (a circle a round floor or ceiling) and refused with why where it does not (a roof, a balcony, a skirting); a terrain made of
/// many contours and a road of lines meeting end to end, each one element; one undo step.
/// </summary>
public sealed class LineworkConvertTargetsTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new SteelModule(), new CivilModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;
    static List<string> Draw(Fixture f, string view, string layer, params JsonObject[] shapes) =>
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["layer"] = layer, ["shapes"] = new JsonArray(shapes) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
    static JsonObject Rect(double x0, double y0, double x1, double y1) => new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Line(double x0, double y0, double x1, double y1) => new() { ["type"] = "line", ["a"] = J(x0, y0), ["b"] = J(x1, y1) };
    static JsonObject Circle(double x, double y, double r) => new() { ["type"] = "circle", ["c"] = J(x, y), ["r"] = r };
    static JsonObject Convert(string view, IEnumerable<string> ids, string converter, JsonObject? options = null) =>
        new() { ["viewId"] = view, ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["converter"] = converter, ["options"] = options ?? [] };
    static JsonObject Preview(Fixture f, JsonObject args) => (JsonObject)f.Session.Query("drafting.convertPreview", args)!;

    [Fact]
    public void Every_new_target_is_listed_for_a_plan()
    {
        var f = F();
        var plan = Plan(f);
        var ids = f.Session.Query("drafting.converters", new JsonObject { ["viewId"] = plan })!["converters"]!.AsArray().Select(c => (string)c!["id"]!).ToHashSet();
        Assert.Superset(new HashSet<string> { "roof", "ceiling", "balcony", "skirting", "terrain", "platform", "road", "civilPath", "breakline", "steelMembers", "steelColumns", "steelOutlines" }, ids);
        // A roof style's choice carries its name beside its id.
        var roof = f.Session.Query("drafting.converters", new JsonObject { ["viewId"] = plan })!["converters"]!.AsArray().Single(c => (string)c!["id"]! == "roof")!;
        Assert.Equal(roof["options"]![0]!["choices"]!.AsArray().Count, roof["options"]![0]!["choiceLabels"]!.AsArray().Count);
    }

    [Fact]
    public void A_circle_becomes_a_round_floor_and_a_round_ceiling_by_their_arcs()
    {
        var f = F();
        var plan = Plan(f);
        var floor = Draw(f, plan, "0", Circle(0, 0, 2));
        f.Run("drafting.convert", Convert(plan, floor, "floor"));
        var slab = f.Model.OfClass(IfcClass.Slab).Single();
        var rises = ArcOutline.Rises(slab.Attrs["arcs"], 4)!;
        Assert.Equal(4, rises.Length);
        Assert.All(rises, r => Assert.Equal(-2 * (1 - Math.Cos(Math.PI / 4)), r, 9));

        var ceiling = Draw(f, plan, "0", Circle(10, 0, 1.5));
        f.Run("drafting.convert", Convert(plan, ceiling, "ceiling", new JsonObject { ["height"] = 2.6 }));
        var c = f.Model.OfClass(Ceilings.Class).Single();
        Assert.Equal(2.6, c.Num("height"), 9);
        Assert.NotNull(ArcOutline.Rises(c.Attrs["arcs"], 4));
    }

    [Fact]
    public void A_closed_outline_becomes_a_roof_in_the_style_chosen_and_a_circle_is_refused_with_why()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Rect(0, 0, 10, 8), Circle(20, 0, 3));
        var style = Lapin.Architecture.Roofs.RoofCatalog.Styles[1].Id;
        var preview = Preview(f, Convert(plan, ids, "roof", new JsonObject { ["style"] = style }));
        Assert.Equal(1, (int)preview["ok"]!);
        Assert.Contains("straight edges only", (string)preview["shapes"]!.AsArray().Single(s => s!["error"] != null)!["error"]!);
        f.Run("drafting.convert", Convert(plan, ids, "roof", new JsonObject { ["style"] = style }));
        var roof = f.Model.OfClass(IfcClass.Roof).Single();
        Assert.Equal(style, roof.Str("style"));
        // The circle stays as drawn.
        Assert.NotNull(f.Model.Get(ids[1]));
    }

    [Fact]
    public void A_closed_outline_becomes_a_balcony_on_its_contour()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Rect(0, 0, 3, 1.2));
        f.Run("drafting.convert", Convert(plan, ids, "balcony"));
        var b = f.Model.OfClass(BalconyModule.Class).Single();
        Assert.Equal("polygon", b.Str("outline"));
        Assert.Equal(4, ((JsonArray)b.Attrs["contour"]!).Count);
    }

    [Fact]
    public void A_line_and_a_polyline_become_skirtings_and_an_arc_is_refused()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Line(0, 0, 4, 0),
            new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 2), J(4, 2), J(4, 5)), ["closed"] = false },
            new JsonObject { ["type"] = "arc", ["c"] = J(10, 0), ["r"] = 2, ["start"] = 0, ["end"] = 90 });
        var r = f.Run("drafting.convert", Convert(plan, ids, "skirting", new JsonObject { ["side"] = "left" }))!;
        Assert.Equal(2, (int)r["converted"]!);
        Assert.Equal(1, (int)r["left"]!);
        var sk = f.Model.OfClass(Skirtings.Class).ToList();
        Assert.Equal(2, sk.Count);
        Assert.All(sk, s => Assert.Equal("left", s.Str("side")));
    }

    [Fact]
    public void Walls_along_a_closed_shape_take_its_bottom_and_top_floors_as_Studio_s_box_did()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Rect(0, 0, 4, 3));
        var r = f.Run("drafting.convert", Convert(plan, ids, "walls", new JsonObject { ["height"] = 2.5, ["bottom"] = true, ["top"] = true, ["justify"] = "inside" }))!;
        Assert.Equal(4, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(2, f.Model.OfClass(IfcClass.Slab).Count());
        // The floors are among what was made, so they are selected after.
        Assert.Equal(6, r["ids"]!.AsArray().Count);
    }

    // ------------------------------------------------------------ site

    static string N(double v) => v.ToString(CultureInfo.InvariantCulture);
    static string Dxf(string entities) => string.Join("\n", "0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015", "9", "$INSUNITS", "70", "6", "0", "ENDSEC", "0", "SECTION", "2", "ENTITIES", entities, "0", "ENDSEC", "0", "EOF");
    static string Line3d(double x1, double y1, double z1, double x2, double y2, double z2, string layer) =>
        string.Join("\n", "0", "LINE", "8", layer, "10", N(x1), "20", N(y1), "30", N(z1), "11", N(x2), "21", N(y2), "31", N(z2));

    static List<string> Import(Fixture f, string plan, string dxf)
    {
        var before = f.Model.OfClass(IfcClass.Annotation).Select(e => e.Id).ToHashSet();
        f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["name"] = "site.dxf", ["data"] = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(dxf)) });
        return [.. f.Model.OfClass(IfcClass.Annotation).Where(e => e.Attrs["shape"] is JsonObject && !before.Contains(e.Id)).OrderBy(e => e.Name).Select(e => e.Id)];
    }

    [Fact]
    public void Contours_with_heights_become_one_terrain_and_shapes_without_need_a_height_given()
    {
        var f = F();
        var plan = Plan(f);
        var contours = Import(f, plan, Dxf(string.Join("\n", Line3d(0, 0, 100, 50, 0, 100, "CONT"), Line3d(0, 20, 101, 50, 20, 101, "CONT"), Line3d(0, 40, 102, 50, 45, 102, "CONT"))));
        var flat = Draw(f, plan, "0", Line(0, 60, 50, 60));
        var all = contours.Concat(flat).ToList();
        var preview = Preview(f, Convert(plan, all, "terrain"));
        Assert.Equal(3, (int)preview["ok"]!);
        Assert.Contains("no heights", (string)preview["shapes"]!.AsArray().Single(s => s!["error"] != null)!["error"]!);
        // One terrain: the group's first shape carries the step, the others are joined to it.
        Assert.Equal(1, (int)preview["steps"]!);
        Assert.Equal(2, preview["shapes"]!.AsArray().Count(s => s!["joined"] != null));

        f.Run("drafting.convert", Convert(plan, all, "terrain", new JsonObject { ["elevation"] = 103 }));
        var t = f.Model.OfClass(IfcClass.GeographicElement).Single();
        var z = ((JsonArray)t.Attrs["points"]!).Select(p => Json.Number(p!.AsArray()[2])!.Value).ToList();
        Assert.Equal(8, z.Count);
        Assert.Equal(100, z.Min(), 9);
        Assert.Equal(103, z.Max(), 9);
        Assert.All(all, id => Assert.Null(f.Model.Get(id)));
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.GeographicElement));
        Assert.All(all, id => Assert.NotNull(f.Model.Get(id)));
    }

    [Fact]
    public void A_platform_needs_a_terrain_and_is_levelled_at_the_elevation_given()
    {
        var f = F();
        var plan = Plan(f);
        var pad = Draw(f, plan, "0", Rect(10, 10, 20, 18));
        Assert.Contains("make the terrain first", (string)Preview(f, Convert(plan, pad, "platform"))["shapes"]![0]!["error"]!);
        f.Run("civil.addTerrain", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0, 10), new JsonArray(50, 0, 10), new JsonArray(50, 50, 12), new JsonArray(0, 50, 12)) });
        f.Run("drafting.convert", Convert(plan, pad, "platform", new JsonObject { ["elevation"] = 10.5 }));
        var p = f.Model.OfClass(IfcClass.EarthworksCut).Single();
        Assert.Equal(10.5, p.Num("elevation"), 9);
    }

    [Fact]
    public void Lines_meeting_end_to_end_become_one_road_and_one_apart_its_own()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Line(0, 0, 30, 0), Line(30, 0, 60, 10), Line(0, 50, 40, 50));
        var preview = Preview(f, Convert(plan, ids, "road"));
        Assert.Equal(2, (int)preview["steps"]!);
        Assert.Equal(ids[0], (string)preview["shapes"]!.AsArray().Single(s => (string)s!["id"]! == ids[1])!["joined"]!);
        f.Run("drafting.convert", Convert(plan, ids, "road"));
        Assert.Equal(2, f.Model.OfClass(IfcClass.Pavement).Count());
    }

    [Fact]
    public void A_breakline_goes_into_the_terrain_under_it()
    {
        var f = F();
        var plan = Plan(f);
        var line = Draw(f, plan, "0", Line(5, 5, 40, 30));
        Assert.Contains("make the terrain first", (string)Preview(f, Convert(plan, line, "breakline"))["shapes"]![0]!["error"]!);
        f.Run("civil.addTerrain", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0, 10), new JsonArray(50, 0, 10), new JsonArray(50, 50, 12), new JsonArray(0, 50, 12)) });
        f.Run("drafting.convert", Convert(plan, line, "breakline"));
        Assert.Single((JsonArray)f.Model.OfClass(IfcClass.GeographicElement).Single().Attrs["breaklines"]!);
    }

    // ------------------------------------------------------------ steel

    [Fact]
    public void Each_straight_piece_becomes_a_steel_member_of_the_profile_chosen_and_an_arc_is_refused()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(6, 0), J(6, 4)), ["closed"] = false },
            new JsonObject { ["type"] = "arc", ["c"] = J(20, 0), ["r"] = 3, ["start"] = 0, ["end"] = 90 });
        var r = f.Run("drafting.convert", Convert(plan, ids, "steelMembers", new JsonObject { ["profile"] = "HEA 200", ["role"] = "PURLIN" }))!;
        Assert.Equal(1, (int)r["left"]!);
        var members = f.Model.OfClass(SteelModule.Member).ToList();
        Assert.Equal(2, members.Count);
        Assert.All(members, m => Assert.Equal("PURLIN", m.PredefinedType));
        // One type of that section, made once and shared.
        var type = Assert.Single(f.Model.OfClass(SteelModule.MemberType));
        Assert.Equal("HEA 200", Section.From(type.Attrs["section"])!.Name);
    }

    [Fact]
    public void Circles_become_steel_columns_of_the_profile_chosen_sharing_one_type()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Circle(0, 0, 0.1), Circle(5, 0, 0.1));
        f.Run("drafting.convert", Convert(plan, ids, "steelColumns", new JsonObject { ["profile"] = "HEB 240" }));
        Assert.Equal(2, f.Model.OfClass(IfcClass.Column).Count());
        var type = Assert.Single(f.Model.OfClass(IfcClass.ColumnType));
        Assert.Equal("HEB 240", Section.From(type.Attrs["section"])!.Name);
    }

    [Fact]
    public void A_band_drawn_on_a_plan_becomes_a_member_through_the_converter_as_Convert_to_member_made_it()
    {
        var f = F();
        var plan = Plan(f);
        // 200 mm wide (an IPE 200's flanges are 100: an HEA/HEB 200 is 200 wide) and 5 m long; and a square, not a band.
        var ids = Draw(f, plan, "0", Rect(0, 0, 5, 0.2), Rect(10, 0, 11, 1));
        var preview = Preview(f, Convert(plan, ids, "steelOutlines"));
        Assert.Equal(1, (int)preview["ok"]!);
        f.Run("drafting.convert", Convert(plan, ids, "steelOutlines"));
        var m = Assert.Single(f.Model.OfClass(SteelModule.Member));
        Assert.Equal(0.2, Section.From(f.Model.Get(f.Model.RelatingOf(m.Id, IfcClass.RelDefinesByType)!.Id)!.Attrs["section"])!.Width, 6);
        Assert.Null(f.Model.Get(ids[0]));
        Assert.NotNull(f.Model.Get(ids[1]));
    }

    [Fact]
    public void Shapes_drawn_on_the_level_s_default_plan_convert_from_its_plan_view_too()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, $"plan:{f.Storey}", "0", Rect(0, 0, 4, 3));
        var r = f.Run("drafting.convert", Convert(plan, ids, "floor"))!;
        Assert.Equal(1, (int)r["converted"]!);
        Assert.Single(f.Model.OfClass(IfcClass.Slab));
    }
}
