using Lapin.Geometry;

namespace Lapin.Platform.Analysis;

/// <summary>
/// A frame section's properties (metres, m², m⁴, m³), about its centroid, in its local axes: 2 along the depth, 3 along the
/// width. <see cref="I33"/> = ∫ y₂² dA (bending about local 3, the strong axis of an I), <see cref="I22"/> = ∫ y₃² dA;
/// S elastic moduli to the farthest fibre; Z plastic moduli about the equal-area axis; <see cref="J"/> the torsion constant.
/// <see cref="Method"/> says how each was found; <see cref="Centroid"/> is the centroid from the box centre (local 3, local 2).
/// </summary>
public sealed record SectionProps(double Area, double I22, double I33, double S22, double S33, double Z22, double Z33, double J,
    Vec2 Centroid, string Method, string? Standard, string? Family, double RootRadius)
{
    /// <summary>
    /// An unsymmetric section's (an angle, a Z, any outline with a product of inertia) principal axes: the angle (degrees, in
    /// the section's drawing, from its y — the depth — towards its x) of the principal axis taken as local 2; the values above are
    /// then about the principal axes, and <see cref="Geometric"/> keeps them about the geometric ones (with <see cref="Ixy"/>).
    /// Zero and null for a section symmetric about an axis.
    /// </summary>
    public double PrincipalDeg { get; init; }
    public SectionProps? Geometric { get; init; }
    /// <summary>The product of inertia ∫x·y dA in the section's drawing (x across, y up its depth), about the centroid.</summary>
    public double Ixy { get; init; }
    /// <summary>The warping constant (m⁶) and how it was found; null where Lapin has no formula for the shape (an arbitrary outline).</summary>
    public double? Iw { get; init; }
    public string? IwMethod { get; init; }
    /// <summary>The outside's length (m: the paintable surface a metre), its fillets rounded.</summary>
    public double Perimeter { get; init; }
    /// <summary>
    /// The shear areas (m²) for a shear force along local 2 (the depth: an I's web, EN 1993-1-1's Av,z) and along local 3 (the
    /// width: an I's flanges, Av,y), and how they were found; null where Lapin has no rule (an arbitrary outline).
    /// </summary>
    public double? Av2 { get; init; }
    public double? Av3 { get; init; }
    public string? AvMethod { get; init; }
}

/// <summary>
/// The properties of a section from its geometry (docs/jg-structures.md, Section properties): closed forms for rectangles,
/// circles and CHS; for the others the exact integrals of the outline as a polygon (Green's theorem), the root fillets of a
/// catalogue profile rounded in as arcs, the plastic axis found by halving the area; the torsion constant by the closed or
/// thin-walled formulas named in <see cref="SectionProps.Method"/>. Never a tabulated value: Lapin holds no profile tables.
/// </summary>
public static class SectionProperties
{
    /// <summary>
    /// The catalogue's profiles: their product standard, family and root radius (mm; a hollow section's outer corner, its
    /// inner being the wall's thickness, EN 10210-2). Lapin keeps only the nominal h, b, tw, tf; these are what is needed
    /// besides them for the area and the inertias to the published values' precision.
    /// </summary>
    static readonly Dictionary<string, (string Standard, string Family, double R)> Catalogue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IPE 200"] = ("EN 10365:2017", "IPE", 12), ["IPE 240"] = ("EN 10365:2017", "IPE", 15), ["IPE 300"] = ("EN 10365:2017", "IPE", 15),
        ["IPE 330"] = ("EN 10365:2017", "IPE", 18), ["IPE 360"] = ("EN 10365:2017", "IPE", 18), ["IPE 400"] = ("EN 10365:2017", "IPE", 21),
        ["IPE 450"] = ("EN 10365:2017", "IPE", 21), ["IPE 500"] = ("EN 10365:2017", "IPE", 21), ["IPE 550"] = ("EN 10365:2017", "IPE", 24),
        ["IPE 600"] = ("EN 10365:2017", "IPE", 24),
        ["HEA 200"] = ("EN 10365:2017", "HEA", 18), ["HEA 240"] = ("EN 10365:2017", "HEA", 21), ["HEA 300"] = ("EN 10365:2017", "HEA", 27), ["HEA 400"] = ("EN 10365:2017", "HEA", 27),
        ["HEB 200"] = ("EN 10365:2017", "HEB", 18), ["HEB 300"] = ("EN 10365:2017", "HEB", 27),
        ["UPN 200"] = ("EN 10365:2017", "UPN", 11.5), ["UPN 300"] = ("EN 10365:2017", "UPN", 16),
        ["L 50×50×5"] = ("EN 10056-1:2017", "L", 7), ["L 60×60×6"] = ("EN 10056-1:2017", "L", 8), ["L 70×70×7"] = ("EN 10056-1:2017", "L", 9),
        ["L 80×80×8"] = ("EN 10056-1:2017", "L", 10), ["L 100×100×10"] = ("EN 10056-1:2017", "L", 12), ["L 120×80×10"] = ("EN 10056-1:2017", "L", 11),
        ["RHS 100×100×5"] = ("EN 10210-2:2019", "RHS", 7.5), ["RHS 150×100×6"] = ("EN 10210-2:2019", "RHS", 9), ["RHS 200×100×8"] = ("EN 10210-2:2019", "RHS", 12),
        ["CHS 60.3×3.2"] = ("EN 10210-2:2019", "CHS", 0), ["CHS 88.9×4"] = ("EN 10210-2:2019", "CHS", 0), ["CHS 114.3×5"] = ("EN 10210-2:2019", "CHS", 0), ["CHS 168.3×6"] = ("EN 10210-2:2019", "CHS", 0),
    };

    /// <summary>The standard, family and root radius (metres) of a catalogue designation; null for anything else.</summary>
    public static (string Standard, string Family, double Radius)? CatalogueOf(string? designation) =>
        designation != null && Catalogue.TryGetValue(designation, out var c) ? (c.Standard, c.Family, c.R / 1000) : null;

    /// <summary>The section's properties; null when its geometry cannot give them (no size, an unknown kind).</summary>
    public static SectionProps? Of(SectionShape s)
    {
        var p = Core(s);
        if (p == null) return null;
        var r = s.Root is { } own && own >= 0 ? own : CatalogueOf(s.Designation)?.Radius ?? 0;
        return ShearAreas(s, p, r) is { } v ? p with { Av2 = Math.Min(v.Av2, p.Area), Av3 = Math.Min(v.Av3, p.Area), AvMethod = v.Method } : p;
    }

    /// <summary>
    /// The shear areas: a rectangle's 5/6·A, a circle's (and an ellipse's) 0.9·A; EN 1993-1-1:2005 §6.2.6(3) for the hot-rolled
    /// and hollow sections — an I or H Av,z = A − 2b·tf + (tw + 2r)·tf, not less than hw·tw, and Av,y = 2b·tf; a channel
    /// A − 2b·tf + (tw + r)·tf; a T A − b·tf + (tw + 2r)·tf/2; an RHS A·h/(b + h) along its depth, A·b/(b + h) across; a CHS
    /// 2A/π — and for the other open sections their webs' and flanges' areas (an angle's legs, a cold-formed sheet's web and
    /// flanges, an asymmetric I's web and both flanges). None for an arbitrary outline.
    /// </summary>
    static (double Av2, double Av3, string Method)? ShearAreas(SectionShape s, SectionProps p, double r)
    {
        double a = p.Area, b = s.Width, h = s.Depth, tw = s.Web, tf = s.Flange;
        const string En = "EN 1993-1-1:2005 §6.2.6(3)";
        switch (s.Kind)
        {
            case "rectangle": return (5.0 / 6 * a, 5.0 / 6 * a, "5/6·A (a rectangle)");
            case "circle" or "ellipse": return (0.9 * a, 0.9 * a, "0.9·A (a solid circle)");
            case "CHS": return (2 * a / Math.PI, 2 * a / Math.PI, $"2A/π, {En}(g)");
            case "RHS": return (a * h / (b + h), a * b / (b + h), $"A·h/(b + h) and A·b/(b + h), {En}(f)");
            case "I":
            {
                var hw = h - 2 * tf;
                return (Math.Max(a - 2 * b * tf + (tw + 2 * r) * tf, hw * tw), 2 * b * tf, $"A − 2b·tf + (tw + 2r)·tf, not less than hw·tw; 2b·tf across: {En}(a)");
            }
            case "U": return (a - 2 * b * tf + (tw + r) * tf, 2 * b * tf, $"A − 2b·tf + (tw + r)·tf; 2b·tf across: {En}(b)");
            case "T": return (a - b * tf + (tw + 2 * r) * tf / 2, b * tf, $"A − b·tf + (tw + 2r)·tf/2; b·tf across: {En}(c)");
            case "asymI":
            {
                var (b2, tf2) = (s.Width2 > 0 ? s.Width2 : b, s.Flange2 > 0 ? s.Flange2 : tf);
                return ((h - tf - tf2) * tw, b * tf + b2 * tf2, "the web's area along the depth, the flanges' across");
            }
            case "L": return (h * tw, b * tf, "each leg's area along it");
            case "C" or "Z" or "SIGMA": return ((h - 2 * tw) * tw, 2 * (b - tw) * tw, "the web's area along the depth, the flanges' across");
            default: return null;
        }
    }

    static SectionProps? Core(SectionShape s)
    {
        if (!(s.Width > 0 && s.Depth > 0) || !double.IsFinite(s.Width) || !double.IsFinite(s.Depth)) return null;
        var cat = CatalogueOf(s.Designation);
        var (standard, family, r) = cat is { } c ? (c.Standard, c.Family, c.Radius) : ((string?)null, (string?)null, 0.0);
        if (s.Root is { } own && own >= 0) r = own;
        standard = s.Standard ?? standard;
        family = s.Family ?? family;
        double b = s.Width, h = s.Depth;
        switch (s.Kind)
        {
            case "rectangle":
            {
                var (a, t) = (Math.Max(b, h), Math.Min(b, h));
                var j = a * t * t * t * (1.0 / 3 - 0.21 * (t / a) * (1 - Math.Pow(t / a, 4) / 12));
                return new SectionProps(b * h, h * b * b * b / 12, b * h * h * h / 12, h * b * b / 6, b * h * h / 6, h * b * b / 4, b * h * h / 4, j, default,
                    "closed form; J = a·b³·(1/3 − 0.21·(b/a)·(1 − b⁴/(12a⁴))) (Roark)", standard, family, 0) { Perimeter = 2 * (b + h) };
            }
            case "circle":
            {
                var d = b;
                var i = Math.PI * Math.Pow(d, 4) / 64;
                return new SectionProps(Math.PI * d * d / 4, i, i, i / (d / 2), i / (d / 2), d * d * d / 6, d * d * d / 6, 2 * i, default, "closed form (exact)", standard, family, 0)
                    { Iw = 0, IwMethod = "a circle does not warp", Perimeter = Math.PI * d };
            }
            case "ellipse":
            {
                // Semi-axes a across, c up the depth: A = π·a·c, I about the horizontal axis π·a·c³/4, Z = 4·a·c²/3, J = π·a³·c³/(a² + c²).
                var (a, cc) = (b / 2, h / 2);
                double i33 = Math.PI * a * cc * cc * cc / 4, i22 = Math.PI * cc * a * a * a / 4;
                var per = Math.PI * (3 * (a + cc) - Math.Sqrt((3 * a + cc) * (a + 3 * cc)));
                return new SectionProps(Math.PI * a * cc, i22, i33, i22 / a, i33 / cc, 4 * cc * a * a / 3, 4 * a * cc * cc / 3, Math.PI * Math.Pow(a, 3) * Math.Pow(cc, 3) / (a * a + cc * cc),
                    default, "closed form (exact); perimeter by Ramanujan", standard, family, 0) { Perimeter = per };
            }
            case "CHS":
            {
                var (d, t) = (b, s.Web);
                if (!(t > 0 && 2 * t < d)) return null;
                var di = d - 2 * t;
                var i = Math.PI * (Math.Pow(d, 4) - Math.Pow(di, 4)) / 64;
                var z = (d * d * d - di * di * di) / 6;
                return new SectionProps(Math.PI / 4 * (d * d - di * di), i, i, i / (d / 2), i / (d / 2), z, z, 2 * i, default, "closed form (exact)", standard, family, 0)
                    { Iw = 0, IwMethod = "closed section: negligible", Perimeter = Math.PI * d };
            }
        }
        var rings = Rings(s, r);
        if (rings == null || rings.Count == 0) return null;
        var (area, sx, sy, _, _) = Integrals(rings);
        if (!(area > 0)) return null;
        var (cx, cy) = (sy / area, sx / area);
        var moved = rings.Select(ring => ring.Select(p => new Vec2(p.X - cx, p.Y - cy)).ToList()).ToList();
        var (jv, jm) = Torsion(s, r);
        var method = r > 0 ? $"polygon integrals of the nominal outline with root radius r = {r * 1000:0.#} mm ({standard}); J: {jm}" : $"polygon integrals of the outline (sharp corners); J: {jm}";
        var perimeter = rings.Where(x => Polygon2.SignedArea(x) > 0).Sum(Length);
        var geometric = About(moved, area, jv, new Vec2(cx, cy), method, standard, family, r);
        if (s.Kind == "outline")
        {
            // No formula for any outline: Saint-Venant's estimate for a compact solid, A⁴/(40·Ip) (too high for a thin-walled one).
            jv = Math.Pow(area, 4) / (40 * (geometric.I22 + geometric.I33));
            method = "polygon integrals of the outline; J: Saint-Venant's estimate A⁴/(40·Ip), for compact solids only";
            geometric = geometric with { J = jv, Method = method };
        }
        var (iw, iwMethod) = Warping(s, geometric);
        geometric = geometric with { Iw = iw, IwMethod = iwMethod, Perimeter = perimeter };
        var ixy = Product(moved);
        if (Math.Abs(ixy) <= 1e-9 * (geometric.I22 + geometric.I33)) return geometric with { Ixy = 0 };
        // The principal axis nearest the depth: ∫(p·v)² is extreme for v = (sin φ, cos φ) with tan 2φ = 2·Ixy / (Ixx − Iyy).
        var phi = 0.5 * Math.Atan2(2 * ixy, geometric.I33 - geometric.I22);
        if (phi > Math.PI / 4 + 1e-12) phi -= Math.PI / 2;
        if (phi <= -Math.PI / 4 - 1e-12) phi += Math.PI / 2;
        var (sn, cs) = (Math.Sin(phi), Math.Cos(phi));
        // Coordinates along the principal axes: y' along v (the new local 2), x' across it.
        var turned = moved.Select(ring => ring.Select(p => new Vec2(p.X * cs - p.Y * sn, p.X * sn + p.Y * cs)).ToList()).ToList();
        var principal = About(turned, area, jv, new Vec2(cx, cy), method + "; about the principal axes", standard, family, r);
        return principal with { PrincipalDeg = phi * 180 / Math.PI, Geometric = geometric with { Ixy = ixy }, Ixy = ixy, Iw = iw, IwMethod = iwMethod, Perimeter = perimeter };
    }

    static double Length(IReadOnlyList<Vec2> ring)
    {
        var l = 0.0;
        for (var i = 0; i < ring.Count; i++) l += (ring[(i + 1) % ring.Count] - ring[i]).Length;
        return l;
    }

    /// <summary>
    /// The warping constant: a doubly symmetric I's Iz·(h − tf)²/4, an asymmetric I's hs²·I1·I2/(I1 + I2), a channel's
    /// thin-walled formula, a cold-formed section's from its sectorial coordinate along the sheet's centreline about its shear
    /// centre; none for sections whose walls meet at a point (an angle, a T) or that are closed (negligible); null for the others.
    /// </summary>
    static (double? Iw, string? Method) Warping(SectionShape s, SectionProps p)
    {
        double b = s.Width, h = s.Depth, tw = s.Web, tf = s.Flange;
        switch (s.Kind)
        {
            case "I": return (p.I22 * (h - tf) * (h - tf) / 4, "Iw = Iz·(h − tf)²/4");
            case "asymI":
            {
                var (b2, tf2) = (s.Width2 > 0 ? s.Width2 : b, s.Flange2 > 0 ? s.Flange2 : tf);
                double i1 = tf * b * b * b / 12, i2 = tf2 * b2 * b2 * b2 / 12, hs = h - (tf + tf2) / 2;
                return (hs * hs * i1 * i2 / (i1 + i2), "Iw = hs²·I1·I2/(I1 + I2), the flanges' own inertias");
            }
            case "U":
            {
                double bp = b - tw / 2, hp = h - tf;
                return (tf * bp * bp * bp * hp * hp / 12 * (3 * bp * tf + 2 * hp * tw) / (6 * bp * tf + hp * tw), "thin-walled channel Iw = tf·b'³·h'²/12·(3b'tf + 2h'tw)/(6b'tf + h'tw)");
            }
            case "L" or "T": return (0, "thin-walled: its walls meet at a point (negligible)");
            case "RHS" or "CHS": return (0, "closed section: negligible");
            case "C" or "Z" or "SIGMA":
            {
                var line = Centreline(s);
                return line.Count >= 2 && tw > 0 ? (SectorialWarping(line, tw), "sectorial coordinate along the sheet's centreline, about the shear centre") : (null, null);
            }
            default: return (null, null);
        }
    }

    /// <summary>
    /// An open thin-walled chain's warping constant (one thickness <paramref name="t"/>, along <paramref name="line"/>): the
    /// sectorial coordinate from the centroid, made mean-free, the pole moved to where ∫ω² dA is least (the shear centre).
    /// </summary>
    public static double SectorialWarping(IReadOnlyList<Vec2> line, double t)
    {
        var n = line.Count;
        double a = 0, sx = 0, sy = 0;
        for (var i = 0; i + 1 < n; i++)
        {
            var l = (line[i + 1] - line[i]).Length;
            a += t * l; sx += t * l * (line[i].X + line[i + 1].X) / 2; sy += t * l * (line[i].Y + line[i + 1].Y) / 2;
        }
        if (!(a > 0)) return 0;
        var c = new Vec2(sx / a, sy / a);
        var p = line.Select(q => q - c).ToList();
        // ω at the nodes about the centroid: twice the area swept from it.
        var w = new double[n];
        for (var i = 0; i + 1 < n; i++) w[i + 1] = w[i] + (p[i].X * p[i + 1].Y - p[i + 1].X * p[i].Y);
        double Int(Func<int, double> f, Func<int, double> g)
        {
            double sum = 0;
            for (var i = 0; i + 1 < n; i++)
            {
                var l = (p[i + 1] - p[i]).Length;
                sum += t * l / 6 * (2 * f(i) * g(i) + f(i) * g(i + 1) + f(i + 1) * g(i) + 2 * f(i + 1) * g(i + 1));
            }
            return sum;
        }
        var mean = Int(i => w[i], _ => 1) / a;
        var wb = w.Select(x => x - mean).ToArray();
        double ixx = Int(i => p[i].Y, i => p[i].Y), iyy = Int(i => p[i].X, i => p[i].X), ixy = Int(i => p[i].X, i => p[i].Y);
        double iwy = Int(i => wb[i], i => p[i].Y), iwx = Int(i => wb[i], i => p[i].X);
        // ω about the pole (px, py): ω̄ − px·y + py·x; its square's integral least where both its derivatives vanish.
        var det = -ixx * iyy + ixy * ixy;
        double px = 0, py = 0;
        if (Math.Abs(det) > 1e-30) { px = (-iwy * iyy + ixy * iwx) / det; py = (-ixx * -iwx - (-ixy) * -iwy) / det; }
        var wp = Enumerable.Range(0, n).Select(i => wb[i] - px * p[i].Y + py * p[i].X).ToArray();
        return Math.Max(0, Int(i => wp[i], i => wp[i]));
    }

    /// <summary>
    /// The section's rings as its properties are found from them (outsides anticlockwise, holes clockwise; fillets rounded),
    /// about its box's centre; a circle, a CHS and an ellipse by the chord rule. Null when the shape cannot give them.
    /// </summary>
    /// <param name="chords">The fillets by the chord rule (<see cref="Chords"/>, for a body a view draws) rather than the fine steps the properties are integrated over.</param>
    public static List<List<Vec2>>? RingsOf(SectionShape s, bool chords = false)
    {
        if (!(s.Width > 0 && s.Depth > 0) && s.Kind != "outline") return null;
        List<Vec2> Ellipse(double a, double c, bool hole = false)
        {
            var n = Chords.Circle(Math.Max(a, c));
            var o = Enumerable.Range(0, n).Select(k => new Vec2(a * Math.Cos(2 * Math.PI * k / n), c * Math.Sin(2 * Math.PI * k / n))).ToList();
            if (hole) o.Reverse();
            return o;
        }
        switch (s.Kind)
        {
            case "circle": return [Ellipse(s.Width / 2, s.Width / 2)];
            case "ellipse": return [Ellipse(s.Width / 2, s.Depth / 2)];
            case "CHS": return s.Web > 0 && 2 * s.Web < s.Width ? [Ellipse(s.Width / 2, s.Width / 2), Ellipse(s.Width / 2 - s.Web, s.Width / 2 - s.Web, true)] : null;
            case "rectangle": { double w = s.Width / 2, d = s.Depth / 2; return [[new(-w, -d), new(w, -d), new(w, d), new(-w, d)]]; }
        }
        var r = s.Root ?? CatalogueOf(s.Designation)?.Radius ?? 0;
        return Rings(s, r, chords);
    }

    /// <summary>The properties about the axes of the rings' coordinates (centred).</summary>
    static SectionProps About(List<List<Vec2>> rings, double area, double j, Vec2 centroid, string method, string? standard, string? family, double r)
    {
        var (_, _, _, ixx, iyy) = Integrals(rings);
        var ymax = rings.SelectMany(x => x).Max(p => Math.Abs(p.Y));
        var xmax = rings.SelectMany(x => x).Max(p => Math.Abs(p.X));
        return new(area, iyy, ixx, iyy / xmax, ixx / ymax, Plastic(rings, area, false), Plastic(rings, area, true), j, centroid, method, standard, family, r);
    }

    /// <summary>The rings' product of inertia ∫x·y dA (each ring by its orientation's sign).</summary>
    static double Product(IEnumerable<IReadOnlyList<Vec2>> rings)
    {
        double ixy = 0;
        foreach (var ring in rings)
            for (var i = 0; i < ring.Count; i++)
            {
                var (p, q) = (ring[i], ring[(i + 1) % ring.Count]);
                var c = p.X * q.Y - q.X * p.Y;
                ixy += (p.X * q.Y + 2 * p.X * p.Y + 2 * q.X * q.Y + q.X * p.Y) * c;
            }
        return ixy / 24;
    }

    /// <summary>The section's rings, counter-clockwise outside and clockwise holes, its root fillets (and a hollow section's corners) rounded.</summary>
    static List<List<Vec2>>? Rings(SectionShape s, double r, bool chords = false)
    {
        double w = s.Width / 2, d = s.Depth / 2, tw = s.Web, tf = s.Flange;
        List<Vec2> outline;
        List<List<Vec2>> holes = [];
        // Sloping flanges: the inner face falls towards the toe by the slope, the thickness tf where it is measured (an I's a
        // quarter of its flange from the toe, DIN 1025-1; a channel's half its flange from the web's back, DIN 1026-1).
        var k = s.Slope > 0 && s.Slope < 0.5 ? s.Slope : 0;
        switch (s.Kind)
        {
            case "I":
            {
                double xm = w / 2, root = tf + k * (xm - tw / 2), toe = tf - k * (w - xm);
                outline = [new(-w, -d), new(w, -d), new(w, -d + toe), new(tw / 2, -d + root), new(tw / 2, d - root), new(w, d - toe), new(w, d), new(-w, d), new(-w, d - toe), new(-tw / 2, d - root), new(-tw / 2, -d + root), new(-w, -d + toe)];
                break;
            }
            case "asymI":
            {
                // The top flange Width × Flange, the bottom Width2 × Flange2, the web centred.
                double w2 = (s.Width2 > 0 ? s.Width2 : s.Width) / 2, tf2 = s.Flange2 > 0 ? s.Flange2 : tf;
                if (!(tw > 0 && tf > 0 && tf2 > 0 && tf + tf2 < s.Depth)) return null;
                outline = [new(-w2, -d), new(w2, -d), new(w2, -d + tf2), new(tw / 2, -d + tf2), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf), new(-tw / 2, -d + tf2), new(-w2, -d + tf2)];
                break;
            }
            case "U":
            {
                double xm = -w + Math.Max(s.Width / 2, tw), root = tf + k * (xm - (-w + tw)), toe = tf - k * (w - xm);
                outline = [new(-w, -d), new(w, -d), new(w, -d + toe), new(-w + tw, -d + root), new(-w + tw, d - root), new(w, d - toe), new(w, d), new(-w, d)];
                break;
            }
            case "outline":
                return s.Rings is { Count: > 0 } given ? [.. given.Where(x => x.Count >= 3).Select(x => x.ToList())] : null;
            case "T": outline = [new(-tw / 2, -d), new(tw / 2, -d), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf)]; break;
            case "L": outline = [new(-w, -d), new(w, -d), new(w, -d + tf), new(-w + tw, -d + tf), new(-w + tw, d), new(-w, d)]; break;
            case "RHS":
            {
                var t = tw;
                if (!(t > 0 && t < w && t < d)) return null;
                outline = [new(-w, -d), new(w, -d), new(w, d), new(-w, d)];
                var hole = new List<Vec2> { new(-w + t, -d + t), new(-w + t, d - t), new(w - t, d - t), new(w - t, -d + t) };
                if (r > 0) { outline = Round(outline, (_, _) => r, chords); hole = Round(hole, (_, _) => s.Toe ?? Math.Max(r - t, 0), chords); }
                return [outline, hole];
            }
            case "C" or "Z" or "SIGMA":
            {
                var line = Centreline(s);
                if (line.Count < 2 || !(tw > 0)) return null;
                return [EnsureCcw(Sheet(line, tw))];
            }
            default: return null;
        }
        if (s.Kind is "I" or "U" or "T" or "L" && !(tw > 0 && tf > 0)) return null;
        outline = EnsureCcw(outline);
        // The root fillets: the re-entrant corners (a clockwise turn in a counter-clockwise outline) rounded by r; the toes
        // (the inner corners of the flanges' or legs' tips) by r2, an angle's r/2 when not given (EN 10056-1).
        int[] toes = s.Kind switch { "I" or "asymI" => [2, 5, 8, 11], "U" => [2, 5], "T" => [3, 6], "L" => [2, 4], _ => [] };
        var r2 = s.Toe ?? (s.Kind == "L" ? r / 2 : 0);
        if (r > 0 || r2 > 0) outline = Round(outline, (i, turn) => turn < 0 ? r : toes.Contains(i) ? r2 : 0, chords);
        return [outline, .. holes];
    }

    /// <summary>A ring's corners rounded by the radius the turn there asks (0: left sharp), each by an arc in 16 steps a quarter.</summary>
    static List<Vec2> Round(List<Vec2> ring, Func<double, double> radiusFor) => Round(ring, (_, turn) => radiusFor(turn), false);

    /// <summary>A ring's corners rounded by the radius each corner (its index, its turn) asks.</summary>
    static List<Vec2> Round(List<Vec2> ring, Func<int, double, double> radiusFor, bool chords = false)
    {
        var o = new List<Vec2>();
        var n = ring.Count;
        double Turn(int i)
        {
            var (a, v, b) = (ring[(i - 1 + n) % n], ring[i], ring[(i + 1) % n]);
            var (d1, d2) = ((v - a).Normalized(), (b - v).Normalized());
            return Math.Atan2(Vec2.Cross(d1, d2), Vec2.Dot(d1, d2));
        }
        var radii = Enumerable.Range(0, n).Select(i => radiusFor(i, Turn(i))).ToArray();
        for (var i = 0; i < n; i++)
        {
            var (a, v, b) = (ring[(i - 1 + n) % n], ring[i], ring[(i + 1) % n]);
            var (d1, d2) = ((v - a).Normalized(), (b - v).Normalized());
            var turn = Turn(i);
            var r = radii[i];
            var tangent = r * Math.Tan(Math.Abs(turn) / 2);
            // An edge shared with another rounded corner gives each half of it; an edge whose other end stays sharp, all of it.
            var room = Math.Min((v - a).Length * (radii[(i - 1 + n) % n] > 0 ? 0.5 : 1), (b - v).Length * (radii[(i + 1) % n] > 0 ? 0.5 : 1));
            if (r <= 0 || Math.Abs(turn) < 1e-9 || tangent > room + 1e-12) { o.Add(v); continue; }
            var t1 = v - d1 * tangent;
            var side = Math.Sign(turn);
            var centre = t1 + d1.Perp * (side * r);
            var start = Math.Atan2(t1.Y - centre.Y, t1.X - centre.X);
            var steps = chords ? Chords.Count(r, turn, 2) : Math.Max(2, (int)Math.Ceiling(Math.Abs(turn) / (Math.PI / 2) * 16));
            for (var k = 0; k <= steps; k++)
            {
                var ang = start + turn * k / steps;
                o.Add(new Vec2(centre.X + r * Math.Cos(ang), centre.Y + r * Math.Sin(ang)));
            }
        }
        return o;
    }

    /// <summary>A cold-formed section's sheet through its thickness's middle, as <c>Section.Centreline</c> lays it.</summary>
    static List<Vec2> Centreline(SectionShape s)
    {
        double h = s.Web / 2, x = s.Width / 2 - h, y = s.Depth / 2 - h, lip = s.Lip > h ? s.Depth / 2 - s.Lip : double.NaN;
        // Without lips a flange's square end stands at the width: the width is always the whole section's.
        var tip = double.IsNaN(lip) ? s.Width / 2 : x;
        var o = new List<Vec2>();
        switch (s.Kind)
        {
            case "C" or "SIGMA":
                if (!double.IsNaN(lip)) o.Add(new(x, -lip));
                o.AddRange([new(tip, -y), new(-x, -y)]);
                if (s.Kind == "SIGMA")
                {
                    var fold = s.Width / 4;
                    o.AddRange([new(-x, -s.Depth / 4 - fold / 2), new(-x + fold, -s.Depth / 4 + fold / 2), new(-x + fold, s.Depth / 4 - fold / 2), new(-x, s.Depth / 4 + fold / 2)]);
                }
                o.AddRange([new(-x, y), new(tip, y)]);
                if (!double.IsNaN(lip)) o.Add(new(x, lip));
                break;
            case "Z":
                if (!double.IsNaN(lip)) o.Add(new(-x, -lip));
                o.AddRange([new(-tip, -y), new(0, -y), new(0, y), new(tip, y)]);
                if (!double.IsNaN(lip)) o.Add(new(x, lip));
                break;
        }
        return o;
    }

    static List<Vec2> Sheet(IReadOnlyList<Vec2> line, double t)
    {
        List<Vec2> Side(double off)
        {
            var o = new List<Vec2>();
            for (var i = 0; i < line.Count; i++)
            {
                var din = i > 0 ? (line[i] - line[i - 1]).Normalized() : (line[1] - line[0]).Normalized();
                var dout = i < line.Count - 1 ? (line[i + 1] - line[i]).Normalized() : din;
                var n = (din.Perp + dout.Perp).Normalized();
                o.Add(line[i] + n * (off / Math.Max(Vec2.Dot(n, din.Perp), 1e-6)));
            }
            return o;
        }
        var right = Side(-t / 2);
        right.Reverse();
        return [.. Side(t / 2), .. right];
    }

    static List<Vec2> EnsureCcw(List<Vec2> ring) => Polygon2.SignedArea(ring) < 0 ? Enumerable.Reverse(ring).ToList() : ring;

    /// <summary>Area, first moments (∫y, ∫x) and second moments (∫y², ∫x²) of the rings, each by its orientation's sign.</summary>
    static (double A, double Sx, double Sy, double Ixx, double Iyy) Integrals(IEnumerable<IReadOnlyList<Vec2>> rings)
    {
        double a = 0, sx = 0, sy = 0, ixx = 0, iyy = 0;
        foreach (var ring in rings)
            for (var i = 0; i < ring.Count; i++)
            {
                var (p, q) = (ring[i], ring[(i + 1) % ring.Count]);
                var c = p.X * q.Y - q.X * p.Y;
                a += c;
                sx += (p.Y + q.Y) * c;
                sy += (p.X + q.X) * c;
                ixx += (p.Y * p.Y + p.Y * q.Y + q.Y * q.Y) * c;
                iyy += (p.X * p.X + p.X * q.X + q.X * q.X) * c;
            }
        return (a / 2, sx / 6, sy / 6, ixx / 12, iyy / 12);
    }

    /// <summary>The plastic modulus about the axis that halves the area, the coordinate y (<paramref name="alongY"/>) or x: ∫|u − c| dA.</summary>
    static double Plastic(List<List<Vec2>> rings, double area, bool alongY)
    {
        double U(Vec2 p) => alongY ? p.Y : p.X;
        (double A, double M) Below(double c)
        {
            double a = 0, m = 0;
            foreach (var ring in rings)
            {
                var clipped = Clip(ring, U, c);
                if (clipped.Count < 3) continue;
                var (ra, rsx, rsy, _, _) = Integrals([clipped]);
                a += ra;
                m += alongY ? rsx : rsy;
            }
            return (a, m);
        }
        var all = rings.SelectMany(x => x).Select(U).ToList();
        double lo = all.Min(), hi = all.Max();
        for (var k = 0; k < 80; k++)
        {
            var mid = (lo + hi) / 2;
            if (Below(mid).A < area / 2) lo = mid; else hi = mid;
        }
        var cut = (lo + hi) / 2;
        var (aBelow, mBelow) = Below(cut);
        var (_, sxAll, syAll, _, _) = Integrals(rings);
        var mAll = alongY ? sxAll : syAll;
        var (aAbove, mAbove) = (area - aBelow, mAll - mBelow);
        return (mAbove - cut * aAbove) - (mBelow - cut * aBelow);
    }

    /// <summary>A ring clipped to where the coordinate is at most <paramref name="c"/> (Sutherland–Hodgman; its orientation kept).</summary>
    static List<Vec2> Clip(IReadOnlyList<Vec2> ring, Func<Vec2, double> coord, double c)
    {
        var o = new List<Vec2>();
        for (var i = 0; i < ring.Count; i++)
        {
            var (p, q) = (ring[i], ring[(i + 1) % ring.Count]);
            var (up, uq) = (coord(p), coord(q));
            var (pin, qin) = (up <= c, uq <= c);
            if (pin) o.Add(p);
            if (pin != qin)
            {
                var t = (c - up) / (uq - up);
                o.Add(new Vec2(p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t));
            }
        }
        return o;
    }

    /// <summary>The torsion constant and how it was found.</summary>
    static (double J, string Method) Torsion(SectionShape s, double r)
    {
        double b = s.Width, h = s.Depth, tw = s.Web, tf = s.Flange;
        switch (s.Kind)
        {
            case "I":
            {
                // ArcelorMittal's formula for rolled I sections (flanges, web, and the web–flange junctions with their root radius).
                var d = ((r + tw / 2) * (r + tw / 2) + (r + tf) * (r + tf) - r * r) / (2 * r + tf);
                var alpha = tw / tf * (0.145 + 0.1 * r / tf);
                var j = 2.0 / 3 * (b - 0.63 * tf) * Math.Pow(tf, 3) + 1.0 / 3 * (h - 2 * tf) * Math.Pow(tw, 3) + 2 * alpha * Math.Pow(d, 4);
                return (j, "rolled I formula It = 2/3·(b − 0.63·tf)·tf³ + 1/3·(h − 2tf)·tw³ + 2·α·D⁴ (with its root radius)");
            }
            case "RHS":
            {
                // EN 10210-2 Annex B: It = t³·h/3 + 2·K·Ah, the mean corner radius Rc = (ro + ri)/2.
                var t = tw;
                var rc = r > 0 ? (r + Math.Max(r - t, 0)) / 2 : 0;
                var ah = (b - t) * (h - t) - rc * rc * (4 - Math.PI);
                var hp = 2 * ((b - t) + (h - t)) - 2 * rc * (4 - Math.PI);
                var k = 2 * ah * t / hp;
                return (t * t * t * hp / 3 + 2 * k * ah, "EN 10210-2 Annex B, It = t³·h/3 + 2·K·Ah");
            }
            case "U": return (2.0 / 3 * (b - tw / 2) * Math.Pow(tf, 3) + 1.0 / 3 * (h - tf) * Math.Pow(tw, 3), "thin-walled open section Σ b·t³/3 (fillets left out)");
            case "asymI":
            {
                var (b2, tf2) = (s.Width2 > 0 ? s.Width2 : b, s.Flange2 > 0 ? s.Flange2 : tf);
                return ((b * Math.Pow(tf, 3) + b2 * Math.Pow(tf2, 3) + (h - (tf + tf2) / 2) * Math.Pow(tw, 3)) / 3, "thin-walled open section Σ b·t³/3 (fillets left out)");
            }
            case "outline": return (0, "");
            case "T": return (1.0 / 3 * b * Math.Pow(tf, 3) + 1.0 / 3 * (h - tf / 2) * Math.Pow(tw, 3), "thin-walled open section Σ b·t³/3 (fillets left out)");
            case "L": return (1.0 / 3 * (b - tw / 2) * Math.Pow(tf, 3) + 1.0 / 3 * (h - tf / 2) * Math.Pow(tw, 3), "thin-walled open section Σ b·t³/3 (fillets left out)");
            default:
            {
                var line = Centreline(s);
                var length = 0.0;
                for (var i = 1; i < line.Count; i++) length += (line[i] - line[i - 1]).Length;
                return (length * Math.Pow(tw, 3) / 3, "thin-walled open section L·t³/3 along the sheet's centreline");
            }
        }
    }
}
