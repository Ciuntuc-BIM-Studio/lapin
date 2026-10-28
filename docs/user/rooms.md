---
title: Rooms and room colours
order: 45
summary: Place rooms with a click inside walls, name and number them, and colour or hatch them on a plan by function or department, with a legend.
---

## Placing a room

Choose **Architecture → Room** (<kbd>RM</kbd>) and click inside a space closed by walls. Lapin finds the room's outline between the faces of the walls, curtain walls and columns around the point. Doors do not open the room: it closes across them. A column standing in the room is left out of its area.

The room keeps up with its walls. Move a wall and the room grows or shrinks with it. If a wall is deleted and nothing closes round the room any more, it keeps the outline it had, and its stamp turns red. **Architecture → Update rooms** stores the outlines the rooms find now.

For a space without walls all round, such as a terrace or one end of an open-plan area, use **Room by outline** and click its corners. That room keeps the outline you draw.

![A plan with two rooms coloured by function and a colour legend](shots/rooms-plan.webp)

## A room's properties

Select a room by clicking its stamp or anywhere inside it. In the properties panel:

- **Name** and **Number.** New rooms are numbered by level: 101, 102… on the ground floor.
- **Function**, **Department** and **Floor finish.** Type anything, or pick a value other rooms already have.
- **Height.** Empty means the level's height.
- **Own colour** and **Own hatch** override the colour scheme for this one room.

The quantities below show the room's net area (without the columns), its perimeter and its volume. Drag the grip on the stamp to move the stamp.

![A room's properties](shots/room-properties.webp)

## Room colours

**View → Room colours** colours the rooms of a plan. Pick a scheme, choose what it colours by (**Function**, **Name**, **Department**, **Number** or **Floor finish**), and set **This plan shows** to the scheme. Each value the rooms have gets a row with:

- a **Colour**,
- a **Hatch** from the project's patterns, with its **Scale** and **Ink** colour,
- and a preview of how it looks.

**Fill missing colours** gives every value without a colour one from a soft palette. **New** and **Duplicate** make more schemes, for example one for a client presentation and one by department.

![The Room colours dialog](shots/room-colors.webp)

The colours are drawn under the walls, the furniture and the door swings, so the plan stays readable. They print on sheets and go into PDF and DXF exports. A new plan shows no room colours until you choose a scheme for it, in **Room colours** or in the plan's properties.

## Colour legend

**Annotate → Colour legend**: click where the legend goes on the plan. It lists the colours the plan's rooms use, with their names, and follows any change to the scheme. Move it with **Move**.

## Rooms in 3D and in IFC

Rooms are hidden in the 3D view, so they do not cover the building. **Visibility / Graphics** in the 3D view can show them.

In an IFC export each room is an IfcSpace, with its number, name, function, areas and volume.

## Rooms from an IFC file

**File → Import IFC…** brings the file's spaces in as rooms, whichever program wrote them (Revit, ArchiCAD, Allplan, Bonsai…):

- each goes on **its own level**, the one the file puts it in;
- its **outline** comes from its shape (an extrusion, a B-rep or a mesh, or the footprint drawn beside it); it keeps that outline, it does not follow walls;
- its **name** is the space's long name and its **number** its name (as Revit and ArchiCAD write them); its **height**, and its **base offset** when it starts above its level (a gallery, a raised floor), come from its shape;
- its **function**, **department** and **floor finish** come from the file where it says them (the object type or the space category, a Department property, the floor covering), so a colour scheme by function or by department colours them at once;
- everything else the space had in the file, its property sets and quantities (Pset_SpaceCommon, BaseQuantities, the program's own sets), is kept and listed, read-only, under **IFC properties** in its properties, with a filter when there are many.

The import's message says how many rooms came in. Look for them on their levels' plans (each shows its stamp), in a **Rooms** schedule (**+** under Schedules in the browser) or in the browser's IFC tab; the 3D view hides rooms.

A space whose shape gives no outline still comes in, with its name and properties, as a room without an outline: it is listed in the schedule but drawn nowhere. Select it (the browser's IFC tab), open its level's plan, then **Find by a click** (click inside its walls) or **Draw outline** in its properties.
