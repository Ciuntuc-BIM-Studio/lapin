using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// An IFC file looked at before it is imported (File → Import IFC…): its storeys and its elements by kind and
/// discipline, a generic element split by the category its name starts with; then only what the user chose
/// brought in, the import's stages told as it goes.
/// </summary>
public class IfcSurveyTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-survey").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static Fixture Make() => new(new ArchitectureModule(), new StudioModule());

    /// <summary>Two storeys; on the first a beam, a column, two dryers (proxies named as Revit names mechanical equipment), a chair; on the second a beam and a plain proxy.</summary>
    static string Model()
    {
        var s = new StringBuilder("""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
            FILE_NAME('plant.ifc','2026-10-29T00:00:00',(''),(''),'','Autodesk Revit 2026 (ENU)','');
            FILE_SCHEMA(('IFC2X3'));
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
            #13=IFCPROJECT('2PlantProject000000000',#3,'Plant',$,$,$,$,(#10),#12);
            #20=IFCSITE('2PlantSite000000000000',#3,'Site',$,$,#21,$,$,.ELEMENT.,$,$,$,$,$);
            #21=IFCLOCALPLACEMENT($,#9);
            #30=IFCBUILDING('2PlantBuilding00000000',#3,'Building',$,$,#31,$,$,.ELEMENT.,$,$,$);
            #31=IFCLOCALPLACEMENT(#21,#9);
            #40=IFCBUILDINGSTOREY('2PlantStoreyLow0000000',#3,'Low',$,$,#41,$,$,.ELEMENT.,0.);
            #41=IFCLOCALPLACEMENT(#31,#9);
            #50=IFCBUILDINGSTOREY('2PlantStoreyHigh000000',#3,'High',$,$,#51,$,$,.ELEMENT.,4000.);
            #51=IFCLOCALPLACEMENT(#31,#9);
            #42=IFCRELAGGREGATES('2PlantRelAgg0000000000',#3,$,$,#13,(#20));
            #43=IFCRELAGGREGATES('2PlantRelAgg0000000001',#3,$,$,#20,(#30));
            #44=IFCRELAGGREGATES('2PlantRelAgg0000000002',#3,$,$,#30,(#40,#50));
            #90=IFCAXIS2PLACEMENT2D(#8,$);
            #91=IFCRECTANGLEPROFILEDEF(.AREA.,$,#90,400.,400.);
            #92=IFCEXTRUDEDAREASOLID(#91,#9,#7,1000.);
            #99=IFCSHAPEREPRESENTATION(#10,'Body','SweptSolid',(#92));

            """);
        var low = new List<int>(); var high = new List<int>();
        var id = 1000;
        void Add(string type, string name, double x, List<int> on, string tail = "$")
        {
            s.Append($"#{id}=IFCCARTESIANPOINT(({x:0.0},0.,0.));\n#{id + 1}=IFCAXIS2PLACEMENT3D(#{id},$,$);\n#{id + 2}=IFCLOCALPLACEMENT(#41,#{id + 1});\n");
            s.Append($"#{id + 3}=IFCPRODUCTDEFINITIONSHAPE($,$,(#99));\n");
            s.Append($"#{id + 4}={type}('{(id + 4).ToString().PadLeft(22, '0')}',#3,'{name}',$,$,#{id + 2},#{id + 3},{tail});\n");
            on.Add(id + 4);
            id += 5;
        }
        Add("IFCBEAM", "Structural Framing : Beam : 1", 0, low);
        Add("IFCCOLUMN", "Structural Columns : Column : 1", 1000, low);
        Add("IFCBUILDINGELEMENTPROXY", "Mechanical Equipment : Dryer : A", 2000, low, "$,.NOTDEFINED.");
        Add("IFCBUILDINGELEMENTPROXY", "Mechanical Equipment : Dryer : B", 3000, low, "$,.NOTDEFINED.");
        Add("IFCFURNISHINGELEMENT", "Furniture : Chair : 1", 4000, low);
        Add("IFCBEAM", "Structural Framing : Beam : 2", 0, high);
        Add("IFCBUILDINGELEMENTPROXY", "anything", 1000, high, "$,.NOTDEFINED.");
        // A room on the high storey (aggregated into it, as ArchiCAD and Revit write it) and a table contained in the room, not in the storey.
        s.Append($"#{id}=IFCSPACE('2PlantSpace00000000000',#3,'R1',$,$,#51,$,'Room',.ELEMENT.,.INTERNAL.,$);\n");
        s.Append($"#{id + 1}=IFCRELAGGREGATES('2PlantRelAgg0000000003',#3,$,$,#50,(#{id}));\n");
        var space = id;
        id += 2;
        var table = new List<int>();
        Add("IFCFURNISHINGELEMENT", "Furniture : Table : 1", 500, table);
        s.Append($"#{id}=IFCRELCONTAINEDINSPATIALSTRUCTURE('2PlantRelCnt0000000002',#3,$,$,(#{table[0]}),#{space});\n");
        // A virtual element (a space boundary): no part of the choice.
        s.Append($"#{id + 1}=IFCVIRTUALELEMENT('2PlantVirtual000000000',#3,'boundary',$,$,$,$,$);\n");
        s.Append($"#5000000=IFCRELCONTAINEDINSPATIALSTRUCTURE('2PlantRelCnt0000000000',#3,$,$,({string.Join(",", low.Select(p => "#" + p))}),#40);\n");
        s.Append($"#5000001=IFCRELCONTAINEDINSPATIALSTRUCTURE('2PlantRelCnt0000000001',#3,$,$,({string.Join(",", high.Select(p => "#" + p))}),#50);\n");
        s.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        return s.ToString();
    }

    string Write()
    {
        var path = Path.Combine(_dir, "plant.ifc");
        File.WriteAllText(path, Model());
        return path;
    }

    static JsonNode? Call(EngineDispatcher d, string method, JsonObject p)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        var o = JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        Assert.Null((string?)o["error"]);
        return o["result"];
    }

    [Fact]
    public void A_file_is_surveyed_by_storey_and_by_kind_grouped_by_discipline()
    {
        var f = Make();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var s = Call(d, "project.ifcSurvey", new JsonObject { ["path"] = Write() })!;
        Assert.Equal("plant.ifc", (string?)s["name"]);
        Assert.Equal("IFC2X3", (string?)s["schema"]);
        Assert.Equal("Autodesk Revit 2026 (ENU)", (string?)s["application"]);
        Assert.Equal(9, (int)s["products"]!);
        // The storeys from the lowest, with how many elements each carries (millimetres read as metres): the room and the table in it on the high one.
        var storeys = s["storeys"]!.AsArray();
        Assert.Equal(["Low", "High"], storeys.Select(x => (string?)x!["name"]));
        Assert.Equal([5, 4], storeys.Select(x => (int)x!["count"]!));
        Assert.Equal(0, (int)s["noStorey"]!);
        Assert.Equal(4.0, (double)storeys[1]!["elevation"]!);
        // By discipline: the dryers are MEP by their name's category, the chair furniture, the unnamed proxy other.
        var groups = s["groups"]!.AsArray().ToDictionary(g => (string)g!["discipline"]!, g => g!);
        Assert.Equal(["architecture", "structure", "mep", "furniture", "other"], groups.Keys);
        Assert.Equal("IfcSpace", (string?)Assert.Single(groups["architecture"]["kinds"]!.AsArray())!["key"]);
        Assert.Equal(3, (int)groups["structure"]["count"]!);
        var mep = Assert.Single(groups["mep"]["kinds"]!.AsArray());
        Assert.Equal("IfcBuildingElementProxy|Mechanical Equipment", (string?)mep!["key"]);
        Assert.Equal("Proxy · Mechanical Equipment", (string?)mep["label"]);
        Assert.Equal(2, (int)mep["count"]!);
        Assert.Equal("IfcBuildingElementProxy", (string?)Assert.Single(groups["other"]["kinds"]!.AsArray())!["key"]);
    }

    [Fact]
    public void Only_the_kinds_and_storeys_chosen_come_in_and_the_import_tells_its_stages()
    {
        var f = Make();
        var stages = new List<string>();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create()) { Progress = p => stages.Add((string)p["stage"]!) };
        var path = Write();
        Call(d, "project.ifcSurvey", new JsonObject { ["path"] = path });
        // The structure on both storeys, the MEP on none: the dryers, the chair and the unnamed proxy stay out.
        var r = Call(d, "project.importIfc", new JsonObject
        {
            ["path"] = path,
            ["kinds"] = new JsonArray("IfcBeam", "IfcColumn"),
            ["storeys"] = new JsonArray("2PlantStoreyLow0000000", "2PlantStoreyHigh000000"),
        })!;
        // The elements (the two beams' identical bodies are kept once, a shared body beside them).
        var names = f.Model.Entities.Where(e => e.Class != StoredBodies.SharedClass && (e.Name.Contains(" : ") || e.Name == "anything")).Select(e => e.Name).Order().ToList();
        Assert.Equal(["Structural Columns : Column : 1", "Structural Framing : Beam : 1", "Structural Framing : Beam : 2"], names);
        Assert.Contains("Indexing the file", stages);
        Assert.Contains(stages, x => x.StartsWith("Recognising", StringComparison.Ordinal) || x.StartsWith("Bringing", StringComparison.Ordinal));
        Assert.Equal(3, (int)r["recognized"]! + (int)r["references"]! + (int)r["elements"]!);

        // The low storey alone, every kind: five elements, the high storey's beam and proxy left out.
        var g = Make();
        var d2 = new EngineDispatcher(g.Session, PackageFiles.Create());
        Call(d2, "project.importIfc", new JsonObject { ["path"] = path, ["storeys"] = new JsonArray("2PlantStoreyLow0000000") });
        Assert.DoesNotContain(g.Model.Entities, e => e.Name is "Structural Framing : Beam : 2" or "anything" or "Furniture : Table : 1");
        Assert.Equal(5, g.Model.Entities.Count(e => e.Class != StoredBodies.SharedClass && e.Name.Contains(" : ")));

        // The high storey alone: the table in its room comes with it.
        var h = Make();
        var d3 = new EngineDispatcher(h.Session, PackageFiles.Create());
        Call(d3, "project.importIfc", new JsonObject { ["path"] = path, ["storeys"] = new JsonArray("2PlantStoreyHigh000000") });
        Assert.Contains(h.Model.Entities, e => e.Name == "Furniture : Table : 1");
        Assert.DoesNotContain(h.Model.Entities, e => e.Name == "Structural Framing : Beam : 1");
    }

    [Fact]
    public void Nothing_chosen_brings_everything_as_before()
    {
        var f = Make();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        Call(d, "project.importIfc", new JsonObject { ["path"] = Write() });
        Assert.Equal(8, f.Model.Entities.Count(e => e.Class != StoredBodies.SharedClass && (e.Name.Contains(" : ") || e.Name == "anything")));
    }
}
