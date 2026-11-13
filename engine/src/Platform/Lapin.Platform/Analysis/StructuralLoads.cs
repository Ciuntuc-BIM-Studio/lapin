using Lapin.Geometry;

namespace Lapin.Platform.Analysis;

/// <summary>
/// A load case as IFC's structural analysis view writes it (docs/plan-fem.md §8.2): its id (the entity's GlobalId), its
/// code (G1, Q-A), name, IfcActionTypeEnum and IfcActionSourceTypeEnum, and whether it carries the analytical members' own
/// weight (SelfWeightCoefficients (0, 0, −1)).
/// </summary>
public sealed record StructuralLoadCaseData(string Id, string Code, string Name, string ActionType, string ActionSource, bool SelfWeight);

/// <summary>
/// One action of a load case, in world metres and kN (docs/plan-fem.md §8.3, §8.4): <see cref="Kind"/> "planar" (an outline,
/// <see cref="Points"/> closed, kN/m², with its <see cref="Holes"/>), "curve" (two points, kN/m) or "point" (one point, kN);
/// <see cref="Direction"/> the force's unit vector (−Z for a load down). A user's load has its entity as <see cref="Id"/>;
/// a derived one (<see cref="Derived"/>) is named by the element it comes from (<see cref="SourceId"/>) and its kind
/// (<see cref="Key"/>: finishes, partitions, occupancy), so its GlobalId is the same at every export.
/// </summary>
public sealed record StructuralActionData(string Id, string Name, string CaseId, string Kind, IReadOnlyList<Vec3> Points, Vec3 Direction, double Value,
    string? SourceId, bool Derived, string? Key = null)
{
    public IReadOnlyList<IReadOnlyList<Vec3>> Holes { get; init; } = [];
    /// <summary>
    /// A planar action on a sloping face: true when its value is per area of its horizontal projection (snow, EN 1991-1-3),
    /// false when per area of the face itself (wind's pressure); null for one drawn on a plan (a horizontal face).
    /// </summary>
    public bool? Projected { get; init; }
}

/// <summary>A combination of load cases (EN 1990): its stable key (uls/&lt;caseId&gt;, slsq…), its kind (uls, sls-characteristic, sls-frequent, sls-quasi), its name written from its factors, and the factors.</summary>
public sealed record StructuralCombinationData(string Key, string Kind, string Name, IReadOnlyList<(string CaseId, double Factor)> Factors);

/// <summary>What the analysis module gives the IFC export: the cases, the actions each holds (the user's and the derived), the combinations.</summary>
public sealed record StructuralLoadsData(IReadOnlyList<StructuralLoadCaseData> Cases, IReadOnlyList<StructuralActionData> Actions, IReadOnlyList<StructuralCombinationData> Combinations);

/// <summary>
/// The project's loads as the IFC structural analysis view writes them (docs/plan-fem.md §8.7), offered by the analysis module
/// as a service; without it, the export writes the analytical model alone.
/// </summary>
public interface IStructuralLoads
{
    /// <summary>The loads on the analytical model the options make (an export's: without what it leaves out); null: all of it.</summary>
    StructuralLoadsData For(IQueryContext query, AnalyticalOptions? options = null);
}
