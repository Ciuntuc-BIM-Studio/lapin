using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde.Remarks;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>Openings as solid voids and MEP checked against them (a tester's request, decision 289).</summary>
public class OpeningVoidTests
{
    /// <summary>A wall along y = 1 from x = −2 to 8 (its centre at x = 0 is 2 m along it).</summary>
    static string Wall(MepFixture f) =>
        (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 1, ["x2"] = 8, ["y2"] = 1, ["join"] = false })!["id"]!;

    static double Thickness(MepFixture f, string wall)
    {
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        return bands.Max(b => b.To) - bands.Min(b => b.From);
    }

    static List<OpeningVoid> Voids(MepFixture f, double offset = 0, params string[] kinds) =>
        OpeningVoids.Of(f.Session.Reader, new VoidOptions(kinds.Length > 0 ? kinds.ToHashSet() : OpeningVoids.Kinds.ToHashSet(), offset));

    /// <summary>A straight heating pipe across the wall at x = 0, its axis 1.0 m up, of the diameter asked.</summary>
    static string Pipe(MepFixture f, double size)
    {
        var system = f.System();
        var run = f.Draw(system, MepFixture.Pts((0, -1), (0, 3)), more: new JsonObject { ["pair"] = false });
        Resize(f, run, size);
        return run;
    }

    static void Resize(MepFixture f, string run, double size) => f.Session.Edit("size", e =>
    {
        var x = e.Model.Get(run)!;
        var a = x.CloneAttrs();
        a["size"] = size;
        a["elevation"] = 1.0;
        e.Replace(x.With(attrs: a));
    });

    /// <summary>A pipe as an imported IFC reference: a vertical cylinder from its stored triangles.</summary>
    static string Riser(MepFixture f, double x, double y, double z0, double z1, double d)
    {
        var tube = Prism.Cylinder(new Vec2(x, y), z0, d / 2, z1 - z0, 24)!;
        var mesh = Tessellator.Tessellate(tube);
        var v = Enumerable.Range(0, mesh.Positions.Length / 3).Select(i => new Vec3(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2])).ToList();
        var t = Enumerable.Range(0, mesh.Indices.Length / 3).Select(i => (mesh.Indices[i * 3], mesh.Indices[i * 3 + 1], mesh.Indices[i * 3 + 2])).ToList();
        var id = GlobalId.New();
        f.Session.Edit("riser", e =>
        {
            e.Add(new Entity(id, "IfcPipeSegment", "Riser", new JsonObject { [StoredBodies.Key] = StoredBodies.Write([("Pipe", "#2e86de", v, t)]) }, "RIGIDSEGMENT"));
            e.AddRelation(new Relation(e.NewId(), IfcClass.RelContainedInSpatialStructure, f.Storey, [id]));
        });
        return id;
    }

    static string Opening(MepFixture f, string wall, double width, double height, double sill) =>
        f.Id("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["width"] = width, ["height"] = height, ["sill"] = sill });

    static List<OpeningClash> Check(MepFixture f, double clearance = 0, bool empty = false) =>
        OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options(clearance, empty));

    [Fact]
    public void Every_opening_has_a_void_of_its_size_through_the_whole_wall_or_floor()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var t = Thickness(f, wall);
        var bare = Opening(f, wall, 0.4, 0.3, 1.2);
        var door = f.Id("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 5 });
        var window = f.Id("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 8 });
        var slab = f.Id("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = MepFixture.Pts((-2, 2), (4, 2), (4, 6), (-2, 6)) });
        var hole = f.Id("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = MepFixture.Pts((0, 3), (0.6, 3), (0.6, 3.5), (0, 3.5)) });

        var voids = Voids(f);
        Assert.Equal(4, voids.Count);
        Assert.Equal(["V1", "V2", "V3", "V4"], voids.Select(v => v.Mark).Order().ToArray());

        var b = voids.Single(v => v.OpeningId == bare);
        Assert.Equal((OpeningVoids.Wall, OpeningVoids.Rectangle, wall), (b.Kind, b.Shape, b.HostId));
        Assert.Equal(0.4, b.Width, 6);
        Assert.Equal(0.3, b.Height, 6);
        Assert.Equal(t, b.Depth, 6);
        // Exactly through the wall: its box is the opening's, the wall's faces its ends.
        Assert.Equal(0.4 * 0.3 * t, Measure.Volume(b.Solid), 6);
        var box = Measure.Bounds(b.Solid)!.Value;
        Assert.Equal(1.2, box.Min.Z, 6);
        Assert.Equal(1.5, box.Max.Z, 6);
        Assert.Equal(t, box.Max.Y - box.Min.Y, 6);

        var d = voids.Single(v => v.FillId == door);
        Assert.Equal(OpeningVoids.Door, d.Kind);
        Assert.Equal(JoinerySpecs.Door.Width, d.Width, 6);
        Assert.Equal(JoinerySpecs.Door.Height, d.Height, 6);
        Assert.Equal(Openings.OpeningOf(f.Model, door)!.Id, d.OpeningId);
        Assert.Equal(OpeningVoids.Window, voids.Single(v => v.FillId == window).Kind);

        var s = voids.Single(v => v.OpeningId == hole);
        Assert.Equal((OpeningVoids.Slab, OpeningVoids.Rectangle, slab), (s.Kind, s.Shape, s.HostId));
        var section = Slabs.Section(f.Model, f.Model.Get(slab)!);
        var depth = section.Max(x => x.To) - section.Min(x => x.From);
        Assert.Equal(depth, s.Depth, 6);
        Assert.Equal(0.6 * 0.5 * depth, Measure.Volume(s.Solid), 6);

        // An offset grows every side; the filter keeps the kinds asked.
        var grown = Voids(f, 0.05).Single(v => v.OpeningId == bare);
        Assert.Equal(0.5, grown.Width, 6);
        Assert.Equal(0.4, grown.Height, 6);
        Assert.Equal(0.5 * 0.4 * t, Measure.Volume(grown.Solid), 6);
        Assert.Equal(0.7 * 0.6 * depth, Measure.Volume(Voids(f, 0.05, OpeningVoids.Slab).Single().Solid), 6);
        Assert.Equal([OpeningVoids.Door, OpeningVoids.Window], Voids(f, 0, OpeningVoids.Door, OpeningVoids.Window).Select(v => v.Kind).Order().ToArray());

        // The query the 3D view draws them from: each with its mark, sizes and triangles.
        var q = (JsonObject)f.Session.Query("coordination.voids", new JsonObject { ["mesh"] = true })!;
        Assert.Equal(4, (int)q["count"]!);
        var first = (JsonObject)((JsonArray)q["voids"]!)[0]!;
        Assert.True(((JsonArray)first["positions"]!).Count >= 24);
        Assert.NotNull(first["selectId"]);
    }

    [Fact]
    public void The_ifc_export_writes_voids_as_provisions_for_voids_only_when_asked()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        Opening(f, wall, 0.4, 0.3, 1.2);
        f.Id("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 5 });

        string Export(IfcExportOptions o)
        {
            using var ms = new MemoryStream();
            IfcExport.Write(f.Session, ms, "t.ifc", o);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        var plain = Export(new IfcExportOptions());
        Assert.DoesNotContain("PROVISIONFORVOID", plain);
        Assert.DoesNotContain("Pset_ProvisionForVoid", plain);
        Assert.Equal(2, plain.Split("IFCOPENINGELEMENT(").Length - 1);

        var ifc = Export(new IfcExportOptions(Voids: true));
        // One proxy per opening, the openings unchanged beside them.
        Assert.Equal(2, ifc.Split("IFCBUILDINGELEMENTPROXY(").Length - 1);
        Assert.Equal(2, ifc.Split(".PROVISIONFORVOID.").Length - 1);
        Assert.Equal(2, ifc.Split("IFCOPENINGELEMENT(").Length - 1);
        Assert.Equal(2, ifc.Split("'Pset_ProvisionForVoid'").Length - 1);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Shape',$,IFCLABEL('Rectangle')", ifc);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Width',$,IFCPOSITIVELENGTHMEASURE(0.4)", ifc);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Height',$,IFCPOSITIVELENGTHMEASURE(0.3)", ifc);
        Assert.Contains("'Lapin_OpeningVoid'", ifc);
        Assert.Contains("'Opening void V", ifc);
        Assert.Equal(2, ifc.Split("IFCRELINTERFERESELEMENTS(").Length - 1);
        // The same GlobalIds at every export.
        var voids = Voids(f);
        Assert.All(voids, v => Assert.Contains($"IFCBUILDINGELEMENTPROXY('{v.GlobalId}'", ifc));
        Assert.Equal(ifc.Split('\n').Count(l => l.Contains("PROVISIONFORVOID")), Export(new IfcExportOptions(Voids: true)).Split('\n').Count(l => l.Contains("PROVISIONFORVOID")));
        // The kinds asked only.
        Assert.Single(Export(new IfcExportOptions(Voids: true, VoidKinds: new HashSet<string> { OpeningVoids.Door })).Split("IFCBUILDINGELEMENTPROXY(").Skip(1));
    }

    [Fact]
    public void A_mesh_export_carries_the_voids_in_a_group_of_their_own_when_asked()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        Opening(f, wall, 0.4, 0.3, 1.2);
        Assert.DoesNotContain(Lapin.Mesh.MeshScene.Build(f.Session, new Lapin.Mesh.MeshExportOptions()).Groups, g => g.Name == "Opening voids");
        var scene = Lapin.Mesh.MeshScene.Build(f.Session, Lapin.Mesh.MeshExportOptions.From(new JsonObject { ["voids"] = true }));
        var group = Assert.Single(scene.Groups, g => g.Name == "Opening voids");
        var el = Assert.Single(group.Elements);
        Assert.Equal((IfcClass.BuildingElementProxy, "Opening void V1"), (el.IfcClass, el.Name));
        Assert.Equal(Voids(f).Single().GlobalId, el.GlobalId);
        Assert.Contains(scene.Materials, m => m.Name == "Opening void" && m.Alpha < 1);
        // Every format writes it.
        var (glb, _) = Lapin.Mesh.MeshExport.Write(f.Session, Lapin.Mesh.MeshExportOptions.From(new JsonObject { ["voids"] = true, ["format"] = "gltf" }));
        Assert.Contains("Opening voids", Encoding.UTF8.GetString(glb));
    }

    [Fact]
    public void A_pipe_through_a_wall_needs_an_opening_it_fits_in_with_its_clearance()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var pipe = Pipe(f, 0.16);

        // No opening: a clash.
        var r = Assert.Single(Check(f));
        Assert.Equal((OpeningClashes.Missing, pipe, wall), (r.Status, r.MepId, r.HostId));
        Assert.StartsWith("No opening:", r.Message);
        Assert.Equal(0, r.Centre.X, 1);
        Assert.Equal(1, r.Centre.Y, 1);

        // A 200 × 200 opening round its axis: the 160 mm pipe is wholly inside the void.
        var op = Opening(f, wall, 0.2, 0.2, 0.9);
        r = Assert.Single(Check(f));
        Assert.Equal(OpeningClashes.Ok, r.Status);
        Assert.Equal(op, r.Void!.OpeningId);

        // Keeping 30 mm clear on every side it needs 220 mm: 10 mm short.
        r = Assert.Single(Check(f, 0.03));
        Assert.Equal(OpeningClashes.TooSmall, r.Status);
        Assert.InRange(r.By, 0.008, 0.0105);
        Assert.Contains("Opening too small", r.Message);
        Assert.Contains("10 mm", r.Message);
        Assert.Equal(OpeningClashes.Ok, Assert.Single(Check(f, 0.02)).Status);

        // A 250 mm pipe in the 200 mm opening stands 25 mm out of it.
        Resize(f, pipe, 0.25);
        r = Assert.Single(Check(f));
        Assert.Equal(OpeningClashes.TooSmall, r.Status);
        Assert.InRange(r.By, 0.02, 0.0255);

        // As the project's settings and the query say it.
        f.Run("coordination.setSettings", new JsonObject { ["clearance"] = 0.01, ["empty"] = true });
        var q = (JsonObject)f.Session.Query("coordination.clashes", new JsonObject())!;
        Assert.Equal(1, (int)q["counts"]![OpeningClashes.TooSmall]!);
        Assert.True((bool)q["available"]!);
    }

    [Fact]
    public void A_pipe_through_a_floor_opening_passes_and_an_empty_void_is_reported_on_request()
    {
        var f = new MepFixture();
        var slab = f.Id("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = MepFixture.Pts((-2, -2), (2, -2), (2, 2), (-2, 2)) });
        var hole = f.Id("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = MepFixture.Pts((-0.15, -0.15), (0.15, -0.15), (0.15, 0.15), (-0.15, 0.15)) });
        f.Id("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = MepFixture.Pts((1, 1), (1.3, 1), (1.3, 1.3), (1, 1.3)) });
        // Nothing goes through either yet: both empty, and with empty voids not asked for nothing is said.
        var results = OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options(0, true));
        Assert.Equal(2, results.Count(x => x.Status == OpeningClashes.Empty));
        Assert.Empty(OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options()));

        // An imported riser (an IFC pipe segment with its own triangles) through the first: wholly inside it.
        var top = Slabs.Top(f.Model, f.Model.Get(slab)!);
        var riser = Riser(f, 0, 0, top - 1, top + 1, 0.2);
        results = OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options(0, true));
        var ok = results.Single(x => x.MepId == riser);
        Assert.Equal((OpeningClashes.Ok, hole), (ok.Status, ok.Void!.OpeningId));
        Assert.Single(results, x => x.Status == OpeningClashes.Empty);

        // A second one beside the openings: through the floor where it has none.
        var lost = Riser(f, -1, -1, top - 1, top + 1, 0.1);
        Assert.Equal(OpeningClashes.Missing, OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options()).Single(x => x.MepId == lost).Status);
        // Too wide for the 300 mm opening: 350 mm stands 25 mm out.
        f.Session.Edit("gone", e => e.Remove(riser));
        var wide = Riser(f, 0, 0, top - 1, top + 1, 0.35);
        var r = OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options()).Single(x => x.MepId == wide);
        Assert.Equal(OpeningClashes.TooSmall, r.Status);
        Assert.InRange(r.By, 0.02, 0.0255);
    }

    [Fact]
    public void Clashes_become_remarks_of_type_clash_once_and_show_in_the_checks()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var pipe = Pipe(f, 0.1);
        var added = (JsonObject)f.Run("coordination.remarks", new JsonObject())!;
        Assert.Equal(1, (int)added["added"]!);
        var remark = Assert.Single(Remarks.All(f.Model));
        Assert.Equal("Clash", remark.Type);
        Assert.StartsWith("No opening", remark.Title);
        Assert.Contains("Openings vs MEP", remark.Labels);
        var vp = Assert.Single(remark.Viewpoints);
        Assert.Contains(pipe, vp.Selection);
        Assert.Contains(wall, vp.Selection);
        Assert.NotNull(vp.Camera);

        // Run again: the same remark updated, none added.
        var again = (JsonObject)f.Run("coordination.remarks", new JsonObject())!;
        Assert.Equal((0, 1), ((int)again["added"]!, (int)again["updated"]!));
        Assert.Single(Remarks.All(f.Model));

        // Out as BCF with the others.
        var bcf = BcfReader.Read(BcfWriter.Write(Remarks.Export(f.Model, new JsonObject())));
        Assert.Equal("Clash", Assert.Single(bcf.Topics).Type);

        // Manage → Check lists it as a warning on the pipe once the check has run (decision 293: it runs only when asked).
        f.Run("coordination.runCheck", new JsonObject());
        var check = (JsonObject)f.Session.Query("cde.check", new JsonObject())!;
        Assert.Contains(((JsonArray)check["findings"]!).OfType<JsonObject>(), x => (string?)x["rule"] == "openingsMep" && (string?)x["entityId"] == pipe && (string?)x["severity"] == "warn");

        // With its opening there, no clash is left.
        Opening(f, wall, 0.2, 0.2, 0.9);
        Assert.Equal(OpeningClashes.Ok, Assert.Single(Check(f)).Status);
    }
}
