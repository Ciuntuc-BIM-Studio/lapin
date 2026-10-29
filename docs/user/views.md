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

Select the section in the browser to set its scale, its depth, its discipline and its style in the properties panel.

## Elevations

**+** next to **Elevations** makes the four elevations of the building at once.

![The south elevation](shots/elevation-view.webp)

## 3D views

<!-- video section-3d -->

Double-click **3D** to open the 3D view. Drag with the left button to orbit around the model. The key plan at the top right shows where you are looking from. **Plan**, **Persp** and **Fit** change the camera. To keep a camera, duplicate the 3D view.

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

## What a view shows

The **View** tab sets what the view in front shows and how it looks.

![The View tab](shots/ribbon-view.webp)

- **View settings.** The view's discipline and style, its range (the cut height and depth), and its graphics.
- **View styles.** Architectural, Structural (the cores only), MEP or your own. Each style chooses the layers drawn (all, without finishes, or the core) and how materials join.
- **IFC 2D symbols.** Elements imported or linked from an IFC file that bring their own plan drawing (a footprint, a door's swing, a fixture's symbol, with the file's colours, fills and texts) draw it in plans instead of their cut. Untick it in View settings (or in a view style) to draw their 3D cut instead.
- **Visibility** (<kbd>VV</kbd>) and **Graphics** (<kbd>VG</kbd>). They show or hide categories and set their pens, line types and fills.
- **Split** and **Window.** They show two views side by side, or open a view in a separate window.
