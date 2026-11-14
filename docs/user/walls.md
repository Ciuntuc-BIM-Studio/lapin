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
- **Curtain wall** (<kbd>CW</kbd>) draws a glazed wall with mullions and panels; its type can take their real shape from a [profile library](profiles.md).
- Walls join by themselves where they meet: a corner as a mitre, a wall ending on another as a T, one crossing another as an X. At a T or an X the layers run into each other as built: the ending wall's core goes on through the other's finishes to its core, and its finishes stop at the other's face, meeting their like, so the plan shows one build-up with no line through it. **Join** (<kbd>JW</kbd>), **Mitre** and **Butt** change how two walls meet when that is not what you want; **Mitre new corners** makes butt the default on this computer. Either way, each layer turns the corner on its own: the finishes meet their like and run round unbroken, and the cores meet on the diagonal (mitre) or one core runs on to the other's outer face (butt), so a plan of layered walls shows clean corners. Each layer goes on into the other wall through its layers of a lower **Priority** and stops at the first as high or higher (see **Wall types** below), so by default cores meet cores and finishes stop at the faces; at a corner, a core of a higher priority runs through the other's, mitre or butt. A wall's **Cut priority** never takes it into a wall it is joined to: between joined walls it only decides which core runs through when both cores have the same priority (at a butt corner or a crossing). A wall you move, turn, mirror, copy or array joins what it lands on the same way, in the same undo step, and a join it leaves behind goes; walls moved or copied together keep their joins among them as they were, and so do an array's items (they join what they land on, not each other). A wall recognised from an imported IFC keeps the file's joins when you move it; a copy of it is your own wall and joins.
- **Swap** changes which of the two walls runs through.
- **Join automatically at start** and **Join automatically at end**, in a wall's properties (a beam's and a strip footing's too), say whether each end joins what it meets by itself; they apply to every wall selected. An end turned off keeps the joins it has (**Join** still makes one there, and the join tools take it off), makes no new one, and no other wall's end is brought to it, so the wall keeps its length and its ends whatever is drawn or edited round it. Right-click a selected wall's end grip for **Don't join at this end** (or **Join automatically at this end**, which joins what it meets there at once). The ends of walls imported from an IFC file start off: they stay as the file has them when you change their height, type or length, and their doors and windows stay where they are in plan when their start moves.

**The line you draw on.** While the Wall tool is on, the list in the command line's bar chooses the line you click: **Core centre** (the axis, as before), **Core, exterior face**, **Core, interior face**, **Exterior face** or **Interior face**. The exterior is the wall's right as you draw (draw round a building anticlockwise to have it outside). The wall's axis stays on its core, set off from the line, and walls drawn one after the other on the same face meet at the corner of that face.

## Changing a wall

Click a wall to select it. Its grips appear. Drag the ends to lengthen or shorten it, or drag the cross in the middle to move it.

![A selected wall with its grips](shots/wall-selected.webp)

The properties panel shows the selected wall. The top of the panel holds its type and layers. Below that are its length, height, base offset, storey and phase. Change any value and the model follows. Each wall's values are its own. Its **Thickness** comes from its type's layers: type a new one and the selected walls take the type with the same build-up and that thickness, the core resized and the finishes kept. Lapin uses the project's type if one has exactly that build-up, else it makes one named after theirs (*Partition wall 130mm* becomes *Partition wall 150mm*). The other walls of the old type stay as they are, and <kbd>Ctrl</kbd> <kbd>Z</kbd> undoes it in one step.

![A wall's properties](shots/wall-properties.webp)

### The wall's top

A wall goes up to the level above by itself. Its **Top**, in its properties, says where it goes:

- **Storey above** (a new wall's default): to the level of the storey above its own. On the topmost storey, to that storey's own top (its level and height).
- a storey of the project: to that storey's level, two storeys up for a wall through a double-height hall.
- **Unconnected height**: to a height of its own, the **Height** you type.

![A wall's top: the storey above, its offset and the height it comes to](shots/wall-top.webp)

**Top offset** is added to the level it goes to: −300 stops a wall under a floor's structure, +900 takes a parapet over the roof's level. The **Height** shown below is worked out from the storeys every time, so raising or lowering a storey (its **Base level** in its properties) moves the tops of the walls going to it, and lowering the storey a wall stands on lowers its base and keeps its top. Their joins, layers that stop at a height, doors and windows, sweeps, quantities, schedules, drawings and the IFC export all follow. A change reaches every selected wall in one step.

A top that would come at or below the wall's base is refused; one that comes so later (the storey above brought down to the wall's) leaves the wall at its own height, and **Problems** says so. Deleting a storey lets the walls going to it stand unconnected at the height they had. **Copy to storeys** sends a copy as many storeys up as the original went. Typing a height in a wall's properties makes it unconnected. **Attach to roof** still takes the wall up to a roof's underside from wherever its top is.

While the Wall tool is on, the list after the line's in the command line's bar chooses the top of the walls you draw: **as the type**, **storey above** or a storey with an offset, or **unconnected height** with a height. A wall type gives its walls a top too: **Wall top** in **Wall types**, under the lintels, is the storey above with an offset (−450 for walls under the beams) or an unconnected height (1100 for a parapet type). Walls drawn before this stay at the height they had (unconnected); walls from an IFC file stand as the file says, and Lapin's own IFC files bring their tops back.

Further down, **Dependencies** shows what is built from the wall and what the wall is built from. **Used by** lists the elements that change when the wall changes: the walls it joins, its doors and windows, a wall attached to it. Click a name to select that element. **Built from** lists the elements the wall depends on, then its type and materials. When you delete an element that others depend on, a message names the ones that were rebuilt; <kbd>Ctrl</kbd> <kbd>Z</kbd> brings everything back.

**Flip** turns the wall's layers to the other side. You can also click the small arrows shown next to a selected wall in the plan.

A wall recognised from an imported IFC whose end the file cuts at an angle keeps that cut: **Start cut angle** and **End cut angle** in its properties, in degrees from square. A join at that end takes its place.

**Hatch alignment** sets how this wall's layers are hatched in plans. **material** follows each material's own setting. **axis** lays every layer's hatch along the wall's axis, starting from the axis's start, so a brick wall at an angle shows its courses along it and they run on from one layer to the next. **view** starts every hatch from the view's origin, unturned.

### A wall by its profile

**Profile…** in a wall's properties draws its top in elevation, seen from its start to its end: a gable, a slope, steps, a wall that follows a stair. Drag the points (**Shift** keeps a point's height), click the top line to add one, right-click a point to take it out, or type them in the table in millimetres. **Start from** gives a **Gable**, a **Slope** or **Steps** up to the height you type. A step is two points one over the other. **Apply** shapes the wall; **Flat top** makes it flat again.

The wall stays a wall: its thickness, its layers, its joins and its doors and windows are as they were, and it exports to IFC with its shape.

![A gable drawn in the profile editor](shots/wall-profile-editor.webp)

![The house's west wall as a gable, its window kept](shots/wall-profile-3d.webp)

### A curtain wall's boundary and members

**Profile…** in a curtain wall's properties draws its boundary in elevation, as for a wall: a gable, a slope, steps, or any outline, with points to drag, add or take out and a table in millimetres. The wall rises to its highest point. Its grid gains a line where the boundary bends (a bend within a mullion and a half of a grid line is moved onto it); each bay keeps the transoms that fit under its lowest point; the panel above the last one fills up to the boundary, and an edge member follows it, sloped; the mullions are cut to it. The type's settings stay, and **Profile…** opens the boundary again to change it, with no rebuilding; **Flat top** gives the rectangle back. It shows in 3D, elevations and sections, the schedules measure the panels and members as drawn, and the IFC carries them as parts.

![A gable drawn as a curtain wall's boundary](shots/curtain-boundary-editor.webp)

![The curtain wall under its gable, in 3D](shots/curtain-boundary-3d.webp)

**Members…** gives chosen mullions and transoms a profile of their own. The wall's elevation shows every member: click one (<kbd>Shift</kbd> or <kbd>Ctrl</kbd>/<kbd>⌘</kbd> adds more, or take all the mullions or all the transoms), choose a profile from the [profile libraries](profiles.md) or a plain box, its **Rotation** and **Mirror**, its alignment across the member (front or back face on its plane) and along it (start or end edge on the grid line or level), and its offsets across and out of the building; the picture beside shows the section as it sits. **Apply to the chosen** gives them the style, **Back to the type's** takes it off. A type's own profile has the same place fields in **Curtain walls…**, for every wall of the type. The profile stays a copy in the project: **Update from the library** changes every member made of it, in this wall and in others.

![Members: a profile placed on the mullions chosen](shots/curtain-members-dialog.webp)

![Two mullions with a profile of their own, in 3D](shots/curtain-members-3d.webp)

**Panels and doors…** gives chosen cells a panel of their own or a door. The wall's elevation shows every cell as the engine builds it: click one (<kbd>Shift</kbd> or <kbd>Ctrl</kbd>/<kbd>⌘</kbd> adds more). A **panel type** is saved once in the project and given to cells of any curtain wall: glass, solid, a **framed infill** (a frame of its width and depth round the cell, the glass or solid infill set back from its outer face by the inset) or **open** (no panel); its thickness, its offset out of the axis and its materials (the curtain wall type's when none is chosen). **Give to the chosen** applies it, **The type's panel** takes it off; editing a panel type changes every cell of it. **Door in the chosen** replaces each chosen cell by a door: a new curtain wall door (single or double, swinging or sliding, its width and height, glazed or solid leaves, its frame profile from the [profile libraries](profiles.md)) or any door type of the project. **Fill the cell** makes it the cell's size; otherwise it keeps its type's, stands left, centred or right in the cell with an offset, and gets a transom over it and a mullion beside it where glass is left, glazed with the cell's panel. In the bottom row a door stands on the floor and the sill transom is cut away under it. The settings are kept by cell, so they follow when the grid changes: a door filling its cell grows or shrinks with it, and one whose cell is gone says so and comes back with it. The door is a door: select it to flip its hand or side, change its type, or move it to another cell (Move along the wall); tags, door schedules (at the size it stands) and the IFC export (an IfcDoor that is part of its IfcCurtainWall) count it.

![Panels and doors: a framed spandrel and a door in the chosen cells](shots/curtain-cells-dialog.webp)

![A curtain wall with a door and framed spandrels, in 3D](shots/curtain-cells-3d.webp)

Under a boundary drawn with **Profile…** a door goes into a trimmed cell wherever its rectangle fits under the boundary: the mullions beside it rise to the boundary, the panes beside them fill up to it, a transom spans its head and the glass over it follows the slope. Filling such a cell, its head stops a transom under the boundary's lower side. Where it does not fit, Lapin says by how much and what to do (lower it, move it to the higher side, or choose a higher cell). A framed panel in a trimmed cell is framed along its outline, sloped side too. An elevation that faces a door's exterior draws its opening symbols, dashed, as for any door or window.

![A door under a sloped curtain wall boundary, with framed panels along the slope, in 3D](shots/curtain-cells-gable.webp)

![The door's opening symbol in the elevation](shots/curtain-cells-elevation.webp)

## Wall types

A wall type is a set of layers, from the face on the right of the way the wall is drawn (the outside, when you draw round a building anticlockwise) to the face on its left. Each layer has a material and a thickness, and one layer is the **core**. The wall's line runs along the core. To edit the types, choose **Architecture → Wall types**. A layer other than the core may have a height of its own: **Layer from** and **Layer up to** set how high the layer goes over the wall's base (not the wall's own top: the placeholder says *wall top* where it runs to the wall's top; a tiled dado up to 1500, a cladding from 300 up); empty, it runs the whole height.

The **Priority** column (0 to 100) says how the layer meets another wall's layers where the walls join: it goes on through the other's layers of a lower priority and stops at the first as high or higher. Empty, the layer takes its material's priority in **Manage → Cut order**, else 50 for the core and 0 for the other layers (the number shown greyed). So a concrete core of 100 is not interrupted by a stud core of 99 and runs through the stud wall, and a hard plaster of 60 holds the core of a wall ending on it. The IFC export writes it as the layer's Priority, and the import reads it back.

The **Note** column says how the layer is laid in this type (*in a continuous layer, Baumit SupraFix*): it belongs to the type, not to the material, and a build-up tag writes it after the layer; IFC carries it as the layer's Description. When the layer's material has catalogue thicknesses (**Manage → Materials → Data**), the small arrow beside the thickness offers them, and a warning sign marks a layer of another thickness.

A layer other than the core may also change material up its height: the **layers** button at the end of its row adds a piece (tiles, 2100 by default), from the layer's bottom up; add more for a band (paint to 900, tiles to 1500), and above the last piece the layer goes on in its own material to its top. Each piece is drawn, cut in plan and sections, and measured in its own material.

A layer other than the core may have a thickness of 0, for a material too thin to model but that must be measured: a waterproofing membrane, a vapour barrier, paint. It builds no solid; plans and sections draw it as a heavy dashed line where it lies between two layers (on a face, the face's line is its own), the Type assemblies build-up names it like the others, and it is measured by area: the side of the layer it lies on, net of openings, in the wall's quantities and the quantity takeoff by material.

The **Category** column says what the layer is in this type: **Structure**, **Finish**, **Insulation**, **Membrane** or **Other**. Empty (*material's*), the layer takes its material's category (**Manage → Materials**). A view style leaves the finishes out by it, so a cement board marked **Finish** goes with the plaster, and a plaster marked **Other** stays (see [Views](views.md)). IFC carries it as the layer's Category.

The **Into floors** column lets a layer go on into the floors' layers, as where walls join. With **↓** ticked, the layer goes down from the wall's base, through the layers of the floor the wall stands on that have a lower priority, to the top of the first layer as high or higher. By default the brick core (50) goes through the floor finish and the screed (0) and stands on the concrete slab (50). With **↑** ticked, the layer goes up through the lower layers of the floor above it (a ceiling plaster) to the underside of the first layer as high or higher. The floor loses what the wall takes, and the quantities of both follow. A floor layer's priority is set in **Floor types**. To make a plaster go down to the screed, give the plaster a priority above the floor finish's and below the screed's (for example a plaster of 10, a floor finish of 0 and a screed of 20). A new wall type's core goes down by default, and so does the default wall type a new project makes for its first wall, so it stands on the structural slab. Types made before stay as they were until you tick the boxes. Only straight walls go into the floors; curved walls stand on the floor as before. The IFC export writes the bodies as built, keeps the layer sets, and marks each such layer (Lapin_LayerJoins: ExtendToFloorBelow, ExtendToFloorAbove). The import reads the marks back.

![A brick core through the floor finish and the screed, on the concrete, in a section](shots/wall-into-floor-section.webp)

![A plaster layer with ceramic tiles up to 2100 and the plaster above them](shots/wall-layer-parts.webp)

**Hatches at hand.** Beside each layer's material, a swatch shows how the material is hatched where it is cut. Click it to set the material's hatches there, without going to **Manage → Materials**: its hatch where a section cuts it, where a plan cuts it and where an elevation sees it, each with its scale, angle, alignment, fill and pen, as on the **2D graphics** tab of Materials. They are the material's own, so every type and element made of it changes too, and each change is one step to undo. **Floor types** have the same swatch. A press outside it or <kbd>Esc</kbd> closes it. **Open in Materials…**, at the foot of the swatch's panel, opens **Manage → Materials** on that material's **2D graphics** tab; what you have typed in Wall types (or Floor types) and not yet saved is kept, and **Back to Wall types** at the top of Materials returns to it as you left it.

![A layer's hatch swatch open in Wall types: the material's cut and elevation hatches](shots/layer-hatch.webp)

![Wall types: the layers of a brick wall](shots/wall-types.webp)

The legend above the layers shows the wall in plan, a section across it, the way a selected wall shows it: the dashed axis with an arrowhead the way it was drawn, **R · exterior** on top and **L · interior** at the bottom. The layers run as the rows of the table, top to bottom: layer 1 at the top, on the R face, to the last, on the L face, with the same numbers; the core, which carries the wall's line, is shaded. R is the face on the right of the drawn direction (where layer 1 goes), and it is the exterior when the wall is drawn anticlockwise round the building, as Lapin draws them; for a wall drawn the other way round, the faces swap. **Reverse layers**, beside **+ Layer**, runs the rows the other way (the layer that was on R goes to L); Lapin asks first, because once the type is saved all its walls swap faces: what was exterior becomes interior. A wall turned with **Flip** has its layers the other way round, from L to R. A message from a save that was refused goes away as soon as you change the type again.

**Preview.** Under the legend, **Preview** shows the type as you edit it, before you save it, the way the drawings will show it: a sample wall of the type, 3 m long, with a shorter wall of the same type joined to its middle in T, in **Plan**, **Section** and **3D** side by side (one at a time, with tabs, when the dialog is narrow). Lapin builds the sample as it builds any wall, joins and layer priorities included, and draws the plan and the section as it draws a view: with the project's hatches, pens and view styles, at the **View scale** (1:20, 1:50 or 1:100) and the **Detail level** (coarse, medium, fine) chosen above the panes. In the plan, **R · exterior** is on top, as layer 1 is in the table; in the section, R is on the right. Move the mouse over a layer's row and that layer lights up in all three. **Length** and **Height** set the sample's size; with no height, the sample goes up to the storey above, or as the type's **Wall top** says. The 3D turns with the mouse and zooms with the wheel. The preview is made apart from the project: nothing is added to it and there is nothing to undo.

![The Wall types preview: the type in plan, in section and in 3D, a layer lit up](shots/wall-type-preview.webp)

- **+ Layer** adds a layer. The arrows move a layer up or down.
- **Zones in height** splits the core from bottom to top, for example a first course, the masonry and a ring beam.
- **Lintels** chooses the lintels set over doors and windows: precast or cast in place, with their bearing and height. A precast lintel can be a beam type of **Structure types**, its section, lengths, material and bearing taken from it (see [Beam types as lintels](structure.md#beam-types-as-lintels)).
- **Draw with it** closes the dialog and starts drawing walls of this type.

A change to a type changes every wall of that type.

**Import…**, **Export CSV** and **Export JSON**, under the list of types, take the wall and floor types out of a project and into another, or into a spreadsheet and back: the CSV has one row a layer (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), a type's rows one after another, and Excel's semicolons and decimal commas are read too. Brought in, a type of the same name is changed to the file's layers and any other is made, in one step to undo; a material the project lacks is made by its name. The JSON carries more than the layers: each type's parameters (its IFC properties, such as *Pset_WallCommon.FireRating* and *IsExternal*, and sets of your own) and the whole definition of every material its layers use (colour, the cut, plan and elevation hatches with their scale, angle, alignment, fill and pen), so the other project makes them as they were. When the file has a material or a type the project already has under the same name and defined otherwise, Lapin lists them before importing, with what differs (*section hatch*, *colour*, *layers*, *parameters*…), and you choose for each: a material **Keep the project's** (the default), **Update from the library** (changed in place: everything that uses it follows) or **Keep both** (the file's is made as *Name (2)* and the imported types use it); a type **Update the project's** (the default: its walls follow), **Keep both** or **Skip**. **Apply to all…** sets one choice for the whole column. What the import did (types made, changed, skipped; materials made, updated, kept) is listed under the buttons, and a failure is always shown. Importing into a project with no wall type at all works the same, and the first type brought in opens in the editor. The CSV stays a spreadsheet's: layers only. **Type library (.lapintypes)…** opens **Manage → Type packs…**, for a library of any kind of type (walls, floors, roofs, doors, windows, columns, beams…) with everything it is made of: see [Extensions](extensions.md).

![Importing types: a material and a type of the same name that differ, a choice for each](shots/type-import-choices.webp)

A type's parameters are in its properties (select an element of the type: **Type parameters**). A parameter the type has for itself, such as a fire rating, is shared by every element of it and goes to IFC on the type. Tick **Instance default** for one each element should have of its own, such as a renovation status (new, existing, demolish) or a room's use: every element given the type afterwards (drawn with it, or changed to it) gets a copy in the same step to undo, which you can then change on the element, and IFC writes it on the elements, not on the type. Elements placed before keep what they had until you press **Apply instance defaults to elements of this type**, which gives each one the defaults it lacks (an element's own value stays). Phases also give IFC each element's *Status* (NEW, EXISTING, DEMOLISH, TEMPORARY) in its common property set, unless you set one yourself.

The **Type assemblies** schedule (Schedules → + → Type assemblies) lists every wall and floor type with its mark, its total thickness, how many elements use it, and its layers in order.

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

Model cladding with the wall's own layers, not with sweeps. Add a finish layer on the outside of the wall type (stone, timber boards, fibre cement, an ETICS), with its thickness and material. **Layer from** and **Layer up to** give it a height of its own (a stone plinth up to 600, render above it), and the layer's **layers** button changes its material in bands up its height. For cladding that stands apart from the wall, draw a wall of its own on the face (the wall tool's line option **Exterior face**) with a type made of the cladding's layers. Cladding laid board by board (vertical or horizontal boards, battens) is not modelled yet.

## Walls under a roof

Walls on the same level as a roof stop under it by themselves and rise to its gables. For a wall on another level, select it and choose **Architecture → Attach to roof** (<kbd>AR</kbd>), then click the roof — or any other body the wall should rise to: a Studio solid, an imported element. See [Floors and roofs](floors-roofs.md).
