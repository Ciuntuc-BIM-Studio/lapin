using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Alignments;

/// <summary>Reading numbers out of JSON however they were written: what the stored specs and the norm profiles are read with.</summary>
static class Json
{
    /// <summary>Any JSON number (however it was written) or numeric string, as a double.</summary>
    public static double? Number(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<float>(out var f)) return f;
        if (v.TryGetValue<decimal>(out var m)) return (double)m;
        if (v.TryGetValue<JsonElement>(out var e) && e.ValueKind == JsonValueKind.Number) return e.GetDouble();
        return v.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : null;
    }
}
