using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// Where a wall's top meets the roof of its storey (BubbleBIM Pro's RoofTop,
/// itself Standalone's roof trim), as a profile along the wall:
/// <list type="bullet">
/// <item>a wall under a GABLE edge of the roof rises to the roof's underside: the gable is the wall's own top;</item>
/// <item>a wall under an EAVE edge of a framed roof carries its wall plate: it stops under it;</item>
/// <item>any other wall under the roof stops where the roof is lower than its top;</item>
/// <item>a wall the roof does not cover keeps its height.</item>
/// </list>
/// The roof's underside is read along the wall's axis (the line the roof's
/// outline usually follows). Every wall on the roof's storey follows it: there
/// is no attach command (decision in decisions-pending).
/// </summary>
public static class RoofTop
{
    /// <summary>A wall as the roof sees it: its axis from A along Dir, its base (absolute) and height, its thickness.</summary>
    public sealed record WallAxis(Vec2 A, Vec2 Dir, double Length, double Base, double Height, double Thickness)
    {
        public Vec2 B => A + Dir * Length;
    }

    /// <summary>A roof's solved shape, cached per roof (an entity is immutable) and model revision: every wall of the storey asks for it.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Entity, Tuple<long, RoofShape?>> Cache = new();

    public static RoofShape? ShapeOf(Entity roof, IModelView m)
    {
        if (Cache.TryGetValue(roof, out var hit) && hit.Item1 == m.Revision) return hit.Item2;
        var r = Roof.Resolve(roof, m);
        var shape = r == null || r.Flat ? null : Roof.Solve(r).Shape;
        Cache.AddOrUpdate(roof, Tuple.Create(m.Revision, shape));
        return shape;
    }

    /// <summary>The roofs over an element's storey.</summary>
    public static IEnumerable<Entity> RoofsOver(string elementId, IModelView m)
    {
        var storey = Spatial.StoreyOf(m, elementId)?.Id;
        return storey == null ? [] : m.OfClass(IfcClass.Roof).Where(r => Spatial.StoreyOf(m, r.Id)?.Id == storey);
    }

    /// <summary>
    /// The wall's top as (u along the wall, v above its base), linear between
    /// points, or null when no roof changes it. Two points at one u are a step.
    /// </summary>
    public static List<(double U, double V)>? Profile(Entity wall, WallAxis w, IModelView m)
    {
        var pieces = new List<Piece>();
        bool attach = false;
        var H = w.Height;
        foreach (var roof in RoofsOver(wall.Id, m))
        {
            var shape = ShapeOf(roof, m);
            if (shape == null) continue;
            if (UnderEdge(w, shape, EdgeKind.Gable)) attach = true;
            else if (UnderEdge(w, shape, EdgeKind.Eave) && Roof.Resolve(roof, m) is { Framed: true } r) H = Math.Min(H, r.PlateZ - w.Base);
            foreach (var p in shape.Planes) pieces.AddRange(Pieces(w.A, w.Dir, w.Length, p, w.Base));
        }
        if (pieces.Count == 0) return null;

        var L = w.Length;
        var us = new SortedSet<double> { 0, L };
        foreach (var p in pieces)
        {
            us.Add(p.U0); us.Add(p.U1);
            // Where a slope passes the wall's own top: the cut starts or stops there.
            if (Math.Abs(p.K) > 1e-12 && (H - p.Z0) / p.K is var uh && uh > p.U0 && uh < p.U1) us.Add(uh);
        }
        for (int i = 0; i < pieces.Count; i++)
            for (int j = i + 1; j < pieces.Count; j++)
                if (pieces[i].Cross(pieces[j]) is { } u) us.Add(u);

        double Env(double u)
        {
            var best = double.PositiveInfinity;
            foreach (var p in pieces) if (u >= p.U0 - 1e-6 && u <= p.U1 + 1e-6) best = Math.Min(best, p.At(u));
            return best;
        }
        double Top(double env) => double.IsPositiveInfinity(env) ? H : attach ? env : Math.Min(H, env);

        var profile = new List<(double U, double V)>();
        var breaks = new List<double>();
        foreach (var u in us.Where(u => u >= 0 && u <= L))
            if (breaks.Count == 0 || u - breaks[^1] > 1e-6) breaks.Add(u); else breaks[^1] = u >= L ? L : breaks[^1];
        foreach (var u in breaks)
        {
            // Left and right of a breakpoint, within the wall: a step where the roof's cover starts or stops.
            var (l, r) = (Top(Env(Math.Max(u - 1e-5, 1e-9))), Top(Env(Math.Min(u + 1e-5, L - 1e-9))));
            var c = Top(Env(u));
            if (Math.Abs(l - r) > 1e-3) { profile.Add((u, l)); profile.Add((u, r)); }
            else profile.Add((u, c));
        }
        if (profile.All(p => Math.Abs(p.V - w.Height) < 1e-9)) return null;
        return Simplify(profile);
    }

    /// <summary>Whether the wall stands on an edge of the roof of that kind: along it, within its thickness.</summary>
    static bool UnderEdge(WallAxis w, RoofShape s, EdgeKind kind)
    {
        for (int edge = 0; edge < s.Outline.Count; edge++)
        {
            if (s.Rules[edge].Kind != kind) continue;
            var a = s.Outline[edge]; var b = s.Outline[(edge + 1) % s.Outline.Count];
            var d = (b - a).Normalized();
            if (Math.Abs(Vec2.Cross(d, w.Dir)) > 0.02) continue;
            var tol = w.Thickness / 2 + 0.05;
            double Off(Vec2 p) => Math.Abs(Vec2.Cross(d, p - a));
            if (Off(w.A) > tol || Off(w.B) > tol) continue;
            double t0 = Vec2.Dot(w.A - a, d), t1 = Vec2.Dot(w.B - a, d), len = (b - a).Length;
            if (Math.Min(Math.Max(t0, t1), len) - Math.Max(Math.Min(t0, t1), 0) > 0.1) return true;
        }
        return false;
    }

    /// <summary>A plane's height over part of a line: z(u) = Z0 + K·u for u in [U0, U1] (above the wall's base).</summary>
    sealed record Piece(double U0, double U1, double Z0, double K)
    {
        public double At(double u) => Z0 + K * u;
        public double? Cross(Piece o)
        {
            if (Math.Abs(K - o.K) < 1e-12) return null;
            var u = (o.Z0 - Z0) / (K - o.K);
            return u > Math.Max(U0, o.U0) && u < Math.Min(U1, o.U1) ? u : null;
        }
    }

    /// <summary>The parts of the line o + dir·u (u in 0 … L) over a plane's plan polygon, with the plane's height there.</summary>
    static IEnumerable<Piece> Pieces(Vec2 o, Vec2 dir, double length, RoofPlane plane, double baseZ)
    {
        var poly = plane.Points.Select(p => p.Xy).ToList();
        var ts = new List<double> { 0, length };
        for (int i = 0; i < poly.Count; i++)
        {
            var (a, b) = (poly[i], poly[(i + 1) % poly.Count]);
            var e = b - a;
            var den = Vec2.Cross(dir, e);
            if (Math.Abs(den) < 1e-12) continue;
            var t = Vec2.Cross(a - o, e) / den;
            var s = Vec2.Cross(a - o, dir) / den;
            if (s >= -1e-9 && s <= 1 + 1e-9 && t > 0 && t < length) ts.Add(t);
        }
        ts.Sort();
        var z = RoofSolver.PlaneZ(plane.Points);
        for (int i = 0; i + 1 < ts.Count; i++)
        {
            var (t0, t1) = (ts[i], ts[i + 1]);
            if (t1 - t0 < 1e-9) continue;
            var mid = o + dir * ((t0 + t1) / 2);
            if (!Polygon2.Contains(poly, mid)) continue;
            double z0 = z(o) - baseZ, k = z(o + dir) - z(o);
            yield return new Piece(t0, t1, z0, k);
        }
    }

    /// <summary>Drop points in line with their neighbours.</summary>
    static List<(double U, double V)> Simplify(List<(double U, double V)> p)
    {
        var o = new List<(double U, double V)>();
        foreach (var q in p)
        {
            if (o.Count > 0 && Math.Abs(o[^1].U - q.U) < 1e-7 && Math.Abs(o[^1].V - q.V) < 1e-7) continue;
            while (o.Count >= 2)
            {
                var (a, b) = (o[^2], o[^1]);
                if (Math.Abs(b.U - a.U) < 1e-12 || Math.Abs(q.U - b.U) < 1e-12) break;
                var cross = (b.U - a.U) * (q.V - a.V) - (b.V - a.V) * (q.U - a.U);
                if (Math.Abs(cross) > 1e-9) break;
                o.RemoveAt(o.Count - 1);
            }
            o.Add(q);
        }
        return o;
    }
}
