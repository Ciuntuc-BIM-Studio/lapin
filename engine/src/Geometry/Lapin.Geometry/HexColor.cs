using System.Globalization;

namespace Lapin.Geometry;

/// <summary>
/// A colour as Lapin keeps it, <c>#rrggbb</c>: read into its components and written from them, in one place for the
/// drawings' writers (PDF, DXF), the IFC reader and writer and the mesh exports.
/// </summary>
public static class HexColor
{
    /// <summary>The components (0–255) of <c>#rrggbb</c> (the '#' may be left out); false for anything else.</summary>
    public static bool TryParse(string? hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (hex == null) return false;
        var h = hex.AsSpan().Trim();
        if (h.Length > 0 && h[0] == '#') h = h[1..];
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v)) return false;
        (r, g, b) = ((v >> 16) & 255, (v >> 8) & 255, v & 255);
        return true;
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
