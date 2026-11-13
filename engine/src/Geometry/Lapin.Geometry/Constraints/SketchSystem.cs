namespace Lapin.Geometry.Constraints;

/// <summary>
/// Collects residual rows and their analytic derivatives. Parameters are numbered over the whole sketch (x of point p
/// is 2p, y is 2p + 1, the radius of entity e is 2·points + e); <see cref="ColOf"/> maps a parameter to its column in
/// the system being built, or −1 for a parameter that is held (fixed, or outside the cluster).
/// </summary>
internal sealed class RowWriter
{
    public int[] ColOf = [];
    public bool Derivatives = true;
    public readonly List<double> Residual = [];
    public readonly List<int> RowStart = [];
    public readonly List<int> Cols = [];
    public readonly List<double> Vals = [];
    public readonly List<int> RowItem = [];
    public int Item;

    public int Rows => Residual.Count;

    public void Clear()
    {
        Residual.Clear(); RowStart.Clear(); Cols.Clear(); Vals.Clear(); RowItem.Clear();
    }

    public void Row(double residual)
    {
        RowStart.Add(Cols.Count);
        Residual.Add(residual);
        RowItem.Add(Item);
    }

    public void D(int param, double value)
    {
        if (!Derivatives) return;
        int c = ColOf[param];
        if (c < 0) return;
        for (int i = RowStart[^1]; i < Cols.Count; i++)
            if (Cols[i] == c) { Vals[i] += value; return; }
        Cols.Add(c);
        Vals.Add(value);
    }

    public int RowEnd(int row) => row + 1 < RowStart.Count ? RowStart[row + 1] : Cols.Count;
}

/// <summary>
/// A 2D vector as a combination of up to three points (u = Σ cₖ·Pₖ), so each residual is written once over vectors
/// and its derivatives reach the points through the coefficients.
/// </summary>
internal readonly struct V(int p0, double c0, int p1 = -1, double c1 = 0, int p2 = -1, double c2 = 0)
{
    public static V Diff(int to, int from) => new(to, 1, from, -1);

    public (double X, double Y) Eval(double[] q)
    {
        double x = c0 * q[2 * p0], y = c0 * q[2 * p0 + 1];
        if (p1 >= 0) { x += c1 * q[2 * p1]; y += c1 * q[2 * p1 + 1]; }
        if (p2 >= 0) { x += c2 * q[2 * p2]; y += c2 * q[2 * p2 + 1]; }
        return (x, y);
    }

    public void Emit(RowWriter w, double gx, double gy)
    {
        w.D(2 * p0, c0 * gx); w.D(2 * p0 + 1, c0 * gy);
        if (p1 >= 0) { w.D(2 * p1, c1 * gx); w.D(2 * p1 + 1, c1 * gy); }
        if (p2 >= 0) { w.D(2 * p2, c2 * gx); w.D(2 * p2 + 1, c2 * gy); }
    }
}

/// <summary>
/// The equations of a sketch: its parameters, which of them are unknown, the side each sided constraint starts on,
/// and the residual rows of every arc (its ends on its circle) and every driving constraint.
/// </summary>
internal sealed class SketchSystem
{
    public readonly Sketch Sketch;
    public readonly int PointCount;
    public readonly int ParamCount;
    public readonly bool[] Unknown;     // per parameter
    public readonly double[] Start;     // parameters of the input geometry
    public readonly List<int> Arcs = [];    // entity indices of arcs: items 0..Arcs.Count-1
    public readonly List<int> Driving = []; // constraint indices: items Arcs.Count..
    private readonly double[] side;     // per constraint: +1/−1, the side a sided constraint started on
    private readonly bool[] internalTangent; // per constraint: two circles tangent inside

    private const double Tiny = 1e-300;

    public SketchSystem(Sketch sketch)
    {
        Sketch = sketch;
        PointCount = sketch.Points.Count;
        ParamCount = 2 * PointCount + sketch.Entities.Count;
        Unknown = new bool[ParamCount];
        Start = new double[ParamCount];
        for (int p = 0; p < PointCount; p++)
        {
            Start[2 * p] = sketch.Points[p].X;
            Start[2 * p + 1] = sketch.Points[p].Y;
            Unknown[2 * p] = Unknown[2 * p + 1] = !sketch.Points[p].Fixed;
        }
        for (int e = 0; e < sketch.Entities.Count; e++)
        {
            var entity = sketch.Entities[e];
            foreach (int p in PointsOf(e))
            {
                if ((uint)p >= (uint)PointCount) throw new ArgumentException($"Entity {e} refers to point {p}, which does not exist.");
                if (entity.Fixed) Unknown[2 * p] = Unknown[2 * p + 1] = false;
            }
            switch (entity)
            {
                case SketchCircle c: Start[R(e)] = c.Radius; Unknown[R(e)] = !c.Fixed; break;
                case SketchArc a: Start[R(e)] = a.Radius; Unknown[R(e)] = !a.Fixed; Arcs.Add(e); break;
            }
        }
        side = new double[sketch.Constraints.Count];
        internalTangent = new bool[sketch.Constraints.Count];
        for (int i = 0; i < sketch.Constraints.Count; i++)
        {
            Validate(i);
            RecordSide(i);
            if (sketch.Constraints[i].Driving) Driving.Add(i);
        }
    }

    public int Items => Arcs.Count + Driving.Count;
    public int R(int entity) => 2 * PointCount + entity;

    /// <summary>The constraint an item stands for, or −1 for an arc's own rows.</summary>
    public int ConstraintOf(int item) => item < Arcs.Count ? -1 : Driving[item - Arcs.Count];

    public IEnumerable<int> PointsOf(int entity) => Sketch.Entities[entity] switch
    {
        SketchLine l => [l.P1, l.P2],
        SketchCircle c => [c.Center],
        SketchArc a => [a.Center, a.Start, a.End],
        _ => [],
    };

    public bool IsCircular(int entity) => Sketch.Entities[entity] is SketchCircle or SketchArc;

    private int CenterOf(int entity) => Sketch.Entities[entity] switch
    {
        SketchCircle c => c.Center,
        SketchArc a => a.Center,
        _ => throw new InvalidOperationException(),
    };

    private SketchLine LineOf(int entity) => (SketchLine)Sketch.Entities[entity];

    // ---- validation ---------------------------------------------------------------------------------------------

    private void Validate(int ci)
    {
        var c = Sketch.Constraints[ci];
        var r = c.Refs;
        foreach (var x in r)
        {
            int count = x.Kind == SketchRefKind.Point ? PointCount : Sketch.Entities.Count;
            if ((uint)x.Index >= (uint)count) throw Bad(ci, $"refers to {x.Kind.ToString().ToLowerInvariant()} {x.Index}, which does not exist");
        }
        bool ok = c.Kind switch
        {
            ConstraintKind.Coincident => Is(r, "PP"),
            ConstraintKind.PointOn => Is(r, "PE"),
            ConstraintKind.Horizontal or ConstraintKind.Vertical => Is(r, "L") || Is(r, "PP"),
            ConstraintKind.Parallel or ConstraintKind.Perpendicular => Is(r, "LL"),
            ConstraintKind.Tangent => (Is(r, "EE") || Is(r, "EEPP")) && (IsCircular(r[0].Index) || IsCircular(r[1].Index)),
            ConstraintKind.Equal => Is(r, "LL") || Is(r, "CC"),
            ConstraintKind.Symmetric => Is(r, "PPL") || Is(r, "PPP"),
            ConstraintKind.Midpoint => Is(r, "PL"),
            ConstraintKind.Fix => Is(r, "P") || Is(r, "E"),
            ConstraintKind.Distance => Is(r, "PP") || Is(r, "PL") || Is(r, "L"),
            ConstraintKind.DistanceX or ConstraintKind.DistanceY => Is(r, "PP") || Is(r, "L") || Is(r, "P"),
            ConstraintKind.Radius or ConstraintKind.Diameter => Is(r, "C"),
            ConstraintKind.Angle => Is(r, "LL") || Is(r, "L"),
            _ => false,
        };
        if (!ok) throw Bad(ci, $"has references that do not fit a {c.Kind} constraint");
    }

    private static ArgumentException Bad(int ci, string what) => new($"Constraint {ci} {what}.");

    /// <summary>Matches references to a pattern: P point, E any entity, L line, C circle or arc.</summary>
    private bool Is(IReadOnlyList<SketchRef> r, string pattern)
    {
        if (r.Count != pattern.Length) return false;
        for (int i = 0; i < r.Count; i++)
        {
            bool fits = pattern[i] switch
            {
                'P' => r[i].Kind == SketchRefKind.Point,
                'E' => r[i].Kind == SketchRefKind.Entity,
                'L' => r[i].Kind == SketchRefKind.Entity && Sketch.Entities[r[i].Index] is SketchLine,
                'C' => r[i].Kind == SketchRefKind.Entity && IsCircular(r[i].Index),
                _ => false,
            };
            if (!fits) return false;
        }
        return true;
    }

    // ---- sides, recorded once from the input geometry so a solve never jumps to the mirror solution --------------

    private void RecordSide(int ci)
    {
        var c = Sketch.Constraints[ci];
        var r = c.Refs;
        side[ci] = 1;
        double[] q = Start;
        if (c.Kind == ConstraintKind.Distance && Is(r, "PL"))
        {
            var l = LineOf(r[1].Index);
            side[ci] = SignOf(Cross(V.Diff(l.P2, l.P1).Eval(q), V.Diff(r[0].Index, l.P1).Eval(q)));
        }
        else if (c.Kind == ConstraintKind.Tangent && r.Count == 2)
        {
            int e1 = r[0].Index, e2 = r[1].Index;
            if (!IsCircular(e1) || !IsCircular(e2))
            {
                int line = IsCircular(e1) ? e2 : e1, circle = IsCircular(e1) ? e1 : e2;
                var l = LineOf(line);
                side[ci] = SignOf(Cross(V.Diff(l.P2, l.P1).Eval(q), V.Diff(CenterOf(circle), l.P1).Eval(q)));
            }
            else
            {
                var (dx, dy) = V.Diff(CenterOf(e2), CenterOf(e1)).Eval(q);
                double d = Math.Sqrt(dx * dx + dy * dy), r1 = q[R(e1)], r2 = q[R(e2)];
                internalTangent[ci] = Math.Abs(d - Math.Abs(r1 - r2)) < Math.Abs(d - (r1 + r2));
                side[ci] = SignOf(r1 - r2);
            }
        }
    }

    private static double SignOf(double v) => v < 0 ? -1 : 1;
    private static double Cross((double X, double Y) a, (double X, double Y) b) => a.X * b.Y - a.Y * b.X;

    // ---- rows ---------------------------------------------------------------------------------------------------

    /// <summary>Writes an item's rows at <paramref name="q"/>.</summary>
    public void Eval(int item, double[] q, RowWriter w)
    {
        w.Item = item;
        if (item < Arcs.Count)
        {
            int e = Arcs[item];
            var a = (SketchArc)Sketch.Entities[e];
            Length(w, q, V.Diff(a.Start, a.Center), -q[R(e)], R(e), -1);
            Length(w, q, V.Diff(a.End, a.Center), -q[R(e)], R(e), -1);
            return;
        }
        int ci = Driving[item - Arcs.Count];
        Constraint(ci, Sketch.Constraints[ci].Value, q, w);
    }

    /// <summary>The value a dimension measures at <paramref name="q"/> (NaN for a constraint with none).</summary>
    public double Measure(int ci, double[] q)
    {
        var c = Sketch.Constraints[ci];
        if (c.Kind is not (ConstraintKind.Distance or ConstraintKind.DistanceX or ConstraintKind.DistanceY
            or ConstraintKind.Radius or ConstraintKind.Diameter or ConstraintKind.Angle)) return double.NaN;
        var w = new RowWriter { Derivatives = false };
        Constraint(ci, 0, q, w);
        double v = w.Residual[0];
        if (c.Kind == ConstraintKind.Angle && v < 0) v += 2 * Math.PI;
        return v;
    }

    private void Constraint(int ci, double value, double[] q, RowWriter w)
    {
        var c = Sketch.Constraints[ci];
        var r = c.Refs;
        switch (c.Kind)
        {
            case ConstraintKind.Coincident:
                Linear(w, q, 0, 2 * r[1].Index, 1, 2 * r[0].Index, -1);
                Linear(w, q, 0, 2 * r[1].Index + 1, 1, 2 * r[0].Index + 1, -1);
                break;

            case ConstraintKind.PointOn:
            {
                int p = r[0].Index, e = r[1].Index;
                if (Sketch.Entities[e] is SketchLine l) OverLength(w, q, V.Diff(l.P2, l.P1), V.Diff(p, l.P1), cross: true, 1, 0);
                else Length(w, q, V.Diff(p, CenterOf(e)), -q[R(e)], R(e), -1);
                break;
            }

            case ConstraintKind.Horizontal or ConstraintKind.Vertical:
            {
                int k = c.Kind == ConstraintKind.Horizontal ? 1 : 0;
                var (a, b) = r.Count == 1 ? (LineOf(r[0].Index).P1, LineOf(r[0].Index).P2) : (r[0].Index, r[1].Index);
                Linear(w, q, 0, 2 * b + k, 1, 2 * a + k, -1);
                break;
            }

            case ConstraintKind.Parallel or ConstraintKind.Perpendicular:
            {
                var l1 = LineOf(r[0].Index);
                var l2 = LineOf(r[1].Index);
                Between(w, q, V.Diff(l1.P2, l1.P1), V.Diff(l2.P2, l2.P1), cross: c.Kind == ConstraintKind.Parallel);
                break;
            }

            case ConstraintKind.Tangent:
                Tangent(ci, q, w);
                break;

            case ConstraintKind.Equal:
            {
                int e1 = r[0].Index, e2 = r[1].Index;
                if (IsCircular(e1)) Linear(w, q, 0, R(e2), 1, R(e1), -1);
                else
                {
                    // |d2| − |d1|: written as two length terms in one row.
                    var l1 = LineOf(e1);
                    var l2 = LineOf(e2);
                    var (ax, ay) = V.Diff(l1.P2, l1.P1).Eval(q);
                    var (bx, by) = V.Diff(l2.P2, l2.P1).Eval(q);
                    double la = Math.Max(Math.Sqrt(ax * ax + ay * ay), Tiny), lb = Math.Max(Math.Sqrt(bx * bx + by * by), Tiny);
                    w.Row(lb - la);
                    V.Diff(l2.P2, l2.P1).Emit(w, bx / lb, by / lb);
                    V.Diff(l1.P2, l1.P1).Emit(w, -ax / la, -ay / la);
                }
                break;
            }

            case ConstraintKind.Symmetric:
            {
                int p = r[0].Index, s = r[1].Index;
                if (r[2].Kind == SketchRefKind.Point)
                {
                    int m = r[2].Index;
                    for (int k = 0; k < 2; k++)
                        Linear3(w, q, 2 * p + k, 0.5, 2 * s + k, 0.5, 2 * m + k, -1);
                }
                else
                {
                    var l = LineOf(r[2].Index);
                    var d = V.Diff(l.P2, l.P1);
                    // The midpoint on the line, and the segment between the two at right angles to it.
                    OverLength(w, q, d, new V(p, 0.5, s, 0.5, l.P1, -1), cross: true, 1, 0);
                    OverLength(w, q, d, V.Diff(s, p), cross: false, 1, 0);
                }
                break;
            }

            case ConstraintKind.Midpoint:
            {
                int p = r[0].Index;
                var l = LineOf(r[1].Index);
                for (int k = 0; k < 2; k++)
                    Linear3(w, q, 2 * p + k, 1, 2 * l.P1 + k, -0.5, 2 * l.P2 + k, -0.5);
                break;
            }

            case ConstraintKind.Fix:
                if (r[0].Kind == SketchRefKind.Point) Hold(w, q, r[0].Index);
                else
                {
                    int e = r[0].Index;
                    var seen = new List<int>();
                    foreach (int p in PointsOf(e))
                        if (!seen.Contains(p)) { seen.Add(p); Hold(w, q, p); }
                    if (IsCircular(e)) Linear(w, q, -Start[R(e)], R(e), 1, -1, 0);
                }
                break;

            case ConstraintKind.Distance:
                if (Is(r, "PP")) Length(w, q, V.Diff(r[1].Index, r[0].Index), -value, -1, 0);
                else if (Is(r, "L")) { var l = LineOf(r[0].Index); Length(w, q, V.Diff(l.P2, l.P1), -value, -1, 0); }
                else { var l = LineOf(r[1].Index); OverLength(w, q, V.Diff(l.P2, l.P1), V.Diff(r[0].Index, l.P1), cross: true, side[ci], -value); }
                break;

            case ConstraintKind.DistanceX or ConstraintKind.DistanceY:
            {
                int k = c.Kind == ConstraintKind.DistanceX ? 0 : 1;
                if (Is(r, "P")) Linear(w, q, -value, 2 * r[0].Index + k, 1, -1, 0);
                else
                {
                    var (a, b) = Is(r, "L") ? (LineOf(r[0].Index).P1, LineOf(r[0].Index).P2) : (r[0].Index, r[1].Index);
                    Linear(w, q, -value, 2 * b + k, 1, 2 * a + k, -1);
                }
                break;
            }

            case ConstraintKind.Radius:
                Linear(w, q, -value, R(r[0].Index), 1, -1, 0);
                break;

            case ConstraintKind.Diameter:
                Linear(w, q, -value, R(r[0].Index), 2, -1, 0);
                break;

            case ConstraintKind.Angle:
                if (r.Count == 1) { var l = LineOf(r[0].Index); AngleOf(w, q, V.Diff(l.P2, l.P1), value); }
                else
                {
                    var l1 = LineOf(r[0].Index);
                    var l2 = LineOf(r[1].Index);
                    AngleBetween(w, q, V.Diff(l1.P2, l1.P1), V.Diff(l2.P2, l2.P1), value);
                }
                break;
        }
    }

    private void Tangent(int ci, double[] q, RowWriter w)
    {
        var r = Sketch.Constraints[ci].Refs;
        int e1 = r[0].Index, e2 = r[1].Index;
        bool c1 = IsCircular(e1), c2 = IsCircular(e2);
        if (r.Count == 4)
        {
            int p1 = r[2].Index, p2 = r[3].Index;
            if (p1 != p2)
            {
                Linear(w, q, 0, 2 * p2, 1, 2 * p1, -1);
                Linear(w, q, 0, 2 * p2 + 1, 1, 2 * p1 + 1, -1);
            }
            if (c1 && c2)
                // The two radii to the joint in one line: both centres on the normal there.
                Between(w, q, V.Diff(p1, CenterOf(e1)), V.Diff(p1, CenterOf(e2)), cross: true);
            else
            {
                var l = LineOf(c1 ? e2 : e1);
                // The radius to the joint at right angles to the line.
                OverLength(w, q, V.Diff(l.P2, l.P1), V.Diff(p1, CenterOf(c1 ? e1 : e2)), cross: false, 1, 0);
            }
            return;
        }
        if (c1 && c2)
        {
            int k1 = CenterOf(e1), k2 = CenterOf(e2);
            if (!internalTangent[ci]) Length2(w, q, V.Diff(k2, k1), R(e1), -1, R(e2), -1);
            else Length2(w, q, V.Diff(k2, k1), R(e1), -side[ci], R(e2), side[ci]);
        }
        else
        {
            int line = c1 ? e2 : e1, circle = c1 ? e1 : e2;
            var l = LineOf(line);
            // The centre's signed distance from the line, on its starting side, equals the radius.
            OverLength(w, q, V.Diff(l.P2, l.P1), V.Diff(CenterOf(circle), l.P1), cross: true, side[ci], -q[R(circle)]);
            w.D(R(circle), -1);
        }
    }

    // ---- residual forms -----------------------------------------------------------------------------------------

    /// <summary>c₁·q[a] + c₂·q[b] + constant (b may be −1).</summary>
    private static void Linear(RowWriter w, double[] q, double constant, int a, double ca, int b, double cb)
    {
        w.Row(constant + ca * q[a] + (b >= 0 ? cb * q[b] : 0));
        w.D(a, ca);
        if (b >= 0) w.D(b, cb);
    }

    private static void Linear3(RowWriter w, double[] q, int a, double ca, int b, double cb, int c, double cc)
    {
        w.Row(ca * q[a] + cb * q[b] + cc * q[c]);
        w.D(a, ca); w.D(b, cb); w.D(c, cc);
    }

    private void Hold(RowWriter w, double[] q, int p)
    {
        Linear(w, q, -Start[2 * p], 2 * p, 1, -1, 0);
        Linear(w, q, -Start[2 * p + 1], 2 * p + 1, 1, -1, 0);
    }

    /// <summary>|u| + constant, plus coefficient·q[extra] already in the constant's derivative (extra may be −1).</summary>
    private static void Length(RowWriter w, double[] q, V u, double constant, int extra, double extraCoeff)
    {
        var (x, y) = u.Eval(q);
        double len = Math.Sqrt(x * x + y * y);
        w.Row(len + constant);
        if (len < Tiny) u.Emit(w, 1, 0); // coincident: any direction, the same every time
        else u.Emit(w, x / len, y / len);
        if (extra >= 0) w.D(extra, extraCoeff);
    }

    /// <summary>|u| + a·q[ra] + b·q[rb].</summary>
    private static void Length2(RowWriter w, double[] q, V u, int ra, double a, int rb, double b)
    {
        var (x, y) = u.Eval(q);
        double len = Math.Sqrt(x * x + y * y);
        w.Row(len + a * q[ra] + b * q[rb]);
        if (len < Tiny) u.Emit(w, 1, 0);
        else u.Emit(w, x / len, y / len);
        w.D(ra, a);
        w.D(rb, b);
    }

    /// <summary>s·(d × u)/|d| + constant (the signed distance of u's tip from the line along d), or s·(d · u)/|d|.</summary>
    private static void OverLength(RowWriter w, double[] q, V d, V u, bool cross, double s, double constant)
    {
        var (dx, dy) = d.Eval(q);
        var (ux, uy) = u.Eval(q);
        double len = Math.Max(Math.Sqrt(dx * dx + dy * dy), Tiny);
        double num = cross ? dx * uy - dy * ux : dx * ux + dy * uy;
        double f = num / len;
        w.Row(s * f + constant);
        // ∂num/∂d and ∂num/∂u.
        double ndx = cross ? uy : ux, ndy = cross ? -ux : uy;
        double nux = cross ? -dy : dx, nuy = cross ? dx : dy;
        d.Emit(w, s * (ndx / len - f * dx / (len * len)), s * (ndy / len - f * dy / (len * len)));
        u.Emit(w, s * nux / len, s * nuy / len);
    }

    /// <summary>(u × v)/(|u||v|) — the sine of the angle between — or (u · v)/(|u||v|), its cosine.</summary>
    private static void Between(RowWriter w, double[] q, V u, V v, bool cross)
    {
        var (ux, uy) = u.Eval(q);
        var (vx, vy) = v.Eval(q);
        double lu2 = Math.Max(ux * ux + uy * uy, Tiny), lv2 = Math.Max(vx * vx + vy * vy, Tiny);
        double n = Math.Sqrt(lu2 * lv2);
        double num = cross ? ux * vy - uy * vx : ux * vx + uy * vy;
        double h = num / n;
        w.Row(h);
        double nux = cross ? vy : vx, nuy = cross ? -vx : vy;
        double nvx = cross ? -uy : ux, nvy = cross ? ux : uy;
        u.Emit(w, nux / n - h * ux / lu2, nuy / n - h * uy / lu2);
        v.Emit(w, nvx / n - h * vx / lv2, nvy / n - h * vy / lv2);
    }

    /// <summary>The signed angle from u to v, counter-clockwise, minus the value, wrapped to (−π, π].</summary>
    private static void AngleBetween(RowWriter w, double[] q, V u, V v, double value)
    {
        var (ux, uy) = u.Eval(q);
        var (vx, vy) = v.Eval(q);
        double c = ux * vy - uy * vx, k = ux * vx + uy * vy;
        double den = Math.Max(c * c + k * k, Tiny);
        w.Row(Wrap(Math.Atan2(c, k) - value));
        // ∂θ = (k·∂c − c·∂k)/(c² + k²).
        u.Emit(w, (k * vy - c * vx) / den, (k * -vx - c * vy) / den);
        v.Emit(w, (k * -uy - c * ux) / den, (k * ux - c * uy) / den);
    }

    /// <summary>The angle of u from the x axis minus the value, wrapped to (−π, π].</summary>
    private static void AngleOf(RowWriter w, double[] q, V u, double value)
    {
        var (x, y) = u.Eval(q);
        double den = Math.Max(x * x + y * y, Tiny);
        w.Row(Wrap(Math.Atan2(y, x) - value));
        u.Emit(w, -y / den, x / den);
    }

    public static double Wrap(double a)
    {
        a = Math.IEEERemainder(a, 2 * Math.PI);
        return a <= -Math.PI ? a + 2 * Math.PI : a;
    }
}
