---
title: Elevators
order: 43
summary: Elevators from a configurator of twelve kinds sized after EN 81-20 and EN 81-70, with their shaft, pit, headroom, machine room, car and landing doors, the openings in the floors they pass through, plans on every storey, a schedule and IFC.
---

## Placing an elevator

**Architecture → Elevator** (<kbd>LF</kbd>) puts an elevator down with a click in the plan. The click is the middle of its shaft, and its doors face down the plan (turn it with the bar's 0°, 90°, 180° or 270°, or later with its grip). The elevator runs from the plan's storey to the project's highest storey and stops on every storey between them. Its configurator opens at once.

While the tool is on, the command line's bar and the properties panel show **the style** it puts down: one of the twelve kinds of the catalogue, or one of the project's own styles.

## The configurator

![The elevator configurator, with the wheelchair-accessible style](shots/elevator-configurator.webp)

The styles are on the left. A style fills every field in, and you can then change any field by hand. The catalogue has these kinds:

| Style | Persons | Rated load | Car (mm) | Drive |
|---|---|---|---|---|
| Small residential | 4 | 320 kg | 900 × 1000 | machine-room-less |
| Standard passenger | 6 | 450 kg | 1000 × 1250 | machine-room-less |
| Medium passenger | 8 | 630 kg | 1100 × 1400 | traction, machine room on top |
| Large passenger | 10 | 800 kg | 1350 × 1400 | traction |
| Heavy-duty passenger | 13 | 1000 kg | 1600 × 1400 | traction, 1.6 m/s |
| Wheelchair-accessible | 8 | 630 kg | 1100 × 1400, door 900 | EN 81-70 type 2 |
| Hospital bed lift | 17 | 1275 kg | 1200 × 2300 | traction, through car |
| Goods lift | 26 | 2000 kg | 1500 × 2700 | traction, 0.5 m/s |
| Hydraulic (low-rise) | 8 | 630 kg | 1100 × 1400 | jack, power unit room beside the shaft |
| Machine-room-less (MRL) | 13 | 1000 kg | 1100 × 2100 | machine in the headroom |
| Panoramic glass | 8 | 630 kg | 1100 × 1400 | glass car in a glass shaft |
| Home lift | 3 | 250 kg | 800 × 950 | 0.15 m/s, shallow pit |

On the right, three tabs:

- **Car and load**: the drive (traction, machine-room-less, hydraulic, home lift), the rated load and persons, the speed, the accessible type (EN 81-70 type 1, 2 or 3), the car's width, depth and height, glass car walls, a firefighters lift, an evacuation lift. **Load and persons from the car** sets the least rated load EN 81-20 gives the car's area, and the persons it carries.
- **Doors and stops**: one entrance or two (a through car), the door's width and height, centre or side opening and its panels (two or three panels slide one behind the other), the highest stop and the storeys it stops on. A storey it passes without stopping keeps the shaft wall closed and has no landing door.
- **Shaft**: the side of the counterweight (or of a hydraulic lift's jack), the pit under the lowest stop and the headroom over the highest (both follow the drive and the speed until you type your own), the machine room (on top for traction, beside the shaft at the bottom for a hydraulic lift, none otherwise), the shaft's walls (a wall type of the project, or concrete of a thickness you give), a glass shaft, the rotation.

![The section through the car, from the pit to the machine room](shots/elevator-section.webp)

The preview shows the plan at the lowest stop and, on the other tabs, a section through the car with every stop (served stops in red, those it passes dashed). Under it, what EN 81 says: a car larger than its rated load allows, more persons than the load or the area carries, an accessible car or door too small, a hydraulic lift travelling more than about 18 m. Below are the shaft's clear and outer sizes, the car's area against what its load allows, the stops, the travel and the floors the shaft opens.

**Apply** changes the elevator in one undo step. **Save as style…** keeps it as a style of the project; **Update style** saves it again and every elevator of that style follows. A project style can be renamed or deleted (its elevators keep how they are).

## In the drawings

![An elevator in plan: the shaft cut, the car with its cross, the counterweight and the landing doors](shots/elevator-plan.webp)

The shaft opens every floor it passes through, with an opening of its own in each floor. When you move, turn or copy the elevator, or change its storeys, the openings follow. A floor drawn after the elevator: **Architecture → Shaft openings** opens it.

Every plan of a storey the elevator reaches draws it: the shaft walls cut, the car with its diagonal cross, the counterweight with its diagonal (a hydraulic lift's jack as a circle), and on the storeys it stops on the landing door's and the car door's panels, dashed where they slide when open. Sections and elevations draw the whole shaft, the pit, the cap, the machine room, the car at the lowest stop and the landing doors. In 3D the car stands at the lowest stop with its doors closed.

![An elevator in 3D, its machine room over the house](shots/elevator-3d.webp)

## Its properties, schedule and IFC

![An elevator's properties](shots/elevator-properties.webp)

Select an elevator to change its lowest and highest stop, tick the storeys it stops on, choose its shaft's wall type, read EN 81's warnings, and open **Configurator…**. Its fields (the drive, load, car, doors, pit, headroom…) are below; those that do not apply hide (no machine room for a machine-room-less lift, no counterweight side for a hydraulic one). Drag its middle grip to move it and the grip in front of its doors to turn it. **Move**, **Copy**, **Rotate** and **Mirror** work as for any element; a mirrored elevator takes its counterweight to the other side.

**Architecture → Elevator schedule** lists each elevator with its kind, drive, rated load, persons, car, doors, the stops it serves, the storeys it runs between, its travel, speed and shaft.

Each elevator has a type made from its kind, load and car (Medium passenger 630 kg, 1100 × 1400): elevators alike share it, so a price article put on the type (**Cost → Map articles…**) prices them all, and planning finds them by their class (IfcTransportElement).

In IFC an elevator is an **IfcTransportElement** with the predefined type **ELEVATOR**, of an **IfcTransportElementType**, with IFC's own **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) and **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin reads its own file back as a native elevator, with its stops and the floors' openings.
