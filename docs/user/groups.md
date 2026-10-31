---
title: Groups and clearing up
order: 112
summary: Keep elements together as a group, open a group to edit it, select by a region or a lasso in 3D, and hide what is in the way.
---

## Groups

A group keeps elements together. Select them and click **Modify → Create group** (<kbd>GP</kbd>). A click on any member then selects the whole group, in plans and in 3D. The group moves, copies, rotates, mirrors and deletes as one, and a copy is a group of its own. In IFC a group is an IfcGroup, so other programs see it too.

A selected group's properties give the number of elements it holds, with three buttons: **Edit group**, **Ungroup** and **Select members**.

![A group selected: its members lit, its properties](shots/group-selected.webp)

## Editing a group

Double-click a member, or click **Edit group** (<kbd>EG</kbd>). The rest of the model is drawn faded and cannot be selected, and a click selects one member at a time. Delete, move or change members as usual. **Remove from group** takes the selected members out. **Done**, or <kbd>Esc</kbd>, closes the group. A group goes when its last member is deleted.

![A group open for editing: the garden wall outside it faded](shots/group-edit.webp)

## Imported files

Every IFC, Fragments or mesh file you import comes in as one group named after the file. Untick **As one group** in the IFC import to bring the elements in loose. A linked file is not grouped: it is already one link, read-only (see [Import and export](import-export.md)). Elements read from an IFC file as references stay where the file put them: a group of them can be cleared up and deleted, not moved.

## Selecting a region in 3D

**View → Select region** (<kbd>SR</kbd>) turns a left drag in the 3D view into a selection, as in plans. Dragged to the right, a window takes what lies wholly inside it. Dragged to the left, a crossing takes what it touches. Press <kbd>Space</kbd> while dragging to draw a lasso, and <kbd>Shift</kbd> to add to the selection. While it is on, the right button turns the view and the middle one moves it. The **×** on its chip, or <kbd>SR</kbd> again, turns it off.

![A crossing dragged over the house in 3D](shots/select-region-3d.webp)

## Hiding for now

**View → Hide** (<kbd>HH</kbd>) hides the selection in the 3D view. **Isolate** (<kbd>HI</kbd>) hides everything else. **Show all** (<kbd>HR</kbd>), or the **×** on the chip, brings everything back. Nothing is saved or deleted: it is a way to reach what lies inside an imported model while you clear it up.
