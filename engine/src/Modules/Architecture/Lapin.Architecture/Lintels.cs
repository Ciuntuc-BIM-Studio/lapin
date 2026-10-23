using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Architecture;

/// <summary>A precast lintel product: its section and the lengths it comes in, metres.</summary>
public sealed record LintelProduct(string Code, double Width, double Height, double[] Lengths, string? Manufacturer = null);

public sealed record LintelBars(int Count, double Diameter);
public sealed record LintelStirrups(double Diameter, double Spacing);
public sealed record LintelReinforcement(LintelBars? Top = null, LintelBars? Bottom = null, LintelStirrups? Stirrups = null, double? Cover = null);

/// <summary>
/// How the openings of a wall (or of a door or window type) get their
/// lintels (the old Lapin's lintel rule): none, cast in place or precast;
/// the bearing past each side, the height, the narrowest opening that gets
/// one, the width across the wall and where it sits, pieces side by side,
/// the precast product, the reinforcement of a cast one.
/// </summary>
public sealed record LintelRule(
    string Mode,
    double Bearing,
    double Height,
    double? BearingEnd = null,
    double? MinWidth = null,
    double? Width = null,
    string? Placement = null,
    int? Pieces = null,
    string? Material = null,
    LintelProduct? Product = null,
    LintelReinforcement? Reinforcement = null)
{
    public static readonly LintelRule Default = new("precast", 0.25, 0.25, MinWidth: 0);

    public double EndBearing => BearingEnd ?? Bearing;

    /// <summary>A precast product's height, else the rule's.</summary>
    public double LintelHeight => Mode == "precast" && Product != null ? Product.Height : Height;

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, Json.Options)!;

    public static LintelRule? From(JsonNode? node)
    {
        try { return node?.Deserialize<LintelRule>(Json.Options); }
        catch (JsonException) { return null; }
    }
}

/// <summary>An opening as the lintels see it: its key, its extent along the wall and up it, its type's own rule (or none).</summary>
public sealed record LintelOpening(string Key, double X0, double X1, double Z0, double Z1, LintelRule? Rule = null, bool NoLintel = false);

/// <summary>A lintel over one or more openings, or the ring beam coming down over them (a downstand).</summary>
public sealed record Lintel(string Key, string Kind, bool Precast, double Length, double X0, double X1, double Z0, double Z1, LintelRule? Rule = null, bool Fits = true);

/// <summary>Lintels over the openings of a wall (docs/pereti-compusi-si-executie.md, part 2; the old Lapin's rules).</summary>
public static class Lintels
{
    const double Eps = 1e-9;

    /// <summary>The generic precast catalogue: 1.00 to 4.00 m in steps of 25 cm.</summary>
    public static readonly double[] PrecastLengths = Enumerable.Range(0, 13).Select(i => 1 + i * 0.25).ToArray();

    public static IReadOnlyList<string> Problems(LintelRule r)
    {
        var o = new List<string>();
        if (r.Mode is not ("none" or "cast" or "precast")) o.Add("unknown lintel mode");
        if (!(r.Bearing >= 0)) o.Add("the bearing must be zero or more");
        if (!(r.Height > 0)) o.Add("the lintel height must be positive");
        if (r.BearingEnd is { } be && !(be >= 0)) o.Add("the end bearing must be zero or more");
        if (r.Width is { } w && !(w > 0)) o.Add("the lintel width must be positive");
        if (r.Pieces is { } p && p is < 1 or > 6) o.Add("pieces must be 1 to 6");
        if (r.Product is { } pr && !(pr.Width > 0 && pr.Height > 0 && pr.Lengths.Length > 0 && pr.Lengths.All(l => l > 0))) o.Add("the product needs a section and at least one length");
        return o;
    }

    /// <summary>The shortest catalogue length that covers <paramref name="need"/> (past the catalogue: rounded up to 25 cm).</summary>
    public static double PrecastLength(double need) =>
        PrecastLengths.Cast<double?>().FirstOrDefault(l => l >= need - Eps) ?? Math.Ceiling(need / 0.25 - Eps) * 0.25;

    /// <summary>The product's shortest length that covers <paramref name="need"/>, or null when none does.</summary>
    public static double? ProductLength(LintelProduct p, double need) =>
        p.Lengths.Order().Cast<double?>().FirstOrDefault(l => l >= need - Eps);

    const double SteelKgM3 = 7850;
    static double BarArea(double d) => Math.PI * d * d / 4;

    /// <summary>The steel of a cast lintel, kg: its top and bottom bars over its length, and its stirrups.</summary>
    public static double ReinforcementMass(LintelReinforcement? r, double length, double width, double height)
    {
        if (r == null) return 0;
        var volume = 0.0;
        foreach (var bars in new[] { r.Top, r.Bottom })
            if (bars is { Count: > 0, Diameter: > 0 }) volume += bars.Count * BarArea(bars.Diameter) * length;
        if (r.Stirrups is { Diameter: > 0, Spacing: > 0 } s)
        {
            var c = r.Cover ?? 0.025;
            var perimeter = 2 * (Math.Max(0, width - 2 * c) + Math.Max(0, height - 2 * c));
            volume += (Math.Floor(length / s.Spacing + Eps) + 1) * perimeter * BarArea(s.Diameter);
        }
        return volume * SteelKgM3;
    }

    /// <summary>
    /// Where a lintel lies across the wall between <paramref name="yMin"/> and
    /// <paramref name="yMax"/> (the core): its whole width, or the rule's (a
    /// product's times its pieces) at the outer face, centred or at the inner
    /// face; cut in pieces side by side.
    /// </summary>
    public static List<(double From, double To)> Slices(LintelRule? rule, double yMin, double yMax, bool outerAtMin)
    {
        var thick = yMax - yMin;
        var width = Math.Min(thick, rule is { Mode: "precast", Product: { } p } ? p.Width * (rule.Pieces ?? 1) : rule?.Width ?? thick);
        var y0 = yMin + (thick - width) / 2;
        if (rule?.Placement == "outer") y0 = outerAtMin ? yMin : yMax - width;
        if (rule?.Placement == "inner") y0 = outerAtMin ? yMax - width : yMin;
        var n = Math.Max(1, rule?.Pieces ?? 1);
        return Enumerable.Range(0, n).Select(i => (y0 + width * i / n, y0 + width * (i + 1) / n)).ToList();
    }

    static LintelRule? RuleFor(LintelOpening o, LintelRule? wall)
    {
        var rule = o.NoLintel ? null : o.Rule ?? wall;
        return rule is { Mode: not "none" } ? rule : null;
    }

    /// <summary>
    /// The lintels of a wall: over each opening that wants one (its type's
    /// rule, else the wall's), wide enough and not up to the top. Openings at
    /// the same head height under the same rule whose bearings touch share
    /// one. Close under the ring beam, the ring beam comes down instead (a
    /// downstand, cast with it). A precast lintel takes a catalogue length
    /// (or the product's), centred over its bearings. Inside the wall's length.
    /// </summary>
    public static List<Lintel> ForWall(IReadOnlyList<LintelOpening> openings, IReadOnlyList<ZoneBand> bands, LintelRule? wallRule, (double X0, double X1, double Height) wall)
    {
        var ring = bands.FirstOrDefault(b => b.Zone.Role == "ringBeam");
        var wanted = openings
            .Select(o => (O: o, Rule: RuleFor(o, wallRule)))
            .Where(w => w.Rule != null && w.O.X1 - w.O.X0 >= (w.Rule.MinWidth ?? 0) - Eps && w.O.Z1 < wall.Height - Eps)
            .OrderBy(w => w.O.X0)
            .ToList();
        var groups = new List<(LintelRule Rule, List<LintelOpening> Openings)>();
        foreach (var (o, rule) in wanted)
        {
            var last = groups.Count > 0 ? groups[^1] : default;
            var prev = last.Openings?.LastOrDefault();
            // The same rule by content (a type's rule is read anew for each of its openings).
            var same = last.Rule != null && last.Rule.ToJson().ToJsonString() == rule!.ToJson().ToJsonString();
            if (prev != null && same && Math.Abs(prev.Z1 - o.Z1) <= 1e-6 && o.X0 - prev.X1 <= rule!.Bearing + rule.EndBearing + Eps) last.Openings!.Add(o);
            else groups.Add((rule!, [o]));
        }
        var o2 = new List<Lintel>();
        foreach (var (rule, group) in groups)
        {
            var head = group[0].Z1;
            var x0 = group[0].X0;
            var x1 = group.Max(g => g.X1);
            var key = string.Join("+", group.Select(g => g.Key));
            if (ring != null && ring.Z0 - head < rule.LintelHeight - Eps)
            {
                if (ring.Z0 - head > Eps)
                {
                    var (a, b) = Clamp(x0 - rule.Bearing, x1 + rule.EndBearing, wall);
                    o2.Add(new Lintel(key, "downstand", false, x1 - x0 + rule.Bearing + rule.EndBearing, a, b, head, ring.Z0));
                }
                continue;
            }
            var need = x1 - x0 + rule.Bearing + rule.EndBearing;
            double? fromProduct = rule is { Mode: "precast", Product: { } p } ? ProductLength(p, need) : null;
            var length = rule.Mode != "precast" ? need : rule.Product != null ? fromProduct ?? need : PrecastLength(need);
            // Centred over the bearings: a longer stock length overhangs both sides alike.
            var mid = (x0 - rule.Bearing + x1 + rule.EndBearing) / 2;
            var top = Math.Min(head + rule.LintelHeight, wall.Height);
            var (c0, c1) = Clamp(mid - length / 2, mid + length / 2, wall);
            o2.Add(new Lintel(key, "lintel", rule.Mode == "precast", length, c0, c1, head, top, rule, !(rule.Product != null && rule.Mode == "precast" && fromProduct == null)));
        }
        return o2;
    }

    static (double, double) Clamp(double x0, double x1, (double X0, double X1, double Height) wall) => (Math.Max(x0, wall.X0), Math.Min(x1, wall.X1));
}
