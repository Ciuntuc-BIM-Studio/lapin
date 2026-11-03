---
title: Floors and roofs
order: 40
summary: Draw floors and roofs by their outline, choose a roof's shape and edges, and cut openings.
---

## Floors

Choose **Architecture → Floor** (<kbd>SB</kbd>), then click the corners of its outline. Close the outline on the first point, or press <kbd>Enter</kbd>. A floor is made of layers too, set in **Architecture → Floor types**.

To change the outline, select the floor and drag its corners. **Floor opening** (<kbd>SH</kbd>) cuts a hole in a floor, for a stair or a shaft.

A floor drawn outside the walls, in front of a door, is a porch. For the steps up to it, see [Stairs](stairs.md).

## Roofs

Choose **Architecture → Roof** (<kbd>RF</kbd>) and click the outline of the walls under it. Lapin builds the roof over that outline, at the top of its level.

![A house with a gable roof in the 3D view](shots/house-3d.webp)

Select the roof to change it in the properties panel:

- **Shape.** Hip, gable, shed, mansard, barrel or flat. A barrel roof is a curved vault between two gable ends; **Barrel: rise** sets how high it rises at the crown, at most half its span.
- **Pitch and overhang** for the whole roof.
- **Ridge direction (gable, shed)**: auto, x or y. A shed roof's ridge is its high side: on x it falls towards −y, on y towards −x; **Shed: flip the slope** makes it fall the other way.
- **Each edge** with its own kind (eave or gable), pitch and overhang. A mansard with gable edges is a gambrel.
- **Style.** The roof styles give a whole look in one choice: the covering, the eaves, the gutters and downpipes.

![A roof's properties](shots/roof-properties.webp)

**Roof configurator** (the button beside the style, or **Architecture → Roof configurator** with a roof selected) gathers everything in one place: the styles on the left, then the shape and its edges, the covering and drainage, the framing and the openings, over a plan of the roof (click an edge to make it auto, an eave or a gable) and, for the framing, a section across the ridge with the truss or couple at the middle. Pick a style to start from and change any field by hand; **Apply** makes it one undo step. **Save as style…** keeps the roof as it is configured as a style of the project, listed under **Project** above the catalogue's (and in the properties panel's list) for other roofs and new ones; **Update style** saves the changes into it again, and every roof of the style follows. A project style can be renamed or deleted; its roofs keep how they look.

![The roof configurator](shots/roof-configurator.webp)

**Gutters and downpipes.** In the configurator's **Covering and drainage** the gutter takes its size, how far out of the eave and how far below the covering it hangs, its fall to the outlets (mm/m) and its end caps; the edges' table switches each eave's gutter on or off. The downpipes start as the roof's own, one every 10 m; **Make them editable** turns them into elements, and **Architecture → Downpipe** adds one with a click on a gutter. A downpipe's grip slides it along its gutter only; its properties set its diameter, the shoe at its foot and its route, a chain of segments each with its length and its angle from the vertical (towards the wall), the last one down to the ground. In IFC a gutter is an IfcPipeSegment GUTTER and a downpipe a rigid segment named Downpipe.

The walls on the roof's level stop under it and rise to its gables by themselves, under a curved roof too. A wall on another level follows the roof after **Architecture → Attach to roof** (<kbd>AR</kbd>): select the walls, then click the roof. The same tool takes any other body over the walls: a Studio solid, a slab, a beam, an element imported from IFC. The walls rise to its underside and are cut along it, whatever its shape. **Detach from roof** undoes it.

**Roof window** and **Dormer** add openings to a roof: click on a slope. They belong to the roof. Select the roof, and each one shows a grip in plan at its middle: drag the grip to move it on the roof (it is refused where it would not fit), or **Ctrl/⌘-click** it to remove it. The roof's properties list them too, each with **×** to remove it, and the roof configurator sets their size.

![A roof window's grip on the selected roof](shots/roof-window-grip.webp)

### Timber framing

**Structure** sets how the roof is framed:

- **rafters**: rafters on purlins, with a ridge beam.
- **collar**: couples of rafters with a level collar tie at **Collar ties: share of the rise** (two thirds by default), no purlins.
- **trusses**: prefabricated trusses along the main ridge, at the **Rafter spacing**: top chords on the slopes, a bottom chord on the wall plates, W webs (a mono truss on a shed roof). A hip roof's ends keep their rafters.
- **none**: a deck, no timber.

**Rafters and chords** takes the old sections in cm or the Nordic ones in mm (45×95 to 45×245); **Truss webs** and **Timber grade** (C14 to C30, glulam) go with them. The rafters sit on the wall plate by a bird's mouth (**Rafter seat on the wall plate**) and end at the eave plumb or square (**Rafter tails**). The **Swedish truss roof** and **Nordic collar-tie roof** styles bring their framing with them.

Every member is a part of its own: the **Roof timber** schedule lists them as a cutting list (member, section, grade, length, pieces, volume), and IFC gets each as an IfcMember of its type, each truss as an IfcElementAssembly.

## False ceilings

**Finishes → Ceiling** (<kbd>CG</kbd>) draws a false ceiling over an outline you click; **Ceiling in a room** fills the room you click, following its outline. A ceiling hangs at its **Height** over the storey (2700 mm unless you change it in its properties).

Its type, in **Finishes → Ceiling types**, sets the **System**: **Tiles on an exposed grid** (the tile's width, length and thickness, the grid's face width, depth and rotation, hangers up to the structure at their spacing) or **Plasterboard, continuous**. The grid is centred on the ceiling, so the cut tiles at the edges come out even.

**Light panel** and **Air diffuser** put one in the cell you click; click the cell again with the other tool to change it. In the properties panel, **Lay out** puts light panels or air diffusers every so many cells, and **Clear** takes them away. The schedules count the whole tiles, the light panels and the diffusers, and the ceiling goes to IFC as an IfcCovering of type CEILING.

![A ceiling's properties: its type, height and fixtures](shots/ceiling-properties.webp)

![Grid ceilings with light panels and air diffusers, in 3D](shots/ceilings-3d.webp)
