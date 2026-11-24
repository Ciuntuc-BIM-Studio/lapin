---
title: Roof panels
order: 41
summary: Lay metal tiles or standing-seam panels on a roof's slopes: what to buy by length, the offcuts reused in the valleys, the panels drawn as the covering.
---

## Metal tiles and panels on a roof

A roof can be covered in a metal tile or panel profile (Bilka Balcanic, Bilka Clasic 35, Bilka Retro Panel come with Lapin; their data are the makers' published dimensions, indicative: check them with the maker before ordering). Lapin lays the panels on every slope by itself, from the roof as it is: change the roof, and the panels follow.

Select a sloped roof and open **Panels** in the properties panel:

- **Profile.** The profile the roof is covered with; **none** keeps the covering as one layer, as before.
- **Colour.** The colours the profile is made in (a profile made in one colour shows it).
- **Longest panel.** For a profile made to order that offers it (Retro Panel: 4000 or 7130 mm): a shorter longest panel puts joints on long slopes, as the maker's guide advises.

Each change is one undo step. Under them the section shows **To buy** (the panels by length and how many), **Panels laid**, **Reused offcuts** (valley pieces cut from what the hip pieces leave of their panels, not bought), **Sheet bought (m²)** (each panel bought at its nominal length by its whole width, as panels are priced) and the **Warnings**: a slope under the profile's least pitch, roof windows and dormers (not cut out of the panels yet).

![A roof's Panels section: Bilka Balcanic, the panels to buy and the offcuts reused](shots/roof-panels-section.webp)

## Panel layout

**Panel layout…** (in the Panels section, or **Architecture → Roof panels → Panel layout…** with a roof selected) shows the layout: the roof **In plan** or **Laid flat** (each roof plane in true size), every panel coloured by its length, the offcuts reused in **orange** with a dashed line from the panel they are cut from, and the edges coloured by kind (eaves, verges, ridges, hips, valleys). Point at a panel to read its code (roof plane, column, row: A03-07-01), its length and where it is cut from.

Beside the drawing: **To buy** (lengths, pieces, metres), the roof area, coverage and sheet bought, **Per roof plane** (pitch, area, panels, bought, reused, sheet), **Edges** (how many, how long, how many accessory pieces of the profile's accessory length) and **Offcuts** (each piece with its donor and how far up the donor panel it is cut).

To compare, pick another **Profile** in the dialog: the layout shows it while the roof keeps its own; **Use this profile** gives it to the roof (one undo step).

![Panel layout in plan: the panels by length, the valley pieces cut from hip offcuts in orange](shots/roof-panel-layout.webp)

![The same roof laid flat, each roof plane in true size](shots/roof-panel-layout-flat.webp)

## The panels in the model

With a profile, the roof's covering **is** its panels: in 3D each panel is a sheet profiled as the tile or panel is (its waves, a tile's steps, a standing seam's ribs), in the profile's colour, the reused offcuts in orange. Plans, elevations and sections draw each panel as a plate of the covering's thickness over what it covers, so a drawing shows every joint without the corrugation's lines; a plan at 1:200 or larger draws the panels' outlines with the roof's other overhead lines. A roof with its own layers has its top layer replaced by the panels.

Where two roofs meet, the panels follow the covering as it is built: a wing's slopes end on the valleys with the main roof, the panels kept the valley gap short of them, and the main slope is panelled round the wing.

The IFC export still writes the covering as one roof slab (the panels as IfcCovering come in a later stage), so its GlobalIds do not change.

![The panels as the roof's covering in 3D](shots/roof-panels-3d.webp)

## Roof panels schedule

**Architecture → Roof panels → Roof panels** makes the **Roof panels** schedule, a field schedule of every roof's panels: roof, profile, colour, length, bought or cut from an offcut, pieces, total length and sheet bought, with a totals line. Like any field schedule it can be changed, sorted and exported. Each panel is a part of its roof, so a **part** tag writes its code and length.
