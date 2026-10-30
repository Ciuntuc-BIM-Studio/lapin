---
title: Studio
order: 85
summary: Model free-form solids, sketched on any face or plane, then push, pull, round, hollow, split and repeat them in 3D.
---

## The Studio tab

The **Studio** tab makes solids that no other tab builds: a bench, a canopy, a handrail, a fountain, a plinth on a facade. Each solid starts from a sketch or a primitive. You then change it in 3D with steps, and every step stays editable.

![The Studio tab](shots/ribbon-studio.webp)

A Studio solid has an IFC class, like any element. Choose it in its properties: a member, a plate, a covering, a railing, a slab or any other. Its quantities and its export follow that class.

## Where you sketch

You sketch a solid's outline with the **Draft** tools, as closed polylines, rectangles and circles. Lines and arcs that meet end to end also make a closed outline. A closed shape inside the outline is a hole.

You can sketch on four kinds of view:

- **A plan** draws on its storey's level.
- **A section** draws on its cut line, upright.
- **An elevation** or **a reference plane**: see *Reference planes* below.

## Making a solid

- **Extrude** (EXTRUDE) pushes the outline straight out. Type S for both ways from the sketch, T for a taper that leans the sides in, and Cut to make it cut the elements you selected before. To run it up to another element's face, use **Up to → a face…** in its properties, or **Extrude up to face**.
- **Sweep** (SWEEP) runs a profile along a path. TWist turns the section on the way, SCale grows it, and RIse lifts it. A circle that rises becomes a helix, going round TUrns times.
- **Loft** (LOFT) joins profiles drawn on different views. Type S to make it smooth through them. A line selected with the role *rail* bends it as a guide.
- **Revolve** (REVOLVE) turns a profile about an axis line.
- **Primitives** place a box, cylinder, sphere, cone, torus, wedge or pyramid with one click. Change their sizes and position in their properties.

![A handrail swept along a circle that rises 3 m in two turns](shots/studio-helix.webp)

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
## Changing a solid in 3D

These tools work on the faces and edges of the body as it is built now. The face under the mouse lights up. When a tool needs a value, a small box opens beside the cursor. Type the value and press <kbd>Enter</kbd>, or press <kbd>Esc</kbd> to drop it.

- **Push/Pull** (PU) moves a face out by a distance, or in when the distance is negative. On an extrusion, its end changes the depth and its side moves the sketch's edge under it. On a wall or any other element, pulling adds a solid in the face's material, and pushing cuts into the element.
- **Fillet edge** (FE) rounds the edge nearest your click, and **Chamfer edge** (CE) bevels it. Choose *all round the face* to do every edge of the face at once.
- **Shell** hollows the selected solids, with walls as thick as you type. It can stay closed, or be left open at its end, at its sketch or at both.
- **Split** cuts a solid in two by the plane of the face you pick, as far into the face as you type. Both parts keep the sketch. **Slice** keeps one side only.
- **Array** repeats the solid in a row, and **Polar array** repeats it round an upright.
- **Rotate 3D** turns the solid about an edge you pick. **Mirror 3D** mirrors it in a face's plane. **Move 3D** moves it by x, y and z.

![Push/pull: the top face lit up, and the distance typed beside the cursor](shots/studio-push-pull.webp)

![The edge filleted: the round follows the block when its depth changes](shots/studio-fillet.webp)

## The steps

The properties of a Studio solid list its steps in order. Each step can be switched off, moved earlier or later, changed or removed. Change the sketch, and each step is done again on the new body. A step that can no longer find its face or edge says so under **Problems**.

![A cylinder hollowed into a pipe and repeated three times: its two steps in its properties](shots/studio-steps.webp)

A polyline's properties list its sides. Type a side's length, and its end moves along it. The sides after it follow, up to the next side that runs the same way, so a rectangle stays a rectangle. A solid drawn on it follows too.

## Meshes from other programs

**File → Import mesh (OBJ, STL, glTF)…** brings in a mesh as references you can see and snap to. An OBJ or STL file does not say its unit or which way is up, so Lapin asks. Select a reference and click **Make Studio solid** to make it editable. It then takes push/pull, fillets, booleans and the other steps. The same tool works on walls, slabs and elements from IFC files.

## Booleans

**Subtract**, **Union** and **Intersect** combine any elements, with the first one selected as the target. The tools stay editable, and the result follows them. **Show operands** shows the hidden tools of a union for editing. **Remove operation** separates the elements again.
