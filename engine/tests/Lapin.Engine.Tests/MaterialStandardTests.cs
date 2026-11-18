using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 3 of docs/plan-material-graphics.md (decision 673): material standards as data. Lapin's built-in standard gives exactly
/// what the coded tables it replaced gave; the packs resolve sensibly in their languages; a project names its standard, switched
/// with or without overwriting its materials' own graphics, previewed without a change; a standard made from a project's
/// materials, exported and imported with the merge choices; every command one undo step.
/// </summary>
public class MaterialStandardTests
{
    // ------------------------------------------------------------ the oracle: the coded tables of decisions 460 and 671

    static readonly (Regex Re, bool Membrane, string? Hatch)[] OldCut =
    [
        (new(@"vapou?r|barrier|membrane|foil|\bdpc\b|damp|bitum|sheet|folie|barier"), true, null),
        (new(@"eps\b|xps\b|polystyren|polistiren|pir\b|pur\b|rigid|extrudat|expandat"), false, "RIGID"),
        (new(@"insul|wool|vata|izola|batt|fibre|fiber"), false, "BATT"),
        (new(@"glass|glazing|sticla|geam|\bair\b|cavity|\baer\b|lama de aer"), false, null),
        (new(@"block|\bcmu\b|bca|aac|ytong|boltari|blocuri"), false, "BLOCK"),
        (new(@"brick|masonry|caramid|zidari|clinker|stone|piatra"), false, "MASONRY"),
        (new(@"concrete|beton|screed|sapa|c\d\d/|reinforced|armat"), false, "CONCRETE"),
        (new(@"plywood|osb|\bclt\b|placaj"), false, "WOODCROSS"),
        (new(@"timber|wood|lemn|lumber|glulam"), false, "WOOD"),
        (new(@"steel|metal|otel|alumin"), false, "STEEL"),
        (new(@"\bsand\b|nisip"), false, "SAND"),
        (new(@"gravel|ballast|pietris|aggregate"), false, "GRAVEL"),
        (new(@"earth|soil|pamant|ground"), false, "EARTH"),
        (new(@"plaster|render|gypsum|tencuial|gips|stucco"), false, "GYPSUM"),
        (new(@"tile|ceramic|gresie|faianta|tigla|olan"), false, "TILES"),
    ];

    static readonly (Regex Re, string? Hatch)[] OldElevation =
    [
        (new(@"clay tile|concrete tile|roof ?tile|tigla|olan|shingle|sindrila|slate|ardezie"), "ROOF_TILES"),
        (new(@"vapou?r|barrier|membrane|foil|\bdpc\b|damp|bitum|folie|barier"), null),
        (new(@"insul|wool|eps\b|xps\b|polystyren|pir\b|pur\b|vata|izola"), null),
        (new(@"plaster|render|gypsum|tencuial|gips|paint|vopsea|stucco|adhesive|adeziv"), null),
        (new(@"bca|aac|ytong|aerated|autoclav"), "BLOCK_600X250"),
        (new(@"block|\bcmu\b|boltari"), "BLOCK_400X200"),
        (new(@"brick|masonry|caramid|zidari|clinker"), "BRICK_240X63"),
        (new(@"stone|piatra|granit|marble|marmura|travertin|limestone|calcar|sandstone|ashlar"), "STONE_600X300"),
        (new(@"timber|wood|lemn|larch|cedar|pine|spruce|lambriu|siding|weatherboard|scandura"), "BOARDS_150"),
        (new(@"clad|placare|sandwich|trapez|tabla|zinc|copper|cupru|standing seam|faltuit|corrugated|cutat|metal sheet|steel sheet|roof sheet"), "METAL_SEAM_500"),
        (new(@"concrete|beton|screed|sapa|c\d\d/"), null),
        (new(@"\bsand\b|nisip"), "SAND"),
        (new(@"gravel|ballast|pietris|aggregate"), "GRAVEL"),
        (new(@"earth|soil|pamant|ground"), "EARTH"),
        (new(@"tile|ceramic|gresie|faianta|porcelain"), "TILE_300X300"),
    ];

    static string OldFold(string name) => MaterialGraphics.Normalise(name).Replace('ă', 'a').Replace('â', 'a').Replace('î', 'i').Replace('ș', 's').Replace('ş', 's').Replace('ț', 't').Replace('ţ', 't');

    static (bool Membrane, string? Hatch)? OldCutOf(string name)
    {
        var n = OldFold(name);
        foreach (var (re, m, h) in OldCut) if (re.IsMatch(n)) return (m, h);
        return null;
    }

    static string? OldElevationOf(string name)
    {
        var n = OldFold(name);
        foreach (var (re, h) in OldElevation) if (re.IsMatch(n)) return h;
        return null;
    }

    /// <summary>Names to compare: every word of the old tables alone, in a phrase, in pairs both ways; Romanian with and without diacritics; names nothing matches.</summary>
    static List<string> Names()
    {
        var words = OldCut.Select(x => x.Re.ToString()).Concat(OldElevation.Select(x => x.Re.ToString()))
            .SelectMany(r => r.Split('|')).Select(w => w.Replace(@"\b", "").Replace("?", "").Replace(@"\d\d/", "25/").Replace("c25/", "C25/30 ")).Distinct().ToList();
        var o = new List<string>
        {
            "", "Default", "Unknown", "Paint", "Finish", "Brick masonry", "Gypsum plaster", "Vapour barrier", "EPS insulation", "XPS insulation", "Render",
            "Ceramic tiles", "Cement screed", "Reinforced concrete", "Concrete C25/30", "Waterproofing", "Facing brick", "Timber cladding", "Metal cladding",
            "Clay roof tiles", "Concrete roof tile", "Bitumen sheet", "Stone wool", "Glass block", "Concrete block", "Zidărie BCA", "Zidărie din cărămidă plină",
            "Cărămidă eficientă", "Vată minerală bazaltică", "Polistiren expandat EPS 80", "Polistiren extrudat", "Șapă autonivelantă", "Țiglă ceramică",
            "Tencuială decorativă", "Beton armat C25/30", "Oțel S235", "Lemn de rășinoase", "Placaj", "Pământ", "Pietriș", "Nisip", "Lamă de aer", "Sticlă",
            "Gresie porțelanată", "Faianță", "Piatră naturală", "Ardezie", "Șindrilă bituminoasă", "Tablă cutată", "Tablă fălțuită", "Adeziv", "Vopsea lavabilă",
            "Aerated concrete block", "Ytong", "CMU", "Air gap", "Airport", "Sandstone", "Sand", "Sandwich panel", "Standing seam zinc", "Copper", "Aluminium",
            "CLT panel", "OSB 18", "Glulam beam", "Steel sheet", "Roof sheet", "Larch boards", "Weatherboard", "Granite", "Marble", "Limestone", "Ashlar",
            "Travertine", "Porcelain", "Ground", "Soil", "Earth", "Gravel", "Ballast", "Aggregate", "Fibre cement", "Rigid board", "PIR board", "PUR foam", "Batt",
            "DPC", "Damp proof course", "Foil", "Membrane", "Cavity", "Glazing", "Clinker", "Stucco", "Gypsum board", "Plywood", "Lumber", "Timber frame",
        };
        o.AddRange(words);
        o.AddRange(words.Select(w => $"Layer of {w} 20 mm"));
        o.AddRange(words.Select(w => w.ToUpperInvariant()));
        for (var i = 0; i < words.Count; i++)
            for (var j = 0; j < words.Count; j += 3)
                if (i != j) o.Add($"{words[i]} {words[j]}");
        return o.Distinct().ToList();
    }

    [Fact]
    public void Lapins_standard_as_data_gives_what_the_coded_tables_gave()
    {
        var names = Names();
        Assert.True(names.Count > 1500, $"only {names.Count} names");
        var bad = new List<string>();
        foreach (var name in names)
        {
            var old = OldCutOf(name);
            var (oldHatch, oldMembrane) = (old?.Membrane == true ? null : old?.Hatch, old?.Membrane ?? false);
            var (hatch, membrane) = MaterialGraphics.FromName(name);
            if (hatch != (old?.Hatch) || membrane != oldMembrane) bad.Add($"cut „{name}”: {hatch}/{membrane} for {old?.Hatch}/{oldMembrane}");
            foreach (var kind in new[] { "section", "plan" })
            {
                var r = MaterialGraphics.Resolve(null, kind, name);
                if (r.Hatch != oldHatch || r.Membrane != oldMembrane || r.HatchScale != 1 || r.Flex || r.Fill != null || r.Source != "name" || r.Standard != null)
                    bad.Add($"{kind} „{name}”: {r.Hatch}/{r.Membrane} for {oldHatch}/{oldMembrane}");
            }
            if (MaterialGraphics.ElevationFromName(name) != OldElevationOf(name)) bad.Add($"facade „{name}”: {MaterialGraphics.ElevationFromName(name)} for {OldElevationOf(name)}");
            var e = MaterialGraphics.Resolve(null, "elevation", name);
            if (e.Hatch != OldElevationOf(name) || e.Membrane) bad.Add($"elevation „{name}”: {e.Hatch} for {OldElevationOf(name)}");
        }
        Assert.True(bad.Count == 0, $"{bad.Count} of {names.Count} differ: {string.Join("; ", bad.Take(10))}");
    }

    [Fact]
    public void A_project_that_names_no_standard_draws_by_Lapins()
    {
        var f = new Fixture();
        Assert.Same(MaterialStandard.LapinCurrent, MaterialStandards.Of(f.Model));
        Assert.Null(Spatial.Project(f.Model)!.Attrs[MaterialStandards.Key]);
        var list = f.Session.Query("standards.materialList", [])!;
        Assert.Equal("lapin", (string?)list["current"]);
        Assert.Equal(["lapin", "iso-128-50", "din-1356", "ro-sr-iso-128-50", "bs-8888"], ((JsonArray)list["standards"]!).Select(s => (string)s!["id"]!).ToArray());
        Assert.All((JsonArray)list["standards"]!, s => Assert.True((bool)s!["builtin"]! && (bool)s["indicative"]!));
        Assert.All(MaterialStandard.BuiltIns, s => Assert.Contains("ndicative", s.Description));
    }

    [Fact]
    public void Every_built_in_standard_is_valid_against_a_new_project()
    {
        var f = new Fixture();
        foreach (var s in MaterialStandard.BuiltIns)
        {
            var c = MaterialStandards.Checked(f.Model, s.ToJson());
            Assert.Equal(s.Rules.Count, c.Rules.Count);
            // Every pattern a rule names is a built-in one or the standard's own.
            foreach (var r in s.Rules)
                foreach (var g in new[] { r.Section, r.Plan, r.Elevation }.OfType<ViewGraphics>())
                    foreach (var h in new[] { g.Hatch }.Concat(g.ByScale?.Select(x => x.Fields.Hatch) ?? []).OfType<string>().Where(x => x.Length > 0))
                        Assert.True(HatchLibrary.Find(h, s.Patterns) != null, $"{s.Id}: no pattern {h}");
        }
    }

    static ResolvedMaterial R(string standard, string name, string kind = "section", double? scale = null) =>
        MaterialGraphics.Resolve(null, kind, name, scale: scale, standard: MaterialStandard.FindBuiltIn(standard)!);

    [Fact]
    public void Each_pack_resolves_the_common_materials_sensibly_in_its_languages()
    {
        foreach (var s in MaterialStandard.BuiltIns)
        {
            Assert.True(R(s.Id, "Vapour barrier").Membrane, s.Id);
            Assert.Null(R(s.Id, "Glass").Hatch);
            foreach (var n in new[] { "Brick masonry", "Concrete", "Reinforced concrete", "EPS insulation", "Mineral wool insulation", "Timber", "Steel", "Gravel", "Earth" })
                Assert.True(R(s.Id, n).Hatch != null, $"{s.Id}: {n} has no hatch");
            Assert.Equal(s.IsLapin ? "BRICK_240X63" : s.Id == "bs-8888" ? "BRICK_215X65" : s.Id == "ro-sr-iso-128-50" ? "BRICK_240X63" : "BRICK_240X71", R(s.Id, "Facing brick", "elevation").Hatch);
            Assert.Null(R(s.Id, "Gypsum plaster", "elevation").Hatch);
        }
        // ISO 128-50: the general 45° lines, finer at 1:100; masonry of other kinds at 135°; metal filled solid from 1:50.
        Assert.Equal("LINES45", R("iso-128-50", "Brick masonry").Hatch);
        Assert.Equal(1, R("iso-128-50", "Brick masonry", scale: 50).HatchScale);
        Assert.Equal(0.7, R("iso-128-50", "Brick masonry", scale: 100).HatchScale, 6);
        Assert.Equal("ISO_135", R("iso-128-50", "Concrete block").Hatch);
        Assert.Equal("STEEL", R("iso-128-50", "Steel", scale: 20).Hatch);
        Assert.Equal("SOLID", R("iso-128-50", "Steel", scale: 50).Hatch);
        Assert.Equal(0.13, R("iso-128-50", "Steel").HatchWeight);
        // DIN 1356, its names in German, folded (Dämmung, Mörtel).
        Assert.Equal("CROSS", R("din-1356", "Stahlbeton C30/37").Hatch);
        Assert.Equal("LINES45", R("din-1356", "Mauerwerk aus Ziegeln").Hatch);
        Assert.Equal("DIN_PORENBETON", R("din-1356", "Porenbeton-Planstein").Hatch);
        Assert.Equal("BATT", R("din-1356", "Wärmedämmung Mineralwolle").Hatch);
        Assert.Equal("INSULATION", R("din-1356", "XPS Hartschaum").Hatch);
        Assert.True(R("din-1356", "Abdichtung Bitumen").Membrane);
        Assert.Equal("SAND", R("din-1356", "Zementestrich").Hatch);
        Assert.Equal("SAND", R("din-1356", "Kalkzementmörtel").Hatch);
        Assert.Equal("ROOF_TILES", R("din-1356", "Dachziegel", "elevation").Hatch);
        // România (SR ISO 128-50): ISO's graphics, Romanian names with or without diacritics.
        Assert.Equal("LINES45", R("ro-sr-iso-128-50", "Zidărie din cărămidă").Hatch);
        Assert.Equal("ISO_AERATED", R("ro-sr-iso-128-50", "Zidărie BCA").Hatch);
        Assert.Equal("RCONCRETE", R("ro-sr-iso-128-50", "Beton armat C25/30").Hatch);
        Assert.Equal("CONCRETE", R("ro-sr-iso-128-50", "Beton simplu").Hatch);
        Assert.Equal("CROSS", R("ro-sr-iso-128-50", "Polistiren expandat").Hatch);
        Assert.Equal("BATT", R("ro-sr-iso-128-50", "Vată minerală").Hatch);
        Assert.True(R("ro-sr-iso-128-50", "Hidroizolație").Membrane);
        Assert.Null(R("ro-sr-iso-128-50", "Gresie porțelanată").Hatch);
        Assert.Equal("WOOD", R("ro-sr-iso-128-50", "Lemn de stejar").Hatch);
        // BS 8888 / BS 1192.
        Assert.Equal("BS_BLOCK", R("bs-8888", "Blockwork").Hatch);
        Assert.Equal("BS_STONE", R("bs-8888", "Natural stone").Hatch);
        Assert.Equal("GRAVEL", R("bs-8888", "Hardcore").Hatch);
    }

    [Fact]
    public void Names_match_in_every_language_a_rule_gives_and_fold_their_diacritics()
    {
        var f = new Fixture();
        f.Run("standards.materialSet", new JsonObject
        {
            ["standard"] = new JsonObject
            {
                ["id"] = "oak", ["name"] = "Oak", ["languages"] = new JsonArray("en", "de", "ro", "pl"),
                ["rules"] = new JsonArray(
                    new JsonObject { ["match"] = new JsonObject { ["names"] = new JsonObject { ["en"] = new JsonArray("oak"), ["de"] = new JsonArray("eiche", "fußboden"), ["ro"] = new JsonArray("stejar"), ["pl"] = new JsonArray("dąb") } }, ["section"] = new JsonObject { ["hatch"] = "WOOD", ["hatchScale"] = 2 } },
                    new JsonObject { ["match"] = new JsonObject { ["category"] = "Insulation" }, ["section"] = new JsonObject { ["hatch"] = "BATT" } }),
            },
        });
        var s = MaterialStandards.Find(Spatial.Project(f.Model), "oak")!;
        foreach (var n in new[] { "Oak floor", "Eiche massiv", "Fussboden Eiche", "FUSSBODEN", "Fußboden", "Stejar", "Parchet din stejar", "Dąb", "Deska dab" })
            Assert.True(MaterialGraphics.Resolve(null, "section", n, standard: s).Hatch == "WOOD", n);
        Assert.Null(MaterialGraphics.Resolve(null, "section", "Pine", standard: s).Hatch);
        // A rule of a category alone matches a material of it.
        var mat = new Entity("m", IfcClass.Material, "Anything", new JsonObject { ["category"] = "Insulation" });
        Assert.Equal("BATT", MaterialGraphics.Resolve(mat, "section", standard: s).Hatch);
        Assert.Equal("oak", MaterialGraphics.Resolve(mat, "section", standard: s).Standard);
    }

    // ------------------------------------------------------------ the project's standard

    static Fixture Project()
    {
        var f = new Fixture();
        f.Type("Test wall", ("Brick masonry", 0.25, true), ("Concrete", 0.1, false), ("Gypsum plaster", 0.015, false));
        // Concrete draws with a hatch of its own.
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Concrete"), ["section"] = new JsonObject { ["hatch"] = "TILES", ["hatchColor"] = "#ff0000" } });
        return f;
    }

    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;
    static ResolvedMaterial Look(Fixture f, string name, string kind = "section", double? scale = null) =>
        MaterialGraphics.Resolve(f.Model.Get(Mat(f, name)), kind, scale: scale, standard: MaterialStandards.Of(f.Model));

    [Fact]
    public void Applying_a_standard_keeps_the_materials_own_graphics_and_takes_its_patterns()
    {
        var f = Project();
        f.Run("standards.applyMaterials", new JsonObject { ["id"] = "din-1356" });
        var project = Spatial.Project(f.Model)!;
        Assert.Equal("din-1356", (string?)project.Attrs[MaterialStandards.Key]);
        Assert.Contains("DIN_PORENBETON", (string)project.Attrs["hatchPatterns"]!);
        Assert.Equal("LINES45", Look(f, "Brick masonry").Hatch);
        Assert.Equal(0.7, Look(f, "Brick masonry", scale: 100).HatchScale, 6);
        // Its own pattern stays, with its own scale (never the standard's), and its colour.
        var concrete = Look(f, "Concrete", scale: 100);
        Assert.Equal("TILES", concrete.Hatch);
        Assert.Equal(1, concrete.HatchScale);
        Assert.Equal("#ff0000", concrete.HatchColor);
        Assert.Equal(0.13, concrete.HatchWeight);
        // The lists say so.
        var listed = ((JsonArray)f.Session.Query("material.list", [])!).First(x => (string?)x!["name"] == "Brick masonry")!;
        Assert.Equal("din-1356", (string?)listed["section"]!["standard"]);
        Assert.Equal("din-1356", (string?)f.Session.Query("standards.materialList", [])!["current"]);
        // A material of no category takes the rule's.
        Assert.Equal("Structure", f.Model.Get(Mat(f, "Brick masonry"))!.Str("category"));
        // Back to Lapin: the project names none again.
        f.Run("standards.applyMaterials", new JsonObject { ["id"] = "lapin" });
        Assert.Null(Spatial.Project(f.Model)!.Attrs[MaterialStandards.Key]);
        Assert.Equal("MASONRY", Look(f, "Brick masonry").Hatch);
    }

    [Fact]
    public void Applying_with_overwrite_takes_off_what_the_standard_sets_and_is_one_undo_step()
    {
        var f = Project();
        var before = f.Model.Get(Mat(f, "Concrete"))!.Attrs.ToJsonString();
        f.Run("standards.applyMaterials", new JsonObject { ["id"] = "din-1356", ["overwrite"] = true });
        var concrete = f.Model.Get(Mat(f, "Concrete"))!;
        // DIN's concrete rule sets the hatch; the pens set the hatch's colour, weight and line type: both taken off.
        Assert.Null(concrete.Attrs["section"]);
        Assert.Equal("CONCRETE", Look(f, "Concrete").Hatch);
        Assert.Null(Look(f, "Concrete").HatchColor);
        f.Session.Undo();
        Assert.Equal(before, f.Model.Get(Mat(f, "Concrete"))!.Attrs.ToJsonString());
        Assert.Null(Spatial.Project(f.Model)!.Attrs[MaterialStandards.Key]);
        Assert.Null(Spatial.Project(f.Model)!.Attrs["hatchPatterns"]);
    }

    [Fact]
    public void A_preview_shows_before_and_after_and_changes_nothing()
    {
        var f = Project();
        var snapshot = string.Join("\n", f.Model.Entities.Select(e => e.Id + e.Attrs.ToJsonString()));
        var canUndo = f.Session.History.CanUndo;
        var label = f.Session.History.UndoLabel;
        var p = f.Session.Query("standards.applyPreview", new JsonObject { ["id"] = "iso-128-50", ["overwrite"] = true, ["scale"] = 100 })!;
        var viaCommand = f.Run("standards.applyMaterials", new JsonObject { ["id"] = "iso-128-50", ["overwrite"] = true, ["scale"] = 100, ["preview"] = true })!;
        Assert.True(JsonNode.DeepEquals(p, viaCommand));
        Assert.Equal(snapshot, string.Join("\n", f.Model.Entities.Select(e => e.Id + e.Attrs.ToJsonString())));
        Assert.Equal(canUndo, f.Session.History.CanUndo);
        Assert.Equal(label, f.Session.History.UndoLabel);
        Assert.Equal("lapin", (string?)p["from"]);
        var brick = ((JsonArray)p["materials"]!).First(x => (string?)x!["name"] == "Brick masonry")!;
        Assert.True((bool)brick["changed"]!);
        Assert.Equal("MASONRY", (string?)brick["before"]!["section"]!["hatch"]);
        Assert.Equal("LINES45", (string?)brick["after"]!["section"]!["hatch"]);
        Assert.Equal(0.7, (double)brick["after"]!["section"]!["hatchScale"]!, 6);
        var concrete = ((JsonArray)p["materials"]!).First(x => (string?)x!["name"] == "Concrete")!;
        Assert.Equal(["hatch", "hatchColor"], ((JsonArray)concrete["cleared"]!["section"]!).Select(x => (string)x!).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Built_in_standards_are_duplicated_to_be_changed_never_changed_or_removed()
    {
        var f = new Fixture();
        Assert.Throws<CommandException>(() => f.Run("standards.materialRemove", new JsonObject { ["id"] = "lapin" }));
        Assert.Throws<CommandException>(() => f.Run("standards.materialSet", new JsonObject { ["standard"] = MaterialStandard.FindBuiltIn("din-1356")!.ToJson() }));
        var id = (string)f.Run("standards.materialDuplicate", new JsonObject { ["id"] = "din-1356", ["name"] = "Büro DIN" })!["id"]!;
        Assert.Equal("buro-din", id);
        var got = f.Session.Query("standards.materialGet", new JsonObject { ["id"] = id })!;
        Assert.False((bool)got["builtin"]!);
        Assert.Equal("din-1356", (string?)got["standard"]!["basedOn"]);
        // Edited: its first rule's hatch.
        var json = (JsonObject)got["standard"]!.DeepClone();
        ((JsonObject)json["rules"]![1]!)["section"] = new JsonObject { ["hatch"] = "CROSS" };
        f.Run("standards.materialSet", new JsonObject { ["standard"] = json });
        Assert.Equal("CROSS", MaterialGraphics.Resolve(null, "section", "EPS", standard: MaterialStandards.Find(Spatial.Project(f.Model), id)).Hatch);
        Assert.Equal("INSULATION", R("din-1356", "EPS").Hatch);
        // A bad rule is refused: an unknown pattern, an expression that is not one, a rule that matches nothing.
        foreach (var bad in new JsonNode[]
        {
            new JsonObject { ["match"] = new JsonObject { ["names"] = new JsonArray("x") }, ["section"] = new JsonObject { ["hatch"] = "NO_SUCH" } },
            new JsonObject { ["match"] = new JsonObject { ["names"] = new JsonArray("(") }, ["section"] = new JsonObject { ["hatch"] = "CROSS" } },
            new JsonObject { ["match"] = new JsonObject(), ["section"] = new JsonObject { ["hatch"] = "CROSS" } },
        })
            Assert.Throws<CommandException>(() => f.Run("standards.materialSet", new JsonObject { ["standard"] = new JsonObject { ["name"] = "Bad", ["rules"] = new JsonArray(bad.DeepClone()) } }));
        // Applied, then removed: the project goes back to Lapin's.
        f.Run("standards.applyMaterials", new JsonObject { ["id"] = id });
        Assert.Equal(id, MaterialStandards.Of(f.Model).Id);
        f.Run("standards.materialRemove", new JsonObject { ["id"] = id });
        Assert.Same(MaterialStandard.LapinCurrent, MaterialStandards.Of(f.Model));
        f.Session.Undo();
        Assert.Equal(id, MaterialStandards.Of(f.Model).Id);
        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Assert.Empty(MaterialStandards.Own(Spatial.Project(f.Model)));
    }

    // ------------------------------------------------------------ from a project, through a file

    [Fact]
    public void A_standard_made_from_a_projects_materials_draws_them_the_same_in_another_project()
    {
        var a = new Fixture();
        a.Type("Wall", ("Brick masonry", 0.25, true), ("Mineral wool", 0.1, false), ("Larch cladding", 0.02, false), ("Odd stuff", 0.01, false));
        a.Run("hatch.import", new JsonObject { ["text"] = "*MY_DOTS, dots\n0, 0,0, 1,1, 0,-1\n" });
        a.Run("material.update", new JsonObject { ["id"] = Mat(a, "Brick masonry"), ["section"] = new JsonObject { ["hatch"] = "MY_DOTS", ["hatchScale"] = 1.5, ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatch"] = "SOLID" }) }, ["plan"] = new JsonObject { ["fill"] = "#eeeeee" } });
        a.Run("material.update", new JsonObject { ["id"] = Mat(a, "Larch cladding"), ["elevation"] = new JsonObject { ["hatch"] = "VERTICAL", ["hatchAngle"] = 5 } });
        a.Run("material.update", new JsonObject { ["id"] = Mat(a, "Odd stuff"), ["membrane"] = true });
        var made = a.Run("standards.fromProject", new JsonObject { ["name"] = "Office 2026", ["author"] = "Office", ["country"] = "RO", ["norm"] = "own" })!;
        var id = (string)made["id"]!;
        var s = MaterialStandards.Find(Spatial.Project(a.Model), id)!;
        Assert.Equal("lapin", s.BasedOn);
        Assert.Equal(3 + MaterialStandard.LapinCurrent.Rules.Count, s.Rules.Count);
        Assert.Contains(s.Patterns, p => p.Name == "MY_DOTS");
        var text = (string)a.Session.Query("standards.export", new JsonObject { ["name"] = "Office", ["materialStandards"] = new JsonArray(id), ["only"] = "materialGraphics" })!["text"]!;
        var file = JsonNode.Parse(text)!.AsObject();
        Assert.Empty((JsonObject)file["drafting"]!);

        var b = new Fixture();
        b.Type("Wall", ("Brick masonry", 0.25, true), ("Mineral wool", 0.1, false), ("Larch cladding", 0.02, false), ("Odd stuff", 0.01, false));
        var read = b.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!;
        Assert.Equal(id, (string?)read["materialStandards"]![0]!["id"]);
        Assert.False((bool)read["materialStandards"]![0]!["exists"]!);
        Assert.Contains((JsonArray)read["entries"]!, e => (string?)e!["kind"] == "materialStandard");
        b.Run("standards.importFrom", new JsonObject { ["text"] = text });
        // Importing never applies it.
        Assert.Same(MaterialStandard.LapinCurrent, MaterialStandards.Of(b.Model));
        b.Run("standards.applyMaterials", new JsonObject { ["id"] = id });
        foreach (var name in new[] { "Brick masonry", "Mineral wool", "Larch cladding", "Odd stuff" })
            foreach (var kind in MaterialGraphics.Kinds)
                foreach (var scale in new double?[] { null, 50, 200 })
                {
                    var (x, y) = (Look(a, name, kind, scale).ToJson(), Look(b, name, kind, scale).ToJson());
                    // A material's membrane flag holds in every kind of view; a standard's is a cut's (a facade: no hatch, as above).
                    foreach (var o in new[] { x, y }) { o.Remove("source"); o.Remove("standard"); o.Remove("step"); o.Remove("color"); if (kind == "elevation") o.Remove("membrane"); }
                    Assert.True(JsonNode.DeepEquals(x, y), $"{name} {kind} 1:{scale}: {x.ToJsonString()} ≠ {y.ToJsonString()}");
                }
    }

    [Fact]
    public void A_file_s_standards_come_in_by_the_merge_choices()
    {
        static JsonObject Std(string hatch) => new()
        {
            ["id"] = "office", ["name"] = "Office", ["version"] = "1.0.0",
            ["rules"] = new JsonArray(new JsonObject { ["match"] = new JsonObject { ["names"] = new JsonArray("brick") }, ["section"] = new JsonObject { ["hatch"] = hatch } }),
        };
        var a = new Fixture();
        a.Run("standards.materialSet", new JsonObject { ["standard"] = Std("CROSS") });
        var text = (string)a.Session.Query("standards.export", new JsonObject { ["name"] = "Office" })!["text"]!;
        Assert.NotNull(JsonNode.Parse(text)!["materialGraphics"]);

        Fixture B() { var b = new Fixture(); b.Run("standards.materialSet", new JsonObject { ["standard"] = Std("TILES") }); return b; }
        string HatchIn(Fixture b, string id) => MaterialGraphics.Resolve(null, "section", "Brick", standard: MaterialStandards.Find(Spatial.Project(b.Model), id)).Hatch!;

        var same = new Fixture();
        same.Run("standards.materialSet", new JsonObject { ["standard"] = Std("CROSS") });
        Assert.True((bool)same.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!["materialStandards"]![0]!["same"]!);
        var kept = same.Run("standards.importFrom", new JsonObject { ["text"] = text })!;
        Assert.Equal(0, (int)kept["added"]! + (int)kept["updated"]!);

        var updated = B();
        var row = (JsonArray)updated.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!["materialStandards"]!;
        Assert.True((bool)row[0]!["exists"]! && !(bool)row[0]!["same"]!);
        updated.Run("standards.importFrom", new JsonObject { ["text"] = text });
        Assert.Equal("CROSS", HatchIn(updated, "office"));

        var both = B();
        var r = both.Run("standards.importFrom", new JsonObject { ["text"] = text, ["choices"] = new JsonObject { ["materialStandard:office"] = "both" } })!;
        Assert.Equal("Office (2)", (string?)r["renamed"]![0]);
        Assert.Equal("TILES", HatchIn(both, "office"));
        Assert.Equal("CROSS", HatchIn(both, "office-2"));

        var skipped = B();
        skipped.Run("standards.importFrom", new JsonObject { ["text"] = text, ["all"] = "skip" });
        Assert.Equal("TILES", HatchIn(skipped, "office"));
        Assert.Single(MaterialStandards.Own(Spatial.Project(skipped.Model)));

        // A built-in one written as data comes back the same; changed, it never changes the built-in: a copy beside it.
        var builtIn = (string)a.Session.Query("standards.export", new JsonObject { ["materialStandards"] = new JsonArray("iso-128-50"), ["only"] = "materialGraphics" })!["text"]!;
        var c = new Fixture();
        var keptBuiltIn = c.Run("standards.importFrom", new JsonObject { ["text"] = builtIn })!;
        Assert.Equal(1, (int)keptBuiltIn["kept"]!);
        Assert.Equal(0, (int)keptBuiltIn["added"]!);
        var changed = JsonNode.Parse(builtIn)!.AsObject();
        changed["materialGraphics"]!["standards"]![0]!["name"] = "ISO, my way";
        c.Run("standards.importFrom", new JsonObject { ["text"] = changed.ToJsonString() });
        Assert.Equal("ISO 128-50", MaterialStandard.FindBuiltIn("iso-128-50")!.Name);
        Assert.Contains(MaterialStandards.Own(Spatial.Project(c.Model)), s => s.Name == "ISO, my way" && s.Id == "iso-128-50-2");
    }

    // ------------------------------------------------------------ the sample sheet

    [Fact]
    public void The_sample_sheet_draws_the_reference_details_with_the_standard()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var o = f.Session.Query("standards.previewSheet", new JsonObject { ["id"] = "din-1356", ["scale"] = 50, ["pdf"] = true, ["svg"] = true })!;
        Assert.Equal("din-1356", (string?)o["standard"]!["id"]);
        var details = (JsonArray)o["details"]!;
        Assert.Equal(MaterialReferenceDetails.All.Select(d => d.Name).ToArray(), details.Select(d => (string)d!["name"]!).ToArray());
        Assert.All(details, d => Assert.All((JsonArray)d!["views"]!, v => Assert.NotNull(v!["drawing"])));
        Assert.StartsWith("%PDF", Encoding.Latin1.GetString(Convert.FromBase64String((string)o["pdf"]!), 0, 4));
        Assert.StartsWith("<svg", ((string)o["svg"]!).TrimStart());
        // Drawn with the standard: its stone pattern for nothing here, but its masonry hatch in 45° lines at its thin pen.
        Assert.Contains("0.13", (string)o["svg"]!);
        // The open project is untouched; a standard given inline (a store's pack not yet installed) draws too.
        Assert.Same(MaterialStandard.LapinCurrent, MaterialStandards.Of(f.Model));
        var inline = f.Session.Query("standards.previewSheet", new JsonObject { ["standard"] = MaterialStandard.FindBuiltIn("bs-8888")!.ToJson().Also(j => j["id"] = "my-bs"), ["details"] = new JsonArray("composite-walls"), ["drawings"] = false })!;
        Assert.Single((JsonArray)inline["details"]!);
        Assert.Equal("my-bs", (string?)inline["standard"]!["id"]);
    }

    /// <summary>
    /// The store's example packs (<c>extensions-packs/standards/&lt;id&gt;/</c>) are what the engine writes for each built-in standard:
    /// <c>LAPIN_WRITE_PACKS=1</c> writes them again (and each pack's sample sheet as preview.pdf, to be turned into screenshot.png).
    /// </summary>
    [Fact]
    public void The_store_packs_are_the_built_in_standards()
    {
        var write = Environment.GetEnvironmentVariable("LAPIN_WRITE_PACKS") == "1";
        var root = Path.Combine(RepoRoot(), "extensions-packs", "standards");
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        foreach (var s in MaterialStandard.BuiltIns)
        {
            var dir = Path.Combine(root, s.Id);
            var text = (string)f.Session.Query("standards.export", new JsonObject { ["name"] = s.Name, ["materialStandards"] = new JsonArray(s.Id), ["only"] = "materialGraphics" })!["text"]!;
            var file = Path.Combine(dir, s.Id + ".lapinstandards");
            if (write)
            {
                Directory.CreateDirectory(dir);
                // The header's date and source left out, so the file changes only when the standard does.
                var o = JsonNode.Parse(text)!.AsObject();
                o.Remove("lapin");
                File.WriteAllText(file, o.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
                var pdf = f.Session.Query("standards.previewSheet", new JsonObject { ["id"] = s.Id, ["scale"] = 50, ["pdf"] = true, ["drawings"] = false })!;
                File.WriteAllBytes(Path.Combine(dir, "preview.pdf"), Convert.FromBase64String((string)pdf["pdf"]!));
                continue;
            }
            if (!File.Exists(file)) continue;
            var kept = JsonNode.Parse(File.ReadAllText(file))!;
            Assert.True(JsonNode.DeepEquals(kept["materialGraphics"]!["standards"]![0], s.Json), $"{file} is not the built-in „{s.Id}” (LAPIN_WRITE_PACKS=1 writes it again).");
            // The file reads back as the built-in, the same.
            var b = new Fixture();
            Assert.True((bool)b.Session.Query("standards.readPack", new JsonObject { ["text"] = File.ReadAllText(file) })!["materialStandards"]![0]!["same"]!);
        }
    }

    static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));

    // ------------------------------------------------------------ follow-ups of stages 1 and 0b

    [Fact]
    public void Renaming_or_removing_a_line_type_follows_it_into_materials_and_standards()
    {
        var f = new Fixture();
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Mine", ["dashes"] = new JsonArray(2, 1) });
        f.Type("W", ("Brick masonry", 0.25, true));
        var id = Mat(f, "Brick masonry");
        f.Run("material.update", new JsonObject
        {
            ["id"] = id,
            ["section"] = new JsonObject
            {
                ["hatchLineType"] = "Mine", ["cutLine"] = new JsonObject { ["lineType"] = "Mine", ["weight"] = 0.5 }, ["separatorLine"] = new JsonObject { ["lineType"] = "Mine" },
                ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatchLineType"] = "Mine", ["cutLine"] = new JsonObject { ["lineType"] = "Mine" } }),
            },
            ["elevation"] = new JsonObject { ["hatchLineType"] = "Mine" },
        });
        f.Run("standards.materialSet", new JsonObject
        {
            ["standard"] = new JsonObject
            {
                ["id"] = "lt", ["name"] = "LT", ["pens"] = new JsonObject { ["hatch"] = new JsonObject { ["lineType"] = "Mine" } },
                ["rules"] = new JsonArray(new JsonObject { ["match"] = new JsonObject { ["names"] = new JsonArray("x") }, ["section"] = new JsonObject { ["hatchLineType"] = "Mine" } }),
            },
        });
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Mine", ["newName"] = "Ours" });
        var text = f.Model.Get(id)!.Attrs.ToJsonString() + Spatial.Project(f.Model)!.Attrs[MaterialStandards.ListKey]!.ToJsonString();
        Assert.DoesNotContain("\"Mine\"", text);
        Assert.Equal(6, Regex.Matches(f.Model.Get(id)!.Attrs.ToJsonString(), "\"Ours\"").Count);
        Assert.Equal(2, Regex.Matches(Spatial.Project(f.Model)!.Attrs[MaterialStandards.ListKey]!.ToJsonString(), "\"Ours\"").Count);
        Assert.Equal("Ours", MaterialGraphics.Resolve(f.Model.Get(id), "section", scale: 100).CutLine!.LineType);
        f.Run("standards.removeLineType", new JsonObject { ["name"] = "Ours" });
        var after = f.Model.Get(id)!;
        Assert.DoesNotContain("Ours", after.Attrs.ToJsonString());
        Assert.Equal(0.5, MaterialGraphics.Resolve(after, "section").CutLine!.Weight);
        Assert.Null(MaterialGraphics.Resolve(after, "section").HatchLineType);
        Assert.DoesNotContain("Ours", Spatial.Project(f.Model)!.Attrs[MaterialStandards.ListKey]!.ToJsonString());
    }

    [Fact]
    public void A_hatch_origin_s_grip_point_and_the_origin_a_dropped_grip_sets_go_both_ways()
    {
        var f = new Fixture();
        var wall = f.Wall(1, 2, 5, 2, height: 3);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south", ["scale"] = 50 })!["id"]!;
        // None of its own: [0, 0], the axis's start; in plan the start itself.
        var none = f.Session.Query("element.hatchOriginPoint", new JsonObject { ["id"] = wall, ["view"] = plan })!;
        Assert.False((bool)none["own"]!);
        Assert.Equal(1, (double)none["point"]![0]!, 6);
        Assert.Equal(2, (double)none["point"]![1]!, 6);
        // Its own, as the node's props show it.
        var diff = f.RunDiff("element.setHatchOrigin", new JsonObject { ["id"] = wall, ["origin"] = new JsonArray(0.5, 0.2) });
        var node = diff.Nodes.First(n => n.Id == wall);
        Assert.Equal(0.5, (double)node.Props["hatchOrigin"]![0]!, 9);
        foreach (var view in new[] { plan, south })
        {
            var p = f.Session.Query("element.hatchOriginPoint", new JsonObject { ["id"] = wall, ["view"] = view })!;
            Assert.True((bool)p["own"]!);
            var back = f.Session.Query("element.hatchOriginAt", new JsonObject { ["id"] = wall, ["view"] = view, ["point"] = p["point"]!.DeepClone() })!;
            Assert.Equal(0.5, (double)back["origin"]![0]!, 6);
            Assert.Equal(0.2, (double)back["origin"]![1]!, 6);
        }
        // In plan: 0.5 along the axis (+x), 0.2 to its left (+y).
        var pp = f.Session.Query("element.hatchOriginPoint", new JsonObject { ["id"] = wall, ["view"] = plan })!;
        Assert.Equal(1.5, (double)pp["point"]![0]!, 6);
        Assert.Equal(2.2, (double)pp["point"]![1]!, 6);
        Assert.Throws<CommandException>(() => f.Session.Query("element.hatchOriginAt", new JsonObject { ["id"] = wall, ["view"] = plan }));
    }
}

static class JsonAlso
{
    public static JsonObject Also(this JsonObject o, Action<JsonObject> a) { a(o); return o; }
}
