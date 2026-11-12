using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>
/// A load on a bar's span, in the bar's own length from its own i end (metres): distributed from <see cref="A"/> to
/// <see cref="B"/>, varying linearly from <see cref="Qa"/> to <see cref="Qb"/> (kN/m, a global vector), or (<see cref="Point"/>)
/// a force <see cref="Qa"/> (kN, global) at <see cref="A"/>.
/// </summary>
public readonly record struct SpanLoad(double A, double B, Vec3 Qa, Vec3 Qb, bool Point = false)
{
    public static SpanLoad Uniform(double a, double b, Vec3 q) => new(a, b, q, q);
    public static SpanLoad Force(double at, Vec3 f) => new(at, at, f, f, true);
    /// <summary>The load's whole force (kN, global).</summary>
    public Vec3 Total => Point ? Qa : (Qa + Qb) * ((B - A) / 2);
    public SpanLoad Scaled(double k) => this with { Qa = Qa * k, Qb = Qb * k };
}

/// <summary>A span load in the bar's local axes (x along it, y local 2, z local 3): kN/m (or kN for a point) per axis.</summary>
public readonly record struct LocalLoad(double A, double B, double Xa, double Ya, double Za, double Xb, double Yb, double Zb, bool Point)
{
    public LocalLoad Scaled(double k) => new(A, B, Xa * k, Ya * k, Za * k, Xb * k, Yb * k, Zb * k, Point);
    /// <summary>The intensity at a station inside the span (a distributed load's).</summary>
    public (double X, double Y, double Z) At(double s)
    {
        var t = B - A > 1e-12 ? (s - A) / (B - A) : 0;
        return (Xa + (Xb - Xa) * t, Ya + (Yb - Ya) * t, Za + (Zb - Za) * t);
    }
}

/// <summary>
/// A bar between two nodes as the solver takes it: its nodes, its own ends' offsets from them (rigid arms, global), local 2
/// (the section's depth, a unit vector square to the bar), E and G (kPa: kN/m²), the section's A, I22 (bending about local 2),
/// I33 (about local 3), J (m², m⁴) and its shear areas (0: none, Euler–Bernoulli), and its ends' releases in local axes.
/// </summary>
public sealed class FrameElement
{
    public required string Id { get; init; }
    public int I { get; init; }
    public int J { get; init; }
    public Vec3 OffI { get; init; }
    public Vec3 OffJ { get; init; }
    public Vec3 Local2 { get; init; }
    public double E { get; init; }
    public double G { get; init; }
    public double A { get; init; }
    public double I22 { get; init; }
    public double I33 { get; init; }
    public double Jt { get; init; }
    public double As2 { get; init; }
    public double As3 { get; init; }
    public bool[] ReleaseI { get; init; } = new bool[6];
    public bool[] ReleaseJ { get; init; } = new bool[6];
    /// <summary>A name to say (its element's).</summary>
    public string? Name { get; init; }
    public string? SourceId { get; init; }

    // Worked out by Prepare.
    public Vec3 Pi, Pj, Ex, Ey, Ez;
    public double L;
    /// <summary>Local end displacements from the nodes' global ones (12 × 12): the rotation onto local axes after the rigid arms.</summary>
    public double[] T = new double[144];
    /// <summary>The local stiffness with the releases condensed out (12 × 12), and what condensing a load vector needs.</summary>
    public double[] K = new double[144];
    double[] _k0 = new double[144];
    int[] _released = [];
    double[]? _kccInv;

    public bool HasReleases => _released.Length > 0;
    /// <summary>Shear's share of the bending in each plane, Φ = 12EI/(G·As·L²): 0 for Euler–Bernoulli.</summary>
    public double PhiY, PhiZ;

    public void Prepare(IReadOnlyList<Vec3> nodes)
    {
        Pi = nodes[I] + OffI;
        Pj = nodes[J] + OffJ;
        var d = Pj - Pi;
        L = d.Length;
        Ex = d * (1 / L);
        var y = Local2 - Ex * Vec3.Dot(Local2, Ex);
        Ey = y.Normalized();
        Ez = Vec3.Cross(Ex, Ey);
        BuildTransform();
        BuildStiffness();
    }

    void BuildTransform()
    {
        // u_end = u_node + θ × off = u_node − S(off)·θ, then each 3-vector onto (Ex, Ey, Ez).
        Array.Clear(T);
        double[] lam = [Ex.X, Ex.Y, Ex.Z, Ey.X, Ey.Y, Ey.Z, Ez.X, Ez.Y, Ez.Z];
        for (var b = 0; b < 4; b++)
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    T[(b * 3 + r) * 12 + b * 3 + c] = lam[r * 3 + c];
        void Arm(int end, Vec3 off)
        {
            if (off.Length < 1e-12) return;
            // −S(off): rows of the translation block times θ.
            Span<double> s = [0, off.Z, -off.Y, -off.Z, 0, off.X, off.Y, -off.X, 0];
            var (row, col) = (end * 6, end * 6 + 3);
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                {
                    var v = 0.0;
                    for (var k = 0; k < 3; k++) v += lam[r * 3 + k] * s[k * 3 + c];
                    T[(row + r) * 12 + col + c] = v;
                }
        }
        Arm(0, OffI);
        Arm(1, OffJ);
    }

    void BuildStiffness()
    {
        var k = _k0;
        Array.Clear(k);
        void S(int r, int c, double v) { k[r * 12 + c] += v; if (r != c) k[c * 12 + r] += v; }
        var (l, l2, l3) = (L, L * L, L * L * L);
        var ea = E * A / l;
        S(0, 0, ea); S(6, 6, ea); S(0, 6, -ea);
        var gj = G * Jt / l;
        S(3, 3, gj); S(9, 9, gj); S(3, 9, -gj);
        // Bending in the x–y plane (v, θz) about local 3; in the x–z plane (w, θy) about local 2. Shear deformation by Φ.
        var phiY = PhiY = As2 > 0 && G > 0 ? 12 * E * I33 / (G * As2 * l2) : 0;
        var phiZ = PhiZ = As3 > 0 && G > 0 ? 12 * E * I22 / (G * As3 * l2) : 0;
        var c = E * I33 / (l3 * (1 + phiY));
        S(1, 1, 12 * c); S(1, 5, 6 * l * c); S(1, 7, -12 * c); S(1, 11, 6 * l * c);
        S(5, 5, (4 + phiY) * l2 * c); S(5, 7, -6 * l * c); S(5, 11, (2 - phiY) * l2 * c);
        S(7, 7, 12 * c); S(7, 11, -6 * l * c); S(11, 11, (4 + phiY) * l2 * c);
        c = E * I22 / (l3 * (1 + phiZ));
        S(2, 2, 12 * c); S(2, 4, -6 * l * c); S(2, 8, -12 * c); S(2, 10, -6 * l * c);
        S(4, 4, (4 + phiZ) * l2 * c); S(4, 8, 6 * l * c); S(4, 10, (2 - phiZ) * l2 * c);
        S(8, 8, 12 * c); S(8, 10, 6 * l * c); S(10, 10, (4 + phiZ) * l2 * c);

        _released = Enumerable.Range(0, 6).Where(i => ReleaseI[i]).Concat(Enumerable.Range(0, 6).Where(i => ReleaseJ[i]).Select(i => i + 6)).ToArray();
        Array.Copy(k, K, 144);
        if (_released.Length == 0) { _kccInv = null; return; }
        // Static condensation: K' = K − K·R (K_RR)⁻¹ R·K, the released rows and columns then zero.
        var n = _released.Length;
        var kcc = new double[n * n];
        for (var a = 0; a < n; a++) for (var b = 0; b < n; b++) kcc[a * n + b] = k[_released[a] * 12 + _released[b]];
        _kccInv = Dense.Inverse(kcc, n);
        for (var r = 0; r < 12; r++)
            for (var cc = 0; cc < 12; cc++)
            {
                var v = 0.0;
                for (var a = 0; a < n; a++)
                {
                    var kra = k[r * 12 + _released[a]];
                    if (kra == 0) continue;
                    for (var b = 0; b < n; b++) v += kra * _kccInv[a * n + b] * k[_released[b] * 12 + cc];
                }
                K[r * 12 + cc] = k[r * 12 + cc] - v;
            }
        foreach (var r in _released) for (var cc = 0; cc < 12; cc++) { K[r * 12 + cc] = 0; K[cc * 12 + r] = 0; }
    }

    /// <summary>The loads in local axes.</summary>
    public LocalLoad ToLocal(SpanLoad s) => new(s.A, s.B,
        Vec3.Dot(s.Qa, Ex), Vec3.Dot(s.Qa, Ey), Vec3.Dot(s.Qa, Ez), Vec3.Dot(s.Qb, Ex), Vec3.Dot(s.Qb, Ey), Vec3.Dot(s.Qb, Ez), s.Point);

    /// <summary>
    /// The span loads' equivalent end loads in local axes (12, before condensing): the work-equivalent forces of the bar's
    /// exact end-displacement fields — linear along it and in twist, and in bending the interdependent interpolation of a
    /// Timoshenko beam (cubic, with shear's share Φ; Hermite's when Φ = 0) — which are the fixed-end reactions, negated.
    /// </summary>
    public double[] Equivalent(IEnumerable<LocalLoad> loads)
    {
        var p = new double[12];
        static (double, double, double, double) Shape(double s, double l, double phi)
        {
            var k = 1 / (1 + phi);
            var (s2, s3) = (s * s, s * s * s);
            return (k * (1 - 3 * s2 + 2 * s3 + phi * (1 - s)),
                    k * l * (s - 2 * s2 + s3 + phi / 2 * (s - s2)),
                    k * (3 * s2 - 2 * s3 + phi * s),
                    k * l * (-s2 + s3 - phi / 2 * (s - s2)));
        }
        void At(double x, double fx, double fy, double fz, double w)
        {
            var s = Math.Clamp(x / L, 0, 1);
            var (n1, n2) = (1 - s, s);
            var (h1, h2, h3, h4) = Shape(s, L, PhiY);
            var (g1, g2, g3, g4) = Shape(s, L, PhiZ);
            p[0] += w * fx * n1; p[6] += w * fx * n2;
            p[1] += w * fy * h1; p[5] += w * fy * h2; p[7] += w * fy * h3; p[11] += w * fy * h4;
            p[2] += w * fz * g1; p[4] -= w * fz * g2; p[8] += w * fz * g3; p[10] -= w * fz * g4;
        }
        foreach (var l in loads)
        {
            if (l.Point) { At(l.A, l.Xa, l.Ya, l.Za, 1); continue; }
            var (a, b) = (Math.Max(0, l.A), Math.Min(L, l.B));
            if (b - a < 1e-12) continue;
            // Gauss–Legendre, 3 points: exact for the linear load times the cubic shape functions.
            var (mid, half) = ((a + b) / 2, (b - a) / 2);
            foreach (var (g, w) in Gauss3)
            {
                var x = mid + half * g;
                var (qx, qy, qz) = l.At(x);
                At(x, qx, qy, qz, w * half);
            }
        }
        return p;
    }

    internal static readonly (double G, double W)[] Gauss3 = [(-Math.Sqrt(0.6), 5.0 / 9), (0, 8.0 / 9), (Math.Sqrt(0.6), 5.0 / 9)];

    /// <summary>A local load vector with the released ends' share passed to the held ones: P' = P − K·R (K_RR)⁻¹ P_R, zero at the released.</summary>
    public double[] Condense(double[] p)
    {
        if (_kccInv == null) return p;
        var n = _released.Length;
        var o = (double[])p.Clone();
        var y = new double[n];
        for (var a = 0; a < n; a++) for (var b = 0; b < n; b++) y[a] += _kccInv[a * n + b] * p[_released[b]];
        for (var r = 0; r < 12; r++)
        {
            var v = 0.0;
            for (var a = 0; a < n; a++) v += _k0[r * 12 + _released[a]] * y[a];
            o[r] -= v;
        }
        foreach (var r in _released) o[r] = 0;
        return o;
    }

    /// <summary>The released end displacements worked out from the held ones and the loads (the bar's own ends then turn as its span bends): u_R = (K_RR)⁻¹ (P_R − K_RH u_H).</summary>
    public void Recover(double[] uLocal, double[] pLocal)
    {
        if (_kccInv == null) return;
        var n = _released.Length;
        var rhs = new double[n];
        var isRel = new bool[12];
        foreach (var r in _released) isRel[r] = true;
        for (var a = 0; a < n; a++)
        {
            var v = pLocal[_released[a]];
            for (var c = 0; c < 12; c++) if (!isRel[c]) v -= _k0[_released[a] * 12 + c] * uLocal[c];
            rhs[a] = v;
        }
        for (var a = 0; a < n; a++)
        {
            var v = 0.0;
            for (var b = 0; b < n; b++) v += _kccInv[a * n + b] * rhs[b];
            uLocal[_released[a]] = v;
        }
    }

    /// <summary>Tᵀ·M·T: a local 12 × 12 matrix on the nodes' global degrees of freedom.</summary>
    public double[] GlobalStiffness()
    {
        var kt = new double[144];
        Dense.Mul(K, T, kt, 12);
        var o = new double[144];
        Dense.MulTransposedA(T, kt, o, 12);
        return o;
    }

    /// <summary>Tᵀ·v: a local end vector as forces on the nodes, global.</summary>
    public double[] ToNodes(double[] local)
    {
        var o = new double[12];
        for (var r = 0; r < 12; r++)
        {
            var v = local[r];
            if (v == 0) continue;
            for (var c = 0; c < 12; c++) o[c] += T[r * 12 + c] * v;
        }
        return o;
    }

    /// <summary>T·u: the nodes' global displacements as the bar's own ends' local ones.</summary>
    public double[] ToLocal(double[] nodes12)
    {
        var o = new double[12];
        for (var r = 0; r < 12; r++)
        {
            var v = 0.0;
            for (var c = 0; c < 12; c++) v += T[r * 12 + c] * nodes12[c];
            o[r] = v;
        }
        return o;
    }

    /// <summary>The end forces on the bar, local: K'·u − P'.</summary>
    public double[] EndForces(double[] uLocal, double[] pCondensed)
    {
        var o = new double[12];
        for (var r = 0; r < 12; r++)
        {
            var v = -pCondensed[r];
            for (var c = 0; c < 12; c++) v += K[r * 12 + c] * uLocal[c];
            o[r] = v;
        }
        return o;
    }
}

/// <summary>Small dense matrices, row-major.</summary>
static class Dense
{
    public static void Mul(double[] a, double[] b, double[] o, int n)
    {
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
            {
                var v = 0.0;
                for (var k = 0; k < n; k++) v += a[i * n + k] * b[k * n + j];
                o[i * n + j] = v;
            }
    }

    /// <summary>o = aᵀ·b.</summary>
    public static void MulTransposedA(double[] a, double[] b, double[] o, int n)
    {
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
            {
                var v = 0.0;
                for (var k = 0; k < n; k++) v += a[k * n + i] * b[k * n + j];
                o[i * n + j] = v;
            }
    }

    /// <summary>The inverse by Gauss–Jordan with partial pivoting (n ≤ 12); a singular block (a bar released at every degree) is refused.</summary>
    public static double[] Inverse(double[] m, int n)
    {
        var a = (double[])m.Clone();
        var inv = new double[n * n];
        for (var i = 0; i < n; i++) inv[i * n + i] = 1;
        for (var c = 0; c < n; c++)
        {
            var p = c;
            for (var r = c + 1; r < n; r++) if (Math.Abs(a[r * n + c]) > Math.Abs(a[p * n + c])) p = r;
            if (Math.Abs(a[p * n + c]) < 1e-300) throw new InvalidOperationException("A bar's releases leave it no stiffness.");
            if (p != c)
                for (var k = 0; k < n; k++) { (a[p * n + k], a[c * n + k]) = (a[c * n + k], a[p * n + k]); (inv[p * n + k], inv[c * n + k]) = (inv[c * n + k], inv[p * n + k]); }
            var d = a[c * n + c];
            for (var k = 0; k < n; k++) { a[c * n + k] /= d; inv[c * n + k] /= d; }
            for (var r = 0; r < n; r++)
            {
                if (r == c) continue;
                var f = a[r * n + c];
                if (f == 0) continue;
                for (var k = 0; k < n; k++) { a[r * n + k] -= f * a[c * n + k]; inv[r * n + k] -= f * inv[c * n + k]; }
            }
        }
        return inv;
    }
}
