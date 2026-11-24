# Gaussian splats

Asked on the forum (topic 237, "Gaussian Splat Support", 23.11.2026) and accepted by the user the same day ("I'm in, will do it").
Decision 812 and the following, one per stage, in `docs/decisions-pending.md`.

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
  1–3, and version 4 of Zstandard streams; MIT spec), PlayCanvas's SOG (a zip or folder: `meta.json` and lossless WebP images; WebP lossless decoded by
  Lapin's own reader after RFC 9649; Zstandard after RFC 8878). Nothing GPL or LGPL.
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

  **Done 23.11.2026 (decision 812, branch `feat/gaussian-splats`).** Built as above, SPZ version 1 read too (float16
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

  **Done 23.11.2026 (decision 813, branch `feat/gaussian-splats`).** `pointcloud.calibrate {id, a, b, length}` multiplies
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

  **Done 24.11.2026 (decision 830, branch `feat/gaussian-splats-g3`).** Two decoders of Lapin's own, managed only, after
  their RFCs: `WebPLossless` (RFC 9649: RIFF, the four transforms with pixel bundling, simple and normal prefix codes, meta
  prefix codes, the colour cache, LZ77 with the 120 neighbourhood distances; a lossy VP8 image refused by name) and `Zstd`
  (RFC 8878: raw, RLE and compressed blocks, Huffman literals by direct or FSE weights in one or four streams, FSE sequence
  tables in every mode, repeat offsets, skippable frames; no dictionaries, the checksum not checked). Readers: `SogReader`
  (PlayCanvas's published specification, versions 2 and 3: a `.sog` zip or a folder's `meta.json`, means in a 16-bit
  symmetric log, scales and colours by codebook, version 3's exponential-map rotations and (a/204)² opacities, version 2's
  smallest three; the harmonics' palette and labels), `CompressedPlyReader` (SuperSplat's chunks of 256 with their bounds,
  colour bounds when present, 11-10-11 positions and log scales, the smallest three in 2 + 3 × 10 bits, the `sh` element's
  bytes over ±4) and `SpzReader` version 4 (the 32-byte header, extension records stepped over, the table of contents,
  each attribute its own Zstandard stream). All three fill `sh.bin`. `pointcloud.splats` takes `sh` (the degree wanted) and
  `shBytes` (the room for it): the page carries the harmonics as `sh.bin` keeps them, packed in ints, and `turn`, the 3 × 3
  matrix taking a project direction into the file's (the placement's linear part inverted, its scale taken out), so the
  shader evaluates the colour (Kerbl et al.'s real basis with the Condon–Shortley phase, `splatSh.ts`, the engine's
  `Splat.ShBasis` and `Splat.Colour` the reference) along the eye-to-splat direction in the file's frame whatever the link's
  rotation and up axis. Decided where the plan was silent: `splatColor` (`full` by default, `base`) on the link (a list
  beside Display in Manage → Links) and on a view's row (V/G's **Splat colour** column); the degree by quality (Fast 1,
  Balanced and Precise 3), lowered by the engine (`SplatStore.DegreeFor`) until the kept splats' coefficients fit the share of
  96 MB (48 MB in a browser) of the clouds shown, so Balanced's 2 million keep degree 3 and Precise's 4 million degree 2; the
  harmonics in a second RGBA32UI texture, 1–3 texels a splat; Base colour asks for none (memory and load time saved); SOG's
  first layout (no `version`, before 2) refused with a message, its opacities above 1 kept at 1, blind data left aside;
  a SOG folder's link takes the folder's name; the Links row names the format in a few words ("SOG v3 · SH 3"). Measured
  (Release, M-series Mac, a synthetic capture of a million Gaussians with harmonics of degree 3, random so hardly
  compressible: one palette entry a splat): the SOG (images by cwebp, 58 MB) indexed with its splats in 1.1 s, the SPZ
  version 4 (streams by zstd -3, 59 MB) in 0.8 s, as G1's PLY and SPZ v3. The guide's SOG (41 407 splats, its back wall
  given degree 1) draws the wall warm from the garden, cool from behind, white in Base colour. Tests: `WebPTests` (3),
  `ZstdTests` (4), `SplatFormatTests` (9: each format read back as the 3DGS PLY's Gaussians within its quantization, the
  basis orthonormal, a page's harmonics, degree and turn), vitest `splatSh.test.ts` (5); the scene `gaussian-splat-sh`.
- **G4 — on sheets.** A 3D or camera view with splats placed on a sheet prints them as a raster made by the engine (a CPU
  splatting of the view at the sheet's resolution, the same sort and projection as the screen's), under the view's lines,
  dimensions and notes; the PDF carries it. Dimensions and notes in a 3D view on a sheet as they are today.

  **Done 24.11.2026 (decision 831, branch `feat/gaussian-splats-g4`).** What existed: a saved 3D view (orthographic or a
  camera's perspective) already went on a sheet (forum topic 91, decision 574), drawn by `View3dDrawings` with the exact
  hidden-line removal of an elevation, lines only (no hatches, no annotations, since a perspective's drawing is not in the
  model's frame), and point clouds were drawn only in plans, sections and elevations (`PointCloudSlices`: a PNG laid under
  the drawing, carried by its `PenImage`, which `Sheets.ClipToBox` puts on the paper and `PdfWriter` writes as an image
  XObject, Flate, its alpha a soft mask). G4 adds the 3D view's own picture through that path: `SplatRaster` (the CPU
  splatter: Σ = (M R) S² (M R)ᵀ placed by the link, Σ' = J W Σ Wᵀ Jᵀ in the picture's pixels plus the screen's 0.3 px²
  low-pass, weighed to 3σ, nothing under 1/255, blended premultiplied back to front by the centres' distances; the
  perspective's map is View3dDrawings' own, x·D/d, y·D/d, so the picture lies exactly under the lines) and `SplatSheets`
  (the view's links drawn as splats by V/G or the link, their Gaussians placed, cropped by the link's box and the view's
  section box, cut at a perspective's near plane, coloured Full along the eye's ray with `Splat.Colour` at the file's own
  degree or Base, halftone; the grid round the Gaussians' 3σ reach, inside the camera's window, 300 dpi on the paper at the
  view's scale, coarser past 24 Mpx or 8192 px a side; at most 4 million Gaussians, the largest and most opaque first as the
  screen's budget), called by `Views.Render` for a 3D view as a mark under the drawing (`Under`, its id
  `pointcloud:<link>:splats:<hash>`, so the view's bounds and the viewport's box count it). Decided where the plan was silent:
  **occlusion by a depth buffer** of the drawn parts' faces (triangulated, in the drawing's depth: the orthographic frame's,
  or a perspective's mapped D − D²/d, planar, so interpolated exactly), a splat drawn where its centre is in front of the
  nearest face (2 cm of tolerance, so a capture's wall on the model's wall still shows), as the 3D view's depth test with
  depthWrite off: the hidden-line drawing has no fills, so without it a capture would show through the model; the picture
  worked in bands of 32 rows, each band its splats and faces, in parallel (sequential in a browser), so memory is the RGBA
  picture and a band's floats; **one resolution**, 300 dpi, for the sheet on the screen and the PDF (the same cached
  picture: the screen shows what prints); **synchronous, cached** rather than drawn in the background with a placeholder:
  a million Gaussians over a whole A3 take about 2.7 s once, then the picture is kept (8 of them) by link, transform,
  camera, window, scale, colour, halftone, crop, section box and a hash of the parts' vertices, so a sheet redrawn after an
  unrelated change takes it again; a cloud drawn as Points in a 3D view is not printed (the 3D views on sheets drew no clouds
  before). Measured (Release, M-series Mac; `SplatSheetBenchTests`, LAPIN_SPLAT_BENCH=1): a million Gaussians of degree 3 on
  the walls and floor of a 12 × 8 × 4 m room (scales 3 mm–6 cm) gathered and coloured in 0.25 s, splatted on 4724 × 3272
  px (an A3's 400 × 277 mm at 300 dpi) in 2.16 s, the PNG (22.5 MB, random colours, the worst case) in 0.27 s; the guide's
  pavilion (41 407) at once. Tests: `SplatSheetTests` (6: a single Gaussian's footprint and second moments against the
  analytic ellipse, two overlapping splats blended back to front whatever their order, a face hiding the one behind, the
  perspective's Jacobian, crop and section box and Full/Base colour against `Splat.Colour`, a 3D view on a sheet: the
  picture's size at 1:50, the model's wall hiding the capture behind it, under the lines, cached, the PDF's image XObject,
  Base colour and Points in the view's row; a camera view inside its field of view); the scene `gaussian-splat-sheet` (the
  pavilion's camera view with a garden wall in front, a note and a dimension on the paper, its PDF), both languages.
  Left: a capture drawn as points in a 3D view printed as dots; a lighter picture for the screen when a sheet's picture is
  large (a whole A3's PNG of a random capture is 22 MB in the sheet's drawing message).
- **G5 — Lapin for the browser.** *Postponed by the user's choice (24.11.2026): not built after G4; taken up when asked.* The `.html` export (`docs/html-export.md`) carries a splat link's Gaussians (SPZ inside
  the page) and draws them with the same renderer; its viewer gets Measure (distance, chain, area). A combined BIM + cloud +
  splat page is that export with splats, not a new format.

Later, if asked: editing splats (selecting and deleting floaters as SuperSplat does), training from photos (that is
Lichtfeld Studio's or Postshot's job, not Lapin's), exporting splats; grips for the crop box in 3D. Two methods from the
forum: MeshSplatting (opaque triangle meshes trained like splats: its PLY output is read since G2, by Import mesh) and
Spaghetti Splatting (no file format of its own: nothing to read until it has one).
