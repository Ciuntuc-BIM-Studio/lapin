---
title: Sheets, PDF and DXF
order: 100
summary: Lay views out on sheets with a title block, and print them to PDF or DXF.
---

<!-- video sheets -->

## Making a sheet

Click **+** next to **Sheets** in the project browser. To place a view, drag it from the browser onto the sheet, or choose it in **Place view…** in the sheet's bar. Each view keeps its own scale and stays linked to the model, so the sheet changes when the model does. On the sheet, drag a view to move it: the view goes alone, with its title, while the frame, the title block and the other views stay; it snaps to the frame's edges and middle and to the other views, and the arrow keys nudge it by 1 mm (10 mm with Shift).

![An A3 sheet with a plan and a section](shots/sheet.webp)

Select the sheet to fill in its **title block** in the properties panel: the project, the client, who drew it, who checked it, the date and the revision. Its number, paper and orientation are set there too. Left empty, the project field shows the project's name: the name given to the project, else its file's name.

## What a view shows

A view on a sheet shows what you choose, without dragging it blind:

- **Crop it on the sheet.** Click a view: handles appear at its corners and edges. Drag them and the box takes more or less of the view, while the drawing stays where it is on the paper. **Shift**-drag inside the view to slide the drawing within its box.
- **Draw its boundary in the view.** **Annotate → View boundary** (<kbd>VB</kbd>): draw a rectangle on a plan, section or elevation and give it a name, unique in the project. In **Place view…** the view is then offered with each of its boundaries ("Ground floor plan › East wing"), and a view placed that way shows exactly that rectangle and follows it when you move or resize it in the view. The same view can go on several sheets, one boundary each. A placed view switches between its boundaries, or to a free crop, in the list on its box. Boundaries show on the screen only, never on paper.
- **Pin it.** The pin on a view's box keeps it where it is: it is still picked, but not moved, nudged or cropped by mistake.

The frame and the title block belong to the paper. They are never picked or moved, and a click picks the smallest view under the pointer, so a view placed over the title block can always be selected.

![A view picked on a sheet: its crop handles, its boundary, the pin](shots/sheet-view-boundary.webp)

## Printing

The **PDF** and **DXF** buttons in the sheet's bar export the sheet. A PDF is ready to print or send. A DXF opens in AutoCAD and other CAD programs with its layers, lines and hatches. **Print** sends the sheet to a printer or plotter at its own paper size, at 1:1, as vector lines: choose the plotter and the paper in the print dialog. On a smaller printer, choose fit to page.
