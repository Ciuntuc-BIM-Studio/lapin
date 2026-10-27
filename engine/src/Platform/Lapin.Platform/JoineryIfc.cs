using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>One panel of a door or window as IFC describes it: IfcDoorPanelProperties or IfcWindowPanelProperties.</summary>
/// <param name="Operation">PanelOperation (a door's: SWINGING, SLIDING…) or OperationType (a window's: SIDEHUNGLEFTHAND…).</param>
/// <param name="Position">LEFT, MIDDLE, RIGHT (and a window's BOTTOM, TOP).</param>
/// <param name="Width">A door panel's width as a ratio of the door's; null for a window's.</param>
public sealed record IfcJoineryPanel(string Operation, string Position, double? Width = null);

/// <summary>The lining (frame) as IfcDoorLiningProperties / IfcWindowLiningProperties say it: depths and thicknesses in metres, offsets as ratios.</summary>
public sealed record IfcJoineryLining(double Depth, double Thickness, double Mullion, double Transom, double Threshold, IReadOnlyList<double> MullionOffsets, IReadOnlyList<double> TransomOffsets);

/// <summary>
/// A door or window type in IFC's terms: a door's OperationType or a window's
/// PartitioningType (with its user-defined text), its panels and its lining.
/// Left and right are IFC's: as seen looking the way the panels open, which is
/// the local +Y of the element's placement.
/// </summary>
public sealed record IfcJoineryType(bool Door, string Operation, string? UserDefined, IReadOnlyList<IfcJoineryPanel> Panels, IfcJoineryLining Lining);

/// <summary>
/// Where a door or window stands, as IFC needs its placement: the origin at the
/// opening's lower left corner on the wall face the panels open away from,
/// +X along the wall, +Y the way the panels open, +Z up (world, metres).
/// <see cref="Mirrored"/>: the element is its type's mirror image (flipped hand,
/// or flipped side, not both), so in IFC it is an occurrence of the mirrored type.
/// </summary>
public sealed record IfcJoineryPlace(Vec3 Origin, Vec3 X, Vec3 Y, bool Mirrored);

/// <summary>
/// Offered by the module that owns doors and windows: what IFC export needs of
/// them (docs/spec-ifc.md, "Deschiderea ușilor și ferestrelor").
/// </summary>
public interface IJoineryIfc
{
    /// <summary>The type in IFC's terms; mirrored: its mirror image (left and right swapped).</summary>
    IfcJoineryType? Type(IModelView m, Entity type, bool mirrored);

    /// <summary>The element's placement frame; null when it stands in no wall.</summary>
    IfcJoineryPlace? Place(IModelView m, Entity element);
}

/// <summary>IFC's left and right, swapped: the names of a mirror image.</summary>
public static class IfcHands
{
    /// <summary>LEFT ↔ RIGHT in an IFC enumeration value (SINGLE_SWING_LEFT, SIDEHUNGRIGHTHAND, TILTANDTURNLEFTHAND, a panel's position…).</summary>
    public static string Mirror(string value)
    {
        if (value.Contains("LEFT", StringComparison.Ordinal)) return value.Replace("LEFT", "RIGHT", StringComparison.Ordinal);
        if (value.Contains("RIGHT", StringComparison.Ordinal)) return value.Replace("RIGHT", "LEFT", StringComparison.Ordinal);
        return value;
    }

    /// <summary>-left ↔ -right in Lapin's operation names, as user-defined text carries them ("swing-left + fixed").</summary>
    public static string MirrorText(string text) =>
        text.Replace("-left", "-\u0001", StringComparison.Ordinal).Replace("-right", "-left", StringComparison.Ordinal).Replace("-\u0001", "-right", StringComparison.Ordinal);
}
