# Studio as a geometry editor (decisions 196–202)

Asked for on 30.10.2026: what Studio was missing to be a more versatile geometry editor, then all of it, in the order recommended. Studio had extrude, sweep (placed, morphing, two rails), loft and revolve from sketches on plans and sections, live booleans, IFC classes and types. This plan records what was added, how, and where it stops.

## 1. Sketches anywhere (196)

- **Elevations** take sketches on the plane an elevation fixes the first time a solid is made on it (its *Sketch plane depth*, `Views.FixElevationPlane`, from the thick solids' work, docs/plan-studio-thick.md), so what is drafted on it stays put as the model grows.
- **Reference planes** (docs/plan-studio-thick.md, view kind `plane`: from a view, on a face picked in 3D, through three points) take sketches like any view. This work first added work planes of its own (a view of kind `workplane`); when it was merged with the thick solids on 31.10.2026 the two were one idea, and the reference planes stayed: the face pick, push/pull and the steps work on them as on any other view.
- `BrepPick.Face` finds the flat face of any element's built body under a 3D pick (the face facing that way whose plane is nearest and whose outline holds the point), for the face tools; any element's body works, not only the named faces of `IElementFaces`.

## 2. Push/pull (196)

`studio.pushPull {id, point, normal, distance}` (positive: out, adding):

- an **extrusion's end** changes its depth (and where it starts), the steps on that end moving with it;
- an **extrusion's side**, over an edge of the sketch's closed polyline (or of a hole's), moves that edge (its ends slide along their neighbours) — the sketch stays the source of truth;
- **any other Studio solid**, or a face a step made, takes a `pushPull` step (below);
- **another element** (a wall, a slab, a reference): pulled, a `prism` solid stands on the face in the face's material; pushed, the prism is a void (`void: true`, never drawn, cut priorities skip it) that the element subtracts, as live as any boolean. `studio.setPush` changes its distance and turns it from one into the other.

## 3. The extrusion's options and profiles (197)

- `symmetric`: both ways from the sketch.
- `taper` (°): the end face the outline moved in by depth · tan(angle), holes out; refused when the end would fold (`Solids.Inset` checks each side keeps its direction and none cross).
- `upTo` {id, point, normal}: up to a face of another element, found again on that element as it is now; each point of the outline runs along the extrusion until it meets the face's plane, so a column ends under a sloping soffit (`Solids.ExtrudeToPlane`). `studio.setUpTo` sets or clears it.
- `cut` [ids]: the extrusion cuts those elements and is a void.
- **Profiles from chains**: lines, arcs and open polylines meeting end to end on a view make a closed outline (`Sketches.Closed`, through drafting's `Joins.Chains`; arcs in steps of at most 5.6°, as circles). Holes may be chains too. Moving a solid drawn in plan moves every piece of its outline.

## 4. Primitives, split, arrays (198)

- `studio.primitive {shape, at, …}`: box, cylinder, cone (a top radius, 0 for a point), sphere, torus, wedge, pyramid, standing on the middle of their base, turned about the upright; sizes are fields; `studio.place` moves one in 3D.
- **Split** (`studio.split`): the solid keeps what is behind the plane (a `slice` step), a second solid what is in front; both share the sketch and the steps before.
- **Arrays** are steps: linear (count, step) or polar (count, angle filled, centre, axis); the copies united.

## 5. Steps and the kernel (199)

A Studio solid keeps `features`: the steps done in order on the body its operation made (`Features.Apply`), each kept with what finds its face or edge again — a point on the face and its normal, an edge's two ends — so every step is redone when the sketch changes. The finding is geometric (nearest face facing the same way; nearest boundary segment along the same line), not a topological name: it survives depth and sketch changes that keep the face's direction, and says so under Problems when it cannot. `studio.addFeature`, `updateFeature`, `removeFeature`, `moveFeature`; `off` switches a step off.

- **pushPull**: a prism on the face, united (out) or subtracted (in).
- **chamfer / fillet** on one edge (`a`, `b`) or on every edge round a face (`edges`): the corner piece in the plane across the edge — the two tangent points, a quarter round in 12 steps or a bevel between — extruded a little past the edge's ends and subtracted (a convex edge) or united (a concave one, filled). Where two rounded edges meet at a corner, the pieces overlap as they fall: no vertex blend.
- **shell** {thickness, open: start, end, both, none}: the operation done again with its profile inset (`Solids.Inset`; holes outset) and its closed ends moved in by the thickness (or run a hair past where open), subtracted. It works for every operation that has a profile (extrude, tapered or up to a face too; sweeps, keeping the outline's box for placement; lofts; revolutions, the ends turned in at the profile's middle) and for the primitives that have an inside (box, cylinder, cone, sphere, torus). It hollows the operation's body, not what later steps added.
- **slice** {point, normal, keep}: intersected with a box as big as the body on the side kept.
- **move / rotate / mirror**: the body placed in 3D (a rotation about an edge picked, a mirror in a face's plane).
- There is no general solid offset and no fillet between curved faces: manifold (the boolean library) has neither, and both are left for later if a case asks for them. Offset of a face is push/pull.

## 6. Placement and sketches by numbers (200)

- A solid sketched on a plan moves with its sketch (as before), its steps carried with it (`Features.Map`). One sketched on a section, an elevation or a work plane is moved in plan by `placed` (12 numbers, an instance's convention): its sketch stays on its view, and a copy shares it. Primitives, prisms and bodies move by their own numbers.
- A polyline's properties list its sides; a side typed a new length keeps its start and direction, its end moves along it and the vertices after it follow up to the next side running the same way (`ui/src/viewport/cad/sides.ts`), so a rectangle stays a rectangle and an L an L; a solid on it follows. A constraint solver (parallel, equal, tangent) is not part of this.

## 7. Sweeps and lofts that change, bodies and meshes (201–202)

- Sweeps: `twist` (° by the end, about the middle of the section's box), `scaleEnd`, `rise` (m by the end), `turns` (a closed path that rises goes round that many times: a helix). A path is cut into stations every 5° of twist and every 5 % of growth. A ring may twist by whole turns only, and keeps its size.
- Lofts: `smooth` (Catmull-Rom through the profiles, 12 stations a span) and a guide `rail` (a drafted line on any view with a plane: how far it strays from the straight line between its ends is added along the loft).
- `studio.fromElement {ids, keep?}`: any element's built body as a Studio solid (`operation: body`), its triangles kept in its own coordinates (`solidBody`, never sent as props, never written as intent properties), its name, storey, material and IFC class (as `exportAs`) kept; the element goes unless kept.
- `project.importMesh` (`Lapin.Mesh/MeshImport.cs`): Wavefront OBJ (objects and groups as pieces, polygons fanned, `usemtl` as a colour by the name's hash, the .mtl not read), STL (binary or text, corners welded) and glTF 2.0 (.gltf with its buffers inside or beside it on the desktop, .glb; the default scene's nodes placed by matrix or T · R · S, triangle primitives only, base colour factors) as references (stored bodies) on the active storey. glTF is metres and Y up; OBJ and STL ask for their unit (scale) and up axis. File → Import mesh (OBJ, STL, glTF)….

## The interface

- 3D face tools take any element (`types: ['*']`) and name the face under the cursor with `studio.faceAt` (`gesture.query`); the face lights up as with the named faces.
- Values are asked for beside the cursor (`shell/AskBox.tsx`): Studio's 3D tools have no command line.
- The command line: EXTRUDE takes S (symmetric), T (taper), CUT (the elements selected before); SWEEP takes TW, SC, RI, TU; LOFT takes S (smooth) and a selected shape with the role rail as its guide.
- The properties card lists the steps with their number, choice, switch, order and removal; a primitive's place; an extrusion's Up to; the problems.

## Tests

`StudioEditorTests` (15: elevation and work plane sketches, push/pull on extrusions, steps and walls, the extrusion's options, chains with arcs, primitives' volumes, chamfer and fillet, shells, split/slice/arrays, 3D placement and moving a section's solid, twisted/scaled/helical sweeps, smooth and guided lofts, bodies), `MeshImportTests` (OBJ in millimetres Y up made into a solid and chamfered, STL text and binary, glTF with its buffer inside and GLB), `sides.test.ts`; the guide's Studio page in six languages with its scene (push/pull and fillet through the interface, a work plane on a face, steps, a helix).
