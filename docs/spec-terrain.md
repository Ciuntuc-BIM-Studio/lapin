# Terrain and civil: specification from the old Lapin

Read from the old app on 24.10.2026. The old user doc is `~/Documents/GitHub/Lapin/docs/infrastructura.md`.

The old terrain is a simple, static TIN:
- created from survey points, from a selected mesh, or from a LandXML import;
- stored as `IfcGeographicElement` TERRAIN, with an `IfcTriangulatedFaceSet` body contained in the storey and coloured `#7a9a5a`;
- used as the ground under road corridors.

It had no contours, no editing, no platforms and no excavation. Those are new in lapin-pro.

## Old behaviour (kept)

- **Survey points** (`.csv .txt .xyz .pts`):
  - One point per line, fields separated by spaces, commas, semicolons or tabs.
  - Lines starting with `#` or `//` are skipped.
  - Four leading numbers are PENZD: `id northing easting z [desc]`, read as (easting, northing, z).
  - Otherwise three numbers are `x y z`.
  - Fewer than 3 points is refused.
- **Delaunay:**
  - Bowyer–Watson in plan.
  - Points that coincide in plan (to 6 decimals) are dropped; the first one wins.
  - Triangles are counter-clockwise and cover the convex hull.
  - Zero triangles: "The terrain needs at least three points that are not in a line."
- **Queries:**
  - `elevationAt(x, y)`: barycentric; null outside the TIN.
  - `hit(origin, dir, maxRun)`: steps of max(cell/4, 0.05) m, then 40 bisections.
- **Mesh to TIN:** vertices are de-duplicated (keyed to 5 decimals), degenerate triangles (|2A| < 1e-10) are dropped, and the rest are oriented counter-clockwise.

## Tests (`tin.test.ts`, the same cases)

1. **Delaunay over a grid** (`grid(n, z)`, 10 m spacing):
   - A 5×5 flat grid gives 32 triangles, all counter-clockwise.
   - An 80-point scatter gives more than 100 triangles, with no point inside any circumcircle.
     - The scatter: seed 7, LCG ×16807 mod 2147483647, over 100×100×5.
     - The circumcircle check uses the tolerance r²(1−1e-9)−1e-6.
2. **A planar surface and a slope line:**
   - A 6×6 grid with z = 100 + 0.1x + 0.02y.
   - `elevationAt(17,23)` = 102.16; `elevationAt(-1,0)` = null.
   - `hit([10,10,104.2],[2,0,-1],50)` gives x ≈ 15.
   - `hit([10,10,200],[1,0,0],20)` = null.
3. **Reading points and meshes:**
   - `"# header\n1,2,3\n4 5 6\n"` gives [[1,2,3],[4,5,6]].
   - `"7,4500.0,500.0,120.5,TREE"` gives [[500,4500,120.5]].
   - A 6-vertex, 2-triangle mesh with shared vertices gives 4 points and 2 triangles.

## Civil beyond the terrain (later, in the civil edition)

- **Alignments:** PIs with radii and clothoids; `IfcAlignment` in IFC4X3.
- **Profiles:** PVIs with parabolas.
- **Corridors:** assemblies of lanes, courses and slopes.
  - Daylight is found on the terrain.
  - Cut and fill use the average end area between stations, written to `Lapin_CorridorQuantities`.
- **Bridges** and **LandXML 1.2**.
- Their tests:
  - `corridor.test.ts`, `alignment.test.ts`, `profile.test.ts`, `landxml.test.ts`, `bridge.test.ts`;
  - in `packages/create/src/civil/`.
