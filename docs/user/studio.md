---
title: Studio solids and work planes
order: 85
summary: Turn drafted shapes into solids, give a shape a thickness with Ring, Box and Wall strip, and draw on reference planes anywhere in the model.
---

## Solids from a sketch

The **Studio** tab turns shapes you draft into solids. Draw the shape with **Draft** in a plan, a section, an elevation or a reference plane, then pick it with a Studio tool:

- **Extrude** (<kbd>EXT</kbd>) pushes a closed shape out by a depth. Closed shapes inside it become holes.
- **Sweep** (<kbd>SW</kbd>) moves a closed profile along a path, **Revolve** (<kbd>REV</kbd>) turns it about an axis and **Loft** (<kbd>LO</kbd>) joins profiles drawn on different views.

![The Studio tab](shots/ribbon-studio.webp)

Each solid stays tied to its sketch. Change the shape and the solid follows, with the same GlobalId. Select a solid to change its IFC class, its predefined type and its type in the properties panel. **Edit sketch** opens the view the sketch lies on.

## Ring, Box and Wall strip

These three tools give a drafted shape a thickness in its own plane, then a height away from that plane:

- **Ring** (<kbd>RING</kbd>) takes a circle, an arc or a closed outline and makes a ring or a tube. Its thickness lies **inside**, **outside** or **centred** on the shape.
- **Box** (<kbd>BOX</kbd>) takes a rectangle, a closed polyline or a circle and makes the walls round it. A **bottom** and a **top** are optional, each with its own thickness.
- **Wall strip** (<kbd>TWALL</kbd>) takes a line, a polyline or an arc and makes a strip of wall. Its thickness lies **left**, **right** or **centred**, seen walking along the line as you drew it. Its corners are mitred.

Start the tool and click the shape. The command line offers the options as letters: <kbd>I</kbd>, <kbd>O</kbd> and <kbd>C</kbd> (or <kbd>L</kbd>, <kbd>R</kbd> and <kbd>C</kbd>) for where the thickness lies, and for a box <kbd>B</kbd> and <kbd>T</kbd> to switch the bottom and the top on or off. Then type the thickness and the height, in metres, or press <kbd>Enter</kbd> to keep the value shown.

**On a plan**, the tools make native walls by default, of the wall type chosen in **Architecture**. A closed shape becomes a loop of walls joined at its corners, a polyline a chain of walls, and an arc or a circle curved walls. Where the thickness lies decides where the walls stand: the wall type's whole thickness goes on the side you chose. A box's bottom and top become floors. Type <kbd>S</kbd> before you pick the shape to make a Studio solid instead.

**On a section, an elevation or a reference plane**, the tools make a Studio solid of the IFC class IfcWall; a box's bottom and top are separate slabs (IfcSlab). The height goes along the plane's normal, towards you; a negative height goes away from you, as with **Extrude**. To give such a wall a type, select it and pick or make one under **Type** in the properties panel.

![A ring of native curved walls round a circle, a box with its bottom and top, and a wall strip, on the ground floor plan](shots/studio-thick-plan.webp)

Everything stays parametric. Select the solid and change its **Thickness**, **Height** and **Thickness lies** in the properties panel. For a box, tick **Bottom** or **Top** and set their thicknesses: Lapin adds or removes the slabs. A box with a bottom or a top is grouped with them in an assembly (IfcElementAssembly). Drag the shape's grips, or edit it on its view, and the solid is rebuilt.

![A box's properties: its thickness, height, bottom and top](shots/studio-box-properties.webp)

## Reference planes

A reference plane is a plane anywhere in the model with a view of its own. You draw on it with Draft, as on a section, and Studio makes solids on it. Make one from the **Studio** tab:

- **Plane from view** (<kbd>REFPLANE</kbd>) lies parallel to the plan, section, elevation or plane in front of you. Type how far it is towards you (a negative value goes away).
- **Plane on face**: in the 3D view, click a face of any element. The plane lies on the face, facing out of it.
- **Plane by 3 points**: in the 3D view, click three points on the model. The plane passes through them.

The new plane opens at once. It is listed under **Reference planes** in the project browser. Its drawing is a section on the plane, looking against the side it faces.

In the 3D view every reference plane is drawn as a translucent pane with its name, as large as the model is on it (or its **Drawn width (3D)** and **Drawn height (3D)**, 2 × 2 m at least), lit up under the cursor. Click it to select the plane, double-click it to open its view. With the plane selected, drag the arrow at its middle to move it along its normal: the offset reads beside the arrow, and what is drawn on the plane goes with it. **View → Reference planes** hides and shows the panes; they never go into exports, renders or pictures. In plans, sections and elevations, a plane that cuts the view is drawn as a dashed line with its name, across the view's extent: click it to select the plane, double-click it to open it. A plane parallel to the view is not drawn there (it cuts nothing of it). These lines are for the screen only: sheets never print them.

![A reference plane on the south facade, with a ring drawn on it](shots/studio-plane.webp)

Select the plane in the browser to move it. **Origin x**, **y** and **z** place it. **Faces** is the plan direction it faces, **Tilt** how far it leans back (90° lies flat, facing up) and **Spin** turns its drawing about its normal. **Move along its normal…** and **Turn about the vertical…** do the same in one step. What is drawn on the plane, and every solid made from it, moves with it.

![A reference plane's properties](shots/studio-plane-properties.webp)

Sections and elevations are planes too. A solid sketched on a section stands on its cut line, and follows the line when you move it. On a developed section, it stands on the piece it is drawn on. An elevation takes a plane of its own the first time you make a solid on it: at the front of the model, with its x as it is drawn now. Change its **Sketch plane depth** in the elevation's properties to move the plane, and its solids, along the view.

![The house with its Studio solids in 3D](shots/studio-3d.webp)

In IFC, a ring, a box's walls and a wall strip are IfcWall elements, a box's bottom and top IfcSlab elements, and a box with them an IfcElementAssembly with IfcRelAggregates; their types are written as IfcWallType and IfcSlabType. Every 3D export (IFC, Fragments, glTF, USD, COLLADA) writes a Studio solid where it stands in the model, whichever plane it was sketched on, with its holes; an assembly is a group node over its parts. Opening Lapin's own IFC again brings the solids back as references, in place, under their class, type and assembly, but no longer editable (their sketches are not in the file). The history of each solid is kept in Lapin's own property set, Lapin_Intent. Studio solids take simple types only, without layers: that is the loose modelling they are for; walls with layers are Lapin's native walls.
