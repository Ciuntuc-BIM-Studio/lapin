---
title: MEP systems
order: 57
summary: Draw heating, water, drainage, electrical and ventilation systems as schematics on the plan, with sources, endpoints and the runs between them, and export them as an IFC network.
---

The **MEP** tab draws the building's services the way an engineer sketches them: a system per service, its **source** (a boiler, a water meter, a stack, a consumer unit, a fan), its **endpoints** (radiators, basins, WCs, outlets, lights, diffusers) and the **runs** between them. Every node is a real IFC element with its ports; every run is a pipe, duct or cable segment; the connections hold when you move things.

![The MEP tab](shots/ribbon-mep.webp)

## A system in one click

Under **New system**, choose the service (**Heating**, **Water**, **Hot water**, **Drainage**, **Electrical**, **Ventilation**) and click in the plan. Lapin places the whole system: the source where you clicked, a first endpoint a few metres away, and the run between them. The system opens for editing.

![A heating system on the ground floor plan: the boiler, two radiators under the windows and the runs between them](shots/mep-plan.webp)

In the edit mode everything that is not the system is faded, and only the system's nodes and runs can be picked. Drag the source and the endpoint where they go: their run follows. Add endpoints with **+ Radiator**, **+ Wash basin**… in the properties panel, or with the tools under **Heating**, **Plumbing**, **Electrical** and **Air**: each new endpoint joins the system by a run of its own, from the nearest free port of the source, or from the nearest run (a tee). **Done** (or <kbd>Esc</kbd>) leaves the edit mode and tells you how many bends and tees the system makes and what is still open. **Edit system** (<kbd>ES</kbd>) opens it again.

## Runs

**Run** (<kbd>RN</kbd>) draws a run point by point; press <kbd>Enter</kbd> to finish. An end that lands on an open port connects to it; an end that lands on a run of the same system branches off it. Select a run to shape it: drag a handle, drag the middle of a leg to slide it, Ctrl-click a handle to take it out or the middle of a leg to add one; **Add handle** puts one where you click.

**Connect** joins a run's end to a node or to another run; **Disconnect** frees it. **Open ports** selects everything still unconnected.

## Nodes

Each node has a **role**: a boiler, a radiator, a manifold, a water meter, a basin, a sink, a WC, a shower, a bath, a stack, a cleanout, a consumer unit, an outlet, a light, a switch, a fan, a diffuser, an extract grille, a valve, a rise/drop, a cap. The role gives it its IFC class, its ports, its size and its symbol on the plan. A valve, a meter or a switch dropped on a run splits it and sits in the middle. A **rise/drop** takes a run to another elevation or to the storey above.

![A radiator's properties: its system, its role and ports, its size and duty](shots/mep-radiator-properties.webp)

In the properties panel a node shows its system, its role and its ports (open or connected); a run shows its two ends. Both take the system's defaults unless you set a value on them: size, elevation, the plane they run in (floor, ceiling, wall), paired or single, material, slope.

## Systems

**Systems…** lists the systems of the project and the defaults of the current one: the size, the plane and the elevations of each plane, the slope (drainage), whether runs are drawn as a pair (heating: supply and return) and how far apart, the material and the insulation. Change a default and every node and run without a value of its own follows.

![The Systems dialog](shots/mep-systems.webp)

## Plans, lists and IFC

**MEP plan** makes a plan of the MEP discipline for the level, filed under the MEP set in the browser. The **Equipment list** and the **Run list** under **System** are schedules like any other: they go on sheets and export with them.

In an IFC export each system is an IfcDistributionSystem, every node and run has its IfcDistributionPorts, and the connections are written between the ports, so the network arrives in another program connected from the source to the last endpoint.

## MEP Pro

The engineering on the network — the demand each run carries and the size that takes it, the checks (open ends, tight bends, drainage falls, overloaded sources), the laying rules by material (copper at 45° steps, PVC turning 45° at most), radiators proposed under windows, routes suggested around the walls, a run's profile and a system's isometric — is in the **Lapin MEP** edition, which adds the **Engineering** and **Assist** groups to this tab.
