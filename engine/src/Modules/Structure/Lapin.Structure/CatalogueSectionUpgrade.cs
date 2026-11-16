using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Structure;

/// <summary>
/// A section an earlier build made from its own steel catalogue (audit 15.11.2026, D1): the catalogue kept only a profile's
/// four nominal sizes, so a type made from „IPE 300” had no root radius, an „UPN 200” parallel flanges. That catalogue is
/// gone and its names are the built-in library's (<see cref="BuiltInProfiles"/>, the same nominal sizes, EN 10365 and its
/// kin); a stored section that holds such a name with exactly the library's sizes and nothing of its own (no radii, slope,
/// standard, library profile or outline) is the library's profile, so it is written as the library writes it now. A section
/// whose sizes were changed, or that came from a library, stays as it is. Derived, so it needs no mark: a section already
/// written so is left alone.
/// </summary>
public sealed class CatalogueSectionUpgrade : IProjectUpgrade
{
    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        List<Entity>? changed = null;
        for (var i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            if (e.Attrs["section"] is not JsonObject s || Refreshed(s) is not { } now) continue;
            changed ??= [.. entities];
            var attrs = e.CloneAttrs();
            attrs["section"] = now;
            changed[i] = e.With(attrs: attrs);
        }
        return (changed ?? entities, relations);
    }

    /// <summary>The library's section for a stored one the old catalogue made; null when it is not one, or already is the library's.</summary>
    public static JsonObject? Refreshed(JsonObject s)
    {
        if ((string?)s["name"] is not { } name || BuiltInProfiles.Find(name) is not { } p) return null;
        if (s["profileId"] != null || s["standard"] != null || s["rings"] != null || s["r1"] != null || s["r2"] != null || s["slope"] != null) return null;
        var kind = (string?)s["kind"];
        if (kind == "H") kind = "I";
        if (kind == "Σ") kind = "SIGMA";
        if (kind != p.Kind) return null;
        static bool Same(JsonNode? v, double want) => Json.Number(v) is { } x && Math.Abs(x - want) < 1e-9;
        static bool SameOrNone(JsonNode? v, double want) => v == null ? want == 0 : Same(v, want);
        var depth = p.Kind == "CHS" ? p.B : p.H;
        if (!Same(s["width"], p.B) || !Same(s["depth"], depth) || !Same(s["web"], p.Tw) || !Same(s["flange"], p.Tf) || !SameOrNone(s["lip"], p.Lip)) return null;
        // The library's fields over the stored ones (anything else a module keeps beside them stays).
        var now = (JsonObject)s.DeepClone();
        foreach (var (k, v) in p.SectionJson(null)) now[k] = v?.DeepClone();
        return JsonNode.DeepEquals(now, s) ? null : now;
    }
}
