using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Host;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A foreign element's IFC carried through Lapin (docs/spec-ifc.md, Ce se păstrează): the MEP elements of a
/// file imported alone and exported again come out as they came — their GlobalIds, classes and attributes,
/// their property sets and quantities with each value's type and every digit, their type with its sets,
/// their material, ports, connections and system, and the relationships' GlobalIds.
/// </summary>
public class IfcCarryTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-carry").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Make() => new(new ArchitectureModule(), new StructureModule(), new MepModule());

    /// <summary>A drainage stack and a bend, their ports connected, in one system, beside a wall; the stack typed, with two sets and quantities.</summary>
    const string Mep = """
ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [ReferenceView]'),'2;1');
FILE_NAME('mep.ifc','2026-10-30T10:00:00',('x'),('x'),'probe','probe','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('0Proj00000000000000001',$,'Probe',$,$,$,$,(#4),#2);
#2=IFCUNITASSIGNMENT((#3,#300));
#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#300=IFCSIUNIT(*,.PRESSUREUNIT.,$,.PASCAL.);
#4=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#6,$);
#5=IFCCARTESIANPOINT((0.,0.,0.));
#6=IFCAXIS2PLACEMENT3D(#5,$,$);
#10=IFCSITE('0Site00000000000000001',$,'Site',$,$,#11,$,$,.ELEMENT.,$,$,$,$,$);
#11=IFCLOCALPLACEMENT($,#6);
#12=IFCBUILDING('0Bldg00000000000000001',$,'Building',$,$,#13,$,$,.ELEMENT.,$,$,$);
#13=IFCLOCALPLACEMENT(#11,#6);
#14=IFCBUILDINGSTOREY('0Stry00000000000000001',$,'Level 0',$,$,#15,$,$,.ELEMENT.,0.);
#15=IFCLOCALPLACEMENT(#13,#6);
#16=IFCRELAGGREGATES('0Agg100000000000000001',$,$,$,#1,(#10));
#17=IFCRELAGGREGATES('0Agg200000000000000001',$,$,$,#10,(#12));
#18=IFCRELAGGREGATES('0Agg300000000000000001',$,$,$,#12,(#14));
#20=IFCDIRECTION((1.,0.,0.));
#21=IFCDIRECTION((0.,0.,1.));
#22=IFCCARTESIANPOINT((0.,0.));
#23=IFCAXIS2PLACEMENT2D(#22,$);
#24=IFCCIRCLEPROFILEDEF(.AREA.,$,#23,0.055);
#25=IFCAXIS2PLACEMENT3D(#5,#20,#21);
#26=IFCEXTRUDEDAREASOLID(#24,#6,#21,3.);
#27=IFCSHAPEREPRESENTATION(#4,'Body','SweptSolid',(#26));
#28=IFCPRODUCTDEFINITIONSHAPE($,$,(#27));
#29=IFCCARTESIANPOINT((1.,2.,0.5));
#30=IFCAXIS2PLACEMENT3D(#29,#20,#21);
#31=IFCLOCALPLACEMENT(#15,#30);
#40=IFCPROPERTYSINGLEVALUE('NominalDiameter',$,IFCPOSITIVELENGTHMEASURE(0.11),$);
#41=IFCPROPERTYSET('0TPst00000000000000001',$,'Pset_PipeSegmentTypeCommon',$,(#40));
#50=IFCPIPESEGMENTTYPE('0PTyp00000000000000001',$,'PVC-U DN110',$,$,(#41),$,'TYP-110',$,.RIGIDSEGMENT.);
#51=IFCRELDEFINESBYTYPE('0RTyp00000000000000001',$,$,$,(#60),#50);
#60=IFCPIPESEGMENT('2Pipe00000000000000001',$,'Pipe 1','Waste stack','Stack DN110',#31,#28,'P-001',.RIGIDSEGMENT.);
#61=IFCPROPERTYSINGLEVALUE('InteriorRoughnessCoefficient',$,IFCPOSITIVELENGTHMEASURE(1.5E-06),$);
#62=IFCPROPERTYSINGLEVALUE('Gradient',$,IFCPOSITIVERATIOMEASURE(0.0105),$);
#63=IFCPROPERTYSET('0Pst100000000000000001',$,'Pset_PipeSegmentOccurrence','occurrence set',(#61,#62));
#64=IFCRELDEFINESBYPROPERTIES('0RPs100000000000000001',$,$,$,(#60),#63);
#65=IFCPROPERTYSINGLEVALUE('Cost',$,IFCMONETARYMEASURE(12.5),$);
#66=IFCPROPERTYSINGLEVALUE('Installed',$,IFCBOOLEAN(.T.),$);
#67=IFCPROPERTYSINGLEVALUE('Notes',$,IFCTEXT('Line one, \X2\00D6\X0\ ok'),$);
#68=IFCPROPERTYSINGLEVALUE('TestPressure',$,IFCPRESSUREMEASURE(600000.123456),$);
#69=IFCPROPERTYENUMERATEDVALUE('Class',$,(IFCLABEL('A'),IFCLABEL('B')),$);
#70=IFCPROPERTYSET('0Pst200000000000000001',$,'Company_Data',$,(#65,#66,#67,#68,#69));
#71=IFCRELDEFINESBYPROPERTIES('0RPs200000000000000001',$,$,$,(#60),#70);
#72=IFCQUANTITYLENGTH('Length',$,$,3.,$);
#73=IFCQUANTITYVOLUME('GrossVolume',$,$,0.0285,$);
#74=IFCELEMENTQUANTITY('0Qto100000000000000001',$,'Qto_PipeSegmentBaseQuantities',$,$,(#72,#73));
#75=IFCRELDEFINESBYPROPERTIES('0RQt100000000000000001',$,$,$,(#60),#74);
#76=IFCMATERIAL('PVC-U',$,'Plastic');
#77=IFCRELASSOCIATESMATERIAL('0RMat00000000000000001',$,$,$,(#60),#76);
#80=IFCCARTESIANPOINT((1.,2.,3.5));
#81=IFCAXIS2PLACEMENT3D(#80,$,$);
#82=IFCLOCALPLACEMENT(#15,#81);
#83=IFCSPHERE(#6,0.08);
#84=IFCCSGSOLID(#83);
#85=IFCSHAPEREPRESENTATION(#4,'Body','CSG',(#84));
#86=IFCPRODUCTDEFINITIONSHAPE($,$,(#85));
#87=IFCBLOCK(#6,0.2,0.2,0.2);
#88=IFCCSGSOLID(#87);
#89=IFCSHAPEREPRESENTATION(#4,'Body','CSG',(#88));
#90=IFCPRODUCTDEFINITIONSHAPE($,$,(#27));
#91=IFCPIPEFITTING('2Fitt00000000000000001',$,'Bend 1',$,$,#82,#90,'F-001',.BEND.);
#100=IFCDISTRIBUTIONPORT('2Port00000000000000001',$,'top',$,$,$,$,.SOURCE.,.PIPE.,.DRAINAGE.);
#101=IFCDISTRIBUTIONPORT('2Port00000000000000002',$,'in',$,$,$,$,.SINK.,.PIPE.,.DRAINAGE.);
#102=IFCRELNESTS('0Nst100000000000000001',$,$,$,#60,(#100));
#103=IFCRELNESTS('0Nst200000000000000001',$,$,$,#91,(#101));
#104=IFCRELCONNECTSPORTS('0Con100000000000000001',$,$,$,#100,#101,$);
#110=IFCDISTRIBUTIONSYSTEM('2Syst00000000000000001',$,'Waste water',$,$,$,.DRAINAGE.);
#111=IFCRELASSIGNSTOGROUP('0Grp100000000000000001',$,$,$,(#60,#91),$,#110);
#112=IFCRELSERVICESBUILDINGS('0Srv100000000000000001',$,$,$,#110,(#12));
#120=IFCCARTESIANPOINT((5.,0.,0.));
#121=IFCAXIS2PLACEMENT3D(#120,$,$);
#122=IFCLOCALPLACEMENT(#15,#121);
#123=IFCBLOCK(#6,4.,0.3,3.);
#124=IFCCSGSOLID(#123);
#125=IFCSHAPEREPRESENTATION(#4,'Body','CSG',(#124));
#126=IFCPRODUCTDEFINITIONSHAPE($,$,(#27));
#127=IFCWALL('2Wall00000000000000001',$,'Wall 1',$,$,#122,#126,$,.STANDARD.);
#130=IFCRELCONTAINEDINSPATIALSTRUCTURE('0Cnt100000000000000001',$,$,$,(#60,#91,#127),#14);
ENDSEC;
END-ISO-10303-21;
""";

    /// <summary>The same in IFC2X3: a flow segment with a port connected to it, a quantity of four attributes, a material of one.</summary>
    const string Mep2x3 = """
ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('mep2x3.ifc','2026-10-30T10:00:00',('x'),('x'),'probe','probe','');
FILE_SCHEMA(('IFC2X3'));
ENDSEC;
DATA;
#1=IFCPROJECT('0Proj00000000000000002',$,'Probe',$,$,$,$,(#4),#2);
#2=IFCUNITASSIGNMENT((#3));
#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#4=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#6,$);
#5=IFCCARTESIANPOINT((0.,0.,0.));
#6=IFCAXIS2PLACEMENT3D(#5,$,$);
#10=IFCSITE('0Site00000000000000002',$,'Site',$,$,#11,$,$,.ELEMENT.,$,$,$,$,$);
#11=IFCLOCALPLACEMENT($,#6);
#12=IFCBUILDING('0Bldg00000000000000002',$,'Building',$,$,#13,$,$,.ELEMENT.,$,$,$);
#13=IFCLOCALPLACEMENT(#11,#6);
#14=IFCBUILDINGSTOREY('0Stry00000000000000002',$,'Level 0',$,$,#15,$,$,.ELEMENT.,0.);
#15=IFCLOCALPLACEMENT(#13,#6);
#16=IFCRELAGGREGATES('0Agg100000000000000002',$,$,$,#1,(#10));
#17=IFCRELAGGREGATES('0Agg200000000000000002',$,$,$,#10,(#12));
#18=IFCRELAGGREGATES('0Agg300000000000000002',$,$,$,#12,(#14));
#21=IFCDIRECTION((0.,0.,1.));
#22=IFCCARTESIANPOINT((0.,0.));
#23=IFCAXIS2PLACEMENT2D(#22,$);
#24=IFCCIRCLEPROFILEDEF(.AREA.,$,#23,0.05);
#26=IFCEXTRUDEDAREASOLID(#24,#6,#21,2.);
#27=IFCSHAPEREPRESENTATION(#4,'Body','SweptSolid',(#26));
#28=IFCPRODUCTDEFINITIONSHAPE($,$,(#27));
#31=IFCLOCALPLACEMENT(#15,#6);
#60=IFCFLOWSEGMENT('2Flow00000000000000002',$,'Duct 1',$,'Duct',#31,#28,'D-1');
#72=IFCQUANTITYLENGTH('Length',$,$,2.);
#74=IFCELEMENTQUANTITY('0Qto100000000000000002',$,'BaseQuantities',$,$,(#72));
#75=IFCRELDEFINESBYPROPERTIES('0RQt100000000000000002',$,$,$,(#60),#74);
#76=IFCMATERIAL('Steel');
#77=IFCRELASSOCIATESMATERIAL('0RMat00000000000000002',$,$,$,(#60),#76);
#100=IFCDISTRIBUTIONPORT('2Port00000000000000003',$,'end',$,$,$,$,.SOURCE.);
#102=IFCRELCONNECTSPORTTOELEMENT('0Nst100000000000000002',$,$,$,#100,#60);
#130=IFCRELCONTAINEDINSPATIALSTRUCTURE('0Cnt100000000000000002',$,$,$,(#60),#14);
ENDSEC;
END-ISO-10303-21;
""";

    string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    string Export(Fixture f, string name)
    {
        var path = Path.Combine(_dir, name);
        IfcExport.Write(f.Session, path);
        return path;
    }

    static readonly IfcImportOptions MepOnly = new("auto", new HashSet<string> { "IfcPipeSegment", "IfcPipeFitting" });

    // ─── What a file says of its elements, written out to compare ─────────

    static string D(object? v) => v switch
    {
        null => "$",
        string s => "'" + s + "'",
        StepEnum e => "." + e.Value + ".",
        StepTyped t => t.Type + "(" + D(t.Value) + ")",
        StepRef => "#",
        List<object?> l => "(" + string.Join(",", l.Select(D)) + ")",
        double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString()!,
    };

    static IEnumerable<StepRef> Refs(object? o) => o is List<object?> l ? l.OfType<StepRef>() : o is StepRef r ? [r] : [];

    /// <summary>
    /// Every element of the classes asked for, by GlobalId: its attributes (not its placement or body), its
    /// sets with their ids and every value as typed, its type with its sets, material, ports with their
    /// connections, groups, and the GlobalId of each relationship. Lapin's intent set is left out.
    /// </summary>
    static string Dump(string path, params string[] classes)
    {
        var f = StepFile.Read(path);
        var sb = new StringBuilder();
        void Set(StepEntity set, string indent)
        {
            sb.AppendLine($"{indent}{set.Type} {D(set[0])} {D(set[2])} {D(set[3])}");
            foreach (var p in Refs(set.Type == "IFCPROPERTYSET" ? set[4] : set[5]).Select(x => f.Get(x)!))
                sb.AppendLine($"{indent}   {p.Type} {D(p[0])} {D(p[1])} = {D(p.Type.StartsWith("IFCQUANTITY") ? p[3] : p[2])}");
        }
        string? Rel(string type, Func<StepEntity, bool> on) => f.OfType(type).Where(on).Select(r => (string?)r[0]).FirstOrDefault();
        foreach (var e in classes.SelectMany(f.OfType).OrderBy(e => (string)e[0]!))
        {
            sb.AppendLine($"{e.Type} {string.Join(" ", e.Args.Select((a, i) => i is 1 or 5 or 6 ? "" : D(a)))}");
            foreach (var r in f.OfType("IFCRELDEFINESBYPROPERTIES").Where(r => Refs(r[4]).Any(x => x.Id == e.Id)).OrderBy(r => (string)r[0]!))
            {
                var set = f.Get(r[5])!;
                if (IfcExport.IsIntentSet((string?)set[2])) continue;
                sb.AppendLine($"   by {D(r[0])}");
                Set(set, "   ");
            }
            foreach (var r in f.OfType("IFCRELDEFINESBYTYPE").Where(r => Refs(r[4]).Any(x => x.Id == e.Id)))
            {
                var t = f.Get(r[5])!;
                sb.AppendLine($"   type by {D(r[0])}: {t.Type} {string.Join(" ", t.Args.Select((a, i) => i is 1 or 5 or 6 ? "" : D(a)))}");
                foreach (var s in Refs(t[5]).Select(x => f.Get(x)!)) Set(s, "      ");
            }
            foreach (var r in f.OfType("IFCRELASSOCIATESMATERIAL").Where(r => Refs(r[4]).Any(x => x.Id == e.Id)))
                sb.AppendLine($"   material by {D(r[0])}: {f.Get(r[5])!.Type} {D(f.Get(r[5])!.Args)}");
            var ports = f.OfType("IFCRELNESTS").Where(r => r[4] is StepRef x && x.Id == e.Id).SelectMany(r => Refs(r[5]).Select(p => (Rel: r[0], Port: f.Get(p)!)))
                .Concat(f.OfType("IFCRELCONNECTSPORTTOELEMENT").Where(r => r[5] is StepRef x && x.Id == e.Id).Select(r => (Rel: r[0], Port: f.Get(r[4])!)));
            foreach (var (rel, p) in ports)
            {
                sb.AppendLine($"   port by {D(rel)}: {D(p[0])} {D(p[2])} {D(p[7])} {D(p[8])} {D(p[9])}");
                foreach (var c in f.OfType("IFCRELCONNECTSPORTS").Where(c => c[4] is StepRef a && a.Id == p.Id))
                    sb.AppendLine($"      connected by {D(c[0])} to {D(f.Get(c[5])![0])}");
            }
            foreach (var r in f.OfType("IFCRELASSIGNSTOGROUP").Where(r => Refs(r[4]).Any(x => x.Id == e.Id)))
            {
                var g = f.Get(r[6])!;
                sb.AppendLine($"   group by {D(r[0])}: {g.Type} {D(g[0])} {D(g[2])} {D(g[g.Args.Count - 1])} serving by {Rel("IFCRELSERVICESBUILDINGS", s => s[4] is StepRef x && x.Id == g.Id) ?? "-"}");
            }
        }
        return sb.ToString();
    }

    // ─── The workflow ───────────────────────────────────────────────────────

    [Fact]
    public void The_MEP_of_a_file_imported_alone_and_exported_comes_out_as_it_came()
    {
        var src = Write("mep.ifc", Mep);
        var f = Make();
        var r = IfcImport.Run(f.Session, src, MepOnly, out _);
        Assert.Equal(2, r.References);
        Assert.DoesNotContain(f.Model.Entities, e => e.Name == "Wall 1");
        var path = Export(f, "out.ifc");

        var before = Dump(src, "IFCPIPESEGMENT", "IFCPIPEFITTING");
        Assert.Equal(before, Dump(path, "IFCPIPESEGMENT", "IFCPIPEFITTING"));
        // What the comparison holds: every digit and type, the type's own set, the ports' connection, the system serving the building.
        Assert.Contains("IFCPOSITIVELENGTHMEASURE(1.5E-06)", before);
        Assert.Contains("IFCPRESSUREMEASURE(600000.123456)", before);
        Assert.Contains("IFCBOOLEAN(.T.)", before);
        Assert.Contains("(IFCLABEL('A'),IFCLABEL('B'))", before);
        Assert.Contains("'Pset_PipeSegmentTypeCommon'", before);
        Assert.Contains("connected by '0Con100000000000000001' to '2Port00000000000000002'", before);
        Assert.Contains("serving by 0Srv100000000000000001", before);
        // The wall stayed out, and no GlobalId is written twice.
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("2Wall00000000000000001", text);
        var ids = System.Text.RegularExpressions.Regex.Matches(text, @"^#\d+=IFC\w+\('([0-9A-Za-z_$]{22})'", System.Text.RegularExpressions.RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // The intent set holds what Lapin needs, not the properties again as text.
        Assert.DoesNotContain("'properties'", text);
    }

    [Fact]
    public void Exported_imported_and_exported_again_nothing_is_lost()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write("mep.ifc", Mep), MepOnly, out _);
        var once = Export(f, "once.ifc");
        var g = Make();
        IfcImport.Run(g.Session, once, null, out _);
        var twice = Export(g, "twice.ifc");
        Assert.Equal(Dump(once, "IFCPIPESEGMENT", "IFCPIPEFITTING"), Dump(twice, "IFCPIPESEGMENT", "IFCPIPEFITTING"));
    }

    [Fact]
    public void Saved_and_opened_the_project_keeps_what_the_file_said()
    {
        var src = Write("mep.ifc", Mep);
        var f = Make();
        IfcImport.Run(f.Session, src, MepOnly, out _);
        var lapin = Path.Combine(_dir, "mep.lapin");
        PackageFiles.Save(f.Session, lapin);
        var g = Make();
        PackageFiles.Open(g.Session, lapin);
        Assert.Equal(Dump(src, "IFCPIPESEGMENT", "IFCPIPEFITTING"), Dump(Export(g, "opened.ifc"), "IFCPIPESEGMENT", "IFCPIPEFITTING"));
    }

    [Fact]
    public void A_value_the_user_set_in_a_files_set_goes_into_that_set()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write("mep.ifc", Mep), MepOnly, out _);
        var pipe = f.Model.Get("2Pipe00000000000000001")!;
        var attrs = pipe.CloneAttrs();
        attrs["params"] = new JsonArray(
            new JsonObject { ["name"] = "Cost", ["kind"] = "number", ["value"] = 14, ["pset"] = "Company_Data" },
            new JsonObject { ["name"] = "Supplier", ["kind"] = "text", ["value"] = "Acme", ["pset"] = "Company_Data" });
        f.Session.Edit("params", e => e.Replace(pipe.With(attrs: attrs)));
        var dump = Dump(Export(f, "edited.ifc"), "IFCPIPESEGMENT");
        // One Company_Data, its id kept: the user's value over the file's, theirs added, the file's others as they were.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(dump, "'Company_Data'"));
        Assert.Contains("IFCPROPERTYSET '0Pst200000000000000001' 'Company_Data'", dump);
        Assert.Contains("'Cost' $ = IFCREAL(14)", dump);
        Assert.Contains("'Supplier' $ = IFCTEXT('Acme')", dump);
        Assert.Contains("'TestPressure' $ = IFCPRESSUREMEASURE(600000.123456)", dump);
    }

    [Fact]
    public void An_element_carried_twice_gets_ids_of_its_own_for_what_it_carries()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write("mep.ifc", Mep), MepOnly, out _);
        var pipe = f.Model.Get("2Pipe00000000000000001")!;
        var storey = Spatial.StoreyOf(f.Model, pipe.Id)!.Id;
        // A twin with the same carried IFC (as a copy would have) and a set of its own content under the same id.
        var attrs = pipe.CloneAttrs();
        var notes = ((JsonObject)attrs[IfcSource.Key]!)["defs"]!.AsArray()[1]!["d"]!["a"]!.AsArray()[4]!.AsArray()[2]!["a"]!.AsArray();
        notes[2] = "IFCTEXT('Changed')";
        f.Session.Edit("twin", e =>
        {
            var twin = new Entity(e.NewId(), pipe.Class, "Pipe 2", attrs);
            e.Add(twin);
            e.Relate(IfcClass.RelContainedInSpatialStructure, storey, twin.Id);
        });
        var text = File.ReadAllText(Export(f, "twins.ifc"));
        var ids = System.Text.RegularExpressions.Regex.Matches(text, @"^#\d+=IFC\w+\('([0-9A-Za-z_$]{22})'", System.Text.RegularExpressions.RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains("IFCTEXT('Changed')", text);
        Assert.Contains("'0Pst200000000000000001'", text);
    }

    [Fact]
    public void From_IFC2X3_what_IFC4_describes_more_fully_is_padded_and_a_port_is_nested()
    {
        var src = Write("mep2x3.ifc", Mep2x3);
        var f = Make();
        IfcImport.Run(f.Session, src, null, out _);
        var text = File.ReadAllText(Export(f, "from2x3.ifc"));
        Assert.Contains("=IFCFLOWSEGMENT('2Flow00000000000000002',$,'Duct 1',$,'Duct',", text);
        Assert.Contains("=IFCQUANTITYLENGTH('Length',$,$,2.,$);", text);
        Assert.Contains("=IFCMATERIAL('Steel',$,$);", text);
        Assert.Contains("=IFCDISTRIBUTIONPORT('2Port00000000000000003',$,'end',$,$,$,$,.SOURCE.,$,$);", text);
        Assert.Matches(@"=IFCRELNESTS\('0Nst100000000000000002',\$,\$,\$,#\d+,\(#\d+\)\);", text);
        Assert.Contains("'0RQt100000000000000002'", text);
        Assert.Contains("'0RMat00000000000000002'", text);
    }

    [Fact]
    public void A_foreign_class_keeps_its_name_and_a_small_value_is_shown_as_it_is()
    {
        Assert.Equal("IfcPipeFitting", IfcModel.ClassName("IFCPIPEFITTING"));
        Assert.Equal("IfcPipeFittingType", IfcModel.ClassName("IFCPIPEFITTINGTYPE"));
        Assert.Equal("IfcAirTerminalBox", IfcModel.ClassName("IFCAIRTERMINALBOX"));
        var f = Make();
        IfcImport.Run(f.Session, Write("mep.ifc", Mep), MepOnly, out _);
        var props = (JsonObject)f.Model.Get("2Pipe00000000000000001")!.Attrs["properties"]!;
        Assert.Equal("1.5E-06", (string?)props["Pset_PipeSegmentOccurrence"]!["InteriorRoughnessCoefficient"]);
        Assert.Equal("IfcPipeFitting", f.Model.Get("2Fitt00000000000000001")!.Class);
    }

    [Fact]
    public void An_element_without_a_body_lapin_reads_is_told_not_dropped_silently()
    {
        var text = Mep.Replace("#90=IFCPRODUCTDEFINITIONSHAPE($,$,(#27));", "#90=IFCPRODUCTDEFINITIONSHAPE($,$,(#89));");
        var f = Make();
        var r = IfcImport.Run(f.Session, Write("csg.ifc", text), MepOnly, out _);
        Assert.Contains(r.Notes, n => n.Contains("1 × IfcPipeFitting left out"));
    }
}
