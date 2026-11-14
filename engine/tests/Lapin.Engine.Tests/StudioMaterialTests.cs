using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A material for Studio solids and their types (forum topic 60): chosen in Properties (by the project's materials; one
/// or several solids at once), a type's the default of its solids without one of their own, worked out on every ask; a placed
/// part's over all its bodies. Whatever reads an element's material sees it: the 3D colour, the cut's hatch and fill, the
/// quantities by material, the IFC export's association and the import back.
/// </summary>
public sealed class StudioMaterialTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = J(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };

    static string Solid(Fixture f, string plan, double x0, double y0, double x1, double y1, double depth = 3) =>
        (string)f.Run("studio.extrude", new JsonObject
        {
            ["profileId"] = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = Rect(x0, y0, x1, y1) })!["ids"]![0]!,
            ["depth"] = depth,
        })!["id"]!;

    static string Material(Fixture f, string name, string color) => (string)f.Run("material.create", new JsonObject { ["name"] = name, ["color"] = color })!["id"]!;
    static void SetMaterial(Fixture f, JsonNode? value, params string[] ids) =>
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["attrs"] = new JsonObject { ["material"] = value } });
    static GeometryPart Part(Fixture f, string id) => Assert.Single(f.Session.Geometry(id)!.Parts);

    static string Export(Fixture f)
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "materials.ifc", new IfcExportOptions());
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void Every_Studio_class_and_the_solid_type_offer_a_material_field()
    {
        var f = F();
        foreach (var cls in StudioModule.Classes.Append(StudioModule.TypeClass))
            Assert.Equal("materialRef", f.Session.Reader.Services is ModuleCatalog c ? c.Class(cls)!.Fields.Single(x => x.Key == "material").Kind : null);
    }

    [Fact]
    public void A_solids_material_is_chosen_for_several_at_once_by_id_or_name_and_colours_it()
    {
        var f = F();
        var plan = Plan(f);
        var (a, b) = (Solid(f, plan, 0, 0, 1, 1), Solid(f, plan, 2, 0, 3, 1));
        Assert.Null(Part(f, a).Material);
        var oak = Material(f, "Oak", "#aa7744");
        // By its name (as a script may give it): kept as the material's id.
        SetMaterial(f, "Oak", a, b);
        foreach (var id in new[] { a, b })
        {
            Assert.Equal(oak, f.Model.Get(id)!.Str("material"));
            Assert.Equal(oak, Part(f, id).Material);
            Assert.Equal("#aa7744", Part(f, id).Color);
        }
        Assert.Throws<CommandException>(() => SetMaterial(f, "No such material", a));
        // Cleared: back to none, the solid grey as before.
        SetMaterial(f, null, a);
        Assert.Null(f.Model.Get(a)!.Attrs["material"]);
        Assert.Null(Part(f, a).Material);
        Assert.Equal("#b9b2a6", Part(f, a).Color);
        // One undo step each.
        f.Session.Undo();
        Assert.Equal(oak, Part(f, a).Material);
    }

    [Fact]
    public void A_solid_without_a_material_takes_its_types_and_its_own_overrides_it()
    {
        var f = F();
        var plan = Plan(f);
        var (a, b) = (Solid(f, plan, 0, 0, 1, 1), Solid(f, plan, 2, 0, 3, 1));
        var type = (string)f.Run("studio.setType", new JsonObject { ["ids"] = J(a, b), ["name"] = "Block" })!["id"]!;
        var (concrete, steel) = (Material(f, "Concrete C25", "#a0a0a0"), Material(f, "Steel S235", "#606870"));
        SetMaterial(f, concrete, type);
        Assert.Equal(concrete, Part(f, a).Material);
        Assert.Equal(concrete, Part(f, b).Material);
        // Worked out on every ask, never copied onto the solids.
        Assert.Null(f.Model.Get(a)!.Attrs["material"]);
        SetMaterial(f, steel, b);
        Assert.Equal(steel, Part(f, b).Material);
        Assert.Equal("#606870", Part(f, b).Color);
        // The type's changed: those without their own follow.
        SetMaterial(f, steel, type);
        Assert.Equal(steel, Part(f, a).Material);
        // Read as the element's main material by the cut order and the cost (its type's when it has none).
        Assert.Equal(steel, CutOrder.MainMaterial(f.Model.Get(a)!, f.Model));
    }

    [Fact]
    public void The_cut_takes_its_materials_fill_instead_of_the_white()
    {
        var f = F();
        var plan = Plan(f);
        var solid = Solid(f, plan, 0, 0, 2, 2);
        Assert.Equal("#ffffff", Assert.Single(Views.Render(f.Session.Reader, f.Model.Get(plan)!).Pens, p => p.Layer == PenLayer.Fill).Fill);
        var mat = Material(f, "Red stone", "#cc3322");
        f.Run("material.update", new JsonObject { ["id"] = mat, ["section"] = new JsonObject { ["fill"] = "#ff0000" } });
        SetMaterial(f, mat, solid);
        var fills = Views.Render(f.Session.Reader, f.Model.Get(plan)!).Pens.Where(p => p.Layer == PenLayer.Fill).Select(p => p.Fill).ToList();
        Assert.Contains("#ff0000", fills);
        Assert.DoesNotContain("#ffffff", fills);
    }

    [Fact]
    public void The_IFC_export_associates_the_solids_material_and_its_types()
    {
        var f = F();
        var plan = Plan(f);
        var (a, b) = (Solid(f, plan, 0, 0, 1, 1), Solid(f, plan, 2, 0, 3, 1));
        var type = (string)f.Run("studio.setType", new JsonObject { ["ids"] = J(b), ["name"] = "Block" })!["id"]!;
        SetMaterial(f, Material(f, "Oak", "#aa7744"), a);
        SetMaterial(f, Material(f, "Granite", "#777777"), type);
        var ifc = Export(f);
        foreach (var name in new[] { "Oak", "Granite" })
        {
            var material = System.Text.RegularExpressions.Regex.Match(ifc, $@"(#\d+)=IFCMATERIAL\('{name}'");
            Assert.True(material.Success, name);
            Assert.Matches($@"IFCRELASSOCIATESMATERIAL\([^;]*{material.Groups[1].Value}\);", ifc);
        }
        // The type's material on the solid that has none of its own, and on the type.
        var granite = System.Text.RegularExpressions.Regex.Match(ifc, @"(#\d+)=IFCMATERIAL\('Granite'").Groups[1].Value;
        var rel = System.Text.RegularExpressions.Regex.Match(ifc, $@"IFCRELASSOCIATESMATERIAL\([^;]*\(([^)]*)\),{granite}\);").Groups[1].Value;
        Assert.Equal(2, rel.Split(',').Length);
    }

    // ------------------------------------------------------------ placed parts

    static JsonObject TwoBodies() => new()
    {
        ["params"] = J(new JsonObject { ["key"] = "W", ["default"] = 0.6 }),
        ["bodies"] = J(
            new JsonObject { ["name"] = "Top", ["plane"] = "xy", ["points"] = J(J(0, 0), J("W", 0), J("W", 0.4), J(0, 0.4)), ["depth"] = 0.03, ["material"] = "Oak" },
            new JsonObject { ["name"] = "Leg", ["plane"] = "xy", ["points"] = J(J(0, 0), J(0.05, 0), J(0.05, 0.05), J(0, 0.05)), ["depth"] = -0.7 }),
    };

    [Fact]
    public void A_placed_parts_material_takes_every_body_its_types_the_bodies_without_one()
    {
        var f = F();
        var oak = Material(f, "Oak", "#aa7744");
        var steel = Material(f, "Steel", "#606870");
        var part = (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Table", ["def"] = TwoBodies() })!["id"]!;
        string Place(double x) => (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(x, 0) })!["id"]!;
        var (a, b, c) = (Place(0), Place(2), Place(4));
        List<string?> Mats(string id) => [.. f.Session.Geometry(id)!.Parts.Select(p => p.Material)];
        Assert.Equal([oak, null], Mats(a));
        // The part's own material: the bodies without one.
        SetMaterial(f, steel, part);
        Assert.Equal([oak, steel], Mats(a));
        // A placed part's: all its bodies.
        SetMaterial(f, steel, b);
        Assert.Equal([steel, steel], Mats(b));
        Assert.Equal([oak, steel], Mats(c));
        // Those of one material still share one body; another material is another.
        var keys = new[] { a, b, c }.Select(id => f.Session.Geometry(id)!.Instance!.Body).ToList();
        Assert.Equal(keys[0], keys[2]);
        Assert.NotEqual(keys[0], keys[1]);
        // Lapin's IFC brings the placed part's material back.
        var g = F();
        IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(Export(f)), "parts.ifc", null, out _);
        Assert.Equal(steel, g.Model.Get(b)!.Str("material"));
        Assert.Equal(steel, Parts.TypeOf(g.Model, g.Model.Get(a)!)!.Str("material"));
        Assert.Equal([steel, steel], g.Session.Geometry(b)!.Parts.Select(p => p.Material).ToList());
    }
}
