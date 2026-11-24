namespace Lapin.Ids;

/// <summary>
/// The names of an IFC entity's explicit attributes, in the order a STEP file writes them, as IDS's attribute facet names them
/// (IFC4 ADD2 TC1 and IFC 4.3 ADD2, IFC2X3 where it differs): what every rooted object has (GlobalId, OwnerHistory, Name,
/// Description), an object's ObjectType, a product's placement and representation, an element's Tag, a type's
/// ApplicableOccurrence, HasPropertySets, RepresentationMaps, Tag and ElementType, and each class's own after them. Inverse and
/// derived attributes are no explicit attribute, so a facet naming one finds nothing (IDS: they cannot be checked). A class not
/// listed is read by its family (an element, a type, a spatial element) and its PredefinedType found where its family puts it.
/// </summary>
public static class IfcAttributes
{
    static readonly string?[] Root = ["GlobalId", "OwnerHistory", "Name", "Description"];
    static readonly string?[] Object = [.. Root, "ObjectType"];
    static readonly string?[] Product = [.. Object, "ObjectPlacement", "Representation"];
    static readonly string?[] Element = [.. Product, "Tag"];
    static readonly string?[] TypeObject = [.. Root, "ApplicableOccurrence", "HasPropertySets"];
    static readonly string?[] TypeProduct = [.. TypeObject, "RepresentationMaps", "Tag"];
    static readonly string?[] ElementType = [.. TypeProduct, "ElementType"];
    static readonly string?[] Spatial = [.. Product, "LongName", "CompositionType"];

    static readonly Dictionary<string, string?[]> Known = new()
    {
        ["IFCPROJECT"] = [.. Object, "LongName", "Phase", "RepresentationContexts", "UnitsInContext"],
        ["IFCPROJECTLIBRARY"] = [.. Object, "LongName", "Phase", "RepresentationContexts", "UnitsInContext"],
        ["IFCSITE"] = [.. Spatial, "RefLatitude", "RefLongitude", "RefElevation", "LandTitleNumber", "SiteAddress"],
        ["IFCBUILDING"] = [.. Spatial, "ElevationOfRefHeight", "ElevationOfTerrain", "BuildingAddress"],
        ["IFCBUILDINGSTOREY"] = [.. Spatial, "Elevation"],
        ["IFCSPACE"] = [.. Spatial, "PredefinedType", "ElevationWithFlooring"],
        ["IFCSPATIALZONE"] = [.. Product, "LongName", "PredefinedType"],
        ["IFCEXTERNALSPATIALELEMENT"] = [.. Product, "LongName", "PredefinedType"],
        ["IFCDOOR"] = [.. Element, "OverallHeight", "OverallWidth", "PredefinedType", "OperationType", "UserDefinedOperationType"],
        ["IFCWINDOW"] = [.. Element, "OverallHeight", "OverallWidth", "PredefinedType", "PartitioningType", "UserDefinedPartitioningType"],
        ["IFCSTAIRFLIGHT"] = [.. Element, "NumberOfRisers", "NumberOfTreads", "RiserHeight", "TreadLength", "PredefinedType"],
        ["IFCPILE"] = [.. Element, "PredefinedType", "ConstructionType"],
        ["IFCELEMENTASSEMBLY"] = [.. Element, "AssemblyPlace", "PredefinedType"],
        ["IFCREINFORCINGBAR"] = [.. Element, "SteelGrade", "NominalDiameter", "CrossSectionArea", "BarLength", "PredefinedType", "BarSurface"],
        ["IFCMECHANICALFASTENER"] = [.. Element, "NominalDiameter", "NominalLength", "PredefinedType"],
        ["IFCDISTRIBUTIONPORT"] = [.. Product, "FlowDirection", "PredefinedType", "SystemType"],
        ["IFCGRID"] = [.. Product, "UAxes", "VAxes", "WAxes", "PredefinedType"],
        ["IFCANNOTATION"] = [.. Product, "PredefinedType"],
        ["IFCALIGNMENT"] = [.. Product, "PredefinedType"],
        ["IFCPROXY"] = [.. Product, "ProxyType", "Tag"],
        ["IFCGROUP"] = Object,
        ["IFCSYSTEM"] = Object,
        ["IFCZONE"] = [.. Object, "LongName"],
        ["IFCDISTRIBUTIONSYSTEM"] = [.. Object, "LongName", "PredefinedType"],
        ["IFCDISTRIBUTIONCIRCUIT"] = [.. Object, "LongName", "PredefinedType"],
        ["IFCBUILDINGSYSTEM"] = [.. Object, "PredefinedType", "LongName"],
        ["IFCBUILTSYSTEM"] = [.. Object, "PredefinedType", "LongName"],
        ["IFCSTRUCTURALANALYSISMODEL"] = [.. Object, "PredefinedType", "OrientationOf2DPlane", "LoadedBy", "HasResults", "SharedPlacement"],
        ["IFCACTOR"] = [.. Object, "TheActor"],
        ["IFCOCCUPANT"] = [.. Object, "TheActor", "PredefinedType"],
        ["IFCTASK"] = [.. Object, "Identification", "LongDescription", "Status", "WorkMethod", "IsMilestone", "Priority", "TaskTime", "PredefinedType"],
        ["IFCPROCEDURE"] = [.. Object, "Identification", "LongDescription", "PredefinedType"],
        ["IFCEVENT"] = [.. Object, "Identification", "LongDescription", "PredefinedType", "EventTriggerType", "UserDefinedEventTriggerType", "EventOccurenceTime"],
        ["IFCCOSTITEM"] = [.. Object, "Identification", "PredefinedType", "CostValues", "CostQuantities"],
        ["IFCCOSTSCHEDULE"] = [.. Object, "Identification", "PredefinedType", "Status", "SubmittedOn", "UpdateDate"],
        ["IFCWORKSCHEDULE"] = [.. Object, "Identification", "CreationDate", "Creators", "Purpose", "Duration", "TotalFloat", "StartTime", "FinishTime", "PredefinedType"],
        ["IFCWORKPLAN"] = [.. Object, "Identification", "CreationDate", "Creators", "Purpose", "Duration", "TotalFloat", "StartTime", "FinishTime", "PredefinedType"],
        ["IFCDOORTYPE"] = [.. ElementType, "PredefinedType", "OperationType", "ParameterTakesPrecedence", "UserDefinedOperationType"],
        ["IFCWINDOWTYPE"] = [.. ElementType, "PredefinedType", "PartitioningType", "ParameterTakesPrecedence", "UserDefinedPartitioningType"],
        ["IFCDOORSTYLE"] = [.. TypeProduct, "OperationType", "ConstructionType", "ParameterTakesPrecedence", "Sizeable"],
        ["IFCWINDOWSTYLE"] = [.. TypeProduct, "ConstructionType", "OperationType", "ParameterTakesPrecedence", "Sizeable"],
        ["IFCSPACETYPE"] = [.. ElementType, "PredefinedType", "LongName"],
        ["IFCTASKTYPE"] = [.. TypeObject, "Identification", "LongDescription", "ProcessType", "PredefinedType", "WorkMethod"],
        ["IFCMATERIAL"] = ["Name", "Description", "Category"],
        ["IFCMATERIALLAYERSET"] = ["MaterialLayers", "LayerSetName", "Description"],
        ["IFCMATERIALLAYER"] = ["Material", "LayerThickness", "IsVentilated", "Name", "Description", "Category", "Priority"],
        ["IFCMATERIALPROFILESET"] = ["Name", "Description", "MaterialProfiles", "CompositeProfile"],
        ["IFCMATERIALCONSTITUENTSET"] = ["Name", "Description", "MaterialConstituents"],
        ["IFCDOORLININGPROPERTIES"] = [.. Root, "LiningDepth", "LiningThickness", "ThresholdDepth", "ThresholdThickness", "TransomThickness", "TransomOffset", "LiningOffset", "ThresholdOffset", "CasingThickness", "CasingDepth", "ShapeAspectStyle", "LiningToPanelOffsetX", "LiningToPanelOffsetY"],
        ["IFCDOORPANELPROPERTIES"] = [.. Root, "PanelDepth", "PanelOperation", "PanelWidth", "PanelPosition", "ShapeAspectStyle"],
        ["IFCWINDOWLININGPROPERTIES"] = [.. Root, "LiningDepth", "LiningThickness", "TransomThickness", "MullionThickness", "FirstTransomOffset", "SecondTransomOffset", "FirstMullionOffset", "SecondMullionOffset", "ShapeAspectStyle", "LiningOffset", "LiningToPanelOffsetX", "LiningToPanelOffsetY"],
        ["IFCWINDOWPANELPROPERTIES"] = [.. Root, "OperationType", "PanelPosition", "FrameDepth", "FrameThickness", "ShapeAspectStyle"],
    };

    /// <summary>What IFC2X3 writes otherwise (no PredefinedType on most elements, a project without its context's).</summary>
    static readonly Dictionary<string, string?[]> Known2X3 = new()
    {
        ["IFCDOOR"] = [.. Element, "OverallHeight", "OverallWidth"],
        ["IFCWINDOW"] = [.. Element, "OverallHeight", "OverallWidth"],
        ["IFCSPACE"] = [.. Spatial, "InteriorOrExteriorSpace", "ElevationWithFlooring"],
        ["IFCSTAIRFLIGHT"] = [.. Element, "NumberOfRiser", "NumberOfTreads", "RiserHeight", "TreadLength"],
        ["IFCMATERIAL"] = ["Name"],
        ["IFCTASK"] = [.. Object, "TaskId", "Status", "WorkMethod", "IsMilestone", "Priority"],
    };

    static readonly HashSet<string> SpatialClasses =
    [
        "IFCFACILITY", "IFCFACILITYPART", "IFCBRIDGE", "IFCBRIDGEPART", "IFCROAD", "IFCROADPART", "IFCRAILWAY", "IFCRAILWAYPART", "IFCMARINEFACILITY", "IFCMARINEPART",
        "IFCFACILITYPARTCOMMON",
    ];

    static readonly HashSet<string> Lengths =
    [
        "OverallHeight", "OverallWidth", "Elevation", "ElevationWithFlooring", "RiserHeight", "TreadLength", "ElevationOfRefHeight", "ElevationOfTerrain", "RefElevation",
        "NominalDiameter", "BarLength", "NominalLength", "LayerThickness",
    ];

    /// <summary>Whether the attribute is a length (its value in the project's length unit, read in metres).</summary>
    public static bool IsLength(string name) => Lengths.Contains(name);

    /// <summary>
    /// The names of an entity's attributes (upper-case class, as many as it has); null where a position has no name IDS addresses.
    /// </summary>
    public static IReadOnlyList<string?> Of(string type, int count, string schema)
    {
        if (schema == "IFC2X3" && Known2X3.TryGetValue(type, out var old)) return Fit(old, count);
        if (Known.TryGetValue(type, out var known)) return Fit(known, count);
        if (SpatialClasses.Contains(type)) return Fit([.. Spatial, "PredefinedType"], count);
        if (type.EndsWith("TYPE", StringComparison.Ordinal) || type.EndsWith("STYLE", StringComparison.Ordinal))
            return count switch
            {
                >= 10 => Fit([.. ElementType, "PredefinedType"], count),
                9 => Fit(ElementType, count),
                _ => Fit(TypeProduct, count),
            };
        // An element (a wall, a slab, a pump…): its eight, then its PredefinedType when it has exactly one more (IFC4's built elements,
        // its distribution elements); a product of seven attributes (a 2X3 element has eight and no type).
        return count switch
        {
            9 => [.. Element, "PredefinedType"],
            _ => Fit(Element, count),
        };
    }

    static string?[] Fit(string?[] names, int count) => count == names.Length ? names : count < names.Length ? names[..count] : [.. names, .. new string?[count - names.Length]];
}
