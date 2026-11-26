using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The hook through which another module (an extension module: planning-plus's cash-flow terms) keeps properties of an
/// estimate in IFC (docs/plan-extension-modules.md §6.3, S5): written in Lapin_Estimate after Lapin's own, read back into
/// the estimate's attributes on import by an edition that has the module; without one the set is what it was, and an
/// edition without the module reads the file as before.
/// </summary>
public sealed class EstimateIfcPropertiesTests
{
    sealed class Terms : IEstimateIfcProperties
    {
        public IEnumerable<(string Name, string Value)> Write(Entity estimate) =>
            estimate.Attrs["terms"] is JsonObject o ? o.Select(kv => ("Terms." + kv.Key, kv.Value?.ToString() ?? "")).Append(("Vat", "99")) : [];

        public void Read(IReadOnlyDictionary<string, string> properties, JsonObject attrs)
        {
            var o = new JsonObject();
            foreach (var (k, v) in properties) if (k.StartsWith("Terms.", StringComparison.Ordinal)) o[k["Terms.".Length..]] = v;
            if (o.Count > 0) attrs["terms"] = o;
        }
    }

    sealed class TermsModule : ILapinModule
    {
        public string Id => "terms";
        public string Name => "Terms (test)";
        public void Register(IModuleRegistry r) => r.Service<IEstimateIfcProperties>(new Terms());
    }

    static Fixture F(bool terms) => terms ? new(new ArchitectureModule(), new CostModule(), new TermsModule()) : new(new ArchitectureModule(), new CostModule());

    static byte[] Export(Fixture f)
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "deviz.ifc");
        return ms.ToArray();
    }

    static List<(string Name, string Value)> EstimateSet(byte[] ifc)
    {
        var step = StepFile.Read(ifc);
        var set = step.OfType("IFCPROPERTYSET").Single(p => (string?)p[2] == "Lapin_Estimate");
        var props = ((List<object?>)set[4]!).OfType<StepRef>().Select(r => step.Get(r)!).ToList();
        return props.Select(p => ((string)p[0]!, p[2] is StepTyped t ? t.Value?.ToString() ?? "" : "")).ToList();
    }

    static string Estimate(Fixture f, JsonObject? terms)
    {
        var id = (string)f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz" })!["id"]!;
        // A line of its own: the import reads a cost schedule with items.
        f.Run("cost.addLine", new JsonObject { ["id"] = id, ["name"] = "Provision", ["qty"] = 1, ["m"] = 500 });
        if (terms != null)
            f.Session.Edit("Terms", edit =>
            {
                var e = edit.Model.Get(id)!;
                var a = e.CloneAttrs();
                a["terms"] = terms;
                edit.Replace(e.With(attrs: a));
            });
        return id;
    }

    [Fact]
    public void A_modules_properties_go_into_Lapin_Estimate_after_Lapins_own_and_come_back_into_the_estimate()
    {
        var f = F(terms: true);
        Estimate(f, new JsonObject { ["retention"] = "5", ["advance"] = "10" });
        var ifc = Export(f);
        var set = EstimateSet(ifc);
        Assert.Equal(["Currency", "EurRate", "LabourTax", "Overheads", "Profit", "Vat", "Grouping", "Equivalent", "Terms.retention", "Terms.advance"], set.Select(p => p.Name));
        // A name of Lapin's own is never written over: its Vat stays the estimate's.
        Assert.NotEqual("99", set.Single(p => p.Name == "Vat").Value);

        var g = F(terms: true);
        IfcImport.Run(g.Session, ifc, "deviz.ifc", new IfcImportOptions(), out _);
        var back = g.Model.OfClass(Costing.Class).Single();
        Assert.Equal("""{"retention":"5","advance":"10"}""", back.Attrs["terms"]!.ToJsonString());

        // An edition without the module reads the estimate as before, the module's properties left unread.
        var h = F(terms: false);
        IfcImport.Run(h.Session, ifc, "deviz.ifc", new IfcImportOptions(), out _);
        Assert.False(h.Model.OfClass(Costing.Class).Single().Attrs.ContainsKey("terms"));
    }

    [Fact]
    public void Without_a_module_or_its_data_the_set_is_as_before()
    {
        var plain = F(terms: false);
        Estimate(plain, null);
        var withModule = F(terms: true);
        Estimate(withModule, null);
        Assert.Equal(EstimateSet(Export(plain)).Select(p => p.Name), EstimateSet(Export(withModule)).Select(p => p.Name));
        Assert.Equal(8, EstimateSet(Export(plain)).Count);
    }
}
