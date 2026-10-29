using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Drawing.Import;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>DXF files read and brought into views as Lapin's own drafting; lines joined into polylines.</summary>
public class DxfImportTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Section(Fixture f, double x1, double y1, double x2, double y2) =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(x1, y1), ["end"] = new JsonArray(x2, y2) })!["id"]!;

    /// <summary>A DXF text: pairs written one per line, as a file holds them.</summary>
    static string Dxf(string entities, string tables = "", string blocks = "", int units = 4) => string.Join("\n", new[]
    {
        "0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015", "9", "$INSUNITS", "70", units.ToString(), "0", "ENDSEC",
        "0", "SECTION", "2", "TABLES", tables, "0", "ENDSEC",
        "0", "SECTION", "2", "BLOCKS", blocks, "0", "ENDSEC",
        "0", "SECTION", "2", "ENTITIES", entities, "0", "ENDSEC", "0", "EOF",
    }.Where(s => s.Length > 0));

    const string Tables = """
        0
        TABLE
        2
        LTYPE
        0
        LTYPE
        2
        DASHED
        49
        0.5
        49
        -0.25
        0
        ENDTAB
        0
        TABLE
        2
        LAYER
        0
        LAYER
        2
        WALLS
        70
        1
        62
        1
        6
        DASHED
        370
        50
        0
        LAYER
        2
        DOORS
        70
        0
        62
        30
        0
        ENDTAB
        """;

    const string Blocks = """
        0
        BLOCK
        2
        DOOR
        10
        0
        20
        0
        0
        LINE
        8
        0
        10
        0
        20
        0
        11
        1000
        21
        0
        0
        ENDBLK
        """;

    const string Entities = """
        0
        LINE
        8
        WALLS
        10
        0
        20
        0
        11
        1000
        21
        0
        0
        LWPOLYLINE
        8
        WALLS
        90
        3
        70
        1
        10
        0
        20
        0
        10
        2000
        20
        0
        42
        1
        10
        2000
        20
        2000
        0
        CIRCLE
        8
        0
        62
        5
        10
        3000
        20
        3000
        40
        500
        0
        ARC
        8
        0
        10
        0
        20
        0
        40
        1000
        50
        0
        51
        90
        0
        TEXT
        8
        0
        10
        100
        20
        100
        40
        250
        1
        Angle 45%%d
        50
        0
        0
        MTEXT
        8
        0
        10
        100
        20
        2000
        40
        250
        71
        1
        1
        {\fArial|b1;Hello}\PWorld
        0
        HATCH
        8
        0
        2
        SOLID
        70
        1
        91
        1
        92
        2
        72
        0
        73
        1
        93
        3
        10
        0
        20
        0
        10
        1000
        20
        0
        10
        0
        20
        1000
        97
        0
        75
        0
        76
        1
        98
        0
        0
        HATCH
        8
        0
        2
        ANSI31
        70
        0
        91
        1
        92
        2
        72
        0
        73
        1
        93
        4
        10
        0
        20
        0
        10
        1000
        20
        0
        10
        1000
        20
        1000
        10
        0
        20
        1000
        97
        0
        75
        0
        76
        1
        52
        0
        41
        1
        77
        0
        78
        1
        53
        45
        43
        0
        44
        0
        45
        -2.2450640
        46
        2.2450640
        79
        0
        98
        0
        0
        INSERT
        8
        DOORS
        2
        DOOR
        10
        5000
        20
        5000
        50
        90
        0
        DIMENSION
        8
        0
        70
        1
        10
        0
        20
        -500
        13
        0
        23
        0
        14
        3000
        24
        0
        """;

    static JsonObject Args(string view, string dxf, Action<JsonObject>? more = null)
    {
        var a = new JsonObject { ["viewId"] = view, ["name"] = "test.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(dxf)) };
        more?.Invoke(a);
        return a;
    }

    static List<Entity> Drafted(Fixture f, string view) => Drafts.On(f.Model, view).ToList();
    static Vec2 P(JsonNode? n) => new((double)n![0]!, (double)n[1]!);

    [Fact]
    public void The_reader_reads_the_tables_the_blocks_and_every_kind_of_entity()
    {
        var doc = DxfReader.Read(Dxf(Entities, Tables, Blocks));
        Assert.Equal(4, doc.Units);
        Assert.Equal(0.001, doc.UnitFactor);
        var walls = doc.Layers["WALLS"];
        Assert.Equal("#ff0000", walls.Color);
        Assert.Equal(0.5, walls.Weight);
        Assert.True(walls.Frozen);
        Assert.False(walls.Visible);
        Assert.Equal("dashed", DxfCurves.LineType("DASHED", doc.LineTypes["DASHED"]));
        Assert.Equal("#ff8000", doc.Layers["DOORS"].Color); // ACI 30: orange
        Assert.Single(doc.Blocks["DOOR"].Entities);
        var line = Assert.IsType<DxfLine>(doc.Entities[0]);
        Assert.Equal(new Vec2(1000, 0), line.B);
        var pl = Assert.IsType<DxfPolyline>(doc.Entities[1]);
        Assert.True(pl.Closed);
        Assert.Equal(1, pl.Vertices[1].Bulge);
        Assert.Equal(500, Assert.IsType<DxfCircle>(doc.Entities[2]).R);
        Assert.Equal("#0000ff", doc.Entities[2].Color);
        Assert.Null(doc.Entities[0].Color);
        Assert.Equal(90, Assert.IsType<DxfArc>(doc.Entities[3]).EndDeg);
        Assert.Equal("Angle 45°", Assert.IsType<DxfText>(doc.Entities[4]).Value);
        var mtext = doc.Entities.OfType<DxfText>().Skip(1).Take(2).ToList();
        Assert.Equal(["Hello", "World"], mtext.Select(t => t.Value));
        Assert.True(mtext[0].At.Y > mtext[1].At.Y);
        var hatches = doc.Entities.OfType<DxfHatch>().ToList();
        Assert.True(hatches[0].Solid);
        Assert.Equal(3, hatches[0].Loops[0].Length);
        Assert.Equal("ANSI31", hatches[1].Pattern);
        Assert.Equal(45, hatches[1].Definition[0].AngleDeg);
        var ins = Assert.IsType<DxfInsert>(doc.Entities.Last(e => e is DxfInsert));
        Assert.Equal(90, ins.RotationDeg);
        var dim = Assert.IsType<DxfDimension>(doc.Entities[^1]);
        Assert.Equal(1, dim.Kind);
        Assert.Equal(new Vec2(3000, 0), dim.P14);
    }

    [Fact]
    public void Formatting_is_stripped_from_mtext_and_special_codes_become_characters()
    {
        Assert.Equal(["Hello", "World ° ± Ø 50%"], DxfReader.MTextLines("{\\fArial|b1;\\H2.5x;Hello}\\PWorld %%d %%p %%c \\L50%%%\\l"));
        Assert.Equal(["1/2 a"], DxfReader.MTextLines("\\S1^2; a"));
        Assert.Equal("ăș", DxfReader.Plain("\\U+0103\\U+0219"));
    }

    [Fact]
    public void Imported_into_a_plan_a_dxf_becomes_layers_shapes_hatches_texts_and_dimensions()
    {
        var f = F();
        var plan = Plan(f);
        var r = (JsonObject)f.Run("drafting.importDxf", Args(plan, Dxf(Entities, Tables, Blocks)))!;
        Assert.Equal("mm", (string)r["units"]!);
        Assert.False((bool)r["flipped"]!);
        // The layers, from the table: colour, pen, line type, frozen → hidden.
        var layers = Drafts.Layers(f.Model).ToDictionary(l => l.Name);
        Assert.Equal("#ff0000", layers["WALLS"].Color);
        Assert.Equal(0.5, layers["WALLS"].Weight);
        Assert.Equal("dashed", layers["WALLS"].LineType);
        Assert.False(layers["WALLS"].Visible);
        Assert.True(layers.ContainsKey("DOORS"));
        var drafted = Drafted(f, plan);
        var shapes = drafted.Where(e => e.Attrs["hatch"] == null).Select(e => (E: e, S: Drafts.ShapeOf(e)!)).ToList();
        // The line, in metres.
        var line = shapes.Single(x => x.S is Line l && l.A == new Vec2(0, 0) && l.B == new Vec2(1, 0));
        Assert.Equal("WALLS", line.E.Str("layer"));
        // The bulged polyline: a line, an arc (bulge 1: a half circle), and the closing line.
        Assert.Contains(shapes, x => x.S is Arc a && Math.Abs(a.R - 1) < 1e-9 && Math.Abs(a.C.X - 2) < 1e-9 && Math.Abs(a.C.Y - 1) < 1e-9);
        // The circle keeps its own colour; the arc its angles.
        var circle = shapes.Single(x => x.S is Circle);
        Assert.Equal("#0000ff", circle.E.Str("color"));
        Assert.Equal(0.5, ((Circle)circle.S).R, 9);
        Assert.Contains(shapes, x => x.S is Arc a && Math.Abs(a.R - 1) < 1e-9 && a.C == new Vec2(0, 0) && Math.Abs(a.End - Math.PI / 2) < 1e-9);
        // The block, exploded where it was inserted, turned, on the insert's layer.
        var door = shapes.Single(x => x.E.Str("layer") == "DOORS");
        var dl = Assert.IsType<Line>(door.S);
        Assert.Equal(5, dl.A.X, 9); Assert.Equal(5, dl.A.Y, 9); Assert.Equal(5, dl.B.X, 9); Assert.Equal(6, dl.B.Y, 9);
        // Hatches: the solid one, and ANSI31 registered in the project from the file's own definition (45°, lines 3.175 apart).
        var hatches = drafted.Where(e => e.Attrs["hatch"] != null).ToList();
        Assert.Equal(2, hatches.Count);
        Assert.Contains(hatches, h => (string?)h.Attrs["hatch"]!["pattern"] == "SOLID");
        var ansi = hatches.Single(h => (string?)h.Attrs["hatch"]!["pattern"] == "ANSI31");
        var pattern = DraftingResources.Patterns(f.Model).Single(p => p.Name == "ANSI31");
        var fam = Assert.Single(pattern.Families);
        Assert.Equal(45, fam.AngleDeg);
        Assert.Equal(0, fam.DeltaX, 4);
        Assert.Equal(3.175, fam.DeltaY, 3);
        // Scale: the pattern (mm of the drawing) in a 1:100 view, 0.001 m per unit over 0.1 m per paper mm.
        Assert.Equal(0.01, (double)ansi.Attrs["hatch"]!["scale"]!, 6);
        Assert.Equal(4, Drafts.HatchLoops(ansi)[0].Count);
        // Texts: 250 mm of model at 1:100 is 2.5 mm on paper; the MTEXT's two lines.
        var texts = f.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == "TEXT").ToList();
        Assert.Equal(3, texts.Count);
        var t = texts.Single(x => x.Str("text") == "Angle 45°");
        Assert.Equal(2.5, (double)t.Attrs["overrides"]!["height"]!, 6);
        Assert.Equal(0.1, P(t.Attrs["at"]).X, 6);
        Assert.Equal(0.1, P(t.Attrs["at"]).Y, 6);
        Assert.Contains(texts, x => x.Str("text") == "Hello");
        // The aligned dimension, native.
        var dim = Assert.Single(f.Model.OfClass(IfcClass.Annotation), e => e.PredefinedType == "DIMENSION");
        Assert.Equal([new Vec2(0, 0), new Vec2(3, 0)], Dimensions.Stations(dim).Select(s => s.P));
        Assert.Equal(new Vec2(0, -0.5), P(dim.Attrs["at"]));
        Assert.Equal(1, (int)r["dimensions"]!);
        // One undo step takes it all back.
        f.Session.Undo();
        Assert.Empty(Drafted(f, plan));
        Assert.DoesNotContain(Drafts.Layers(f.Model), l => l.Name == "WALLS");
    }

    [Fact]
    public void Units_come_from_the_file_or_are_guessed_and_can_be_overridden()
    {
        var f = F();
        var plan = Plan(f);
        var line = "0\nLINE\n8\n0\n10\n0\n20\n0\n11\n500\n21\n0";
        // Centimetres declared.
        f.Run("drafting.importDxf", Args(plan, Dxf(line, units: 5)));
        Assert.Contains(Drafted(f, plan), e => Drafts.ShapeOf(e) is Line l && Math.Abs(l.B.X - 5) < 1e-9);
        // Nothing declared, 500 units across: centimetres guessed.
        var r = (JsonObject)f.Run("drafting.importDxf", Args(plan, Dxf(line, units: 0)))!;
        Assert.True((bool)r["unitsGuessed"]!);
        Assert.Equal("cm", (string)r["units"]!);
        // Told metres, placed at (10, 0) and turned 90°.
        f.Run("drafting.importDxf", Args(plan, Dxf(line, units: 0), a => { a["units"] = "m"; a["at"] = new JsonArray(10, 0); a["rotation"] = 90; }));
        Assert.Contains(Drafted(f, plan), e => Drafts.ShapeOf(e) is Line l && Math.Abs(l.A.X - 10) < 1e-9 && Math.Abs(l.B.X - 10) < 1e-9 && Math.Abs(l.B.Y - 500) < 1e-9);
        // The summary before importing.
        var info = (JsonObject)f.Session.Query("drafting.dxfInfo", Args(plan, Dxf(Entities, Tables, Blocks)))!;
        Assert.Equal("mm", (string)info["units"]!["name"]!);
        Assert.Equal(2, (int)info["counts"]!["lines"]!); // the line, and the block's line, exploded
        Assert.Contains(info["layers"]!.AsArray(), l => (string)l!["name"]! == "WALLS" && (int)l["count"]! == 2);
        Assert.False((bool)info["view"]!["flip"]!);
    }

    [Fact]
    public void In_a_section_the_drawing_lies_on_the_cut_plane_and_mirrors_when_the_view_looks_towards_minus()
    {
        var f = F();
        var dxf = Dxf("0\nLINE\n8\n0\n10\n0\n20\n0\n11\n1000\n21\n500\n0\nARC\n8\n0\n10\n0\n20\n0\n40\n1000\n50\n0\n51\n90\n0\nTEXT\n8\n0\n10\n0\n20\n0\n40\n250\n1\nAB");
        // Drawn from left to right, a section looks to its left: +Y. The DXF's x runs along it, y is the height.
        var plus = Section(f, 0, 0, 10, 0);
        Assert.Equal(new Vec2(0, 1), Views.Look(f.Model.Get(plus)!));
        var r1 = (JsonObject)f.Run("drafting.importDxf", Args(plus, dxf))!;
        Assert.False((bool)r1["flipped"]!);
        var l1 = Assert.IsType<Line>(Drafts.ShapeOf(Drafted(f, plus).First(e => Drafts.ShapeOf(e) is Line)));
        Assert.Equal(new Vec2(1, 0.5), l1.B);
        // Drawn the other way it looks towards −Y: the drawing is mirrored about its y axis, so its left stays on the viewer's left.
        var minus = Section(f, 10, 0, 0, 0);
        Assert.Equal(new Vec2(0, -1), Views.Look(f.Model.Get(minus)!));
        var r2 = (JsonObject)f.Run("drafting.importDxf", Args(minus, dxf))!;
        Assert.True((bool)r2["flipped"]!);
        var shapes = Drafted(f, minus).Select(Drafts.ShapeOf).ToList();
        var l2 = Assert.IsType<Line>(shapes.First(s => s is Line));
        Assert.Equal(-1, l2.B.X, 9); Assert.Equal(0.5, l2.B.Y, 9);
        // The arc keeps its points (1,0)→(−1,0) and (0,1): it now runs from 90° to 180°.
        var arc = Assert.IsType<Arc>(shapes.First(s => s is Arc));
        Assert.Equal(Math.PI / 2, arc.Start, 9); Assert.Equal(Math.PI, arc.End, 9);
        // The text stays readable: not mirrored, anchored where its mirrored box is.
        var text = f.Model.OfClass(IfcClass.Annotation).Single(e => e.PredefinedType == "TEXT" && e.Str("viewId") == minus);
        Assert.Equal(0, text.Num("angle"), 6);
        Assert.True(P(text.Attrs["at"]).X < 0);
        // Elevations: east looks towards −X and mirrors; west looks towards +X and does not. The rule can be overridden.
        var east = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "east" })!["id"]!;
        Assert.True(DxfImport.FlipFor(f.Model.Get(east)!));
        var west = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "west" })!["id"]!;
        Assert.False(DxfImport.FlipFor(f.Model.Get(west)!));
        var r3 = (JsonObject)f.Run("drafting.importDxf", Args(minus, dxf, a => a["flip"] = false))!;
        Assert.False((bool)r3["flipped"]!);
        Assert.Contains(Drafted(f, minus), e => Drafts.ShapeOf(e) is Line l && l.B == new Vec2(1, 0.5));
    }

    [Fact]
    public void Join_welds_lines_into_polylines_closes_rings_and_stops_at_junctions()
    {
        var f = F();
        var plan = Plan(f);
        static JsonObject L(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = new JsonArray(x1, y1), ["b"] = new JsonArray(x2, y2) };
        // A square drawn as four lines in mixed directions, one corner 0.2 mm open; an open run of three; a T of three; a circle.
        var ids = f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shapes"] = new JsonArray(
            L(0, 0, 2, 0), L(2, 2, 2, 0), L(2, 2, 0, 2), L(0, 2.0002, 0, 0),
            L(5, 0, 6, 0), L(6, 0, 6, 1), L(7, 1, 6, 1),
            L(10, 0, 11, 0), L(11, 0, 12, 0), L(11, 0, 11, 1),
            new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(20, 0), ["r"] = 1 }) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var r = (JsonObject)f.Run("drafting.join", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) })!;
        Assert.Equal(2, (int)r["joined"]!);
        Assert.Equal(10, (int)r["pieces"]!);
        var left = Drafted(f, plan).Select(e => (e.Id, S: Drafts.ShapeOf(e)!)).ToList();
        // The square: one closed polygon of four points, kept on the first line's entity.
        var square = Assert.Single(left, x => x.S is Polyline { Closed: true });
        Assert.Equal(ids[0], square.Id);
        Assert.Equal(4, ((Polyline)square.S).Pts.Count);
        // The run: one open polyline of four points.
        var run = Assert.Single(left, x => x.S is Polyline { Closed: false });
        Assert.Equal(4, ((Polyline)run.S).Pts.Count);
        Assert.Equal(new Vec2(5, 0), ((Polyline)run.S).Pts[0]);
        Assert.Equal(new Vec2(7, 1), ((Polyline)run.S).Pts[3]);
        // The T stays three lines; the circle stays.
        Assert.Equal(3, left.Count(x => x.S is Line));
        Assert.Single(left, x => x.S is Circle);
        Assert.Equal(2 + 3 + 1, left.Count);
        // Closed is a property: an open polyline can be closed from the panel, and back.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = run.Id, ["shape"] = (new Polyline(((Polyline)run.S).Pts, true)).ToJson() });
        Assert.True(((Polyline)Drafts.ShapeOf(f.Model.Get(run.Id)!)!).Closed);
        // Only circles: nothing to join.
        Assert.Throws<CommandException>(() => f.Run("drafting.join", new JsonObject { ["ids"] = new JsonArray(ids[^1]) }));
    }

    [Fact]
    public void A_view_exported_as_dxf_comes_back_as_the_same_shapes()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-DETAIL", ["color"] = "#c0392b", ["weight"] = 0.35, ["lineType"] = "dashed" });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["layer"] = "A-DETAIL", ["shapes"] = new JsonArray(
            new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(5, 2) },
            new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(3, 0), new JsonArray(3, 3)), ["closed"] = true },
            new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(1, 1), ["r"] = 0.5 }) });
        var bytes = Views.Dxf(f.Session.Reader, f.Model.Get(plan)!);
        var other = Plan(f);
        f.Run("drafting.importDxf", Args(other, Encoding.UTF8.GetString(bytes)));
        // The export writes every shape as lines; joined, the polygon and the circle come back as closed polylines.
        var imported = Drafted(f, other).Select(e => e.Id).ToList();
        f.Run("drafting.join", new JsonObject { ["ids"] = new JsonArray(imported.Select(x => (JsonNode)x).ToArray()) });
        var shapes = Drafted(f, other).Select(e => (E: e, S: Drafts.ShapeOf(e)!)).ToList();
        Assert.Contains(shapes, x => x.S is Line l && (l.A - new Vec2(0, 0)).Length < 1e-6 && (l.B - new Vec2(5, 2)).Length < 1e-6);
        Assert.Contains(shapes, x => x.S is Polyline { Closed: true } p && p.Pts.Count == 3 && p.Pts.Any(q => (q - new Vec2(3, 3)).Length < 1e-6));
        Assert.Contains(shapes, x => x.S is Polyline { Closed: true } p && p.Pts.Count > 20);
        var layer = Drafts.Layers(f.Model).Single(l => l.Name == "A-DETAIL");
        Assert.Equal("dashed", layer.LineType);
        Assert.Equal(0.35, layer.Weight);
    }

    [Fact]
    public void A_drawing_in_map_coordinates_lies_where_the_map_puts_it()
    {
        // A cadastral line in Stereo 70, in metres (the file says nothing of its units).
        var line = "0\nLINE\n8\nCADASTRU\n10\n587010\n20\n326020\n11\n587030\n21\n326020";
        var f = F();
        var plan = Plan(f);
        var info = (JsonObject)f.Session.Query("drafting.dxfInfo", Args(plan, Dxf(line, units: 0)))!;
        Assert.True((bool)info["map"]!["likely"]!);
        Assert.False((bool)info["map"]!["georeferenced"]!);
        // The project not yet tied to the map: the drawing ties it, on its lower-left corner.
        f.Run("drafting.importDxf", Args(plan, Dxf(line, units: 0), a => a["map"] = true));
        Assert.Equal((587010.0, 326020.0), GeoRef.ToMap(f.Model, new Vec2(0, 0)));
        Assert.Equal("EPSG:3844", Location.Crs(f.Model));
        // Tied, with the project turned 90° from true north: the line runs along the plan's y.
        var g = F();
        var plan2 = Plan(g);
        g.Run("project.location", new JsonObject { ["crs"] = "EPSG:3844", ["base"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["elevation"] = 0, ["northAngle"] = 90 }, ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587000, ["northing"] = 326000 } });
        var a0 = GeoRef.ToPlan(g.Model, 587010, 326020);
        var a1 = GeoRef.ToPlan(g.Model, 587030, 326020);
        g.Run("drafting.importDxf", Args(plan2, Dxf(line, units: 0), a => { a["map"] = true; a["at"] = new JsonArray(99, 99); a["rotation"] = 45; }));
        var drawn = Views.Render(g.Session.Reader, g.Model.Get(plan2)!).Marks.SelectMany(m => m.Lines).Where(l => l.Layer != "PICK").ToList();
        Assert.Contains(drawn, l => (l.A - a0).Length < 1e-6 && (l.B - a1).Length < 1e-6 || (l.A - a1).Length < 1e-6 && (l.B - a0).Length < 1e-6);
        Assert.Equal(20, (a1 - a0).Length, 6);
        // Only into a plan, and only coordinates that are a map's.
        Assert.Throws<CommandException>(() => g.Run("drafting.importDxf", Args(Section(g, 0, 0, 10, 0), Dxf(line, units: 0), a => a["map"] = true)));
        var h = F();
        Assert.Throws<CommandException>(() => h.Run("drafting.importDxf", Args(Plan(h), Dxf("0\nLINE\n8\n0\n10\n0\n20\n0\n11\n10\n21\n0", units: 6), a => a["map"] = true)));
    }
}

/// <summary>A real DXF brought into a plan, its summary and the plan as SVG, for the eye (LAPIN_DXF_SAMPLE=/path/file.dxf and LAPIN_DXF_SVG_DIR).</summary>
public class DxfImportSvgTests
{
    [Fact]
    public void A_sample_file_as_svg_when_asked()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_DXF_SAMPLE");
        var dir = Environment.GetEnvironmentVariable("LAPIN_DXF_SVG_DIR");
        if (path == null || dir == null || !File.Exists(path)) return;
        Directory.CreateDirectory(dir);
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var info = f.Session.Query("drafting.dxfInfo", new JsonObject { ["path"] = path, ["viewId"] = plan });
        var infoMs = sw.ElapsedMilliseconds;
        sw.Restart();
        var r = f.Run("drafting.importDxf", new JsonObject { ["path"] = path, ["viewId"] = plan });
        var importMs = sw.ElapsedMilliseconds;
        sw.Restart();
        var drawing = Views.Drawing(f.Session.Reader, f.Model.Get(plan)!);
        var drawMs = sw.ElapsedMilliseconds;
        File.WriteAllText(Path.Combine(dir, "dxf-result.json"), new JsonObject { ["info"] = info, ["result"] = r, ["ms"] = new JsonObject { ["info"] = infoMs, ["import"] = importMs, ["draw"] = drawMs } }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Write(Path.Combine(dir, "dxf-plan.svg"), drawing);
    }

    static void Write(string file, JsonObject d)
    {
        var pens = d["pens"]?.AsArray() ?? [];
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        void See(double x, double y) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
        var sb = new StringBuilder();
        foreach (var pen in pens.OfType<JsonObject>())
        {
            var stroke = (string?)pen["stroke"] ?? "#000";
            var width = (Json.Number(pen["width"]) ?? 0.18) * 0.02;
            var fill = (string?)pen["fill"];
            var dash = pen["dash"] is JsonArray da && da.Count > 0 ? $" stroke-dasharray=\"{string.Join(' ', da.Select(v => (Json.Number(v) ?? 1) * 0.02))}\"" : "";
            if (pen["loops"] is JsonArray loops)
                foreach (var loop in loops.OfType<JsonArray>())
                {
                    var c = loop.Select(v => Json.Number(v) ?? 0).ToList();
                    for (var i = 0; i + 1 < c.Count; i += 2) See(c[i], c[i + 1]);
                    sb.Append($"<polygon points=\"{string.Join(' ', c.Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)))}\" fill=\"{(fill is { } fc && fc != "none" ? fc : "#d9d9d9")}\" fill-opacity=\"0.6\" stroke=\"none\"/>\n");
                }
            if (pen["segments"] is JsonArray seg)
            {
                var c = seg.Select(v => Json.Number(v) ?? 0).ToList();
                for (var i = 0; i + 3 < c.Count; i += 4)
                {
                    See(c[i], c[i + 1]); See(c[i + 2], c[i + 3]);
                    sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<line x1=\"{c[i]:0.###}\" y1=\"{c[i + 1]:0.###}\" x2=\"{c[i + 2]:0.###}\" y2=\"{c[i + 3]:0.###}\" stroke=\"{stroke}\" stroke-width=\"{width}\"{dash}/>\n");
                }
            }
        }
        foreach (var pen in pens.OfType<JsonObject>())
            foreach (var t in pen["texts"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var (x, y) = (Json.Number(t["x"]) ?? 0, Json.Number(t["y"]) ?? 0);
                See(x, y);
                var h = (Json.Number(t["height"]) ?? 2.5) * (Json.Number(d["scale"]) ?? 100) / 1000;
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<text x=\"{x:0.###}\" y=\"{-y:0.###}\" font-size=\"{h:0.###}\" transform=\"scale(1,-1) rotate({-(Json.Number(t["angle"]) ?? 0):0.##} {x:0.###} {-y:0.###})\" fill=\"#222\">{System.Security.SecurityElement.Escape((string?)t["text"] ?? "")}</text>\n");
            }
        if (minX > maxX) { minX = 0; minY = 0; maxX = 10; maxY = 10; }
        var w = maxX - minX; var h2 = maxY - minY;
        var svg = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{minX - 1:0.###} {-(maxY + 1):0.###} {w + 2:0.###} {h2 + 2:0.###}\" width=\"{(w + 2) * 70:0}\" height=\"{(h2 + 2) * 70:0}\" style=\"background:#fff\"><g transform=\"scale(1,-1)\">{sb}</g></svg>");
        File.WriteAllText(file, svg, Encoding.UTF8);
    }

}
