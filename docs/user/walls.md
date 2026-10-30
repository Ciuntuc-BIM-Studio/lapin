---
title: Walls
order: 20
summary: Draw walls corner by corner, change them in the properties panel, and set up wall types with their layers.
---

<!-- video walls -->

## Drawing walls

Open the **Architecture** tab and choose **Wall**, or type <kbd>WA</kbd>.

![The Architecture tab](shots/ribbon-architecture.webp)

Click where the wall starts, then click at each corner. The next wall follows the mouse. While you draw, you can type a length and an angle in the boxes above the view instead of clicking. Press <kbd>Enter</kbd> or <kbd>Esc</kbd> to stop.

![Drawing walls: the next wall follows the mouse, with its length and angle shown](shots/wall-drawing.webp)

Where two walls meet, Lapin joins them by itself. Their layers run into each other the way they would be built.

- **Curved wall** (<kbd>WC</kbd>) draws a wall along an arc.
- **Curtain wall** (<kbd>CW</kbd>) draws a glazed wall with mullions and panels.
- Walls join by themselves where they meet: a corner as a mitre, a wall ending on another as a T. **Join** (<kbd>JW</kbd>), **Mitre** and **Butt** change how two walls meet when that is not what you want; **Mitre new corners** makes butt the default on this computer.
- **Swap** changes which of the two walls runs through.

## Changing a wall

Click a wall to select it. Its grips appear. Drag the ends to lengthen or shorten it, or drag the cross in the middle to move it.

![A selected wall with its grips](shots/wall-selected.webp)

The properties panel shows the selected wall. The top of the panel holds its type and layers. Below that are its length, height, base offset, storey and phase. Change any value and the model follows.

![A wall's properties](shots/wall-properties.webp)

**Flip** turns the wall's layers to the other side. You can also click the small arrows shown next to a selected wall in the plan.

## Wall types

A wall type is a set of layers, from the outside face to the inside face. Each layer has a material and a thickness, and one layer is the **core**. The wall's line runs along the core. To edit the types, choose **Architecture → Wall types**.

![Wall types: the layers of a brick wall](shots/wall-types.webp)

- **+ Layer** adds a layer. The arrows move a layer up or down.
- **Zones in height** splits the core from bottom to top, for example a first course, the masonry and a ring beam.
- **Lintels** chooses the lintels set over doors and windows: precast or cast in place, with their bearing and height.
- **Draw with it** closes the dialog and starts drawing walls of this type.

A change to a type changes every wall of that type.

## Walls under a roof

Walls on the same level as a roof stop under it by themselves and rise to its gables. For a wall on another level, select it and choose **Architecture → Attach to roof** (<kbd>AR</kbd>), then click the roof — or any other body the wall should rise to: a Studio solid, an imported element. See [Floors and roofs](floors-roofs.md).
