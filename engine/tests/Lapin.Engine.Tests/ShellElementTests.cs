using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The flat shell element (docs/plan-fem.md §10.2, §10.8): six rigid-body modes and no spurious ones; the membrane and the
/// bending patch tests on MacNeal and Harder's distorted patch; plates against Timoshenko and Woinowsky-Krieger (simply
/// supported and clamped, square and 2:1, uniform load) with convergence on refinement and no shear locking when thin; a
/// cantilever wall in its plane against Timoshenko's beam; Scordelis and Lo's roof; a slab on beams sharing its nodes, the
/// beams below it on rigid arms, in balance.
/// </summary>
public class ShellElementTests(ITestOutputHelper output)
{
    static double Rel(double expected, double actual) => Math.Abs(actual - expected) / Math.Abs(expected);

    /// <summary>A symmetric matrix's eigenvalues by cyclic Jacobi rotations.</summary>
    static double[] Eigenvalues(double[] a0, int n)
    {
        var a = (double[])a0.Clone();
        for (var sweep = 0; sweep < 100; sweep++)
        {
            var off = 0.0;
            for (var i = 0; i < n; i++) for (var j = i + 1; j < n; j++) off += a[i * n + j] * a[i * n + j];
            if (off < 1e-30) break;
            for (var p = 0; p < n; p++)
                for (var q = p + 1; q < n; q++)
                {
                    var apq = a[p * n + q];
                    if (Math.Abs(apq) < 1e-300) continue;
                    var theta = (a[q * n + q] - a[p * n + p]) / (2 * apq);
                    var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    var c = 1 / Math.Sqrt(t * t + 1);
                    var s = t * c;
                    for (var k = 0; k < n; k++)
                    {
                        double akp = a[k * n + p], akq = a[k * n + q];
                        a[k * n + p] = c * akp - s * akq;
                        a[k * n + q] = s * akp + c * akq;
                    }
                    for (var k = 0; k < n; k++)
                    {
                        double apk = a[p * n + k], aqk = a[q * n + k];
                        a[p * n + k] = c * apk - s * aqk;
                        a[q * n + k] = s * apk + c * aqk;
                    }
                }
        }
        return Enumerable.Range(0, n).Select(i => a[i * n + i]).OrderBy(x => x).ToArray();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    public void A_free_element_has_six_rigid_body_modes_and_no_other(int corners)
    {
        // A distorted element, tilted in space.
        Vec3[] pts = corners == 4
            ? [new(0, 0, 0), new(2.1, 0.3, 0.4), new(1.8, 1.7, 0.9), new(-0.2, 1.2, 0.5)]
            : [new(0, 0, 0), new(2.1, 0.3, 0.4), new(0.6, 1.5, 0.7)];
        // Planar: move the fourth onto the plane of the first three.
        if (corners == 4)
        {
            var n = Vec3.Cross(pts[1] - pts[0], pts[2] - pts[0]).Normalized();
            pts[3] -= n * Vec3.Dot(pts[3] - pts[0], n);
        }
        foreach (var t in new[] { 0.2, 0.01 })
        {
            var e = new ShellElement { Id = "e", Nodes = Enumerable.Range(0, corners).ToArray(), E = 30e6, Nu = 0.2, Thickness = t };
            e.Prepare(pts);
            Assert.True(e.Warp < 1e-12);
            var k = e.GlobalStiffness();
            var size = e.Size;
            for (var i = 0; i < size; i++) for (var j = 0; j < size; j++) Assert.Equal(k[i * size + j], k[j * size + i], 6);
            var ev = Eigenvalues(k, size);
            var top = ev[^1];
            Assert.All(ev.Take(6), x => Assert.True(Math.Abs(x) < 1e-9 * top, $"a rigid mode with {x / top:E2}"));
            Assert.True(ev[6] > 1e-9 * top, $"a spurious mode: {ev[6] / top:E2}");
        }
    }

    // MacNeal and Harder's patch ("A proposed standard set of problems to test finite element accuracy", Finite Elem. Anal.
    // Des. 1, 1985): a 0.24 × 0.12 rectangle, four inner nodes, five distorted quadrilaterals; E = 1e6, ν = 0.25, t = 0.001.
    static readonly Vec3[] Patch =
    [
        new(0, 0, 0), new(0.24, 0, 0), new(0.24, 0.12, 0), new(0, 0.12, 0),
        new(0.04, 0.02, 0), new(0.18, 0.03, 0), new(0.16, 0.08, 0), new(0.08, 0.08, 0),
    ];
    static readonly int[][] PatchQuads = [[0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7], [4, 5, 6, 7]];

    /// <summary>The patch with its corners' six displacements prescribed by a field, its inner nodes solved (dense).</summary>
    static (double[] U, List<ShellElement> Elements) SolvePatch(Func<Vec3, double[]> field, bool triangles = false)
    {
        var elems = new List<ShellElement>();
        foreach (var q in PatchQuads)
        {
            if (triangles)
            {
                elems.Add(new ShellElement { Id = "t", Nodes = [q[0], q[1], q[2]], E = 1e6, Nu = 0.25, Thickness = 0.001 });
                elems.Add(new ShellElement { Id = "t", Nodes = [q[0], q[2], q[3]], E = 1e6, Nu = 0.25, Thickness = 0.001 });
            }
            else elems.Add(new ShellElement { Id = "q", Nodes = q, E = 1e6, Nu = 0.25, Thickness = 0.001 });
        }
        var n = Patch.Length * 6;
        var k = new double[n, n];
        foreach (var e in elems)
        {
            e.Prepare(Patch);
            var g = e.GlobalStiffness();
            var m = e.Size;
            for (var r = 0; r < m; r++)
                for (var c = 0; c < m; c++)
                    k[e.Nodes[r / 6] * 6 + r % 6, e.Nodes[c / 6] * 6 + c % 6] += g[r * m + c];
        }
        var u = new double[n];
        for (var i = 0; i < 4; i++) Array.Copy(field(Patch[i]), 0, u, i * 6, 6);
        // K_ff·u_f = −K_fp·u_p over the inner nodes' 24 equations.
        var free = Enumerable.Range(24, 24).ToArray();
        var a = new double[24, 25];
        for (var r = 0; r < 24; r++)
        {
            for (var c = 0; c < 24; c++) a[r, c] = k[free[r], free[c]];
            for (var c = 0; c < 24; c++) a[r, 24] -= k[free[r], c] * u[c];
        }
        for (var c = 0; c < 24; c++)
        {
            var piv = Enumerable.Range(c, 24 - c).MaxBy(r => Math.Abs(a[r, c]));
            for (var x = 0; x <= 24; x++) (a[c, x], a[piv, x]) = (a[piv, x], a[c, x]);
            for (var r = 0; r < 24; r++)
            {
                if (r == c) continue;
                var f = a[r, c] / a[c, c];
                for (var x = c; x <= 24; x++) a[r, x] -= f * a[c, x];
            }
        }
        for (var r = 0; r < 24; r++) u[free[r]] = a[r, 24] / a[r, r];
        return (u, elems);
    }

    static double[] Gather(double[] u, ShellElement e)
    {
        var g = new double[e.Size];
        for (var x = 0; x < e.Count; x++) Array.Copy(u, e.Nodes[x] * 6, g, 6 * x, 6);
        return g;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_membrane_passes_the_patch_test_with_its_drilling_rotation(bool triangles)
    {
        // u = 1e-3 (x + y/2), v = 1e-3 (y + x/2) + 2e-3 x: constant strains εx = εy = 1e-3, γxy = 3e-3; ω = ½(v,x − u,y) = 1e-3.
        static double[] Field(Vec3 p) => [1e-3 * (p.X + p.Y / 2), 1e-3 * (p.Y + p.X / 2) + 2e-3 * p.X, 0, 0, 0, 1e-3];
        var (u, elems) = SolvePatch(Field, triangles);
        for (var i = 4; i < 8; i++)
        {
            var exact = Field(Patch[i]);
            for (var d = 0; d < 6; d++) Assert.True(Math.Abs(u[i * 6 + d] - exact[d]) < 1e-12, $"node {i} dof {d}: {u[i * 6 + d]} vs {exact[d]}");
        }
        // N = t·E/(1 − ν²)·(εx + ν εy), Nxy = t·G·γxy.
        const double t = 0.001, E = 1e6, nu = 0.25;
        var nx = t * E / (1 - nu * nu) * (1e-3 + nu * 1e-3);
        var nxy = t * E / (2 * (1 + nu)) * 3e-3;
        foreach (var e in elems)
        {
            var (centre, corners) = e.Resultants(e.ToLocal(Gather(u, e)));
            foreach (var f in corners.Append(centre))
            {
                Assert.Equal(nx, f.Nx, 9);
                Assert.Equal(nx, f.Ny, 9);
                Assert.Equal(nxy, f.Nxy, 9);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_plate_passes_the_constant_curvature_patch_test(bool triangles)
    {
        // w = 1e-3 (x² + xy + y²)/2, Kirchhoff rotations θx = w,y, θy = −w,x: κx = κy = κxy = −1e-3, no shear.
        static double[] Field(Vec3 p) => [0, 0, 1e-3 * (p.X * p.X + p.X * p.Y + p.Y * p.Y) / 2, 1e-3 * (p.X / 2 + p.Y), -1e-3 * (p.X + p.Y / 2), 0];
        var (u, elems) = SolvePatch(Field, triangles);
        for (var i = 4; i < 8; i++)
        {
            var exact = Field(Patch[i]);
            for (var d = 0; d < 6; d++) Assert.True(Math.Abs(u[i * 6 + d] - exact[d]) < 1e-12, $"node {i} dof {d}: {u[i * 6 + d]} vs {exact[d]}");
        }
        const double t = 0.001, E = 1e6, nu = 0.25;
        var d0 = E * t * t * t / (12 * (1 - nu * nu));
        foreach (var e in elems)
        {
            var (centre, corners) = e.Resultants(e.ToLocal(Gather(u, e)));
            foreach (var f in corners.Append(centre))
            {
                Assert.Equal(d0 * (-1e-3 - nu * 1e-3), f.Mx, 12);
                Assert.Equal(d0 * (-1e-3 - nu * 1e-3), f.My, 12);
                Assert.Equal(d0 * (1 - nu) / 2 * -1e-3, f.Mxy, 12);
                Assert.Equal(0, f.Qx, 9);
                Assert.Equal(0, f.Qy, 9);
            }
        }
    }

    /// <summary>
    /// A rectangular plate a × b on a regular mesh in the XY plane, its edges simply supported ("hard": w and the turning
    /// along the edge held) or clamped, under a uniform load q downwards. In-plane movement is held (pure bending).
    /// </summary>
    static (FrameProblem P, int Centre, int EdgeMid) Plate(double a, double b, int nx, int ny, double t, bool clamped, double q, bool triangles = false)
    {
        var p = new FrameProblem();
        for (var j = 0; j <= ny; j++)
            for (var i = 0; i <= nx; i++)
            {
                var (x, y) = (a * i / nx, b * j / ny);
                bool onX = i == 0 || i == nx, onY = j == 0 || j == ny;
                // Membrane held everywhere; on an edge w held, and the turning about the edge's normal (SS: an edge x = const
                // holds rx, as w,y = 0 along it) or both (clamped).
                var held = new[] { true, true, onX || onY, clamped ? onX || onY : onX, clamped ? onX || onY : onY, true };
                p.AddNode(new Vec3(x, y, 0), $"N{i}.{j}", held);
            }
        int N(int i, int j) => j * (nx + 1) + i;
        var c = new CaseLoads("q");
        for (var j = 0; j < ny; j++)
            for (var i = 0; i < nx; i++)
            {
                int[] quad = [N(i, j), N(i + 1, j), N(i + 1, j + 1), N(i, j + 1)];
                if (triangles)
                {
                    p.Shells.Add(new ShellElement { Id = $"t{i}.{j}a", Nodes = [quad[0], quad[1], quad[2]], E = 30e6, Nu = 0.3, Thickness = t, Group = "plate" });
                    p.Shells.Add(new ShellElement { Id = $"t{i}.{j}b", Nodes = [quad[0], quad[2], quad[3]], E = 30e6, Nu = 0.3, Thickness = t, Group = "plate" });
                    c.Surface.Add((p.Shells.Count - 2, new Vec3(0, 0, -q)));
                    c.Surface.Add((p.Shells.Count - 1, new Vec3(0, 0, -q)));
                }
                else
                {
                    p.Shells.Add(new ShellElement { Id = $"q{i}.{j}", Nodes = quad, E = 30e6, Nu = 0.3, Thickness = t, Group = "plate" });
                    c.Surface.Add((p.Shells.Count - 1, new Vec3(0, 0, -q)));
                }
            }
        p.Cases.Add(c);
        return (p, N(nx / 2, ny / 2), N(nx, ny / 2));
    }

    static FrameSolution Solve(FrameProblem p)
    {
        var s = FrameSolver.Solve(p);
        Assert.True(s.Ok, s.Mechanism is { } m ? $"mechanism at {m.Node} {FrameSolver.DofName(m.Dof)}" : s.Refused);
        foreach (var c in s.Cases) Assert.True(c.Balance < 1e-9, $"case {c.Id} off balance by {c.Balance}");
        return s;
    }

    [Theory]
    // Simply supported square: w = 0.00406235 q a⁴/D, Mx = My = 0.0478864 q a² (Navier's series; Timoshenko and
    // Woinowsky-Krieger, "Theory of Plates and Shells", 2nd ed., §30, Table 8, ν = 0.3).
    [InlineData(1.0, false, 0.00406235, 0.0478864, 0.0478864)]
    // Simply supported, b = 2a: w = 0.01013 q a⁴/D, Mx = 0.1017 q a², My = 0.0464 q a² (Table 8).
    [InlineData(2.0, false, 0.01013, 0.1017, 0.0464)]
    // Clamped square: w = 0.00126532 q a⁴/D, M = 0.022905 q a² at the centre (Timoshenko and Woinowsky-Krieger §44,
    // Table 35: 0.00126, 0.0231; refined by Taylor and Govindjee, "Solution of clamped rectangular plate problems",
    // Commun. Numer. Meth. Engng 20, 2004).
    [InlineData(1.0, true, 0.00126532, 0.022905, 0.022905)]
    public void A_plate_under_a_uniform_load_converges_to_the_plate_theory(double ratio, bool clamped, double alpha, double betaX, double betaY)
    {
        const double a = 4, t = 0.04, q = 10;
        var d = 30e6 * t * t * t / (12 * (1 - 0.09));
        var wRef = alpha * q * Math.Pow(a, 4) / d;
        var errors = new List<(double W, double Mx, double My)>();
        foreach (var n in new[] { 4, 8, 16, 32 })
        {
            var (p, centre, _) = Plate(a, a * ratio, n, (int)(n * ratio), t, clamped, q);
            var s = Solve(p);
            var r = s.Cases[0];
            var w = -r.U[centre * 6 + 2];
            var m = ShellSmoothing.AtNodes(p, r)[("plate", centre)];
            // Sagging: the bottom (−z) face in tension, Mx negative. The patch recovery reads the same there.
            errors.Add((Rel(wRef, w), Rel(betaX * q * a * a, -m.Mx), Rel(betaY * q * a * a, -m.My)));
            var spr = ShellSmoothing.Recovered(p, r)[("plate", centre)];
            // (A plane fitted over a peak sits a little under it: O(h²).)
            if (n >= 16) Assert.True(Math.Abs(spr.Mx - m.Mx) < 0.02 * Math.Abs(m.Mx) && Math.Abs(spr.My - m.My) < 0.02 * Math.Abs(m.My), $"{spr} vs {m}");
            output.WriteLine($"{n}: w {w / wRef:F5}, Mx {-m.Mx / (betaX * q * a * a):F5}, My {-m.My / (betaY * q * a * a):F5}");
        }
        // Converging: each refinement closer (to the thick plate's own value, which a clamped plate a/t = 100 has 0.1 % over
        // Kirchhoff's), the 16 × 16 mesh within 1.5 %, the 32 × 32 within 0.5 % (deflection) and 1 % (moments).
        for (var i = 1; i < errors.Count; i++) Assert.True(errors[i].W < errors[i - 1].W || errors[i].W < 0.002);
        Assert.True(errors[2].W < 0.015 && errors[2].Mx < 0.02 && errors[2].My < 0.02, $"16: {errors[2]}");
        Assert.True(errors[3].W < 0.005 && errors[3].Mx < 0.01 && errors[3].My < 0.01, $"32: {errors[3]}");
    }

    /// <summary>A big plate through the whole solver, timed (LAPIN_FEM_BENCH=1): 150 × 150 and 180 × 180 quadrilaterals, all six dofs free inside.</summary>
    [Fact]
    public void A_big_plate_timed()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAPIN_FEM_BENCH"))) return;
        foreach (var n in new[] { 150, 180 })
        {
            var (p, centre, _) = Plate(30, 30, n, n, 0.2, true, 10);
            // Only the edges held: the membrane free inside, as a floor's is.
            for (var i = 0; i < p.Nodes.Count; i++) if (p.Held[i] is { } h && !h[2]) p.Held[i] = null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var s = Solve(p);
            output.WriteLine($"plate {n} × {n}: {s.Dofs} DOFs, {s.Profile * 8 / 1e6:N0} MB factor, {sw.Elapsed.TotalSeconds:N2} s, balance {s.Cases[0].Balance:E1}");
        }
    }

    [Fact]
    public void A_clamped_plates_edge_moment_is_0_0513_q_a2()
    {
        // Timoshenko and Woinowsky-Krieger Table 35: −0.0513 q a² at the middle of an edge (−0.05133, Taylor and Govindjee).
        const double a = 4, t = 0.04, q = 10;
        var (p, _, edge) = Plate(a, a, 32, 32, t, true, q);
        var r = Solve(p).Cases[0];
        // The corner mean lags by half an element at the edge (MITC4's Mx is constant across an element); the patch recovery does not.
        var mean = ShellSmoothing.AtNodes(p, r)[("plate", edge)];
        var m = ShellSmoothing.Recovered(p, r)[("plate", edge)];
        output.WriteLine($"edge Mx {m.Mx / (q * a * a):F5} (corner mean {mean.Mx / (q * a * a):F5})");
        // Hogging there: the top face in tension, Mx positive.
        Assert.True(Rel(0.05133 * q * a * a, m.Mx) < 0.02, $"{m.Mx / (q * a * a)}");
    }

    [Theory]
    [InlineData(0.04)]
    [InlineData(0.004)]
    [InlineData(0.0004)]
    public void A_thin_plate_does_not_lock_in_shear(double t)
    {
        // a/t from 100 to 10,000: the simply supported square on an 8 × 8 mesh stays within 3 % of Kirchhoff's deflection.
        const double a = 4, q = 1e-3;
        var d = 30e6 * t * t * t / (12 * (1 - 0.09));
        var (p, centre, _) = Plate(a, a, 8, 8, t, false, q);
        // The balance is K·u summed; with the shear a million times stiffer than the bending, its roundoff reaches 1e-8.
        var s = FrameSolver.Solve(p);
        Assert.True(s.Ok && s.Cases[0].Balance < 1e-6, $"{s.Cases[0].Balance}");
        var w = -s.Cases[0].U[centre * 6 + 2];
        Assert.True(Rel(0.00406235 * q * Math.Pow(a, 4) / d, w) < 0.03, $"t {t}: {w / (0.00406235 * q * Math.Pow(a, 4) / d)}");
    }

    [Fact]
    public void Triangles_converge_on_the_simply_supported_plate_too()
    {
        // MITC3 is stiffer than MITC4; the leftovers of the mesher are few, but they too must converge.
        const double a = 4, t = 0.04, q = 10;
        var d = 30e6 * t * t * t / (12 * (1 - 0.09));
        var wRef = 0.00406235 * q * Math.Pow(a, 4) / d;
        var errs = new List<double>();
        foreach (var n in new[] { 8, 16, 32 })
        {
            var (p, centre, _) = Plate(a, a, n, n, t, false, q, triangles: true);
            var w = -Solve(p).Cases[0].U[centre * 6 + 2];
            errs.Add(Rel(wRef, w));
            output.WriteLine($"tri {n}: w {w / wRef:F5}");
        }
        Assert.True(errs[1] < errs[0] && errs[2] < errs[1]);
        Assert.True(errs[2] < 0.02, $"{errs[2]}");
    }

    [Fact]
    public void A_cantilever_wall_deflects_in_its_plane_as_a_Timoshenko_beam()
    {
        // A wall B = 3 m long, H = 12 m high, t = 0.25 m, fixed at its foot, P = 100 kN along it at its head:
        // δ = PH³/3EI + PH/(κGA), κ = 5/6.
        const double B = 3, H = 12, t = 0.25, E = 30e6, nu = 0.2, P = 100;
        var g = E / (2 * (1 + nu));
        var i0 = t * B * B * B / 12;
        var delta = P * H * H * H / (3 * E * i0) + P * H / (5.0 / 6 * g * t * B);
        var errs = new List<double>();
        foreach (var (nx, nz) in new[] { (2, 8), (4, 16), (8, 32) })
        {
            var p = new FrameProblem();
            for (var k = 0; k <= nz; k++)
                for (var i = 0; i <= nx; i++)
                    p.AddNode(new Vec3(B * i / nx, 0, H * k / nz), $"N{i}.{k}", k == 0 ? [true, true, true, true, true, true] : null);
            int N(int i, int k) => k * (nx + 1) + i;
            for (var k = 0; k < nz; k++)
                for (var i = 0; i < nx; i++)
                    p.Shells.Add(new ShellElement { Id = $"w{i}.{k}", Nodes = [N(i, k), N(i + 1, k), N(i + 1, k + 1), N(i, k + 1)], E = E, Nu = nu, Thickness = t, Group = "wall" });
            var c = new CaseLoads("P");
            // The head's force shared as a uniform line load would be: half shares at the ends.
            for (var i = 0; i <= nx; i++) c.AddNodal(N(i, nz), new Vec3(P / nx * (i == 0 || i == nx ? 0.5 : 1), 0, 0));
            p.Cases.Add(c);
            var r = Solve(p).Cases[0];
            var top = Enumerable.Range(0, nx + 1).Average(i => r.U[N(i, nz) * 6]);
            errs.Add(Rel(delta, top));
            output.WriteLine($"wall {nx}×{nz}: δ {top / delta:F5}");
            // The foot's reactions: −P along, the overturning P·H.
            Assert.Equal(-P, r.Reactions.Values.Sum(x => x[0]), 6);
            var overturn = r.Reactions.Sum(kv => kv.Value[4] + p.Nodes[kv.Key].X * kv.Value[2] * -1);
            Assert.True(Rel(P * H, Math.Abs(overturn)) < 1e-6, $"{overturn}");
        }
        Assert.True(errs[1] < 0.03 && errs[2] < 0.015, string.Join(", ", errs));
    }

    [Fact]
    public void Scordelis_and_Los_roof_sags_0_3024_at_the_middle_of_its_free_edge()
    {
        // A cylindrical roof R = 25, L = 50, 80° of arc, t = 0.25, E = 4.32e8, ν = 0, self-weight 90 per unit area, on rigid
        // diaphragms at its ends (Scordelis and Lo, J. ACI 61, 1964; MacNeal and Harder 1985: 0.3024). A quarter, by symmetry.
        const double R = 25, L = 50, phi = 40 * Math.PI / 180, t = 0.25;
        var errs = new List<double>();
        foreach (var n in new[] { 8, 16, 24 })
        {
            var p = new FrameProblem();
            for (var j = 0; j <= n; j++)
                for (var i = 0; i <= n; i++)
                {
                    var x = L / 2 * i / n;
                    var a = phi * j / n;
                    // x = 0: the middle (symmetry: ux, ry, rz held); x = L/2: the diaphragm (uy, uz held); φ = 0: the crown (uy, rx, rz held).
                    var held = new bool[6];
                    if (i == 0) { held[0] = held[4] = held[5] = true; }
                    if (i == n) { held[1] = held[2] = true; }
                    if (j == 0) { held[1] = held[3] = held[5] = true; }
                    p.AddNode(new Vec3(x, R * Math.Sin(a), R * Math.Cos(a)), $"N{i}.{j}", held.Any(h => h) ? held : null);
                }
            int N(int i, int j) => j * (n + 1) + i;
            var c = new CaseLoads("g");
            for (var j = 0; j < n; j++)
                for (var i = 0; i < n; i++)
                {
                    p.Shells.Add(new ShellElement { Id = $"r{i}.{j}", Nodes = [N(i, j), N(i + 1, j), N(i + 1, j + 1), N(i, j + 1)], E = 4.32e8, Nu = 0, Thickness = t, Group = "roof" });
                    c.Surface.Add((p.Shells.Count - 1, new Vec3(0, 0, -90)));
                }
            p.Cases.Add(c);
            var r = Solve(p).Cases[0];
            var w = -r.U[N(0, n) * 6 + 2];
            errs.Add(Rel(0.3024, w));
            output.WriteLine($"roof {n}: w {w:F5} ({w / 0.3024:F4})");
        }
        Assert.True(errs[2] < 0.02, string.Join(", ", errs));
    }

    [Fact]
    public void A_slab_on_beams_below_it_shares_their_nodes_and_balances()
    {
        // A 6 × 6 m slab (t = 0.2) on four edge beams 0.3 × 0.6 whose axes lie 0.4 m below its mid-plane (rigid arms), held at
        // its four corners; 5 kPa on it. ΣR = ΣF, each corner a quarter, and the composite action: the beams in tension at midspan.
        const double a = 6, t = 0.2, E = 30e6, nu = 0.2, q = 5;
        const int n = 12;
        var p = new FrameProblem();
        for (var j = 0; j <= n; j++)
            for (var i = 0; i <= n; i++)
            {
                var corner = (i == 0 || i == n) && (j == 0 || j == n);
                p.AddNode(new Vec3(a * i / n, a * j / n, 0), $"N{i}.{j}", corner ? [true, true, true, false, false, false] : null);
            }
        int N(int i, int j) => j * (n + 1) + i;
        var c = new CaseLoads("q");
        for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++)
            {
                p.Shells.Add(new ShellElement { Id = $"s{i}.{j}", Nodes = [N(i, j), N(i + 1, j), N(i + 1, j + 1), N(i, j + 1)], E = E, Nu = nu, Thickness = t, Group = "slab" });
                c.Surface.Add((p.Shells.Count - 1, new Vec3(0, 0, -q)));
            }
        var arm = new Vec3(0, 0, -0.4);
        FrameElement Beam(int i, int j) => new()
        {
            Id = $"b{i}-{j}", I = i, J = j, Local2 = Vec3.UnitZ, E = E, G = E / (2 * (1 + nu)), A = 0.18, I22 = 0.6 * 0.027 / 12, I33 = 0.3 * 0.216 / 12,
            Jt = 0.0045, As2 = 0.15, As3 = 0.15, OffI = arm, OffJ = arm,
        };
        var midBeams = new List<int>();
        for (var k = 0; k < n; k++)
        {
            foreach (var (i0, j0, i1, j1) in new[] { (k, 0, k + 1, 0), (k, n, k + 1, n), (0, k, 0, k + 1), (n, k, n, k + 1) })
            {
                p.Elements.Add(Beam(N(i0, j0), N(i1, j1)));
                if (k == n / 2) midBeams.Add(p.Elements.Count - 1);
            }
        }
        p.Cases.Add(c);
        var s = Solve(p);
        var r = s.Cases[0];
        Assert.Equal(q * a * a, r.SumReactions.Z, 9);
        foreach (var (node, react) in r.Reactions) Assert.Equal(q * a * a / 4, react[2], 6);
        foreach (var b in midBeams) Assert.True(Stations.At(p.Elements[b], r.Elements[b], 0).N > 0, "the beam in tension under the slab");
        // The slab in compression along the beam over it at midspan.
        var nodal = ShellSmoothing.AtNodes(p, r);
        Assert.True(nodal[("slab", N(n / 2, 0))].Nx < 0);
    }
}
