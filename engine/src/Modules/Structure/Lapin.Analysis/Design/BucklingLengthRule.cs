using Lapin.Analysis.Solver;
using static Lapin.Analysis.Design.Checks;

namespace Lapin.Analysis.Design;

/// <summary>The buckling length a check uses about one axis (m), where it came from ("given", "k", "alpha"), why, and the αcr length when there is one.</summary>
public sealed record UsedLength(double Lcr, string Source, string How, double? FromAlpha = null, double? Alpha = null, int? Mode = null);

/// <summary>A design set's lengths about both axes, and the sentence the checks say of them.</summary>
public sealed record SetLengths(UsedLength Y, UsedLength Z, string Text);

/// <summary>
/// Which buckling length a member's check takes about an axis when the project (or the member) asks for the lengths from αcr
/// (docs/plan-fem.md §14.24):
/// <list type="number">
/// <item>a length the user gave (lcrY, lcrZ) is taken as it is;</item>
/// <item>with second order on (P-Δ), the non-sway length k·L: the sway effects are in the forces (EN 1993-1-1 5.2.2(3)b), an
/// αcr length would count them twice;</item>
/// <item>no αcr length (no mode bends the member about that axis, its own mode, tension, a seismic bound): k·L;</item>
/// <item>otherwise the longer of k·L and the αcr length: k·L is the floor (the non-sway length, and one analysis element a
/// member overstates its own non-sway Ncr), the αcr length (the sway one) takes over above it; past 3 L it is said, as a
/// length to look at (a column the model leaves unrestrained, a frame only just stable). L is the element's whole length:
/// the analysis cuts a column at the rails' nodes into pieces, each a design member of its own whose k·L is the piece's.</item>
/// </list>
/// </summary>
public static class BucklingLengthRule
{
    /// <summary>An αcr length over this many times the member's length is said.</summary>
    public const double Long = 3.0;

    /// <param name="l">The element's whole length (m; a bar cut at nodes along it: all its pieces).</param>
    /// <param name="kL">The user's (or the ends') length, k·L (m).</param>
    /// <param name="given">A length the user gave (m), or null.</param>
    /// <param name="a">The αcr length about the axis, or null.</param>
    /// <param name="why">Why there is no αcr length, when there is none.</param>
    public static UsedLength Choose(double l, double kL, double? given, AxisLength? a, bool secondOrder, string? why = null)
    {
        if (given is { } g) return new UsedLength(g, "given", "the length given", a?.Lcr, a?.Alpha, a?.Mode);
        if (secondOrder) return new UsedLength(kL, "k", "second order on: the non-sway length (EN 1993-1-1 5.2.2(3)b)", a?.Lcr, a?.Alpha, a?.Mode);
        if (a == null) return new UsedLength(kL, "k", why ?? "no buckling mode of the structure bends it this way");
        if (a.Lcr <= kL) return new UsedLength(kL, "k", $"αcr's length {F(a.Lcr, "0.##")} m is under k·L: k·L kept", a.Lcr, a.Alpha, a.Mode);
        var how = $"from αcr = {F(a.Alpha, "0.##")} (mode {a.Mode}): π√(EI/(αcr NEd))";
        if (a.Lcr > Long * l) how += $", over {F(Long, "0.#")} L: look at the model (a column left unrestrained, a frame only just stable)";
        return new UsedLength(a.Lcr, "alpha", how, a.Lcr, a.Alpha, a.Mode);
    }
}
