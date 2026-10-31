using Lapin.Geometry;

namespace Lapin.Architecture;

/*
 * What dresses a door or window on the facade (reports #38 and #39, decision 174): shutters,
 * a safety grille, a window box, a canopy. Each is part of the type's details, like the sills
 * and the surround, and sits by the host wall's real faces (its layers' outer faces, read
 * from the wall where the window is): a shutter or a grille on the facade stands off the
 * exterior face (or off the surround, when there is one) by its gap, a grille in the reveal
 * is set back from that face. Each may be moved by hand along the wall from the window's axis
 * (Along) and up (Rise), and its gap from the wall face set.
 */

/// <summary>
/// Shutters on the facade: one or two leaves, louvred, panelled or of planks, shown open
/// (folded flat against the wall beside the opening) or closed over it. <see cref="Gap"/>:
/// from the wall's exterior face (or the surround's) to the leaves; <see cref="Overlap"/>: how
/// far each leaf runs past the jamb when closed; <see cref="Along"/>, <see cref="Rise"/>: moved
/// by hand from the window's axis and its bottom; <see cref="Height"/>: null, the opening's.
/// </summary>
public sealed record JoineryShutters(
    bool On = false, string Style = "louvred", int Leaves = 2, bool Open = true,
    double Thickness = 0.03, double Gap = 0.01, double Overlap = 0.03,
    double Along = 0, double Rise = 0, double? Height = null,
    double Pitch = 0.05, string? Material = null, string Color = "#4f6f52");

/// <summary>
/// A safety grille: bars across the opening, horizontal, vertical or both, in a flat-bar frame.
/// <see cref="Side"/>: "revealInside" (in the opening, set <see cref="Gap"/> back from the interior
/// face, never into the frame: where safety grilles usually go, report #47), "reveal" (in the
/// opening, set <see cref="Gap"/> back from the exterior face), "outside" (fixed on the facade,
/// <see cref="Gap"/> off the exterior face and <see cref="Overlap"/> past the opening each side)
/// or "inside" (on the interior wall face, past the opening likewise). <see cref="Bar"/>: the bars' section; <see cref="Spacing"/>: centre to centre.
/// </summary>
public sealed record JoineryGrille(
    bool On = false, string Side = "revealInside", string Pattern = "horizontal",
    double Bar = 0.012, double Spacing = 0.1, double FrameWidth = 0.03,
    double Gap = 0.03, double Overlap = 0.05, double Along = 0, double Rise = 0,
    string? Material = null, string Color = "#2f2f2f");

/// <summary>A window box under the window on the facade: its width (null: the opening's), depth out from the wall, height, gap from the face, and how far below the opening's bottom its top is.</summary>
public sealed record JoineryWindowBox(
    bool On = false, double? Width = null, double Depth = 0.2, double Height = 0.18,
    double Gap = 0.0, double Drop = 0.06, double Along = 0, string? Material = null, string Color = "#8a5a3c");

/// <summary>A canopy over the opening: how far it projects, its thickness, how far past the jambs it runs, how high over the head it sits, its slope (degrees, falling outwards), its gap from the face.</summary>
public sealed record JoineryCanopy(
    bool On = false, double Projection = 0.6, double Thickness = 0.06, double Overhang = 0.15,
    double Rise = 0.15, double Slope = 5, double Gap = 0.0, double Along = 0, string? Material = null, string Color = "#6b6b6b");

/// <summary>
/// One piece of a dressing in the type's frame (X along the opening, Y to the exterior, Z up
/// from its bottom): boxes, or a profile in Y–Z swept from X0 to X1 (a sloped canopy).
/// <see cref="Key"/> is the part's key ("shutters", "grille", "windowbox", "canopy").
/// </summary>
public sealed record DressingPiece(string Key, string Name, List<JoineryBox> Boxes, string? Material, string Color, (double X0, double X1, (double Y, double Z)[] Profile)? Swept = null);

/// <summary>The dressings' pieces, from a type's details and where the wall's faces are.</summary>
public static class WindowDressings
{
    /// <summary>
    /// Where the wall stands in the type's frame: its exterior and interior faces (Y), the
    /// surround's face outside it when there is one, and the frame's exterior face. Dressings on
    /// the facade stand off <see cref="Facade"/>; one in the reveal is set back from <see cref="Out"/>.
    /// </summary>
    public readonly record struct Faces(double Out, double In, double Facade, double FrameOut, double FrameIn);

    /// <summary>The faces of a wall <paramref name="thickness"/> thick (its outermost layers' faces) for a spec.</summary>
    public static Faces Of(JoinerySpec s, double thickness)
    {
        var d = s.Details ?? new JoineryDetails();
        var surround = d.Surround is { On: true } sur ? Math.Max(0.005, sur.Projection) : 0;
        return new Faces(thickness / 2, -thickness / 2, thickness / 2 + surround, s.Frame.Offset + s.Frame.Depth / 2, s.Frame.Offset - s.Frame.Depth / 2);
    }

    static JoineryBox B(string role, double x0, double x1, double y0, double y1, double z0, double z1) =>
        new(role, new Vec3(Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1)), new Vec3(Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1)));

    public static List<DressingPiece> Pieces(JoinerySpec s, Faces f)
    {
        var d = s.Details ?? new JoineryDetails();
        var o = new List<DressingPiece>();
        if (d.Shutters is { On: true } sh) o.Add(new("shutters", "Shutters", Shutters(s, sh, f), sh.Material, sh.Color));
        if (d.Grille is { On: true } g) o.Add(new("grille", "Grille", Grille(s, g, f), g.Material, g.Color));
        if (!s.IsDoor && d.WindowBox is { On: true } wb)
        {
            var w = Math.Max(0.1, wb.Width ?? s.Width);
            var y0 = f.Facade + Math.Max(0, wb.Gap);
            var top = -Math.Max(0, wb.Drop);
            o.Add(new("windowbox", "Window box", [B("windowbox", wb.Along - w / 2, wb.Along + w / 2, y0, y0 + Math.Max(0.05, wb.Depth), top - Math.Max(0.05, wb.Height), top)], wb.Material, wb.Color));
        }
        if (d.Canopy is { On: true } c)
        {
            var (x0, x1) = (c.Along - s.Width / 2 - Math.Max(0, c.Overhang), c.Along + s.Width / 2 + Math.Max(0, c.Overhang));
            var y0 = f.Facade + Math.Max(0, c.Gap);
            var y1 = y0 + Math.Max(0.05, c.Projection);
            var z = s.Height + c.Rise;
            var th = Math.Max(0.01, c.Thickness);
            var drop = (y1 - y0) * Math.Tan(Math.Clamp(c.Slope, 0, 45) * Math.PI / 180);
            o.Add(new("canopy", "Canopy", [], c.Material, c.Color, (x0, x1, [(y0, z + th), (y1, z + th - drop), (y1, z - drop), (y0, z)])));
        }
        return o;
    }

    /// <summary>The leaves, closed over the opening or open flat against the wall beside it, each with its stiles and rails and its louvres, panel or planks.</summary>
    static List<JoineryBox> Shutters(JoinerySpec s, JoineryShutters sh, Faces f)
    {
        var o = new List<JoineryBox>();
        var leaves = Math.Clamp(sh.Leaves, 1, 2);
        var t = Math.Max(0.01, sh.Thickness);
        var y0 = f.Facade + Math.Max(0, sh.Gap);
        var y1 = y0 + t;
        var lap = Math.Max(0, sh.Overlap);
        var span = s.Width + 2 * lap;
        var lw = span / leaves;
        var (z0, z1) = (sh.Rise, sh.Rise + Math.Max(0.2, sh.Height ?? s.Height));
        var cx = sh.Along;
        // Each leaf's x range: closed, side by side over the opening; open, turned about its hinge onto the wall beside it.
        var ranges = new List<(double A, double B)>();
        for (var i = 0; i < leaves; i++)
        {
            double a = cx - span / 2 + i * lw, b = a + lw;
            if (sh.Open) (a, b) = leaves == 1 || i == 1 ? (cx + span / 2, cx + span / 2 + lw) : (cx - span / 2 - lw, cx - span / 2);
            ranges.Add((a, b));
        }
        foreach (var (a, b) in ranges) o.AddRange(Leaf(sh, a, b, y0, y1, z0, z1));
        return o;
    }

    static IEnumerable<JoineryBox> Leaf(JoineryShutters sh, double a, double b, double y0, double y1, double z0, double z1)
    {
        const double stile = 0.06;
        var w = b - a;
        if (sh.Style == "panel" || w < 3 * stile || z1 - z0 < 3 * stile)
        {
            yield return B("shutters", a, b, y0, y1, z0, z1);
            yield break;
        }
        // The frame of the leaf: two stiles, a top and a bottom rail.
        yield return B("shutters", a, a + stile, y0, y1, z0, z1);
        yield return B("shutters", b - stile, b, y0, y1, z0, z1);
        yield return B("shutters", a + stile, b - stile, y0, y1, z0, z0 + stile);
        yield return B("shutters", a + stile, b - stile, y0, y1, z1 - stile, z1);
        if (sh.Style == "planks")
        {
            // Vertical boards with a 5 mm joint, filling between the stiles.
            var inner = w - 2 * stile;
            var n = Math.Max(1, (int)Math.Round(inner / 0.1));
            var bw = inner / n;
            for (var i = 0; i < n; i++) yield return B("shutters", a + stile + i * bw + 0.0025, a + stile + (i + 1) * bw - 0.0025, y0 + 0.004, y1 - 0.004, z0 + stile, z1 - stile);
            yield break;
        }
        // Louvres: slats across, one every pitch, thinner than the leaf.
        var pitch = Math.Max(0.02, sh.Pitch);
        var depth = (y1 - y0) * 0.6;
        var ym = (y0 + y1) / 2;
        for (var z = z0 + stile + pitch / 2; z < z1 - stile - 1e-6; z += pitch)
            yield return B("shutters", a + stile, b - stile, ym - depth / 2, ym + depth / 2, z - pitch * 0.35, z + pitch * 0.35);
    }

    /// <summary>The grille's frame and bars, in the reveal (inside the opening, on the room's or the street's side, short of the frame), on the facade or on the interior face.</summary>
    static List<JoineryBox> Grille(JoinerySpec s, JoineryGrille g, Faces f)
    {
        var o = new List<JoineryBox>();
        var bar = Math.Clamp(g.Bar, 0.005, 0.1);
        var fw = Math.Clamp(g.FrameWidth, bar, 0.2);
        var depth = Math.Max(bar, 0.01);
        double x0, x1, z0, z1, y0;
        switch (g.Side)
        {
            case "outside":
                (x0, x1, z0, z1) = (-s.Width / 2 - g.Overlap, s.Width / 2 + g.Overlap, -g.Overlap, s.Height + g.Overlap);
                y0 = f.Facade + Math.Max(0, g.Gap);
                break;
            case "inside":
                (x0, x1, z0, z1) = (-s.Width / 2 - g.Overlap, s.Width / 2 + g.Overlap, -g.Overlap, s.Height + g.Overlap);
                y0 = f.In - Math.Max(0, g.Gap) - depth;
                break;
            case "revealInside":
                // In the reveal on the room's side: the clear opening, set back from the interior face, never into the frame.
                (x0, x1, z0, z1) = (-s.Width / 2, s.Width / 2, 0, s.Height);
                y0 = Math.Min(f.FrameIn - 0.005 - depth, f.In + Math.Max(0, g.Gap));
                break;
            default:
                // In the reveal: the clear opening, set back from the exterior face, never into the frame.
                (x0, x1, z0, z1) = (-s.Width / 2, s.Width / 2, 0, s.Height);
                y0 = Math.Max(f.FrameOut + 0.005, f.Out - Math.Max(0, g.Gap) - depth);
                break;
        }
        (x0, x1, z0, z1) = (x0 + g.Along, x1 + g.Along, z0 + g.Rise, z1 + g.Rise);
        var y1 = y0 + depth;
        o.Add(B("grille", x0, x0 + fw, y0, y1, z0, z1));
        o.Add(B("grille", x1 - fw, x1, y0, y1, z0, z1));
        o.Add(B("grille", x0 + fw, x1 - fw, y0, y1, z0, z0 + fw));
        o.Add(B("grille", x0 + fw, x1 - fw, y0, y1, z1 - fw, z1));
        var spacing = Math.Max(bar * 2, g.Spacing);
        var (ym0, ym1) = ((y0 + y1) / 2 - bar / 2, (y0 + y1) / 2 + bar / 2);
        if (g.Pattern is "horizontal" or "grid")
            foreach (var z in Stations(z0 + fw, z1 - fw, spacing)) o.Add(B("grille", x0 + fw, x1 - fw, ym0, ym1, z - bar / 2, z + bar / 2));
        if (g.Pattern is "vertical" or "grid")
            foreach (var x in Stations(x0 + fw, x1 - fw, spacing)) o.Add(B("grille", x - bar / 2, x + bar / 2, ym0, ym1, z0 + fw, z1 - fw));
        return o;
    }

    /// <summary>Points spread evenly between a and b at most <paramref name="spacing"/> apart, the ends left out.</summary>
    public static List<double> Stations(double a, double b, double spacing)
    {
        var n = (int)Math.Ceiling((b - a) / spacing - 1e-9);
        return n <= 1 ? [] : Enumerable.Range(1, n - 1).Select(i => a + (b - a) * i / n).ToList();
    }
}
