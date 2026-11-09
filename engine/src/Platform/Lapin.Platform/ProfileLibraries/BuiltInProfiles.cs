using System.Globalization;

namespace Lapin.Platform.ProfileLibraries;

/// <summary>
/// The profile library Lapin ships (docs/plan-profile-libraries.md): „EN steel (Lapin)”, the common European steel profiles
/// by their standard dimensions only — IPE, HEA, HEB and HEM (EN 10365), UPN (sloping flanges, 8 % to 300, 5 % above) and
/// UPE, equal and unequal angles (EN 10056-1, the toes r2 = r1/2), hot-finished SHS, RHS and CHS (EN 10210-2: the corners
/// 1.5 t outside and t inside) — and Lapin's generic cold-formed C, Z and Σ. Every property is worked out by Lapin from
/// these; no producer's table is in it. Everything else comes from the user's own libraries. Built when first asked.
/// </summary>
public static class BuiltInProfiles
{
    public const string Name = "EN steel (Lapin)";

    static readonly Lazy<IReadOnlyList<SectionProfile>> _all = new(Build);
    static readonly Lazy<Dictionary<string, SectionProfile>> _byName = new(() =>
    {
        var d = new Dictionary<string, SectionProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _all.Value) { d.TryAdd(p.Name, p); d.TryAdd(Key(p.Name), p); }
        return d;
    });

    public static IReadOnlyList<SectionProfile> All => _all.Value;

    /// <summary>A profile of the library by its name, spaces and × or x as typed (IPE300, HEB 240, L 100x100x10).</summary>
    public static SectionProfile? Find(string? name) => string.IsNullOrWhiteSpace(name) ? null
        : _byName.Value.GetValueOrDefault(name.Trim()) ?? _byName.Value.GetValueOrDefault(Key(name));

    static string Key(string name) => new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant().Replace('X', '×').Replace("Σ", "SIGMA");

    static string Mm(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    static IReadOnlyList<SectionProfile> Build()
    {
        var o = new List<SectionProfile>();
        const string en10365 = "EN 10365:2017", en10056 = "EN 10056-1:2017", en10210 = "EN 10210-2:2019";
        void I(string family, double h, double b, double tw, double tf, double r, int size) => o.Add(new SectionProfile
        {
            Name = $"{family} {size}", Kind = "I", Family = family, Standard = en10365, Category = "hot-rolled",
            H = h / 1000, B = b / 1000, Tw = tw / 1000, Tf = tf / 1000, R1 = r / 1000,
        });
        // IPE: h, b, tw, tf, r.
        foreach (var (n, h, b, tw, tf, r) in new (int, double, double, double, double, double)[]
        {
            (80, 80, 46, 3.8, 5.2, 5), (100, 100, 55, 4.1, 5.7, 7), (120, 120, 64, 4.4, 6.3, 7), (140, 140, 73, 4.7, 6.9, 7), (160, 160, 82, 5, 7.4, 9),
            (180, 180, 91, 5.3, 8, 9), (200, 200, 100, 5.6, 8.5, 12), (220, 220, 110, 5.9, 9.2, 12), (240, 240, 120, 6.2, 9.8, 15), (270, 270, 135, 6.6, 10.2, 15),
            (300, 300, 150, 7.1, 10.7, 15), (330, 330, 160, 7.5, 11.5, 18), (360, 360, 170, 8, 12.7, 18), (400, 400, 180, 8.6, 13.5, 21), (450, 450, 190, 9.4, 14.6, 21),
            (500, 500, 200, 10.2, 16, 21), (550, 550, 210, 11.1, 17.2, 24), (600, 600, 220, 12, 19, 24),
        }) I("IPE", h, b, tw, tf, r, n);
        foreach (var (n, h, b, tw, tf, r) in new (int, double, double, double, double, double)[]
        {
            (100, 96, 100, 5, 8, 12), (120, 114, 120, 5, 8, 12), (140, 133, 140, 5.5, 8.5, 12), (160, 152, 160, 6, 9, 15), (180, 171, 180, 6, 9.5, 15),
            (200, 190, 200, 6.5, 10, 18), (220, 210, 220, 7, 11, 18), (240, 230, 240, 7.5, 12, 21), (260, 250, 260, 7.5, 12.5, 24), (280, 270, 280, 8, 13, 24),
            (300, 290, 300, 8.5, 14, 27), (320, 310, 300, 9, 15.5, 27), (340, 330, 300, 9.5, 16.5, 27), (360, 350, 300, 10, 17.5, 27), (400, 390, 300, 11, 19, 27),
            (450, 440, 300, 11.5, 21, 27), (500, 490, 300, 12, 23, 27), (550, 540, 300, 12.5, 24, 27), (600, 590, 300, 13, 25, 27),
        }) I("HEA", h, b, tw, tf, r, n);
        foreach (var (n, h, b, tw, tf, r) in new (int, double, double, double, double, double)[]
        {
            (100, 100, 100, 6, 10, 12), (120, 120, 120, 6.5, 11, 12), (140, 140, 140, 7, 12, 12), (160, 160, 160, 8, 13, 15), (180, 180, 180, 8.5, 14, 15),
            (200, 200, 200, 9, 15, 18), (220, 220, 220, 9.5, 16, 18), (240, 240, 240, 10, 17, 21), (260, 260, 260, 10, 17.5, 24), (280, 280, 280, 10.5, 18, 24),
            (300, 300, 300, 11, 19, 27), (320, 320, 300, 11.5, 20.5, 27), (340, 340, 300, 12, 21.5, 27), (360, 360, 300, 12.5, 22.5, 27), (400, 400, 300, 13.5, 24, 27),
            (450, 450, 300, 14, 26, 27), (500, 500, 300, 14.5, 28, 27), (550, 550, 300, 15, 29, 27), (600, 600, 300, 15.5, 30, 27),
        }) I("HEB", h, b, tw, tf, r, n);
        foreach (var (n, h, b, tw, tf, r) in new (int, double, double, double, double, double)[]
        {
            (100, 120, 106, 12, 20, 12), (120, 140, 126, 12.5, 21, 12), (140, 160, 146, 13, 22, 12), (160, 180, 166, 14, 23, 15), (180, 200, 186, 14.5, 24, 15),
            (200, 220, 206, 15, 25, 18), (220, 240, 226, 15.5, 26, 18), (240, 270, 248, 18, 32, 21), (260, 290, 268, 18, 32.5, 24), (280, 310, 288, 18.5, 33, 24),
            (300, 340, 310, 21, 39, 27), (320, 359, 309, 21, 40, 27), (340, 377, 309, 21, 40, 27), (360, 395, 308, 21, 40, 27), (400, 432, 307, 21, 40, 27),
            (450, 478, 307, 21, 40, 27), (500, 524, 306, 21, 40, 27), (550, 572, 306, 21, 40, 27), (600, 620, 305, 21, 40, 27),
        }) I("HEM", h, b, tw, tf, r, n);
        // UPN: h, b, tw, tf, r1, r2; the flanges' inner faces at 8 % (5 % from UPN 320).
        foreach (var (n, b, tw, tf, r1, r2) in new (int, double, double, double, double, double)[]
        {
            (80, 45, 6, 8, 8, 4), (100, 50, 6, 8.5, 8.5, 4.5), (120, 55, 7, 9, 9, 4.5), (140, 60, 7, 10, 10, 5), (160, 65, 7.5, 10.5, 10.5, 5.5),
            (180, 70, 8, 11, 11, 5.5), (200, 75, 8.5, 11.5, 11.5, 6), (220, 80, 9, 12.5, 12.5, 6.5), (240, 85, 9.5, 13, 13, 6.5), (260, 90, 10, 14, 14, 7),
            (280, 95, 10, 15, 15, 7.5), (300, 100, 10, 16, 16, 8), (320, 100, 14, 17.5, 17.5, 8.75), (350, 100, 14, 16, 16, 8), (380, 102, 13.5, 16, 16, 8), (400, 110, 14, 18, 18, 9),
        }) o.Add(new SectionProfile
        {
            Name = $"UPN {n}", Kind = "U", Family = "UPN", Standard = en10365, Category = "hot-rolled",
            H = n / 1000.0, B = b / 1000, Tw = tw / 1000, Tf = tf / 1000, R1 = r1 / 1000, R2 = r2 / 1000, Slope = n >= 320 ? 0.05 : 0.08,
        });
        // UPE: parallel flanges, h, b, tw, tf, r.
        foreach (var (n, b, tw, tf, r) in new (int, double, double, double, double)[]
        {
            (80, 50, 4, 7, 10), (100, 55, 4.5, 7.5, 10), (120, 60, 5, 8, 12), (140, 65, 5, 9, 12), (160, 70, 5.5, 9.5, 12), (180, 75, 5.5, 10.5, 12),
            (200, 80, 6, 11, 13), (220, 85, 6.5, 12, 13), (240, 90, 7, 12.5, 15), (270, 95, 7.5, 13.5, 15), (300, 100, 9.5, 15, 15), (330, 105, 11, 16, 18),
            (360, 110, 12, 17, 18), (400, 115, 13.5, 18, 18),
        }) o.Add(new SectionProfile
        {
            Name = $"UPE {n}", Kind = "U", Family = "UPE", Standard = en10365, Category = "hot-rolled",
            H = n / 1000.0, B = b / 1000, Tw = tw / 1000, Tf = tf / 1000, R1 = r / 1000,
        });
        // Angles: legs (the first up the depth), thickness, root radius; the toes at half of it.
        foreach (var (h, b, t, r) in new (double, double, double, double)[]
        {
            (20, 20, 3, 3.5), (25, 25, 3, 3.5), (25, 25, 4, 3.5), (30, 30, 3, 5), (30, 30, 4, 5), (35, 35, 4, 5), (40, 40, 4, 6), (40, 40, 5, 6), (45, 45, 4.5, 7),
            (50, 50, 5, 7), (50, 50, 6, 7), (60, 60, 6, 8), (60, 60, 8, 8), (65, 65, 7, 9), (70, 70, 7, 9), (75, 75, 8, 9), (80, 80, 8, 10), (80, 80, 10, 10),
            (90, 90, 9, 11), (100, 100, 10, 12), (100, 100, 12, 12), (120, 120, 10, 13), (120, 120, 12, 13), (150, 150, 12, 16), (150, 150, 15, 16),
            (200, 200, 16, 18), (200, 200, 20, 18),
            (60, 40, 5, 6), (75, 50, 6, 7), (80, 40, 6, 7), (100, 50, 6, 8), (100, 65, 7, 10), (100, 75, 8, 10), (120, 80, 8, 11), (120, 80, 10, 11),
            (150, 90, 10, 12), (150, 100, 10, 12), (200, 100, 10, 15),
        }) o.Add(new SectionProfile
        {
            Name = $"L {Mm(h)}×{Mm(b)}×{Mm(t)}", Kind = "L", Family = h == b ? "L equal" : "L unequal", Standard = en10056, Category = "hot-rolled",
            H = h / 1000, B = b / 1000, Tw = t / 1000, Tf = t / 1000, R1 = r / 1000, R2 = r / 2000,
        });
        // Hot-finished hollow sections: SHS and RHS (h × b × t; RHS as the catalogue names them), CHS (D × t).
        void Hollow(double h, double b, double t) => o.Add(new SectionProfile
        {
            Name = $"{(h == b && !Rhs.Contains((h, t)) ? "SHS" : "RHS")} {Mm(h)}×{Mm(b)}×{Mm(t)}", Kind = "RHS", Family = h == b ? "SHS" : "RHS", Standard = en10210, Category = "hot-finished",
            H = h / 1000, B = b / 1000, Tw = t / 1000, Tf = t / 1000, R1 = 1.5 * t / 1000, R2 = t / 1000,
        });
        foreach (var (a, ts) in new (double, double[])[]
        {
            (40, [3, 4]), (50, [3, 4, 5]), (60, [3, 4, 5]), (70, [4, 5]), (80, [4, 5, 6]), (90, [5]), (100, [4, 5, 6, 8]), (120, [5, 6, 8]), (140, [6, 8]),
            (150, [6, 8, 10]), (160, [8]), (180, [8, 10]), (200, [8, 10]), (250, [10]), (300, [10]),
        }) foreach (var t in ts) Hollow(a, a, t);
        foreach (var (h, b, t) in new (double, double, double)[]
        {
            (60, 40, 3), (60, 40, 4), (80, 40, 4), (100, 50, 4), (100, 50, 5), (120, 60, 5), (120, 80, 5), (120, 80, 6), (150, 100, 6), (150, 100, 8),
            (160, 80, 6), (200, 100, 6), (200, 100, 8), (200, 120, 8), (250, 150, 8), (250, 150, 10), (300, 200, 10),
        }) Hollow(h, b, t);
        foreach (var (d, ts) in new (double, double[])[]
        {
            (21.3, [2.6, 3.2]), (26.9, [3.2]), (33.7, [3.2, 4]), (42.4, [3.2, 4]), (48.3, [3.2, 4, 5]), (60.3, [3.2, 4, 5]), (76.1, [3.2, 4, 5]), (88.9, [4, 5, 6.3]),
            (114.3, [4, 5, 6.3]), (139.7, [5, 6.3, 8]), (168.3, [5, 6, 6.3, 8]), (193.7, [6.3, 8]), (219.1, [6.3, 8, 10]), (273, [8, 10]), (323.9, [8, 10]),
        }) foreach (var t in ts) o.Add(new SectionProfile
        {
            Name = $"CHS {Mm(d)}×{Mm(t)}", Kind = "CHS", Family = "CHS", Standard = en10210, Category = "hot-finished",
            H = d / 1000, B = d / 1000, Tw = t / 1000, Tf = t / 1000,
        });
        // Lapin's generic cold-formed sections (decision 268), sharp bends: depth, width (a Z's both flanges), thickness, lips.
        foreach (var (kind, label, h, b, t, lip) in new (string, string, double, double, double, double)[]
        {
            ("C", "C 150×50×2", 150, 50, 2, 15), ("C", "C 200×65×2", 200, 65, 2, 20), ("Z", "Z 150×55×2", 150, 110, 2, 15), ("Z", "Z 200×65×2", 200, 130, 2, 20),
            ("SIGMA", "Σ 200×62×2", 200, 62, 2, 20),
        }) o.Add(new SectionProfile
        {
            Name = label, Kind = kind, Family = $"{(kind == "SIGMA" ? "Σ" : kind)} generic", Standard = null, Category = "cold-formed",
            H = h / 1000, B = b / 1000, Tw = t / 1000, Tf = t / 1000, Lip = lip / 1000,
        });
        return o;
    }

    /// <summary>The square hollow sections the catalogue already named RHS (decision 268): the same name, so their types stay theirs.</summary>
    static readonly HashSet<(double, double)> Rhs = [(100, 5)];
}
