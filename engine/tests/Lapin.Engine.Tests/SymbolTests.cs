using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// 2D symbols of the user's own (docs/plan-user-extensibility.md §3): the formulas of live text, the definitions and Lapin's
/// built-ins, instances on views and sheets with their attributes, flipped either way with their texts kept readable, drafting
/// saved as a symbol and exploded back, carried by move, rotate and mirror, the library file with its choices, one undo step each.
/// </summary>
public sealed class SymbolTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f, double scale = 100)
    {
        return (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = scale })!["id"]!;
    }

    static JsonObject Def(string scale, params JsonNode[] items) => new() { ["scale"] = scale, ["category"] = "other", ["items"] = new JsonArray(items) };
    static JsonObject LineItem(double x0, double y0, double x1, double y1) => new() { ["type"] = "line", ["a"] = J(x0, y0), ["b"] = J(x1, y1), ["w"] = 0.25 };
    static JsonObject TextItem(double x, double y, string text, string align = "start") => new() { ["type"] = "text", ["at"] = J(x, y), ["text"] = text, ["h"] = 2.5, ["align"] = align };

    static string Create(Fixture f, JsonObject def, string name = "Mine") => (string)f.Run("symbols.create", new JsonObject { ["name"] = name, ["def"] = def })!["id"]!;
    static string Place(Fixture f, string view, string symbol, double x, double y, double angle = 0, JsonObject? more = null)
    {
        var a = new JsonObject { ["viewId"] = view, ["symbolId"] = symbol, ["at"] = J(x, y), ["angle"] = angle };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("drafting.placeSymbol", a)!["id"]!;
    }

    static ViewMark Mark(Fixture f, string view, string id) => Views.Render(f.Session.Reader, f.Model.Get(view)!).Marks.Single(m => m.Owner == id);
    static List<(Vec2 A, Vec2 B)> Drawn(ViewMark mk) => mk.Lines.Where(l => l.Layer != "PICK").Select(l => (l.A, l.B)).ToList();
    static void Near(Vec2 want, Vec2 got, double tol = 1e-6) { Assert.Equal(want.X, got.X, tol); Assert.Equal(want.Y, got.Y, tol); }

    // ------------------------------------------------------------ formulas

    [Fact]
    public void Formulas_read_numbers_operators_functions_and_names_and_refuse_what_does_not_read()
    {
        double E(string s, Func<string, double?>? n = null) => Expr.Eval(s, n ?? (_ => null));
        Assert.Equal(7, E("1 + 2 * 3"));
        Assert.Equal(9, E("(1 + 2) * 3"));
        Assert.Equal(512, E("2 ^ 3 ^ 2"));
        Assert.Equal(-4, E("-2 * 2"));
        Assert.Equal(3, E("max(1, 3, 2)"));
        Assert.Equal(1.5, E("min(4; 1.5)"));
        Assert.Equal(2.35, E("round(2.345; 2)"), 9);
        Assert.Equal(2.35, E("round(2.33; 0.05)"), 9);
        Assert.Equal(1, E("3 > 2 and not (1 == 2)"));
        Assert.Equal(20, E("if(attr.w > 1, attr.w * 2, 0)", n => n == "attr.w" ? 10 : null));
        Assert.Equal(1, E("sin(90)"), 9);
        Assert.Equal(1, E("7 % 3"));
        Assert.Throws<ExprException>(() => E("1 +"));
        Assert.Throws<ExprException>(() => E("1 / 0"));
        Assert.Throws<ExprException>(() => E("foo(1)"));
        Assert.Throws<ExprException>(() => E("attr.missing"));
        Assert.Throws<ExprException>(() => E("(1 + 2"));
        Assert.Throws<ExprException>(() => E(new string('(', 200) + "1" + new string(')', 200)));
        Assert.Equal(["attr.w", "view.scaleNumber"], Expr.Names("attr.w * 1000 / view.scaleNumber"));
    }

    [Fact]
    public void Live_text_evaluates_formulas_with_a_format_then_fills_tokens()
    {
        (bool, string?) R(string k) => k switch { "attr.l" => (true, "2,5"), "attr.name" => (true, "Beam"), "level.z" => (true, "3.2"), _ => (false, null) };
        var unknown = new List<string>();
        Assert.Equal("Beam: 5.00 m", LiveText.Fill("{attr.name}: {= attr.l * 2 | 0.00} m", R, unknown));
        Assert.Equal("+3.20", LiveText.Fill("{= level.z | +0.00;-0.00}", R));
        Assert.Equal("<attr.x * 2> <attr.y>", LiveText.Fill("{= attr.x * 2} {attr.y}", R, unknown));
        Assert.Equal(2, unknown.Count);
    }

    // ------------------------------------------------------------ definitions

    [Fact]
    public void Lapin_s_symbols_are_listed_read_only_and_a_copy_can_be_changed()
    {
        var f = F();
        var list = f.Session.Query("symbols.list", new JsonObject { ["previews"] = true })!["items"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(list, s => (string)s["id"]! == BuiltinSymbols.Prefix + "north-arrow" && (bool)s["builtin"]!);
        Assert.All(list, s => Assert.True(s["preview"]!["pens"]!.AsArray().Count > 0));
        // Every built-in is a definition as the user's are: it reads back the same.
        foreach (var b in BuiltinSymbols.All) Assert.True(JsonNode.DeepEquals(b.Def.ToJson(), SymbolDef.Parse(b.Def.ToJson()).ToJson()));
        Assert.Throws<CommandException>(() => f.Run("symbols.update", new JsonObject { ["id"] = BuiltinSymbols.Prefix + "north-arrow", ["name"] = "X" }));
        var copy = (string)f.Run("symbols.create", new JsonObject { ["from"] = BuiltinSymbols.Prefix + "callout" })!["id"]!;
        Assert.Equal("Callout bubble copy", f.Model.Get(copy)!.Name);
        f.Run("symbols.update", new JsonObject { ["id"] = copy, ["name"] = "Detail" });
        Assert.Equal("Detail", f.Model.Get(copy)!.Name);
        // A name is never a built-in's nor another's.
        Assert.Equal("North arrow 2", f.Model.Get(Create(f, Def("annotation", LineItem(0, 0, 1, 0)), "North arrow"))!.Name);
    }

    [Fact]
    public void A_definition_that_is_not_a_symbol_s_is_refused_with_why()
    {
        var f = F();
        Assert.Throws<CommandException>(() => Create(f, new JsonObject { ["scale"] = "annotation", ["items"] = new JsonArray() }));
        Assert.Throws<CommandException>(() => Create(f, Def("annotation", new JsonObject { ["type"] = "blob" })));
        Assert.Throws<CommandException>(() => Create(f, Def("paper", LineItem(0, 0, 1, 0))));
        Assert.Throws<CommandException>(() => Create(f, Def("annotation", new JsonObject { ["type"] = "circle", ["c"] = J(0, 0), ["r"] = -1 })));
        Assert.Throws<CommandException>(() => Create(f, Def("annotation", LineItem(0, 0, 1e9, 0))));
    }

    // ------------------------------------------------------------ instances

    [Fact]
    public void An_annotation_symbol_is_paper_mm_and_a_model_one_the_building_s_mm()
    {
        var f = F();
        var plan = Plan(f, 50);
        var paper = Create(f, Def("annotation", LineItem(0, 0, 10, 0)), "Paper");
        var model = Create(f, Def("model", LineItem(0, 0, 1000, 0)), "Model");
        var a = Place(f, plan, paper, 2, 3);
        var b = Place(f, plan, model, 2, 3);
        // 10 paper mm at 1:50 is 0.5 m; 1000 mm of the building is 1 m at any scale.
        var la = Drawn(Mark(f, plan, a)).Single();
        Near(new(2, 3), la.A); Near(new(2.5, 3), la.B);
        var lb = Drawn(Mark(f, plan, b)).Single();
        Near(new(3, 3), lb.B);
        // At 1:100 the paper symbol is 1 m long, the model one still 1 m.
        var plan2 = Plan(f, 100);
        Near(new(3, 3), Drawn(Mark(f, plan2, Place(f, plan2, paper, 2, 3))).Single().B);
        Near(new(3, 3), Drawn(Mark(f, plan2, Place(f, plan2, model, 2, 3))).Single().B);
        a = Place(f, plan2, paper, 2, 3);
        plan = plan2;
        // Its own scale doubles it.
        f.Run("drafting.setSymbol", new JsonObject { ["id"] = a, ["scale"] = 2 });
        Near(new(4, 3), Drawn(Mark(f, plan, a)).Single().B);
    }

    [Fact]
    public void Texts_fill_the_instance_s_attributes_the_view_s_and_formulas()
    {
        var f = F();
        var plan = Plan(f);
        var def = Def("annotation", TextItem(0, 0, "D{attr.no} · {view.scale} · {= attr.w * 2 | 0.0} · {level.name}"));
        def["attributes"] = new JsonArray(new JsonObject { ["key"] = "no", ["label"] = "Number", ["default"] = "1" }, new JsonObject { ["key"] = "w", ["default"] = "0.6" });
        var s = Create(f, def);
        var i = Place(f, plan, s, 0, 0);
        var storey = f.Model.Get(f.Storey)!.Name;
        Assert.Equal($"D1 · 1:100 · 1.2 · {storey}", Mark(f, plan, i).Texts.Single().Text.Text);
        f.Run("drafting.setSymbol", new JsonObject { ["id"] = i, ["values"] = new JsonObject { ["no"] = "7", ["w"] = "1.5" } });
        Assert.Equal($"D7 · 1:100 · 3.0 · {storey}", Mark(f, plan, i).Texts.Single().Text.Text);
        // A value given back as null takes the default again.
        f.Run("drafting.setSymbol", new JsonObject { ["id"] = i, ["values"] = new JsonObject { ["no"] = null } });
        Assert.StartsWith("D1 ·", Mark(f, plan, i).Texts.Single().Text.Text);
    }

    [Fact]
    public void Flipped_either_way_the_lines_turn_over_and_the_texts_stay_readable_where_they_were()
    {
        var f = F();
        var plan = Plan(f);
        // A line 10 mm along +x and a text starting 2 mm along it, 1 mm up.
        var s = Create(f, Def("annotation", LineItem(0, 0, 10, 0), TextItem(2, 1, "AB")));
        var i = Place(f, plan, s, 0, 0);
        var t0 = Mark(f, plan, i).Texts.Single().Text;
        Assert.Equal(0, t0.Angle, 6); Assert.Equal("start", t0.Anchor); Near(new(0.2, 0.1), t0.At);

        // Left ↔ right: the line runs to −x, the text ends where it started, reading left to right.
        f.Run("drafting.flipSymbol", new JsonObject { ["id"] = i, ["axis"] = "x" });
        var mk = Mark(f, plan, i);
        Near(new(-1, 0), Drawn(mk).Single().B);
        var tx = mk.Texts.Single().Text;
        Assert.Equal(0, tx.Angle, 6); Assert.Equal("end", tx.Anchor); Near(new(-0.2, 0.1), tx.At);

        // Up ↔ down as well (both: a half turn): the text below the line, still readable, covering the same place.
        f.Run("drafting.flipSymbol", new JsonObject { ["id"] = i, ["axis"] = "y" });
        var ty = Mark(f, plan, i).Texts.Single().Text;
        Assert.Equal(0, ty.Angle, 6); Assert.Equal("end", ty.Anchor);
        // Its letters rise from −0.1 − 0.25 (the text's height, 2.5 mm at 1:100) up to −0.1.
        Near(new(-0.2, -0.1 - 0.25), ty.At);

        // Only up ↔ down: the line stays along +x, the text hangs under it, read left to right.
        f.Run("drafting.flipSymbol", new JsonObject { ["id"] = i, ["axis"] = "x" });
        var t2 = Mark(f, plan, i).Texts.Single().Text;
        Assert.Equal(0, t2.Angle, 6); Assert.Equal("start", t2.Anchor); Near(new(0.2, -0.35), t2.At);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(135, -45)]
    [InlineData(180, 0)]
    [InlineData(270, 90)]
    [InlineData(300, -60)]
    public void Turned_any_way_a_text_reads_left_to_right_or_bottom_to_top(double angle, double reads)
    {
        var f = F();
        var plan = Plan(f);
        var s = Create(f, Def("annotation", TextItem(0, 0, "N")));
        var t = Mark(f, plan, Place(f, plan, s, 0, 0, angle)).Texts.Single().Text;
        Assert.Equal(reads, t.Angle, 6);
    }

    [Fact]
    public void Drafting_saved_as_a_symbol_becomes_one_instance_drawing_the_same_and_explodes_back()
    {
        var f = F();
        var plan = Plan(f);
        var ids = f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan,
            ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(1, 1), ["b"] = J(2, 1) }, new JsonObject { ["type"] = "circle", ["c"] = J(1, 1), ["r"] = 0.3 }),
        })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var text = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = plan, ["at"] = J(1.2, 1.4), ["text"] = "{attr.mark}" })!["id"]!;
        var before = Views.Render(f.Session.Reader, f.Model.Get(plan)!).Marks.Where(m => ids.Contains(m.Owner!)).SelectMany(Drawn).ToList();

        var made = f.Run("drafting.toSymbol", new JsonObject
        {
            ["viewId"] = plan, ["ids"] = new JsonArray([.. ids.Append(text).Select(x => (JsonNode)x)]), ["base"] = J(1, 1), ["name"] = "Pile", ["category"] = "other",
        })!;
        var symbol = (string)made["symbolId"]!;
        var instance = (string)made["instanceId"]!;
        // The drafting is gone, one instance stands where it was, the text's token became an attribute.
        Assert.All(ids.Append(text), id => Assert.Null(f.Model.Get(id)));
        var def = Symbols.DefOf(f.Model, symbol)!;
        Assert.Equal("mark", def.Attributes.Single().Key);
        Assert.Equal(3, def.Items.Count);
        var after = Drawn(Mark(f, plan, instance));
        // The line is the same line; the circle's chords lie on the same circle.
        Assert.Contains(after, l => (l.A - new Vec2(1, 1)).Length < 1e-6 && (l.B - new Vec2(2, 1)).Length < 1e-6);
        Assert.All(after.Where(l => (l.B - new Vec2(2, 1)).Length > 1e-6), l => Assert.Equal(0.3, (l.A - new Vec2(1, 1)).Length, 6));

        // One undo step brings the drafting back.
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(ids[0]));
        Assert.Null(f.Model.Get(instance));
        f.Session.Redo();

        // Exploded: a line, a circle (still a circle) and the text as it reads.
        f.Run("drafting.setSymbol", new JsonObject { ["id"] = instance, ["values"] = new JsonObject { ["mark"] = "P1" } });
        var parts = f.Run("drafting.explodeSymbol", new JsonObject { ["id"] = instance })!["ids"]!.AsArray().Select(x => f.Model.Get((string)x!)!).ToList();
        Assert.Null(f.Model.Get(instance));
        Assert.Contains(parts, e => Drafts.ShapeOf(e) is Line l && (l.A - new Vec2(1, 1)).Length < 1e-6);
        Assert.Contains(parts, e => Drafts.ShapeOf(e) is Circle c && Math.Abs(c.R - 0.3) < 1e-9);
        Assert.Contains(parts, e => e.PredefinedType == "TEXT" && e.Str("text") == "P1");
    }

    [Fact]
    public void A_symbol_moves_turns_and_mirrors_with_the_drafting_tools()
    {
        var f = F();
        var plan = Plan(f);
        var s = Create(f, Def("annotation", LineItem(0, 0, 10, 0)));
        var i = Place(f, plan, s, 1, 0);
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(i), ["op"] = "move", ["by"] = J(1, 1) });
        Near(new(2, 1), Symbols.Pose(f.Model.Get(i)!).At);
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(i), ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 90 });
        var p = Symbols.Pose(f.Model.Get(i)!);
        Near(new(-1, 2), p.At); Assert.Equal(90, p.Angle, 6);
        // Mirrored across the y axis: its +x runs the other way and it is turned over; it draws as the mirror image.
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(i), ["op"] = "mirror", ["a"] = J(0, 0), ["b"] = J(0, 1) });
        var m = Symbols.Pose(f.Model.Get(i)!);
        Near(new(1, 2), m.At);
        var line = Drawn(Mark(f, plan, i)).Single();
        Near(new(1, 2), line.A); Near(new(1, 3), line.B);
        var copies = f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(i), ["op"] = "copy", ["by"] = J(5, 0) })!["ids"]!.AsArray();
        Assert.Equal(s, f.Model.Get((string)copies[0]!)!.Str("symbolId"));
    }

    [Fact]
    public void A_symbol_on_a_sheet_draws_in_paper_mm_with_the_sheet_s_tokens()
    {
        var f = F();
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        var s = Create(f, Def("annotation", LineItem(0, 0, 20, 0), TextItem(0, 2, "{sheet.number} {attr.note}")));
        f.Run("drafting.placeSymbol", new JsonObject { ["sheetId"] = sheet, ["symbolId"] = s, ["at"] = J(50, 40), ["values"] = new JsonObject { ["note"] = "ok" } });
        var pens = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens;
        Assert.Contains(pens.SelectMany(p => p.Texts), t => t.Text == "A-101 ok");
        // y down on paper: the line from (50, 40) to (70, 40), the text above it at y 38.
        Assert.Contains(pens.SelectMany(p => p.Segments), sg => (sg.A - new Vec2(50, 40)).Length < 1e-6 && (sg.B - new Vec2(70, 40)).Length < 1e-6);
        Assert.Contains(pens.SelectMany(p => p.Texts), t => (t.At - new Vec2(50, 38)).Length < 1e-6);
    }

    [Fact]
    public void A_symbol_placed_is_not_deleted_unless_its_instances_go_too()
    {
        var f = F();
        var plan = Plan(f);
        var s = Create(f, Def("annotation", LineItem(0, 0, 1, 0)));
        var i = Place(f, plan, s, 0, 0);
        Assert.Throws<CommandException>(() => f.Run("symbols.delete", new JsonObject { ["id"] = s }));
        Assert.Equal(1, (int)f.Run("symbols.delete", new JsonObject { ["id"] = s, ["instances"] = true })!["removed"]!);
        Assert.Null(f.Model.Get(i));
    }

    // ------------------------------------------------------------ the library file

    [Fact]
    public void A_library_file_brings_symbols_in_with_a_choice_for_each_that_differs()
    {
        var f = F();
        var a = Create(f, Def("annotation", LineItem(0, 0, 1, 0)), "Arrow");
        var b = Create(f, Def("annotation", LineItem(0, 0, 2, 0)), "Bubble");
        var text = (string)f.Session.Query("symbols.export", new JsonObject { ["ids"] = new JsonArray(a, b), ["name"] = "Office" })!["text"]!;
        Assert.Contains("lapin-symbols", text);

        var g = F();
        Create(g, Def("annotation", LineItem(0, 0, 1, 0)), "Arrow");   // the same
        var mine = Create(g, Def("annotation", LineItem(0, 0, 9, 0)), "Bubble"); // differs
        var read = g.Session.Query("symbols.readPack", new JsonObject { ["text"] = text })!["symbols"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.True((bool)read.Single(r => (string)r["name"]! == "Arrow")["same"]!);
        Assert.False((bool)read.Single(r => (string)r["name"]! == "Bubble")["same"]!);

        // Keep both: the file's comes in beside the project's.
        var r1 = g.Run("symbols.importFrom", new JsonObject { ["text"] = text })!;
        Assert.Equal(1, (int)r1["kept"]!); Assert.Equal(1, (int)r1["added"]!);
        Assert.Contains(g.Model.OfClass(Symbols.Class), e => e.Name == "Bubble 2");
        g.Session.Undo();
        // Update: the project's takes the file's definition and keeps its id.
        g.Run("symbols.importFrom", new JsonObject { ["text"] = text, ["choices"] = new JsonObject { ["Bubble"] = "update" } });
        Assert.Equal(2, Symbols.DefOf(g.Model, mine)!.Items[0]!["b"]![0]!.GetValue<double>());
        // A file that is not a symbol library is refused whole.
        Assert.Throws<CommandException>(() => g.Run("symbols.importFrom", new JsonObject { ["text"] = "{\"format\":\"lapin-type-pack\"}" }));
    }
}
