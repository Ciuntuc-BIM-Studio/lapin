using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Mesh.Bos;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// An import's storeys (decision 406): the file's levels as the file gives them, in its unit, and the rule that the file's
/// storeys replace the project's (ImportStoreys) for BIM Open Schema, Fragments and IFC: a project storey at a file storey's
/// level merged into it (its elements kept where they are, its views moved), an empty one at no level of the file's removed,
/// one that holds something kept and reported; one undo step with the import.
/// </summary>
public class ImportStoreysTests
{
    static Fixture Source(params (string Name, double Elevation, double Height)[] storeys)
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        f.Session.New(storeys: storeys);
        foreach (var s in Spatial.Storeys(f.Model).ToList())
            f.Run("architecture.addWall", new JsonObject { ["storeyId"] = s.Id, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0 });
        return f;
    }

    static FragImportResult Import(Fixture into, Fixture from, string format)
    {
        if (format == "bos")
        {
            var bytes = MeshExport.Write(from.Session, new MeshExportOptions("bos", Origin: "internal")).Bytes;
            return BosImport.Run(into.Session, BosImport.Read(bytes, "model.bos"), "model.bos", out _);
        }
        var frag = FragWriter.Write(MeshScene.Build(from.Session, new MeshExportOptions("frag", Origin: "internal")));
        return FragImport.Run(into.Session, frag, "model.frag", out _);
    }

    [Theory]
    [InlineData("bos")]
    [InlineData("frag")]
    public void The_files_levels_come_as_it_gives_them_and_its_storeys_replace_the_projects_empty_ones(string format)
    {
        // An upper floor whose element hangs far below it (a tester's file: E01 at +3.75 read as +0.00).
        var a = Source(("B01", -3.37, 3.22), ("E00", -0.15, 3.9), ("E01", 3.75, 3));
        var e01 = Spatial.Storeys(a.Model).Single(s => s.Name == "E01").Id;
        a.Run("architecture.addWall", new JsonObject { ["storeyId"] = e01, ["x1"] = 0, ["y1"] = 2, ["x2"] = 4, ["y2"] = 2, ["baseOffset"] = -3.6, ["height"] = 0.5 });
        var b = new Fixture(new ArchitectureModule(), new StructureModule());
        b.Session.New();
        var r = Import(b, a, format);
        // The project's Foundations, Ground floor and First floor, empty and at none of the file's levels, are gone.
        Assert.Equal([("B01", -3.37), ("E00", -0.15), ("E01", 3.75)], Spatial.Storeys(b.Model).Select(s => (s.Name!, Math.Round(s.Num("elevation"), 4))));
        Assert.Equal(["Foundations", "Ground floor", "First floor"], r.Replaced!.Removed);
        Assert.Contains(r.Notes, n => n.Contains("Empty storey(s) removed"));
        // The site and location plans the new project made on its ground floor stand on the file's storeys now.
        var ids = Spatial.Storeys(b.Model).Select(s => s.Id).ToHashSet();
        Assert.All(b.Model.OfClass(Views.Class).Where(v => v.Str("storeyId") != null), v => Assert.Contains(v.Str("storeyId")!, ids));
        // One undo step: the project's storeys back.
        b.Session.Undo();
        Assert.Equal(["Foundations", "Ground floor", "First floor"], Spatial.Storeys(b.Model).Select(s => s.Name));
    }

    [Theory]
    [InlineData("bos")]
    [InlineData("frag")]
    public void A_project_storey_at_a_file_level_is_merged_into_it_and_one_that_holds_elements_stays(string format)
    {
        var a = Source(("Parter", 0.03, 3), ("Etaj 2", 6, 3));
        var b = new Fixture(new ArchitectureModule(), new StructureModule());
        b.Session.New();
        var storeys = Spatial.Storeys(b.Model).ToDictionary(s => s.Name!, s => s.Id);
        var ground = b.Run("architecture.addWall", new JsonObject { ["storeyId"] = storeys["Ground floor"], ["x1"] = 10, ["y1"] = 0, ["x2"] = 14, ["y2"] = 0 })!["id"]!.GetValue<string>();
        var first = b.Run("architecture.addWall", new JsonObject { ["storeyId"] = storeys["First floor"], ["x1"] = 10, ["y1"] = 5, ["x2"] = 14, ["y2"] = 5 })!["id"]!.GetValue<string>();
        var (lo, hi) = b.Bounds(ground);
        var r = Import(b, a, format);

        // Ground floor (±0.00) is the file's Parter (+0.03): its wall on it, where it stood; First floor (+3.00) holds a wall
        // and matches no level of the file's: it stays, and the report says so; Foundations, empty, goes.
        var parter = Spatial.Storeys(b.Model).Single(s => s.Name == "Parter");
        Assert.Equal(0.03, parter.Num("elevation"), 6);
        Assert.Null(b.Model.Get(storeys["Ground floor"]));
        Assert.Equal(parter.Id, Spatial.StoreyOf(b.Model, ground)!.Id);
        var (lo2, hi2) = b.Bounds(ground);
        Assert.Equal(lo.Z, lo2.Z, 6); Assert.Equal(hi.Z, hi2.Z, 6);
        Assert.Equal(storeys["First floor"], Spatial.StoreyOf(b.Model, first)!.Id);
        Assert.Equal([("Ground floor", "Parter")], r.Replaced!.Merged);
        Assert.Equal(["First floor"], r.Replaced.Kept);
        Assert.Equal(["Foundations"], r.Replaced.Removed);
        Assert.Contains(r.Notes, n => n.Contains("„First floor”") && n.Contains("Kept"));
        Assert.Equal(["Parter", "First floor", "Etaj 2"], Spatial.Storeys(b.Model).Select(s => s.Name));
        // Imported again: the same storeys by their ids, nothing more.
        Import(b, a, format);
        Assert.Equal(["Parter", "First floor", "Etaj 2"], Spatial.Storeys(b.Model).Select(s => s.Name));
    }

    [Fact]
    public void Drafting_on_a_storeys_plan_keeps_it_and_the_views_of_one_removed_go_to_the_files_nearest()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Session.New();
        var storeys = Spatial.Storeys(f.Model).ToDictionary(s => s.Name!, s => s.Id);
        StoreyReplacement r = StoreyReplacement.None;
        string file = "";
        f.Session.Edit("test", edit =>
        {
            edit.Add(new Entity(edit.NewId(), "LapinTestNote", "Note", new JsonObject { ["viewId"] = StoreyPlans.Prefix + storeys["First floor"], ["at"] = new JsonArray(1, 2) }));
            var s = new Entity(edit.NewId(), IfcClass.BuildingStorey, "Level 1", new JsonObject { ["elevation"] = 0.5, ["height"] = 3.0 });
            edit.Add(s);
            edit.Relate(IfcClass.RelAggregates, Spatial.Building(edit.Model)!.Id, s.Id);
            file = s.Id;
            r = ImportStoreys.Replace(edit, f.Session.Catalog, [s.Id]);
        });
        Assert.Equal(["First floor"], r.Kept);
        Assert.Equal(["Foundations", "Ground floor"], r.Removed);
        // The site plan stood on the ground floor: on Level 1 now, the file's storey nearest it.
        Assert.Contains(f.Model.OfClass(Views.Class), v => v.Str("site") != null && v.Str("storeyId") == file);
        Assert.DoesNotContain(f.Model.OfClass(Views.Class), v => v.Str("storeyId") == storeys["Ground floor"]);
    }

    [Fact]
    public void A_models_levels_are_read_in_its_unit_by_their_spacing_then_by_its_elements()
    {
        // Metres, though the upper floor's elements hang below it (the old per-storey fit read 3.75 m as 3.75 mm).
        Assert.Equal(1, StoreyUnits.Scale([(-3.37, -3.87), (-0.15, -0.37), (3.75, -0.05)]));
        // Millimetres, whatever the elements say.
        Assert.Equal(0.001, StoreyUnits.Scale([(-3370, -13.3), (-150, 2.4), (3750, -3.3)]));
        // One storey: by its elements; without them, a level above 500 is millimetres.
        Assert.Equal(0.001, StoreyUnits.Scale([(2800, 2.79)]));
        Assert.Equal(1, StoreyUnits.Scale([(2.8, 2.7)]));
        Assert.Equal(0.001, StoreyUnits.Scale([(3000, null)]));
        Assert.Equal(1, StoreyUnits.Scale([(3, null)]));
    }

    [Fact]
    public void A_prism_of_many_sides_is_read_and_a_body_of_many_faces_that_is_none_is_refused_at_once()
    {
        static Solid Extruded(int n, double top)
        {
            var v = new List<Vec3>();
            for (var i = 0; i < n; i++) v.Add(new Vec3(Math.Cos(2 * Math.PI * i / n), Math.Sin(2 * Math.PI * i / n), 0));
            for (var i = 0; i < n; i++) v.Add(new Vec3(top * Math.Cos(2 * Math.PI * i / n), top * Math.Sin(2 * Math.PI * i / n), 3));
            var faces = new List<Face>
            {
                new([.. Enumerable.Range(0, n).Reverse()], [], new Vec3(0, 0, -1)),
                new([.. Enumerable.Range(n, n)], [], new Vec3(0, 0, 1)),
            };
            for (var i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n;
                var nrm = Vec3.Cross(v[b] - v[a], v[n + a] - v[a]).Normalized();
                faces.Add(new([a, b, n + b, n + a], [], nrm));
            }
            return new Solid([.. v], [.. faces]);
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.Contains(IfcModel.PrismsOfSolid(Extruded(60, 1)), p => p.Vertical);
        Assert.Empty(IfcModel.PrismsOfSolid(Extruded(3000, 0.5)));
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"{sw.Elapsed.TotalSeconds:0.0} s");
    }
}
