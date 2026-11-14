using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A beam type as a lintel (report #135): its section's box (the width across the wall, the height), the lengths it is made
/// in, the code of each, its manufacturer, the bearing it takes by default, its material and mark. Read by the lintel rules
/// (architecture) and written by the structure types' table import and the lintel catalogue's (both modules), so lintels and
/// beam types are one list. A type with no lengths is cut to length.
/// </summary>
public sealed record BeamLintel(string TypeId, string Name, string? Mark, string? Code, double Width, double Height, double[] Lengths, string[]? Codes,
    string? Manufacturer, double? Bearing, string? Material)
{
    /// <summary>The code of the stock length <paramref name="length"/> (each length its own article), else the type's.</summary>
    public string? CodeOf(double length)
    {
        if (Codes is { Length: > 0 } codes)
            for (var i = 0; i < Lengths.Length && i < codes.Length; i++)
                if (Math.Abs(Lengths[i] - length) < 1e-6 && codes[i].Length > 0) return codes[i];
        return Code;
    }
}

public static class LintelTypes
{
    /// <summary>IfcBeamType's PredefinedType for a lintel.</summary>
    public const string Lintel = "LINTEL";

    /// <summary>The attributes a beam type keeps as a lintel (besides its section, material and mark).</summary>
    public const string LengthsKey = "lengths", CodesKey = "codes", CodeKey = "code", ManufacturerKey = "manufacturer", BearingKey = "bearing", MarkKey = "mark";

    /// <summary>The beam type <paramref name="id"/> as a lintel; null when it is no beam type or has no section.</summary>
    public static BeamLintel? Read(IModelView m, string? id) => id != null && m.Get(id) is { Class: IfcClass.BeamType } t ? Read(m, t) : null;

    public static BeamLintel? Read(IModelView m, Entity t)
    {
        if (t.Attrs["section"] is not JsonObject s || Json.Number(s["width"]) is not { } w || Json.Number(s["depth"]) is not { } d) return null;
        // A round section's depth is its diameter as well.
        if (!(w > 0 && d > 0)) return null;
        var lengths = new List<double>();
        var codes = new List<string>();
        if (t.Attrs[LengthsKey] is JsonArray ls)
            for (var i = 0; i < ls.Count; i++)
                if (Json.Number(ls[i]) is { } l && l > 0) { lengths.Add(l); codes.Add(t.Attrs[CodesKey] is JsonArray cs && i < cs.Count ? (string?)cs[i] ?? "" : ""); }
        var order = lengths.Select((l, i) => (l, i)).OrderBy(x => x.l).ToList();
        return new BeamLintel(t.Id, t.Name, Blank(t.Str(MarkKey)), Blank(t.Str(CodeKey)), w, d, order.Select(x => x.l).ToArray(),
            codes.Any(c => c.Length > 0) ? order.Select(x => codes[x.i]).ToArray() : null,
            Blank(t.Str(ManufacturerKey)), t.NumOrNull(BearingKey) is { } b && b >= 0 ? b : null,
            t.Str("material") is { } mat ? m.Get(mat)?.Name : null);
    }

    static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>The project's beam types that can be lintels (every beam type with a section), those made as lintels first, then by name.</summary>
    public static List<BeamLintel> All(IModelView m) =>
        m.OfClass(IfcClass.BeamType).OrderBy(t => t.PredefinedType == Lintel ? 0 : 1).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => Read(m, t)).OfType<BeamLintel>().ToList();

    public static JsonObject ToJson(BeamLintel b)
    {
        var o = new JsonObject { ["id"] = b.TypeId, ["name"] = b.Name, ["width"] = b.Width, ["height"] = b.Height, ["lengths"] = new JsonArray(b.Lengths.Select(l => (JsonNode)l).ToArray()) };
        if (b.Mark != null) o["mark"] = b.Mark;
        if (b.Code != null) o["code"] = b.Code;
        if (b.Manufacturer != null) o["manufacturer"] = b.Manufacturer;
        if (b.Bearing != null) o["bearing"] = b.Bearing;
        if (b.Material != null) o["material"] = b.Material;
        return o;
    }

    /// <summary>
    /// The lintel facts written into a beam type's attributes: lengths (and their codes) sorted, a code, a manufacturer, a
    /// default bearing, a mark; a value not given (null) taken away.
    /// </summary>
    public static void Write(JsonObject attrs, IReadOnlyList<double>? lengths, IReadOnlyList<string>? codes, string? code, string? manufacturer, double? bearing, string? mark)
    {
        var pairs = (lengths ?? []).Select((l, i) => (L: l, C: codes != null && i < codes.Count ? codes[i] ?? "" : "")).Where(p => p.L > 0)
            .GroupBy(p => Math.Round(p.L, 6)).Select(g => g.First()).OrderBy(p => p.L).ToList();
        void Set(string key, JsonNode? v) { if (v is null) attrs.Remove(key); else attrs[key] = v; }
        Set(LengthsKey, pairs.Count > 0 ? new JsonArray(pairs.Select(p => (JsonNode)p.L).ToArray()) : null);
        Set(CodesKey, pairs.Any(p => p.C.Length > 0) ? new JsonArray(pairs.Select(p => (JsonNode)p.C).ToArray()) : null);
        Set(CodeKey, Blank(code) is { } c ? c : null);
        Set(ManufacturerKey, Blank(manufacturer) is { } mf ? mf : null);
        Set(BearingKey, bearing is { } b && b >= 0 ? b : null);
        Set(MarkKey, Blank(mark) is { } mk ? mk : null);
    }

    /// <summary>A precast lintel's beam type: a rectangle <paramref name="width"/> across the wall by <paramref name="height"/>, PredefinedType LINTEL.</summary>
    public static Entity Create(IEdit edit, string name, double width, double height, string material, IReadOnlyList<double>? lengths, IReadOnlyList<string>? codes,
        string? code, string? manufacturer, double? bearing, string? mark)
    {
        var attrs = new JsonObject
        {
            ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = width, ["depth"] = height },
            ["material"] = Materials.Ensure(edit, material).Id,
        };
        Write(attrs, lengths, codes, code, manufacturer, bearing, mark);
        var t = new Entity(edit.NewId(), IfcClass.BeamType, name.Trim(), attrs, Lintel);
        edit.Add(t);
        return t;
    }
}
