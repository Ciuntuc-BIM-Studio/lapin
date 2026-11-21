namespace Lapin.Drawing.Hatch;

/// <summary>
/// The built-in patterns (the old Lapin's own designs), in millimetres as they
/// print at 1:1 on paper: the caller scales them to the drawing. A project's
/// imported patterns take precedence over a built-in of the same name.
/// The real-size ones (decision 679, <c>;%TYPE=MODEL</c>, <see cref="HatchUnits.Model"/>) are millimetres of the
/// object: brick courses (240×63, the Romanian format, the default; 240×71; 215×65) with 10 mm joints drawn as one line
/// at the joint, blocks, tiles, boards, coursed stone, roof tiles and standing-seam sheet, drawn by Lapin.
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

*RCONCRETE, Reinforced concrete (speckle over 45 degree lines)
45, 0,0, 0,3
50, 0,0, 2.1,5.3, 0.9,-6.7
140, 1.3,0.4, 3.7,4.9, 0.6,-5.9
15, 0.7,2.2, 4.4,6.1, 0,-4.3, 0,-7.1
95, 2.6,1.5, 3.2,5.7, 0,-5.2

*MASONRY, Masonry and brick cut: 45 degree lines 3.2 mm apart
45, 0,0, 0,3.2

*BLOCK, Concrete blocks cut: diagonal crosshatch 3 mm
45, 0,0, 0,3
135, 0,0, 0,3

*RIGID, Rigid insulation cut: square grid 2.4 mm
0, 0,0, 0,2.4
90, 0,0, 0,2.4

*GYPSUM, Gypsum and plaster cut: staggered stipple
0, 0,0, 2.2,1.6, 0,-4.4
0, 1.1,0.8, 2.2,1.6, 0,-4.4

*SAND, Sand: dense stipple
0, 0,0, 1.2,0.9, 0,-2.4
0, 0.6,0.45, 1.2,0.9, 0,-2.4
0, 0.3,0.2, 1.7,1.35, 0,-3.4

*INSULATION, Batt insulation zig-zag rows
60, 0,0, -2.309401,4, 4.618802,-4.618802
120, 4.618802,0, 2.309401,4, 4.618802,-4.618802

;; Insulation (drawn by Lapin from simple geometry; BATT, the batt's heads along the layer, is drawn by its own rule)
*FOAMBOARD, Foam board insulation: a grid of circles 2.4 mm
;%LAPIN-TILE=2.4,2.4
;%LAPIN-WHOLE
;%LAPIN-ARC=1.2,1.2,0.9,0,360

*REFLECTIVE, Reflective insulation: rows of zigzags
;%LAPIN-TILE=2,1.6
;%LAPIN-LINE=0,0.3,1,1.3
;%LAPIN-LINE=1,1.3,2,0.3

*BLANKET, Blanket insulation: a close diamond lattice at 60 degrees
60, 0,0, 0,1.2
120, 0,0, 0,1.2

*FIBERGLASS, Fibreglass insulation: rows of overlapping scales
;%LAPIN-TILE=4,4
;%LAPIN-ARC=2,0,2,0,180
;%LAPIN-ARC=0,2,2,0,180

*BLOWNIN, Blown-in insulation: a honeycomb of hexagons 1 mm a side
;%LAPIN-TILE=3,1.7320508075688772
;%LAPIN-LINE=0,0.8660254037844386,0.5,0.8660254037844386
;%LAPIN-LINE=0.5,0.8660254037844386,1,0
;%LAPIN-LINE=1,0,2,0
;%LAPIN-LINE=2,0,2.5,0.8660254037844386
;%LAPIN-LINE=2.5,0.8660254037844386,3,0.8660254037844386
;%LAPIN-LINE=0.5,0.8660254037844386,1,1.7320508075688772
;%LAPIN-LINE=2.5,0.8660254037844386,2,1.7320508075688772

*RIGIDFOAM, Rigid foam insulation: rounded squares with small diamonds at their corners
;%LAPIN-TILE=3,3
;%LAPIN-WHOLE
;%LAPIN-LINE=0.9,0.5,2.1,0.5
;%LAPIN-LINE=2.5,0.9,2.5,2.1
;%LAPIN-LINE=2.1,2.5,0.9,2.5
;%LAPIN-LINE=0.5,2.1,0.5,0.9
;%LAPIN-LINE=0.25,0,0,0.25
;%LAPIN-LINE=0,0.25,-0.25,0
;%LAPIN-LINE=-0.25,0,0,-0.25
;%LAPIN-LINE=0,-0.25,0.25,0
;%LAPIN-ARC=2.1,0.9,0.4,270,90
;%LAPIN-ARC=2.1,2.1,0.4,0,90
;%LAPIN-ARC=0.9,2.1,0.4,90,90
;%LAPIN-ARC=0.9,0.9,0.4,180,90

*ACOUSTIC, Acoustic insulation: interlocking rows of pointed ovals
;%LAPIN-TILE=4,2.4
;%LAPIN-ARC=2,-1.2333333333333334,2.433333333333333,48.887,82.226
;%LAPIN-ARC=2,2.433333333333333,2.433333333333333,228.887,82.226
;%LAPIN-ARC=0,-0.03333333333333344,2.433333333333333,48.887,82.226
;%LAPIN-ARC=0,3.6333333333333333,2.433333333333333,228.887,82.226

*RIGIDFIBER, Rigid fibre board: bands of lines crossing at shallow angles
0, 0,0, 0,3
18, 0,0, 0,1.5
162, 0,0, 0,1.5

*COTTONFIBER, Cotton fibre insulation: a lattice of long lenses
;%LAPIN-TILE=3,1
;%LAPIN-ARC=1.5,-2,2.5,53.13010235415598,73.73979529168804
;%LAPIN-ARC=1.5,3,2.5,233.13010235415598,73.73979529168804

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

*VERTICAL, Vertical lines 4 mm (cladding seams)
90, 0,0, 0,4

*BOARDS, Timber boards 3 mm with staggered butt joints
0, 0,0, 0,3
90, 0,0, 3,15, 3,-9

*STONE, Coursed stone 18 x 6 mm
0, 0,0, 0,6
90, 0,0, 6,6, 6,-12

*GRAVEL, Gravel dots and grains
0, 0,0, 1.7,2.9, 0,-3.3
37, 0.8,1.1, 2.3,3.1, 0.5,-4.2
118, 2.1,0.4, 1.9,3.6, 0.4,-3.9
74, 1.4,2.2, 2.6,3.4, 0,-2.8

;; Real size (millimetres of the object; decision 679)
*BRICK_240X63, Brick 240 x 63 running bond, 10 mm joints (real size)
;%TYPE=MODEL
0, 0,0, 0,73
90, 0,0, 73,125, 73,-73

*BRICK_240X71, Brick 240 x 71 running bond, 10 mm joints (real size)
;%TYPE=MODEL
0, 0,0, 0,81
90, 0,0, 81,125, 81,-81

*BRICK_215X65, Brick 215 x 65 running bond, 10 mm joints (real size)
;%TYPE=MODEL
0, 0,0, 0,75
90, 0,0, 75,112.5, 75,-75

*BLOCK_400X200, Concrete blocks 390 x 190, 10 mm joints, half bond (real size)
;%TYPE=MODEL
0, 0,0, 0,200
90, 0,0, 200,200, 200,-200

*BLOCK_600X250, Aerated concrete blocks 600 x 250, half bond (real size)
;%TYPE=MODEL
0, 0,0, 0,250
90, 0,0, 250,300, 250,-250

*TILE_300X300, Tiles 300 x 300 (real size)
;%TYPE=MODEL
0, 0,0, 0,300
90, 0,0, 0,300

*TILE_600X600, Tiles 600 x 600 (real size)
;%TYPE=MODEL
0, 0,0, 0,600
90, 0,0, 0,600

*BOARDS_150, Timber boards 150 mm, 2.4 m long, staggered butt joints (real size)
;%TYPE=MODEL
0, 0,0, 0,150
90, 0,0, 150,600, 150,-450

*STONE_600X300, Coursed stone 600 x 300, joints staggered by a third (real size)
;%TYPE=MODEL
0, 0,0, 0,300
90, 0,0, 300,200, 300,-600

*ROOF_TILES, Roof tiles: courses at a 330 mm gauge, 300 mm wide, half bond (real size)
;%TYPE=MODEL
0, 0,0, 0,330
90, 0,0, 330,150, 330,-330

*METAL_SEAM_500, Standing-seam metal sheet, seams every 500 mm (real size)
;%TYPE=MODEL
90, 0,0, 0,500
""";

    static readonly List<HatchPattern> Patterns = [.. Pat.Parse(BuiltIn).Patterns, BattHatch.Pattern];

    /// <summary>The built-in patterns (records: immutable, safe to share).</summary>
    public static IReadOnlyList<HatchPattern> BuiltInPatterns => Patterns;

    /// <summary>Case-insensitive lookup; <paramref name="extra"/> (a project's imported patterns) wins.</summary>
    public static HatchPattern? Find(string name, IEnumerable<HatchPattern>? extra = null)
    {
        var key = name.Trim().ToUpperInvariant();
        return extra?.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? Patterns.FirstOrDefault(p => p.Name == key);
    }
}
