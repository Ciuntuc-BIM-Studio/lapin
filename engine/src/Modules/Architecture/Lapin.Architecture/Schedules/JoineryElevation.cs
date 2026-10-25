using BubbleBim.Geometry;

namespace Lapin.Architecture.Schedules;

/// <summary>A stroke of a 2D symbol: its ends (m), its weight (cut, outline, thin) and whether it is dashed.</summary>
public readonly record struct Stroke(Vec2 A, Vec2 B, string Weight, bool Dashed = false);

/// <summary>
/// A door or window type in elevation, as a schedule's card shows it: the
/// frame (heavy), the bars between its cells, each panel's sash or leaf, and
/// how it opens (European convention, dashed): a hinged panel's triangle
/// points to its hinges, a bottom-hung one's to its bottom, a tilt-turn has
/// both, a sliding one an arrow. x along the width (centred), y up from the
/// bottom; seen from the interior, or mirrored for the exterior.
/// </summary>
public static class JoineryElevation
{
    public static List<Stroke> Of(JoinerySpec s, bool exterior = false)
    {
        var o = new List<Stroke>();
        void Rect(double x0, double y0, double x1, double y1, string w)
        {
            o.Add(new(new(x0, y0), new(x1, y0), w)); o.Add(new(new(x1, y0), new(x1, y1), w));
            o.Add(new(new(x1, y1), new(x0, y1), w)); o.Add(new(new(x0, y1), new(x0, y0), w));
        }
        var half = s.Width / 2;
        Rect(-half, 0, half, s.Height, "cut");
        var inner = Joinery.InnerRect(s);
        Rect(inner.X0, inner.Z0, inner.X1, inner.Z1, "outline");
        var (cols, rows) = Joinery.Grid(s);
        for (var i = 0; i + 1 < cols.Count; i++) Rect(cols[i].B, inner.Z0, cols[i + 1].A, inner.Z1, "outline");
        for (var i = 0; i + 1 < rows.Count; i++) Rect(inner.X0, rows[i].B, inner.X1, rows[i + 1].A, "outline");
        foreach (var p in Joinery.Normalised(s))
        {
            var r = Joinery.PanelRect(s, p);
            var op = p.Operation;
            if (op == "fixed") continue;
            // The sash or leaf inside its cell, then its opening lines.
            var inset = Joinery.IsDoorLeaf(op) ? 0 : Math.Min(s.Sash.Width * 0.5, Math.Min(r.X1 - r.X0, r.Z1 - r.Z0) / 6);
            var (x0, x1, y0, y1) = (r.X0 + inset, r.X1 - inset, r.Z0 + inset, r.Z1 - inset);
            Rect(x0, y0, x1, y1, "outline");
            if (Joinery.IsDoorLeaf(op) && p.Glazed)
            {
                var g = Math.Min(0.12, (x1 - x0) / 5);
                Rect(x0 + g, y0 + g + 0.3, x1 - g, y1 - g, "thin");
            }
            var (xm, ym) = ((x0 + x1) / 2, (y0 + y1) / 2);
            void Towards(Vec2 apex, Vec2 a, Vec2 b) { o.Add(new(a, apex, "thin", true)); o.Add(new(b, apex, "thin", true)); }
            var left = op.EndsWith("-left") && !op.Contains("sliding") && !op.Contains("folding");
            var right = op.EndsWith("-right") && !op.Contains("sliding") && !op.Contains("folding");
            if (left) Towards(new(x0, ym), new(x1, y0), new(x1, y1));
            if (right) Towards(new(x1, ym), new(x0, y0), new(x0, y1));
            if (op.StartsWith("tilt-turn") || op == "bottom-hung") Towards(new(xm, y0), new(x0, y1), new(x1, y1));
            if (op == "top-hung") Towards(new(xm, y1), new(x0, y0), new(x1, y0));
            if (op == "pivot-horizontal") { Towards(new(x0, ym), new(x1, y0), new(x1, y1)); Towards(new(x1, ym), new(x0, y0), new(x0, y1)); }
            if (op == "pivot-vertical") { Towards(new(xm, y0), new(x0, y1), new(x1, y1)); Towards(new(xm, y1), new(x0, y0), new(x1, y0)); }
            if (op.Contains("sliding"))
            {
                // An arrow the way it slides.
                var (from, to) = op.EndsWith("vertical") ? (new Vec2(xm, y0 + (y1 - y0) * 0.25), new Vec2(xm, y1 - (y1 - y0) * 0.25))
                    : op.EndsWith("left") ? (new Vec2(x1 - (x1 - x0) * 0.25, ym), new Vec2(x0 + (x1 - x0) * 0.25, ym))
                    : (new Vec2(x0 + (x1 - x0) * 0.25, ym), new Vec2(x1 - (x1 - x0) * 0.25, ym));
                var d = (to - from).Normalized() * Math.Min(0.1, (to - from).Length / 3);
                o.Add(new(from, to, "thin"));
                o.Add(new(to, to - d + d.Perp * 0.5, "thin"));
                o.Add(new(to, to - d - d.Perp * 0.5, "thin"));
            }
            if (op.Contains("folding"))
            {
                // A zigzag across the leaf.
                for (var i = 0; i < 4; i++)
                    o.Add(new(new(x0 + (x1 - x0) * i / 4, i % 2 == 0 ? y0 : y1), new(x0 + (x1 - x0) * (i + 1) / 4, i % 2 == 0 ? y1 : y0), "thin", true));
            }
        }
        return exterior ? o.Select(x => x with { A = new Vec2(-x.A.X, x.A.Y), B = new Vec2(-x.B.X, x.B.Y) }).ToList() : o;
    }
}
