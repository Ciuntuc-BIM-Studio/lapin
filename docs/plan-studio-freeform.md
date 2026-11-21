# Studio: free-form surfaces (SubD and NURBS), the ideas worth keeping

Written 21.11.2026 after reading `cekuhnen/blender-nurbs` ("NURBS Proto", a Blender add-on on NVIDIA's USD BRep kernel), its README, development notes and kernel build. The user asked for the good ideas to be noted, and for free-form surfaces in Studio to be the use. Status: **ideas only, nothing decided or built**.

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
