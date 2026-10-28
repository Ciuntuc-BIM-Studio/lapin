using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall's top under what is over it: Lapin's own roofs (<see cref="RoofTop"/>,
/// by their solved planes) and any other body the wall is attached to — a Studio
/// solid, a slab, a beam, an element imported from IFC, a roof a file brought
/// without Lapin's rules — read from the body's underside: straight up from the
/// wall's base along its axis, the lowest face that looks down. In <c>auto</c>
/// the storey's roofs count, imported ones with them; attached, the elements
/// named (<c>roofIds</c>), else every roof. Where the two kinds overlap the
/// lower one wins.
/// </summary>
public static class WallTops
{
    /// <summary>The wall's top as (u along the axis, v above its base), or null when nothing over it changes it.</summary>
    public static List<(double U, double V)>? Profile(Entity wall, RoofTop.WallAxis w, IModelView m, IServices? services)
    {
        var roofs = RoofTop.Profile(wall, w, m);
        var bodies = Bodies(wall, m, services);
        if (bodies.Count == 0) return roofs;
        var under = Underside(bodies, w);
        return under == null ? roofs : roofs == null ? under : Merge(roofs, under);
    }

    /// <summary>A triangle of a body with its plan box, and whether it looks down (an underside).</summary>
    sealed record Tri(Vec3 A, Vec3 B, Vec3 C, double X0, double X1, double Y0, double Y1, bool Down);

    /// <summary>A body's triangles, kept per entity (immutable) and model revision: every wall under it asks for them.</summary>
    static readonly ConditionalWeakTable<Entity, Tuple<long, List<Tri>>> Cache = new();

    /// <summary>The triangles of the bodies the wall rises to, besides Lapin's own roofs.</summary>
    static List<Tri> Bodies(Entity wall, IModelView m, IServices? services)
    {
        var mode = wall.Str("roofTop") ?? "auto";
        if (mode == "none" || services == null) return [];
        IEnumerable<Entity> targets;
        if (mode == "attach")
        {
            var named = wall.Attrs["roofIds"] is JsonArray ids ? ids.Select(x => (string?)x).OfType<string>().Select(m.Get).OfType<Entity>().ToList() : null;
            targets = named is { Count: > 0 } ? named : m.OfClass(IfcClass.Roof);
        }
        else targets = RoofTop.RoofsOver(wall.Id, m);
        var o = new List<Tri>();
        foreach (var t in targets)
        {
            if (t.Class == IfcClass.Wall || t.Id == wall.Id) continue;
            // Lapin's own roofs are read by their planes (RoofTop), not by their covering.
            if (t.Class == IfcClass.Roof && !StoredBodies.Has(t) && Roof.Resolve(t, m) != null) continue;
            o.AddRange(TrianglesOf(t, m, services));
        }
        return o;
    }

    static List<Tri> TrianglesOf(Entity t, IModelView m, IServices services)
    {
        if (Cache.TryGetValue(t, out var hit) && hit.Item1 == m.Revision) return hit.Item2;
        var producer = StoredBodies.Has(t) ? new GeometryProducer(StoredBodies.Geometry) : services.Get<IGeometryProducers>()?.Geometry(t.Class);
        var list = new List<Tri>();
        foreach (var part in producer?.Invoke(t, m, services)?.Parts ?? [])
        {
            var v = part.Solid.Vertices;
            foreach (var (a, b, c) in Measure.Triangles(part.Solid))
            {
                var (p, q, r) = (v[a], v[b], v[c]);
                var nz = Vec3.Cross(q - p, r - p).Z;
                if (Math.Abs(nz) < 1e-12) continue;   // vertical: never an underside
                list.Add(new Tri(p, q, r, Math.Min(p.X, Math.Min(q.X, r.X)), Math.Max(p.X, Math.Max(q.X, r.X)), Math.Min(p.Y, Math.Min(q.Y, r.Y)), Math.Max(p.Y, Math.Max(q.Y, r.Y)), nz < 0));
            }
        }
        Cache.AddOrUpdate(t, Tuple.Create(m.Revision, list));
        return list;
    }

    /// <summary>The triangle's height over a plan point, or null when the point is not under it.</summary>
    static double? ZAt(Tri t, Vec2 p)
    {
        var d = t.B.Xy - t.A.Xy;
        var e = t.C.Xy - t.A.Xy;
        var den = Vec2.Cross(d, e);
        if (Math.Abs(den) < 1e-14) return null;
        var q = p - t.A.Xy;
        var l1 = Vec2.Cross(q, e) / den;
        var l2 = Vec2.Cross(d, q) / den;
        if (l1 < -1e-9 || l2 < -1e-9 || l1 + l2 > 1 + 1e-9) return null;
        return t.A.Z + l1 * (t.B.Z - t.A.Z) + l2 * (t.C.Z - t.A.Z);
    }

    /// <summary>
    /// The bodies' underside along the axis, sampled every few centimetres from
    /// the wall's base up (the wall's own height where nothing is over it); a
    /// jump between two samples — the body's end, a vertical face — is found to
    /// a millimetre and kept as a step. Null when the top stays the wall's own.
    /// </summary>
    static List<(double U, double V)>? Underside(List<Tri> tris, RoofTop.WallAxis w)
    {
        var (L, H) = (w.Length, w.Height);
        if (L < 1e-6) return null;
        var (x0, x1) = (Math.Min(w.A.X, w.B.X) - 1e-6, Math.Max(w.A.X, w.B.X) + 1e-6);
        var (y0, y1) = (Math.Min(w.A.Y, w.B.Y) - 1e-6, Math.Max(w.A.Y, w.B.Y) + 1e-6);
        var near = tris.Where(t => t.Down && t.X1 >= x0 && t.X0 <= x1 && t.Y1 >= y0 && t.Y0 <= y1).ToList();
        if (near.Count == 0) return null;
        double? Under(double u)
        {
            var p = w.A + w.Dir * u;
            var best = double.PositiveInfinity;
            foreach (var t in near)
            {
                if (p.X < t.X0 || p.X > t.X1 || p.Y < t.Y0 || p.Y > t.Y1) continue;
                if (ZAt(t, p) is { } z && z > w.Base + 1e-6 && z < best) best = z;
            }
            return double.IsPositiveInfinity(best) ? null : best - w.Base;
        }
        var step = Math.Clamp(L / 200, 0.02, 0.1);
        var n = Math.Max(1, (int)Math.Ceiling(L / step - 1e-9));
        var samples = new List<(double U, double V)>();
        for (var i = 0; i <= n; i++) { var u = Math.Min(L, i * step); samples.Add((u, Under(u) ?? H)); }
        var profile = new List<(double U, double V)> { samples[0] };
        for (var i = 1; i < samples.Count; i++)
        {
            var (p, q) = (samples[i - 1], samples[i]);
            if (Math.Abs(q.V - p.V) > Math.Max(0.1, 2.5 * step))
            {
                var (lo, hi, vLo, vHi) = (p.U, q.U, p.V, q.V);
                for (var k = 0; k < 12 && hi - lo > 1e-3; k++)
                {
                    var mid = (lo + hi) / 2;
                    var vm = Under(mid) ?? H;
                    if (Math.Abs(vm - vLo) <= Math.Abs(vm - vHi)) (lo, vLo) = (mid, vm); else (hi, vHi) = (mid, vm);
                }
                var at = (lo + hi) / 2;
                profile.Add((at, vLo));
                profile.Add((at, vHi));
            }
            profile.Add(q);
        }
        if (profile.All(p => Math.Abs(p.V - H) < 1e-6)) return null;
        return Simplify(profile);
    }

    /// <summary>The value of a profile at u: linear between its points, flat past its ends (a step is read a hair to either side).</summary>
    static double Eval(List<(double U, double V)> p, double u)
    {
        if (u <= p[0].U) return p[0].V;
        if (u >= p[^1].U) return p[^1].V;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var (a, b) = (p[i], p[i + 1]);
            if (b.U - a.U < 1e-12 || u < a.U || u > b.U) continue;
            return a.V + (b.V - a.V) * (u - a.U) / (b.U - a.U);
        }
        return p[^1].V;
    }

    /// <summary>The lower of two profiles everywhere: at both their points, and where they cross.</summary>
    static List<(double U, double V)> Merge(List<(double U, double V)> a, List<(double U, double V)> b)
    {
        var us = new SortedSet<double>(a.Select(p => p.U).Concat(b.Select(p => p.U)));
        for (var i = 0; i + 1 < a.Count; i++)
            for (var j = 0; j + 1 < b.Count; j++)
            {
                var (a0, a1, b0, b1) = (a[i], a[i + 1], b[j], b[j + 1]);
                if (a1.U - a0.U < 1e-9 || b1.U - b0.U < 1e-9) continue;
                var (lo, hi) = (Math.Max(a0.U, b0.U), Math.Min(a1.U, b1.U));
                if (hi - lo < 1e-9) continue;
                var (ka, kb) = ((a1.V - a0.V) / (a1.U - a0.U), (b1.V - b0.V) / (b1.U - b0.U));
                if (Math.Abs(ka - kb) < 1e-12) continue;
                var u = ((b0.V - kb * b0.U) - (a0.V - ka * a0.U)) / (ka - kb);
                if (u > lo + 1e-9 && u < hi - 1e-9) us.Add(u);
            }
        const double hair = 1e-7;
        var o = new List<(double U, double V)>();
        foreach (var u in us)
        {
            var l = Math.Min(Eval(a, u - hair), Eval(b, u - hair));
            var r = Math.Min(Eval(a, u + hair), Eval(b, u + hair));
            if (Math.Abs(l - r) > 1e-3) { o.Add((u, l)); o.Add((u, r)); }
            else o.Add((u, Math.Min(Eval(a, u), Eval(b, u))));
        }
        return Simplify(o);
    }

    /// <summary>Drop the points within half a millimetre of the line between their neighbours.</summary>
    static List<(double U, double V)> Simplify(List<(double U, double V)> p)
    {
        var o = new List<(double U, double V)>();
        foreach (var q in p)
        {
            if (o.Count > 0 && Math.Abs(o[^1].U - q.U) < 1e-9 && Math.Abs(o[^1].V - q.V) < 1e-9) continue;
            while (o.Count >= 2 && Deviation(o[^2], o[^1], q) < 5e-4) o.RemoveAt(o.Count - 1);
            o.Add(q);
        }
        return o;
    }

    /// <summary>How far b stands off the segment a–c.</summary>
    static double Deviation((double U, double V) a, (double U, double V) b, (double U, double V) c)
    {
        var (dx, dy) = (c.U - a.U, c.V - a.V);
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-18) return Math.Sqrt((b.U - a.U) * (b.U - a.U) + (b.V - a.V) * (b.V - a.V));
        var t = Math.Clamp(((b.U - a.U) * dx + (b.V - a.V) * dy) / len2, 0, 1);
        var (px, py) = (a.U + dx * t - b.U, a.V + dy * t - b.V);
        return Math.Sqrt(px * px + py * py);
    }
}
