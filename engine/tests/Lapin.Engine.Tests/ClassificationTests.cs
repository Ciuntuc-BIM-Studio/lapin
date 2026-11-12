using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Classifications, keynotes and shared parameters (docs/plan-user-extensibility.md §7.3): a system read from a table, its codes put
/// on elements and types (by hand and from a table), read by tags (a keynote), written to IFC; a system deleted takes its codes
/// with it; shared parameter definitions carried by file and giving a parameter its kind and set.
/// </summary>
public sealed class ClassificationTests
{
    static Fixture F() => new(new ArchitectureModule());

    static string Uniclass(Fixture f)
    {
        var csv = "Code;Title;Parent\nSs;Systems;\nSs_25;Wall and barrier systems;Ss\nSs_25_10;Framed wall systems;Ss_25\nSs_25_11;Monolithic wall systems;Ss_25\n";
        f.Run("tables.import", new JsonObject { ["target"] = "core.classification", ["text"] = csv, ["defaults"] = new JsonObject { ["system"] = "Uniclass" } });
        return Classifications.System(f.Model, "Uniclass")!.Id;
    }

    [Fact]
    public void A_system_comes_from_a_table_with_its_tree()
    {
        var f = F();
        var id = Uniclass(f);
        var top = f.Session.Query("classification.items", new JsonObject { ["id"] = id, ["parent"] = "Ss_25" })!["items"]!.AsArray();
        Assert.Equal(["Ss_25_10", "Ss_25_11"], top.Select(i => (string)i!["code"]!).ToArray());
        var found = f.Session.Query("classification.items", new JsonObject { ["id"] = id, ["search"] = "monolithic" })!["items"]!.AsArray();
        Assert.Equal("Ss_25_11", (string)found.Single()!["code"]!);
        // Read again: titles changed, nothing doubled.
        f.Run("tables.import", new JsonObject { ["target"] = "core.classification", ["text"] = "Code;Title\nSs_25_10;Framed walls\n", ["defaults"] = new JsonObject { ["system"] = "Uniclass" } });
        var items = Classifications.Items(f.Model.Get(id)!);
        Assert.Equal(4, items.Count);
        Assert.Equal("Framed walls", items.Single(i => i.Code == "Ss_25_10").Title);
    }

    [Fact]
    public void Codes_go_on_elements_and_types_tags_read_them_and_IFC_carries_them()
    {
        var f = F();
        Uniclass(f);
        var wall = f.Wall(0, 0, 5, 0);
        var other = f.Wall(5, 0, 5, 4);
        f.Run("element.rename", new JsonObject { ["id"] = other, ["name"] = "W-02" });
        f.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(wall), ["system"] = "Uniclass", ["code"] = "Ss_25_10" });
        f.Run("tables.import", new JsonObject { ["target"] = "core.classify", ["text"] = "Element;Code\nW-02;Ss_25_11\n", ["defaults"] = new JsonObject { ["system"] = "Uniclass" } });
        var e = f.Model.Get(wall)!;
        Assert.Equal("Ss_25_10", TagText.Value(f.Model, e, "classification.code"));
        Assert.Equal("Framed wall systems", TagText.Value(f.Model, e, "classification.title"));
        Assert.Equal("Ss_25_11", TagText.Value(f.Model, f.Model.Get(other)!, "classification.Uniclass"));
        // A code the system lacks is refused, unless it is added (a keynote of the office's own).
        Assert.Throws<CommandException>(() => f.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(wall), ["system"] = "Uniclass", ["code"] = "X1" }));
        f.Run("classification.create", new JsonObject { ["name"] = "Keynotes" });
        f.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(wall), ["system"] = "Keynotes", ["code"] = "K12", ["title"] = "Plaster 15 mm", ["add"] = true });
        Assert.Equal("K12", TagText.Value(f.Model, f.Model.Get(wall)!, "classification.Keynotes"));

        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "classified.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCCLASSIFICATION(", ifc);
        Assert.Contains("'Ss_25_10','Framed wall systems'", ifc);
        Assert.Equal(3, ifc.Split("IFCRELASSOCIATESCLASSIFICATION(").Length - 1);

        // The system deleted: its codes go from the elements; the keynotes stay.
        f.Run("classification.delete", new JsonObject { ["id"] = Classifications.System(f.Model, "Uniclass")!.Id });
        Assert.Equal(["K12"], Classifications.RefsOf(f.Model.Get(wall)!).Select(r => r.Code).ToArray());
        Assert.Empty(Classifications.RefsOf(f.Model.Get(other)!));
    }

    [Fact]
    public void Shared_parameter_definitions_travel_by_file_and_give_a_parameter_its_kind_and_set()
    {
        var a = F();
        a.Run("params.setDef", new JsonObject { ["def"] = new JsonObject { ["name"] = "FireRating", ["kind"] = "text", ["pset"] = "Pset_WallCommon" } });
        a.Run("params.setDef", new JsonObject { ["def"] = new JsonObject { ["name"] = "U-value", ["kind"] = "number", ["pset"] = "Office_Thermal", ["description"] = "W/m²K" } });
        var text = (string)a.Session.Query("params.exportDefs", [])!["text"]!;

        var b = F();
        Assert.Equal(2, (int)b.Run("params.importDefs", new JsonObject { ["text"] = text })!["added"]!);
        var wall = b.Wall(0, 0, 4, 0);
        b.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(wall), ["name"] = "U-value", ["value"] = 0.18 });
        var p = (b.Model.Get(wall)!.Attrs["params"] as JsonArray)!.OfType<JsonObject>().Single();
        Assert.Equal("Office_Thermal", (string)p["pset"]!);
        Assert.Equal("number", (string)p["kind"]!);
        // Brought in again: nothing new; the ids kept.
        Assert.Equal(0, (int)b.Run("params.importDefs", new JsonObject { ["text"] = text })!["added"]!);
        Assert.Throws<CommandException>(() => b.Run("params.setDef", new JsonObject { ["def"] = new JsonObject { ["name"] = "X", ["kind"] = "text", ["pset"] = "Pset_Mine" } }));
    }

    [Fact]
    public void Codes_come_back_from_the_IFC_file_onto_the_elements()
    {
        var a = F();
        Uniclass(a);
        var wall = a.Wall(0, 0, 5, 0);
        a.Run("classification.assign", new JsonObject { ["ids"] = new JsonArray(wall), ["system"] = "Uniclass", ["code"] = "Ss_25_10" });
        var path = Path.Combine(Path.GetTempPath(), $"lapin-class-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(a.Session, path);
            var b = new Fixture(new ArchitectureModule());
            IfcImport.Run(b.Session, path, null, out _);
            var system = Classifications.System(b.Model, "Uniclass");
            Assert.NotNull(system);
            Assert.Equal("Framed wall systems", Classifications.Items(system!).Single(i => i.Code == "Ss_25_10").Title);
            var classified = b.Model.Entities.Where(e => Classifications.RefsOf(e).Any(r => r.Code == "Ss_25_10")).ToList();
            Assert.Single(classified);
        }
        finally { File.Delete(path); }
    }
}
