using System.Text.Json.Nodes;

namespace Lapin.Platform;

/// <summary>
/// What depends on what, as the engine already knows it (docs/dependencies.md): each element's geometry
/// is built through a view that records what it reads, and an edit builds again whatever read what it
/// touched. The query reads that record; nothing is declared or saved apart from the model.
/// </summary>
public static class Dependencies
{
    /// <summary>
    /// <c>core.dependents {id | ids}</c>: <c>dependents</c>, the elements built from the given ones (they
    /// change when these change, or are deleted), the given ones left out; and, for one id,
    /// <c>dependencies</c>, the entities it was built from.
    /// </summary>
    public static void Register(IModuleRegistry r) => r.Query("core.dependents", (q, a) =>
    {
        var ids = Args.Ids(a).Where(id => q.Model.Get(id) != null).ToList();
        var given = ids.ToHashSet();
        var dependents = ids.SelectMany(q.DependentsOf).Where(id => !given.Contains(id)).Distinct().Order(StringComparer.Ordinal);
        var o = new JsonObject { ["dependents"] = new JsonArray([.. dependents.Select(id => (JsonNode)id)]) };
        if (ids.Count == 1) o["dependencies"] = new JsonArray([.. q.DependenciesOf(ids[0]).Select(id => (JsonNode)id)]);
        return o;
    });
}
