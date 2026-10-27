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

In an IFC export each room is an IfcSpace, with its number, name, function, areas and volume. Rooms from another program's IFC file come back as rooms.
