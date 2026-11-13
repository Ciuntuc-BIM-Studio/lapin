using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A part's parameter table (docs/plan-studio-parametric.md §3.2, stage P1, decision 520).
///
/// <para><b>A parameter</b> is <c>{key, label, kind: length | number | angle | bool | text | integer, unit?, default?, formula?,
/// instance?, published?, group?, description?, min?, max?}</c>. A parameter with a <c>formula</c> is <b>derived</b>: its value is
/// that formula of the others (<see cref="Expr"/>), worked out on every ask, never stored, and no placed part has a value of its own
/// for it. <c>instance: false</c> makes a <b>type parameter</b>: one value, its default, for every placed part. <c>min</c> and
/// <c>max</c> bound defaults, placed parts' values and derived values. A parameter without the newer fields reads as before: an
/// instance's own, shown (<c>instance</c> and <c>published</c> true).</para>
///
/// <para><b>Order.</b> Derived parameters are worked out in the order their formulas need (a topological order); a cycle is refused
/// when the definition is checked, its path named (<c>W → D → W</c>).</para>
///
/// <para><b>Commands</b> (each one undo step; <c>partId</c> the part or a placed part of it; the whole definition checked as every
/// part edit is, <c>PartSketches.Commit</c>: formulas read, names known, no cycle, the defaults build, and every placed part still
/// builds or the edit is refused naming it): <c>studio.addPartParam</c>, <c>updatePartParam</c> (a new key renamed in every formula
/// of the part and in every placed part's values), <c>removePartParam</c> (refused while a formula names it), <c>movePartParam</c>;
/// the query <c>studio.partValues</c>.</para>
/// </summary>
public static class PartParams
{
    /// <summary>Words a formula reads as itself, so never a parameter's key.</summary>
    static readonly string[] Reserved = ["pi", "true", "false", "and", "or", "not"];

    static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? (b ? 1 : 0) : Json.Number(n);
    static bool? Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    internal static string? FormulaOf(JsonObject p) => ((string?)p["formula"])?.Trim() is { Length: > 0 } f ? f : null;
    internal static bool IsDerived(JsonObject p) => FormulaOf(p) != null;
    internal static bool IsInstance(JsonObject p) => Flag(p["instance"]) ?? true;
    internal static bool IsText(JsonObject p) => (string?)p["kind"] == "text";

    static List<JsonObject> ParamsOf(JsonObject def) => [.. (def["params"] as JsonArray ?? []).OfType<JsonObject>()];

    /// <summary>A key a formula can read: a letter, then letters, digits or _, at most 24, not a word formulas keep.</summary>
    internal static string ValidKey(string? given)
    {
        if (given?.Trim() is not { Length: > 0 and <= 24 } key || !char.IsLetter(key[0]) || !key.All(c => char.IsLetterOrDigit(c) || c == '_'))
            throw new CommandException("Each parameter has a key of letters and digits (W, Height, HasShelf).");
        if (Reserved.Contains(key, StringComparer.OrdinalIgnoreCase)) throw new CommandException($"„{key}” is a word formulas keep; give the parameter another key.");
        return key;
    }

    // ------------------------------------------------------------ names in formulas

    /// <summary>
    /// Walks a formula's words: <paramref name="word"/> gets each name it reads (a whole word of letters, digits, _ and dots, not a
    /// function called, not a number's exponent) and returns what to write instead. The same scan as the interface's
    /// <c>renameInFormula</c> (familyModel.ts).
    /// </summary>
    static string Scan(string text, Func<string, string> word)
    {
        var o = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                // A number, with its exponent: 2e3, 1.5E-2.
                var j = i;
                while (j < text.Length && (char.IsDigit(text[j]) || text[j] == '.')) j++;
                if (j < text.Length && text[j] is 'e' or 'E' && j + 1 < text.Length && (char.IsDigit(text[j + 1]) || text[j + 1] is '-' or '+'))
                {
                    j += 2;
                    while (j < text.Length && char.IsDigit(text[j])) j++;
                }
                o.Append(text, i, j - i);
                i = j;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var j = i;
                while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] is '_' or '.')) j++;
                var w = text[i..j];
                var k = j;
                while (k < text.Length && char.IsWhiteSpace(text[k])) k++;
                var call = k < text.Length && text[k] == '(';
                o.Append(call || Reserved.Contains(w, StringComparer.OrdinalIgnoreCase) ? w : word(w));
                i = j;
            }
            else { o.Append(c); i++; }
        }
        return o.ToString();
    }

    /// <summary>The names a formula reads (whether or not it evaluates), each once.</summary>
    public static List<string> NamesIn(string formula)
    {
        var o = new List<string>();
        Scan(formula, w => { if (!o.Contains(w, StringComparer.OrdinalIgnoreCase)) o.Add(w); return w; });
        return o;
    }

    /// <summary>A formula with the name <paramref name="oldKey"/> (any case, whole words only) written <paramref name="newKey"/>.</summary>
    public static string Rename(string formula, string oldKey, string newKey) =>
        Scan(formula, w => w.Equals(oldKey, StringComparison.OrdinalIgnoreCase) ? newKey : w);

    // ------------------------------------------------------------ where formulas are

    static void Cell(List<Site> o, string where, JsonArray? a)
    {
        if (a == null) return;
        for (var i = 0; i < a.Count; i++)
        {
            var k = i;
            if (a[k] is JsonValue v && v.TryGetValue<string>(out var s)) o.Add(new(where, s, t => a[k] = t));
        }
    }

    static void Cell(List<Site> o, string where, JsonObject obj, string key)
    {
        if (obj[key] is JsonValue v && v.TryGetValue<string>(out var s)) o.Add(new(where, s, t => obj[key] = t));
    }

    /// <summary>Every formula of a definition: the derived parameters', the bodies' corners, holes, depths, conditions and offsets, the sketches' offsets and constraint formulas.</summary>
    internal static List<Site> Sites(JsonObject def)
    {
        var o = new List<Site>();
        foreach (var p in ParamsOf(def)) Cell(o, $"the formula of {(string?)p["key"]}", p, "formula");
        var i = 0;
        foreach (var b in (def["bodies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = Parts.BodyName(b, ++i);
            foreach (var corner in (b["points"] as JsonArray ?? []).OfType<JsonArray>()) Cell(o, $"the corners of {name}", corner);
            foreach (var hole in (b["holes"] as JsonArray ?? []).OfType<JsonArray>())
                foreach (var corner in hole.OfType<JsonArray>()) Cell(o, $"the holes of {name}", corner);
            Cell(o, $"the depth of {name}", b, "depth");
            Cell(o, $"the condition of {name}", b, "when");
            Cell(o, $"the offset of {name}", b["offset"] as JsonArray);
        }
        foreach (var sk in PartSketches.SketchesOf(def).OfType<JsonObject>())
        {
            var name = (string?)sk["name"] ?? (string?)sk["id"];
            Cell(o, $"the offset of sketch {name}", sk["offset"] as JsonArray);
            foreach (var c in PartSketches.ConstraintsOf(sk)) Cell(o, $"the {(string?)c["kind"]} {(string?)c["id"]} of sketch {name}", c, "formula");
        }
        return o;
    }

    // ------------------------------------------------------------ order and cycles

    /// <summary>
    /// The derived parameters in the order their formulas need them. With <paramref name="strict"/>, a formula naming no parameter
    /// (or a text one) and a cycle are refused, the cycle's path named; otherwise what a cycle leaves is put last.
    /// </summary>
    internal static List<JsonObject> Order(IReadOnlyList<JsonObject> ps, bool strict)
    {
        var byKey = ps.ToDictionary(p => (string)p["key"]!, StringComparer.OrdinalIgnoreCase);
        var deps = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ps.Where(IsDerived))
        {
            var key = (string)p["key"]!;
            var l = new List<string>();
            foreach (var n in NamesIn(FormulaOf(p)!))
            {
                if (byKey.TryGetValue(n, out var q) && !IsText(q)) { l.Add((string)q["key"]!); continue; }
                if (strict)
                    throw new CommandException(byKey.TryGetValue(n, out var tq)
                        ? $"The formula of {key} names „{tq["key"]}”, a text: formulas read numbers."
                        : $"The formula of {key} names „{n}”, which is not a parameter of the part (it has {string.Join(", ", byKey.Keys)}).");
            }
            deps[key] = l;
        }
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        var order = new List<JsonObject>();
        void Visit(string k)
        {
            if (state.TryGetValue(k, out var s))
            {
                if (s == 2) return;
                if (strict)
                {
                    var from = stack.FindIndex(x => x.Equals(k, StringComparison.OrdinalIgnoreCase));
                    throw new CommandException($"The formulas go round: {string.Join(" → ", stack.Skip(from).Append(k))}.");
                }
                return;
            }
            state[k] = 1;
            stack.Add(k);
            foreach (var d in deps.TryGetValue(k, out var l) ? l : []) if (deps.ContainsKey(d)) Visit(d);
            stack.RemoveAt(stack.Count - 1);
            state[k] = 2;
            order.Add(byKey[k]);
        }
        foreach (var p in ps.Where(IsDerived)) Visit((string)p["key"]!);
        return order;
    }

    // ------------------------------------------------------------ values

    static JsonNode? Lookup(JsonObject? o, string key) => o?.FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    static string Text(JsonNode? n) => n is JsonValue v
        ? v.TryGetValue<string>(out var s) ? s : Json.Number(v) is { } x ? x.ToString("0.#########", CultureInfo.InvariantCulture) : v.ToJsonString()
        : "";

    static string Show(double v) => Math.Round(v, 9).ToString("0.#########", CultureInfo.InvariantCulture);

    /// <summary>What a value breaks of its parameter's bounds and kind, or null.</summary>
    internal static string? OutOfBounds(JsonObject p, double v)
    {
        var key = (string)p["key"]!;
        var label = (string?)p["label"] is { Length: > 0 } l && !l.Equals(key, StringComparison.Ordinal) ? $"{l} ({key})" : key;
        if (!double.IsFinite(v)) return $"{label} is not a number.";
        if ((string?)p["kind"] == "integer" && Math.Abs(v - Math.Round(v)) > 1e-9) return $"{label} is a whole number, not {Show(v)}.";
        if (Num(p["min"]) is { } min && v < min - 1e-9) return $"{label} is at least {Show(min)}, not {Show(v)}.";
        if (Num(p["max"]) is { } max && v > max + 1e-9) return $"{label} is at most {Show(max)}, not {Show(v)}.";
        return null;
    }

    /// <summary>
    /// A part's values: each parameter's default, an instance's own over it (<paramref name="own"/>, for its instance parameters),
    /// <paramref name="over"/> over both for any parameter not derived (a flex in the edit mode), then the derived ones worked out in
    /// their order. Never throws: what does not hold is in <see cref="Resolved.Problems"/>.
    /// </summary>
    public static Resolved Resolve(JsonObject def, JsonObject? own, JsonObject? over = null)
    {
        var r = new Resolved();
        var ps = ParamsOf(def);
        foreach (var p in ps)
        {
            var key = (string)p["key"]!;
            if (IsDerived(p)) continue;
            var inst = IsInstance(p);
            r.Sources[key] = inst ? "default" : "type";
            if (IsText(p))
            {
                var t = Text(p["default"]);
                if (inst && Lookup(own, key) is JsonValue ov) { t = Text(ov); r.Sources[key] = "instance"; }
                if (Lookup(over, key) is JsonValue fv) t = Text(fv);
                r.Texts[key] = t;
                continue;
            }
            var v = Num(p["default"]) ?? 0;
            if (inst && Num(Lookup(own, key)) is { } o) { v = o; r.Sources[key] = "instance"; }
            if (Num(Lookup(over, key)) is { } f) v = f;
            if (OutOfBounds(p, v) is { } why) r.Problems[key] = why;
            r.Numbers[key] = v;
        }
        foreach (var p in Order(ps, strict: false))
        {
            var key = (string)p["key"]!;
            r.Sources[key] = "formula";
            var (v, error) = Expr.TryEval(FormulaOf(p)!, k => r.Numbers.TryGetValue(k, out var x) ? x : null);
            if (v is not { } x) { r.Problems[key] = $"The formula of {key}: {error}"; continue; }
            x = (string?)p["kind"] switch { "bool" => x != 0 ? 1 : 0, "integer" => Math.Round(x), _ => x };
            if (OutOfBounds(p, x) is { } why) r.Problems[key] = why;
            r.Numbers[key] = x;
        }
        // A parameter a cycle left out (only in a definition never checked): told.
        foreach (var p in ps.Where(IsDerived))
            if (!r.Sources.ContainsKey((string)p["key"]!)) r.Problems[(string)p["key"]!] = $"The formula of {p["key"]} goes round.";
        return r;
    }

    // ------------------------------------------------------------ checking

    /// <summary>A parameter as a command or a file gives it, checked and written the one way (the key checked by the caller).</summary>
    internal static JsonObject CheckOne(JsonObject po, string key)
    {
        var kind = (string?)po["kind"] ?? "length";
        if (!Parts.ParamKinds.Contains(kind)) throw new CommandException($"A parameter is a {string.Join(", ", Parts.ParamKinds)}.");
        var pc = new JsonObject { ["key"] = key, ["label"] = ((string?)po["label"])?.Trim() is { Length: > 0 } l ? l : key, ["kind"] = kind };
        var formula = po["formula"] is JsonValue fv && fv.TryGetValue<string>(out var fs) && fs.Trim() is { Length: > 0 } ft ? ft : null;
        if (formula != null)
        {
            if (kind == "text") throw new CommandException($"{key} is a text: a text takes no formula.");
            pc["formula"] = ParamFormulas.Check(formula);
        }
        else if (kind == "text") pc["default"] = Text(po["default"]);
        else
        {
            var d = Num(po["default"]) ?? throw new CommandException($"{key} needs its default.");
            if (!double.IsFinite(d) || Math.Abs(d) > 1000) throw new CommandException($"{key}'s default is a number within ±1000.");
            pc["default"] = d;
        }
        // What the parameter table keeps: kept when given, never required.
        foreach (var text in new[] { "unit", "group", "description" })
            if (po[text] is JsonValue tv && tv.TryGetValue<string>(out var s) && s.Trim() is { Length: > 0 } st) pc[text] = st;
        foreach (var flag in new[] { "instance", "published" })
            if (Flag(po[flag]) is { } fb) pc[flag] = fb;
        if (kind is not ("text" or "bool"))
        {
            foreach (var bound in new[] { "min", "max" })
            {
                if (po[bound] == null) continue;
                pc[bound] = Num(po[bound]) is { } b && double.IsFinite(b) ? b : throw new CommandException($"{key}'s {bound} is a number.");
            }
            if (Num(pc["min"]) is { } min && Num(pc["max"]) is { } max && min > max) throw new CommandException($"{key}'s minimum is above its maximum.");
            if (formula == null && OutOfBounds(pc, Num(pc["default"]) ?? 0) is { } why) throw new CommandException($"Its default: {why}");
        }
        return pc;
    }

    // ------------------------------------------------------------ the commands

    static List<Entity> Instances(IModelView m, Entity type) => [.. m.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(Parts.IsInstance)];

    static int IndexOf(JsonArray list, string? key, Entity type)
    {
        var i = list.OfType<JsonObject>().ToList().FindIndex(p => ((string?)p["key"])?.Equals(key, StringComparison.OrdinalIgnoreCase) == true);
        return i >= 0 ? i : throw new CommandException($"„{type.Name}” has no parameter „{key}”.");
    }

    /// <summary>Every placed part's own values changed by <paramref name="change"/> (it returns whether it changed them); how many changed.</summary>
    static int EachOwn(ICommandContext ctx, Entity type, Func<Entity, JsonObject, bool> change)
    {
        var n = 0;
        foreach (var e in Instances(ctx.Model, type))
        {
            if (e.Attrs["values"] is not JsonObject { Count: > 0 } had) continue;
            var own = (JsonObject)had.DeepClone();
            if (!change(e, own)) continue;
            n++;
            var attrs = e.CloneAttrs();
            if (own.Count == 0) attrs.Remove("values"); else attrs["values"] = own;
            ctx.Replace(e.With(attrs: attrs));
        }
        return n;
    }

    static bool Drop(JsonObject own, string key)
    {
        var k = own.Select(kv => kv.Key).FirstOrDefault(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
        return k != null && own.Remove(k);
    }

    /// <summary>{partId, param} → {key}: a parameter added at the end of the table (a missing default: nothing, no, an empty text, or its minimum).</summary>
    public static JsonNode? Add(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartSketches.PartOf(ctx.Model, (string?)a["partId"]);
        var p = a["param"] as JsonObject ?? throw new CommandException("Which parameter: {param: {key, kind, default?, formula?, …}}?");
        var key = ValidKey((string?)p["key"]);
        var list = def["params"] as JsonArray ?? (JsonArray)(def["params"] = new JsonArray());
        if (list.OfType<JsonObject>().Any(x => ((string?)x["key"])?.Equals(key, StringComparison.OrdinalIgnoreCase) == true))
            throw new CommandException($"„{type.Name}” has a parameter „{key}” already.");
        var given = (JsonObject)p.DeepClone();
        given["key"] = key;
        if (given["default"] == null && given["formula"] == null)
            given["default"] = (string?)given["kind"] switch { "text" => "", "bool" => false, _ => Num(given["min"]) ?? 0 };
        list.Add((JsonNode)given);
        PartSketches.Commit(ctx, type, def);
        return new JsonObject { ["key"] = key };
    }

    /// <summary>A value of a placed part made the kind <paramref name="kind"/>, or null when it cannot be.</summary>
    static JsonNode? Convert(JsonNode? v, string kind)
    {
        if (kind == "text") return Text(v);
        var x = Num(v) ?? (v is JsonValue sv && sv.TryGetValue<string>(out var s) ? LiveText.Number(s) : null);
        if (x is not { } n) return null;
        return kind switch { "bool" => n != 0 ? 1 : 0, "integer" => Math.Round(n), _ => n };
    }

    /// <summary>
    /// {partId, key, changes: {newKey?, label?, kind?, unit?, default?, formula? (null clears), instance?, published?, group? (null
    /// clears), description?, min?, max? (null clears)}} → {key, droppedValues}. A new key is renamed in every formula of the part and
    /// in every placed part's values; a parameter made derived or a type parameter drops the placed parts' own values for it (counted).
    /// </summary>
    public static JsonNode? Update(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartSketches.PartOf(ctx.Model, (string?)a["partId"]);
        var list = def["params"] as JsonArray ?? [];
        var at = IndexOf(list, (string?)a["key"], type);
        var p = (JsonObject)list[at]!;
        var key = (string)p["key"]!;
        var ch = a["changes"] as JsonObject ?? throw new CommandException("What changes: {changes: {…}}?");
        var oldKind = (string?)p["kind"] ?? "length";
        if (ch["kind"] is JsonValue kv && kv.TryGetValue<string>(out var kind) && kind != oldKind)
        {
            if (!Parts.ParamKinds.Contains(kind)) throw new CommandException($"A parameter is a {string.Join(", ", Parts.ParamKinds)}.");
            p["kind"] = kind;
            if (!IsDerived(p)) p["default"] = Convert(p["default"], kind) ?? (kind == "text" ? "" : 0);
            if (kind is "text" or "bool") { p.Remove("min"); p.Remove("max"); }
        }
        if (ch.ContainsKey("default")) p["default"] = ch["default"]?.DeepClone();
        if (ch.ContainsKey("formula"))
        {
            if (ch["formula"] is JsonValue fv && fv.TryGetValue<string>(out var f) && f.Trim().Length > 0)
            {
                p["formula"] = f.Trim();
                if (!ch.ContainsKey("default")) p.Remove("default");
            }
            else if (IsDerived(p))
            {
                // Cleared: the parameter keeps the value its formula gave with the defaults, as its default.
                if (!ch.ContainsKey("default"))
                    p["default"] = Parts.Values(def, null).TryGetValue(key, out var was) ? Math.Round(was, 9) : 0;
                p.Remove("formula");
            }
        }
        foreach (var text in new[] { "label", "unit", "group", "description" })
        {
            if (!ch.ContainsKey(text)) continue;
            if (ch[text] is JsonValue tv && tv.TryGetValue<string>(out var s) && s.Trim().Length > 0) p[text] = s.Trim();
            else p.Remove(text);
        }
        foreach (var flag in new[] { "instance", "published" })
            if (ch.ContainsKey(flag)) p[flag] = Flag(ch[flag]) ?? throw new CommandException($"{flag} is true or false.");
        foreach (var bound in new[] { "min", "max" })
        {
            if (!ch.ContainsKey(bound)) continue;
            if (ch[bound] == null) p.Remove(bound); else p[bound] = ch[bound]!.DeepClone();
        }
        // The placed parts' own values: none for a derived or a type parameter (dropped, counted), else made its kind.
        var newKind = (string?)p["kind"] ?? "length";
        var noOwn = IsDerived(p) || !IsInstance(p);
        var dropped = 0;
        EachOwn(ctx, type, (e, own) =>
        {
            var k = own.Select(x => x.Key).FirstOrDefault(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (k == null) return false;
            if (noOwn) { dropped++; return own.Remove(k); }
            if (newKind == oldKind) return false;
            own[k] = Convert(own[k], newKind) ?? throw new CommandException($"„{e.Name}” has {key} = {own[k]?.ToJsonString()}, which is no {newKind}. Nothing was changed.");
            return true;
        });
        // Renamed everywhere: every formula of the part, the parameter, every placed part's values.
        var final = key;
        if (ch["newKey"] is JsonValue nv && nv.TryGetValue<string>(out var nk) && nk.Trim() != key)
        {
            final = ValidKey(nk);
            if (list.OfType<JsonObject>().Where(x => x != p).Any(x => ((string?)x["key"])?.Equals(final, StringComparison.OrdinalIgnoreCase) == true))
                throw new CommandException($"„{type.Name}” has a parameter „{final}” already.");
            foreach (var site in Sites(def))
                if (Rename(site.Text, key, final) is var renamed && renamed != site.Text) site.Set(renamed);
            p["key"] = final;
            EachOwn(ctx, type, (_, own) =>
            {
                var k = own.Select(x => x.Key).FirstOrDefault(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (k == null) return false;
                var v = own[k]?.DeepClone();
                own.Remove(k);
                own[final] = v;
                return true;
            });
        }
        PartSketches.Commit(ctx, type, def);
        return new JsonObject { ["key"] = final, ["droppedValues"] = dropped };
    }

    /// <summary>{partId, key} → {droppedValues}: refused while a formula of the part names it (the message lists where); the placed parts' own values for it dropped.</summary>
    public static JsonNode? Remove(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartSketches.PartOf(ctx.Model, (string?)a["partId"]);
        var list = def["params"] as JsonArray ?? [];
        var at = IndexOf(list, (string?)a["key"], type);
        var key = (string)list[at]!["key"]!;
        var own = $"the formula of {key}";
        var users = Sites(def).Where(s => s.Where != own && NamesIn(s.Text).Contains(key, StringComparer.OrdinalIgnoreCase)).Select(s => s.Where).Distinct().ToList();
        if (users.Count > 0) throw new CommandException($"„{key}” is used by {string.Join(", ", users)}: change those first.");
        list.RemoveAt(at);
        var dropped = EachOwn(ctx, type, (_, o) => Drop(o, key));
        PartSketches.Commit(ctx, type, def);
        return new JsonObject { ["droppedValues"] = dropped };
    }

    /// <summary>{partId, key, to}: the parameter moved to the place <c>to</c> in the table (0 the first).</summary>
    public static JsonNode? Move(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartSketches.PartOf(ctx.Model, (string?)a["partId"]);
        var list = def["params"] as JsonArray ?? [];
        var at = IndexOf(list, (string?)a["key"], type);
        var to = Json.Number(a["to"]) is { } t && double.IsFinite(t) ? (int)Math.Clamp(Math.Round(t), 0, list.Count - 1) : throw new CommandException("Where to: {to: index}.");
        if (to == at) return null;
        var p = list[at]!;
        list.RemoveAt(at);
        list.Insert(to, p);
        PartSketches.Commit(ctx, type, def);
        return null;
    }

    /// <summary>{id} (a placed part) → {values: [{key, value, source: default | instance | formula | type, problem?}]}, in the table's order.</summary>
    public static JsonNode Values(IQueryContext q, JsonObject a)
    {
        var e = q.Model.Get((string?)a["id"] ?? "") is { } x && Parts.IsInstance(x) ? x : throw new CommandException("Not a placed part.");
        var def = Parts.DefOf(Parts.TypeOf(q.Model, e)) ?? throw new CommandException("Its part is gone, or no longer has a definition.");
        var r = Resolve(def, e.Attrs["values"] as JsonObject);
        return new JsonObject { ["values"] = new JsonArray([.. ParamsOf(def).Select(p => (JsonNode)ValueJson(p, r))]) };
    }

    /// <summary>A parameter's resolved value, its source and its problem, as <c>studio.partValues</c> and the part's tree show them.</summary>
    internal static JsonObject ValueJson(JsonObject p, Resolved r)
    {
        var key = (string)p["key"]!;
        var o = new JsonObject { ["key"] = key };
        o["value"] = IsText(p) ? r.Texts.TryGetValue(key, out var t) ? t : "" : r.Numbers.TryGetValue(key, out var v) ? Math.Round(v, 9) : null;
        o["source"] = r.Sources.TryGetValue(key, out var s) ? s : "default";
        if (r.Problems.TryGetValue(key, out var problem)) o["problem"] = problem;
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        // The parameter table (P1): one undo step each, the part and every placed part checked and rebuilt.
        r.Command("addPartParam", "Add parameter", Add);
        r.Command("updatePartParam", "Change parameter", Update);
        r.Command("removePartParam", "Remove parameter", Remove);
        r.Command("movePartParam", "Move parameter", Move);
        r.Query("partValues", Values);
    }

    // Declared last, so the catalogue's reading of this file finds the commands' class.

    /// <summary>A formula of a part's definition: where it is (for messages), its text, and how to write it back.</summary>
    internal sealed record Site(string Where, string Text, Action<string> Set);

    /// <summary>The values a placed part is built with: numbers by key, texts by key, where each comes from, and what does not hold.</summary>
    public sealed class Resolved
    {
        public Dictionary<string, double> Numbers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Texts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Problems { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
