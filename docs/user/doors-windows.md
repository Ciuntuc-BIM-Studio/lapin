---
title: Doors and windows
order: 30
summary: Place doors and windows in walls, move them, and design their types in the configurator.
---

<!-- video doors-windows -->

## Placing a door or a window

Choose **Architecture → Door** (<kbd>DR</kbd>) or **Window** (<kbd>WN</kbd>), then click a wall where you want the opening. Lapin cuts the opening in every layer of the wall and adds the lintel the wall type asks for.

While Door or Window is on, the properties panel shows the type it will place, with its size: choose another there before you click the wall.

![A plan with a door and windows](shots/openings-plan.webp)

To move a door or window along its wall, select it and drag it. To change it, use the properties panel: its type, its sill height, and which side it opens to (**Side**). With several doors and windows selected, **Hand**, **Side**, the sill and the type change all of them in one step, and so does a flip arrow in the plan.

For an exact distance, use **Move** (<kbd>M</kbd>) or **Copy** (<kbd>CO</kbd>) on the command line, as for any element: select the door, window or wall opening, click a base point, then type the distance (`500`) or click a second point. Only the part of the move along the wall counts: the opening slides along its wall and keeps its sill. Moved into another wall of the same storey, it goes into that wall and still opens the same way in the room. A copy is a new door or window of the same type, with the next number. If it would not fit (past the wall's end, or over another opening), nothing moves. **Rotate** and **Mirror** place it the same way, and a mirror changes its hand.

![A door's properties](shots/door-properties.webp)

## Openings without a door or window

**Architecture → Wall opening** (<kbd>WO</kbd>) cuts an opening alone through a wall where you click it: a passage, a hatch, a hole for a door that comes later. Set its width, height and sill in the properties panel before the click. Selected, its size, its place along the wall and its sill are changed there; like a door, it must fit in the wall and stay clear of the wall's other openings. In IFC it is an opening voiding the wall, with nothing filling it.

Selected, an opening has grips like a window: drag its middle to slide it along the wall, or a jamb to make it wider or narrower while the other jamb stays put; the length beside the mouse can be typed, as when you draw. It is picked by a click in its gap in the plan, or in its hole in the 3D view, where its outline lights up. **Move** (<kbd>M</kbd>) slides it along the wall by a typed distance, and its height and sill are in the properties panel.

**Head** shapes its top: **Rectangle**, **Segmental arch** (a shallow arc), **Elliptical arch** (a half ellipse, a quarter of the width high unless you say), **Semicircle**, or **Radiused corners** (a flat head with its top corners rounded). **Rise of the head** sets how high a segmental or elliptical head rises, **Corner radius** how round the corners are (150 mm unless you say, never more than half the width); empty, the kind's own. The wall's opening follows the outline in 3D, in elevations and sections and in IFC; a plan shows its jambs as before.

## The configurator

**Architecture → Doors & windows** opens the configurator. Every door and window type of the project is listed on the left. You can change:

- the type's name and mark (the letter in the tags);
- its width and height;
- the frame's depth and width;
- its columns and rows of panels, and how each panel opens;
- the reveal finish, the surround on the facade, the threshold, and the colours.

The drawings on the right show the type as it will be drawn in elevation, plan and section, while you change it.

**Left and right.** A door's swing is named the way architects name it: look from the handle towards the hinges; a leaf that opens to the left of that line is a left swing, one that opens to the right a right swing. The configurator's choices for a leaf say it so, with the side of its hinges in the **Panels** grid, which is seen from outside, the side the panels are pushed from: *Swing right (hinges at left)*. An IFC file names the hand its own way (the hinges as seen looking the way the leaf opens), so the same door is written there as SINGLE_SWING_LEFT; Lapin turns one into the other when it writes and reads IFC. A window's left and right are IFC's: its hinges as seen from outside. The elevation is seen from inside, the side the panels open to, unless **From outside** is ticked; like the plan, it shows the door as it stands in the model. A door flipped to the other hand (**Hand**, or a flip arrow along the wall) is the mirror image of its type; the schedules list it on its own card (see [Schedules](schedules.md)).

![The configurator for doors and windows](shots/joinery-configurator.webp)

**Save type** keeps your changes. Every door or window of that type follows them. **Place with it** closes the configurator and starts placing that type.

### Windows with more panes

**Columns of panes** takes the panes' relative widths, one number for each: `1 1` is two equal panes, `1 1 1` three, `1 2 1` a wide one between two narrow ones. **Rows of panes** splits the window in height the same way, from the bottom up: `1 1` gives two rows. Six panes are `1 1 1` in Columns of panes and `1 1` in Rows of panes. Then, in **Panels**, choose how each pane opens: fixed, side-hung, tilt-and-turn, bottom-hung, top-hung, pivoting or sliding.

### Shutters, grilles, window boxes and canopies

<!-- video window-dressings -->

Under **On the facade**, a window type can have:

- **Shutters**: louvred, panel or planks, one or two leaves, shown open (folded against the wall beside the window) or closed over it;
- a **Safety grille**: in the reveal on the room's side (the default, where safety grilles usually go), in the reveal on the street's side, on the facade or on the interior face, with horizontal or vertical bars or both, their size and spacing;
- a **Window box** under the window;
- a **Canopy** over it, with its projection and slope.

Each one sits by the wall's real faces where the window is, from the wall's layers, so it stays right on any wall type. When the window has a surround, what is on the facade stands off the surround. You can move each one by hand: **From the wall face** sets how far it stands off the wall (for a grille in the reveal, **Set back from the face** sets how far in it sits), **From the window's axis** moves it along the wall, and **Up or down** moves it in height. They show in 3D, in plans, elevations and sections, and the schedules measure them.

![Shutters, a grille, a window box and a canopy in the configurator](shots/joinery-dressings.webp)

![The windows dressed, in 3D](shots/window-dressings-3d.webp)

### Shapes, styles and ornaments

**Shape** gives a door or window an arched head (**Round arch**, **Segmental arch**, **Elliptical arch**, **Radiused corners**, **Pointed arch**, **Triangular head**) or makes it a **Round or oval** window (round when the width and height are equal). An arched type keeps its panes under a transom at the spring line, and above it a fixed fanlight that follows the head, with bars if you want them: a **Sunburst**, **The mullions carried up** or a **Cross**. **Rise of the head** sets how high a segmental, elliptical, pointed or triangular head rises, **Corner radius** how round a radiused head's corners are. The wall's opening, the frame, the glass, the surround and the drawings all follow the outline.

**Glazing bars** divide the panes: **Grid (colonial)** with its lites across and up, **Prairie** with bars near the edges, or a **Diamond lattice**.

On the facade, besides the surround, a window can have a **Keystone**, a **Crown over the opening** (a **Cornice**, a **Pediment**, a **Segmental pediment**, or a **Hood mould** that follows an arched head) and an **Apron under the sill** with its consoles.

**Apply a style…** sets the shape, the glazing bars and the ornaments at once: Modern, Classical, Georgian, Victorian, Romanesque, Baroque, Gothic, Tudor, Prairie, Traditional, or a round window. Everything it sets stays editable.

![A baroque window: a round arch with a sunburst, a keystone and a segmental pediment](shots/joinery-styles.webp)

![Arched windows in 3D](shots/window-styles-3d.webp)

### Handles

**Handle** picks the handle of the type: a **Lever**, a **Knob**, a **Pull bar** or a **Window handle**, or **None**. By default a door has a lever and a window a window handle. A door has its handle on both faces, at the **Handle height** you set; **Handle colour** sets its colour. Handles show in 3D, in sections and elevations, and in the configurator's drawings when **Hardware** is ticked.

### Parameters

**Parameters · in schedules**, at the bottom of the configurator, holds the type's own parameters: **+ Fire rating** (kept in IFC's Pset_DoorCommon or Pset_WindowCommon as FireRating), **+ Manufacturer**, **+ Product code**, **+ Website**, or any parameter you name. The type's doors and windows inherit them, and each one becomes a row of the joinery schedules: see [Schedules and charts](schedules.md).

## Tags in plan

**Annotate → Door & window tags** puts a marker on each door and window of a plan: a line across the wall, the mark outside, and the size and sill inside. To change how the tags look, go to **Manage → Drafting standards → Tags**.

### Editing a marker on screen

Each marker can be laid out by hand, part by part. Click its line or one of its texts to select it: every text then has a square grip (drag it to move the text) and a round one past its end (drag it to turn the text), and each end of the line has a grip (drag it to lengthen or shorten the line; the text at that end goes with it). Ctrl/⌘-click a grip to put that part back as it was. Each drag is one step to undo. With several markers of the same plan selected, a part dragged on one changes the same part of all of them.

![A door's marker selected: a grip for each text, its turning grip, and one at each end of the line](shots/marker-grips.webp)

![The mark moved and turned, the outer end of the line drawn out](shots/marker-edited.webp)

The edits belong to the marker, and so to that plan: the same door can be laid out differently in another plan. They are kept along and across the wall, so they follow the door when it moves, turns or flips to the other side. In the marker's properties, **Marker** lists its parts: type a text's turn in degrees or an end's reach in paper millimetres, put one part back with ↺, or all of them with **Put the label back**. The engine draws markers as edited, so sheets, PDF and DXF print them the same.

![A marker's parts in its properties](shots/marker-properties.webp)

Whether the texts lie across the marker's line (along the wall, as by default) or along it is set for each tag type, in **Manage → Drafting standards → Tags**, column **Text direction**: **perpendicular to the tag line** or **along the tag line**. Either way they are drawn upright, never upside down; a text turned by hand is kept upright too.

![The door marker's texts along the tag line](shots/marker-along.webp)
