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

## The control cage (issue #113, decision 751)

A terrain may keep a few of its survey points as **controls** (`cage`: their places in plan, `[[x, y], …]`, a point matching within 1 mm; `cageReach`: optional). Places, not indices, so adding, removing or merging points never moves a control onto another point; the TIN and the GlobalId are untouched. `civil.setTerrainControls` sets them (indices with mode set/add/remove/toggle, `spacing`: the point nearest each node of a plan grid, `boundary`: the points on the outline, `clear`), one undo step, moving no point.

Moving controls up or down (`civil.moveTerrainPoint`, `civil.setTerrainPoints`) moves the free points, in the same step, by

    shift(x) = Σc Δc / |x − xc|²  /  (Σc 1 / |x − xc|² + 1 / R²)

over every control c (Δc its move, 0 for one that stays): Shepard's inverse-distance weighting, power 2, with one more weight 1/R² for "stay". At a control it is that control's move exactly; between two controls it goes monotonically from one's move to the other's; far from every control it fades (one control alone lifts a smooth bump, half its move at R). R is `cageReach`, else twice the mean nearest-neighbour distance of the controls (half the survey's shorter side with one). The weights depend only on where the controls are, so the shift is linear in the moves: applying each move to the points as they are equals deriving each free point from its height when the cage was set plus the interpolated offsets since, and nothing but the controls' places is stored. Removing a point, replacing the survey and splitting drop the controls that no longer stand on a point (or lie outside a half); merging gathers them. With no controls, every point command works as before. Code: `Lapin.Civil/Cage.cs`; tests `CivilCageTests`, `pointGrips.test.ts`.
