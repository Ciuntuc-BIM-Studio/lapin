using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lapin.Alignments;

/// <summary>A break where the stationing jumps: at a distance along, the station arriving (<see cref="Back"/>, the region before it) and the one leaving (<see cref="Ahead"/>).</summary>
public sealed record StationEquation(double At, double Back, double Ahead);

/// <summary>A named point of the stationing (an IfcReferent): a km post, a station point or another mark, located by its station — and, inside an overlap where the value occurs twice, its region.</summary>
public sealed record Referent(string Id, string Name, double Station, int? Region, string Kind)
{
    public static readonly string[] Kinds = ["kmpost", "station", "other"];
}

/// <summary>A run of the stationing: the distance-along range [From, To] and the station at From.</summary>
public sealed record StationRegion(double From, double To, double Start);

/// <summary>
/// The stationing of an alignment: the station (kilometrage) of a distance
/// along, with station equations — the breaks where the stationing jumps, as
/// IfcReferent carries them (Pset_Stationing: Station, IncomingStation) and as
/// every railway keeps them after a realignment. Ahead &gt; back skips stations
/// (a gap: they exist nowhere); ahead &lt; back repeats them (an overlap: a
/// value occurs twice, and the region tells which is meant). Region 0 runs from
/// the start to the first break, region k from break k − 1 to break k; a break
/// belongs to the region after it. Metres throughout.
/// </summary>
public sealed class Stationing
{
    const double Eps = 1e-9;

    public double StartStation { get; }
    public double Length { get; }
    /// <summary>The breaks, sorted, inside (0, length), each Back recomputed from the stationing before it.</summary>
    public IReadOnlyList<StationEquation> Equations { get; }
    public IReadOnlyList<StationRegion> Regions { get; }

    public Stationing(double startStation, double length, IEnumerable<StationEquation>? equations = null)
    {
        StartStation = startStation; Length = length;
        Equations = Normalize(startStation, equations ?? [], length);
        var regions = new List<StationRegion>();
        double from = 0, start = startStation;
        foreach (var e in Equations) { regions.Add(new(from, e.At, start)); from = e.At; start = e.Ahead; }
        regions.Add(new(from, length, start));
        Regions = regions;
    }

    /// <summary>The breaks sorted by distance, those outside (0, length) dropped, duplicates merged (the later wins), Back recomputed.</summary>
    public static List<StationEquation> Normalize(double startStation, IEnumerable<StationEquation> equations, double length = double.PositiveInfinity)
    {
        var sorted = equations.Where(e => double.IsFinite(e.At) && double.IsFinite(e.Ahead) && e.At > Eps && e.At < length - Eps).Select((e, i) => (e, i)).OrderBy(x => x.e.At).ThenBy(x => x.i).Select(x => x.e).ToList();
        var o = new List<StationEquation>();
        foreach (var e in sorted)
        {
            if (o.Count > 0 && Math.Abs(o[^1].At - e.At) < 1e-6) o.RemoveAt(o.Count - 1);
            o.Add(e);
        }
        double from = 0, start = startStation;
        for (var i = 0; i < o.Count; i++)
        {
            o[i] = o[i] with { Back = start + (o[i].At - from) };
            from = o[i].At; start = o[i].Ahead;
        }
        return o;
    }

    int RegionOf(double d)
    {
        var k = 0;
        while (k + 1 < Regions.Count && d >= Regions[k + 1].From - Eps) k++;
        return k;
    }

    /// <summary>The station of a distance along, and the region it falls in.</summary>
    public (double Station, int Region) StationOf(double d)
    {
        var k = RegionOf(d);
        return (Regions[k].Start + (d - Regions[k].From), k);
    }

    double? InRegion(double station, int k)
    {
        if (k < 0 || k >= Regions.Count) return null;
        var r = Regions[k];
        var d = r.From + (station - r.Start);
        // A region is [from, to); the last one, and a lone station at a break's back side, reach `to`.
        var last = k == Regions.Count - 1;
        if (d < r.From - 1e-7 || d > r.To + 1e-7) return null;
        if (!last && d > r.To - 1e-7 && Math.Abs(Regions[k + 1].Start - station) < 1e-7) return null;
        return Math.Clamp(d, r.From, r.To);
    }

    /// <summary>The distance along of a station: in the region given, else the first region that has it; null when none has it (a gap, or off the ends).</summary>
    public double? DistanceOf(double station, int? region = null)
    {
        if (region is { } k) return InRegion(station, k);
        for (var i = 0; i < Regions.Count; i++) if (InRegion(station, i) is { } d) return d;
        return null;
    }

    /// <summary>Every place a station value occurs — two or more inside an overlap.</summary>
    public List<(double DistanceAlong, int Region)> Occurrences(double station) =>
        Regions.Select((_, k) => (D: InRegion(station, k), k)).Where(x => x.D != null).Select(x => (x.D!.Value, x.k)).ToList();

    /// <summary>
    /// Referents every <paramref name="every"/> metres of station in every
    /// region — the round values a post is set at, not every so many metres
    /// of distance: after a gap the posts go on from the next round value.
    /// Ids are deterministic (prefix, region, station).
    /// </summary>
    public List<Referent> Generate(double every, string? kind = null, string prefix = "ref")
    {
        var o = new List<Referent>();
        if (!(every > 0)) return o;
        kind ??= every >= 1000 ? "kmpost" : "station";
        for (var k = 0; k < Regions.Count; k++)
        {
            var r = Regions[k];
            var last = k == Regions.Count - 1;
            var end = r.Start + (r.To - r.From);
            for (var n = Math.Ceiling((r.Start - 1e-6) / every); ; n++)
            {
                var station = Math.Round(n * every * 1e6) / 1e6;
                // The region's end belongs to the next region when the stationing runs on through it.
                if (station > end + 1e-6 || (!last && station > end - 1e-6 && Math.Abs(Regions[k + 1].Start - station) < 1e-6)) break;
                o.Add(new($"{prefix}_{k}_{station.ToString(CultureInfo.InvariantCulture)}", ReferentName(station), station, Occurrences(station).Count > 1 ? k : null, kind));
            }
        }
        return o;
    }

    /// <summary>The name a generated referent gets: "km 12" on a whole kilometre, "12+100" otherwise.</summary>
    public static string ReferentName(double station)
    {
        // + 0.0: a station a hair under zero rounds to −0, which would read "km -0".
        var r = Math.Round(station * 1000) / 1000 + 0.0;
        return r % 1000 == 0 ? $"km {(r / 1000).ToString(CultureInfo.InvariantCulture)}" : FormatKm(r, r == Math.Floor(r) ? 0 : 3);
    }

    /// <summary>A station as a kilometrage: 12345.6 → "12+345,600" (km + metres on three digits); rounded first, so 999.9996 reads "1+000,000". Negative stations keep their sign in front.</summary>
    public static string FormatKm(double station, int decimals = 3, char sep = ',')
    {
        if (!double.IsFinite(station)) return "—";
        var f = Math.Pow(10, decimals);
        var units = (long)Math.Round(Math.Abs(station) * f);
        var neg = station < 0 && units > 0;
        var km = units / (long)(1000 * f);
        var rest = units - km * (long)(1000 * f);
        var whole = rest / (long)f; var frac = rest - whole * (long)f;
        var m = whole.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') + (decimals > 0 ? sep + frac.ToString(CultureInfo.InvariantCulture).PadLeft(decimals, '0') : "");
        return $"{(neg ? "-" : "")}{km}+{m}";
    }

    static readonly Regex KmPattern = new(@"^(-?)(\d+)\+(\d+(?:[.,]\d*)?)$", RegexOptions.Compiled);
    static readonly Regex PlainPattern = new(@"^-?\d+(?:[.,]\d*)?$", RegexOptions.Compiled);

    /// <summary>What a person types for a station → metres: "12+345,6", "km 12+345.600", "12345,6", "-0+012,5"; null when it is none of these.</summary>
    public static double? ParseKm(string text)
    {
        var t = Regex.Replace(text.Trim(), @"^km\s*", "", RegexOptions.IgnoreCase).Replace(" ", "");
        if (t.Length == 0) return null;
        if (KmPattern.Match(t) is { Success: true } m)
        {
            if (!double.TryParse(m.Groups[3].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var metres) || metres >= 1000) return null;
            var v = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 1000 + metres;
            return m.Groups[1].Value.Length > 0 ? -v : v;
        }
        if (!PlainPattern.IsMatch(t)) return null;
        return double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    // ─── JSON, as a track keeps them ─────────────────────────────────────────

    /// <summary>Equations from JSON: rows [at, back, ahead] or objects {at, back, ahead}; Back may be left out (it is recomputed).</summary>
    public static List<StationEquation> ReadEquations(JsonNode? n) => n is JsonArray a
        ? a.Select(r => r switch
        {
            JsonArray v when v.Count >= 2 => new StationEquation(Json.Number(v[0]) ?? double.NaN, v.Count > 2 ? Json.Number(v[1]) ?? 0 : 0, Json.Number(v[^1]) ?? double.NaN),
            JsonObject o => new StationEquation(Json.Number(o["at"]) ?? double.NaN, Json.Number(o["back"]) ?? 0, Json.Number(o["ahead"]) ?? double.NaN),
            _ => null,
        }).OfType<StationEquation>().Where(e => double.IsFinite(e.At) && double.IsFinite(e.Ahead)).ToList()
        : [];

    public static JsonArray WriteEquations(IEnumerable<StationEquation> rows) => new(rows.Select(e => (JsonNode)new JsonArray(e.At, e.Back, e.Ahead)).ToArray());

    public static List<Referent> ReadReferents(JsonNode? n) => n is JsonArray a
        ? a.OfType<JsonObject>().Select((o, i) => new Referent(
            (string?)o["id"] is { Length: > 0 } id ? id : $"r{i + 1}",
            (string?)o["name"] is { Length: > 0 } nm ? nm : ReferentName(Json.Number(o["station"]) ?? 0),
            Json.Number(o["station"]) ?? double.NaN,
            Json.Number(o["region"]) is { } rg ? (int)rg : null,
            Referent.Kinds.Contains((string?)o["kind"]) ? (string)o["kind"]! : "other"))
            .Where(r => double.IsFinite(r.Station)).ToList()
        : [];

    public static JsonArray WriteReferents(IEnumerable<Referent> rows) => new(rows.Select(r =>
    {
        var o = new JsonObject { ["id"] = r.Id, ["name"] = r.Name, ["station"] = r.Station, ["kind"] = r.Kind };
        if (r.Region is { } k) o["region"] = k;
        return (JsonNode)o;
    }).ToArray());
}
