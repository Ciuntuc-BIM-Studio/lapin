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
- **Refused** (each with a message): a curved wall; a reference (its body is its file's); a layer of no thickness; an edge the
  element does not have; a layer stopping short of both ends (or bottom and top) by more than it is long (high); a floor
  layer's outline that would cross itself.
- **IFC import**: nothing is added to imported elements; an extent is only set by the user.

## Editing (stage 1)

Properties → **Layer extents** on a straight wall or a floor: a row per extent — the layer, the edge, the offset, **Cuts
others** — and **Add a layer extent** (the first free place, 2 cm past). Each change is one command, one undo step
(`architecture.setLayerExtent`; a change of layer or edge is the old one taken away and the new one set, one `edit.batch`);
`architecture.clearLayerExtents` takes them all away. Both are in the command catalogue; a guest editor may run them (every
`doc.execute` an editor may).

## What is left (stage 2 and later)

- **Grips**: "Edit layers" in a section and a plan — each layer's edge grip on the selected element, dragged or typed (the
  finish painter's edit scope). Stage 1 is numeric only.
- Curved walls; a wall's top under a roof (the layers follow the roof; a top extent is not used there); the gable's piece over
  a zoned core's ring beam.
- Roofs: their coverings are not a layer set yet (`docs/view-styles.md`); when they are, the same `layerExtents`.
- A type default for an extent (a floor type whose finish always runs 2 cm past); per-edge extents of a wall's end by face
  (the imported `layerEnds` does it for free ends).
- Cutting what is not a wall or a floor (columns, beams, footings) and following the cut order instead of the on/off choice.
- Extents follow the layer's index: a type changed for one with other layers keeps the indices (those past the last layer are
  ignored).
- Copies and storey copies keep the attribute (it is an attribute), groups too; a floor's edge index after a contour drawn
  again from scratch (`RemapEdges` finds the edge along the old line, or drops it).
