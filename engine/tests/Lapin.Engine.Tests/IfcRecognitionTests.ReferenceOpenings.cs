using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// The openings of an element kept as a reference (docs/spec-ifc.md, Golurile referințelor): the import cuts them into its
/// stored body and keeps neither the IfcOpeningElement nor the IfcRelVoidsElement, and its door comes as a reference of its
/// own with no IfcRelFillsElement. What an IFC export and a second import then make of it, as it is today.
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
    public void A_references_openings_leave_only_in_its_cut_body_and_are_not_cut_twice()
    {
        var wallId = Guid("foreign-test", "ref-wall");
        var doorId = Guid("foreign-test", "ref-door");
        var f = Full();
        IfcImport.Run(f.Session, WallWithDoor(), new IfcImportOptions("reference"), out _);
        var m = f.Model;
        // On import: the body cut where the opening is, the opening itself and both relationships gone; the door a reference beside it.
        Assert.True(StoredBodies.Has(m.Get(wallId)!));
        Assert.Equal(Whole - Door, VolumeOf(f, wallId), 6);
        Assert.Empty(m.OfClass(IfcClass.OpeningElement));
        Assert.DoesNotContain(m.Relations, r => r.Class is IfcClass.RelVoidsElement or IfcClass.RelFillsElement);
        Assert.True(StoredBodies.Has(m.Get(doorId)!));

        // The export writes the cut body only: no IfcOpeningElement, no IfcRelVoidsElement, no IfcRelFillsElement; the door stands alone.
        var exported = Path.Combine(_dir, "wall-door-out.ifc");
        IfcExport.Write(f.Session, exported);
        var step = StepFile.Read(File.ReadAllBytes(exported));
        Assert.Contains(step.OfType("IFCWALL"), e => (string?)e[0] == wallId);
        Assert.Contains(step.OfType("IFCDOOR"), e => (string?)e[0] == doorId);
        Assert.Empty(step.OfType("IFCOPENINGELEMENT"));
        Assert.Empty(step.OfType("IFCRELVOIDSELEMENT"));
        Assert.Empty(step.OfType("IFCRELFILLSELEMENT"));

        // Read again as references: the same cut body, not cut twice (there is no opening left to cut it), and still no opening.
        var g = Full();
        IfcImport.Run(g.Session, exported, new IfcImportOptions("reference"), out _);
        Assert.Equal(Whole - Door, VolumeOf(g, wallId), 4);
        Assert.Empty(g.Model.OfClass(IfcClass.OpeningElement));

        // Read again recognising: the cut body is no prism, so the file's representation gives no wall; recognition from its
        // triangles (decision 214) finds the hole and the door standing in it, and makes the wall with its door again.
        var h = Full();
        var r = IfcImport.Run(h.Session, exported, null, out _);
        output.WriteLine(string.Join("\n", r.Notes));
        var wall = h.Model.Get(wallId)!;
        output.WriteLine($"auto re-import: {wall.Class}, reference {StoredBodies.Has(wall)}, openings {h.Model.OfClass(IfcClass.OpeningElement).Count()}, door {h.Model.Get(doorId)?.Class}");
        Assert.Equal(IfcClass.Wall, wall.Class);
        Assert.False(StoredBodies.Has(wall));
        Assert.Single(h.Model.OfClass(IfcClass.OpeningElement));
        Assert.Equal(IfcClass.Door, h.Model.Get(doorId)!.Class);
        Assert.Equal(Whole - Door, VolumeOf(h, wallId), 3);
        Assert.True(r.Fidelity!.Checks.Single(c => c.Id == wallId).Pass);
    }

    [Fact(Skip = "Known loss (docs/spec-ifc.md, Golurile referințelor): a reference keeps only its body cut by its openings; the IfcOpeningElement, " +
        "its IfcRelVoidsElement and the door's IfcRelFillsElement are not kept, so the export cannot write them back. A fix keeps the uncut body and the openings.")]
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
