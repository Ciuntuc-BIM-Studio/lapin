using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The retaining wall with batters (docs/plan-wing-walls.md, S1–S2): the wing walls of a bridge abutment or a tunnel portal. Each
/// segment of its path may lean its front and its back (a batter n:1, measured in the section square to the segment), carry its own
/// crown thickness, run its crown from one elevation at its start to another at its end, sit on its own footing level, and warp: its
/// face going from one batter at its start to another at its end (a ruled surface, written exactly as a B-spline patch).
/// <para>
/// The faces lean from the crown's edges, so the crown keeps its thickness and the stem grows thicker where it is taller (a wing wall's
/// shape). Over a corner the crown stays level (as far as the faces meet, down to the footing): the segments' slopes run between their
/// corners. Each segment is built as pieces with plane faces (or its warped face) and the pieces are joined by one boolean union, so two
/// segments' faces meet on the exact line where their planes cross, on the outside and on the inside of a corner.
/// </para>
/// A wall with none of this (no batter, no segment overrides, no toe, heel or key) is built as before (<see cref="RetainingWalls"/>).
/// </summary>
public static class BatteredWalls
{
    /// <summary>The wall's own keys that make it a battered wall.</summary>
    public static readonly string[] WallKeys = ["frontBatter", "backBatter", "baseThickness", "toe", "heel", "keyWidth", "keyDepth", "keyAt"];

    /// <summary>A segment's numbers (null or absent: the wall's).</summary>
    public static readonly string[] SegmentKeys = ["frontBatter", "backBatter", "frontBatterEnd", "backBatterEnd", "thickness", "baseThickness", "topStart", "topEnd", "footingLevel", "endBatter", "endTurn", "toe", "heel", "footingThickness"];

    /// <summary>A segment's numbers that may be negative (elevations, an end's turn); the rest are kept at 0 or more.</summary>
    public static readonly string[] SignedSegmentKeys = ["topStart", "topEnd", "footingLevel", "endTurn"];

    public static bool Is(Entity e)
    {
        if (e.Num("frontBatter") > 0 || e.Num("backBatter") > 0) return true;
        foreach (var k in new[] { "baseThickness", "toe", "heel" }) if (e.NumOrNull(k) != null) return true;
        if (e.Num("keyDepth") > 0 && e.Num("keyWidth") > 0) return true;
        return e.Attrs["segments"] is JsonArray a && a.Any(x => x is JsonObject o && o.Count > 0);
    }

    /// <summary>
    /// One segment's overrides, as stored (null: the wall's own value). <see cref="TwistAll"/>: a warped face turns along the whole
    /// segment (<c>twist: "segment"</c>), not only along its slope. <see cref="EndBatter"/> and <see cref="EndTurn"/> shape the free end
    /// the segment has (the first segment's start, the last one's end): the end leaning n:1 with the embankment, and turned by degrees
    /// about the vertical, + making its side towards the high ground the longer (S3, docs/plan-wing-walls.md). <see cref="Name"/>: what the
    /// segment is (a wing, the face wall), its part's name.
    /// </summary>
    public sealed record SegmentSpec(double? FrontBatter, double? BackBatter, double? FrontBatterEnd, double? BackBatterEnd, bool Warp,
        double? Thickness, double? BaseThickness, double? TopStart, double? TopEnd, double? FootingLevel,
        bool TwistAll = false, double? EndBatter = null, double? EndTurn = null, string? Name = null,
        double? Toe = null, double? Heel = null, double? FootingThickness = null)
    {
        public static readonly SegmentSpec None = new(null, null, null, null, false, null, null, null, null, null);
    }

    public static SegmentSpec SegmentOf(Entity e, int i)
    {
        if (e.Attrs["segments"] is not JsonArray a || i < 0 || i >= a.Count || a[i] is not JsonObject o) return SegmentSpec.None;
        double? N(string k) => Json.Number(o[k]);
        var warp = o["warp"] is JsonValue v && v.TryGetValue<bool>(out var w) && w;
        return new(N("frontBatter"), N("backBatter"), N("frontBatterEnd"), N("backBatterEnd"), warp, N("thickness"), N("baseThickness"), N("topStart"), N("topEnd"), N("footingLevel"),
            (string?)o["twist"] == "segment", N("endBatter"), N("endTurn"), (string?)o["name"] is { Length: > 0 } nm ? nm : null,
            N("toe"), N("heel"), N("footingThickness"));
    }

    /// <summary>A batter n:1 (n up for one across) as the run across per unit of height; 0 or none is upright. Kept to 1:5 at the flattest.</summary>
    public static double Run(double? n) => n is > 0 ? 1 / Math.Max(0.2, n.Value) : 0;

    /// <summary>
    /// One segment, resolved: its line (from <see cref="A"/> along <see cref="D"/>, <see cref="H"/> the side of the high ground), its
    /// crown thickness, its batters as runs per unit of height at the start and at the end of its slope, its crown at both ends, its
    /// footing. Offsets w are across the segment, + towards the high ground; x along it from its start.
    /// </summary>
    public sealed class Leg
    {
        public int Index;
        public Vec2 A, B, D, H;
        public double L, T, Kf0, Kf1, Kb0, Kb1, Z0, Z1, Bottom, Zf;
        public bool Warp, Footing;
        /// <summary>The footing's front and back offsets.</summary>
        public double Fa, Fb;
        /// <summary>How far the footing reaches past the stem's foot, in front (the low ground's side) and behind (the high ground's): this segment's own, or the wall's; null: as wide as the wall's footing width, centred.</summary>
        public double? Toe, Heel;
        /// <summary>The key under the footing, its two offsets and its underside (when it has one).</summary>
        public double? KeyA, KeyB;
        public double KeyBottom;
        /// <summary>Where the slope runs: from S0 to S1 (the corners' extents; 0 and L at a free end).</summary>
        public double S0, S1;
        public bool StartCorner, EndCorner;

        /// <summary>A warped face turns along the whole segment (from 0 to L), not only along its slope (S0 to S1).</summary>
        public bool TwistAll;
        /// <summary>Its free ends, when it has one and it is not square: the run per unit of height the end leans by, and the tangent of its turn.</summary>
        public double StartLean, StartTurn, EndLean, EndTurn;
        public string? Name;

        public double Fraction(double x) => S1 - S0 < 1e-9 ? (x <= S0 ? 0 : 1) : Math.Clamp((x - S0) / (S1 - S0), 0, 1);
        /// <summary>How far along its turn a warped face is at x: along the slope, or along the whole segment.</summary>
        public double Twist(double x) => TwistAll ? (L < 1e-9 ? 0 : Math.Clamp(x / L, 0, 1)) : Fraction(x);
        public double Top(double x) => Z0 + (Z1 - Z0) * Fraction(x);
        public double Kf(double x) => Kf0 + (Kf1 - Kf0) * Twist(x);
        public double Kb(double x) => Kb0 + (Kb1 - Kb0) * Twist(x);
        /// <summary>Whether the free end at the start (or the end) is not square: it leans or is turned.</summary>
        public bool Shaped(bool atEnd) => atEnd ? !EndCorner && (EndLean > 1e-9 || Math.Abs(EndTurn) > 1e-9) : !StartCorner && (StartLean > 1e-9 || Math.Abs(StartTurn) > 1e-9);
        public double WF(double x, double z) => -T / 2 - (Top(x) - z) * Kf(x);
        public double WB(double x, double z) => T / 2 + (Top(x) - z) * Kb(x);
        public Vec3 P(double x, double w, double z) { var p = A + D * x + H * w; return new Vec3(p.X, p.Y, z); }
        public Vec2 P2(double x, double w) => A + D * x + H * w;
        public Vec3 D3 => new(D.X, D.Y, 0);
        public Vec3 H3 => new(H.X, H.Y, 0);
        public bool FrontWarps => Warp && Math.Abs(Kf1 - Kf0) > 1e-12;
        public bool BackWarps => Warp && Math.Abs(Kb1 - Kb0) > 1e-12;
        /// <summary>The band's widest extent across, for sizing the pieces at a corner.</summary>
        public double Width => Math.Max(Fb - Fa, Math.Max(WB(0, Zf) - WF(0, Zf), WB(L, Zf) - WF(L, Zf)));
    }

    /// <summary>A corner between two segments: its point, which side is its outside, and where the edges of the two meet there.</summary>
    public sealed record Corner(int Before, Vec2 V, bool OuterFront, bool Straight, IReadOnlyDictionary<string, Vec2> Crossings);

    public static (List<Leg> Legs, List<Corner> Corners, List<string> Notes) Resolve(IModelView m, Entity e)
    {
        var notes = new List<string>();
        var path = Chain.Clean(RetainingWalls.Path(e), null).Path;
        var spec = CorridorSpec.Of(e);
        if (spec.Curves.Any(c => !c.IsCorner) || spec.Horizontal != null) notes.Add("A battered retaining wall follows its path's straight segments; its curves are left out.");
        var chain = new Chain(path, spec.StationStart);
        var s = RetainingWalls.Of(e);
        var levels = RetainingWalls.Levels(m, e, chain);
        var tf = s.FootingThickness;
        double? wFront = e.NumOrNull("frontBatter"), wBack = e.NumOrNull("backBatter"), wBase = e.NumOrNull("baseThickness");
        double? toe = e.NumOrNull("toe"), heel = e.NumOrNull("heel");
        var legs = new List<Leg>();
        for (var i = 0; i < chain.Segments; i++)
        {
            var g = SegmentOf(e, i);
            var (a, b) = (path[i], path[i + 1]);
            var d = (b - a).Normalized();
            var leg = new Leg { Index = i, A = a, B = b, D = d, L = (b - a).Length, H = s.HighOnLeft ? d.Perp : -d.Perp };
            leg.T = Math.Max(0.05, g.Thickness ?? s.Thickness);
            leg.Bottom = g.FootingLevel ?? levels.Levels[i].Bottom;
            // The footing's thickness, toe and heel are the segment's own where it has them (each wing's footing follows its own axis), else the wall's.
            var tfi = Math.Max(0, g.FootingThickness ?? tf);
            leg.Zf = leg.Bottom + tfi;
            leg.Footing = tfi > 1e-6;
            leg.Toe = g.Toe ?? toe;
            leg.Heel = g.Heel ?? heel;
            var (st0, st1) = (chain.StationOf(i), chain.StationOf(i + 1));
            // A crown given at one end only slopes from the wall's own crown at the other (the panel's "the wall's").
            var z0 = g.TopStart ?? levels.Top(st0, i);
            var z1 = g.TopEnd ?? levels.Top(st1, i);
            leg.Z0 = Math.Max(z0, leg.Zf + 0.05);
            leg.Z1 = Math.Max(z1, leg.Zf + 0.05);
            leg.Warp = g.Warp;
            leg.Kf0 = Run(g.FrontBatter ?? wFront);
            leg.Kf1 = g.Warp && g.FrontBatterEnd != null ? Run(g.FrontBatterEnd) : leg.Kf0;
            // The back: its batter, or the thickness at the base where the stem is tallest (its batter worked out from it).
            var baseT = g.BaseThickness ?? (g.BackBatter == null ? wBase : null);
            if (baseT is { } tb)
            {
                var tallStart = leg.Z0 >= leg.Z1;
                var h = (tallStart ? leg.Z0 : leg.Z1) - leg.Zf;
                leg.Kb0 = Math.Max(0, (tb - leg.T) / Math.Max(h, 1e-6) - (tallStart ? leg.Kf0 : leg.Kf1));
            }
            else leg.Kb0 = Run(g.BackBatter ?? wBack);
            leg.Kb1 = g.Warp && g.BackBatterEnd != null ? Run(g.BackBatterEnd) : leg.Kb0;
            leg.TwistAll = g.Warp && g.TwistAll;
            leg.Name = g.Name;
            leg.S0 = 0;
            leg.S1 = leg.L;
            // Its free ends' shape: leaning n:1 (a run per unit of height), turned (up to 60° either way).
            var lean = Run(g.EndBatter);
            var turn = Math.Tan(Math.Clamp(g.EndTurn ?? 0, -60, 60) * Math.PI / 180);
            if (i == 0) { leg.StartLean = lean; leg.StartTurn = turn; }
            if (i == chain.Segments - 1) { leg.EndLean = lean; leg.EndTurn = turn; }
            Foot(leg, null);
            legs.Add(leg);
        }
        // The footing: the toe and the heel from the stem's base where it reaches furthest (a footing as wide as the wall's
        // footing width, centred on the stem, when they are not given). A face that turns along its whole segment has its foot
        // found by sampling once the corners are known (its crown is level over them, its batter turning already).
        void Foot(Leg leg, int? samples)
        {
            if (leg.Footing)
            {
                // Each face's foot is a quadratic along the slope: its furthest point at an end or at its vertex.
                static double Ext(Func<double, double> q, bool min)
                {
                    var (a, m, b) = (q(0), q(0.5), q(1));
                    var c2 = 2 * (a + b - 2 * m);
                    var c1 = b - a - c2;
                    var vals = new List<double> { a, b };
                    if (Math.Abs(c2) > 1e-15 && -c1 / (2 * c2) is var t and > 0 and < 1) vals.Add(q(t));
                    return min ? vals.Min() : vals.Max();
                }
                double TopF(double f) => leg.Z0 + (leg.Z1 - leg.Z0) * f;
                var front = samples is { } n1 ? Enumerable.Range(0, n1 + 1).Min(k => leg.WF(leg.L * k / n1, leg.Zf))
                    : Ext(f => -leg.T / 2 - (TopF(f) - leg.Zf) * (leg.Kf0 + (leg.Kf1 - leg.Kf0) * f), true);
                var back = samples is { } n2 ? Enumerable.Range(0, n2 + 1).Max(k => leg.WB(leg.L * k / n2, leg.Zf))
                    : Ext(f => leg.T / 2 + (TopF(f) - leg.Zf) * (leg.Kb0 + (leg.Kb1 - leg.Kb0) * f), false);
                // At least 5 cm past the stem either side (a footing flush with a curved foot would touch it at one point only).
                var spare = Math.Max(0.05, (s.FootingWidth - (back - front)) / 2);
                leg.Fa = front - Math.Max(0, leg.Toe ?? spare);
                leg.Fb = back + Math.Max(0, leg.Heel ?? spare);
                var (kw, kd) = (e.Num("keyWidth"), e.Num("keyDepth"));
                if (kw > 0 && kd > 0)
                {
                    var at = e.NumOrNull("keyAt") ?? (leg.Fb - leg.Fa - kw) / 2;
                    var ka = Math.Clamp(leg.Fa + at, leg.Fa, Math.Max(leg.Fa, leg.Fb - kw));
                    leg.KeyA = ka;
                    leg.KeyB = Math.Min(leg.Fb, ka + kw);
                    leg.KeyBottom = leg.Bottom - kd;
                }
            }
            else { leg.Fa = leg.WF(0, leg.Zf); leg.Fb = leg.WB(0, leg.Zf); }
        }
        var corners = Corners(legs, s.HighOnLeft, notes);
        // A face turning along its whole segment: its foot again, by its batter as it is with the corners known, and the corners again.
        if (legs.Any(g => g.TwistAll && g.Footing))
        {
            foreach (var g in legs.Where(g => g.TwistAll)) Foot(g, 200);
            foreach (var g in legs) { g.S0 = 0; g.S1 = g.L; }
            corners = Corners(legs, s.HighOnLeft, []);
        }
        return (legs, corners, notes);
    }

    /// <summary>The corners: where each pair of the two segments' edges cross, and how far the crown stays level either side.</summary>
    static List<Corner> Corners(List<Leg> legs, bool highOnLeft, List<string> notes)
    {
        var corners = new List<Corner>();
        for (var k = 0; k + 1 < legs.Count; k++)
        {
            var (li, lj) = (legs[k], legs[k + 1]);
            var sin = Vec2.Cross(li.D, lj.D);
            var cos = Vec2.Dot(li.D, lj.D);
            var straight = Math.Abs(sin) < Math.Sin(Math.PI / 180) && cos > 0;
            if (cos < Math.Cos(170 * Math.PI / 180)) notes.Add($"Segments {k + 1} and {k + 2} turn back on each other: the corner may not close.");
            var outerFront = (sin > 0) == highOnLeft;
            var crossings = new Dictionary<string, Vec2>();
            if (!straight)
            {
                var pairs = new List<(string Key, double Wi, double Wj)>
                {
                    ("crownF", -li.T / 2, -lj.T / 2), ("crownB", li.T / 2, lj.T / 2),
                    ("baseF", -li.T / 2 - (li.Z1 - li.Zf) * li.Kf1, -lj.T / 2 - (lj.Z0 - lj.Zf) * lj.Kf0),
                    ("baseB", li.T / 2 + (li.Z1 - li.Zf) * li.Kb1, lj.T / 2 + (lj.Z0 - lj.Zf) * lj.Kb0),
                };
                if (li.Footing && lj.Footing) { pairs.Add(("footF", li.Fa, lj.Fa)); pairs.Add(("footB", li.Fb, lj.Fb)); }
                double b = 0, a = 0;
                foreach (var (key, wi, wj) in pairs)
                {
                    if (Cross(li.B + li.H * wi, li.D, lj.A + lj.H * wj, lj.D) is not { } p) continue;
                    crossings[key] = p;
                    // How far the crossing is back along the first from the corner, and along the second (the level stretch over the corner on each).
                    b = Math.Max(b, -Vec2.Dot(p - li.B, li.D));
                    a = Math.Max(a, Vec2.Dot(p - lj.A, lj.D));
                }
                li.S1 = li.L - Math.Min(b, 0.45 * li.L);
                lj.S0 = Math.Min(a, 0.45 * lj.L);
            }
            li.EndCorner = lj.StartCorner = true;
            corners.Add(new Corner(k, li.B, outerFront, straight, crossings));
        }
        return corners;
    }

    static Vec2? Cross(Vec2 p, Vec2 dp, Vec2 q, Vec2 dq)
    {
        var den = Vec2.Cross(dp, dq);
        if (Math.Abs(den) < 1e-12) return null;
        var t = Vec2.Cross(q - p, dq) / den;
        return p + dp * t;
    }

    // ------------------------------------------------------------------ the pieces

    /// <summary>A warped face, exactly: the leg it is on, its side, its patch, the direction its parameter runs along, the stretch of the leg it covers.</summary>
    public sealed record Warp(int Leg, bool Front, BSplineSurface Patch, Vec3 Along, double X0, double X1);

    /// <summary>A plane the wall's faces may lie on, and what a face on it is (front, back, crown, end, footing, footingTop, bottom, key, step).</summary>
    public sealed record LabelledPlane(Vec3 N, double D, string Label);

    /// <summary>What the wall is made of, before the union: its pieces, the planes they lie on, its warped faces.</summary>
    public sealed class Pieces
    {
        public List<Solid> Stem = [], Footing = [];
        /// <summary>The warped pieces with their volumes exactly (what the facets miss), and the leg each is of.</summary>
        public List<(Solid Piece, double Exact, int Leg)> Warped = [];
        /// <summary>The leg each piece is of (a part of the assembly is its leg's pieces).</summary>
        public Dictionary<Solid, int> LegOf = new(ReferenceEqualityComparer.Instance);
        public List<LabelledPlane> Planes = [];
        public List<Warp> Warps = [];
    }

    /// <summary>
    /// The patch of a warped face over its slope: u along the leg from S0 to S1, v from the footing's top (0) to the crown (1). The crown
    /// edge is a line, the base the parabola the batter's change makes there, the face between them ruled: degree 2 by 1, exact.
    /// </summary>
    public static BSplineSurface Patch(Leg g, bool front) => Patch(g, front, g.S0, g.S1);

    /// <summary>The patch over x0 to x1 (a face turning along its whole segment has one over each corner's level crown too, its base a line there).</summary>
    public static BSplineSurface Patch(Leg g, bool front, double x0, double x1)
    {
        var ctrl = new Vec3[3, 2];
        double W(double x, double z) => front ? g.WF(x, z) : g.WB(x, z);
        var crownW = front ? -g.T / 2 : g.T / 2;
        // Bernstein coefficients of the base's offset (quadratic in u): b1 = 2 q(½) − (q(0) + q(1)) / 2.
        var (q0, qm, q1) = (W(x0, g.Zf), W((x0 + x1) / 2, g.Zf), W(x1, g.Zf));
        var wb = new[] { q0, 2 * qm - (q0 + q1) / 2, q1 };
        var xb = new[] { x0, (x0 + x1) / 2, x1 };
        var zt = new[] { g.Top(x0), (g.Top(x0) + g.Top(x1)) / 2, g.Top(x1) };
        for (var i = 0; i < 3; i++)
        {
            ctrl[i, 0] = g.P(xb[i], wb[i], g.Zf);
            ctrl[i, 1] = g.P(xb[i], crownW, zt[i]);
        }
        return new BSplineSurface(2, 1, [0, 0, 0, 1, 1, 1], [0, 0, 1, 1], ctrl);
    }

    /// <summary>The ring of a stem's section at x: up the back, across the crown, down the front (counter-clockwise in w, z), at the rows given.</summary>
    static List<(double W, double Z)> StemRing(Leg g, double x, IReadOnlyList<double> rows)
    {
        var top = g.Top(x);
        var o = new List<(double, double)>();
        foreach (var v in rows) { var z = g.Zf + v * (top - g.Zf); o.Add((g.WB(x, z), z)); }
        for (var k = rows.Count - 1; k >= 0; k--) { var z = g.Zf + rows[k] * (top - g.Zf); o.Add((g.WF(x, z), z)); }
        // Start at the front's foot: rotate so the first point is (front, Zf).
        var first = o[^1];
        o.RemoveAt(o.Count - 1);
        o.Insert(0, first);
        return o;
    }

    static List<(double W, double Z)> Box(double w0, double w1, double z0, double z1) => [(w0, z0), (w1, z0), (w1, z1), (w0, z1)];

    /// <summary>A piece lofted along a leg through its rings (each the same count, counter-clockwise in w, z) at the stations given.</summary>
    static Solid? Loft(Leg g, IReadOnlyList<double> xs, Func<double, List<(double W, double Z)>> ringAt)
    {
        if (xs.Count < 2) return null;
        var rings = xs.Select(x => ringAt(x).Select(q => g.P(x, q.W, q.Z)).ToList()).ToList();
        var n = rings[0].Count;
        var verts = new List<Vec3>();
        var faces = new List<FaceInput>();
        int Add(Vec3 p) { verts.Add(p); return verts.Count - 1; }
        var ringIdx = rings.Select(r => r.Select(Add).ToArray()).ToList();
        var rw = ringAt(xs[0]);
        for (var s = 0; s + 1 < rings.Count; s++)
            for (var k = 0; k < n; k++)
            {
                var k1 = (k + 1) % n;
                var (a, b, c, d) = (ringIdx[s][k], ringIdx[s][k1], ringIdx[s + 1][k1], ringIdx[s + 1][k]);
                var (dw, dz) = (rw[k1].W - rw[k].W, rw[k1].Z - rw[k].Z);
                var hint = g.H3 * dz + Vec3.UnitZ * (-dw);
                if (Planar(verts[a], verts[b], verts[c], verts[d])) faces.Add(new FaceInput([a, b, c, d], null, hint));
                else { faces.Add(new FaceInput([a, b, c], null, hint)); faces.Add(new FaceInput([a, c, d], null, hint)); }
            }
        faces.Add(new FaceInput(ringIdx[0], null, -g.D3));
        faces.Add(new FaceInput(ringIdx[^1], null, g.D3));
        return SolidBuilder.Make(verts, faces, "retaining");
    }

    static bool Planar(Vec3 a, Vec3 b, Vec3 c, Vec3 d)
    {
        var n = Vec3.Cross(b - a, c - a);
        var l = n.Length;
        if (l < 1e-15) return true;
        return Math.Abs(Vec3.Dot(d - a, n * (1 / l))) < 1e-9;
    }

    /// <summary>A convex solid with what lies beyond the plane n·p = d cut off (the side n points to); null when nothing is left.</summary>
    public static Solid? Clip(Solid? s, Vec3 n, double d)
    {
        if (s == null) return null;
        const double eps = 1e-9;
        if (s.Vertices.All(p => Vec3.Dot(n, p) - d <= eps)) return s;
        if (s.Vertices.All(p => Vec3.Dot(n, p) - d >= -eps)) return null;
        var polys = new List<(List<Vec3> Ring, Vec3 Normal)>();
        var cut = new List<Vec3>();
        foreach (var f in s.Faces)
        {
            var ring = f.Outer.Select(i => s.Vertices[i]).ToList();
            var o = new List<Vec3>();
            for (var k = 0; k < ring.Count; k++)
            {
                var (p, q) = (ring[k], ring[(k + 1) % ring.Count]);
                var (dp, dq) = (Vec3.Dot(n, p) - d, Vec3.Dot(n, q) - d);
                if (dp <= eps) o.Add(p);
                if (Math.Abs(dp) <= eps) cut.Add(p);
                if ((dp < -eps && dq > eps) || (dp > eps && dq < -eps))
                {
                    var x = p + (q - p) * (dp / (dp - dq));
                    o.Add(x);
                    cut.Add(x);
                }
            }
            if (o.Count >= 3) polys.Add((o, f.Normal));
        }
        // The cap: the points on the plane, in order round their middle.
        var pts = new List<Vec3>();
        foreach (var p in cut) if (!pts.Any(q => (q - p).Length < 1e-9)) pts.Add(p);
        if (pts.Count >= 3)
        {
            var mid = pts.Aggregate(Vec3.Zero, (a, p) => a + p) * (1.0 / pts.Count);
            var (u, v) = VecMath.PlaneBasis(n);
            pts.Sort((a, b) => Math.Atan2(Vec3.Dot(a - mid, v), Vec3.Dot(a - mid, u)).CompareTo(Math.Atan2(Vec3.Dot(b - mid, v), Vec3.Dot(b - mid, u))));
            polys.Add((pts, n));
        }
        if (polys.Count < 4) return null;
        var verts = new List<Vec3>();
        var faces = new List<FaceInput>();
        foreach (var (ring, normal) in polys)
        {
            var idx = new int[ring.Count];
            for (var k = 0; k < ring.Count; k++) { verts.Add(ring[k]); idx[k] = verts.Count - 1; }
            faces.Add(new FaceInput(idx, null, normal));
        }
        var made = SolidBuilder.Make(verts, faces, s.Tag);
        return made.Faces.Length >= 4 && Measure.Volume(made) > 1e-12 ? made : null;
    }

    /// <summary>The plane of a leg's outside face at one of its ends (the crown level there, flat), pointing out of the leg.</summary>
    static (Vec3 N, double D) OuterStem(Leg g, bool atEnd, bool outerFront)
    {
        var x = atEnd ? g.L : 0;
        var top = atEnd ? g.Z1 : g.Z0;
        var k = outerFront ? (atEnd ? g.Kf1 : g.Kf0) : (atEnd ? g.Kb1 : g.Kb0);
        var sign = outerFront ? -1 : 1;
        double W(double z) => sign * (g.T / 2 + (top - z) * k);
        var p1 = g.P(x, W(g.Zf), g.Zf);
        var p2 = g.P(x, W(top), top);
        var n = Vec3.Cross(g.D3, p2 - p1).Normalized();
        if (Vec3.Dot(n, g.H3 * sign) < 0) n = -n;
        return (n, Vec3.Dot(n, p1));
    }

    static (Vec3 N, double D) OuterFoot(Leg g, bool outerFront)
    {
        var n = outerFront ? -g.H3 : g.H3;
        return (n, Vec3.Dot(n, g.P(0, outerFront ? g.Fa : g.Fb, 0)));
    }

    /// <summary>Points along a slope of a leg the chord rule asks for (a warped face's patches and their twist), from S0 to S1.</summary>
    static (List<double> Xs, List<double> Rows) Samples(Leg g, List<Warp> warps) => Samples(g, warps, g.S0, g.S1);

    /// <summary>The same over the stretch x0 to x1 of the leg (its patches over that stretch).</summary>
    static (List<double> Xs, List<double> Rows) Samples(Leg g, List<Warp> warps, double x0, double x1)
    {
        var mine = warps.Where(w => w.Leg == g.Index && Math.Abs(w.X0 - x0) < 1e-9 && Math.Abs(w.X1 - x1) < 1e-9).ToList();
        if (mine.Count == 0) return ([x0, x1], [0, 1]);
        var us = new SortedSet<double> { 0, 1 };
        var vs = new SortedSet<double> { 0, 1 };
        foreach (var w in mine)
        {
            var (gu, gv) = w.Patch.Grid();
            foreach (var u in gu) us.Add(u);
            foreach (var v in gv) vs.Add(v);
            // The twist: a cell split into two triangles strays by |Suv| Δu Δv / 4; as many cells as keep that within the chord tolerance.
            var twist = Enumerable.Range(0, 9).Max(k => w.Patch.Derivatives(k / 8.0, 0.5, 2)[1, 1].Length);
            var cells = (int)Math.Ceiling(Math.Sqrt(twist / (4 * Chords.Tolerance)));
            for (var k = 1; k < cells; k++) { us.Add((double)k / cells); vs.Add((double)k / cells); }
        }
        return ([.. us.Select(u => x0 + (x1 - x0) * u)], [.. vs]);
    }

    /// <summary>Every point along a leg its patches ask for, over all their stretches (for lines drawn along it).</summary>
    static List<double> AllSamples(Leg g, List<Warp> warps)
    {
        var xs = new SortedSet<double>(Samples(g, warps).Xs);
        foreach (var w in warps.Where(w => w.Leg == g.Index)) foreach (var x in Samples(g, warps, w.X0, w.X1).Xs) xs.Add(x);
        return [.. xs];
    }

    /// <summary>The stretches of a leg its warped faces cover: the slope, and with a whole-segment twist the level crown over each corner too.</summary>
    static List<(double X0, double X1)> WarpedStretches(Leg g)
    {
        var o = new List<(double, double)>();
        if (!g.FrontWarps && !g.BackWarps) return o;
        if (g.TwistAll && g.StartCorner && g.S0 > 1e-6) o.Add((0, g.S0));
        if (g.S1 - g.S0 > 1e-6) o.Add((g.S0, g.S1));
        if (g.TwistAll && g.EndCorner && g.L - g.S1 > 1e-6) o.Add((g.S1, g.L));
        return o;
    }

    public static Pieces Make(List<Leg> legs, List<Corner> corners)
    {
        var o = new Pieces();
        if (legs.Count == 0) return o;
        foreach (var g in legs)
            foreach (var (x0, x1) in WarpedStretches(g))
            {
                if (g.FrontWarps) o.Warps.Add(new Warp(g.Index, true, Patch(g, true, x0, x1), g.D3, x0, x1));
                if (g.BackWarps) o.Warps.Add(new Warp(g.Index, false, Patch(g, false, x0, x1), g.D3, x0, x1));
            }
        var width = legs.Max(g => g.Width);
        foreach (var g in legs)
        {
            void Stem(Solid? piece) { if (piece != null) { o.Stem.Add(piece); o.LegOf[piece] = g.Index; } }
            void Footing(Solid? piece) { if (piece != null) { o.Footing.Add(piece); o.LegOf[piece] = g.Index; } }
            var prev = g.StartCorner ? corners[g.Index - 1] : null;
            var next = g.EndCorner ? corners[g.Index] : null;
            var (pl, nl) = (prev != null ? legs[g.Index - 1] : null, next != null ? legs[g.Index + 1] : null);
            var ext = 4 * width + g.L + 1;
            // The stem: its slope between the corners (and with a whole-segment twist, its warped stretches over the corners), and its
            // level ends over each corner (from where its twist starts).
            var stretches = WarpedStretches(g);
            if (stretches.Count == 0 && g.S1 - g.S0 > 1e-6) stretches.Add((g.S0, g.S1));
            // The stretches lofted as one piece (a whole-segment twist's three: no faces between them inside the wall), its rows the
            // finest any of them asks, carried a hair past each end it shares with another piece (its section there kept), so no two
            // pieces meet face to face on the plane a corner's pieces are cut on as well: three faces on one plane may leave a crack.
            if (stretches.Count > 0)
            {
                var (x0, x1) = (stretches[0].X0, stretches[^1].X1);
                var xs = new SortedSet<double>();
                var rowSet = new SortedSet<double>();
                foreach (var (a0, a1) in stretches) { var (sx, sr) = Samples(g, o.Warps, a0, a1); foreach (var x in sx) xs.Add(x); foreach (var r in sr) rowSet.Add(r); }
                var rows = rowSet.ToList();
                // A section where an assembly's joint with the face wall falls, so the joint's cut runs along the facets' edges, on the surface.
                if (prev != null && g.S0 + JointPast < x1) xs.Add(g.S0 + JointPast);
                if (next != null && g.S1 - JointPast > x0) xs.Add(g.S1 - JointPast);
                var (e0, e1) = (x0 > 1e-9 || prev != null ? Overlap : 0, x1 < g.L - 1e-9 || next != null ? Overlap : 0);
                var stations = (e0 > 0 ? new[] { x0 - e0 } : []).Concat(xs).Concat(e1 > 0 ? new[] { x1 + e1 } : []).ToList();
                if (Loft(g, stations, x => StemRing(g, Math.Clamp(x, x0, x1), rows)) is { } mid)
                {
                    Stem(mid);
                    if (o.Warps.Any(w => w.Leg == g.Index))
                        o.Warped.Add((mid, stretches.Sum(st => ExactVolume(g, st.X0, st.X1)) + e0 * RingArea(StemRing(g, x0, rows)) + e1 * RingArea(StemRing(g, x1, rows)), g.Index));
                }
            }
            var (c0, c1) = (stretches.Count > 0 ? stretches[0].X0 : g.S0, stretches.Count > 0 ? stretches[^1].X1 : g.S1);
            Solid? StartPiece(List<(double W, double Z)> ring, (Vec3 N, double D) outer, double at) =>
                Clip(Clip(Loft(g, [at - ext, at], _ => ring), outer.N, outer.D), -pl!.D3, -(Vec3.Dot(pl.D3, pl.A.At(0)) + pl.S1));
            Solid? EndPiece(List<(double W, double Z)> ring, (Vec3 N, double D) outer, double at) =>
                Clip(Clip(Loft(g, [at, at + ext], _ => ring), outer.N, outer.D), nl!.D3, Vec3.Dot(nl.D3, nl.A.At(0)) + nl.S0);
            if (prev != null) Stem(StartPiece(StemRing(g, c0, [0, 1]), OuterStem(pl!, true, prev.OuterFront), c0));
            if (next != null) Stem(EndPiece(StemRing(g, c1, [0, 1]), OuterStem(nl!, false, next.OuterFront), c1));
            // A free end that leans or is turned: the stem and the footing carried past it, cut by its plane once joined.
            var (eStart, eEnd) = (EndReach(g, false), EndReach(g, true));
            if (eStart > 0) Stem(Loft(g, [-eStart, Overlap], _ => StemRing(g, 0, [0, 1])));
            if (eEnd > 0) Stem(Loft(g, [g.L - Overlap, g.L + eEnd], _ => StemRing(g, g.L, [0, 1])));
            // The footing, the same way round its corners.
            if (g.Footing)
            {
                var ring = Box(g.Fa, g.Fb, g.Bottom, g.Zf);
                // Carried a hair past a corner as the stem is (no three faces on one plane).
                if (g.S1 - g.S0 > 1e-6) Footing(Loft(g, [g.S0 - (prev != null ? Overlap : 0), g.S1 + (next != null ? Overlap : 0)], _ => ring));
                if (prev != null) Footing(StartPiece(ring, OuterFoot(pl!, prev.OuterFront), g.S0));
                if (next != null) Footing(EndPiece(ring, OuterFoot(nl!, next.OuterFront), g.S1));
                if (eStart > 0) Footing(Loft(g, [-eStart - FootSpare(g, false), Overlap], _ => ring));
                if (eEnd > 0) Footing(Loft(g, [g.L - Overlap, g.L + eEnd + FootSpare(g, true)], _ => ring));
                // The key, mitred at the corners.
                if (g.KeyA is { } ka && g.KeyB is { } kb)
                {
                    var key = Loft(g, [-ext, g.L + ext], _ => Box(ka, kb, g.KeyBottom, g.Bottom));
                    if (pl != null) { var n = -(pl.D3 + g.D3).Normalized(); key = Clip(key, n, Vec3.Dot(n, g.A.At(0))); }
                    else key = Clip(key, -g.D3, -Vec3.Dot(g.D3, g.A.At(0)));
                    if (nl != null) { var n = (g.D3 + nl.D3).Normalized(); key = Clip(key, n, Vec3.Dot(n, g.B.At(0))); }
                    else key = Clip(key, g.D3, Vec3.Dot(g.D3, g.B.At(0)));
                    Footing(key);
                }
            }
            Label(o, g, legs, corners);
        }
        return o;
    }

    /// <summary>How far a stem's stretch is carried past an end it shares with another piece (a tenth of a millimetre).</summary>
    const double Overlap = 1e-4;

    /// <summary>How far past its slope's start a wing's joint with the face wall is, as an assembly (5 mm).</summary>
    const double JointPast = 5e-3;

    static double RingArea(List<(double W, double Z)> ring)
    {
        var a = 0.0;
        for (var k = 0; k < ring.Count; k++) { var (p, q) = (ring[k], ring[(k + 1) % ring.Count]); a += p.W * q.Z - q.W * p.Z; }
        return Math.Abs(a) / 2;
    }

    // ------------------------------------------------------------------ free ends

    /// <summary>How far past a free end its stem reaches before its plane cuts it (0: a square end, cut by nothing).</summary>
    static double EndReach(Leg g, bool atEnd)
    {
        if (!g.Shaped(atEnd)) return 0;
        var (lean, turn) = atEnd ? (g.EndLean, g.EndTurn) : (g.StartLean, g.StartTurn);
        var top = atEnd ? g.Z1 : g.Z0;
        var across = Math.Max(Math.Abs(g.Fa), Math.Abs(g.Fb));
        return (top - g.Zf) * lean + across * Math.Abs(turn) + 0.1;
    }

    /// <summary>How far the footing reaches past a leaning end's foot: its toe there (none for an end turned only, flush with the stem).</summary>
    static double FootSpare(Leg g, bool atEnd)
    {
        var lean = atEnd ? g.EndLean : g.StartLean;
        if (lean <= 1e-9 || !g.Footing) return 0;
        var x = atEnd ? g.L : 0;
        return Math.Max(0.05, g.WF(x, g.Zf) - g.Fa);
    }

    /// <summary>
    /// The plane of a free end (pointing out of the wall), at the crown's level there: the stem's, leaning back from the crown's end
    /// (s·(x − Xe) − w·tan φ − (Ze − z)·k = 0), or with <paramref name="footing"/> the footing's, upright, its toe past the stem's foot.
    /// </summary>
    public static (Vec3 N, double D) EndPlane(Leg g, bool atEnd, bool footing)
    {
        var sgn = atEnd ? 1.0 : -1.0;
        var (lean, turn) = atEnd ? (g.EndLean, g.EndTurn) : (g.StartLean, g.StartTurn);
        var (xe, ze) = atEnd ? (g.L, g.Z1) : (0.0, g.Z0);
        var xy = g.D * sgn - g.H * turn;
        if (footing)
        {
            var nf = new Vec3(xy.X, xy.Y, 0);
            var df = Vec2.Dot(xy, g.A) + sgn * xe + (ze - g.Zf) * lean + FootSpare(g, atEnd);
            var lf = nf.Length;
            return (nf * (1 / lf), df / lf);
        }
        var n = new Vec3(xy.X, xy.Y, lean);
        var d = Vec2.Dot(xy, g.A) + sgn * xe + ze * lean;
        var l = n.Length;
        return (n * (1 / l), d / l);
    }

    /// <summary>What cuts a wall's shaped free ends: beyond each end's plane, the stem's above the footing's top, the footing's below it (one, upright, for a turned end).</summary>
    static List<Solid> EndCutters(List<Leg> legs)
    {
        var o = new List<Solid>();
        if (legs.Count == 0) return o;
        var big = legs.Sum(g => g.L) + legs.Max(g => g.Width) * 4 + 50;
        var zLow = legs.Min(g => g.KeyA != null ? g.KeyBottom : g.Bottom) - 5;
        var zHigh = legs.Max(g => Math.Max(g.Z0, g.Z1)) + 5;
        foreach (var (g, atEnd) in new[] { (legs[0], false), (legs[^1], true) })
        {
            if (!g.Shaped(atEnd)) continue;
            var lean = atEnd ? g.EndLean : g.StartLean;
            var xe = atEnd ? g.L : 0;
            Solid? Half((Vec3 N, double D) plane, double z0, double z1) =>
                Clip(Loft(g, [xe - big, xe + big], _ => Box(-big, big, z0, z1)), -plane.N, -plane.D);
            if (lean <= 1e-9 || !g.Footing)
            {
                if (Half(EndPlane(g, atEnd, false), zLow, zHigh) is { } c) o.Add(c);
                continue;
            }
            if (Half(EndPlane(g, atEnd, false), g.Zf, zHigh) is { } stem) o.Add(stem);
            if (Half(EndPlane(g, atEnd, true), zLow, g.Zf) is { } foot) o.Add(foot);
        }
        return o;
    }

    /// <summary>The planes a leg's faces may lie on, labelled (formwork is reckoned by them).</summary>
    static void Label(Pieces o, Leg g, List<Leg> legs, List<Corner> corners)
    {
        void Plane(Vec3 a, Vec3 b, Vec3 c, string label)
        {
            var n = Vec3.Cross(b - a, c - a);
            if (n.Length < 1e-12) return;
            n = n.Normalized();
            o.Planes.Add(new(n, Vec3.Dot(n, a), label));
            o.Planes.Add(new(-n, -Vec3.Dot(n, a), label));
        }
        foreach (var x in new[] { 0, g.S0, g.S1, g.L }.Distinct())
        {
            // The faces through the ruling at each end of the slope and of the leg, along the leg (the faces over the corners and past
            // a shaped free end, which keep one section); where planar, the slope's own.
            foreach (var front in new[] { true, false })
            {
                double W(double xx, double z) => front ? g.WF(xx, z) : g.WB(xx, z);
                var label = front ? "front" : "back";
                Plane(g.P(x, W(x, g.Zf), g.Zf), g.P(x, W(x, g.Top(x)), g.Top(x)), g.P(x, W(x, g.Zf), g.Zf) + g.D3, label);
                if (!(front ? g.FrontWarps : g.BackWarps) && g.S1 - g.S0 > 1e-6)
                    Plane(g.P(g.S0, W(g.S0, g.Zf), g.Zf), g.P(g.S1, W(g.S1, g.Zf), g.Zf), g.P(g.S0, W(g.S0, g.Top(g.S0)), g.Top(g.S0)), label);
            }
        }
        // The crown: level over the corners, sloping between.
        Plane(g.P(0, 0, g.Z0), g.P(0, 1, g.Z0), g.P(1, 0, g.Z0), "crown");
        Plane(g.P(0, 0, g.Z1), g.P(0, 1, g.Z1), g.P(1, 0, g.Z1), "crown");
        if (g.S1 - g.S0 > 1e-6) Plane(g.P(g.S0, 0, g.Z0), g.P(g.S0, 1, g.Z0), g.P(g.S1, 0, g.Z1), "crown");
        Plane(g.P(0, 0, g.Zf), g.P(0, 1, g.Zf), g.P(1, 0, g.Zf), "footingTop");
        Plane(g.P(0, 0, g.Bottom), g.P(0, 1, g.Bottom), g.P(1, 0, g.Bottom), "bottom");
        if (g.Footing)
            foreach (var w in new[] { g.Fa, g.Fb }) Plane(g.P(0, w, 0), g.P(1, w, 0), g.P(0, w, 1), "footing");
        if (g.KeyA is { } ka && g.KeyB is { } kb)
        {
            foreach (var w in new[] { ka, kb }) Plane(g.P(0, w, 0), g.P(1, w, 0), g.P(0, w, 1), "key");
            Plane(g.P(0, 0, g.KeyBottom), g.P(0, 1, g.KeyBottom), g.P(1, 0, g.KeyBottom), "bottom");
        }
        // The wall's two ends.
        if (!g.StartCorner) Plane(g.P(0, 0, 0), g.P(0, 1, 0), g.P(0, 0, 1), "end");
        if (!g.EndCorner) Plane(g.P(g.L, 0, 0), g.P(g.L, 1, 0), g.P(g.L, 0, 1), "end");
        // A shaped free end's planes: the stem's (leaning, turned) and the footing's.
        foreach (var atEnd in new[] { false, true })
        {
            if (!g.Shaped(atEnd)) continue;
            foreach (var foot in new[] { false, true })
            {
                var (n, d) = EndPlane(g, atEnd, foot);
                o.Planes.Add(new(n, d, "end"));
                o.Planes.Add(new(-n, -d, "end"));
            }
        }
        // Where the slope starts and ends (a step where the crown jumps at a corner).
        foreach (var x in new[] { g.S0, g.S1 }) Plane(g.P(x, 0, 0), g.P(x, 1, 0), g.P(x, 0, 1), "step");
    }

    /// <summary>The volume of a leg's slope (its middle piece) exactly: ∫ (T·h + (kf + kb)·h²/2) dx, a cubic, by five-point Gauss.</summary>
    public static double ExactVolume(Leg g) => ExactVolume(g, g.S0, g.S1);

    /// <summary>The same over x0 to x1 (a cubic on each stretch the crown and the batters are linear on).</summary>
    public static double ExactVolume(Leg g, double x0, double x1) => Gauss(x0, x1, x => { var h = g.Top(x) - g.Zf; return g.T * h + (g.Kf(x) + g.Kb(x)) * h * h / 2; });

    /// <summary>A patch's area: ∫∫ |Su × Sv|, by Gauss in both directions (ten points each: well within a square millimetre on a wall).</summary>
    public static double TrueArea(BSplineSurface s)
    {
        double sum = 0;
        for (var i = 0; i < G10.Length; i++)
            for (var j = 0; j < G10.Length; j++)
            {
                var (u, v) = ((G10[i].X + 1) / 2, (G10[j].X + 1) / 2);
                var d = s.Derivatives(u, v, 1);
                sum += G10[i].W * G10[j].W / 4 * Vec3.Cross(d[1, 0], d[0, 1]).Length;
            }
        return sum;
    }

    static double Gauss(double a, double b, Func<double, double> f)
    {
        double[] x = [0, -0.5384693101056831, 0.5384693101056831, -0.9061798459386640, 0.9061798459386640];
        double[] w = [0.5688888888888889, 0.4786286704993665, 0.4786286704993665, 0.2369268850561891, 0.2369268850561891];
        var s = 0.0;
        for (var i = 0; i < 5; i++) s += w[i] * f((a + b) / 2 + (b - a) / 2 * x[i]);
        return s * (b - a) / 2;
    }

    static readonly (double X, double W)[] G10 =
    [
        (-0.1488743389816312, 0.2955242247147529), (0.1488743389816312, 0.2955242247147529),
        (-0.4333953941292472, 0.2692667193099963), (0.4333953941292472, 0.2692667193099963),
        (-0.6794095682990244, 0.2190863625159820), (0.6794095682990244, 0.2190863625159820),
        (-0.8650633666889845, 0.1494513791305938), (0.8650633666889845, 0.1494513791305938),
        (-0.9739065285171717, 0.0666713443086881), (0.9739065285171717, 0.0666713443086881),
    ];

    // ------------------------------------------------------------------ the wall

    /// <summary>One part of the wall as an assembly (a wing, the face wall): its leg, its key and name, its body.</summary>
    public sealed record Part(int Leg, string Key, string Name, Solid Solid)
    {
        /// <summary>Whether a point of the wall is in this part (its warped pieces' share of what the facets miss is reckoned by it).</summary>
        public Func<Vec3, bool> Holds { get; init; } = _ => true;
    }

    /// <summary>A built wall: its body and what made it (its quantities and its exact body read them).</summary>
    public sealed class Built
    {
        public required List<Leg> Legs;
        public required List<Corner> Corners;
        public required Pieces Pieces;
        public Solid? Solid;
        public ISolidBooleans? Booleans;
        public List<string> Notes = [];
        /// <summary>Its parts, one per segment, when it is built as an assembly (none otherwise).</summary>
        public List<Part> Parts = [];
        double? _stem;

        /// <summary>The stem's volume, exactly where it warps: its pieces joined, less what the facets miss.</summary>
        public double StemVolume()
        {
            if (_stem is { } v) return v;
            // A shaped free end cuts the stem as it cuts the wall.
            var cut = EndCutters(Legs);
            var joined = Booleans != null && (Pieces.Stem.Count > 1 || cut.Count > 0) ? Booleans.UnionLess(Pieces.Stem, cut, "stem") : null;
            var raw = joined != null ? Measure.Volume(joined) : Pieces.Stem.Sum(Measure.Volume);
            return (_stem = raw + Correction).Value;
        }

        /// <summary>The volume the warped faces' facets miss: their exact volume less their facets'.</summary>
        public double Correction => Pieces.Warped.Sum(w => w.Exact - Measure.Volume(w.Piece));

        /// <summary>What the warped pieces in a part miss (its share of <see cref="Correction"/>): each piece the part its middle is in.</summary>
        public double CorrectionOf(Part part) => Pieces.Warped.Where(w => part.Holds(w.Piece.Vertices.Aggregate(Vec3.Zero, (a, p) => a + p) * (1.0 / w.Piece.Vertices.Length)))
            .Sum(w => w.Exact - Measure.Volume(w.Piece));
    }

    static readonly ConditionalWeakTable<Solid, Built> Made = new();

    /// <summary>Whether the wall is written as an assembly of its segments (each a wall of its own: the wings and the face wall).</summary>
    public static bool AsAssembly(Entity e) => e.Str("ifcMapping") == "assembly";

    /// <summary>A segment's part key and its name (its own, else by where it stands: the wings at the free ends, the face wall between).</summary>
    public static (string Key, string Name) PartOf(Leg g, int count) =>
        ($"leg:{g.Index}", g.Name ?? (count >= 3 && g.Index == 0 ? "Wing" : count >= 3 && g.Index == count - 1 ? "Wing" : count >= 3 ? "Face wall" : $"Segment {g.Index + 1}"));

    /// <summary>The wall built: its pieces joined (by the booleans given; without them, laid side by side, with a note).</summary>
    public static Built Build(Entity e, IModelView m, ISolidBooleans? booleans)
    {
        var (legs, corners, notes) = Resolve(m, e);
        var pieces = Make(legs, corners);
        var all = pieces.Stem.Concat(pieces.Footing).ToList();
        // Its joints: a gap as wide as each, square to its segment; its shaped free ends, cut by their planes.
        var spec = CorridorSpec.Of(e);
        var joints = JointPlaces(e, legs);
        var cutters = joints.Select(j => Loft(j.Leg, [j.X - spec.JointWidth / 2, j.X + spec.JointWidth / 2],
            _ => Box(j.Leg.Fa - 1, j.Leg.Fb + 1, (j.Leg.KeyA != null ? j.Leg.KeyBottom : j.Leg.Bottom) - 1, Math.Max(j.Leg.Z0, j.Leg.Z1) + 1))).OfType<Solid>().ToList();
        cutters.AddRange(EndCutters(legs));
        // A portal at a tunnel lining's end: the lining's outside and its clear opening through the face wall (S4).
        cutters.AddRange(Tunnels.PortalCutters(m, e));
        Solid? solid = null;
        if (booleans != null)
        {
            // Joined and cut in one go; when that fails, joined only (a note says what was left uncut).
            try { solid = booleans.UnionLess(all, cutters, "retaining"); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                try { solid = booleans.Union(all, "retaining"); notes.Add($"The joints and the ends could not be cut: {ex.Message}"); }
                catch (Exception ex2) when (ex2 is InvalidOperationException or ArgumentException) { notes.Add($"The wall's pieces could not be joined: {ex2.Message}"); }
            }
        }
        else
        {
            notes.Add("The wall's pieces are laid side by side: this build has no booleans to join them.");
            if (legs.Any(g => g.Shaped(false) || g.Shaped(true))) notes.Add("The wall's free ends are left square: this build has no booleans to cut them.");
        }
        solid ??= all.Count > 0 ? Paths.Combine(all, "retaining") : null;
        var built = new Built { Legs = legs, Corners = corners, Pieces = pieces, Solid = solid, Booleans = booleans, Notes = notes };
        if (solid != null) Made.AddOrUpdate(solid, built);
        // As an assembly: a part per segment. A wing (the first or the last segment) is the wall from where its slope starts, a plane
        // square to it (its construction joint with the face wall), to its free end; the face wall is the rest, its corners with it;
        // segments between corners are split among them on the mitres. Each cut as the wall is; every input a piece as built.
        if (AsAssembly(e) && legs.Count > 1 && booleans != null)
        {
            try
            {
                foreach (var (leg, region, keep) in Regions(legs, corners))
                {
                    var part = booleans.UnionLess(all, [.. cutters, .. region], "retaining");
                    if (part == null || part.Faces.Length == 0) continue;
                    var (key, name) = PartOf(leg, legs.Count);
                    built.Parts.Add(new Part(leg.Index, key, name, part) { Holds = keep });
                }
                built.Parts.Sort((a, b) => a.Leg.CompareTo(b.Leg));
                foreach (var p in built.Parts) Made.AddOrUpdate(p.Solid, built);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                built.Parts.Clear();
                notes.Add($"The wall could not be split into its parts: {ex.Message}");
            }
        }
        return built;
    }

    /// <summary>
    /// Each segment's region of the wall as an assembly: what is taken away from the wall to leave it (convex cutters), and whether a
    /// point is in it. The wings from their slope's start (the segment next to them past its middle, so the other wing is not); the
    /// segments between, on the mitres at their corners, less the wings'.
    /// </summary>
    static List<(Leg Leg, List<Solid> Away, Func<Vec3, bool> Holds)> Regions(List<Leg> legs, List<Corner> corners)
    {
        var n = legs.Count;
        var big = legs.Sum(g => g.L) + legs.Max(g => g.Width) * 4 + 50;
        var (zLow, zHigh) = (legs.Min(g => g.KeyA != null ? g.KeyBottom : g.Bottom) - 5, legs.Max(g => Math.Max(g.Z0, g.Z1)) + 5);
        Solid? Keep(IEnumerable<(Vec3 N, double D)> planes)
        {
            var g = legs[0];
            Solid? box = Loft(g, [-big, big], _ => Box(-big, big, zLow, zHigh));
            foreach (var (pn, pd) in planes) box = Clip(box, pn, pd);
            return box;
        }
        // x along a leg at least (or at most) c, as a plane n·p ≤ d.
        (Vec3 N, double D) AtLeast(Leg g, double c) => (-g.D3, -(Vec3.Dot(g.D3, g.A.At(0)) + c));
        (Vec3 N, double D) AtMost(Leg g, double c) => (g.D3, Vec3.Dot(g.D3, g.A.At(0)) + c);
        static (Vec3 N, double D) Flip((Vec3 N, double D) h) => (-h.N, -h.D);
        static bool In(Vec3 p, (Vec3 N, double D) h) => Vec3.Dot(h.N, p) <= h.D + 1e-9;
        var o = new List<(Leg, List<Solid>, Func<Vec3, bool>)>();
        // The wings: the last from its slope's start, the first to its slope's end (when there are two segments, the last only).
        var wings = new List<(Leg Leg, (Vec3 N, double D) A, (Vec3 N, double D) B)>();
        // The joint 5 mm past the slope's start: off the planes the pieces meet on, so no cut runs along one or leaves a sliver.
        const double Past = JointPast;
        var last = legs[^1];
        wings.Add((last, AtLeast(last, Math.Min(last.L, last.S0 + Past)), AtLeast(legs[^2], legs[^2].L / 2)));
        if (n >= 3) wings.Add((legs[0], AtMost(legs[0], Math.Max(0, legs[0].S1 - Past)), AtMost(legs[1], legs[1].L / 2)));
        // What is not a wing's: short of its joint, and past its joint beyond the middle of the segment next to it (the two overlapping
        // by a metre, so they leave no sheet on the joint's plane between them).
        foreach (var (g, a, b) in wings)
            o.Add((g, [.. new[] { Keep([Flip(a)]), Keep([(a.N, a.D + 1), Flip(b)]) }.OfType<Solid>()], p => In(p, a) && In(p, b)));
        var wingAway = wings.Select(w => Keep([w.A, w.B])).OfType<Solid>().ToList();
        bool InWing(Vec3 p) => wings.Any(w => In(p, w.A) && In(p, w.B));
        var middle = legs.Where(g => !wings.Any(w => w.Leg == g)).ToList();
        foreach (var g in middle)
        {
            var away = new List<Solid>(wingAway);
            var planes = new List<(Vec3 N, double D)>();
            // Between the mitres at its corners with the other segments between (a wing's side is the wing's already).
            if (g.StartCorner && middle.Contains(legs[g.Index - 1])) { var m = -(legs[g.Index - 1].D3 + g.D3).Normalized(); planes.Add((m, Vec3.Dot(m, g.A.At(0)))); }
            if (g.EndCorner && middle.Contains(legs[g.Index + 1])) { var m = (g.D3 + legs[g.Index + 1].D3).Normalized(); planes.Add((m, Vec3.Dot(m, g.B.At(0)))); }
            foreach (var h in planes) if (Keep([Flip(h)]) is { } beyond) away.Add(beyond);
            o.Add((g, away, p => !InWing(p) && planes.All(h => In(p, h))));
        }
        return o;
    }

    /// <summary>The wall a body was built as (the geometry's), when it is still known.</summary>
    public static Built? Of(Solid s) => Made.TryGetValue(s, out var b) ? b : null;

    /// <summary>Where the joints fall: the leg and the distance along it.</summary>
    static List<(Leg Leg, double X)> JointPlaces(Entity e, List<Leg> legs)
    {
        var spec = CorridorSpec.Of(e);
        var c = new Chain(Chain.Clean(RetainingWalls.Path(e), null).Path, spec.StationStart);
        var o = new List<(Leg, double)>();
        foreach (var j in Corridors.Joints(c, spec))
        {
            var seg = c.SegmentAt(j);
            if (seg >= 0 && seg < legs.Count) o.Add((legs[seg], j - c.StationOf(seg)));
        }
        return o;
    }

    public static ElementGeometry Geometry(Entity e, IModelView m, IServices services)
    {
        var built = Build(e, m, services.Get<ISolidBooleans>());
        if (built.Solid is not { } solid || solid.Faces.Length == 0) return new ElementGeometry([], Diagnostics: ["The retaining wall could not be built."]);
        var material = e.Str("material");
        var color = material != null ? Materials.ColorOf(m, material) : RetainingWalls.Color;
        // As an assembly, a part per segment (each a wall of its own in IFC); else the wall whole.
        var parts = built.Parts.Count > 1
            ? built.Parts.Select(p => new GeometryPart(p.Name, color, p.Solid, material, p.Key)).ToList()
            : new List<GeometryPart> { new("Retaining wall", color, solid, material, "wall") };
        var path = built.Legs.Select(g => g.A).Append(built.Legs[^1].B).ToList();
        return new ElementGeometry(parts, Plan(e, built), [path[0], path[^1]], built.Notes.Count > 0 ? built.Notes : null);
    }

    // ------------------------------------------------------------------ plans and sections

    /// <summary>
    /// A line along the wall at an offset (by leg, along x): through each leg's samples, from the crossing at each corner (or the leg's
    /// end at a free end or a straight joint; at a shaped free end, where its plane cuts the line: at the crown, at the stem's foot or
    /// at the footing, as <paramref name="level"/> says).
    /// </summary>
    static List<Vec2> Along(Built b, string key, Func<Leg, double, double> w, string level = "crown")
    {
        var o = new List<Vec2>();
        foreach (var g in b.Legs)
        {
            var prev = g.StartCorner ? b.Corners[g.Index - 1] : null;
            var next = g.EndCorner ? b.Corners[g.Index] : null;
            if (prev?.Crossings.TryGetValue(key, out var p0) == true) { if (o.Count == 0 || (o[^1] - p0).Length > 1e-9) o.Add(p0); }
            else o.Add(FreeEnd(g, false, w, level));
            foreach (var x in AllSamples(g, b.Pieces.Warps))
                if (prev != null || x >= Cut(g, false, w, level) - 1e-12)
                    if (next != null || x <= Cut(g, true, w, level) + 1e-12) o.Add(g.P2(x, w(g, x)));
            if (next?.Crossings.TryGetValue(key, out var p1) == true) o.Add(p1);
            else o.Add(FreeEnd(g, true, w, level));
        }
        var clean = new List<Vec2>();
        foreach (var p in o) if (clean.Count == 0 || (clean[^1] - p).Length > 1e-9) clean.Add(p);
        return clean;
    }

    /// <summary>Where a free end cuts a line along the leg (its distance along it): its own end when square, else on its plane.</summary>
    static double Cut(Leg g, bool atEnd, Func<Leg, double, double> w, string level)
    {
        var xe = atEnd ? g.L : 0;
        if (!g.Shaped(atEnd)) return xe;
        var sgn = atEnd ? 1.0 : -1.0;
        var (lean, turn) = atEnd ? (g.EndLean, g.EndTurn) : (g.StartLean, g.StartTurn);
        var ze = atEnd ? g.Z1 : g.Z0;
        var drop = level switch { "base" => (ze - g.Zf) * lean, "foot" => (ze - g.Zf) * lean + FootSpare(g, atEnd), _ => 0 };
        // The line's offset where it meets the plane: past the end it keeps the end's; before it, found again from where it is (planar there).
        var x = xe;
        for (var k = 0; k < 4; k++) x = xe + sgn * (w(g, Math.Clamp(x, 0, g.L)) * turn + drop);
        return x;
    }

    static Vec2 FreeEnd(Leg g, bool atEnd, Func<Leg, double, double> w, string level)
    {
        var x = Cut(g, atEnd, w, level);
        return g.P2(x, w(g, Math.Clamp(x, 0, g.L)));
    }

    static List<PlanItem> Plan(Entity e, Built b)
    {
        var front = Along(b, "crownF", (g, _) => -g.T / 2);
        var back = Along(b, "crownB", (g, _) => g.T / 2);
        var crown = front.Concat(Enumerable.Reverse(back)).ToList();
        var plan = new List<PlanItem> { new("cut", true, crown) };
        // A leaning front is seen from above down to its foot; a leaning back is under the high ground; the footing under both.
        if (b.Legs.Any(g => g.Kf0 > 0 || g.Kf1 > 0)) plan.Add(new("outline", false, Along(b, "baseF", (g, x) => g.WF(x, g.Zf), "base")));
        if (b.Legs.Any(g => g.Kb0 > 0 || g.Kb1 > 0)) plan.Add(new("dashed", false, Along(b, "baseB", (g, x) => g.WB(x, g.Zf), "base")));
        if (b.Legs.Any(g => g.Footing))
        {
            var fa = Along(b, "footF", (g, _) => g.Fa, "foot");
            var fb = Along(b, "footB", (g, _) => g.Fb, "foot");
            plan.Add(new("dashed", true, fa.Concat(Enumerable.Reverse(fb)).ToList()));
        }
        foreach (var (g, x) in JointPlaces(e, b.Legs)) plan.Add(new("cut", false, [g.P2(x, -g.T / 2), g.P2(x, g.T / 2)]));
        // A leaning free end is seen from above down to its foot: its crown's end, then the line across its foot.
        foreach (var (g, atEnd) in new[] { (b.Legs[0], false), (b.Legs[^1], true) })
            if (g.Shaped(atEnd) && (atEnd ? g.EndLean : g.StartLean) > 1e-9)
                plan.Add(new("outline", false, [FreeEnd(g, atEnd, (l, x) => l.WF(x, l.Zf), "base"), FreeEnd(g, atEnd, (l, x) => l.WB(x, l.Zf), "base")]));
        return plan;
    }

    /// <summary>The section at a station (offset, elevation; + left as the corridor's): the stem leaning as it does there, on its footing and key.</summary>
    public static (IReadOnlyList<SectionPart> Parts, double Left, double Right) SectionAt(IModelView m, Entity e, double station)
    {
        var (legs, _, _) = Resolve(m, e);
        var spec = CorridorSpec.Of(e);
        var c = new Chain(Chain.Clean(RetainingWalls.Path(e), null).Path, spec.StationStart);
        if (legs.Count == 0) return ([], 0, 0);
        var seg = Math.Clamp(c.SegmentAt(station), 0, legs.Count - 1);
        var g = legs[seg];
        var x = Math.Clamp(station - c.StationOf(seg), 0, g.L);
        var top = g.Top(x);
        var ring = new List<Vec2>();
        if (g.Footing)
        {
            ring.Add(new(g.Fa, g.Bottom));
            if (g.KeyA is { } ka && g.KeyB is { } kb) { ring.Add(new(ka, g.Bottom)); ring.Add(new(ka, g.KeyBottom)); ring.Add(new(kb, g.KeyBottom)); ring.Add(new(kb, g.Bottom)); }
            ring.Add(new(g.Fb, g.Bottom));
            ring.Add(new(g.Fb, g.Zf));
        }
        ring.Add(new(g.WB(x, g.Zf), g.Zf));
        ring.Add(new(g.T / 2, top));
        ring.Add(new(-g.T / 2, top));
        ring.Add(new(g.WF(x, g.Zf), g.Zf));
        if (g.Footing) ring.Add(new(g.Fa, g.Zf));
        var clean = new List<Vec2>();
        foreach (var p in ring) if (clean.Count == 0 || (clean[^1] - p).Length > 1e-9) clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-9) clean.RemoveAt(clean.Count - 1);
        // Offsets are + towards the high ground; the corridor's are + left.
        var left = RetainingWalls.Of(e).HighOnLeft;
        var poly = left ? clean : Enumerable.Reverse(clean).Select(p => new Vec2(-p.X, p.Y)).ToList();
        return ([new("wall", poly)], poly.Max(p => p.X), poly.Min(p => p.X));
    }

    // ------------------------------------------------------------------ quantities

    /// <summary>
    /// The areas of the body's faces by what they are (front, back, crown, end, footing…), the warped faces at their true area. A face
    /// one of <paramref name="others"/> (the assembly's other parts) has on the same plane facing it is where two parts meet: no
    /// formwork, left out.
    /// </summary>
    public static Dictionary<string, double> Areas(Built b, Solid s, IReadOnlyList<Solid>? others = null)
    {
        var o = new Dictionary<string, double>();
        void Add(string k, double a) => o[k] = o.GetValueOrDefault(k) + a;
        var surfaces = b.Pieces.Warps.Select(w => (Warp: w, Surface: new SplineSurface(w.Patch))).ToList();
        var zf = b.Legs.Max(g => g.Zf);
        var seen = new HashSet<Warp>();
        for (var f = 0; f < s.Faces.Length; f++)
        {
            var face = s.Faces[f];
            var p0 = s.Vertices[face.Outer[0]];
            var n = face.Normal;
            var d = Vec3.Dot(n, p0);
            var area = Measure.FaceArea(s, f);
            // Where it meets another part is inside the wall: only the rest of the face is formwork.
            if (others != null) area = Math.Max(0, area - Contact(s, f, others));
            if (area < 1e-9) continue;
            var label = b.Pieces.Planes.FirstOrDefault(pl => Vec3.Dot(pl.N, n) > 1 - 1e-7 && Math.Abs(pl.D - d) < 1e-6)?.Label;
            if (label == null)
            {
                var pts = face.Outer.Select(i => s.Vertices[i]).ToList();
                var on = surfaces.FirstOrDefault(x => pts.All(p => x.Surface.Distance(p) < 1e-6)).Warp;
                if (on != null) { seen.Add(on); continue; } // counted whole below, exactly
                label = "other";
            }
            // An end face below the footing's top is the footing's.
            if (label is "end" or "step" && face.Outer.All(i => s.Vertices[i].Z <= zf + 1e-6)) label = "footing";
            Add(label, area);
        }
        // Each warped face this body has, whole (a part has only its own leg's).
        foreach (var (w, _) in surfaces) if (others == null || seen.Contains(w)) Add(w.Front ? "front" : "back", TrueArea(w.Patch));
        return o;
    }

    /// <summary>
    /// How much of face f of s lies on faces of the others facing it (the plane the same, the normals opposite): where two parts of the
    /// assembly meet. Their triangles overlapped exactly (each pair of triangles clipped one by the other in the plane).
    /// </summary>
    public static double Contact(Solid s, int f, IReadOnlyList<Solid> others)
    {
        var face = s.Faces[f];
        var n = face.Normal;
        var d = Vec3.Dot(n, s.Vertices[face.Outer[0]]);
        var (u, v) = VecMath.PlaneBasis(n);
        Vec2 P(Vec3 x) => new(Vec3.Dot(x, u), Vec3.Dot(x, v));
        List<List<Vec2>> Tris(Solid x, Face g) =>
            [.. Triangulator.Face(x.Vertices, g.Outer, g.Holes, g.Normal).Select(t => Polygon2.EnsureCcw([P(x.Vertices[t.A]), P(x.Vertices[t.B]), P(x.Vertices[t.C])]).ToList())];
        List<List<Vec2>>? mine = null;
        var sum = 0.0;
        foreach (var other in others)
        {
            if (ReferenceEquals(other, s)) continue;
            foreach (var g in other.Faces)
            {
                if (Vec3.Dot(g.Normal, n) > -1 + 1e-6) continue;
                if (Math.Abs(Vec3.Dot(g.Normal, other.Vertices[g.Outer[0]]) + d) > 1e-5) continue;
                mine ??= Tris(s, face);
                foreach (var t in Tris(other, g))
                    foreach (var a in mine)
                    {
                        // a clipped by each edge of t (both anticlockwise): what is left is their overlap.
                        IReadOnlyList<Vec2> ring = a;
                        for (var k = 0; k < 3 && ring.Count >= 3; k++)
                        {
                            var (p0, p1) = (t[k], t[(k + 1) % 3]);
                            ring = Polygon2.ClipHalf(ring, q => Vec2.Cross(p1 - p0, q - p0));
                        }
                        if (ring.Count >= 3) sum += Math.Abs(Polygon2.SignedArea(ring));
                    }
            }
        }
        return sum;
    }

    /// <summary>A body's measures as a quantity set: its concrete, its formwork by face, its crown.</summary>
    static List<Quantity> Measures(Dictionary<string, double> areas, double concrete)
    {
        double A(string k) => areas.GetValueOrDefault(k);
        var (front, back, ends, footing) = (A("front"), A("back"), A("end") + A("step") + A("other"), A("footing"));
        return
        [
            new("ConcreteVolume", concrete, "VOLUME", "Concrete"), new("GrossVolume", concrete, "VOLUME"), new("NetVolume", concrete, "VOLUME"),
            new("FormworkArea", front + back + ends + footing, "AREA", "Formwork"),
            new("FormworkFront", front, "AREA", "Formwork, front face"), new("FormworkBack", back, "AREA", "Formwork, back face"),
            new("FormworkEnds", ends, "AREA", "Formwork, ends"), new("FormworkFooting", footing, "AREA", "Formwork, footing"),
            new("CrownArea", A("crown"), "AREA", "Crown"),
        ];
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry g, IModelView m)
    {
        var first = g.Parts.FirstOrDefault(p => p.Key == "wall") ?? g.Parts.FirstOrDefault(p => p.Key?.StartsWith("leg:", StringComparison.Ordinal) == true);
        if (first == null) yield break;
        var built = Of(first.Solid) ?? Build(e, m, null);
        var solid = built.Solid ?? first.Solid;
        var length = built.Legs.Sum(x => x.L);
        var concrete = Measure.Volume(solid) + built.Correction;
        var stem = built.StemVolume();
        var areas = Areas(built, solid);
        var joints = JointPlaces(e, built.Legs).Count;
        var material = e.Str("material") is { } mid && m.Get(mid) is { Class: IfcClass.Material } ? mid : null;
        var height = built.Legs.Max(x => Math.Max(x.Z0, x.Z1) - x.Bottom);
        var main = new List<Quantity>
        {
            new("Length", length, "LENGTH"), new("MaxHeight", height, "LENGTH", "Height, footing included"),
        };
        main.AddRange(Measures(areas, concrete).Take(3));
        main.Add(new("StemVolume", stem, "VOLUME", "Stem"));
        main.Add(new("FootingVolume", Math.Max(0, concrete - stem), "VOLUME", "Footing"));
        main.AddRange(Measures(areas, concrete).Skip(3));
        main.Add(new("Joints", joints, "COUNT", "Joints"));
        // As an assembly: each part's own measures (where it meets another part is no formwork), and on the wall the face wall's and the
        // wings' together, so a schedule's columns can show them.
        var parts = built.Parts.Where(p => g.Parts.Any(x => ReferenceEquals(x.Solid, p.Solid))).ToList();
        var sets = new List<QuantitySet>();
        if (parts.Count > 1)
        {
            var solids = parts.Select(p => p.Solid).ToList();
            double faceV = 0, wingV = 0, faceF = 0, wingF = 0;
            foreach (var p in parts)
            {
                var leg = built.Legs[p.Leg];
                var measures = Measures(Areas(built, p.Solid, solids), Measure.Volume(p.Solid) + built.CorrectionOf(p));
                var v = measures.First(x => x.Name == "ConcreteVolume").Value;
                var fw = measures.First(x => x.Name == "FormworkArea").Value;
                if (leg.StartCorner && leg.EndCorner) { faceV += v; faceF += fw; } else { wingV += v; wingF += fw; }
                sets.Add(new QuantitySet("Lapin_RetainingWallQuantities", [new("Length", leg.L, "LENGTH"), .. measures], Part: p.Key, PartName: p.Name, Material: material));
            }
            main.Add(new("FaceWallVolume", faceV, "VOLUME", "Concrete, face wall"));
            main.Add(new("WingsVolume", wingV, "VOLUME", "Concrete, wings"));
            main.Add(new("FaceWallFormwork", faceF, "AREA", "Formwork, face wall"));
            main.Add(new("WingsFormwork", wingF, "AREA", "Formwork, wings"));
        }
        yield return new QuantitySet("Lapin_RetainingWallQuantities", main, Material: material);
        foreach (var x in sets) yield return x;
    }

    // ------------------------------------------------------------------ exactly

    /// <summary>
    /// The wall exactly, when a face of it warps (its planes and its B-spline patches; a wall of planes is exact as its facets): read off
    /// its body, the edges between a patch and a plane the curves through their points at their distance along the leg (the base's
    /// parabola, reproduced exactly by a quadratic).
    /// </summary>
    public static ExactBody? Exact(Built b, Solid body, double tolerance)
    {
        if (b.Pieces.Warps.Count == 0) return null;
        var surfaces = b.Pieces.Warps.Select(w => (ExactSurface)new SplineSurface(w.Patch)).ToList();
        ExactCurve? Curve(IReadOnlyList<Vec3> pts)
        {
            for (var k = 0; k < surfaces.Count; k++)
            {
                if (!pts.All(p => surfaces[k].Distance(p) < 1e-6)) continue;
                var along = b.Pieces.Warps[k].Along;
                var list = pts.ToList();
                var t = list.Select(p => Vec3.Dot(p, along)).ToList();
                if (t[^1] < t[0]) { list.Reverse(); t.Reverse(); }
                for (var i = 1; i < t.Count; i++) if (t[i] <= t[i - 1] + 1e-12) return null;
                return new SplineCurve(BSplineCurve.Interpolate(list, 2, t));
            }
            return null;
        }
        var raw = ExactRegions.Of(body, surfaces, 1e-6, Curve);
        return raw == null ? null : ExactCheck.Fit(raw, body, tolerance).Body;
    }
}

/// <summary>A battered retaining wall's exact body for the IFC (IfcAdvancedBrep) and STEP exports, when a face of it warps.</summary>
sealed class RetainingWallExactBodies : IIfcExactBodies
{
    public ExactBody? ExactOf(IModelView model, IServices services, Entity element, Solid body)
    {
        if (element.Class != RetainingWalls.Class || !BatteredWalls.Is(element)) return null;
        var built = BatteredWalls.Of(body) ?? BatteredWalls.Build(element, model, services.Get<ISolidBooleans>());
        return BatteredWalls.Exact(built, body, 0.003);
    }

    /// <summary>A wing or the face wall of a wall written as an assembly: its own body exactly, when it warps.</summary>
    public ExactBody? ExactOfPart(IModelView model, IServices services, Entity element, string key, Solid body)
    {
        if (element.Class != RetainingWalls.Class || !key.StartsWith("leg:", StringComparison.Ordinal)) return null;
        var built = BatteredWalls.Of(body) ?? BatteredWalls.Build(element, model, services.Get<ISolidBooleans>());
        // A wing whose face warps; the face wall (the corners, a few millimetres of the wings' warped faces at their joints) as its facets.
        var part = built.Parts.FirstOrDefault(p => p.Key == key);
        if (part == null || !built.Pieces.Warps.Any(w => w.Leg == part.Leg)) return null;
        return BatteredWalls.Exact(built, body, 0.003);
    }
}

/// <summary>A wall's path with one segment's length or turn changed, the segments after it keeping their lengths and turns.</summary>
public static class RetainingWallPaths
{
    /// <param name="turn">Degrees from the segment before, + to the left; for the first segment, its direction from east.</param>
    public static List<Vec2> WithSegment(IReadOnlyList<Vec2> path, int index, double? length, double? turn)
    {
        var n = path.Count - 1;
        var lengths = new double[n];
        var dirs = new double[n];
        for (var k = 0; k < n; k++) { var d = path[k + 1] - path[k]; lengths[k] = d.Length; dirs[k] = Math.Atan2(d.Y, d.X); }
        var turns = new double[n];
        for (var k = 1; k < n; k++) turns[k] = dirs[k] - dirs[k - 1];
        if (length is { } l) lengths[index] = l;
        if (turn is { } t) { if (index == 0) dirs[0] = t * Math.PI / 180; else turns[index] = t * Math.PI / 180; }
        var o = new List<Vec2> { path[0] };
        var at = dirs[0];
        for (var k = 0; k < n; k++)
        {
            if (k > 0) at += turns[k];
            o.Add(o[^1] + new Vec2(Math.Cos(at), Math.Sin(at)) * lengths[k]);
        }
        return o;
    }
}
