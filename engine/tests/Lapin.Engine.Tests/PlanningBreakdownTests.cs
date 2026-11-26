using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Product × Location in the open planning module (docs/plan-4d5d.md, "Produs × Loc"; the consultant's ABS / PBS / GBS): the
/// PBS is a classification of the project, its codes labelling types and elements (a type's is its elements', an element's
/// own wins); a task's rule names products (a node and the items under it) and locations (the building, storeys, rooms), so
/// "Install doors" × "Floor 1" takes the doors of floor 1; the unlinked elements and the CDE check by product; the labels and
/// rules through IFC; MS Project's Text2. Rules without products or locations take what they took. (The PBS tools, the
/// starter, the pickers and tasks from the cross-product, are the planning-plus extension's, with their tests.)
/// </summary>
public sealed class PlanningBreakdownTests
{
    static Fixture F() => new(new ArchitectureModule(), new PhasesModule(), new PlanningModule());

    sealed record House(Fixture F, string Floor1, string Floor2, string[] Doors1, string Door2, string OwnLabelled, string[] Walls1, string Wall2);

    /// <summary>
    /// Two storeys: on the first, two walls with a door each and a third door labelled a window on its own; on the second, a
    /// wall with a door. The doors' type is labelled 40.20 Doors, the walls' 40.10 Partition walls (the starter PBS).
    /// </summary>
    static House Build()
    {
        var f = F();
        var floor1 = f.Storey;
        var floor2 = (string)f.Run("storey.create", new JsonObject { ["name"] = "Floor 2" })!["id"]!;
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(0, 5, 6, 5);
        var c = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = floor2, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        string Door(string wall, double along) => (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;
        var d1 = Door(a, 1.5);
        var d2 = Door(b, 1.5);
        var own = Door(a, 4.5);
        var d3 = Door(c, 1.5);
        MakePbs(f);
        var doorType = Types.Of(f.Model, f.Model.Get(d1)!)!.Id;
        var wallType = Types.Of(f.Model, f.Model.Get(a)!)!.Id;
        f.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(doorType), ["system"] = "PBS", ["code"] = "40.20" });
        f.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(wallType), ["system"] = "PBS", ["code"] = "40.10" });
        // An element's own label wins over its type's.
        f.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(own), ["system"] = "PBS", ["code"] = "30.20" });
        return new(f, floor1, floor2, [d1, d2], d3, own, [a, b], c);
    }

    /// <summary>A classification „PBS” with a few products by trade, as a user would make or import one.</summary>
    static void MakePbs(Fixture f)
    {
        (string Code, string Title, string? Parent)[] items =
        [
            ("30", "Envelope", null), ("30.10", "External walls", "30"), ("30.20", "Windows", "30"),
            ("40", "Interior", null), ("40.10", "Partition walls", "40"), ("40.20", "Doors", "40"),
        ];
        f.Run("classification.create", new JsonObject
        {
            ["name"] = "PBS",
            ["items"] = new JsonArray(items.Select(i => (JsonNode)new JsonObject { ["code"] = i.Code, ["title"] = i.Title, ["parent"] = i.Parent }).ToArray()),
        });
    }

    static string Task(Fixture f, string name, JsonObject rule)
    {
        var id = (string)f.Run("planning.addTask", new JsonObject { ["name"] = name, ["duration"] = 5 })!["id"]!;
        f.Run("planning.updateTask", new JsonObject { ["id"] = id, ["rule"] = rule });
        return id;
    }

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;

    static List<string> Elements(Fixture f, string task) =>
        (Schedule(f)["tasks"]!.AsArray().First(t => (string)t!["id"]! == task)!["elements"] as JsonArray)?.Select(x => (string)x!).Order(StringComparer.Ordinal).ToList() ?? [];

    static List<string> Sorted(params string[] ids) => [.. ids.Order(StringComparer.Ordinal)];

    static JsonArray A(params string[] xs) => new(xs.Select(x => (JsonNode)x).ToArray());

    [Fact]
    public void The_PBS_is_the_classification_named_PBS_else_the_only_one()
    {
        var f = F();
        Assert.Null(Breakdowns.Pbs(f.Model));
        f.Run("classification.create", new JsonObject { ["name"] = "Office codes" });
        // The only classification of the project.
        Assert.Equal("Office codes", Breakdowns.Pbs(f.Model)!.Name);
        MakePbs(f);
        // Two: the one named PBS.
        Assert.Equal("PBS", Breakdowns.Pbs(f.Model)!.Name);
    }

    [Fact]
    public void A_rule_by_product_and_storey_takes_exactly_the_doors_of_floor_1()
    {
        var h = Build();
        var f = h.F;
        var install = Task(f, "Install doors", new JsonObject { ["products"] = A("40.20"), ["locations"] = A(h.Floor1) });
        Assert.Equal(Sorted(h.Doors1), Elements(f, install));
        // Floor 2's door on floor 2; the product's parent takes its children (the doors and the partitions); by the storey's name too.
        var upper = Task(f, "Install doors 2", new JsonObject { ["products"] = A("40.20"), ["locations"] = A("Floor 2") });
        Assert.Equal([h.Door2], Elements(f, upper));
        var interior = Task(f, "Interior", new JsonObject { ["products"] = A("40"), ["locations"] = A(h.Floor1) });
        Assert.Equal(Sorted([.. h.Doors1, .. h.Walls1]), Elements(f, interior));
        // The building: every storey. Several locations: any of them.
        var building = Spatial.Building(f.Model)!.Id;
        var all = Task(f, "All doors", new JsonObject { ["products"] = A("40.20"), ["locations"] = A(building) });
        Assert.Equal(Sorted([.. h.Doors1, h.Door2]), Elements(f, all));
        var both = Task(f, "Both", new JsonObject { ["products"] = A("40.20"), ["locations"] = A(h.Floor1, h.Floor2) });
        Assert.Equal(Sorted([.. h.Doors1, h.Door2]), Elements(f, both));
        // Combined with the rule's other dimensions, as they were.
        var walls = Task(f, "Walls by class", new JsonObject { ["classes"] = A("IfcDoor"), ["products"] = A("40") });
        Assert.Equal(Sorted([.. h.Doors1, h.Door2]), Elements(f, walls));
        // Its words name the product.
        var rule = TaskRule.Read(f.Model.Get(install)!.Attrs["rule"])!;
        Assert.Contains("40.20 Doors", TaskElements.Describe(f.Model, _ => null, rule, null, 0, 0));
    }

    [Fact]
    public void A_room_holds_what_stands_in_it_and_a_door_on_its_edge()
    {
        var h = Build();
        var f = h.F;
        string Room(string name, params (double X, double Y)[] p) => (string)f.Run("architecture.addRoom", new JsonObject
        {
            ["storeyId"] = h.Floor1, ["name"] = name, ["points"] = new JsonArray(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray()),
        })!["id"]!;
        var left = Room("Left", (0, 0), (3, 0), (3, 5), (0, 5));
        Room("Far", (20, 0), (23, 0), (23, 3), (20, 3));
        var t = Task(f, "Doors of the left room", new JsonObject { ["products"] = A("40.20"), ["locations"] = A(left) });
        // Both doors at 1.5 m along their walls are on the left room's edge; the one at 4.5 m is not a door by its own label anyway.
        Assert.Equal(Sorted(h.Doors1), Elements(f, t));
        var far = Task(f, "Far", new JsonObject { ["products"] = A("40.20"), ["locations"] = A("Far") });
        Assert.Empty(Elements(f, far));
    }

    [Fact]
    public void Old_rules_take_the_same_elements_with_a_PBS_and_keep_their_form()
    {
        var f = F();
        var upper = (string)f.Run("storey.create", new JsonObject())!["id"]!;
        var a = f.Wall(0, 0, 6, 0);
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 });
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 1.5 });
        JsonObject[] rules =
        [
            new() { ["classes"] = A("IfcWall"), ["storeys"] = A(f.Storey) },
            new() { ["classes"] = A("IfcDoor") },
            new(),
            new() { ["storeys"] = A(upper), ["conditions"] = new JsonArray(new JsonObject { ["field"] = "@class", ["op"] = "=", ["value"] = "Wall" }) },
        ];
        var tasks = rules.Select((r, i) => Task(f, $"T{i}", (JsonObject)r.DeepClone())).ToList();
        var before = tasks.Select(t => Elements(f, t)).ToList();
        var forms = tasks.Select(t => f.Model.Get(t)!.Attrs["rule"]!.ToJsonString()).ToList();
        MakePbs(f);
        f.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(a), ["system"] = "PBS", ["code"] = "30.10" });
        Assert.Equal(before, tasks.Select(t => Elements(f, t)).ToList());
        Assert.Equal(forms, tasks.Select(t => f.Model.Get(t)!.Attrs["rule"]!.ToJsonString()).ToList());
        Assert.Equal(rules.Select(r => r.ToJsonString()), rules.Select(r => TaskRule.Read(r)!.ToJson().ToJsonString()));
    }

    [Fact]
    public void Elements_without_a_task_are_counted_by_product_and_the_check_names_it()
    {
        var h = Build();
        var f = h.F;
        Task(f, "Partitions", new JsonObject { ["products"] = A("40.10") });
        var unlinked = (JsonObject)f.Session.Query("planning.unlinked", new JsonObject())!;
        var by = unlinked["byProduct"]!.AsArray().OfType<JsonObject>().ToDictionary(x => (string)x["code"]!);
        Assert.Equal(3, (int)by["40.20"]["count"]!);
        Assert.Equal("Doors", (string)by["40.20"]["title"]!);
        Assert.Equal(1, (int)by["30.20"]["count"]!);
        Assert.False(by.ContainsKey("40.10"));
        var findings = ((JsonArray)((JsonObject)f.Session.Query("cde.check", new JsonObject())!)["findings"]!).OfType<JsonObject>().Where(x => (string?)x["rule"] == "planningTasks").ToList();
        Assert.Contains(findings, x => ((string)x["message"]!).Contains("(PBS 40.20 Doors) has no task"));
    }

    [Fact]
    public void Labels_and_product_rules_go_through_IFC_and_MS_Project_shows_the_products()
    {
        var h = Build();
        var f = h.F;
        var t = Task(f, "Install doors", new JsonObject { ["products"] = A("40.20"), ["locations"] = A(h.Floor1) });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "pbs.ifc");
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("'40.20','Doors'", ifc);
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "pbs.ifc", new IfcImportOptions(), out _);
        var pbs = Breakdowns.Pbs(g.Model);
        Assert.NotNull(pbs);
        Assert.Equal("PBS", pbs!.Name);
        // The doors labelled 40.20 again (by their type's code or their own), the own label kept.
        Assert.Equal("40.20", Breakdowns.CodeOf(g.Model, g.Model.Get(h.Door2)!, pbs.Id));
        Assert.Equal("30.20", Breakdowns.CodeOf(g.Model, g.Model.Get(h.OwnLabelled)!, pbs.Id));
        // The rule read back finds the doors of floor 1 in the new project.
        var row = Schedule(g)["tasks"]!.AsArray().OfType<JsonObject>().Single(x => (string)x["name"]! == "Install doors");
        Assert.Equal("40.20", (string)row["rule"]!["products"]![0]!);
        Assert.Equal(Sorted(h.Doors1), (row["elements"] as JsonArray)!.Select(x => (string)x!).Order(StringComparer.Ordinal).ToList());

        var xml = Encoding.UTF8.GetString(Convert.FromBase64String((string)f.Session.Query("planning.exportMsp", new JsonObject())!));
        Assert.Contains("<Alias>Lapin PBS</Alias>", xml);
        Assert.Contains("<Value>40.20</Value>", xml);
        // Read back, the rule is Text1's as before.
        var back = F();
        back.Run("planning.importMsp", new JsonObject { ["xml"] = xml });
        Assert.Contains(Schedule(back)["tasks"]!.AsArray().OfType<JsonObject>(), x => x["rule"]?["products"] is JsonArray);
        _ = t;
    }
}
