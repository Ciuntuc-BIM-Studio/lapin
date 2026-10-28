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

Double-click **{3D}** to open the 3D view. Drag with the left button to orbit around the model. The key plan at the top right shows where you are looking from. **Plan**, **Persp** and **Fit** change the camera. To keep a camera, duplicate the 3D view.

### A section in 3D

Right-click a section in the browser and choose **Show in 3D**. The 3D view cuts the model along the section's line. It keeps the side the section looks at, up to the section's depth if it has one. The section's own drawing lies on the cut, with the same cut fills and hatches a sheet prints. Its dimensions, level marks, tags and texts lie on the same plane, the same size and in the same place as in the section. Lines keep their pen weights and their full dash patterns. Those drawn beside the building stand in the air beside the cut. **Annotations** on the chip at the bottom of the 3D view hides them or shows them again. An elevation's **Show in 3D** turns the camera to look the way the elevation does.

![The section's right-click menu in the browser](shots/section-3d-menu.webp)

![Section B-B seen in 3D](shots/section-3d.webp)

To see the whole model again, click the **×** on the **Section** chip at the bottom of the 3D view, or choose **Show in 3D** again.

### Walking into a room

In a plan, right-click inside a room and choose **Walk in 3D**. The 3D view puts you inside the room, 1.60 m above its floor, looking along its longest side.

![A room's right-click menu in the plan](shots/room-walk-menu.webp)

![Standing in the living room](shots/room-walk.webp)

- Drag with the left button to look around.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> or the arrow keys walk. Hold <kbd>Shift</kbd> to run. <kbd>Space</kbd> jumps.
- The mouse wheel steps forward and back.

You walk as a person would. Walls, columns, furniture and closed windows stop you, and you slide along them. You stand on floors, walk up and down stairs and ramps, and fall if you walk off an edge. Doors are open for you: walk through their openings.

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
