using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis.Solver;

/// <summary>What the supports' springs came to (§14.2): how many supports and nodes, on springs, on Winkler's bed, lifting.</summary>
public sealed record SpringSummary(int Supports, int Nodes, int Winkler, int Slabs, int SlabNodes, int CompressionOnly);

public sealed partial class StructuralProblem
{
    public SpringSummary? Springs { get; private set; }

    /// <summary>
    /// The supports' springs (docs/plan-fem.md §14.2) on the solver's nodes: each node a support holds (its own, or a shell
    /// wall's foot along it) takes the support's <see cref="SupportSettings"/>: Winkler's bed under a pad footing (kz = ks·A,
    /// the footprint's ks·I about x and y, shared by the nodes it holds), under a strip (each node its length along it:
    /// kz = ks·B·l, ks·l·B³/12 about the strip's axis and ks·B·l³/12 across it), the springs given (each replacing the hold of
    /// its degree of freedom), and compression only (lifting). A slab on the ground: kz = ks·Aᵢ at each of its nodes (Aᵢ the
    /// elements' consistent shares), ux and uy held there.
    /// </summary>
    void SupportSprings(IModelView m)
    {
        var p = Problem;
        var settings = new Dictionary<string, SupportSettings?>();
        SupportSettings? Of(string id) => settings.TryGetValue(id, out var s) ? s : settings[id] = SupportSettings.Of(m.Get(id));
        var supports = Model.Nodes.Select(n => n.Support).OfType<AnalyticalSupport>().Distinct().Where(s => Of(s.SourceId) != null).ToList();
        var nodesOf = new Dictionary<AnalyticalSupport, List<int>>();
        var anode = NodeIndex.ToDictionary(x => x.Value, x => x.Key);
        if (supports.Count > 0)
            for (var n = 0; n < p.Nodes.Count; n++)
            {
                if (p.Held[n] == null) continue;
                var sup = anode.TryGetValue(n, out var a) && a.Support is { } own ? own : supports.FirstOrDefault(s => s.ForFrame == null && Under(s, p.Nodes[n]));
                if (sup == null || !supports.Contains(sup)) continue;
                if (!nodesOf.TryGetValue(sup, out var list)) nodesOf[sup] = list = [];
                list.Add(n);
            }
        var (count, winkler, lifting) = (0, 0, 0);
        var noFootprint = new List<string>();
        foreach (var (sup, nodes) in nodesOf)
        {
            var st = Of(sup.SourceId)!;
            count += nodes.Count;
            // A strip's nodes: each its length along the line, half way to its neighbours, the ends to the strip's.
            var lengths = new Dictionary<int, double>();
            Vec2 dir = default;
            if (st.Winkler != null && sup.LineA is { } la && sup.LineB is { } lb)
            {
                var len = (lb - la).Length;
                dir = len > 1e-9 ? (lb - la) * (1 / len) : new Vec2(1, 0);
                var along = nodes.Select(n => (n, T: Math.Clamp(Vec2.Dot(p.Nodes[n].Xy - la, dir), 0, len))).OrderBy(x => x.T).ToList();
                for (var i = 0; i < along.Count; i++)
                {
                    var lo = i == 0 ? 0 : (along[i - 1].T + along[i].T) / 2;
                    var hi = i == along.Count - 1 ? len : (along[i].T + along[i + 1].T) / 2;
                    lengths[along[i].n] = Math.Max(0, hi - lo);
                }
            }
            if (st.Winkler != null && sup.Region == null && sup.LineA == null) noFootprint.Add(sup.Name);
            foreach (var n in nodes)
            {
                var held = (bool[])p.Held[n]!.Clone();
                var k = new double[36];
                if (st.Winkler is { } ks)
                {
                    if (sup.Region is { Count: >= 3 } region)
                    {
                        var pad = Footprint(region);
                        var share = 1.0 / nodes.Count;
                        // A turn θx sinks the soil by y·θx, a turn θy by −x·θy (about the footprint's centroid): ks [∫y², −∫xy; −∫xy, ∫x²].
                        k[2 * 6 + 2] = ks * pad.Area * share;
                        k[3 * 6 + 3] = ks * pad.Ixx * share;
                        k[4 * 6 + 4] = ks * pad.Iyy * share;
                        k[3 * 6 + 4] = k[4 * 6 + 3] = -ks * pad.Ixy * share;
                        held[2] = held[3] = held[4] = false;
                        winkler++;
                    }
                    else if (lengths.TryGetValue(n, out var l))
                    {
                        var b = sup.Width;
                        k[2 * 6 + 2] = ks * b * l;
                        // About the strip's axis d and across it e (both level): kd d dᵀ + ke e eᵀ on (rx, ry).
                        var (d, e) = (dir, new Vec2(-dir.Y, dir.X));
                        var (kd, ke) = (ks * l * b * b * b / 12, ks * b * l * l * l / 12);
                        k[3 * 6 + 3] = kd * d.X * d.X + ke * e.X * e.X;
                        k[4 * 6 + 4] = kd * d.Y * d.Y + ke * e.Y * e.Y;
                        k[3 * 6 + 4] = k[4 * 6 + 3] = kd * d.X * d.Y + ke * e.X * e.Y;
                        held[2] = held[3] = held[4] = false;
                        winkler++;
                    }
                }
                if (st.Springs is { } sp)
                    for (var d = 0; d < 6; d++)
                    {
                        if (sp[d] is not { } v) continue;
                        for (var x = 0; x < 6; x++) { k[d * 6 + x] = 0; k[x * 6 + d] = 0; }
                        k[d * 6 + d] = v;
                        held[d] = false;
                    }
                p.Held[n] = held;
                if (k.Any(x => x != 0)) p.Links.Add(Link.Spring(n, k, $"support:{sup.SourceId}:{p.NodeIds[n]}"));
                if (st.CompressionOnly) { p.Uplift.Add(n); lifting++; }
            }
        }

        // Slabs on the ground: Winkler's bed at their nodes.
        var (slabs, slabNodes) = (0, 0);
        var shellsOf = p.Shells.Select((s, i) => (s, i)).GroupBy(x => x.s.Group ?? "").ToDictionary(g => g.Key, g => g.Select(x => x.s).ToList());
        foreach (var (g, info) in Surfaces.Where(x => x.Value.Kind == "slab"))
        {
            if (Of(info.SourceId) is not { Winkler: { } ks } st || !shellsOf.TryGetValue(g, out var shells)) continue;
            slabs++;
            var area = new Dictionary<int, double>();
            foreach (var s in shells)
            {
                // The element's area from its corners (not yet prepared): half the diagonals' cross product, a triangle's two sides'.
                var c = s.Nodes.Select(n => p.Nodes[n]).ToArray();
                var whole = 0.5 * (c.Length == 4 ? Vec3.Cross(c[2] - c[0], c[3] - c[1]) : Vec3.Cross(c[1] - c[0], c[2] - c[0])).Length;
                foreach (var n in s.Nodes) area[n] = area.GetValueOrDefault(n) + whole / s.Count;
            }
            foreach (var (n, a) in area.OrderBy(x => x.Key))
            {
                var held = p.Held[n] is { } h ? (bool[])h.Clone() : new bool[6];
                held[0] = held[1] = true;
                p.Held[n] = held;
                p.Links.Add(Link.Spring(n, [0, 0, ks * a, 0, 0, 0], $"slab:{info.SourceId}:{p.NodeIds[n]}"));
                if (st.CompressionOnly) { p.Uplift.Add(n); lifting++; }
                slabNodes++;
            }
        }
        foreach (var (_, info) in Surfaces.Where(x => x.Value.Kind == "slab"))
            if (Of(info.SourceId) is { Winkler: null }) Notes.Add($"„{info.Name}”: a slab takes Winkler's ks only; its other support values do nothing.");
        if (nodesOf.Count == 0 && slabs == 0)
        {
            var unseen = Model.Shells.Where(s => s.Source.Kind == "slab" && !Surfaces.Any(x => x.Value.SourceId == s.Source.SourceId) && Of(s.Source.SourceId) is { Winkler: not null }).Select(s => s.Source.Name).Distinct().ToList();
            if (unseen.Count > 0) Notes.Add($"Winkler's bed under {string.Join(", ", unseen.Take(6))}: the slab is not analysed as a shell (the analysis options), so the bed does nothing.");
            return;
        }
        Springs = new SpringSummary(nodesOf.Count, count, winkler, slabs, slabNodes, lifting);
        if (nodesOf.Count > 0)
            Notes.Add($"{nodesOf.Count} support{(nodesOf.Count == 1 ? "" : "s")} on springs ({string.Join(", ", nodesOf.Keys.Select(s => s.Name).Distinct().Take(6))}{(nodesOf.Count > 6 ? "…" : "")}): {count} node{(count == 1 ? "" : "s")}" +
                (winkler > 0 ? $", {winkler} on Winkler's bed (kz = ks·A, the footprint's ks·I against turning; across and along a strip each node its length)" : "") + "; the springs' forces are reactions.");
        if (slabs > 0) Notes.Add($"{slabs} slab{(slabs == 1 ? "" : "s")} on the ground on Winkler's bed: {slabNodes} nodes, kz = ks × each node's share of the area; ux and uy held there (the ground's friction).");
        if (lifting > 0) Notes.Add($"{lifting} support node{(lifting == 1 ? "" : "s")} in compression only: each combination solved again where one would pull, lifted (free).");
        if (noFootprint.Count > 0) Notes.Add($"Winkler's ks on {string.Join(", ", noFootprint.Take(6))}: no footprint to take it on (a column base); give its springs instead.");
    }

    /// <summary>
    /// A footprint's area and second moments about its centroid in global axes (m², m⁴): Ixx = ∫(y − yc)², Iyy = ∫(x − xc)²,
    /// Ixy = ∫(x − xc)(y − yc), by Green's theorem over its outline (either way round).
    /// </summary>
    public static (double Area, double Ixx, double Iyy, double Ixy) Footprint(IReadOnlyList<Vec2> outline)
    {
        double a = 0, sx = 0, sy = 0, ixx = 0, iyy = 0, ixy = 0;
        var o = outline[0];
        for (var i = 0; i < outline.Count; i++)
        {
            var (p, q) = (outline[i] - o, outline[(i + 1) % outline.Count] - o);
            var c = p.X * q.Y - q.X * p.Y;
            a += c;
            sx += (p.X + q.X) * c;
            sy += (p.Y + q.Y) * c;
            ixx += (p.Y * p.Y + p.Y * q.Y + q.Y * q.Y) * c;
            iyy += (p.X * p.X + p.X * q.X + q.X * q.X) * c;
            ixy += (p.X * q.Y + 2 * p.X * p.Y + 2 * q.X * q.Y + q.X * p.Y) * c;
        }
        a /= 2;
        if (Math.Abs(a) < 1e-15) return (0, 0, 0, 0);
        var (xc, yc) = (sx / (6 * a), sy / (6 * a));
        // About the origin (the first vertex), then moved to the centroid (parallel axes); the signs follow the outline's turn, as the area's.
        var (jxx, jyy, jxy) = (ixx / 12, iyy / 12, ixy / 24);
        return (Math.Abs(a), Math.Abs(jxx - a * yc * yc), Math.Abs(jyy - a * xc * xc), Math.Sign(a) * (jxy - a * xc * yc));
    }

    /// <summary>Whether a support's footprint or strip is under a point (as the analytical model finds the feet it holds).</summary>
    static bool Under(AnalyticalSupport s, Vec3 at)
    {
        if (at.Z < s.Bottom - 0.05 || at.Z > s.Top + 2.0) return false;
        var p = at.Xy;
        if (s.Region is { Count: >= 3 } r) return Polygon2.Contains(r, p, 0.05);
        return s.LineA is { } a && s.LineB is { } b && Segment(p, a, b) <= s.Width / 2 + 0.05;
    }

    static double Segment(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var l2 = Vec2.Dot(ab, ab);
        var t = l2 > 1e-18 ? Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1) : 0;
        return (p - (a + ab * t)).Length;
    }
}
