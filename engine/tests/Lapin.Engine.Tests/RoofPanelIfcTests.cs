using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof panels in IFC (docs/plan-roof-panels.md §2.6, stage S6): on a roof that names a profile, each panel is an IfcCovering
/// ROOFING aggregated by the IfcRoof in place of the covering slab, its body the panel's plate, with Lapin_RoofPanel and
/// Qto_CoveringBaseQuantities, assigned to an IfcGroup per roof plane; GlobalIds derived from the roof's and the panel's code,
/// the same at every export; IFC4 and IFC4X3; a roof without panels exported as before; read back, the roof is native with
/// its panels again, never a second layout.
/// </summary>
public class RoofPanelIfcTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static string AddRoof(Fixture f, JsonArray points, JsonObject attrs)
    {
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = points })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = attrs });
        return id;
    }

    static Fixture New() => new(new ArchitectureModule(), new RoofPanelsModule());

    static JsonObject Layout(Fixture f, string roof) => (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = roof })!;

    /// <summary>The reference roof: hipped, 10 × 6, 30°, 0.5 overhang, in Bilka Clasic 35 RAL 7016.</summary>
    static (Fixture F, string Roof) Reference(string profile = "bilka-clasic-35", string? color = "7016")
    {
        var f = New();
        var roof = AddRoof(f, Pts((0, 0), (10, 0), (10, 6), (0, 6)), new JsonObject { ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.5 });
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = roof, ["profile"] = profile, ["color"] = color });
        return (f, roof);
    }

    static StepFile Export(Fixture f, IfcExportOptions? o = null) => StepFile.Read(IfcExport.Bytes(f.Session, "panels.ifc", o).Bytes);

    static List<StepEntity> Coverings(StepFile s) => [.. s.OfType("IFCCOVERING").Where(c => c[8] is StepEnum { Value: "ROOFING" })];

    /// <summary>The property set or quantity set of this name defining an entity (through IfcRelDefinesByProperties).</summary>
    static StepEntity? SetOf(StepFile s, StepEntity e, string name) =>
        s.OfType("IFCRELDEFINESBYPROPERTIES").Where(r => r[4] is List<object?> l && l.OfType<StepRef>().Any(x => x.Id == e.Id))
            .Select(r => s.Get(r[5])).FirstOrDefault(d => d != null && (string?)d[2] == name);

    static object? Value(StepFile s, StepEntity set, string name)
    {
        foreach (var x in ((List<object?>)set[set.Type == "IFCELEMENTQUANTITY" ? 5 : 4]!).Select(s.Get))
            if (x != null && (string?)x[0] == name) return x.Type == "IFCPROPERTYSINGLEVALUE" ? (x[2] as StepTyped)?.Value : x[3];
        return null;
    }

    static double Number(object? v) => Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC4X3")]
    public void Each_panel_is_a_roofing_covering_of_the_roof_with_its_set_and_quantities(string schema)
    {
        var (f, roof) = Reference();
        var layout = Layout(f, roof);
        var panels = layout["panels"]!.AsArray().Select(p => (JsonObject)p!).ToList();
        var s = Export(f, new IfcExportOptions(Schema: schema));
        var coverings = Coverings(s);
        // As many coverings as the layout has panels, each named and tagged by its code; no covering slab left.
        Assert.Equal(panels.Count, coverings.Count);
        Assert.True(coverings.Count > 20);
        Assert.DoesNotContain(s.OfType("IFCSLAB"), x => (string?)x[2] == "Covering");
        var ifcRoof = s.OfType("IFCROOF").Single();
        // Aggregated by the IfcRoof, beside its other parts.
        var parts = s.OfType("IFCRELAGGREGATES").Single(r => r[4] is StepRef to && to.Id == ifcRoof.Id);
        var aggregated = ((List<object?>)parts[5]!).OfType<StepRef>().Select(x => x.Id).ToHashSet();
        Assert.All(coverings, c => Assert.Contains(c.Id, aggregated));
        // The set of each, its values the layout's; the material the panels' painted sheet.
        var byCode = panels.ToDictionary(p => (string)p["id"]!);
        foreach (var c in coverings)
        {
            var set = SetOf(s, c, "Lapin_RoofPanel")!;
            var code = (string)Value(s, set, "Code")!;
            var p = byCode[code];
            Assert.Equal("Bilka Clasic 35", Value(s, set, "Profile"));
            Assert.Equal("7016", Value(s, set, "Colour"));
            Assert.Equal((string)p["face"]!, Value(s, set, "RoofPlane"));
            Assert.Equal((int)p["length"]! / 1000.0, Number(Value(s, set, "Length")), 6);
            Assert.Equal(1.2, Number(Value(s, set, "GrossWidth")), 6);
            Assert.Equal(1.12, Number(Value(s, set, "UsefulWidth")), 6);
            Assert.Equal(0.15, Number(Value(s, set, "SlopeLap")), 6);
            Assert.Equal(30 * Math.PI / 180, Number(Value(s, set, "Pitch")), 4);
            Assert.Equal((int)p["column"]!, (int)Number(Value(s, set, "Column")));
            Assert.Equal((int)p["row"]!, (int)Number(Value(s, set, "Row")));
            var qto = SetOf(s, c, "Qto_CoveringBaseQuantities")!;
            Assert.Equal(Number(Value(s, set, "CoveredArea")), Number(Value(s, qto, "NetArea")), 9);
            Assert.True(Number(Value(s, qto, "GrossArea")) >= Number(Value(s, qto, "NetArea")) - 1e-9);
            Assert.Contains(s.OfType("IFCRELASSOCIATESMATERIAL"), r => r[4] is List<object?> l && l.OfType<StepRef>().Any(x => x.Id == c.Id)
                && s.Get(r[5]) is { Type: "IFCMATERIAL" } m && ((string)m[0]!).StartsWith("Bilka Clasic 35, RAL 7016", StringComparison.Ordinal));
        }
        // What they cover adds up to the layout's coverage.
        var net = coverings.Sum(c => Number(Value(s, SetOf(s, c, "Qto_CoveringBaseQuantities")!, "NetArea")));
        Assert.Equal((double)layout["totals"]!["coverage"]!, net, 3);
        Assert.Equal(RoofPanels.RoofPanels.Of(f.Model.Get(roof)!, f.Model)!.Coverage, net, 6);
    }

    [Fact]
    public void The_panels_are_grouped_by_roof_plane()
    {
        var (f, roof) = Reference();
        var panels = Layout(f, roof)["panels"]!.AsArray().Select(p => (JsonObject)p!).ToList();
        var s = Export(f);
        var coverings = Coverings(s).ToDictionary(c => c.Id, c => (string)Value(s, SetOf(s, c, "Lapin_RoofPanel")!, "RoofPlane")!);
        var groups = s.OfType("IFCGROUP").Where(g => (string?)g[4] == "Roof plane").ToList();
        // Four planes of a hipped roof, A01…A04, each with the panels laid on it, its pitch and what it covers.
        Assert.Equal(["A01", "A02", "A03", "A04"], groups.Select(g => (string)g[2]!).Order());
        foreach (var g in groups)
        {
            var rel = s.OfType("IFCRELASSIGNSTOGROUP").Single(r => r[6] is StepRef to && to.Id == g.Id);
            var members = ((List<object?>)rel[4]!).OfType<StepRef>().Select(x => x.Id).ToList();
            var plane = (string)g[2]!;
            Assert.Equal(panels.Count(p => (string)p["face"]! == plane), members.Count);
            Assert.All(members, id => Assert.Equal(plane, coverings[id]));
            var set = SetOf(s, g, "Lapin_RoofPlane")!;
            Assert.Equal(members.Count, (int)Number(Value(s, set, "Panels")));
            Assert.Equal(30 * Math.PI / 180, Number(Value(s, set, "Pitch")), 4);
        }
        Assert.Equal(coverings.Count, groups.Sum(g => ((List<object?>)s.OfType("IFCRELASSIGNSTOGROUP").Single(r => r[6] is StepRef to && to.Id == g.Id)[4]!).Count));
    }

    [Fact]
    public void The_panels_GlobalIds_are_the_same_at_every_export_and_after_an_unrelated_edit()
    {
        var (f, roof) = Reference();
        Dictionary<string, string> Ids(StepFile s) => Coverings(s).ToDictionary(c => (string)c[2]!, c => (string)c[0]!);
        HashSet<string> Groups(StepFile s) => [.. s.OfType("IFCGROUP").Select(g => (string)g[0]!)];
        var first = Export(f);
        var ids = Ids(first);
        Assert.All(ids.Values, g => Assert.True(GlobalId.IsValid(g)));
        Assert.Equal(ids.Count, ids.Values.Distinct().Count());
        var second = Export(f);
        Assert.Equal(ids, Ids(second));
        Assert.Equal(Groups(first), Groups(second));
        // A wall elsewhere: the roof's panels keep their ids.
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 20, ["y1"] = 0, ["x2"] = 25, ["y2"] = 0, ["height"] = 3 });
        var third = Export(f);
        Assert.Equal(ids, Ids(third));
        Assert.Equal(Groups(first), Groups(third));
        // Derived from the roof's GlobalId and the panel's code.
        var a = Coverings(first).First();
        var code = (string)Value(first, SetOf(first, a, "Lapin_RoofPanel")!, "Code")!;
        Assert.Equal(Step.Guid(roof, "panel:" + code), (string)a[0]!);
    }

    [Fact]
    public void A_roof_without_panels_exports_as_before()
    {
        // The same roof in a session without the module and in one with it but no profile: the same file, entity for entity.
        string Data(Fixture f)
        {
            AddRoof(f, Pts((0, 0), (10, 0), (10, 6), (0, 6)), new JsonObject { ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.5 });
            var text = Encoding.UTF8.GetString(IfcExport.Bytes(f.Session, "x.ifc").Bytes);
            return text[text.IndexOf("DATA;", StringComparison.Ordinal)..];
        }
        using (Fixture.Seeded(4242))
        {
            var without = Data(new Fixture(new ArchitectureModule()));
            var with = Data(New());
            Assert.Equal(without, with);
            Assert.Contains("'Covering'", with);
            Assert.DoesNotContain("IFCCOVERING(", with);
        }
    }

    [Fact]
    public void A_layered_roof_writes_its_lower_layers_as_parts_and_its_panels_in_place_of_the_top_one()
    {
        var f = New();
        var roof = AddRoof(f, Pts((0, 0), (10, 0), (10, 6), (0, 6)), new JsonObject
        {
            ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.3,
            ["layers"] = new JsonArray(new JsonObject { ["name"] = "Insulation", ["thickness"] = 0.12 }, new JsonObject { ["name"] = "Tiles", ["thickness"] = 0.03 }),
        });
        static List<StepEntity> PartsOf(StepFile s)
        {
            var ifcRoof = s.OfType("IFCROOF").Single();
            return [.. ((List<object?>)s.OfType("IFCRELAGGREGATES").Single(r => r[4] is StepRef to && to.Id == ifcRoof.Id)[5]!).OfType<StepRef>().Select(x => s.Get(x)!)];
        }
        var before = PartsOf(Export(f));
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = roof, ["profile"] = "bilka-clasic-35", ["color"] = "8017" });
        var count = Layout(f, roof)["panels"]!.AsArray().Count;
        var s = Export(f);
        Assert.Equal(count, Coverings(s).Count);
        var parts = PartsOf(s);
        // The top layer's part gone, the panels in its place, the insulation and the rest of the roof's parts as before; the panels' plates as thick as the top layer was.
        Assert.Equal(before.Count - 1 + count, parts.Count);
        Assert.Equal(before.Count - 1, parts.Count(p => p.Type != "IFCCOVERING"));
        Assert.All(Coverings(s), c => Assert.Equal(0.03, Number(Value(s, SetOf(s, c, "Qto_CoveringBaseQuantities")!, "Width")), 6));
    }

    [Fact]
    public void A_roof_left_out_of_the_export_leaves_its_panels_out_too()
    {
        var (f, roof) = Reference();
        var s = Export(f, new IfcExportOptions(Exclude: new HashSet<string> { roof }));
        Assert.Empty(s.OfType("IFCROOF"));
        Assert.Empty(Coverings(s));
        Assert.DoesNotContain(s.OfType("IFCGROUP"), g => (string?)g[4] == "Roof plane");
    }

    [Fact]
    public void Read_back_the_roof_is_native_with_its_panels_and_no_second_layout()
    {
        var (f, roof) = Reference();
        var bytes = IfcExport.Bytes(f.Session, "panels.ifc").Bytes;
        var count = Layout(f, roof)["panels"]!.AsArray().Count;
        var g = New();
        IfcImport.Run(g.Session, bytes, "panels.ifc", null, out _);
        // One roof, native, naming its profile again: its panels are its layout, worked out; none came as an element of its own.
        var back = Assert.Single(g.Model.OfClass(IfcClass.Roof));
        Assert.NotNull(RoofPanels.RoofPanels.ChoiceOf(back));
        Assert.Equal(count, Layout(g, back.Id)["panels"]!.AsArray().Count);
        Assert.DoesNotContain(g.Model.Entities, e => e.Class == "IfcCovering");
        Assert.DoesNotContain(g.Model.Entities, e => e.Class == "IfcGroup" && e.Name is "A01" or "A02" or "A03" or "A04");
        // Exported again, the same panels with the same GlobalIds.
        var again = StepFile.Read(IfcExport.Bytes(g.Session, "again.ifc").Bytes);
        Assert.Equal(Coverings(StepFile.Read(bytes)).Select(c => (string)c[0]!).Order(), Coverings(again).Select(c => (string)c[0]!).Order());
    }

    [Fact]
    public void Someone_elses_file_with_panels_brings_them_as_references()
    {
        var (f, roof) = Reference();
        var count = Layout(f, roof)["panels"]!.AsArray().Count;
        // The same file without Lapin's intent: what another program wrote.
        var text = Encoding.UTF8.GetString(IfcExport.Bytes(f.Session, "panels.ifc").Bytes).Replace("'" + IfcExport.IntentSetName + "'", "'Other_Intent'");
        var g = New();
        IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(text), "other.ifc", null, out _);
        Assert.DoesNotContain(g.Model.Entities, e => e.Class == IfcClass.Roof && RoofPanels.RoofPanels.ChoiceOf(e) != null);
        Assert.Equal(count, g.Model.Entities.Count(e => e.Class == "IfcCovering"));
    }
}
