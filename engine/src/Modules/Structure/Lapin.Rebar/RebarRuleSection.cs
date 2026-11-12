using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// The reinforcement's rules in a rule pack (docs/plan-user-extensibility.md §5.4, §6.5): the steels offered and the one a new bar
/// takes, the diameters offered (mm), the mandrel (a factor of Ø up to a diameter, the last one beyond) and the hooks' straight
/// tails (a factor of Ø up to an angle, the last beyond, never under the least) — Lapin's own SR EN 1992-1-1 8.3's (indicative).
/// A bar made takes the pack's steel and, when the pack's mandrel differs from Lapin's, its bend radius; a hook its tail. The check
/// lists bars of a steel or a diameter the pack does not offer.
/// </summary>
public sealed class RebarRuleSection : IRuleSection, IRuleCheck
{
    public const string SectionKey = "rebar";
    public string Key => SectionKey;
    public string Label => "Reinforcement";

    public JsonObject Default => new()
    {
        ["steels"] = new JsonArray(RebarShapes.Steels.Select(s => (JsonNode)s).ToArray()),
        ["defaultSteel"] = RebarShapes.DefaultSteel,
        ["diameters"] = new JsonArray(RebarShapes.Diameters.Select(d => (JsonNode)Math.Round(d * 1000, 3)).ToArray()),
        ["mandrel"] = new JsonArray(new JsonObject { ["upTo"] = 16, ["factor"] = 4 }, new JsonObject { ["factor"] = 7 }),
        ["hooks"] = new JsonArray(new JsonObject { ["upTo"] = 90, ["factor"] = 12 }, new JsonObject { ["upTo"] = 135, ["factor"] = 10 }, new JsonObject { ["factor"] = 5 }),
        ["minHook"] = 50,
    };

    static JsonArray Steps(JsonNode? n, JsonArray fallback, string what, double maxUpTo)
    {
        if (n is not JsonArray a) return (JsonArray)fallback.DeepClone();
        if (a.Count == 0 || a.Count > 12) throw new CommandException($"The {what} is one to twelve steps.");
        var o = new JsonArray();
        double? last = null;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] is not JsonObject s || Json.Number(s["factor"]) is not { } f || !(f > 0 && f <= 50)) throw new CommandException($"Each step of the {what} has a factor of Ø above 0 and at most 50.");
            var step = new JsonObject { ["factor"] = f };
            if (i < a.Count - 1)
            {
                var up = Json.Number(s["upTo"]) ?? throw new CommandException($"Each step of the {what} but the last says up to what.");
                if (!(up > 0 && up <= maxUpTo) || (last is { } l && up <= l)) throw new CommandException($"The {what}'s steps go up, each above the one before.");
                step["upTo"] = up;
                last = up;
            }
            o.Add((JsonNode)step);
        }
        return o;
    }

    public JsonObject Check(JsonNode? section)
    {
        var d = Default;
        var given = section as JsonObject ?? new JsonObject();
        var steels = given["steels"] is JsonArray sa ? sa.Select(x => ((string?)x)?.Trim()).OfType<string>().Where(s => s.Length is > 0 and <= 30).Distinct().ToList() : RebarShapes.Steels.ToList();
        if (steels.Count == 0) throw new CommandException("The rules offer one steel at least.");
        var def = ((string?)given["defaultSteel"])?.Trim() is { Length: > 0 } ds ? ds : steels.Contains(RebarShapes.DefaultSteel) ? RebarShapes.DefaultSteel : steels[0];
        if (!steels.Contains(def)) throw new CommandException($"The default steel „{def}” is not one the rules offer.");
        var diameters = given["diameters"] is JsonArray da ? da.Select(x => Json.Number(x) ?? double.NaN).ToList() : RebarShapes.Diameters.Select(x => Math.Round(x * 1000, 3)).ToList();
        if (diameters.Count == 0 || diameters.Any(x => !(x >= 4 && x <= 50))) throw new CommandException("Each diameter offered is 4 to 50 mm.");
        return new JsonObject
        {
            ["steels"] = new JsonArray(steels.Select(s => (JsonNode)s).ToArray()), ["defaultSteel"] = def,
            ["diameters"] = new JsonArray(diameters.Distinct().Order().Select(x => (JsonNode)x).ToArray()),
            ["mandrel"] = Steps(given["mandrel"], (JsonArray)d["mandrel"]!, "mandrel", 50),
            ["hooks"] = Steps(given["hooks"], (JsonArray)d["hooks"]!, "hooks' tails", 180),
            ["minHook"] = RulePacks.Num(given, d, "minHook", 0, 500, "The least hook tail (mm)"),
        };
    }

    static JsonObject Rules(IModelView m) { var s = new RebarRuleSection(); try { return s.Check(RulePacks.RawSection(m, SectionKey)); } catch (CommandException) { return s.Default; } }

    static double Factor(JsonArray steps, double x)
    {
        foreach (var s in steps.OfType<JsonObject>())
            if (Json.Number(s["upTo"]) is not { } up || x <= up + 1e-9) return Json.Number(s["factor"]) ?? 1;
        return Json.Number(steps.OfType<JsonObject>().Last()["factor"]) ?? 1;
    }

    /// <summary>The bend's radius on the bar's axis (m) the project's rules give a diameter (m): half the mandrel.</summary>
    public static double BendRadius(IModelView m, double d) => Factor((JsonArray)Rules(m)["mandrel"]!, d * 1000) * d / 2;

    /// <summary>A hook's straight tail (m) the project's rules give.</summary>
    public static double HookLength(IModelView m, double d, double angle)
    {
        var r = Rules(m);
        return Math.Max((Json.Number(r["minHook"]) ?? 50) / 1000, Factor((JsonArray)r["hooks"]!, angle) * d);
    }

    /// <summary>Whether the project's rules are Lapin's own for the mandrel (a bar then needs no bend radius of its own).</summary>
    static bool LapinMandrel(IModelView m) => JsonNode.DeepEquals(Rules(m)["mandrel"], new RebarRuleSection().Default["mandrel"]);

    /// <summary>
    /// A bar's arguments as the project's rules complete them before it is made: its steel when none is given, its hooks' tails
    /// where they give none, and its bend radius when the rules' mandrel is not Lapin's (a polyline bar keeps its own).
    /// </summary>
    public static void Complete(IModelView m, JsonObject a, double diameter, string shape)
    {
        if (RulePacks.RawSection(m, SectionKey) == null) return;
        var r = Rules(m);
        if (a["steel"] == null && (string?)r["defaultSteel"] is { } steel && steel != RebarShapes.DefaultSteel) a["steel"] = steel;
        if (a["hooks"] is JsonObject ho)
            foreach (var end in new[] { "start", "end" })
                if (ho[end] is JsonObject h && h["length"] == null && Json.Number(h["angle"]) is { } angle && angle > 0) h["length"] = Math.Round(HookLength(m, diameter, angle), 6);
        if (shape != "polyline" && a["bendRadius"] == null && !LapinMandrel(m)) a["bendRadius"] = Math.Round(BendRadius(m, diameter), 6);
    }

    public IEnumerable<RuleFinding> Check(IQueryContext q)
    {
        var r = Rules(q.Model);
        var steels = (r["steels"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var diameters = (r["diameters"] as JsonArray ?? []).Select(x => Json.Number(x) ?? 0).ToList();
        foreach (var b in q.Model.OfClass(Rebars.Class))
        {
            var steel = b.Str("steel") ?? RebarShapes.DefaultSteel;
            if (!steels.Contains(steel)) yield return new RuleFinding("rebar", b.Id, "info", $"„{b.Name}” is of {steel}, a steel the project's rules do not offer.");
            var d = Math.Round(b.Num("diameter") * 1000, 3);
            if (d > 0 && !diameters.Any(x => Math.Abs(x - d) < 1e-6)) yield return new RuleFinding("rebar", b.Id, "info", $"„{b.Name}” is Ø{d:0.#}, a diameter the project's rules do not offer.");
        }
    }

    public static void Register(IModuleRegistry r)
    {
        var s = new RebarRuleSection();
        r.Service<IRuleSection>(s);
        r.Service<IRuleCheck>(s);
        // The project's reinforcement rules: the steels and diameters to offer, the mandrel and the hooks.
        r.Query("rules", (q, _) => Rules(q.Model));
    }
}
