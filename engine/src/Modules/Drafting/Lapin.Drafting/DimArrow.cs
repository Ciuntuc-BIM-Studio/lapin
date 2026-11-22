using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A dimension's terminator, as every dimension draws it (a string's, an angle's, a radius', an arc's, a slope's; audit
/// 22.11.2026 §4: it was written twice): a tick leaning 45° across the line, a dot, an open arrow, or nothing ("none").
/// The tick and the dot take the style's tick pen (<see cref="DimStyle.TickWeightOr"/>, 0.25 mm unless the style says), the
/// arrow the dimension line's.
/// </summary>
public static class DimArrow
{
    /// <summary>
    /// The terminator at <paramref name="tip"/>: <paramref name="inward"/> the unit direction its arrow's barbs run back along
    /// the dimension line, <paramref name="lean"/> the unit normal its tick leans towards (with the line, 45°); paper mm in
    /// view units <paramref name="mm"/>.
    /// </summary>
    public static void Draw(ViewMark mk, DimStyle style, double mm, Vec2 tip, Vec2 inward, Vec2 lean, double lineWeight, string color)
    {
        var s = style.ArrowSize * mm;
        switch (style.Arrow)
        {
            case "tick": { var t = (inward + lean).Normalized() * (s / 2); mk.Line(tip - t, tip + t, "DIMENSION", style.TickWeightOr(), color); break; }
            case "dot": mk.Polyline(ViewMark.Circle(tip, s / 4, 12), true, "DIMENSION", style.TickWeightOr(), color); break;
            case "arrow": mk.Line(tip, tip + inward * s + lean * (s * 0.3), "DIMENSION", lineWeight, color).Line(tip, tip + inward * s - lean * (s * 0.3), "DIMENSION", lineWeight, color); break;
        }
    }
}
