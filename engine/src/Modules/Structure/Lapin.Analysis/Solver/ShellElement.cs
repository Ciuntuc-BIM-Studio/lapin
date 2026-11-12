using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>
/// A shell's stress resultants at a point, in its element's local axes (x <see cref="ShellElement.Ex"/>, y
/// <see cref="ShellElement.Ey"/>, z the normal): membrane forces Nx, Ny, Nxy (kN/m), moments Mx, My, Mxy (kN·m/m, Mx = ∫σx·z dz:
/// positive when the +z face is in tension) and transverse shears Qx, Qy (kN/m).
/// </summary>
public readonly record struct ShellForces(double Nx, double Ny, double Nxy, double Mx, double My, double Mxy, double Qx, double Qy)
{
    public static ShellForces operator +(ShellForces a, ShellForces b) =>
        new(a.Nx + b.Nx, a.Ny + b.Ny, a.Nxy + b.Nxy, a.Mx + b.Mx, a.My + b.My, a.Mxy + b.Mxy, a.Qx + b.Qx, a.Qy + b.Qy);
    public static ShellForces operator *(ShellForces a, double k) =>
        new(a.Nx * k, a.Ny * k, a.Nxy * k, a.Mx * k, a.My * k, a.Mxy * k, a.Qx * k, a.Qy * k);
    public double this[int i] => i switch { 0 => Nx, 1 => Ny, 2 => Nxy, 3 => Mx, 4 => My, 5 => Mxy, 6 => Qx, _ => Qy };
    public static readonly string[] Names = ["Nx", "Ny", "Nxy", "Mx", "My", "Mxy", "Qx", "Qy"];
}

/// <summary>
/// A flat shell element of 4 (or 3) nodes, 6 degrees of freedom a node (docs/plan-fem.md §10.2), linear elastic, isotropic.
/// <para>
/// <b>Bending</b> (Reissner–Mindlin, so thick and thin plates alike): the quadrilateral is MITC4 (Bathe and Dvorkin, "A
/// four-node plate bending element based on Mindlin/Reissner plate theory and a mixed interpolation", IJNME 21, 1985;
/// Dvorkin and Bathe, "A continuum mechanics based four-node shell element for general nonlinear analysis", Eng. Comput. 1,
/// 1984): w, θx, θy bilinear, the transverse shear strains interpolated from their covariant values at the edges' midpoints,
/// so a thin plate does not lock in shear and the element has no spurious modes. The triangle is MITC3 (Lee and Bathe,
/// "Development of MITC isotropic triangular shell finite elements", Comput. Struct. 82, 2004): the shear field a constant
/// plus a rotation, tied to the three edges. Shear correction 5/6.
/// </para>
/// <para>
/// <b>Membrane</b> with a true drilling rotation θz: Allman's interpolation (Allman, "A compatible triangular element including
/// vertex rotations for plane elasticity analysis", Comput. Struct. 19, 1984), the edges' normal displacement quadratic from
/// the rotations at their ends, and the rotation tied to the in-plane rotation of the displacement field by a penalty
/// γ·∫(θz − ω)² dA after Hughes and Brezzi ("On drilling degrees of freedom", CMAME 72, 1989), as Ibrahimbegović, Taylor and
/// Wilson do for the quadrilateral ("A robust quadrilateral membrane finite element with drilling degrees of freedom", IJNME
/// 30, 1990); γ = <see cref="Drilling"/>·G. So walls and slabs meet bars and each other with all six degrees of freedom
/// stiff, and no fictitious drilling spring is needed.
/// </para>
/// Integration: 2 × 2 Gauss points on the quadrilateral, three points on the triangle (exact for its quadratic integrands).
/// The element is flat: its nodes are taken in their mean plane (<see cref="Warp"/> says how far they are from it).
/// Written for Lapin from those papers.
/// </summary>
public sealed class ShellElement
{
    public const double ShearFactor = 5.0 / 6;

    public required string Id { get; init; }
    /// <summary>3 or 4 nodes, counter-clockwise seen from the side the normal points to.</summary>
    public required int[] Nodes { get; init; }
    /// <summary>Young's modulus (kPa: kN/m²), Poisson's ratio, thickness (m).</summary>
    public double E { get; init; }
    public double Nu { get; init; }
    public double Thickness { get; init; }
    /// <summary>The local x axis' direction before it is laid in the element's plane; null: global X for a slab, horizontal for a wall.</summary>
    public Vec3? Axis { get; init; }
    /// <summary>The penalty tying θz to the in-plane rotation, as a share of G.</summary>
    public double Drilling { get; init; } = 1.0;
    /// <summary>The surface the element belongs to (its results are smoothed over it), its element's GlobalId and name.</summary>
    public string? Group { get; init; }
    public string? SourceId { get; init; }
    public string? Name { get; init; }

    public double G => E / (2 * (1 + Nu));
    public int Count => Nodes.Length;
    /// <summary>The element's degrees of freedom: 6 a node, [ux, uy, uz, rx, ry, rz] each.</summary>
    public int Size => 6 * Nodes.Length;

    // Worked out by Prepare.
    public Vec3 Centre, Ex, Ey, Ez;
    public double Area, Warp;
    double[] _x = [], _y = [];
    double[] _k = [];

    public void Prepare(IReadOnlyList<Vec3> nodes)
    {
        var n = Count;
        if (n is not (3 or 4)) throw new InvalidOperationException($"Shell {Id}: {n} nodes; 3 or 4 are taken.");
        var p = Nodes.Select(i => nodes[i]).ToArray();
        Centre = p.Aggregate(Vec3.Zero, (a, b) => a + b) * (1.0 / n);
        var normal = VecMath.NewellNormal(p);
        Ez = normal.Normalized();
        var axis = Axis ?? (Math.Abs(Ez.Z) < 0.7 ? Vec3.Cross(Vec3.UnitZ, Ez) : new Vec3(1, 0, 0));
        var x = axis - Ez * Vec3.Dot(axis, Ez);
        if (x.Length < 1e-6) x = Math.Abs(Ez.X) < 0.9 ? new Vec3(1, 0, 0) - Ez * Ez.X : new Vec3(0, 1, 0) - Ez * Ez.Y;
        Ex = x.Normalized();
        Ey = Vec3.Cross(Ez, Ex);
        _x = p.Select(q => Vec3.Dot(q - Centre, Ex)).ToArray();
        _y = p.Select(q => Vec3.Dot(q - Centre, Ey)).ToArray();
        Warp = p.Max(q => Math.Abs(Vec3.Dot(q - Centre, Ez)));
        Area = 0;
        for (var i = 0; i < n; i++) Area += _x[i] * _y[(i + 1) % n] - _x[(i + 1) % n] * _y[i];
        Area /= 2;
        if (!(Area > 0)) throw new InvalidOperationException($"Shell {Id} has no area.");
        _k = LocalStiffness();
    }

    /// <summary>The nodes' coordinates in the element's own axes (metres, from its centre).</summary>
    public (double X, double Y) Local(int i) => (_x[i], _y[i]);

    // Gauss points: (a, b, weight) in the parent element.
    static readonly double G2 = 1 / Math.Sqrt(3);
    static readonly (double A, double B, double W)[] QuadPoints = [(-G2, -G2, 1), (G2, -G2, 1), (G2, G2, 1), (-G2, G2, 1)];
    static readonly (double A, double B, double W)[] TriPoints = [(1.0 / 6, 1.0 / 6, 1.0 / 6), (2.0 / 3, 1.0 / 6, 1.0 / 6), (1.0 / 6, 2.0 / 3, 1.0 / 6)];
    static readonly double[] Xi = [-1, 1, 1, -1], Eta = [-1, -1, 1, 1];

    /// <summary>The corner functions and their derivatives in the parent element.</summary>
    void Shape(double a, double b, Span<double> n, Span<double> na, Span<double> nb)
    {
        if (Count == 4)
            for (var i = 0; i < 4; i++)
            {
                n[i] = 0.25 * (1 + Xi[i] * a) * (1 + Eta[i] * b);
                na[i] = 0.25 * Xi[i] * (1 + Eta[i] * b);
                nb[i] = 0.25 * Eta[i] * (1 + Xi[i] * a);
            }
        else
        {
            n[0] = 1 - a - b; n[1] = a; n[2] = b;
            na[0] = -1; na[1] = 1; na[2] = 0;
            nb[0] = -1; nb[1] = 0; nb[2] = 1;
        }
    }

    /// <summary>Allman's edge functions (edge k from node k to node k + 1): 1 at the edge's midpoint, 0 at the corners and on the other edges.</summary>
    void Bubbles(double a, double b, ReadOnlySpan<double> n, ReadOnlySpan<double> na, ReadOnlySpan<double> nb, Span<double> ma, Span<double> mb)
    {
        if (Count == 4)
        {
            ma[0] = -a * (1 - b); mb[0] = -0.5 * (1 - a * a);
            ma[1] = 0.5 * (1 - b * b); mb[1] = -(1 + a) * b;
            ma[2] = -a * (1 + b); mb[2] = 0.5 * (1 - a * a);
            ma[3] = -0.5 * (1 - b * b); mb[3] = -(1 - a) * b;
        }
        else
            for (var k = 0; k < 3; k++)
            {
                var (i, j) = (k, (k + 1) % 3);
                ma[k] = 4 * (na[i] * n[j] + n[i] * na[j]);
                mb[k] = 4 * (nb[i] * n[j] + n[i] * nb[j]);
            }
    }

    /// <summary>The Jacobian [[x,a y,a],[x,b y,b]] at a point, its determinant and inverse.</summary>
    (double Xa, double Ya, double Xb, double Yb, double Det, double I00, double I01, double I10, double I11) Jacobian(ReadOnlySpan<double> na, ReadOnlySpan<double> nb)
    {
        double xa = 0, ya = 0, xb = 0, yb = 0;
        for (var i = 0; i < Count; i++) { xa += na[i] * _x[i]; ya += na[i] * _y[i]; xb += nb[i] * _x[i]; yb += nb[i] * _y[i]; }
        var det = xa * yb - ya * xb;
        return (xa, ya, xb, yb, det, yb / det, -ya / det, -xb / det, xa / det);
    }

    /// <summary>
    /// The strain–displacement rows at a point (local dofs per node: membrane [u, v, θz], bending [w, θx, θy]): membrane
    /// strains [εx, εy, γxy], the drilling row θz − ω, curvatures [κx, κy, κxy], transverse shears [γxz, γyz] (assumed).
    /// </summary>
    void Rows(double a, double b, double[,] bm, double[] bd, double[,] bb, double[,] bs, out double det)
    {
        var c = Count;
        Span<double> n = stackalloc double[4], na = stackalloc double[4], nb = stackalloc double[4];
        Span<double> ma = stackalloc double[4], mb = stackalloc double[4];
        Shape(a, b, n, na, nb);
        Bubbles(a, b, n, na, nb, ma, mb);
        var j = Jacobian(na, nb);
        det = j.Det;
        Array.Clear(bm);
        Array.Clear(bd);
        Array.Clear(bb);
        Array.Clear(bs);
        for (var i = 0; i < c; i++)
        {
            var nx = j.I00 * na[i] + j.I01 * nb[i];
            var ny = j.I10 * na[i] + j.I11 * nb[i];
            bm[0, 3 * i] = nx;
            bm[1, 3 * i + 1] = ny;
            bm[2, 3 * i] = ny;
            bm[2, 3 * i + 1] = nx;
            bd[3 * i + 2] += n[i];
            bd[3 * i] += 0.5 * ny;
            bd[3 * i + 1] -= 0.5 * nx;
            bb[0, 3 * i + 2] = nx;
            bb[1, 3 * i + 1] = -ny;
            bb[2, 3 * i + 2] = ny;
            bb[2, 3 * i + 1] = -nx;
        }
        // Allman's terms: on edge k (node p to node q), u += M·(y_q − y_p)/8·(θq − θp), v += M·(x_p − x_q)/8·(θq − θp).
        for (var k = 0; k < c; k++)
        {
            var (p, q) = (k, (k + 1) % c);
            var cx = (_y[q] - _y[p]) / 8;
            var cy = (_x[p] - _x[q]) / 8;
            var mx = j.I00 * ma[k] + j.I01 * mb[k];
            var my = j.I10 * ma[k] + j.I11 * mb[k];
            Span<double> d = [mx * cx, my * cy, my * cx + mx * cy, -0.5 * (mx * cy - my * cx)];
            for (var r = 0; r < 3; r++) { bm[r, 3 * q + 2] += d[r]; bm[r, 3 * p + 2] -= d[r]; }
            bd[3 * q + 2] += d[3];
            bd[3 * p + 2] -= d[3];
        }
        // Transverse shear: covariant strains tied at the edges, mapped back to x, y.
        Span<double> ea = stackalloc double[12], eb = stackalloc double[12];
        if (c == 4)
        {
            Span<double> sA = stackalloc double[12], sC = stackalloc double[12], sB = stackalloc double[12], sD = stackalloc double[12];
            Covariant(0, -1, true, sA);
            Covariant(0, 1, true, sC);
            Covariant(1, 0, false, sB);
            Covariant(-1, 0, false, sD);
            for (var x = 0; x < 12; x++)
            {
                ea[x] = 0.5 * (1 - b) * sA[x] + 0.5 * (1 + b) * sC[x];
                eb[x] = 0.5 * (1 - a) * sD[x] + 0.5 * (1 + a) * sB[x];
            }
        }
        else
        {
            Span<double> r1 = stackalloc double[12], s2 = stackalloc double[12], r3 = stackalloc double[12], s3 = stackalloc double[12];
            Covariant(0.5, 0, true, r1);
            Covariant(0, 0.5, false, s2);
            Covariant(0.5, 0.5, true, r3);
            Covariant(0.5, 0.5, false, s3);
            for (var x = 0; x < 9; x++)
            {
                var cc = s2[x] - r1[x] - s3[x] + r3[x];
                ea[x] = r1[x] + cc * b;
                eb[x] = s2[x] - cc * a;
            }
        }
        for (var x = 0; x < 3 * c; x++)
        {
            bs[0, x] = j.I00 * ea[x] + j.I01 * eb[x];
            bs[1, x] = j.I10 * ea[x] + j.I11 * eb[x];
        }
    }

    /// <summary>The covariant transverse shear along a (or b) at a point, from the displacements: w,a + x,a·θy − y,a·θx.</summary>
    void Covariant(double a, double b, bool alongA, Span<double> row)
    {
        Span<double> n = stackalloc double[4], na = stackalloc double[4], nb = stackalloc double[4];
        Shape(a, b, n, na, nb);
        var j = Jacobian(na, nb);
        var (dx, dy) = alongA ? (j.Xa, j.Ya) : (j.Xb, j.Yb);
        row.Clear();
        for (var i = 0; i < Count; i++)
        {
            row[3 * i] = alongA ? na[i] : nb[i];
            row[3 * i + 1] = -dy * n[i];
            row[3 * i + 2] = dx * n[i];
        }
    }

    (double A, double B, double W)[] Points => Count == 4 ? QuadPoints : TriPoints;

    /// <summary>The stiffness in the element's own axes, 6 dofs a node [u, v, w, θx, θy, θz] (row-major).</summary>
    double[] LocalStiffness()
    {
        var c = Count;
        var m = 3 * c;
        var size = 6 * c;
        var k = new double[size * size];
        var t = Thickness;
        var f = E / (1 - Nu * Nu);
        double[,] cm = { { f, f * Nu, 0 }, { f * Nu, f, 0 }, { 0, 0, f * (1 - Nu) / 2 } };
        var db = t * t * t / 12;
        var ds = ShearFactor * G * t;
        var gamma = Drilling * G * t;
        var (bm, bd, bb, bs) = (new double[3, m], new double[m], new double[3, m], new double[2, m]);
        var km = new double[m, m];
        var kb = new double[m, m];
        foreach (var (a, b, w) in Points)
        {
            Rows(a, b, bm, bd, bb, bs, out var det);
            var dv = w * det;
            for (var r = 0; r < m; r++)
                for (var s = 0; s < m; s++)
                {
                    double sm = 0, sb = 0;
                    for (var x = 0; x < 3; x++)
                        for (var y = 0; y < 3; y++)
                        {
                            sm += bm[x, r] * cm[x, y] * bm[y, s];
                            sb += bb[x, r] * cm[x, y] * bb[y, s];
                        }
                    km[r, s] += dv * (t * sm + gamma * bd[r] * bd[s]);
                    kb[r, s] += dv * (db * sb + ds * (bs[0, r] * bs[0, s] + bs[1, r] * bs[1, s]));
                }
        }
        // Into the node's six: membrane [u, v, θz] → 0, 1, 5; bending [w, θx, θy] → 2, 3, 4.
        int[] mOf = [0, 1, 5], bOf = [2, 3, 4];
        for (var r = 0; r < m; r++)
            for (var s = 0; s < m; s++)
            {
                int rm = 6 * (r / 3) + mOf[r % 3], sm = 6 * (s / 3) + mOf[s % 3];
                int rb = 6 * (r / 3) + bOf[r % 3], sb = 6 * (s / 3) + bOf[s % 3];
                k[rm * size + sm] += km[r, s];
                k[rb * size + sb] += kb[r, s];
            }
        return k;
    }

    /// <summary>The stiffness in global axes (row-major, <see cref="Size"/>²), each node's [ux, uy, uz, rx, ry, rz].</summary>
    public double[] GlobalStiffness()
    {
        var size = Size;
        Span<double> rot = [Ex.X, Ex.Y, Ex.Z, Ey.X, Ey.Y, Ey.Z, Ez.X, Ez.Y, Ez.Z];
        // K_g = Tᵀ·K·T, T block diagonal with R (rows Ex, Ey, Ez) on each 3 × 3 block.
        var tmp = new double[size * size];
        for (var r = 0; r < size; r++)
            for (var bc = 0; bc < size; bc += 3)
                for (var j = 0; j < 3; j++)
                {
                    var s = 0.0;
                    for (var x = 0; x < 3; x++) s += _k[r * size + bc + x] * rot[x * 3 + j];
                    tmp[r * size + bc + j] = s;
                }
        var g = new double[size * size];
        for (var br = 0; br < size; br += 3)
            for (var i = 0; i < 3; i++)
                for (var c = 0; c < size; c++)
                {
                    var s = 0.0;
                    for (var x = 0; x < 3; x++) s += rot[x * 3 + i] * tmp[(br + x) * size + c];
                    g[(br + i) * size + c] = s;
                }
        return g;
    }

    /// <summary>Global displacements of the element's nodes (6 a node) into its own axes.</summary>
    public double[] ToLocal(ReadOnlySpan<double> global)
    {
        var u = new double[Size];
        for (var b = 0; b < Size; b += 3)
        {
            var v = new Vec3(global[b], global[b + 1], global[b + 2]);
            u[b] = Vec3.Dot(v, Ex);
            u[b + 1] = Vec3.Dot(v, Ey);
            u[b + 2] = Vec3.Dot(v, Ez);
        }
        return u;
    }

    /// <summary>∫Nᵢ dA for each corner: the share of a uniform load on the element each node takes (consistent, Σ = area).</summary>
    public double[] Shares()
    {
        var s = new double[Count];
        Span<double> n = stackalloc double[4], na = stackalloc double[4], nb = stackalloc double[4];
        foreach (var (a, b, w) in Points)
        {
            Shape(a, b, n, na, nb);
            var det = Jacobian(na, nb).Det;
            for (var i = 0; i < Count; i++) s[i] += n[i] * w * det;
        }
        return s;
    }

    /// <summary>The nodal forces (global, 6 a node) of a uniform load on the element (kN/m², a global vector): its translations' consistent shares; no moments, the rotations being interpolated apart.</summary>
    public double[] Equivalent(Vec3 q)
    {
        var f = new double[Size];
        var s = Shares();
        for (var i = 0; i < Count; i++) { f[6 * i] = q.X * s[i]; f[6 * i + 1] = q.Y * s[i]; f[6 * i + 2] = q.Z * s[i]; }
        return f;
    }

    /// <summary>The resultants at a point of the parent element, from the local displacements.</summary>
    public ShellForces At(double a, double b, ReadOnlySpan<double> uLocal)
    {
        var c = Count;
        var m = 3 * c;
        var (bm, bd, bb, bs) = (new double[3, m], new double[m], new double[3, m], new double[2, m]);
        Rows(a, b, bm, bd, bb, bs, out _);
        Span<double> um = stackalloc double[12], ub = stackalloc double[12];
        for (var i = 0; i < c; i++)
        {
            um[3 * i] = uLocal[6 * i]; um[3 * i + 1] = uLocal[6 * i + 1]; um[3 * i + 2] = uLocal[6 * i + 5];
            ub[3 * i] = uLocal[6 * i + 2]; ub[3 * i + 1] = uLocal[6 * i + 3]; ub[3 * i + 2] = uLocal[6 * i + 4];
        }
        Span<double> e = stackalloc double[3], kap = stackalloc double[3], gam = stackalloc double[2];
        for (var x = 0; x < m; x++)
        {
            for (var r = 0; r < 3; r++) { e[r] += bm[r, x] * um[x]; kap[r] += bb[r, x] * ub[x]; }
            gam[0] += bs[0, x] * ub[x];
            gam[1] += bs[1, x] * ub[x];
        }
        var f = E / (1 - Nu * Nu);
        var t = Thickness;
        var db = t * t * t / 12;
        var ds = ShearFactor * G * t;
        return new ShellForces(
            t * f * (e[0] + Nu * e[1]), t * f * (Nu * e[0] + e[1]), t * f * (1 - Nu) / 2 * e[2],
            db * f * (kap[0] + Nu * kap[1]), db * f * (Nu * kap[0] + kap[1]), db * f * (1 - Nu) / 2 * kap[2],
            ds * gam[0], ds * gam[1]);
    }

    /// <summary>
    /// The resultants at the element's centre and at its corners: on the quadrilateral the Gauss points' values extrapolated
    /// bilinearly to the corners (their mean at the centre); on the triangle taken at the corners and the centroid.
    /// </summary>
    public (ShellForces Centre, ShellForces[] Corners) Resultants(ReadOnlySpan<double> uLocal)
    {
        if (Count == 3)
            return (At(1.0 / 3, 1.0 / 3, uLocal), [At(0, 0, uLocal), At(1, 0, uLocal), At(0, 1, uLocal)]);
        var gp = new ShellForces[4];
        for (var g = 0; g < 4; g++) gp[g] = At(QuadPoints[g].A, QuadPoints[g].B, uLocal);
        var corners = new ShellForces[4];
        var s3 = Math.Sqrt(3);
        for (var i = 0; i < 4; i++)
        {
            var (a, b) = (Xi[i] * s3, Eta[i] * s3);
            var sum = default(ShellForces);
            for (var g = 0; g < 4; g++) sum += gp[g] * (0.25 * (1 + Xi[g] * a) * (1 + Eta[g] * b));
            corners[i] = sum;
        }
        var centre = (gp[0] + gp[1] + gp[2] + gp[3]) * 0.25;
        return (centre, corners);
    }
}
