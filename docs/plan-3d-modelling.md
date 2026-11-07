# Modelling in the 3D view as in plan

Decisions 383– (`docs/decisions-pending.md`), from two testers' reports of the same user, approved by the user on 07.11.2026:

- **#104** "Moving 3D object and make it snap to the area desired": a small stair stands just off the edge of a floor slab in the 3D view (the chip *Drafting on Ground floor · Pick a face* at the bottom). Expected: move the object smoothly onto the area wanted, snapping to the slab's edge, corner or face, as SketchUp's Move does.
- **#105** "Make the architecture components addable and editable in the 3D viewport": the same placing and dragging of components as in the plan, in the 3D view too; "sometimes the user may have to work fully in 3D, not just in plan".

It follows decisions 312 (the command line in 3D), 374–378 (Studio's sketching in 3D: the shared inference, the typed value and who owns it) and the grips in 3D of #121.

## What there is today

### How a tool takes its input

Every placing tool is a `ToolDef` with a `gesture` (`ui/src/platform/module.ts`); the view hosting it turns clicks into the gesture's points and calls `complete` once (`viewport/tools.ts`). The plan (`PlanView.tsx`) hosts every gesture: the cursor aimed by the CAD canvas (`cad/aim.ts`: object snaps on the plan's lines and points, ortho, polar, tracking, the grid), a rubber band of the points placed, the live input (a length and an angle typed, `liveInput.ts`), Enter and Esc.

The 3D view (`Viewport3D.tsx`, `clickRef`) hosts only part of them, and crudely:

| Gesture | Tools | In 3D today |
|---|---|---|
| `point` | column, pad footing, room, balcony, ceiling and skirting in a room, downpipe, elevator, plant, entourage, spot level, text, Studio primitives, furniture, MEP devices | the click's ray on the active level, rounded to 5 cm; no snap, no preview |
| `twoPoints` | wall, curtain wall, beam, strip footing, steel member, gallery edge, sunshade along, kitchen, rebar run, section, grid axis | the same, two clicks; no rubber band, no typed length |
| `rectangle` | flat terrain, view boundary | as `twoPoints` |
| `threePoints` | curved wall, curved beam, curved strip, curved section | **not hosted** |
| `polygon` | floor, roof, floor opening, ceiling, room outline, balcony outline, parcel, platform, planting zone | **not hosted** |
| `polyline` | railing, skirting, road, path, ditch, retaining wall, scaffold, gas main, rebar polyline, jogged section | **not hosted** |
| `pick` | door, window, bay window, wall opening, sunshade, attach to roof, chairs round a table, MEP connections | the element picked, but the point given is the ray on the level, not the face clicked: a door clicked on a wall's face at 1.5 m lands where the ray meets the floor, behind the wall |
| `face` | wall reveal, wall sweep, formwork face, Studio's face tools | hosted: the face lit and named (`core.faceAt`, `studio.faceAt`) |
| `pickThenPoint` | fan duct, bar split | the pick alone |

Stairs and ramps are the command line's STAIR, which already runs in 3D on the level (decision 312), with the plan's object snaps only.

### Moving and editing

- **MOVE, COPY** (M, CO, Modify → Move, Copy) run in 3D on the work plane (the active level) through `Draft3D.tsx`: the base and second point on the level, snapped to the plan's cut lines and drafts, never to the bodies in 3D; the displacement is horizontal only. This is what the tester of #104 met: the stair could not be put on the slab's corner, because the slab's top corner is not on the level's plane and the plan's snaps do not see it.
- **Studio's Move 3D** (arrows along x, y, z) moves Studio solids only, by a distance along one axis.
- **Grips in 3D** (#121, `Grips3D.tsx`): the plan's grips drawn at each element's height and dragged on that horizontal plane, to the centimetre, with no snap to the other elements.
- The engine's `element.transform` moves in plan; its `rise` (a move in an elevation) reaches only what reads a height from it (a wall sweep).
- No catalog card or ribbon tool can be dropped on any view.

## Design

One **3D snapping core** shared by everything that takes a point in the 3D view, beside the sketch's inference of decision 374 (which works on a plane): `viewport/snap3d.ts`.

### The 3D snapping core (`snap3d.ts`)

The cursor is a ray. What it infers, in SketchUp's order:

1. **Endpoint**, then **Midpoint**: the ends and middles of the edges of the bodies near the cursor (their sharp edges, 20°, as `sketch/edges.ts` reads them), within 10 px on the screen; a point hidden behind the face under the cursor is not taken.
2. **On edge**: the point of an edge nearest the ray, within the same reach.
3. From a **base point** (the move's, the wall's start): the model's **red, green and blue axes** through it, when the cursor runs along one on the screen. A locked axis (→ red, ← green, ↑ blue, the same key lets go; Shift holds the one inferred, as in the sketch) keeps the point on its line, and an endpoint hovered then is projected onto the line ("from point"), so a move can line up with a corner without going there.
4. **On face**: where the ray meets the body under the cursor.
5. **On level**: where it meets the active storey's level, in empty space.

An imported DXF shown in 3D (decision 382) is snapped to too: its lines' ends, middles and points on them, so walls can be traced over a DXF in 3D.

The inferred kind is named beside the cursor in its colour, the guide is drawn from the base, as the sketch's are.

### Stage A: Move and Copy in 3D with snaps (#104; done, 07.11.2026, decision 383)

- **MOVE, COPY** in the 3D view (M, CO, the command line, Modify → Move and Copy, SketchUp's M) become a 3D move: the selection's base point is picked with the snaps above (a corner of the stair), then the target point (the slab's corner): the selection follows the cursor (the bodies themselves are carried on the screen, not a box), the displacement in 3D. A length typed goes along the way the cursor points (`1200`); `dx,dy,dz` or `dx,dy` typed is the displacement itself. Enter applies the typed value; Esc cancels; COPY keeps copying from the same base until Esc or Enter.
- **Drag by body**: pressing on a selected element and dragging moves it the same way, the base point the snapped point under the press; letting go applies. View → *Drag to move in 3D* turns it off (kept on this computer), for those who orbit by dragging on the selection.
- **The engine**: `element.transform` takes `up` (m), the move's vertical part. What has a height of its own goes up or down by it: a wall's, column's or furniture's base offset, a stair's base and top offsets, a beam's top offset, a floor's top offset, a roof's wall plate, an object's height above the floor (`IElementRise` for a module's own: Studio solids take a move step, `op: move`). What has none keeps its height and is named in the answer (`kept`), and the interface says so. A door or a window slides in its wall as before.
- **Studio's Move 3D** stays (moving along one axis by arrows); SketchUp's M starts the new move.

### Stage B: the placing tools in 3D (#105; done, 07.11.2026, decision 384)

A **3D placement host** (`Place3D.tsx`, the gestures' logic in `gesture3d.ts`, shared rules with the plan's): while a tool with a gesture of points is on in the 3D view,

- the cursor is snapped by the core above, then brought onto the **active level** (the point's x, y: a wall started at a slab's corner, a column at a beam's end); the points placed and the cursor are drawn on the level (the rubber band, the rectangle, the polygon closing back to its first point, an arc through its third point), with the length and the inferred kind beside the cursor;
- every gesture is hosted: `point`, `twoPoints` (chained for walls), `rectangle`, `threePoints` (arcs), `polygon` (back on the first point or Enter closes it), `polyline` (Enter ends it, or a click on the last point), `pickThenPoint`;
- a **length typed** goes along the way the cursor points from the last point (`1200`, `1000<30`, as in the sketch), Backspace takes the last point back, Esc drops the gesture, again the tool;
- **doors, windows** and other `pick` tools take the point where the face was clicked (its x, y on the wall), not where the ray meets the floor; `face` tools are as they were.

### Stage C: grips with snaps, components dropped on faces (#105; done, 07.11.2026, decision 385)

- **Grips in 3D** snap with the core: a wall's end dragged onto another wall's corner, a slab's vertex onto a column's corner; the point kept on the grip's own height, as now.
- **Dropped on a face**: a catalog card or a ribbon tool dragged onto the 3D view (or a plan) starts its tool with that type there; a tool placed by one click (furniture, a column, a pad, an elevator, a plant…) is placed at once where it was dropped, on the face under the cursor; dropped on a floor of another storey, the element goes on that storey (`completeTool` on the storey of the element under the drop).
- **On the storey of the face**: in 3D a one-click tool clicked on a floor's top or a roof belongs to that element's storey, not the active one, so furniture can be set on the first floor while the ground floor is active.

## Stages and order

A first (the report #104 is the sharpest: a move that cannot snap), then B (most of #105: every tool in 3D), then C (editing and dropping). Each stage is a commit of its own with its tests, the user guide (View → 3D views → *Modelling in 3D*, nine languages), the interface strings in every table, an Ask Lapin entry and a decision row.

## Tests

- `snap3d.test.ts`: endpoint over midpoint over edge, hidden points left, axes from a base, a lock and the point projected onto it, face and level last.
- `move3d.test.ts`: the typed value (a length along the cursor, `dx,dy,dz`, `dx,dy`), the displacement and the command it makes (`by`, `up`).
- `gesture3d.test.ts`: the storey a point stands on; each gesture's clicks as in plan (chained walls, a polygon closed on its first point, a polyline ended), a typed length, Backspace.
- Engine: `ElementTransformUpTests` (a stair, a wall, a column, a floor, a beam, furniture and a Studio solid raised; a drafted shape kept and named).
