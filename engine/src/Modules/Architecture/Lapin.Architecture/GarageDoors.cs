using System.Globalization;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A garage door's settings (forum topic #48, decision 530), on a door type whose one panel is a garage door's operation
/// (<see cref="GarageDoors.Operations"/>): <see cref="Sections"/> the horizontal panels of a sectional door (null: from its
/// height, about 610 mm each), <see cref="Finish"/> the panels' face ("ribbed", "flat", "v-groove", "glazed-top": a row of
/// windows in the top panel), a pedestrian <see cref="Wicket"/> door cut into a sectional door (<see cref="WicketWidth"/>
/// wide, on the <see cref="WicketSide"/> seen from inside: "left", "centre", "right"), the <see cref="Track"/> of a sectional
/// door ("standard", "low-headroom", "high-lift" rising <see cref="Lift"/> over the opening first), the room it needs
/// (<see cref="Headroom"/> over the opening, <see cref="Backroom"/> into the garage from the wall, <see cref="Sideroom"/>
/// beside it; null: the kind's, <see cref="GarageDoors.HeadroomOf"/>), a roller door's roll box <see cref="Box"/> ("inside"
/// or "outside" of the curtain), an up-and-over door's <see cref="Mechanism"/> ("canopy": a part stays outside, open;
/// "retractable": it runs back into the garage on tracks), and whether the tracks are bodies in 3D (<see cref="Tracks"/>).
/// Sizes are indicative, as makers' catalogues give them; the user's own product sets them.
/// </summary>
public sealed record JoineryGarage(int? Sections = null, string Finish = "ribbed", bool Wicket = false, double WicketWidth = 0.9, string WicketSide = "left",
    string Track = "standard", double? Headroom = null, double? Backroom = null, double? Sideroom = null, double Lift = 0.6,
    string Box = "inside", string Mechanism = "canopy", bool Tracks = true);

/// <summary>
/// Garage doors (forum topic #48, decision 530): a door type whose one panel opens as a garage door does, its frame, opening,
/// lintel and reveal the joinery's own. Sectional overhead (horizontal panels running up and back under the ceiling on two
/// tracks), roller (a curtain of slats rolling up onto a drum in a box over the head), up-and-over (one panel tilting up,
/// canopy or retractable), side-sliding sectional (vertical panels running round the corner along a side wall). The type
/// frame is the joinery's: X along the wall, Y across (+Y the exterior), Z up; the door opens into the garage (−Y).
/// </summary>
public static class GarageDoors
{
    public static readonly string[] Operations = ["sectional-overhead", "roller", "up-and-over", "sectional-side-left", "sectional-side-right"];
    public static readonly string[] Finishes = ["ribbed", "flat", "v-groove", "glazed-top"];
    public static readonly string[] Tracks = ["standard", "low-headroom", "high-lift"];
    public static readonly string[] WicketSides = ["left", "centre", "right"];
    public static readonly string[] BoxSides = ["inside", "outside"];
    public static readonly string[] Mechanisms = ["canopy", "retractable"];

    /// <summary>
    /// Common sizes (width × height, metres), indicative: Australia's (2400, 2700, 3000, 4800, 5400 × 2100) and Europe's
    /// (2375 × 2000, 2500 × 2125, 3000 × 2125, 5000 × 2125).
    /// </summary>
    public static readonly (string Region, double Width, double Height)[] Sizes =
    [
        ("AU", 2.4, 2.1), ("AU", 2.7, 2.1), ("AU", 3.0, 2.1), ("AU", 4.8, 2.1), ("AU", 5.4, 2.1),
        ("EU", 2.375, 2.0), ("EU", 2.5, 2.125), ("EU", 3.0, 2.125), ("EU", 5.0, 2.125),
    ];

    /// <summary>A sectional panel's height the count is taken from, a roller curtain's slat pitch, the curtain's thicknesses.</summary>
    public const double PanelHeight = 0.61, SlatPitch = 0.075, SectionalThickness = 0.04, RollerThickness = 0.012, PanelThickness = 0.03;

    /// <summary>A track's section (across and up), and a sectional track's bend radius (standard; low headroom).</summary>
    public const double TrackWidth = 0.035, TrackHeight = 0.05, Radius = 0.3, LowRadius = 0.1;

    public static bool Is(string operation) => Operations.Contains(operation);

    /// <summary>The garage door's operation of a type, or null when it is none.</summary>
    public static string? KindOf(JoinerySpec s) => s.IsDoor ? s.Panels.Select(p => p.Operation).FirstOrDefault(Is) : null;

    public static bool Of(JoinerySpec s) => KindOf(s) != null;

    static JoineryGarage G(JoinerySpec s) => s.Garage ?? new JoineryGarage();

    static bool Sectional(string kind) => kind == "sectional-overhead";
    static bool Side(string kind) => kind.StartsWith("sectional-side", StringComparison.Ordinal);

    /// <summary>Why a garage door cannot be built, or null.</summary>
    public static string? Problem(JoinerySpec s)
    {
        var kind = KindOf(s)!;
        var g = G(s);
        if (s.Columns.Length != 1 || s.Rows.Length != 1 || s.Panels.Length != 1)
            return "A garage door fills its whole opening: one column, one row and its one panel.";
        if (s.Frameless != null || s.Bay != null || JoineryShapes.Shaped(s)) return "A garage door is a framed rectangle: no frameless glass, no bay, no shaped head.";
        if (s.Width is < 0.6 or > 12 || s.Height is < 1.5 or > 7) return "A garage door is 0.6 to 12 m wide and 1.5 to 7 m high.";
        if (!Finishes.Contains(g.Finish)) return $"„{g.Finish}” is not a garage door's finish ({string.Join(", ", Finishes)}).";
        if (!Tracks.Contains(g.Track)) return $"„{g.Track}” is not a sectional door's track ({string.Join(", ", Tracks)}).";
        if (!BoxSides.Contains(g.Box)) return "A roller door's box is inside or outside its curtain.";
        if (!Mechanisms.Contains(g.Mechanism)) return "An up-and-over door is a canopy or a retractable one.";
        if (!WicketSides.Contains(g.WicketSide)) return "A wicket door stands on the left, in the centre or on the right.";
        if (g.Sections is { } n && n is < 2 or > 16) return "A sectional door has 2 to 16 panels.";
        if (g.Headroom is { } hr && (!double.IsFinite(hr) || hr is < 0 or > 4)) return "The headroom is 0 to 4 m.";
        if (g.Backroom is { } br && (!double.IsFinite(br) || br is < 0 or > 15)) return "The backroom is 0 to 15 m.";
        if (g.Sideroom is { } sr && (!double.IsFinite(sr) || sr is < 0 or > 2)) return "The sideroom is 0 to 2 m.";
        if (g.Track == "high-lift" && (!double.IsFinite(g.Lift) || g.Lift <= 0 || g.Lift >= s.Height * 2)) return "A high-lift track rises over 0 m, and less than twice the door's height.";
        var r = Curtain(s);
        if (r.X1 - r.X0 < 0.4 || r.Z1 - r.Z0 < 0.8) return "The frame leaves no room for the door.";
        if (Sectional(kind) && g.Wicket)
        {
            if (!double.IsFinite(g.WicketWidth) || g.WicketWidth is < 0.6 or > 1.2) return "A wicket door is 600 to 1200 mm wide.";
            if (r.X1 - r.X0 < g.WicketWidth + 0.6) return "The door is too narrow for its wicket door.";
            if (r.Z1 - r.Z0 < 1.9) return "A wicket door needs a door 1.9 m high at least.";
        }
        return null;
    }

    /// <summary>The panels of a sectional door (horizontal) or a side-sliding one (vertical): the type's, else one per 610 mm (550 mm across).</summary>
    public static int SectionCount(JoinerySpec s)
    {
        var r = Curtain(s);
        if (G(s).Sections is { } n) return n;
        return Side(KindOf(s) ?? "") ? Math.Clamp((int)Math.Ceiling((r.X1 - r.X0) / 0.55 - 1e-9), 2, 16) : Math.Clamp((int)Math.Ceiling((r.Z1 - r.Z0) / PanelHeight - 1e-9), 2, 16);
    }

    /// <summary>The room over the opening a door needs: its own, else the kind's (indicative).</summary>
    public static double HeadroomOf(JoinerySpec s)
    {
        var g = G(s);
        if (g.Headroom is { } h) return h;
        return KindOf(s) switch
        {
            "sectional-overhead" => g.Track switch { "low-headroom" => 0.15, "high-lift" => g.Lift + 0.3, _ => 0.3 },
            // The roll's diameter grows with the curtain: about 300 mm and 60 mm for each metre of height.
            "roller" => Math.Round(0.3 + 0.06 * s.Height, 2),
            "up-and-over" => g.Mechanism == "retractable" ? 0.1 : 0.05,
            _ => 0.1,
        };
    }

    /// <summary>The room into the garage a door needs, from the wall's face: its own, else the kind's (indicative).</summary>
    public static double BackroomOf(JoinerySpec s)
    {
        var g = G(s);
        if (g.Backroom is { } b) return b;
        var r = Curtain(s);
        // As makers give it: from the door's height (the opening's); a sectional door's tracks 500 mm more.
        var h = s.Height;
        return KindOf(s) switch
        {
            "sectional-overhead" => Math.Round((g.Track == "high-lift" ? Math.Max(h - g.Lift, 0.5) : h) + 0.5, 3),
            "roller" => HeadroomOf(s),
            "up-and-over" => Math.Round(g.Mechanism == "retractable" ? h + 0.2 : h * 2 / 3, 3),
            _ => Math.Round(r.X1 - r.X0 + SideroomOf(s), 3),
        };
    }

    /// <summary>The room beside the opening a door needs (its tracks or guides; a side-sliding door's bend): its own, else the kind's.</summary>
    public static double SideroomOf(JoinerySpec s)
    {
        var g = G(s);
        if (g.Sideroom is { } sr) return sr;
        return KindOf(s) switch { "sectional-overhead" => 0.1, "roller" => 0.08, "up-and-over" => 0.05, _ => 0.3 };
    }

    /// <summary>The curtain's (or panel's) rectangle in the type frame: behind the frame (sectional, side-sliding), in it (roller, up-and-over).</summary>
    public static CellRect Curtain(JoinerySpec s)
    {
        var inner = Joinery.InnerRect(s);
        var lap = s.Frame.Width / 2;
        return KindOf(s) switch
        {
            "sectional-overhead" or "sectional-side-left" or "sectional-side-right" => new CellRect(inner.X0 - lap, inner.X1 + lap, inner.Z0, inner.Z1 + lap),
            "up-and-over" => new CellRect(inner.X0 + 0.005, inner.X1 - 0.005, inner.Z0 + 0.01, inner.Z1 - 0.005),
            _ => inner,
        };
    }

    /// <summary>The curtain's faces across the wall: room side, outside.</summary>
    public static (double Y0, double Y1) Plane(JoinerySpec s)
    {
        var f = s.Frame;
        var y0 = f.Offset - f.Depth / 2;
        return KindOf(s) switch
        {
            "roller" => (f.Offset - RollerThickness / 2, f.Offset + RollerThickness / 2),
            "up-and-over" => (f.Offset - PanelThickness / 2, f.Offset + PanelThickness / 2),
            _ => (y0 - SectionalThickness, y0),
        };
    }

    /// <summary>The frame's interior face, which the backroom is measured from.</summary>
    static double Inside(JoinerySpec s) => s.Frame.Offset - s.Frame.Depth / 2;

    /// <summary>A sectional door's panels' heights, bottom up (z0, z1).</summary>
    public static List<(double Z0, double Z1)> SectionBands(JoinerySpec s)
    {
        var r = Curtain(s);
        var n = SectionCount(s);
        var h = (r.Z1 - r.Z0) / n;
        return [.. Enumerable.Range(0, n).Select(i => (r.Z0 + i * h, r.Z0 + (i + 1) * h))];
    }

    /// <summary>A side-sliding door's panels' widths, left to right (x0, x1).</summary>
    public static List<(double X0, double X1)> SideBands(JoinerySpec s)
    {
        var r = Curtain(s);
        var n = SectionCount(s);
        var w = (r.X1 - r.X0) / n;
        return [.. Enumerable.Range(0, n).Select(i => (r.X0 + i * w, r.X0 + (i + 1) * w))];
    }

    /// <summary>A sectional door's wicket door: its rectangle in the curtain, or null.</summary>
    public static CellRect? WicketRect(JoinerySpec s)
    {
        var g = G(s);
        if (!g.Wicket || KindOf(s) != "sectional-overhead") return null;
        var r = Curtain(s);
        // Seen from inside (the type frame's +Y is the wall's right, so standing inside looking out +X is on the left).
        var x0 = g.WicketSide switch
        {
            "centre" => -g.WicketWidth / 2 + (r.X0 + r.X1) / 2,
            "right" => r.X0 + 0.3,
            _ => r.X1 - 0.3 - g.WicketWidth,
        };
        return new CellRect(x0, x0 + g.WicketWidth, r.Z0 + 0.1, Math.Min(r.Z1 - 0.2, r.Z0 + 2.05));
    }

    /// <summary>A sectional door's track: where its horizontal run's middle stands, its bend radius, the top of its vertical run.</summary>
    public static (double Zh, double R, double Zv, double Yt) TrackLine(JoinerySpec s)
    {
        var g = G(s);
        var (yc0, _) = Plane(s);
        var zh = s.Height + HeadroomOf(s) - 0.06;
        var r = g.Track == "low-headroom" ? LowRadius : Radius;
        r = Math.Max(0.05, Math.Min(r, zh - 0.3));
        return (zh, r, zh - r, yc0 - TrackWidth / 2 - 0.005);
    }

    // ─── Body ─────────────────────────────────────────────────────────────

    static JoineryBox B(string role, double x0, double x1, double y0, double y1, double z0, double z1) =>
        new(role, new Vec3(Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1)), new Vec3(Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1)));

    /// <summary>The body: the frame round the opening (no bottom member), the curtain or panel with its finish, the tracks, the roll box.</summary>
    public static List<JoineryBox> Boxes(JoinerySpec s)
    {
        var f = s.Frame;
        double y0 = f.Offset - f.Depth / 2, y1 = f.Offset + f.Depth / 2;
        var inner = Joinery.InnerRect(s);
        var o = new List<JoineryBox>
        {
            B("frame", -s.Width / 2, inner.X0, y0, y1, 0, s.Height),
            B("frame", inner.X1, s.Width / 2, y0, y1, 0, s.Height),
            B("frame", inner.X0, inner.X1, y0, y1, inner.Z1, s.Height),
        };
        if (inner.Z0 > 1e-6) o.Add(B("frame", inner.X0, inner.X1, y0, y1, 0, inner.Z0));
        switch (KindOf(s))
        {
            case "sectional-overhead": Sectional(s, o); break;
            case "roller": Roller(s, o); break;
            case "up-and-over": UpAndOver(s, o); break;
            default: SideSliding(s, o); break;
        }
        return o.Where(b => b.Max.X - b.Min.X > 1e-6 && b.Max.Y - b.Min.Y > 1e-6 && b.Max.Z - b.Min.Z > 1e-6).ToList();
    }

    /// <summary>A panel's face: ribs, grooved boards, or plain; <paramref name="across"/>: the ribs run up (an up-and-over door's), else along.</summary>
    static IEnumerable<JoineryBox> Face(string finish, double x0, double x1, double z0, double z1, double yOut, bool across)
    {
        if (finish == "ribbed")
        {
            if (across)
            {
                var n = Math.Max(2, (int)Math.Round((x1 - x0) / 0.1));
                for (var i = 1; i < n; i++) { var x = x0 + (x1 - x0) * i / n; yield return B("leaf", x - 0.01, x + 0.01, yOut, yOut + 0.005, z0 + 0.02, z1 - 0.02); }
            }
            else
                for (var k = 1; k <= 3; k++) { var z = z0 + (z1 - z0) * k / 4; yield return B("leaf", x0 + 0.02, x1 - 0.02, yOut, yOut + 0.005, z - 0.01, z + 0.01); }
        }
        else if (finish == "v-groove")
        {
            // Boards on the outer face with a groove between each two.
            const double gap = 0.004;
            if (across)
            {
                var n = Math.Max(1, (int)Math.Round((x1 - x0) / 0.1));
                for (var i = 0; i < n; i++) yield return B("leaf", x0 + (x1 - x0) * i / n + (i > 0 ? gap / 2 : 0), x0 + (x1 - x0) * (i + 1) / n - (i < n - 1 ? gap / 2 : 0), yOut, yOut + 0.004, z0, z1);
            }
            else
            {
                var n = Math.Max(1, (int)Math.Round((z1 - z0) / 0.1));
                for (var i = 0; i < n; i++) yield return B("leaf", x0, x1, yOut, yOut + 0.004, z0 + (z1 - z0) * i / n + (i > 0 ? gap / 2 : 0), z0 + (z1 - z0) * (i + 1) / n - (i < n - 1 ? gap / 2 : 0));
            }
        }
    }

    /// <summary>A band of windows across a panel (a glazed top panel): rails and stiles of the leaf, the glass between.</summary>
    static IEnumerable<JoineryBox> Windows(double x0, double x1, double z0, double z1, double ya, double yb)
    {
        const double rail = 0.08, stile = 0.1;
        var n = Math.Max(2, (int)Math.Round((x1 - x0) / 0.7));
        var w = (x1 - x0 - (n + 1) * stile) / n;
        if (w < 0.1 || z1 - z0 < 2 * rail + 0.1) { yield return B("leaf", x0, x1, ya, yb, z0, z1); yield break; }
        yield return B("leaf", x0, x1, ya, yb, z0, z0 + rail);
        yield return B("leaf", x0, x1, ya, yb, z1 - rail, z1);
        var mid = (ya + yb) / 2;
        for (var i = 0; i <= n; i++)
        {
            var xs = x0 + i * (w + stile);
            yield return B("leaf", xs, xs + stile, ya, yb, z0 + rail, z1 - rail);
            if (i < n) yield return B("glass", xs + stile, xs + stile + w, mid - 0.01, mid + 0.01, z0 + rail, z1 - rail);
        }
    }

    static void Sectional(JoinerySpec s, List<JoineryBox> o)
    {
        var g = G(s);
        var r = Curtain(s);
        var (yc0, yc1) = Plane(s);
        var bands = SectionBands(s);
        var wicket = WicketRect(s);
        const double joint = 0.002;
        for (var i = 0; i < bands.Count; i++)
        {
            var (z0, z1) = (bands[i].Z0 + (i > 0 ? joint : 0), bands[i].Z1 - (i < bands.Count - 1 ? joint : 0));
            var top = i == bands.Count - 1;
            if (g.Finish == "glazed-top" && top) { o.AddRange(Windows(r.X0, r.X1, z0, z1, yc0, yc1)); continue; }
            o.Add(B("leaf", r.X0, r.X1, yc0, g.Finish == "v-groove" ? yc1 - 0.004 : yc1, z0, z1));
            o.AddRange(Face(g.Finish, r.X0, r.X1, z0, z1, g.Finish == "v-groove" ? yc1 - 0.004 : yc1, false));
        }
        if (wicket is { } w)
        {
            // The wicket's frame on the outer face, its handle both sides.
            const double fw = 0.04, proud = 0.008;
            var yo = yc1 + (g.Finish == "ribbed" ? 0.005 : 0);
            o.Add(B("frame", w.X0, w.X0 + fw, yo, yo + proud, w.Z0, w.Z1));
            o.Add(B("frame", w.X1 - fw, w.X1, yo, yo + proud, w.Z0, w.Z1));
            o.Add(B("frame", w.X0 + fw, w.X1 - fw, yo, yo + proud, w.Z0, w.Z0 + fw));
            o.Add(B("frame", w.X0 + fw, w.X1 - fw, yo, yo + proud, w.Z1 - fw, w.Z1));
            o.AddRange(Joinery.HandleBoxes(s, w, "swing-left", yc0, -1, true));
            o.AddRange(Joinery.HandleBoxes(s, w, "swing-left", yo + proud, +1, true));
        }
        else
        {
            // A lift handle on the bottom panel, inside and out.
            var zh = bands[0].Z0 + Math.Min(0.3, (bands[0].Z1 - bands[0].Z0) / 2);
            var xm = (r.X0 + r.X1) / 2;
            o.Add(B("handle", xm - 0.07, xm + 0.07, yc0 - 0.03, yc0, zh - 0.015, zh + 0.015));
        }
        if (!g.Tracks) return;
        // Two tracks: up beside the curtain, the bend, back under the ceiling to the backroom; a cross angle at their end.
        var (zh2, rad, zv, yt) = TrackLine(s);
        var yEnd = Math.Min(Inside(s) - BackroomOf(s), yt - rad - 0.05);
        foreach (var (xa, xb) in new[] { (r.X0, r.X0 + TrackWidth), (r.X1 - TrackWidth, r.X1) })
        {
            o.Add(B("hardware", xa, xb, yt - TrackWidth / 2, yt + TrackWidth / 2, 0, zv));
            o.AddRange(Bend(xa, xb, yt, zv, rad));
            o.Add(B("hardware", xa, xb, yEnd, yt - rad, zh2 - TrackHeight / 2, zh2 + TrackHeight / 2));
        }
        o.Add(B("hardware", r.X0 + TrackWidth, r.X1 - TrackWidth, yEnd, yEnd + 0.04, zh2 - 0.02, zh2 + 0.02));
    }

    /// <summary>A track's bend from the vertical (at <paramref name="yt"/>, up to <paramref name="zv"/>) to the horizontal running into the room, in four short pieces.</summary>
    static IEnumerable<JoineryBox> Bend(double xa, double xb, double yt, double zv, double rad)
    {
        const int n = 4;
        var half = TrackWidth / 2;
        for (var i = 0; i < n; i++)
        {
            double A(int k) => Math.PI / 2 * k / n;
            var (ya, za) = (yt - rad + rad * Math.Cos(A(i)), zv + rad * Math.Sin(A(i)));
            var (yb, zb) = (yt - rad + rad * Math.Cos(A(i + 1)), zv + rad * Math.Sin(A(i + 1)));
            yield return B("hardware", xa, xb, Math.Min(ya, yb) - half, Math.Max(ya, yb) + half, Math.Min(za, zb) - half, Math.Max(za, zb) + half);
        }
    }

    /// <summary>A roller door's roll box over the head: its extent across the wall and up.</summary>
    public static (double Y0, double Y1, double Z0, double Z1) RollBox(JoinerySpec s)
    {
        var d = HeadroomOf(s);
        var yc = s.Frame.Offset;
        return G(s).Box == "outside" ? (yc - 0.03, yc - 0.03 + d, s.Height, s.Height + d) : (yc + 0.03 - d, yc + 0.03, s.Height, s.Height + d);
    }

    static void Roller(JoinerySpec s, List<JoineryBox> o)
    {
        var r = Curtain(s);
        var (yc0, yc1) = Plane(s);
        // The curtain of slats in the guides (the jambs), each slat's bead on the outer face, the bottom rail.
        o.Add(B("leaf", r.X0 - 0.01, r.X1 + 0.01, yc0, yc1, r.Z0 + 0.05, r.Z1 + s.Frame.Width));
        for (var z = r.Z0 + 0.05 + SlatPitch; z < r.Z1 - 0.01; z += SlatPitch) o.Add(B("leaf", r.X0, r.X1, yc1, yc1 + 0.004, z - 0.006, z + 0.006));
        o.Add(B("hardware", r.X0, r.X1, yc0 - 0.01, yc1 + 0.01, r.Z0, r.Z0 + 0.05));
        var (by0, by1, bz0, bz1) = RollBox(s);
        o.Add(B("frame", -s.Width / 2, s.Width / 2, by0, by1, bz0, bz1));
    }

    static void UpAndOver(JoinerySpec s, List<JoineryBox> o)
    {
        var g = G(s);
        var r = Curtain(s);
        var (yc0, yc1) = Plane(s);
        if (g.Finish == "glazed-top")
        {
            var band = Math.Min(0.4, (r.Z1 - r.Z0) / 4);
            o.Add(B("leaf", r.X0, r.X1, yc0, yc1, r.Z0, r.Z1 - band));
            o.AddRange(Windows(r.X0, r.X1, r.Z1 - band, r.Z1, yc0, yc1));
        }
        else
        {
            o.Add(B("leaf", r.X0, r.X1, yc0, g.Finish == "v-groove" ? yc1 - 0.004 : yc1, r.Z0, r.Z1));
            o.AddRange(Face(g.Finish, r.X0, r.X1, r.Z0, r.Z1, g.Finish == "v-groove" ? yc1 - 0.004 : yc1, true));
        }
        // The T handle in the middle, both faces.
        var zh = r.Z0 + Math.Min(1.0, (r.Z1 - r.Z0) / 2);
        o.Add(B("handle", -0.07, 0.07, yc0 - 0.03, yc0, zh - 0.015, zh + 0.015));
        o.Add(B("handle", -0.07, 0.07, yc1, yc1 + 0.03, zh - 0.015, zh + 0.015));
        if (!g.Tracks) return;
        var inside = Inside(s);
        if (g.Mechanism == "retractable")
        {
            // Horizontal tracks under the ceiling from the head back to the backroom, and the springs beside the jambs.
            var zt = s.Height + Math.Max(0.06, HeadroomOf(s) - 0.02);
            foreach (var (xa, xb) in new[] { (r.X0, r.X0 + TrackWidth), (r.X1 - TrackWidth, r.X1) })
                o.Add(B("hardware", xa, xb, inside - BackroomOf(s), yc0, zt - TrackHeight, zt));
        }
        else
            // The pivot arms and springs of a canopy door, each side.
            foreach (var (xa, xb) in new[] { (r.X0, r.X0 + 0.03), (r.X1 - 0.03, r.X1) })
            {
                var za = r.Z0 + (r.Z1 - r.Z0) * 0.55;
                o.Add(B("hardware", xa, xb, yc0 - 0.35, yc0, za - 0.02, za + 0.02));
                o.Add(B("hardware", xa, xb, yc0 - 0.05, yc0 - 0.02, r.Z0 + 0.1, za - 0.02));
            }
    }

    static void SideSliding(JoinerySpec s, List<JoineryBox> o)
    {
        var g = G(s);
        var r = Curtain(s);
        var (yc0, yc1) = Plane(s);
        var bands = SideBands(s);
        const double joint = 0.002;
        for (var i = 0; i < bands.Count; i++)
        {
            var (x0, x1) = (bands[i].X0 + (i > 0 ? joint : 0), bands[i].X1 - (i < bands.Count - 1 ? joint : 0));
            if (g.Finish == "glazed-top")
            {
                var band = Math.Min(0.4, (r.Z1 - r.Z0) / 4);
                o.Add(B("leaf", x0, x1, yc0, yc1, r.Z0, r.Z1 - band));
                o.AddRange(Windows(x0, x1, r.Z1 - band, r.Z1, yc0, yc1));
                continue;
            }
            o.Add(B("leaf", x0, x1, yc0, g.Finish == "v-groove" ? yc1 - 0.004 : yc1, r.Z0, r.Z1));
            o.AddRange(Face(g.Finish, x0, x1, r.Z0, r.Z1, g.Finish == "v-groove" ? yc1 - 0.004 : yc1, false));
        }
        // The handle on the panel that leads, by the far side from where it parks.
        var left = KindOf(s) == "sectional-side-left";
        var xh = left ? r.X1 - 0.12 : r.X0 + 0.12;
        o.Add(B("handle", xh - 0.015, xh + 0.015, yc0 - 0.03, yc0, 0.95, 1.15));
        if (!g.Tracks) return;
        // The top track across the opening, its bend and its run along the side wall to the backroom.
        var (zt0, zt1) = (r.Z1, r.Z1 + TrackHeight);
        var yt = yc0 - TrackWidth / 2 - 0.005;
        var rad = Math.Max(0.05, SideroomOf(s));
        var xEnd = left ? r.X0 : r.X1;
        var sg = left ? -1 : 1;
        o.Add(B("hardware", r.X0, r.X1, yt - TrackWidth / 2, yt + TrackWidth / 2, zt0, zt1));
        const int n = 4;
        for (var i = 0; i < n; i++)
        {
            double A(int k) => Math.PI / 2 * k / n;
            var (xa, ya) = (xEnd + sg * rad * Math.Sin(A(i)), yt - rad + rad * Math.Cos(A(i)));
            var (xb, yb) = (xEnd + sg * rad * Math.Sin(A(i + 1)), yt - rad + rad * Math.Cos(A(i + 1)));
            o.Add(B("hardware", Math.Min(xa, xb) - TrackWidth / 2, Math.Max(xa, xb) + TrackWidth / 2, Math.Min(ya, yb) - TrackWidth / 2, Math.Max(ya, yb) + TrackWidth / 2, zt0, zt1));
        }
        var xr = xEnd + sg * rad;
        o.Add(B("hardware", xr - TrackWidth / 2, xr + TrackWidth / 2, Math.Min(Inside(s) - BackroomOf(s), yt - rad - 0.05), yt - rad, zt0, zt1));
    }

    // ─── Drawings ─────────────────────────────────────────────────────────

    static JStroke Rect(double x0, double y0, double x1, double y1, string w, bool dashed = false) =>
        new([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)], w, true, dashed);
    static JStroke Line(Vec2 a, Vec2 b, string w, bool dashed = false) => new([a, b], w, false, dashed);

    /// <summary>
    /// The plan's garage door (the frame is drawn with the other cut boxes): the curtain at the opening, cut (a line at LOD 100), and
    /// what is over the cut dashed as CAD libraries draw it: a sectional door's two tracks running back to the backroom and the door
    /// open under the ceiling, a roller's box, an up-and-over door's open panel, a side-sliding door's track round the corner along
    /// the side wall and the door parked there.
    /// </summary>
    public static List<JStroke> Plan(JoinerySpec s, double z, int level)
    {
        var o = new List<JStroke>();
        var kind = KindOf(s)!;
        var g = G(s);
        var r = Curtain(s);
        var (yc0, yc1) = Plane(s);
        var ym = (yc0 + yc1) / 2;
        o.Add(level <= PlanDetail.Coarse ? Line(new(r.X0, ym), new(r.X1, ym), "cut") : Rect(r.X0, yc0, r.X1, yc1, "cut"));
        var inside = Inside(s);
        var back = inside - BackroomOf(s);
        var hc = r.Z1 - r.Z0;
        switch (kind)
        {
            case "sectional-overhead":
            {
                var (_, rad, _, yt) = TrackLine(s);
                var yEnd = Math.Min(back, yt - rad - 0.05);
                // The tracks cut beside the curtain at fine detail.
                if (level >= PlanDetail.Fine && g.Tracks)
                    foreach (var xa in new[] { r.X0, r.X1 - TrackWidth }) o.Add(Rect(xa, yt - TrackWidth / 2, xa + TrackWidth, yt + TrackWidth / 2, "outline"));
                foreach (var x in new[] { r.X0 + TrackWidth / 2, r.X1 - TrackWidth / 2 }) o.Add(Line(new(x, yc0), new(x, yEnd), "thin", true));
                o.Add(Line(new(r.X0, yEnd), new(r.X1, yEnd), "thin", true));
                // The door open overhead, its far edge where its height puts it.
                o.Add(Rect(r.X0, yc0 - Math.Min(hc, yc0 - yEnd), r.X1, yc0, "thin", true));
                break;
            }
            case "roller":
            {
                var (by0, by1, _, _) = RollBox(s);
                o.Add(Rect(-s.Width / 2, by0, s.Width / 2, by1, "thin", true));
                break;
            }
            case "up-and-over":
            {
                // Open: a canopy door a third outside, two thirds in; a retractable one all inside, on its tracks to the backroom.
                if (g.Mechanism == "retractable")
                {
                    o.Add(Rect(r.X0, yc0 - hc, r.X1, yc0, "thin", true));
                    foreach (var x in new[] { r.X0 + TrackWidth / 2, r.X1 - TrackWidth / 2 }) o.Add(Line(new(x, yc0), new(x, back), "thin", true));
                }
                else o.Add(Rect(r.X0, ym - hc * 2 / 3, r.X1, ym + hc / 3, "thin", true));
                break;
            }
            default:
            {
                var left = kind == "sectional-side-left";
                var sg = left ? -1 : 1;
                var xEnd = left ? r.X0 : r.X1;
                var rad = Math.Max(0.05, SideroomOf(s));
                var yt = yc0 - TrackWidth / 2 - 0.005;
                var yEnd = Math.Min(back, yt - rad - 0.05);
                var arc = Enumerable.Range(0, 9).Select(i => { var a = Math.PI / 2 * i / 8; return new Vec2(xEnd + sg * rad * Math.Sin(a), yt - rad + rad * Math.Cos(a)); }).ToList();
                o.Add(new JStroke([new(left ? r.X1 : r.X0, yt), .. arc, new(xEnd + sg * rad, yEnd)], "thin", false, true));
                // The door parked along the side wall.
                var xr = xEnd + sg * rad;
                o.Add(Rect(xr, Math.Max(yEnd, yt - rad - (r.X1 - r.X0)), xr + sg * SectionalThickness, yt - rad, "thin", true));
                // The way it runs.
                var xm = (r.X0 + r.X1) / 2;
                var len = (r.X1 - r.X0) / 4;
                var ya = yc0 - 0.12;
                var tip = new Vec2(xm + sg * len, ya);
                o.Add(Line(new(xm - sg * len, ya), tip, "thin"));
                o.Add(new([new(tip.X - sg * 0.08, ya + 0.04), tip, new(tip.X - sg * 0.08, ya - 0.04)], "thin"));
                break;
            }
        }
        return o;
    }

    /// <summary>
    /// The elevation's lines in the curtain (seen from inside; the caller mirrors it for outside): a sectional door's panel joints and
    /// face, its windows and wicket; a roller's slats and box; an up-and-over door's face; a side-sliding door's joints and its way.
    /// <paramref name="exterior"/>: what is on the room's side (a roll box inside) is behind the wall, dashed.
    /// </summary>
    public static List<JStroke> Elevation(JoinerySpec s, bool exterior)
    {
        var o = new List<JStroke>();
        var kind = KindOf(s)!;
        var g = G(s);
        var r = Curtain(s);
        void FaceLines(double x0, double x1, double z0, double z1, bool across)
        {
            if (g.Finish == "ribbed" || g.Finish == "v-groove")
            {
                var step = g.Finish == "ribbed" ? (across ? 0.1 : (z1 - z0) / 4) : 0.1;
                if (across) { var n = Math.Max(2, (int)Math.Round((x1 - x0) / step)); for (var i = 1; i < n; i++) { var x = x0 + (x1 - x0) * i / n; o.Add(Line(new(x, z0), new(x, z1), "thin")); } }
                else { var n = Math.Max(2, (int)Math.Round((z1 - z0) / step)); for (var i = 1; i < n; i++) { var z = z0 + (z1 - z0) * i / n; o.Add(Line(new(x0, z), new(x1, z), "thin")); } }
            }
        }
        void WindowLines(double x0, double x1, double z0, double z1)
        {
            foreach (var b in Windows(x0, x1, z0, z1, 0, 1).Where(b => b.Role == "glass")) o.Add(Rect(b.Min.X, b.Min.Z, b.Max.X, b.Max.Z, "thin"));
        }
        switch (kind)
        {
            case "sectional-overhead":
            {
                var bands = SectionBands(s);
                for (var i = 0; i < bands.Count; i++)
                {
                    if (i > 0) o.Add(Line(new(r.X0, bands[i].Z0), new(r.X1, bands[i].Z0), "outline"));
                    if (g.Finish == "glazed-top" && i == bands.Count - 1) WindowLines(r.X0, r.X1, bands[i].Z0, bands[i].Z1);
                    else FaceLines(r.X0, r.X1, bands[i].Z0, bands[i].Z1, false);
                }
                if (WicketRect(s) is { } w) o.Add(Rect(w.X0, w.Z0, w.X1, w.Z1, "outline"));
                break;
            }
            case "roller":
            {
                for (var z = r.Z0 + 0.05 + 2 * SlatPitch; z < r.Z1 - 0.01; z += 2 * SlatPitch) o.Add(Line(new(r.X0, z), new(r.X1, z), "thin"));
                o.Add(Line(new(r.X0, r.Z0 + 0.05), new(r.X1, r.Z0 + 0.05), "outline"));
                var (_, _, bz0, bz1) = RollBox(s);
                o.Add(Rect(-s.Width / 2, bz0, s.Width / 2, bz1, "thin", exterior == (g.Box == "inside")));
                break;
            }
            case "up-and-over":
                if (g.Finish == "glazed-top") { var band = Math.Min(0.4, (r.Z1 - r.Z0) / 4); WindowLines(r.X0, r.X1, r.Z1 - band, r.Z1); }
                else FaceLines(r.X0, r.X1, r.Z0, r.Z1, true);
                break;
            default:
            {
                var bands = SideBands(s);
                for (var i = 1; i < bands.Count; i++) o.Add(Line(new(bands[i].X0, r.Z0), new(bands[i].X0, r.Z1), "outline"));
                // Its way, seen from inside (x as the type's): left runs to −X.
                var sg = kind == "sectional-side-left" ? -1 : 1;
                var (xm, zm, len) = ((r.X0 + r.X1) / 2, (r.Z0 + r.Z1) / 2, (r.X1 - r.X0) / 4);
                var tip = new Vec2(xm + sg * len, zm);
                o.Add(Line(new(xm - sg * len, zm), tip, "thin"));
                o.Add(new([new(tip.X - sg * 0.08, zm + 0.04), tip, new(tip.X - sg * 0.08, zm - 0.04)], "thin"));
                break;
            }
        }
        return o;
    }

    // ─── Schedules, quantities, IFC ───────────────────────────────────────

    /// <summary>The schedule's Opening words for a garage door's operation.</summary>
    public static string Label(string op) => op switch
    {
        "sectional-overhead" => "sectional overhead",
        "roller" => "roller",
        "up-and-over" => "up-and-over",
        "sectional-side-left" => "side-sliding sectional, to the left",
        "sectional-side-right" => "side-sliding sectional, to the right",
        _ => op,
    };

    static string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>The schedule's Hanging row for a garage door: its panels, finish, track or box, wicket, and the room it needs.</summary>
    public static string Describe(JoinerySpec s)
    {
        var g = G(s);
        var kind = KindOf(s)!;
        var parts = new List<string>();
        if (Sectional(kind) || Side(kind)) parts.Add($"{SectionCount(s)} panels");
        if (kind != "roller") parts.Add(g.Finish switch { "glazed-top" => "glazed top", "v-groove" => "V-groove", _ => g.Finish });
        if (Sectional(kind)) parts.Add(g.Track switch { "low-headroom" => "low-headroom track", "high-lift" => $"high-lift track (+{Mm(g.Lift)} mm)", _ => "standard track" });
        if (kind == "roller") parts.Add(g.Box == "outside" ? "box outside" : "box inside");
        if (kind == "up-and-over") parts.Add(g.Mechanism);
        if (WicketRect(s) != null) parts.Add($"wicket door {Mm(g.WicketWidth)} mm");
        parts.Add($"headroom {Mm(HeadroomOf(s))} mm");
        parts.Add($"backroom {Mm(BackroomOf(s))} mm");
        parts.Add($"sideroom {Mm(SideroomOf(s))} mm");
        return string.Join(", ", parts);
    }

    /// <summary>A garage door's own measures (quantities, Lapin_GarageDoor): the curtain's area, its panels, the room it needs.</summary>
    public static IEnumerable<(string Name, double Value, string Unit)> Quantities(JoinerySpec s)
    {
        var r = Curtain(s);
        var kind = KindOf(s)!;
        yield return ("CurtainArea", (r.X1 - r.X0) * (r.Z1 - r.Z0), "AREA");
        if (Sectional(kind) || Side(kind)) yield return ("Panels", SectionCount(s), "COUNT");
        yield return ("Headroom", HeadroomOf(s), "LENGTH");
        yield return ("Backroom", BackroomOf(s), "LENGTH");
        yield return ("Sideroom", SideroomOf(s), "LENGTH");
    }

    /// <summary>
    /// The door's OperationType in each schema (IfcDoorTypeOperationEnum, after buildingSMART's published IFC4 ADD2 TC1 and
    /// IFC4X3 ADD2): a roller door ROLLINGUP in both; a sectional overhead or up-and-over door IFC4X3's LIFTING_HORIZONTAL (the
    /// door lifts and lies flat overhead), USERDEFINED in IFC4 (SECTIONAL_OVERHEAD, UP_AND_OVER); a side-sliding sectional door
    /// USERDEFINED in both (SECTIONAL_SIDE_LEFT, …_RIGHT: it slides round a corner, which SLIDING_TO_… does not say).
    /// </summary>
    public static (string Ifc4, string? UserDefined, string Ifc4X3) Operation(string op) => op switch
    {
        "roller" => ("ROLLINGUP", null, "ROLLINGUP"),
        "sectional-overhead" => ("USERDEFINED", "SECTIONAL_OVERHEAD", "LIFTING_HORIZONTAL"),
        "up-and-over" => ("USERDEFINED", "UP_AND_OVER", "LIFTING_HORIZONTAL"),
        "sectional-side-left" => ("USERDEFINED", "SECTIONAL_SIDE_LEFT", "USERDEFINED"),
        _ => ("USERDEFINED", "SECTIONAL_SIDE_RIGHT", "USERDEFINED"),
    };

    /// <summary>A panel's PanelOperation (IfcDoorPanelOperationEnum, the same in both schemas): ROLLINGUP, SLIDING, else USERDEFINED.</summary>
    public static string PanelOperation(string op) => op switch { "roller" => "ROLLINGUP", "sectional-side-left" or "sectional-side-right" => "SLIDING", _ => "USERDEFINED" };

    /// <summary>The garage door an IFC file's OperationType or its user-defined text names; null for any other.</summary>
    public static string? FromIfc(string? operation, string? userDefined) => operation switch
    {
        "ROLLINGUP" => "roller",
        "LIFTING_HORIZONTAL" => "sectional-overhead",
        _ => (userDefined ?? "").Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_') switch
        {
            "SECTIONAL_OVERHEAD" or "SECTIONAL" or "OVERHEAD_SECTIONAL" => "sectional-overhead",
            "UP_AND_OVER" or "TILTING" or "UP_AND_OVER_CANOPY" or "UP_AND_OVER_RETRACTABLE" => "up-and-over",
            "SECTIONAL_SIDE_LEFT" => "sectional-side-left",
            "SECTIONAL_SIDE_RIGHT" => "sectional-side-right",
            "ROLLER" or "ROLLER_SHUTTER" or "ROLLINGUP" => "roller",
            _ => null,
        },
    };
}
