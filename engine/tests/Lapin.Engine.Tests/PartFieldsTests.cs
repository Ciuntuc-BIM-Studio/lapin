using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Cost;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Studio;
using Parts = Lapin.Studio.Parts;

namespace Lapin.Engine.Tests;

/// <summary>
/// R6 of docs/plan-forum-round-1126.md (Fabio Emmolo's request): a placed part's published parameters, derived ones too, are its
/// fields — field schedules' columns, filters, formulas and tags read them by the part's parameter names, worked out on every ask —
/// and a parameter its author marks as a measure (Length, Count, Area, Volume) is a quantity of the part (Lapin_PartQuantities)
/// that the estimate's articles read first, schedules list and IFC writes; a part without marks is measured as before.
/// </summary>
public sealed class PartFieldsTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule(), new CostModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    /// <summary>A fence: L long (20 m by default here), posts every 2.5 m (N, derived), H high, a hidden gap; a body L × 0.05 × H.</summary>
    static JsonObject Fence(string? lengthMeasure = null, string? countMeasure = null)
    {
        var l = new JsonObject { ["key"] = "L", ["label"] = "Length", ["default"] = 10, ["max"] = 100 };
        if (lengthMeasure != null) l["measure"] = lengthMeasure;
        var n = new JsonObject { ["key"] = "N", ["label"] = "Posts", ["kind"] = "integer", ["formula"] = "floor(L / 2.5) + 1" };
        if (countMeasure != null) n["measure"] = countMeasure;
        return new JsonObject
        {
            ["params"] = J(l, n,
                new JsonObject { ["key"] = "H", ["default"] = 1.2 },
                new JsonObject { ["key"] = "Gap", ["default"] = 0.01, ["published"] = false },
                new JsonObject { ["key"] = "Code", ["kind"] = "text", ["default"] = "F-1" }),
            ["bodies"] = J(new JsonObject { ["name"] = "Panel", ["plane"] = "xz", ["points"] = J(J(0, 0), J("L", 0), J("L", "H"), J(0, "H")), ["depth"] = 0.05 }),
        };
    }

    static string Part(Fixture f, JsonObject def) => (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Fence", ["def"] = def })!["id"]!;
    static string Place(Fixture f, string part, JsonObject? values = null, double x = 0) =>
        (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(x, 0), ["values"] = values })!["id"]!;

    static ScheduleGrid Grid(Fixture f, string id) => new FieldSchedule().Grid(f.Session.Reader, f.Model.Get(id)!);

    const string Prices = "Cod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport\n" +
        "GARD01;Gard din panouri;m;30;10;0;0\n" +
        "STALP01;Stâlp de gard;buc;12;3;0;0\n" +
        "VOL01;Panou la volum;mc;1000;0;0;0\n";

    static JsonObject Estimate(Fixture f) => (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
    static JsonObject? Line(JsonObject est, string code) =>
        est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()).FirstOrDefault(l => (string)l!["code"]! == code) as JsonObject;

    [Fact]
    public void A_placed_part_s_values_are_fields_of_schedules_filters_formulas_and_tags()
    {
        var f = F();
        var part = Part(f, Fence());
        var a = Place(f, part, new JsonObject { ["L"] = 20 });
        var b = Place(f, part, null, 30);

        // A field schedule of the fence parts: L (its own 20, the other's default 10), the derived N, a formula of both, a text.
        var id = (string)f.Run("architecture.createSchedule", new JsonObject
        {
            ["kind"] = "fields", ["name"] = "Fences",
            ["fields"] = new JsonObject
            {
                ["filter"] = new JsonObject { ["classes"] = J("IfcBuildingElementProxy") },
                ["columns"] = J(
                    new JsonObject { ["field"] = "name" },
                    new JsonObject { ["field"] = "L", ["aggregate"] = "sum", ["decimals"] = 2 },
                    new JsonObject { ["field"] = "N", ["aggregate"] = "sum" },
                    new JsonObject { ["formula"] = "L * N", ["heading"] = "LN" },
                    new JsonObject { ["field"] = "Lapin_PartParameters.Code" },
                    new JsonObject { ["field"] = "Gap" }),
            },
        })!["id"]!;
        var g = Grid(f, id);
        Assert.Equal(3, g.Rows.Count);
        string[] Of(ScheduleGrid grid, string id) => [.. grid.Rows[grid.Keys!.ToList().IndexOf(id)].Skip(1)];
        Assert.Equal(["20.00", "9", "180", "F-1", ""], Of(g, a));
        Assert.Equal(["10.00", "5", "50", "F-1", ""], Of(g, b));
        Assert.Equal(["Total", "30.00", "14"], g.Rows[2].Take(3));

        // Worked out on every ask: a new value is in the next grid, nothing was stored on the element.
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(b), ["values"] = new JsonObject { ["L"] = 5 } });
        Assert.Equal(["5.00", "3", "15", "F-1", ""], Of(Grid(f, id), b));
        Assert.Null(f.Model.Get(b)!.Attrs["L"]);

        // Filters compare them as numbers.
        int Count(string field, string op, JsonNode? value) =>
            ElementFilter.Elements(f.Session.Reader, ElementFilter.Check(new JsonObject { ["conditions"] = J(new JsonObject { ["field"] = field, ["op"] = op, ["value"] = value }) })).Count;
        Assert.Equal(1, Count("L", ">", 10));
        Assert.Equal(2, Count("N", ">=", 3));
        Assert.Equal(0, Count("Gap", "exists", null));

        // Formulas, tags and live text read them as a parameter of the user's own (a length in the project's units in a tag).
        var reader = new ElementFields(f.Session.Reader, f.Model.Get(a)!);
        Assert.Equal(40, reader.Value("= L * 2").Number);
        Assert.Equal("9", TagText.Value(f.Model, f.Model.Get(a)!, "N"));
        Assert.Equal("20000", TagText.Value(f.Model, f.Model.Get(a)!, "L"));
        Assert.Equal("F-1", TagText.Value(f.Model, f.Model.Get(a)!, "Code"));

        // The fields' pickers list them among its parameters, the unpublished one left out.
        var fields = FieldCatalog.Across(f.Session.Reader, null);
        var ps = fields["groups"]!.AsArray().Single(x => (string)x!["id"]! == "params")!["fields"]!.AsArray();
        Assert.Contains(ps, x => (string)x!["token"]! == "L" && (string)x["set"]! == PartIfc.SetName && (string)x["kind"]! == "number" && (int)x["count"]! == 2);
        Assert.Contains(ps, x => (string)x!["token"]! == "N");
        Assert.DoesNotContain(ps, x => (string)x!["token"]! == "Gap");
        var available = FieldCatalog.Available(f.Session.Reader, f.Model.Get(a)!);
        var mine = available["groups"]!.AsArray().Single(x => (string)x!["id"]! == "params")!["fields"]!.AsArray();
        Assert.Contains(mine, x => (string)x!["token"]! == "N" && (string)x["value"]! == "9");

        // Not marked: no quantity of its own, the quantities the body gives as before.
        Assert.Equal(["Qto_BuildingElementProxyBaseQuantities"], f.Session.Quantities(a).Select(s => s.Name));
    }

    [Fact]
    public void A_parameter_marked_as_a_measure_is_a_quantity_the_estimate_prices_by()
    {
        var f = F();
        f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Prices)), ["name"] = "Gard.csv" });
        var part = Part(f, Fence());
        var a = Place(f, part, new JsonObject { ["L"] = 20 });
        // A price per metre, one per post, one by volume: on the part, the type its placed parts have.
        f.Run("cost.setArticles", new JsonObject
        {
            ["type"] = part,
            ["articles"] = J(new JsonObject { ["code"] = "GARD01" }, new JsonObject { ["code"] = "STALP01" }, new JsonObject { ["code"] = "VOL01" }),
        });
        f.Run("cost.createEstimate", new JsonObject { ["name"] = "Gard" });

        // Before any mark (today's): a metre has nothing to read on a proxy, a post is one per element, the volume its body's.
        var e = Estimate(f);
        Assert.Null(Line(e, "GARD01"));
        Assert.Equal(1, (double)Line(e, "STALP01")!["qty"]!);
        Assert.Equal(20 * 1.2 * 0.05, (double)Line(e, "VOL01")!["qty"]!, 6);

        // L marked Length, N marked Count: 20 m at 40 a metre, 9 posts at 15; the volume unchanged.
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "L", ["changes"] = new JsonObject { ["measure"] = "length" } });
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "N", ["changes"] = new JsonObject { ["measure"] = "count" } });
        var set = f.Session.Quantities(a).Single(s => s.Name == AuthoredQuantities.Set);
        Assert.Equal([("L", 20.0, "LENGTH"), ("N", 9.0, "COUNT")], set.Quantities.Select(q => (q.Name, q.Value, q.Kind)));
        e = Estimate(f);
        Assert.Equal(20, (double)Line(e, "GARD01")!["qty"]!, 9);
        Assert.Equal(800, (double)Line(e, "GARD01")!["value"]!, 2);
        Assert.Equal(9, (double)Line(e, "STALP01")!["qty"]!, 9);
        Assert.Equal(135, (double)Line(e, "STALP01")!["value"]!, 2);
        Assert.Equal(20 * 1.2 * 0.05, (double)Line(e, "VOL01")!["qty"]!, 6);

        // Worked out on every ask: a longer fence is priced longer.
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["L"] = 30 } });
        Assert.Equal(30, (double)Line(Estimate(f), "GARD01")!["qty"]!, 9);
        Assert.Equal(13, (double)Line(Estimate(f), "STALP01")!["qty"]!, 9);

        // A schedule column reads the quantity by its set and name.
        var grid = new FieldSchedule().Grid(f.Session.Reader, f.Model.Get((string)f.Run("architecture.createSchedule", new JsonObject
        {
            ["kind"] = "fields", ["fields"] = new JsonObject { ["filter"] = new JsonObject { ["classes"] = J("IfcBuildingElementProxy") }, ["columns"] = J(new JsonObject { ["field"] = "name" }, new JsonObject { ["field"] = "Lapin_PartQuantities.L" }) },
        })!["id"]!)!);
        Assert.Equal("30", grid.Rows[0][1]);

        // Cleared (null), undone: one undo step each.
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "L", ["changes"] = new JsonObject { ["measure"] = null } });
        Assert.Null(Line(Estimate(f), "GARD01"));
        Assert.Null(Parts.DefOf(f.Model.Get(part))!["params"]![0]!["measure"]);
        f.Session.Undo();
        Assert.Equal("length", (string)Parts.DefOf(f.Model.Get(part))!["params"]![0]!["measure"]!);

        // Refused: a measure Lapin does not know, a text or a yes/no as a measure.
        Assert.Throws<CommandException>(() => f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "H", ["changes"] = new JsonObject { ["measure"] = "weight" } }));
        Assert.Throws<CommandException>(() => f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "Code", ["changes"] = new JsonObject { ["measure"] = "length" } }));
        // A kind that measures nothing drops the mark.
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "L", ["changes"] = new JsonObject { ["kind"] = "angle" } });
        Assert.Null(Parts.DefOf(f.Model.Get(part))!["params"]![0]!["measure"]);
        f.Session.Undo();

        // The part tree shows the mark.
        var tree = (JsonObject)f.Session.Query("studio.partTree", new JsonObject { ["id"] = a })!;
        Assert.Equal("length", (string)tree["params"]!.AsArray().Single(p => (string)p!["key"]! == "L")!["measure"]!);
    }

    [Fact]
    public void Measures_go_to_IFC_as_Lapin_s_own_quantity_set_and_saved_parts_open_unchanged()
    {
        var f = F();
        var marked = Part(f, Fence("length", "count"));
        var plain = Part(f, Fence());
        var a = Place(f, marked, new JsonObject { ["L"] = 20 });
        var b = Place(f, plain, new JsonObject { ["L"] = 20 }, 30);

        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "fence.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(ifc, "IFCELEMENTQUANTITY\\('[^']*',[^,]*,'Lapin_PartQuantities'"));
        Assert.Contains("IFCQUANTITYLENGTH('L','Length',$,20.", ifc);
        Assert.Contains("IFCQUANTITYCOUNT('N','Posts',$,9,", ifc);
        Assert.DoesNotContain("Qto_PartQuantities", ifc);
        // The same file twice: the set's ids stable.
        using var ms2 = new MemoryStream();
        IfcExport.Write(f.Session, ms2, "fence.ifc", new IfcExportOptions());
        Assert.Equal(
            System.Text.RegularExpressions.Regex.Match(ifc, "IFCELEMENTQUANTITY\\('([^']*)',[^,]*,'Lapin_PartQuantities'").Groups[1].Value,
            System.Text.RegularExpressions.Regex.Match(Encoding.UTF8.GetString(ms2.ToArray()), "IFCELEMENTQUANTITY\\('([^']*)',[^,]*,'Lapin_PartQuantities'").Groups[1].Value);

        // Saved and opened: the marks kept, the part without them still without, its measures as before.
        using var pkg = new MemoryStream();
        ProjectPackage.Write(f.Session, pkg);
        var g = F();
        var content = LapinPackage.Read(new MemoryStream(pkg.ToArray()));
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal("length", (string)Parts.DefOf(g.Model.Get(marked))!["params"]![0]!["measure"]!);
        Assert.False(Parts.DefOf(g.Model.Get(plain))!["params"]![0]!.AsObject().ContainsKey("measure"));
        Assert.Equal(f.Session.Quantities(b).Select(s => s.Name), g.Session.Quantities(b).Select(s => s.Name));
        Assert.Equal(20, g.Session.Quantities(a).Single(s => s.Name == AuthoredQuantities.Set).Quantities[0].Value, 9);
        Assert.Null(g.Model.Get(a)!.Attrs[AuthoredQuantities.Set]);
    }
}
