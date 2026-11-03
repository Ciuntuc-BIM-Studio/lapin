using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// One end of a steel member (decision 268), a set of rules its body is derived from:
/// <list type="bullet">
/// <item><see cref="Extension"/>: how far the end reaches past its point (negative: set back, a fitting gap);</item>
/// <item><see cref="Cut"/>: <c>square</c>; <c>plane</c>, turned from square by <see cref="PlanAngle"/> about the profile's
/// up and by <see cref="ElevAngle"/> about its side (degrees); <c>mitre</c> with the member <see cref="Ref"/>, on the plane
/// halving the angle between their axes; <c>fit</c> on the face of <see cref="Ref"/> it comes to (both follow it when it
/// moves);</item>
/// <item>a cope: the flanges notched <see cref="CopeLength"/> back from the end and <see cref="CopeDepth"/> deep from the
/// profile's top (<see cref="CopeTop"/>) and/or bottom (<see cref="CopeBottom"/>).</item>
/// </list>
/// </summary>
public sealed record SteelEnd(double Extension = 0, string Cut = "square", double PlanAngle = 0, double ElevAngle = 0, string? Ref = null,
    double CopeLength = 0, double CopeDepth = 0, bool CopeTop = false, bool CopeBottom = false)
{
    public static readonly string[] Cuts = ["square", "plane", "mitre", "fit"];
    public static readonly SteelEnd Square = new();

    /// <summary>The key a member, a beam or a column keeps its ends under: {start, end}.</summary>
    public const string Key = "steelEnds";

    public bool Coped => CopeLength > 0 && CopeDepth > 0 && (CopeTop || CopeBottom);
    public bool Refers => Cut is "mitre" or "fit" && !string.IsNullOrEmpty(Ref);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["cut"] = Cut };
        if (Extension != 0) o["extension"] = Extension;
        if (Cut == "plane") { o["planAngle"] = PlanAngle; o["elevAngle"] = ElevAngle; }
        if (Ref != null && Cut is "mitre" or "fit") o["ref"] = Ref;
        if (Coped) { o["copeLength"] = CopeLength; o["copeDepth"] = CopeDepth; o["copeTop"] = CopeTop; o["copeBottom"] = CopeBottom; }
        return o;
    }

    public static SteelEnd From(JsonNode? n)
    {
        if (n is not JsonObject o) return Square;
        static bool B(JsonNode? x) => x is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        var cut = (string?)o["cut"] is { } c && Cuts.Contains(c) ? c : "square";
        return new SteelEnd(Json.Number(o["extension"]) ?? 0, cut, Json.Number(o["planAngle"]) ?? 0, Json.Number(o["elevAngle"]) ?? 0, (string?)o["ref"],
            Math.Max(0, Json.Number(o["copeLength"]) ?? 0), Math.Max(0, Json.Number(o["copeDepth"]) ?? 0), B(o["copeTop"]), B(o["copeBottom"]));
    }

    /// <summary>An end as a command gives it, checked: a known cut, angles short of the axis, a member to mitre or fit to.</summary>
    public static SteelEnd Parse(JsonNode? n)
    {
        if (n is JsonObject o && (string?)o["cut"] is { } c && !Cuts.Contains(c)) throw new CommandException($"An end is cut square, by a plane, mitred or fitted, not „{c}”.");
        var e = From(n);
        if (Math.Abs(e.PlanAngle) >= 80 || Math.Abs(e.ElevAngle) >= 80) throw new CommandException("An end's cut turns less than 80° from square.");
        if (e.Cut is "mitre" or "fit" && string.IsNullOrEmpty(e.Ref)) throw new CommandException("A mitred or fitted end names the member it meets.");
        return e;
    }

    /// <summary>The ends an element keeps: start and end, square when it has none.</summary>
    public static (SteelEnd Start, SteelEnd End) Of(Entity e) =>
        e.Attrs[Key] is JsonObject o ? (From(o["start"]), From(o["end"])) : (Square, Square);

    public static JsonObject Json2(SteelEnd start, SteelEnd end) => new() { ["start"] = start.ToJson(), ["end"] = end.ToJson() };
}
