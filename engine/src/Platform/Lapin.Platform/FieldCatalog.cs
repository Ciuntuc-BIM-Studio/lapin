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

/// <summary>
/// A field a module works out from the model and its geometry (the room a piece of furniture stands in, by the rooms' outlines as
/// built): read where a query is at hand — schedules, filters, the fields' pickers — not by tags, which read the model alone.
/// </summary>
public sealed record QueriedField(string Key, string Label, Func<IQueryContext, Entity, string?> Value);

/// <summary>The fields modules work out, by class (<see cref="DerivedField"/>, <see cref="QueriedField"/>).</summary>
public static class DerivedFields
{
    static readonly ConcurrentDictionary<string, DerivedField[]> ByClass = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, QueriedField[]> Queried = new(StringComparer.Ordinal);

    /// <summary>A field of a class's worked out with the geometry at hand; one of the same key again replaces it.</summary>
    public static void AddQueried(string cls, QueriedField field) =>
        Queried.AddOrUpdate(cls, [field], (_, old) => [.. old.Where(f => !string.Equals(f.Key, field.Key, StringComparison.OrdinalIgnoreCase)), field]);

    public static IReadOnlyList<QueriedField> QueriedOf(string cls) => Queried.TryGetValue(cls, out var l) ? l : [];

    /// <summary>Its value on <paramref name="e"/>, when its class has such a field and the element no attribute of that key; null else.</summary>
    public static string? Value(IQueryContext q, Entity e, string key)
    {
        var k = TagText.Squash(key);
        return QueriedOf(e.Class).FirstOrDefault(f => TagText.Squash(f.Key) == k) is { } f && e.Attrs[f.Key] is null ? f.Value(q, e) : null;
    }

    /// <summary>A field of a class's; one of the same key again replaces it.</summary>
    public static void Add(string cls, DerivedField field) =>
        ByClass.AddOrUpdate(cls, [field], (_, old) => [.. old.Where(f => !string.Equals(f.Key, field.Key, StringComparison.OrdinalIgnoreCase)), field]);

    /// <summary>A measure as a worked-out field gives it: to six decimals, so sums of it round as the measure's own would.</summary>
    public static string Number(double v) => Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture);

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

    // ------------------------------------------------------------ fields named by the element's own definition

    static readonly ConcurrentDictionary<(string Owner, string Class), NamedFieldsOf> Named = new();

    /// <summary>
    /// Fields of an element of <paramref name="cls"/> whose names its own definition gives (a placed Studio part's parameters, named
    /// by its part's table, R6 of docs/plan-forum-round-1126.md), worked out by <paramref name="of"/> on every ask and never stored;
    /// <paramref name="owner"/> names who adds them, so adding again replaces them. Read by formulas, tags, schedules and filters
    /// after the user's own parameters and before the element's attributes and quantities.
    /// </summary>
    public static void AddNamed(string owner, string cls, NamedFieldsOf of) => Named[(owner, cls)] = of;

    /// <summary>The fields named by <paramref name="e"/>'s own definition, in their order (none when nothing gives it any).</summary>
    public static IReadOnlyList<NamedField> NamedOf(IModelView m, Entity e)
    {
        List<NamedField>? o = null;
        foreach (var ((_, cls), of) in Named)
            if (cls == e.Class && of(m, e) is { Count: > 0 } l) (o ??= []).AddRange(l);
        return o ?? (IReadOnlyList<NamedField>)[];
    }

    /// <summary>
    /// The field <paramref name="name"/> names among <paramref name="e"/>'s named fields (case and spaces aside), alone or after its
    /// set (<c>Lapin_PartParameters.L</c>); null when none has that name.
    /// </summary>
    public static NamedField? FindNamed(IModelView m, Entity e, string name)
    {
        if (Named.IsEmpty || name.Length == 0) return null;
        var dot = name.LastIndexOf('.');
        var key = TagText.Squash(dot >= 0 ? name[(dot + 1)..] : name);
        var set = dot > 0 ? TagText.Squash(name[..dot]) : null;
        foreach (var f in NamedOf(m, e))
            if (TagText.Squash(f.Key) == key && (set == null || f.Set != null && TagText.Squash(f.Set) == set)) return f;
        return null;
    }
}

/// <summary>
/// A field named by an element's own definition (<see cref="DerivedFields.AddNamed"/>): its key (what tokens, formulas and schedule
/// columns name), its label, its kind (length, number, integer, angle, bool, text), its value (a number in SI as formulas read it —
/// metres, degrees, 1 or 0 — and the text a tag writes) and the property set it is written in.
/// </summary>
public sealed record NamedField(string Key, string Label, string Kind, double? Number, string? Text, string? Set = null, string? Description = null)
{
    /// <summary>As a tag writes it: its text, else its number.</summary>
    public string Shown => Text ?? (Number is { } n ? Expr.Format(n, null) : "");

    /// <summary>As a formula reads it: a yes/no as one, a number as a number, a text as a text.</summary>
    public XValue AsValue => Kind == "bool" && Number is { } b ? XValue.Of(b != 0) : Number is { } n ? XValue.Of(n) : XValue.Read(Text ?? "");
}

/// <summary>The fields named by one element's own definition, worked out from the model as it is.</summary>
public delegate IReadOnlyList<NamedField> NamedFieldsOf(IModelView m, Entity e);

/// <summary>
/// Every field an element's tags, live texts and formulas can read (forum topic 84, the Parameters inspector): its own — the
/// fields every element has, its attributes, what its module works out, its quantities, its parameters, the standard sets'
/// properties and those it came with — then each scope's it has (its type, storey, building, site, the project, its group,
/// assembly, host and, as modules add them, its rooms), each field with the token to write, its value now and a few words on
/// what it is. Read from the model as it is on every ask; nothing kept.
/// </summary>
public static partial class FieldCatalog
{
    /// <summary>Attributes that are Lapin's own bookkeeping, not fields a tag would show.</summary>
    static readonly HashSet<string> Internal = new(StringComparer.OrdinalIgnoreCase)
    {
        "params", "properties", "exportAs", "ifcClass", "reference", "referenceLine", "auto", "storeyId", "viewId", "objectType", IfcSource.Key, Plan2d.Key, ReferenceQuantities.Key,
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
        foreach (var d in DerivedFields.QueriedOf(e.Class)) Add(derived, d.Key, d.Label, "Worked out on every ask, never stored (schedules and filters read it)", value: DerivedFields.Value(q, e, d.Key) ?? "");
        groups.Add(derived);

        var measured = new Group("quantities", "Its quantities", null, e);
        static string Measure(Quantity x) => x.Value.ToString("0.###", CultureInfo.InvariantCulture) + (x.Unit.Length > 0 ? " " + x.Unit : "");
        var all = q.Quantities(e.Id).SelectMany(s => s.Quantities).ToList();
        foreach (var x in all)
            Add(measured, x.Name, x.Label ?? x.Name, "A quantity, read in formulas ({= NetArea * 1.1})", x.Kind.ToLowerInvariant(), Measure(x), formulaOnly: true);
        // The common names (Area, Perimeter, Volume…) its class's quantities answer to (QuantityNames): a room's Area is its NetFloorArea.
        foreach (var common in QuantityNames.Offered)
            if (QuantityNames.Resolve(common, k => all.FirstOrDefault(y => TagText.Squash(y.Name) == k)?.Value) is { } hit && all.First(y => y.Name == hit.Name) is var found)
                Add(measured, common, common, $"Its {hit.Name}, by a common name", found.Kind.ToLowerInvariant(), Measure(found), formulaOnly: true);
        groups.Add(measured);

        var parameters = new Group("params", "Its parameters", null, e);
        foreach (var p in StoredParams(e))
            if (((string?)p["name"])?.Trim() is { Length: > 0 } name)
                Add(parameters, Params.SetOf(p) is { } set ? $"{set}.{name}" : name, name,
                    ParamFormulas.IsFormula(p) ? "Your parameter, worked out by its formula" : "Your parameter", (string?)p["kind"]);
        // The parameters its own definition names (a placed part's: its part's table), worked out on every ask.
        foreach (var nf in DerivedFields.NamedOf(m, e))
            Add(parameters, nf.Key, nf.Label, nf.Description ?? "A parameter of its part, worked out on every ask", nf.Kind, Value(nf.Key));
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

        var aliases = new Group("aliases", "Your office's names (aliases)", null, Spatial.Project(m));
        foreach (var x in FieldAliases.All(m))
            Add(aliases, x.Name, x.Name, x.Description ?? "Your office's name for the first of: " + string.Join(", ", x.Sources));
        groups.Add(aliases);

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
        // The fields across the model or a filter's elements, and one field's values (FieldValues).
        RegisterModel(r);
        FieldAliases.Register(r);
    }
}
