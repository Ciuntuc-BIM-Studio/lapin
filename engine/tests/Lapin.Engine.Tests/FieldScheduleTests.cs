using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// Schedules of the user's own from the models' fields (docs/plan-user-extensibility.md, „Extrase și câmpuri configurabile”):
/// a field schedule lists the elements a filter takes (its dimensions, and conditions on any field), a row each or grouped by
/// any fields, with columns of any field or formula added up as asked and a totals line, on native elements and on the
/// references of an IFC file alike (its property sets, its quantity sets); the office's aliases, read in order, seen by tags,
/// formulas, schedules and the takeoff; templates carried to another project; the fields query for the pickers.
/// </summary>
public class FieldScheduleTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-field-schedule").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture New() => new(new ArchitectureModule());

    static string Gid(string key) => Guid("field-schedule-test", key);

    /// <summary>
    /// A file from another program, in metres: three walls of the office's own sets — W1 (5 m, work L1, stage E1), W2 (3 m, L1,
    /// E2) with their base quantities, W3 (L2, its stage in ArchiCAD's set, its length only in a set of its own, 4 m, no base
    /// quantities) — and a block with no property at all.
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
        string Real(string name, double value) => f.Add("IFCPROPERTYSINGLEVALUE", S(name), Null, Typed("IFCREAL", R(value)), Null);
        void Set(string key, string wall, string name, params string[] props) =>
            f.Add("IFCRELDEFINESBYPROPERTIES", G(key + "-rel"), Null, Null, Null, L([wall]), f.Add("IFCPROPERTYSET", G(key), Null, S(name), Null, L(props)));
        string Wall(string key, string name, double y, double length)
        {
            var w = f.Add("IFCWALL", G(key), Null, S(name), Null, Null, f.Add("IFCLOCALPLACEMENT", storeyPlace, world), Box(length / 2, y, length, 0.25, 3), Null, E("STANDARD"));
            return w;
        }
        void Qto(string key, string wall, double length) =>
            f.Add("IFCRELDEFINESBYPROPERTIES", G(key + "-rel"), Null, Null, Null, L([wall]), f.Add("IFCELEMENTQUANTITY", G(key), Null, S("Qto_WallBaseQuantities"), Null, Null, L([
                f.Add("IFCQUANTITYLENGTH", S("Length"), Null, Null, R(length), Null),
                f.Add("IFCQUANTITYAREA", S("NetSideArea"), Null, Null, R(length * 3), Null),
            ])));
        var w1 = Wall("w1", "W1", 0, 5);
        Qto("q1", w1, 5);
        Set("p1", w1, "Pset_Firma", Label("CodLucrare", "L1"), Label("Etapa", "E1"));
        var w2 = Wall("w2", "W2", 2, 3);
        Qto("q2", w2, 3);
        Set("p2", w2, "Pset_Firma", Label("CodLucrare", "L1"), Label("Etapa", "E2"));
        var w3 = Wall("w3", "W3", 4, 4);
        Set("p3", w3, "Pset_Firma", Label("CodLucrare", "L2"));
        Set("p3a", w3, "ArchiCADProperties", Label("Faza", "E2"));
        Set("p3x", w3, "Pset_X", Real("L_m", 4));
        var block = f.Add("IFCBUILDINGELEMENTPROXY", G("block"), Null, S("Block"), Null, Null, f.Add("IFCLOCALPLACEMENT", storeyPlace, world), Box(10, 1, 1, 2, 0.5), Null, E("ELEMENT"));
        f.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", G("contained"), Null, Null, Null, L([w1, w2, w3, block]), storey);
        var path = Path.Combine(_dir, "foreign.ifc");
        f.Write(path, "foreign.ifc", "ViewDefinition [ReferenceView_V1.2]");
        return path;
    }

    Fixture Referenced()
    {
        var f = New();
        IfcImport.Run(f.Session, Foreign(), new IfcImportOptions("reference"), out _);
        Assert.True(ReferenceLayers.IsReference(f.Model.Get(Gid("w1"))!));
        return f;
    }

    static string Create(Fixture f, JsonObject fields, string name = "Walls") =>
        (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "fields", ["name"] = name, ["fields"] = fields })!["id"]!;

    static ScheduleGrid Grid(Fixture f, string id) => new FieldSchedule().Grid(f.Session.Reader, f.Model.Get(id)!);

    static Dictionary<string, string> Row(ScheduleGrid g, string first) =>
        g.Columns.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => g.Rows.Single(r => r[0] == first)[x.i]);

    static JsonArray A(params JsonNode[] items) => new(items);

    [Fact]
    public void A_field_schedule_lists_references_by_their_files_property_sets_and_adds_up_their_quantities()
    {
        var f = Referenced();
        // The walls of work L1: a row each, its name, its stage, its length, a formula on its area, the totals.
        var id = Create(f, new JsonObject
        {
            ["filter"] = new JsonObject
            {
                ["classes"] = A("IfcWall"),
                ["conditions"] = A(new JsonObject { ["field"] = "Pset_Firma.CodLucrare", ["op"] = "=", ["value"] = "l1" }),
            },
            ["columns"] = A(
                new JsonObject { ["field"] = "name" },
                new JsonObject { ["field"] = "Pset_Firma.Etapa", ["heading"] = "Stage" },
                new JsonObject { ["field"] = "Qto_WallBaseQuantities.Length", ["heading"] = "Length", ["unit"] = "m", ["decimals"] = 2 },
                new JsonObject { ["formula"] = "NetSideArea * 1.1", ["heading"] = "Area +10%", ["decimals"] = 1 }),
        });
        var g = Grid(f, id);
        Assert.Equal(["Name", "Stage", "Length (m)", "Area +10%"], g.Columns);
        Assert.Equal(3, g.Rows.Count);
        Assert.Equal(["W1", "E1", "5.00", "16.5"], g.Rows[0]);
        Assert.Equal(["W2", "E2", "3.00", "9.9"], g.Rows[1]);
        Assert.Equal(["Total", "", "8.00", "26.4"], g.Rows[2]);
        Assert.Equal([Gid("w1"), Gid("w2"), null], g.Keys);

        // Grouped by its work code, its walls counted, their lengths summed and their stages listed.
        f.Run("architecture.updateSchedule", new JsonObject
        {
            ["id"] = id,
            ["fields"] = new JsonObject
            {
                ["filter"] = new JsonObject { ["classes"] = A("IfcWall") },
                ["groupBy"] = A("Pset_Firma.CodLucrare"),
                ["columns"] = A(
                    new JsonObject { ["field"] = "Length", ["aggregate"] = "sum", ["decimals"] = 1 },
                    new JsonObject { ["field"] = "Pset_Firma.Etapa", ["heading"] = "Stages" },
                    new JsonObject { ["field"] = "Pset_Firma.Etapa", ["aggregate"] = "countDistinct", ["heading"] = "Stage count" },
                    new JsonObject { ["field"] = "Length", ["aggregate"] = "max", ["heading"] = "Longest" }),
            },
        });
        g = Grid(f, id);
        Assert.Equal(["CodLucrare", "Count", "Length", "Stages", "Stage count", "Longest"], g.Columns);
        Assert.Equal(["L1", "2", "8.0", "E1, E2", "2", "5"], g.Rows[0]);
        // W3 has no base quantities: no length (an alias gives it one, below).
        Assert.Equal(["L2", "1", "", "", "0", ""], g.Rows[1]);
        Assert.Equal(["Total", "3", "8.0", "", "2", "5"], g.Rows[2]);

        // A schedule like any: its CSV, its layout's grid, a chart of its column.
        var csv = (string)f.Session.Query("schedule.csv", new JsonObject { ["id"] = id })!;
        Assert.StartsWith("CodLucrare,Count,Length,Stages,Stage count,Longest\nL1,2,8.0,\"E1, E2\",2,5\n", csv);
        var layout = (JsonObject)f.Session.Query("schedule.layout", new JsonObject { ["id"] = id })!;
        Assert.Equal(3, layout["grid"]!["rows"]!.AsArray().Count);
        f.Run("architecture.scheduleChart", new JsonObject { ["id"] = id, ["chart"] = new JsonObject { ["category"] = "CodLucrare", ["value"] = "Length" } });
        Assert.NotEmpty(Convert.FromBase64String((string)f.Session.Query("schedule.xlsx", new JsonObject { ["id"] = id })!));
    }

    [Fact]
    public void Conditions_compare_numbers_as_numbers_and_texts_case_aside()
    {
        var f = Referenced();
        int Count(string field, string op, JsonNode? value = null)
        {
            var filter = new JsonObject { ["conditions"] = A(new JsonObject { ["field"] = field, ["op"] = op, ["value"] = value }) };
            return ElementFilter.Elements(f.Session.Reader, ElementFilter.Check(filter)).Count;
        }
        Assert.Equal(4, Count("name", "exists"));
        Assert.Equal(2, Count("Length", ">", 2.5));
        Assert.Equal(1, Count("Length", ">=", "5"));
        Assert.Equal(1, Count("Length", "<", 4));
        Assert.Equal(2, Count("Qto_WallBaseQuantities.Length", "≥", 3));
        Assert.Equal(2, Count("Pset_Firma.Etapa", "missing"));
        Assert.Equal(3, Count("Pset_Firma.CodLucrare", "starts with", "l"));
        Assert.Equal(1, Count("CodLucrare", "contains", "2"));
        Assert.Equal(3, Count("Pset_Firma.Etapa", "≠", "E1"));
        Assert.Equal(1, Count("ArchiCADProperties.Faza", "=", "e2"));
        Assert.Throws<CommandException>(() => ElementFilter.Check(new JsonObject { ["conditions"] = A(new JsonObject { ["field"] = "name", ["op"] = "like", ["value"] = "W" }) }));
    }

    [Fact]
    public void Aliases_read_the_first_source_an_element_has_and_tags_formulas_schedules_and_the_takeoff_see_them()
    {
        var f = Referenced();
        f.Run("fields.setAlias", new JsonObject { ["name"] = "Phase", ["sources"] = A("Pset_Firma.Etapa", "ArchiCADProperties.Faza") });
        f.Run("fields.setAlias", new JsonObject { ["name"] = "Length", ["sources"] = A("Qto_WallBaseQuantities.Length", "Pset_X.L_m") });
        var (w1, w3) = (f.Model.Get(Gid("w1"))!, f.Model.Get(Gid("w3"))!);
        var q = f.Session.Reader;

        // Tags and live text.
        Assert.Equal("E1", TagText.Value(f.Model, w1, "Phase"));
        Assert.Equal("E2", TagText.Value(f.Model, w3, "phase"));
        Assert.Equal("W3 E2", TagText.Fill("{name} {Phase}", f.Model, w3));
        Assert.Equal("", TagText.Value(f.Model, f.Model.Get(Gid("block"))!, "Phase"));
        // Formulas: the first source the element has, its own quantity (the source named like the alias) for W1.
        Assert.Equal(5, ParamFormulas.Read(f.Model, w1, null, ParamFormulas.Quantities(q, w1.Id), "Length")!.Value.Number);
        var tried = (JsonObject)f.Session.Query("params.tryFormula", new JsonObject { ["id"] = w3.Id, ["formula"] = "Length * 2" })!;
        Assert.Equal(8.0, (double)tried["value"]!);
        // The takeoff: W3's length from its own set, as the alias named like a standard quantity fills in what its file lacks.
        Assert.Equal(4.0, f.Session.Quantities(w3.Id)[0].Quantities.Single(x => x.Name == "Length").Value, 9);
        Assert.Equal(5.0, f.Session.Quantities(w1.Id)[0].Quantities.Single(x => x.Name == "Length").Value, 9);
        var takeoff = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        var tg = new QuantityTakeoff().Grid(q, f.Model.Get(takeoff)!);
        Assert.Equal("12.00", tg.Rows.Single(r => r[0] == "Wall")[tg.Columns.ToList().IndexOf("Length (m)")]);
        // The fields an element has: the aliases among them.
        var available = FieldCatalog.Available(q, w3);
        var aliases = available["groups"]!.AsArray().Single(g => (string?)g!["id"] == "aliases")!["fields"]!.AsArray();
        Assert.Contains(aliases, x => (string?)x!["token"] == "Phase" && (string?)x["value"] == "E2");

        // A schedule grouped by an alias.
        var id = Create(f, new JsonObject
        {
            ["filter"] = new JsonObject { ["classes"] = A("IfcWall") },
            ["groupBy"] = A("Phase"),
            ["columns"] = A(new JsonObject { ["field"] = "Length", ["aggregate"] = "sum", ["decimals"] = 2 }),
        });
        var g = Grid(f, id);
        Assert.Equal(["Phase", "Count", "Length"], g.Columns);
        Assert.Equal(["E1", "1", "5.00"], g.Rows[0]);
        Assert.Equal(["E2", "2", "7.00"], g.Rows[1]);
        Assert.Equal(["Total", "3", "12.00"], g.Rows[2]);

        // One undo step each: the alias undone, the field reads as before.
        f.Run("fields.setAlias", new JsonObject { ["name"] = "Phase", ["sources"] = A("ArchiCADProperties.Faza") });
        Assert.Equal("", TagText.Value(f.Model, w1, "Phase"));
        f.Session.Undo();
        Assert.Equal("E1", TagText.Value(f.Model, w1, "Phase"));

        // Refused: a builtin's name, a dotted name, an alias without sources.
        Assert.Throws<CommandException>(() => f.Run("fields.setAlias", new JsonObject { ["name"] = "name", ["sources"] = A("Pset_Firma.Etapa") }));
        Assert.Throws<CommandException>(() => f.Run("fields.setAlias", new JsonObject { ["name"] = "Pset.X", ["sources"] = A("Pset_Firma.Etapa") }));
        Assert.Throws<CommandException>(() => f.Run("fields.setAlias", new JsonObject { ["name"] = "Empty", ["sources"] = A() }));

        // Carried to another project as a .lapinaliases file.
        var file = (JsonObject)f.Session.Query("fields.exportAliases", new JsonObject())!;
        Assert.Equal("aliases.lapinaliases", (string?)file["file"]);
        var other = Referenced();
        var res = other.Run("fields.importAliases", new JsonObject { ["text"] = (string)file["text"]! })!;
        Assert.Equal(2, (int)res["added"]!);
        Assert.Equal(["Phase", "Length"], FieldAliases.All(other.Model).Select(a => a.Name));
        Assert.Equal("E2", TagText.Value(other.Model, other.Model.Get(Gid("w3"))!, "Phase"));
        res = other.Run("fields.importAliases", new JsonObject { ["aliases"] = file["aliases"]!.DeepClone() })!;
        Assert.Equal(0, (int)res["added"]! + (int)res["updated"]!);
    }

    [Fact]
    public void A_field_schedule_on_native_elements_reads_their_parameters_attributes_and_quantities()
    {
        var f = New();
        var t = f.Type("Brick 30", ("Brick", 0.3, true));
        var a = f.Wall(0, 0, 6, 0, t);
        var b = f.Wall(6, 0, 6, 4, t);
        var c = f.Wall(0, 4, 3, 4, t);
        void Param(string id, string name, string value, string? pset = null) =>
            f.Run("element.setParam", new JsonObject { ["ids"] = A(id), ["name"] = name, ["value"] = value, ["kind"] = "text", ["pset"] = pset });
        Param(a, "Fire rating", "REI 90");
        Param(b, "Fire rating", "REI 90");
        Param(c, "Fire rating", "REI 30");
        Param(a, "CodLucrare", "L1", "Firma");
        var id = Create(f, new JsonObject
        {
            ["filter"] = new JsonObject
            {
                ["classes"] = A("Wall"), ["storeys"] = A(f.Model.Get(f.Storey)!.Name), ["types"] = A("Brick 30"), ["materials"] = A("brick"), ["phases"] = A("new-construction"),
                ["conditions"] = A(new JsonObject { ["field"] = "Fire rating", ["op"] = "=", ["value"] = "rei 90" }),
            },
            ["columns"] = A(
                new JsonObject { ["field"] = "name" },
                new JsonObject { ["field"] = "Firma.CodLucrare", ["heading"] = "Code" },
                new JsonObject { ["field"] = "Length", ["decimals"] = 2 },
                new JsonObject { ["field"] = "Length", ["unit"] = "mm", ["heading"] = "Length", ["decimals"] = 0 },
                new JsonObject { ["formula"] = "{= Length * 2}", ["heading"] = "Twice", ["decimals"] = 1 },
                new JsonObject { ["formula"] = "{storey.name}/{mark}", ["heading"] = "Where" }),
        });
        var g = Grid(f, id);
        Assert.Equal(["Name", "Code", "Length", "Length (mm)", "Twice", "Where"], g.Columns);
        Assert.Equal(3, g.Rows.Count);
        var lengths = f.Model.OfClass(IfcClass.Wall).Where(w => w.Id != c).Select(w => f.Session.Quantities(w.Id)[0].Quantities.First(x => x.Name == "Length").Value).ToList();
        Assert.Equal(lengths.Sum().ToString("F2", System.Globalization.CultureInfo.InvariantCulture), g.Rows[2][2]);
        Assert.Equal((lengths.Sum() * 1000).ToString("F0", System.Globalization.CultureInfo.InvariantCulture), g.Rows[2][3]);
        Assert.Contains(g.Rows, r => r[1] == "L1");
        Assert.All(g.Rows.Take(2), r => Assert.StartsWith(f.Model.Get(f.Storey)!.Name + "/", r[5]));
        // Another material, another phase: none of them.
        Assert.Empty(ElementFilter.Elements(f.Session.Reader, ElementFilter.Check(new JsonObject { ["materials"] = A("Concrete") })));
        Assert.Empty(ElementFilter.Elements(f.Session.Reader, ElementFilter.Check(new JsonObject { ["classes"] = A("IfcWall"), ["phases"] = A("existing") })));
        // A condition nothing holds: no rows, no totals.
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["fields"] = new JsonObject { ["filter"] = new JsonObject { ["types"] = A("No such type") } } });
        Assert.Empty(Grid(f, id).Rows);
        // The joinery's schedules take no fields; a bad aggregate is refused.
        var joinery = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "door" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("architecture.updateSchedule", new JsonObject { ["id"] = joinery, ["fields"] = new JsonObject() }));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["fields"] = new JsonObject { ["columns"] = A(new JsonObject { ["field"] = "Length", ["aggregate"] = "median" }) } }));
    }

    [Fact]
    public void A_field_schedule_is_drawn_and_exported_under_its_own_name()
    {
        var f = New();
        var t = f.Type("Brick 30", ("Brick", 0.3, true));
        f.Wall(0, 0, 6, 0, t);
        f.Wall(6, 0, 6, 4, t);
        var id = Create(f, new JsonObject { ["filter"] = new JsonObject { ["classes"] = A("Wall") } }, "Walls by stage");
        string[] Texts(string sid) => [.. new ScheduleEntity().Pages(f.Session.Reader, f.Model.Get(sid)!, 380, null)[0].Pens.SelectMany(p => p.Texts).Select(x => x.Text)];
        Assert.Contains("Walls by stage", Texts(id));
        Assert.DoesNotContain(FieldSchedule.Title, Texts(id));
        // The workbook's sheet is named the same.
        string Workbook(string sid)
        {
            using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(Convert.FromBase64String((string)f.Session.Query("schedule.xlsx", new JsonObject { ["id"] = sid })!)));
            using var r = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open());
            return r.ReadToEnd();
        }
        Assert.Contains("name=\"Walls by stage\"", Workbook(id));
        // Unnamed: the kind's title; another module's kind keeps its own label.
        var entity = f.Model.Get(id)!;
        Assert.Equal(FieldSchedule.Title, ScheduleEntity.TitleOf(new FieldSchedule(), new Entity(entity.Id, entity.Class, " ", entity.Attrs)));
        var takeoff = new QuantityTakeoff();
        Assert.Equal(takeoff.Label, ScheduleEntity.TitleOf(takeoff, entity));
    }

    [Fact]
    public void A_template_goes_to_another_project_by_names_and_a_schedule_is_saved_in_the_package()
    {
        var f = Referenced();
        var id = Create(f, new JsonObject
        {
            ["filter"] = new JsonObject { ["classes"] = A("IfcWall"), ["storeys"] = A(Spatial.Storeys(f.Model).First().Id) },
            ["groupBy"] = A("Pset_Firma.CodLucrare"),
            ["columns"] = A(new JsonObject { ["field"] = "Length", ["aggregate"] = "sum", ["decimals"] = 2 }),
        }, "By work");
        f.Run("architecture.addScheduleColumn", new JsonObject { ["id"] = id, ["label"] = "Notes" });
        var exported = (JsonObject)f.Session.Query("schedule.exportTemplate", new JsonObject { ["id"] = id })!;
        Assert.Equal("By work.lapinschedule", (string?)exported["file"]);
        // The storey by its name: another project has its own id for it.
        Assert.Equal("Ground", (string?)exported["template"]!["fields"]!["filter"]!["storeys"]![0]);

        var other = Referenced();
        var made = other.Run("architecture.importSchedule", new JsonObject { ["text"] = (string)exported["text"]! })!;
        Assert.Equal("By work", (string?)made["name"]);
        var mine = ScheduleEntity.SourceGrid(f.Session.Reader, f.Model.Get(id)!, new FieldSchedule()).ToGrid();
        var theirs = ScheduleEntity.SourceGrid(other.Session.Reader, other.Model.Get((string)made["id"]!)!, new FieldSchedule()).ToGrid();
        Assert.Equal(mine.Columns, theirs.Columns);
        Assert.Equal(mine.Rows, theirs.Rows);
        Assert.Contains("Notes", theirs.Columns);
        // From the library's object too, one undo step.
        var again = other.Run("architecture.importSchedule", new JsonObject { ["template"] = exported["template"]!.DeepClone(), ["name"] = "Copy" })!;
        other.Session.Undo();
        Assert.Null(other.Model.Get((string)again["id"]!));
        Assert.Throws<CommandException>(() => other.Run("architecture.importSchedule", new JsonObject { ["text"] = "{\"format\":\"lapin-parameters\"}" }));

        // Saved and opened again: the same table.
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var reopened = New();
        reopened.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(mine.Rows, ScheduleEntity.SourceGrid(reopened.Session.Reader, reopened.Model.Get(id)!, new FieldSchedule()).ToGrid().Rows);
    }

    [Fact]
    public void The_fields_query_lists_what_the_models_elements_have_by_source_and_one_fields_values()
    {
        var f = Referenced();
        f.Run("fields.setAlias", new JsonObject { ["name"] = "Phase", ["sources"] = A("Pset_Firma.Etapa", "ArchiCADProperties.Faza") });
        var all = (JsonObject)f.Session.Query("fields.model", new JsonObject())!;
        Assert.Equal(4, (int)all["elements"]!);
        var groups = all["groups"]!.AsArray().ToDictionary(g => (string)g!["id"]!, g => g!["fields"]!.AsArray().ToDictionary(x => (string)x!["token"]!, x => (JsonObject)x!));
        Assert.Equal(4, (int)groups["lapin"]["name"]["count"]!);
        var code = groups["pset:Pset_Firma"]["Pset_Firma.CodLucrare"];
        Assert.Equal(3, (int)code["count"]!);
        Assert.Equal("text", (string?)code["kind"]);
        Assert.Equal(["L1", "L2"], code["examples"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("CodLucrare", (string?)code["short"]);
        Assert.Equal("number", (string?)groups["pset:Pset_X"]["Pset_X.L_m"]["kind"]);
        Assert.Equal(2, (int)groups["quantities"]["Length"]["count"]!);
        Assert.Equal(3, (int)groups["aliases"]["Phase"]["count"]!);
        Assert.Contains("storey.name", groups["scopes"].Keys);

        // A filter's elements only.
        var walls = (JsonObject)f.Session.Query("fields.model", new JsonObject { ["filter"] = new JsonObject { ["classes"] = A("IfcBuildingElementProxy") } })!;
        Assert.Equal(1, (int)walls["elements"]!);
        Assert.DoesNotContain(walls["groups"]!.AsArray(), g => (string?)g!["id"] == "pset:Pset_Firma");

        // One field's values, for a filter's value picker.
        var values = (JsonObject)f.Session.Query("fields.values", new JsonObject { ["field"] = "Pset_Firma.CodLucrare" })!;
        Assert.Equal(3, (int)values["count"]!);
        Assert.Equal(1, (int)values["missing"]!);
        Assert.Equal(["L1:2", "L2:1"], values["values"]!.AsArray().Select(v => $"{(string)v!["value"]!}:{(int)v["count"]!}"));
        var lengths = (JsonObject)f.Session.Query("fields.values", new JsonObject { ["field"] = "Length", ["filter"] = new JsonObject { ["classes"] = A("IfcWall") } })!;
        Assert.Equal("number", (string?)lengths["kind"]);
        Assert.Equal(["3", "5"], lengths["values"]!.AsArray().Select(v => (string)v!["value"]!));
    }

    [Fact]
    public void The_takeoff_groups_by_any_field_besides_its_four_dimensions()
    {
        var f = Referenced();
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = A("kind", "Pset_Firma.CodLucrare") });
        var g = new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
        Assert.Equal(["Kind", "CodLucrare", "Count"], g.Columns.Take(3));
        var walls = g.Rows.Where(r => r[0] == "Wall").ToList();
        Assert.Equal(["L1", "L2"], walls.Select(r => r[1]));
        Assert.Equal("2", walls[0][2]);
        Assert.Equal("—", g.Rows.Single(r => r[0] != "Wall" && r[0] != "Total")[1]);
    }
}
