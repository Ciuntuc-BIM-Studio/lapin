using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using System.Text.Json.Nodes;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// The openings of an element kept as a reference (docs/spec-ifc.md, Golurile referințelor): the import cuts them into the
/// body the views draw, and keeps beside it the body before the cut and the openings with their IfcRelVoidsElement and the
/// door's IfcRelFillsElement, which an IFC export writes back; a project kept before (its body cut, nothing else) as it was.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>A wall 5 m × 300 × 2.8 m with a door opening 900 × 2100 at 2 m along, filled by a door.</summary>
    string WallWithDoor()
    {
        var x = new Foreign();
        var wallPl = x.Place(0, 0, 0);
        var wall = x.Element("IFCWALL", "ref-wall", "W1", wallPl, x.Shape(x.Box(2500, 0, 0, 5000, 300, 2800)), E("STANDARD"));
        x.Material(wall, x.Brick);
        var op = x.Element("IFCOPENINGELEMENT", "ref-op", "Door opening", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(2000, 0, 0, 900, 500, 2100)), E("OPENING"));
        var door = x.Element("IFCDOOR", "ref-door", "D1", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(2000, 0, 0, 900, 100, 2100)), "2100.", "900.", E("DOOR"), E("SINGLE_SWING_RIGHT"), Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("ref-v"), Null, Null, Null, wall, op);
        x.F.Add("IFCRELFILLSELEMENT", x.G("ref-f"), Null, Null, Null, op, door);
        return x.Write(Path.Combine(_dir, "wall-door.ifc"));
    }

    static double VolumeOf(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    const double Whole = 5 * 0.3 * 2.8, Door = 0.9 * 0.3 * 2.1;

    [Fact]
    public void A_references_openings_are_kept_beside_its_cut_body_and_cut_once()
    {
        var wallId = Guid("foreign-test", "ref-wall");
        var doorId = Guid("foreign-test", "ref-door");
        var f = Full();
        IfcImport.Run(f.Session, WallWithDoor(), new IfcImportOptions("reference"), out _);
        var m = f.Model;
        var wall = m.Get(wallId)!;
        // On import: the body the views draw cut where the opening is; the body before the cut and the opening kept with the
        // reference, not as entities or relationships of the model; the door a reference beside it.
        Assert.True(StoredBodies.Has(wall));
        Assert.Equal(Whole - Door, VolumeOf(f, wallId), 6);
        Assert.Equal(Whole, f.Session.Geometry(wallId, new GeometryOptions(CutOpenings: false))!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
        Assert.Empty(m.OfClass(IfcClass.OpeningElement));
        Assert.DoesNotContain(m.Relations, r => r.Class is IfcClass.RelVoidsElement or IfcClass.RelFillsElement);
        var kept = Assert.Single(((JsonArray)wall.Attrs[StoredBodies.OpeningsKey]!).OfType<JsonObject>());
        Assert.Equal(Guid("foreign-test", "ref-op"), (string?)kept["id"]);
        Assert.Equal(Guid("foreign-test", "ref-v"), (string?)kept["rel"]);
        Assert.Equal(doorId, (string?)kept["fills"]![0]!["id"]);
        Assert.Equal(Guid("foreign-test", "ref-f"), (string?)kept["fills"]![0]!["rel"]);
        Assert.True(StoredBodies.Has(m.Get(doorId)!));

        // The export writes the uncut body with its opening voiding it, and the door filling the opening, under the file's GlobalIds.
        var exported = Path.Combine(_dir, "wall-door-out.ifc");
        IfcExport.Write(f.Session, exported);
        var step = StepFile.Read(File.ReadAllBytes(exported));
        var outWall = step.OfType("IFCWALL").Single(e => (string?)e[0] == wallId);
        var outDoor = step.OfType("IFCDOOR").Single(e => (string?)e[0] == doorId);
        var opening = step.OfType("IFCOPENINGELEMENT").Single();
        Assert.Equal(Guid("foreign-test", "ref-op"), (string?)opening[0]);
        Assert.Equal("Door opening", (string?)opening[2]);
        var voids = step.OfType("IFCRELVOIDSELEMENT").Single();
        Assert.Equal(Guid("foreign-test", "ref-v"), (string?)voids[0]);
        Assert.Equal(outWall.Id, ((StepRef)voids[4]!).Id);
        Assert.Equal(opening.Id, ((StepRef)voids[5]!).Id);
        var fills = step.OfType("IFCRELFILLSELEMENT").Single();
        Assert.Equal(Guid("foreign-test", "ref-f"), (string?)fills[0]);
        Assert.Equal(opening.Id, ((StepRef)fills[4]!).Id);
        Assert.Equal(outDoor.Id, ((StepRef)fills[5]!).Id);

        // Read again as references: the uncut body cut once by the opening, which is kept again.
        var g = Full();
        IfcImport.Run(g.Session, exported, new IfcImportOptions("reference"), out _);
        Assert.Equal(Whole - Door, VolumeOf(g, wallId), 4);
        Assert.Empty(g.Model.OfClass(IfcClass.OpeningElement));
        Assert.Single((JsonArray)g.Model.Get(wallId)!.Attrs[StoredBodies.OpeningsKey]!);

        // Read again recognising: the wall made native with its opening and its door.
        var h = Full();
        var r = IfcImport.Run(h.Session, exported, null, out _);
        output.WriteLine(string.Join("\n", r.Notes));
        var native = h.Model.Get(wallId)!;
        output.WriteLine($"auto re-import: {native.Class}, reference {StoredBodies.Has(native)}, openings {h.Model.OfClass(IfcClass.OpeningElement).Count()}, door {h.Model.Get(doorId)?.Class}");
        Assert.Equal(IfcClass.Wall, native.Class);
        Assert.False(StoredBodies.Has(native));
        Assert.Single(h.Model.OfClass(IfcClass.OpeningElement));
        Assert.Equal(IfcClass.Door, h.Model.Get(doorId)!.Class);
        Assert.Equal(Whole - Door, VolumeOf(h, wallId), 3);
        Assert.True(r.Fidelity!.Checks.Single(c => c.Id == wallId).Pass);
    }

    /// <summary>A reference as a project kept before its openings were (its body cut, nothing else): what it was before.</summary>
    static void AsKeptBefore(Fixture f, string id) => f.Session.Edit("as before", e =>
    {
        var x = e.Model.Get(id)!;
        var a = x.CloneAttrs();
        foreach (var k in StoredBodies.ExportOnly) a.Remove(k);
        e.Replace(x.With(attrs: a));
    });

    /// <summary>
    /// A project saved before the openings were kept: its reference's body is the cut one and nothing more. It opens as it
    /// was, draws the same, exports its cut body alone as before, and a second import cuts nothing twice.
    /// </summary>
    [Fact]
    public void A_project_kept_before_exports_its_cut_body_as_before_and_is_not_cut_twice()
    {
        var wallId = Guid("foreign-test", "ref-wall");
        var f = Full();
        IfcImport.Run(f.Session, WallWithDoor(), new IfcImportOptions("reference"), out _);
        AsKeptBefore(f, wallId);
        var path = Path.Combine(_dir, "before.lapin");
        Lapin.Host.PackageFiles.Create().Save(f.Session, path);
        var g = Full();
        Lapin.Host.PackageFiles.Create().Open(g.Session, path);
        Assert.Null(g.Model.Get(wallId)!.Attrs[StoredBodies.UncutKey]);
        Assert.Equal(Whole - Door, VolumeOf(g, wallId), 6);
        // The uncut geometry asked for has nothing else to give: the cut body.
        Assert.Equal(Whole - Door, g.Session.Geometry(wallId, new GeometryOptions(CutOpenings: false))!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);

        var exported = Path.Combine(_dir, "before-out.ifc");
        IfcExport.Write(g.Session, exported);
        var step = StepFile.Read(File.ReadAllBytes(exported));
        Assert.Empty(step.OfType("IFCOPENINGELEMENT"));
        Assert.Empty(step.OfType("IFCRELVOIDSELEMENT"));
        Assert.Empty(step.OfType("IFCRELFILLSELEMENT"));
        var h = Full();
        IfcImport.Run(h.Session, exported, new IfcImportOptions("reference"), out _);
        Assert.Equal(Whole - Door, VolumeOf(h, wallId), 4);
        Assert.Null(h.Model.Get(wallId)!.Attrs[StoredBodies.OpeningsKey]);

        // A project saved with them keeps them through the package.
        var k = Full();
        IfcImport.Run(k.Session, WallWithDoor(), new IfcImportOptions("reference"), out _);
        var kept = Path.Combine(_dir, "kept.lapin");
        Lapin.Host.PackageFiles.Create().Save(k.Session, kept);
        var l = Full();
        Lapin.Host.PackageFiles.Create().Open(l.Session, kept);
        Assert.Single((JsonArray)l.Model.Get(wallId)!.Attrs[StoredBodies.OpeningsKey]!);
        Assert.Equal(Whole, l.Session.Geometry(wallId, new GeometryOptions(CutOpenings: false))!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
    }

    /// <summary>
    /// What the views draw and the quantities measure is the same with the openings kept as without: a plan, a section
    /// through the door, the 3D render, the volumes and the quantity sets.
    /// </summary>
    [Fact]
    public void Keeping_a_references_openings_changes_no_drawing_render_or_quantity()
    {
        var wallId = Guid("foreign-test", "ref-wall");
        var doorId = Guid("foreign-test", "ref-door");
        var file = WallWithDoor();
        string Seen(Fixture f)
        {
            var storey = Spatial.Storeys(f.Model).First().Id;
            var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey, ["name"] = "Plan" })!["id"]!;
            var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = "S", ["start"] = new JsonArray(2.0, -2), ["end"] = new JsonArray(2.0, 2) })!["id"]!;
            JsonNode Drawing(string view) => f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
            var render = f.Session.Snapshot().Render.Where(r => r.NodeId == wallId || r.NodeId == doorId).OrderBy(r => r.NodeId)
                .Select(r => System.Text.Json.JsonSerializer.Serialize(r));
            var quantities = new[] { wallId, doorId }.SelectMany(id => f.Session.Quantities(id).SelectMany(q => q.Quantities.Select(x => $"{q.Name}.{x.Name}={Math.Round(x.Value, 9)}")));
            // The ids made new in each project (the views, their marks) aside; the wall's and the door's kept.
            var text = string.Join("\n", [Drawing(plan).ToJsonString(), Drawing(section).ToJsonString(), .. render, .. quantities, $"{VolumeOf(f, wallId):0.000000000}"]);
            var fromFile = f.Model.Entities.Where(e => e.Attrs["reference"] != null).Select(e => e.Id).ToHashSet();
            return System.Text.RegularExpressions.Regex.Replace(text, "(?<=\")[0-9A-Za-z_$]{22}(?=\")", x => fromFile.Contains(x.Value) ? x.Value : "new");
        }
        var now = Full();
        IfcImport.Run(now.Session, file, new IfcImportOptions("reference"), out _);
        var before = Full();
        IfcImport.Run(before.Session, file, new IfcImportOptions("reference"), out _);
        AsKeptBefore(before, wallId);
        Assert.NotNull(now.Model.Get(wallId)!.Attrs[StoredBodies.UncutKey]);
        var a = Seen(now);
        var b = Seen(before);
        Assert.Contains("\"segments\"", a);
        Assert.Equal(b, a);
    }

    /// <summary>
    /// The kept openings are no elements: nothing lists or selects them, the interface never gets the bodies, and making the
    /// reference a Studio solid or a native wall reads its cut body as before (the solid keeps none of them).
    /// </summary>
    [Fact]
    public void A_references_kept_openings_stay_out_of_the_model_the_interface_and_conversions()
    {
        var wallId = Guid("foreign-test", "ref-wall");
        var doorId = Guid("foreign-test", "ref-door");
        var file = WallWithDoor();
        var f = Full();
        IfcImport.Run(f.Session, file, new IfcImportOptions("reference"), out _);
        // No entity, no relationship: every list, schedule, plan or check walks the model's entities and relationships.
        Assert.DoesNotContain(f.Model.Entities, e => e.Id == Guid("foreign-test", "ref-op"));
        Assert.Empty(f.Model.OfClass(IfcClass.OpeningElement));
        Assert.DoesNotContain(f.Model.Relations, r => r.Class is IfcClass.RelVoidsElement or IfcClass.RelFillsElement);
        // Nor sent to the interface as props.
        var node = f.Session.Snapshot().Nodes.Single(n => n.Id == wallId);
        foreach (var k in StoredBodies.ExportOnly) Assert.False(node.Props.ContainsKey(k), k);

        // Made a Studio solid of its triangles: the cut body, without the uncut one or the openings.
        f.Session.Edit("Make editable", edit => EditableReferences.Make(edit, f.Session.Catalog, [wallId]));
        var solid = f.Model.Get(wallId)!;
        Assert.False(StoredBodies.Has(solid));
        foreach (var k in StoredBodies.ExportOnly) Assert.Null(solid.Attrs[k]);
        Assert.Equal(Whole - Door, VolumeOf(f, wallId), 4);

        // Converted to native from its triangles (Modify → Convert to native): its cut body read, the wall made with its door.
        var g = Full();
        IfcImport.Run(g.Session, file, new IfcImportOptions("reference"), out _);
        MeshRecognitionReport? report = null;
        g.Session.Edit("Recognise", edit => report = MeshRecognition.Recognize(edit, g.Session.Catalog, [wallId, doorId]));
        Assert.Equal(IfcClass.Wall, g.Model.Get(wallId)!.Class);
        Assert.Single(g.Model.OfClass(IfcClass.OpeningElement));
        Assert.Equal(IfcClass.Door, g.Model.Get(doorId)!.Class);
        Assert.Equal(Whole - Door, VolumeOf(g, wallId), 3);
    }

    [Fact]
    public void A_references_openings_and_fills_survive_an_IFC_round_trip()
    {
        var f = Full();
        IfcImport.Run(f.Session, WallWithDoor(), new IfcImportOptions("reference"), out _);
        var exported = Path.Combine(_dir, "wall-door-kept.ifc");
        IfcExport.Write(f.Session, exported);
        var step = StepFile.Read(File.ReadAllBytes(exported));
        var wall = step.OfType("IFCWALL").Single(e => (string?)e[0] == Guid("foreign-test", "ref-wall"));
        var voids = Assert.Single(step.OfType("IFCRELVOIDSELEMENT"), r => r[4] is StepRef w && w.Id == wall.Id);
        var opening = step.Get(voids[5])!;
        Assert.Equal(Guid("foreign-test", "ref-op"), (string?)opening[0]);
        Assert.Single(step.OfType("IFCRELFILLSELEMENT"), r => r[4] is StepRef o && o.Id == opening.Id);
        // And its body uncut, as the file gave it: the opening cuts it, once, wherever it is read.
        var g = Full();
        IfcImport.Run(g.Session, exported, new IfcImportOptions("reference"), out _);
        Assert.Equal(Whole - Door, VolumeOf(g, Guid("foreign-test", "ref-wall")), 6);
    }
}
