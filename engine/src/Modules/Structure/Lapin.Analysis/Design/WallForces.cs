using Lapin.Analysis.Solver;
using Lapin.Geometry;

namespace Lapin.Analysis.Design;

/// <summary>
/// A horizontal cut through a wall's shell elements (docs/plan-fem.md §12.5): the row of its mesh's nodes at one height (the
/// foot, the row nearest mid-height, the head), the elements on one side of it, the cut's centre, the wall's directions along
/// it and out of its plane, and the solid length the cut crosses (the piers between openings).
/// </summary>
public sealed record WallCut(string Where, double Z, int[] Nodes, int[] Elements, bool Above, Vec3 Centre, Vec3 Along, Vec3 Normal, double Length);

/// <summary>The resultants of a cut (kN, kN·m): N compression positive, V in the wall's plane, M in its plane about the cut's centre, m out of its plane per metre, V out of its plane.</summary>
public readonly record struct WallSection(double N, double V, double M, double MOut, double VOut)
{
    public static WallSection operator +(WallSection a, WallSection b) => new(a.N + b.N, a.V + b.V, a.M + b.M, a.MOut + b.MOut, a.VOut + b.VOut);
    public static WallSection operator *(WallSection a, double k) => new(a.N * k, a.V * k, a.M * k, a.MOut * k, a.VOut * k);
    public double[] ToArray() => [N, V, M, MOut, VOut];
    public static WallSection Of(double[] v) => new(v[0], v[1], v[2], v[3], v[4]);
}

/// <summary>
/// A wall's internal forces from its shell elements' nodal forces (Kₑuₑ − fₑ, the elements' own loads taken off) on one side
/// of a cut, summed at the cut's nodes: what one part of the wall gives the other, in exact balance with everything on that
/// side (the force method of a free body: no smoothing, no integration of stresses).
/// </summary>
public static class WallForces
{
    /// <summary>
    /// The cuts of a wall surface (its shell elements' indices): at its foot (the elements over its lowest row of nodes), at
    /// the row nearest its mid-height, and at its head (the elements under its highest row, the force they take from above).
    /// </summary>
    public static List<WallCut> Cuts(FrameProblem p, IReadOnlyList<int> shells, Vec3 along)
    {
        var o = new List<WallCut>();
        if (shells.Count == 0) return o;
        var nodes = shells.SelectMany(s => p.Shells[s].Nodes).Distinct().ToList();
        var zs = nodes.Select(n => Math.Round(p.Nodes[n].Z, 3)).Distinct().Order().ToList();
        if (zs.Count < 2) return o;
        var normal = Vec3.Cross(Vec3.UnitZ, along).Length > 1e-9 ? p.Shells[shells[0]].Ez : new Vec3(0, 1, 0);
        WallCut? Cut(string where, double z, bool above)
        {
            var row = nodes.Where(n => Math.Abs(p.Nodes[n].Z - z) < 1e-3).ToHashSet();
            var els = shells.Where(s =>
            {
                var ns = p.Shells[s].Nodes;
                if (!ns.Any(row.Contains)) return false;
                return above ? ns.All(n => p.Nodes[n].Z >= z - 1e-3) : ns.All(n => p.Nodes[n].Z <= z + 1e-3);
            }).ToArray();
            if (els.Length == 0 || row.Count == 0) return null;
            // The solid length: the elements' edges along the row.
            var length = 0.0;
            foreach (var s in els)
            {
                var ns = p.Shells[s].Nodes;
                for (var i = 0; i < ns.Length; i++)
                {
                    var (a, b) = (ns[i], ns[(i + 1) % ns.Length]);
                    if (row.Contains(a) && row.Contains(b)) length += (p.Nodes[b] - p.Nodes[a]).Length;
                }
            }
            var pts = row.Select(n => p.Nodes[n]).ToList();
            var ts = pts.Select(q => Vec3.Dot(q, along)).ToList();
            var (t0, t1) = (ts.Min(), ts.Max());
            var first = pts[ts.IndexOf(t0)];
            var centre = first + along * ((t1 - t0) / 2);
            return new WallCut(where, z, [.. row.Order()], els, above, new Vec3(centre.X, centre.Y, z), along, normal, length);
        }
        var mid = zs.MinBy(z => Math.Abs(z - (zs[0] + zs[^1]) / 2));
        foreach (var c in new[] { Cut("foot", zs[0], true), Cut("middle", mid, true), Cut("head", zs[^1], false) })
            if (c != null) o.Add(c);
        return o;
    }

    /// <summary>
    /// The cut's resultants from a set of global displacements <paramref name="u"/> and the elements' own uniform loads
    /// (kN/m², global; null: none, as for a mode). The elements' global stiffness comes from <paramref name="stiffness"/>.
    /// </summary>
    public static WallSection Of(FrameProblem p, WallCut cut, double[] u, Func<int, Vec3>? loadOf, Func<int, double[]> stiffness)
    {
        var row = cut.Nodes.ToHashSet();
        Vec3 f = default, m = default;
        foreach (var s in cut.Elements)
        {
            var e = p.Shells[s];
            var k = stiffness(s);
            var size = e.Size;
            var ue = new double[size];
            for (var i = 0; i < e.Count; i++) for (var d = 0; d < 6; d++) ue[6 * i + d] = u[e.Nodes[i] * 6 + d];
            var fe = new double[size];
            for (var r = 0; r < size; r++) { var x = 0.0; for (var c = 0; c < size; c++) x += k[r * size + c] * ue[c]; fe[r] = x; }
            if (loadOf?.Invoke(s) is { } q && q.Length > 0) { var eq = e.Equivalent(q); for (var r = 0; r < size; r++) fe[r] -= eq[r]; }
            for (var i = 0; i < e.Count; i++)
            {
                if (!row.Contains(e.Nodes[i])) continue;
                var fi = new Vec3(fe[6 * i], fe[6 * i + 1], fe[6 * i + 2]);
                var mi = new Vec3(fe[6 * i + 3], fe[6 * i + 4], fe[6 * i + 5]);
                f += fi;
                m += mi + Vec3.Cross(p.Nodes[e.Nodes[i]] - cut.Centre, fi);
            }
        }
        // What the other part gives this one at the cut: from below for the elements over it, from above for those under it.
        // N is the compression: the force from below pushing up (or from above pushing down).
        var sign = cut.Above ? 1 : -1;
        f *= sign; m *= sign;
        var len = Math.Max(cut.Length, 1e-9);
        return new WallSection(f.Z, Vec3.Dot(f, cut.Along), Vec3.Dot(m, cut.Normal), Vec3.Dot(m, cut.Along) / len, Vec3.Dot(f, cut.Normal));
    }
}
