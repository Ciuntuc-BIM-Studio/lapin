---
title: Schedules and charts
order: 95
summary: List doors, windows, lintels, rooms and anything a module counts; add rows and columns of your own, type over the model's values, and chart the table.
---

## Getting quantities

Everything Lapin models is measured from its geometry: a wall's length, height, gross and net side area and volume, each layer's area and volume, a floor's areas and volume, a room's area and perimeter, a door's count. There are three ways to get the numbers:

- **One element.** Select it: the **Quantities** section of the Properties panel lists what it measures, then each of its layers or parts.
- **The whole model.** **Quantities → Quantity takeoff** makes (or opens) the takeoff: every element the model measures, added up row by row, with the count, length, gross and net area, gross and net volume and weight. Its toolbar chooses what is added up together: **By kind and type** (the default: the walls of each wall type, the floors of each floor type…), **By material** (each layer under its own material: the brick, the plaster and the insulation of the walls apart, the lintels' concrete), by storey, or a mix of them. **All elements** chooses which kinds it lists (only walls and floors, say). The takeoff follows the model like any schedule, and takes rows and columns of your own (a provision, a unit price).
- **Any other list.** **Quantities → Schedules…** makes any schedule this edition offers: rooms, room finishes, joinery, type assemblies, timber, rebar, formwork, steel… The same list is under **+** beside **Schedules** in the project browser.

![The quantity takeoff grouped by kind and material](shots/quantity-takeoff.webp)

**Quantities → Export XLSX** saves the takeoff as a workbook straight away (downloaded in a browser).

## Quantities on a sheet

A schedule goes on a sheet like a view, and is drawn there with all its rows and values, its totals and its charts:

- **Place on sheet…** in the schedule's toolbar: **New sheet** makes an A3 sheet with the table on it and opens it, or pick a sheet you have.
- Or drag the schedule from the project browser onto an open sheet: it lands where you drop it. Plans, sections and elevations are dragged the same way.
- Or **Place view…** in the sheet's own toolbar.

On the sheet, drag the table to move it; a table longer than the frame goes on over more pages (the sheet says so, and **place it on a new sheet** continues it). The table follows the model: add a wall and the sheet's numbers change. It prints with **Print** and goes into the sheet's PDF and DXF exactly as it is drawn.

![The quantity takeoff placed on a new sheet](shots/takeoff-sheet.webp)

## Making a schedule

A schedule is a table the model fills: doors and windows, lintels, rooms, rebar, timber, formwork and more. Click **+** beside **Schedules** in the project browser and pick what it lists, or use a module's tool (Wood → Timber schedule, Site works → Formwork schedule…). Double-click it in the browser to open it in a tab. A schedule follows the model: move a wall, add a window, and the table changes with it.

**Joinery**, **door**, **window** and **lintel** schedules show a card for each type, with its drawing (the interior view by default) and its values row by row. Other schedules are a plain table, one row per item and a totals line at the end.

A door or window flipped to the other hand of its type (**Hand**, or one flip arrow; flipped both ways it is only turned round) is the mirror image of its type, and gets a card of its own: its mark ends in **-M** (D-M), its name in "(mirrored)", and its **Opening** row, drawing and counts are those of the other hand. Flip it back and it returns to its type's card. Left and right are IFC's: the hinges as seen by someone pushing it open. The drawings show each door as it stands in the model, seen from the interior, the side it opens to (so a *swing left* door's hinges are on the right). A note under the cards says this.

![A joinery schedule with its fire rating and manufacturer rows](shots/joinery-schedule.webp)

## Fire ratings, manufacturers and other parameters

A door's or a window's parameters go into the joinery schedule by themselves:

- **Fire rating.** The **Fire rating** row reads a parameter named Fire rating, Fire Rated, FireRating or Fire resistance. Put it on the type, so every door of the type has it. In **Architecture → Doors & windows**, **Parameters · in schedules** → **+ Fire rating** adds the type's FireRating to Pset_DoorCommon (Pset_WindowCommon for a window), which is where IFC keeps it. A parameter you add yourself to one door, in its properties, shows too.
- **Any other parameter.** **+ Manufacturer**, **+ Product code**, **+ Website**, or a parameter of your own, becomes a row of its own in every joinery schedule. A door's own value shows next to its type's, when the doors of a type differ.
- **Typed in the schedule.** A value typed in a parameter's row (in italics) goes into the type, and every door of the type follows. Empty it to take the parameter off the type.

![The type's parameters in the doors and windows configurator](shots/joinery-params.webp)

**Unused types.** A type no door or window in the model uses is left out of the schedule. **Unused types** in the schedule's toolbar shows them again, with how many there are.

## Rows and columns of your own

Every schedule can be filled by hand, as a spreadsheet would be:

- **Row** adds a row of your own. In a joinery schedule it has a label, and you type its value for each type (Supplier, Delivery date…). In any other schedule it is a whole row, typed cell by cell (a terrace not modelled, a provision); it sits above the totals line.
- **Column** adds a column of your own. In a joinery schedule it is a card with no type behind it, every value typed (a garage door ordered apart). In any other schedule it is a column after the model's (Floor finish, Notes…).
- **Any cell** of the table under the drawing can be typed over. A value typed over the model's is marked; **↺** gives the model's value back. What you type stays with its row: a room's notes follow the room when the list is sorted or the room is renamed. In a **Rooms** schedule, a number, name, function, department, floor finish or height typed in a room's row goes into the room itself, as if typed in its properties; the plan's stamp and every other schedule follow.
- **×** beside a row or a column of your own removes it, with what was typed in it.
- The arrows in a table's header **sort** its rows by that column: once for rising, again for falling, a third time for the model's order. The totals line stays last.

All of it prints: on a sheet, in the PDF and DXF, and in the CSV and XLSX exports.

## Charts

**Charts** in the schedule's toolbar opens the charts beside the table. **New chart** makes one from the table:

- **Columns**, **Bars**, **Pie** or **Line**.
- **Categories from**: the column that names each bar or slice (Function, Name, Mark, Storey…).
- **How**: **Sum**, **Average**, **Least** or **Most** of a column (Area, Total, Volume…), or **Count of rows**.
- **At most**: keeps only the largest categories, and adds up the rest as Other.
- **Title**: its own title, or the one it makes (Sum of Area by Function).

The engine draws each chart after the tables, so it shows in the schedule, on a sheet that places the schedule, and in the PDF, exactly the same. It follows the model, and what you typed: change a room's area or add a row of your own, and the chart changes with it. **✎** changes a chart, **×** removes it.

![A room schedule with a row of its own and a pie chart of the areas by function](shots/schedule-chart.webp)

## Type assemblies

The **Type assemblies** schedule (**+** → **Type assemblies**) lists every wall and floor type: its mark, its total thickness, how many elements use it, then its layers in order with their materials and thicknesses.

**Build-up drawings**, in its toolbar, draws each type after the table, cut through its layers at the schedule's scale (1:50 unless you pick another): a wall as a plan cuts it, a floor as a section does, layer 1 on top. Each layer is filled and hatched as its material is in sections, the core's faces are drawn heavier, the total thickness is dimensioned, and each layer is named beside it as the table names it. The drawings print with the table, on a sheet and in the PDF. A new type assemblies schedule draws them; untick **Build-up drawings** to keep the table alone.

The drawings **On each card** (interior view, exterior view, plan, section), in a schedule's **Settings**, belong to the cards of the joinery schedules; a plain table has no cards, so they are not offered there.

![The type assemblies schedule with each type's build-up](shots/type-assemblies.webp)

## Exporting

**CSV** and **XLSX** in the toolbar export the schedule as a spreadsheet (an XLSX has one sheet for each table). In the desktop app Lapin asks where to save the file and offers the schedule's name; in a browser the file is downloaded. Exported wall and floor types are saved the same way.
