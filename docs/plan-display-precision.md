# Precision against display speed (decision 203)

Asked for on 31.10.2026, after the analysis of where geometry should live (CPU or GPU): the geometry stays in the engine, on the CPU, in double precision; the GPU only draws. What the user cares about is precision against display speed, managed from the engine. Three steps, all done.

## 1. One rule for curves: the chord's error, not a fixed angle

Lapin's bodies are polyhedral (manifold's booleans work on meshes), so a curve becomes straight pieces when its body is built. Each place used to pick its own fixed step: arcs of walls and beams every 5°, drafted arcs and Studio profiles every 5.6° or 7.5°, revolutions every 10°, fillets in 12 steps, IFC circles in 24 sides, pipes in 12, Studio primitives in 32. A fixed angle is wrong both ways: a 10 m curved wall at 5° strays 12 mm from its arc; a 50 mm pipe at 5.6° has 64 sides.

`Lapin.Geometry/Chords` is now the one rule: a chord strays at most `Tolerance` (1 mm) from its arc; a step turns at most `MaxStep` (15°, under `Tessellator.CreaseDeg`, so a round surface draws no line between its facets: a circle has at least 24 sides) and at least `MinStep` (0.5°, reached past a radius of about 105 m). It is used by:

- `PathCurve.Stations` (curved walls, beams, footings, curtain walls: their plan, body and IFC axis);
- `Shapes.Polylines` (drafted circles and arcs, the outlines and paths Studio builds on; an explicit `maxAngle` keeps the old behaviour for a caller that asks);
- Studio's revolutions (at the profile's point farthest from the axis), primitives (round ones at their largest radius; a shell's inside takes its outside's sides, so its walls stay even) and fillets (`Features`);
- IFC import: circle and ellipse profiles, circles, trimmed arcs, three-point arcs, revolved areas, cylindrical surfaces and edges of advanced B-reps; swept disks (bars, pipes) by the rule too but at least 8 sides and up to 45° a step (a rebar cage is thousands of them);
- MEP pipes, risers, tees and bends; roof downpipes, snow guard tubes and half-round gutters; round columns and beams (`Frames`); `Prism.Cylinder` when no count is given.

Left as they were, on purpose: the barrel roof's strips (7.5°: each is a roof plane with its own covering, not a tessellation), symbols drawn in plan (trees, fixtures' details, markers), loft resampling and sweep stations (twist and growth, not curvature), road stations.

The rule is a constant, not a setting: the same project gives the same geometry on any machine at any time, so undo, sessions shared with guests, booleans, GlobalIds and quantities stay stable.

What it changes: a 10 m radius wall gets 223 sides a circle (it had 72) and is within the millimetre; a 50 mm pipe gets 24 (it had 12, folding 30°, with a line drawn between each facet); a 0.5 m Studio cylinder is within 0.3 % of the true volume (32 sides lost 0.6 %). An IFC file whose curves are already triangles (most exported models) is unchanged.

## 2. Coarser copies for the screen

`Lapin.Geometry/MeshSimplify` makes coarser copies of a heavy part (256 triangles or more): Garland and Heckbert's edge collapse by quadric error, the vertices welded, the error measured as the mean squared distance to the planes of the triangles merged (by their area), collapses that would fold a triangle or join two sheets refused (the link condition), open edges held by a plane across them. One pass makes every level: it collapses up to 1/400, 1/100 and 1/25 of the part's box diagonal, each level kept only when it has at most 60 % of the triangles of the last one kept. The error a copy says it has is then measured, not the target: every original corner's distance to the copy's triangles round the vertex it went into (the quadric's error is a mean, and a chord sagging across a curve strays more than the corners moved; the measure is about 1.7 times the target on a sphere). A copy travels as three corners a triangle and its edges (open, or folded past 40°), with no normals or indices: half the weight.

`Session` adds them to every render part it sends (`RenderPart.Lods`: `RenderLod` {error, positions, normals, indices, edges}), shared meshes included. A sphere of 1 m in 4,828 triangles gets copies of 814, 202 and 42 triangles, straying 15 mm, 61 mm and 221 mm (`DisplayPrecisionTests`).

The interface (`ui/src/viewport/lod.ts`) numbers a copy's corners and shades it smooth across folds under 40° (`creasedNormals`): shaded flat, a copy showed its facets in the light even where its outline was within a pixel. It draws, for each part, the coarsest copy whose error, at the part's nearest distance (perspective) or at the view's scale (orthographic), covers at most the quality's pixels. An element drawn on its own has a group per copy and shows one; an instance of a shared mesh is written into the InstancedMesh of its copy and is nothing in the others (`instancing.ts`; chosen again at most every 150 ms while the camera moves, and once more when it stops). Copies are never picked: a click and the face tools meet the part itself (an instance far away is picked on its copy, its id the same). The walker, exports, drawings and quantities use the part itself.

## 3. Display quality, the project's

View → Display quality: **Fast** (3 pixels), **Balanced** (1 pixel, the default) or **Precise** (every part as it is). Kept on the project (`displayQuality`, `project.setDisplayQuality`, one undo step); it changes only what the screen draws.

## Measured

Release, the IFC benchmark (`LAPIN_IFC_BENCH`, `IfcBenchmarkTests`), before (7685835) and after; timings on this machine vary by a factor of two from run to run (the plant's import took 16.6 s to 28.3 s before, 17.7 s and 18.1 s after), so the triangles and frames are the measure. "Drawn" counts every instance; "from afar" is every part at its coarsest copy, what a view of the whole model draws.

| Model | Triangles sent | Coarser copies | Drawn near / from afar | Frame |
|---|---|---|---|---|
| Bloc-P-4 (58 k triangles, no curves) | 58,108 → 58,108 | | | 7 MB → 7 MB |
| BURKWIL (ArchiCAD, small parts) | 438,668 → 438,668 | 6,642 | 472,166 / 429,474 | 35 MB → 36 MB |
| An industrial plant (78 MB IFC, 15,110 elements) | 1,842,462 → 1,842,462 | 518,440 | 3,185,978 / 1,340,394 (−58 %) | 193 MB → 224 MB |

An architectural model is many small parts: the copies change little there, and what limits its frame rate is the number of objects drawn, not their triangles (merging them by material is the next step if a model asks for it). The copies pay off on heavy parts: imported plants, machines, furniture and fittings in great numbers.

## Not done

- Curves are not re-tessellated when zooming: after a boolean a body no longer knows which faces came from which cylinder. Keeping exact surfaces through booleans would be a kernel of its own.
- No GPU picking, no merging of small objects into batches, no WebGPU: the analysis lists them as the next steps for very large models, each to be measured first.
