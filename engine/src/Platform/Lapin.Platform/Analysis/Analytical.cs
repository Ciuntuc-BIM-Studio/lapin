using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Analysis;

/// <summary>
/// A frame's cross-section as the analytical model reads it (docs/jg-structures.md), metres: a rectangle (width × depth),
/// a circle (its diameter), a hot-rolled profile I (H), U, T or L (depth, flanges' width, web and flange thickness), a hollow
/// RHS or CHS (its wall <see cref="Web"/>), a cold-formed C, Z or SIGMA (the sheet <see cref="Web"/>, lips <see cref="Lip"/>).
/// <see cref="Width"/> lies along the frame's local 3, <see cref="Depth"/> along its local 2. <see cref="Designation"/> is
/// the catalogue's exact name (IPE 300) when the section is one of Lapin's catalogue with its dimensions unchanged.
/// </summary>
public sealed record SectionShape(string Kind, double Width, double Depth, double Web = 0, double Flange = 0, double Lip = 0, string? Designation = null);

/// <summary>A frame section: its key (the type's id, or a key made from its shape), its name, its shape, where it was read.</summary>
public sealed record FrameSection(string Key, string Name, SectionShape Shape, string Source);

/// <summary>A support's degrees of freedom [UX, UY, UZ, RX, RY, RZ] held, its kind (fixed, pinned) and where that was read.</summary>
public sealed record SupportSpec(bool[] Dofs, string Kind, string Source)
{
    public static SupportSpec Fixed(string source) => new([true, true, true, true, true, true], "fixed", source);
    public static SupportSpec Pinned(string source) => new([true, true, true, false, false, false], "pinned", source);
}

/// <summary>
/// A bar of the physical model as an analytical line (docs/jg-structures.md): its element (<see cref="SourceId"/>, a
/// GlobalId), <see cref="Kind"/> beam or column, its axis through the section's box centre (two points, or a polyline of
/// chords for a curved one), the section's depth direction (<see cref="DepthDir"/>, local 2 before any rotation is worked
/// out), its section and material. A horizontal bar gives its <see cref="Depth"/> so the model can lay it in the slab it
/// carries; <see cref="Width"/> is how far from its axis a column end or a slab corner snaps to it.
/// </summary>
public sealed record AnalyticalFrame(
    string SourceId, string Name, string IfcClass, string Kind, string Category,
    IReadOnlyList<Vec3> Points, Vec3 DepthDir, FrameSection Section, string? MaterialId, string? StoreyId,
    double Depth, double Width, string? Role = null, bool Mirrored = false, string? Note = null);

/// <summary>
/// A wall or a slab as an analytical surface: a slab its core's outline at the core's mid-plane (<see cref="Outline"/>,
/// <see cref="Z"/>); a wall its core's centreline (<see cref="Line"/>: two points, or chords of an arc) from <see cref="Z0"/>
/// to <see cref="Z1"/>. <see cref="Thickness"/> is the core's.
/// </summary>
public sealed record AnalyticalShell(
    string SourceId, string Name, string IfcClass, string Kind, string SectionKey, string SectionName, double Thickness,
    string? MaterialId, string? StoreyId, bool LoadBearing,
    IReadOnlyList<Vec2>? Outline = null, double Z = 0,
    IReadOnlyList<Vec2>? Line = null, double Z0 = 0, double Z1 = 0, string? Note = null);

/// <summary>
/// What holds the model at its base: a footing's footprint (<see cref="Region"/>) or a strip's line (<see cref="LineA"/> to
/// <see cref="LineB"/>, <see cref="Width"/> across) between <see cref="Bottom"/> and <see cref="Top"/>; or a connection's
/// support on one end of one frame (<see cref="ForFrame"/>, <see cref="AtEnd"/>: false its first point).
/// </summary>
public sealed record AnalyticalSupport(
    string SourceId, string Name, SupportSpec Spec, IReadOnlyList<Vec2>? Region = null, Vec2? LineA = null, Vec2? LineB = null,
    double Width = 0, double Bottom = 0, double Top = 0, string? ForFrame = null, bool AtEnd = false);

/// <summary>An element the analytical model leaves out, and why.</summary>
public sealed record LeftOut(string SourceId, string Name, string IfcClass, string Reason);

/// <summary>What the modules give the analytical model.</summary>
public sealed class AnalyticalContribution
{
    public List<AnalyticalFrame> Frames { get; } = [];
    public List<AnalyticalShell> Shells { get; } = [];
    public List<AnalyticalSupport> Supports { get; } = [];
    public List<LeftOut> LeftOut { get; } = [];
}

/// <summary>
/// A module's elements as analytical members (columns and beams from structure, steel members from steel, walls and
/// slabs from architecture): the platform assembles them (<see cref="AnalyticalModel"/>), for an analysis program's
/// exchange file (JG Structures) and, later, IFC's structural analysis view.
/// </summary>
public interface IAnalyticalElements
{
    void Contribute(IModelView m, AnalyticalContribution into);
}

/// <summary>The kinds of members the user may leave out of an export.</summary>
public static class AnalyticalKinds
{
    public const string Columns = "columns", Beams = "beams", Members = "members", Walls = "walls", Slabs = "slabs", Supports = "supports";
    public static readonly string[] All = [Columns, Beams, Members, Walls, Slabs, Supports];
}

/// <summary>What the analytical model takes: the storeys (null: all), the kinds (null: all), every wall or only load-bearing ones.</summary>
public sealed record AnalyticalOptions(IReadOnlySet<string>? Storeys = null, IReadOnlySet<string>? Kinds = null, bool AllWalls = false)
{
    public bool Takes(string kind) => Kinds == null || Kinds.Contains(kind);
    public bool On(string? storeyId) => Storeys == null || storeyId == null || Storeys.Contains(storeyId);
}
