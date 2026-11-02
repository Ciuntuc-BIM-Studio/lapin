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

## The Timber schedule

**Wood → Timber schedule** makes a cutting list of every piece of timber in the project, the walls' members and the roofs' framing: member, section, grade, length, pieces, total length and volume, with the totals by section. In IFC each member is an IfcMember (STUD, PLATE, a header or sill by name) under its wall, and the insulation an IfcCovering INSULATION.
