using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>
/// Visual graphs (docs/plan-visual-graphs.md, decision 522): a project's graphs, each a <see cref="Class"/> entity holding
/// its nodes and wires (<see cref="GraphDoc"/>), saved, renamed and deleted by commands (one undo step each, so they go into
/// undo, versions, shared sessions and the rule of an absent module like any content), written also as
/// <c>graphs/&lt;name&gt;.json</c> in the .lapin. <c>graphs.evaluate</c> is a query: the graph's values and the ghost of what
/// it would make, the model untouched (<see cref="Preview"/>); <c>graphs.bake</c> makes it, one undo step, refused whole
/// when one of its commands fails. A graph runs no code: its nodes are the module's own and the product's commands and
/// queries (those reading files or running scripts refused, <see cref="GraphLimits.Refused"/>), so it asks no trust.
/// </summary>
public sealed class GraphsModule : ILapinModule, IPackageTexts, IDrivenElements
{
    public const string ModuleId = "graphs";
    public const string Class = "LapinGraph";

    public string Id => ModuleId;
    public string Name => "Graphs";
    string IPackageTexts.Module => ModuleId;

    /// <summary>A graph's name: letters, digits, spaces and the usual marks, no folder, not hidden; at most 100 characters.</summary>
    static readonly Regex NamePattern = new(@"^[^/\\:*?""<>|\x00-\x1f.][^/\\:*?""<>|\x00-\x1f]{0,99}$", RegexOptions.CultureInvariant);

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Graph",
        [
            new("description", "Description", "text"),
            new("graph", "Graph", "text", ReadOnly: true),
        ], "#7c4dff", Kind: "resource", Template: false));
        r.Service<IPackageTexts>(this);
        r.Service<IDrivenElements>(this);

        // {id?, name, graph, description?}: a graph kept in the project, a new one or (by id) one of its own changed.
        r.Command("save", "Save graph", (ctx, a) =>
        {
            var existing = (string?)a["id"] is { Length: > 0 } id ? Graph(ctx.Model, id) : null;
            var name = a.ContainsKey("name") || existing == null ? NameOf(a) : existing.Name;
            RefuseTaken(ctx.Model, name, existing?.Id);
            var attrs = existing?.CloneAttrs() ?? [];
            if (a.ContainsKey("graph") || existing == null) attrs["graph"] = GraphDoc.Read(a["graph"] ?? new JsonObject()).ToJson();
            if (a.ContainsKey("description"))
            {
                if (((string?)a["description"])?.Trim() is { Length: > 0 } d) attrs["description"] = d; else attrs.Remove("description");
            }
            if (existing == null)
            {
                var e = new Entity(ctx.NewId(), Class, name, attrs);
                ctx.Add(e);
                return Json(e, created: true);
            }
            var after = existing.With(name: name, attrs: attrs);
            if (after.Name != existing.Name || attrs.ToJsonString() != existing.Attrs.ToJsonString()) ctx.Replace(after);
            return Json(after, created: false);
        });

        r.Command("rename", "Rename graph", (ctx, a) =>
        {
            var e = Graph(ctx.Model, (string?)a["id"]);
            var name = NameOf(a);
            RefuseTaken(ctx.Model, name, e.Id);
            if (name != e.Name) ctx.Replace(e.With(name: name));
            return Json(e.With(name: name), created: false);
        });

        r.Command("delete", "Delete graph", (ctx, a) =>
        {
            ctx.Remove(Graph(ctx.Model, (string?)a["id"]).Id);
            return null;
        });

        // {id} or {graph}: the graph made, one undo step; refused whole, with the first failure, when one of its commands fails.
        r.Command("bake", "Bake graph", (ctx, a) =>
        {
            var doc = DocOf(ctx.Model, a);
            var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("No commands to bake here.");
            var tracking = new TrackingContext(ctx, catalog, new ModelQuery(ctx.Model, catalog));
            var e = new Evaluator(doc, ctx.Model, catalog, tracking, tracking.Query, GraphLimits.BakeTime).RunAll();
            if (e.Results.FirstOrDefault(x => x.Value.IsCommand && (x.Value.Error != null || x.Value.Failed > 0)) is { Value: { } bad, Key: { } at })
                throw new CommandException($"The graph was not baked: „{Label(doc.Node(at)!)}” failed: {bad.Error ?? bad.Warning}");
            return new JsonObject { ["made"] = new JsonArray(e.Made.Select(x => (JsonNode)x).ToArray()), ["calls"] = e.Calls };
        });

        r.Query("list", (q, _) => new JsonArray(All(q.Model).Select(e => (JsonNode)Json(e, created: false)).ToArray()));

        r.Query("get", (q, a) =>
        {
            var e = Graph(q.Model, (string?)a["id"]);
            var o = Json(e, created: false);
            o["graph"] = e.Attrs["graph"]?.DeepClone();
            return o;
        });

        // {id} or {graph} (an unsaved one, or {id, graph}: the saved one as the editor has it now): its values and its ghost.
        r.Query("evaluate", (q, a) => Preview.Of(q, DocOf(q.Model, a)));

        // The module's own nodes with their ports, and the product's commands and queries a graph may run.
        r.Query("nodes", (q, _) =>
        {
            var catalog = q.Services as ModuleCatalog;
            return new JsonObject
            {
                ["nodes"] = new JsonArray(Builtins.All.Select(d => (JsonNode)d.ToJson()).ToArray()),
                ["commands"] = new JsonArray((catalog?.Commands ?? []).Where(c => GraphLimits.Refused(c.Name) == null).OrderBy(c => c.Name, StringComparer.Ordinal)
                    .Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["label"] = c.Label, ["module"] = c.Module }).ToArray()),
                ["queries"] = new JsonArray((catalog?.QueryNames ?? []).Where(n => GraphLimits.Refused(n) == null).Order(StringComparer.Ordinal).Select(n => (JsonNode)n).ToArray()),
            };
        });
    }

    static string Label(GraphNode n) => n.Label ?? Builtins.Of(n.Kind)?.Label ?? n.Kind.Split(':')[^1];

    static GraphDoc DocOf(IModelView m, JsonObject a)
    {
        if (a["graph"] is JsonObject g) return GraphDoc.Read(g);
        var e = Graph(m, (string?)a["id"]);
        return GraphDoc.Read(e.Attrs["graph"] ?? new JsonObject());
    }

    static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class).OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase);

    static Entity Graph(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } e ? e : throw new CommandException("The project has no such graph.");

    static string NameOf(JsonObject a)
    {
        var name = ((string?)a["name"])?.Trim() ?? throw new CommandException("Give the graph a name first.");
        if (!NamePattern.IsMatch(name)) throw new CommandException($"„{name}” is not a graph's name: letters, digits and spaces, up to 100.");
        return name;
    }

    static void RefuseTaken(IModelView m, string name, string? self)
    {
        if (m.OfClass(Class).Any(e => e.Id != self && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new CommandException($"The project has a graph called „{name}” already.");
    }

    static JsonObject Json(Entity e, bool created)
    {
        var g = e.Attrs["graph"] as JsonObject;
        var o = new JsonObject { ["id"] = e.Id, ["name"] = e.Name, ["nodes"] = (g?["nodes"] as JsonArray)?.Count ?? 0 };
        if (e.Str("description") is { } d) o["description"] = d;
        if (created) o["created"] = true;
        return o;
    }

    // ── the package's copies: graphs/<name>.json ──

    public IEnumerable<(string Path, string Text)> Texts(IModelView model) =>
        model.OfClass(Class).Where(e => NamePattern.IsMatch(e.Name))
            .Select(e => ($"graphs/{e.Name}.json", Indented(e.Attrs["graph"] ?? new JsonObject())));

    static string Indented(JsonNode n)
    {
        using var ms = new MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true })) n.WriteTo(w);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    // ── a graph changes through the commands above only ──

    public bool Drives(IModelView m, Entity e) => e.Class == Class;

    public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
    {
        if (edit.Command is "element.delete") return;
        throw new CommandException("A graph is changed in the Graphs panel (View → Graphs), not here.");
    }
}
