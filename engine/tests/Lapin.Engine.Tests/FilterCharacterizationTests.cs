using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Clash;
using Lapin.Cost;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// What each of Lapin's element filters selects (audit stage 2, D3/D4): view filters and Quick select, a field schedule's
/// filter, a task's rule, a clash search set and an article rule, pinned on one varied model — native walls of two layered
/// types and one without a type, a door in a wall and one taken off its storey, an IFC file's references (a wall of a file
/// type with a layer set, a wall of a brick material, a door in an opening, a block of nothing), a user's parameters, a
/// number written with a comma, phases. Each table is what the filter takes, so every change to what one selects is made
/// on purpose and written here.
/// </summary>
public class FilterCharacterizationTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-filter-characterization").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>Test-only edits: an element taken off its storey, a type taken off an element.</summary>
    sealed class Probe : ILapinModule
    {
        public string Id => "probe";
        public string Name => "Probe";
        public void Register(IModuleRegistry r)
        {
            r.Command("uncontain", "Uncontain", (ctx, a) => { ctx.Unrelate(IfcClass.RelContainedInSpatialStructure, (string)a["id"]!); return null; });
            r.Command("untype", "Untype", (ctx, a) => { ctx.Unrelate(IfcClass.RelDefinesByType, (string)a["id"]!); return null; });
        }
    }

    static string Gid(string key) => Guid("filter-characterization", key);

    /// <summary>
    /// A file in metres on one storey "Ground": RW1 of the file's type WT-EXT with a layer set (Concrete core, Insulation) and
    /// Pset_WallCommon.FireRating REI 90; RW2 of the material Brick; RD a door filling an opening in RW1, on no storey; a block.
    /// </summary>
    string Foreign()
    {
        var f = new Step();
        string G(string key) => S(Gid(key));
        var origin = f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
        var world = f.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
        var ctx = f.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
        var m = f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), Null, E("METRE"));
        var project = f.Add("IFCPROJECT", G("project"), Null, S("Foreign"), Null, Null, Null, Null, L([ctx]), f.Add("IFCUNITASSIGNMENT", L([m])));
        var pl = f.Add("IFCLOCALPLACEMENT", Null, world);
        var building = f.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, pl, Null, Null, E("ELEMENT"), Null, Null, Null);
        var storeyPlace = f.Add("IFCLOCALPLACEMENT", pl, world);
        var storey = f.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Ground"), Null, Null, storeyPlace, Null, Null, E("ELEMENT"), "0.");
        f.Add("IFCRELAGGREGATES", G("agg1"), Null, Null, Null, project, L([building]));
        f.Add("IFCRELAGGREGATES", G("agg2"), Null, Null, Null, building, L([storey]));
        string Box(double cx, double cy, double dx, double dy, double depth)
        {
            var prof = f.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, f.Add("IFCAXIS2PLACEMENT2D", f.Add("IFCCARTESIANPOINT", $"({R(cx)},{R(cy)})"), Null), R(dx), R(dy));
            var solid = f.Add("IFCEXTRUDEDAREASOLID", prof, f.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null), f.Add("IFCDIRECTION", "(0.,0.,1.)"), R(depth));
            return f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([f.Add("IFCSHAPEREPRESENTATION", ctx, S("Body"), S("SweptSolid"), L([solid]))]));
        }
        string Label(string name, string value) => f.Add("IFCPROPERTYSINGLEVALUE", S(name), Null, Typed("IFCLABEL", S(value)), Null);
        string Place() => f.Add("IFCLOCALPLACEMENT", storeyPlace, world);
        var rw1 = f.Add("IFCWALL", G("rw1"), Null, S("RW1"), Null, Null, Place(), Box(2.5, 10, 5, 0.3, 3), Null, E("STANDARD"));
        var rw2 = f.Add("IFCWALL", G("rw2"), Null, S("RW2"), Null, Null, Place(), Box(2.5, 14, 5, 0.25, 3), Null, E("STANDARD"));
        var type = f.Add("IFCWALLTYPE", G("wt"), Null, S("WT-EXT"), Null, Null, Null, Null, Null, Null, E("STANDARD"));
        f.Add("IFCRELDEFINESBYTYPE", G("wt-rel"), Null, Null, Null, L([rw1]), type);
        var concrete = f.Add("IFCMATERIAL", S("Concrete"), Null, Null);
        var insulation = f.Add("IFCMATERIAL", S("Insulation"), Null, S("Insulation"));
        var set = f.Add("IFCMATERIALLAYERSET", L([f.Add("IFCMATERIALLAYER", concrete, R(0.2), Null, Null, Null, Null, Null), f.Add("IFCMATERIALLAYER", insulation, R(0.1), Null, Null, Null, Null, Null)]), S("Ext"), Null);
        var usage = f.Add("IFCMATERIALLAYERSETUSAGE", set, E("AXIS2"), E("POSITIVE"), R(0), Null);
        f.Add("IFCRELASSOCIATESMATERIAL", G("mat1"), Null, Null, Null, L([rw1]), usage);
        var brick = f.Add("IFCMATERIAL", S("Brick"), Null, Null);
        f.Add("IFCRELASSOCIATESMATERIAL", G("mat2"), Null, Null, Null, L([rw2]), brick);
        f.Add("IFCRELDEFINESBYPROPERTIES", G("p1-rel"), Null, Null, Null, L([rw1]), f.Add("IFCPROPERTYSET", G("p1"), Null, S("Pset_WallCommon"), Null, L([Label("FireRating", "REI 90")])));
        var opening = f.Add("IFCOPENINGELEMENT", G("op"), Null, S("Opening"), Null, Null, Place(), Box(2, 10, 0.9, 0.4, 2.1), Null, E("OPENING"));
        f.Add("IFCRELVOIDSELEMENT", G("voids"), Null, Null, Null, rw1, opening);
        var door = f.Add("IFCDOOR", G("rd"), Null, S("RD"), Null, Null, Place(), Box(2, 10, 0.9, 0.05, 2.1), Null, R(2.1), R(0.9), E("DOOR"), E("SINGLE_SWING_LEFT"), Null);
        f.Add("IFCRELFILLSELEMENT", G("fills"), Null, Null, Null, opening, door);
        var block = f.Add("IFCBUILDINGELEMENTPROXY", G("block"), Null, S("Block"), Null, Null, Place(), Box(10, 10, 1, 2, 0.5), Null, E("ELEMENT"));
        f.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", G("contained"), Null, Null, Null, L([rw1, rw2, block]), storey);
        var path = Path.Combine(_dir, "foreign.ifc");
        f.Write(path, "foreign.ifc", "ViewDefinition [ReferenceView_V1.2]");
        return path;
    }

    sealed record Model(Fixture F, Dictionary<string, string> Labels, Dictionary<string, string> Ids);

    /// <summary>
    /// The model: on the first storey A (type Brick 300: Brick core, Plaster; Fire rating REI 120, Code „1,200”), B (type
    /// Partition 100, Gypsum board; Fire rating EI 60, 3.5 m high, created in the existing phase), C (of Brick 300 with its
    /// type taken off, so its own layers and the type's name; Code 900), the door DA in A and DB in B taken off its storey;
    /// the file's RW1, RW2, RD (on the storey, as a reference import puts it) and Block.
    /// </summary>
    Model Varied()
    {
        var f = new Fixture(new ArchitectureModule(), new PhasesModule(), new PlanningModule(), new ClashModule(), new CostModule(), new Probe());
        IfcImport.Run(f.Session, Foreign(), new IfcImportOptions("reference"), out _);
        var brick = f.Type("Brick 300", ("Brick", 0.25, true), ("Plaster", 0.02, false));
        var partition = f.Type("Partition 100", ("Gypsum board", 0.1, true));
        var a = f.Wall(0, 0, 6, 0, brick, height: 3);
        var b = f.Wall(0, 4, 6, 4, partition, height: 3.5);
        var c = f.Wall(0, 8, 6, 8, brick, height: 2.8);
        f.Run("probe.untype", new JsonObject { ["id"] = c });
        var da = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 2 })!["id"]!;
        var db = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = b, ["along"] = 2 })!["id"]!;
        f.Run("probe.uncontain", new JsonObject { ["id"] = db });
        void Param(string id, string name, string kind, JsonNode value) => f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(id), ["name"] = name, ["kind"] = kind, ["value"] = value });
        Param(a, "Fire rating", "text", "REI 120");
        Param(b, "Fire rating", "text", "EI 60");
        Param(a, "Code", "text", "1,200");
        Param(c, "Code", "text", "900");
        f.Run("phases.assign", new JsonObject { ["ids"] = new JsonArray(b), ["created"] = "existing" });
        var ids = new Dictionary<string, string>
        {
            ["A"] = a, ["B"] = b, ["C"] = c, ["DA"] = da, ["DB"] = db,
            ["RW1"] = Gid("rw1"), ["RW2"] = Gid("rw2"), ["RD"] = Gid("rd"), ["Block"] = Gid("block"),
        };
        return new(f, ids.ToDictionary(x => x.Value, x => x.Key), ids);
    }

    static string Names(Model md, IEnumerable<string> ids) =>
        string.Join(" ", ids.Select(id => md.Labels.GetValueOrDefault(id)).OfType<string>().Order(StringComparer.Ordinal));

    /// <summary>The elements of the model the tables are about.</summary>
    static IEnumerable<Entity> Ours(Model md) => md.Ids.Values.Select(id => md.F.Model.Get(id)!);

    static JsonObject Rule(string field, string op, string? value = null, string? name = null)
    {
        var o = new JsonObject { ["field"] = field, ["op"] = op };
        if (value != null) o["value"] = value;
        if (name != null) o["name"] = name;
        return o;
    }

    static string Table(IEnumerable<(string Rule, string Takes)> rows) => string.Join("\n", rows.Select(r => $"{r.Rule} => {r.Takes}"));

    static void Pinned(string expected, string actual, string what)
    {
        if (expected.Trim() == actual.Trim()) return;
        var path = Path.Combine(Path.GetTempPath(), $"lapin-characterization-{what}.txt");
        File.WriteAllText(path, actual);
        Assert.Fail($"{what} selects differently (now in {path}):\n{actual}");
    }

    // ---- View filters (VisibilityGraphics, viewFilter.count) and Quick select (select.query) ----

    static readonly (string Label, JsonObject Rule)[] ViewRules =
    [
        ("type = Brick 300", Rule("type", "equals", "Brick 300")),
        ("type = WT-EXT", Rule("type", "equals", "WT-EXT")),
        ("type ≠ Brick 300", Rule("type", "notEquals", "Brick 300")),
        ("type has none", Rule("type", "hasNoValue")),
        ("storey = Ground", Rule("storey", "equals", "Ground")),
        ("storey has none", Rule("storey", "hasNoValue")),
        ("material = Brick", Rule("material", "equals", "Brick")),
        ("material = Plaster", Rule("material", "equals", "Plaster")),
        ("material = Concrete", Rule("material", "equals", "Concrete")),
        ("material has none", Rule("material", "hasNoValue")),
        ("class = IfcDoor", Rule("class", "equals", "IfcDoor")),
        ("class = Door", Rule("class", "equals", "Door")),
        ("class contains Wall", Rule("class", "contains", "Wall")),
        ("category = walls", Rule("category", "equals", "walls")),
        ("name contains W", Rule("name", "contains", "W")),
        ("Fire rating begins REI", Rule("param", "beginsWith", "REI", "Fire rating")),
        ("Fire rating has value", Rule("param", "hasValue", null, "Fire rating")),
        ("FireRating = REI 90", Rule("param", "equals", "REI 90", "FireRating")),
        ("height > 3", Rule("param", "greater", "3", "height")),
        ("height 3..3.5", Rule("param", "between", "3..3.5", "height")),
        ("Code = 1200", Rule("param", "equals", "1200", "Code")),
        ("Code = 1.2", Rule("param", "equals", "1.2", "Code")),
        ("Code > 1000", Rule("param", "greater", "1000", "Code")),
        ("Code < 1000", Rule("param", "less", "1000", "Code")),
        ("phase = existing", Rule("phase", "equals", "existing")),
        ("phase = new-construction", Rule("phase", "equals", "new-construction")),
        ("phase = New construction", Rule("phase", "equals", "New construction")),
        ("phase has none", Rule("phase", "hasNoValue")),
        ("group has value", Rule("group", "hasValue")),
        ("link has none", Rule("link", "hasNoValue")),
        ("predefined has value", Rule("predefinedType", "hasValue")),
    ];

    // Changed on purpose by the one engine (audit D3), from what view filters took before:
    // - type: a reference's file type counts (RW1 is of WT-EXT, so it has a type);
    // - storey: a door left off every storey is on its wall's (DB on Ground; nothing is on none);
    // - class: the name without Ifc and the label are the class too (Door takes the doors);
    // - numbers in a text are read by the one reader (Numbers.Parse): a lone mark is decimal, „1,200” is 1.2, as `=` on a
    //   field reads it, for every operator (audit 2026-11-22: `=` took 1.2 and `between` 1200);
    // - phase: an element left unphased is created in the default phase, by its id or name. A saved rule asking whether the
    //   phase has a value is turned into one on the stored attribute by ViewFilterUpgrade, so it keeps taking what it took.
    const string ViewFiltersToday = """
        type = Brick 300 => A
        type = WT-EXT => RW1
        type ≠ Brick 300 => B Block C DA DB RD RW1 RW2
        type has none => Block C RD RW2
        storey = Ground => A B Block C DA DB RD RW1 RW2
        storey has none => 
        material = Brick => A C
        material = Plaster => A C
        material = Concrete => 
        material has none => Block DA DB RD RW1 RW2
        class = IfcDoor => DA DB RD
        class = Door => DA DB RD
        class contains Wall => A B C RW1 RW2
        category = walls => A B C RW1 RW2
        name contains W => RW1 RW2
        Fire rating begins REI => A RW1
        Fire rating has value => A B RW1
        FireRating = REI 90 => RW1
        height > 3 => B
        height 3..3.5 => A B
        Code = 1200 => 
        Code = 1.2 => A
        Code > 1000 => 
        Code < 1000 => A C
        phase = existing => B
        phase = new-construction => A Block C DA DB RD RW1 RW2
        phase = New construction => A Block C DA DB RD RW1 RW2
        phase has none => 
        group has value => 
        link has none => A B Block C DA DB RD RW1 RW2
        predefined has value => A B C DA DB
        """;

    [Fact]
    public void View_filter_rules_take_today()
    {
        var md = Varied();
        var m = md.F.Model;
        var rows = ViewRules.Select(r => (r.Label, Names(md, Ours(md).Where(e => ViewFilters.Matches(m, e, ViewFilters.Rules(new JsonArray(r.Rule.DeepClone()))[0])).Select(e => e.Id))));
        Pinned(ViewFiltersToday, Table(rows), "view-filters");
    }

    // Changed on purpose: Quick select looks at the elements on a storey following the host (DB, a door off every storey,
    // now found); with the geometry at hand a material is an element's part's too (DA's plaster reveal).
    const string QuickSelectToday = """
        type = Brick 300 => A
        storey has none => 
        class = IfcDoor => DA DB RD
        material has none => Block RD RW1 RW2
        any: class = IfcDoor, material = Brick => A C DA DB RD
        """;

    [Fact]
    public void Quick_select_and_a_filter_s_count_take_today()
    {
        var md = Varied();
        string Select(string label, string match, params JsonObject[] rules)
        {
            var r = (JsonObject)md.F.Session.Query("select.query", new JsonObject { ["match"] = match, ["rules"] = new JsonArray([.. rules]) })!;
            return $"{label} => {Names(md, r["ids"]!.AsArray().Select(x => (string)x!))}";
        }
        var table = string.Join("\n",
            Select("type = Brick 300", "all", Rule("type", "equals", "Brick 300")),
            Select("storey has none", "all", Rule("storey", "hasNoValue")),
            Select("class = IfcDoor", "all", Rule("class", "equals", "IfcDoor")),
            Select("material has none", "all", Rule("material", "hasNoValue")),
            Select("any: class = IfcDoor, material = Brick", "any", Rule("class", "equals", "IfcDoor"), Rule("material", "equals", "Brick")));
        Pinned(QuickSelectToday, table, "quick-select");

        // A filter with no rules takes nothing; its count counts the elements on a storey it takes.
        var empty = (string)md.F.Run("viewFilter.create", new JsonObject { ["name"] = "Empty", ["rules"] = new JsonArray() })!["id"]!;
        Assert.Equal(0, (int)md.F.Session.Query("viewFilter.count", new JsonObject { ["id"] = empty })!);
        var doors = (string)md.F.Run("viewFilter.create", new JsonObject { ["name"] = "Doors", ["rules"] = new JsonArray(Rule("class", "equals", "IfcDoor")) })!["id"]!;
        Assert.Equal(DoorsCountToday, (int)md.F.Session.Query("viewFilter.count", new JsonObject { ["id"] = doors })!);
        Assert.False(ViewFilters.Matches(md.F.Model, md.F.Model.Get(md.Ids["A"])!, ViewFilters.All(md.F.Model).Single(x => x.Id == empty)));
    }

    // DB, off every storey, is on its wall's now (DA, DB, RD).
    const int DoorsCountToday = 3;

    // ---- A field schedule's filter (ElementFilter) ----

    static JsonObject Filter(string key, params string[] values) => new() { [key] = new JsonArray([.. values.Select(v => (JsonNode)v)]) };
    static JsonObject Conditions(params JsonObject[] c) => new() { ["conditions"] = new JsonArray([.. c]) };
    static JsonObject Cond(string field, string op, JsonNode? value = null) => new() { ["field"] = field, ["op"] = op, ["value"] = value };

    // Unchanged: the field schedules' filter is the engine the others now use.
    const string ElementFilterToday = """
        empty => A B Block C DA DB RD RW1 RW2
        types Brick 300 => A
        types WT-EXT => RW1
        types <id> => A
        storeys Ground => A B Block C DA DB RD RW1 RW2
        materials Brick => A C
        materials Plaster => A C DA
        materials Concrete => 
        classes Door => DA DB RD
        classes IfcWall => A B C RW1 RW2
        phases new-construction => A Block C DA DB RD RW1 RW2
        phases existing => B
        Fire rating starts with REI => A RW1
        height > 3 => B
        Code = 1200 => 
        Code = 1.2 => A
        type = Brick 300 => A C
        storey.name = Ground => A B Block C DA DB RD RW1 RW2
        """;

    [Fact]
    public void A_field_schedule_s_filter_takes_today()
    {
        var md = Varied();
        var q = md.F.Session.Reader;
        var brickType = md.F.Model.OfClass(IfcClass.WallType).First(t => t.Name == "Brick 300").Id;
        string Takes(string label, JsonObject filter) => $"{label} => {Names(md, ElementFilter.Elements(q, ElementFilter.Check(filter)).Select(e => e.Id))}";
        var table = string.Join("\n",
            Takes("empty", new JsonObject()),
            Takes("types Brick 300", Filter("types", "Brick 300")),
            Takes("types WT-EXT", Filter("types", "WT-EXT")),
            Takes("types <id>", Filter("types", brickType)),
            Takes("storeys Ground", Filter("storeys", "Ground")),
            Takes("materials Brick", Filter("materials", "Brick")),
            Takes("materials Plaster", Filter("materials", "Plaster")),
            Takes("materials Concrete", Filter("materials", "Concrete")),
            Takes("classes Door", Filter("classes", "Door")),
            Takes("classes IfcWall", Filter("classes", "IfcWall")),
            Takes("phases new-construction", Filter("phases", "new-construction")),
            Takes("phases existing", Filter("phases", "existing")),
            Takes("Fire rating starts with REI", Conditions(Cond("Fire rating", "starts with", "REI"))),
            Takes("height > 3", Conditions(Cond("height", ">", 3))),
            Takes("Code = 1200", Conditions(Cond("Code", "=", 1200))),
            Takes("Code = 1.2", Conditions(Cond("Code", "=", 1.2))),
            Takes("type = Brick 300", Conditions(Cond("type", "=", "Brick 300"))),
            Takes("storey.name = Ground", Conditions(Cond("storey.name", "=", "Ground"))));
        Pinned(ElementFilterToday, table, "element-filter");
    }

    // ---- A task's rule (TaskRule) ----

    // Changed on purpose: a task's rule names its types, storeys, materials and classes by id as before, and now by name too
    // (and a class without Ifc), as a schedule's filter does; a rule saved by the interface names ids, so it takes the same.
    const string TaskRuleToday = """
        empty => A B Block C DA DB RD RW1 RW2
        types <id> => A
        types Brick 300 => A
        storeys <id> => A B Block C DA DB RD RW1 RW2
        storeys Ground => A B Block C DA DB RD RW1 RW2
        materials <brick> => A C
        materials Brick => A C
        classes IfcDoor => DA DB RD
        classes Door => DA DB RD
        phases new-construction => A Block C DA DB RD RW1 RW2
        Fire rating starts with REI => A RW1
        """;

    [Fact]
    public void A_task_rule_takes_today()
    {
        var md = Varied();
        var f = md.F;
        var brickType = f.Model.OfClass(IfcClass.WallType).First(t => t.Name == "Brick 300").Id;
        var brick = f.Model.OfClass(IfcClass.Material).First(x => x.Name == "Brick" && x.Str("ifcSource") == null && Layers0(f, x.Id)).Id;
        string Takes(string label, JsonObject rule)
        {
            var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = label, ["duration"] = 1 })!["id"]!;
            f.Run("planning.updateTask", new JsonObject { ["id"] = id, ["rule"] = rule });
            var row = ((JsonObject)f.Session.Query("planning.schedule", new JsonObject())!)["tasks"]!.AsArray().OfType<JsonObject>().First(t => (string)t["id"]! == id);
            return $"{label} => {Names(md, (row["elements"] as JsonArray ?? []).Select(x => (string)x!))}";
        }
        var table = string.Join("\n",
            Takes("empty", new JsonObject()),
            Takes("types <id>", Filter("types", brickType)),
            Takes("types Brick 300", Filter("types", "Brick 300")),
            Takes("storeys <id>", Filter("storeys", f.Storey)),
            Takes("storeys Ground", Filter("storeys", "Ground")),
            Takes("materials <brick>", Filter("materials", brick)),
            Takes("materials Brick", Filter("materials", "Brick")),
            Takes("classes IfcDoor", Filter("classes", "IfcDoor")),
            Takes("classes Door", Filter("classes", "Door")),
            Takes("phases new-construction", Filter("phases", "new-construction")),
            Takes("Fire rating starts with REI", Conditions(Cond("Fire rating", "starts with", "REI"))));
        Pinned(TaskRuleToday, table, "task-rule");
    }

    /// <summary>Whether a material is the one the native wall types' layers use (not the file's of the same name).</summary>
    static bool Layers0(Fixture f, string material) =>
        f.Model.OfClass(IfcClass.WallType).Any(t => LayerSets.LayersOf(f.Model, t.Id).Any(l => l.Material == material));

    // ---- A clash search set (SearchSets) ----

    static JsonObject SetRuleJson(string field, string op, params string[] values) =>
        new() { ["field"] = field, ["op"] = op, ["values"] = new JsonArray([.. values.Select(v => (JsonNode)v)]) };
    static JsonObject ParamRule(string name, string op, params string[] values) { var r = SetRuleJson("parameter", op, values); r["parameter"] = name; return r; }
    static JsonObject Set(bool all, JsonObject[] rules, params JsonObject[] groups) =>
        new() { ["matchAll"] = all, ["rules"] = new JsonArray([.. rules]), ["groups"] = new JsonArray([.. groups]) };

    // Changed on purpose: a reference's file type counts; a door off every storey is on its wall's; an element's materials
    // are its layers' (C's own) and, with the geometry, its parts' (DA's plaster); a parameter is read as a tag writes it
    // (the user's parameters, the files' property sets) besides the attribute as stored, so Fire rating and Code are found.
    const string SearchSetToday = """
        empty => A B Block C DA DB RD RW1 RW2
        incomplete => A B Block C DA DB RD RW1 RW2
        type is Brick 300 => A
        type is WT-EXT => RW1
        storey is Ground => A B Block C DA DB RD RW1 RW2
        material is Brick => A C
        material contains Concrete => 
        material is Plaster => A C DA
        class is Door => DA DB RD
        class one of IfcWall => A B C RW1 RW2
        Fire rating contains REI => A RW1
        height at least 3 => A B
        Code at least 1000 => 
        Code at most 1000 => A C
        Code between 1000 1300 => 
        link is own => A B Block C DA DB RD RW1 RW2
        not type Brick 300 => B Block C DA DB RD RW1 RW2
        any: door, all(brick, ground) => A C DA DB RD
        """;

    [Fact]
    public void A_clash_search_set_takes_today()
    {
        var md = Varied();
        string Takes(string label, JsonObject set)
        {
            var r = (JsonObject)md.F.Session.Query("clash.members", new JsonObject { ["set"] = set })!;
            return $"{label} => {Names(md, r["ids"]!.AsArray().Select(x => (string)x!))}";
        }
        var table = string.Join("\n",
            Takes("empty", Set(false, [])),
            Takes("incomplete", Set(true, [SetRuleJson("type", "is")])),
            Takes("type is Brick 300", Set(true, [SetRuleJson("type", "is", "brick 300")])),
            Takes("type is WT-EXT", Set(true, [SetRuleJson("type", "is", "WT-EXT")])),
            Takes("storey is Ground", Set(true, [SetRuleJson("storey", "is", "Ground")])),
            Takes("material is Brick", Set(true, [SetRuleJson("material", "is", "Brick")])),
            Takes("material contains Concrete", Set(true, [SetRuleJson("material", "contains", "Concrete")])),
            Takes("material is Plaster", Set(true, [SetRuleJson("material", "is", "Plaster")])),
            Takes("class is Door", Set(true, [SetRuleJson("class", "is", "Door")])),
            Takes("class one of IfcWall", Set(true, [SetRuleJson("class", "isOneOf", "IfcWall", "IfcSlab")])),
            Takes("Fire rating contains REI", Set(true, [ParamRule("Fire rating", "contains", "REI")])),
            Takes("height at least 3", Set(true, [ParamRule("height", "atLeast", "3")])),
            Takes("Code at least 1000", Set(true, [ParamRule("Code", "atLeast", "1000")])),
            Takes("Code at most 1000", Set(true, [ParamRule("Code", "atMost", "1000")])),
            Takes("Code between 1000 1300", Set(true, [ParamRule("Code", "between", "1300", "1000")])),
            Takes("link is own", Set(true, [SetRuleJson("link", "is", "")])),
            Takes("not type Brick 300", Set(false, [SetRuleJson("type", "isNot", "Brick 300")])),
            Takes("any: door, all(brick, ground)", Set(false, [SetRuleJson("class", "is", "IfcDoor")], Set(true, [SetRuleJson("material", "is", "Brick"), SetRuleJson("storey", "is", "Ground")]))));
        Pinned(SearchSetToday, table, "search-set");
    }

    // ---- An article rule (ArticleRule) ----

    [Fact]
    public void An_article_rule_takes_today()
    {
        var md = Varied();
        var m = md.F.Model;
        var plaster = m.OfClass(IfcClass.Material).First(x => x.Name == "Plaster").Id;
        var brick = m.OfClass(IfcClass.Material).First(x => x.Name == "Brick" && Layers0(md.F, x.Id)).Id;
        var byMaterial = new ArticleRule("r1", brick, null, new ArticleRef("A1"));
        var finishes = new ArticleRule("r2", null, "Finish", new ArticleRef("A2"));
        Assert.True(byMaterial.Matches(m, brick));
        Assert.False(byMaterial.Matches(m, plaster));
        Assert.False(byMaterial.Matches(m, null));
        Assert.True(finishes.Matches(m, plaster));
        Assert.False(finishes.Matches(m, brick));
    }

    // ---- The one engine itself ----

    [Fact]
    public void A_saved_phase_rule_keeps_what_it_took_after_the_upgrade()
    {
        var md = Varied();
        var m = md.F.Model;
        var project = Spatial.Project(m)!;
        var attrs = project.CloneAttrs();
        attrs.Remove(ViewFilterUpgrade.RevKey);
        attrs[ViewFilters.Key] = new JsonArray(new JsonObject { ["id"] = "f1", ["name"] = "Unphased", ["match"] = "all", ["rules"] = new JsonArray(Rule("phase", "hasNoValue")) });
        var (entities, _) = new ViewFilterUpgrade().Upgrade([.. m.Entities.Select(e => e.Id == project.Id ? project.With(attrs: attrs) : e)], []);
        var upgraded = entities.Single(e => e.Class == IfcClass.Project);
        Assert.Equal(ViewFilterUpgrade.Rev, (int)upgraded.Attrs[ViewFilterUpgrade.RevKey]!);
        var rule = upgraded.Attrs[ViewFilters.Key]![0]!["rules"]![0]!;
        Assert.Equal("param", (string)rule["field"]!);
        Assert.Equal("phaseCreated", (string)rule["name"]!);
        // Read as the rule now says: the elements whose phase was never set, B (existing) left out, as before.
        var r = ViewFilters.Rules(new JsonArray(rule.DeepClone()))[0];
        Assert.Equal("A Block C DA DB RD RW1 RW2", Names(md, Ours(md).Where(e => ViewFilters.Matches(m, e, r)).Select(e => e.Id)));
        // Upgraded once.
        var (again, _) = new ViewFilterUpgrade().Upgrade(entities, []);
        Assert.Same(upgraded, again.Single(e => e.Class == IfcClass.Project));
    }

    [Fact]
    public void Every_filter_reads_facets_fields_lists_ranges_and_groups_the_same_way()
    {
        var md = Varied();
        var q = md.F.Session.Reader;
        string Takes(JsonObject filter) => Names(md, ElementFilter.Elements(q, ElementFilter.Check(filter)).Select(e => e.Id));
        // Facets as conditions: a reference's file type, a door's storey through its wall, a list, a negative on several materials.
        Assert.Equal("RW1", Takes(Conditions(Cond("@type", "=", "wt-ext"))));
        Assert.Equal("DA DB RD", Takes(new JsonObject { ["classes"] = new JsonArray("IfcDoor"), ["conditions"] = new JsonArray(Cond("@storey", "=", "Ground")) }));
        Assert.Equal("B RW1 RW2", Takes(new JsonObject { ["classes"] = new JsonArray("Wall"), ["conditions"] = new JsonArray(Cond("@material", "≠", "Brick")) }));
        Assert.Equal("C", Takes(Conditions(new JsonObject { ["field"] = "@text:Code", ["op"] = "between", ["values"] = new JsonArray("800", "1300") })));
        // „1,200” is 1.2 for a range as for `=`.
        Assert.Equal("A", Takes(Conditions(new JsonObject { ["field"] = "@text:Code", ["op"] = "between", ["values"] = new JsonArray("1", "2") })));
        // Any of a group: walls of Partition 100, or anything with a fire rating starting REI.
        Assert.Equal("A B RW1", Takes(new JsonObject
        {
            ["classes"] = new JsonArray("IfcWall"),
            ["match"] = "any",
            ["conditions"] = new JsonArray(Cond("@type", "=", "Partition 100")),
            ["nested"] = new JsonArray(new JsonObject { ["match"] = "all", ["conditions"] = new JsonArray(Cond("Fire rating", "starts with", "REI")) }),
        }));
        Assert.Throws<CommandException>(() => ElementFilter.Check(Conditions(Cond("@colour", "=", "red"))));
        // A view filter and Quick select by any field, the schedules' way (numbers in SI): the walls higher than 3.2 m.
        var high = (string)md.F.Run("viewFilter.create", new JsonObject { ["name"] = "High", ["rules"] = new JsonArray(Rule("field", "greater", "3.2", "height")) })!["id"]!;
        Assert.Equal(1, (int)md.F.Session.Query("viewFilter.count", new JsonObject { ["id"] = high })!);
        var oneOf = Rule("field", "oneOf", null, "Fire rating");
        oneOf["values"] = new JsonArray("EI 60", "REI 90");
        var ids = ((JsonObject)md.F.Session.Query("select.query", new JsonObject { ["rules"] = new JsonArray(oneOf) })!)["ids"]!.AsArray().Select(x => (string)x!);
        Assert.Equal("B RW1", Names(md, ids));
        // The facets' values for the pickers.
        var values = (JsonObject)md.F.Session.Query("fields.values", new JsonObject { ["field"] = "@type" })!;
        Assert.Contains(values["values"]!.AsArray(), v => (string)v!["value"]! == "WT-EXT");
        // One storey rule (D4): an opening on the storey of what it voids, a door on its wall's.
        var opening = q.Model.RelatingOf(md.Ids["DB"], IfcClass.RelFillsElement)!;
        Assert.Equal(md.F.Storey, Spatial.StoreyOf(q.Model, opening, followHost: true)!.Id);
        Assert.Equal(md.F.Storey, Spatial.StoreyOf(q.Model, q.Model.Get(md.Ids["DB"])!, followHost: true)!.Id);
        Assert.Null(Spatial.StoreyOf(q.Model, md.Ids["DB"]));
    }
}
