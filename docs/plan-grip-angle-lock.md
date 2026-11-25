# Keeping an element's own angle while dragging an end grip: plan

Asked on 25.11.2026 in forum topic 260 (Andras: "stretch an element along the original angle it was drawn": a modifier that jumps to / locks the angle, the typed or clicked distance lengthens or shortens the element along it; Revit and ArchiCAD have it).

**Order: 1 of 6** (the six plans of 25.11.2026: angle lock, structural usage, level datums, leaning walls, national packs, 3D mouse).
**Status: stages A, B and D done (25.11.2026, branch `feat/grip-angle-lock`); stage C partly (outline vertices); the rest of C proposed.** UI only, no engine change.

- A done: `Grip.along` (`gripKit.ts`), Ctrl/⌘ read while dragging (pointermove and keydown/keyup), `liveInput` lock (`liveLock`, `alongPoint`; typed value = change, `=` total), the "Angle kept" tag and the hint in the live box.
- B done: `resolveAim` `along` (point snap projected, line snap intersected, Ctrl over Shift, guide kind `own`, soft guide with polar on).
- C: outline and polygon vertices lock to the edge before them (`outlineGrips`). Not done: polyline vertices of drafted shapes, section lines, MEP runs, railings and the 3D grips (Move3D); each module gives its grips `along` with `alongOf(from, end)`.
- D done: guide (walls.md, drafting.md, interface.md, EN and RO), scene `grip-angle-lock`, strings in all languages, plan-direct-editing.md E1/E5.

## What exists today

- `ui/src/viewport/GripLayer.tsx`: the modifiers are taken.
  - At the press: Alt moves one element only, or removes a dimension point; Ctrl/⌘ copies with a move grip and inserts, extends or removes on vertices and dimensions; Shift on an edge middle offsets the outline.
  - While dragging: Alt reads the movers; Shift frees a `constrain` grip and drives `gripSnapped`; Tab over a shared grip cycles its owner (docs/plan-direct-editing.md, E1).
- Shift held is ortho (`PlanView.tsx`, `DrawingView` `orthoHeld`); Tab and `<` belong to live input (`app/keys.ts`).
- The shared end grips (`axisGrips`, `pathGrips` in `viewport/gripKit.ts`, used by walls, beams, footings, drafted lines, grid axes and rebar): Ctrl/⌘ at the press of an end grip does nothing today.
- `viewport/liveInput.ts` already holds a direction when an angle is typed and projects the cursor along it (signed: lengthen or shorten). A grip drag starts it with base = the grip, `delta: true`.
- Decision 739 is the angular-dimension lock ("locked angles kept after every command"); it does not touch grips and does not clash.

## Design

- **Grip.** A new `along?: {from: Vec2; dir: Vec2}` (the fixed other end and the direction at the press). `axisGrips` and straight `pathGrips` fill it for start and end grips.
- **Key: Ctrl (⌘ on macOS) held while dragging.** `onPointerMove` reads it; also listen to keydown/keyup so the lock shows without moving the mouse. What Ctrl does at the press stays as it is.
  - Not Alt: taken, and on Linux window managers Alt-drag moves the window.
  - Not Ctrl+Shift / Alt+Shift: they switch the keyboard layout on Windows.
  - Not plain Ctrl on a Mac: Ctrl-click opens the context menu there.
  - The same chord on web and desktop.
- **Behaviour.** The point lands on the element's own line, on the cursor's side, so it lengthens or shortens. The live angle field is filled with the element's angle and shows a lock. A typed value is the **change** in length along that line, signed by the cursor's side; `=3.5` gives a total length. Ctrl wins over Shift's ortho. Snaps: a line snap gives the intersection with the element's line; a point snap is projected onto it.
- **Soft help.** With polar on, the element's own direction is always offered as a tracking guide labelled "own angle" (a guide kind `own` in `cad/aim.ts` and `tracking.ts`).
- **Discoverable.** Grip tooltip "Ctrl/⌘ while dragging: keep its angle"; a hint line in the live box during an end drag and in the command-line prompt; the guide is drawn while locked.
- A lock on a joined end shared by several elements uses the grip owner's own direction. One drop is one step and one undo step; no GlobalId changes, no `IProjectUpgrade`.
- Curved elements: no lock (an arc keeps its rise).

## IFC, drawings, quantities

None: the result is an ordinary move of an axis end. Joins, quantities and drawings follow the existing commands.

## Tests first

- `liveInput.test`: the lock projects onto the line; the side follows the cursor; a typed delta applies on either side.
- A GripLayer/grips test: Ctrl held gives the end on the line; a copy grip is unaffected; Alt+Ctrl still moves one element only.
- `aim.test`: snap intersection and projection, the own guide, Ctrl over Shift.
- `keys.test`: Tab, `<` and Alt unchanged.

## Risks

- Mid-drag modifier events (pointermove carries `ctrlKey`; keydown also needed).
- Ctrl at the press on a vertex removes the corner: the hint must say "after you start dragging".
- A joined end shared by several elements.

## Stages

| Stage | Content | Effort |
|---|---|---|
| A (done) | `along`, the Ctrl/⌘ lock, live input, hints | S |
| B (done) | Snaps on the line, the soft own guide, ortho/polar interplay | M |
| C (partly) | End vertices of polylines and outlines (lock to the adjacent segment), section lines, MEP runs, railings, 3D grips (Move3D) | M |
| D (done) | Guide (walls.md, drafting.md, interface.md, EN and RO), scene (a wall at 37° stretched with ⌘, its typed length shown), strings in all languages; add the rule to plan-direct-editing.md E1/E5 | S |

## The user's answers (25.11.2026)

The user accepted every recommendation:

1. The key is Ctrl/⌘ held while dragging. **Yes.**
2. A typed value is the change in length (not the total), signed by the cursor side; `=3.5` gives a total. **Yes.**
3. The element's own direction is always offered as a soft guide when polar is on. **Yes.**
4. Curved walls and beams: no lock. **Yes.**
5. Polyline and outline end vertices lock to the adjacent segment, in stage C. **Yes.**
