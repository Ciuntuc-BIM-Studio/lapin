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

    /// <summary>
    /// A real file imported as references, timed (LAPIN_IFC_REFERENCES=/path/file.ifc; run in Release): the import, the
    /// memory after it, the references with kept openings and their doors, every reference moved and moved back, the
    /// export (its openings, voids and fills against the source's), and a second import of the export.
    /// </summary>
    [Fact]
    public void References_with_openings_on_a_sample_file_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_REFERENCES");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        void Lap(string what)
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess(); proc.Refresh();
            output.WriteLine($"{what,-58} {sw.Elapsed.TotalSeconds,7:0.000} s   managed {GC.GetTotalMemory(false) / 1048576.0,6:0} MB   working set {proc.WorkingSet64 / 1048576.0,6:0} MB");
            sw.Restart();
        }
        var source = StepFile.Read(File.ReadAllBytes(path));
        int Count(StepFile s, string t) => s.OfType(t).Count();
        output.WriteLine($"source: {Count(source, "IFCOPENINGELEMENT")} openings, {Count(source, "IFCRELVOIDSELEMENT")} voids, {Count(source, "IFCRELFILLSELEMENT")} fills");
        Fixture f = null!;
        for (var run = 0; run < 3; run++)
        {
            f = Full();
            GC.Collect();
            sw.Restart();
            IfcImport.Run(f.Session, path, new IfcImportOptions("reference"), out _);
            Lap($"import as references (run {run + 1})");
        }
        var refs = f.Model.Entities.Where(StoredBodies.Has).ToList();
        var hosts = refs.Where(e => e.Attrs[StoredBodies.OpeningsKey] is JsonArray).ToList();
        output.WriteLine($"{refs.Count} references, {hosts.Count} with kept openings, {hosts.Sum(h => ((JsonArray)h.Attrs[StoredBodies.OpeningsKey]!).Count)} openings, {hosts.Sum(h => ReferenceFills.Fillers(h).Count())} fills");
        var ids = new JsonArray([.. refs.Select(e => (JsonNode)e.Id)]);
        sw.Restart();
        f.Run("element.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "move", ["by"] = new JsonArray(10.0, 0) });
        Lap("every reference moved 10 m");
        f.Run("element.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "move", ["by"] = new JsonArray(-10.0, 0) });
        Lap("and back");
        var exported = Path.Combine(_dir, "references-out.ifc");
        IfcExport.Write(f.Session, exported);
        Lap("export");
        var outStep = StepFile.Read(File.ReadAllBytes(exported));
        output.WriteLine($"export: {Count(outStep, "IFCOPENINGELEMENT")} openings, {Count(outStep, "IFCRELVOIDSELEMENT")} voids, {Count(outStep, "IFCRELFILLSELEMENT")} fills");
        var g = Full();
        sw.Restart();
        IfcImport.Run(g.Session, exported, new IfcImportOptions("reference"), out _);
        Lap("export imported again as references");
        Assert.Equal(hosts.Count, g.Model.Entities.Count(e => e.Attrs[StoredBodies.OpeningsKey] is JsonArray));
    }

    static double MinX(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Select(p => Measure.Bounds(p.Solid)!.Value.Min.X).Min();

    static JsonObject KeptOpening(Fixture f, string wallId) => Assert.Single(((JsonArray)f.Model.Get(wallId)!.Attrs[StoredBodies.OpeningsKey]!).OfType<JsonObject>());

    /// <summary>
    /// A referenced wall moved, turned and copied takes its kept opening and the door filling it along, as a native wall
    /// takes its doors: the copy's opening has GlobalIds of its own and is filled by a copy of the door, both written to
    /// IFC once each; the door moved on its own leaves the opening (it no longer fills it).
    /// </summary>
    [Fact]
    public void A_references_openings_and_their_doors_go_with_it_when_it_moves_or_is_copied()
    {
        var wallId = Guid("foreign-test", "ref-wall");
        var doorId = Guid("foreign-test", "ref-door");
        var f = Full();
        IfcImport.Run(f.Session, WallWithDoor(), new IfcImportOptions("reference"), out _);
        var (wall0, door0) = (MinX(f, wallId), MinX(f, doorId));

        // Moved 10 m: the wall, its opening's body and the door together; the opening still cuts the same.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wallId), ["op"] = "move", ["by"] = new JsonArray(10.0, 0) });
        Assert.Equal(wall0 + 10, MinX(f, wallId), 6);
        Assert.Equal(door0 + 10, MinX(f, doorId), 6);
        Assert.Equal(Whole - Door, VolumeOf(f, wallId), 6);
        Assert.Equal(Whole, f.Session.Geometry(wallId, new GeometryOptions(CutOpenings: false))!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
        // Selected with its door, the door is carried once.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wallId, doorId), ["op"] = "move", ["by"] = new JsonArray(-10.0, 0) });
        Assert.Equal(wall0, MinX(f, wallId), 6);
        Assert.Equal(door0, MinX(f, doorId), 6);
        // Turned a half turn about the wall's middle: the door turns with it, to the other end (its middle 2 m from it, 0.9 m wide).
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wallId), ["op"] = "rotate", ["center"] = new JsonArray(2.5, 0.15), ["angle"] = 180.0 });
        Assert.Equal(5 - 2.0 - 0.45, MinX(f, doorId) - MinX(f, wallId), 3);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wallId), ["op"] = "rotate", ["center"] = new JsonArray(2.5, 0.15), ["angle"] = 180.0 });
        Assert.Equal(door0, MinX(f, doorId), 6);

        // Copied: the copy keeps the uncut body and the opening under new ids, filled by a copy of the door.
        var made = (JsonArray)f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wallId), ["op"] = "copy", ["by"] = new JsonArray(0.0, 5) })!["ids"]!;
        var copyId = (string)Assert.Single(made)!;
        var original = KeptOpening(f, wallId);
        var copied = KeptOpening(f, copyId);
        Assert.NotEqual((string?)original["id"], (string?)copied["id"]);
        Assert.Null(copied["rel"]);
        var doorCopy = (string)copied["fills"]![0]!["id"]!;
        Assert.NotEqual(doorId, doorCopy);
        Assert.True(StoredBodies.Has(f.Model.Get(doorCopy)!));
        Assert.Equal(doorId, (string?)original["fills"]![0]!["id"]);
        Assert.Equal(Whole - Door, VolumeOf(f, copyId), 6);
        Assert.Equal(door0, MinX(f, doorCopy), 6);
        var exported = Path.Combine(_dir, "copied.ifc");
        IfcExport.Write(f.Session, exported);
        var step = StepFile.Read(File.ReadAllBytes(exported));
        Assert.Equal(2, step.OfType("IFCOPENINGELEMENT").Count());
        Assert.Equal(2, step.OfType("IFCRELVOIDSELEMENT").Count());
        var fills = step.OfType("IFCRELFILLSELEMENT").ToList();
        Assert.Equal(2, fills.Count);
        Assert.Equal(2, fills.Select(r => ((StepRef)r[5]!).Id).Distinct().Count());
        var rooted = new[] { "IFCWALL", "IFCDOOR", "IFCOPENINGELEMENT", "IFCRELVOIDSELEMENT", "IFCRELFILLSELEMENT" }.SelectMany(step.OfType).Select(e => (string)e[0]!).ToList();
        Assert.Equal(rooted.Count, rooted.Distinct().Count());

        // The door moved on its own leaves the file's opening behind: it fills it no more, the hole stays.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(doorCopy), ["op"] = "move", ["by"] = new JsonArray(1.0, 0) });
        Assert.Equal(door0 + 1, MinX(f, doorCopy), 6);
        Assert.Empty((JsonArray)KeptOpening(f, copyId)["fills"]!);
        Assert.Equal(Whole - Door, VolumeOf(f, copyId), 6);
    }

    /// <summary>
    /// A referenced wall deleted takes the door in its opening with it, as a native wall does; a door deleted leaves the
    /// wall's opening empty (exported as an opening without a filling); one undo brings both back.
    /// </summary>
    [Fact]
    public void A_references_doors_go_with_it_when_it_is_deleted_and_a_door_deleted_leaves_the_opening_empty()
    {
        var wallId = Guid("foreign-test", "ref-wall");
        var doorId = Guid("foreign-test", "ref-door");
        var f = Full();
        IfcImport.Run(f.Session, WallWithDoor(), new IfcImportOptions("reference"), out _);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(wallId) });
        Assert.Null(f.Model.Get(wallId));
        Assert.Null(f.Model.Get(doorId));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(wallId));
        Assert.NotNull(f.Model.Get(doorId));

        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(doorId) });
        Assert.Null(f.Model.Get(doorId));
        Assert.Empty((JsonArray)KeptOpening(f, wallId)["fills"]!);
        Assert.Equal(Whole - Door, VolumeOf(f, wallId), 6);
        var exported = Path.Combine(_dir, "no-door.ifc");
        IfcExport.Write(f.Session, exported);
        var step = StepFile.Read(File.ReadAllBytes(exported));
        Assert.Single(step.OfType("IFCOPENINGELEMENT"));
        Assert.Single(step.OfType("IFCRELVOIDSELEMENT"));
        Assert.Empty(step.OfType("IFCRELFILLSELEMENT"));
    }

    /// <summary>
    /// The file of a referenced wall with a door, exported, imported and exported again (LAPIN_IFC_CYCLES times, 100 by
    /// default): every cycle the same opening, voids and fills under the same GlobalIds, and the same volumes, uncut and cut.
    /// </summary>
    [Fact]
    public void A_references_openings_hold_over_many_IFC_cycles()
    {
        var cycles = int.TryParse(Environment.GetEnvironmentVariable("LAPIN_IFC_CYCLES"), out var n) && n > 0 ? n : 100;
        var wallId = Guid("foreign-test", "ref-wall");
        var doorId = Guid("foreign-test", "ref-door");
        var file = WallWithDoor();
        string? first = null;
        double? cut = null, uncut = null;
        for (var i = 0; i < cycles; i++)
        {
            var f = Full();
            IfcImport.Run(f.Session, file, new IfcImportOptions("reference"), out _);
            var c = VolumeOf(f, wallId);
            var u = f.Session.Geometry(wallId, new GeometryOptions(CutOpenings: false))!.Parts.Sum(p => Measure.Volume(p.Solid));
            Assert.Equal(cut ??= c, c, 12);
            Assert.Equal(uncut ??= u, u, 12);
            var next = Path.Combine(_dir, $"cycle-{i % 2}.ifc");
            IfcExport.Write(f.Session, next);
            var step = StepFile.Read(File.ReadAllBytes(next));
            var opening = Assert.Single(step.OfType("IFCOPENINGELEMENT"));
            var voids = Assert.Single(step.OfType("IFCRELVOIDSELEMENT"));
            var fills = Assert.Single(step.OfType("IFCRELFILLSELEMENT"));
            var wall = step.Get(voids[4])!;
            var door = step.Get(fills[5])!;
            Assert.Equal(opening.Id, ((StepRef)voids[5]!).Id);
            Assert.Equal(opening.Id, ((StepRef)fills[4]!).Id);
            var said = string.Join("|", (string?)wall[0], wall.Type, (string?)opening[0], (string?)opening[2], (string?)voids[0], (string?)fills[0], (string?)door[0], door.Type);
            Assert.Equal(first ??= said, said);
            file = next;
        }
        Assert.Contains(wallId, first);
        Assert.Contains(doorId, first);
        Assert.Equal(Whole - Door, cut!.Value, 6);
        Assert.Equal(Whole, uncut!.Value, 6);
    }
}
