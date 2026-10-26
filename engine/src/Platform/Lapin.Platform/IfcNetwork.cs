using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A port of an element as IFC takes it (IfcDistributionPort): its key (the
/// port's name), where it is and which way it faces, its flow (SOURCE, SINK,
/// SOURCEANDSINK), what carries it (PIPE, DUCT, CABLE, CABLECARRIER,
/// WIRELESS) and the system type it belongs to (HEATING, DOMESTICCOLDWATER…).
/// </summary>
public sealed record IfcPort(string Key, Vec3 At, Vec3 Dir, string Flow, string Kind, string SystemType);

/// <summary>Two ports joined (IfcRelConnectsPorts): an element's port and another's.</summary>
public sealed record IfcPortConnection(string ElementA, string PortA, string ElementB, string PortB);

/// <summary>A system (IfcDistributionSystem): its id (a GlobalId), name, type and the elements it groups; it serves the building.</summary>
public sealed record IfcSystemGroup(string Id, string Name, string Predefined, IReadOnlyList<string> Members);

/// <summary>
/// What a module adds to the IFC distribution network on export: the ports of
/// its elements, the connections between ports, the systems. The export
/// writes IfcDistributionPort nested in the element, IfcRelConnectsPorts and
/// IfcDistributionSystem with its members, serving the building.
/// </summary>
public interface IIfcNetwork
{
    IEnumerable<IfcPort> PortsOf(IModelView model, Entity element);
    IEnumerable<IfcPortConnection> Connections(IModelView model);
    IEnumerable<IfcSystemGroup> Systems(IModelView model);
}
