# Acknowledgements

What Lapin has taken from other projects, and where it is in the code.

## BimGo

[BimGo](https://github.com/aussieBIMguru/BimGo) by Aussie BIM Guru is a first-person walkthrough for Revit models. It is released under the MIT No Attribution licence (MIT-0), so nothing below is owed. We credit it anyway, because a good part of Lapin's coordination and presentation tools follows its design, its numbers and its tests.

Lapin uses none of BimGo's renderer: BimGo draws with its own OpenGL engine, and Lapin draws with three.js. What Lapin took is the methods, the constants and the test cases. The code was rewritten in Lapin's engine and interface.

### Clash detection (`clash` module, decisions 313 and 317)

- **Engine.** `engine/src/Modules/Coordination/Lapin.Clash/ClashEngine.cs` follows BimGo's `ClashEngine`:
  - a broad phase of sweep and prune over boxes, along the axis the centres spread most;
  - a narrow phase over triangles, in parallel, with a triangle tree for bodies of 384 triangles or more;
  - the hard depth as the narrowest width of the crossing over the axes and the crossing faces' normals;
  - buried corners for shallow crossings (a pipe sunk into a slab);
  - clearance tests by triangle distances;
  - the 5 mm hard tolerance, the 50 mm clearance and the 10,000-clash limit.
- **Geometry.** `engine/src/Geometry/Lapin.Geometry/Collisions.cs` follows BimGo's `ClashGeometry` and `ClashTree`:
  - Möller's triangle–triangle test;
  - Ericson's closest points;
  - Möller–Trumbore rays;
  - the even–odd inside test along three skewed rays;
  - the median-split triangle tree.
- **Search sets.** `SearchSets.cs` follows BimGo's search sets:
  - the rule operators and how "not" rules take elements out;
  - nested groups;
  - numbers read as people write them ("600 mm", "1,200", "2,5").

  The fields are Lapin's own: IFC class, type, storey, material, linked file and parameter.
- **Following clashes.** `ClashTests.cs` follows BimGo's `ClashMerge`:
  - clashes followed from run to run by their pair;
  - the statuses new, active, reviewed, approved and resolved;
  - a run cut short at the limit resolving nothing.
- **Groups and reports.** BimGo's `ClashGroups` and `ClashReports` gave:
  - grouping by element, with the element in most clashes first and two clashes at least per group;
  - the XML report in the layout Navisworks writes;
  - the HTML report with a picture of each clash.
- **Framing.** BimGo's `ClashFraming` gave the way a clash is shown in 3D: a section box with 1 m round the clash and 2 m at least, the camera 2.5 m back and 20° down.
- **Tests.** BimGo's clash tests are ported with their cases and numbers in `engine/tests/Lapin.Engine.Tests/ClashEngineTests.cs`:
  - the duct through a beam, 200 mm deep;
  - the 3 mm overlap, ignored at 5 mm;
  - the pipe sunk 40 mm into a slab;
  - the clearance gaps;
  - the limit.

Where Lapin goes further:
- an element wholly inside another is reported (BimGo leaves it out; `Contained: false` behaves as BimGo does);
- pairs meant to meet are skipped by Lapin's own relations (joins, fills, assemblies, attached elements);
- the openings check is a test kind of its own.

### The realistic 3D view (decision 310)

`ui/src/viewport/render3d.ts` takes BimGo's numbers:
- the ambient occlusion radius of about 0.6 m;
- shadow quality tiers;
- the sun's colour from warm at sunrise to white at noon.

BimGo's sun position and sun hours, which use NOAA's formulas, matched the sun Lapin already had in its urban module; that sun is now in the platform (`Sun.cs`).

### Daylight studies (planned: issues #11 and #12)

The plan in `docs/plan-visualisation.md` follows BimGo's sun hours, daylight factor and illuminance studies:
- the grid on the room's surfaces;
- the 192 sky patches;
- the BRE split-flux formula;
- the Ladybug colour scale;
- the CSV columns.

### Entourage (planned, stage 4)

BimGo's trees, people and cars are drawn in code (`EntourageArt.cs`), with no third-party pictures. They are the starting point for Lapin's.

## Published methods

The methods below are credited the way BimGo credits them:
- T. Möller, "A Fast Triangle-Triangle Intersection Test" (1997);
- T. Möller and B. Trumbore, "Fast, Minimum Storage Ray-Triangle Intersection" (1997);
- C. Ericson, "Real-Time Collision Detection" (2005), ch. 5;
- NOAA Global Monitoring Laboratory, "General Solar Position Calculations";
- the CIE overcast and clear skies;
- BRE's split-flux formula.

## Graphisoft Archicad

The vector sun shadows in elevations and sections (decision 322) follow how Archicad lets each elevation take the 3D view's sun or one of its own. The default sun follows the old drawing convention: over the left shoulder, along a cube's diagonal. No Archicad code or content is used.
