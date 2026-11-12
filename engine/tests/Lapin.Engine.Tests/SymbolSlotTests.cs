using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The marks views draw as slots a symbol takes (docs/plan-user-extensibility.md §3.3): none chosen, Lapin's drawing exactly as
/// before; a section's head, a level's marker, a grid's bubbles and a view's title on a sheet drawn by the symbol chosen, the
/// mark's own over the project's, reading the mark's tokens (its name, its number and sheet where it is placed).
/// </summary>
public sealed class SymbolSlotTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;
    static string Section(Fixture f, string name = "A-A") =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = name, ["start"] = J(-1, 2), ["end"] = J(6, 2), ["scale"] = 50 })!["id"]!;

    static List<ViewMark> Marks(Fixture f, string view) => Views.Render(f.Session.Reader, f.Model.Get(view)!).Marks;

    static string Signature(IEnumerable<ViewMark> marks)
    {
        var sb = new StringBuilder();
        foreach (var mk in marks)
        {
            foreach (var l in mk.Lines) sb.Append($"L{l.A.X:0.######},{l.A.Y:0.######},{l.B.X:0.######},{l.B.Y:0.######},{l.Width};");
            foreach (var (t, _) in mk.Texts) sb.Append($"T{t.Text}@{t.At.X:0.######},{t.At.Y:0.######};");
            foreach (var (loops, _) in mk.Fills) sb.Append($"F{loops.Sum(l => l.Count)};");
        }
        return sb.ToString();
    }

    [Fact]
    public void With_no_symbol_chosen_every_mark_draws_as_before_and_clearing_a_slot_brings_it_back()
    {
        var f = F();
        f.Wall(0, 4, 5, 4);
        var plan = Plan(f);
        var section = Section(f);
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = -1, ["x2"] = 0, ["y2"] = 4 });
        var planBefore = Signature(Marks(f, plan).Where(m => m.Owner == section || m.Owner?.Contains('#') == true));
        var sectionBefore = Signature(Marks(f, section));
        // A symbol made, the slots set and cleared again: the same drawing.
        foreach (var slot in new[] { "sectionHead", "levelHead", "gridBubble" })
        {
            f.Run("symbols.setSlot", new JsonObject { ["slot"] = slot, ["symbolId"] = BuiltinSymbols.Prefix + "callout" });
            f.Run("symbols.setSlot", new JsonObject { ["slot"] = slot, ["symbolId"] = null });
        }
        Assert.Equal(planBefore, Signature(Marks(f, plan).Where(m => m.Owner == section || m.Owner?.Contains('#') == true)));
        Assert.Equal(sectionBefore, Signature(Marks(f, section)));
        Assert.Throws<CommandException>(() => f.Run("symbols.setSlot", new JsonObject { ["slot"] = "nope", ["symbolId"] = null }));
        Assert.Throws<CommandException>(() => f.Run("symbols.setSlot", new JsonObject { ["slot"] = "sectionHead", ["symbolId"] = "missing" }));
    }

    [Fact]
    public void A_section_head_symbol_reads_the_section_s_number_and_sheet_and_the_section_s_own_comes_first()
    {
        var f = F();
        var plan = Plan(f);
        var section = Section(f);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Sections", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = section, ["x"] = 150, ["y"] = 120 });
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "sectionHead", ["symbolId"] = BuiltinSymbols.Prefix + "section-head" });
        var head = Marks(f, plan).Single(m => m.Owner == section);
        // Two heads, each with the section's number on its sheet and the sheet's number; the section's name is not written beside them.
        Assert.Equal(2, head.Texts.Count(t => t.Text.Text == "1"));
        Assert.Equal(2, head.Texts.Count(t => t.Text.Text == "A-101"));
        Assert.DoesNotContain(head.Texts, t => t.Text.Text == "A-A");
        // Texts upright whichever way the section looks.
        Assert.All(head.Texts, t => Assert.InRange(t.Text.Angle, -90 + 1e-6, 90 + 1e-6));

        // The section's own symbol over the project's.
        var mine = (string)f.Run("symbols.create", new JsonObject
        {
            ["name"] = "Arrow head",
            ["def"] = new JsonObject { ["scale"] = "annotation", ["items"] = new JsonArray(new JsonObject { ["type"] = "text", ["at"] = J(0, 3), ["text"] = "§{mark.name}", ["h"] = 2.5 }) },
        })!["id"]!;
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "sectionHead", ["symbolId"] = mine, ["ownerIds"] = new JsonArray(section) });
        Assert.Equal(2, Marks(f, plan).Single(m => m.Owner == section).Texts.Count(t => t.Text.Text == "§A-A"));
        var slots = f.Session.Query("symbols.slots", new JsonObject { ["ownerId"] = section })!["slots"]!.AsArray().OfType<JsonObject>().Single(s => (string)s["id"]! == "sectionHead");
        Assert.Equal(mine, (string)slots["own"]!);
        Assert.Equal(BuiltinSymbols.Prefix + "section-head", (string)slots["project"]!);
    }

    [Fact]
    public void A_level_marker_symbol_writes_each_level_s_name_and_elevation()
    {
        var f = F();
        f.Wall(0, 4, 5, 4);
        var section = Section(f);
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "levelHead", ["symbolId"] = BuiltinSymbols.Prefix + "level-half" });
        var storey = f.Model.Get(f.Storey)!;
        var mark = Marks(f, section).Single(m => m.Owner == storey.Id);
        Assert.Contains(mark.Texts, t => t.Text.Text == $"{storey.Name} {Symbols.Level(storey.Num("elevation"))}");
        Assert.NotEmpty(mark.Fills);
    }

    [Fact]
    public void A_grid_bubble_symbol_carries_each_axis_label()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 6, ["tag"] = "7" });
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "gridBubble", ["symbolId"] = BuiltinSymbols.Prefix + "grid-square" });
        var axis = Marks(f, plan).Single(m => m.Owner?.EndsWith("#7", StringComparison.Ordinal) == true);
        Assert.Equal(2, axis.Texts.Count(t => t.Text.Text == "7"));
    }

    [Fact]
    public void A_view_title_symbol_replaces_the_title_on_sheets()
    {
        var f = F();
        var plan = Plan(f);
        f.Wall(0, 0, 5, 0);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["x"] = 150, ["y"] = 120 });
        var title = (string)f.Run("symbols.create", new JsonObject
        {
            ["name"] = "Title",
            ["def"] = new JsonObject { ["scale"] = "annotation", ["items"] = new JsonArray(new JsonObject { ["type"] = "text", ["at"] = J(0, -6), ["text"] = "{mark.number} — {view.name} ({view.scale})", ["h"] = 3 }) },
        })!["id"]!;
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "viewTitle", ["symbolId"] = title });
        var texts = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        var name = f.Model.Get(plan)!.Name;
        Assert.Contains($"1 — {name} (1:100)", texts);
        Assert.DoesNotContain(name, texts);
    }

    [Fact]
    public void A_spot_level_reads_its_height_from_the_section_and_the_level_in_a_plan()
    {
        var f = F();
        f.Wall(0, 4, 5, 4);
        var section = Section(f);
        var storey = f.Model.Get(f.Storey)!;
        // The level's line in the section: where the storey's elevation is drawn.
        var y = Marks(f, section).Single(m => m.Owner == storey.Id).Lines[0].A.Y;
        string Spot(string view, double x, double yy)
        {
            var id = (string)f.Run("drafting.placeSymbol", new JsonObject { ["viewId"] = view, ["symbolId"] = BuiltinSymbols.Prefix + "spot-level", ["at"] = J(x, yy) })!["id"]!;
            return Marks(f, view).Single(m => m.Owner == id).Texts.Single().Text.Text;
        }
        Assert.Equal(Symbols.Level(storey.Num("elevation")), Spot(section, 1, y));
        Assert.Equal(Symbols.Level(storey.Num("elevation") + 3.2), Spot(section, 1, y + 3.2));
        Assert.Equal(Symbols.Level(storey.Num("elevation")), Spot(Plan(f), 1, 1));
    }

    [Fact]
    public void A_section_head_symbol_points_the_way_the_section_looks_at_its_marker_s_size()
    {
        var f = F();
        var plan = Plan(f);
        var section = Section(f);
        var before = Marks(f, plan).Single(m => m.Owner == section);
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "sectionHead", ["symbolId"] = BuiltinSymbols.Prefix + "section-head" });
        var head = Marks(f, plan).Single(m => m.Owner == section);
        // Lapin's own arrow and the symbol's both stand on the side the section looks to (the line at y 2, the look +y or −y).
        double Side(ViewMark mk) => mk.Lines.Where(l => l.Layer != "PICK" && Math.Abs(l.A.Y - 2) + Math.Abs(l.B.Y - 2) > 1e-6).Average(l => (l.A.Y + l.B.Y) / 2) - 2;
        Assert.Equal(Math.Sign(Side(before)), Math.Sign(Side(head)));
        // At 1:100 the 12 mm bubble is 1.2 m across (with the line's end stroke beside it, at most 1.6).
        var xs = head.Lines.Where(l => l.Layer != "PICK").SelectMany(l => new[] { l.A.X, l.B.X }).Where(x => x < 0.5).ToList();
        Assert.InRange(xs.Max() - xs.Min(), 1.0, 1.6);
    }
}
