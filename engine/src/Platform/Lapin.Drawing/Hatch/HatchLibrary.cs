namespace Lapin.Drawing.Hatch;

/// <summary>
/// The built-in patterns (the old Lapin's own designs), in millimetres as they
/// print at 1:1 on paper: the caller scales them to the drawing. A project's
/// imported patterns take precedence over a built-in of the same name.
/// </summary>
public static class HatchLibrary
{
    const string BuiltIn = """
;; Lapin built-in hatch patterns (millimetre units)
*SOLID, Solid fill

*LINES45, Single diagonal lines at 45 degrees
45, 0,0, 0,3

*HORIZONTAL, Horizontal lines
0, 0,0, 0,2.5

*CROSS, Diagonal cross-hatch
45, 0,0, 0,3.5
135, 0,0, 0,3.5

*TILES, Square tile grid 8 mm
0, 0,0, 0,8
90, 0,0, 0,8

*BRICK, Running-bond brick courses 8 x 4 mm
0, 0,0, 0,4
90, 0,0, 4,4, 4,-4

*STEEL, Paired diagonal lines (steel section)
45, 0,0, 0,4.2
45, -0.55,0.55, 0,4.2

*CONCRETE, Concrete speckle
50, 0,0, 2.1,5.3, 0.9,-6.7
140, 1.3,0.4, 3.7,4.9, 0.6,-5.9
15, 0.7,2.2, 4.4,6.1, 0,-4.3, 0,-7.1
95, 2.6,1.5, 3.2,5.7, 0,-5.2

*INSULATION, Batt insulation zig-zag rows
60, 0,0, -2.309401,4, 4.618802,-4.618802
120, 4.618802,0, 2.309401,4, 4.618802,-4.618802

*EARTH, Groups of short strokes (earth)
0, 0,0, 6,6, 4,-8
0, 0,0.8, 6,6, 4,-8
0, 0,1.6, 6,6, 4,-8
90, 5.2,-2.6, -6,6, 4,-8
90, 6,-2.6, -6,6, 4,-8
90, 6.8,-2.6, -6,6, 4,-8

*WOOD, Wood grain (approximated waves)
0, 0,0, 7.3,2.4, 9,-1.5
8, 0,0.9, 5.1,2.4, 4.2,-11
352, 3,1.4, 6.7,2.4, 3.6,-9.8

*WOODCROSS, Wood across the grain (WOOD turned 90°, cross layers)
90, 0,0, 7.3,2.4, 9,-1.5
98, -0.9,0, 5.1,2.4, 4.2,-11
82, -1.4,3, 6.7,2.4, 3.6,-9.8

*GRAVEL, Gravel dots and grains
0, 0,0, 1.7,2.9, 0,-3.3
37, 0.8,1.1, 2.3,3.1, 0.5,-4.2
118, 2.1,0.4, 1.9,3.6, 0.4,-3.9
74, 1.4,2.2, 2.6,3.4, 0,-2.8
""";

    static readonly List<HatchPattern> Patterns = Pat.Parse(BuiltIn).Patterns;

    /// <summary>The built-in patterns (records: immutable, safe to share).</summary>
    public static IReadOnlyList<HatchPattern> BuiltInPatterns => Patterns;

    /// <summary>Case-insensitive lookup; <paramref name="extra"/> (a project's imported patterns) wins.</summary>
    public static HatchPattern? Find(string name, IEnumerable<HatchPattern>? extra = null)
    {
        var key = name.Trim().ToUpperInvariant();
        return extra?.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? Patterns.FirstOrDefault(p => p.Name == key);
    }
}
