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
/// MEP symbol sets: Lapin's ISO 4067 / IEC 60617 set as data, the default, drawing as before; the pending Romanian STAS set.
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
}
