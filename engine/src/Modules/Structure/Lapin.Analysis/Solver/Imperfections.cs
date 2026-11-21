using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>One row of columns in the vertical plane of a sway (§14.14): its lines' count m and φ.</summary>
public sealed record ImperfectionRow(int Lines, int M, double Phi);

/// <summary>
/// The sway imperfection of one horizontal direction (§14.14): φ per column piece (by its row and material), the equivalent
/// forces (φ·|N| at the top, −φ·|N| at the foot of every compressed column piece), the rows, and the force the frame takes
/// sideways (the forces on the nodes not held along the direction: ΣφN at the columns' feet).
/// </summary>
public sealed record ImperfectionSet(Vec3 Direction, double H, double AlphaH, List<ImperfectionRow> Rows, Dictionary<int, double> PhiOf, CaseLoads Loads, double Force)
{
    /// <summary>The largest φ of the direction's columns, with its row's m (0: no column, or a timber column's, which has none).</summary>
    public (double Phi, int M) Governing { get; init; }
    /// <summary>Some column is timber: its φ by EN 1995-1-1 5.4.4(2).</summary>
    public bool Timber { get; init; }
}

/// <summary>
/// Global sway imperfections (docs/plan-fem.md §14.14): SR EN 1993-1-1 5.3.2(3) φ = φ0·αh·αm, φ0 = 1/200, αh = 2/√h in
/// [2/3, 1], αm = √(0.5 (1 + 1/m)), m the columns of a row carrying at least half the row's mean vertical load; the same form
/// for concrete (SR EN 1992-1-1 5.2(5), θi); timber by SR EN 1995-1-1 5.4.4(2) (0.005, × √(5/h) above 5 m). Replaced by the
/// equivalent horizontal forces of 5.3.2(7): φ·NEd at each column piece's top in the sway's direction, the opposite at its foot.
/// Written for Lapin from the standards' text.
/// </summary>
public static class Imperfections
{
    public const double Phi0 = 1.0 / 200;
    /// <summary>A column: a bar piece within 10° of the vertical.</summary>
    public static readonly double Vertical = Math.Cos(10 * Math.PI / 180);
    /// <summary>Column pieces within this in plan are one line of columns; lines within <see cref="RowTolerance"/> across the sway one row.</summary>
    public const double LineTolerance = 0.1, RowTolerance = 0.3;

    /// <summary>αh = 2/√h, 2/3 ≤ αh ≤ 1 (h in metres).</summary>
    public static double AlphaH(double h) => h <= 0 ? 1 : Math.Clamp(2 / Math.Sqrt(h), 2.0 / 3, 1);

    /// <summary>αm = √(0.5 (1 + 1/m)).</summary>
    public static double AlphaM(int m) => Math.Sqrt(0.5 * (1 + 1.0 / Math.Max(1, m)));

    /// <summary>φ = φ0·αh·αm (EN 1993-1-1 5.3.2(3); EN 1992-1-1 5.2(5) θi of the same form).</summary>
    public static double Phi(double h, int m) => Phi0 * AlphaH(h) * AlphaM(m);

    /// <summary>Timber frames (EN 1995-1-1 5.4.4(2)): 0.005 up to 5 m, 0.005·√(5/h) above.</summary>
    public static double PhiTimber(double h) => h <= 5 ? 0.005 : 0.005 * Math.Sqrt(5 / h);

    /// <summary>The column pieces (active bars within 10° of the vertical): index, bottom and top node, their compression (kN, 0 when in tension).</summary>
    public static List<(int K, int Bottom, int Top, double N)> Columns(FrameProblem p, double[] axial)
    {
        var o = new List<(int, int, int, double)>();
        var tiny = 1e-9 * Math.Max(1, axial.Length == 0 ? 0 : axial.Max(Math.Abs));
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var e = p.Elements[k];
            if (e.Inactive) continue;
            var (a, b) = (p.Nodes[e.I] + e.OffI, p.Nodes[e.J] + e.OffJ);
            var d = b - a;
            if (d.Length < 1e-9 || Math.Abs(d.Z) / d.Length < Vertical) continue;
            var n = axial[k] < -tiny ? -axial[k] : 0;
            o.Add(d.Z > 0 ? (k, e.I, e.J, n) : (k, e.J, e.I, n));
        }
        return o;
    }

    /// <summary>Groups of values within a tolerance of their neighbour (sorted), as index lists.</summary>
    static List<List<int>> Cluster(IReadOnlyList<double> values, double tol)
    {
        var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToList();
        var o = new List<List<int>>();
        foreach (var i in order)
        {
            if (o.Count > 0 && values[i] - values[o[^1][^1]] <= tol) o[^1].Add(i);
            else o.Add([i]);
        }
        return o;
    }

    /// <summary>
    /// The imperfection along a horizontal direction (unit, X or Y) for a result's axial forces: the columns, their lines
    /// and rows, φ per piece, the equivalent forces as loads.
    /// </summary>
    public static ImperfectionSet Of(FrameProblem p, double[] axial, Vec3 direction, string id)
    {
        // h is the structure's: every column's, in compression or not; the forces are the compressed ones'.
        var all = Columns(p, axial);
        var loads = new CaseLoads(id);
        var h = all.Count == 0 ? 0 : all.Max(c => p.Nodes[c.Top].Z) - all.Min(c => p.Nodes[c.Bottom].Z);
        var cols = all.Where(c => c.N > 0).ToList();
        if (cols.Count == 0) return new ImperfectionSet(direction, h, AlphaH(h), [], [], loads, 0);
        var alphaH = AlphaH(h);
        // Lines: the pieces on one vertical (their mid-points in plan within 0.1 m).
        Vec2 Mid(int i) => ((p.Nodes[cols[i].Bottom] + p.Nodes[cols[i].Top]) * 0.5).Xy;
        var lines = new List<List<int>>();
        var centres = new List<Vec2>();
        for (var i = 0; i < cols.Count; i++)
        {
            var m = Mid(i);
            var at = centres.FindIndex(c => (c - m).Length <= LineTolerance);
            if (at < 0) { lines.Add([i]); centres.Add(m); }
            else lines[at].Add(i);
        }
        var lineN = lines.Select(l => l.Max(i => cols[i].N)).ToList();
        // Rows across the sway: for X the lines of one Y, for Y those of one X.
        var across = centres.Select(c => Math.Abs(direction.X) >= Math.Abs(direction.Y) ? c.Y : c.X).ToList();
        var rows = new List<ImperfectionRow>();
        var phiOf = new Dictionary<int, double>();
        foreach (var row in Cluster(across, RowTolerance))
        {
            var mean = row.Average(l => lineN[l]);
            var m = Math.Max(1, row.Count(l => lineN[l] >= 0.5 * mean - 1e-9));
            var phi = Phi(h, m);
            rows.Add(new ImperfectionRow(row.Count, m, phi));
            foreach (var l in row)
                foreach (var i in lines[l])
                    phiOf[cols[i].K] = p.Elements[cols[i].K].Material == "timber" ? PhiTimber(h) : phi;
        }
        foreach (var c in cols)
        {
            var f = direction * (phiOf[c.K] * c.N);
            loads.AddNodal(c.Top, f);
            loads.AddNodal(c.Bottom, -f);
        }
        // What the frame takes sideways: the forces on its free nodes (a foot's goes into its support), ΣφN of the columns' feet.
        var dof = Math.Abs(direction.X) >= Math.Abs(direction.Y) ? 0 : 1;
        var force = loads.Nodal.Where(x => p.Held[x.Key] is not { } held || !held[dof]).Sum(x => Vec3.Dot(x.Value, direction));
        var top = cols.MaxBy(c => phiOf[c.K]);
        var timber = p.Elements[top.K].Material == "timber";
        return new ImperfectionSet(direction, h, alphaH, rows, phiOf, loads, force)
        {
            Governing = (phiOf[top.K], timber ? 0 : rows.First(r => Math.Abs(r.Phi - phiOf[top.K]) < 1e-15).M),
            Timber = cols.Any(c => p.Elements[c.K].Material == "timber"),
        };
    }

    /// <summary>The four variants' directions and names (+X, −X, +Y, −Y; EN 1993-1-1 5.3.2(8): one horizontal direction at a time).</summary>
    public static readonly (string Name, Vec3 Direction)[] Variants =
        [("+X", new Vec3(1, 0, 0)), ("-X", new Vec3(-1, 0, 0)), ("+Y", new Vec3(0, 1, 0)), ("-Y", new Vec3(0, -1, 0))];
}
