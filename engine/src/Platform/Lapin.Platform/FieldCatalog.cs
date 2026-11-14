using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform.Documents;

namespace Lapin.Platform;

/// <summary>
/// A field a module works out for elements of a class (forum topic 84): read by tags, live text and formulas by its key, as an
/// attribute would be, and never stored. A room's <c>finish.floor</c> is its finish's top floor layer's material, worked out on
/// every ask from the finish the room has now. An attribute of the element of the same key comes first (a room's own
/// <c>finishFloor</c> over the one its finish gives), and so do the user's own parameters.
/// </summary>
public sealed record DerivedField(string Key, string Label, Func<IModelView, Entity, string?> Value);

/// <summary>The fields modules work out, by class (<see cref="DerivedField"/>).</summary>
public static class DerivedFields
{
    static readonly ConcurrentDictionary<string, DerivedField[]> ByClass = new(StringComparer.Ordinal);

    /// <summary>A field of a class's; one of the same key again replaces it.</summary>
    public static void Add(string cls, DerivedField field) =>
        ByClass.AddOrUpdate(cls, [field], (_, old) => [.. old.Where(f => !string.Equals(f.Key, field.Key, StringComparison.OrdinalIgnoreCase)), field]);

    public static IReadOnlyList<DerivedField> Of(string cls) => ByClass.TryGetValue(cls, out var l) ? l : [];

    /// <summary>The field <paramref name="key"/> names on <paramref name="e"/> (case and spaces aside); null when its class has none.</summary>
    public static DerivedField? Find(Entity e, string key)
    {
        var k = TagText.Squash(key);
        return Of(e.Class).FirstOrDefault(f => TagText.Squash(f.Key) == k);
    }

    /// <summary>Its value on <paramref name="e"/>, when its class has such a field and the element no attribute of that key.</summary>
    public static string? Value(IModelView m, Entity e, string key) =>
        Find(e, key) is { } f && e.Attrs[f.Key] is null ? f.Value(m, e) : null;
}

/// <summary>
/// Every field an element's tags, live texts and formulas can read (forum topic 84, the Parameters inspector): its own — the
/// fields every element has, its attributes, what its module works out, its quantities, its parameters, the standard sets'
/// properties and those it came with — then each scope's it has (its type, storey, building, site, the project, its group,
/// assembly, host and, as modules add them, its rooms), each field with the token to write, its value now and a few words on
/// what it is. Read from the model as it is on every ask; nothing kept.
/// </summary>
public static class FieldCatalog
{
    /// <summary>Attributes that are Lapin's own bookkeeping, not fields a tag would show.</summary>
    static readonly HashSet<string> Internal = new(StringComparer.OrdinalIgnoreCase)
    {
        "params", "properties", "exportAs", "ifcClass", "reference", "auto", "storeyId", "viewId", "objectType", IfcSource.Key, Plan2d.Key,
    };

    static readonly (string Key, string Description)[] Builtins =
    [
        ("mark", "Its mark: its own, else its type's, else its type's name"),
        ("name", "Its name"),
        ("type", "Its type's name (its own name without a type)"),
        ("class", "Its IFC class, without Ifc"),
    ];

    /// <summary>What each scope is, as the inspector says it.</summary>
    static string ScopeLabel(string scope) => scope.ToLowerInvariant() switch
    {
        "type" => "Its type",
        "storey" => "Its storey",
        "building" => "Its building",
        "site" => "Its site",
        "project" => "The project",
        "group" => "Its group",
        "assembly" => "The assembly it is a part of",
        "host" => "Its host (a door's or window's wall)",
        "room" => "The room it stands in",
        "fromroom" => "The room on its exterior side",
        "toroom" => "The room on its interior side",
        _ => scope,
    };

    sealed class Group(string id, string label, string? scope, Entity? owner)
    {
        public readonly JsonArray Fields = [];
        public JsonObject ToJson() => new()
        {
            ["id"] = id, ["label"] = label, ["scope"] = scope,
            ["entityId"] = owner?.Id, ["entityName"] = owner?.Name, ["entityClass"] = owner?.Class,
            ["fields"] = Fields,
        };
    }

    /// <summary>The fields of <paramref name="e"/>, grouped: <c>{id, name, class, groups: [{id, label, scope, entityId, fields: [{token, label, value, description, kind, formulaOnly}]}]}</c>.</summary>
    public static JsonObject Available(IQueryContext q, Entity e)
    {
        var m = q.Model;
        var units = DraftingResources.Book(m).Units;
        var quantities = ParamFormulas.Quantities(q, e.Id);
        var groups = new List<Group>();
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);

        string Value(string token) => TagText.Value(m, e, token, null, units, quantities);

        // ── its own
        var own = new Group("element", "This element", null, e);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(Group g, string token, string label, string description, string? kind = null, string? value = null, bool formulaOnly = false)
        {
            if (!seen.Add(token)) return;
            g.Fields.Add((JsonNode)new JsonObject
            {
                ["token"] = token, ["label"] = label, ["value"] = value ?? Value(token), ["description"] = description, ["kind"] = kind, ["formulaOnly"] = formulaOnly,
            });
        }
        foreach (var (key, description) in Builtins) Add(own, key, key, description);
        groups.Add(own);

        var attributes = new Group("attributes", "Its attributes", null, e);
        foreach (var (key, label, kind) in AttributesOf(q, e)) Add(attributes, key, label, "An attribute of the element", kind);
        groups.Add(attributes);

        var derived = new Group("derived", "Worked out", null, e);
        foreach (var d in DerivedFields.Of(e.Class)) Add(derived, d.Key, d.Label, "Worked out on every ask, never stored");
        groups.Add(derived);

        var measured = new Group("quantities", "Its quantities", null, e);
        foreach (var set in q.Quantities(e.Id))
            foreach (var x in set.Quantities)
                Add(measured, x.Name, x.Label ?? x.Name, "A quantity, read in formulas ({= NetArea * 1.1})", x.Kind.ToLowerInvariant(),
                    x.Value.ToString("0.###", CultureInfo.InvariantCulture) + (x.Unit.Length > 0 ? " " + x.Unit : ""), formulaOnly: true);
        groups.Add(measured);

        var parameters = new Group("params", "Its parameters", null, e);
        foreach (var p in StoredParams(e))
            if (((string?)p["name"])?.Trim() is { Length: > 0 } name)
                Add(parameters, Params.SetOf(p) is { } set ? $"{set}.{name}" : name, name,
                    ParamFormulas.IsFormula(p) ? "Your parameter, worked out by its formula" : "Your parameter", (string?)p["kind"]);
        groups.Add(parameters);

        var standard = new Group("standard", "IFC standard properties", null, e);
        foreach (var s in StandardPsets.All.Where(s => StandardPsets.AppliesTo(s, e.Class)))
            foreach (var (name, kind) in s.Properties)
                Add(standard, $"{s.Name}.{name}", name, "A property of an IFC standard set (empty until it is given a value)", kind);
        foreach (var x in new[] { e, type })
            if (x?.Attrs["properties"] is JsonObject sets)
                foreach (var (set, props) in sets)
                    if (props is JsonObject po)
                        foreach (var (name, _) in po) Add(standard, $"{set}.{name}", name, "From the file it came from");
        groups.Add(standard);

        var globals = new Group("globals", "Project parameters, by their key", null, Spatial.Project(m));
        foreach (var c in ParamScopes.Globals(m))
            Add(globals, c.Key, c.Label, "A parameter of the project's (Manage → Project information): read by its key from any element", c.Type);
        groups.Add(globals);

        // ── its scopes'
        foreach (var scope in ParamScopes.Names)
        {
            if (ParamScopes.Of(m, e, scope) is not { } owner || owner.Id == e.Id) continue;
            var g = new Group(scope, ScopeLabel(scope), scope, owner);
            Add(g, $"{scope}.name", "name", "Its name");
            if (owner.Class == IfcClass.Project)
            {
                foreach (var (key, label) in ProjectInfo.Fields) Add(g, $"project.{key}", label, "The project's information (Manage → Project information)");
                foreach (var c in ParamScopes.Globals(m)) Add(g, $"project.{c.Key}", c.Label, "A parameter of the project's (Manage → Project information)", c.Type);
            }
            else
            {
                Add(g, $"{scope}.mark", "mark", "Its mark: its own, else its type's, else its type's name");
                foreach (var (key, label, kind) in AttributesOf(q, owner)) Add(g, $"{scope}.{key}", label, "An attribute of the element", kind);
                foreach (var d in DerivedFields.Of(owner.Class)) Add(g, $"{scope}.{d.Key}", d.Label, "Worked out on every ask, never stored");
            }
            foreach (var p in StoredParams(owner))
                if (((string?)p["name"])?.Trim() is { Length: > 0 } name)
                    Add(g, Params.SetOf(p) is { } set ? $"{scope}.{set}.{name}" : $"{scope}.{name}", name, "Your parameter", (string?)p["kind"]);
            groups.Add(g);
        }

        return new JsonObject
        {
            ["id"] = e.Id, ["name"] = e.Name, ["class"] = e.Class,
            ["groups"] = new JsonArray([.. groups.Where(g => g.Fields.Count > 0).Select(g => (JsonNode)g.ToJson())]),
            ["functions"] = new JsonArray([.. Expr.FunctionNames.Select(f => (JsonNode)f)]),
        };
    }

    static IEnumerable<JsonObject> StoredParams(Entity e) => e.Attrs["params"] is JsonArray a ? a.OfType<JsonObject>() : [];

    /// <summary>
    /// An entity's attributes a tag can show: its class's fields (by their labels) and any other text, number or yes/no it keeps,
    /// not Lapin's bookkeeping nor what names another entity (an id).
    /// </summary>
    static IEnumerable<(string Key, string Label, string? Kind)> AttributesOf(IQueryContext q, Entity e)
    {
        var described = q.Describe(e.Class)?.Fields ?? [];
        foreach (var (key, v) in e.Attrs)
        {
            if (v is not JsonValue value || Internal.Contains(key) || TagText.Builtin.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
            if (value.TryGetValue<string>(out var s) && (GlobalId.IsValid(s) || s.Length > 200)) continue;
            var d = described.FirstOrDefault(f => f.Key == key);
            yield return (key, d?.Label ?? key, d?.Kind);
        }
    }

    public static void Register(IModuleRegistry r)
    {
        // Every field an element's tags and formulas can read, grouped, with its value now and what it is (forum topic 84).
        r.Query("fields.available", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            return Available(q, e);
        });
    }
}
