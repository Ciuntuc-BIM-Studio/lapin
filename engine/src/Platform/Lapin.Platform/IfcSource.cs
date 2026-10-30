namespace Lapin.Platform;

/// <summary>
/// What a foreign IFC element's file said of it that Lapin does not model,
/// kept as the file wrote it so an export writes it back (docs/spec-ifc.md,
/// Ce se păstrează): its attributes, property sets and quantities with their
/// value types and units, its type, material, ports, connections and systems,
/// each with its GlobalId. The element holds what is its own under
/// <see cref="Key"/>; what elements share (a type, a system, a material) is
/// one <see cref="RecordClass"/> entity they name.
/// </summary>
public static class IfcSource
{
    public const string Key = "ifc";
    public const string RecordClass = "LapinIfcRecord";
}
