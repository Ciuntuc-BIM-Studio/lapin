using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Package.Remote;

/// <summary>
/// The JSON Canonicalization Scheme, RFC 8785 (docs/spec-lapin-remote.md §3): members sorted by their names as UTF-16 code
/// units, no whitespace, strings escaped only where they must be (<c>"</c>, <c>\</c>, the controls), numbers in ECMAScript's
/// <c>Number.prototype.toString</c> form. Written by hand: System.Text.Json escapes non-ASCII and writes numbers in .NET's
/// own form, and the browser's trimmed engine takes no reflection. A duplicate member name, a lone surrogate, NaN or an
/// infinity make the JSON invalid (<see cref="InvalidDataException"/>).
/// </summary>
public static class Jcs
{
    /// <summary>The canonical bytes of the JSON in <paramref name="json"/> (UTF-8); refused when it is not valid JSON or has a member twice.</summary>
    public static byte[] Canonicalize(ReadOnlySpan<byte> json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 256 }); }
        catch (JsonException ex) { throw new InvalidDataException($"Not valid JSON: {ex.Message}"); }
        using (doc)
        {
            var sb = new StringBuilder();
            Write(sb, doc.RootElement);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }

    /// <summary>A node's canonical bytes (what Lapin writes for its own objects).</summary>
    public static byte[] Canonicalize(JsonNode? node)
    {
        var sb = new StringBuilder();
        Write(sb, node);
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Whether <paramref name="bytes"/> are canonical JSON: their own canonical form, byte for byte (§3, "the stored bytes are the object").</summary>
    public static bool IsCanonical(ReadOnlySpan<byte> bytes)
    {
        // No byte-order mark, and the UTF-8 must be valid (the parser accepts some invalid sequences inside strings).
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return false;
        try { return Canonicalize(bytes).AsSpan().SequenceEqual(bytes); }
        catch (InvalidDataException) { return false; }
    }

    static void Write(StringBuilder sb, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var members = new List<(string Name, JsonElement Value)>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var m in e.EnumerateObject())
                {
                    if (!seen.Add(m.Name)) throw new InvalidDataException($"The member „{m.Name}” is there twice.");
                    members.Add((m.Name, m.Value));
                }
                // Ordinal comparison of .NET strings is by UTF-16 code units, unsigned: exactly RFC 8785's order.
                members.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                sb.Append('{');
                for (var i = 0; i < members.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    String(sb, members[i].Name);
                    sb.Append(':');
                    Write(sb, members[i].Value);
                }
                sb.Append('}');
                break;
            }
            case JsonValueKind.Array:
            {
                sb.Append('[');
                var first = true;
                foreach (var x in e.EnumerateArray())
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, x);
                }
                sb.Append(']');
                break;
            }
            case JsonValueKind.String:
            {
                string s;
                try { s = e.GetString()!; }
                catch (InvalidOperationException) { throw new InvalidDataException("A string is not valid UTF-16 (a lone surrogate)."); }
                String(sb, s);
                break;
            }
            case JsonValueKind.Number: sb.Append(Number(e.GetRawText())); break;
            case JsonValueKind.True: sb.Append("true"); break;
            case JsonValueKind.False: sb.Append("false"); break;
            case JsonValueKind.Null: sb.Append("null"); break;
            default: throw new InvalidDataException("Not valid JSON.");
        }
    }

    static void Write(StringBuilder sb, JsonNode? node)
    {
        switch (node)
        {
            case null: sb.Append("null"); break;
            case JsonObject o:
            {
                var members = o.ToList();
                members.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                sb.Append('{');
                for (var i = 0; i < members.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    String(sb, members[i].Key);
                    sb.Append(':');
                    Write(sb, members[i].Value);
                }
                sb.Append('}');
                break;
            }
            case JsonArray a:
                sb.Append('[');
                for (var i = 0; i < a.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Write(sb, a[i]);
                }
                sb.Append(']');
                break;
            case JsonValue v:
            {
                // Through its own JSON text: whatever the value holds (a string, a long, a double, a JsonElement), one way.
                var raw = v.ToJsonString();
                using var doc = JsonDocument.Parse(raw);
                Write(sb, doc.RootElement);
                break;
            }
        }
    }

    /// <summary>A string as RFC 8785 writes it: only <c>"</c>, <c>\</c> and U+0000–U+001F escaped, everything else raw.</summary>
    static void String(StringBuilder sb, string s)
    {
        sb.Append('"');
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1])) throw new InvalidDataException("A string holds a lone surrogate.");
                sb.Append(c).Append(s[++i]);
                continue;
            }
            if (char.IsLowSurrogate(c)) throw new InvalidDataException("A string holds a lone surrogate.");
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\t': sb.Append("\\t"); break;
                case '\n': sb.Append("\\n"); break;
                case '\f': sb.Append("\\f"); break;
                case '\r': sb.Append("\\r"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    /// <summary>A JSON number's text as ECMAScript's <c>Number.prototype.toString</c> writes the double it parses to.</summary>
    public static string Number(string raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || double.IsNaN(d) || double.IsInfinity(d))
            throw new InvalidDataException($"The number {raw} is not a finite double.");
        return Number(d);
    }

    /// <summary>A double as ECMAScript writes it (ECMA-262, Number::toString): the shortest digits that read back to it.</summary>
    public static string Number(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) throw new InvalidDataException("NaN and infinities are not JSON.");
        if (d == 0) return "0";
        var negative = d < 0;
        // "R" is the shortest round-trip form on .NET Core 3.0 and later: its digits are ECMAScript's.
        var shortest = Math.Abs(d).ToString("R", CultureInfo.InvariantCulture);
        var (digits, exponent) = Decompose(shortest);
        // value = 0.digits × 10^n, digits without leading or trailing zeros; k digits.
        var k = digits.Length;
        var n = exponent;
        var sb = new StringBuilder();
        if (negative) sb.Append('-');
        if (k <= n && n <= 21) sb.Append(digits).Append('0', n - k);
        else if (0 < n && n <= 21) sb.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
        else if (-6 < n && n <= 0) sb.Append("0.").Append('0', -n).Append(digits);
        else
        {
            var e = n - 1;
            sb.Append(digits[0]);
            if (k > 1) sb.Append('.').Append(digits, 1, k - 1);
            sb.Append('e').Append(e < 0 ? '-' : '+').Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>.NET's shortest form ("1.2345E-07", "123.45", "1E+21") as its significant digits and n, with value = 0.d₁d₂… × 10ⁿ.</summary>
    static (string Digits, int N) Decompose(string s)
    {
        var exp = 0;
        var eAt = s.IndexOfAny(['E', 'e']);
        if (eAt >= 0)
        {
            exp = int.Parse(s[(eAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            s = s[..eAt];
        }
        var dot = s.IndexOf('.');
        var intPart = dot < 0 ? s : s[..dot];
        var fracPart = dot < 0 ? "" : s[(dot + 1)..];
        var all = intPart + fracPart;
        // The point sits after intPart's digits: value = 0.all × 10^(intPart.Length + exp).
        var n = intPart.Length + exp;
        var lead = 0;
        while (lead < all.Length - 1 && all[lead] == '0') { lead++; n--; }
        all = all[lead..].TrimEnd('0');
        if (all.Length == 0) all = "0";
        return (all, n);
    }
}
