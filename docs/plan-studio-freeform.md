# Studio: free-form surfaces (SubD and NURBS), the ideas worth keeping

Written 21.11.2026 after reading [`cekuhnen/blender-nurbs`](https://github.com/cekuhnen/blender-nurbs), credited as the inspiration in the user guide (Studio → SubD solids); ideas and behaviours only, no code ("NURBS Proto", a Blender add-on on NVIDIA's USD BRep kernel), its README, development notes and kernel build. The user asked for the good ideas to be noted, and for free-form surfaces in Studio to be the use. Status: §0–§6 are ideas; **§7–§9 are the plan the user approved on 21.11.2026** (decision 780: F1 SubD in C# first, then F2; the terrain keeps its TIN and gains a SubD design surface).

## 0. The rule for this document

- The add-on is **GPL-3.0-or-later** (its manifest and README). Lapin is MIT. **No code, no file, no data structure from it enters Lapin**; it is read as a list of *behaviours*, reimplemented from the behaviour and from public mathematics (Piegl & Tiller's *The NURBS Book*, Catmull–Clark 1978, Stam's exact evaluation, ISO 10303 for STEP), as with `docs/transfer.md`.
- The kernel it stands on, **USD BRep** (NVIDIA, Apache-2.0, C++ with Python bindings only), is a separate question: a possible dependency, to be evaluated on its own (§5). Its licence notice says "except where otherwise noted", so the licence of every SMLib component has to be read before any use.
- Reviewers: do not read the add-on's Python while writing the Lapin version of an item; write from this list.

## 1. What Studio has today

From `plan-studio-editor.md` and `plan-studio-parametric.md` §1:

- The kernel (`Lapin.Geometry.Solid`) is a **closed polyhedron with planar faces**. Curved surfaces exist only **tessellated** (`Chords`, the one rule for every curve); there are no exact analytic or B-spline surfaces. Booleans come from manifold (a mesh library).
- Solids: extrude (tapered, symmetric, up to a face), sweep (placed, morphing, two rails), loft (`smooth`: Catmull–Rom through the profiles, 12 stations a span; a guide rail), revolve, primitives, chamfer and fillet on edges, shell, slice, push/pull, move/rotate/mirror.
- STEP out writes facets unless the recipe knows the surface; STEP in tessellates (`IfcAdvanced.cs` reads B-spline surfaces and curves of IFC4 advanced B-reps, but only to a mesh).
- **Missing, and said so:** no offset of a solid, no fillet between curved faces ("left for later if a case asks for them"), no surface that is edited by pulling a control point, no continuity control between surfaces.

## 2. SubD and NURBS against what Lapin has

| | Today (facets) | SubD (Catmull–Clark cage) | NURBS surfaces and B-rep |
|---|---|---|---|
| Exact curved geometry | no: a circle is a polygon at the chord tolerance | no: a smooth limit surface of a mesh cage (exact as a surface, not as an analytic circle or cone) | **yes**: circles, cones, spheres, revolutions exactly; free-form by control points and knots |
| Editing a smooth form | by the profile or the rails of each step | **very good**: move a few cage points, the whole surface follows; fast to learn (Blender, Rhino SubD, Fusion Form) | good but fiddly: grids of control points, patch layout, seams |
| Continuity between neighbours | none (facets) | G2 almost everywhere by construction; at "star points" (an irregular vertex) G1 only unless special rules | chosen per edge: G0, G1, G2, G3 against the neighbour; needs matching and checking |
| Booleans, fillets, offsets | manifold booleans on the mesh; no offset, no curved fillet | booleans on the tessellated result only; offset and fillet are not natural | exact intersections and trims, true offsets (thicken) and rolling-ball fillets, in a real kernel |
| Drawings and sections | cut of the tessellation (chord tolerance) | the same, on the tessellation | exact section curves, so plans, sections and elevations of a curved roof or facade are exact lines |
| Fabrication and exchange | facets | convert to NURBS to exchange (hard: star points, see §4) | STEP / IFC advanced B-rep **exactly**; what steel benders, panel makers and formwork need |
| Cost to Lapin | none | **low**: a Catmull–Clark subdivider is a few hundred lines in C#; the result is a closed mesh that the present kernel and manifold already take | **high**: a B-rep kernel (trimming, intersection, tolerance, healing). Either years of work or a dependency (USD BRep, OCCT) |
| Fits a BIM model | good for massing, canopies, sculptural roofs and facades, furniture | | needed once the output is fabricated or must be dimensioned exactly |

Plain reading: what Lapin lacks is not smoothness (facets at a fine tolerance look smooth) but **editing a free form directly** and **exactness for drawings, quantities and exchange**. SubD gives the first cheaply. NURBS gives the second, at the cost of a kernel. They are not rivals: the reference add-on itself edits in SubD and converts to exact NURBS bodies for CAD.

### Suggested order (to be decided by the user)

1. **SubD, in C#, in Studio** (stage F1): a cage solid, edited by its points, edges and faces; creases; the result is a closed `Solid` at the chord rule, so booleans, sections, quantities and IFC mesh export work as they do now. Cheap, visible, useful at once.
2. **Exact surfaces we can write ourselves** (F2): ruled, loft and revolve surfaces kept as their definition (B-spline in, B-spline out) beside the tessellation, so STEP and IFC advanced B-rep export are exact for what Studio already makes. No trimming, no general intersection.
3. **A real NURBS kernel** (F3): only if a case needs trimmed free-form faces with fillets and exact intersections. Then evaluate USD BRep (Apache-2.0) as an optional native module behind a C wrapper (as `manifoldc`), never in the MIT core by default; see §5.

## 3. The ideas, by area (each marked: **A** very useful for BIM, **B** useful, **C** later or other trades)

### Construction history
- **A** A solid or surface remembers how it was built; moving a curve, changing a setting or a cutter rebuilds everything made from it (Studio already does this for its own features, §1 of the parametric plan).
- **B** A **construction tree**: what an object is built from, and what uses it; removing a feature steps back and returns its inputs.
- **B** Pause rebuild (for heavy models), delete history, collect inputs.
- **B** Live feature settings (continuity, weights, degree, spans) on the object's panel.

### Curves
- **A** Draw: CV curve, lines with tangent arcs, arc, circle, ellipse, rounded rectangle, polygon, slot, helix (pitch, taper, hand), profile.
- **A** Between curves: **blend curve** (with continuity), **fillet that trims and joins**.
- **A** From curves: **offset** (individual or joined; sharp, round or chamfered corners); **offset on a surface**, measured along the surface.
- **A** From surfaces: iso curve, duplicate edge, project curve, intersect.
- **B** Edit: rebuild (fewer points, fixed degree), extend, align, trim, split, join, unjoin.
- **A** **Snapping** on curves, surfaces and meshes (vertex, edge, edge centre, face centre; onto the face), **smart guides** (aligned, tangent, parallel, perpendicular), sketching on the face you start on; Alt free, Ctrl grid.

### Surfaces
- **A** **Loft** through curves or edges with a continuity to the surfaces at its edges (G0 to G3); **loft with rails**; **curve network** (a grid of crossing curves or edges, G0/G1/G2 to the surfaces around); revolve; **rail revolve**; sweep; pipe.
- **A** **Extrude** curves or edges into walls; a face into a solid joined to its body, cut from it or new, **with draft** on flat faces.
- **B** **Flange**: from a free edge or a curve on a surface, at an angle to the surface or at a constant draft to a pull direction; the base is the edge exactly.
- **A** Planar surface from a closed curve, **dome**, **boundary** surface (from edges), **patch** (a surface fitted through points and curves).
- **A** **Offset** and **thicken** a surface (this is what Studio lacks: a roof or a facade skin with a thickness).
- **B** **Pattern**: linear, circular and along a path, with real copies that follow their source and path.
- **C** **Unroll** a developable surface flat (cones, cylinders, folded sheet; trims and curves come along) and **flow** curves or surfaces from one surface onto another: sheet-metal cladding, roof flashings, panel patterns; also folds a drawing on the flat pattern back onto the part. Worth a note for metal roofing and cladding.
- **B** **Merge patches**: a 4-sided grid of patches rebuilt as one surface in three modes (exact, reduce knots within a tolerance, refit with the fewest spans of a chosen degree), outer edges holding free/G0/G1/G2, with gap, angle and curvature checks.

### Editing surfaces
- **A** **Edit by control points** (Tab toggles object and CV editing), with a nudge by step along a direction or the normal.
- **A** **Match surface** and **surface align** to a neighbour at G0/G1/G2; **extend surface**; **untrim**, split at an iso line, join, unjoin, flip normal.
- **B** **Refit to trim**: rebuild a trimmed patch tightly to its trim.

### Fillets and solids
- **A** **Edge fillet** (circular, chamfer, blends G1 to G3), **soft fillet** (curvature ramped G2/G3 with an exact minimum radius) and corner fills, **surface fillet** between two surfaces, variable-radius.
- **A** Union, difference, intersection; **push/pull** of a face; **replace face** with a surface; **boundary fill** (a solid from enclosing surfaces).

### Analysis (draw the problem, not only the model)
- **A** **Zebra stripes**, **curvature map**, **draft angle**, matcap: the usual way to see whether a surface is smooth.
- **A** **Continuity check** between surfaces (G0 gap, G1 angle, G2 curvature difference) and **feature labels** on the edges showing the continuity each one holds, live, clickable to change.
- **B** **Curvature comb** on curves, surface combs, **dynamic section** (a moving cut plane through the model).
- **B** Measure: distance with ΔX ΔY ΔZ, radius and diameter of circles, edges and faces (exact for true circles, else radius of curvature; a face's two principal radii).
- **B** Display: CV hulls, spans (the knot lines of each face, clipped to its trims), fine grid.

### Convert and fit
- **A** **Mesh to solid** with recognition (flat faces as planes, round holes and cylinders exact, bevels as fillets of their radius): this is the same idea as Lapin's IFC recognition (decision 129) applied to meshes.
- **B** **SubD to NURBS body**: one bicubic span per regular face; star points as even 2×2-piece faces solved so every join is exact (G2 at degree 6, G1 at degree 5). Elegant and costly; only if F3 happens.
- **B** **Fit to scan** (point clouds are in the platform already): fit a SubD cage to a scan so the *smooth surface*, not the vertices, lies on it; offset or drape variants; **scan deviation** overlay (colour by distance from the scan). Fits as-built surveys (terrain, roofs, façades).
- **A** **BREP to mesh** at low/medium/high detail (n-gons on flat faces, quads on curved), welded, with creases, UVs and seams: the render and game export that Lapin's mesh exports (`plan-mesh-export.md`) should offer.

### Exchange
- **A** STEP in and out with **exact** analytic entities (planes, cylinders, cones, spheres, tori, revolutions, extrusions, offsets, circles, ellipses, lines) and B-spline surfaces; closed bodies as solids; units and assembly placements converted. In Lapin this is `plan-studio-parametric.md` X1/X2 plus F2.

## 4. Things the reference teaches by what it had to do

- **Star points are the whole difficulty of SubD to exact surfaces.** Regular quads are one bicubic patch each (exact); every irregular vertex needs extra work. For Lapin's F1 this does not matter (the result is a mesh), and it argues against promising exact export of arbitrary SubD.
- **The kernel was the work.** About 100 000 lines of Python sit on a C++ kernel that does booleans, fillets, healing and STEP. Writing that in-house is not a stage but a project; its own known-issues list (healing can report success when a repair failed; tori and spheres with missing seams) shows the long tail.
- **Windows and Linux builds of a kernel are slow and fragile** (about 50 minutes for Windows in its CI; math functions that behave differently past a range's end; a wrapper had to harden them). Any native dependency of Lapin carries this cost on three platforms, as `manifoldc` already does.
- **Continuity needs to be seen to be trusted**: labels and a check beside every edge, live, are what make G1/G2 usable. Build the check with the surface, not after.
- **Mesh recognition and the 1 mm / 0.5 % rule** are shared concerns: a recognised exact surface that differs from the mesh by more than the tolerance must stay a reference (`RecognitionFidelity`).

## 5. If the kernel question is opened (F3)

- **Candidate:** USD BRep (NVIDIA, Apache-2.0): NURBS B-rep with booleans and healing, stored in USD, with converters to and from OpenCASCADE (its own STEP support not checked); C++ and Python APIs only. It would need a **C wrapper** (as `manifoldc`), native builds per platform (macOS arm64, Windows x64, Linux x64), and must not drag OpenUSD in (the reference add-on builds without it, which shows it can be done).
- **Conflicts to settle first:** the standing principle "exchange formats are written after their public specifications, with no dependency" (STEP would keep its own writer); the licence of each SMLib component; size of the package; how it sits as an optional module (`requires`), never in the MIT core by default; its maturity (public since 2026, and the notes list known healing issues).
- **A prototype before any decision:** the wrapper for three calls (loft of two B-spline curves into a face, a boolean, a tessellation), built on the three platforms, measuring size, build time and time of a boolean on a facade-sized model.

## 6. Open questions for the user

1. F1 first (SubD in C#, no dependency)? Which cases are real: a sculptural roof, a canopy, a facade skin, furniture, a landscape form?
2. Is exactness needed in drawings of curved elements (exact section lines of a curved roof), or is the tessellation at the chord rule enough?
3. Is fabrication data (curtain-wall panels, formwork, bent steel) something users will ask for? That is what decides F2 and F3.
4. Do the thickness and offset of a free-form surface come before editing it (a skin of 20 cm on a curved roof is the first thing an architect asks for)?

## 7. The terrain and SubD (the user's question, 21.11.2026)

- **The surveyed terrain stays a TIN.** Catmull–Clark approximates: its surface does not pass through the cage points, so it would move the surveyor's heights; cut and fill are measured on the TIN's triangles, as authorities and contractors expect; breaklines, boundaries with arcs and platforms are constraints of the triangulation, which a quad cage does not follow; a survey has tens of thousands of points.
- **Where SubD helps the ground: the designed ground.** Mounds, gentle banks, swales, a shaped garden have no measured heights to keep. A small open SubD cage edited by a few points gives a smooth form; the terrain takes it as a **design surface** (as a platform is one): inside its outline the proposed ground is the SubD surface, sampled onto the TIN, and cut and fill are measured between it and the surveyed ground, as they already are for platforms (stage F1e).
- **Later, optional:** Loop subdivision of a sparse TIN for display (smoother contours), the breaklines as creases, always labelled as smoothed and never used for quantities.

## 8. F1: SubD in Studio (C#, no dependency)

### 8.1 What the user gets
- **SubD solids** in Studio, from a cage: a box, a cylinder (n sides), a plane grid (open), or an existing Studio solid's mesh as a cage (small ones only).
- **Edit the cage** in the 3D view: pick vertices, edges or faces (the cage drawn as a wireframe hull over the smooth body), move them with the 3D snaps and the axes (stage A of `plan-3d-modelling.md`), scale and rotate about their middle; typed distances.
- **Shape the cage:** extrude faces, insert an edge loop across a ring of quads, delete faces (an open surface), **creases** on edges (sharpness 0 smooth … whole numbers semi-sharp … ∞ sharp), **mirror symmetry** across a plane (edit one half, the other follows).
- **An open surface gets a thickness** (`thickness`, one side, the other or centred): a roof or facade skin of 20 cm is one property.
- **Levels:** the display and the model use the chord rule (`Chords`): the body is subdivided until it lies within the tolerance of the limit surface, capped (default 4 levels, and a face budget); one rule for drawings, quantities and exports, as for every curve.
- Each edit is one undo step; the body keeps its GlobalId when edited.

### 8.2 The engine
- **`Lapin.Geometry/SubD.cs`** (new, public mathematics only: Catmull & Clark 1978; creases after DeRose, Kass & Truong 1998; limit positions by the standard limit masks): a cage (points, polygons of any side count, creases), one subdivision step, limit positions, normals, the boundary rule (an open border is a crease: it stays a cubic B-spline of its border points), and the result as triangles in a closed `Solid` (or an open shell, thickened into a solid). Validity: manifold cages only (each edge in one or two faces), refused otherwise with a reason.
- **Studio operation `subd`** (`StudioModule.Geometry.Base`, beside `primitive` and `body`): attributes `cage` {points, faces, creases}, `levels` (optional cap), `thickness`, `side`, `symmetry` (a plane or none), `at`, `rotation`. Booleans, cut priorities, features (fillet, shell, slice…), quantities and IFC export work on the body as on any Studio solid.
- **Commands** (one undo step each; catalogue entries): `studio.subdCreate` {shape: box | cylinder | plane | fromSolid, sizes, at}, `studio.subdEdit` {id, moves: vertices, edges or faces by index with a translation, scale or rotation about their middle}, `studio.subdOp` {id, op: extrude | loop | delete | crease, …}, `studio.subdSet` {id, levels, thickness, side, symmetry}.
- **IFC:** the body tessellated as now; the cage kept in Lapin's own property set (`Lapin_SubD`, never `Pset_`) so Lapin and BubbleBIM read it back editable.
- **Tests** (`SubDTests`): a cube cage converges to a known limit (volume within bounds, symmetric); fully creased cube stays a cube; one face's crease keeps its edge straight; an open plane cage keeps its border curve; closed cages stay closed and manifold (Euler characteristic kept); levels by the chord rule; a thickened open grid has the thickness everywhere (to the tolerance); refused non-manifold cages; symmetry gives a mirrored body; edits keep the GlobalId.

### 8.3 Stages
- **F1a** engine core `SubD.cs` and its tests (Opus).
- **F1b** Studio `subd` operation, commands, IFC property set, `.lapin` round trip, tests (Opus).
- **F1c** interface: Studio → SubD (box, cylinder, plane), the cage edit mode in 3D (pick vertex/edge/face, move, scale, rotate, extrude, loop, delete, crease), the properties card (levels, thickness, side, symmetry), strings in every language, the user guide page and its scene (Sonnet, with the engine's commands).
- **F1d** thickness and symmetry polish, from what F1c shows.
- **F1e** the terrain's **design surface**: a SubD open surface named as the proposed ground of a terrain, sampled inside its outline, cut and fill against the surveyed ground (Civil; Opus for the engine).

### 8.4 What F1a and F1b did (21.11.2026)
- **`Lapin.Geometry/SubD.cs`:** `SubDCage` (points, faces of any side count, `SubDCrease(a, b, sharpness)`), `SubD.Problem` (manifold, one orientation, one fan of faces round each point, indices, crease edges), `SubD.Step`, `SubD.Limit`, `SubD.Build(cage, SubDOptions(MaxLevel, Tolerance, FaceBudget, Thickness, Side))` → `SubDResult(Solid, Level, Deviation, Closed, Problem)`, `SubD.Mirror` and `SubD.Partners`. Sharpness 10 or more (`SubD.Infinite`) is sharp for good; children of a semi-sharp edge take s − 1 (the plain rule of DeRose et al., not the Chaikin-smoothed one).
- **The chord rule, as built:** the deviation of a level is measured where the next level puts limit points (each edge's middle against its chord, each face's middle against its corners' mean); it stops at the first level within `Chords.Tolerance`, at least once and at least as many levels as the sharpest semi-sharp crease, at most 4 (`levels`, up to 6) and 200 000 quads. The smooth unit cube stops at the cap (4 levels, 1.76 mm off): its deviation falls 3.5 times a level while regular quads lead and only 2.4 times once the valence-3 corners lead, so 1 mm on a 1 m cube needs a fifth level; the cap wins, the deviation is reported (`studio.subdCage` → `deviation`). A semi-sharp crease left above 0 at the cap is pushed to its limit by blending the smooth and crease masks (an approximation, as is the limit normal at a semi-sharp point).
- **`Lapin.Geometry/SubDCages.cs`:** `Box`, `Cylinder` (its caps' rims creased by default, so the ends stay flat), `Plane`, `FromSolid` (outer loops, no holes, at most 2000 faces), `Compact`, `PointsOf`, `Move`, `Extrude` (creases inside the region go up with it; those on its rim stay and are copied to the lifted rim), `InsertLoop` (the ring both ways until it closes, meets a border or a face that is not a quad, which takes the new point), `DeleteFaces`, `SetCreases`.
- **Thickness:** an open cage offset along its limit normals (out: the surface and above it; in: below; centre: half each way), quads kept whole where flat, borders stitched. The volume follows Steiner (A·t·(1 ± H·t)); a closed cage ignores `thickness` (no hollow SubD yet).
- **Studio:** operation `subd` in `StudioModule.SubD.cs`; the cage is in the solid's own frame (`at`, `rotation`), the body cached per attribute object. The symmetry is kept on the cage itself: setting it mirrors the cage (`SubD.Mirror`, the positive half kept unless `keep: negative`; refused when a face crosses the plane, so a box needs `segments: [2, 1, 1]` for x), an edit moves the picked half and sets every partner from it, an op mirrors again from the half it was done on (points and faces renumbered). An open surface without thickness is drawn, measured by its area only and cuts nothing (`IsOpenSurface`). `place`, plan moves and storey shifts move it as a primitive.
- **Commands** (each one undo step, GlobalId kept): `studio.subdCreate {shape: box|cylinder|plane|fromSolid, at, storeyId?, rotation?, class?, name?, width, length, height, radius, sizes?, segments?, sides?, sourceId (fromSolid; the source stays), thickness?, side?, levels?}`, `studio.subdEdit {id, vertices?, edges?, faces?, scale?, rotate?, translate?}` (world coordinates, about the selection's middle, in that order), `studio.subdOp {id, op: extrude {faces, distance} | loop {edge, fraction} | delete {faces} | crease {edges, sharpness}}`, `studio.subdSet {id, levels?, thickness?, side?, symmetry?: x|y|z|{point, normal}|null, keep?}`; query `studio.subdCage {id}`. Guests who may edit run them as any edit (`AllowedRemote` lets editors run every `doc.execute`).
- **IFC:** `Lapin_SubD.Cage` (IFCTEXT, the cage's JSON); the intent set leaves `cage` out (`IIfcExportFilter.IsPrivate`, new); the import puts it back (`IfcImport.SubDCageOf`). The body is the tessellation, as for every Studio solid.
- **Left for F1c/F1d:** the interface (no UI strings were added; the engine field label "SubD levels (at most)" needs its translations), mirroring by a plan move (`Placement2D.Mirrored` is not applied to the cage, as for primitives and bodies), hollow closed SubDs, the self-intersection of a thickness larger than the surface's radius of curvature (not checked).

### 8.5 What F1c did (22.11.2026)
- **Create:** Studio → Solids → **SubD** pulldown: SubD box, cylinder, plane (a click, plan or ground in 3D, `studio.subdCreate`), **SubD from solid**, **Edit cage**.
- **Cage edit mode** (`modules/studio/SubDEdit3D.tsx`, `subdStore.ts`, pure rules and tests in `subdModel.ts`/`subdModel.test.ts`): Tab, a double click or the card's button enter it; Esc, Tab, Done or another selection leave it. The cage is an SVG overlay over the body (creases thicker, semi-sharp orange and sharp red, borders dashed); filter Vertex/Edge/Face (1/2/3); click picks, Shift adds; a drag takes `snap3d.ts`'s snaps (plus the cage's other points) and the arrow locks, a typed distance or dx,dy,dz (Enter), shows the wireframe moved (partners of a symmetric cage by the mirrored move), and letting go is ONE `studio.subdEdit` with `translate`. Scale and Turn typed on the bar; Extrude (distance), Insert loop, Delete faces, Crease (Smooth, Semi-sharp 1-3, Sharp) by `studio.subdOp`, the cage asked again after each. A plain click in the mode is taken by the new `pickHandlers` hook (`platform/scope.ts`, called from `Viewport3D`'s click) so it only lets go of the pick.
- **Card** (`SubDCard.tsx`): counts, open or closed, level and deviation, thickness and side (open surfaces only: said so, disabled on a closed cage), symmetry x/y/z/none with the engine's refusal shown; the cap "SubD levels (at most)" is the generic field.
- **Strings** in all ten languages; the guide's Studio page (EN, RO) and the scene `studio-subd` (not run).
- **Left:** the body is redrawn only after the release (the live preview is the wireframe); the keep-negative-half option of `subdSet` has no control; the interface was typechecked and unit-tested but not yet looked at on screen.


## 9. F2: exact surfaces for what Studio makes (after F1)

- **`Lapin.Geometry/BSpline.cs`:** B-spline and NURBS curves and surfaces (evaluation, derivatives, knot insertion, degree elevation, rational weights), from *The NURBS Book*; tests on its worked examples.
- **Exact definitions kept beside the tessellation** for what Studio builds exactly: extrusions of lines and arcs (planes, cylinders), revolutions (cones, spheres, tori, surfaces of revolution), lofts (`smooth`: a B-spline through the profiles, replacing the Catmull–Rom stations with the same look), sweeps along lines and arcs. Each face of the body knows its surface; the tessellation stays the one the model uses (`Chords`).
- **SubD's regular faces are exact bicubic B-spline patches** (a regular 4×4 net is one uniform cubic patch): they export exactly; faces at star points export as facets (no promise of exact irregular SubD).
- **Export:** IFC4 `IfcAdvancedBrep` (with `IfcBSplineSurfaceWithKnots`, `IfcCylindricalSurface`, …) and STEP AP214 `ADVANCED_FACE` from `Lapin.Ifc/Step.cs` (`plan-studio-parametric.md` X1), written after the public schemas.
- **Not in F2:** trimming of free-form faces, general surface–surface intersection, fillets between curved faces. Those are F3 (a kernel), only if a case asks.

### 9.1 What F2 did (22.11.2026, decision 781, branch `feat/studio-exact`)

- **F2a, `Lapin.Geometry/BSpline.cs`** (Piegl & Tiller, *The NURBS Book*, 2nd ed.; public mathematics only): `BSpline` (A2.1 span, A2.2 basis, A2.3 derivatives, clamped and averaged knots eq. 9.8, chord-length parameters eq. 9.5, an LU solve), `BSplineCurve` (points and derivatives A3.1/A3.2 with A4.1/A4.2 for rational ones, A5.1 knot insertion, refinement, split, reverse, reparametrise, degree elevation, exact arcs A7.1, A9.1 interpolation, `Join` of pieces at their length fractions, `Compatible` (§10.3: one degree, one domain, merged knots), `Samples` by the chord rule), `BSplineSurface` (A3.5/A3.6 with A4.3/A4.4, normals, knot insertion and splits both ways, iso-curves, A8.1 revolution, §10.3 skinning, `UniformBicubic` (a regular Catmull–Clark face's 4 × 4 net in Bézier form), `Grid` by the chord rule). Degree elevation is done by collocation at the elevated space's Greville abscissae in homogeneous space: the curve lies in that space, so the solution is the curve (not A5.9's algorithm, the same result). Tests `BSplineTests` (20): Ex2.3, Ex2.4, Ex5.1, Ex9.1, eq. 5.36, the quarter circles of ch. 1 (w = 1, 1, 2) and ch. 7 (w = √2/2) to 1e-12, derivatives against differences, revolved cylinder/cone/sphere exact, skinning through its sections, the Bézier patch equal to the uniform one, the chord rule on circles of 5 cm to 40 m.
- **F2b, exact faces beside the tessellation** (`Exact.cs`, `ExactBuild.cs`, `ExactCheck.cs`, `SubDExact.cs` in `Lapin.Geometry`; `ExactProfiles.cs`, `StudioModule.Exact.cs` in Studio): `ExactBody` = vertices, edges on `LineCurve`/`CircleCurve`/`SplineCurve`, faces on `PlaneSurface`/`CylinderSurface`/`ConeSurface`/`SphereSurface`/`TorusSurface`/`SplineSurface` with their loops and same sense. **Nothing is stored**: `StudioModule.ExactRaw` derives it on demand from the recipe, and `ExactCheck.Fit` turns each face as the body's own face there faces and refuses the exact body unless it *is* the body: every face and edge within the tolerance (3 mm; a SubD 2 × its deviation + 1 mm) of the tessellation, every facet of the tessellation on one of the exact surfaces, the shell closed (each edge once each way). A cut by a priority, a step, or a fault anywhere shows there, and the solid is written as its facets.
  - By operation: **primitives** (box, wedge, pyramid: their planes; cylinder: an extruded circle; cone, sphere, torus: turned profiles); **extrude** of lines and arcs, holes included (not tapered, not up to a face): planes and cylinders, the sketch read as drawn (`ExactProfiles.Of` chains the outline's lines, polylines, arcs and circles); **revolve** (lines → planes, cylinders or cones; arcs → spheres when centred on the axis, tori otherwise; an arc whose circle reaches the axis refuses); **prism** (push/pull): planes; **loft** `smooth` through three profiles or more: the skinned surface (below); **SubD**, closed: its regular faces as patches. Whole circles and turns are cut in halves, so no face meets itself along a seam.
  - **The smooth loft is now a B-spline surface**: each profile anticlockwise from where the ray east of its middle crosses it (as `Solids.Matched` lines them up), joined into one NURBS of its lines and arcs, the profiles made compatible and skinned by a cubic across; the body is cut from that surface by the chord rule (`BSplineSurface.Grid`, every profile's corner a column of it), so drawings, quantities and the export are the one surface. The look is kept (it still bulges past the straight loft between profiles); its middle profiles are passed exactly (a circle stays a circle). With a guide rail, the older Catmull–Rom stations stay (not exact).
  - **SubD**: a closed cage's regular faces (quads whose four corners each have four quads round them, no crease in the 4 × 4 neighbourhood) are each one uniform bicubic patch, exactly Catmull–Clark's limit there; with irregular faces the cage is taken after two steps, where all but the faces round each star point are regular (a box cage: 72 patches of 96 faces). The rest are the body's own facets (its level's limit quads, flat ones whole, else two triangles on the shorter diagonal), their edges along a patch the patch's boundary cut at their points (limit points at dyadic parameters, so they meet). An open SubD (a surface or, thickened, an offset) and a fully creased cage have no exact body.
  - Tests `ExactBodyTests` (7) and `StudioExactTests` (7).
- **F2c, export**: `Lapin.Ifc/ExactWriter.cs` writes an exact body after ISO 10303-42 in either dialect: IFC4 (`IfcAdvancedBrep`, `IfcClosedShell`, `IfcAdvancedFace`, `IfcFaceOuterBound`/`IfcFaceBound`, `IfcEdgeLoop`, `IfcOrientedEdge`, `IfcEdgeCurve`, `IfcVertexPoint`, `IfcPlane`, `IfcCylindricalSurface`, `IfcConicalSurface`, `IfcSphericalSurface`, `IfcToroidalSurface`, `IfcBSplineSurfaceWithKnots`, `IfcRationalBSplineSurfaceWithKnots`, `IfcLine`, `IfcCircle`, `IfcBSplineCurveWithKnots`, `IfcRationalBSplineCurveWithKnots`) and STEP (the same with a name first; a rational B-spline as a complex instance of its partial records, `Step.AddComplex`). `IfcSurfaceOfRevolution` is not needed: Studio's revolutions are all elementary surfaces.
  - **IFC**: option `AdvancedBrep` of the export (`advancedBrep` of `project.exportIfc`; the dialog's **Exact surfaces** checkbox), **off by default**: a body Lapin cannot say exactly stays as before, and many viewers and checkers still read faceted B-reps best, so exact output is the user's choice for programs that use it. Lapin writes only IFC4 and IFC4X3 (no IFC2X3), both of which have `IfcAdvancedBrep`. A module says what it knows exactly through `IIfcExactBodies` (Platform); Studio answers. Notes count the bodies written exactly.
  - **STEP**: `Lapin.Ifc/StepExport.cs` (`project.exportStep {ids?, kinds?, storeys?, hidden?, path?}`; the dialog's **STEP** format): AP214 (`AUTOMOTIVE_DESIGN`), millimetres and radians, each element a PRODUCT with an ADVANCED_BREP_SHAPE_REPRESENTATION of MANIFOLD_SOLID_BREPs named after it, colours by STYLED_ITEM; exact surfaces where known, the planes of its facets otherwise.
  - **Reading back**: `StepFile.Partials` reads a complex instance's records; `StepBreps.Read` reads a STEP file's MANIFOLD_SOLID_BREPs through IFC's advanced B-rep reader (the same entities, named after IFC; a step towards X2); `IfcAdvanced` now tessellates spheres and tori on both their angles (`Doubly`: cells by the chord rule both ways, new points on the surface itself), where before a doubly curved face came out flat across.
  - Tests `ExactExportTests` (4): every kind written (cylindrical, conical, spherical, toroidal, B-spline and rational B-spline surfaces; lines, circles, B-spline curves) and read back by Lapin's readers within 0.5 % of the Studio bodies' volumes and areas (box, cylinder, cone, sphere, torus, revolved ring, smooth loft, SubD, and a sweep as facets), the same by STEP, a cylinder as two cylindrical surfaces of radius 250 mm, an exact IFC imported back as references of the same volumes.
- **Left**: sweeps along lines and arcs (a straight sweep is an extrusion, an arc's a revolution: they need the sweep's placement of the profile reproduced, not done); tapered extrusions (cones over arcs) and extrusions up to a face; a two-profile smooth loft (it is straight, ruled) and lofts with a guide; exact SubD near star points (Stam's evaluation, or G1 patches) and of open or thickened cages; checking the files in FreeCAD, Inventor and an IFC checker (only Lapin's readers read them so far); STEP AP242 and an import of STEP (X2) beyond the reader `StepBreps`; the guide's export scene was not retaken.
