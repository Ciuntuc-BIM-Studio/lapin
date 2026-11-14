---
title: Sheets, PDF and DXF
order: 100
summary: Lay views out on sheets with a title block, and print them to PDF or DXF.
---

<!-- video sheets -->

## Making a sheet

Click **+** next to **Sheets** in the project browser. To place a view, drag it from the browser onto the sheet, or choose it in **Place view…** in the sheet's bar. Each view keeps its own scale and stays linked to the model, so the sheet changes when the model does. On the sheet, drag a view to move it: the view goes alone, with its title, while the frame, the title block and the other views stay; it snaps to the frame's edges and middle and to the other views, and the arrow keys nudge it by 1 mm (10 mm with Shift).

![An A3 sheet with a plan and a section](shots/sheet.webp)

A saved **3D view** goes on a sheet the same way. It is drawn in lines, as an elevation is, from the camera the view keeps (the camera button beside it in the project browser, **Keep the camera as it is now**, saves the camera as it is). An orthographic camera shows the whole model seen from that side, an axonometric at the view's scale (1:100 until you set another). A perspective camera shows what its field of view takes in, at its true size where the camera looks. The drawing follows the view's Visibility / Graphics, the elements hidden in it, the storeys it shows and its section box, and prints the same in the PDF. A 3D view saved without a camera is seen from the south-west, from above. Double-click it on the sheet, or click ✎, to open it in 3D with its camera.

Select the sheet to fill in its **title block** in the properties panel: the project, the client, who drew it, who checked it, the date and the revision. Its number, paper and orientation are set there too. The paper is an A size (A0 to A4), one of ISO's long sheets (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), a North American size (Letter, Tabloid, ARCH D, ARCH E), or **custom**, with its **Paper width** and **Paper height** in millimetres. Left empty, the project field shows the project's name: the name given to the project, else its file's name.

## What a view shows

A view on a sheet shows what you choose, without dragging it blind:

- **Crop it on the sheet.** Click a view: handles appear at its corners and edges. Drag them and the box takes more or less of the view, while the drawing stays where it is on the paper. **Shift**-drag inside the view to slide the drawing within its box.
- **Draw its boundary in the view.** **Annotate → View boundary** (<kbd>VB</kbd>): draw a rectangle on a plan, section or elevation and give it a name, unique in the project. In **Place view…** the view is then offered with each of its boundaries ("Ground floor plan › East wing"), and a view placed that way shows exactly that rectangle and follows it when you move or resize it in the view. The same view can go on several sheets, one boundary each. A placed view switches between its boundaries, or to a free crop, in the list on its box. Boundaries show on the screen only, never on paper.
- **Pin it.** The pin on a view's box keeps it where it is: it is still picked, but not moved, nudged or cropped by mistake.
- **Place it again at another scale.** Right-click a view on the sheet and choose **Place again at 1:25…** (the project's other standard scales are offered), then drag a rectangle on the view round the part to show, or just click to take the view as it is cropped. Lapin duplicates the view with its annotations, gives the copy that scale, crops it to the part and places it on the same sheet beside the original, to its right if there is room (else left, below or above), in one undo step: a plan at 1:100 and its corner at 1:25 on one sheet. The copy is named after the view and its scale ("Ground floor 1:25"). In the project browser, right-click a view that is on a sheet for the same, with the view as it is cropped; its sheet opens.

![A section placed again at 1:50 beside itself on its sheet](shots/sheet-place-again.webp)

The frame and the title block belong to the paper. They are never picked or moved, and a click picks the smallest view under the pointer, so a view placed over the title block can always be selected.

![A view picked on a sheet: its crop handles, its boundary, the pin](shots/sheet-view-boundary.webp)

## Your own title block

**Manage → Title blocks…** lists the title blocks: Lapin's own (**Lapin standard**, the one every sheet had so far, the Romanian **cartuș** and an **ISO 7200** block), this project's, and this computer's library, which every project on this computer offers. Click one to see it with the project's values. **Duplicate into the project** makes an editable copy, **New** an empty one; **Set as project default** gives it to every sheet that does not choose its own, and a sheet chooses its own under **Title block** in its properties.

![Title blocks: Lapin's, the project's and this computer's, the chosen one with the project's values](shots/title-blocks.webp)

**Edit…** opens the designer. The block is a box in paper millimetres that sits in a corner of the frame (bottom right by default), so the same block fits A4 to A0, portrait or landscape; the frame's margins are set with it. Draw with **Line**, **Rectangle**, **Polyline**, **Text**, **Image** (your logo, PNG or JPEG) and **Revision table**; a rectangle or a closed polyline takes a fill or a hatch, a text a height or one of the text styles of Drafting standards. Clicks snap to the items' ends, corners and middles and to the grid; **Shift** keeps a line straight. Drag an item to move it, its handles to reshape it; the arrows nudge it, **Delete** removes it, **Ctrl+Z** undoes.

The **Fields** list holds the live text: the project's (name, number, client, site, permit, designer, people, and your own parameters), the sheet's (number, name, scale, paper, revision, status, date, drawn, checked and approved by, phase, sheet x of y) and the system's (date of print, file name). A click puts a field into the selected text, or places it as a new one; a text can mix words and fields, `Project: {project.name}`. The designer shows each field's value on a sample sheet; a field with no value shows as its name, greyed, and one Lapin does not know in red (it prints as `<field>` and **Manage → Check** reports it). **Save** keeps the block as one step to undo.

![The designer: a Finnish-style title block, its fields with their values, a field added under the drawing number](shots/title-block-designer.webp)

The sheets, their PDF, Print and the issue draw it with each sheet's values. In DXF it is a block with attributes, one for each live text (`PROJECT_NAME`, `SHEET_NUMBER`…), as AutoCAD's own title blocks are. **Export…** saves a title block as a `.json` file, its images inside, and **Import…** reads one into the project or the library, so an office passes its title blocks from one computer to the next. Old projects keep Lapin standard and draw exactly as before.

![The sheet with its own title block](shots/sheet-title-block.webp)

In the desktop app, **Title blocks** also has a **Firm folders** block: the `.lapintitleblock` files in your firm's folders, one click adding a block to the project, and **Save to firm folder…** writing the chosen block into a writable folder, so every drawing office project starts from the same blocks.

![Title blocks: the firm folder's file](shots/title-blocks-firm-folder.webp)

## Printing

The **PDF** and **DXF** buttons in the sheet's bar export the sheet. A PDF is ready to print or send. A DXF opens in AutoCAD and other CAD programs with its layers, lines and hatches. **Print** sends the sheet to a printer or plotter at its own paper size, at 1:1, as vector lines: choose the plotter and the paper in the print dialog. On a smaller printer, choose fit to page.
