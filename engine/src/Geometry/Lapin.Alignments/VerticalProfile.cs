namespace Lapin.Alignments;

/// <summary>A PVI (point of vertical intersection): station, elevation, and the parabolic curve's length (0: a grade break).</summary>
public sealed record Pvi(double Station, double Elevation, double Length = 0);

/// <summary>
/// The longitudinal profile (the red line) in station / elevation: PVIs
/// joined by constant grades, each inner one rounded by a symmetric parabola
/// of length L spanning L/2 either side. Ported from BubbleBIM Pro's Profile.
/// </summary>
public sealed class VerticalProfile
{
    readonly Pvi[] _p;
    readonly double[] _grades;

    public IReadOnlyList<Pvi> Pvis => _p;
    public double StartStation => _p[0].Station;
    public double EndStation => _p[^1].Station;

    VerticalProfile(Pvi[] p)
    {
        _p = p;
        _grades = p.Skip(1).Select((q, i) => (q.Elevation - p[i].Elevation) / (q.Station - p[i].Station)).ToArray();
    }

    /// <summary>Why the PVIs make no profile, or null.</summary>
    public static string? Problem(IReadOnlyList<Pvi> pvis)
    {
        var p = pvis.OrderBy(v => v.Station).ToArray();
        if (p.Length < 2) return "A profile needs two points (PVIs) at least.";
        for (var i = 1; i < p.Length; i++)
        {
            if (p[i].Station - p[i - 1].Station < 1e-6) return $"PVIs {i} and {i + 1} are at the same station.";
            var l0 = i == 1 ? 0 : p[i - 1].Length / 2;
            var l1 = i == p.Length - 1 ? 0 : p[i].Length / 2;
            if (l0 + l1 > p[i].Station - p[i - 1].Station + 1e-9) return $"The vertical curves at PVIs {i} and {i + 1} overlap: shorten them.";
        }
        return null;
    }

    public static VerticalProfile Build(IReadOnlyList<Pvi> pvis) =>
        Problem(pvis) is { } why ? throw new InvalidOperationException(why) : new VerticalProfile(pvis.OrderBy(v => v.Station).ToArray());

    public double ElevationAt(double station)
    {
        var s = Math.Clamp(station, StartStation, EndStation);
        for (var i = 1; i < _p.Length - 1; i++)
        {
            var len = _p[i].Length;
            var bvc = _p[i].Station - len / 2;
            if (len > 0 && s >= bvc && s <= _p[i].Station + len / 2)
            {
                var x = s - bvc;
                var zBvc = _p[i].Elevation - _grades[i - 1] * len / 2;
                return zBvc + _grades[i - 1] * x + (_grades[i] - _grades[i - 1]) * x * x / (2 * len);
            }
        }
        for (var i = 1; i < _p.Length; i++)
            if (s <= _p[i].Station) return _p[i - 1].Elevation + _grades[i - 1] * (s - _p[i - 1].Station);
        return _p[^1].Elevation;
    }

    /// <summary>The grade (ratio) at a station; on a vertical curve it changes linearly.</summary>
    public double GradeAt(double station)
    {
        if (station < StartStation || station > EndStation) return 0;
        for (var i = 1; i < _p.Length - 1; i++)
        {
            var len = _p[i].Length;
            if (len > 0 && station >= _p[i].Station - len / 2 && station <= _p[i].Station + len / 2)
                return _grades[i - 1] + (_grades[i] - _grades[i - 1]) * (station - (_p[i].Station - len / 2)) / len;
        }
        for (var i = 1; i < _p.Length; i++) if (station < _p[i].Station) return _grades[i - 1];
        return _grades[^1];
    }

    /// <summary>
    /// A profile that follows the ground: sampled every <paramref name="every"/> m,
    /// simplified (Douglas–Peucker, <paramref name="tolerance"/> m) to the PVIs
    /// that matter, each rounded by a curve as long as its neighbours allow.
    /// </summary>
    public static List<Pvi> FromGround(double from, double to, Func<double, double?> groundAt, double fallback, double every = 5, double tolerance = 0.1)
    {
        var samples = new List<(double S, double Z)>();
        for (var s = from; s < to - 1e-9; s += every) if (groundAt(s) is { } z) samples.Add((s, z));
        if (groundAt(to) is { } ze) samples.Add((to, ze));
        if (samples.Count < 2) return [new(from, fallback), new(to, fallback)];
        var pvis = Simplify(samples, tolerance).Select(k => new Pvi(Math.Round(k.S * 1000) / 1000, Math.Round(k.Z * 1000) / 1000)).ToList();
        for (var i = 1; i < pvis.Count - 1; i++)
        {
            var room = Math.Min(pvis[i].Station - pvis[i - 1].Station, pvis[i + 1].Station - pvis[i].Station);
            pvis[i] = pvis[i] with { Length = Math.Round(room * 0.8 * 100) / 100 };
        }
        return pvis;
    }

    static List<(double S, double Z)> Simplify(List<(double S, double Z)> pts, double tol)
    {
        if (pts.Count <= 2) return pts;
        var (a, b) = (pts[0], pts[^1]);
        double worst = 0; var at = 0;
        for (var i = 1; i < pts.Count - 1; i++)
        {
            var t = (pts[i].S - a.S) / (b.S - a.S);
            var d = Math.Abs(pts[i].Z - (a.Z + (b.Z - a.Z) * t));
            if (d > worst) { worst = d; at = i; }
        }
        if (worst <= tol) return [a, b];
        return [.. Simplify(pts[..(at + 1)], tol).SkipLast(1), .. Simplify(pts[at..], tol)];
    }
}
