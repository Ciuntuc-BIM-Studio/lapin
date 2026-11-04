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
- Walls join by themselves where they meet: a corner as a mitre, a wall ending on another as a T, one crossing another as an X. At a T or an X the layers run into each other as built: the ending wall's core goes on through the other's finishes to its core, and its finishes stop at the other's face, meeting their like, so the plan shows one build-up with no line through it. **Join** (<kbd>JW</kbd>), **Mitre** and **Butt** change how two walls meet when that is not what you want; **Mitre new corners** makes butt the default on this computer. Either way, each layer turns the corner on its own: the finishes meet their like and run round unbroken, and the cores meet on the diagonal (mitre) or one core runs on to the other's outer face (butt), so a plan of layered walls shows clean corners.
- **Swap** changes which of the two walls runs through.

**The line you draw on.** While the Wall tool is on, the list in the command line's bar chooses the line you click: **Core centre** (the axis, as before), **Core, exterior face**, **Core, interior face**, **Exterior face** or **Interior face**. The exterior is the wall's right as you draw (draw round a building anticlockwise to have it outside). The wall's axis stays on its core, set off from the line, and walls drawn one after the other on the same face meet at the corner of that face.

## Changing a wall

Click a wall to select it. Its grips appear. Drag the ends to lengthen or shorten it, or drag the cross in the middle to move it.

![A selected wall with its grips](shots/wall-selected.webp)

The properties panel shows the selected wall. The top of the panel holds its type and layers. Below that are its length, height, base offset, storey and phase. Change any value and the model follows.

![A wall's properties](shots/wall-properties.webp)

Further down, **Dependencies** shows what is built from the wall and what the wall is built from. **Used by** lists the elements that change when the wall changes: the walls it joins, its doors and windows, a wall attached to it. Click a name to select that element. **Built from** lists the elements the wall depends on, then its type and materials. When you delete an element that others depend on, a message names the ones that were rebuilt; <kbd>Ctrl</kbd> <kbd>Z</kbd> brings everything back.

**Flip** turns the wall's layers to the other side. You can also click the small arrows shown next to a selected wall in the plan.

## Wall types

A wall type is a set of layers, from the outside face to the inside face. Each layer has a material and a thickness, and one layer is the **core**. The wall's line runs along the core. To edit the types, choose **Architecture → Wall types**. A layer other than the core may have a height of its own: **From** and **Up to** set how high it goes over the wall's base (a tiled dado up to 1500, a cladding from 300 up); empty, it runs the whole height.

A layer other than the core may also change material up its height: the **layers** button at the end of its row adds a piece (tiles, 2100 by default), from the layer's bottom up; add more for a band (paint to 900, tiles to 1500), and above the last piece the layer goes on in its own material to its top. Each piece is drawn, cut in plan and sections, and measured in its own material.

A layer other than the core may have a thickness of 0, for a material too thin to model but that must be measured: a waterproofing membrane, a vapour barrier, paint. It builds no solid; plans and sections draw it as a heavy dashed line where it lies between two layers (on a face, the face's line is its own), the Type assemblies build-up names it like the others, and it is measured by area: the side of the layer it lies on, net of openings, in the wall's quantities and the quantity takeoff by material.

![A plaster layer with ceramic tiles up to 2100 and the plaster above them](shots/wall-layer-parts.webp)

![Wall types: the layers of a brick wall](shots/wall-types.webp)

- **+ Layer** adds a layer. The arrows move a layer up or down.
- **Zones in height** splits the core from bottom to top, for example a first course, the masonry and a ring beam.
- **Lintels** chooses the lintels set over doors and windows: precast or cast in place, with their bearing and height.
- **Draw with it** closes the dialog and starts drawing walls of this type.

A change to a type changes every wall of that type.

**Import…**, **Export CSV** and **Export JSON**, under the list of types, take the wall and floor types out of a project and into another, or into a spreadsheet and back: the CSV has one row a layer (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), a type's rows one after another, and Excel's semicolons and decimal commas are read too. Brought in, a type of the same name is changed to the file's layers and any other is made, in one step to undo; a material the project lacks is made by its name. The **Type assemblies** schedule (Schedules → + → Type assemblies) lists every wall and floor type with its mark, its total thickness, how many elements use it, and its layers in order.

## Walls under a roof

Walls on the same level as a roof stop under it by themselves and rise to its gables. For a wall on another level, select it and choose **Architecture → Attach to roof** (<kbd>AR</kbd>), then click the roof — or any other body the wall should rise to: a Studio solid, an imported element. See [Floors and roofs](floors-roofs.md).
