namespace Lapin.Analysis.Solver;

/// <summary>
/// A member's buckling length about one axis from a buckling mode of the structure (§14.23): Lcr = π √(EI / (αcr |NEd|)), the
/// mode's αcr and number (1-based), and the member's participation in it about that axis (its mean squared slope in that
/// plane over the compression-weighted mean of every compressed member's, both planes).
/// </summary>
public sealed record AxisLength(double Lcr, double Alpha, int Mode, double Participation);

/// <summary>
/// A member's buckling lengths from the structure's buckling (§14.23): its axial force (kN, tension positive, the length-weighted
/// mean of its pieces'), about the strong axis (<see cref="Y"/>: bending about local 3, EN 1993's y-y) and the weak one
/// (<see cref="Z"/>: local 2, z-z), each null when no mode found bends it that way; <see cref="Why"/> says why there is none.
/// </summary>
public sealed record MemberLength(double N, AxisLength? Y, AxisLength? Z, string? Why = null);

/// <summary>
/// The buckling lengths of the compressed members from the elastic critical load of the structure (docs/plan-fem.md §14.23;
/// EN 1993-1-1 5.2.2(8), 6.3.1.2: Ncr,i = αcr NEd,i, Lcr,i = π √(EI / Ncr,i)). For each axis of a member, the lowest mode
/// found (the structure's or the member's own: its own mode is its buckling with the restraint the structure really gives its
/// ends) that bends it about that axis: its mean squared slope in that plane, ∫v′²dx / L, at least <see cref="Participation"/>
/// of the compression-weighted mean of all the compressed members' (Σ|N|∫(v′² + w′²)dx / Σ|N|L, which is φᵀGφ / Σ|N|L), and
/// its compression at least <see cref="MinForce"/> of the most compressed member's the mode bends. A member the mode moves with
/// a third of the frame's slopes or less (√0.1 ≈ 0.32) is carried by it, not buckling in it.
/// </summary>
public static class BucklingLengths
{
    /// <summary>The share of the frame's mean squared slope a member needs in a mode's plane to take that mode's length.</summary>
    public const double Participation = 0.1;
    /// <summary>
    /// The least compression, as a share of the most compressed member the mode bends, for a member to take the mode's length:
    /// under it the member is carried by the mode (a beam with a little thrust), and its length would be meaningless (over
    /// √(1/0.05) ≈ 4.5 times a driving member's of like EI), its check's buckling term ≈ 1/αcr whatever its force.
    /// </summary>
    public const double MinForce = 0.05;

    /// <param name="p">The problem the buckling's modes are on (its elements prepared).</param>
    /// <param name="members">Each member's elements (its pieces) in <paramref name="p"/>.</param>
    /// <param name="b">The buckling, with its modes and the axial forces it was worked out for.</param>
    public static MemberLength[] Of(FrameProblem p, IReadOnlyList<int[]> members, BucklingResult b)
    {
        var o = new MemberLength[members.Count];
        var axial = b.Axial;
        var n = new double[members.Count];
        var len = new double[members.Count];
        for (var k = 0; k < members.Count; k++)
        {
            double s = 0, l = 0;
            foreach (var e in members[k])
            {
                var el = p.Elements[e];
                l += el.L;
                if (e < axial.Length && !el.Inactive) s += axial[e] * el.L;
            }
            (n[k], len[k]) = (l > 0 ? s / l : 0, l);
        }
        if (!b.Ok || b.Alpha.Length == 0 || axial.Length == 0)
        {
            var why = !b.Ok ? b.Message ?? "the structure's buckling was not worked out" : "no buckling mode under the combination";
            for (var k = 0; k < o.Length; k++) o[k] = new MemberLength(n[k], null, null, why);
            return o;
        }
        var tiny = 1e-9 * Math.Max(1e-9, axial.Max(Math.Abs));
        var compressed = Enumerable.Range(0, members.Count).Where(k => n[k] < -tiny && len[k] > 0).ToList();
        var y = new AxisLength?[members.Count];
        var z = new AxisLength?[members.Count];
        var small = new bool[members.Count];
        Span<double> u = stackalloc double[12];
        for (var m = 0; m < b.Alpha.Length && m < b.Modes.Length; m++)
        {
            var mode = b.Modes[m];
            var alpha = b.Alpha[m];
            var sy = new double[members.Count];
            var sz = new double[members.Count];
            double work = 0, weight = 0;
            foreach (var k in compressed)
            {
                foreach (var e in members[k])
                {
                    var el = p.Elements[e];
                    for (var d = 0; d < 6; d++) { u[d] = mode[el.I * 6 + d]; u[6 + d] = mode[el.J * 6 + d]; }
                    var (a, c) = el.Slopes(u);
                    sy[k] += a;
                    sz[k] += c;
                }
                work += -n[k] * (sy[k] + sz[k]);
                weight += -n[k] * len[k];
            }
            if (!(work > 0 && weight > 0)) continue;
            var mean = work / weight;
            var bent = compressed.Where(k => (sy[k] + sz[k]) / len[k] / mean >= Participation).ToList();
            var nRef = bent.Count > 0 ? bent.Max(k => -n[k]) : 0;
            foreach (var k in compressed)
            {
                var el = p.Elements[members[k][0]];
                double py = sy[k] / len[k] / mean, pz = sz[k] / len[k] / mean;
                if (Math.Max(py, pz) >= Participation && -n[k] < MinForce * nRef) { small[k] = true; continue; }
                if (y[k] == null && py >= Participation) y[k] = new AxisLength(Math.PI * Math.Sqrt(el.E * el.I33 / (alpha * -n[k])), alpha, m + 1, py);
                if (z[k] == null && pz >= Participation) z[k] = new AxisLength(Math.PI * Math.Sqrt(el.E * el.I22 / (alpha * -n[k])), alpha, m + 1, pz);
            }
        }
        for (var k = 0; k < o.Length; k++)
        {
            string? why = null;
            if (!(n[k] < -tiny)) why = "not in compression under the combination";
            else if (small[k] && y[k] == null && z[k] == null) why = $"its compression under {MinForce:0.##} of the most compressed member's in the modes that bend it: carried by them";
            else if (y[k] == null || z[k] == null) why = $"bent too little by the structure's first {b.Alpha.Length} buckling modes" + (y[k] == null && z[k] == null ? "" : y[k] == null ? " about its strong axis" : " about its weak axis");
            o[k] = new MemberLength(n[k], y[k], z[k], why);
        }
        return o;
    }
}
