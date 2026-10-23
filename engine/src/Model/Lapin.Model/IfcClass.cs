namespace Lapin.Model;

/// <summary>The IFC4X3 entity names Lapin's model uses, spelled once.</summary>
public static class IfcClass
{
    // Spatial structure.
    public const string Project = "IfcProject";
    public const string Site = "IfcSite";
    public const string Building = "IfcBuilding";
    public const string BuildingStorey = "IfcBuildingStorey";

    // Elements and their types.
    public const string Wall = "IfcWall";
    public const string WallType = "IfcWallType";

    // Materials.
    public const string Material = "IfcMaterial";
    public const string MaterialLayerSet = "IfcMaterialLayerSet";
    public const string MaterialLayerSetUsage = "IfcMaterialLayerSetUsage";

    // Relationships.
    public const string RelAggregates = "IfcRelAggregates";
    public const string RelContainedInSpatialStructure = "IfcRelContainedInSpatialStructure";
    public const string RelDefinesByType = "IfcRelDefinesByType";
    public const string RelAssociatesMaterial = "IfcRelAssociatesMaterial";
    public const string RelVoidsElement = "IfcRelVoidsElement";
    public const string RelFillsElement = "IfcRelFillsElement";
    public const string RelConnectsPathElements = "IfcRelConnectsPathElements";
}
