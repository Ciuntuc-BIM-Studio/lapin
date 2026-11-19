using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>One line or arc of a stair's sketch: from A to B, rising <see cref="Rise"/> off its chord at its middle (left of A → B when positive; 0 straight).</summary>
public readonly record struct SketchPiece(Vec2 A, Vec2 B, double Rise)
{
    public SketchPiece Reversed => new(B, A, -Rise);
}

/// <summary>
/// A stair drawn by sketch, as Revit's "stair by sketch" (users asked for it, 19.11.2026): the two sides of the stair
/// (<see cref="Boundary"/>, lines and arcs end to end), the risers (<see cref="Riser"/>, each a line, an arc or a polyline from one
/// side to the other; none: spread evenly, as the rules ask), and optionally the stair's path (<see cref="Path"/>), which says only
/// which way it climbs: from the end of the sketch nearer the path's start. <see cref="Flip"/> turns the way it climbs round.
/// Kept on the stair (<c>sketch</c>) in the stair's own frame (its foot at the origin, climbing along +X), so moving and
/// turning the stair carry it; design intent that goes to IFC with its other attributes.
/// </summary>
public sealed record StairSketchData(IReadOnlyList<SketchPiece> Boundary, IReadOnlyList<SketchPiece> Riser, IReadOnlyList<SketchPiece> Path, bool Flip = false)
{
    public const string Key = "sketch";

    /// <summary>The sketch as stored or sent: <c>{boundary, riser, path?, flip?}</c>, each a list of pieces <c>[ax, ay, bx, by, rise?]</c>; null when it is none.</summary>
    public static StairSketchData? From(JsonNode? node)
    {
        if (node is not JsonObject o || o["boundary"] is not JsonArray) return null;
        return new(Pieces(o["boundary"]), Pieces(o["riser"]), Pieces(o["path"]), o["flip"] is JsonValue f && f.TryGetValue(out bool b) && b);
    }

    public static List<SketchPiece> Pieces(JsonNode? node)
    {
        var o = new List<SketchPiece>();
        foreach (var item in node as JsonArray ?? [])
        {
            if (item is not JsonArray a || a.Count < 4) throw new CommandException("A sketch's line is [ax, ay, bx, by] or [ax, ay, bx, by, rise].");
            double N(int i) => Json.Number(a[i]) ?? throw new CommandException("A sketch's line has a coordinate that is not a number.");
            var p = new SketchPiece(new Vec2(N(0), N(1)), new Vec2(N(2), N(3)), a.Count > 4 ? Json.Number(a[4]) ?? 0 : 0);
            if (!double.IsFinite(p.A.X + p.A.Y + p.B.X + p.B.Y + p.Rise)) throw new CommandException("A sketch's line has a coordinate that is not a number.");
            o.Add(p);
        }
        return o;
    }

    static JsonArray ToJson(IEnumerable<SketchPiece> pieces) => new([.. pieces.Select(p => (JsonNode)new JsonArray(R(p.A.X), R(p.A.Y), R(p.B.X), R(p.B.Y), R(p.Rise)))]);
    static double R(double v) => Math.Round(v, 6);

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["boundary"] = ToJson(Boundary), ["riser"] = ToJson(Riser) };
        if (Path.Count > 0) o["path"] = ToJson(Path);
        if (Flip) o["flip"] = true;
        return o;
    }
}

/// <summary>
/// A sketched stair worked out (<see cref="StairSketch"/>): each riser's line from the left side to the right as one climbs, the
/// two sides between the first riser and the last (with the index of each riser's end on them), the walking line through the
/// risers' middles, and which treads are landings.
/// </summary>
public sealed record SketchRun(IReadOnlyList<Vec2[]> Risers, Vec2[] Left, Vec2[] Right, int[] LeftAt, int[] RightAt, Vec2[] Walk, bool[] Landing)
{
    /// <summary>The walking line's length, from the first riser to the last.</summary>
    public double Length => Enumerable.Range(0, Walk.Length - 1).Sum(i => (Walk[i + 1] - Walk[i]).Length);

    /// <summary>Riser k's middle and the way one climbs over it (square to it, towards the next riser; the last towards its own tread).</summary>
    public (Vec2 At, Vec2 Dir) Across(int k)
    {
        var line = Risers[k];
        var u = (line[^1] - line[0]).Normalized();
        var along = k + 1 < Walk.Length ? Walk[k + 1] - Walk[k] : Walk[k] - Walk[k - 1];
        var dir = u.Perp * -1;
        if (Vec2.Dot(dir, along) < 0) dir = dir * -1;
        return (Walk[k], dir);
    }
}

/// <summary>
/// The stair by sketch (users asked for Revit's, 19.11.2026): from the sketch's two sides and its risers, the treads, each the
/// shape between two risers and the two sides, at its height (n riser lines: n risers of H / n; n − 1 treads, the last riser
/// landing on the floor above); a tread much deeper than a step is a landing. Without risers drawn, the rules' risers are
/// spread evenly along the line midway between the sides. The result is a <see cref="Resolved"/> stair like any other, its
/// treads as winders (each its own outline and height), so the body, the plan's symbol cut at the view's cut plane, the
/// railings, the opening above, the quantities and IFC are the ones every stair has.
/// </summary>
public static class StairSketch
{
    /// <summary>How near two ends are to be one (m): drawn with snaps they meet exactly; drawn by eye, within a millimetre.</summary>
    const double Tol = 1e-3;

    /// <summary>How far a riser drawn short of a side still reaches it (m).</summary>
    const double Reach = 0.05;

    /// <summary>A tread at least this deep (m), and more than one and a half of the rules' deepest tread, is a landing.</summary>
    const double LandingMin = 0.5;

    /// <summary>The stair the sketch makes, climbing <paramref name="height"/>; a <see cref="CommandException"/> saying what is wrong with the sketch when it makes none.</summary>
    public static Resolved Resolve(StairSpec spec, double height, StairInputs? inputs, StairSketchData sketch)
    {
        height = Math.Max(height, 0.05);
        var rules = spec.Rules;
        var sides = Chains(sketch.Boundary).ToList();
        if (sides.Any(c => c.Closed)) throw new CommandException("The stair's boundary is a closed outline: draw its two sides apart, the first riser and the last closing it.");
        if (sides.Count != 2) throw new CommandException(sides.Count < 2
            ? "Draw the stair's two sides as boundary lines (lines or arcs end to end), one on each side."
            : $"The boundary holds {sides.Count} separate lines: the stair has two sides, each one line or several end to end.");
        var a = Poly.Of(sides[0].Pieces);
        var b = Poly.Of(sides[1].Pieces);
        if (a.Length < Tol || b.Length < Tol) throw new CommandException("A side of the stair has no length.");

        // The path says only which way the stair climbs: from the end its start is nearer.
        Vec2? pathStart = sketch.Path.Count > 0 ? Chains(sketch.Path).OrderByDescending(c => c.Pieces.Sum(p => (p.B - p.A).Length)).First().Pieces[0].A : null;

        List<(double SA, double SB, Vec2[] Line)> risers;
        if (sketch.Riser.Count == 0)
        {
            // Both sides from the same end, so the risers go across.
            if ((a.At(0) - b.At(0)).Length + (a.End - b.End).Length > (a.At(0) - b.End).Length + (a.End - b.At(0)).Length) b = b.Reversed();
            var flip = sketch.Flip;
            if (pathStart is { } ps && (ps - Mid(a.At(0), b.At(0))).Length > (ps - Mid(a.End, b.End)).Length) flip = !flip;
            if (flip) { a = a.Reversed(); b = b.Reversed(); }
            var n = inputs?.Risers is { } fixedN ? Math.Clamp(fixedN, 2, StairSolver.MaxRisers)
                : Math.Max(2, StairSolver.Resolve(spec with { Shape = "straight" }, height, inputs).Risers);
            risers = Even(a, b, n);
        }
        else
        {
            risers = [];
            var k = 0;
            foreach (var (pieces, closed) in Chains(sketch.Riser))
            {
                k++;
                if (closed) throw new CommandException($"Riser {k} is a closed outline: a riser runs from one side of the stair to the other.");
                risers.Add(Across(a, b, Poly.Of(pieces), k));
            }
            if (risers.Count < 2) throw new CommandException("A stair needs two risers at least: its first and its last.");
            risers.Sort((x, y) => x.SA.CompareTo(y.SA));
            for (var i = 1; i < risers.Count; i++)
                if (risers[i].SA - risers[i - 1].SA < Tol) throw new CommandException("Two risers meet a side at the same point: keep one.");
            // The other side runs the same way, its risers in the same order, else they cross.
            var up = risers[^1].SB > risers[0].SB;
            for (var i = 1; i < risers.Count; i++)
                if (up != (risers[i].SB > risers[i - 1].SB) || Math.Abs(risers[i].SB - risers[i - 1].SB) < Tol)
                    throw new CommandException("Two risers cross each other: each riser goes from one side to the other without crossing another.");
            if (!up) { var lb = b.Length; b = b.Reversed(); risers = [.. risers.Select(r => (r.SA, lb - r.SB, r.Line))]; }
            var flip = sketch.Flip;
            if (pathStart is { } ps && (ps - Middle(risers[0].Line)).Length > (ps - Middle(risers[^1].Line)).Length) flip = !flip;
            if (flip)
            {
                var (la, lb) = (a.Length, b.Length);
                a = a.Reversed(); b = b.Reversed();
                risers.Reverse();
                risers = [.. risers.Select(r => (la - r.SA, lb - r.SB, r.Line))];
            }
        }

        var walk = risers.Select(r => Middle(r.Line)).ToArray();
        // Left as one climbs: the side a's riser ends lie on, or the other.
        var climb = walk[1] - walk[0];
        var aLeft = Vec2.Dot(risers[0].Line[0] - walk[0], climb.Perp) > 0;
        var lines = risers.Select(r => aLeft ? r.Line : [.. r.Line.Reverse()]).ToList();
        var (left, leftAt) = Side(aLeft ? a : b, [.. risers.Select(r => aLeft ? r.SA : r.SB)]);
        var (right, rightAt) = Side(aLeft ? b : a, [.. risers.Select(r => aLeft ? r.SB : r.SA)]);

        var count = risers.Count;
        var h = height / count;
        var treads = new List<(Vec2[] Outline, double Depth)>();
        for (var k = 0; k + 1 < count; k++)
        {
            var ring = new List<Vec2>();
            void Add(Vec2 p) { if (ring.Count == 0 || (ring[^1] - p).Length > 1e-7) ring.Add(p); }
            foreach (var p in lines[k]) Add(p);
            for (var i = rightAt[k] + 1; i < rightAt[k + 1]; i++) Add(right[i]);
            foreach (var p in lines[k + 1].Reverse()) Add(p);
            for (var i = leftAt[k + 1] - 1; i > leftAt[k]; i--) Add(left[i]);
            if (ring.Count > 2 && (ring[^1] - ring[0]).Length <= 1e-7) ring.RemoveAt(ring.Count - 1);
            if (ring.Count < 3 || Math.Abs(Polygon2.SignedArea(ring)) < 1e-6) throw new CommandException($"The tread between risers {k + 1} and {k + 2} has no area: keep the risers apart.");
            if (Polygon2.SignedArea(ring) < 0) ring.Reverse();
            treads.Add(([.. ring], (walk[k + 1] - walk[k]).Length));
        }

        var landingFrom = Math.Max(LandingMin, 1.5 * rules.TreadMax);
        var isLanding = treads.Select(t => t.Depth >= landingFrom).ToArray();
        var winders = new List<Winder>();
        var landings = new List<Landing>();
        for (var k = 0; k < treads.Count; k++)
            if (isLanding[k]) landings.Add(new Landing(treads[k].Outline, (k + 1) * h));
            else winders.Add(new Winder(treads[k].Outline, (k + 1) * h, k + 1));
        var steps = treads.Where((_, k) => !isLanding[k]).Select(t => t.Depth).ToList();
        var tread = steps.Count > 0 ? steps.Average() : Math.Clamp(((rules.StepMin + rules.StepMax) / 2 - 2 * h), rules.TreadMin, rules.TreadMax);
        var widths = lines.Select(l => Enumerable.Range(0, l.Length - 1).Sum(i => (l[i + 1] - l[i]).Length)).ToList();
        var width = widths.Min();
        var landingLength = landings.Count > 0 ? treads.Where((_, k) => isLanding[k]).Min(t => t.Depth) : width;
        // The risers in each run of steps between landings.
        var runs = new List<int>();
        var run = 1;
        for (var k = 0; k < treads.Count; k++)
            if (isLanding[k]) { runs.Add(run); run = 1; }
            else run++;
        runs.Add(run);

        var checks = new List<StairCheck>
        {
            new("riser", "Riser", h, rules.RiserMin, rules.RiserMax, h >= rules.RiserMin - 1e-6 && h <= rules.RiserMax + 1e-6),
            new("tread", "Tread", tread, rules.TreadMin, rules.TreadMax, steps.All(d => d >= rules.TreadMin - 1e-6 && d <= rules.TreadMax + 1e-6)),
            new("step", "2h + b", 2 * h + tread, rules.StepMin, rules.StepMax, 2 * h + tread >= rules.StepMin - 1e-6 && 2 * h + tread <= rules.StepMax + 1e-6),
            new("width", "Width", width, rules.MinWidth, null, width >= rules.MinWidth - 1e-6),
            new("flight", "Risers in a flight", runs.Max(), rules.MinRisers, rules.MaxRisers, runs.All(x => x >= rules.MinRisers && x <= rules.MaxRisers)),
        };
        if (landings.Count > 0) checks.Add(new("landing", "Landing", landingLength, width, null, landingLength >= width - 1e-6));
        var sketchRun = new SketchRun(lines, left, right, leftAt, rightAt, walk, isLanding);
        return new Resolved(spec with { Shape = "sketch" }, height, count, h, tread, widths.Average(), landingLength, [], landings, winders, checks, Sketch: sketchRun);
    }

    /// <summary>Where a sketch puts the stair's foot (the middle of its first riser) and the way it climbs there, in the sketch's own coordinates.</summary>
    public static (Vec2 At, Vec2 Dir) Foot(StairSpec spec, double height, StairInputs? inputs, StairSketchData sketch)
    {
        var r = Resolve(spec, height, inputs, sketch);
        return r.Sketch!.Across(0);
    }

    /// <summary>The sketch in the stair's own frame: its foot at the origin, climbing along +X.</summary>
    public static StairSketchData ToLocal(StairSketchData world, Vec2 at, Vec2 dir) =>
        Map(world, p => new Vec2(Vec2.Dot(p - at, dir), Vec2.Dot(p - at, dir.Perp)));

    /// <summary>The sketch in plan, from the stair's own frame.</summary>
    public static StairSketchData ToWorld(StairSketchData local, StairPlacement place) => Map(local, place.World);

    /// <summary>The sketch moved by a rigid motion (a move and a turn: the rises stay).</summary>
    public static StairSketchData Map(StairSketchData s, Func<Vec2, Vec2> f)
    {
        SketchPiece M(SketchPiece p) => new(f(p.A), f(p.B), p.Rise);
        return new([.. s.Boundary.Select(M)], [.. s.Riser.Select(M)], [.. s.Path.Select(M)], s.Flip);
    }

    static Vec2 Mid(Vec2 p, Vec2 q) => (p + q) * 0.5;

    /// <summary>A line's middle along it.</summary>
    static Vec2 Middle(Vec2[] line) => new Poly([.. line]).At(new Poly([.. line]).Length / 2);

    /// <summary><paramref name="n"/> risers spread evenly along the line midway between the two sides (both running from the foot).</summary>
    static List<(double SA, double SB, Vec2[] Line)> Even(Poly a, Poly b, int n)
    {
        const int m = 400;
        var centre = new List<Vec2>();
        for (var i = 0; i <= m; i++) centre.Add(Mid(a.At(a.Length * i / m), b.At(b.Length * i / m)));
        var c = new Poly(centre, keepAll: true);
        var o = new List<(double, double, Vec2[])>();
        for (var j = 0; j < n; j++)
        {
            // The fraction of the sides at that length along the centre line.
            var f = c.Fraction(c.Length * j / (n - 1));
            var (sa, sb) = (a.Length * f, b.Length * f);
            o.Add((sa, sb, [a.At(sa), b.At(sb)]));
        }
        return o;
    }

    /// <summary>A riser drawn across the stair: where it meets each side, and its line between them from side a to side b.</summary>
    static (double SA, double SB, Vec2[] Line) Across(Poly a, Poly b, Poly riser, int k)
    {
        var r = riser.Extended(Reach);
        var ha = a.Hits(r).OrderBy(x => x.T).ToList();
        var hb = b.Hits(r).OrderBy(x => x.T).ToList();
        if (ha.Count == 0 || hb.Count == 0) throw new CommandException($"Riser {k} does not reach both sides of the stair: draw it from one boundary to the other.");
        // The nearest pair along the riser: the part of it across the stair.
        var (x, y) = ha.SelectMany(p => hb.Select(q => (p, q))).MinBy(t => Math.Abs(t.p.T - t.q.T));
        var line = x.T <= y.T ? r.Slice(x.T, y.T) : [.. r.Slice(y.T, x.T).AsEnumerable().Reverse()];
        line[0] = x.P;
        line[^1] = y.P;
        return (x.S, y.S, line);
    }

    /// <summary>A side between the first riser and the last, with the index of each riser's end on it.</summary>
    static (Vec2[] Points, int[] At) Side(Poly side, double[] at)
    {
        var pts = new List<Vec2> { side.At(at[0]) };
        var idx = new int[at.Length];
        for (var k = 0; k + 1 < at.Length; k++)
        {
            pts.AddRange(side.Between(at[k], at[k + 1]));
            idx[k + 1] = pts.Count;
            pts.Add(side.At(at[k + 1]));
        }
        return ([.. pts], idx);
    }

    /// <summary>Pieces joined end to end into lines (each open, or closed on itself).</summary>
    public static IEnumerable<(List<SketchPiece> Pieces, bool Closed)> Chains(IEnumerable<SketchPiece> all)
    {
        static bool Same(Vec2 p, Vec2 q) => (p - q).Length <= Tol;
        var left = all.Where(p => (p.B - p.A).Length > Tol).ToList();
        while (left.Count > 0)
        {
            var chain = new List<SketchPiece> { left[0] };
            left.RemoveAt(0);
            for (var grown = true; grown;)
            {
                grown = false;
                for (var i = 0; i < left.Count; i++)
                {
                    var p = left[i];
                    if (Same(p.A, chain[^1].B)) chain.Add(p);
                    else if (Same(p.B, chain[^1].B)) chain.Add(p.Reversed);
                    else if (Same(p.B, chain[0].A)) chain.Insert(0, p);
                    else if (Same(p.A, chain[0].A)) chain.Insert(0, p.Reversed);
                    else continue;
                    left.RemoveAt(i);
                    grown = true;
                    break;
                }
            }
            yield return (chain, chain.Count > 1 && Same(chain[^1].B, chain[0].A));
        }
    }

    /// <summary>A line in chords (its arcs by the one rule, <see cref="Chords"/>), measured along.</summary>
    sealed class Poly
    {
        public readonly List<Vec2> P;
        public readonly double[] S;

        public Poly(List<Vec2> pts, bool keepAll = false)
        {
            P = [];
            foreach (var p in pts) if (keepAll || P.Count == 0 || (P[^1] - p).Length > 1e-9) P.Add(p);
            if (P.Count == 1) P.Add(P[0]);
            S = new double[P.Count];
            for (var i = 1; i < P.Count; i++) S[i] = S[i - 1] + (P[i] - P[i - 1]).Length;
        }

        public static Poly Of(List<SketchPiece> pieces)
        {
            var pts = new List<Vec2> { pieces[0].A };
            pts.AddRange(pieces.Select(p => p.B));
            return new Poly(Railings.Chorded(pts, [.. pieces.Select(p => p.Rise)]).Points);
        }

        public double Length => S[^1];
        public Vec2 End => P[^1];

        public Vec2 At(double s)
        {
            s = Math.Clamp(s, 0, Length);
            var i = Seg(s);
            var d = S[i + 1] - S[i];
            return d < 1e-12 ? P[i] : P[i] + (P[i + 1] - P[i]) * ((s - S[i]) / d);
        }

        int Seg(double s)
        {
            var i = Array.BinarySearch(S, s);
            if (i < 0) i = ~i - 1;
            return Math.Clamp(i, 0, P.Count - 2);
        }

        /// <summary>The fraction of the vertices' count at a length along (for a line sampled evenly, the fraction of each side).</summary>
        public double Fraction(double s)
        {
            s = Math.Clamp(s, 0, Length);
            var i = Seg(s);
            var d = S[i + 1] - S[i];
            return (i + (d < 1e-12 ? 0 : (s - S[i]) / d)) / (P.Count - 1);
        }

        public Poly Reversed() { var r = new List<Vec2>(P); r.Reverse(); return new Poly(r); }

        /// <summary>The vertices strictly between two lengths along.</summary>
        public IEnumerable<Vec2> Between(double s0, double s1)
        {
            for (var i = 0; i < P.Count; i++) if (S[i] > s0 + 1e-9 && S[i] < s1 - 1e-9) yield return P[i];
        }

        /// <summary>The part between two lengths along: its ends and the vertices between.</summary>
        public Vec2[] Slice(double s0, double s1) => [At(s0), .. Between(s0, s1), At(s1)];

        /// <summary>Lengthened at both ends, straight on, by <paramref name="d"/>.</summary>
        public Poly Extended(double d)
        {
            var pts = new List<Vec2>(P);
            var d0 = (P[0] - P[1]).Normalized();
            var d1 = (P[^1] - P[^2]).Normalized();
            pts[0] = P[0] + d0 * d;
            pts[^1] = P[^1] + d1 * d;
            return new Poly(pts);
        }

        /// <summary>Where another line crosses this one: the length along this one, along the other, and the point.</summary>
        public IEnumerable<(double S, double T, Vec2 P)> Hits(Poly q)
        {
            for (var i = 0; i + 1 < P.Count; i++)
                for (var j = 0; j + 1 < q.P.Count; j++)
                {
                    var (p0, p1, q0, q1) = (P[i], P[i + 1], q.P[j], q.P[j + 1]);
                    var (r, s) = (p1 - p0, q1 - q0);
                    var den = Vec2.Cross(r, s);
                    if (Math.Abs(den) < 1e-12) continue;
                    var t = Vec2.Cross(q0 - p0, s) / den;
                    var u = Vec2.Cross(q0 - p0, r) / den;
                    if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) continue;
                    yield return (S[i] + t * r.Length, q.S[j] + u * s.Length, p0 + r * t);
                }
        }
    }
}
