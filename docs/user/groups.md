---
title: Groups and clearing up
order: 112
summary: Keep elements together as a group, open a group to edit it, select by a region or a lasso in 3D, and hide what is in the way.
---

## Groups

A group keeps elements together. Select them and click **Modify → Create group** (<kbd>GP</kbd>). A click on any member then selects the whole group, in plans and in 3D. The group moves, copies, rotates, mirrors and deletes as one, and a copy is a group of its own. In IFC a group is an IfcGroup, so other programs see it too.

The right-click menu on a plan, a section or an elevation has the same commands: **Create group** for the selection, and on a group **Ungroup** and **Edit group**. Lines, texts and hatches drawn on a view group too, an imported DXF plan's lines among them, so a whole drawing moves as one.

![Lines drawn on the plan, grouped from the right-click menu](shots/group-drafting.webp)

A selected group's properties give the number of elements it holds, with three buttons: **Edit group**, **Ungroup** and **Select members**.

![A group selected: its members lit, its properties](shots/group-selected.webp)

## Editing a group

Double-click a member, or click **Edit group** (<kbd>EG</kbd>). The rest of the model is drawn faded and cannot be selected, and a click selects one member at a time. Delete, move or change members as usual. **Remove from group** takes the selected members out. **Done**, or <kbd>Esc</kbd>, closes the group. A group goes when its last member is deleted.

![A group open for editing: the garden wall outside it faded](shots/group-edit.webp)

## Components

A component is a group whose copies stay the same, as in SketchUp: change one and all of them change. It suits what you repeat many times: a planter, a bay of a canopy, a kitchen island, a roof light, a typical room's floor. It takes **Studio solids, floors and rooms**; walls cannot be part of one, since a wall's joins and doors depend on its neighbours.

Select the elements and choose **Modify → Make component** (<kbd>CK</kbd>). Then copy it as you copy anything: **Copy**, **Rotate**, **Copy to storeys**. Each copy is an **instance**.

- **Double-click** an instance to open it, as a group. What you change inside (an outline, a height, a push/pull, a new or deleted member) changes in every instance, in one undo step.
- **Move and turn** an instance on its own: the others stay where they are, and an edit reaches a turned instance turned.
- **Make unique**, in its properties, lets one instance go its own way. **Select instances** selects them all.
- **Mirror** asks you to make the instance unique first.

**Manage → Components** lists the project's components, with how many instances each has. Rename them there, or select all the instances of one. The instances are ordinary elements in plans, sections, schedules, quantities and exports, each with its own GlobalId.

![A component in four instances, one turned: the outline changed in one, followed by all](shots/components-3d.webp)

![An instance's properties: the component, Select instances, Make unique](shots/component-properties.webp)

## Drilling into assemblies

Some elements are parts of a whole: a steel hall's columns, rafters and purlins, the parts of an assembly (a Studio box, an assembly read from an IFC file), and the members of a group. **View → Drill into assemblies**, off at first, picks them the way the whole is built. The first click on a part selects the whole at the top: the group, or the hall. While it is selected, a click inside it selects the part one level down on the way to what you clicked, and each further click goes one level deeper, down to the element itself. A click on another part of the same whole selects that part at the same level. <kbd>Esc</kbd> steps one level up, and at the top lets go of the selection; a click on empty space lets go too. <kbd>Ctrl</kbd>+click (<kbd>Cmd</kbd>+click on a Mac) selects the element itself straight away, and <kbd>Shift</kbd> adds the whole at the top to the selection. A breadcrumb over the view names the levels, from the storey to the selection: click one to select it. While a part inside a whole is selected, the rest of the model is drawn lightly faded. It works the same in 3D, plans, sections and elevations. A selection box or a lasso takes the wholes at the top. A double-click still opens a group for editing; inside it, the drilling starts below the group. The choice is kept on this computer, and is also in **View → View settings**.

![Drilled into a steel hall: the rafter selected, the breadcrumb over the 3D](shots/drill-select.webp)

## Imported files

Every IFC, Fragments or mesh file you import comes in as one group named after the file. Untick **As one group** in the IFC import to bring the elements in loose. A linked file is not grouped: it is already one link, read-only (see [Import and export](import-export.md)). Elements read from an IFC file as references stay where the file put them: a group of them can be cleared up and deleted, not moved.

## Selecting a region in 3D

**View → Select region** (<kbd>SR</kbd>) turns a left drag in the 3D view into a selection, as in plans. Dragged to the right, a window takes what lies wholly inside it. Dragged to the left, a crossing takes what it touches. Press <kbd>Space</kbd> while dragging to draw a lasso, and <kbd>Shift</kbd> to add to the selection. While it is on, the right button turns the view and the middle one moves it. The **×** on its chip, or <kbd>SR</kbd> again, turns it off.

![A crossing dragged over the house in 3D](shots/select-region-3d.webp)

## Hiding for now

**View → Hide** (<kbd>HH</kbd>) hides the selection in the 3D view. **Isolate** (<kbd>HI</kbd>) hides everything else. **Show all** (<kbd>HR</kbd>), or the **×** on the chip, brings everything back. Nothing is saved or deleted: it is a way to reach what lies inside an imported model while you clear it up.
