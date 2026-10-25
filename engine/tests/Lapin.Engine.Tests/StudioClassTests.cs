using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>A Studio solid's IFC class, predefined type and type from the inspector, and what follows from them: quantities, cut priority, export.</summary>
public class StudioClassTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static IReadOnlyList<Quantity> Q(Fixture f, string id) => f.Session.Quantities(id).Single().Quantities;
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void A_pavement_slab_measured_as_a_slab_ranked_as_slabs_and_exported_with_its_type()
    {
        var f = F();
        var plan = Plan(f);
        // A 4 × 2 m block 0.15 thick, under a wall's foot (the wall from 0,1 to 6,1).
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, plan, Rect(1, 0, 5, 2)), ["depth"] = 0.15 })!["id"]!;
        var wall = f.Wall(0, 1, 6, 1);
        var wallFull = Vol(f, wall);
        Assert.True(Vol(f, solid) < 4 * 2 * 0.15 - 1e-6, "a proxy gives way to the wall");
        f.Run("studio.setClass", new JsonObject { ["id"] = solid, ["class"] = "IfcSlab", ["predefinedType"] = "paving" });
        var e = f.Model.Get(solid)!;
        Assert.Equal(IfcClass.BuildingElementProxy, e.Class);
        Assert.Equal("IfcSlab", e.Str("exportAs"));
        Assert.Equal("PAVING", e.PredefinedType);
        // Measured as a slab: its area and thickness; ranked as slabs (60), it now cuts the wall's foot.
        Assert.Equal("Qto_SlabBaseQuantities", f.Session.Quantities(solid).Single().Name);
        Assert.Equal(0.15, Q(f, solid).Single(q => q.Name == "Width").Value, 9);
        Assert.Equal(8, Q(f, solid).Single(q => q.Name == "NetArea").Value, 6);
        Assert.Equal(4 * 2 * 0.15, Vol(f, solid), 6);
        Assert.True(Vol(f, wall) < wallFull - 1e-6);
        // A type, kept when the class changes again; exported as the slab type.
        var type = (string)f.Run("studio.setType", new JsonObject { ["ids"] = new JsonArray(solid), ["name"] = "Pavement 15" })!["id"]!;
        Assert.Equal(type, f.Model.RelatingOf(solid, IfcClass.RelDefinesByType)!.Id);
        var dir = Directory.CreateTempSubdirectory("lapin-studio").FullName;
        try
        {
            var path = Path.Combine(dir, "p.ifc");
            IfcExport.Write(f.Session, path);
            var text = File.ReadAllText(path);
            Assert.Contains("IFCSLAB(", text);
            // PAVING is IFC4X3's: in IFC4 it is written as a user-defined type named so.
            Assert.Contains(".USERDEFINED.", text);
            Assert.Contains("'PAVING'", text);
            Assert.Contains("IFCSLABTYPE(", text);
            Assert.Contains("'Pavement 15'", text);
        }
        finally { Directory.Delete(dir, true); }
        f.Run("studio.setClass", new JsonObject { ["id"] = solid, ["class"] = "COVERING" });
        Assert.Equal("IfcCovering", f.Model.Get(solid)!.Class);
        Assert.Null(f.Model.Get(solid)!.PredefinedType);
        Assert.Equal(type, f.Model.RelatingOf(solid, IfcClass.RelDefinesByType)!.Id);
        f.Run("studio.setType", new JsonObject { ["ids"] = new JsonArray(solid) });
        Assert.Null(f.Model.RelatingOf(solid, IfcClass.RelDefinesByType));
        Assert.Throws<CommandException>(() => f.Run("studio.setClass", new JsonObject { ["id"] = solid, ["predefinedType"] = "not ok" }));
    }

    [Fact]
    public void A_swept_member_measures_its_length_along_the_path_and_its_section()
    {
        var f = F();
        var plan = Plan(f);
        var profile = Draw(f, plan, Rect(20, 0, 20.1, 0.2));
        var path = Draw(f, plan, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(3, 0), J(3, 4)), ["closed"] = false });
        var id = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = profile, ["pathId"] = path, ["class"] = "MEMBER" })!["id"]!;
        Assert.Equal("Qto_MemberBaseQuantities", f.Session.Quantities(id).Single().Name);
        Assert.Equal(7, Q(f, id).Single(q => q.Name == "Length").Value, 9);
        Assert.Equal(0.02, Q(f, id).Single(q => q.Name == "CrossSectionArea").Value, 9);
        var classes = (JsonArray)f.Session.Query("studio.classes", [])!;
        Assert.Contains(classes, c => (string)c!["class"]! == "IfcSlab" && c["predefined"]!.AsArray().Any(p => (string)p! == "PAVING"));
    }
}
