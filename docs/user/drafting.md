---
title: Drafting and the command line
order: 80
summary: Type commands as in CAD, snap to points, move and copy, draw lines, and annotate with dimensions, text and tags.
---

## The command line

Type a tool's letters anywhere in the window, then press <kbd>Enter</kbd>. For example, type <kbd>WA</kbd> for a wall, <kbd>CO</kbd> to copy, <kbd>M</kbd> to move, or <kbd>L</kbd> for a line. The command line at the bottom shows what the tool needs next. You can click a point or type a length, an angle or coordinates.

Typed coordinates are in the project's unit, as the lengths beside the cursor are: in a project in millimetres, `@4000,4000` after the first corner of a rectangle makes it 4 m by 4 m, and `4000<90` is 4 m straight up.

![Copying a wall: after the base point, the command line and the live input show the length and angle to the mouse](shots/command-line.webp)

Every tool shows its letters in its tooltip; you can change them in **View → Keyboard shortcuts…** (see [The interface and shortcuts](interface.md)). These are the ones used most:

| Model | | Draft | |
|---|---|---|---|
| Wall | <kbd>WA</kbd> | Line | <kbd>L</kbd> |
| Door, window | <kbd>DR</kbd>, <kbd>WN</kbd> | Polyline | <kbd>PL</kbd> |
| Floor | <kbd>SB</kbd> | Rectangle, circle, arc | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Roof | <kbd>RF</kbd> | Move, copy | <kbd>M</kbd>, <kbd>CO</kbd> |
| Stair, room | <kbd>ST</kbd>, <kbd>RM</kbd> | Rotate, mirror, scale | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Column, beam | <kbd>CL</kbd>, <kbd>BM</kbd> | Offset, trim, extend | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Section | <kbd>SE</kbd> | Fillet, chamfer | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Measure | <kbd>ME</kbd> | Hatch, text | <kbd>H</kbd>, <kbd>T</kbd> |
| Fit everything | <kbd>ZE</kbd> | Erase | <kbd>E</kbd> |

## Measuring

**Measure** (<kbd>ME</kbd>, the second quick tool, or **MEASURE** or **DIST** on the command line) measures without drawing anything: click points in a plan, a section or an elevation, snapping as you go, and press <kbd>Enter</kbd>. While you click, the live input shows each leg's length and angle; at the end the notice gives the total length, each leg (for one leg, its Δx, Δy and angle) and, from three points on, the area they enclose.

## Snaps and helpers

Lapin snaps to the ends, middles and intersections of what is drawn, and to the grid. <kbd>F3</kbd> turns snapping on or off, <kbd>F8</kbd> ortho and <kbd>F10</kbd> polar tracking. Their state shows at the bottom right of the view.

## Selecting

Click an element to select it; <kbd>Shift</kbd>-click adds to the selection. To select several at once, drag with the left button where nothing is selected. Dragged to the right, the box is blue and takes what lies wholly inside it (a window); dragged to the left, it is green and dashed and takes everything it touches (crossing), as in AutoCAD. Press <kbd>Space</kbd> while you drag and the box becomes a lasso that follows the mouse, keeping the mode the box had; <kbd>Space</kbd> again goes back to the box. On the command line, while a command asks you to select objects, type **WP** or **CP** and click the corners of a polygon, then <kbd>Enter</kbd>: a window polygon or a crossing polygon.

![A lasso in the plan: the box dragged to the right, then Space, and the loop round the two walls it takes](shots/select-lasso.webp)

## Copying and moving

To copy, select the element and type <kbd>CO</kbd>. Click a base point, then a second point. Lapin keeps copying until you press <kbd>Enter</kbd> or <kbd>Esc</kbd>. **Move** (<kbd>M</kbd>) works the same way.

While you move or copy, a marker stays on the base point and a dashed line runs from it to the mouse, with a faded outline of what you move left where it was until you click or press <kbd>Esc</kbd>. The line and the marker change colour with the snap: green on a snapped point, blue when ortho or polar tracking holds the direction, the accent colour on a tracking line, grey when free. Next to the mouse, Lapin shows how far you have moved: Δx, Δy, the distance and the angle, in the project's units. You can type the distance and the angle there, as when you draw. In the Compact interface, these values are in the status line. Dragging a grip shows the same, and so does dragging a selected element itself, from the point where you pressed; press <kbd>Esc</kbd> before you let go and the element stays where it was.

![Moving a wall: the marker on the base point, the dashed line to the mouse, the wall's ghost where it was and the offset at the mouse](shots/move-trace.webp)

You can also drag instead. Drag the cross in the middle of a selected element to move it. Hold <kbd>Ctrl</kbd> (<kbd>⌘</kbd> on a Mac) while you drag to leave a copy.

A door, a window or a wall opening moves and copies along its wall: only the part of the move along the wall counts, so typing `500` after the base point slides it 500 along the wall; moved into another wall, it goes into that wall. With its wall selected too, it goes with the wall.

A grid axis moves, copies, rotates and mirrors on its own, or together with other elements. A copy is a new axis of the same grid, named after the last axis that runs its way: with axes 1, 2 and 3, a copy of any of them is 4.

## Drafting

The **Draft** tab draws lines, polylines, rectangles, circles and arcs. It edits them with offset, trim, extend, fillet, chamfer and join, fills areas with hatches, and organises them on **Layers**. **Import DXF** brings a CAD drawing in as lines you can snap to and edit.

![The Draft tab](shots/ribbon-draft.webp)

**Offset** (<kbd>O</kbd>) copies a line, a polyline, a rectangle, a circle or an arc at a distance. Type the distance in the project's unit (in a project in millimetres `500` is half a metre; `2m` or `1'6"` say their own unit), click the shape, then the side it goes to; it asks for the next shape until you press <kbd>Enter</kbd> or <kbd>Esc</kbd>. Type **T** (Through) instead and each copy passes through the point you click. A polyline's corners stay sharp (mitred), as in CAD. Offset works in plans, sections, elevations, on reference planes and in the 3D view.

Every length the command line asks for is in the project's unit, and the prompt says which: `Depth (mm)`, `Height (ft-in)`. Angles, factors and counts are plain numbers: `90` after **Rotate** is 90° whatever the unit.

### Tracing over a PDF or an image

**File → Import PDF or image…**, or **Draft → PDF or image**, brings a drawing in to trace over: a scanned plan, a photo of a facade, a PDF from another office. It goes into the plan, section or elevation in front, under its drawing.

- **A PDF**: choose the **Page** and the scale its drawing was made to (**Drawn at 1:**). A paper millimetre then measures that many millimetres here, so a 1:100 plan comes in at its true size. The page's lines come in as drafting you can snap to, edit and trace, and its pictures (a scan inside the PDF) as images under them. Either can be left out. The page comes in as one group named after the file, so **Move**, **Rotate** and **Scale** take it whole; double-click it to work on its parts.
- **An image** (PNG or JPEG): give its **Width**; its height follows its pixels. **Draft → Scale image** then sets it exactly: select the image, click two points on it a known distance apart, and type that distance.

An image is see-through (its **Opacity**, 0.5 by default, in its properties) and is never part of the model: it does not print on sheets unless you leave it in the view.

![A plan printed to PDF, brought into another level to trace over](shots/trace-pdf.webp)

### Drafting in the 3D view

**Line**, **Polyline**, **Rectangle**, **Circle**, **Arc** and the Draft edits work in the 3D view too, with the command line at the bottom. While a command runs, the chip at the top says where you draw: on the active storey's level, or on the reference plane you selected. **Pick a face** in the chip, then a click on a face of the model, makes a reference plane on that face and draws on it. Points snap as on a plan: to the plan's cut lines (the walls' corners and faces) and to what is drafted on the same plane, with ortho and the tracking guides. What you draw is the same drafted element as one drawn on the plan, and shows there too.

![Drafting in the 3D view: the chip says the plane, the line follows the mouse on the storey's level](shots/draft-3d.webp)

### Drafts in the 3D view

What you draft on a plan (lines, polylines, rectangles, circles, arcs) also stands in the 3D view, as lines at its storey's level (the level itself, not the plan's cut height); what you draft on a reference plane, a section or an elevation stands on that plane. Click one in 3D to select it, then move, copy, offset or delete it. **View → Drafts in 3D**, also in **View → View settings** of a 3D view, shows or hides them all; it is on unless you turn it off. Dimensions, texts, tags, hatches and construction lines (the role draft) stay on their views. A DXF brought onto a view lies in 3D too, on that view's plane, with a switch of its own (see [Import and export](import-export.md)).

### Draw order

Where drafted elements overlap, the one drawn later lies on top. To change it, select them (lines, polylines, rectangles, circles, arcs, hatches and fills, texts, dimensions, tags, a DXF's shapes), right-click and choose **Bring to front**, **Bring forward**, **Send backward** or **Send to back**; **Draft → Order** and **Annotate → Order** have the same, with <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>[</kbd> and <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>[</kbd> (⌘ on a Mac). Forward and backward step past the next element the selection overlaps. Drafting draws over the model's own drawing; **Send behind the model** puts it under the walls' cut and the seen lines (a hatch under a wall), and a click there then takes the wall; **Bring to front** or **Send to back** bring it over the model again. On the command line, **DRAWORDER** (**DRO**) asks for the objects, then **Above objects** or **Under objects** (click the elements they go above or under, then <kbd>Enter</kbd>), **Front**, **Back** or behind **Model**. The order belongs to the view, is one step to undo, and sheets, the PDF and the DXF print it as you see it; what you draw afterwards comes on top.

![Right-click on a selected fill: the draw order](shots/draw-order-menu.webp)

![Brought to the front, the grey fill covers the text](shots/draw-order-front.webp)

## Annotating

The **Annotate** tab has the dimensions, text, grid axes, levels and tags. A dimension reads the model, so it updates when a wall moves. How text, dimensions, axes and tags look is set in **Manage → Drafting standards**.

An aligned or linear dimension goes on as a string: after the first two points and its line, each click adds the next point on the same line, and **Enter** (or **Esc**) makes the whole string one dimension (**U** takes the last point back; type **S** first for a single dimension). A string behaves like a polyline: a click selects all of it, and it moves, copies and deletes as one. A string that measures walls, edges or grid axes stays on them: dragged or moved on its own, only its dimension line slides, square to the string, so a slip of the mouse never leaves it measuring empty space; moved together with what it measures, it goes with them. Its grips sit on each measured point (drag to move it, Ctrl/⌘-click to take it out), in the middle of each segment (drag to add a point, Ctrl/⌘-click to add one halfway) and at the ends of the dimension line (drag to move the line). To put more points into a string, as in ArchiCAD, select it and click **Annotate → Add points** (or type **DIMADD**, or **Add points** in its properties): each point you click goes into the string where it falls along its line, between the points around it or beyond an end as the new end, and holds on the wall, edge or axis it lands on; **Enter** or **Esc** finishes. To take points out, Ctrl/⌘-click their grips, or click **Annotate → Remove points** (**DIMDEL**, or **Remove points** in its properties) and click near each point; the segments either side become one. Each segment's value has a grip too: drag it to move the value off its place where segments are short and values would overlap; once it stands off its segment, a thin leader ties it back. Ctrl/⌘-click the grip, or **Texts back on the line** in the properties, puts it back. A moved value stays with its segment when points move, and turns with the string. Double-click a segment to type its value: the element it measures moves, and so does everything after it in the string. The properties show each segment's value and text, split a string into single dimensions, and join single dimensions that continue one another on one line.

Annotations on a plan (dimensions, texts, drafted lines, tags) can go to another plan where they are, since plans share their coordinates: select them and choose the plan in their properties, under **View** to move them or **Copy to** to copy them. This also brings dimensions made on a site plan to the level's floor plan. A dimension's points then hold on what the new plan cuts at that place: walls, edges, axes.

**Opening heights.** A segment of a dimension string that goes from one jamb of a door, window or bare opening to the other writes the width over the line and the opening's height under it, as Romanian and European drawings do. It is read from the model each time, so it follows the opening when it is resized, moved or given another type; a segment you typed a text over writes none. **Manage → Drafting standards → Dimensions → Opening height** sets it for each dimension style: off, `150`, `h=150`, `h=150` with the sill `p=90` under it, or `150/90`, in the style's unit. It works in plans, sections and elevations, and sheets, the PDF and the DXF print it. New projects have it on (the height alone); a project made before keeps its dimensions as they were until you turn it on there.

![The door's and the window's heights under their segments](shots/dimension-opening-height.webp)

![Opening height in the dimension styles](shots/dimension-opening-height-style.webp)

### Tags

**Annotate → Tag** (<kbd>TG</kbd>) puts a box on a view with what an element says of itself, and a leader to it. While the tool is on, the bar under the view chooses what the tag shows: the mark, name, class or type, **Fire rating**, **Acoustic rating**, **Thermal transmittance**, or **Another parameter…** by its name; or a tag type of the drafting standards; and **LEADER** turns the leader on or off. Click the element, then where the tag goes (type **E** first to bend the leader at an elbow). **Annotate → Tag a parameter** asks for the parameter's name and starts the tool with it.

![The Tag tool's bar under the view](shots/tag-options.webp)

A tag reads any parameter: the element's own, its type's (then every element of the type shows it), a standard property such as `Pset_WallCommon.FireRating`, or one an imported element came with. A name is found without its spaces, so **Fire rating** and `FireRating` are the same. To fill one in, use **Parameters** in the element's or its type's properties: type a name (Fire rating, Sound rating, Moisture resistance and Thermal resistance are offered), or choose **Add a standard property…** for the IFC properties of its class. The tag follows as soon as there is a value, and lengths are written in the project's units.

A selected tag has grips. Drag the box to move the tag: the leader keeps pointing where it did. A tag goes with its element: when the element is moved, turned, mirrored or reshaped by its grips, its tag follows it, keeping its place beside it, and the leader stays on it; a tag you moved keeps the offset you gave it. Drag the leader's middle to bend it at an elbow (Ctrl/⌘-click the elbow to straighten it again), and its end to point it at another spot (Ctrl/⌘-click: back on the element's middle). The properties change what the tag **Shows**, give it a **Text** of its own (`{mark}|{FireRating}` writes two lines), a **Tag type**, or turn the **Leader** off. The engine draws tags, so sheets and PDFs print them as you see them. The tag types in **Manage → Drafting standards → Tags** read the same fields in their templates; a new project has Fire rating, Acoustic rating and Thermal transmittance among them.

**Leaders as ArchiCAD's labels.** By default a tag's leader is an *elbow*: an oblique line from the element to a knee, then a shoulder that runs under the text and underlines its first row, with no box round the text. After you click the element, the next click is the knee and the text goes beside it. Drag the knee's grip: the oblique line keeps to 30°, 45°, 60° or 90° from its end (hold <kbd>Shift</kbd> for any angle), and dragged past the end the text moves to that side, aligned towards the knee. Drag the text to move it with its knee. In **Manage → Drafting standards → Tags**, **Leader…** on a tag type (and the build-up editor) sets its **Leader shape** (an elbow, or straight from a box), what the shoulder underlines (**Underline**: the first row, each row, the last row, or nothing, when it stops at the text's side) and **Extension mm**, how much further the shoulder reaches from the text, beside the engine's preview. With a leader per layer, each ends under its own row, their knees one column; dragging the knee moves the column. **Leader shape** in a tag's properties overrides its type's; a tag whose leader was bent at a free elbow keeps its straight leader. Tags in projects made before keep their straight leader in a box.

![A tag's leader bent at its elbow, with its grips](shots/tag-leader.webp)

![A tag's properties](shots/tag-properties.webp)

A door's or window's marker is edited part by part, each text moved or turned and each end of its line drawn out: see [Doors and windows](doors-windows.md).

#### Build-up tag

A build-up tag writes an element's layers as an architectural note: a header (*EXTERIOR*), a line for each layer, a footer (*INTERIOR*). A new project has **Build-up (exterior → interior)**: each line is the material's description (its name when it has none), its λ when it has one, and the layer's thickness in mm. Choose it in the **Tag** tool's bar and click a wall, a floor, a roof or a room's finish; other elements get the field instead. The text is read from the model each time it is drawn: change a layer, a material's description or its λ, and the tag follows.

![Two build-up tags on a facade wall: one leader, and one leader per layer](shots/buildup-tag.webp)

Its form is set in **Manage → Drafting standards → Tags**: make a tag type's **Shape** *build-up*, or click **Build-up…** on one, to edit it beside a preview on one of the project's wall or floor types.

- **Header text** and **Footer**: written above and under the layers (empty: none; `|` starts a new line).
- **Each layer**: the line written once per layer. It reads the layer's fields, `{description}`, `{material}`, `{thickness}` (in the unit chosen under **Thickness in**), `{note}` (the layer's note in its type), `{function}` (core, finish, insulation…) and `{index}`; any parameter of the material by its name or its symbol, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (see the material's [Data](materials.md#data-description-parameters-how-it-is-measured)); and the element's, `{name}`, `{type}`, `{mark}`. **Insert a field…** puts one where the cursor is and lists the parameters the project's materials have.
- **Square brackets** make a part optional: `[, λ = {λ} W/mK]` is written only where the material has a λ; a part is left out whole when a field in it is empty. **[ ]** makes the selection such a part. To write a bracket itself, type `\[` or `\]`.
- **Order**: from the exterior to the interior of a wall (its first layer's side is its exterior, so a flipped wall still starts there), from the top to the bottom of a floor or a roof; or the other way. Choose the order that runs as the layers lie beside the note, so leaders per layer never cross.
- **Layers of no thickness** (membranes, foils, paint) are written unless you untick **shown**. A layer that changes material up its height writes a line for each material.
- **Leaders**: one, to the element, or **one per layer**, each from its line to a point inside its own layer, where the view can tell the layers apart (a wall in plans and sections, a floor in sections; elsewhere the tag keeps one). Drag the leader's end along the element to cross it elsewhere.
- **Lines start with** a bullet, a dash, their number or nothing; they wrap at **Width mm** on paper, hanging after the bullet, and a value stays with its symbol and unit (`λ = 0.035 W/mK`).

![A build-up tag type's editor beside its preview](shots/buildup-tag-type.webp)

Like any tag, a build-up tag follows its element, and sheets and PDFs print it as you see it, λ, μ and ρ included. A **Text** of its own in its properties replaces the line per layer for that tag alone.

### Grid axes

**Annotate → Grid axis** (<kbd>GR</kbd>) draws one axis by its two ends; it takes the next label of its direction. **Annotate → Axis grid…** lays a whole grid at once: the spacings **Across** (the numbered axes, left to right) and **Along** (the lettered axes, bottom to top) in the project's units, as `6000 6000 4500` or `3*6000`; the **Origin** where the first axes cross, an **Angle**, how far the axes run **Past the grid**, the **First labels** and the **Axis type**. With **replace the grid's axes** ticked, the axes drawn so far go.

![The Axis grid dialog](shots/grid-dialog.webp)

Click an axis to select it. Its properties show its **Start** and **End**, **Length**, **Angle**, and its spacing from the parallel axis before it (↔). Type a new spacing and the axis moves; the axes beyond it move too, unless you untick **the axes beyond move too**. **Bubbles** chooses which ends carry a bubble. **Axis styles…** opens **Drafting standards → Grid axes**, where the bubble, its size, the line type and the first label are set. Select the whole grid to list its axes and their spacings.

![A grid axis's properties](shots/axis-properties.webp)

Every length here follows the project's units. In feet and inches, the ends and spacings read 19'-8 1/4", and the levels are written in feet and inches too.

![The same axis in feet and inches](shots/axis-properties-imperial.webp)

![The Annotate tab](shots/ribbon-annotate.webp)
