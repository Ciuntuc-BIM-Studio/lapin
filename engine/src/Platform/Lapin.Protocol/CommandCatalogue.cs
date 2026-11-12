using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// What each command and query of the engine reads (decision 489, docs/python.md): the arguments found in the engine's
/// source at build time (<c>scripts/command-catalogue.mjs</c> writes <c>catalogue.json</c>, embedded here), joined with
/// the commands and queries the running product has. Served as <c>session.catalogue</c>, so the MCP bridge and the Python
/// package know the arguments in an installed app too, where there is no source. A command the source catalogue lacks (a
/// premium module's, built elsewhere) is listed with no arguments.
/// </summary>
public static class CommandCatalogue
{
    const string Resource = "Lapin.Protocol.catalogue.json";

    static readonly Lazy<(Dictionary<string, JsonObject> Commands, Dictionary<string, JsonObject> Queries)> Source = new(Read);

    static (Dictionary<string, JsonObject>, Dictionary<string, JsonObject>) Read() => Parse(typeof(CommandCatalogue).Assembly, Resource);

    static (Dictionary<string, JsonObject>, Dictionary<string, JsonObject>) Parse(System.Reflection.Assembly asm, string resource)
    {
        using var s = asm.GetManifestResourceStream(resource);
        if (s == null) return ([], []);
        var root = JsonNode.Parse(s) as JsonObject ?? [];
        static Dictionary<string, JsonObject> ByName(JsonNode? list) =>
            (list as JsonArray ?? []).OfType<JsonObject>().Where(o => (string?)o["name"] != null).GroupBy(o => (string)o["name"]!).ToDictionary(g => g.Key, g => g.First());
        return (ByName(root["commands"]), ByName(root["queries"]));
    }

    /// <summary>
    /// What a module built elsewhere (a premium module's own repository, docs/overrides.md) says of its own commands: the
    /// catalogue its repository made with <c>scripts/command-catalogue.mjs --root &lt;its engine/src&gt; --out catalogue.json</c>,
    /// embedded in its assembly as <see cref="ModuleResource"/>. Read once per assembly.
    /// </summary>
    public const string ModuleResource = "Lapin.catalogue.json";

    static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Reflection.Assembly, (Dictionary<string, JsonObject> Commands, Dictionary<string, JsonObject> Queries)> Modules = new();

    static (Dictionary<string, JsonObject> Commands, Dictionary<string, JsonObject> Queries) OfModules(Session session)
    {
        var commands = new Dictionary<string, JsonObject>();
        var queries = new Dictionary<string, JsonObject>();
        foreach (var asm in session.Catalog.Modules.Select(m => m.GetType().Assembly).Distinct())
        {
            var (c, q) = Modules.GetOrAdd(asm, a => Parse(a, ModuleResource));
            foreach (var (k, v) in c) commands.TryAdd(k, v);
            foreach (var (k, v) in q) queries.TryAdd(k, v);
        }
        return (commands, queries);
    }

    /// <summary>The source catalogue's names (tests: every command a module registers is in it).</summary>
    public static IReadOnlyCollection<string> SourceCommands => Source.Value.Commands.Keys;
    public static IReadOnlyCollection<string> SourceQueries => Source.Value.Queries.Keys;

    /// <summary>
    /// The product's commands and queries with their arguments: <c>{format, version, commands: [{name, module, label,
    /// args: [{name, type?}]}], queries: [{name, module, args}]}</c>, each sorted by name.
    /// </summary>
    public static JsonObject For(Session session)
    {
        var (commands, queries) = Source.Value;
        var (ownCommands, ownQueries) = OfModules(session);
        // Lapin's own catalogue first; a module built elsewhere fills in its own commands from the catalogue it carries.
        JsonArray Args(Dictionary<string, JsonObject> from, string name) =>
            (from.TryGetValue(name, out var o) ? o : (ReferenceEquals(from, commands) ? ownCommands : ownQueries).GetValueOrDefault(name)) is { } e && e["args"] is JsonArray a ? (JsonArray)a.DeepClone() : [];
        var c = new JsonArray();
        foreach (var e in session.Catalog.Commands.OrderBy(e => e.Name, StringComparer.Ordinal))
            c.Add((JsonNode)new JsonObject { ["name"] = e.Name, ["module"] = e.Module, ["label"] = e.Label, ["args"] = Args(commands, e.Name) });
        var q = new JsonArray();
        foreach (var name in session.Catalog.QueryNames.Order(StringComparer.Ordinal))
            q.Add((JsonNode)new JsonObject
            {
                ["name"] = name,
                ["module"] = (queries.TryGetValue(name, out var o) || ownQueries.TryGetValue(name, out o)) && (string?)o["module"] is { } m ? m : name.Split('.')[0],
                ["args"] = Args(queries, name),
            });
        return new JsonObject { ["format"] = 1, ["version"] = Session.AppVersion, ["commands"] = c, ["queries"] = q };
    }
}
