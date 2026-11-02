---
title: Wood
order: 54
summary: Timber-frame walls from the configurator: studs on a module, plates, noggins, and every door and window framed by its rules.
---

## Timber-frame walls

A wall type can be built as a timber frame: **Wood → Timber frame** opens the configurator, with the project's wall types on the left. Tick **Timber frame** and the type's core is built of members instead of one layer: the plates, the studs on their module, the noggin rows, and round every door and window the king studs, the jack studs, the header and the sill. The other layers (cladding, sheathing, boards) stay as they are, and the insulation fills the cavities between the members.

![The timber frame configurator](shots/wood-frame.webp)

- **Presets**: **Nordic** (45 × 145 at 600 mm, C24) or **Romanian** (50 × 150 at 625 mm, C24, a noggin row at 1.25 m). Every field can then be changed.
- **Studs, spacing, grade, plates, end studs, noggin rows**. **Module from** sets where the studs' module starts: each wall's start, or the project's origin, so the walls of a facade share it.
- **Load-bearing**: a partition carries nothing and takes a flat member over its openings instead of a header.

## Doors and windows

The doors and windows stay the architect's: move one, or change its type, and its framing follows. The frame is measured from the **rough opening**: the door or window plus the fitting gaps (at the sides, on top, under a window; a door goes down to the floor).

Each opening takes the rule of its **width band** (the table under **Openings**: up to 900 mm, up to 1800 mm, beyond, by default): its king and jack studs a side, its header (none, solid, LVL or glulam, its section and plies) and its sill plies. Studs never stand in a rough opening; the short ones above the header and under the sill (cripples) keep the module, so the boards' joints still land on studs. Two openings closer than the **narrowest pier** share one header and their outer king studs. An opening too close to the wall's end is reported in the wall's properties, never moved.

Select a door or window: its **Wood** section shows its rough opening, the rule it takes and what it gets. Tick **Own rule** to give that opening alone other king or jack studs, another header or sill.

## Corners, boards and the frame elevation

- **Corners**: where another wall stops against this one's end (a butt corner), the wall that runs through takes **three studs** (one past the other wall's inside face), a **California** corner (a stud laid flat on the inside face, for the lining) or its **end studs only**. A mitred corner keeps both walls' end studs.
- **T junctions**: where a wall stops against this one in its run, this one takes **a stud each side** of it, or **studs and ladder blocking** between them.
- **Boards**: tick a layer of the build-up (OSB, fibreboard, plasterboard) and give its sheet size: the layer is laid as upright sheets, their joints on the studs' module, cut at the rough openings and along the top. **Wood → Boards schedule** counts the sheets each wall takes, their net area and what is cut off.
- **Frame elevation**: in a framed wall's **Wood** section, the button makes a section along the wall through the middle of its core, at 1:20, every member cut, with a chain of dimensions through the rough openings, the studs' module under it and each rough opening's size. Place it on a sheet like any view.

## CLT

In the same configurator a wall or floor type can be built **in CLT panels**: give the plies (each one's thickness, 3, 5, 7 or 9 of them), the grade, the longest and the highest (or widest) panel the press makes, the joint between panels (butt, half lap, spline), how far an opening keeps from a panel's edge and its fitting gap. A wall is cut into panels along its run, the joints moved clear of the openings; each panel is the core's own body, its rough openings cut out. A floor is cut into panels across its span (the short way, or along x or y). **Wood → CLT panels** lists every panel with its mark, size, plies, grade, joint, area and volume; in IFC each one is an IfcBuildingElementPart named CLT panel.

## The Timber schedule

**Wood → Timber schedule** makes a cutting list of every piece of timber in the project, the walls' members and the roofs' framing: member, section, grade, length, pieces, total length and volume, with the totals by section. In IFC each member is an IfcMember (STUD, PLATE, a header or sill by name) under its wall, and the insulation an IfcCovering INSULATION.
