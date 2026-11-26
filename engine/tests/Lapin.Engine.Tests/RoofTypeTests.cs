using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Roof types (issue #151, a CYPECAD MEP tester): an IfcRoofType with type parameters (Pset_RoofCommon, the user's own sets) and,
/// made from a roof, its build-up, which every roof of the type takes (its shape its own); a roof typed by one or by none, in one
/// step; the type saved again from a roof puts itself on again; IFC writes it as IfcRoofType with its sets, related by
/// IfcRelDefinesByType, and reads it back. The project's roof styles are untouched.
/// </summary>
public sealed class RoofTypeTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-roof-types").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray House10x6 => new(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6));

    static string AddRoof(Fixture f, string style, JsonObject? extra = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addRoof", a)!["id"]!;
    }

    static JsonArray Params() => new(
        new JsonObject { ["pset"] = "Pset_RoofCommon", ["name"] = "IsExternal", ["value"] = true },
        new JsonObject { ["pset"] = "Acme_Codes", ["name"] = "Code", ["kind"] = "text", ["value"] = "R-01" });

    [Fact]
    public void A_roof_type_made_from_a_roof_holds_its_build_up_and_parameters_and_types_it_in_one_step()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "ro_saxon");
        var type = (string)f.Run("architecture.createRoofType", new JsonObject { ["name"] = "Clay tile on rafters", ["roofId"] = roof, ["params"] = Params() })!["id"]!;
        var t = f.Model.Get(type)!;
        Assert.Equal(RoofTypes.Class, t.Class);
        Assert.Equal("Clay tile on rafters", t.Name);
        Assert.Equal(type, f.Model.RelatingOf(roof, IfcClass.RelDefinesByType)!.Id);
        var buildUp = (JsonObject)t.Attrs[RoofTypes.BuildUpKey]!;
        Assert.Equal("Clay tile", (string?)buildUp["covering"]);
        Assert.Equal("rafters", (string?)buildUp["structure"]);
        // The shape is the roof's own.
        Assert.Null(buildUp["shape"]);
        Assert.Null(buildUp["pitch"]);
        Assert.Equal(2, ((JsonArray)t.Attrs["params"]!).Count);
        // One undo step: the type and the roof typed by it go together.
        f.Session.Undo();
        Assert.Null(f.Model.Get(type));
        Assert.Null(f.Model.RelatingOf(roof, IfcClass.RelDefinesByType));
    }

    [Fact]
    public void A_roof_given_the_type_takes_its_build_up_and_keeps_its_shape_and_saved_again_every_roof_takes_it_again()
    {
        var f = new Fixture();
        var source = AddRoof(f, "ro_saxon");
        var type = (string)f.Run("architecture.createRoofType", new JsonObject { ["name"] = "Clay tile", ["roofId"] = source })!["id"]!;
        var hip = AddRoof(f, "traditional_basarabia");
        f.Run("architecture.setRoofType", new JsonObject { ["ids"] = new JsonArray(hip), ["typeId"] = type });
        var h = f.Model.Get(hip)!;
        Assert.Equal("Clay tile", h.Str("covering"));
        Assert.Equal("hip", h.Str("shape"));
        Assert.Equal(32, h.Num("pitch"), 6);

        // The type saved again from a roof of another covering: both roofs take it.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = source, ["attrs"] = new JsonObject { ["covering"] = "Slate" } });
        f.Run("architecture.updateRoofType", new JsonObject { ["id"] = type, ["roofId"] = source, ["name"] = "Slate" });
        Assert.Equal("Slate", f.Model.Get(type)!.Name);
        Assert.Equal("Slate", f.Model.Get(hip)!.Str("covering"));
        Assert.Equal("hip", f.Model.Get(hip)!.Str("shape"));

        // No type: the roof keeps how it is.
        f.Run("architecture.setRoofType", new JsonObject { ["ids"] = new JsonArray(hip), ["typeId"] = null });
        Assert.Null(f.Model.RelatingOf(hip, IfcClass.RelDefinesByType));
        Assert.Equal("Slate", f.Model.Get(hip)!.Str("covering"));
    }

    [Fact]
    public void A_type_of_parameters_only_leaves_the_roof_as_it_is_and_a_roof_can_be_drawn_with_a_type()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createRoofType", new JsonObject { ["params"] = Params() })!["id"]!;
        Assert.Equal("Roof type 1", f.Model.Get(type)!.Name);
        var roof = AddRoof(f, "traditional_basarabia");
        var before = f.Model.Get(roof)!.Attrs.ToJsonString();
        f.Run("architecture.setRoofType", new JsonObject { ["ids"] = new JsonArray(roof), ["typeId"] = type });
        Assert.Equal(before, f.Model.Get(roof)!.Attrs.ToJsonString());
        var drawn = AddRoof(f, "ro_saxon", new JsonObject { ["typeId"] = type });
        Assert.Equal(type, f.Model.RelatingOf(drawn, IfcClass.RelDefinesByType)!.Id);
        // The project's roof styles are not types.
        Assert.Null(Spatial.Project(f.Model)!.Attrs[ProjectRoofStyles.Key]);
    }

    [Fact]
    public void IFC_writes_the_roof_type_with_its_property_sets_and_reads_it_back()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "ro_saxon");
        var type = (string)f.Run("architecture.createRoofType", new JsonObject { ["name"] = "Clay tile", ["roofId"] = roof, ["params"] = Params() })!["id"]!;
        var path = Path.Combine(_dir, "types.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(path);
        var rt = Assert.Single(file.OfType("IFCROOFTYPE"));
        Assert.Equal("Clay tile", (string?)rt[2]);
        var sets = ((List<object?>)rt[5]!).Select(r => file.Get(r)!).ToList();
        var common = sets.Single(s => (string?)s[2] == "Pset_RoofCommon");
        Assert.Contains(((List<object?>)common[4]!).Select(x => file.Get(x)!), p => (string?)p[0] == "IsExternal" && p[2] is StepTyped { Type: "IFCBOOLEAN" });
        Assert.Contains(sets, s => (string?)s[2] == "Acme_Codes");
        var ifcRoof = file.OfType("IFCROOF").Single();
        Assert.Contains(file.OfType("IFCRELDEFINESBYTYPE"), r => r[5] is StepRef to && to.Id == rt.Id && r[4] is List<object?> l && l.OfType<StepRef>().Any(x => x.Id == ifcRoof.Id));

        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        var back = Assert.Single(g.Model.OfClass(RoofTypes.Class));
        Assert.Equal(type, back.Id);
        Assert.Equal("Clay tile", back.Name);
        Assert.Equal(2, ((JsonArray)back.Attrs["params"]!).Count);
        Assert.Equal("Clay tile", (string?)back.Attrs[RoofTypes.BuildUpKey]?["covering"]);
        Assert.Equal(type, g.Model.RelatingOf(roof, IfcClass.RelDefinesByType)?.Id);
    }

    [Fact]
    public void A_roof_type_in_use_stays_as_any_type_and_goes_once_its_roofs_have_none()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "ro_saxon");
        var type = (string)f.Run("architecture.createRoofType", new JsonObject { ["roofId"] = roof })!["id"]!;
        var before = f.Model.Get(roof)!.Attrs.ToJsonString();
        Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(type) }));
        f.Run("architecture.setRoofType", new JsonObject { ["ids"] = new JsonArray(roof), ["typeId"] = null });
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(type) });
        Assert.Null(f.Model.Get(type));
        Assert.Equal(before, f.Model.Get(roof)!.Attrs.ToJsonString());
    }
}
