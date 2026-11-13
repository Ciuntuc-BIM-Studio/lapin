---
title: Floors and roofs
order: 40
summary: Draw floors and roofs by their outline, choose a roof's shape and edges, and cut openings.
---

## Floors

Choose **Architecture → Floor** (<kbd>SB</kbd>), then click the corners of its outline. Close the outline on the first point, or press <kbd>Enter</kbd>. A floor is made of layers too, set in **Architecture → Floor types**. A layer of a floor type may have a thickness of 0 (a waterproofing, paint): it builds nothing, is drawn as a line in sections and is measured by the floor's area.

In **Floor types**, the layers run from the top down. The arrows at the end of a layer's row move it up or down one place (they are buttons, reachable with <kbd>Tab</kbd>), and **Reverse layers** runs the whole build-up the other way, so the top layer goes to the underside; it asks first, because every floor of the type turns over once the type is saved.

![The layers of a floor type, with the arrows and Reverse layers](shots/floor-types.webp)

In **Floor types**, each layer has a **Priority** (0 to 100) and a **Category**, as a wall type's layers do. A wall layer that goes down into the floor (**Into floors** in **Wall types**) passes the floor's layers of a lower priority and stops on the first as high or higher. Empty, the priority is the material's in **Manage → Cut order**, else 50 for the core and 0 for the other layers. A layer marked **Finish** goes when a view leaves out the floors' finishes.

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
- **Covering material.** The covering is made of a project material, the covering's own by default (Clay tile, Metal sheet, Slate…), made in the covering's colour the first time a roof needs it. Pick another one in the properties or in the configurator's **Covering and drainage**; changing the covering changes it too, unless you chose one. Elevations and sections hatch the covering by that material (tiles, shingles and slates in level courses, metal sheet in vertical seams, unless you give it an **Elevation** hatch of its own), a section draws the cut covering with the material's section hatch, and IFC, the quantity takeoff and the estimate list the covering under its material.
- **Covering thickness.** Empty, the covering is as thick as the catalogue's (a membrane 2 cm, metal sheet 3 cm, tiles 5 cm…); type one to give it its own. A roof imported from another program keeps its file's.
- **Deck.** A flat roof, a barrel vault and a roof with **Structure** none have a deck under the covering, a part of its own: **Deck material** (Reinforced concrete by default, made the first time a roof needs it) and **Deck thickness** (16 cm by default), in the properties or in the configurator's **Framing**. The covering lies on it, so the roof stays as thick as the two together. A section draws two bands, each hatched by its material; roof windows go through both; a wall attached to a flat roof rises to the deck's underside; IFC writes the deck and the covering as two roof slabs of their materials, and the quantity takeoff and the estimate measure the deck (area and volume) and the covering (area) each under its material. The properties show these two fields only on a roof without framing, and the rafters' fields (sections, spacing, grade, purlins, tails) only on a framed one; a field that applies to one shape only (the mansard's, the vault's rise, the shed's flip) shows on that shape.

![A roof's properties](shots/roof-properties.webp)

![A flat roof in section: the concrete deck and the covering over it, each hatched by its material](shots/roof-deck-section.webp)

**Roof configurator** (the button beside the style, or **Architecture → Roof configurator** with a roof selected) gathers everything in one place: the styles on the left, then the shape and its edges, the covering and drainage, the framing and the openings, over a plan of the roof (click an edge to make it auto, an eave or a gable) and, for the framing, a section across the ridge with the truss or couple at the middle. Pick a style to start from and change any field by hand; **Apply** makes it one undo step. **Save as style…** keeps the roof as it is configured as a style of the project, listed under **Project** above the catalogue's (and in the properties panel's list) for other roofs and new ones; **Update style** saves the changes into it again, and every roof of the style follows. A project style can be renamed or deleted; its roofs keep how they look.

![The roof configurator](shots/roof-configurator.webp)

**Gutters and downpipes.** In the configurator's **Covering and drainage** the gutter takes its size, how far out of the eave and how far below the covering it hangs, its fall to the outlets (mm/m) and its end caps; the edges' table switches each eave's gutter on or off. The downpipes start as the roof's own, one every 10 m; **Make them editable** turns them into elements, and **Architecture → Downpipe** adds one with a click on a gutter. A downpipe's grip slides it along its gutter only; its properties set its diameter, the shoe at its foot and its route, a chain of segments each with its length and its angle from the vertical (towards the wall), the last one down to the ground. In IFC a gutter is an IfcPipeSegment GUTTER and a downpipe a rigid segment named Downpipe.

The walls on the roof's level stop under it and rise to its gables by themselves, under a curved roof too. A wall on another level follows the roof after **Architecture → Attach to roof** (<kbd>AR</kbd>): select the walls, then click the roof. The same tool takes any other body over the walls: a Studio solid, a slab, a beam, an element imported from IFC. The walls rise to its underside and are cut along it, whatever its shape. **Detach from roof** undoes it.

**Roof window** and **Dormer** add openings to a roof: click on a slope. They belong to the roof. Select the roof, and each one shows its grips in plan. Drag the grip at its middle to move it on the roof. Drag a roof window's edge or corner grips to resize it on its slope (the opposite edge stays where it is; you can type the length as you drag). The width runs along the eave and the length up the slope, measured on the slope. A dormer has a grip at each side for its width. A change that would not fit on the roof is refused. To remove one, right-click its grip and choose **Remove roof window** (or **Remove dormer**), press **Delete** while the cursor is over its grip, or **Ctrl/⌘-click** its middle grip. The roof's properties list them too: type a window's width and length there, or click **×** to remove it. A dormer's roof is covered in the roof's covering material and measured with it: the roof's quantities list the **dormer covering** apart (the area of its slopes, the number of dormers), so the quantity takeoff by material, the estimate and a planning task on the covering count the roof's slopes (less the dormers' notches) and the dormers' roofs, each once.

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

**Interior → Ceiling** (<kbd>CG</kbd>) draws a false ceiling over an outline you click; **Ceiling in a room** fills the room you click, following its outline. A ceiling hangs at its **Height** over the storey (2700 mm unless you change it in its properties).

Its type, in **Interior → Ceiling types**, sets the **System**: **Tiles on an exposed grid** (the tile's width, length and thickness, the grid's face width, depth and rotation, hangers up to the structure at their spacing) or **Plasterboard, continuous**. The grid is centred on the ceiling, so the cut tiles at the edges come out even.

**Light panel** and **Air diffuser** put one in the cell you click; click the cell again with the other tool to change it. In the properties panel, **Lay out** puts light panels or air diffusers every so many cells, and **Clear** takes them away. The schedules count the whole tiles, the light panels and the diffusers, and the ceiling goes to IFC as an IfcCovering of type CEILING.

![A ceiling's properties: its type, height and fixtures](shots/ceiling-properties.webp)

![Grid ceilings with light panels and air diffusers, in 3D](shots/ceilings-3d.webp)

## Skirtings

**Interior → Skirting in room** (<kbd>SK</kbd>) lays a skirting board round the room you click, along the walls' faces, broken at each door (any opening that comes down to the floor); clicked inside walls with no room, it follows them the same way. A room's skirting follows the room: move a wall or a door and the skirting goes with it. **Skirting** draws one along a line you click on a wall's face (Enter finishes); the board goes on the side away from the wall, and **Flip side** in its properties turns it over.

Its type, in **Interior → Skirting types**, sets the **Profile** — **Flush (square)**, **Bevelled (MDF)**, **Moulded (wooden)**, **Cove foot (metal)**, **Pencil round** or **Bullnose**, drawn beside it to scale — the **Height** (40 to 300 mm), the **Thickness** (5 to 40 mm), the **Material** and its colour. **From the library** brings ready-made types into the project: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 and Bullnose 70. While a skirting tool is on, the properties panel shows the type it lays, to choose before the click.

A skirting's properties hold its type, its **Base offset** over the floor (under Dimensions) and **Gaps at the doors** (off, it runs on across them). **Skirting schedule** (or **Skirtings** in the project browser's list of new schedules) lists them by type with their pieces and length, the doors' gaps left out. The board is mitred at the corners; a plan draws its front edge, and a skirting goes to IFC as an IfcCovering of type SKIRTINGBOARD, of an IfcCoveringType of the same.

![The skirting types: the profile drawn to scale, the library](shots/skirting-types.webp)

![A skirting's properties: its type, base offset and the gaps at the doors](shots/skirting-properties.webp)
