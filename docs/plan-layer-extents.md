# Layer extents (forum topic 89)

A tester (benlungu) asked to "unlock" the layers of an assembly and edit them at the element's edges: a floor modelled to the
edge of a foundation or a wall whose finish should go on 2 cm over that edge; and, when a layer is extended, to say whether it
cuts the element it runs into or not. In Revit the workaround is to model two floors and split the wall into two or three
walls; doing it on the layers keeps one element. (The reply on the forum showed another workaround: an offset wall and join
priorities, or a wall sweep.)

## The model

- The element keeps its type's layer set and its usage (`IfcMaterialLayerSetUsage`); nothing changes on the type.
- **An extent** is the element's own design intent, in its attribute `layerExtents` (carried in `Lapin_Intent` like every
  attribute, so a Lapin IFC file brings it back): `{layer, edge, index?, offset, cuts?}`.
  - `layer`: the layer's index in the layer set (as the type lists it, whichever way the wall is flipped).
  - `edge`: a **wall**'s `start`, `end` (along its axis, whatever ends there: a free end or a join), `bottom` or `top`;
    a **floor**'s `outline` (all round) or `edge` with `index`, its contour's edge from corner `index` to the next (an edge's
    own extent wins over the outline's).
  - `offset`: metres; positive past the edge, negative short of it; at most 1 m either way. 0 takes the extent away.
  - `cuts`: whether the piece past the edge is taken out of the walls and floors it meets. Off (the default) it overlaps
    them: both keep their volumes and the quantities count it twice, as asked.
- An element without extents (every element made before) builds exactly as it did; nothing to upgrade.

## What it builds (derived on every ask)

- **Short of an edge**: the layer is built shorter where it is built (`Walls.Geometry`'s reach and `LayerSpan`, the floor's
  contour moved in by `Polygon2.Offset`, per edge).
- **Past an edge**: a piece of its own (`Walls.ExtentPieces`, `Slabs.ExtentPieces`), added *after* the cut priorities (which
  would take it back: a floor outranks a wall), cut by the openings, and joined in one body with the part of its layer it goes
  on from (`LayerExtents.AddJoined`), so a section draws no seam. A wall's strip past an end runs the layer's whole height
  (each of its height pieces in its material, a zoned core's zones); under its base and over its top, its footprint as the
  joins leave it. A floor's piece is its layer's outline moved out less the outline it is built to.
- **Cuts**: every wall and floor takes out of itself the pieces past the edges, set to cut, of the walls and floors round it
  (`LayerExtents.CutBy`). They are read under a narrow key (`LayerExtentReads`, `extents:<class>`, touched when an element
  gets, changes or loses extents; stamped as the class for undo), so an edit of an ordinary wall rebuilds nothing more.
- So **plans** (a wall's cut runs on past its ends; a floor draws each layer's own outline once), **sections** (the engine
  cuts the bodies), **3D**, **quantities** (net volumes of the parts) and **IFC** (bodies as built, layer sets kept) follow.
- GlobalIds stay: the element is the same entity, its parts keep their keys.
- **A floor's contour edited** (`updateSlab`): an edge's extent follows its edge (the new edge along the old line, its own
  index first); one whose edge is gone goes with it.
- **Refused** (each with a message): a reference (its body is its file's); a layer of no thickness; an edge the
  element does not have; a layer stopping short of both ends (or bottom and top) by more than it is long (high); a floor
  layer's outline that would cross itself.
- **IFC import**: nothing is added to imported elements; an extent is only set by the user.

## Editing (stage 1)

Properties → **Layer extents** on a straight wall or a floor: a row per extent — the layer, the edge, the offset, **Cuts
others** — and **Add a layer extent** (the first free place, 2 cm past). Each change is one command, one undo step
(`architecture.setLayerExtent`; a change of layer or edge is the old one taken away and the new one set, one `edit.batch`);
`architecture.clearLayerExtents` takes them all away. Both are in the command catalogue; a guest editor may run them (every
`doc.execute` an editor may).

## Stage 2 (decision 578)

- **Edit layers** (Architecture → Edit layers, or the button under the rows): an edit scope (kind `Layers`, walls and floors
  kept, the rest faded, the selection kept). The one wall or floor selected shows each layer's outline, dashed, and a grip on
  each edge of each layer (`layerEdit.ts`, `LayerEditOverlays.tsx`). The engine says where the edges are as built
  (`architecture.layerEdges {id}` → `{layers: [{layer, name, footprint, z0, z1}], grips: [{layer, name, edge, index?, line,
  dir, offset, own}]}`, model coordinates; `LayerExtents.Edges`); the interface places them in the view: in a plan the edges
  that move sideways (a wall's ends, a floor's edges), at their middle; in a section those moving in its plane, where the cut
  crosses their line (a wall's base and top, a floor's edges), or, for an edge lying along the cut, where the cut runs
  through its layer (a wall's end, the section along the wall); in an elevation the nearest of the edges seen at one point.
  A developed section shows none. A grip is kept on its way (`constrain`), snaps as every grip does (the view's `aim`), takes a
  typed distance while dragged (the live input); its drop is one `architecture.setLayerExtent` (its cut left as it is), one
  undo step. The views lend a section's frame and kind to the grips (`GripView.frame`, `kind`, `developed`), and a module whose
  grips change without the model changing bumps them (`bumpGrips`, `useGripRev`). Through an inset view the grips are not
  mapped: the source's frame is not the host's (the inset's mapping is a plan's; left).
- **Curved walls**: start, end, bottom and top, as straight ones. Short of an end, along the arc (`PathCurve.Band` with the
  stations moved); past an end, on along the end's tangent, as the wall's own body is past its axis (`PathCurve.PointAt`), one
  rule for curves (`Chords` through `PathCurve.Stations`). `Walls.ExtentFrame` gives straight and curved walls one shape for the
  pieces and the grips (the axis as a `PathCurve`, where each band ends, the footprints, the roof).
- **Under a roof**: a layer going up to the roof (no top of its own under the wall's) takes its top extent by following the
  roof: its body built that much higher and cut along the roof's line moved by it (`Walls.RoofShift`, `HeightExtents`), so
  10 cm under the roof or 5 cm into it; a layer with its own top keeps its extent from it. A zoned core's gable goes up with it,
  and its strip past an end now includes the gable over the ring beam.
- **Roofs' layer sets**: roofs have none (their covering, deck and framing are parts of their own, `RoofCoverings`,
  `RoofParts`); giving them one is a decision of its own (the user's), so no roof extents; the guide says so.
- **Type defaults**: a wall type's or a floor type's own `layerExtents` (a floor type's all round only), set with the same
  command on the type. An element builds with `LayerExtents.Effective`: its type's, each one it sets at the same place replaced
  (an own 0 keeps the layer at that edge, stored only where the type has one), then its own; `inherit: true` gives the place
  back to the type. The holders of cutting extents include the elements of types that have them (`LayerExtentReads.Holders`
  notes those types and their typing relations), the `extents:<class>` key is touched by a type's change and stamped with the
  type class's stamp too. Properties → Layer extents shows on types, and an element's rows from its type marked *From the type*
  (↺ back to the type's). A Lapin IFC file carries the type's in its Lapin_Intent.
- **Cutting columns, beams and footings**: the platform's `ILayerExtentCuts`, provided by architecture, applied by structure's
  columns, beams, strip footings and pads after their cut priorities (`LayerExtentCuts.Apply`).
- **The cut order**: `cuts: "order"` (Properties: Overlaps / Cuts others / By cut order) cuts only the elements its element
  outranks by the cut order (`ICutters.PriorityOf`): a floor's finish cuts the wall, a wall's plaster going down leaves the
  floor whole.
- Saved projects open unchanged: nothing is stored differently (`cuts` may now also be `"order"`; types may hold the
  attribute); no upgrade. IFC import adds nothing.

## What is left

- Edit layers through an inset view (the source view's frame lent to the inset's grips) and in a developed section.
- The grips in 3D: a wall's and floor's grips are placed as in a plan there (at the storey's height); grips of its own in 3D
  (on the faces, the finish painter's way) would be better.
- Roofs' layer sets (a decision of its own), then the same `layerExtents` on them.
- Per-face extents of a wall's end (the imported `layerEnds` does it for free ends).
- Extents follow the layer's index: a type changed for one with other layers keeps the indices (those past the last layer are
  ignored).
- A cutting piece under a roof's line (a layer going into the roof) does not cut the roof: roofs are not cut by layers.
