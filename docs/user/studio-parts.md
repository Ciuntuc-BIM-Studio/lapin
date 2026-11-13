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

## Constrain part: a part's own sketches

A part whose bodies are written as corners (`[W, D]`, `[W - L, L]`…) can be turned into one with **sketches of its own**. In the part's tree, press **Constrain part** (also in the **Part** section of Properties). Each body's outline becomes a sketch on its plane, each corner held by horizontal and vertical dimensions that carry the same formulas, so the part keeps exactly the shape and volume it had. The tree then lists those sketches, with how free each one is, and the button goes away. It is one undo step.

![The tree after Constrain part: the sketches of the part, each fully constrained](shots/studio-part-constrained.webp)

## Editing a part's sketch

Click a sketch of the part in the tree (or double-click the part in its edit mode, which opens the first sketch with a body). The sketch is edited **in place**:

- the 3D camera turns to look straight at the sketch's plane, with a smooth move, and goes back to where it was when you leave;
- the rest of the part stays faded behind, and the sketch is drawn over the view: its shapes coloured by how free they are (blue free, dark fully held, red in conflict), its constraint glyphs and its dimensions;
- the bar at the bottom names the sketch, tells how many **degrees of freedom** it still has, and holds the tools.
- editing the sketch edits the **part**, so every object of it follows. The sketch is shown as the part stores it, with the part's **default** values: when this object has values of its own (a width of 1600 where the part's default is 1200), the bar says *Editing the part: values shown are its defaults*, and the object's own outline is drawn dashed behind.

![A part's sketch edited in place: the camera square to its plane, the shapes, the glyphs and the tool bar](shots/studio-part-sketch.webp)

- **Move a point** by dragging its square grip: the engine solves the constraints while you drag, and shows the result live; if the move is not possible it says why. One drag is one undo step.
- **Draw**: **Line**, **Rectangle**, **Circle** and **Arc (3 points)** in the bar. Click the points; a point near an existing one snaps to it and stays **coincident** with it (hold Ctrl while clicking to place it free), and a rectangle's sides stay horizontal and vertical. Each shape is one undo step, and the shapes already there keep their identity, so their constraints stay. **Delete** takes away the shape you click (or select a shape and press Delete). Esc puts the tool down.
- **Constraints**: the same tools as in a plan's sketch (Coincident, Horizontal, Equal, Fix… and the dimensions), picked on this sketch. Double-click a dimension to change its value, or type `=` and a formula of the part's parameters (`=W - 2*L`): the part follows, in every object of it. See [Sketch constraints](sketch-constraints.md).

![A line added with the Line tool](shots/studio-part-sketch-line.webp)

Leave with **Finish sketch**, **Esc** (once the tool is down) or **Tab**: you are back in the part's edit mode, and the camera as it was.

## Sketches and bodies in the tree

- **+ Sketch** adds a sketch on the plane chosen (top, front or right). Beside each sketch of the part are buttons to rename it and to remove it (the bodies drawn from it go with it).
- **+ Body from sketch** makes a body of a sketch's closed shape: choose the sketch and type its **depth**, a length or a formula of the parameters (`=H - T`). Under the body's step in the tree, its depth and its *made when* condition can be changed, and the body removed.

In the Families dialog, a body drawn from a sketch reads *Drawn from sketch* followed by the sketch's name, instead of a table of corners: the sketch is edited in the part, with **Edit the sketch in the part** (it needs an object of the part in the project).

## Where to go next

The Studio page tells how to model solids, and the Families page how parameters and formulas make a part.

Sketches in a part can be held with **constraints** and dimensions: see [Sketch constraints](sketch-constraints.md).
