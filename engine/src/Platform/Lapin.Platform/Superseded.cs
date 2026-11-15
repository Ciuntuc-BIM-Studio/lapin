using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An element another was made from and kept hidden (forum topic 101: the solids a layered wall was made of, kept with
/// <c>supersededBy</c> naming the wall): while the element made of it is there, it is drawn, written and counted nowhere,
/// so nothing is counted twice. Deleted, that element gives the solids back; Show the original solids clears the mark.
/// <para>
/// The one rule (<see cref="Counts"/>) is read where the model's elements are gathered for everyone after: the views and mesh
/// exports (<see cref="VisibilityGraphics"/>), the IFC export, the quantities (<c>Session.Quantities</c>, which the quantity
/// takeoff, the schedules, the cost estimate and the planning's quantities read), the elements a schedule or a filter may
/// list (<see cref="ElementFilter.IsListed"/>), the analytical model (<c>AnalyticalModel.Build</c>), and the modules that
/// gather elements by rules of their own (clash detection, a planning task's elements).
/// </para>
/// </summary>
public static class Superseded
{
    public const string Key = "supersededBy";

    /// <summary>Whether the element is kept hidden behind another that still exists.</summary>
    public static bool Hidden(IModelView m, Entity e) => e.Str(Key) is { Length: > 0 } id && m.Get(id) != null;

    /// <summary>Whether the element is drawn, written and counted: everything but what is kept hidden behind another.</summary>
    public static bool Counts(IModelView m, Entity e) => !Hidden(m, e);

    /// <summary>Whether the entity of that id counts (an id that names nothing does: the caller says what it is).</summary>
    public static bool Counts(IModelView m, string id) => m.Get(id) is not { } e || Counts(m, e);
}
