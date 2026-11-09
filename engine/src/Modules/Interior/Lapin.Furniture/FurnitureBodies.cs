using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>
/// A piece's body and plan symbol in its own frame (docs/plan-furniture.md, Desenul): x across it as seen from its front
/// (left to right), y from its front (−depth/2) to its back (+depth/2), z up from where it stands; the origin the middle
/// of its footprint. Every solid is a block with rounded corners or a cylinder standing upright, low-poly and closed;
/// mirrored, the blocks' spans are mirrored (never the solids), so faces keep their orientation.
/// </summary>
public sealed class Builder(Vec2 at, double z, double rotationDeg, bool mirror)
{
    readonly double c = Math.Cos(rotationDeg * Math.PI / 180), s = Math.Sin(rotationDeg * Math.PI / 180);

    /// <summary>The solids by material role, in the order they were made.</summary>
    public readonly Dictionary<string, List<Solid>> Solids = [];
    public readonly List<PlanItem> Plan = [];
    public readonly List<Vec2> Snaps = [];
    /// <summary>The pieces in the local frame, for the types dialog's front view: x, z extents.</summary>
    public readonly List<(string Role, double X0, double X1, double Z0, double Z1, double Y0)> Front = [];

    double Mx(double x) => mirror ? -x : x;
    Vec3 W(double x, double y, double zz) => new(at.X + c * x - s * y, at.Y + s * x + c * y, z + zz);
    public Vec2 Q(double x, double y) { x = Mx(x); return new(at.X + c * x - s * y, at.Y + s * x + c * y); }

    void Add(string role, Solid? solid) { if (solid == null) return; if (!Solids.TryGetValue(role, out var l)) Solids[role] = l = []; l.Add(solid); }

    /// <summary>A rounded rectangle's corners, counter-clockwise, in the local frame (already mirrored).</summary>
    static List<(double X, double Y)> Rect(double x0, double x1, double y0, double y1, double r, int steps = 3)
    {
        r = Math.Max(0, Math.Min(r, Math.Min(x1 - x0, y1 - y0) / 2 - 1e-4));
        if (r < 1e-4) return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
        var o = new List<(double, double)>();
        (double Cx, double Cy, double A)[] corners = [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)];
        foreach (var (cx, cy, a0) in corners)
            for (var i = 0; i <= steps; i++)
            {
                var a = (a0 + 90.0 * i / steps) * Math.PI / 180;
                o.Add((cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
        return o;
    }

    /// <summary>A block from x0 to x1, y0 to y1, z0 to z1, its upright edges rounded by r.</summary>
    public void Block(string role, double x0, double x1, double y0, double y1, double z0, double z1, double r = 0)
    {
        if (x1 - x0 < 1e-4 || y1 - y0 < 1e-4 || z1 - z0 < 1e-4) return;
        Front.Add((role, x0, x1, z0, z1, y0));
        if (mirror) (x0, x1) = (-x1, -x0);
        var profile = Rect(x0, x1, y0, y1, r).Select(p => W(p.X, p.Y, z0)).ToList();
        Add(role, Prism.Extrude(profile, null, new Vec3(0, 0, z1 - z0), "furniture"));
    }

    /// <summary>An upright cylinder about (cx, cy).</summary>
    public void Cylinder(string role, double cx, double cy, double r, double z0, double z1, int sides = 12)
    {
        if (r < 1e-4 || z1 - z0 < 1e-4) return;
        Front.Add((role, cx - r, cx + r, z0, z1, cy - r));
        cx = Mx(cx);
        var profile = Enumerable.Range(0, sides).Select(i => { var a = 2 * Math.PI * i / sides; return W(cx + r * Math.Cos(a), cy + r * Math.Sin(a), z0); }).ToList();
        Add(role, Prism.Extrude(profile, null, new Vec3(0, 0, z1 - z0), "furniture"));
    }

    // ---- Plan ----

    public void Line(string kind, params (double X, double Y)[] pts) => Plan.Add(new PlanItem(kind, false, [.. pts.Select(p => Q(p.X, p.Y))]));
    public void Loop(string kind, IEnumerable<(double X, double Y)> pts) => Plan.Add(new PlanItem(kind, true, [.. pts.Select(p => Q(p.X, p.Y))]));
    public void PlanRect(string kind, double x0, double x1, double y0, double y1, double r = 0) => Loop(kind, Rect(x0, x1, y0, y1, r));
    public void Circle(string kind, double cx, double cy, double r, int sides = 24) =>
        Loop(kind, Enumerable.Range(0, sides).Select(i => { var a = 2 * Math.PI * i / sides; return (cx + r * Math.Cos(a), cy + r * Math.Sin(a)); }));
    public void Arc(string kind, double cx, double cy, double r, double a0, double a1, int steps = 12) =>
        Line(kind, [.. Enumerable.Range(0, steps + 1).Select(i => { var a = (a0 + (a1 - a0) * i / steps) * Math.PI / 180; return (cx + r * Math.Cos(a), cy + r * Math.Sin(a)); })]);
}

/// <summary>The bodies of the kinds, each from its spec (<see cref="Spec"/>).</summary>
public static class FurnitureBodies
{
    const string Sym = "symbol";

    /// <summary>Whether the kind is built mirrored for its hand (a corner sofa with its corner on the right is the left one mirrored).</summary>
    public static bool Handed(Spec s) => s.Kind.Key == "cornerSofa" && s.Str("corner") == "right" || KitchenLayouts.Handed(s);

    /// <summary>The piece built (its builder mirrored for <see cref="Handed"/> already): <paramref name="length"/> is a kitchen's own (its run's length).</summary>
    public static void Build(Builder b, Spec s, double? length = null)
    {
        switch (s.Kind.Key)
        {
            case "sofa": Sofa(b, s, s.Int("seats", 3), s.Str("arms"), s.Str("chaise")); break;
            case "armchair": Sofa(b, s, 1, "both", "none"); break;
            case "cornerSofa": CornerSofa(b, s); break;
            case "chair": Chair(b, s); break;
            case "bed": Bed(b, s); break;
            case "bedside": Cabinet(b, s, s.Bool("legs") ? 0.14 : 0.04, s.Int("drawers", 2), 1); break;
            case "wardrobe": Wardrobe(b, s); break;
            case "chest": Cabinet(b, s, s.Bool("legs") ? 0.14 : 0.06, s.Int("rows", 4), s.Int("columns", 1)); break;
            case "bookcase": Bookcase(b, s); break;
            case "sideboard": Sideboard(b, s); break;
            case "table": Table(b, s, false); break;
            case "coffeeTable": Table(b, s, true); break;
            case "desk": Desk(b, s); break;
            case "kitchen": Kitchen(b, s, length ?? s.W); break;
        }
    }

    /// <summary>The footprint's corners, which the kitchen's own length widens.</summary>
    public static (double W, double D) Footprint(Spec s, double? length) => s.Kind.Key == "kitchen" ? (length ?? s.W, KitchenLayouts.Depth(s)) : (s.W, s.D);

    static string Swap(string side) => side switch { "left" => "right", "right" => "left", _ => side };

    // ---- Seating ----

    /// <summary>A sofa or an armchair: legs or a plinth, its base, arms, back, a cushion per seat, a chaise longue on one side.</summary>
    static void Sofa(Builder b, Spec s, int seats, string arms, string chaise)
    {
        double w = s.W, d = s.D, h = s.H, aw = s.Num("armWidth", 0.18), sh = s.Num("seatHeight", 0.43);
        var legs = s.Bool("legs");
        double lz = legs ? 0.08 : 0.015, bd = Math.Min(0.24, d * 0.25), ah = Math.Min(h - 0.05, sh + 0.2);
        bool al = arms is "both" or "left", ar = arms is "both" or "right";
        double xl = -w / 2 + (al ? aw : 0), xr = w / 2 - (ar ? aw : 0);
        var cl = s.Num("chaiseLength", 0.75);
        // The chaise longue lies under the end seat, coming out in front.
        double seatW = (xr - xl) / Math.Max(1, seats);
        (double X0, double X1)? ch = chaise == "left" ? (-w / 2, xl + seatW) : chaise == "right" ? (xr - seatW, w / 2) : null;
        const string U = "upholstery";
        b.Block(U, -w / 2, w / 2, -d / 2, d / 2, lz, sh - 0.12, 0.03);
        if (ch is { } c0) b.Block(U, c0.X0, c0.X1, -d / 2 - cl, -d / 2 + 0.03, lz, sh - 0.12, 0.03);
        if (al) b.Block(U, -w / 2, -w / 2 + aw, -d / 2, d / 2, lz, ah, 0.05);
        if (ar) b.Block(U, w / 2 - aw, w / 2, -d / 2, d / 2, lz, ah, 0.05);
        b.Block(U, xl, xr, d / 2 - bd, d / 2, lz, h, 0.05);
        for (var i = 0; i < seats; i++)
        {
            double x0 = xl + i * seatW + 0.004, x1 = xl + (i + 1) * seatW - 0.004;
            var front = ch is { } c1 && x0 >= c1.X0 - 1e-6 && x1 <= c1.X1 + 1e-6 ? -d / 2 - cl + 0.02 : -d / 2 + 0.02;
            b.Block(U, x0, x1, front, d / 2 - bd, sh - 0.12, sh, 0.06);
            b.Block(U, x0, x1, d / 2 - bd - 0.15, d / 2 - bd, sh, Math.Min(h - 0.03, sh + 0.4), 0.06);
            if (i > 0) b.Line(Sym, (x0 - 0.004, ch is { } c2 && x0 - 0.004 > c2.X0 && x0 - 0.004 < c2.X1 ? -d / 2 - cl : -d / 2), (x0 - 0.004, d / 2 - bd));
        }
        if (legs)
        {
            var feet = new List<(double, double)> { (-w / 2 + 0.06, -d / 2 + 0.06), (w / 2 - 0.06, -d / 2 + 0.06), (-w / 2 + 0.06, d / 2 - 0.06), (w / 2 - 0.06, d / 2 - 0.06) };
            if (ch is { } c3) { feet.Add((c3.X0 + 0.06, -d / 2 - cl + 0.06)); feet.Add((c3.X1 - 0.06, -d / 2 - cl + 0.06)); }
            foreach (var (fx, fy) in feet) b.Cylinder("metal", fx, fy, 0.018, 0, lz, 8);
        }
        // In plan: the outline (with the chaise), the back, the arms, the seats.
        if (ch is { } c4)
        {
            var pts = c4.X0 <= -w / 2 + 1e-6
                ? new (double, double)[] { (-w / 2, -d / 2 - cl), (c4.X1, -d / 2 - cl), (c4.X1, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2) }
                : [(-w / 2, -d / 2), (c4.X0, -d / 2), (c4.X0, -d / 2 - cl), (w / 2, -d / 2 - cl), (w / 2, d / 2), (-w / 2, d / 2)];
            b.Loop(Sym, pts);
            b.Line(Sym, (c4.X0 <= -w / 2 + 1e-6 ? xl : c4.X0, -d / 2), (c4.X1 >= w / 2 - 1e-6 ? xr : c4.X1, -d / 2));
        }
        else b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2, 0.03);
        b.Line(Sym, (xl, d / 2 - bd), (xr, d / 2 - bd));
        if (al) b.Line(Sym, (xl, ch is { } c5 && c5.X0 <= -w / 2 + 1e-6 ? -d / 2 - cl : -d / 2), (xl, d / 2));
        if (ar) b.Line(Sym, (xr, ch is { } c6 && c6.X1 >= w / 2 - 1e-6 ? -d / 2 - cl : -d / 2), (xr, d / 2));
        if (ch != null)
        {
            if (al && chaise == "left") b.Line(Sym, (-w / 2, -d / 2 - cl), (xl, -d / 2 - cl));
            if (ar && chaise == "right") b.Line(Sym, (xr, -d / 2 - cl), (w / 2, -d / 2 - cl));
        }
    }

    /// <summary>A corner sofa: its long side along x, the corner side coming forward on the left (on the right: built mirrored).</summary>
    static void CornerSofa(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, aw = s.Num("armWidth", 0.15), sh = s.Num("seatHeight", 0.43), cl = Math.Max(d + 0.4, s.Num("cornerLength", 1.9));
        var arms = Handed(s) ? Swap(s.Str("arms")) : s.Str("arms");
        bool aEnd = arms is "both" or "right", aFront = arms is "both" or "left";
        var legs = s.Bool("legs");
        double lz = legs ? 0.08 : 0.015, bd = Math.Min(0.24, d * 0.25), ah = Math.Min(h - 0.05, sh + 0.2);
        const string U = "upholstery";
        double front = d / 2 - cl; // the corner side's front end
        double xr = w / 2 - (aEnd ? aw : 0), yf = front + (aFront ? aw : 0);
        b.Block(U, -w / 2, w / 2, -d / 2, d / 2, lz, sh - 0.12, 0.03);
        b.Block(U, -w / 2, -w / 2 + d, front, -d / 2 + 0.03, lz, sh - 0.12, 0.03);
        b.Block(U, -w / 2 + bd, xr, d / 2 - bd, d / 2, lz, h, 0.05);
        b.Block(U, -w / 2, -w / 2 + bd, yf, d / 2, lz, h, 0.05);
        if (aEnd) b.Block(U, w / 2 - aw, w / 2, -d / 2, d / 2, lz, ah, 0.05);
        if (aFront) b.Block(U, -w / 2, -w / 2 + d, front, front + aw, lz, ah, 0.05);
        // Cushions: the corner seat, the long side's seats, the corner side's.
        var seats = Math.Max(1, s.Int("seats", 2));
        double x0 = -w / 2 + bd, cornerEnd = -w / 2 + d;
        var seatW = (xr - cornerEnd) / seats;
        b.Block(U, x0 + 0.004, cornerEnd - 0.004, -d / 2 + 0.02, d / 2 - bd, sh - 0.12, sh, 0.06);
        for (var i = 0; i < seats; i++)
        {
            double a = cornerEnd + i * seatW + 0.004, c = cornerEnd + (i + 1) * seatW - 0.004;
            b.Block(U, a, c, -d / 2 + 0.02, d / 2 - bd, sh - 0.12, sh, 0.06);
            b.Block(U, a, c, d / 2 - bd - 0.15, d / 2 - bd, sh, Math.Min(h - 0.03, sh + 0.4), 0.06);
            b.Line(Sym, (a - 0.004, -d / 2), (a - 0.004, d / 2 - bd));
        }
        var n = Math.Max(1, (int)Math.Round((-d / 2 - yf) / 0.7));
        var step = (-d / 2 - yf) / n;
        for (var i = 0; i < n; i++)
        {
            double y1 = -d / 2 - i * step - 0.004, y0 = -d / 2 - (i + 1) * step + 0.004;
            b.Block(U, x0 + 0.004, cornerEnd - 0.02, y0, y1, sh - 0.12, sh, 0.06);
            b.Block(U, x0, x0 + 0.15, y0, y1, sh, Math.Min(h - 0.03, sh + 0.4), 0.06);
            b.Line(Sym, (x0, y1 + 0.004), (cornerEnd, y1 + 0.004));
        }
        if (legs)
            foreach (var (fx, fy) in new[] { (-w / 2 + 0.06, d / 2 - 0.06), (w / 2 - 0.06, d / 2 - 0.06), (w / 2 - 0.06, -d / 2 + 0.06), (-w / 2 + d - 0.06, -d / 2 + 0.06), (-w / 2 + 0.06, front + 0.06), (-w / 2 + d - 0.06, front + 0.06) })
                b.Cylinder("metal", fx, fy, 0.018, 0, lz, 8);
        b.Loop(Sym, [(-w / 2, front), (cornerEnd, front), (cornerEnd, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)]);
        b.Line(Sym, (x0, yf), (x0, d / 2 - bd), (xr, d / 2 - bd));
        if (aEnd) b.Line(Sym, (xr, -d / 2), (xr, d / 2));
        if (aFront) b.Line(Sym, (-w / 2, yf), (cornerEnd, yf));
    }

    static void Chair(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, sh = s.Num("seatHeight", 0.46);
        const string B = "body";
        var leg = 0.032;
        foreach (var (fx, fy) in new[] { (-w / 2 + 0.03, -d / 2 + 0.03), (w / 2 - 0.03, -d / 2 + 0.03) })
            b.Block(B, fx - leg / 2, fx + leg / 2, fy - leg / 2, fy + leg / 2, 0, sh - 0.03);
        // The back legs run up into the back.
        foreach (var fx in new[] { -w / 2 + 0.03, w / 2 - 0.03 })
            b.Block(B, fx - leg / 2, fx + leg / 2, d / 2 - 0.04, d / 2 - 0.04 + leg, 0, h);
        b.Block(B, -w / 2, w / 2, -d / 2, d / 2 - 0.03, sh - 0.03, sh - (s.Bool("upholstered") ? 0.005 : 0), 0.015);
        if (s.Bool("upholstered")) b.Block("upholstery", -w / 2 + 0.01, w / 2 - 0.01, -d / 2 + 0.01, d / 2 - 0.05, sh - 0.005, sh + 0.035, 0.02);
        b.Block(B, -w / 2 + 0.03 + leg / 2, w / 2 - 0.03 - leg / 2, d / 2 - 0.035, d / 2 - 0.012, Math.Max(sh + 0.1, h - 0.3), h - 0.01, 0.01);
        if (s.Bool("arms"))
            foreach (var x in new[] { -w / 2, w / 2 - 0.04 })
            {
                b.Block(B, x, x + 0.04, -d / 2 + 0.06, d / 2 - 0.04, sh + 0.2, sh + 0.23, 0.01);
                b.Block(B, x + 0.005, x + 0.035, -d / 2 + 0.07, -d / 2 + 0.1, sh, sh + 0.2);
            }
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2 - 0.03, 0.02);
        b.PlanRect(Sym, -w / 2 + 0.03, w / 2 - 0.03, d / 2 - 0.035, d / 2 - 0.012);
    }

    // ---- Beds ----

    static void Bed(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, mh = s.Num("mattressHeight", 0.5);
        var hb = s.Bool("headboard") ? 0.08 : 0.0;
        var fb = s.Bool("footboard") ? 0.04 : 0.0;
        double back = d / 2 - hb, front = -d / 2 + fb;
        b.Block("body", -w / 2, w / 2, front, back, 0.06, mh - 0.2, 0.01);
        foreach (var (fx, fy) in new[] { (-w / 2 + 0.05, front + 0.05), (w / 2 - 0.05, front + 0.05), (-w / 2 + 0.05, back - 0.05), (w / 2 - 0.05, back - 0.05) })
            b.Block("body", fx - 0.025, fx + 0.025, fy - 0.025, fy + 0.025, 0, 0.06);
        b.Block("linen", -w / 2 + 0.015, w / 2 - 0.015, front + 0.015, back - 0.01, mh - 0.2, mh, 0.06);
        if (hb > 0) b.Block("upholstery", -w / 2, w / 2, back, d / 2, 0.06, Math.Max(h, mh + 0.15), 0.03);
        if (fb > 0) b.Block("body", -w / 2, w / 2, -d / 2, front, 0.06, mh + 0.08, 0.01);
        var pillows = Math.Clamp(s.Int("pillows", 2), 0, 4);
        if (w < 1.2) pillows = Math.Min(pillows, 1);
        var pw = pillows > 0 ? (w - 0.08) / pillows : 0;
        for (var i = 0; i < pillows; i++)
        {
            double x0 = -w / 2 + 0.04 + i * pw + 0.02, x1 = -w / 2 + 0.04 + (i + 1) * pw - 0.02;
            b.Block("linen", x0, x1, back - 0.5, back - 0.06, mh, mh + 0.12, 0.07);
            b.PlanRect(Sym, x0, x1, back - 0.5, back - 0.06, 0.07);
        }
        var duvetTop = back - (pillows > 0 ? 0.6 : 0.25);
        if (s.Bool("duvet"))
        {
            b.Block("upholstery", -w / 2 + 0.005, w / 2 - 0.005, front + 0.005, duvetTop - 0.3, mh, mh + 0.05, 0.04);
            b.Block("linen", -w / 2 + 0.005, w / 2 - 0.005, duvetTop - 0.3, duvetTop, mh, mh + 0.07, 0.04);
            b.Line(Sym, (-w / 2, duvetTop), (w / 2, duvetTop));
            b.Line(Sym, (-w / 2, duvetTop - 0.3), (w / 2 - 0.45, duvetTop - 0.3), (w / 2, duvetTop));
        }
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2);
        if (hb > 0) b.Line(Sym, (-w / 2, back), (w / 2, back));
        if (fb > 0) b.Line(Sym, (-w / 2, front), (w / 2, front));
    }

    // ---- Storage ----

    /// <summary>A carcase on legs (or a plinth) with a grid of drawers: a bedside table, a chest of drawers.</summary>
    static void Cabinet(Builder b, Spec s, double baseH, int rows, int cols)
    {
        double w = s.W, d = s.D, h = s.H;
        var legs = s.Bool("legs");
        b.Block("body", -w / 2, w / 2, -d / 2 + 0.018, d / 2, baseH, h, 0.003);
        if (legs) foreach (var (fx, fy) in new[] { (-w / 2 + 0.04, -d / 2 + 0.05), (w / 2 - 0.04, -d / 2 + 0.05), (-w / 2 + 0.04, d / 2 - 0.04), (w / 2 - 0.04, d / 2 - 0.04) })
                b.Cylinder("metal", fx, fy, 0.015, 0, baseH, 8);
        else b.Block("body", -w / 2 + 0.02, w / 2 - 0.02, -d / 2 + 0.06, d / 2, 0, baseH);
        Fronts(b, -w / 2, w / 2, baseH, h, -d / 2, rows, Math.Max(1, cols), true);
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2);
        b.Line(Sym, (-w / 2, -d / 2 + 0.018), (w / 2, -d / 2 + 0.018));
    }

    /// <summary>A grid of fronts (drawers or doors) over x0…x1, z0…z1, standing in front of y, with their handles.</summary>
    static void Fronts(Builder b, double x0, double x1, double z0, double z1, double y, int rows, int cols, bool drawers, double gap = 0.003)
    {
        if (rows < 1 || cols < 1 || z1 - z0 < 0.05) return;
        double fw = (x1 - x0) / cols, fh = (z1 - z0) / rows;
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                double a = x0 + c * fw + gap / 2, e = x0 + (c + 1) * fw - gap / 2, lo = z0 + r * fh + gap / 2, hi = z0 + (r + 1) * fh - gap / 2;
                b.Block("fronts", a, e, y, y + 0.018, lo, hi, 0.002);
                var mid = (a + e) / 2;
                if (drawers) b.Block("metal", mid - Math.Min(0.06, fw / 4), mid + Math.Min(0.06, fw / 4), y - 0.025, y, hi - Math.Min(0.05, fh / 3) - 0.006, hi - Math.Min(0.05, fh / 3) + 0.006);
                else
                {
                    // A door's handle by the edge it opens at: the left door's on its right, the others' on their left.
                    var hx = c % 2 == 0 && c < cols - 1 ? e - 0.04 : a + 0.04;
                    b.Block("metal", hx - 0.006, hx + 0.006, y - 0.025, y, (lo + hi) / 2 - Math.Min(0.12, fh / 4), (lo + hi) / 2 + Math.Min(0.12, fh / 4));
                }
            }
    }

    static void Wardrobe(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, pl = s.Num("plinth", 0.08);
        int doors = Math.Max(1, s.Int("doors", 3)), drawers = s.Int("drawers", 0);
        var sliding = s.Str("opening") == "sliding";
        var depthFront = sliding ? 0.045 : 0.018;
        b.Block("body", -w / 2, w / 2, -d / 2 + depthFront, d / 2, pl, h, 0.002);
        if (pl > 0) b.Block("body", -w / 2 + 0.01, w / 2 - 0.01, -d / 2 + 0.06, d / 2, 0, pl);
        var dz = drawers > 0 ? Math.Min(0.22 * drawers, (h - pl) * 0.4) : 0;
        if (drawers > 0) Fronts(b, -w / 2, w / 2, pl, pl + dz, -d / 2 + depthFront - 0.018, drawers, 1, true);
        if (sliding)
        {
            // Two tracks: the doors overlap by 30 mm, every other one in front.
            var dw = (w + 0.03 * (doors - 1)) / doors;
            for (var i = 0; i < doors; i++)
            {
                var x0 = -w / 2 + i * (dw - 0.03);
                var y0 = i % 2 == 0 ? -d / 2 : -d / 2 + 0.024;
                b.Block("fronts", x0, x0 + dw, y0, y0 + 0.02, pl + dz + 0.003, h - 0.003, 0.002);
                b.Line(Sym, (x0, y0 + 0.01), (x0 + dw, y0 + 0.01));
            }
        }
        else
        {
            Fronts(b, -w / 2, w / 2, pl + dz, h, -d / 2, 1, doors, false);
            for (var i = 1; i < doors; i++) { var x = -w / 2 + i * w / doors; b.Line(Sym, (x, -d / 2), (x, -d / 2 + 0.06)); }
            b.Line(Sym, (-w / 2, -d / 2 + 0.018), (w / 2, -d / 2 + 0.018));
        }
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2);
        // The hanging rail along its middle, as a wardrobe is drawn.
        b.Line("dashed", (-w / 2 + 0.05, (depthFront) / 2), (w / 2 - 0.05, depthFront / 2));
    }

    static void Bookcase(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, t = 0.018, dh = Math.Min(s.Num("doorsHeight", 0), h - 0.2);
        var shelves = Math.Max(1, s.Int("shelves", 5));
        const string B = "body";
        b.Block(B, -w / 2, -w / 2 + t, -d / 2, d / 2, 0, h);
        b.Block(B, w / 2 - t, w / 2, -d / 2, d / 2, 0, h);
        b.Block(B, -w / 2 + t, w / 2 - t, -d / 2, d / 2, h - t, h);
        b.Block(B, -w / 2 + t, w / 2 - t, -d / 2 + 0.02, d / 2, 0, 0.06);
        b.Block(B, -w / 2 + t, w / 2 - t, -d / 2 + 0.02, d / 2, 0.06, 0.06 + t);
        b.Block(B, -w / 2 + t, w / 2 - t, d / 2 - 0.006, d / 2, 0.06 + t, h - t);
        var step = (h - t - 0.06 - t) / (shelves + 1);
        for (var i = 1; i <= shelves; i++)
        {
            var z = 0.06 + t + i * step;
            b.Block(B, -w / 2 + t, w / 2 - t, -d / 2 + 0.02, d / 2 - 0.006, z - t / 2, z + t / 2);
        }
        if (dh > 0.1) Fronts(b, -w / 2 + 0.001, w / 2 - 0.001, 0.06, dh, -d / 2 - 0.018, 1, w > 0.6 ? 2 : 1, false);
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2);
        b.Line(Sym, (-w / 2 + t, -d / 2), (-w / 2 + t, d / 2 - 0.006), (w / 2 - t, d / 2 - 0.006), (w / 2 - t, -d / 2));
    }

    static void Sideboard(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        var legs = s.Bool("legs");
        var baseH = legs ? 0.16 : 0.06;
        int doors = s.Int("doors", 3), drawers = s.Int("drawers", 3);
        b.Block("body", -w / 2, w / 2, -d / 2 + 0.018, d / 2, baseH, h, 0.003);
        if (legs) foreach (var (fx, fy) in new[] { (-w / 2 + 0.05, -d / 2 + 0.06), (w / 2 - 0.05, -d / 2 + 0.06), (-w / 2 + 0.05, d / 2 - 0.05), (w / 2 - 0.05, d / 2 - 0.05) })
                b.Cylinder("metal", fx, fy, 0.016, 0, baseH, 8);
        else b.Block("body", -w / 2 + 0.02, w / 2 - 0.02, -d / 2 + 0.06, d / 2, 0, baseH);
        var dz = drawers > 0 ? Math.Min(0.16, (h - baseH) * 0.3) : 0;
        if (drawers > 0) Fronts(b, -w / 2, w / 2, h - dz, h, -d / 2, 1, drawers, true);
        if (doors > 0) Fronts(b, -w / 2, w / 2, baseH, h - dz, -d / 2, 1, doors, false);
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2);
        b.Line(Sym, (-w / 2, -d / 2 + 0.018), (w / 2, -d / 2 + 0.018));
    }

    // ---- Tables ----

    static void Table(Builder b, Spec s, bool coffee)
    {
        double w = s.W, d = s.D, h = s.H, t = coffee ? 0.025 : s.Num("topThickness", 0.03);
        var shape = s.Str("shape", "rectangular");
        var pedestal = !coffee && s.Str("base") == "pedestal";
        if (shape == "round") d = w;
        if (shape == "round") { b.Cylinder("top", 0, 0, w / 2, h - t, h, 32); b.Circle(Sym, 0, 0, w / 2, 32); }
        else
        {
            var r = shape == "oval" ? d / 2 : 0.01;
            b.Block("top", -w / 2, w / 2, -d / 2, d / 2, h - t, h, r);
            b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2, r);
        }
        var under = h - t;
        if (pedestal)
        {
            b.Cylinder("metal", 0, 0, 0.05, 0.03, under, 12);
            b.Cylinder("metal", 0, 0, Math.Min(w, d) * 0.3, 0, 0.03, 20);
        }
        else
        {
            var legs = new List<(double, double)>();
            if (shape == "round")
                for (var i = 0; i < 4; i++) { var a = Math.PI / 4 + i * Math.PI / 2; legs.Add((w / 2 * 0.62 * Math.Cos(a), w / 2 * 0.62 * Math.Sin(a))); }
            else
            {
                double ix = shape == "oval" ? w / 2 - d * 0.35 : w / 2 - 0.06, iy = shape == "oval" ? d / 2 - 0.12 : d / 2 - 0.06;
                legs.AddRange([(-ix, -iy), (ix, -iy), (-ix, iy), (ix, iy)]);
            }
            foreach (var (lx, ly) in legs)
                if (coffee) b.Cylinder("metal", lx, ly, 0.018, 0, under, 8);
                else b.Block("metal", lx - 0.025, lx + 0.025, ly - 0.025, ly + 0.025, 0, under);
        }
        if (coffee && s.Bool("shelf"))
        {
            if (shape == "round") b.Cylinder("top", 0, 0, w / 2 * 0.75, 0.1, 0.12, 24);
            else b.Block("top", -w / 2 + 0.06, w / 2 - 0.06, -d / 2 + 0.06, d / 2 - 0.06, 0.1, 0.12, shape == "oval" ? d / 2 - 0.06 : 0);
        }
    }

    static void Desk(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, t = 0.03;
        var side = s.Str("pedestal", "right");
        b.Block("top", -w / 2, w / 2, -d / 2, d / 2, h - t, h, 0.005);
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2);
        const double pw = 0.42;
        bool pl = side == "left", pr = side == "right";
        void Legs(double x) { b.Block("metal", x - 0.025, x + 0.025, -d / 2 + 0.04, -d / 2 + 0.09, 0, h - t); b.Block("metal", x - 0.025, x + 0.025, d / 2 - 0.09, d / 2 - 0.04, 0, h - t); }
        if (!pl) Legs(-w / 2 + 0.06);
        if (!pr) Legs(w / 2 - 0.06);
        if (pl || pr)
        {
            double x0 = pl ? -w / 2 + 0.01 : w / 2 - 0.01 - pw, x1 = x0 + pw;
            b.Block("top", x0, x1, -d / 2 + 0.03, d / 2 - 0.02, 0.02, h - t, 0.003);
            Fronts(b, x0, x1, 0.02, h - t, -d / 2 + 0.012, Math.Max(1, s.Int("drawers", 3)), 1, true);
            b.Line("dashed", (pl ? x1 : x0, -d / 2), (pl ? x1 : x0, d / 2));
        }
    }

    // ---- Kitchen ----

    /// <summary>A kitchen of the main run's length <paramref name="len"/> in its layout (<see cref="KitchenLayouts"/>): runs of base units with a worktop, the sink and the hob, wall units, a tall unit.</summary>
    static void Kitchen(Builder b, Spec s, double len) => KitchenLayouts.Build(b, s, len);
}
