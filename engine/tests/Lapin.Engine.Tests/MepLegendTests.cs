using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The MEP legend: placed on a plan or on a sheet, it lists the line types and symbols the views draw (a pair's supply and
/// return apart, a rise and a drop apart), each in its system's colour and named in its language; it follows the model.
/// </summary>
public class MepLegendTests
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
        public string Node(string system, string role, double x, double y, JsonObject? more = null)
        {
            var a = new JsonObject { ["systemId"] = system, ["role"] = role, ["x"] = x, ["y"] = y };
            foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
            return Id("mep.placeNode", a);
        }
        /// <summary>The legend's mark on its view: its texts (title and names) and how many rows it says.</summary>
        public (List<string> Texts, int Rows, ViewMark Mark) Legend(string view, string legend)
        {
            var mk = Views.Render(Session.Reader, Views.Find(Model, view)!).Marks.First(x => x.Owner == legend);
            return (mk.Texts.Select(t => t.Text.Text).ToList(), (int)mk.Data!["rows"]!, mk);
        }
    }

    static JsonArray Pts(params (double X, double Y)[] pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    [Fact]
    public void A_legend_on_a_plan_lists_what_it_draws_and_follows_the_model()
    {
        var f = new F();
        var plan = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "MEP", ["scale"] = 50 });
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        var cold = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater });
        f.Node(heating, "radiator", 0, 0);
        f.Id("mep.drawRun", new JsonObject { ["systemId"] = heating, ["points"] = Pts((0, 2), (4, 2)) });
        f.Id("mep.drawRun", new JsonObject { ["systemId"] = cold, ["points"] = Pts((0, 4), (4, 4)) });
        f.Node(cold, "valve", 6, 4);
        f.Node(cold, "rise", 8, 4);

        var legend = f.Id("sheet.addLegend", new JsonObject { ["viewId"] = plan, ["at"] = new JsonArray(10, 5), ["kind"] = "mep" });
        var (texts, rows, mk) = f.Legend(plan, legend);
        Assert.Equal("MEP LEGEND", texts[0]);
        // The heating's supply and return (a paired run), the cold water's line; the radiator, the valve and the rise going up.
        Assert.Equal(["MEP LEGEND", "Heating — Supply line", "Heating — Return line", "Cold water", "Radiator", "Valve", "Rise (going up)"], texts);
        Assert.Equal(6, rows);
        // The heating supply's line in its colour, the return in its line type; the valve's symbol in the cold water's green.
        Assert.Contains(mk.Lines, l => l.Layer == "LEGEND" && l.Color == "#d32f2f" && l.Dash == null);
        Assert.Contains(mk.Lines, l => l.Layer == "LEGEND" && l.Color == "#1565c0" && l.Dash != null);
        Assert.Contains(mk.Lines, l => l.Layer == "LEGEND" && l.Color == "#2e7d32");
        // What is not drawn is not listed; a boiler placed comes in, removed goes again (one step back).
        Assert.DoesNotContain("Boiler", texts);
        f.Node(heating, "boiler", 0, -3);
        Assert.Contains("Boiler", f.Legend(plan, legend).Texts);
        f.Session.Undo();
        Assert.DoesNotContain("Boiler", f.Legend(plan, legend).Texts);
        // A drop is its own row.
        f.Node(cold, "rise", 9, 4, new JsonObject { ["toElevation"] = -1 });
        Assert.Contains("Drop (going down)", f.Legend(plan, legend).Texts);
    }

    [Fact]
    public void Its_words_are_in_its_language_and_it_lists_a_sheets_views_or_the_one_named()
    {
        var f = new F();
        var plan = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "MEP", ["scale"] = 50 });
        var other = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Other", ["scale"] = 100 });
        var cold = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater });
        f.Node(cold, "waterMeter", 0, 0);
        var legend = f.Id("sheet.addLegend", new JsonObject { ["viewId"] = plan, ["at"] = new JsonArray(10, 5), ["kind"] = "mep", ["lang"] = "ro" });
        var texts = f.Legend(plan, legend).Texts;
        Assert.Equal("LEGENDĂ INSTALAȚII", texts[0]);
        Assert.Contains("Apometru", texts);
        Assert.True(MepLegend.Words.Has("Smoke detector", "ro"));
        // Every role and service is named in every language of the interface.
        foreach (var w in Roles.All.Select(r => r.Label).Concat(Services.All.Select(Services.Label)))
            foreach (var lang in DrawingWords.Languages.Where(l => l != "en"))
                Assert.True(MepLegend.Words.Has(w, lang), $"{w} in {lang}");

        // On a sheet: the views placed on it.
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "MEP", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = other, ["x"] = 150, ["y"] = 140 });
        var onSheet = f.Id("sheet.addLegend", new JsonObject { ["viewId"] = PaperViews.IdOf(sheet), ["at"] = new JsonArray(0.3, 0.25), ["kind"] = "mep" });
        Assert.Contains("Water meter", f.Legend(PaperViews.IdOf(sheet), onSheet).Texts);
        // Named a view of its own: that one's.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = onSheet, ["sourceViewId"] = plan });
        Assert.Contains("Water meter", f.Legend(PaperViews.IdOf(sheet), onSheet).Texts);
        // Lapin's legends stay on sheets.
        Assert.Throws<CommandException>(() => f.Run("sheet.addLegend", new JsonObject { ["viewId"] = plan, ["at"] = new JsonArray(0, 0), ["kind"] = "materials" }));
        Assert.Throws<CommandException>(() => f.Run("sheet.addLegend", new JsonObject { ["viewId"] = plan, ["at"] = new JsonArray(0, 0), ["kind"] = "pipes" }));
    }
}
