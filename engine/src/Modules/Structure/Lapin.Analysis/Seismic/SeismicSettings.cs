using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis.Seismic;

/// <summary>
/// The project's seismic parameters (docs/plan-fem.md §11.3), on the IfcProject (<c>seismic</c>), written by
/// <c>analysis.setSeismic</c>: the code (P100-1/2013 by default, EN 1998-1 with its recommended values the option), the site's
/// design ground acceleration ag (as a fraction of g; the user's, from the code's map: Lapin ships no zoning map), P100's
/// corner period TC or EN's ground type and spectrum type, the importance class, the behaviour factor q, the damping, the
/// modal and directional combinations, the accidental eccentricity, the modes' limit, the cracked stiffness of concrete and
/// masonry, the optional rigid diaphragm (only for levels without a shell floor) and the drift limits. All indicative.
/// </summary>
public sealed record SeismicSettings
{
    public const string Key = "seismic";
    public const string P100 = "P100-1/2013", En1998 = "EN 1998-1";
    public static readonly string[] Codes = [P100, En1998];
    public static readonly double[] TcChoices = [0.7, 1.0, 1.6];
    public static readonly string[] Grounds = ["A", "B", "C", "D", "E"];
    public static readonly string[] Classes = ["I", "II", "III", "IV"];
    public static readonly string[] ModalChoices = ["cqc", "srss"];
    public static readonly string[] DirectionChoices = ["30%", "srss"];
    public static readonly string[] DiaphragmChoices = ["none", "rigid"];

    public string Code { get; init; } = P100;
    /// <summary>ag / g (P100: the design ground acceleration for the 225-year return period; EN: agR / g).</summary>
    public double? Ag { get; init; }
    public double Tc { get; init; } = 1.0;
    public string Ground { get; init; } = "C";
    public int SpectrumType { get; init; } = 1;
    public string Importance { get; init; } = "III";
    public double Q { get; init; } = 1.5;
    public double Damping { get; init; } = 0.05;
    public string ModalRule { get; init; } = "cqc";
    public string DirectionRule { get; init; } = "30%";
    public double Torsion { get; init; } = 0.05;
    public int Modes { get; init; } = 60;
    public double CrackedConcrete { get; init; } = 0.5;
    public double CrackedMasonry { get; init; } = 0.5;
    public string Diaphragm { get; init; } = "none";
    public double DriftSls { get; init; } = 0.005;
    public double DriftUls { get; init; } = 0.025;
    public double DriftNu { get; init; } = 0.5;
    /// <summary>Snow's ψ2 in the seismic combination: P100 / CR 0-2012 0.4 (to confirm), EN 1990 0 (sites up to 1000 m).</summary>
    public double? SnowPsi2 { get; init; }

    public bool Given => Ag is > 0;
    public bool IsP100 => Code == P100;
    public double SnowPsi2Used => SnowPsi2 ?? (IsP100 ? 0.4 : 0);

    /// <summary>The importance factor: P100-1/2013 γI,e (I 1.4, II 1.2, III 1.0, IV 0.8; §4.4.5, to confirm), EN 1998-1 γI (I 0.8, II 1.0, III 1.2, IV 1.4; §4.2.5).</summary>
    public double Gamma => IsP100
        ? Importance switch { "I" => 1.4, "II" => 1.2, "III" => 1.0, _ => 0.8 }
        : Importance switch { "I" => 0.8, "II" => 1.0, "III" => 1.2, _ => 1.4 };

    public static readonly SeismicSettings Default = new();

    public static SeismicSettings Of(IModelView m) => Spatial.Project(m)?.Attrs[Key] is JsonObject o ? Read(o) : Default;

    static double? Num(JsonObject o, string k) => Json.Number(o[k]) is { } v && double.IsFinite(v) ? v : null;

    static SeismicSettings Read(JsonObject o)
    {
        var d = Default;
        var cracked = o["cracked"] as JsonObject;
        var drift = o["drift"] as JsonObject;
        return new SeismicSettings
        {
            Code = (string?)o["code"] is { } c && Codes.Contains(c) ? c : d.Code,
            Ag = Num(o, "ag"),
            Tc = Num(o, "tc") ?? d.Tc,
            Ground = (string?)o["ground"] is { } g && Grounds.Contains(g) ? g : d.Ground,
            SpectrumType = Num(o, "spectrum") is 2 ? 2 : 1,
            Importance = (string?)o["importance"] is { } i && Classes.Contains(i) ? i : d.Importance,
            Q = Num(o, "q") ?? d.Q,
            Damping = Num(o, "damping") ?? d.Damping,
            ModalRule = (string?)o["modal"] is { } mr && ModalChoices.Contains(mr) ? mr : d.ModalRule,
            DirectionRule = (string?)o["directions"] is { } dr && DirectionChoices.Contains(dr) ? dr : d.DirectionRule,
            Torsion = Num(o, "torsion") ?? d.Torsion,
            Modes = (int)(Num(o, "modes") ?? d.Modes),
            CrackedConcrete = cracked != null ? Num(cracked, "concrete") ?? d.CrackedConcrete : d.CrackedConcrete,
            CrackedMasonry = cracked != null ? Num(cracked, "masonry") ?? d.CrackedMasonry : d.CrackedMasonry,
            Diaphragm = (string?)o["diaphragm"] is { } di && DiaphragmChoices.Contains(di) ? di : d.Diaphragm,
            DriftSls = drift != null ? Num(drift, "sls") ?? d.DriftSls : d.DriftSls,
            DriftUls = drift != null ? Num(drift, "uls") ?? d.DriftUls : d.DriftUls,
            DriftNu = drift != null ? Num(drift, "nu") ?? d.DriftNu : d.DriftNu,
            SnowPsi2 = Num(o, "snowPsi2"),
        };
    }

    public JsonObject ToJson() => new()
    {
        ["code"] = Code, ["ag"] = Ag, ["tc"] = Tc, ["ground"] = Ground, ["spectrum"] = SpectrumType, ["importance"] = Importance, ["gamma"] = Gamma,
        ["q"] = Q, ["damping"] = Damping, ["modal"] = ModalRule, ["directions"] = DirectionRule, ["torsion"] = Torsion, ["modes"] = Modes,
        ["cracked"] = new JsonObject { ["concrete"] = CrackedConcrete, ["masonry"] = CrackedMasonry }, ["diaphragm"] = Diaphragm,
        ["drift"] = new JsonObject { ["sls"] = DriftSls, ["uls"] = DriftUls, ["nu"] = DriftNu }, ["snowPsi2"] = SnowPsi2Used, ["given"] = Given,
    };

    /// <summary>The query analysis.seismicOptions: the project's values, the defaults, the choices, and P100's masonry q (Table 8.10, indicative).</summary>
    public JsonObject OptionsJson()
    {
        var o = ToJson();
        o["defaults"] = Default.ToJson();
        o["choices"] = new JsonObject
        {
            ["code"] = new JsonArray(Codes.Select(x => (JsonNode)x).ToArray()), ["tc"] = new JsonArray(TcChoices.Select(x => (JsonNode)x).ToArray()),
            ["ground"] = new JsonArray(Grounds.Select(x => (JsonNode)x).ToArray()), ["importance"] = new JsonArray(Classes.Select(x => (JsonNode)x).ToArray()),
            ["modal"] = new JsonArray(ModalChoices.Select(x => (JsonNode)x).ToArray()), ["directions"] = new JsonArray(DirectionChoices.Select(x => (JsonNode)x).ToArray()),
            ["diaphragm"] = new JsonArray(DiaphragmChoices.Select(x => (JsonNode)x).ToArray()),
        };
        // P100-1/2013 Table 8.10 (as quoted by Partene, UPT 2018): q = factor · αu/α1 for masonry, regular / irregular.
        o["masonryQ"] = new JsonArray(
            new JsonObject { ["kind"] = "ZNA", ["regular"] = 1.75, ["irregular"] = 1.5 },
            new JsonObject { ["kind"] = "ZC", ["regular"] = 2.25, ["irregular"] = 2.0 },
            new JsonObject { ["kind"] = "ZC+AR", ["regular"] = 2.5, ["irregular"] = 2.25 },
            new JsonObject { ["kind"] = "ZIA", ["regular"] = 2.75, ["irregular"] = 2.5 });
        o["indicative"] = Indicative;
        return o;
    }

    public const string Indicative = "indicative: P100-1/2013 (or EN 1998-1, its recommended values); the site's ag and TC are yours";

    /// <summary>
    /// {code?, ag?, tc?, ground?, spectrum?, importance?, q?, damping?, modal?, directions?, torsion?, modes?, cracked?, diaphragm?,
    /// drift?, snowPsi2?}: written onto the project's own, each checked; null takes a key back to its default (ag null: no
    /// seismic analysis), absent leaves it.
    /// </summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        string[] keys = ["code", "ag", "tc", "ground", "spectrum", "importance", "q", "damping", "modal", "directions", "torsion", "modes", "cracked", "diaphragm", "drift", "snowPsi2"];
        if (!keys.Any(a.ContainsKey)) throw new CommandException($"Say what to set: {string.Join(", ", keys)}.");
        foreach (var k in a.Select(x => x.Key)) if (!keys.Contains(k)) throw new CommandException($"„{k}” is not a seismic parameter: {string.Join(", ", keys)}.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var own = attrs[Key] is JsonObject had ? (JsonObject)had.DeepClone() : new JsonObject();
        static string F(double v) => v.ToString(CultureInfo.InvariantCulture);
        // Each key read where it is named, so the command catalogue sees them (scripts/command-catalogue.mjs).
        void Number(string key, double? given, double min, double max, string what)
        {
            if (!a.ContainsKey(key)) return;
            if (a[key] is null) { own.Remove(key); return; }
            if (given is { } v && double.IsFinite(v) && v >= min && v <= max) own[key] = v;
            else throw new CommandException($"{what} is a number from {F(min)} to {F(max)}.");
        }
        void Choice(string key, string? given, string[] choices, string what)
        {
            if (!a.ContainsKey(key)) return;
            if (a[key] is null) { own.Remove(key); return; }
            if (given is { } s && choices.Contains(s)) own[key] = s;
            else throw new CommandException($"{what} is one of {string.Join(", ", choices)}.");
        }
        void Group(string key, JsonObject? given, (string Key, double Min, double Max, string What)[] parts)
        {
            if (!a.ContainsKey(key)) return;
            if (a[key] is null) { own.Remove(key); return; }
            if (given is not { } g) throw new CommandException($"{key} is {{{string.Join(", ", parts.Select(p => p.Key))}}}.");
            var mine = own[key] is JsonObject h ? (JsonObject)h.DeepClone() : new JsonObject();
            foreach (var (k, min, max, what) in parts)
            {
                if (!g.ContainsKey(k)) continue;
                if (g[k] is null) { mine.Remove(k); continue; }
                if (Json.Number(g[k]) is { } v && double.IsFinite(v) && v >= min && v <= max) mine[k] = v;
                else throw new CommandException($"{what} is a number from {F(min)} to {F(max)}.");
            }
            if (mine.Count > 0) own[key] = mine; else own.Remove(key);
        }
        Choice("code", (string?)a["code"], Codes, "The seismic code");
        Number("ag", Json.Number(a["ag"]), 0.01, 1.5, "The design ground acceleration ag (a fraction of g)");
        if (a.ContainsKey("tc") && a["tc"] is not null && !(Json.Number(a["tc"]) is { } tc && TcChoices.Any(x => Math.Abs(x - tc) < 1e-9)))
            throw new CommandException("The corner period TC is 0.7, 1.0 or 1.6 s (P100-1/2013 Table 3.1).");
        Number("tc", Json.Number(a["tc"]), 0.7, 1.6, "TC");
        Choice("ground", (string?)a["ground"], Grounds, "The ground type");
        if (a.ContainsKey("spectrum") && a["spectrum"] is not null && Json.Number(a["spectrum"]) is not (1 or 2)) throw new CommandException("The spectrum type is 1 or 2 (EN 1998-1 3.2.2.2).");
        Number("spectrum", Json.Number(a["spectrum"]), 1, 2, "The spectrum type");
        Choice("importance", (string?)a["importance"], Classes, "The importance class");
        Number("q", Json.Number(a["q"]), 1, 8, "The behaviour factor q");
        Number("damping", Json.Number(a["damping"]), 0.005, 0.3, "The damping ratio ξ");
        Choice("modal", (string?)a["modal"], ModalChoices, "The modal combination");
        Choice("directions", (string?)a["directions"], DirectionChoices, "The combination of the directions");
        Number("torsion", Json.Number(a["torsion"]), 0, 0.2, "The accidental eccentricity (a fraction of the storey's width)");
        Number("modes", Json.Number(a["modes"]), 1, 300, "The modes' limit");
        Group("cracked", a["cracked"] as JsonObject, [("concrete", 0.1, 1, "Concrete's cracked stiffness factor"), ("masonry", 0.1, 1, "Masonry's cracked stiffness factor")]);
        Choice("diaphragm", (string?)a["diaphragm"], DiaphragmChoices, "The diaphragm");
        Group("drift", a["drift"] as JsonObject, [("sls", 0.001, 0.05, "The serviceability drift limit"), ("uls", 0.005, 0.1, "The ultimate drift limit"), ("nu", 0.1, 1, "The reduction factor ν")]);
        Number("snowPsi2", Json.Number(a["snowPsi2"]), 0, 1, "Snow's ψ2 in the seismic combination");
        attrs.Remove(Key);
        if (own.Count > 0) attrs[Key] = own;
        ctx.Replace(project.With(attrs: attrs));
    }
}
