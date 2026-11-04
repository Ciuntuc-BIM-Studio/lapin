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

A grid axis moves, copies, rotates and mirrors on its own, or together with other elements. A copy is a new axis of the same grid, named after the last axis that runs its way: with axes 1, 2 and 3, a copy of any of them is 4.

## Drafting

The **Draft** tab draws lines, polylines, rectangles, circles and arcs. It edits them with offset, trim, extend, fillet, chamfer and join, fills areas with hatches, and organises them on **Layers**. **Import DXF** brings a CAD drawing in as lines you can snap to and edit.

![The Draft tab](shots/ribbon-draft.webp)

## Annotating

The **Annotate** tab has the dimensions, text, grid axes, levels and tags. A dimension reads the model, so it updates when a wall moves. How text, dimensions, axes and tags look is set in **Manage → Drafting standards**.

An aligned or linear dimension goes on as a string: after the first two points and its line, each click adds the next point on the same line, and **Enter** (or **Esc**) makes the whole string one dimension (**U** takes the last point back; type **S** first for a single dimension). A string behaves like a polyline: a click selects all of it, and it moves, copies and deletes as one. A string that measures walls, edges or grid axes stays on them: dragged or moved on its own, only its dimension line slides, square to the string, so a slip of the mouse never leaves it measuring empty space; moved together with what it measures, it goes with them. Its grips sit on each measured point (drag to move it, Ctrl/⌘-click to take it out), in the middle of each segment (drag to add a point, Ctrl/⌘-click to add one halfway) and at the ends of the dimension line (drag to move the line). To put more points into a string, as in ArchiCAD, select it and click **Annotate → Add points** (or type **DIMADD**, or **Add points** in its properties): each point you click goes into the string where it falls along its line, between the points around it or beyond an end as the new end, and holds on the wall, edge or axis it lands on; **Enter** or **Esc** finishes. To take points out, Ctrl/⌘-click their grips, or click **Annotate → Remove points** (**DIMDEL**, or **Remove points** in its properties) and click near each point; the segments either side become one. Each segment's value has a grip too: drag it to move the value off its place where segments are short and values would overlap; once it stands off its segment, a thin leader ties it back. Ctrl/⌘-click the grip, or **Texts back on the line** in the properties, puts it back. A moved value stays with its segment when points move, and turns with the string. Double-click a segment to type its value: the element it measures moves, and so does everything after it in the string. The properties show each segment's value and text, split a string into single dimensions, and join single dimensions that continue one another on one line.

Annotations on a plan (dimensions, texts, drafted lines, tags) can go to another plan where they are, since plans share their coordinates: select them and choose the plan in their properties, under **View** to move them or **Copy to** to copy them. This also brings dimensions made on a site plan to the level's floor plan. A dimension's points then hold on what the new plan cuts at that place: walls, edges, axes.

### Tags

**Annotate → Tag** (<kbd>TG</kbd>) puts a box on a view with what an element says of itself, and a leader to it. While the tool is on, the bar under the view chooses what the tag shows: the mark, name, class or type, **Fire rating**, **Acoustic rating**, **Thermal transmittance**, or **Another parameter…** by its name; or a tag type of the drafting standards; and **LEADER** turns the leader on or off. Click the element, then where the tag goes (type **E** first to bend the leader at an elbow). **Annotate → Tag a parameter** asks for the parameter's name and starts the tool with it.

![The Tag tool's bar under the view](shots/tag-options.webp)

A tag reads any parameter: the element's own, its type's (then every element of the type shows it), a standard property such as `Pset_WallCommon.FireRating`, or one an imported element came with. A name is found without its spaces, so **Fire rating** and `FireRating` are the same. To fill one in, use **Parameters** in the element's or its type's properties: type a name (Fire rating, Sound rating, Moisture resistance and Thermal resistance are offered), or choose **Add a standard property…** for the IFC properties of its class. The tag follows as soon as there is a value, and lengths are written in the project's units.

A selected tag has grips. Drag the box to move the tag: the leader keeps pointing where it did. Drag the leader's middle to bend it at an elbow (Ctrl/⌘-click the elbow to straighten it again), and its end to point it at another spot (Ctrl/⌘-click: back on the element's middle). The properties change what the tag **Shows**, give it a **Text** of its own (`{mark}|{FireRating}` writes two lines), a **Tag type**, or turn the **Leader** off. The engine draws tags, so sheets and PDFs print them as you see them. The tag types in **Manage → Drafting standards → Tags** read the same fields in their templates; a new project has Fire rating, Acoustic rating and Thermal transmittance among them.

![A tag's leader bent at its elbow, with its grips](shots/tag-leader.webp)

![A tag's properties](shots/tag-properties.webp)

A door's or window's marker is edited part by part, each text moved or turned and each end of its line drawn out: see [Doors and windows](doors-windows.md).

### Grid axes

**Annotate → Grid axis** (<kbd>GR</kbd>) draws one axis by its two ends; it takes the next label of its direction. **Annotate → Axis grid…** lays a whole grid at once: the spacings **Across** (the numbered axes, left to right) and **Along** (the lettered axes, bottom to top) in the project's units, as `6000 6000 4500` or `3*6000`; the **Origin** where the first axes cross, an **Angle**, how far the axes run **Past the grid**, the **First labels** and the **Axis type**. With **replace the grid's axes** ticked, the axes drawn so far go.

![The Axis grid dialog](shots/grid-dialog.webp)

Click an axis to select it. Its properties show its **Start** and **End**, **Length**, **Angle**, and its spacing from the parallel axis before it (↔). Type a new spacing and the axis moves; the axes beyond it move too, unless you untick **the axes beyond move too**. **Bubbles** chooses which ends carry a bubble. **Axis styles…** opens **Drafting standards → Grid axes**, where the bubble, its size, the line type and the first label are set. Select the whole grid to list its axes and their spacings.

![A grid axis's properties](shots/axis-properties.webp)

Every length here follows the project's units. In feet and inches, the ends and spacings read 19'-8 1/4", and the levels are written in feet and inches too.

![The same axis in feet and inches](shots/axis-properties-imperial.webp)

![The Annotate tab](shots/ribbon-annotate.webp)
