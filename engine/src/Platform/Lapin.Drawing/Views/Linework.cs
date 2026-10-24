using System.Text.Json.Nodes;
using BubbleBim.Geometry;

namespace Lapin.Drawing.Views;

/// <summary>
/// One edit of a view's generated lines (the old Lapin's linework, as Revit's
/// Linework tool): on an element's edge, from A to B in the view's
/// coordinates: hide it, show it (a hidden one drawn as seen), restyle it, or
/// add a piece (a corner joined).
/// </summary>
public sealed record LineEdit(string Id, string Element, Vec2 A, Vec2 B, string Action, LineStyleSpec? Style = null)
{
    public static readonly string[] Actions = ["hide", "show", "style", "add"];

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["id"] = Id, ["element"] = Element, ["a"] = new JsonArray(A.X, A.Y), ["b"] = new JsonArray(B.X, B.Y), ["action"] = Action };
        if (Style is { Empty: false } s) o["style"] = new JsonObject { ["pen"] = s.Pen, ["color"] = s.Color, ["pattern"] = s.Pattern };
        return o;
    }

    public static LineEdit? From(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["element"] is not { } el || (string?)o["action"] is not { } act || !Actions.Contains(act)) return null;
        static Vec2? P(JsonNode? x) => x is JsonArray a && a.Count >= 2 ? new Vec2(Lapin.Model.Json.Number(a[0]) ?? 0, Lapin.Model.Json.Number(a[1]) ?? 0) : null;
        if (P(o["a"]) is not { } pa || P(o["b"]) is not { } pb) return null;
        return new LineEdit((string?)o["id"] ?? "", el, pa, pb, act, LineStyleSpec.From(o["style"]));
    }
}

/// <summary>An element's whole edge in a drawing: the touching pieces along one line, and where it was picked.</summary>
public sealed record Edge(string Element, string Class, Vec2 A, Vec2 B, Vec2 Pick);

/// <summary>
/// Applying a view's line edits to its generated drawing. An edit holds on
/// the element and the piece of its edge, so it survives a regenerated
/// drawing and the pieces hidden-line removal cut the edge into; an edit
/// whose edge is gone is an orphan and draws nothing.
/// </summary>
public static class Linework
{
    const double Tol = 1e-4;

    static double T(Vec2 a, Vec2 b, Vec2 p) { var d = b - a; return Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18); }
    static double Dist(Vec2 p, Vec2 a, Vec2 b) { var d = b - a; return Math.Abs(Vec2.Cross(p - a, d)) / Math.Max(d.Length, 1e-18); }

    /// <summary>Whether a drawn line lies along an edit's line.</summary>
    static bool Along(DLine l, Vec2 a, Vec2 b) => (l.B - l.A).Length > Tol && Dist(l.A, a, b) < Tol && Dist(l.B, a, b) < Tol;

    /// <summary>The part of a line an edit's span covers, as parameters along the line (null: none).</summary>
    static (double T0, double T1)? Overlap(DLine l, Vec2 a, Vec2 b)
    {
        var (ta, tb) = (T(l.A, l.B, a), T(l.A, l.B, b));
        var (t0, t1) = (Math.Max(0, Math.Min(ta, tb)), Math.Min(1, Math.Max(ta, tb)));
        var len = (l.B - l.A).Length;
        return (t1 - t0) * len > Tol ? (t0, t1) : null;
    }

    static DLine Piece(DLine l, double t0, double t1) => l with { A = l.A + (l.B - l.A) * t0, B = l.A + (l.B - l.A) * t1 };

    public static (ViewDrawing Drawing, List<string> Orphans) Apply(ViewDrawing d, IReadOnlyList<LineEdit> edits)
    {
        if (edits.Count == 0) return (d, []);
        var lines = d.Lines.ToList();
        var orphans = new List<string>();
        foreach (var e in edits)
        {
            if (e.Action == "add")
            {
                var cls = lines.FirstOrDefault(l => l.Element == e.Element)?.Class ?? d.Cuts.FirstOrDefault(c => c.Element == e.Element)?.Class;
                if (cls == null) { orphans.Add(e.Id); continue; }
                lines.Add(new DLine(e.A, e.B, "projection", e.Element, cls, e.Style));
                continue;
            }
            var hit = false;
            var next = new List<DLine>(lines.Count);
            foreach (var l in lines)
            {
                if (l.Element != e.Element || !Along(l, e.A, e.B) || Overlap(l, e.A, e.B) is not var (t0, t1))
                {
                    next.Add(l);
                    continue;
                }
                hit = true;
                if (t0 > Tol) next.Add(Piece(l, 0, t0));
                var mid = Piece(l, t0, t1);
                switch (e.Action)
                {
                    case "show": next.Add(mid.Role == "hidden" ? mid with { Role = "projection" } : mid); break;
                    case "style": next.Add(mid with { Style = LineStyles.Merge(e.Style ?? new LineStyleSpec(), mid.Style) }); break;
                }
                if (t1 < 1 - Tol) next.Add(Piece(l, t1, 1));
            }
            lines = next;
            if (!hit) orphans.Add(e.Id);
        }
        return (d with { Lines = lines }, orphans);
    }

    /// <summary>The element's whole edge near a point: its touching pieces along one line (gaps end it).</summary>
    public static Edge? EdgeAt(ViewDrawing d, Vec2 p, double tol)
    {
        DLine? best = null;
        var bestD = tol;
        foreach (var l in d.Lines)
        {
            var dd = l.B - l.A;
            if (dd.Length < Tol) continue;
            var t = Math.Clamp(T(l.A, l.B, p), 0, 1);
            var dist = (p - (l.A + dd * t)).Length;
            // The first one found wins a tie (lines lying on each other).
            if (dist <= tol && (best == null || dist < bestD - 1e-12)) { bestD = dist; best = l; }
        }
        if (best == null) return null;
        var (a, b) = (best.A, best.B);
        var u = (b - a).Normalized();
        // The pieces of the same element along the same line, as intervals along it, merged while they touch.
        var spans = d.Lines.Where(l => l.Element == best.Element && Along(l, a, b))
            .Select(l => { var (s0, s1) = (Vec2.Dot(l.A - a, u), Vec2.Dot(l.B - a, u)); return (Math.Min(s0, s1), Math.Max(s0, s1)); })
            .OrderBy(s => s.Item1).ToList();
        var at = Vec2.Dot(p - a, u);
        var (lo, hi) = (double.NaN, double.NaN);
        foreach (var (s0, s1) in spans)
        {
            if (double.IsNaN(lo) || s0 > hi + Tol)
            {
                if (!double.IsNaN(lo) && at >= lo - tol && at <= hi + tol) break;
                (lo, hi) = (s0, s1);
            }
            else hi = Math.Max(hi, s1);
        }
        return new Edge(best.Element, best.Class, a + u * lo, a + u * hi, p);
    }

    /// <summary>Where two edges' lines cross (null when parallel).</summary>
    public static Vec2? Crossing(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2)
    {
        var (d1, d2) = (a2 - a1, b2 - b1);
        var den = Vec2.Cross(d1, d2);
        if (Math.Abs(den) < 1e-12) return null;
        return a1 + d1 * (Vec2.Cross(b1 - a1, d2) / den);
    }

    /// <summary>
    /// An edge brought to a corner: when the corner lies past it, the piece up
    /// to it is added; when it falls on it, the overshoot is hidden: away from
    /// where it was picked (JOIN, EXTEND), or the picked side ("other": TRIM).
    /// </summary>
    public static List<(string Action, Vec2 A, Vec2 B)> ToCorner(Edge e, Vec2 x, string side)
    {
        var u = (e.B - e.A).Normalized();
        var len = (e.B - e.A).Length;
        var tx = Vec2.Dot(x - e.A, u);
        if (tx > len + Tol) return [("add", e.B, x)];
        if (tx < -Tol) return [("add", x, e.A)];
        var pickBefore = Vec2.Dot(e.Pick - e.A, u) < tx;
        var hideBefore = side == "other" ? pickBefore : !pickBefore;
        if (hideBefore) return tx > Tol ? [("hide", e.A, x)] : [];
        return tx < len - Tol ? [("hide", x, e.B)] : [];
    }

    /// <summary>A new edit, dropping the earlier ones of its kind (visibility: hide and show; style) on its element that it covers.</summary>
    public static List<LineEdit> Add(IReadOnlyList<LineEdit> edits, LineEdit next)
    {
        static string Kind(string a) => a is "hide" or "show" ? "visibility" : a;
        bool Covered(LineEdit o)
        {
            if (o.Element != next.Element || Kind(o.Action) != Kind(next.Action) || o.Action == "add") return false;
            if (Dist(o.A, next.A, next.B) > Tol || Dist(o.B, next.A, next.B) > Tol) return false;
            var (t0, t1) = (T(next.A, next.B, o.A), T(next.A, next.B, o.B));
            return Math.Min(t0, t1) >= -Tol && Math.Max(t0, t1) <= 1 + Tol;
        }
        return [.. edits.Where(o => !Covered(o)), next];
    }
}
