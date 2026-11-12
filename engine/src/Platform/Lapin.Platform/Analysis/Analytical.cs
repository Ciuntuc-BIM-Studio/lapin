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
public sealed record SectionShape(string Kind, double Width, double Depth, double Web = 0, double Flange = 0, double Lip = 0, string? Designation = null)
{
    /// <summary>The root radius (metres; a hollow section's outer corner) when the section says it (a profile library's), else the catalogue's by its designation.</summary>
    public double? Root { get; init; }
    /// <summary>The toes' radius (a flange's or a leg's tip, metres); null: half the root's for an angle, none for the others.</summary>
    public double? Toe { get; init; }
    /// <summary>The slope of the flanges' inner faces (rise over run: 0.08 on a UPN, 0.14 on an IPN), the flange thickness being at its middle.</summary>
    public double Slope { get; init; }
    /// <summary>An asymmetric I's bottom flange (kind <c>asymI</c>): its width and thickness, the top one being <see cref="Width"/> and <see cref="Flange"/>.</summary>
    public double Width2 { get; init; }
    public double Flange2 { get; init; }
    /// <summary>An arbitrary outline's rings (kind <c>outline</c>), about the box's centre: outsides anticlockwise, holes clockwise.</summary>
    public IReadOnlyList<IReadOnlyList<Vec2>>? Rings { get; init; }
    /// <summary>The product standard and family when a profile library says them (else the catalogue's).</summary>
    public string? Standard { get; init; }
    public string? Family { get; init; }
}

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
    IReadOnlyList<Vec2>? Line = null, double Z0 = 0, double Z1 = 0, string? Note = null)
{
    /// <summary>A sloping slab (a roof's deck): its mid-plane's height is <see cref="AnalyticalShell.Z"/> + Gradient·(x, y); level when zero.</summary>
    public Vec2 Gradient { get; init; }
    /// <summary>A slab's openings in plan (shafts, stairwells, roof windows).</summary>
    public IReadOnlyList<IReadOnlyList<Vec2>> Holes { get; init; } = [];
    /// <summary>A wall's openings (doors, windows, bare openings), each over its own span of the wall.</summary>
    public IReadOnlyList<AnalyticalOpening> Openings { get; init; } = [];
    /// <summary>A wall whose top follows what is over it (a roof, an attached slab): its top from start to end, plan point and height.</summary>
    public IReadOnlyList<Vec3>? Top { get; init; }
}

/// <summary>An opening in a wall: its centre on the wall's axis in plan, the axis' direction there, half its width, its bottom and top (absolute).</summary>
public sealed record AnalyticalOpening(string Id, Vec2 Centre, Vec2 Dir, double HalfWidth, double Bottom, double Top);

/// <summary>
/// A connection the physical model knows (a steel cleat, gusset or fly brace connection): <see cref="MemberId"/> is fixed to
/// <see cref="HostId"/>, wherever their axes come closest, though its axis does not meet the host's (eccentric).
/// </summary>
public sealed record AnalyticalLink(string HostId, string MemberId, string Source);

/// <summary>
/// What holds the model at its base: a footing's footprint (<see cref="Region"/>) or a strip's line (<see cref="LineA"/> to
/// <see cref="LineB"/>, <see cref="Width"/> across) between <see cref="Bottom"/> and <see cref="Top"/>; or a connection's
/// support on one end of one frame (<see cref="ForFrame"/>, <see cref="AtEnd"/>: false its first point).
/// </summary>
public sealed record AnalyticalSupport(
    string SourceId, string Name, SupportSpec Spec, IReadOnlyList<Vec2>? Region = null, Vec2? LineA = null, Vec2? LineB = null,
    double Width = 0, double Bottom = 0, double Top = 0, string? ForFrame = null, bool AtEnd = false);

/// <summary>
/// A roof framed in bars (a timber roof's members, which the frames give): its planes as its roof module solves them, each
/// its outline in 3D (eaves and overhangs included). An analysis lays the roof's area loads on its members plane by plane.
/// </summary>
public sealed record AnalyticalRoof(string SourceId, IReadOnlyList<IReadOnlyList<Vec3>> Planes);

/// <summary>An element the analytical model leaves out, and why.</summary>
public sealed record LeftOut(string SourceId, string Name, string IfcClass, string Reason);

/// <summary>What the modules give the analytical model.</summary>
public sealed class AnalyticalContribution
{
    public List<AnalyticalFrame> Frames { get; } = [];
    public List<AnalyticalShell> Shells { get; } = [];
    public List<AnalyticalSupport> Supports { get; } = [];
    public List<LeftOut> LeftOut { get; } = [];
    public List<AnalyticalLink> Links { get; } = [];
    public List<AnalyticalRoof> Roofs { get; } = [];
    /// <summary>The project's services (a wall's top under a roof needs the bodies' geometry).</summary>
    public IServices? Services { get; init; }
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
    public const string Columns = "columns", Beams = "beams", Members = "members", Walls = "walls", Slabs = "slabs", Roofs = "roofs", Supports = "supports";
    public static readonly string[] All = [Columns, Beams, Members, Walls, Slabs, Roofs, Supports];
}

/// <summary>
/// What the analytical model takes: the storeys (null: all), the kinds (null: all), every wall or only load-bearing ones, and
/// how the feet on footings are held (<see cref="Footings"/>: "fixed", the default, or "pinned"; a steel column base keeps
/// its connection's own).
/// </summary>
public sealed record AnalyticalOptions(IReadOnlySet<string>? Storeys = null, IReadOnlySet<string>? Kinds = null, bool AllWalls = false, string Footings = "fixed")
{
    public static readonly string[] FootingFixities = ["fixed", "pinned"];
    public bool Takes(string kind) => Kinds == null || Kinds.Contains(kind);
    public bool On(string? storeyId) => Storeys == null || storeyId == null || Storeys.Contains(storeyId);
}
