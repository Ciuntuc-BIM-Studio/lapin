using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;

namespace Lapin.Engine.Tests;

/// <summary>
/// Level marks relative or absolute (in-app report #190, decision 867, docs/plan-level-datums.md): one resolver
/// (<see cref="LevelDatum"/>) writes every level a drawing, a schedule or a document shows, relative to the project's ±0.00
/// (model 0 or a chosen storey), absolute (the altitude: z + the base point's elevation) or both; its texts are the
/// <c>levels</c> notation domain. With nothing set it writes exactly what Lapin wrote before.
/// </summary>
public partial class LevelDatumTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new CivilModule());
    static JsonArray J(params double[] v) => new(v.Select(x => (JsonNode)x).ToArray());

    static List<string> Texts(Fixture f, string view) => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!.AsArray()
        .OfType<JsonObject>().Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).ToList();

    /// <summary>The texts that hold a level (a sign before a digit), sorted: what the golden sweep compares.</summary>
    static string Levels(IEnumerable<string> texts) => string.Join("\n", texts.Where(t => LevelRe().IsMatch(t)).Order(StringComparer.Ordinal));

    [GeneratedRegex(@"[±+−-]\d")]
    private static partial Regex LevelRe();

    /// <summary>
    /// The scene of the golden sweep: two storeys (0 and 3.00), a wall, a section across it; a surveyed slope (datum 100) with a
    /// platform and a spot level on the ground floor's plan, a level dimension there, the base point shown.
    /// </summary>
    static (Fixture F, string Section, string Plan, string Terrain, string Upper) Scene()
    {
        var f = F();
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "Upper", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        f.Wall(0, 0, 6, 0);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(3, -2), ["end"] = J(3, 2) })!["id"]!;
        var lines = new List<string>();
        for (var i = 0; i <= 4; i++) for (var j = 0; j <= 4; j++) lines.Add($"{i * 10} {j * 10} {100 + 0.05 * i * 10}");
        var t = (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["text"] = string.Join("\n", lines), ["datum"] = 100, ["interval"] = 0.5, ["majorEvery"] = 2, ["depth"] = 1 })!["id"]!;
        f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 10, ["y1"] = 10, ["x2"] = 20, ["y2"] = 20, ["elevation"] = 0.75 });
        f.Run("civil.addSpot", new JsonObject { ["id"] = t, ["x"] = 35, ["y"] = 35 });
        var plan = "plan:" + f.Storey;
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "level", ["a"] = J(1, 1), ["at"] = J(3, 1), ["z"] = -0.45 });
        // The survey took the terrain's datum as the project's altitude (decision 867); the golden sweep's project has none, as before.
        Assert.Equal(100.0, Location.Base(f.Model).Elevation, 9);
        f.Run("project.location", new JsonObject { ["show"] = true, ["base"] = new JsonObject { ["elevation"] = 0 } });
        return (f, section, plan, t, upper);
    }

    // ------------------------------------------------------------ the golden sweep: the defaults write what Lapin wrote

    const string SectionGolden = "Ground floor +0.00\nUpper +3.00";

    const string PlanGolden = "+1.00\n+1.75\n+2.00\nBase point ±0.00\nPlatform 1 +0.75\n−0.45";

    [Fact]
    public void With_nothing_set_every_level_is_written_as_before()
    {
        var (f, section, plan, _, _) = Scene();
        Assert.Equal(SectionGolden, Levels(Texts(f, section)));
        Assert.Equal(PlanGolden, Levels(Texts(f, plan)));
        // Documents (a storey's elevation in Romanian and English).
        var book = DraftingResources.Book(f.Model);
        Assert.Equal("+3,00", DocNumbers.Level(3, new DocFormat("ro", "metric") { Book = book }));
        Assert.Equal("−0.45", DocNumbers.Level(-0.45, new DocFormat("en", "metric") { Book = book }));
        Assert.Equal("±0,00", DocNumbers.Level(0, new DocFormat("ro", "metric")));
        // The defaults store nothing.
        Assert.Null(Spatial.Project(f.Model)!.Attrs[LevelDatum.Key]);
        Assert.Null(Spatial.Project(f.Model)!.Attrs[Notations.Key]);
    }

    [Fact]
    public void The_default_resolver_writes_what_FormatLevel_writes()
    {
        var f = F();
        var d = LevelDatum.Of(f.Model);
        var book = DraftingResources.Book(f.Model);
        Assert.True(d.IsDefault);
        foreach (var z in new[] { 0, 0.004, -0.004, 3, -0.45, 12.345, 650.37 })
        {
            Assert.Equal(Standards.FormatLevel(z, book), d.Text(z, book));
            Assert.Equal(Standards.FormatLevel(z, book, plusAtZero: true), d.Text(z, book, plusAtZero: true));
            Assert.Equal(Standards.FormatLevel(z), d.Text(z, null));
        }
        var imperial = book with { Units = "ft-in" };
        Assert.Equal(Standards.FormatLevel(3.048, imperial), d.Text(3.048, imperial));
        Assert.Equal(Standards.FormatLevel(0, imperial), d.Text(0, imperial));
    }

    // ------------------------------------------------------------ relative to a storey, absolute, both

    [Fact]
    public void Relative_to_a_storey_absolute_and_both()
    {
        var (f, section, plan, _, upper) = Scene();
        // ±0.00 at the upper storey: its mark reads +0.00, the ground floor −3.00.
        f.Run("project.levelDatum", new JsonObject { ["zeroStorey"] = upper });
        Assert.Contains("Upper +0.00", Texts(f, section));
        Assert.Contains("Ground floor −3.00", Texts(f, section));
        // Tied: ±0.00 (the upper storey) = 650.00, the model does not move: the base point's elevation is 647.00.
        f.Run("project.levelDatum", new JsonObject { ["tie"] = 650.0 });
        Assert.Equal(647.0, Location.Base(f.Model).Elevation, 9);
        Assert.Equal(3.0, Spatial.Storeys(f.Model).Single(s => s.Id == upper).Num("elevation"), 9);
        f.Run("project.levelDatum", new JsonObject { ["mode"] = "absolute" });
        var texts = Texts(f, section);
        Assert.Contains("Upper +650.00", texts);
        Assert.Contains("Ground floor +647.00", texts);
        f.Run("project.levelDatum", new JsonObject { ["mode"] = "both" });
        Assert.Contains("Upper +0.00 (+650.00)", Texts(f, section));
        // A view says its own over the project's.
        f.Run("view.levelDatum", new JsonObject { ["id"] = section, ["datum"] = "relative" });
        Assert.Contains("Upper +0.00", Texts(f, section));
        Assert.Equal("relative", Views.Find(f.Model, section)!.Str(LevelDatum.Key));
        f.Run("view.levelDatum", new JsonObject { ["id"] = section, ["datum"] = "project" });
        Assert.Null(Views.Find(f.Model, section)!.Attrs[LevelDatum.Key]);
        // One undo step each: back to "absolute".
        f.Session.Undo();
        f.Session.Undo();
        Assert.Contains("Upper +0.00 (+650.00)", Texts(f, section));
        Assert.Throws<CommandException>(() => f.Run("project.levelDatum", new JsonObject { ["mode"] = "sideways" }));
        Assert.Throws<CommandException>(() => f.Run("project.levelDatum", new JsonObject { ["zeroStorey"] = "nowhere" }));
        Assert.Throws<CommandException>(() => f.Run("view.levelDatum", new JsonObject { ["id"] = section, ["datum"] = "sideways" }));
        // The plan's level dimension reads it too.
        Assert.Contains(Texts(f, plan), t => t.Contains("(+646.55)"));
    }

    [Fact]
    public void A_sheet_sets_every_viewport_s_view()
    {
        var (f, section, _, _, _) = Scene();
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "A1" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = section });
        f.Run("view.levelDatum", new JsonObject { ["sheet"] = sheet, ["datum"] = "absolute" });
        Assert.Equal("absolute", Views.Find(f.Model, section)!.Str(LevelDatum.Key));
    }

    [Fact]
    public void Imperial_and_a_preset()
    {
        var (f, section, _, _, _) = Scene();
        f.Run("project.levelDatum", new JsonObject { ["tie"] = 650.0, ["mode"] = "absolute" });
        f.Run("standards.options", new JsonObject { ["units"] = "ft-in" });
        Assert.Contains(Texts(f, section), t => t.StartsWith("Upper +2142'-", StringComparison.Ordinal));
        f.Run("standards.options", new JsonObject { ["units"] = "mm" });
        // France: NGF after the altitude, without its plus.
        f.Run("notation.set", new JsonObject { ["domain"] = LevelDatum.Domain.Id, ["preset"] = "fr" });
        Assert.Contains("Upper 653.00 NGF", Texts(f, section));
        Assert.Equal("±0.00 = 650.00 NGF", LevelDatum.Of(f.Model).Tie(DraftingResources.Book(f.Model)));
        // The user's own zero sign.
        f.Run("notation.set", new JsonObject { ["domain"] = LevelDatum.Domain.Id, ["preset"] = null, ["values"] = new JsonObject { ["zero"] = "0" } });
        Assert.Equal("00.00 = +650.00", LevelDatum.Of(f.Model).Tie(DraftingResources.Book(f.Model)));
    }

    [Fact]
    public void Notation_describe_lists_the_levels_domain()
    {
        var f = F();
        var all = (JsonArray)f.Session.Query("notation.describe", new JsonObject())!;
        var levels = all.OfType<JsonObject>().Single(d => (string?)d["id"] == "levels");
        var keys = ((JsonArray)levels["keys"]!).OfType<JsonObject>().Select(k => (string?)k["key"]).ToList();
        Assert.Contains("relative", keys);
        Assert.Contains("absolute", keys);
        Assert.Contains("both", keys);
        Assert.Contains("tie", keys);
        var presets = ((JsonArray)levels["presets"]!).OfType<JsonObject>().Select(p => (string?)p["id"]).ToList();
        Assert.Equal(["fr", "ro", "de"], presets);
    }

    // ------------------------------------------------------------ dimensions and symbol tokens

    [Fact]
    public void A_level_dimension_says_its_own_datum()
    {
        var (f, _, plan, _, _) = Scene();
        f.Run("project.levelDatum", new JsonObject { ["tie"] = 100.0 });
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "level", ["a"] = J(5, 5), ["at"] = J(8, 5), ["z"] = 1.25, ["datum"] = "absolute" });
        var texts = Texts(f, plan);
        Assert.Contains("+101.25", texts);
        Assert.Contains("−0.45", texts);
        Assert.Throws<CommandException>(() => f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["dimKind"] = "level", ["a"] = J(5, 5), ["at"] = J(8, 5), ["datum"] = "up" }));
    }

    [Fact]
    public void Symbol_tokens_read_the_altitude_the_relative_level_and_the_tie()
    {
        var (f, section, _, _, upper) = Scene();
        f.Run("project.levelDatum", new JsonObject { ["zeroStorey"] = upper, ["tie"] = 650.0 });
        var view = Views.Find(f.Model, "plan:" + upper)!;
        var resolve = Symbols.Resolver(f.Model, null, null, view, new Lapin.Geometry.Vec2(0, 0), null);
        Assert.Equal((true, "+650.00"), resolve("level.altitude"));
        Assert.Equal((true, "±0.00"), resolve("level.relative"));
        Assert.Equal((true, "±0.00 = +650.00"), resolve("level.tie"));
        Assert.Equal((true, "+650.00"), resolve("project.altitude"));
        Assert.Equal((true, "±0.00 = +650.00"), resolve("project.levelTie"));
        // The storey's elevation token follows the project's datum (relative here).
        Assert.Equal((true, "±0.00"), resolve("level.elevation"));
        Assert.Contains(Symbols.FieldList(f.Model, null), x => x.Key == "level.altitude");
        Assert.Contains(Symbols.FieldList(f.Model, null), x => x.Key == "project.altitude");
        _ = section;
    }

    // ------------------------------------------------------------ terrain and site

    [Fact]
    public void Absolute_views_draw_contours_at_round_absolute_heights()
    {
        var (f, _, plan, t, _) = Scene();
        // The altitude 100.20: relative contours at model 0.5 steps; absolute ones at altitudes 100.5, 101… (model 0.30, 0.80…).
        f.Run("project.levelDatum", new JsonObject { ["tie"] = 100.2 });
        var before = ContourXs(f, plan);
        Assert.Contains(10.0, before);
        Assert.Contains("+1.00", Texts(f, plan));
        plan = (string)f.Run("view.levelDatum", new JsonObject { ["id"] = plan, ["datum"] = "absolute" })!["id"]!;
        var after = ContourXs(f, plan);
        // z = 0.05 x: the 0.30 contour at x = 6, 0.80 at x = 16 …
        Assert.Contains(6.0, after);
        Assert.DoesNotContain(10.0, after);
        var texts = Texts(f, plan);
        Assert.Contains("+101.00", texts);
        Assert.Contains("Platform 1 +100.95", texts);
        // The terrain's datum (100) differs from the project's altitude: a warning, and one click to take it.
        var warning = (JsonObject)f.Session.Query("project.levelDatum", new JsonObject())!;
        Assert.Contains(((JsonArray)warning["terrains"]!).OfType<JsonObject>(), x => (string?)x["id"] == t && (double)x["datum"]! == 100);
        f.Run("project.levelDatum", new JsonObject { ["fromTerrain"] = t });
        Assert.Equal(100.0, Location.Base(f.Model).Elevation, 9);
        Assert.Empty((JsonArray)((JsonObject)f.Session.Query("project.levelDatum", new JsonObject())!)["terrains"]!);
    }

    [Fact]
    public void A_survey_import_into_a_project_at_altitude_0_takes_the_terrain_s_datum_and_says_so()
    {
        var f = F();
        var r = (JsonObject)f.Run("civil.addTerrain", new JsonObject { ["text"] = "0 0 100\n10 0 101\n0 10 100\n10 10 101", ["datum"] = 100 })!;
        Assert.Equal(100.0, Location.Base(f.Model).Elevation, 9);
        Assert.Contains("100", (string?)r["note"] ?? "");
        // One undo step: the terrain and the altitude go together.
        f.Session.Undo();
        Assert.Equal(0.0, Location.Base(f.Model).Elevation, 9);
        // A project already tied keeps its own.
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["elevation"] = 90.0 } });
        f.Run("civil.addTerrain", new JsonObject { ["text"] = "0 0 100\n10 0 101\n0 10 100\n10 10 101", ["datum"] = 100 });
        Assert.Equal(90.0, Location.Base(f.Model).Elevation, 9);
    }

    static List<double> ContourXs(Fixture f, string plan)
    {
        var view = Views.Find(f.Model, plan)!;
        var t = f.Model.OfClass(IfcClass.GeographicElement).First(Terrains.IsTerrain);
        var items = f.Session.Geometry(t.Id)!.Plan!;
        var hook = ((IServices)f.Session.Catalog).All<IPlanInView>().First(h => h.Handles(t));
        return hook.PlanIn(f.Model, view, t, items).Where(p => p.Kind is "contour" or "contourMajor")
            .SelectMany(p => p.Points.Select(x => Math.Round(x.X, 6))).Distinct().Order().ToList();
    }

    // ------------------------------------------------------------ IFC

    [Fact]
    public void An_ifc_round_trip_keeps_the_altitude_the_mode_and_the_zero_storey()
    {
        var (f, _, _, _, upper) = Scene();
        f.Run("project.levelDatum", new JsonObject { ["zeroStorey"] = upper, ["tie"] = 650.0, ["mode"] = "both" });
        var ifc = Export(f);
        Assert.Matches(@"IFCBUILDING\([^;]*\.ELEMENT\.,647\.?0*,\$,\$\)", ifc);
        var g = F();
        Lapin.Ifc.IfcImport.Run(g.Session, System.Text.Encoding.UTF8.GetBytes(ifc), "datum.ifc", null, out _);
        var d = LevelDatum.Of(g.Model);
        Assert.Equal("both", d.Mode);
        Assert.Equal(647.0, d.Altitude, 6);
        Assert.Equal(3.0, d.Zero, 6);
    }

    [Fact]
    public void An_ifc_without_a_map_conversion_gives_its_altitude_from_the_building()
    {
        var f = F();
        var ifc = Export(f);
        // No altitude: ElevationOfRefHeight stays unset, as before.
        Assert.Matches(@"IFCBUILDING\([^;]*\.ELEMENT\.,\$,\$,\$\)", ifc);
        var foreign = Regex.Replace(ifc, @"(IFCBUILDING\([^;]*\.ELEMENT\.,)\$", "${1}312.5");
        // A foreign file: without Lapin's own intent on the project.
        foreign = Regex.Replace(foreign, @"'Lapin_Intent'", "'Other_Intent'");
        var g = F();
        Lapin.Ifc.IfcImport.Run(g.Session, System.Text.Encoding.UTF8.GetBytes(foreign), "foreign.ifc", null, out _);
        Assert.Equal(312.5, Location.Base(g.Model).Elevation, 6);
    }

    static string Export(Fixture f)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lapin-datum-{Guid.NewGuid():N}.ifc");
        try { Lapin.Ifc.IfcExport.Write(f.Session, path); return File.ReadAllText(path); }
        finally { File.Delete(path); }
    }

    // ------------------------------------------------------------ no new direct FormatLevel in drawings

    /// <summary>
    /// Every level a drawing writes goes through <see cref="LevelDatum"/>: <see cref="Standards.FormatLevel(double)"/> is the
    /// number formatter underneath, called only by the resolver itself (and Standards, where it lives).
    /// </summary>
    [Fact]
    public void No_code_writes_a_level_past_the_resolver()
    {
        var src = Path.Combine(RepoRoot(), "engine", "src");
        var allowed = new[] { Path.Combine("Lapin.Drawing", "Standards.cs"), "LevelDatums.cs" };
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            if (allowed.Any(a => file.EndsWith(a, StringComparison.Ordinal))) continue;
            var n = 0;
            foreach (var line in File.ReadLines(file))
            {
                n++;
                var code = line.TrimStart();
                if (code.StartsWith("///", StringComparison.Ordinal) || code.StartsWith("//", StringComparison.Ordinal)) continue;
                if (code.Contains("FormatLevel(", StringComparison.Ordinal)) offenders.Add($"{Path.GetRelativePath(src, file)}:{n}");
            }
        }
        Assert.True(offenders.Count == 0, "Levels written past LevelDatum: " + string.Join(", ", offenders));
    }

    static string RepoRoot([CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));
}
