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
/// MEP plans that read as MEP drawings: each service's colour and line type (a pair's supply and return apart), set on the
/// system and overridden by Visibility / Graphics; what lies over the cut dashed as overhead; the architecture halftone in an
/// MEP plan; the coverage circles by their setting; Tag system's tags, one step, following their elements.
/// </summary>
public class MepPlanGraphicsTests
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
        public string Plan => "plan:" + Storey;
        public List<DLine> Lines(string view, string element) =>
            Views.Generate(Session.Reader, Views.Find(Model, view)!).Drawing!.Lines.Where(l => l.Element == element).ToList();
        public List<Pen> Pens(string view) => Views.Render(Session.Reader, Views.Find(Model, view)!).Pens;
    }

    static JsonArray Pts(params (double X, double Y)[] pts) => new(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    static string Run(F f, string system, JsonObject? more = null)
    {
        var a = new JsonObject { ["systemId"] = system, ["points"] = Pts((0, 0), (4, 0)) };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return f.Id("mep.drawRun", a);
    }

    [Fact]
    public void Each_service_draws_in_its_colour_and_line_type_a_pair_its_supply_and_return_apart()
    {
        var f = new F();
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        var run = Run(f, heating);
        var view = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Heating" });
        var lines = f.Lines(f.Plan, run).Where(l => l.Role == LineRole.Projection).ToList();
        // The supply red and solid, the return blue and dashed: black and white still tells them apart.
        Assert.Contains(lines, l => l.Own is { Color: "#d32f2f", Pattern: null });
        Assert.Contains(lines, l => l.Own is { Color: "#1565c0", Pattern: "dashed" });
        var pens = f.Pens(f.Plan);
        Assert.Contains(pens, p => p.Stroke == "#d32f2f" && p.Layer == PenLayer.Seen && (p.Dash == null || p.Dash.Length == 0));
        Assert.Contains(pens, p => p.Stroke == "#1565c0" && p.Dash is { Length: > 0 });

        // The defaults by service: cold water green on a centre line, drainage brown and heavier.
        Assert.Equal(new LineLook("#2e7d32", "center", "dotted"), SystemStyle.For(Services.ColdWater).Line);
        Assert.Equal(5, SystemStyle.For(Services.Drainage).Line.Pen);
        // A pair's two lines differ by their line types wherever the service has a pair.
        Assert.NotEqual(SystemStyle.For(Services.Heating).Line.Pattern, SystemStyle.For(Services.Heating).Return.Pattern);

        // Set on the system (Systems…): its own colour and line type.
        f.Run("mep.updateSystem", new JsonObject { ["id"] = heating, ["style"] = new JsonObject { ["line"] = new JsonObject { ["color"] = "#ff00ff", ["pattern"] = "dashdot" } } });
        Assert.Contains(f.Lines(f.Plan, run), l => l.Own is { Color: "#ff00ff", Pattern: "dashdot" });
        Assert.Throws<CommandException>(() => f.Run("mep.updateSystem", new JsonObject { ["id"] = heating, ["style"] = new JsonObject { ["line"] = new JsonObject { ["pattern"] = "zigzag" } } }));
        // Null: the service's again.
        f.Run("mep.updateSystem", new JsonObject { ["id"] = heating, ["style"] = new JsonObject { ["line"] = new JsonObject { ["color"] = null } } });
        Assert.Contains(f.Lines(f.Plan, run), l => l.Own is { Color: "#d32f2f", Pattern: "dashdot" });

        // The view's Visibility / Graphics wins: the MEP category's line colour over the system's.
        f.Run("view.graphics", new JsonObject { ["id"] = view, ["category"] = "mep", ["value"] = new JsonObject { ["lineColor"] = "#000000" } });
        var after = f.Pens(view);
        Assert.DoesNotContain(after, p => p.Stroke is "#d32f2f" or "#1565c0");
    }

    [Fact]
    public void A_run_over_the_cut_draws_dashed_as_overhead_and_under_the_level_as_beyond()
    {
        var f = new F();
        var cold = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater });
        var floor = Run(f, cold);
        var ceiling = f.Id("mep.drawRun", new JsonObject { ["systemId"] = cold, ["points"] = Pts((0, 2), (4, 2)), ["plane"] = "ceiling" });
        var under = f.Id("mep.drawRun", new JsonObject { ["systemId"] = cold, ["points"] = Pts((0, 4), (4, 4)), ["elevation"] = -0.4 });
        Assert.All(f.Lines(f.Plan, floor), l => Assert.Equal(LineRole.Projection, l.Role));
        var over = f.Lines(f.Plan, ceiling);
        Assert.NotEmpty(over);
        // Over the cut: overhead, in the system's colour, its "above" line type (cold water: dotted).
        Assert.All(over, l => { Assert.Equal(LineRole.Overhead, l.Role); Assert.Equal("#2e7d32", l.Own!.Color); Assert.Equal("dotted", l.Own.Pattern); });
        Assert.All(f.Lines(f.Plan, under), l => Assert.Equal(LineRole.Beyond, l.Role));
        // A plan cut higher than the ceiling run draws it as seen again.
        var high = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "High" });
        f.Run("view.setRange", new JsonObject { ["id"] = high, ["cutHeight"] = 2.7 });
        Assert.All(f.Lines(high, ceiling), l => Assert.Equal(LineRole.Projection, l.Role));
        // A heating supply over the cut takes the overhead dashes (solid otherwise).
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        var hot = f.Id("mep.drawRun", new JsonObject { ["systemId"] = heating, ["points"] = Pts((0, 6), (4, 6)), ["plane"] = "ceiling", ["pair"] = false });
        Assert.All(f.Lines(f.Plan, hot), l => { Assert.Equal(LineRole.Overhead, l.Role); Assert.Null(l.Own!.Pattern); });
        Assert.Contains(f.Pens(f.Plan), p => p.Layer == PenLayer.Overhead && p.Stroke == "#d32f2f" && p.Dash is { Length: > 0 });
    }

    [Fact]
    public void Node_symbols_draw_whole_wherever_they_are_mounted()
    {
        var f = new F();
        var alarm = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.FireAlarm });
        // A panel on the wall at 1.30 m (over the 1.20 m cut) and a detector on the ceiling: both seen, solid, in the system's colour.
        var panel = f.Id("mep.placeNode", new JsonObject { ["systemId"] = alarm, ["role"] = "firePanel", ["x"] = 0, ["y"] = 0 });
        var detector = f.Id("mep.placeNode", new JsonObject { ["systemId"] = alarm, ["role"] = "smokeDetector", ["x"] = 3, ["y"] = 0 });
        foreach (var id in new[] { panel, detector })
        {
            var lines = f.Lines(f.Plan, id).Where(l => l.Role != LineRole.Hidden).ToList();
            Assert.NotEmpty(lines);
            Assert.All(lines, l => { Assert.True(l.Role is LineRole.Projection or LineRole.Cut, l.Role); Assert.Null(l.Own!.Pattern); Assert.Equal("#e53935", l.Own.Color); });
        }
    }

    /// <summary>The width (metres) of what a plan draws of an element, its coverage circle left out.</summary>
    static double Extent(F f, string view, string element)
    {
        var lines = f.Lines(view, element).Where(l => l.Role != LineRole.Hidden).ToList();
        Assert.NotEmpty(lines);
        var xs = lines.SelectMany(l => new[] { l.A.X, l.B.X }).ToList();
        return xs.Max() - xs.Min();
    }

    [Fact]
    public void Device_symbols_print_the_same_size_at_1_50_and_1_100_an_appliance_keeps_its_real_outline()
    {
        var f = new F();
        var cold = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater });
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        var valve = f.Id("mep.placeNode", new JsonObject { ["systemId"] = cold, ["role"] = "valve", ["x"] = 0, ["y"] = 0 });
        var radiator = f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "radiator", ["x"] = 3, ["y"] = 0 });
        var rise = f.Id("mep.placeNode", new JsonObject { ["systemId"] = cold, ["role"] = "rise", ["x"] = 6, ["y"] = 0 });
        var p50 = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "1:50", ["scale"] = 50 });
        var p100 = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "1:100", ["scale"] = 100 });
        // The valve's and the rise's signs: as coded at 1:50, twice as large on the model at 1:100, so the same on paper.
        foreach (var id in new[] { valve, rise })
        {
            var (a, b) = (Extent(f, p50, id), Extent(f, p100, id));
            Assert.Equal(2 * a, b, 3);
            Assert.Equal(a / 50, b / 100, 6);
        }
        // The valve's body 80 mm across, its ports' ticks 60 mm out either side.
        Assert.Equal(0.2, Extent(f, p50, valve), 3);
        // A radiator is drawn at its length at any scale (1 m), its ports' ticks inside it.
        Assert.Equal(1.0, Extent(f, p50, radiator), 3);
        Assert.Equal(1.0, Extent(f, p100, radiator), 3);
        // The interface's plan lines carry the anchor so its picks lie on what the plan draws.
        var dto = f.Session.Snapshot().Render.First(r => r.NodeId == valve).Plan;
        Assert.Contains(dto, p => p.Kind == "symbol" && p.Paper is [0, 0, 50]);
        Assert.Contains(dto, p => p.Kind == "hit" && p.Paper != null);
    }

    [Fact]
    public void Tag_system_places_neighbours_tags_clear_of_each_other_and_of_the_runs()
    {
        var f = new F();
        var plan = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["discipline"] = "MEP", ["name"] = "MEP" });
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        // Two radiators side by side, a boiler with a run going up past it: their tags would land on each other at one fixed place.
        f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "radiator", ["x"] = 0, ["y"] = 0 });
        f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "radiator", ["x"] = 1.2, ["y"] = 0 });
        f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "boiler", ["x"] = 0, ["y"] = -2 });
        f.Id("mep.drawRun", new JsonObject { ["systemId"] = heating, ["points"] = Pts((0.6, -3), (0.6, 2)) });
        f.Id("mep.drawRun", new JsonObject { ["systemId"] = heating, ["points"] = Pts((-3, 1), (4, 1)) });
        Assert.Equal(5, (int)f.Run("mep.tagSystem", new JsonObject { ["viewId"] = plan })!["tagged"]!);
        var tags = TagsOn(f, plan).Select(t => t.Id).ToHashSet();
        var boxes = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Marks
            .Where(mk => mk.Owner != null && tags.Contains(mk.Owner) && mk.Lines.Any(l => l.Layer == "PICK"))
            .Select(mk => mk.Lines.Where(l => l.Layer == "PICK").SelectMany(l => new[] { l.A, l.B }).ToList())
            .Select(p => (X0: p.Min(q => q.X), Y0: p.Min(q => q.Y), X1: p.Max(q => q.X), Y1: p.Max(q => q.Y))).ToList();
        Assert.Equal(5, boxes.Count);
        for (var i = 0; i < boxes.Count; i++)
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var (a, b) = (boxes[i], boxes[j]);
                Assert.False(a.X0 < b.X1 && b.X0 < a.X1 && a.Y0 < b.Y1 && b.Y0 < a.Y1, $"tags {i} and {j} overlap: {string.Join(" ; ", boxes)} at {string.Join(" ; ", TagsOn(f, plan).Select(t => f.Model.Get(t.Str("element")!)!.Name + " " + t.Attrs["at"]!.ToJsonString()))}");
            }
    }

    [Fact]
    public void An_mep_plan_draws_the_architecture_halftone_unless_its_style_says_not()
    {
        var f = new F();
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 1, ["x2"] = 8, ["y2"] = 1, ["join"] = false });
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        Run(f, heating);
        var mep = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["discipline"] = "MEP", ["name"] = "MEP" });
        bool Grey(List<Pen> pens) => pens.Any(p => p.Layer == PenLayer.Cut && p.Stroke == Links.Halftone);
        var pens = f.Pens(mep);
        Assert.True(Grey(pens));
        // The services stay in their colours.
        Assert.Contains(pens, p => p.Stroke == "#d32f2f");
        // The architectural plan: black walls.
        Assert.False(Grey(f.Pens(f.Plan)));
        // Turned off on the view (View settings): black again.
        f.Run("view.setStyle", new JsonObject { ["id"] = mep, ["halftoneOthers"] = false });
        Assert.False(Grey(f.Pens(mep)));
        f.Run("view.setStyle", new JsonObject { ["id"] = mep, ["halftoneOthers"] = null });
        Assert.True(Grey(f.Pens(mep)));
        // A colour the user gave the walls in Visibility / Graphics wins.
        f.Run("view.graphics", new JsonObject { ["id"] = mep, ["category"] = "walls", ["value"] = new JsonObject { ["lineColor"] = "#00aa00" } });
        Assert.False(Grey(f.Pens(mep)));
    }

    [Fact]
    public void Coverage_circles_draw_as_the_view_or_the_system_says_selected_only_by_default()
    {
        var f = new F();
        var alarm = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.FireAlarm });
        var detector = f.Id("mep.placeNode", new JsonObject { ["systemId"] = alarm, ["role"] = "smokeDetector", ["x"] = 2, ["y"] = 2 });
        int Reach(string view) => f.Lines(view, detector).Count(l => l.Role == LineRole.Hidden);
        // The geometry keeps the circle (pale, thin), the interface draws it for the selected detector; the drawing and its print leave it out.
        var circle = f.Session.Geometry(detector)!.Plan!.Single(i => i.Kind == "reach");
        Assert.Equal(1, circle.Style!.Pen);
        Assert.Equal(0, Reach(f.Plan));
        f.Run("mep.updateSystem", new JsonObject { ["id"] = alarm, ["coverage"] = "all" });
        Assert.True(Reach(f.Plan) > 20);
        // The view's own setting over the system's.
        var plan = f.Id("mep.setCoverage", new JsonObject { ["viewId"] = f.Plan, ["mode"] = "none" });
        Assert.Equal(0, Reach(plan));
        f.Run("mep.setCoverage", new JsonObject { ["viewId"] = plan, ["mode"] = null });
        Assert.True(Reach(plan) > 20);
        Assert.Throws<CommandException>(() => f.Run("mep.setCoverage", new JsonObject { ["viewId"] = plan, ["mode"] = "some" }));
    }

    static List<Entity> TagsOn(F f, string view) =>
        f.Model.OfClass(IfcClass.Annotation).Where(a => a.PredefinedType == "TAG" && a.Str("viewId") == view).ToList();

    static string Text(F f, Entity tag)
    {
        var book = DraftingResources.Book(f.Model);
        var e = f.Model.Get(tag.Str("element")!)!;
        return string.Join("|", Tags.LinesOf(f.Model, tag, e, Standards.TagTypeOf(book, tag.Str("tagType")), book.Units));
    }

    [Fact]
    public void Tag_system_tags_runs_rises_and_terminals_in_one_step_skipping_the_tagged_and_the_tags_follow()
    {
        var f = new F();
        var plan = f.Id("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["discipline"] = "MEP", ["name"] = "MEP" });
        var water = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.ColdWater, ["defaults"] = new JsonObject { ["material"] = "pp-r" } });
        var pipe = Run(f, water);
        var drain = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Drainage });
        var fall = f.Id("mep.drawRun", new JsonObject { ["systemId"] = drain, ["points"] = Pts((0, 3), (5, 3)) });
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        var radiator = f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "radiator", ["x"] = 6, ["y"] = 0 });
        var rise = f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "rise", ["x"] = 8, ["y"] = 0, ["toElevation"] = 2.4 });

        var tagged = (int)f.Run("mep.tagSystem", new JsonObject { ["viewId"] = plan })!["tagged"]!;
        Assert.True(tagged == 4, string.Join(", ", TagsOn(f, plan).Select(t => f.Model.Get(t.Str("element")!)!.Name)));
        // The tag types put in the drafting standards with them.
        var book = DraftingResources.Book(f.Model);
        Assert.Contains(book.TagTypes, t => t.Id == MepTags.RunType && t.Template == "{mepSize}|{mepSlope}");
        var tags = TagsOn(f, plan).ToDictionary(t => t.Str("element")!);
        Assert.Equal("PP-R 20", Text(f, tags[pipe]));
        Assert.Equal("PVC 110|i=2 %", Text(f, tags[fall]));
        Assert.Equal("P=1200 W", Text(f, tags[radiator]));
        Assert.Equal("↑ +2.40", Text(f, tags[rise]));
        Assert.Equal(MepTags.RiseType, tags[rise].Str("tagType"));
        // One step: undone, the tags and the tag types go together; redone, they come back.
        f.Session.Undo();
        Assert.Empty(TagsOn(f, plan));
        Assert.DoesNotContain(DraftingResources.Book(f.Model).TagTypes, t => t.Id == MepTags.RunType);
        f.Session.Redo();
        Assert.Equal(4, TagsOn(f, plan).Count);

        // Again: what is tagged is left; a new run gets its tag; one system alone.
        Assert.Equal(0, (int)f.Run("mep.tagSystem", new JsonObject { ["viewId"] = plan })!["tagged"]!);
        var second = f.Id("mep.drawRun", new JsonObject { ["systemId"] = water, ["points"] = Pts((0, 8), (3, 8)) });
        f.Id("mep.drawRun", new JsonObject { ["systemId"] = drain, ["points"] = Pts((0, 10), (3, 10)) });
        Assert.Equal(1, (int)f.Run("mep.tagSystem", new JsonObject { ["viewId"] = plan, ["systemId"] = water })!["tagged"]!);
        Assert.Contains(TagsOn(f, plan), t => t.Str("element") == second);

        // A tag follows its run when it moves: its head goes with it.
        JsonObject MarkOf(string tag) => (JsonObject)Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Marks.First(mk => mk.Owner == tag && mk.Data?["at"] != null).Data!;
        var tagId = tags[pipe].Id;
        var at0 = (JsonArray)MarkOf(tagId)["at"]!;
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(pipe), ["op"] = "move", ["by"] = new JsonArray(1.0, 2.0) });
        var at1 = (JsonArray)MarkOf(tagId)["at"]!;
        Assert.Equal((double)at0[0]! + 1, (double)at1[0]!, 3);
        Assert.Equal((double)at0[1]! + 2, (double)at1[1]!, 3);
    }

    [Fact]
    public void The_fields_read_a_runs_size_a_ducts_section_and_nothing_of_other_elements()
    {
        var f = new F();
        var air = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.SupplyAir });
        var duct = f.Id("mep.drawRun", new JsonObject { ["systemId"] = air, ["points"] = Pts((0, 0), (3, 0)), ["height"] = 0.2, ["size"] = 0.3 });
        Assert.Equal("300×200", TagText.Value(f.Model, f.Model.Get(duct)!, "mepSize"));
        Assert.Equal("", TagText.Value(f.Model, f.Model.Get(duct)!, "mepSlope"));
        Assert.Equal("+2.40", MepTags.Level(2.4));
        // A boiler's power in kilowatts.
        var heating = f.Id("mep.createSystem", new JsonObject { ["service"] = Services.Heating });
        var boiler = f.Id("mep.placeNode", new JsonObject { ["systemId"] = heating, ["role"] = "boiler", ["x"] = 0, ["y"] = 5 });
        Assert.Equal("P=24 kW", TagText.Value(f.Model, f.Model.Get(boiler)!, "mepDutyText"));
        Assert.Equal("±0.00", MepTags.Level(0.001));
        Assert.Equal("-0.30", MepTags.Level(-0.3));
    }
}
