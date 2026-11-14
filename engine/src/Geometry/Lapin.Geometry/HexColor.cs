using System.Globalization;

namespace Lapin.Geometry;

/// <summary>
/// A colour as Lapin keeps it, <c>#rrggbb</c>: read into its components and written from them, in one place for the
/// drawings' writers (PDF, DXF), the IFC reader and writer and the mesh exports.
/// A drawing's fill may carry an opacity as CSS writes it, <c>#rrggbbaa</c> (forum #79, a room colour's): its components
/// read as the colour's, <see cref="Alpha"/> its opacity, <see cref="Opaque"/> the colour without it.
/// </summary>
public static class HexColor
{
    /// <summary>The components (0–255) of <c>#rrggbb</c> or <c>#rrggbbaa</c> (the '#' may be left out); false for anything else.</summary>
    public static bool TryParse(string? hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (hex == null) return false;
        var h = hex.AsSpan().Trim();
        if (h.Length > 0 && h[0] == '#') h = h[1..];
        if (h.Length == 8 && int.TryParse(h[6..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _)) h = h[..6];
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v)) return false;
        (r, g, b) = ((v >> 16) & 255, (v >> 8) & 255, v & 255);
        return true;
    }

    /// <summary>A colour's opacity 0–1: its <c>aa</c> in <c>#rrggbbaa</c>; 1 for <c>#rrggbb</c> and anything else.</summary>
    public static double Alpha(string? hex)
    {
        var h = (hex ?? "").AsSpan().Trim();
        if (h.Length > 0 && h[0] == '#') h = h[1..];
        return h.Length == 8 && int.TryParse(h[6..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var a) ? a / 255.0 : 1;
    }

    /// <summary>The colour without its opacity, <c>#rrggbb</c>; anything that is not a colour as it is.</summary>
    public static string Opaque(string hex) => TryParse(hex, out var r, out var g, out var b) && Alpha(hex) < 1 ? $"#{r:x2}{g:x2}{b:x2}" : hex;

    /// <summary>
    /// <c>#rrggbb</c> given an opacity 0–1: <c>#rrggbbaa</c> below full, the colour alone at full (or when it is no colour).
    /// An opacity it already has is replaced.
    /// </summary>
    public static string WithAlpha(string hex, double alpha)
    {
        if (!TryParse(hex, out var r, out var g, out var b)) return hex;
        var a = double.IsFinite(alpha) ? (int)Math.Round(Math.Clamp(alpha, 0, 1) * 255) : 255;
        return a >= 255 ? $"#{r:x2}{g:x2}{b:x2}" : $"#{r:x2}{g:x2}{b:x2}{a:x2}";
    }

    /// <summary>The components 0–255, or <paramref name="fallback"/>'s when <paramref name="hex"/> is no colour.</summary>
    public static (int R, int G, int B) Bytes(string? hex, (int R, int G, int B) fallback = default) =>
        TryParse(hex, out var r, out var g, out var b) ? (r, g, b) : fallback;

    /// <summary>The components 0–1, or <paramref name="fallback"/> when <paramref name="hex"/> is no colour.</summary>
    public static (double R, double G, double B) Unit(string? hex, (double R, double G, double B) fallback = default) =>
        TryParse(hex, out var r, out var g, out var b) ? (r / 255.0, g / 255.0, b / 255.0) : fallback;

    /// <summary><c>#rrggbb</c> of components 0–1, each clamped there first (a file's colour may stray out of it).</summary>
    public static string Of(double r, double g, double b)
    {
        static int C(double v) => double.IsFinite(v) ? (int)Math.Round(Math.Clamp(v, 0, 1) * 255) : 0;
        return $"#{C(r):x2}{C(g):x2}{C(b):x2}";
    }
}
