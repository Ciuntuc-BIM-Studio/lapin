---
title: Sections, elevations and 3D
order: 70
summary: Make sections and elevations, open 3D views, and set what each view shows and how.
---

All the views are in the project browser, filed under their discipline: Architecture, Structure or MEP. Double-click a view to open it in a tab.

![The project browser with a section and an elevation](shots/browser.webp)

## Sections

Choose **View → Section** (<kbd>SE</kbd>), or click **+** next to **Sections** in the browser. Then draw the cut line in a plan with two clicks, and click a third time on the side the section looks to. The section is drawn from the model and stays up to date as you change it.

![Section A-A through the house](shots/section-view.webp)

A section need not be one straight line. **Jogged section** (<kbd>SJ</kbd>) draws it as a polyline, click after click, and <kbd>Enter</kbd> finishes: each straight piece is a planar cut that sees only between its own ends, and the pieces are drawn side by side at their real lengths, a fine dashed line marking each jog. **Curved section** (<kbd>SV</kbd>) draws an arc through three points (its start, its end, a point on it) and unrolls it the same way, so a curved facade reads at its true length. **Section along** (<kbd>SA</kbd>) picks an element with a line in plan, a road or a path, a ditch, a retaining wall, a wall, a beam, a footing or a drafted polyline, and follows it as it is now; it writes the stations along the bottom (0+020, every tenth of the scale, or the step you set under **Stations** in the view's settings). In the plan, a section's points are grips: drag one, drag a piece's middle to add a jog, Ctrl/⌘-click a point to take it out. Shown in 3D, a developed section cuts the model along its first piece and lays its drawing piece by piece.

![A jogged section through the house: three pieces side by side, the jogs marked](shots/section-jogged.webp)

Select the section in the browser to set its scale, its depth, its discipline and its style in the properties panel.

### Where a section is marked

A section's line is drawn in every plan, and the section is marked in every other section and elevation its plane crosses: a fine dashed line where the plane cuts the view, with the section's name and an arrow looking the way the section looks at its top, the same size on paper as in a plan. A section is never marked in itself. Click a marker to select the section, double-click it to open it.

Each section sits on a **marker layer** (**Sections** at first; **Marker layer** in the section's properties changes it), and each view chooses what it marks. **View → Sections…** lists the project's sections by layer for the view in front, with a checkbox per layer and per section for this view alone, the layer of each section (an existing one, or **New layer…**), and in how many views each is hidden. A sheet marks what its views mark.

A section cut across a beam or a column (**Section here**, **Section across** on the Simple rebar tab) has grips in plan too: drag either end along the line to give that side more or less margin, drag the middle along the element to move the station it cuts at. The section's window follows.

![View → Sections… for the south elevation](shots/sections-dialog.webp)

## Elevations

**+** next to **Elevations** makes the four elevations of the building at once.

![The south elevation](shots/elevation-view.webp)

### Sun shadows

An elevation, a section or a site plan can draw the shadows the sun casts on what it sees, filled under its lines, so they print on sheets and in PDF and DXF. Choose them in the view's properties, under **Sun shadows**:

- **own sun**: a sun of the view's own. By default the drawing convention: the sun over your left shoulder, its rays along a cube's diagonal (from 45°, 35.26° high), so a shadow is as deep as what casts it stands out. **Sun from** and **Sun altitude** turn it; **Drawing convention** brings it back.
- **project sun**: the sun of the project's place (Manage → Location) on a day and at an hour of the view's own. **As in Sun and shadows** takes the day and hour of View → Sun and shadows.

A section casts and receives only what lies beyond its cut. A storey plan draws no shadows: its walls, cut at the plan's height, would cast them too short.

![The south elevation with the roof's shadow](shots/elevation-shadows.webp)

![A view's sun shadows](shots/view-shadows.webp)

### Surface hatches

An elevation or a section hatches each face it sees with its material's **Elevation** hatch (**Manage → Materials**, the **2D graphics** tab): its pattern, scale, angle, pen and fill. Only the part of a face no nearer element hides is hatched, so a facade's render or brick shows round its doors and windows, and a section hatches what it sees beyond its cut, not behind the elements it cuts. A layered wall shows its outermost layer's material. The hatch is drawn under the lines, so it prints on sheets and in PDF and DXF (layers SURFACE-HATCH and SURFACE-FILL).

With **Alignment** *view* the pattern runs on from one face to the next; with *layer* it lies on the face itself, along its level line from its bottom corner, and a face seen at a slant shows it foreshortened. One material going on in one plane across several elements is hatched as one, unless the view style's **Join materials** is off.

Until you choose one, a material's elevation hatch comes **from the name**, as its section hatch does, and **Manage → Materials** shows which (*from the name (BRICK)*): brick and masonry BRICK, stone STONE, timber BOARDS, metal cladding VERTICAL, roof tiles, shingles and slates HORIZONTAL, tiles TILES, gravel and earth as in sections. Insulation, membranes, plaster, render, paint and concrete get none: insulation's BATT is a section's hatch, never suggested for a surface (chosen by hand, it is drawn). Choose **none** to draw nothing. A developed section (jogged, curved or along an element) hatches each of its pieces between the piece's ends, and draws their sun shadows the same way. The view style's **Surface hatches** (**View → View settings**, **Manage → View styles**) turns them off for whole views: on in Architectural, off in Structural, MEP and Coordination. Plans keep their own cut and plan hatches. A roof's covering is hatched by its covering material, so a tiled roof shows its courses on the slopes the view sees, and none on those turned away.

![The south elevation, its facing brick hatched as its name suggests](shots/elevation-surface-hatch.webp)

![A jogged section hatching the garden wall its last piece sees](shots/section-developed-surface-hatch.webp)

![Surface hatches in the view's settings](shots/view-surface-hatch.webp)

## 3D views

<!-- video section-3d -->

Double-click **3D** to open the 3D view. Drag with the left button to orbit around the model. The key plan at the top right shows where you are looking from. **Plan**, **Persp** and **Fit** change the camera. To keep a camera, duplicate the 3D view.

The mouse wheel zooms to the point of the model under the cursor, which stays where it is on the screen; the zoom stops just short of that point and never passes through it. A drag with the left button turns the view round the selected elements' middle, which stays where it is on the screen, so you can look at them from every side; with nothing selected it turns round the point of the model under the cursor where the drag starts, and round the middle of the view if the cursor is over empty space. **View → Zoom to cursor** and **View → Orbit around selection** turn each off (the wheel then zooms to the middle of the view, and the view turns round its middle), and so do the checkboxes in a 3D view's properties. The choice is kept on this computer.

### A section in 3D

Right-click a section in the browser and choose **Show in 3D**. The 3D view cuts the model along the section's line. It keeps the side the section looks at, up to the section's depth if it has one. Every cut element is closed where it is cut, layer by layer, each layer in its own material: a wall shows its brick core and its plaster, as solid as the wall is. **Hatches** on the chip at the bottom of the 3D view lays the section's drawing on the cut instead, with the cut fills and hatches a sheet prints. The section's dimensions, level marks, tags and texts lie on the same plane, the same size and in the same place as in the section. Lines keep their pen weights and their full dash patterns. Those drawn beside the building stand in the air beside the cut. **Annotations** on the chip at the bottom of the 3D view hides them or shows them again. An elevation's **Show in 3D** turns the camera to look the way the elevation does.

![The section's right-click menu in the browser](shots/section-3d-menu.webp)

![Section B-B seen in 3D](shots/section-3d.webp)

To see the whole model again, click the **×** on the **Section** chip at the bottom of the 3D view, or choose **Show in 3D** again.

![Section B-B with Hatches on: the cut as the sheet prints it](shots/section-3d-hatches.webp)

### The section box

**View → Section box** (shortcut BX) cuts the 3D view to a box. The box starts round the whole model. Each face has an arrow in its middle: drag it to move that face. Every element the box cuts is closed on that face, layer by layer, in its materials, and the faces meet cleanly at the box's edges. **Cut at plan** sets the box's top at the active level's plan cut (its view range), so the level reads in 3D as its plan cuts it. **Fit** puts the box back round the whole model. To turn the box, drag the round handle past its top corner (by the degree; hold Shift for 15° steps), or select a wall and click **Align**: the box turns the way the wall runs. **Hatches** draws the box's cuts as a sheet does, each material with its cut fill and hatch, instead of the materials. A saved 3D view keeps its section box: save the view with the box on, or open a saved view and set its box, and the box is there each time the view opens. The **×** on the **Section box** chip turns the box off.

![The section box cut at the ground floor's plan, its east face dragged in](shots/section-box.webp)

![The same box with Hatches on: the cuts as a sheet draws them](shots/section-box-hatches.webp)

### One storey at a time

To look inside the building, show only some of its storeys. Right-click a level in the project browser and choose **Show only this level in 3D**, or **Show this level and below in 3D** to see the building cut at that level. The level becomes the active one, and the 3D view follows the active level: click another level in the browser to go up or down a floor.

![The level's right-click menu in the browser](shots/storeys-3d-menu.webp)

![The ground floor alone in 3D](shots/storeys-3d.webp)

**View → Storeys shown**, or the 3D view's properties, sets the rest:

- **Show**: every storey, the storeys ticked, the active storey only, or the active storey and those below.
- **On no storey**: what stands on no storey (the site, the terrain, linked files) is shown or hidden.
- **Cut above its level**: the model is cut that far above the highest storey shown, 1.20 m at first, as a plan cuts it. Every element cut there is closed on the cut, layer by layer, in its materials, as a section shown in 3D is.

![The ground floor cut at 1.20 m: its walls closed, layer by layer](shots/storeys-3d-cut.webp)

The storeys hidden are not drawn, and you cannot select them, fit to them or walk on them. What a cut takes away (the cut above the storeys shown, a section shown in 3D, the section box) is not there for a walk either: you walk past a wall's upper part the cut has removed, and the caps on the cut close what it opens. **File → Export 3D…** takes the 3D view as it shows it (see [Import and export](import-export.md)). A saved 3D view keeps its own choice. The **Storeys** chip at the bottom of the 3D view says what is shown; its **×** shows every storey again.

### Without finishes, or the cores only

A 3D view can show the structure of the building alone, as a structural plan does. In the 3D view's properties (with nothing selected), or in **View → View settings** with the 3D view in front, **Layers** shows:

- **every layer**: the walls, floors and footings as they are built;
- **without finishes**: without the layers whose material is a finish (plaster, render, paint, tiles, boards, floor coverings, or a material whose category is Finish in Manage → Materials); the insulation stays;
- **the core only**: only the layer marked as the core (the brick, the concrete slab).

![A house in 3D with the core only: the walls are their brick, the floors their slab](shots/layers-3d-core.webp)

Each layer kept stays a closed solid in its own material, so a cut or the section box closes it as before. Doors and windows stay in their openings; a reveal lined in a finish goes with the wall's finishes. A 3D view takes its layers from its style, like a plan: set its **Discipline** to Structure and it shows the cores only; set **Layers** to choose otherwise, and the button beside it goes back to the style's. The **Layers shown** chip at the bottom of the 3D view says what is left out; its **×** shows every layer again. The layers hidden are not selected or walked against, and **File → Export 3D…** as the view shows it exports the same layers.

### Walking into a room

In a plan, right-click inside a room and choose **Walk in 3D**. The 3D view puts you inside the room, 1.60 m above its floor, looking along its longest side.

![A room's right-click menu in the plan](shots/room-walk-menu.webp)

![Standing in the living room](shots/room-walk.webp)

- Drag with the left button to look around.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> or the arrow keys walk. Hold <kbd>Shift</kbd> to run. <kbd>Space</kbd> jumps.
- The mouse wheel steps forward and back.

You walk as a person would. Walls, columns, furniture and closed windows stop you, and you slide along them. You stand on floors, walk up and down stairs and ramps, and fall if you walk off an edge. Doors are open for you: walk through their openings. Only what the 3D view draws stops you: hidden elements, the storeys it does not show and what its cuts take away are not in your way.

![Walking to the bedroom's door](shots/room-walk-door.webp)

![Up the stair](shots/room-walk-stair.webp)

**Fly** in the walk's chip, or <kbd>F</kbd>, lets you fly instead: no gravity, and you pass through walls and floors. <kbd>E</kbd> and <kbd>Q</kbd> go up and down. Press <kbd>F</kbd> again to walk; you drop onto the floor under you.

<kbd>Esc</kbd> or **Exit walk** brings back the camera you had before.

### Realistic, with the sun

View → **Display quality** sets how the 3D view renders, on this computer:

- **Drawing**: the sheet’s palette and an even light, as Lapin always drew. Quick on any computer.
- **Medium**: the sun’s shadows, shade in the corners where faces meet, and light from the sky.
- **Realistic**: the sky behind the model and a ground under it (where the project has no terrain), and glass the light comes through. For presenting, on a good graphics card.

![A house in the realistic view](shots/view-realistic.webp)

The sun is the project’s: where it stands for the project’s place (Manage → **Location**) on a day at an hour. View → **Sun and shadows** sets the day (the equinox and the solstices are a click away) and the hour, plays the day through, and turns the sun on in the drawing view too. Without a coordinate system and a survey point, the place is Bucharest. The time zone follows the place’s longitude, with European summer time in Europe.

![Sun and shadows](shots/sun-dialog.webp)

The realistic view takes some of its numbers (the shade in corners, the shadows' quality, the sun's colour) from [BimGo](https://github.com/aussieBIMguru/BimGo), by Aussie BIM Guru (MIT-0).

## What a view shows

The **View** tab sets what the view in front shows and how it looks.

**Grid** (<kbd>F7</kbd>) hides or shows the plan's background grid, and **Thin lines** (<kbd>TL</kbd>) draws every line one pixel thick, to read a dense drawing. Both are for the screen only: sheets, PDF and DXF keep their pens, and snapping to the grid stays. The 3D view's ground grid follows the same switch.

![The View tab](shots/ribbon-view.webp)

- **View settings.** The view's discipline and style, its range (the cut height and depth), and its graphics.
- **View styles.** Architectural, Structural (the cores only), MEP or your own. Each style chooses the layers drawn (all, without finishes, or the core) and how materials join.
- **IFC 2D symbols.** Elements imported or linked from an IFC file that bring their own plan drawing (a footprint, a door's swing, a fixture's symbol, with the file's colours, fills and texts) draw it in plans instead of their cut. Untick it in View settings (or in a view style) to draw their 3D cut instead.
- **Visibility** (<kbd>VV</kbd>) and **Graphics** (<kbd>VG</kbd>). They show or hide categories and set their pens, line types and fills.
- **Split** and **Window.** They show two views side by side, or open a view in a separate window.
- **Display quality.** How the 3D view renders (**Drawing**, **Medium** or **Realistic**, kept on this computer: see *Realistic, with the sun*), and how the 3D view draws parts of many triangles (an imported plant, furniture, pipes) when they are far away. **Balanced** draws a far part coarser only where the difference is under a pixel. **Fast** lets it stray up to 3 pixels, for large models and slower computers. **Precise** draws every part as it is. The precision belongs to the project. The model, its drawings, exports and quantities are never coarsened: a curve is made of straight pieces that stray at most a millimetre from it.
- **Sun and shadows.** The project’s sun on a day at an hour, lighting the 3D view with its shadows (see *Realistic, with the sun*).

![Display quality](shots/display-quality.webp)
