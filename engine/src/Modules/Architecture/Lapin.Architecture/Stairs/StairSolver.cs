using Lapin.Geometry;

namespace Lapin.Architecture.Stairs;

/// <summary>One rule checked: the value, its bounds, and whether it holds.</summary>
public sealed record StairCheck(string Key, string Label, double Value, double? Min, double? Max, bool Ok);

/// <summary>A flight: its risers, where its first riser stands (the centre of its line), which way it climbs, the finished floor level it starts from.</summary>
public sealed record Flight(int Risers, Vec2 Start, Vec2 Dir, double Z0)
{
    public Vec2 Left => Dir.Perp;
}

/// <summary>A landing: its outline in plan and its finished top.</summary>
public sealed record Landing(Vec2[] Outline, double Z);

/// <summary>A winder: one turning step, its outline and its finished top; it is riser <see cref="Riser"/> of the stair.</summary>
public sealed record Winder(Vec2[] Outline, double Z, int Riser);

/// <summary>
/// What the instance's inputs fix (the width, the risers, the riser, the
/// tread, the landing), each null for "as the type and the rules say".
/// </summary>
public sealed record StairInputs(double? Width = null, int? Risers = null, double? RiserHeight = null, double? Tread = null, double? Landing = null);

/// <summary>
/// A stair worked out: the risers n, the riser h and the tread b, the flights,
/// landings and winders laid out in the stair's own frame (the foot of the
/// first riser at the origin, the first flight along +X, the turn to +Y when
/// it turns left), and the checks against the rules.
/// </summary>
public sealed record Resolved(
    StairSpec Spec, double Height, int Risers, double RiserHeight, double Tread, double Width, double LandingLength,
    IReadOnlyList<Flight> Flights, IReadOnlyList<Landing> Landings, IReadOnlyList<Winder> Winders, IReadOnlyList<StairCheck> Checks)
{
    public double Step => 2 * RiserHeight + Tread;
    public bool Ok => Checks.All(c => c.Ok);
    /// <summary>The top of the stair: where the last riser lands (the centre of its line) and the direction of the last flight.</summary>
    public (Vec2 At, Vec2 Dir) Top { get { var f = Flights[^1]; return (f.Start + f.Dir * ((f.Risers - 1) * Tread), f.Dir); } }
    /// <summary>The outline of a flight in plan.</summary>
    public Vec2[] FlightOutline(Flight f)
    {
        var run = (f.Risers - 1) * Tread;
        var l = f.Left * (Width / 2);
        return [f.Start - l, f.Start + f.Dir * run - l, f.Start + f.Dir * run + l, f.Start + l];
    }
    /// <summary>Everything the stair covers in plan, as the outlines of its parts.</summary>
    public IEnumerable<Vec2[]> Footprint() => Flights.Select(FlightOutline).Concat(Landings.Select(l => l.Outline)).Concat(Winders.Select(w => w.Outline));
}

/// <summary>
/// The stair's arithmetic: from the height to climb, the type's rules and
/// what the user fixed, the number of risers, their height and the tread,
/// then the flights laid out for the shape.
/// </summary>
public static class StairSolver
{
    public static Resolved Resolve(StairSpec spec, double height, StairInputs? inputs = null, bool turnLeft = true)
    {
        inputs ??= new StairInputs();
        var r = spec.Rules;
        var width = inputs.Width ?? spec.Width;
        var landing = inputs.Landing ?? (spec.LandingLength > 0 ? spec.LandingLength : width);
        height = Math.Max(height, 0.3);

        // The risers: as fixed, else as many as the target riser gives, pushed into the bounds.
        int n;
        if (inputs.Risers is { } fixedN) n = Math.Max(2, fixedN);
        else if (inputs.RiserHeight is { } fixedH && fixedH > 0.01) n = Math.Max(2, (int)Math.Round(height / fixedH));
        else
        {
            n = Math.Max(2, (int)Math.Round(height / r.RiserTarget));
            if (height / n > r.RiserMax + 1e-9) n = (int)Math.Ceiling(height / r.RiserMax);
            if (height / n < r.RiserMin - 1e-9) n = Math.Max(2, (int)Math.Floor(height / r.RiserMin));
        }
        var h = height / n;
        // The tread: as fixed, else from the step rule, to the half centimetre, within the bounds.
        var b = inputs.Tread is { } fixedB && fixedB > 0.05 ? fixedB
            : Math.Clamp(Math.Round(((r.StepMin + r.StepMax) / 2 - 2 * h) / 0.005) * 0.005, r.TreadMin, r.TreadMax);

        // The flights: the risers split by the shape; winders take theirs from the middle.
        var winders = spec.Turns_ && spec.Turn == "winders" ? Math.Min(spec.Winders * (spec.Shape == "u" ? 2 : 1), Math.Max(0, n - 2)) : 0;
        var inFlights = n - winders;
        var split = spec.Shape == "straight" ? [inFlights] : new[] { (int)Math.Ceiling(inFlights / 2.0), (int)Math.Floor(inFlights / 2.0) };
        if (split.Length == 2 && split[1] == 0) split = [inFlights];

        var flights = new List<Flight>();
        var landings = new List<Landing>();
        var winderSteps = new List<Winder>();
        var side = turnLeft ? 1.0 : -1.0;
        var dir = new Vec2(1, 0);
        var left = dir.Perp * side;
        var pos = new Vec2(0, 0);
        var z = 0.0;
        var w = width;

        flights.Add(new Flight(split[0], pos, dir, z));
        z += split[0] * h;
        pos += dir * ((split[0] - 1) * b);
        if (split.Length == 2)
        {
            Vec2 start2, dir2;
            switch (spec.Shape)
            {
                case "straight-landing":
                    landings.Add(new Landing([pos - left * (w / 2), pos + dir * landing - left * (w / 2), pos + dir * landing + left * (w / 2), pos + left * (w / 2)], z));
                    start2 = pos + dir * landing; dir2 = dir;
                    break;
                case "l":
                {
                    if (winders > 0)
                    {
                        var pivot = pos + left * (w / 2);
                        Vec2[] outer = [pos - left * (w / 2), pos + dir * w - left * (w / 2), pos + dir * w + left * (w / 2)];
                        winderSteps.AddRange(Fan(pivot, outer, winders, z, h, split[0]));
                        z += winders * h;
                    }
                    else landings.Add(new Landing([pos - left * (w / 2), pos + dir * w - left * (w / 2), pos + dir * w + left * (w / 2), pos + left * (w / 2)], z));
                    start2 = pos + dir * (w / 2) + left * (w / 2); dir2 = left;
                    break;
                }
                default: // u
                {
                    var across = 2 * w + spec.Gap;
                    if (winders > 0)
                    {
                        var pivot = pos + left * (w / 2 + spec.Gap / 2);
                        Vec2[] outer = [pos - left * (w / 2), pos + dir * w - left * (w / 2), pos + dir * w + left * (across - w / 2), pos + left * (across - w / 2)];
                        winderSteps.AddRange(Fan(pivot, outer, winders, z, h, split[0]));
                        z += winders * h;
                    }
                    else landings.Add(new Landing([pos - left * (w / 2), pos + dir * landing - left * (w / 2), pos + dir * landing + left * (across - w / 2), pos + left * (across - w / 2)], z));
                    start2 = pos + left * (w + spec.Gap); dir2 = -dir;
                    break;
                }
            }
            flights.Add(new Flight(split[1], start2, dir2, z));
            z += split[1] * h;
        }

        var checks = new List<StairCheck>
        {
            new("riser", "Riser", h, r.RiserMin, r.RiserMax, h >= r.RiserMin - 1e-6 && h <= r.RiserMax + 1e-6),
            new("tread", "Tread", b, r.TreadMin, r.TreadMax, b >= r.TreadMin - 1e-6 && b <= r.TreadMax + 1e-6),
            new("step", "2h + b", 2 * h + b, r.StepMin, r.StepMax, 2 * h + b >= r.StepMin - 1e-6 && 2 * h + b <= r.StepMax + 1e-6),
            new("width", "Width", width, r.MinWidth, null, width >= r.MinWidth - 1e-6),
            new("flight", "Risers in a flight", flights.Max(f => f.Risers), 3, r.MaxRisers, flights.All(f => f.Risers >= 3 && f.Risers <= r.MaxRisers)),
        };
        if (landings.Count > 0) checks.Add(new("landing", "Landing", landing, width, null, landing >= width - 1e-6));
        return new Resolved(spec, height, n, h, b, width, landing, flights, landings, winderSteps, checks);
    }

    /// <summary>Winders fanning from a pivot: the outer path split into equal parts, each step the wedge from the pivot to its part (with the corner it passes).</summary>
    static IEnumerable<Winder> Fan(Vec2 pivot, Vec2[] outer, int count, double z, double h, int firstRiser)
    {
        var lengths = new List<double>();
        for (var i = 0; i + 1 < outer.Length; i++) lengths.Add((outer[i + 1] - outer[i]).Length);
        var total = lengths.Sum();
        Vec2 At(double t)
        {
            var d = t * total;
            for (var i = 0; i < lengths.Count; i++)
            {
                if (d <= lengths[i] + 1e-12 || i == lengths.Count - 1) return outer[i] + (outer[i + 1] - outer[i]) * (lengths[i] < 1e-12 ? 0 : Math.Min(1, d / lengths[i]));
                d -= lengths[i];
            }
            return outer[^1];
        }
        List<Vec2> Between(double t0, double t1)
        {
            var pts = new List<Vec2> { At(t0) };
            var acc = 0.0;
            for (var i = 0; i + 1 < outer.Length; i++)
            {
                acc += lengths[i];
                var t = acc / total;
                if (t > t0 + 1e-9 && t < t1 - 1e-9) pts.Add(outer[i + 1]);
            }
            pts.Add(At(t1));
            return pts;
        }
        for (var i = 0; i < count; i++)
        {
            var ring = new List<Vec2> { pivot };
            ring.AddRange(Between((double)i / count, (double)(i + 1) / count));
            yield return new Winder([.. ring], z + (i + 1) * h, firstRiser + i + 1);
        }
    }
}
