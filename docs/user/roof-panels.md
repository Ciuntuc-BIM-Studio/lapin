---
title: Roof panels
order: 41
summary: Lay metal tiles or standing-seam panels on a roof's slopes: what to buy by length, the offcuts reused in the valleys, the accessories, fixings, battens and membrane, what it all costs from your price list, roof windows cut out, the panels drawn as the covering.
---

## Metal tiles and panels on a roof

A roof can be covered in a metal tile or panel profile (Bilka Balcanic, Bilka Clasic 35, Bilka Retro Panel come with Lapin; their data are the makers' published dimensions, indicative: check them with the maker before ordering). Lapin lays the panels on every slope by itself, from the roof as it is: change the roof, and the panels follow.

Select a sloped roof and open **Panels** in the properties panel:

- **Profile.** The profile the roof is covered with; **none** keeps the covering as one layer, as before.
- **Colour.** The colours the profile is made in (a profile made in one colour shows it).
- **Longest panel.** For a profile made to order that offers it (Retro Panel: 4000 or 7130 mm): a shorter longest panel puts joints on long slopes, as the maker's guide advises.
- **Ridge caps.** For a tile that comes with two (Balcanic, Clasic 35): the **Large** (290 mm) or **Small** (230 mm) ridge and hip caps, with their seal roll.
- **Support.** For a profile carried in more than one way (Retro Panel): **Boards 150 mm** (150 mm wide, 50 mm apart, the maker's current guide) or **Battens** (the older guide's, 300 mm apart, 350 mm on slopes over 25°).
- **Support fixings.** How the battens and counter-battens are fixed to the rafters: **Nails** 4 × 100 mm (the default) or **Screws** 6 × 100 mm.

Each change is one undo step. Under them the section shows **To buy** (the panels by length and how many), **Panels laid**, **Reused offcuts** (valley pieces cut from what the hip pieces leave of their panels, not bought), **Sheet bought (m²)** (each panel bought at its nominal length by its whole width, as panels are priced), **Accessories and support** (every line of the takeoff below, with its quantity and unit), **Cost** (the indicative subtotal once a price list prices the lines, see [What it costs](#what-it-costs); until then a hint and **Price lists…**) and the **Warnings**: a slope under the profile's least pitch, a roof without rafters (its counter-battens estimated).

![A roof's Panels section: Bilka Balcanic, the panels to buy and the offcuts reused](shots/roof-panels-section.webp)

## Panel layout

**Panel layout…** (in the Panels section, or **Architecture → Roof panels → Panel layout…** with a roof selected) shows the layout: the roof **In plan** or **Laid flat** (each roof plane in true size), every panel coloured by its length, the offcuts reused in **orange** with a dashed line from the panel they are cut from, and the edges coloured by kind (eaves, verges, ridges, hips, valleys). Point at a panel to read its code (roof plane, column, row: A03-07-01), its length and where it is cut from.

Beside the drawing: **To buy** (lengths, pieces, metres), the roof area, coverage and sheet bought, **Per roof plane** (pitch, area, panels, bought, reused, sheet), **Edges** (how many, how long, how many accessory pieces of the profile's accessory length), the takeoff's tables (below) and **Offcuts** (each piece with its donor and how far up the donor panel it is cut).

To compare, pick another **Profile** in the dialog: the layout shows it while the roof keeps its own; **Use this profile** gives it to the roof (one undo step).

![Panel layout in plan: the panels by length, the valley pieces cut from hip offcuts in orange](shots/roof-panel-layout.webp)

![The same roof laid flat, each roof plane in true size](shots/roof-panel-layout-flat.webp)

## Accessories, fixings, battens and membrane

The layout counts what else the panels need, as the profile's data says, on the roof as it is:

- **Flashings and accessories.** Along each edge, pieces of the profile's accessory length (2 m) lapped as the profile says: ridge and hip caps, hip end caps where a hip meets the eave, ridge seal rolls, ridge end caps at the verges, eave, verge and top-edge flashings, valley flashings and their foam seals, side and top wall abutment flashings, break flashings, snow guards along the roof's own snow guards, a flashing kit for each roof window. A standing seam (Retro Panel) has its own: angled ridge caps, ridge supports, lap elements, butyl tape, valley clips. Ridge nodes, valley heads and valley clips are made from flat sheet (see **Flat sheet** below).
- **Fixings.** The tile's stitching screws where each side lap crosses the module and its fixing screws (ten a m² of cover, along the eaves, ridges, hips, verges, abutments and snow guards); a standing seam's flat-headed screws on each board a panel crosses and the flashings' sealed screws; and the support's nails or screws, one where a batten crosses a rafter and along the counter-battens. Each with 10 % to spare, in its pack.
- **Battens and membrane.** The battens across each slope on the profile's module (the first at the eave, the second 380 mm up, then every 350 mm; Retro Panel's boards every 200 mm), their length in bars; the counter-battens along the roof's own rafters, their real length; the membrane over every slope with its 100 mm laps and 8 % for cuts, in rolls, and the tape along its laps. Each slope's battens, rows and membrane laps are in **Battens and membrane per roof plane**. They are counted, not drawn: the 3D view shows the roof's build-up as before.

A column says what it is counted from in **Needed** (metres run, the area, the fixings). The numbers are the Panotaj Bilka app's on the same roofs; they are budget quantities, indicative: confirm them with the maker's installation guide.

![The takeoff in Panel layout: the accessories, the fixings, the battens and the membrane](shots/roof-panels-takeoff.webp)

## Rainwater system

The layout sizes the roof's gutters and downpipes as the Panotaj Bilka app does, after Bilka's guide, on the roof's own gutters (the ones **Gutters** in the roof's properties draws) and its downpipes:

- **Where the water goes.** Each slope drains to its eave when the eave has a gutter. A slope without one passes its water over a pitch break to the slope under it, or down a valley to the eaves at the valley's foot; what reaches no gutter is said (*… m² of roof drain to no gutter*), so turn a gutter off on an eave and its slope says it has no outlet.
- **The least downpipes on a slope**: the most of one, one per 10 m of its gutter, and its flow (its area by the rain) over a downpipe's capacity (1.5 l/s for 125/90, 3.0 l/s for 150/100, indicative).
- **The system.** **Auto** takes 150/100 when the roof drains more than 100 m² or the rain is heavier than 0.05 l/s·m², else 125/90; or choose one.

In the roof's **Panels** section, under **Rainwater system**:

1. **System**: Auto (it names the one it takes), 125/90 or 150/100.
2. **Downpipes**: leave it empty for Auto (the least), or type the number to count.
3. **Rain (l/s·m²)**: 0.05 for a budget; check the local value.

The section shows the **Least estimated**, the **Downpipes counted** and those **In the model**, and the check: *Within the estimate*, *Another system than the recommended one*, or *Under the least estimated*. A change of the system, the downpipes or the rain also takes the roof's gutters and downpipes to the system's sizes (125 or 150 mm, 90 or 100 mm), in the same undo step, so the model draws what the estimate counts. When the roof's gutters differ (after a change of the roof), **Size the gutters to the system** does it again. The model's downpipes stay where they are: if it has fewer than the estimate counts, it says so; add downpipes on the gutters (**Downpipe**) or make them editable.

![The Panels section's rainwater system: Auto, the least, the count, the model's downpipes](shots/roof-panels-drainage-section.webp)

In **Panel layout**, the **Rainwater** tab is the app's *Pluvial* sheet: each slope with a guttered eave (its eave, the area that drains to it, the flow, the least for each system, the downpipes the estimate gives it and the model's), then the gutters and downpipes counted as the app counts them: 4 m gutters along each eave, hooks every 600 mm, outer and inner corners, joints, end caps at free ends, an outlet, two 60° elbows, an extension, clamps (at most 2 m apart) and a shoe for each downpipe, the 3 m downpipes at the model's downpipes' average length, the wall plugs, and a cartridge of sealant every five ridge nodes, valley heads, hip end caps and odd corners. A gutter corner at another angle than 90° is cut from flat sheet. The lines have codes such as `gutter-150`, `outlet-150`, `downpipe-100`, `pipe-clamp-100` (the 125/90 system's end in `-125` and `-90`), priced by the Bilka – Dedeman template with the rest.

![The Rainwater tab: the system, each slope's least downpipes, the gutters and downpipes counted](shots/roof-panels-drainage.webp)

These are budget numbers: check the local rain, the gutters' falls, the outlets' places and the water a valley brings to one spot.

## Flat sheet

What is made from flat sheet (ridge nodes where three ridges meet, ridge–valley heads, Retro Panel's hip end caps and ridge end caps, odd gutter corners, strips for Retro Panel's valley clips) is laid on 2000 × 1250 mm sheets: each piece a whole rectangle on one sheet, 10 mm from the sheet's edges and 5 mm from the next piece, as the app's *Debitare tablă* sheet does. The pieces' sizes are budget envelopes that grow with the ridge cap's width (290 or 230 mm, Retro Panel's 417 mm) and the gutter's size.

The **Flat sheet** tab of **Panel layout** lists the sheets each combination of ridge cap and rainwater system takes (the roof's own marked ✓), draws each of the roof's sheets with its pieces (hover a piece for its name and size) and lists each piece's place. The sheets of the roof's own combination are bought: the line `flat-sheet-8017` (`flat-sheet-8019` for Retro Panel) in the Cost tab and the estimate.

![The Flat sheet tab: the four combinations, the roof's two sheets with the ridge nodes and the valley head](shots/roof-panels-sheets.webp)

## What it costs

Lapin has no prices: they come from your price lists (see [Cost](cost.md)). Every line the panels are bought by carries an **article code**, and a price list prices it by that code:

- **The panels**, per m² of sheet bought (each panel's nominal length by its whole width, how the shops price them), a line for each length: `bilka-clasic-35/7016` (Clasic 35 in RAL 7016), `bilka-retro-panel/8019`; Balcanic, sold in three stock lengths, has an article for each: `bilka-balcanic/8017/2930`.
- **The accessories, fixings and support** by their codes: `ridge-large`, `hip-small`, `eave`, `valley`, `screw-long`, `batten`, `board`, `membrane`, `wood-nails`… (Retro Panel's own flashings end in `-retro`); the rainwater system's and the flat sheet's: `gutter-150`, `outlet-150`, `downpipe-100`, `sealant`, `flat-sheet-8017`…

To try it with real prices, add the **Bilka – Dedeman (indicative, 24.11.2026)** list: in **Panel layout**, open the **Cost** tab and click **Add the Bilka – Dedeman template**, or **Construction → Price lists… → Templates → Add to the project**. It holds the public prices read on 24.11.2026, mostly at Dedeman, each row with its source (the shop and its product code), its page and the day it was read. It is marked *indicative* and is added only when you ask: a project never gets it by itself.

The **Cost** tab lists each line by group (panels, flashings and accessories, fixings, battens and membrane) with its **Quantity** and **Unit**, the **Price** the first list that has its code gives it, the **Amount** (the quantity by the price, rounded to the ban) and the **Source** (click it to open the shop's page). A line no list prices says **To quote**. The **Subtotal of the priced lines** adds only those with a price. Apart from them: the **Joints between ridge caps** (an operation, their material is in the caps) and what is **Cut from flat sheet** (ridge nodes, Retro Panel's hip end caps and valley clips, odd gutter corners: not bought; the flat sheets they are cut from are). The rainwater system has its group. Labour and transport are not included. The Panels section shows the same subtotal.

Change the profile, the colour, the ridge caps or the fixings and the table follows at once: Clasic 35 costs the same in RAL 7016 and 8017, Retro Panel has its own price, Balcanic a price for each stock length. The numbers are those of the Panotaj Bilka app's *Deviz RON* sheet on the same roofs.

![The Cost tab: each line with its price, amount and source, the subtotal of the priced lines, the ridge joints and what is cut from flat sheet apart](shots/roof-panels-cost.webp)

The same lines reach the **estimate** (**Construction → Estimate**) under the chapter **Roof**: they are the roof's quantities (the **Lapin_RoofPanelQuantities** set, which the properties, the schedules and the IFC export show too), priced by their codes. If the roof's type also has an article for its covering, take it off, or the covering is counted twice.

**Your own prices.** Import your list with the same codes (code, description, unit, price; a source, a link and a date if you have them), then in the estimate's settings choose the price lists it prices by, yours first: a code takes the first list's price that has it, the template the rest. Or remove the template from **Price lists…**. A price in another unit than the line's (a price per metre for a line in 2 m pieces) is marked ⚠: change the unit or the price. These prices are indicative: check them when you order.

## Walls, roof windows and dormers

An edge of the covering against a wall that rises over it (within 35 mm of its face, the wall's top at least 60 mm higher) is a **wall abutment**: a **wall top** where it is level (a lean-to under a wall), a **wall side** where it slopes (a gable against a higher wall). So is the top of a roof that runs under a higher roof's eave and ends there. Each gets its abutment flashing.

Roof windows and dormers are **cut out of the panels**: a column an opening crosses is laid below it, beside it and above it, each run starting on the profile's module as a column does. Round a roof window its kit's **bottom**, **top** and **sides**, and one flashing kit; round a dormer its front wall's apron (a wall top), its cheeks (wall sides) and the valleys of its roof with the slope. A roof standing inside a slope is cut out the same way, met in valleys. The battens and the membrane leave the openings out.

![A roof window cut out of the panels](shots/roof-panels-window.webp)

## The panels in the model

With a profile, the roof's covering **is** its panels: in 3D each panel is a sheet profiled as the tile or panel is (its waves, a tile's steps, a standing seam's ribs), in the profile's colour, the reused offcuts in orange. Plans, elevations and sections draw each panel as a plate of the covering's thickness over what it covers, so a drawing shows every joint without the corrugation's lines; a plan at 1:200 or larger draws the panels' outlines with the roof's other overhead lines. A roof with its own layers has its top layer replaced by the panels.

Where two roofs meet, the panels follow the covering as it is built: a wing's slopes end on the valleys with the main roof, the panels kept the valley gap short of them, and the main slope is panelled round the wing.

The IFC export still writes the covering as one roof slab (the panels as IfcCovering come in a later stage), so its GlobalIds do not change.

![The panels as the roof's covering in 3D](shots/roof-panels-3d.webp)

In the **Diagram** display the panels take the roofs' colour, as any covering does.

![The same roof in the Diagram display](shots/roof-panels-3d-diagram.webp)

## Roof panels schedules

**Architecture → Roof panels → Roof panels** makes the **Roof panels** schedule, a field schedule of every roof's panels: roof, profile, colour, length, bought or cut from an offcut, pieces, total length and sheet bought, with a totals line. Like any field schedule it can be changed, sorted and exported. Each panel is a part of its roof, so a **part** tag writes its code and length.

**Architecture → Roof panels → Roof panel accessories** makes the **Roof panel accessories** schedule: a row for each roof and line of its takeoff (group, item, quantity, unit, and what it is counted from).
