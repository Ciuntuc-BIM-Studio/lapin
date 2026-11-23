# Gaussian splats

Asked on the forum (topic 237, "Gaussian Splat Support", 23.11.2026) and accepted by the user the same day ("I'm in, will do it").
Decision 809 and the following, one per stage, in `docs/decisions-pending.md`.

The request: link Gaussian splats (3DGS `.ply`, the compressed `.spz` and `.sog`) as point clouds are linked; switch between
the splat and the point view; snap to them; measure distance and area on them; rescale a splat by two points and the real
distance between them (SuperSplat's way); put them on sheets with dimensions and notes; later a standalone `.html` that
carries splats with its measuring tools.

## Principles

- **A splat is a point cloud with more to it.** It is a `LapinLink` of kind `pointcloud` whose file holds Gaussians; the
  link carries `splats: true`. Its centres go through the point cloud index unchanged (colour from the zero-order
  spherical harmonics, the nearly transparent ones left out), so everything point clouds do works on a splat from the
  first day: the Links tab, placement and unit check, Visibility / Graphics' Point clouds tab, the slice drawn in plans and
  sections, snapping in 2D and 3D, Measure, the mesh exports. Its points never enter the model.
- **The Gaussians are a second store beside the index** (`splats.bin` in the same cache folder): every splat's centre,
  scale, rotation, colour and opacity, 32 bytes each (antimatter15's layout: 3 × float32 position, 3 × float32 scale,
  RGBA8, rotation quantized to 4 bytes), in the file's coordinates; the link's transform places them as it places the
  points. Spherical harmonics of degree 1–3 are kept apart (`sh.bin`, 8 bits each) and used from stage G3.
- **Our own renderer, no dependency.** Splatting is a published method (Kerbl et al. 2023, the EWA projection of a 3D
  Gaussian); Lapin draws it with three.js as instanced quads, the 2D covariance worked out in the vertex shader, sorted
  back to front in a Web Worker (a 16-bit counting sort of the depths, again when the camera has turned or moved enough),
  blended premultiplied. It is clipped by the view's planes, halved for halftone and held to a budget by the display
  quality like the points, so it sits in Lapin's viewport as the clouds do. No library (Spark, GaussianSplats3D,
  PlayCanvas): each would bring a second scene graph beside ours, and the clipping, budget and picking would be theirs.
- **Formats after their public specifications**, Lapin's own C# readers, managed only (the browser's engine has them too):
  3DGS PLY (INRIA's layout: `x y z`, `f_dc_*`, `f_rest_*`, `opacity` as a logit, `scale_*` as logarithms, `rot_*` as a
  quaternion w x y z), SuperSplat's compressed PLY (`chunk` and packed `vertex` elements), Niantic's SPZ (gzip, versions
  2 and 3; MIT spec), PlayCanvas's SOG (a zip or folder: `meta.json` and lossless WebP images; WebP lossless decoded by
  Lapin's own reader after RFC 9649). Nothing GPL or LGPL.
- **One rule for the view mode.** A link's own default (Splats or Points) on the Links tab, overridden per view on the
  Point clouds tab of Visibility / Graphics (Splats / Points / By link). Plans and sections draw the slice as dots, as for
  any cloud; a 3D view and a camera view draw splats.
- **Scale is the link's.** Calibrate (two points picked on the cloud, the true distance typed) writes a factor on the link's
  placement, beside its unit; it applies to the points, the splats, the slices and the snaps alike, and is undone in one
  step. It works for every point cloud, not only splats.

## Stages

- **G1 — link and draw.** `GaussianPlyReader` and `SpzReader` (an `IPointReader` each, so the index takes them; plus the
  splat records for `splats.bin`); `PlyReader` hands a 3DGS file to the new reader; `pointcloud.link` takes `.spz` and
  marks `splats`; `pointcloud.splats` sends a link's Gaussians in pages as raw arrays (positions placed in the project,
  scale × the link's factor, rotation turned by the link's, colour, opacity), the protocol's binary frame as for tiles;
  `ui/src/viewport/splats3d.ts` (the renderer, its sort worker, the budget: the largest splats first when the budget is
  smaller than the file); the Links tab's Display: Splats / Points; File → Link point cloud… lists `.spz`; V/G's Point
  clouds tab gets the mode column; the guide (import-export.md, both languages), the scene `gaussian-splat`, Ask Lapin.
  Tests: the readers on small files written by the tests (PLY binary, SPZ v2 and v3), the transform of a Gaussian (a
  rotated, scaled link gives the covariance expected), the renderer's covariance and sort functions in vitest.

  **Done 23.11.2026 (decision 809, branch `feat/gaussian-splats`).** Built as above, SPZ version 1 read too (float16
  positions); version 4 (ZSTD streams, new since the plan) is refused with a message. Decided where this plan was silent:
  an **up axis** on every cloud's placement (`placement.up`: `z`, `y`, `-y`; `-y` by default for a 3DGS PLY, COLMAP's axes,
  `y` for an SPZ, RUB), applied before the placement mode and asked in the place dialog, without which every capture lay on
  its side; a budget of 1, 2 or 4 million splats by display quality (half in a browser), shared by the clouds shown, the
  choice made from a byte of rank a Gaussian (`rank.bin`, log₂ of opacity × mean area) without sorting; Σ = R S² Rᵀ worked
  out once in the interface and kept as halves in an integer texture; the sort again when the view turns more than about
  1° or the eye moves more than 1 % of its distance; a cloud drawn as splats keeps its point tiles, not drawn, for the
  snaps; each cloud sorted on its own (two overlapping captures do not interleave exactly). Measured (Release, M-series
  Mac): a 68 MB 3DGS PLY of a million Gaussians indexed with its splats in 0.9 s, the same as SPZ v3 (9 MB) in 0.4 s; a
  budget of 500 000 chosen and read in 17 ms; the guide's scene (41 407 Gaussians) drawn and sorted at once. The first
  render drew nothing: the quad's minor axis was turned clockwise from the major, so every quad was a culled back face;
  the shader turns it anticlockwise now and draws both sides. Tests: `SplatTests` (7), `splats3d.test.ts` (8).
- **G2 — calibrate and measure.** **Calibrate scale…** on a cloud's row and on its context menu in 3D: two points snapped
  on the cloud, the true distance typed (in the project's unit), the factor written on the link (`pointcloud.place` with
  `scale`); the unit check takes it into account. Measure: snapping to splat centres in 3D and an **Area** mode (a closed
  polyline of snapped points: its area and perimeter in its best-fit plane) and a chain of distances, if Measure does not
  have them already. A cloud's **crop box** (the part of a scan kept; outside hidden in every view), since splats carry
  floaters around the subject.
  **From the forum** (a user pointed to MeshSplatting, which trains opaque triangle meshes instead of Gaussians): a **PLY
  mesh** (a `face` element, vertex colours) taken by Import mesh with Lapin's own reader, and Link point cloud… given a PLY
  with faces says it is a mesh and offers the mesh import instead.

  **Done 23.11.2026 (decision 810, branch `feat/gaussian-splats`).** `pointcloud.calibrate {id, a, b, length}` multiplies
  `placement.scale` by the ratio and moves the offset so that the first point stays put (one undo step; `pointcloud.place`
  takes `scale` too, the place dialog shows it as Scale factor); `TransformOf` multiplies the unit by it, so points, splats
  (centres and Gaussian sizes), slices, snaps, hierarchy boxes and the mesh exports follow; `UnitWarning` counts it. The tool
  (`pointcloud.calibrate`, Place3D with heights, the length typed at the cursor or asked for after) starts from the ruler on
  the cloud's row and from the 3D view's right-click near a cloud (`cloudAt`: the cloud whose drawn point is nearest the
  ray). The crop is `crop: {center, size, rotation}` on the link (`CropBox`, project coordinates, turned about z;
  `pointcloud.crop` with a box, `fit` or null), applied where the engine delivers: tiles, splat pages, slices, export
  samples, the hierarchy's box; no re-indexing, so a crop changes nothing in the cache. Measure got a mode: Distance (the
  chain, its running total in the 3D chip) and Area (a polygon; Newell's normal gives the area in the best-fit plane, the
  perimeter and the plane's tilt), its points kept at their snapped heights in 3D (`inSpace`, `liveInfo` on ToolDef). Snapping
  to splat centres in 3D already worked (the point tiles stay loaded, not drawn); the guide's scene now proves it by moving
  the cursor over the capture. The PLY mesh reader (`Lapin.Mesh/PlyMesh.cs`: ascii and binary both ways, faces fanned)
  gathers faces by colour, 3 bits a channel, into the parts of one element (at most 512), and Import mesh offers −y up for a
  3DGS capture. Decided where the plan was silent: calibration scales about the first point, not the file's origin; the
  crop box is in project coordinates (it stays put when the cloud is moved; Fit to cloud aligns it with the link's turn);
  no grips for the crop in 3D yet (numbers on the row); in a plan Calibrate takes both points at the level's height.
  Measured: the scenes calibrate a capture linked at half size back to a factor within 2 % of 1 by two clicks on splat
  centres, and measure the pavilion's back wall at 15.21 m² (the snapped corners' 5.86 × 2.60 m). Tests:
  `SplatCalibrateCropTests` (5), vitest (3 more in `measure.test.ts`); the scenes `gaussian-splat-calibrate`, `-crop`,
  `-area` in both languages.
- **G3 — the other formats and the colour by direction.** SOG (with the WebP lossless reader), SuperSplat's compressed
  PLY; spherical harmonics of degree 1–3 in the shader (Display: Full colour / Base colour), behind the display quality.
- **G4 — on sheets.** A 3D or camera view with splats placed on a sheet prints them as a raster made by the engine (a CPU
  splatting of the view at the sheet's resolution, the same sort and projection as the screen's), under the view's lines,
  dimensions and notes; the PDF carries it. Dimensions and notes in a 3D view on a sheet as they are today.
- **G5 — Lapin for the browser.** The `.html` export (`docs/html-export.md`) carries a splat link's Gaussians (SPZ inside
  the page) and draws them with the same renderer; its viewer gets Measure (distance, chain, area). A combined BIM + cloud +
  splat page is that export with splats, not a new format.

Later, if asked: editing splats (selecting and deleting floaters as SuperSplat does), training from photos (that is
Lichtfeld Studio's or Postshot's job, not Lapin's), exporting splats; grips for the crop box in 3D. Two methods from the
forum: MeshSplatting (opaque triangle meshes trained like splats: its PLY output is read since G2, by Import mesh) and
Spaghetti Splatting (no file format of its own: nothing to read until it has one).
