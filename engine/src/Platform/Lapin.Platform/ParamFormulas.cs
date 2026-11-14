using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Parameters worked out by a formula (docs/plan-user-extensibility.md §5.3): a parameter of the user's given a <c>formula</c>
/// instead of a value (<c>Height * 2</c>, <c>round(NetArea * 1.1; 2)</c>, <c>if(IsExternal; 0.18; 0.35)</c>) holds no value of its own:
/// it is worked out on every ask (never stored, as everything that can be worked out), from the element's other parameters and its
/// type's, its own attributes (height, width, a room's number…), where the caller has them its quantities (Length, NetArea,
/// NetVolume…), its scopes' (decision 551: <c>storey.code</c>, <c>project.number</c>, <c>host.FireRating</c>, <c>room.name</c>,
/// <see cref="ParamScopes"/>) and the project's own parameters by their key. A text parameter's formula gives a text (decision 550):
/// an expression with texts (<c>concat(storey.code; "."; number)</c>, <c>upper(name)</c>, <c>storey.code &amp; "." &amp; pad(number; 2)</c>),
/// or a template, a text with fields in braces as a tag writes them (<c>{storey.code}.{Hall}.{Flat}.{number}</c>, formulas
/// <c>{= …}</c> in it too). A formula naming another formula reads it worked out; one reading itself, through others or not, gives
/// no value; one that does not read gives none either, its reason told.
/// </summary>
public static class ParamFormulas
{
    public const string Key = "formula";
    const int MaxDepth = 16;

    /// <summary>The formulas being worked out on this thread (element and parameter): one asked again reads itself.</summary>
    [ThreadStatic] static HashSet<string>? _busy;

    public static bool IsFormula(JsonObject p) => (string?)p[Key] is { Length: > 0 };

    /// <summary>Whether a formula is a template: a text with fields in braces (<c>{storey.code}.{number}</c>), not an expression.</summary>
    public static bool IsTemplate(string formula) => formula.Contains('{');

    /// <summary>A formula as a command gives it, checked that it reads (its names are read at its evaluation).</summary>
    public static string Check(string formula)
    {
        var f = formula.Trim();
        if (f.Length == 0) throw new CommandException("A formula cannot be empty.");
        if (f.Length > Expr.MaxLength) throw new CommandException($"A formula is at most {Expr.MaxLength} characters.");
        if (IsTemplate(f))
        {
            // Every brace closed, every {= …} in it a formula that reads.
            var depth = 0;
            foreach (var c in f) { depth += c == '{' ? 1 : c == '}' ? -1 : 0; if (depth is < 0 or > 1) throw new CommandException("A field's braces do not match: {field}."); }
            if (depth != 0) throw new CommandException("A field's brace is not closed: {field}.");
            var errors = new List<string>();
            LiveText.Formulas(f, _ => XValue.Of(1), errors);
            if (errors.Count > 0) throw new CommandException(errors[0]);
            return f;
        }
        var (_, error) = Expr.TryEvalValue(f, _ => XValue.Of(1));
        if (error != null) throw new CommandException(error);
        return f;
    }

    /// <summary>
    /// What a formula's name reads on <paramref name="e"/> (<paramref name="type"/> its type): a scope's entity first (<c>storey.code</c>),
    /// then its parameters and its type's (squashed names: „Fire rating” is FireRating; a set's name before a dot is left out),
    /// worked out when they are formulas; its numeric attributes and its type's; <paramref name="more"/> (its quantities); its
    /// texts (a room's number, function); its name, mark, type and class; and last a parameter of the project's of that key.
    /// </summary>
    public static XValue? Read(IModelView m, Entity e, Entity? type, Func<string, double?>? more, string name)
    {
        if (ParamScopes.Split(m, e, name) is { } scoped)
        {
            if (scoped.Owner is not { } owner) return null;
            if (owner.Class == IfcClass.Project && ParamScopes.ProjectFormulaValue(m, scoped.Field) is { } pv) return pv;
            return Read(m, owner, owner.Class == IfcClass.Project ? null : m.RelatingOf(owner.Id, IfcClass.RelDefinesByType), null, scoped.Field);
        }
        var key = TagText.Squash(name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name);
        foreach (var x in new[] { e, type })
        {
            if (x?.Attrs["params"] is not JsonArray list) continue;
            var p = list.OfType<JsonObject>().FirstOrDefault(y => TagText.Squash((string?)y["name"] ?? "") == key);
            if (p == null) continue;
            if (IsFormula(p)) return EvaluateValue(m, e, type, p, more).Value;
            if (p["value"] is JsonValue v)
            {
                if (v.TryGetValue<bool>(out var b)) return XValue.Of(b);
                if (Json.Number(v) is { } n) return XValue.Of(n);
                if (v.TryGetValue<string>(out var s)) return XValue.Read(s);
            }
        }
        foreach (var x in new[] { e, type })
            if (x?.Attrs.FirstOrDefault(kv => TagText.Squash(kv.Key) == key).Value is JsonValue av && Json.Number(av) is { } an) return XValue.Of(an);
        if ((more?.Invoke(name) ?? more?.Invoke(key)) is { } q) return XValue.Of(q);
        foreach (var x in new[] { e, type })
            if (x?.Attrs.FirstOrDefault(kv => TagText.Squash(kv.Key) == key).Value is JsonValue sv && sv.TryGetValue<string>(out var text)) return XValue.Read(text);
        switch (key)
        {
            case "name": return XValue.Of(e.Name);
            case "mark": return XValue.Read(TagText.Mark(m, e));
            case "type": return XValue.Of(type?.Name ?? e.Name);
            case "class": return XValue.Of(e.Class.StartsWith("Ifc", StringComparison.Ordinal) ? e.Class[3..] : e.Class);
        }
        // What its module works out (forum topic 84: a room's finish.floor), never stored.
        if (DerivedFields.Value(m, e, name) is { } derived) return XValue.Read(derived);
        // The project's parameters are global: read by their key from any element.
        return ParamScopes.Globals(m).Any(c => TagText.Squash(c.Key) == key) ? ParamScopes.ProjectFormulaValue(m, name) : null;
    }

    /// <summary>A formula parameter worked out: its value as a number, or null with why (a text that is not a number is not one).</summary>
    public static (double? Value, string? Error) Evaluate(IModelView m, Entity e, Entity? type, JsonObject p, Func<string, double?>? more = null, int depth = 0)
    {
        var (v, error) = EvaluateValue(m, e, type, p, more);
        if (v is not { } value) return (null, error);
        return value.Number is { } n ? (n, null) : (null, $"„{value.Text}” is a text, not a number.");
    }

    /// <summary>A formula parameter worked out: its value, a number or a text, or null with why.</summary>
    public static (XValue? Value, string? Error) EvaluateValue(IModelView m, Entity e, Entity? type, JsonObject p, Func<string, double?>? more = null)
    {
        var formula = (string?)p[Key] ?? "";
        var id = $"{e.Id}\u001f{Params.SetOf(p)}\u001f{TagText.Squash((string?)p["name"] ?? "")}";
        _busy ??= [];
        if (_busy.Count >= MaxDepth || !_busy.Add(id)) return (null, $"„{(string?)p["name"]}” reads itself, through other formulas.");
        try
        {
            if (IsTemplate(formula)) return (XValue.Of(TagText.Fill(formula, m, e, null, DraftingResources.Book(m).Units, more)), null);
            return Expr.TryEvalValue(formula, n => Read(m, e, type, more, n));
        }
        finally { _busy.Remove(id); }
    }

    /// <summary>The quantities a query can give an element, by name (case and spaces aside): the first of that name among its sets.</summary>
    public static Func<string, double?> Quantities(IQueryContext q, string id)
    {
        Dictionary<string, double>? map = null;
        return name =>
        {
            map ??= q.Quantities(id).SelectMany(s => s.Quantities).GroupBy(x => TagText.Squash(x.Name)).ToDictionary(g => g.Key, g => g.First().Value);
            return map.TryGetValue(TagText.Squash(name), out var v) ? v : null;
        };
    }

    /// <summary>A formula parameter with its value worked out (in a copy; the model keeps its formula alone): a text for a text, yes or no for a yes/no.</summary>
    public static JsonObject ResolvedOne(IModelView m, Entity e, Entity? type, JsonObject p, Func<string, double?>? more = null)
    {
        var copy = (JsonObject)p.DeepClone();
        var (value, _) = EvaluateValue(m, e, type, p, more);
        copy["value"] = value is not { } v ? null
            : (string?)p["kind"] is "text" or "choice" ? JsonValue.Create(v.AsText)
            // A yes/no formula is yes when it is not zero.
            : (string?)p["kind"] == "bool" ? JsonValue.Create(v.Truth)
            : v.Number is { } n ? JsonValue.Create(Math.Round(n, 9)) : null;
        return copy;
    }

    /// <summary>An element's parameters with each formula's value worked out (in a copy; the model keeps its formula alone).</summary>
    public static List<JsonObject> Resolved(IModelView m, Entity? e, Entity? type, Func<string, double?>? more = null)
    {
        if (e?.Attrs["params"] is not JsonArray list) return [];
        return [.. list.OfType<JsonObject>().Select(p => IsFormula(p) ? ResolvedOne(m, e, type, p, more) : p)];
    }

    /// <summary>The kind a new parameter given only a formula takes: a text when it gives one (a template, a text), else a number.</summary>
    public static string KindOf(IModelView m, Entity e, string formula)
    {
        if (IsTemplate(formula)) return "text";
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        var (v, _) = Expr.TryEvalValue(formula, n => Read(m, e, type, null, n));
        return v is { IsText: true, IsNumericText: false } ? "text" : "number";
    }

    static JsonObject Result(XValue? value, string? error, string? kind = null) => new()
    {
        ["value"] = value is { } v ? (kind is "text" or "choice" || v.Number == null ? JsonValue.Create(v.AsText) : JsonValue.Create(Math.Round(v.Number.Value, 9))) : null,
        ["error"] = error,
        ["text"] = value is { } t ? (t.Number is { } n && !t.IsText ? n.ToString("0.###", CultureInfo.InvariantCulture) : t.AsText) : null,
    };

    public static void Register(IModuleRegistry r)
    {
        // An element's parameters worked out: each formula's value (a number, or a text) or why it has none, its quantities read too.
        r.Query("params.evaluate", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            var type = q.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var quantities = Quantities(q, e.Id);
            var o = new JsonArray();
            foreach (var p in (e.Attrs["params"] as JsonArray ?? []).OfType<JsonObject>().Where(IsFormula))
            {
                var (value, error) = EvaluateValue(q.Model, e, type, p, quantities);
                var kind = (string?)p["kind"];
                // A number parameter whose formula gives a text has no value: why is said.
                if (value is { Number: null } tv && kind is not ("text" or "choice")) (value, error) = (null, $"„{tv.Text}” is a text, not a number.");
                var row = Result(value, error, kind);
                row["name"] = (string?)p["name"];
                row["pset"] = (string?)p["pset"];
                row["formula"] = (string?)p[Key];
                o.Add((JsonNode)row);
            }
            return new JsonObject { ["params"] = o };
        });

        // A formula tried on an element before it is set: its value (a number, or a text), or why it does not read.
        r.Query("params.tryFormula", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            var type = q.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var formula = (string?)a["formula"] ?? "";
            try { Check(formula); }
            catch (CommandException x) { return Result(null, x.Message); }
            var p = new JsonObject { ["name"] = "?", [Key] = formula };
            var (value, error) = EvaluateValue(q.Model, e, type, p, Quantities(q, e.Id));
            return Result(value, error, (string?)a["kind"]);
        });

        // What a tag or a stamp of an element can show (TagText.Fields): its own fields and parameters, then its scopes' (decision 551).
        r.Query("params.fields", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            return TagText.Fields(q.Model, e, DraftingResources.Book(q.Model).Units);
        });

        // The scopes a formula or a field may read on an element (decision 551): each with the entity it names there, if any.
        r.Query("params.scopes", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "");
            return new JsonObject
            {
                ["scopes"] = new JsonArray(ParamScopes.Names.Select(s => (JsonNode)new JsonObject
                {
                    ["scope"] = s,
                    ["id"] = e != null ? ParamScopes.Of(q.Model, e, s)?.Id : null,
                    ["name"] = e != null ? ParamScopes.Of(q.Model, e, s)?.Name : null,
                }).ToArray()),
                ["functions"] = new JsonArray(Expr.FunctionNames.Select(f => (JsonNode)f).ToArray()),
            };
        });
    }
}
