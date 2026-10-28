using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// Rooms and their colour schemes (docs/rooms.md): a click inside walls finds
/// the room's outline (less the columns standing in it) and keeps it up with
/// the walls; a point nothing closes round is refused; a drawn outline is
/// kept; the room measures its area, perimeter and volume; plans fill it in
/// its scheme's colour and hatch under the walls, with its stamp over them; a
/// legend lists what the view uses; IFC gets an IfcSpace aggregated into its
/// storey, and a foreign IfcSpace comes back as a room.
/// </summary>
public sealed class RoomTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-rooms").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static Fixture New() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    /// <summary>A 5 × 4 m box of 20 cm walls on their axes (the room inside is 4.80 × 3.80).</summary>
    static (Fixture F, string[] Walls) Box()
    {
        var f = New();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        string[] walls = [f.Wall(0, 0, 5, 0, type), f.Wall(5, 0, 5, 4, type), f.Wall(5, 4, 0, 4, type), f.Wall(0, 4, 0, 0, type)];
        return (f, walls);
    }

    static string Room(Fixture f, double x, double y, JsonObject? more = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addRoom", a)!["id"]!;
    }

    static Rooms.Outline Outline(Fixture f, string id) => Rooms.OfGeometry(f.Session.Geometry(id)) ?? throw new InvalidOperationException("no outline");
    static double Qty(Fixture f, string id, string name) => f.Session.Quantities(id).Single().Quantities.Single(q => q.Name == name).Value;

    [Fact]
    public void A_click_inside_four_walls_finds_the_room_between_their_faces()
    {
        var (f, _) = Box();
        var id = Room(f, 2, 2);
        var o = Outline(f, id);
        Assert.Equal(4.8 * 3.8, o.NetArea, 6);
        Assert.Equal(2 * (4.8 + 3.8), o.Perimeter, 6);
        Assert.Empty(o.Islands);
        Assert.False(o.Lost);
        var room = f.Model.Get(id)!;
        Assert.True(room.Bool("auto"));
        Assert.Equal("Room", room.Name);
        Assert.Equal("001", room.Str("number"));
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, id)!.Id);
        // Its quantities: IFC's base quantities of a space, the storey's height.
        Assert.Equal(4.8 * 3.8, Qty(f, id, "NetFloorArea"), 6);
        Assert.Equal(3.0, Qty(f, id, "Height"), 6);
        Assert.Equal(4.8 * 3.8 * 3, Qty(f, id, "NetVolume"), 6);
        // Its body: the outline up the storey's height.
        var (min, max) = f.Bounds(id);
        Assert.Equal(0.1, min.X, 6); Assert.Equal(4.9, max.X, 6); Assert.Equal(0, min.Z, 6); Assert.Equal(3, max.Z, 6);
        // The next one on the storey takes the next number.
        Assert.Equal("002", Rooms.NextNumber(f.Model, f.Storey));
    }

    [Fact]
    public void A_column_standing_in_the_room_is_an_island_its_area_leaves_out()
    {
        var (f, _) = Box();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 40", ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.4, ["depth"] = 0.4 } })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 2.5, ["y"] = 2 });
        var id = Room(f, 1, 1);
        var o = Outline(f, id);
        Assert.Single(o.Islands);
        Assert.Equal(4.8 * 3.8 - 0.16, o.NetArea, 6);
        Assert.Equal(4.8 * 3.8, o.GrossArea, 6);
        Assert.Equal(4.8 * 3.8, Qty(f, id, "GrossFloorArea"), 6);
        Assert.False(o.Holds(new Vec2(2.5, 2)));
        Assert.True(o.Holds(new Vec2(1, 1)));
    }

    [Fact]
    public void A_point_nothing_closes_round_or_in_a_wall_or_in_a_room_is_refused()
    {
        var f = New();
        f.Wall(0, 0, 5, 0); f.Wall(5, 0, 5, 4); f.Wall(5, 4, 0, 4); // open on the left
        var e = Assert.Throws<CommandException>(() => Room(f, 2, 2));
        Assert.Contains("Nothing closes round", e.Message);
        Assert.Empty(f.Model.OfClass(IfcClass.Space));
        var (g, _) = Box();
        Assert.Contains("inside a wall", Assert.Throws<CommandException>(() => Room(g, 2.5, 0)).Message);
        Room(g, 2, 2);
        Assert.Contains("room here already", Assert.Throws<CommandException>(() => Room(g, 3, 3)).Message);
    }

    [Fact]
    public void A_room_found_from_its_walls_follows_them_and_keeps_its_outline_when_they_open()
    {
        var (f, walls) = Box();
        var id = Room(f, 2, 2);
        // The right wall moved out by a metre: the room grows with it (its corners follow, joined).
        f.Run("architecture.updateWall", new JsonObject { ["id"] = walls[1], ["x1"] = 6, ["x2"] = 6 });
        Assert.Equal(5.8 * 3.8, Outline(f, id).NetArea, 6);
        // A wall taken away: nothing closes round its point; it keeps the outline it stored, and says so.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(walls[3]) });
        var lost = Outline(f, id);
        Assert.True(lost.Lost);
        Assert.Equal(4.8 * 3.8, lost.NetArea, 6);
        Assert.Contains(f.Session.Geometry(id)!.Diagnostics!, d => d.Contains("keeps the outline"));
        // Update rooms stores what a room finds; one that finds nothing is counted as lost.
        var r = f.Run("architecture.updateRooms", new JsonObject())!;
        Assert.Equal(1, (int)r["lost"]!);
    }

    [Fact]
    public void A_drawn_room_keeps_its_outline_and_a_click_moves_its_point()
    {
        var (f, _) = Box();
        var id = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 10, ["y1"] = 0, ["x2"] = 13, ["y2"] = 2, ["name"] = "Terrace", ["function"] = "Balcony" })!["id"]!;
        var room = f.Model.Get(id)!;
        Assert.False(room.Bool("auto"));
        Assert.Equal("Terrace", room.Name);
        Assert.Equal(6, Outline(f, id).NetArea, 6);
        // Found again round a point inside the walls.
        f.Run("architecture.updateRoom", new JsonObject { ["id"] = id, ["x"] = 1, ["y"] = 1 });
        Assert.True(f.Model.Get(id)!.Bool("auto"));
        Assert.Equal(4.8 * 3.8, Outline(f, id).NetArea, 6);
        // The stamp moved, then put back.
        f.Run("architecture.updateRoom", new JsonObject { ["id"] = id, ["stampX"] = 1, ["stampY"] = 3 });
        Assert.Equal(new Vec2(1, 3), Rooms.StampAt(f.Model.Get(id)!));
        f.Run("architecture.updateRoom", new JsonObject { ["id"] = id, ["stamp"] = null });
        Assert.Null(Rooms.StampAt(f.Model.Get(id)!));
        // A move carries its point and its outline.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(0.5, 0) });
        Assert.Equal(1.5, Rooms.Seed(f.Model.Get(id)!)!.Value.X, 9);
    }

    [Fact]
    public void Free_space_is_found_round_a_point_between_overlapping_and_touching_areas()
    {
        // Two walls crossing in an L, two touching at a T, a column inside: the room is the rectangle inside, less the column.
        IReadOnlyList<IReadOnlyList<Vec2>> R(double x0, double y0, double x1, double y1) => [[new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]];
        var fs = FreeSpace.Of([R(-0.1, -0.1, 5.1, 0.1), R(4.9, -0.1, 5.1, 4.1), R(0, 3.9, 5.1, 4.1), R(-0.1, 0, 0.1, 4), R(2, 2, 2.4, 2.4)]);
        var r = fs.Around(new Vec2(1, 1))!;
        Assert.Equal(4.8 * 3.8 - 0.16, r.Area, 6);
        Assert.Single(r.Islands);
        Assert.Null(fs.Around(new Vec2(2.2, 2.2)));
        Assert.Null(fs.Around(new Vec2(9, 9)));
        Assert.True(fs.InMaterial(new Vec2(2.5, 0)));
    }

    // ─── Colour schemes ─────────────────────────────────────────────────

    static (Fixture F, string Kitchen, string Bedroom, string Plan) Flat()
    {
        var (f, _) = Box();
        var type = f.Model.OfClass(IfcClass.WallType).First().Id;
        f.Wall(2.5, 0, 2.5, 4, type);
        var kitchen = Room(f, 1, 1, new JsonObject { ["name"] = "Kitchen", ["function"] = "Kitchen" });
        var bedroom = Room(f, 4, 1, new JsonObject { ["name"] = "Bedroom", ["function"] = "Bedroom" });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (f, kitchen, bedroom, plan);
    }

    static Views.Rendered Render(Fixture f, string view) => Views.Render(new Q(f), Views.Find(f.Model, view)!);

    [Fact]
    public void A_plan_fills_its_rooms_in_the_schemes_colours_under_the_walls()
    {
        var (f, kitchen, bedroom, plan) = Flat();
        // No scheme: no fills, the stamps alone.
        var bare = Render(f, plan);
        Assert.DoesNotContain(bare.Pens, p => p.Layer == "ROOM-FILL");
        Assert.Contains(bare.Pens.SelectMany(p => p.Texts), t => t.Text == "Kitchen");
        Assert.Contains(bare.Pens.SelectMany(p => p.Texts), t => t.Text == "8.74 m²");
        f.Run("architecture.viewColorScheme", new JsonObject { ["viewId"] = plan, ["schemeId"] = "by-function" });
        var r = Render(f, plan);
        var fills = r.Pens.Where(p => p.Layer == "ROOM-FILL").ToList();
        Assert.Contains(fills, p => p.Fill == "#f4c7a1" && p.Loops.Count == 1); // the kitchen
        Assert.Contains(fills, p => p.Fill == "#a7c7e7"); // the bedroom
        // Under the model: every room fill comes before the walls' cut lines, the stamps after them.
        var firstCut = r.Pens.FindIndex(p => p.Layer == PenLayer.Cut);
        Assert.True(firstCut > 0);
        Assert.All(fills, p => Assert.True(r.Pens.IndexOf(p) < firstCut));
        Assert.True(r.Pens.FindIndex(p => p.Texts.Any(t => t.Text == "Kitchen")) > firstCut);
        // The stamp picks its room.
        Assert.Contains(r.Marks, m => m.Owner == kitchen && m.Lines.Any(l => l.Layer == "PICK"));
        // A room's own colour wins over its scheme's.
        f.Run("element.update", new JsonObject { ["id"] = bedroom, ["attrs"] = new JsonObject { ["fillColor"] = "#123456" } });
        Assert.Contains(Render(f, plan).Pens, p => p.Layer == "ROOM-FILL" && p.Fill == "#123456");
    }

    [Fact]
    public void A_scheme_entry_can_hatch_and_new_values_get_the_palettes_colours()
    {
        var (f, kitchen, _, plan) = Flat();
        var id = (string)f.Run("architecture.createColorScheme", new JsonObject { ["name"] = "Departments", ["by"] = "department" })!["id"]!;
        f.Run("element.update", new JsonObject { ["id"] = kitchen, ["attrs"] = new JsonObject { ["department"] = "Day" } });
        var added = f.Run("architecture.fillColorScheme", new JsonObject { ["id"] = id })!;
        Assert.Equal(1, (int)added["added"]!);
        var scheme = ColorSchemes.Find(f.Model, id)!;
        Assert.Equal("Day", scheme.Entries.Single().Value);
        Assert.Contains(scheme.Entries.Single().Color, ColorSchemes.Palette);
        f.Run("architecture.setColorSchemeEntry", new JsonObject { ["id"] = id, ["value"] = "Day", ["color"] = null, ["hatch"] = "LINES45", ["hatchColor"] = "#336699" });
        f.Run("architecture.viewColorScheme", new JsonObject { ["viewId"] = plan, ["schemeId"] = id });
        var r = Render(f, plan);
        Assert.DoesNotContain(r.Pens, p => p.Layer == "ROOM-FILL");
        var hatch = Assert.Single(r.Pens, p => p.Layer == "ROOM-HATCH");
        Assert.Equal("#336699", hatch.Stroke);
        Assert.NotEmpty(hatch.Segments);
        Assert.True(r.Pens.IndexOf(hatch) < r.Pens.FindIndex(p => p.Layer == PenLayer.Cut));
        // The defaults stay in the project once one scheme is added; deleting the view's scheme leaves it without one.
        Assert.Equal(3, ColorSchemes.Of(f.Model).Count);
        f.Run("architecture.deleteColorScheme", new JsonObject { ["id"] = id });
        Assert.Null(f.Model.Get(plan)!.Str("colorScheme"));
    }

    [Fact]
    public void A_legend_lists_the_entries_the_views_rooms_use()
    {
        var (f, _, _, plan) = Flat();
        f.Run("architecture.viewColorScheme", new JsonObject { ["viewId"] = plan, ["schemeId"] = "by-function" });
        var legend = (string)f.Run("architecture.addColorLegend", new JsonObject { ["viewId"] = plan, ["x"] = 8, ["y"] = 4, ["showArea"] = true })!["id"]!;
        var mark = Assert.Single(Render(f, plan).Marks, m => m.Owner == legend);
        var texts = mark.Texts.Select(t => t.Text.Text).ToList();
        Assert.Equal("By function", texts[0]);
        // In the scheme's order (bedroom before kitchen), with their areas; the functions no room has are not listed.
        Assert.Equal(["Bedroom", "Kitchen"], texts.Skip(1).Select(t => t.Split("   ")[0]));
        Assert.All(texts.Skip(1), t => Assert.EndsWith("m²", t));
        Assert.Equal(2, mark.Fills.Count);
        // It moves with the annotations.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(legend), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal(9, (double)f.Model.Get(legend)!.Attrs["at"]![0]!, 9);
    }

    [Fact]
    public void Rooms_stay_out_of_the_3d_view_and_out_of_the_plans_cut()
    {
        var (f, kitchen, _, plan) = Flat();
        var hidden = f.Session.Catalog.Query("view.hidden3d")!(new Q(f), new JsonObject())!.AsArray().Select(x => (string)x!).ToList();
        Assert.Contains(kitchen, hidden);
        var d = Views.Generate(new Q(f), Views.Find(f.Model, plan)!).Drawing!;
        Assert.DoesNotContain(d.Cuts, c => c.Element == kitchen);
        // Shown when the 3D's graphics say so.
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["category"] = "spaces", ["value"] = new JsonObject { ["hidden"] = false } });
        hidden = f.Session.Catalog.Query("view.hidden3d")!(new Q(f), new JsonObject())!.AsArray().Select(x => (string)x!).ToList();
        Assert.DoesNotContain(kitchen, hidden);
    }

    [Fact]
    public void The_room_schedule_lists_every_room_with_its_area_and_a_total()
    {
        var (f, _, _, _) = Flat();
        var grid = new RoomSchedule().Grid(new Q(f));
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal("Total", grid.Rows[^1][0]);
        Assert.Equal((2.3 * 3.8 + 2.3 * 3.8).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), grid.Rows[^1][5]);
    }

    // ─── IFC ────────────────────────────────────────────────────────────

    [Fact]
    public void A_room_exports_as_an_ifcspace_aggregated_into_its_storey_and_comes_back()
    {
        var (f, kitchen, _, _) = Flat();
        f.Run("element.update", new JsonObject { ["id"] = kitchen, ["attrs"] = new JsonObject { ["number"] = "K1", ["department"] = "Day" } });
        var path = Path.Combine(_dir, "rooms.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(File.ReadAllBytes(path));
        var spaces = file.OfType("IFCSPACE").ToList();
        Assert.Equal(2, spaces.Count);
        var k = spaces.Single(s => (string?)s[7] == "Kitchen");
        Assert.Equal("K1", (string?)k[2]);
        Assert.Equal("Kitchen", (string?)k[4]);
        Assert.Equal("SPACE", ((StepEnum)k[9]!).Value);
        // Aggregated into the storey, not contained in it.
        var storey = file.OfType("IFCBUILDINGSTOREY").Single();
        Assert.Contains(file.OfType("IFCRELAGGREGATES"), r => ((StepRef)r[4]!).Id == storey.Id && ((List<object?>)r[5]!).Contains(new StepRef(k.Id)));
        Assert.DoesNotContain(file.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"), r => ((List<object?>)r[4]!).Contains(new StepRef(k.Id)));
        // An extruded body, its quantities, Pset_SpaceCommon.
        Assert.NotEmpty(file.OfType("IFCEXTRUDEDAREASOLID"));
        var qto = file.OfType("IFCELEMENTQUANTITY").Single(q => (string?)q[2] == "Qto_SpaceBaseQuantities" && file.OfType("IFCRELDEFINESBYPROPERTIES").Any(r => ((List<object?>)r[4]!).Contains(new StepRef(k.Id)) && ((StepRef)r[5]!).Id == q.Id));
        var area = ((List<object?>)qto[5]!).Select(x => file.Get(x)!).Single(x => (string?)x[0] == "NetFloorArea");
        Assert.Equal(2.3 * 3.8, IfcModel.Num(area[3]), 6);
        Assert.Contains(file.OfType("IFCPROPERTYSET"), p => (string?)p[2] == "Pset_SpaceCommon");

        // Back into a new project: the same rooms, natively, with their names and numbers.
        var g = New();
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, path, null, out _);
        var back = g.Model.Get(kitchen)!;
        Assert.Equal(IfcClass.Space, back.Class);
        Assert.Equal("Kitchen", back.Name);
        Assert.Equal("K1", back.Str("number"));
        Assert.Equal("Day", back.Str("department"));
        Assert.Equal(2.3 * 3.8, Rooms.OfGeometry(g.Session.Geometry(kitchen))!.NetArea, 6);
    }

    [Fact]
    public void A_foreign_ifcspace_becomes_a_room_with_its_outline_name_number_and_function()
    {
        var x = new Step();
        string G(string key) => S(Guid("room-test", key));
        var origin = x.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
        var world = x.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
        var ctx = x.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
        var mm = x.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
        var project = x.Add("IFCPROJECT", G("project"), Null, S("Foreign"), Null, Null, Null, Null, L([ctx]), x.Add("IFCUNITASSIGNMENT", L([mm])));
        var sitePl = x.Add("IFCLOCALPLACEMENT", Null, world);
        var site = x.Add("IFCSITE", G("site"), Null, S("Site"), Null, Null, sitePl, Null, Null, E("ELEMENT"), Null, Null, Null, Null, Null);
        var bPl = x.Add("IFCLOCALPLACEMENT", sitePl, world);
        var building = x.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, bPl, Null, Null, E("ELEMENT"), Null, Null, Null);
        var sPl = x.Add("IFCLOCALPLACEMENT", bPl, world);
        var storey = x.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Ground"), Null, Null, sPl, Null, Null, E("ELEMENT"), "0.");
        x.Add("IFCRELAGGREGATES", G("a1"), Null, Null, Null, project, L([site]));
        x.Add("IFCRELAGGREGATES", G("a2"), Null, Null, Null, site, L([building]));
        x.Add("IFCRELAGGREGATES", G("a3"), Null, Null, Null, building, L([storey]));
        // An L-shaped space, 6 × 4 less a 2 × 2 corner, 2.7 m high, as an extruded polyline in millimetres.
        var pts = new[] { "(0.,0.)", "(6000.,0.)", "(6000.,2000.)", "(4000.,2000.)", "(4000.,4000.)", "(0.,4000.)", "(0.,0.)" }.Select(p => x.Add("IFCCARTESIANPOINT", p));
        var profile = x.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, x.Add("IFCPOLYLINE", L(pts)));
        var solid = x.Add("IFCEXTRUDEDAREASOLID", profile, x.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null), x.Add("IFCDIRECTION", "(0.,0.,1.)"), "2700.");
        var shape = x.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([x.Add("IFCSHAPEREPRESENTATION", ctx, S("Body"), S("SweptSolid"), L([solid]))]));
        var space = x.Add("IFCSPACE", G("space"), Null, S("0.12"), Null, S("Living"), x.Add("IFCLOCALPLACEMENT", sPl, world), shape, S("Living room"), E("ELEMENT"), E("INTERNAL"), Null);
        x.Add("IFCRELAGGREGATES", G("a4"), Null, Null, Null, storey, L([space]));
        var path = Path.Combine(_dir, "foreign-space.ifc");
        x.Write(path, "foreign-space.ifc", "ViewDefinition [ReferenceView_V1.2]");

        var f = New();
        f.Session.New(plain: true);
        var result = IfcImport.Run(f.Session, path, null, out _);
        var room = Assert.Single(f.Model.OfClass(IfcClass.Space));
        Assert.Equal(1, result.Recognized);
        Assert.Equal(Guid("room-test", "space"), room.Id);
        Assert.Equal("Living room", room.Name);
        Assert.Equal("0.12", room.Str("number"));
        Assert.Equal("Living", room.Str("function"));
        Assert.False(room.Bool("auto"));
        Assert.Equal(20, Rooms.OfGeometry(f.Session.Geometry(room.Id))!.NetArea, 6);
        Assert.Equal(2.7, room.Num("height"), 6);
    }

    /// <summary>
    /// A file as Revit or ArchiCAD write one (IFC2X3, millimetres, a storey at
    /// +3.00, the spaces aggregated into it): an extrusion with Pset_SpaceCommon,
    /// Revit's identity set and base quantities; a faceted B-rep whose floor
    /// has nearly collinear points (as ArchiCAD writes them); a space with only
    /// its FootPrint curve; one raised 45 cm on its storey; one whose Name is
    /// its LongName; one with no shape at all, only its properties.
    /// </summary>
    string ForeignSpaces()
    {
        var x = new Step();
        string G(string key) => S(Guid("room-test", key));
        var origin = x.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
        var world = x.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
        var ctx = x.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
        var mm = x.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
        var project = x.Add("IFCPROJECT", G("project"), Null, S("Foreign"), Null, Null, Null, Null, L([ctx]), x.Add("IFCUNITASSIGNMENT", L([mm])));
        var sitePl = x.Add("IFCLOCALPLACEMENT", Null, world);
        var site = x.Add("IFCSITE", G("site"), Null, S("Site"), Null, Null, sitePl, Null, Null, E("ELEMENT"), Null, Null, Null, Null, Null);
        var bPl = x.Add("IFCLOCALPLACEMENT", sitePl, world);
        var building = x.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, bPl, Null, Null, E("ELEMENT"), Null, Null, Null);
        var sPl = x.Add("IFCLOCALPLACEMENT", bPl, x.Add("IFCAXIS2PLACEMENT3D", x.Add("IFCCARTESIANPOINT", "(0.,0.,3000.)"), Null, Null));
        var storey = x.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Level 1"), Null, Null, sPl, Null, Null, E("ELEMENT"), "3000.");
        x.Add("IFCRELAGGREGATES", G("a1"), Null, Null, Null, project, L([site]));
        x.Add("IFCRELAGGREGATES", G("a2"), Null, Null, Null, site, L([building]));
        x.Add("IFCRELAGGREGATES", G("a3"), Null, Null, Null, building, L([storey]));
        string Pt(double px, double py, double? pz = null) => x.Add("IFCCARTESIANPOINT", pz is { } z ? L([R(px), R(py), R(z)]) : L([R(px), R(py)]));
        string Rep(string id, string kind, string item) => x.Add("IFCSHAPEREPRESENTATION", ctx, S(id), S(kind), L([item]));
        string Shape(params string[] reps) => x.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps));
        string Extrusion((double X, double Y)[] pts, double depth, double z = 0) =>
            x.Add("IFCEXTRUDEDAREASOLID", x.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, x.Add("IFCPOLYLINE", L(pts.Append(pts[0]).Select(p => Pt(p.X, p.Y))))),
                x.Add("IFCAXIS2PLACEMENT3D", Pt(0, 0, z), Null, Null), x.Add("IFCDIRECTION", "(0.,0.,1.)"), R(depth));
        var spaces = new List<string>();
        string Space(string key, string? name, string? longName, string? shape)
        {
            // IFC2X3: …, LongName, CompositionType, InteriorOrExteriorSpace, ElevationWithFlooring.
            var id = x.Add("IFCSPACE", G(key), Null, S(name), Null, Null, x.Add("IFCLOCALPLACEMENT", sPl, world), shape ?? Null, S(longName), E("ELEMENT"), E("INTERNAL"), Null);
            spaces.Add(id);
            return id;
        }
        string Label(string n, string v) => x.Add("IFCPROPERTYSINGLEVALUE", S(n), Null, Typed("IFCLABEL", S(v)), Null);
        void Define(string obj, string key, string set) => x.Add("IFCRELDEFINESBYPROPERTIES", G(key), Null, Null, Null, L([obj]), set);

        // 101 Office: a 5 × 4 m extrusion, 2.60 m high.
        var office = Space("office", "101", "Office", Shape(Rep("Body", "SweptSolid", Extrusion([(0, 0), (5000, 0), (5000, 4000), (0, 4000)], 2600))));
        Define(office, "office-common", x.Add("IFCPROPERTYSET", G("office-common-set"), Null, S("Pset_SpaceCommon"), Null, L([Label("Reference", "101"), Label("Category", "Office work"), Label("FloorCovering", "Oak parquet")])));
        Define(office, "office-revit", x.Add("IFCPROPERTYSET", G("office-revit-set"), Null, S("PSet_Revit_Identity Data"), Null, L([Label("Department", "Administration"), Label("Occupant", "ACME")])));
        Define(office, "office-qto", x.Add("IFCELEMENTQUANTITY", G("office-qto-set"), Null, S("BaseQuantities"), Null, Null, L([x.Add("IFCQUANTITYAREA", S("NetFloorArea"), Null, Null, "20.")])));
        // 102 Corridor: a faceted B-rep 10.20 × 3.00 m, its long side in nearly collinear points, no FootPrint.
        (double X, double Y)[] floor = [(0, 6000), (10200, 6000), (10200, 9000.007), (5760.5, 8999.99999), (1410.25763, 8999.99314), (1410.25762, 8999.80921), (0, 8999.69526)];
        string Face(IEnumerable<string> pts) => x.Add("IFCFACE", L([x.Add("IFCFACEOUTERBOUND", x.Add("IFCPOLYLOOP", L(pts)), True)]));
        var bottom = floor.Select(p => Pt(p.X, p.Y, 0)).ToArray();
        var top = floor.Select(p => Pt(p.X, p.Y, 2480)).ToArray();
        var faces = new List<string> { Face(bottom.Reverse()), Face(top) };
        for (var i = 0; i < floor.Length; i++) faces.Add(Face([bottom[i], bottom[(i + 1) % floor.Length], top[(i + 1) % floor.Length], top[i]]));
        Space("corridor", "102", "Corridor", Shape(Rep("Body", "Brep", x.Add("IFCFACETEDBREP", x.Add("IFCCLOSEDSHELL", L(faces))))));
        // 103 Store: only its FootPrint, an L of 7 m².
        var footprint = x.Add("IFCGEOMETRICCURVESET", L([x.Add("IFCPOLYLINE", L(new (double X, double Y)[] { (6000, 0), (9000, 0), (9000, 2000), (7000, 2000), (7000, 3000), (6000, 3000), (6000, 0) }.Select(p => Pt(p.X, p.Y))))]));
        Space("store", "103", "Store", Shape(Rep("FootPrint", "GeometricCurveSet", footprint)));
        // 104 Gallery: raised 45 cm on its storey, 2.10 m high.
        Space("gallery", "104", "Gallery", Shape(Rep("Body", "SweptSolid", Extrusion([(12000, 0), (16000, 0), (16000, 2000), (12000, 2000)], 2100, 450))));
        // Hall: its Name is its LongName.
        Space("hall", "Hall", "Hall", Shape(Rep("Body", "SweptSolid", Extrusion([(12000, 3000), (15000, 3000), (15000, 5000), (12000, 5000)], 2600))));
        // 105 Shaft: no shape, its properties only.
        var shaft = Space("shaft", "105", "Shaft", null);
        Define(shaft, "shaft-common", x.Add("IFCPROPERTYSET", G("shaft-common-set"), Null, S("Pset_SpaceCommon"), Null, L([Label("Reference", "105"), Label("Category", "Technical")])));
        x.Add("IFCRELAGGREGATES", G("a4"), Null, Null, Null, storey, L(spaces));
        var path = Path.Combine(_dir, "revit-spaces.ifc");
        x.Write(path, "revit-spaces.ifc", "ViewDefinition [CoordinationView_V2.0]", "IFC2X3");
        return path;
    }

    [Fact]
    public void Foreign_spaces_come_in_as_rooms_with_their_outlines_names_heights_and_properties()
    {
        var f = New();
        f.Session.New(plain: true);
        var result = IfcImport.Run(f.Session, ForeignSpaces(), null, out _);
        Entity Room(string key) => f.Model.Get(Guid("room-test", key)) ?? throw new Xunit.Sdk.XunitException($"no room {key}");
        double Area(string key) => Rooms.OfGeometry(f.Session.Geometry(Room(key).Id))?.NetArea ?? double.NaN;
        Assert.Equal(6, f.Model.OfClass(IfcClass.Space).Count());
        Assert.Equal(6, result.Recognized);

        // The office: its number and name, its function, department and floor finish from its sets, its outline and height.
        var office = Room("office");
        Assert.Equal("Office", office.Name);
        Assert.Equal("101", office.Str("number"));
        Assert.Equal("Office work", office.Str("function"));
        Assert.Equal("Administration", office.Str("department"));
        Assert.Equal("Oak parquet", office.Str("finishFloor"));
        Assert.Equal("Level 1", Spatial.StoreyOf(f.Model, office.Id)!.Name);
        Assert.Equal(20, Area("office"), 6);
        Assert.Equal(2.6, Rooms.HeightOf(office, f.Model), 6);
        Assert.Null(office.NumOrNull("baseOffset"));
        // Every set it had, kept for the properties panel.
        var sets = (JsonObject)office.Attrs["imported"]!["properties"]!;
        Assert.Equal("ACME", (string?)sets["PSet_Revit_Identity Data"]!["Occupant"]);
        Assert.Equal("20", (string?)sets["BaseQuantities"]!["NetFloorArea"]);
        Assert.Equal("101", (string?)sets["Pset_SpaceCommon"]!["Reference"]);
        // A colour scheme by department reads it.
        Assert.Equal("Administration", ColorSchemes.ValueOf(office, "department"));

        // The B-rep corridor: the outline of its floor, nearly collinear points and all.
        Assert.Equal(10.2 * 3, Area("corridor"), 2);
        Assert.Equal(2.48, Rooms.HeightOf(Room("corridor"), f.Model), 6);
        // The store: its FootPrint curve; the storey's height.
        Assert.Equal(7, Area("store"), 6);
        // The gallery stands 45 cm above its storey, 2.10 m high.
        var gallery = Room("gallery");
        Assert.Equal(0.45, gallery.Num("baseOffset"), 6);
        Assert.Equal(2.1, Rooms.HeightOf(gallery, f.Model), 6);
        var (min, max) = f.Bounds(gallery.Id);
        Assert.Equal(3.45, min.Z, 6); Assert.Equal(5.55, max.Z, 6);
        // The hall's Name is its name, not a number: it takes the storey's next one.
        var hall = Room("hall");
        Assert.Equal("Hall", hall.Name);
        Assert.NotEqual("Hall", hall.Str("number"));
        Assert.False(string.IsNullOrEmpty(hall.Str("number")));
        // The shaft has no shape: a room without an outline, with its properties, listed in the schedule, the import says so.
        var shaft = Room("shaft");
        Assert.Equal("Shaft", shaft.Name);
        Assert.Equal("105", shaft.Str("number"));
        Assert.Equal("Technical", shaft.Str("function"));
        Assert.True(double.IsNaN(Area("shaft")));
        Assert.Contains(new RoomSchedule().Grid(new Q(f)).Rows, r => r[2] == "Shaft" && r[5] == "");
        Assert.Contains(result.Notes, n => n.Contains("without an outline", StringComparison.Ordinal));
        // Where the rooms are, for whoever looks for them in 3D.
        Assert.Contains(result.Notes, n => n.StartsWith("6 room(s) from the file's spaces", StringComparison.Ordinal));
        // Given an outline afterwards (its properties' Draw outline), it is measured like any other.
        f.Run("architecture.updateRoom", new JsonObject { ["id"] = shaft.Id, ["points"] = new JsonArray(new JsonArray(20, 0), new JsonArray(22, 0), new JsonArray(22, 1), new JsonArray(20, 1)) });
        Assert.Equal(2, Area("shaft"), 6);
        Assert.Equal("Technical", Room("shaft").Str("function"));

        // Out again: the gallery's IfcSpace starts 45 cm above its storey.
        var path = Path.Combine(_dir, "rooms-again.ifc");
        IfcExport.Write(f.Session, path);
        var g = New();
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, path, null, out _);
        // On its own storey (the project's ground floor is another), raised as it was.
        Assert.Equal("Level 1", Spatial.StoreyOf(g.Model, gallery.Id)!.Name);
        Assert.Equal(0.45, g.Model.Get(gallery.Id)!.Num("baseOffset"), 6);
        Assert.Equal(3.45, g.Bounds(gallery.Id).Min.Z, 6);
    }
}

/// <summary>A real file's spaces, when one is given (LAPIN_IFC_SAMPLE=/path/to/file.ifc): how many came in as rooms, with what, for the eye.</summary>
public class RoomSampleTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void A_sample_files_spaces_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_SAMPLE");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var file = StepFile.Read(File.ReadAllBytes(path));
        var spaces = file.OfType("IFCSPACE").ToList();
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule());
        f.Session.New(plain: true);
        var r = IfcImport.Run(f.Session, path, null, out _);
        var rooms = f.Model.OfClass(IfcClass.Space).ToList();
        var outlined = rooms.Count(x => Rooms.OfGeometry(f.Session.Geometry(x.Id)) != null);
        output.WriteLine($"{Path.GetFileName(path)}: {spaces.Count} IfcSpace, {rooms.Count} rooms, {outlined} with an outline");
        // Each on the storey the file puts it in (aggregated, or contained).
        var fileStorey = new Dictionary<int, string?>();
        foreach (var rel in file.OfType("IFCRELAGGREGATES").Concat(file.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE")))
        {
            var agg = rel.Type == "IFCRELAGGREGATES";
            if (file.Get(rel[agg ? 4 : 5]) is not { Type: "IFCBUILDINGSTOREY" } st) continue;
            foreach (var o in (List<object?>)rel[agg ? 5 : 4]!) if (o is StepRef sr) fileStorey[sr.Id] = (string?)st[2];
        }
        var onTheirs = spaces.Count(sp => f.Model.Get((string)sp[0]!) is { } rm && fileStorey.GetValueOrDefault(sp.Id) is { } want && Spatial.StoreyOf(f.Model, rm.Id)?.Name == want);
        output.WriteLine($"  on the file's storey: {onTheirs}; by storey: {string.Join(", ", rooms.GroupBy(x => Spatial.StoreyOf(f.Model, x.Id)?.Name).Select(g => $"{g.Key} {g.Count()}"))}");
        foreach (var n in r.Notes) output.WriteLine("  note: " + n);
        foreach (var x in rooms.Take(Environment.GetEnvironmentVariable("LAPIN_ROOMS_ALL") != null ? int.MaxValue : 4))
        {
            var props = x.Attrs["imported"]?["properties"] as JsonObject;
            output.WriteLine($"  {x.Str("number")} | {x.Name} | fn={x.Str("function")} | dep={x.Str("department")} | floor={x.Str("finishFloor")} | h={x.NumOrNull("height")} | off={x.NumOrNull("baseOffset")} | storey={Spatial.StoreyOf(f.Model, x.Id)?.Name} | area={Rooms.OfGeometry(f.Session.Geometry(x.Id))?.NetArea:0.00} | sets={string.Join(",", props?.Select(p => $"{p.Key}({(p.Value as JsonObject)?.Count})") ?? [])}");
        }
        foreach (var s in spaces.Where(s => f.Model.Get((string)s[0]!) == null).Take(10))
            output.WriteLine($"  missing #{s.Id} {s[2]} {s[7]}: {new IfcModel(file).Describe(s.Id).ToJsonString()[..Math.Min(300, new IfcModel(file).Describe(s.Id).ToJsonString().Length)]}");
    }
}
