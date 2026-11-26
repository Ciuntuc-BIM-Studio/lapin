using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A sloped roof's covering one IfcSlab ROOF per slope (issue #139, a CYPECAD MEP tester): an export option, off by default (the
/// covering one slab of all its slopes, decisions 328 and 329, the file unchanged). On, the covering (the deck of a roof without
/// framing, the custom layers) is one slab per slope under the IfcRoof, its GlobalId the roof's and the slope's, stable from one
/// export to the next; the timber, gutters and downpipes as before; with one body each the roof is still written over its slopes;
/// a vault stays one; Lapin reads such a file back as the one roof.
/// </summary>
public sealed class RoofSlopesIfcTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-roof-slopes").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray House10x6 => new(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6));

    static string AddRoof(Fixture f, string style) =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style })!["id"]!;

    string Export(Fixture f, string name, IfcExportOptions? options = null)
    {
        var path = Path.Combine(_dir, name);
        IfcExport.Write(f.Session, path, options);
        return path;
    }

    static List<StepEntity> RoofSlabs(StepFile f) => f.OfType("IFCSLAB").Where(s => s[8] is StepEnum { Value: "ROOF" }).ToList();

    static bool PartOf(StepFile f, StepEntity whole, StepEntity part) =>
        f.OfType("IFCRELAGGREGATES").Any(r => r[4] is StepRef to && to.Id == whole.Id && r[5] is List<object?> parts && parts.OfType<StepRef>().Any(x => x.Id == part.Id));

    static IEnumerable<string> Lines(string path) => File.ReadAllLines(path).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal));

    [Fact]
    public void Off_by_default_the_covering_is_one_slab_and_the_file_is_unchanged()
    {
        var f = new Fixture();
        AddRoof(f, "ro_saxon");
        var path = Export(f, "default.ifc");
        var file = StepFile.Read(path);
        Assert.Equal("Covering", Assert.Single(RoofSlabs(file))[2]);
        Assert.Equal(Lines(path), Lines(Export(f, "off.ifc", new IfcExportOptions(RoofSlopes: false))));
    }

    [Fact]
    public void On_a_gable_roofs_covering_is_one_roof_slab_per_slope_under_the_roof_with_stable_ids()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "ro_saxon");
        var before = StepFile.Read(Export(f, "whole.ifc"));
        var file = StepFile.Read(Export(f, "slopes.ifc", new IfcExportOptions(RoofSlopes: true)));
        var ifcRoof = file.OfType("IFCROOF").Single();
        var slabs = RoofSlabs(file);
        Assert.Equal(["Covering, slope 1", "Covering, slope 2"], slabs.Select(s => (string)s[2]!).Order());
        Assert.All(slabs, s => Assert.True(PartOf(file, ifcRoof, s)));
        // Each of the covering's material.
        Assert.All(slabs, s => Assert.Contains(file.OfType("IFCRELASSOCIATESMATERIAL"), r => r[4] is List<object?> l && l.OfType<StepRef>().Any(x => x.Id == s.Id)));
        // The timber, the gutters as before.
        Assert.Equal(before.OfType("IFCMEMBER").Count(), file.OfType("IFCMEMBER").Count());
        Assert.Equal(before.OfType("IFCPIPESEGMENT").Count(), file.OfType("IFCPIPESEGMENT").Count());
        // The same GlobalIds from one export to the next, and after the pitch changes.
        var ids = slabs.Select(s => (string)s[0]!).Order().ToList();
        Assert.Equal(ids, RoofSlabs(StepFile.Read(Export(f, "again.ifc", new IfcExportOptions(RoofSlopes: true)))).Select(s => (string)s[0]!).Order());
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["pitch"] = 35 } });
        Assert.Equal(ids, RoofSlabs(StepFile.Read(Export(f, "pitched.ifc", new IfcExportOptions(RoofSlopes: true)))).Select(s => (string)s[0]!).Order());
    }

    [Fact]
    public void The_slopes_are_the_covering_in_pieces()
    {
        var f = new Fixture();
        var hip = AddRoof(f, "traditional_campie");
        var covering = f.Session.Geometry(hip)!.Parts.Single(p => p.Key == "covering");
        Assert.Equal(4, covering.Pieces!.Count);
        Assert.Equal(["slope:1", "slope:2", "slope:3", "slope:4"], covering.Pieces.Select(p => p.Key));
        Assert.Equal(Measure.Volume(covering.Solid), covering.Pieces.Sum(p => Measure.Volume(p.Solid)), 6);
        // A solid given anew is no longer the pieces.
        Assert.Null((covering with { Solid = covering.Pieces[0].Solid }).Pieces);
    }

    [Fact]
    public void Without_framing_the_deck_and_the_covering_are_each_split_and_a_vault_stays_whole()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "ro_saxon");
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["structure"] = "none" } });
        var file = StepFile.Read(Export(f, "deck.ifc", new IfcExportOptions(RoofSlopes: true)));
        Assert.Equal(["Covering, slope 1", "Covering, slope 2", "Deck, slope 1", "Deck, slope 2"], RoofSlabs(file).Select(s => (string)s[2]!).Order());

        var g = new Fixture();
        AddRoof(g, "modern_barrel");
        var vault = StepFile.Read(Export(g, "vault.ifc", new IfcExportOptions(RoofSlopes: true)));
        Assert.Equal(["Covering", "Deck"], RoofSlabs(vault).Select(s => (string)s[2]!).Order());
    }

    [Fact]
    public void With_one_body_each_the_roof_is_still_written_over_its_slopes()
    {
        var f = new Fixture();
        AddRoof(f, "ro_saxon");
        var simple = StepFile.Read(Export(f, "simple.ifc", new IfcExportOptions(Detail: "simple")));
        Assert.Empty(RoofSlabs(simple));
        var file = StepFile.Read(Export(f, "simple-slopes.ifc", new IfcExportOptions(Detail: "simple", RoofSlopes: true)));
        var ifcRoof = file.OfType("IFCROOF").Single();
        Assert.Equal(2, RoofSlabs(file).Count);
        Assert.All(RoofSlabs(file), s => Assert.True(PartOf(file, ifcRoof, s)));
    }

    [Fact]
    public void Lapin_reads_a_file_of_slopes_back_as_the_one_roof()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "ro_saxon");
        var path = Export(f, "back.ifc", new IfcExportOptions(RoofSlopes: true));
        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        var back = Assert.Single(g.Model.OfClass(IfcClass.Roof));
        Assert.Equal(roof, back.Id);
        Assert.DoesNotContain(g.Model.Entities, e => e.Class == IfcClass.Slab);
        Assert.Equal(2, g.Session.Geometry(back.Id)!.Parts.Single(p => p.Key == "covering").Pieces!.Count);
    }

    [Fact]
    public void The_export_command_takes_the_option()
    {
        var f = new Fixture();
        AddRoof(f, "ro_saxon");
        var path = Path.Combine(_dir, "command.ifc");
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "project.exportIfc", ["params"] = new JsonObject { ["path"] = path, ["roofSlopes"] = true } }.ToJsonString(), Caller.Local);
        Assert.Null(JsonNode.Parse(BinaryFrame.HeaderText(reply))!["error"]);
        Assert.Equal(2, RoofSlabs(StepFile.Read(path)).Count);
    }
}
