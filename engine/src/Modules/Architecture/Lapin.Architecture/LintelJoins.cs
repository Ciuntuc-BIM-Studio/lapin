using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// One piece of a wall's lintel, placed in the storey: along the wall from
/// <see cref="X0"/> to <see cref="X1"/> (from <see cref="Origin"/>, the wall's
/// joined start), across it from <see cref="Y0"/> to <see cref="Y1"/>, and up
/// from <see cref="Z0"/> to <see cref="Z1"/> (absolute). <see cref="AtStart"/>
/// and <see cref="AtEnd"/>: it reaches a joined end of its wall.
/// </summary>
public sealed record LintelPiece(Lintel Lintel, int Piece, Vec2 Origin, Vec2 Dir, double X0, double X1, double Y0, double Y1, double Z0, double Z1, bool AtStart, bool AtEnd)
{
    Vec2 P(double x, double y) => Origin + Dir * x + Dir.Perp * y;
    public Vec2[] Plan => [P(X0, Y0), P(X1, Y0), P(X1, Y1), P(X0, Y1)];
    public Solid? Box() => Prism.ExtrudeFootprint(Plan, Z0, Z1 - Z0, "lintel");
}

/// <summary>
/// Lintels of two walls that meet at a corner (docs/pereti-compusi-si-executie.md;
/// the old Lapin's "Lapin lintel join"): when both reach the corner at the
/// same height, the longer runs through, out to the far face of the other
/// wall's core, and cuts the shorter and that core; on a tie, the wall whose
/// id sorts first runs through. Worked out from both walls as they are,
/// whenever either is built.
/// </summary>
public static class LintelJoins
{
    const double Eps = 1e-4;

    /// <summary>A lintel piece and what it is cut by.</summary>
    public sealed record Resolved(LintelPiece Piece, List<Solid> CutBy);

    /// <summary>
    /// The wall's lintel pieces after its corners: each extended or cut, and
    /// the other walls' lintels that run into this wall's core (<paramref name="intoCore"/>),
    /// which the core gives way to.
    /// </summary>
    public static List<Resolved> Resolve(Entity wall, IReadOnlyList<LintelPiece> own, IModelView m, IServices? services, out List<(double Z0, double Z1, Solid Box)> intoCore)
    {
        intoCore = [];
        var result = own.Select(p => new Resolved(p, [])).ToList();
        if (own.Count == 0 || services?.Get<IPathElements>() == null) return result;
        foreach (var rel in Joins.Of(m, wall.Id))
        {
            if (m.Get(Joins.Other(rel, wall.Id)) is not { Class: IfcClass.Wall } other) continue;
            var theirs = Walls.LintelPieces(other, m, services);
            if (theirs.Count == 0) continue;
            var (otherFaces, mineFaces) = (CoreFaces(m, other), CoreFaces(m, wall));
            for (var i = 0; i < result.Count; i++)
            {
                var p = result[i].Piece;
                var pe = Extend(p, otherFaces);
                foreach (var q in theirs)
                {
                    var qe = Extend(q, mineFaces);
                    if (!Meet(pe, qe)) continue;
                    var pRuns = p.Lintel.Length > q.Lintel.Length + 1e-6 || (Math.Abs(p.Lintel.Length - q.Lintel.Length) <= 1e-6 && string.CompareOrdinal(wall.Id, other.Id) < 0);
                    if (pRuns) result[i] = result[i] with { Piece = p = p with { X0 = Math.Min(p.X0, pe.X0), X1 = Math.Max(p.X1, pe.X1) } };
                    else if (qe.Box() is { } cutter)
                    {
                        result[i].CutBy.Add(cutter);
                        intoCore.Add((qe.Z0, qe.Z1, cutter));
                    }
                }
            }
        }
        return result;
    }

    /// <summary>The two faces of a wall's core in plan: a point on each and the wall's direction.</summary>
    static (Vec2 A, Vec2 B, Vec2 Dir)? CoreFaces(IModelView m, Entity wall)
    {
        var (s, e) = (Walls.Start(wall), Walls.End(wall));
        if ((e - s).Length < 1e-9) return null;
        var core = Walls.Section(m, wall).FirstOrDefault(b => b.Core);
        var d = (e - s).Normalized();
        return (s + d.Perp * core.From, s + d.Perp * core.To, d);
    }

    /// <summary>The piece carried on past its joined end out to the far face of the other wall's core (it does not shrink).</summary>
    public static LintelPiece Extend(LintelPiece p, (Vec2 A, Vec2 B, Vec2 Dir)? faces)
    {
        if (faces is not { } f || !(p.AtStart || p.AtEnd)) return p;
        var den = Vec2.Cross(p.Dir, f.Dir);
        if (Math.Abs(den) < 1e-6) return p;
        double T(Vec2 o, Vec2 s) => Vec2.Cross(s - o, f.Dir) / den;
        var ts = new[] { p.Y0, p.Y1 }.SelectMany(y => new[] { f.A, f.B }.Select(s => T(p.Origin + p.Dir.Perp * y, s))).ToList();
        var mid = ts.Average();
        // The end the other wall is at; it must be the joined one the piece reaches.
        if (Math.Abs(mid - p.X1) <= Math.Abs(mid - p.X0)) return p.AtEnd ? p with { X1 = Math.Max(p.X1, ts.Max()) } : p;
        return p.AtStart ? p with { X0 = Math.Min(p.X0, ts.Min()) } : p;
    }

    /// <summary>Whether two pieces overlap, in plan (separating axes) and in height.</summary>
    public static bool Meet(LintelPiece a, LintelPiece b)
    {
        if (Math.Min(a.Z1, b.Z1) - Math.Max(a.Z0, b.Z0) <= Eps) return false;
        var (pa, pb) = (a.Plan, b.Plan);
        foreach (var poly in new[] { pa, pb })
            for (var i = 0; i < poly.Length; i++)
            {
                var edge = poly[(i + 1) % poly.Length] - poly[i];
                if (edge.Length < 1e-12) continue;
                var n = edge.Normalized().Perp;
                var (a0, a1) = (pa.Min(v => Vec2.Dot(v, n)), pa.Max(v => Vec2.Dot(v, n)));
                var (b0, b1) = (pb.Min(v => Vec2.Dot(v, n)), pb.Max(v => Vec2.Dot(v, n)));
                if (Math.Min(a1, b1) - Math.Max(a0, b0) <= Eps) return false;
            }
        return true;
    }
}
