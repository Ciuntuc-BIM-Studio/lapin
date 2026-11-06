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
        if (!(s.Width > 0 && s.Depth > 0) || !double.IsFinite(s.Width) || !double.IsFinite(s.Depth)) return null;
        var cat = CatalogueOf(s.Designation);
        var (standard, family, r) = cat is { } c ? (c.Standard, c.Family, c.Radius) : ((string?)null, (string?)null, 0.0);
        double b = s.Width, h = s.Depth;
        switch (s.Kind)
        {
            case "rectangle":
            {
                var (a, t) = (Math.Max(b, h), Math.Min(b, h));
                var j = a * t * t * t * (1.0 / 3 - 0.21 * (t / a) * (1 - Math.Pow(t / a, 4) / 12));
                return new(b * h, h * b * b * b / 12, b * h * h * h / 12, h * b * b / 6, b * h * h / 6, h * b * b / 4, b * h * h / 4, j, default,
                    "closed form; J = a·b³·(1/3 − 0.21·(b/a)·(1 − b⁴/(12a⁴))) (Roark)", null, null, 0);
            }
            case "circle":
            {
                var d = b;
                var i = Math.PI * Math.Pow(d, 4) / 64;
                return new(Math.PI * d * d / 4, i, i, i / (d / 2), i / (d / 2), d * d * d / 6, d * d * d / 6, 2 * i, default, "closed form (exact)", null, null, 0);
            }
            case "CHS":
            {
                var (d, t) = (b, s.Web);
                if (!(t > 0 && 2 * t < d)) return null;
                var di = d - 2 * t;
                var i = Math.PI * (Math.Pow(d, 4) - Math.Pow(di, 4)) / 64;
                var z = (d * d * d - di * di * di) / 6;
                return new(Math.PI / 4 * (d * d - di * di), i, i, i / (d / 2), i / (d / 2), z, z, 2 * i, default, "closed form (exact)", standard, family, 0);
            }
        }
        var rings = Rings(s, r);
        if (rings == null) return null;
        var (area, sx, sy, _, _) = Integrals(rings);
        if (!(area > 0)) return null;
        var (cx, cy) = (sy / area, sx / area);
        var moved = rings.Select(ring => ring.Select(p => new Vec2(p.X - cx, p.Y - cy)).ToList()).ToList();
        var (jv, jm) = Torsion(s, r);
        var method = r > 0 ? $"polygon integrals of the nominal outline with root radius r = {r * 1000:0.#} mm ({standard}); J: {jm}" : $"polygon integrals of the outline (sharp corners); J: {jm}";
        var geometric = About(moved, area, jv, new Vec2(cx, cy), method, standard, family, r);
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
        return principal with { PrincipalDeg = phi * 180 / Math.PI, Geometric = geometric with { Ixy = ixy }, Ixy = ixy };
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
    static List<List<Vec2>>? Rings(SectionShape s, double r)
    {
        double w = s.Width / 2, d = s.Depth / 2, tw = s.Web, tf = s.Flange;
        List<Vec2> outline;
        List<List<Vec2>> holes = [];
        switch (s.Kind)
        {
            case "I": outline = [new(-w, -d), new(w, -d), new(w, -d + tf), new(tw / 2, -d + tf), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf), new(-tw / 2, -d + tf), new(-w, -d + tf)]; break;
            case "U": outline = [new(-w, -d), new(w, -d), new(w, -d + tf), new(-w + tw, -d + tf), new(-w + tw, d - tf), new(w, d - tf), new(w, d), new(-w, d)]; break;
            case "T": outline = [new(-tw / 2, -d), new(tw / 2, -d), new(tw / 2, d - tf), new(w, d - tf), new(w, d), new(-w, d), new(-w, d - tf), new(-tw / 2, d - tf)]; break;
            case "L": outline = [new(-w, -d), new(w, -d), new(w, -d + tf), new(-w + tw, -d + tf), new(-w + tw, d), new(-w, d)]; break;
            case "RHS":
            {
                var t = tw;
                if (!(t > 0 && t < w && t < d)) return null;
                outline = [new(-w, -d), new(w, -d), new(w, d), new(-w, d)];
                var hole = new List<Vec2> { new(-w + t, -d + t), new(-w + t, d - t), new(w - t, d - t), new(w - t, -d + t) };
                if (r > 0) { outline = Round(outline, _ => r); hole = Round(hole, _ => Math.Max(r - t, 0)); }
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
        // The root fillets: the re-entrant corners (a clockwise turn in a counter-clockwise outline) rounded by r; an angle's
        // toes (the inner corners of its legs' tips) by r2 = r/2 (EN 10056-1).
        if (r > 0) outline = s.Kind == "L" ? Round(outline, (i, turn) => turn < 0 ? r : i is 2 or 4 ? r / 2 : 0) : Round(outline, turn => turn < 0 ? r : 0);
        return [outline, .. holes];
    }

    /// <summary>A ring's corners rounded by the radius the turn there asks (0: left sharp), each by an arc in 16 steps a quarter.</summary>
    static List<Vec2> Round(List<Vec2> ring, Func<double, double> radiusFor) => Round(ring, (_, turn) => radiusFor(turn));

    /// <summary>A ring's corners rounded by the radius each corner (its index, its turn) asks.</summary>
    static List<Vec2> Round(List<Vec2> ring, Func<int, double, double> radiusFor)
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
            var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(turn) / (Math.PI / 2) * 16));
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
        var o = new List<Vec2>();
        switch (s.Kind)
        {
            case "C" or "SIGMA":
                if (!double.IsNaN(lip)) o.Add(new(x, -lip));
                o.AddRange([new(x, -y), new(-x, -y)]);
                if (s.Kind == "SIGMA")
                {
                    var fold = s.Width / 4;
                    o.AddRange([new(-x, -s.Depth / 4 - fold / 2), new(-x + fold, -s.Depth / 4 + fold / 2), new(-x + fold, s.Depth / 4 - fold / 2), new(-x, s.Depth / 4 + fold / 2)]);
                }
                o.AddRange([new(-x, y), new(x, y)]);
                if (!double.IsNaN(lip)) o.Add(new(x, lip));
                break;
            case "Z":
                if (!double.IsNaN(lip)) o.Add(new(-x, -lip));
                o.AddRange([new(-x, -y), new(0, -y), new(0, y), new(x, y)]);
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
