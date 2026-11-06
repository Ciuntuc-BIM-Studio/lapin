using System.Text;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The spatial structure's identity through IFC (docs/spec-ifc.md, Structura spațială): an IFC opened as a project takes the
/// file's project, site, building and storeys, with their GlobalIds, and the GlobalIds of the aggregations and containments
/// that hold them, cycle after cycle; imported into a project, the project's own stay and a storey the file brings new keeps
/// the file's.
/// </summary>
public class IfcSpatialIdentityTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-spatial").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Make() => new(new ArchitectureModule(), new StructureModule());

    /// <summary>A GlobalId of 22 characters from a short tag.</summary>
    static string G(string tag) => ("2Sp" + tag).PadRight(22, '0');

    /// <summary>
    /// Another program's file, in millimetres: a project, site and building, the storeys <paramref name="storeys"/> (name,
    /// elevation in mm), each with a box standing on it, contained by an IfcRelContainedInSpatialStructure of its own.
    /// </summary>
    static string Foreign(params (string Name, double Z)[] storeys)
    {
        var s = new StringBuilder($"""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
            FILE_NAME('other.ifc','2026-11-06T00:00:00',(''),(''),'','Other 1.0','');
            FILE_SCHEMA(('IFC4'));
            ENDSEC;
            DATA;
            #1=IFCORGANIZATION($,'Test',$,$,$);
            #2=IFCAPPLICATION(#1,'1.0','test','T');
            #3=IFCOWNERHISTORY(#5,#2,$,.NOTDEFINED.,$,$,$,0);
            #4=IFCPERSON($,'User',$,$,$,$,$,$);
            #5=IFCPERSONANDORGANIZATION(#4,#1,$);
            #6=IFCDIRECTION((1.,0.,0.));
            #7=IFCDIRECTION((0.,0.,1.));
            #8=IFCCARTESIANPOINT((0.,0.,0.));
            #9=IFCAXIS2PLACEMENT3D(#8,#7,#6);
            #10=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#9,$);
            #11=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
            #12=IFCUNITASSIGNMENT((#11));
            #13=IFCPROJECT('{G("Project")}',#3,'Casa Popescu',$,$,$,$,(#10),#12);
            #20=IFCSITE('{G("Site")}',#3,'Teren',$,$,#21,$,$,.ELEMENT.,$,$,$,$,$);
            #21=IFCLOCALPLACEMENT($,#9);
            #30=IFCBUILDING('{G("Building")}',#3,'Corp A',$,$,#31,$,$,.ELEMENT.,$,$,$);
            #31=IFCLOCALPLACEMENT(#21,#9);
            #42=IFCRELAGGREGATES('{G("AggSite")}',#3,$,$,#13,(#20));
            #43=IFCRELAGGREGATES('{G("AggBuilding")}',#3,$,$,#20,(#30));
            #90=IFCAXIS2PLACEMENT2D(#8,$);
            #91=IFCRECTANGLEPROFILEDEF(.AREA.,$,#90,400.,400.);
            #92=IFCEXTRUDEDAREASOLID(#91,#9,#7,1000.);
            #99=IFCSHAPEREPRESENTATION(#10,'Body','SweptSolid',(#92));

            """);
        var id = 100;
        var storeyRefs = new List<int>();
        for (var i = 0; i < storeys.Length; i++)
        {
            var (name, z) = storeys[i];
            var st = id;
            s.Append($"#{st}=IFCBUILDINGSTOREY('{G("Storey" + i)}',#3,'{name}',$,$,#{st + 1},$,$,.ELEMENT.,{z:0.0});\n");
            s.Append($"#{st + 1}=IFCLOCALPLACEMENT(#31,#{st + 8});\n#{st + 8}=IFCAXIS2PLACEMENT3D(#{st + 9},$,$);\n#{st + 9}=IFCCARTESIANPOINT((0.,0.,{z:0.0}));\n");
            s.Append($"#{st + 2}=IFCCARTESIANPOINT(({1000 * (i + 1):0.0},0.,0.));\n#{st + 3}=IFCAXIS2PLACEMENT3D(#{st + 2},$,$);\n#{st + 4}=IFCLOCALPLACEMENT(#{st + 1},#{st + 3});\n");
            s.Append($"#{st + 5}=IFCPRODUCTDEFINITIONSHAPE($,$,(#99));\n");
            s.Append($"#{st + 6}=IFCBUILDINGELEMENTPROXY('{G("Box" + i)}',#3,'Box {i}',$,$,#{st + 4},#{st + 5},$,.NOTDEFINED.);\n");
            s.Append($"#{st + 7}=IFCRELCONTAINEDINSPATIALSTRUCTURE('{G("Contains" + i)}',#3,$,$,(#{st + 6}),#{st});\n");
            storeyRefs.Add(st);
            id += 10;
        }
        s.Append($"#{id}=IFCRELAGGREGATES('{G("AggStoreys")}',#3,$,$,#30,({string.Join(",", storeyRefs.Select(r => "#" + r))}));\n");
        s.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        return s.ToString();
    }

    /// <summary>A file's spatial roots and the relationships holding them: GlobalId → class and name (a storey with its elevation, a relationship with what it relates).</summary>
    static SortedDictionary<string, string> Spatial(string path)
    {
        var f = StepFile.Read(path);
        var o = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string Name(object? r) => f.Get(r) is { } e ? $"{e.Type}:{IfcModel.Str(e[2])}" : "?";
        foreach (var e in f.Rooted)
        {
            if (e.Type is "IFCPROJECT" or "IFCSITE" or "IFCBUILDING") o[IfcModel.Str(e[0])!] = $"{e.Type}:{IfcModel.Str(e[2])}";
            else if (e.Type == "IFCBUILDINGSTOREY") o[IfcModel.Str(e[0])!] = $"{e.Type}:{IfcModel.Str(e[2])}@{Math.Round(IfcModel.Num(e[9]), 6)}";
            else if (e.Type == "IFCRELAGGREGATES" && f.Get(e[4]) is { Type: "IFCPROJECT" or "IFCSITE" or "IFCBUILDING" }) o[IfcModel.Str(e[0])!] = $"aggregates {Name(e[4])}";
            else if (e.Type == "IFCRELCONTAINEDINSPATIALSTRUCTURE") o[IfcModel.Str(e[0])!] = $"contains in {Name(e[5])}";
        }
        return o;
    }

    string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    static IfcImportResult Open(Fixture f, string path) => IfcImport.Open(f.Session, StepFile.Read(path), Path.GetFileName(path), null, out _);

    [Fact]
    public void An_IFC_opened_as_a_project_takes_the_files_spatial_structure_and_its_GlobalIds()
    {
        var path = Write("other.ifc", Foreign(("Subsol", -2500), ("Parter", 0), ("Etaj 1", 2800)));
        var f = Make();
        // As the start screen does: a new project of the template's storeys first, then the file opened as a project.
        f.Session.New();
        Assert.Contains(Lapin.Platform.Spatial.Storeys(f.Model), s => s.Name == "Foundations");
        Open(f, path);

        var m = f.Model;
        Assert.Equal(G("Project"), Lapin.Platform.Spatial.Project(m)!.Id);
        Assert.Equal("Casa Popescu", Lapin.Platform.Spatial.Project(m)!.Name);
        Assert.Equal((G("Site"), "Teren"), m.OfClass(IfcClass.Site).Select(s => (s.Id, s.Name)).Single());
        Assert.Equal((G("Building"), "Corp A"), (Lapin.Platform.Spatial.Building(m)!.Id, Lapin.Platform.Spatial.Building(m)!.Name));
        // The file's storeys only, bottom up, with their GlobalIds and elevations; their heights up to the next one; the template's are gone.
        var storeys = Lapin.Platform.Spatial.Storeys(m).ToList();
        Assert.Equal([(G("Storey0"), "Subsol", -2.5, 2.5), (G("Storey1"), "Parter", 0.0, 2.8), (G("Storey2"), "Etaj 1", 2.8, 3.0)],
            storeys.Select(s => (s.Id, s.Name, s.Num("elevation"), s.Num("height"))));
        // Every box on its storey, and each storey's plan drawing it.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(G("Storey" + i), Lapin.Platform.Spatial.StoreyOf(m, G("Box" + i))?.Id);
            var plan = Views.Find(m, "plan:" + G("Storey" + i));
            Assert.NotNull(plan);
            Assert.Contains(Views.Generate(f.Session.Reader, plan!).Drawing!.Lines, l => l.Element == G("Box" + i));
        }
        // Undo takes the import back to the project the file made (its structure), not to the template.
        f.Session.Undo();
        Assert.Equal(3, Lapin.Platform.Spatial.Storeys(f.Model).Count());
        Assert.Null(f.Model.Get(G("Box0")));
    }

    [Fact]
    public void Opened_as_a_project_and_exported_over_and_over_every_spatial_GlobalId_stays_the_files()
    {
        var path = Write("cycle-0.ifc", Foreign(("Subsol", -2500), ("Parter", 0), ("Etaj 1", 2800)));
        var first = Spatial(path);
        // The file's own: project, site, building, three storeys, three aggregations and three containments.
        Assert.Equal(12, first.Count);
        for (var cycle = 1; cycle <= 10; cycle++)
        {
            var f = Make();
            Open(f, path);
            var next = Path.Combine(_dir, $"cycle-{cycle}.ifc");
            IfcExport.Write(f.Session, next);
            path = next;
            var now = Spatial(path);
            // Lapin writes the same relations under the same GlobalIds (elevations in metres now: the names compared, the ids exactly).
            Assert.Equal(first.Keys, now.Keys);
            Assert.Equal(first.Values.Select(v => v.Split('@')[0]), now.Values.Select(v => v.Split('@')[0]));
        }
    }

    [Fact]
    public void Imported_into_a_project_the_projects_ids_stay_and_a_new_storey_takes_the_files()
    {
        var path = Write("other.ifc", Foreign(("Ground floor", 0), ("Mansarda", 5600)));
        var f = Make();
        f.Session.New();
        var project = Lapin.Platform.Spatial.Project(f.Model)!.Id;
        var site = f.Model.OfClass(IfcClass.Site).Single().Id;
        var building = Lapin.Platform.Spatial.Building(f.Model)!.Id;
        var ground = Lapin.Platform.Spatial.Storeys(f.Model).Single(s => s.Name == "Ground floor").Id;
        IfcImport.Run(f.Session, path, null, out _);

        var m = f.Model;
        Assert.Equal(project, Lapin.Platform.Spatial.Project(m)!.Id);
        Assert.Equal(site, m.OfClass(IfcClass.Site).Single().Id);
        Assert.Equal(building, Lapin.Platform.Spatial.Building(m)!.Id);
        // The template's storeys stay; the file's Ground floor is the project's; Mansarda is new, under the file's GlobalId.
        Assert.Equal(["Foundations", "Ground floor", "First floor", "Mansarda"], Lapin.Platform.Spatial.Storeys(m).Select(s => s.Name));
        Assert.Equal(ground, Lapin.Platform.Spatial.StoreyOf(m, G("Box0"))?.Id);
        Assert.Equal(G("Storey1"), Lapin.Platform.Spatial.StoreyOf(m, G("Box1"))?.Id);
        Assert.Contains(Views.Generate(f.Session.Reader, Views.Find(m, "plan:" + G("Storey1"))!).Drawing!.Lines, l => l.Element == G("Box1"));

        var out1 = Path.Combine(_dir, "out.ifc");
        IfcExport.Write(f.Session, out1);
        var ids = Spatial(out1);
        Assert.DoesNotContain(G("Project"), ids.Keys);
        Assert.DoesNotContain(G("Storey0"), ids.Keys);
        Assert.DoesNotContain(G("Contains0"), ids.Keys);
        // The new storey and its containment as the file had them.
        Assert.Equal("IFCBUILDINGSTOREY:Mansarda@5.6", ids[G("Storey1")]);
        Assert.Equal("contains in IFCBUILDINGSTOREY:Mansarda", ids[G("Contains1")]);
        // Imported again, nothing new: the same storey by its id.
        IfcImport.Run(f.Session, path, null, out _);
        Assert.Equal(4, Lapin.Platform.Spatial.Storeys(f.Model).Count());
    }

    [Fact]
    public void A_file_with_no_storey_opened_as_a_project_has_a_ground_floor()
    {
        var path = Write("bare.ifc", Foreign());
        var f = Make();
        Open(f, path);
        Assert.Equal(G("Project"), Lapin.Platform.Spatial.Project(f.Model)!.Id);
        Assert.Equal("Ground floor", Lapin.Platform.Spatial.Storeys(f.Model).Single().Name);
    }

    [Fact]
    public void A_storey_left_out_in_the_survey_comes_empty_and_the_diff_is_one_reset()
    {
        var path = Write("other.ifc", Foreign(("Subsol", -2500), ("Parter", 0)));
        var f = Make();
        IfcImport.Open(f.Session, StepFile.Read(path), "other.ifc", new IfcImportOptions(Storeys: new HashSet<string> { G("Storey1") }), out var diff);
        Assert.Equal([G("Storey0"), G("Storey1")], Lapin.Platform.Spatial.Storeys(f.Model).Select(s => s.Id));
        Assert.Null(f.Model.Get(G("Box0")));
        // One reset: the new project with what the import brought.
        Assert.True(diff.Reset);
        Assert.Contains(diff.Nodes, n => n.Id == G("Box1"));
        Assert.Contains(diff.Nodes, n => n.Id == G("Storey1"));
    }
}
