using Lapin.Drawing;

namespace Lapin.Platform;

/// <summary>
/// The colour a solid hatch pattern fills with (forum topic 88, decision 678), one rule for the elevations' surfaces
/// (<see cref="SurfaceHatches"/>) and the 3D view's caps, the rule the drawings' bands follow (Lapin.Drawing's Pens, which the
/// legends' swatches are drawn by; audit 2026-11-22): a hatch colour the material gives (its background's for a background
/// hatch) over everything; else its fill (<paramref name="fill"/>: the category's or the material's, as the caller has it), else
/// the ink (<paramref name="ink"/>: the category's line colour, black without one); halftone and the hatch's opacity applied.
/// </summary>
public static class SolidFill
{
    /// <summary>The ink without a category: what a drawing's lines and a legend's are drawn in.</summary>
    public const string Ink = "#000000";

    public static string Of(ResolvedMaterial? look, string? fill, string ink = Ink, bool background = false)
    {
        var bg = background ? look?.Background : null;
        if (look?.HatchColor != null || bg?.HatchColor != null)
            return bg != null ? ResolvedMaterial.Ink(bg.HatchColor ?? ink, look!.Halftone, look.HatchOpacity) : look!.HatchInk(ink);
        var solid = fill ?? ink;
        return look == null ? solid : ResolvedMaterial.Ink(solid, look.Halftone, look.HatchOpacity);
    }
}
