using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Material keys (forum topic 255, item 5): each material's short key, the number or code a material key tag writes on a facade
/// and a keyed legend lists, "1", "2", "3" or "F1", "F2", "F3".
/// <list type="bullet">
/// <item>Its key is its own code when the user gave one (<c>key</c> on the material: "F1", "RF-02"), else the project's format
/// (<see cref="StyleBook.MaterialKeyFormat"/>, <c>{n}</c> by default: "F{n}", "M-{n:00}") with its number.</item>
/// <item>Its number is stored once (<c>keyNo</c> on the material): given the first time the material is keyed on a drawing (a
/// material key tag placed on it, a keyed legend placed or changed, or <c>material.numberKeys</c>), the next after the highest
/// number the project has, so the materials keyed first are 1, 2, 3 in the order they were first keyed. A number is never
/// given again nor taken back by itself: a material deleted leaves its gap, a material added takes the next one, so a key on
/// a sheet already issued never changes. <c>material.renumberKeys</c> closes the gaps, by hand, in one step.</item>
/// <item>A material not keyed yet that shows up in a keyed legend is numbered there for the time being after the highest
/// number, in the order of the names (<see cref="Numbers"/>), until it is keyed.</item>
/// </list>
/// The key text is worked out on every ask (its format and own code), only the number is stored. None of it goes into a
/// property set: a key is Lapin's own, IFC carries the material by its name.
/// </summary>
public static partial class MaterialKeys
{
    /// <summary>The material's stored number and its own code (attributes of the material).</summary>
    public const string NumberKey = "keyNo", OwnKey = "key";
    public const string DefaultFormat = "{n}";
    public const int MaxOwn = 16, MaxFormat = 24;

    [GeneratedRegex(@"\{n(?::(0{1,4}))?\}")]
    private static partial Regex Slot();

    /// <summary>A format as the standards keep it: trimmed, with one {n} or {n:00}, short; null when it is not one.</summary>
    public static string? FormatOf(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim() is { Length: > 0 and <= MaxFormat } t && Slot().Matches(t).Count == 1 && !t.Contains('|') ? t : null;

    /// <summary>The project's format of a key (<c>{n}</c> when it sets none).</summary>
    public static string FormatIn(IModelView m) => DraftingResources.Book(m).MaterialKeyFormat ?? DefaultFormat;

    /// <summary>A number written by a format: "F{n}" with 3 is "F3", "M-{n:00}" with 3 is "M-03".</summary>
    public static string Write(string format, int n) =>
        Slot().Replace(format, x => x.Groups[1].Success ? n.ToString(x.Groups[1].Value, CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture));

    /// <summary>A material's stored number, or null when it was never keyed.</summary>
    public static int? Stored(Entity mat) => mat.NumOrNull(NumberKey) is double d && d >= 1 && d < 1e6 ? (int)Math.Round(d) : null;

    /// <summary>A material's own code ("F1"), or null when it takes the format's.</summary>
    public static string? Own(Entity mat) => mat.Str(OwnKey) is { } k && k.Trim().Length > 0 ? k.Trim() : null;

    /// <summary>
    /// Every material's number in effect: its stored one; the others after the highest stored number, in the order of their
    /// names (then ids), for the time being.
    /// </summary>
    public static Dictionary<string, int> Numbers(IModelView m)
    {
        var all = m.OfClass(IfcClass.Material).ToList();
        var o = new Dictionary<string, int>();
        foreach (var mat in all) if (Stored(mat) is { } n) o[mat.Id] = n;
        var next = o.Count == 0 ? 1 : o.Values.Max() + 1;
        foreach (var mat in all.Where(x => !o.ContainsKey(x.Id)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id, StringComparer.Ordinal))
            o[mat.Id] = next++;
        return o;
    }

    /// <summary>One material's key as drawings write it, or null when it is no material.</summary>
    public static string? KeyOf(IModelView m, string? materialId, IReadOnlyDictionary<string, int>? numbers = null)
    {
        if (materialId == null || m.Get(materialId) is not { Class: IfcClass.Material } mat) return null;
        if (Own(mat) is { } own) return own;
        numbers ??= Numbers(m);
        return numbers.TryGetValue(mat.Id, out var n) ? Write(FormatIn(m), n) : null;
    }

    /// <summary>Every material's key, by its id.</summary>
    public static Dictionary<string, string> Keys(IModelView m)
    {
        var numbers = Numbers(m);
        var format = FormatIn(m);
        return m.OfClass(IfcClass.Material).ToDictionary(x => x.Id, x => Own(x) ?? Write(format, numbers[x.Id]));
    }

    /// <summary>
    /// The materials named given their number for good when they have none (keyed on a drawing): in the order given, the next
    /// after the highest stored number each. Returns how many were numbered.
    /// </summary>
    public static int Freeze(ICommandContext ctx, IEnumerable<string?> ids)
    {
        var m = ctx.Model;
        var next = m.OfClass(IfcClass.Material).Select(Stored).Where(n => n != null).Select(n => n!.Value).DefaultIfEmpty(0).Max() + 1;
        var done = 0;
        foreach (var id in ids.OfType<string>().Distinct())
        {
            if (m.Get(id) is not { Class: IfcClass.Material } mat || Stored(mat) != null) continue;
            var attrs = mat.CloneAttrs();
            attrs[NumberKey] = next++;
            ctx.Replace(mat.With(attrs: attrs));
            done++;
        }
        return done;
    }

    /// <summary>Keys compared as people read them: "2" before "10", "F2" before "F10".</summary>
    public static int Compare(string? a, string? b)
    {
        if (a == null || b == null) return a == null ? (b == null ? 0 : 1) : -1;
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var (x, y) = (a[si..i].TrimStart('0'), b[sj..j].TrimStart('0'));
                if (x.Length != y.Length) return x.Length.CompareTo(y.Length);
                var c = string.CompareOrdinal(x, y);
                if (c != 0) return c;
                continue;
            }
            var d = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (d != 0) return d;
            i++; j++;
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    /// <summary>The first line of a material's description, as a legend's row writes it; null when it has none.</summary>
    public static string? DescriptionLine(Entity mat) =>
        mat.Str("description") is { } d && d.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) is { } first ? first : null;

    /// <summary>What a tag or a legend's row reads of a material: {materialKey}, {materialName}, {materialDescription} (and {key}, {name}, {description} in a row).</summary>
    public static Dictionary<string, string> Fields(IModelView m, Entity mat, string key) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["materialKey"] = key, ["materialName"] = mat.Name, ["materialDescription"] = DescriptionLine(mat) ?? "",
        ["key"] = key, ["name"] = mat.Name, ["description"] = DescriptionLine(mat) ?? "", ["category"] = mat.Str("category") ?? "",
    };

    /// <summary>The fields a material key tag reads (its field, or any of them in its text).</summary>
    public static readonly string[] TagFields = ["materialKey", "materialName", "materialDescription"];

    /// <summary>Whether a tag's field or text reads the material it points at.</summary>
    public static bool Reads(string? field, string? template) =>
        field != null && TagFields.Any(f => string.Equals(f, field, StringComparison.OrdinalIgnoreCase))
        || template != null && TagFields.Any(f => template.Contains("{" + f, StringComparison.OrdinalIgnoreCase));

    static void Unique(IModelView m, Entity mat, string key)
    {
        foreach (var (id, other) in Keys(m))
            if (id != mat.Id && string.Equals(other, key, StringComparison.OrdinalIgnoreCase))
                throw new CommandException($"„{key}” is the key of {m.Get(id)!.Name} already.");
    }

    public static void Register(IModuleRegistry r)
    {
        // Every material's key: {format, rows: [{id, name, key, number, stored, own}]} in the keys' order.
        r.Query("material.keys", (q, _) =>
        {
            var m = q.Model;
            var numbers = Numbers(m);
            var format = FormatIn(m);
            var rows = m.OfClass(IfcClass.Material).Select(x => (Mat: x, Key: Own(x) ?? Write(format, numbers[x.Id]))).OrderBy(x => x.Key, Comparer<string>.Create(Compare)).ToList();
            return new JsonObject
            {
                ["format"] = format,
                ["rows"] = new JsonArray([.. rows.Select(x => (JsonNode)new JsonObject
                {
                    ["id"] = x.Mat.Id, ["name"] = x.Mat.Name, ["key"] = x.Key, ["number"] = numbers[x.Mat.Id], ["stored"] = Stored(x.Mat) != null, ["own"] = Own(x.Mat),
                })]),
            };
        });

        // A material's key: {id, key?: its own code ("F1"; null or "": the format's), number?: its number (null: none stored)}.
        // A key another material has already is refused, as is a number another has.
        r.Command("material.setKey", "Change material key", (ctx, a) =>
        {
            var mat = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Material);
            if (!a.ContainsKey("key") && !a.ContainsKey("number")) throw new CommandException("Nothing to change: a key or a number.");
            var attrs = mat.CloneAttrs();
            if (a.ContainsKey("number"))
            {
                if (a["number"] is null) attrs.Remove(NumberKey);
                else if (Json.Number(a["number"]) is { } d && d >= 1 && d < 1e6 && Math.Abs(d - Math.Round(d)) < 1e-9)
                {
                    var n = (int)Math.Round(d);
                    if (ctx.Model.OfClass(IfcClass.Material).FirstOrDefault(x => x.Id != mat.Id && Stored(x) == n) is { } taken)
                        throw new CommandException($"Number {n} is {taken.Name}'s already.");
                    attrs[NumberKey] = n;
                }
                else throw new CommandException("A material's number is a whole number from 1.");
            }
            if (a.ContainsKey("key"))
            {
                if (((string?)a["key"])?.Trim() is { Length: > 0 } k)
                {
                    if (k.Length > MaxOwn || k.Contains('|') || k.Contains('{')) throw new CommandException($"A material's key is a short code of up to {MaxOwn} characters (F1, RF-02).");
                    attrs[OwnKey] = k;
                }
                else attrs.Remove(OwnKey);
            }
            var updated = mat.With(attrs: attrs);
            // Its key now, against the others' (theirs as they are).
            var own = Own(updated);
            if (own != null) Unique(ctx.Model, mat, own);
            ctx.Replace(updated);
            return null;
        });

        // The materials given their number for good: {ids?} (none: every material without one), in the keys' order now.
        r.Command("material.numberKeys", "Number material keys", (ctx, a) =>
        {
            var numbers = Numbers(ctx.Model);
            var ids = a["ids"] is JsonArray many ? many.Select(x => (string?)x).OfType<string>().ToList() : ctx.Model.OfClass(IfcClass.Material).Select(x => x.Id).ToList();
            var n = Freeze(ctx, ids.Where(numbers.ContainsKey).OrderBy(id => numbers[id]));
            return new JsonObject { ["numbered"] = n };
        });

        // The gaps closed: every numbered material 1, 2, 3… in its order now; the others keep none. It changes keys on sheets
        // already drawn, so it is asked for by hand only (and a guest of a shared session never runs it, as marks.renumber).
        r.Command("material.renumberKeys", "Renumber material keys", (ctx, _) =>
        {
            var k = 0;
            foreach (var mat in ctx.Model.OfClass(IfcClass.Material).Where(x => Stored(x) != null).OrderBy(x => Stored(x)).ToList())
            {
                k++;
                if (Stored(mat) == k) continue;
                var attrs = mat.CloneAttrs();
                attrs[NumberKey] = k;
                ctx.Replace(mat.With(attrs: attrs));
            }
            return new JsonObject { ["numbered"] = k };
        });
    }
}
