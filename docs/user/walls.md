---
title: Walls
order: 20
summary: Draw walls corner by corner, change them in the properties panel, and set up wall types with their layers.
---

<!-- video walls -->

## Drawing walls

Open the **Architecture** tab and choose **Wall**, or type <kbd>WA</kbd>.

While the tool is on, the properties panel shows **New element**: the type the walls are drawn with. Choose another there before the first click, and see its layers, its thickness and how many walls already use it; **Edit types…** opens Wall types. Door, Window, Floor, Ceiling and Curtain wall show their types the same way.

![The Architecture tab](shots/ribbon-architecture.webp)

Click where the wall starts, then click at each corner. The next wall follows the mouse. While you draw, you can type a length and an angle in the boxes above the view instead of clicking. Press <kbd>Enter</kbd> or <kbd>Esc</kbd> to stop.

![Drawing walls: the next wall follows the mouse, with its length and angle shown](shots/wall-drawing.webp)

Where two walls meet, Lapin joins them by itself. Their layers run into each other the way they would be built.

- **Curved wall** (<kbd>WC</kbd>) draws a wall along an arc.
- **Curtain wall** (<kbd>CW</kbd>) draws a glazed wall with mullions and panels.
- Walls join by themselves where they meet: a corner as a mitre, a wall ending on another as a T, one crossing another as an X. At a T or an X the layers run into each other as built: the ending wall's core goes on through the other's finishes to its core, and its finishes stop at the other's face, meeting their like, so the plan shows one build-up with no line through it. **Join** (<kbd>JW</kbd>), **Mitre** and **Butt** change how two walls meet when that is not what you want; **Mitre new corners** makes butt the default on this computer. Either way, each layer turns the corner on its own: the finishes meet their like and run round unbroken, and the cores meet on the diagonal (mitre) or one core runs on to the other's outer face (butt), so a plan of layered walls shows clean corners. Each layer goes on into the other wall through its layers of a lower **Priority** and stops at the first as high or higher (see **Wall types** below), so by default cores meet cores and finishes stop at the faces; at a corner, a core of a higher priority runs through the other's, mitre or butt. A wall's **Cut priority** never takes it into a wall it is joined to: between joined walls it only decides which core runs through when both cores have the same priority (at a butt corner or a crossing).
- **Swap** changes which of the two walls runs through.

**The line you draw on.** While the Wall tool is on, the list in the command line's bar chooses the line you click: **Core centre** (the axis, as before), **Core, exterior face**, **Core, interior face**, **Exterior face** or **Interior face**. The exterior is the wall's right as you draw (draw round a building anticlockwise to have it outside). The wall's axis stays on its core, set off from the line, and walls drawn one after the other on the same face meet at the corner of that face.

## Changing a wall

Click a wall to select it. Its grips appear. Drag the ends to lengthen or shorten it, or drag the cross in the middle to move it.

![A selected wall with its grips](shots/wall-selected.webp)

The properties panel shows the selected wall. The top of the panel holds its type and layers. Below that are its length, height, base offset, storey and phase. Change any value and the model follows.

![A wall's properties](shots/wall-properties.webp)

Further down, **Dependencies** shows what is built from the wall and what the wall is built from. **Used by** lists the elements that change when the wall changes: the walls it joins, its doors and windows, a wall attached to it. Click a name to select that element. **Built from** lists the elements the wall depends on, then its type and materials. When you delete an element that others depend on, a message names the ones that were rebuilt; <kbd>Ctrl</kbd> <kbd>Z</kbd> brings everything back.

**Flip** turns the wall's layers to the other side. You can also click the small arrows shown next to a selected wall in the plan.

A wall recognised from an imported IFC whose end the file cuts at an angle keeps that cut: **Start cut angle** and **End cut angle** in its properties, in degrees from square. A join at that end takes its place.

**Hatch alignment** sets how this wall's layers are hatched in plans. **material** follows each material's own setting. **axis** lays every layer's hatch along the wall's axis, starting from the axis's start, so a brick wall at an angle shows its courses along it and they run on from one layer to the next. **view** starts every hatch from the view's origin, unturned.

### A wall by its profile

**Profile…** in a wall's properties draws its top in elevation, seen from its start to its end: a gable, a slope, steps, a wall that follows a stair. Drag the points (**Shift** keeps a point's height), click the top line to add one, right-click a point to take it out, or type them in the table in millimetres. **Start from** gives a **Gable**, a **Slope** or **Steps** up to the height you type. A step is two points one over the other. **Apply** shapes the wall; **Flat top** makes it flat again.

The wall stays a wall: its thickness, its layers, its joins and its doors and windows are as they were, and it exports to IFC with its shape.

![A gable drawn in the profile editor](shots/wall-profile-editor.webp)

![The house's west wall as a gable, its window kept](shots/wall-profile-3d.webp)

## Wall types

A wall type is a set of layers, from the face on the right of the way the wall is drawn (the outside, when you draw round a building anticlockwise) to the face on its left. Each layer has a material and a thickness, and one layer is the **core**. The wall's line runs along the core. To edit the types, choose **Architecture → Wall types**. A layer other than the core may have a height of its own: **From** and **Up to** set how high it goes over the wall's base (a tiled dado up to 1500, a cladding from 300 up); empty, it runs the whole height.

The **Priority** column (0 to 100) says how the layer meets another wall's layers where the walls join: it goes on through the other's layers of a lower priority and stops at the first as high or higher. Empty, the layer takes its material's priority in **Manage → Cut order**, else 50 for the core and 0 for the other layers (the number shown greyed). So a concrete core of 100 is not interrupted by a stud core of 99 and runs through the stud wall, and a hard plaster of 60 holds the core of a wall ending on it. The IFC export writes it as the layer's Priority, and the import reads it back.

The **Note** column says how the layer is laid in this type (*in a continuous layer, Baumit SupraFix*): it belongs to the type, not to the material, and a build-up tag writes it after the layer; IFC carries it as the layer's Description. When the layer's material has catalogue thicknesses (**Manage → Materials → Data**), the small arrow beside the thickness offers them, and a warning sign marks a layer of another thickness.

A layer other than the core may also change material up its height: the **layers** button at the end of its row adds a piece (tiles, 2100 by default), from the layer's bottom up; add more for a band (paint to 900, tiles to 1500), and above the last piece the layer goes on in its own material to its top. Each piece is drawn, cut in plan and sections, and measured in its own material.

A layer other than the core may have a thickness of 0, for a material too thin to model but that must be measured: a waterproofing membrane, a vapour barrier, paint. It builds no solid; plans and sections draw it as a heavy dashed line where it lies between two layers (on a face, the face's line is its own), the Type assemblies build-up names it like the others, and it is measured by area: the side of the layer it lies on, net of openings, in the wall's quantities and the quantity takeoff by material.

![A plaster layer with ceramic tiles up to 2100 and the plaster above them](shots/wall-layer-parts.webp)

![Wall types: the layers of a brick wall](shots/wall-types.webp)

The legend above the layers shows the wall in plan the way a selected wall shows it: the dashed axis with an arrowhead the way it was drawn, **R** on its right face and **L** on its left face. The layers are numbered from 1, on the R face, to the last, on the L face, with the same numbers as the rows of the table; the core, which carries the wall's line, is shaded. A wall turned with **Flip** has its layers the other way round, from L to R. A message from a save that was refused goes away as soon as you change the type again.

- **+ Layer** adds a layer. The arrows move a layer up or down.
- **Zones in height** splits the core from bottom to top, for example a first course, the masonry and a ring beam.
- **Lintels** chooses the lintels set over doors and windows: precast or cast in place, with their bearing and height.
- **Draw with it** closes the dialog and starts drawing walls of this type.

A change to a type changes every wall of that type.

**Import…**, **Export CSV** and **Export JSON**, under the list of types, take the wall and floor types out of a project and into another, or into a spreadsheet and back: the CSV has one row a layer (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), a type's rows one after another, and Excel's semicolons and decimal commas are read too. Brought in, a type of the same name is changed to the file's layers and any other is made, in one step to undo; a material the project lacks is made by its name. The **Type assemblies** schedule (Schedules → + → Type assemblies) lists every wall and floor type with its mark, its total thickness, how many elements use it, and its layers in order.

## Wall sweeps and reveals

**Architecture → Wall sweep** (<kbd>WB</kbd>) runs a profile along a wall's face: a cornice, a band, a plinth, a sill. **Wall reveal** (<kbd>RV</kbd>) cuts one into the wall: a groove, a shadow gap. Choose the profile and the height in the command line's bar, then click the wall's face. In 3D the sweep goes at the height you click; in a plan it goes at the height typed, on the side you click. **from the top** measures the height down from the wall's top, for a cornice under the eaves. **Sweep on selected walls** and **Reveal on selected walls** put the same on every wall selected, on its exterior face.

A sweep runs the face's whole length and stops at the doors and windows it would cross. Where two walls meet in a corner and both carry a sweep of the same profile at the same height on the faces that meet, the two are mitred into one another, one moulding round the corner. Select a sweep to change it in Properties: its profile, its material, its face (**Flip side**), its height, its offset from the face, where it starts and ends along the wall, and whether it stops at openings. A wall's properties list the sweeps and reveals it carries.

Sweeps and reveals go with their wall when it is copied, moved to another level or deleted. They are measured by length and volume (for a reveal, the volume taken out). In IFC a sweep is an IfcCovering MOLDING and a reveal an IfcOpeningElement RECESS in its wall; both come back as they were.

![Cornices, bands and a reveal on a house's outer walls](shots/wall-sweeps-3d.webp)

### Sweeps on the wall type

A sweep every wall of a type should carry (a plinth band, a cornice, a shadow gap) belongs to the type. In **Architecture → Wall types**, under the layers, **Sweeps and reveals** lists them, one row each: the profile, **Wall sweep** or **Wall reveal**, the face (R, the face of the type's first layer, or L, as the legend names them), the height over the wall's base (or under its top with **From the top**), the offset out from the face, a sweep's material and its returns. **Wall sweep** and **Wall reveal** under the table add a row. Save the type: every wall of it gets them, and so does a wall drawn with the type or changed to it. They are sweeps like the others: measured, listed in the Wall sweeps schedule, mitred round the corners where walls of the type meet, written to IFC. A flipped wall's sweeps change face with its layers. **Export JSON** under the list of types carries them, with the profiles' outlines and the materials by name (the CSV has only the layers).

Select one on a wall: Properties says **From the type**. Change its height, its offset or any other of its fields and that field becomes the wall's own (**Own: height**): later changes to the type leave it as it is. **Back to the type** gives it the type's values again. **Switch off on this wall**, or deleting it, leaves the type's sweep off that wall; **Switch the type's sweeps on again**, in the wall's **Sweeps and reveals**, brings them back.

![A sweep on the wall type, in the Wall types dialog](shots/wall-type-sweeps.webp)

### Returns

A sweep that reaches a free end of its wall (no wall joined there) returns: mitred at the end, its profile turns round the corner onto the end of the wall and stops in the plane of the face, so the moulding is closed instead of showing its cut. **Returns at free ends** in Properties chooses both (the default for a sweep), at the start, at the end or none, for square ends. A reveal never returns. At a corner joined to another wall a sweep does not return: it runs to the corner, or into a like sweep on the other wall. At doors and windows it stops square; tick **Returns at openings** to return it round the jambs as well. **Set back from the start** and **Set back from the end** stop it short of the face's ends (square there), and **Profile upside down** turns its profile over.

### Moving and copying a sweep

Select a sweep and use **MOVE** or **COPY** as for any element; its wall stays. What the move goes along the wall slides the sweep along it (a sweep along the whole face then gets its own start and end); what it goes across the wall is left out. Moved up or down in an elevation or a section, its height changes. A copy lands on the same wall, shifted, or on another wall when it lands inside it, on the face it lands on. **ROTATE** and **MIRROR** keep it on its wall: mirrored across the wall's axis, it goes to the other face. A sweep of the type you move keeps its new place as its own; moved onto another wall, it becomes that wall's own sweep and the type's is switched off on the first.

### Profiles

**Manage → Profiles** holds the project's profiles, the outlines that sweeps, reveals and strip footings are swept with. **New profile** starts from the catalogue with its sizes: a rectangle, a chamfered band, a cornice, a half-round, a sill nose, a band with a drip; a V-groove, a rectangular or a round groove; for footings, rectangular, stepped in 2 or 3 steps, with a sloped top, eccentric, with a stem. Or choose **Typed corners**: each corner u v in millimetres, a semicolon between them, a third number to make the side from that corner an arc. **From a drafted outline** reads a closed polyline or rectangle drawn with Drafting and selected in its view. The circle in the preview is the insertion point: for a sweep it lies on the wall's face at the sweep's height, u going out of the face (into the wall for a reveal). Change a profile and everything that uses it follows.

![The Profiles dialog](shots/profiles.webp)

### Cladding

Model cladding with the wall's own layers, not with sweeps. Add a finish layer on the outside of the wall type (stone, timber boards, fibre cement, an ETICS), with its thickness and material. **From** and **Up to** give it a height of its own (a stone plinth up to 600, render above it), and the layer's **layers** button changes its material in bands up its height. For cladding that stands apart from the wall, draw a wall of its own on the face (the wall tool's line option **Exterior face**) with a type made of the cladding's layers. Cladding laid board by board (vertical or horizontal boards, battens) is not modelled yet.

## Walls under a roof

Walls on the same level as a roof stop under it by themselves and rise to its gables. For a wall on another level, select it and choose **Architecture → Attach to roof** (<kbd>AR</kbd>), then click the roof — or any other body the wall should rise to: a Studio solid, an imported element. See [Floors and roofs](floors-roofs.md).
