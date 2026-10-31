using Lapin.Geometry;

namespace Lapin.Architecture.Stairs;

/// <summary>One rule checked: the value, its bounds, and whether it holds.</summary>
public sealed record StairCheck(string Key, string Label, double Value, double? Min, double? Max, bool Ok);

/// <summary>A flight: its risers, where its first riser stands (the centre of its line), which way it climbs, the finished floor level it starts from.</summary>
public sealed record Flight(int Risers, Vec2 Start, Vec2 Dir, double Z0, double? Width = null)
{
    public Vec2 Left => Dir.Perp;
}

/// <summary>
/// A spiral's or curved stair's run (decision 190): its steps fan round <see cref="Centre"/> between
/// the inner and outer radii, the first riser at <see cref="A0"/>, each step <see cref="Step"/> further
/// round (radians, signed: positive anticlockwise), <see cref="Steps"/> treads in all; the walking line on the centre radius.
/// </summary>
public sealed record ArcRun(Vec2 Centre, double Inner, double Outer, double A0, double Step, int Steps)
{
    public double Walk => (Inner + Outer) / 2;
    public double Angle(int k) => A0 + Step * k;
    public Vec2 At(double angle, double radius) => Centre + new Vec2(Math.Cos(angle), Math.Sin(angle)) * radius;
    /// <summary>The way one climbs at an angle: along the walking line.</summary>
    public Vec2 Tangent(double angle) => new Vec2(-Math.Sin(angle), Math.Cos(angle)) * Math.Sign(Step);
}

/// <summary>A landing: its outline in plan and its finished top.</summary>
public sealed record Landing(Vec2[] Outline, double Z);

/// <summary>A winder: one turning step, its outline and its finished top; it is riser <see cref="Riser"/> of the stair.</summary>
public sealed record Winder(Vec2[] Outline, double Z, int Riser);

/// <summary>
/// What the instance's inputs fix (the width, the risers, the riser, the
/// tread, the landing), each null for "as the type and the rules say".
/// </summary>
public sealed record StairInputs(double? Width = null, int? Risers = null, double? RiserHeight = null, double? Tread = null, double? Landing = null, double? Sweep = null);

/// <summary>
/// A stair worked out: the risers n, the riser h and the tread b, the flights,
/// landings and winders laid out in the stair's own frame (the foot of the
/// first riser at the origin, the first flight along +X, the turn to +Y when
/// it turns left), and the checks against the rules.
/// </summary>
public sealed record Resolved(
    StairSpec Spec, double Height, int Risers, double RiserHeight, double Tread, double Width, double LandingLength,
    IReadOnlyList<Flight> Flights, IReadOnlyList<Landing> Landings, IReadOnlyList<Winder> Winders, IReadOnlyList<StairCheck> Checks, ArcRun? Arc = null)
{
    public double Step => 2 * RiserHeight + Tread;
    public bool Ok => Checks.All(c => c.Ok);
    /// <summary>The top of the stair: where the last riser lands (the centre of its line) and the direction of the last flight.</summary>
    public (Vec2 At, Vec2 Dir) Top
    {
        get
        {
            if (Arc is { } a) { var end = a.Angle(a.Steps); return (a.At(end, a.Walk), a.Tangent(end)); }
            var f = Flights[^1];
            return (f.Start + f.Dir * ((f.Risers - 1) * Tread), f.Dir);
        }
    }
    /// <summary>The foot: the middle of the first riser and the way one sets off.</summary>
    public (Vec2 At, Vec2 Dir, Vec2 Left, double Width) Foot =>
        Arc is { } a ? (a.At(a.A0, a.Walk), a.Tangent(a.A0), a.Tangent(a.A0).Perp, Width) : (Flights[0].Start, Flights[0].Dir, Flights[0].Left, WidthOf(Flights[0]));
    public double WidthOf(Flight f) => f.Width ?? Width;
    /// <summary>The outline of a flight in plan.</summary>
    public Vec2[] FlightOutline(Flight f)
    {
        var run = (f.Risers - 1) * Tread;
        var l = f.Left * (WidthOf(f) / 2);
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
        // A step outside a door may be a single riser of a few centimetres; a stair between storeys has many.
        height = Math.Max(height, 0.05);
        var least = Math.Clamp(r.MinRisers, 1, 2);

        // The risers: as fixed, else as many as the target riser gives, pushed into the bounds.
        int n;
        if (inputs.Risers is { } fixedN) n = Math.Max(1, fixedN);
        else if (inputs.RiserHeight is { } fixedH && fixedH > 0.01) n = Math.Max(1, (int)Math.Round(height / fixedH));
        else
        {
            n = Math.Max(least, (int)Math.Round(height / r.RiserTarget));
            if (height / n > r.RiserMax + 1e-9) n = (int)Math.Ceiling(height / r.RiserMax);
            if (height / n < r.RiserMin - 1e-9) n = Math.Max(least, (int)Math.Floor(height / r.RiserMin));
        }
        var h = height / n;
        // The tread: as fixed, else from the step rule, to the half centimetre, within the bounds.
        var b = inputs.Tread is { } fixedB && fixedB > 0.05 ? fixedB
            : Math.Clamp(Math.Round(((r.StepMin + r.StepMax) / 2 - 2 * h) / 0.005) * 0.005, r.TreadMin, r.TreadMax);

        if (spec.Round) return ResolveRound(spec, height, n, h, b, width, inputs.Sweep ?? spec.Sweep, turnLeft);

        // The flights: the risers split by the shape; winders take theirs from the middle.
        var winders = spec.Turns_ && spec.Turn == "winders" ? Math.Min(spec.Winders * (spec.Shape == "u" ? 2 : 1), Math.Max(0, n - 2)) : 0;
        // The first flight's last riser lands on the first winder and the second flight leaves from the last: the winders add one riser fewer than their count.
        var inFlights = winders > 0 ? n - winders + 1 : n;
        var split = spec.Shape == "straight" ? [inFlights] : new[] { (int)Math.Ceiling(inFlights / 2.0), (int)Math.Floor(inFlights / 2.0) };
        if (spec.Shape == "split" && split.Length == 2 && split[1] > 0) return ResolveSplit(spec, height, n, h, b, width, landing, split, turnLeft);
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
                        z += (winders - 1) * h;
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
                        z += (winders - 1) * h;
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
            new("flight", "Risers in a flight", flights.Max(f => f.Risers), r.MinRisers, r.MaxRisers, flights.All(f => f.Risers >= r.MinRisers && f.Risers <= r.MaxRisers)),
        };
        if (landings.Count > 0) checks.Add(new("landing", "Landing", landing, width, null, landing >= width - 1e-6));
        return new Resolved(spec, height, n, h, b, width, landing, flights, landings, winderSteps, checks);
    }

    static List<StairCheck> Checks(StairRules r, double h, double b, double width, IEnumerable<int> flights)
    {
        var fs = flights.ToList();
        return
        [
            new("riser", "Riser", h, r.RiserMin, r.RiserMax, h >= r.RiserMin - 1e-6 && h <= r.RiserMax + 1e-6),
            new("tread", "Tread", b, r.TreadMin, r.TreadMax, b >= r.TreadMin - 1e-6 && b <= r.TreadMax + 1e-6),
            new("step", "2h + b", 2 * h + b, r.StepMin, r.StepMax, 2 * h + b >= r.StepMin - 1e-6 && 2 * h + b <= r.StepMax + 1e-6),
            new("width", "Width", width, r.MinWidth, null, width >= r.MinWidth - 1e-6),
            new("flight", "Risers in a flight", fs.Max(), r.MinRisers, r.MaxRisers, fs.All(f => f >= r.MinRisers && f <= r.MaxRisers)),
        ];
    }

    /// <summary>
    /// A spiral or curved stair (decision 190): the tread is measured on the walking line (the centre
    /// radius), so each step turns tread ÷ radius; asked to turn a given sweep, the tread follows from it.
    /// The first riser lies across the walking line at the foot (the origin), climbing along +X, the
    /// centre to the side it turns. A spiral is checked for headroom where it passes over itself.
    /// </summary>
    static Resolved ResolveRound(StairSpec spec, double height, int n, double h, double b, double width, double? sweepDeg, bool turnLeft)
    {
        var r = spec.Rules;
        var inner = Math.Max(0.03, spec.InnerRadius);
        var walk = inner + width / 2;
        var steps = Math.Max(1, n - 1);
        if (sweepDeg is { } sw && sw > 0) b = walk * (sw * Math.PI / 180) / steps;
        var side = turnLeft ? 1.0 : -1.0;
        var centre = new Vec2(0, side * walk);
        var a0 = Math.Atan2(-centre.Y, -centre.X);
        var arc = new ArcRun(centre, inner, inner + width, a0, side * b / walk, steps);
        var treads = new List<Winder>();
        for (var k = 0; k < steps; k++)
        {
            // Each tread: the wedge between two risers, its arcs sampled every few degrees.
            var (t0, t1) = (arc.Angle(k), arc.Angle(k + 1));
            var m = Math.Max(2, (int)Math.Ceiling(Math.Abs(t1 - t0) / (Math.PI / 36)));
            var outer = Enumerable.Range(0, m + 1).Select(i => arc.At(t0 + (t1 - t0) * i / m, arc.Outer));
            var innerPts = Enumerable.Range(0, m + 1).Select(i => arc.At(t1 - (t1 - t0) * i / m, arc.Inner));
            treads.Add(new Winder([.. outer, .. innerPts], (k + 1) * h, k + 1));
        }
        var checks = Checks(r, h, b, width, [n]);
        if (spec.Shape == "spiral")
        {
            // Headroom where it comes round over itself: the risers in a full turn, less the tread's thickness.
            var perTurn = 2 * Math.PI / Math.Abs(arc.Step);
            if (steps + 1 > perTurn)
            {
                var clear = perTurn * h - spec.Construction.Waist - spec.Finish.TreadThickness;
                checks.Add(new("headroom", "Headroom", clear, r.Headroom, null, clear >= r.Headroom - 1e-6));
            }
        }
        return new Resolved(spec, height, n, h, b, width, 0, [], [], treads, checks, arc);
    }

    /// <summary>
    /// A split stair (decision 190): the main flight up the middle to a landing as wide as all three,
    /// then two return flights back on either side of it, each with the risers left.
    /// </summary>
    static Resolved ResolveSplit(StairSpec spec, double height, int n, double h, double b, double width, double landing, int[] split, bool turnLeft)
    {
        var r = spec.Rules;
        var side = Math.Max(r.MinWidth, spec.SideWidth ?? Math.Round(width * 0.6 / 0.05) * 0.05);
        var gap = Math.Max(0, spec.Gap);
        var dir = new Vec2(1, 0);
        var left = dir.Perp;
        var first = new Flight(split[0], new Vec2(0, 0), dir, 0);
        var z = split[0] * h;
        var pos = dir * ((split[0] - 1) * b);
        var across = width / 2 + gap + side;
        var deck = new Landing([pos - left * across, pos + dir * landing - left * across, pos + dir * landing + left * across, pos + left * across], z);
        var offset = width / 2 + gap + side / 2;
        // Back down the way it came, beside the main flight; each starts at the landing's near edge.
        var a = new Flight(split[1], pos + left * offset, -dir, z, side);
        var c = new Flight(split[1], pos - left * offset, -dir, z, side);
        var checks = Checks(r, h, b, Math.Min(width, side), split);
        checks.Add(new("landing", "Landing", landing, side, null, landing >= side - 1e-6));
        return new Resolved(spec, height, n, h, b, width, landing, [first, a, c], [deck], [], checks);
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
            // The first winder is what the flight's last riser climbs to; each next one a riser up.
            yield return new Winder([.. ring], z + i * h, firstRiser + i);
        }
    }
}
