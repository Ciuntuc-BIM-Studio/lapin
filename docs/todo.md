# To do

Work asked for or left open, not started yet. When an item is done, it moves to `docs/decisions-pending.md` (with its decision number) and leaves this list.

## Asked for

- **Vents and breeze blocks as openings** (asked 08.11.2026). Options for openings that are not doors or windows:
  - vents: a wall or roof vent, an air brick, a louvred grille, a round or rectangular duct outlet, with its free area for ventilation;
  - breeze blocks (claustra, decorative screen blocks): an opening, or a whole wall panel, filled with a pattern of pierced blocks, with the block's size, pattern and joint, the wall's opening cut once around the panel.

  Both are configured like doors and windows (a type, a configurator with its pictures, styles), drawn in plan, elevation, section and 3D, scheduled and measured, and written to IFC. Fit them in with wall openings (decision 392) and the exhaust fans' wall provisions (decision 349).

- **BIM Open Schema in the browser** (found 09.11.2026). A `.bos` whose Parquet pages are Brotli-compressed (the `digitalhub-*.bos` samples) cannot be read in the web build, before or after the lazy parts of decision 413; the desktop reads them. Fix it, or say so in the guide's BOS section.

## Left open by earlier work

- Point clouds:
  - the index kept beside the project, so a project moves to another computer without reindexing (approved 08.11.2026);
  - the plan hang seen while filming is not reproduced: watch for it (decision 399);
  - developed sections draw no cloud;
  - DXF export leaves the slice out;
  - Studio's 3D sketch does not snap to cloud points.
- Visibility / Graphics:
  - a subcategory's own colour and pattern;
  - line weight on filters;
  - overrides inside a link set to Custom;
  - a link drawn as an underlay;
  - halftone for traced images;
  - a DXF in 3D following a 3D view's imported categories.
- Level of development:
  - steel as a line in 3D;
  - bay windows per level;
  - whether site symbols (sleepers, plants) follow the LOD instead of the scale.
- Wall openings: the reveal finish on curved walls and in plans; a pointed or triangular head is kept when a door or window becomes an opening again; tags of a door or window turned into an opening.
- Doors and windows: the joinery schedule card draws arched types as rectangles.
- Stairs: balanced (dancing) winders.
