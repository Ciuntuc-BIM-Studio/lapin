---
title: Drafting and the command line
order: 80
summary: Type commands as in CAD, snap to points, move and copy, draw lines, and annotate with dimensions, text and tags.
---

## The command line

Type a tool's letters anywhere in the window, then press <kbd>Enter</kbd>. For example, type <kbd>WA</kbd> for a wall, <kbd>CO</kbd> to copy, <kbd>M</kbd> to move, or <kbd>L</kbd> for a line. The command line at the bottom shows what the tool needs next. You can click a point or type a length, an angle or coordinates.

![Copying a wall: after the base point, the command line and the live input show the length and angle to the mouse](shots/command-line.webp)

Every tool shows its letters in its tooltip; you can change them in **View → Keyboard shortcuts…** (see [The interface and shortcuts](interface.md)). These are the ones used most:

| Model | | Draft | |
|---|---|---|---|
| Wall | <kbd>WA</kbd> | Line | <kbd>L</kbd> |
| Door, window | <kbd>DR</kbd>, <kbd>WN</kbd> | Polyline | <kbd>PL</kbd> |
| Floor | <kbd>SB</kbd> | Rectangle, circle, arc | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Roof | <kbd>RF</kbd> | Move, copy | <kbd>M</kbd>, <kbd>CO</kbd> |
| Stair, room | <kbd>ST</kbd>, <kbd>RM</kbd> | Rotate, mirror, scale | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Column, beam | <kbd>CL</kbd>, <kbd>BM</kbd> | Offset, trim, extend | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Section | <kbd>SE</kbd> | Fillet, chamfer | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Measure | <kbd>ME</kbd> | Hatch, text | <kbd>H</kbd>, <kbd>T</kbd> |
| Fit everything | <kbd>ZE</kbd> | Erase | <kbd>E</kbd> |

## Snaps and helpers

Lapin snaps to the ends, middles and intersections of what is drawn, and to the grid. <kbd>F3</kbd> turns snapping on or off, <kbd>F8</kbd> ortho and <kbd>F10</kbd> polar tracking. Their state shows at the bottom right of the view.

## Copying and moving

To copy, select the element and type <kbd>CO</kbd>. Click a base point, then a second point. Lapin keeps copying until you press <kbd>Enter</kbd> or <kbd>Esc</kbd>. **Move** (<kbd>M</kbd>) works the same way.

You can also drag instead. Drag the cross in the middle of a selected element to move it. Hold <kbd>Ctrl</kbd> (<kbd>⌘</kbd> on a Mac) while you drag to leave a copy.

A grid axis moves, copies, rotates and mirrors on its own, or together with other elements. A copy is a new axis of the same grid, named after the last axis that runs its way: with axes 1, 2 and 3, a copy of any of them is 4.

## Drafting

The **Draft** tab draws lines, polylines, rectangles, circles and arcs. It edits them with offset, trim, extend, fillet, chamfer and join, fills areas with hatches, and organises them on **Layers**. **Import DXF** brings a CAD drawing in as lines you can snap to and edit.

![The Draft tab](shots/ribbon-draft.webp)

## Annotating

The **Annotate** tab has the dimensions, text, grid axes, levels and tags. A dimension reads the model, so it updates when a wall moves. How text, dimensions, axes and tags look is set in **Manage → Drafting standards**.

An aligned or linear dimension goes on as a string: after the first two points and its line, each click adds the next point on the same line, and **Enter** (or **Esc**) makes the whole string one dimension (**U** takes the last point back; type **S** first for a single dimension). A string behaves like a polyline: a click selects all of it, and it moves, copies and deletes as one. Its grips sit on each measured point (drag to move it, Ctrl/⌘-click to take it out), in the middle of each segment (drag to add a point, Ctrl/⌘-click to add one halfway) and at the ends of the dimension line (drag to move the line). To put more points into a string, as in ArchiCAD, select it and click **Annotate → Add points** (or type **DIMADD**, or **Add points** in its properties): each point you click goes into the string where it falls along its line, between the points around it or beyond an end as the new end, and holds on the wall, edge or axis it lands on; **Enter** or **Esc** finishes. Double-click a segment to type its value: the element it measures moves, and so does everything after it in the string. The properties show each segment's value and text, split a string into single dimensions, and join single dimensions that continue one another on one line.

![The Annotate tab](shots/ribbon-annotate.webp)
