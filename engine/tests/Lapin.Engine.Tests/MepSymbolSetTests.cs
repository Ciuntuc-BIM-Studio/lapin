using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// MEP symbol sets: Lapin's ISO 4067 / IEC 60617 set as data, the default, drawing as before; the pending Romanian STAS set; a
/// symbol the user drew given to a role (in the project's set, made in the same step) drawing instead of the coded one, at its
/// scale and in its system's colour, gone again when removed; a set shared as a .lapinsymbols file.
/// </summary>
public class MepSymbolSetTests
{
    sealed class F
    {
        public Session Session { get; }
        public F()
        {
            ILapinModule[] use = [new ArchitectureModule(), new DraftingModule(), new MepModule()];
            Session = new Session(ModuleCatalog.Create(use), new ProductManifest("test", "Test", ["core", .. use.Select(m => m.Id)]));
        }
        public IModelView Model => Session.Model;
        public string Storey => Spatial.Storeys(Model).First().Id;
        public JsonNode? Run(string command, JsonObject args) { Session.Execute(command, args, out var r); return r; }
        public string Id(string command, JsonObject args) => (string)Run(command, args)!["id"]!;
        public JsonObject Q(string query, JsonObject? args = null) => (JsonObject)Session.Query(query, args ?? [])!;
        public List<DLine> Lines(string view, string element) =>
            Views.Generate(Session.Reader, Views.Find(Model, view)!).Drawing!.Lines.Where(l => l.Element == element && l.Role != LineRole.Hidden).ToList();
        public string PlanAt(double scale) => Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = Storey, ["name"] = $"1:{scale}", ["scale"] = scale });
        public double Extent(string view, string element)
        {
            var xs = Lines(view, element).SelectMany(l => new[] { l.A.X, l.B.X }).ToList();
            return xs.Max() - xs.Min();
        }
    }

    /// <summary>A ring of radius <paramref name="r"/> mm with a bar across it: no ISO symbol looks like it.</summary>
    static JsonObject Ring(double r, string scale = "annotation") => new()
    {
        ["scale"] = scale, ["category"] = "other",
        ["items"] = new JsonArray(
            new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(0, 0), ["r"] = r },
            new JsonObject { ["type"] = "line", ["a"] = new JsonArray(-r, 0), ["b"] = new JsonArray(r, 0), ["w"] = 0.5 }),
    };

    [Fact]
    public void Lapins_ISO_set_is_data_for_every_role_and_the_default_drawing_as_before()
    {
        var iso = MepSymbols.Builtin.First(s => s.Id == MepSymbols.Default);
        Assert.Equal("ISO 4067 / IEC 60617", iso.Name);
        // Every role and the drop drawn by the coded drawing of the role's own symbol name: the output as before.
        foreach (var role in Roles.All) Assert.Equal(role.Symbol, iso.Symbols[role.Id].Coded);
        Assert.Equal("rise", iso.Symbols[MepSymbols.Drop].Coded);
        // The bodies' real outlines on the model, the rest on paper.
        Assert.False(iso.Symbols["radiator"].Paper);
        Assert.False(iso.Symbols["boiler"].Paper);
        Assert.True(iso.Symbols["valve"].Paper);
        Assert.True(iso.Symbols["smokeDetector"].Paper);

        var f = new F();
        Assert.Equal(MepSymbols.Default, MepSymbols.Chosen(f.Model));
        var cold = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater });
        var valve = f.Id("mep.placeNode", new JsonObject { ["systemId"] = cold, ["role"] = "valve", ["x"] = 0, ["y"] = 0 });
        var before = f.Session.Geometry(valve)!.Plan!.Select(i => (i.Kind, string.Join(";", i.Points.Select(p => $"{p.X:0.####},{p.Y:0.####}")))).ToList();
        // Choosing ISO by name changes nothing.
        f.Run("mep.setSymbolSet", new JsonObject { ["id"] = "iso" });
        Assert.Equal(before, f.Session.Geometry(valve)!.Plan!.Select(i => (i.Kind, string.Join(";", i.Points.Select(p => $"{p.X:0.####},{p.Y:0.####}")))).ToList());
    }

    [Fact]
    public void The_Romanian_STAS_set_waits_for_its_symbols_and_is_made_ones_own_to_fill()
    {
        var stas = MepSymbols.Builtin.First(s => s.Id == "ro-stas");
        Assert.True(stas.Pending);
        Assert.Empty(stas.Symbols);
        Assert.Equal("iso", stas.Base);
        // It lists every role it would need, so a sheet of the standard can fill it.
        Assert.All(MepSymbols.Keys, k => Assert.Contains(k, stas.Needs!));
        var f = new F();
        Assert.Throws<CommandException>(() => f.Run("mep.setSymbolSet", new JsonObject { ["id"] = "ro-stas" }));
        // Made one's own: named after it, falling back on ISO, chosen; every role still draws ISO until given a symbol.
        var id = f.Id("mep.createSymbolSet", new JsonObject { ["from"] = "ro-stas" });
        var set = MepSymbols.Find(f.Model, id)!;
        Assert.Equal("Romanian (STAS)", set.Name);
        Assert.Equal("iso", set.Base);
        Assert.Equal(id, MepSymbols.Chosen(f.Model));
        Assert.Equal("valve", MepSymbols.For(f.Model, "valve").Coded);
        Assert.Equal("iso", MepSymbols.For(f.Model, "valve").SetId);
    }

    [Fact]
    public void A_drawn_symbol_replaces_a_roles_symbol_on_the_plan_and_removing_it_brings_the_built_in_back()
    {
        var f = new F();
        var p50 = f.PlanAt(50);
        var p100 = f.PlanAt(100);
        var cold = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater });
        var valve = f.Id("mep.placeNode", new JsonObject { ["systemId"] = cold, ["role"] = "valve", ["x"] = 0, ["y"] = 0, ["rotation"] = 90 });
        var iso50 = f.Extent(p50, valve);
        var symbol = f.Id("symbols.create", new JsonObject { ["name"] = "My valve", ["def"] = Ring(5) });

        // Given to the valve: the project's set is made from ISO, chosen, in the same step.
        var setId = (string)f.Run("mep.setRoleSymbol", new JsonObject { ["role"] = "valve", ["symbolId"] = symbol })!["setId"]!;
        Assert.Equal(setId, MepSymbols.Chosen(f.Model));
        Assert.Equal("Project symbols", MepSymbols.Find(f.Model, setId)!.Name);
        // A 10 mm ring on paper: 0.5 m across at 1:50, 1 m at 1:100 (the node turned: its ticks along y, the ring across x).
        Assert.Equal(0.5, f.Extent(p50, valve), 3);
        Assert.Equal(1.0, f.Extent(p100, valve), 3);
        var lines = f.Lines(p50, valve);
        Assert.All(lines, l => Assert.Equal("#2e7d32", l.Own!.Color));
        // Its 0.5 mm bar is the heavy line.
        Assert.Contains(lines, l => l.Role == LineRole.Cut);
        // Every other role keeps ISO's.
        Assert.Equal("radiator", MepSymbols.For(f.Model, "radiator").Coded);

        // One step back: ISO again; and forth.
        f.Session.Undo();
        Assert.Equal(iso50, f.Extent(p50, valve), 6);
        f.Session.Redo();
        Assert.Equal(0.5, f.Extent(p50, valve), 3);
        // Removed from the role: the base's (ISO's) again.
        f.Run("mep.setRoleSymbol", new JsonObject { ["role"] = "valve", ["symbolId"] = null });
        Assert.Equal(iso50, f.Extent(p50, valve), 6);
        // A symbol deleted from the project falls back the same way.
        f.Run("mep.setRoleSymbol", new JsonObject { ["role"] = "valve", ["symbolId"] = symbol });
        f.Run("symbols.delete", new JsonObject { ["id"] = symbol });
        Assert.Equal(iso50, f.Extent(p50, valve), 6);
    }

    [Fact]
    public void A_model_symbol_draws_at_its_real_size_and_a_drop_takes_its_own_sign_in_runs_too()
    {
        var f = new F();
        var p100 = f.PlanAt(100);
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        var radiator = f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "radiator", ["x"] = 0, ["y"] = 0 });
        // A 1.2 m panel drawn in the building's mm.
        var panel = f.Id("symbols.create", new JsonObject
        {
            ["name"] = "Panel radiator",
            ["def"] = new JsonObject { ["scale"] = "model", ["category"] = "other", ["items"] = new JsonArray(new JsonObject { ["type"] = "rect", ["x"] = -600, ["y"] = -50, ["w"] = 1200, ["h"] = 100 }) },
        });
        f.Run("mep.setRoleSymbol", new JsonObject { ["role"] = "radiator", ["symbolId"] = panel });
        Assert.Equal(1.2, f.Extent(p100, radiator), 3);
        Assert.False(MepSymbols.For(f.Model, "radiator").Paper);

        // A drop of the user's own: the rise node going down and a run's drop inside it draw it; a rise keeps ISO's dot.
        var cold = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater });
        var mine = f.Id("symbols.create", new JsonObject { ["name"] = "Drop", ["def"] = Ring(4) });
        f.Run("mep.setRoleSymbol", new JsonObject { ["role"] = "drop", ["symbolId"] = mine });
        var down = f.Id("mep.placeNode", new JsonObject { ["systemId"] = cold, ["role"] = "rise", ["x"] = 5, ["y"] = 0, ["toElevation"] = -1 });
        var up = f.Id("mep.placeNode", new JsonObject { ["systemId"] = cold, ["role"] = "rise", ["x"] = 8, ["y"] = 0 });
        Assert.Equal(0.4, f.Extent(f.PlanAt(50), down), 3);
        Assert.Equal(0.12, f.Extent(f.PlanAt(50), up), 3);
        var glyph = MepGlyphs.Of(f.Model, MepSymbols.Drop, null, 100);
        var (min, max) = MepGlyphs.Bounds(glyph);
        Assert.Equal(8, max.X - min.X, 3);
    }

    [Fact]
    public void A_set_travels_as_a_symbols_file_and_comes_in_as_the_projects_set()
    {
        var a = new F();
        var symbol = a.Id("symbols.create", new JsonObject { ["name"] = "Office valve", ["def"] = Ring(3) });
        var setId = a.Id("mep.createSymbolSet", new JsonObject { ["name"] = "Office", ["standard"] = "Our office" });
        a.Run("mep.setRoleSymbol", new JsonObject { ["setId"] = setId, ["role"] = "valve", ["symbolId"] = symbol });
        var file = a.Q("mep.exportSymbolSet", new JsonObject { ["id"] = setId });
        Assert.EndsWith(".lapinsymbols", (string)file["file"]!);
        var text = (string)file["text"]!;
        // A symbols library like any: Manage → Symbols reads it too.
        Assert.Single(Symbols.Read(text));

        var b = new F();
        var read = b.Q("mep.readSymbolSet", new JsonObject { ["text"] = text });
        Assert.Equal("Office", (string)read["name"]!);
        Assert.Equal("valve", (string)((JsonArray)read["roles"]!)[0]!["role"]!);
        var imported = (JsonObject)b.Run("mep.importSymbolSet", new JsonObject { ["text"] = text })!;
        var id = (string)imported["id"]!;
        Assert.Equal(id, MepSymbols.Chosen(b.Model));
        Assert.Equal("Our office", MepSymbols.Find(b.Model, id)!.Standard);
        var choice = MepSymbols.For(b.Model, "valve");
        Assert.NotNull(choice.Def);
        Assert.Equal("Office valve", Symbols.NameOf(b.Model, choice.Entry.SymbolId));
        // A built-in set stays as it is; a set without a drawn symbol has nothing to share.
        Assert.Throws<CommandException>(() => b.Run("mep.setRoleSymbol", new JsonObject { ["setId"] = "iso", ["role"] = "valve", ["symbolId"] = choice.Entry.SymbolId }));
        var empty = b.Id("mep.createSymbolSet", new JsonObject { ["name"] = "Empty", ["use"] = false });
        Assert.Throws<CommandException>(() => b.Q("mep.exportSymbolSet", new JsonObject { ["id"] = empty }));
        // Deleted, the plans draw ISO again.
        b.Run("mep.deleteSymbolSet", new JsonObject { ["id"] = id });
        Assert.Equal(MepSymbols.Default, MepSymbols.Chosen(b.Model));
    }
}
