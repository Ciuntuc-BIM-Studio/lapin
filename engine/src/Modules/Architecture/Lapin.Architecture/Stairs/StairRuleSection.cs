using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// The stairs' rules in a rule pack (docs/plan-user-extensibility.md §5.4): each set (residential, public, entrance, ramp, and any
/// the user adds) its riser, tread and step bounds, least width, most risers, headroom and slope — Lapin's own the ones
/// <see cref="StairRules"/> has always had (STAS 2965, NP 063, P118, indicative). A new stair type takes its set from the project's
/// pack; Apply the project's rules to stair types copies them in; the check lists types whose rules differ from the pack's.
/// </summary>
public sealed class StairRuleSection : IRuleSection, IRuleCheck
{
    public const string SectionKey = "stairs";
    public string Key => SectionKey;
    public string Label => "Stairs";

    static readonly StairRules[] Builtins = [StairRules.Residential, StairRules.Public, StairRules.Entrance, StairRules.Ramp];

    static JsonObject Json(StairRules r)
    {
        var o = (JsonObject)JsonSerializer.SerializeToNode(r, ArchitectureCatalogJson.Default.StairRules)!;
        o.Remove("set");
        return o;
    }

    public JsonObject Default => new(Builtins.Select(b => KeyValuePair.Create(b.Set, (JsonNode?)Json(b))));

    /// <summary>A set's rules from its JSON over <paramref name="d"/> (a field left out keeps <paramref name="d"/>'s), checked.</summary>
    public static StairRules Read(string set, JsonObject o, StairRules d)
    {
        double N(string k, double v, double min, double max, string label) => RulePacks.Num(o, new JsonObject { [k] = v }, k, min, max, label);
        var r = new StairRules(set,
            N("riserMin", d.RiserMin, 0.05, 0.3, "The least riser"), N("riserMax", d.RiserMax, 0.05, 0.3, "The most riser"), N("riserTarget", d.RiserTarget, 0.05, 0.3, "The riser aimed at"),
            N("treadMin", d.TreadMin, 0.1, 0.6, "The least tread"), N("treadMax", d.TreadMax, 0.1, 0.6, "The most tread"), N("treadTarget", d.TreadTarget, 0.1, 0.6, "The tread aimed at"),
            N("stepMin", d.StepMin, 0.4, 0.8, "The least step (2 risers + a tread)"), N("stepMax", d.StepMax, 0.4, 0.8, "The most step"),
            N("minWidth", d.MinWidth, 0.5, 5, "The least width"), (int)N("maxRisers", d.MaxRisers, 1, 60, "The most risers in a flight"),
            N("headroom", d.Headroom, 1.5, 4, "The headroom"), N("maxSlope", d.MaxSlope, 0, 0.5, "The most slope"), (int)N("minRisers", d.MinRisers, 1, 20, "The least risers"));
        if (r.RiserMin > r.RiserMax || r.TreadMin > r.TreadMax || r.StepMin > r.StepMax) throw new CommandException($"{set}: a least value is above its most.");
        return r;
    }

    public JsonObject Check(JsonNode? section)
    {
        var given = section as JsonObject ?? new JsonObject();
        var o = new JsonObject();
        foreach (var b in Builtins) o[b.Set] = Json(Read(b.Set, given[b.Set] as JsonObject ?? new JsonObject(), b));
        // Sets of the user's own: read over the residential one.
        foreach (var (k, v) in given)
            if (o[k] == null && v is JsonObject so && k.Length is > 0 and <= 40) o[k] = Json(Read(k, so, StairRules.Residential));
        return o;
    }

    /// <summary>The project's rules for a set (Lapin's when the edition has no rule packs or the set is unknown).</summary>
    public static StairRules For(IModelView m, IServices? s, string? set)
    {
        var name = set ?? "residential";
        var builtin = StairRules.Of(name);
        if (RulePacks.TrySection(m, s, SectionKey)?[name] is JsonObject o)
            try { return Read(name, o, builtin with { Set = name }); } catch (CommandException) { }
        return builtin;
    }

    public IEnumerable<RuleFinding> Check(IQueryContext q)
    {
        foreach (var t in StairsModule.Types(q.Model))
        {
            if (StairSpec.From(t.Attrs["spec"]) is not { } spec) continue;
            var pack = For(q.Model, q.Services, spec.Rules.Set);
            var differs = Diff(spec.Rules, pack).ToList();
            if (differs.Count > 0)
                yield return new RuleFinding("architecture", t.Id, "warning", $"„{t.Name}” ({spec.Rules.Set}): {string.Join(", ", differs)} differ from the project's rules.");
        }
    }

    static IEnumerable<string> Diff(StairRules a, StairRules b)
    {
        var ja = Json(a);
        var jb = Json(b);
        foreach (var (k, v) in jb)
            if (!JsonNode.DeepEquals(ja[k], v)) yield return $"{k} {ja[k]} (rules {v})";
    }

    public static void Register(IModuleRegistry r)
    {
        var section = new StairRuleSection();
        r.Service<IRuleSection>(section);
        r.Service<IRuleCheck>(section);

        // The project's rules for a stair set ({set}), for a new type.
        r.Query("stairRules", (q, a) => Json(For(q.Model, q.Services, (string?)a["set"])));

        // The project's rules copied into stair types ({typeIds?}: all), each keeping its set; one undo step.
        r.Command("applyStairRules", "Apply rules to stair types", (ctx, a) =>
        {
            var only = a["typeIds"] is JsonArray ids ? ids.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
            var changed = 0;
            foreach (var t in StairsModule.Types(ctx.Model).ToList())
            {
                if (only != null && !only.Contains(t.Id)) continue;
                if (StairSpec.From(t.Attrs["spec"]) is not { } spec) continue;
                var rules = For(ctx.Model, ctx.Services, spec.Rules.Set);
                if (rules == spec.Rules) continue;
                var attrs = t.CloneAttrs();
                attrs["spec"] = (spec with { Rules = rules }).ToJson();
                ctx.Replace(t.With(attrs: attrs));
                changed++;
            }
            return new JsonObject { ["changed"] = changed };
        });
    }
}
