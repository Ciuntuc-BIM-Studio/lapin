---
title: Parts
order: 87
summary: Edit a Studio solid or a placed part in place with Tab — the rest of the model fades, and the part's tree lists its origin, sketches, steps and parameters.
---

## What a part is

A **part** is a Studio solid with a definition behind it: a sketched solid with its steps, or an object of your own whose bodies follow **parameters** (what used to be a family). A part is placed as many times as you need, and each object keeps its own parameter values.

**Families are now parts.** A project saved with families opens with the same objects as parts, with the same names, bodies, values and ids, and the Families page still describes how they are made. **Studio → Families…** and its tools keep working.

## Editing a part

Select one Studio solid or placed part in 3D, then do one of these:

- press **Tab**;
- **double-click** the part in 3D;
- press **Edit part** in **Studio → Part**, or in the **Part** section of its Properties.

The part is then in **edit mode**. The rest of the model fades and cannot be selected, but it is still there to snap to. The part is drawn as usual, and the bar over the view says what you are editing.

![The table in the model, selected](shots/studio-part-model.webp)

![The table in edit mode: the model faded, the front plane of the origin shown, the part's tree on the left](shots/studio-part-edit.webp)

To leave, press **Tab** or **Esc** again, or **Finish part** in the tree. Leaving is not an undo step; every change made inside is one undo step, as everywhere in Lapin.

Tab does nothing special while a text field has the focus, while a dialog is open, or while a tool is running: it folds the panels as before.

## The part's tree

The panel on the left of the view lists what the part is made of, and is read again after every change.

![The part's tree: origin, sketches, steps and parameters](shots/studio-part-tree.webp)

- **Origin** has the three planes of the part: top (xy), front (xz) and right (yz). Point at one to see it in 3D, or click it to keep it shown; click again to hide it.
- **Sketches** lists the part's sketches. A sketch of a solid (an extrusion, a sweep, a revolution, a loft) opens on its view, as **Edit sketch** does; because the sketch lives on its view, the edit mode ends as it opens. A part made of bodies opens in its editor, with the part chosen.
- **Steps** lists what makes the part, in order: its operation or its bodies, then the steps done to it (push/pull, fillet, shell, split…). A step that is off is greyed; a body made only when a condition holds shows it (*off when not HasShelf*). For the steps done to the part, the buttons switch one off or on, move it earlier or later, or remove it. Pressing a step's name shows the part in Properties, where its numbers are edited.
- **Parameters** lists each parameter with its value, its unit, its default, and whether it belongs to each object (*instance*) and is *published*. Type a new value and press Enter: the part's object follows. A parameter that differs from its default is in bold. The flags can be read for now; they become editable when parameters grow formulas.

## Where to go next

The Studio page tells how to model solids, and the Families page how parameters and formulas make a part.

Sketches in a part can be held with **constraints** and dimensions: see [Sketch constraints](sketch-constraints.md).
