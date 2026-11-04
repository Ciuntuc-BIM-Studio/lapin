---
title: MEP systems
order: 57
summary: Draw heating, water, drainage, electrical, ventilation, gas and fuel oil systems as schematics on the plan, with sources, endpoints and the runs between them, the gas supply in the street, and export them as an IFC network.
---

The **MEP** tab draws the building's services the way an engineer sketches them: a system per service, its **source** (a boiler, a water meter, a stack, a consumer unit, a fan), its **endpoints** (radiators, basins, WCs, outlets, lights, diffusers) and the **runs** between them. Every node is a real IFC element with its ports; every run is a pipe, duct or cable segment; the connections hold when you move things.

![The MEP tab](shots/ribbon-mep.webp)

## A system in one click

Under **New system**, choose the service (**Heating**, **Water**, **Hot water**, **Drainage**, **Electrical**, **Ventilation**) and click in the plan. Lapin places the whole system: the source where you clicked, a first endpoint a few metres away, and the run between them. The system opens for editing.

![A heating system on the ground floor plan: the boiler, two radiators under the windows and the runs between them](shots/mep-plan.webp)

In the edit mode everything that is not the system is faded, and only the system's nodes and runs can be picked. Drag the source and the endpoint where they go: their run follows. Add endpoints with **+ Radiator**, **+ Wash basin**… in the properties panel, or with the tools under **Heating**, **Plumbing**, **Electrical** and **Air**: each new endpoint joins the system by a run of its own, from the nearest free port of the source, or from the nearest run (a tee). **Done** (or <kbd>Esc</kbd>) leaves the edit mode and tells you how many bends and tees the system makes and what is still open. **Edit system** (<kbd>ES</kbd>) opens it again.

## Runs

**Run** (<kbd>RN</kbd>) draws a run point by point; press <kbd>Enter</kbd> to finish. An end that lands on an open port connects to it; an end that lands on a run of the same system branches off it.

**Connect** joins a run's end to a node or to another run; **Disconnect** frees it. **Open ports** selects everything still unconnected.

## Reshaping a run

Select a run: its handles sit on the line as it is drawn, even after you move the nodes it connects.

- **Drag a handle** to move that corner freely.
- **Drag the middle of a leg** to slide the leg sideways; the legs next to it stretch, so a square route stays square. Slide the first or the last leg and Lapin adds a jog there, so the end stays on its port.
- **Ctrl-click** a handle to take it out, or the middle of a leg to add one; **Add handle** puts one where you click.
- **Detour** (<kbd>DU</kbd>) takes a run round something in the way: click the run where the detour starts, click where it ends on the same leg, then move to the side it goes and click (or type the distance). The run goes out, along and back, with four new corners; it keeps its connections.
- **Redraw path** (<kbd>RP</kbd>) gives a run a new route: select it (or start on it) and draw the path point by point, <kbd>Enter</kbd> to finish. The run keeps its system, its size and its connections, and its ends go back onto their ports.

![A radiator run taken round an obstacle with Detour: out, along and back](shots/mep-detour.webp)


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

## Gas and fuel oil

**Gas** and **Fuel oil** are services of their own under **New system**. A gas system says its gas (**Natural gas** or **LPG**) and its **Pressure class** (low inside the house, medium in the street) in **Systems…**; its pipes default to black steel on the walls at 2.1 m, and may be copper, multilayer or **PE 100** for what is buried. Fuel oil runs in copper.

A house's gas installation starts at the **meter box** on the facade. Click **New system → Gas**: the meter box goes where you click and a **gas boiler** inside, joined by a run. On the way, the tools under **Gas** drop the **gas regulator**, the **gas meter**, the **main gas shut-off valve**, the **appliance gas valves** and **flexible connectors** on the run, which they split as any fitting does; the **gas cooker** and the **gas water heater** join the installation by a run of their own. Where a gas or fuel oil pipe crosses a wall it goes through a **sleeve**, drawn and counted with the run. A gas boiler drawn in the heating system as its source joins the gas installation from its properties (the **+** button with the gas system's name).

![A house's gas installation: the meter box, the regulator and the meter outside, the boiler and the cooker in the kitchen](shots/mep-gas-plan.webp)

Each appliance keeps its **consumption** (m³/h) and its **appliance type** by its flue (A without a flue, B open flue, C room-sealed), for the lists and the sizing.

**Fuel oil** places a **fuel oil tank** (indoor, outdoor or buried) and an **oil boiler**; the **fill point** and the **vent cap** join the tank's own fill and vent, and the **oil filter** sits on the supply.

### The supply in the street

Under **Street**, **Gas main along…** (<kbd>GM</kbd>) lays a gas main along what you click — a road, an alignment, a drafted line, a wall — at the **offset** from it and the **depth (cover)** set in the command line's bar; under a terrain the depth is taken from the ground, else from the level. **Gas main** draws one point by point. The main goes in a medium-pressure gas system of PE pipe, with its **warning tape**.

**Service connection** (<kbd>GS</kbd>): click the main, then the meter box. Lapin puts a **tapping tee** on the main, lays the service pipe square off it at its cover and on to the box, and rises into the box there.

![The service connection from the street's main to the meter box, selected](shots/mep-gas-street.webp)

![The main and the service connection under the ground, the installation on the facade](shots/mep-gas-3d.webp)

![A service connection's properties: from the tapping tee to the meter box, its cover and size](shots/mep-gas-service-properties.webp)

![A gas system's defaults: its gas and its pressure class](shots/mep-gas-systems.webp)

**Gas and fuel oil bill of materials** under **System** lists, system by system, the pipes by material and size with their length (and of it the risers), the bends, tees and wall sleeves, the warning tape, the valves, meters and boxes, and the appliances with their installed consumption and heat output. In IFC a gas system is an IfcDistributionSystem of type GAS and a fuel oil one FUEL; the meter box is an IfcDistributionChamberElement, the meter an IfcFlowMeter GASMETER, the valves IfcValve, the cooker an IfcBurner, the tank an IfcTank.

![The gas and fuel oil bill of materials](shots/mep-gas-list.webp)

Sizing the pipes from the appliances' consumption and checking the pressure drop are engineering, in the **Lapin MEP** edition.

## MEP Pro

The engineering on the network — the demand each run carries and the size that takes it, the checks (open ends, tight bends, drainage falls, overloaded sources), the laying rules by material (copper at 45° steps, PVC turning 45° at most), radiators proposed under windows, routes suggested around the walls, a run's profile and a system's isometric — is in the **Lapin MEP** edition, which adds the **Engineering** and **Assist** groups to this tab.
