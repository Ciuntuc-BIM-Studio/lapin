---
title: Visual graphs
order: 128
summary: Wire nodes into a recipe — numbers, lists, points and the tools' own commands — see what it would make as a ghost, and bake it in one undo step.
---

## What a graph is

A **graph** is a recipe drawn as boxes and wires. Each box is a **node**: a number, a range of numbers, a point, a sum, or one of the tools themselves (Wall, Floor, Column… every command of your edition). Wires carry what one node gives into another. Lapin works the graph out as you change it and shows what it would make as a **ghost** in the plans and the 3D view; nothing is made until you press **Bake**, and then it is one undo step.

Graphs need no programming and no Python: Lapin's engine evaluates them, in the desktop app and in Lapin for the browser alike. They are kept with the project.

## The Graphs panel

Open **View → Graphs**. The panel opens beside the view: drag the line between them to share the width. On the desktop, drag the **Graphs** tab out of the window (or press the window button beside the tabs) to give the panel a window of its own, to float or to put on a second screen; the ghost still shows in the main window.

![A graph making a row of walls: the panel beside the plan, the walls as a ghost](shots/graphs-panel.webp)

- **Above**: the project's graphs, **New graph**, the graph's name (type to rename it), **Delete graph**, the eye that shows or hides the ghost, and **Bake**.
- **The canvas**: double-click an empty place to add a node: type a few letters (`wall`, `range`, `point`) and press **Enter**. Drag from an output (right side of a node) to an input (left side) to wire them. Drag a node by its title; Shift-click to select more, or drag a box round them; **Delete** takes them out. The wheel zooms, the middle button (or **Space** and drag) moves the canvas.
- **The inspector**, on the right: the selected node's inputs that have no wire (type a value), what it gives, and why it failed if it did.

Every change is one undo step: **Ctrl+Z** undoes a node added, moved or wired as it undoes a wall.

## Ports and lists

Ports are coloured by what they carry: numbers blue, texts amber, points green, elements purple, lists grey. A wire between kinds that do not meet is refused, with why.

There are no data trees. A **list** wired into an input that takes one value runs the node once for each item: a **Series** of three numbers into a **Point**'s X gives three points, and three points into a **Wall**'s Start give three walls. Several lists run side by side; a shorter list repeats its last item. A wire carrying a list is drawn thicker, with the number of items on it. Some inputs take the whole list instead (**Count**, **Sum**, **Item**, **Polyline**).

A tool's node takes the same inputs as its command: a **Wall** has **Start** and **End** points, a height, a storey, a type. Its outputs are **Elements made** and **Result** (what the command answered).

## List nodes

Besides **Range**, **Series**, **Count**, **Item**, **Reverse**, **Flatten**, **Sum**, **Smallest and largest** and **Filter**, the **Lists** group has:

- **List**: a list you type, its items between semicolons or each on a line of its own: `3; 4,5; 6`. A decimal comma or point is fine. **Kind** **Auto** gives numbers when every item is a number, else texts; **Numbers** says which item is not one; **Texts** keeps everything as text.
- **Join lists**: the lists wired into A, B, C and D one after the other. A single value counts as a list of one, so it also gathers a few values into a list; an input left empty is left out.
- **Sort**: numbers by size, texts alphabetically, with the numbers in them by size (W2 before W10, capitals or not). Wire another list into **By** to sort by its values, place for place: the walls of a class by their height (**Property** `height` into By). **Descending** turns it round. Equal values keep their order; an empty item goes last. **Order** says where each item was, from 0.
- **Part of a list**: **Count** items from **Start** (from 0). A negative start counts from the end; no count takes everything to the end. The first three: start 0, count 3; the last three: start -3.
- **Unique**: each value once, in the order first seen, and **How many** times each was there. Numbers a billionth apart or less are the same, points too; texts are compared as written (capitals count), and the number 1 is not the text „1”.
- **Every with every**: every pair of an item of A and an item of B. Its outputs A and B are as long as the pairs (A's items each repeated, B's over and over), so the next node, fed both, runs once for every pair: a **List** of X values and one of Y values into Every with every, then into a **Point**, give a point at every crossing. At most 10,000 pairs.

## Errors

A node that fails turns red and says why in the inspector (and when hovered): a wall of no length, a division by zero. The nodes after it say which input has nothing; the rest of the graph is still worked out. When only some items of a list fail, the node turns amber and says how many.

## Bake

**Bake** makes what the ghost shows, in one undo step named **Bake graph**. If any of the tools' nodes fails, nothing is made and Lapin says which node failed. Once baked, the ghost is hidden (the eye shows it again).

![The row of walls baked: three walls, one undo step](shots/graphs-baked.webp)
 Bake again after a change and the graph makes new elements; the ones made before stay as they are (a graph that keeps its elements up to date is planned).

## Good to know

- A graph does not run code and cannot read or write files: tools that import, export or open projects are not offered as nodes.
- A graph is evaluated for at most 5 seconds and runs at most 2,000 commands at once; a list holds at most 10,000 items.
- In a shared session, graphs are the host's, like scripts: a guest cannot open, preview, bake, rename or delete them; their Graphs panel says so.
