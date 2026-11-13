---
title: Families
order: 86
summary: Objects of your own with parameters — a table, a cabinet, a bench — made of bodies whose sizes follow formulas, placed like any element and exported as the IFC class you choose.
---

## What a family is

Families are now **parts**: a project with families opens them as parts with the same ids, and **Tab** on a placed one edits it in place (see the Parts page).

A **family** is an object you define once and place as many times as you need. It has **parameters** (a width, a height, a yes/no for a shelf) and **bodies**, each a closed outline pushed to a depth. Every corner, depth and offset is a number or a **formula** of the parameters, so one family gives a table of any size. Each object placed keeps its own values and follows the family when you change it.

A family is lighter than a type configurator: it has no layers or catalogue sizes, only the solids you describe. It belongs to the project. To use it in other projects, keep it in a library (below).

## Studio → Families…

**Studio → Families…** opens the families of the project, each with the number of objects that use it. **New** starts a box with the parameters W, H and D.

![The Families dialog: a table with its wireframe, the values to try and its parameters](shots/families-dialog.webp)

- **Name**, **IFC class** and **Description** come first. The class is what the object is exported as: Building element proxy (the default), Furnishing element, Column, Beam, Member, Plate, Slab, Wall, Flow terminal and others.
- The **wireframe** is built by the engine from the family as it stands, with each change. Type values under **Try values** to see another size; an empty one is the default.
- **Parameters** have a key (letters and digits, starting with a letter: `W`, `Height`, `HasShelf`), a label, a kind and a default. The kinds are Length, Number, Angle and Yes/no. Lengths are in metres and angles in degrees here. When you change a key (`W` to `Width`), every formula of the family that names it follows: corners, holes, depth, offset and conditions. Only whole names change (`W2` and `max(` are left alone), and a key another parameter already has is refused with a message.
- **Bodies** each have a plane: *Lying (xy)* is an outline on the floor pushed up, *Standing, front (xz)* and *Standing, side (yz)* stand up. The **corners** are rows of two cells, x and y. **Depth** is the distance the outline is pushed. **Offset x, y, z** moves the body from the object's origin. **Material** gives it a project material, and without one it is grey.

Press **Save** to keep the changes: the objects already placed follow. **Revert** goes back to the saved family.

## Formulas and conditions

A cell holds a number or a formula: `W - 0.04`, `H / 2`, `2 * T`. The names are the parameters' keys, and the functions are the ones of the parameter formulas (min, max, round, floor, ceil, sqrt, sin, cos, tan, atan2, if, clamp). Open **Formulas** at the bottom of the dialog for the list of names this family can use. A cell with a formula is shown in colour.

**Made when** is a condition: the body is made only when it is not 0. A yes/no parameter such as `HasShelf` works, and so does a comparison such as `W > 1.2`. In the table above the shelf has `HasShelf`, so objects with the shelf switched off have no shelf.

When a formula does not read, the engine says why under the picture, and **Save** refuses it. The wireframe stays at the last good one.

![A condition that does not read: the engine's sentence under the picture](shots/families-error.webp)

## Placing objects

**Studio → Family** places an object: choose the family in the bar, click on the plan. **R** turns it a quarter, **M** mirrors it, and the bar's buttons do the same. A ghost of the object, drawn as lines, follows the cursor on the plan, turned and mirrored as the bar says and as wide as the values in the bar make it. The bar also has one field per parameter: type a value for the next object, or leave it empty for the family's own. **Edit…** opens the family. **Place on a plan** in the dialog starts the same tool.

![The ghost of the table at the cursor, turned a quarter](shots/family-place-ghost.webp)

![The Family tool's bar: the family, its turn, its mirror and its values](shots/family-place-bar.webp)

An object is moved, turned, mirrored, copied and deleted like any element. It is stored on the storey where you placed it.

## An object's properties

Select an object to see its **Family** section in the properties. It shows the family and its IFC class, and **Edit family…** opens the dialog on it. Each parameter has its own value: the family's value is shown in grey, and an empty field is the family's. Type a value to give this object its own, and clear the field to go back. With several objects of a family selected, a value is set on all of them. Under it, **Rotation**, **Height above the storey** and **Mirrored** are the object's own fields.

![An object's properties: its own width, the shelf switched off, the family's values in grey](shots/family-properties.webp)

![The table objects in 3D, standing in the house](shots/family-3d.webp)

A family cannot be deleted while objects of it stand in the project: delete the objects first.

## Make family from sketch

Draw closed outlines on a plan, a section or an elevation with the Draft tools (closed polylines, rectangles and circles), select them and choose **Make family from sketch…**, in the **Family** group of the Draft tab or with a right-click. The dialog asks the **name**, the **depth** and the **IFC class**. A plan's outlines lie flat and a section's or an elevation's stand. With **Width, height and depth as parameters** on, the family has the parameters `W`, `H` and `D`, and the outline's right and top corners follow `W` and `H`, so it stretches. The drafting stays where it was. The new family opens in the editor.

![Make family from sketch: its name, depth and IFC class](shots/family-from-sketch.webp)

## Files and libraries

A family is exported as a **`.lapinfamily`** file (**Export…**) and read back with **Import…**. **Keep on this computer** keeps a copy that the **Catalog** offers in every project. In the desktop app, **Firm folders** in the dialog lists the families in your firm's folders; a click adds one to the project, and **Save to firm folder…** writes the chosen family into a writable folder. See *Firm folders in the library dialogs* in the Interface page.
