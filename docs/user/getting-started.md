---
title: Getting started
order: 10
summary: The start screen, a new project, the parts of the window, moving around a drawing and choosing your units.
---

## The start screen

When Lapin opens, it shows the start screen. **New project** starts an empty building. **Open project…** opens a `.lapin` file. The projects you have opened before are listed under **Recent**.

![The start screen](shots/start-screen.webp)

A new project already has three levels: **Foundations** (−1.50), **Ground floor** (±0.00) and **First floor** (+3.00). It opens on the ground floor's plan, ready for the first wall.

## The window

![The parts of Lapin's window](shots/workspace.webp)

1. **The ribbon tabs.** Draft, Architecture, Finishes, Structure, Site, Studio, Annotate, Manage, Quantities and View each group their tools. **File** opens, saves, imports and exports. **Undo** and **Redo** are at the right end of the same bar (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **The ribbon.** It holds the current tab's tools. Hover over a tool to see its name and its keyboard letters.
3. **The project browser.** It lists the levels, plans, sections, elevations, 3D views, sheets and schedules. Double-click one to open it in a tab. A project opens on the ground floor's plan, with the 3D view in a tab beside it.
4. **The view.** This is where you draw: a plan, a section, a 3D view or a sheet. You can open several views side by side with the two buttons at the top right of the view.
5. **The hotbar.** It gives the most used tools on the number keys <kbd>1</kbd>–<kbd>9</kbd>.
6. **The command line.** Type a tool's letters here and press <kbd>Enter</kbd>. While a tool runs, the line tells you what it needs next. See [Drafting and the command line](drafting.md).
7. **The properties panel.** It shows whatever is selected. With nothing selected, it shows the view in front.
8. **Ask Me.** This is Lapin's help assistant. See [Help and feedback](help.md).

Want more room for the drawing? **View → Interface…** chooses a more compact window, and the side panels fold away. See [The interface and shortcuts](interface.md).

## Moving around

- **Zoom.** Turn the mouse wheel or pinch on a trackpad. Lapin zooms where the cursor is.
- **Pan.** Drag with the middle or the right mouse button, or with two fingers on a trackpad.
- **Orbit in 3D.** Drag with the left mouse button.
- **Show everything.** Press the **Fit** button, type <kbd>ZE</kbd>, or double-click the middle button.

## Units

Lapin works in millimetres unless you choose otherwise. To change the units, open **Manage → Drafting standards → Project → Units**, or click **Units** in the status bar at the bottom of the window. You can choose millimetres, centimetres, metres, feet and inches, or inches.

![Drafting standards, Project tab: the project's units](shots/standards-project.webp)

Every length follows this setting: the panels, the values you type while drawing, the command line and the dimensions. The model itself is kept in metres, so changing the units never changes your building.

## Saving

**File → Save** (<kbd>Ctrl</kbd> <kbd>S</kbd>) writes a `.lapin` file. See [Files, versions and recovery](files.md).
