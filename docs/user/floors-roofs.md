---
title: Floors and roofs
order: 40
summary: Draw floors and roofs by their outline, choose a roof's shape and edges, and cut openings.
---

## Floors

Choose **Architecture → Floor** (<kbd>SB</kbd>), then click the corners of its outline. Close the outline on the first point, or press <kbd>Enter</kbd>. A floor is made of layers too, set in **Architecture → Floor types**.

To change the outline, select the floor and drag its corners. **Floor opening** (<kbd>SH</kbd>) cuts a hole in a floor, for a stair or a shaft.

## Roofs

Choose **Architecture → Roof** (<kbd>RF</kbd>) and click the outline of the walls under it. Lapin builds the roof over that outline, at the top of its level.

![A house with a gable roof in the 3D view](shots/house-3d.webp)

Select the roof to change it in the properties panel:

- **Shape.** Hip, gable, shed, mansard, barrel or flat. A barrel roof is a curved vault between two gable ends; **Barrel: rise** sets how high it rises at the crown, at most half its span.
- **Pitch and overhang** for the whole roof.
- **Each edge** with its own kind (eave or gable), pitch and overhang. A mansard with gable edges is a gambrel.
- **Style.** The roof styles give a whole look in one choice: the covering, the eaves, the gutters and downpipes.

![A roof's properties](shots/roof-properties.webp)

The walls on the roof's level stop under it and rise to its gables by themselves, under a curved roof too. A wall on another level follows the roof after **Architecture → Attach to roof** (<kbd>AR</kbd>): select the walls, then click the roof. The same tool takes any other body over the walls: a Studio solid, a slab, a beam, an element imported from IFC. The walls rise to its underside and are cut along it, whatever its shape. **Detach from roof** undoes it.

**Roof window** and **Dormer** add openings to a roof.
