using Lapin.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>
/// A drawn cell made a pattern (stage 7 of docs/plan-material-graphics.md, Î12). Straight pieces and dots that <c>.pat</c> line
/// families can say exactly become a pattern of line families: a piece repeated on the cell's grid is one family when it runs
/// along a vector of the grid (p·width, q·height), p and q whole numbers with no common factor and at most
/// <see cref="MaxStep"/>: its lines through the grid's points along that vector, one period |(p·w, q·h)| apart along a line, the
/// next line the grid vector (a·w, b·h) with p·b − q·a = 1 over (so the lines lie w·h / period apart), its dashes the piece and the
/// rest of the period. A piece and its copy one cell over (a cell's top edge and the next one's bottom) are one family. Anything
/// else (arcs, circles, a piece at an angle no small grid vector takes) makes a tile pattern (<see cref="HatchTile"/>).
/// </summary>
public static class TileFit
{
    /// <summary>The largest grid step a family may take along a line (a diagonal across up to this many cells).</summary>
    public const int MaxStep = 8;

    const double Eps = 1e-6;

    /// <summary>The pattern a cell's pieces make: line families when every piece fits one, else a tile.</summary>
    public static HatchPattern Make(string name, string description, HatchUnits units, double width, double height, IReadOnlyList<TileLine> lines, IReadOnlyList<TileArc> arcs)
    {
        if (!(width > 0 && height > 0)) throw new ArgumentException("A cell has a width and a height.");
        var families = arcs.Count == 0 ? Families(width, height, lines) : null;
        return families != null
            ? new HatchPattern(name, description, families, units)
            : new HatchPattern(name, description, [], units) { Tile = new HatchTile(width, height, lines, arcs) };
    }

    /// <summary>The line families the straight pieces (and dots) of a cell make, or null when one of them fits none.</summary>
    public static List<HatchLineFamily>? Families(double w, double h, IReadOnlyList<TileLine> lines)
    {
        var o = new List<HatchLineFamily>();
        var seen = new List<TileLine>();
        foreach (var l in lines)
        {
            // A piece one cell over from another (the next cell's bottom edge is this one's top): the same lines.
            if (seen.Any(x => Same(x, l, w, h))) continue;
            seen.Add(l);
            if (l.Dot)
            {
                // A dot on every cell: a horizontal family of dots, one cell apart along and across.
                o.Add(new HatchLineFamily(0, Round(l.A), 0, Round(h), [0, -Round(w)]));
                continue;
            }
            if (Family(w, h, l) is not { } f) return null;
            o.Add(f);
        }
        return o;
    }

    static double Round(double v) => Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v) : Math.Round(v, 9);
    static Vec2 Round(Vec2 v) => new(Round(v.X), Round(v.Y));

    /// <summary>Whether two pieces are one another moved by whole cells.</summary>
    static bool Same(TileLine x, TileLine l, double w, double h)
    {
        if (((x.B - x.A) - (l.B - l.A)).Length > Eps) return false;
        var d = l.A - x.A;
        var (i, j) = (d.X / w, d.Y / h);
        return Math.Abs(i - Math.Round(i)) < Eps && Math.Abs(j - Math.Round(j)) < Eps;
    }

    /// <summary>The family a straight piece makes on the grid, or null when it runs along no small grid vector.</summary>
    static HatchLineFamily? Family(double w, double h, TileLine l)
    {
        var d = l.B - l.A;
        var len = d.Length;
        for (var n = 1; n <= MaxStep; n++)
            for (var p = -n; p <= n; p++)
                foreach (var q in new[] { n - Math.Abs(p), -(n - Math.Abs(p)) }.Distinct())
                {
                    if (p == 0 && q == 0 || Gcd(Math.Abs(p), Math.Abs(q)) != 1) continue;
                    var v = new Vec2(p * w, q * h);
                    var period = v.Length;
                    // Along the piece, its own way (the dashes start at its first end).
                    if (Math.Abs(d.X * v.Y - d.Y * v.X) > Eps * len * period || d.X * v.X + d.Y * v.Y <= 0) continue;
                    if (len > period + Eps) return null;
                    var (a, b) = Next(p, q);
                    var next = new Vec2(a * w, b * h);
                    var u = v * (1 / period);
                    var across = new Vec2(-u.Y, u.X);
                    var dx = next.X * u.X + next.Y * u.Y;
                    var dy = next.X * across.X + next.Y * across.Y;
                    var angle = Math.Atan2(v.Y, v.X) * 180 / Math.PI;
                    IReadOnlyList<double> dashes = len >= period - Eps ? [] : [Round(len), -Round(period - len)];
                    return new HatchLineFamily(Round(angle), Round(l.A), Round(dx), Round(dy), dashes);
                }
        return null;
    }

    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    /// <summary>Whole a, b with p·b − q·a = 1 (p, q with no common factor).</summary>
    static (int A, int B) Next(int p, int q)
    {
        // Extended Euclid: p·x + q·y = 1, so b = x, a = −y.
        var (g, x, y) = Ext(p, q);
        if (g < 0) (x, y) = (-x, -y);
        return (-y, x);
    }

    static (int G, int X, int Y) Ext(int a, int b)
    {
        if (b == 0) return (a, 1, 0);
        var (g, x, y) = Ext(b, a % b);
        return (g, y, x - a / b * y);
    }
}
