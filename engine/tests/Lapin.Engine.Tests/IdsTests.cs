using Lapin.Ids;
using Lapin.Ifc;

namespace Lapin.Engine.Tests;

/// <summary>
/// IDS 1.0 (docs/ids.md): the reader and the checker against cases written from buildingSMART's IDS user manual (its facet tables,
/// restrictions, cardinality and tolerance pages) and from what the names of its implementers' test cases state, each case its own
/// small IFC file and IDS here. No file of the test suite is copied: the cases are the behaviour, the files are written anew.
/// </summary>
public class IdsTests
{
    // ------------------------------------------------------------ helpers

    const string G1 = "1hqIFTRjfV6AWq_bMtnZwI", G2 = "3Agm079vPIYBL4JExVrhD5", G3 = "2nJrDaLQfJ1QPhdJR0o97J", G4 = "16MocU_IDOF8_x3Iqllz0d",
        G5 = "1xdwj8qGXK4hzoNbvMdXJW", G6 = "05rScmOVzMoQXOfbYdtLYj", G7 = "0Kq0jE3x5BpQ8i0nKoV1cZ", G8 = "3vV9Xz5sjFzw0jbbLz0T6Q", G9 = "2c3AhQe7n8cRZ2yMoN6kqT";

    static string Step(string schema, params string[] data) =>
        $"ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'2;1');\nFILE_NAME('t.ifc','',(''),(''),'','','');\nFILE_SCHEMA(('{schema}'));\nENDSEC;\nDATA;\n{string.Join("\n", data)}\nENDSEC;\nEND-ISO-10303-21;\n";

    static string Ids(params string[] specs) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ids xmlns=\"http://standards.buildingsmart.org/IDS\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">\n" +
        "<info><title>Test</title></info>\n<specifications>" + string.Join("", specs) + "</specifications></ids>";

    const string Required = "minOccurs=\"1\" maxOccurs=\"unbounded\"", Optional = "minOccurs=\"0\" maxOccurs=\"unbounded\"", Prohibited = "minOccurs=\"0\" maxOccurs=\"0\"";

    static string Spec(string app, string req = "", string occurs = Required, string versions = "IFC2X3 IFC4") =>
        $"<specification name=\"S\" ifcVersion=\"{versions}\"><applicability {occurs}>{app}</applicability>{(req.Length > 0 ? $"<requirements>{req}</requirements>" : "")}</specification>";

    static string V(string s) => s.StartsWith('<') ? s : $"<simpleValue>{s}</simpleValue>";
    static string R(string b, string facets) => $"<xs:restriction base=\"xs:{b}\">{facets}</xs:restriction>";
    static string En(params string[] values) => string.Concat(values.Select(v => $"<xs:enumeration value=\"{v}\"/>"));
    static string Pat(string p) => $"<xs:pattern value=\"{p}\"/>";

    static string Entity(string name, string? pre = null) => $"<entity><name>{V(name)}</name>{(pre != null ? $"<predefinedType>{V(pre)}</predefinedType>" : "")}</entity>";
    static string Attr(string name, string? value = null, string? card = null) =>
        $"<attribute{(card != null ? $" cardinality=\"{card}\"" : "")}><name>{V(name)}</name>{(value != null ? $"<value>{V(value)}</value>" : "")}</attribute>";
    static string Prop(string set, string name, string? dataType = null, string? value = null, string? card = null) =>
        $"<property{(dataType != null ? $" dataType=\"{dataType}\"" : "")}{(card != null ? $" cardinality=\"{card}\"" : "")}><propertySet>{V(set)}</propertySet><baseName>{V(name)}</baseName>{(value != null ? $"<value>{V(value)}</value>" : "")}</property>";
    static string Class(string system, string? value = null, string? card = null) =>
        $"<classification{(card != null ? $" cardinality=\"{card}\"" : "")}>{(value != null ? $"<value>{V(value)}</value>" : "")}<system>{V(system)}</system></classification>";
    static string Mat(string? value = null, string? card = null) => $"<material{(card != null ? $" cardinality=\"{card}\"" : "")}>{(value != null ? $"<value>{V(value)}</value>" : "")}</material>";
    static string PartOf(string entity, string? relation = null, string? card = null, string? pre = null) =>
        $"<partOf{(relation != null ? $" relation=\"{relation}\"" : "")}{(card != null ? $" cardinality=\"{card}\"" : "")}>{Entity(entity, pre)}</partOf>";

    const string Wall = "IFCWALL";
    static string WallApp => Entity(Wall);

    static IdsSpecResult Run(string ids, params string[] data) => RunIn("IFC4", ids, data);
    static IdsSpecResult RunIn(string schema, string ids, params string[] data) => IdsChecker.Run(StepFile.Parse(Step(schema, data)), IdsReader.Read(ids)).Specifications[0];
    static string Status(string ids, params string[] data) => Run(ids, data).Status;

    static string AWall(string name = "'W1'", string pre = ".STANDARD.", string gid = G1, int id = 1, string objectType = "$", string description = "$") =>
        $"#{id}=IFCWALL('{gid}',$,{name},{description},{objectType},$,$,$,{pre});";

    // ------------------------------------------------------------ specifications: cardinality and versions

    [Fact]
    public void A_required_specification_needs_one_applicable_entity_and_an_optional_one_none()
    {
        Assert.Equal("pass", Status(Ids(Spec(WallApp)), AWall()));
        var none = Run(Ids(Spec(WallApp)), "#1=IFCSLAB('" + G1 + "',$,$,$,$,$,$,$,$);");
        Assert.Equal("fail", none.Status);
        Assert.Contains("nothing", none.Message);
        Assert.Equal("pass", Status(Ids(Spec(WallApp, occurs: Optional)), "#1=IFCSLAB('" + G1 + "',$,$,$,$,$,$,$,$);"));
        // Optional and a facet optional together: nothing applicable, nothing to fail.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", card: "optional"), Optional)), "#1=IFCSLAB('" + G1 + "',$,$,$,$,$,$,$,$);"));
    }

    [Fact]
    public void A_prohibited_specification_fails_when_anything_matches_and_may_have_no_requirements()
    {
        Assert.Equal("fail", Status(Ids(Spec(WallApp, occurs: Prohibited)), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, occurs: Prohibited)), "#1=IFCSLAB('" + G1 + "',$,$,$,$,$,$,$,$);"));
        var ex = Assert.Throws<IdsFormatException>(() => IdsReader.Read(Ids(Spec(WallApp, Attr("Name"), Prohibited))));
        Assert.Contains("prohibited", ex.Message);
    }

    [Fact]
    public void A_specification_passes_only_if_every_requirement_passes_and_its_version_is_metadata_alone()
    {
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Name") + Attr("Description"))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("Name") + Attr("Description"))), AWall(description: "'D'")));
        // A specification for IFC2X3 alone is not run on an IFC4 file.
        var skipped = Run(Ids(Spec(WallApp, versions: "IFC2X3")), AWall());
        Assert.Equal("skipped", skipped.Status);
        // The IDS's own version is metadata only.
        var withVersion = Ids(Spec(WallApp)).Replace("<title>Test</title>", "<title>Test</title><version>9.9</version>");
        Assert.Equal("pass", Status(withVersion, AWall()));
    }

    // ------------------------------------------------------------ entity

    [Fact]
    public void Entity_names_match_exactly_in_upper_case_with_no_subclasses()
    {
        Assert.Equal("fail", Status(Ids(Spec(Entity("IfcWall"))), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCBUILDINGELEMENT"))), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(WallApp)), $"#1=IFCWALLSTANDARDCASE('{G1}',$,$,$,$,$,$,$,$);"));
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCFOOBAR"))), AWall()));
        // A restriction may name several, or a pattern.
        Assert.Equal("pass", Status(Ids(Spec(Entity(R("string", En("IFCSLAB", "IFCWALL"))))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(Entity(R("string", Pat("IFC.*")))) ), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(Entity(R("string", Pat("IFCSL.*"))))), AWall()));
    }

    [Fact]
    public void Predefined_types_match_exactly_and_are_inherited_from_and_overridden_by_the_type()
    {
        string Req(string pre) => Ids(Spec(WallApp, Entity(Wall, pre)));
        Assert.Equal("pass", Status(Req("STANDARD"), AWall()));
        Assert.Equal("fail", Status(Req("SHEAR"), AWall()));
        Assert.Equal("fail", Status(Req("standard"), AWall()));
        Assert.Equal("fail", Status(Req("STANDARD"), AWall(pre: "$")));
        // USERDEFINED: the object's ObjectType names it; both USERDEFINED and the name meet it, case-sensitively.
        Assert.Equal("pass", Status(Req("FOO"), AWall(pre: ".USERDEFINED.", objectType: "'FOO'")));
        Assert.Equal("pass", Status(Req("USERDEFINED"), AWall(pre: ".USERDEFINED.", objectType: "'FOO'")));
        Assert.Equal("fail", Status(Req("foo"), AWall(pre: ".USERDEFINED.", objectType: "'FOO'")));
        // The type's predefined type wins over the occurrence's.
        string[] typed =
        [
            AWall(pre: ".NOTDEFINED."),
            $"#2=IFCWALLTYPE('{G2}',$,'T',$,$,$,$,$,$,.SHEAR.);",
            $"#3=IFCRELDEFINESBYTYPE('{G3}',$,$,$,(#1),#2);",
        ];
        Assert.Equal("pass", Status(Req("SHEAR"), typed));
        Assert.Equal("fail", Status(Req("NOTDEFINED"), typed));
        // A user-defined type's ElementType names it.
        string[] userType = [AWall(pre: "$"), $"#2=IFCWALLTYPE('{G2}',$,'T',$,$,$,$,$,'BAR',.USERDEFINED.);", $"#3=IFCRELDEFINESBYTYPE('{G3}',$,$,$,(#1),#2);"];
        Assert.Equal("pass", Status(Req("BAR"), userType));
        // A restriction on the predefined type.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Entity(Wall, R("string", En("SHEAR", "STANDARD"))))), AWall()));
        // A process type's predefined type.
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCTASK"), Entity("IFCTASK", "FOO"))),
            $"#1=IFCTASK('{G1}',$,'T',$,'FOO',$,$,$,$,.F.,$,$,.USERDEFINED.);"));
    }

    // ------------------------------------------------------------ attribute

    [Fact]
    public void Attributes_are_required_filled_in_and_compared_case_sensitively()
    {
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("Name"))), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Name"))), AWall(name: "$")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Name"))), AWall(name: "''")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("Name", "W1"))), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Name", "w1"))), AWall()));
        // An attribute IFC does not have, an inverse or a derived one: never met.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Foobar"))), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("IsDefinedBy"))), AWall()));
        // GlobalIds are texts; non-ASCII texts as they are.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("GlobalId", G1))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("Name", "Perete exterior ăîșț"))), AWall(name: "'Perete exterior \\X2\\0103\\X0\\\\X2\\00EE\\X0\\\\X2\\0219\\X0\\\\X2\\021B\\X0\\'")));
        // A restriction on the name matches any attribute it names.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr(R("string", Pat("Na.*")), "W1"))), AWall()));
    }

    [Fact]
    public void Optional_and_prohibited_attributes()
    {
        var optional = Ids(Spec(WallApp, Attr("Name", "Foobar", "optional")));
        Assert.Equal("pass", Status(optional, AWall(name: "$")));
        Assert.Equal("fail", Status(optional, AWall(name: "''")));
        Assert.Equal("pass", Status(optional, AWall(name: "'Foobar'")));
        Assert.Equal("fail", Status(optional, AWall(name: "'Other'")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Name", card: "prohibited"))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("Name", card: "prohibited"))), AWall(name: "$")));
        // Prohibited with a value: that value must not be it; nothing at all is fine.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Name", "W1", "prohibited"))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("Name", "W2", "prohibited"))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("Name", "W1", "prohibited"))), AWall(name: "$")));
    }

    [Fact]
    public void Attribute_values_are_type_cast_numbers_in_SI_booleans_in_lower_case_and_objects_have_no_value()
    {
        string Door(string h, string units = "") => $"#1=IFCDOOR('{G1}',$,'D',$,$,$,$,$,{h},0.9,.DOOR.,$,$);" + units;
        string[] mm =
        [
            Door("2000."),
            $"#10=IFCPROJECT('{G9}',$,'P',$,$,$,$,$,#11);", "#11=IFCUNITASSIGNMENT((#12));", "#12=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);",
        ];
        var app = Entity("IFCDOOR");
        Assert.Equal("pass", Status(Ids(Spec(app, Attr("OverallHeight", "2"))), Door("2.")));
        Assert.Equal("pass", Status(Ids(Spec(app, Attr("OverallHeight", "2.0000001"))), Door("2.")));
        Assert.Equal("fail", Status(Ids(Spec(app, Attr("OverallHeight", "2.1"))), Door("2.")));
        // In millimetres in the file, in metres in the IDS.
        Assert.Equal("pass", Status(Ids(Spec(app, Attr("OverallHeight", "2"))), mm));
        Assert.Equal("pass", Status(Ids(Spec(app, Attr("OverallHeight", R("double", "<xs:minInclusive value=\"1.9\"/><xs:maxInclusive value=\"2.1\"/>")))), mm));
        // Zero is a value.
        Assert.Equal("pass", Status(Ids(Spec(app, Attr("OverallHeight", "0"))), Door("0.")));
        // Whole numbers are whole numbers.
        string Flight(string n) => $"#1=IFCSTAIRFLIGHT('{G1}',$,'F',$,$,$,$,$,{n},10,0.17,0.28,.STRAIGHT.);";
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCSTAIRFLIGHT"), Attr("NumberOfRisers", "11"))), Flight("11")));
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCSTAIRFLIGHT"), Attr("NumberOfRisers", "11.0"))), Flight("11")));
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCSTAIRFLIGHT"), Attr("NumberOfRisers", R("string", En("10", "11"))))), Flight("11")));
        // Booleans: true and false, never TRUE.
        string DoorType(string b) => $"#1=IFCDOORTYPE('{G1}',$,'DT',$,$,$,$,$,$,.DOOR.,.SINGLE_SWING_LEFT.,{b},$);";
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCDOORTYPE"), Attr("ParameterTakesPrecedence", "true"))), DoorType(".T.")));
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCDOORTYPE"), Attr("ParameterTakesPrecedence", "false"))), DoorType(".F.")));
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCDOORTYPE"), Attr("ParameterTakesPrecedence", "TRUE"))), DoorType(".T.")));
        // A logical unknown is no value.
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCDOORTYPE"), Attr("ParameterTakesPrecedence"))), DoorType(".U.")));
        // An attribute referring to an object is filled in, but has no value to compare.
        string[] placed = [$"#1=IFCWALL('{G1}',$,'W',$,$,#2,$,$,$);", "#2=IFCLOCALPLACEMENT($,$);"];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Attr("ObjectPlacement"))), placed));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("ObjectPlacement", "#2"))), placed));
    }

    [Fact]
    public void Attributes_are_not_inherited_from_the_type_and_dates_are_texts()
    {
        string[] typed = [AWall(), $"#2=IFCWALLTYPE('{G2}',$,'T','Typed',$,$,$,$,$,.SHEAR.);", $"#3=IFCRELDEFINESBYTYPE('{G3}',$,$,$,(#1),#2);"];
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Attr("Description"))), typed));
        // A date as the file writes it: equal as text, a pattern as text.
        string Task(string d) => $"#1=IFCWORKPLAN('{G1}',$,'P',$,$,$,'{d}',$,$,$,$,$,$,.ACTUAL.);";
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCWORKPLAN"), Attr("CreationDate", "2022-01-01"))), Task("2022-01-01")));
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCWORKPLAN"), Attr("CreationDate", "2022-01-01"))), Task("2022-01-01T00:00:00")));
    }

    // ------------------------------------------------------------ classification

    static string[] Classified(string system, string code, string? parentCode = null, bool onType = false)
    {
        var o = new List<string> { AWall(), $"#2=IFCCLASSIFICATION($,$,$,'{system}',$,$,$);" };
        if (parentCode != null)
        {
            o.Add($"#3=IFCCLASSIFICATIONREFERENCE($,'{parentCode}',$,#2,$,$);");
            o.Add($"#4=IFCCLASSIFICATIONREFERENCE($,'{code}',$,#3,$,$);");
        }
        else o.Add($"#4=IFCCLASSIFICATIONREFERENCE($,'{code}',$,#2,$,$);");
        if (onType)
        {
            o.Add($"#6=IFCWALLTYPE('{G2}',$,'T',$,$,$,$,$,$,.SHEAR.);");
            o.Add($"#7=IFCRELDEFINESBYTYPE('{G3}',$,$,$,(#1),#6);");
            o.Add($"#5=IFCRELASSOCIATESCLASSIFICATION('{G4}',$,$,$,(#6),#4);");
        }
        else o.Add($"#5=IFCRELASSOCIATESCLASSIFICATION('{G4}',$,$,$,(#1),#4);");
        return [.. o];
    }

    [Fact]
    public void Classifications_match_their_system_exactly_and_a_code_by_its_references_above_it()
    {
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class("Uniclass 2015"))), Classified("Uniclass 2015", "EF_25_10_25")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("uniclass 2015"))), Classified("Uniclass 2015", "EF_25_10_25")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("Uniclass"))), Classified("Uniclass 2015", "EF_25_10_25")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class("Uniclass 2015", "EF_25_10_25"))), Classified("Uniclass 2015", "EF_25_10_25")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("Uniclass 2015", "EF_25_10"))), Classified("Uniclass 2015", "EF_25_10_25")));
        // A full classification: the code under EF_25_10 meets EF_25_10.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class("Uniclass 2015", "EF_25_10"))), Classified("Uniclass 2015", "EF_25_10_25", "EF_25_10")));
        // System and value both, from the same reference.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("Other", "EF_25_10_25"))), Classified("Uniclass 2015", "EF_25_10_25")));
        // Restrictions on either.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class(R("string", Pat("Uniclass.*")), R("string", Pat("EF_25_.*"))))), Classified("Uniclass 2015", "EF_25_10_25")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class(R("string", Pat("Uniclass.*")), R("string", Pat("Pr_.*"))))), Classified("Uniclass 2015", "EF_25_10_25")));
        // From the type, and an element without one fails.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class("Uniclass 2015", "EF_25_10_25"))), Classified("Uniclass 2015", "EF_25_10_25", onType: true)));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("Uniclass 2015"))), AWall()));
    }

    [Fact]
    public void An_occurrence_overrides_its_type_classification_per_system()
    {
        string[] both =
        [
            AWall(), $"#2=IFCCLASSIFICATION($,$,$,'Uniclass 2015',$,$,$);",
            $"#3=IFCCLASSIFICATIONREFERENCE($,'EF_OWN',$,#2,$,$);", $"#4=IFCCLASSIFICATIONREFERENCE($,'EF_TYPE',$,#2,$,$);",
            $"#6=IFCWALLTYPE('{G2}',$,'T',$,$,$,$,$,$,.SHEAR.);", $"#7=IFCRELDEFINESBYTYPE('{G3}',$,$,$,(#1),#6);",
            $"#5=IFCRELASSOCIATESCLASSIFICATION('{G4}',$,$,$,(#1),#3);", $"#8=IFCRELASSOCIATESCLASSIFICATION('{G5}',$,$,$,(#6),#4);",
        ];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class("Uniclass 2015", "EF_OWN"))), both));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("Uniclass 2015", "EF_TYPE"))), both));
    }

    [Fact]
    public void Optional_and_prohibited_classifications()
    {
        var optional = Ids(Spec(WallApp, Class(R("string", Pat("[A-Za-z0-9 ]+")), "ExpectedValue", "optional")));
        Assert.Equal("pass", Status(optional, AWall()));
        Assert.Equal("fail", Status(optional, Classified("Uniclass", "Other")));
        Assert.Equal("pass", Status(optional, Classified("Uniclass", "ExpectedValue")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("Uniclass", card: "prohibited"))), Classified("Uniclass", "X")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class("Uniclass", card: "prohibited"))), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Class("Uniclass", "X", "prohibited"))), Classified("Uniclass", "X")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Class("Uniclass", "Y", "prohibited"))), Classified("Uniclass", "X")));
    }

    // ------------------------------------------------------------ material

    static string[] WithMaterial(string material, params string[] more) =>
        [AWall(), .. more, $"#20=IFCRELASSOCIATESMATERIAL('{G5}',$,$,$,(#1),{material});"];

    [Fact]
    public void Materials_by_name_category_layer_profile_constituent_and_list()
    {
        var concrete = Ids(Spec(WallApp, Mat("Concrete")));
        Assert.Equal("pass", Status(concrete, WithMaterial("#2", "#2=IFCMATERIAL('Concrete',$,$);")));
        Assert.Equal("fail", Status(concrete, WithMaterial("#2", "#2=IFCMATERIAL('C25/30',$,$);")));
        Assert.Equal("pass", Status(concrete, WithMaterial("#2", "#2=IFCMATERIAL('C25/30',$,'Concrete');")));
        Assert.Equal("fail", Status(concrete, WithMaterial("#2", "#2=IFCMATERIAL('concrete',$,$);")));
        // A layer set: its name, its layers' names and categories, their materials'.
        string[] layered = WithMaterial("#5",
            "#2=IFCMATERIAL('Brick',$,$);", "#3=IFCMATERIAL('Gypsum',$,'Board');",
            "#6=IFCMATERIALLAYER(#2,0.25,$,'Masonry',$,'Structure',$);", "#7=IFCMATERIALLAYER(#3,0.0125,$,$,$,$,$);",
            "#4=IFCMATERIALLAYERSET((#6,#7),'Wall 250',$);", "#5=IFCMATERIALLAYERSETUSAGE(#4,.AXIS2.,.POSITIVE.,0.,$);");
        foreach (var name in new[] { "Brick", "Gypsum", "Board", "Wall 250", "Masonry", "Structure" })
            Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat(name))), layered));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Mat("Insulation"))), layered));
        // A profile set, a constituent set, a list.
        string[] profiles = WithMaterial("#4", "#2=IFCMATERIAL('S355',$,'Steel');", "#3=IFCMATERIALPROFILE('HEA 200',$,#2,$,$,'Primary');", "#4=IFCMATERIALPROFILESET('Column',$,(#3),$);");
        foreach (var name in new[] { "S355", "Steel", "HEA 200", "Primary", "Column" }) Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat(name))), profiles));
        string[] constituents = WithMaterial("#4", "#2=IFCMATERIAL('Glass',$,$);", "#3=IFCMATERIALCONSTITUENT('Glazing',$,#2,$,'Pane');", "#4=IFCMATERIALCONSTITUENTSET('Window',$,(#3));");
        foreach (var name in new[] { "Glass", "Glazing", "Pane", "Window" }) Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat(name))), constituents));
        string[] list = WithMaterial("#4", "#2=IFCMATERIAL('Oak',$,'Wood');", "#3=IFCMATERIAL('Steel',$,$);", "#4=IFCMATERIALLIST((#2,#3));");
        foreach (var name in new[] { "Oak", "Wood", "Steel" }) Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat(name))), list));
        // A set with nothing named fails a value, passes "any material".
        Assert.Equal("fail", Status(concrete, WithMaterial("#4", "#4=IFCMATERIALCONSTITUENTSET($,$,$);")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat())), WithMaterial("#4", "#4=IFCMATERIALCONSTITUENTSET($,$,$);")));
        // No material at all.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Mat())), AWall()));
    }

    [Fact]
    public void Materials_come_from_the_type_unless_the_occurrence_has_its_own_and_optional_and_prohibited()
    {
        string[] typed = [AWall(), "#2=IFCMATERIAL('Timber',$,$);", $"#6=IFCWALLTYPE('{G2}',$,'T',$,$,$,$,$,$,.SHEAR.);", $"#7=IFCRELDEFINESBYTYPE('{G3}',$,$,$,(#1),#6);", $"#8=IFCRELASSOCIATESMATERIAL('{G5}',$,$,$,(#6),#2);"];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat("Timber"))), typed));
        string[] overridden = [.. typed, "#9=IFCMATERIAL('Concrete',$,$);", $"#10=IFCRELASSOCIATESMATERIAL('{G6}',$,$,$,(#1),#9);"];
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Mat("Timber"))), overridden));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat("Concrete"))), overridden));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat("Concrete", "optional"))), AWall()));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Mat("Concrete", "optional"))), typed));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Mat(card: "prohibited"))), typed));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat(card: "prohibited"))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Mat("Concrete", "prohibited"))), typed));
    }

    // ------------------------------------------------------------ property

    static string[] WithProps(params string[] props)
    {
        var refs = string.Join(",", Enumerable.Range(0, props.Length).Select(i => $"#{10 + i}"));
        return [AWall(), $"#2=IFCPROPERTYSET('{G4}',$,'Foo_Bar',$,({refs}));", $"#3=IFCRELDEFINESBYPROPERTIES('{G5}',$,$,$,(#1),#2);",
            .. props.Select((p, i) => $"#{10 + i}={p};")];
    }

    [Fact]
    public void A_property_needs_its_set_its_name_and_a_value_and_meets_its_type_and_value()
    {
        var label = WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('Bar'),$)");
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Fob"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Baz", "Foo"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo"))), AWall()));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCTEXT"))), label));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "Bar"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "bar"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "Baz"))), label));
        // Empty, null and unknown are no value; false is one.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,$,$)")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL(''),$)")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCLOGICAL(.U.),$)")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCBOOLEAN(.F.),$)")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCBOOLEAN", "false"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCBOOLEAN(.F.),$)")));
        // A number written as a label is a text.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "42"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('42'),$)")));
        // Numbers by type: integers whole, reals within tolerance; a measure's own type.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCINTEGER", "42"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCINTEGER(42),$)")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", "42"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCREAL(42.),$)")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", "42.000001"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCREAL(42.),$)")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", "42.1"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCREAL(42.),$)")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLENGTHMEASURE", "42"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCREAL(42.),$)")));
        // Durations and dates are texts.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCDURATION", "PT0S"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCDURATION('PT0S'),$)")));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCDURATION", "PT0S"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCDURATION('P0D'),$)")));
        // Complex and reference properties are not read.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo"))), WithProps("IFCCOMPLEXPROPERTY('Foo',$,'U',())")));
    }

    [Fact]
    public void Every_matching_property_must_meet_the_facet()
    {
        string[] two =
        [
            AWall(),
            $"#2=IFCPROPERTYSET('{G4}',$,'Foo_Bar',$,(#10));", $"#3=IFCRELDEFINESBYPROPERTIES('{G5}',$,$,$,(#1),#2);",
            $"#4=IFCPROPERTYSET('{G6}',$,'Foo_Baz',$,(#11));", $"#5=IFCRELDEFINESBYPROPERTIES('{G7}',$,$,$,(#1),#4);",
            "#10=IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('Bar'),$);", "#11=IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('Baz'),$);",
        ];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop(R("string", Pat("Foo_.*")), "Foo", "IFCLABEL"))), two));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop(R("string", Pat("Foo_.*")), "Foo", "IFCLABEL", "Bar"))), two));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop(R("string", Pat("Foo_.*")), "Foo", "IFCLABEL", R("string", En("Bar", "Baz"))))), two));
    }

    [Fact]
    public void Properties_come_from_the_type_and_the_occurrence_overrides_them()
    {
        string[] typed =
        [
            AWall(), $"#6=IFCWALLTYPE('{G2}',$,'T',$,$,(#2),$,$,$,.SHEAR.);", $"#7=IFCRELDEFINESBYTYPE('{G3}',$,$,$,(#1),#6);",
            $"#2=IFCPROPERTYSET('{G4}',$,'Foo_Bar',$,(#10));", "#10=IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('FromType'),$);",
        ];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "FromType"))), typed));
        string[] overridden = [.. typed, $"#4=IFCPROPERTYSET('{G6}',$,'Foo_Bar',$,(#11));", $"#5=IFCRELDEFINESBYPROPERTIES('{G7}',$,$,$,(#1),#4);", "#11=IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('Own'),$);"];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "Own"))), overridden));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "FromType"))), overridden));
        // The type object itself has its properties too.
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCWALLTYPE"), Prop("Foo_Bar", "Foo"))), typed));
    }

    [Fact]
    public void Quantities_are_properties_of_their_measure_in_SI_units()
    {
        string[] q =
        [
            AWall(), $"#2=IFCELEMENTQUANTITY('{G4}',$,'Qto_WallBaseQuantities',$,$,(#10,#11));", $"#3=IFCRELDEFINESBYPROPERTIES('{G5}',$,$,$,(#1),#2);",
            "#10=IFCQUANTITYLENGTH('Length',$,$,3000.,$);", "#11=IFCQUANTITYAREA('GrossSideArea',$,$,9000000.,$);",
            $"#20=IFCPROJECT('{G9}',$,'P',$,$,$,$,$,#21);", "#21=IFCUNITASSIGNMENT((#22,#23));",
            "#22=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);", "#23=IFCSIUNIT(*,.AREAUNIT.,.MILLI.,.SQUARE_METRE.);",
        ];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Qto_WallBaseQuantities", "Length"))), q));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Qto_WallBaseQuantities", "Length", "IFCLENGTHMEASURE", "3"))), q));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Qto_WallBaseQuantities", "Length", "IFCLENGTHMEASURE", "3000"))), q));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Qto_WallBaseQuantities", "Length", "IFCAREAMEASURE"))), q));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Qto_WallBaseQuantities", "GrossSideArea", "IFCAREAMEASURE", "9"))), q));
        // A property's measure converted the same way; the property's own unit wins over the project's.
        string[] p = [.. q[..2].Take(1), .. WithProps("IFCPROPERTYSINGLEVALUE('Width',$,IFCLENGTHMEASURE(250.),$)").Skip(1), .. q[5..]];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Width", "IFCLENGTHMEASURE", "0.25"))), p));
        string[] own = [.. q[..2].Take(1), .. WithProps("IFCPROPERTYSINGLEVALUE('Width',$,IFCLENGTHMEASURE(25.),#30)").Skip(1), .. q[5..], "#30=IFCSIUNIT(*,.LENGTHUNIT.,.CENTI.,.METRE.);"];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Width", "IFCLENGTHMEASURE", "0.25"))), own));
    }

    [Fact]
    public void Enumerated_list_table_and_bounded_values()
    {
        var enumerated = WithProps("IFCPROPERTYENUMERATEDVALUE('Foo',$,(IFCLABEL('A'),IFCLABEL('B')),$)");
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "A"))), enumerated));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "B"))), enumerated));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", "C"))), enumerated));
        var list = WithProps("IFCPROPERTYLISTVALUE('Foo',$,(IFCLENGTHMEASURE(1.),IFCLENGTHMEASURE(2.)),$)");
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLENGTHMEASURE", "2"))), list));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLENGTHMEASURE", "3"))), list));
        // A range: every value within it.
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLENGTHMEASURE", R("double", "<xs:minInclusive value=\"1\"/><xs:maxInclusive value=\"2\"/>")))), list));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLENGTHMEASURE", R("double", "<xs:minInclusive value=\"1.5\"/>")))), list));
        var table = WithProps("IFCPROPERTYTABLEVALUE('Foo',$,(IFCREAL(1.),IFCREAL(2.)),(IFCREAL(10.),IFCREAL(20.)),$,$,$,$)");
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", "20"))), table));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", "30"))), table));
        // Bounded values, as the user manual's table has them: (IDS value, lower, upper) → result.
        string Bounded(string lower, string upper) => $"IFCPROPERTYBOUNDEDVALUE('Foo',$,{upper},{lower},$,$)";
        var range = R("double", "<xs:minExclusive value=\"2\"/><xs:maxInclusive value=\"5\"/>");
        (string Value, string Lower, string Upper, string Expected)[] rows =
        [
            (range, "IFCREAL(3.)", "IFCREAL(4.)", "pass"), (range, "$", "IFCREAL(4.)", "fail"), (range, "IFCREAL(3.)", "$", "fail"),
            (range, "IFCREAL(2.)", "IFCREAL(3.)", "fail"), (range, "IFCREAL(3.)", "IFCREAL(5.)", "pass"),
            ("3", "IFCREAL(2.)", "IFCREAL(4.)", "pass"), ("2", "IFCREAL(2.)", "IFCREAL(4.)", "pass"), ("2", "IFCREAL(2.)", "$", "pass"),
            ("2", "$", "IFCREAL(2.)", "pass"), ("5", "IFCREAL(2.)", "IFCREAL(4.)", "fail"), ("5", "$", "IFCREAL(4.)", "fail"),
            (range, "$", "$", "fail"), ("3", "$", "$", "fail"),
        ];
        foreach (var (value, lower, upper, expected) in rows)
            Assert.True(expected == Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", value))), WithProps(Bounded(lower, upper))), $"{value} [{lower}, {upper}] should {expected}");
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL"))), WithProps(Bounded("IFCREAL(2.)", "$"))));
    }

    [Fact]
    public void Optional_and_prohibited_properties_and_material_and_project_properties()
    {
        var label = WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('Bar'),$)");
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", card: "optional"))), label));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Bar", "IFCLABEL", card: "optional"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCTEXT", card: "optional"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", card: "prohibited"))), label));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", card: "prohibited"))), WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,$,$)")));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Bar", card: "prohibited"))), label));
        // A material's own properties (IFC4's IfcMaterialProperties).
        string[] material = ["#1=IFCMATERIAL('Concrete',$,$);", "#2=IFCMATERIALPROPERTIES('Pset_MaterialCommon',$,(#3),#1);", "#3=IFCPROPERTYSINGLEVALUE('MassDensity',$,IFCMASSDENSITYMEASURE(2400.),$);"];
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCMATERIAL"), Prop("Pset_MaterialCommon", "MassDensity", "IFCMASSDENSITYMEASURE", "2400"))), material));
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCMATERIAL"), Prop("Pset_MaterialCommon", "Porosity"))), material));
        // The project's own (IfcContext in IFC4).
        string[] project = [$"#1=IFCPROJECT('{G1}',$,'P',$,$,$,$,$,$);", $"#2=IFCPROPERTYSET('{G4}',$,'Foo_Bar',$,(#3));", $"#4=IFCRELDEFINESBYPROPERTIES('{G5}',$,$,$,(#1),#2);", "#3=IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('Bar'),$);"];
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCPROJECT"), Prop("Foo_Bar", "Foo", "IFCLABEL", "Bar"))), project));
    }

    // ------------------------------------------------------------ partOf

    [Fact]
    public void Part_of_by_containment_directly_and_through_the_spatial_structure()
    {
        string[] model =
        [
            AWall(), $"#2=IFCBUILDINGSTOREY('{G2}',$,'L1',$,$,$,$,$,.ELEMENT.,0.);", $"#3=IFCBUILDING('{G3}',$,'B',$,$,$,$,$,.ELEMENT.,$,$,$);",
            $"#4=IFCRELCONTAINEDINSPATIALSTRUCTURE('{G4}',$,$,$,(#1),#2);", $"#5=IFCRELAGGREGATES('{G5}',$,$,$,#3,(#2));",
        ];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, PartOf("IFCBUILDINGSTOREY", "IFCRELCONTAINEDINSPATIALSTRUCTURE"))), model));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, PartOf("IFCBUILDING", "IFCRELCONTAINEDINSPATIALSTRUCTURE"))), model));
        Assert.Equal("pass", Status(Ids(Spec(WallApp, PartOf("IFCBUILDING"))), model));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, PartOf("IFCSPACE"))), model));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, PartOf("IFCBUILDINGSTOREY", "IFCRELAGGREGATES"))), model));
        // The container itself is part of no storey.
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCBUILDINGSTOREY"), PartOf("IFCBUILDINGSTOREY"))), model));
        // Prohibited, and as an applicability.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, PartOf("IFCBUILDINGSTOREY", card: "prohibited"))), model));
        Assert.Equal("pass", Status(Ids(Spec(WallApp + PartOf("IFCBUILDINGSTOREY"), Attr("Name", "W1"))), model));
        // The whole's predefined type.
        string[] space = [AWall(), $"#2=IFCSPACE('{G2}',$,'R',$,$,$,$,$,.ELEMENT.,.INTERNAL.,$);", $"#4=IFCRELCONTAINEDINSPATIALSTRUCTURE('{G4}',$,$,$,(#1),#2);"];
        Assert.Equal("pass", Status(Ids(Spec(WallApp, PartOf("IFCSPACE", pre: "INTERNAL"))), space));
        Assert.Equal("fail", Status(Ids(Spec(WallApp, PartOf("IFCSPACE", pre: "EXTERNAL"))), space));
    }

    [Fact]
    public void Part_of_by_aggregation_groups_nests_and_openings()
    {
        string[] assembly =
        [
            $"#1=IFCBEAM('{G1}',$,'B',$,$,$,$,$,.BEAM.);", $"#2=IFCELEMENTASSEMBLY('{G2}',$,'A',$,$,$,$,$,.FACTORY.,.TRUSS.);",
            $"#3=IFCELEMENTASSEMBLY('{G3}',$,'Top',$,$,$,$,$,.FACTORY.,.RIGID_FRAME.);",
            $"#4=IFCRELAGGREGATES('{G4}',$,$,$,#2,(#1));", $"#5=IFCRELAGGREGATES('{G5}',$,$,$,#3,(#2));",
        ];
        var beam = Entity("IFCBEAM");
        Assert.Equal("pass", Status(Ids(Spec(beam, PartOf("IFCELEMENTASSEMBLY", "IFCRELAGGREGATES"))), assembly));
        Assert.Equal("pass", Status(Ids(Spec(beam, PartOf("IFCELEMENTASSEMBLY", "IFCRELAGGREGATES", pre: "RIGID_FRAME"))), assembly));
        Assert.Equal("fail", Status(Ids(Spec(beam, PartOf("IFCELEMENTASSEMBLY", "IFCRELAGGREGATES", pre: "GIRDER"))), assembly));
        // The whole is part of nothing by aggregation.
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCELEMENTASSEMBLY", "RIGID_FRAME"), PartOf("IFCELEMENTASSEMBLY", "IFCRELAGGREGATES"))), assembly));

        string[] grouped = [$"#1=IFCDUCTSEGMENT('{G1}',$,'D',$,$,$,$,$,.RIGIDSEGMENT.);", $"#2=IFCDISTRIBUTIONSYSTEM('{G2}',$,'S',$,$,$,.VENTILATION.);", $"#3=IFCRELASSIGNSTOGROUP('{G3}',$,$,$,(#1),$,#2);"];
        var duct = Entity("IFCDUCTSEGMENT");
        Assert.Equal("pass", Status(Ids(Spec(duct, PartOf("IFCDISTRIBUTIONSYSTEM", "IFCRELASSIGNSTOGROUP"))), grouped));
        Assert.Equal("fail", Status(Ids(Spec(duct, PartOf("IFCGROUP", "IFCRELASSIGNSTOGROUP"))), grouped));
        Assert.Equal("pass", Status(Ids(Spec(duct, PartOf("IFCDISTRIBUTIONSYSTEM", "IFCRELASSIGNSTOGROUP", pre: "VENTILATION"))), grouped));
        Assert.Equal("fail", Status(Ids(Spec(duct, PartOf("IFCDISTRIBUTIONSYSTEM", "IFCRELAGGREGATES"))), grouped));

        string[] nested = [$"#1=IFCDISTRIBUTIONPORT('{G1}',$,'P',$,$,$,$,.SINK.,.DUCT.,$);", $"#2=IFCDUCTSEGMENT('{G2}',$,'D',$,$,$,$,$,.RIGIDSEGMENT.);", $"#3=IFCRELNESTS('{G3}',$,$,$,#2,(#1));"];
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCDISTRIBUTIONPORT"), PartOf("IFCDUCTSEGMENT", "IFCRELNESTS"))), nested));
        Assert.Equal("fail", Status(Ids(Spec(Entity("IFCDUCTSEGMENT"), PartOf("IFCDUCTSEGMENT", "IFCRELNESTS"))), nested));

        string[] opening =
        [
            AWall(), $"#2=IFCOPENINGELEMENT('{G2}',$,'O',$,$,$,$,$,.OPENING.);", $"#3=IFCWINDOW('{G3}',$,'W',$,$,$,$,$,1.5,1.2,.WINDOW.,.SINGLE_PANEL.,$);",
            $"#4=IFCRELVOIDSELEMENT('{G4}',$,$,$,#1,#2);", $"#5=IFCRELFILLSELEMENT('{G5}',$,$,$,#2,#3);",
        ];
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCWINDOW"), PartOf("IFCWALL", "IFCRELVOIDSELEMENT IFCRELFILLSELEMENT"))), opening));
        Assert.Equal("pass", Status(Ids(Spec(Entity("IFCWINDOW"), PartOf("IFCOPENINGELEMENT"))), opening));
    }

    // ------------------------------------------------------------ restrictions

    [Fact]
    public void Restrictions_bounds_lengths_enumerations_and_patterns()
    {
        var real = WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCREAL(42.),$)");
        string Bound(string facets) => Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", R("double", facets)))), real);
        Assert.Equal("fail", Bound("<xs:minExclusive value=\"42\"/>"));
        Assert.Equal("pass", Bound("<xs:minExclusive value=\"41.9\"/>"));
        Assert.Equal("fail", Bound("<xs:maxExclusive value=\"42\"/>"));
        Assert.Equal("pass", Bound("<xs:minInclusive value=\"42\"/>"));
        Assert.Equal("pass", Bound("<xs:maxInclusive value=\"42\"/>"));
        Assert.Equal("pass", Bound("<xs:minInclusive value=\"0\"/><xs:maxInclusive value=\"100\"/>"));
        Assert.Equal("fail", Bound("<xs:minInclusive value=\"42.0000001\"/>"));   // no tolerance on bounds
        var text = WithProps("IFCPROPERTYSINGLEVALUE('Foo',$,IFCLABEL('Abc'),$)");
        string Text(string facets) => Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", R("string", facets)))), text);
        Assert.Equal("pass", Text("<xs:length value=\"3\"/>"));
        Assert.Equal("fail", Text("<xs:length value=\"2\"/>"));
        Assert.Equal("pass", Text("<xs:minLength value=\"2\"/><xs:maxLength value=\"3\"/>"));
        Assert.Equal("fail", Text("<xs:minLength value=\"4\"/>"));
        Assert.Equal("fail", Text("<xs:maxLength value=\"2\"/>"));
        Assert.Equal("pass", Text(En("Abc", "Def")));
        Assert.Equal("fail", Text(En("abc", "ABC")));
        Assert.Equal("pass", Text(Pat("[A-Z][a-z]{2}")));
        Assert.Equal("fail", Text(Pat("[a-z]+")));
        Assert.Equal("pass", Text(Pat("X.*") + Pat("A.*")));
        // A pattern never meets a number.
        Assert.Equal("fail", Status(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", R("double", Pat("42.*"))))), real));
    }

    [Fact]
    public void Xsd_patterns_are_whole_value_and_keep_their_own_meaning()
    {
        bool M(string p, string v) => XsdPattern.Matches(XsdPattern.Compile(p), v);
        Assert.True(M("DT[0-9]{2}", "DT01"));
        Assert.False(M("DT[0-9]{2}", "XDT01"));
        Assert.False(M("DT[0-9]{2}", "DT012"));
        Assert.True(M(".*DT.*", "ABC-DT-01"));
        Assert.True(M("^A$", "^A$"));                     // ^ and $ are characters
        Assert.False(M("\\w+", "EF_25"));                  // XSD's \w is not the underscore's
        Assert.True(M("\\w+", "Perete"));
        Assert.True(M("[a-z-[aeiou]]+", "bcd"));           // class subtraction
        Assert.False(M("[a-z-[aeiou]]+", "bad"));
        Assert.False(M(".", "\n"));
        Assert.True(M("\\p{Lu}\\d", "A1"));
        Assert.Throws<FormatException>(() => XsdPattern.Compile("(?i)abc"));
        Assert.Throws<FormatException>(() => XsdPattern.Compile("a*?"));
        Assert.Throws<FormatException>(() => XsdPattern.Compile("[abc"));
        Assert.Throws<FormatException>(() => XsdPattern.Compile("\\b"));
    }

    // ------------------------------------------------------------ the reader

    static IdsFormatException Bad(string xml) => Assert.Throws<IdsFormatException>(() => IdsReader.Read(xml));

    [Fact]
    public void The_reader_keeps_info_specifications_occurs_and_instructions()
    {
        var xml = Ids(Spec(WallApp, Prop("Pset_WallCommon", "FireRating", "IFCLABEL", R("string", En("EI 60", "EI 90")))))
            .Replace("<title>Test</title>", "<title>Test</title><copyright>Office</copyright><version>1.2</version><description>D</description><author>a@b.ro</author><date>2026-11-24</date><purpose>Coordination</purpose><milestone>PT</milestone>")
            .Replace("<specification name=\"S\"", "<specification name=\"S\" identifier=\"SP01\" description=\"Why\" instructions=\"How\"")
            .Replace("<property ", "<property instructions=\"Set the fire rating\" ");
        var doc = IdsReader.Read(xml);
        Assert.Equal(("Test", "Office", "1.2", "a@b.ro", "2026-11-24", "Coordination", "PT"), (doc.Info.Title, doc.Info.Copyright, doc.Info.Version, doc.Info.Author, doc.Info.Date, doc.Info.Purpose, doc.Info.Milestone));
        var s = Assert.Single(doc.Specifications);
        Assert.Equal(("SP01", "Why", "How"), (s.Identifier, s.Description, s.Instructions));
        Assert.Equal(new[] { "IFC2X3", "IFC4" }, s.IfcVersions);
        Assert.Equal(IdsUsage.Required, s.Usage);
        var p = Assert.IsType<IdsPropertyFacet>(Assert.Single(s.Requirements));
        Assert.Equal("Set the fire rating", p.Instructions);
        Assert.Equal(new[] { "EI 60", "EI 90" }, p.Value!.Restriction!.Enumeration);
        Assert.Equal(IdsUsage.Optional, IdsReader.Read(Ids(Spec(WallApp, occurs: Optional))).Specifications[0].Usage);
        Assert.Equal(IdsUsage.Prohibited, IdsReader.Read(Ids(Spec(WallApp, occurs: Prohibited))).Specifications[0].Usage);
        // A restriction's base may use any prefix bound to XML Schema.
        Assert.NotNull(IdsReader.Read(Ids(Spec(WallApp, Attr("Name", "<x:restriction xmlns:x=\"http://www.w3.org/2001/XMLSchema\" base=\"x:string\"><x:pattern value=\"W.*\"/></x:restriction>")))));
    }

    [Fact]
    public void Malformed_files_are_refused_with_what_and_where()
    {
        Assert.Contains("well-formed", Bad("<ids><info>").Message);
        Assert.Contains("namespace", Bad("<ids><info><title>T</title></info></ids>").Message);
        Assert.Contains("<title>", Bad(Ids(Spec(WallApp)).Replace("<title>Test</title>", "")).Message);
        Assert.Contains("ifcVersion", Bad(Ids(Spec(WallApp)).Replace(" ifcVersion=\"IFC2X3 IFC4\"", "")).Message);
        Assert.Contains("IFC5", Bad(Ids(Spec(WallApp, versions: "IFC5"))).Message);
        Assert.Contains("specification", Bad(Ids()).Message);
        Assert.Contains("applicability", Bad(Ids("<specification name=\"S\" ifcVersion=\"IFC4\"/>")).Message);
        Assert.Contains("e-mail", Bad(Ids(Spec(WallApp)).Replace("<title>Test</title>", "<title>Test</title><author>nobody</author>")).Message);
        Assert.Contains("date", Bad(Ids(Spec(WallApp)).Replace("<title>Test</title>", "<title>Test</title><date>24.11.2026</date>")).Message);
        // The manual's facet configurations.
        Assert.Contains("dataType", Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", value: "Bar")))).Message);
        Assert.Contains("nothing to check", Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", card: "optional")))).Message);
        Assert.Contains("prohibited", Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCLABEL", card: "prohibited")))).Message);
        Assert.Contains("nothing to check", Bad(Ids(Spec(WallApp, Attr("Name", card: "optional")))).Message);
        Assert.Contains("nothing to check", Bad(Ids(Spec(WallApp, Mat(card: "optional")))).Message);
        Assert.Contains("cardinality", Bad(Ids(Spec(WallApp, PartOf("IFCBUILDINGSTOREY", card: "optional")))).Message);
        Assert.Contains("cardinality", Bad(Ids(Spec(WallApp + Attr("Name", card: "required")))).Message);
        Assert.Contains("relation", Bad(Ids(Spec(WallApp, PartOf("IFCBUILDINGSTOREY", "IFCRELFOO")))).Message);
        // Values in their type's lexical form.
        Assert.Contains("boolean", Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCBOOLEAN", "TRUE")))).Message);
        Assert.Contains("whole number", Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCINTEGER", "42.5")))).Message);
        Assert.Contains("number", Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", "1,5")))).Message);
        Assert.Contains("number", Bad(Ids(Spec(WallApp, Attr("Name", R("double", En("abc")))))).Message);
        Assert.Contains("base", Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", "IFCREAL", R("string", En("1")))))).Message);
        Assert.Contains("does not apply", Bad(Ids(Spec(WallApp, Attr("Name", R("double", "<xs:length value=\"2\"/>"))))).Message);
        Assert.Contains("pattern", Bad(Ids(Spec(WallApp, Attr("Name", R("string", Pat("[abc")))))).Message);
        Assert.Contains("base", Bad(Ids(Spec(WallApp, Attr("Name", R("foo", En("a")))))).Message);
        // Structure: what is not in the schema, and its order.
        Assert.Contains("not allowed", Bad(Ids(Spec(WallApp + "<foo/>"))).Message);
        Assert.Contains("comes before", Bad(Ids(Spec(Attr("Name") + WallApp))).Message);
        Assert.Contains("more than one <entity>", Bad(Ids(Spec(WallApp + WallApp))).Message);
        Assert.Contains("<system>", Bad(Ids(Spec(WallApp, "<classification><value><simpleValue>X</simpleValue></value></classification>"))).Message);
        // Every problem, each with its line.
        var many = Bad(Ids(Spec(WallApp, Prop("Foo_Bar", "Foo", value: "Bar") + Attr("Name", card: "optional"))));
        Assert.True(many.Problems.Count >= 2);
        Assert.All(many.Problems, p => Assert.StartsWith("Line ", p));
        // No DTD, so no entity expansion.
        Assert.Contains("well-formed", Bad("<?xml version=\"1.0\"?><!DOCTYPE ids [<!ENTITY x \"y\">]><ids/>").Message);
    }

    [Fact]
    public void A_run_reports_each_failure_with_its_GlobalId_requirement_actual_and_expected_in_the_files_order()
    {
        string[] walls = [AWall(gid: G1, id: 1), AWall(name: "$", gid: G2, id: 2), AWall(name: "$", gid: G3, id: 3, pre: ".SHEAR.")];
        var r = Run(Ids(Spec(WallApp, Attr("Name") + Entity(Wall, "STANDARD"))), walls);
        Assert.Equal(("fail", 3, 1, 2), (r.Status, r.Applicable, r.Passed, r.Failed));
        Assert.Equal(new[] { G2, G3 }, r.FailedGlobalIds);
        Assert.Equal(new[] { G2, G3, G3 }, r.Failures.Select(f => f.GlobalId));
        var first = r.Failures[0];
        Assert.Equal(("IfcWall", "Attribute Name"), (first.Class, first.Requirement));
        Assert.Contains("Name", first.Expected);
        Assert.Equal("SHEAR", r.Failures[2].Actual);
        Assert.Equal("STANDARD", r.Failures[2].Expected);
        // The same twice.
        var again = Run(Ids(Spec(WallApp, Attr("Name") + Entity(Wall, "STANDARD"))), walls);
        Assert.Equal(r.Failures.Select(f => f.ToJson().ToJsonString()), again.Failures.Select(f => f.ToJson().ToJsonString()));
    }

    [Fact]
    public void A_run_can_be_stopped()
    {
        var walls = Enumerable.Range(0, 3000).Select(i => AWall(gid: GlobalIdOf(i), id: i + 1)).ToArray();
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => IdsChecker.Run(StepFile.Parse(Step("IFC4", walls)), IdsReader.Read(Ids(Spec(WallApp, Attr("Name")))), stop: stop.Token));
    }

    internal static string GlobalIdOf(int i) => Lapin.Model.GlobalId.Derive("ids-test", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
