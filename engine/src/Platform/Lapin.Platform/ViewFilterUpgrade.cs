using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A project saved before the one filter engine (audit D3) keeps what its view filters take. A rule asking whether the
/// phase has a value (or has none) asked whether the element's phase had been set: an element left unphased had none. The
/// phase rule now reads the phase every element is created in (the default one when it is unset), which always has a
/// value, so such a rule becomes the same question on the attribute as stored (<c>param phaseCreated</c>). Then the project
/// is marked <see cref="Rev"/>. Every other rule keeps its words; what it takes differently is written in
/// <c>FilterCharacterizationTests</c>.
/// </summary>
public sealed class ViewFilterUpgrade : IProjectUpgrade
{
    /// <summary>The project's mark of the filter engine its view filters were written for (a new project has today's).</summary>
    public const string RevKey = "filterRev";
    public const int Rev = 1;

    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        if (entities.FirstOrDefault(e => e.Class == IfcClass.Project) is not { } project) return (entities, relations);
        if ((Json.Number(project.Attrs[RevKey]) ?? 0) >= Rev) return (entities, relations);
        var attrs = project.CloneAttrs();
        attrs[RevKey] = Rev;
        if (attrs[ViewFilters.Key] is JsonArray filters)
            foreach (var rule in filters.OfType<JsonObject>().SelectMany(f => (f["rules"] as JsonArray ?? []).OfType<JsonObject>()))
                if ((string?)rule["field"] == "phase" && (string?)rule["op"] is "hasValue" or "hasNoValue")
                {
                    rule["field"] = "param";
                    rule["name"] = "phaseCreated";
                }
        var upgraded = project.With(attrs: attrs);
        return (entities.Select(e => e.Id == project.Id ? upgraded : e).ToList(), relations);
    }
}
