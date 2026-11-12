using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// A connection's parameters as they are kept (decision 273): each one given, or null for the default the profiles it joins
/// choose (<see cref="NodeSpec.Resolve"/>). Lengths in metres; <see cref="Bolt"/> the bolts' (or anchors') nominal
/// diameter; <see cref="Rows"/> the rows of bolts between the flanges (an extended end plate adds one above), <see cref="Cols"/>
/// the bolts across (2 or 4); <see cref="Edge"/>, <see cref="Pitch"/> and <see cref="Gauge"/> the distances to the plate's
/// edges, between rows and between the two middle columns; <see cref="Extension"/> how far an eaves' end plate stands
/// above the rafter; the column base's <see cref="Base"/> (pinned or fixed), its anchors (<see cref="Anchor"/>: straight,
/// hooked or with an anchor plate; how many; their length below the concrete's top) and the grout under its plate;
/// a gable post's <see cref="Gap"/> under its rafter.
/// </summary>
public sealed record NodeParams(
    double? Plate = null, double? Bolt = null, string? Grade = null, int? Rows = null, int? Cols = null,
    double? Edge = null, double? Pitch = null, double? Gauge = null, double? Extension = null, bool? Stiffeners = null,
    string? Base = null, string? Anchor = null, int? Anchors = null, double? AnchorLength = null, double? Grout = null, double? Gap = null)
{
    public static readonly NodeParams None = new();
    public static readonly string[] Bases = ["pinned", "fixed"];
    public static readonly string[] AnchorKinds = ["straight", "hooked", "plate"];
    public static readonly string[] Grades = ["4.6", "5.6", "8.8", "10.9"];

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        void N(string k, double? v) { if (v is { } x) o[k] = Math.Round(x, 6); }
        N("plate", Plate); N("bolt", Bolt);
        if (Grade != null) o["grade"] = Grade;
        if (Rows is { } r) o["rows"] = r;
        if (Cols is { } c) o["cols"] = c;
        N("edge", Edge); N("pitch", Pitch); N("gauge", Gauge); N("extension", Extension);
        if (Stiffeners is { } s) o["stiffeners"] = s;
        if (Base != null) o["base"] = Base;
        if (Anchor != null) o["anchor"] = Anchor;
        if (Anchors is { } a) o["anchors"] = a;
        N("anchorLength", AnchorLength); N("grout", Grout); N("gap", Gap);
        return o;
    }

    public static NodeParams From(JsonNode? n)
    {
        if (n is not JsonObject o) return None;
        static bool? B(JsonNode? x) => x is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
        static int? I(JsonNode? x) => Json.Number(x) is { } d ? (int)Math.Round(d) : null;
        string? S(string k, string[] allowed) => (string?)o[k] is { } s && allowed.Contains(s) ? s : null;
        // A grade is the project's to name (its rule pack offers Lapin's four and any it adds, docs/plan-user-extensibility.md §5.4): a short text.
        var grade = ((string?)o["grade"])?.Trim() is { Length: > 0 and <= 20 } gr ? gr : null;
        return new NodeParams(Json.Number(o["plate"]), Json.Number(o["bolt"]), grade, I(o["rows"]), I(o["cols"]),
            Json.Number(o["edge"]), Json.Number(o["pitch"]), Json.Number(o["gauge"]), Json.Number(o["extension"]), B(o["stiffeners"]),
            S("base", Bases), S("anchor", AnchorKinds), I(o["anchors"]), Json.Number(o["anchorLength"]), Json.Number(o["grout"]), Json.Number(o["gap"]));
    }

    /// <summary>These parameters over <paramref name="under"/>: each given here wins, the others are taken from it.</summary>
    public NodeParams Over(NodeParams under) => new(
        Plate ?? under.Plate, Bolt ?? under.Bolt, Grade ?? under.Grade, Rows ?? under.Rows, Cols ?? under.Cols,
        Edge ?? under.Edge, Pitch ?? under.Pitch, Gauge ?? under.Gauge, Extension ?? under.Extension, Stiffeners ?? under.Stiffeners,
        Base ?? under.Base, Anchor ?? under.Anchor, Anchors ?? under.Anchors, AnchorLength ?? under.AnchorLength, Grout ?? under.Grout, Gap ?? under.Gap);

    /// <summary>A patch as a command gives it: its fields checked; a field given as null goes back to its default.</summary>
    public static NodeParams Patch(NodeParams current, JsonObject patch)
    {
        if (patch["grade"] is JsonValue gv && (string?)gv is { } g && g.Trim() is not { Length: > 0 and <= 20 }) throw new CommandException("A bolt's grade is a short name: 8.8, 10.9, A325…");
        if (patch["base"] is JsonValue bv && (string?)bv is { } b && !Bases.Contains(b)) throw new CommandException("A column base is pinned or fixed.");
        if (patch["anchor"] is JsonValue av && (string?)av is { } a && !AnchorKinds.Contains(a)) throw new CommandException("An anchor is straight, hooked or with an anchor plate.");
        var o = current.ToJson();
        foreach (var (k, v) in patch)
        {
            if (v == null) o.Remove(k);
            else o[k] = v.DeepClone();
        }
        var p = From(o);
        static void Range(double? v, double lo, double hi, string what) { if (v is { } x && !(x >= lo && x <= hi)) throw new CommandException(what); }
        Range(p.Plate, 0.004, 0.12, "A plate is from 4 mm to 120 mm thick.");
        Range(p.Bolt, 0.01, 0.04, "A bolt is from M10 to M39.");
        Range(p.Edge, 0.012, 0.15, "An edge distance is from 12 mm to 150 mm.");
        Range(p.Pitch, 0.025, 0.4, "Bolts stand from 25 mm to 400 mm apart.");
        Range(p.Gauge, 0.03, 0.5, "The bolts' gauge is from 30 mm to 500 mm.");
        Range(p.Extension, 0, 0.4, "An end plate stands at most 400 mm above its rafter.");
        Range(p.AnchorLength, 0.1, 4, "An anchor goes from 0.1 m to 4 m into the concrete.");
        Range(p.Grout, 0, 0.15, "The grout under a base plate is at most 150 mm.");
        Range(p.Gap, 0, 0.2, "The gap over a gable post is at most 200 mm.");
        if (p.Rows is { } r && r is < 1 or > 16) throw new CommandException("An end plate has from 1 to 16 rows of bolts.");
        if (p.Cols is { } c && c is not (2 or 4)) throw new CommandException("An end plate has 2 or 4 bolts in a row.");
        if (p.Anchors is { } n && n is not (2 or 4 or 6 or 8)) throw new CommandException("A column base has 2, 4, 6 or 8 anchors.");
        return p;
    }
}

/// <summary>
/// A connection's parameters resolved: what was given, the rest chosen by the profiles it joins (<see cref="Resolve"/>),
/// as a steel detailer's first guess: bolts from M16 to M27 by the depth of the member bolted, an end plate as thick as
/// its bolts, holes 2 mm wider than the bolt, edges 1.5 and pitches 3 holes, a base plate 20–40 mm, anchors from M20 to
/// M30, 30 diameters into the concrete (25 hooked, 20 with an anchor plate).
/// </summary>
public sealed record NodeSpec(
    double Plate, double Bolt, string Grade, int Rows, int Cols, double Edge, double Pitch, double Gauge, double Extension, bool Stiffeners,
    string Base, string Anchor, int Anchors, double AnchorLength, double Grout, double Gap, bool RowsGiven)
{
    static double Up5(double m) => Math.Ceiling(m * 1000 / 5 - 1e-9) * 5 / 1000;
    static double Up10(double m) => Math.Ceiling(m * 100 - 1e-9) / 100;
    static double Up50(double m) => Math.Ceiling(m * 20 - 1e-9) / 20;

    /// <summary>A bolt's hole: 2 mm wider than the bolt, 3 mm past M24.</summary>
    public static double Hole(double bolt) => bolt + (bolt > 0.024 ? 0.003 : 0.002);

    /// <summary>
    /// The parameters of a connection of <paramref name="kind"/> resolved over the section it bolts
    /// (<paramref name="bolted"/>: the column at a base, the rafter at an eaves, the rafters at an apex or a splice, the
    /// post under a gable rafter).
    /// </summary>
    public static NodeSpec Resolve(string kind, Section bolted, NodeParams p)
    {
        var h = bolted.Depth;
        var basePlate = kind == "base";
        var fixedBase = basePlate && (p.Base ?? "pinned") == "fixed";
        var bolt = p.Bolt ?? (basePlate
            ? fixedBase ? (h < 0.5 ? 0.024 : 0.03) : (h < 0.4 ? 0.02 : 0.024)
            : kind == "post" ? 0.016
            : kind == "cleat" ? (h < 0.25 ? 0.012 : 0.016)
            : kind == "gusset" ? (h < 0.07 ? 0.012 : 0.016)
            : kind == "fly" ? 0.012
            : h < 0.3 ? 0.016 : h < 0.45 ? 0.02 : h < 0.6 ? 0.024 : 0.027);
        var hole = Hole(bolt);
        var edge = p.Edge ?? Up5(1.5 * hole);
        var pitch = p.Pitch ?? Up5(Math.Max(3 * hole, 0.06));
        var plate = p.Plate ?? (basePlate ? (fixedBase ? (h < 0.5 ? 0.03 : 0.04) : (h < 0.4 ? 0.02 : 0.025))
            : kind is "post" or "gusset" ? 0.01 : kind == "cleat" ? (h < 0.25 ? 0.008 : 0.01) : kind == "fly" ? 0.006 : Math.Max(0.012, Up5(bolt)));
        var b = bolted.Width;
        var gauge = p.Gauge ?? Math.Clamp(Math.Round((b - 2 * edge) * 200) / 200, 0.07, 0.14);
        var anchors = p.Anchors ?? (fixedBase ? (h < 0.4 ? 4 : h < 0.6 ? 6 : 8) : h < 0.3 ? 2 : 4);
        if (basePlate && !fixedBase && anchors > 4) anchors = 4;
        if (basePlate && fixedBase && anchors < 4) anchors = 4;
        var anchor = p.Anchor ?? "straight";
        var length = p.AnchorLength ?? Up50(bolt * (anchor == "hooked" ? 25 : anchor == "plate" ? 20 : 30));
        var grade = p.Grade ?? (basePlate ? "5.6" : "8.8");
        return new NodeSpec(plate, bolt, grade, p.Rows ?? 0, p.Cols ?? 2, edge, pitch, gauge,
            p.Extension ?? (kind == "eaves" ? Up10(2 * edge + 0.01) : 0), p.Stiffeners ?? (kind == "eaves" || fixedBase),
            fixedBase ? "fixed" : "pinned", anchor, anchors, length, p.Grout ?? 0.03, p.Gap ?? 0.02, p.Rows != null);
    }

    public JsonObject ToJson() => new()
    {
        ["plate"] = Plate, ["bolt"] = Bolt, ["grade"] = Grade, ["rows"] = Rows, ["cols"] = Cols, ["edge"] = Edge, ["pitch"] = Pitch, ["gauge"] = Gauge,
        ["extension"] = Extension, ["stiffeners"] = Stiffeners, ["base"] = Base, ["anchor"] = Anchor, ["anchors"] = Anchors,
        ["anchorLength"] = AnchorLength, ["grout"] = Grout, ["gap"] = Gap,
    };
}
