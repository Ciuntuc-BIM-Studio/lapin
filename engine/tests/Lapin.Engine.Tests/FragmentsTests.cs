using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// That Open Fragments (docs/fragments.md): the writer keeps the conventions
/// read off the files the official importer makes, the reader is its mirror,
/// and a file imports as references on their storeys, identical bodies shared.
/// The decisive check is the official library reading the file; it runs
/// outside (scripts/fragments-check.mjs); here the Python reader of the
/// BubbleBIM converter can be run as an oracle (LAPIN_FRAG_PYTHON, LAPIN_FRAG_PACKAGE).
/// </summary>
public class FragmentsTests
{
    static Fixture House()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        f.Wall(0, 0, 6, 0);
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        foreach (var x in new[] { 1.0, 3.0, 5.0 })
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = 2 });
        return f;
    }

    static MeshScene Scene(Fixture f) => MeshScene.Build(f.Session, new MeshExportOptions("frag", Origin: "internal"));

    [Fact]
    public void The_file_keeps_the_conventions_of_the_reference_files()
    {
        var f = House();
        var scene = Scene(f);
        var m = FragReader.Read(FragWriter.Write(scene, raw: true, modelGuid: "test"));
        Assert.Equal("test", m.Guid);
        var meshes = m.Meshes;
        // Items with geometry first: meshes_items is the identity, and the four elements lead the categories.
        Assert.Equal(4, meshes.MeshesItems.Length);
        Assert.Equal([0u, 1u, 2u, 3u], meshes.MeshesItems);
        Assert.Equal("IFCWALL", m.Categories[0]);
        Assert.Equal("IFCCOLUMN", m.Categories[1]);
        Assert.Contains("IFCBUILDINGSTOREY", m.Categories);
        Assert.Contains("IFCPROJECT", m.Categories);
        // The three columns are one shell; the wall one per layer. A sample per element and material.
        var wallParts = scene.Groups.SelectMany(g => g.Elements).Single(e => e.IfcClass == IfcClass.Wall).Parts.Count;
        Assert.Equal(wallParts + 1, meshes.Shells.Length);
        Assert.Equal(wallParts + 3, meshes.Samples.Length);
        Assert.Single(meshes.LocalTransforms);
        Assert.Equal(4, meshes.GlobalTransforms.Length);
        // Geometry near the origin: the wall's transform holds its centre (Y up: x, z, −y), its points lie about zero.
        var wallId = f.Model.OfClass(IfcClass.Wall).Single().Id;
        var (lo, hi) = f.Bounds(wallId);
        var g = meshes.GlobalTransforms[0];
        Assert.Equal((lo.X + hi.X) / 2, g.X, 3);
        Assert.Equal((lo.Z + hi.Z) / 2, g.Y, 3);
        Assert.Equal(-(lo.Y + hi.Y) / 2, g.Z, 3);
        var wallShell = meshes.Shells[(int)meshes.Representations[(int)meshes.Samples[0].Representation].Id];
        var xs = Enumerable.Range(0, wallShell.Points.Length / 3).Select(i => wallShell.Points[i * 3]).ToList();
        Assert.Equal(-xs.Min(), xs.Max(), 3);
        // Every profile is a triangle of its own shell's points, one face id per profile; the representation's box is the shell's.
        foreach (var s in meshes.Shells)
        {
            Assert.All(s.Profiles, p => { Assert.Equal(3, p.Length); Assert.All(p, k => Assert.InRange(k, 0, s.Points.Length / 3 - 1)); });
            Assert.Equal(s.Profiles.Length, s.FaceIds.Length);
            Assert.False(s.Big);
        }
        var rep = meshes.Representations[0];
        Assert.Equal(wallShell.Points.Where((_, i) => i % 3 == 0).Min(), rep.Min[0], 5);
        Assert.Equal(wallShell.Points.Where((_, i) => i % 3 == 2).Max(), rep.Max[2], 5);
        // Resource ids above every item's; the guids are the elements' and the storey's GlobalIds.
        var maxItem = m.LocalIds.Max();
        Assert.All(meshes.GlobalTransformIds!, id => Assert.True(id > maxItem));
        Assert.All(meshes.SampleIds!, id => Assert.True(id > maxItem));
        Assert.True(m.MaxLocalId > meshes.SampleIds!.Max());
        Assert.Contains(wallId, m.Guids);
        Assert.Contains(f.Storey, m.Guids);
        // Attributes as JSON triples, relations as JSON arrays, the storey containing its elements.
        var wallAttrs = m.AttributesOf(0);
        Assert.Equal(f.Model.Get(wallId)!.Name, (string?)wallAttrs["Name"]);
        Assert.StartsWith("[\"Name\",", m.Attributes![0][0]);
        var storeyLocal = m.LocalIds[Array.IndexOf(m.Categories, "IFCBUILDINGSTOREY")];
        Assert.Equal([storeyLocal], m.RelationsOf(m.LocalIds[0])["ContainedInStructure"]);
        Assert.Equal(4, m.RelationsOf(storeyLocal)["ContainsElements"].Count);
        // The spatial tree alternates category and entity nodes: IFCPROJECT → project → IFCSITE → site → IFCBUILDING → building → IFCBUILDINGSTOREY → storey → classes → elements.
        var root = m.SpatialStructure!;
        Assert.Equal("IFCPROJECT", root.Category);
        var project = root.Children.Single();
        Assert.NotNull(project.LocalId);
        Assert.Equal("IFCSITE", project.Children.Single().Category);
        var site = project.Children.Single().Children.Single();
        var building = site.Children.Single(c => c.Category == "IFCBUILDING").Children.Single();
        var storey = building.Children.Single().Children.Single();
        Assert.Equal(storeyLocal, storey.LocalId);
        Assert.Equal(["IFCCOLUMN", "IFCWALL"], storey.Children.Select(c => c.Category).Order());
        Assert.Equal(3, storey.Children.Single(c => c.Category == "IFCCOLUMN").Children.Length);
        var metadata = m.MetadataJson!;
        Assert.Equal("IFC4", (string?)metadata["schema"]);
    }

    [Fact]
    public void The_file_is_zlib_unless_raw_is_asked_for()
    {
        var scene = Scene(House());
        var packed = FragWriter.Write(scene, modelGuid: "t");
        var raw = FragWriter.Write(scene, raw: true, modelGuid: "t");
        Assert.Equal(0x78, packed[0]);
        Assert.NotEqual(0x78, raw[0]);
        Assert.True(packed.Length < raw.Length);
        Assert.Equal(FragReader.Read(raw).LocalIds, FragReader.Read(packed).LocalIds);
        Assert.Equal(raw.Length, FragReader.Decompress(packed).Length);
        Assert.Equal(raw, FragReader.Decompress(raw));
    }

    [Fact]
    public void Export_writes_frag_as_one_of_the_formats()
    {
        var f = House();
        var (bytes, result) = MeshExport.Write(f.Session, new MeshExportOptions("frag"));
        Assert.Equal("frag", result.Format);
        Assert.Equal(4, result.Elements);
        Assert.Equal(4, FragReader.Read(bytes).Meshes.MeshesItems.Length);
    }

    [Fact]
    public void A_file_imports_as_references_on_their_storeys_sharing_identical_bodies()
    {
        var f = House();
        var bytes = FragWriter.Write(Scene(f));
        var wallId = f.Model.OfClass(IfcClass.Wall).Single().Id;
        var (lo, hi) = f.Bounds(wallId);

        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        var r = FragImport.Run(g.Session, bytes, "house.frag", out var diff);
        Assert.Equal(4, r.Elements);
        Assert.Equal(1, r.Shared);
        Assert.Equal(0, r.Storeys);       // the storey has the same name as the project's
        Assert.Equal(0, r.Skipped);
        Assert.Equal(1, r.ByClass["IfcWall"]);
        Assert.Equal(3, r.ByClass["IfcColumn"]);
        // The wall keeps its id, its body and its place; the columns are instances of one shared body.
        var wall = g.Model.Get(wallId)!;
        Assert.Equal(IfcClass.Wall, wall.Class);
        Assert.True(StoredBodies.Has(wall));
        var (lo2, hi2) = g.Bounds(wallId);
        Assert.Equal(lo.X, lo2.X, 3); Assert.Equal(lo.Y, lo2.Y, 3); Assert.Equal(lo.Z, lo2.Z, 3);
        Assert.Equal(hi.X, hi2.X, 3); Assert.Equal(hi.Y, hi2.Y, 3); Assert.Equal(hi.Z, hi2.Z, 3);
        var columns = g.Model.OfClass(IfcClass.Column).ToList();
        Assert.Equal(3, columns.Count);
        Assert.All(columns, c => Assert.NotNull(StoredBodies.InstanceOf(c)));
        Assert.Single(g.Model.OfClass(StoredBodies.SharedClass));
        var xs = columns.Select(c => (g.Bounds(c.Id).Min.X + g.Bounds(c.Id).Max.X) / 2).Order().ToList();
        Assert.Equal(1, xs[0], 3); Assert.Equal(3, xs[1], 3); Assert.Equal(5, xs[2], 3);
        var (clo, chi) = g.Bounds(columns[0].Id);
        Assert.Equal(0.3, chi.X - clo.X, 3);
        Assert.Equal(0.3, chi.Y - clo.Y, 3);
        Assert.True(chi.Z - clo.Z > 2);
        // On the storey, with the file's attributes under IFC properties, an undo step of their own.
        Assert.Equal(g.Storey, Spatial.StoreyOf(g.Model, wallId)!.Id);
        Assert.Equal(wall.Name, (string?)wall.Attrs["properties"]!["Fragments"]!["Name"]);
        Assert.Contains(diff.Nodes, n => n.Id == wallId);
        // Imported again, the same file updates what it brought: no duplicates.
        FragImport.Run(g.Session, bytes, "house.frag", out _);
        Assert.Equal(3, g.Model.OfClass(IfcClass.Column).Count());
        Assert.Single(g.Model.OfClass(StoredBodies.SharedClass));
        g.Session.Undo();
        g.Session.Undo();
        Assert.Empty(g.Model.OfClass(IfcClass.Column));
    }

    [Fact]
    public void A_storey_the_project_lacks_is_made_at_the_files_elevation()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Attic", ["elevation"] = 6.0 });
        f.Wall(0, 0, 4, 0);
        var bytes = FragWriter.Write(Scene(f));
        var g = new Fixture(new ArchitectureModule());
        var r = FragImport.Run(g.Session, bytes, "attic.frag", out _);
        Assert.Equal(1, r.Storeys);
        var attic = Spatial.Storeys(g.Model).Single(s => s.Name == "Attic");
        Assert.Equal(6.0, attic.Num("elevation"), 6);
        var wall = g.Model.OfClass(IfcClass.Wall).Single();
        Assert.Equal(attic.Id, Spatial.StoreyOf(g.Model, wall.Id)!.Id);
        Assert.Equal(6.0, g.Bounds(wall.Id).Min.Z, 3);
    }

    /// <summary>A file exported from this very project, imported back: the Lapin elements it holds stay Lapin elements.</summary>
    [Fact]
    public void A_files_own_elements_are_never_replaced_by_references()
    {
        var f = House();
        var bytes = FragWriter.Write(Scene(f));
        var r = FragImport.Run(f.Session, bytes, "house.frag", out _);
        Assert.Equal(0, r.Elements);
        Assert.Equal(4, r.Skipped);
        Assert.Contains(r.Notes, n => n.Contains("left as they are"));
        var wall = f.Model.OfClass(IfcClass.Wall).Single();
        Assert.False(StoredBodies.Has(wall));
        Assert.Equal(3, f.Model.OfClass(IfcClass.Column).Count());
    }

    [Fact]
    public void Something_else_is_refused_with_a_reason()
    {
        var g = new Fixture(new ArchitectureModule());
        Assert.Throws<CommandException>(() => FragImport.Run(g.Session, "ISO-10303-21;"u8.ToArray(), "x.ifc", out _));
    }

    /// <summary>The BubbleBIM converter's Python reader on Lapin's file, when it is at hand: LAPIN_FRAG_PYTHON (the interpreter) and LAPIN_FRAG_PACKAGE (the folder holding ifc_convert).</summary>
    [Fact]
    public void The_python_reader_of_the_converter_reads_the_file_the_same()
    {
        var python = Environment.GetEnvironmentVariable("LAPIN_FRAG_PYTHON");
        var package = Environment.GetEnvironmentVariable("LAPIN_FRAG_PACKAGE");
        if (string.IsNullOrEmpty(python) || string.IsNullOrEmpty(package)) return;
        var scene = Scene(House());
        var path = Path.Combine(Path.GetTempPath(), $"lapin-{Guid.NewGuid():N}.frag");
        File.WriteAllBytes(path, FragWriter.Write(scene));
        var mine = FragReader.Read(File.ReadAllBytes(path));
        try
        {
            var script = "import sys; sys.path.insert(0, sys.argv[1]); from ifc_convert.frag_reader import read_frag; m = read_frag(sys.argv[2]); ms = m.meshes; "
                + "print(len(m.local_ids), len(ms.shells), len(ms.samples), len(ms.global_transforms), m.meshes_items_identity if hasattr(m, 'meshes_items_identity') else list(ms.meshes_items) == list(range(len(ms.meshes_items))), m.guid, len(m.attributes), len(m.relations), m.spatial_structure.category, m.spatial_structure.children[0].children[0].category, ms.representations[0]['min'], [p.tolist() for p in ms.shells[0].profiles[:2]])";
            var psi = new ProcessStartInfo(python) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add("-I"); psi.ArgumentList.Add("-c"); psi.ArgumentList.Add(script); psi.ArgumentList.Add(package); psi.ArgumentList.Add(path);
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            var errors = p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.True(p.ExitCode == 0, errors);
            var expected = $"{mine.LocalIds.Length} {mine.Meshes.Shells.Length} {mine.Meshes.Samples.Length} {mine.Meshes.GlobalTransforms.Length} True {mine.Guid} {mine.Attributes!.Length} {mine.Relations!.Length} IFCPROJECT IFCSITE";
            Assert.StartsWith(expected, output.Trim());
        }
        finally { File.Delete(path); }
    }
}
