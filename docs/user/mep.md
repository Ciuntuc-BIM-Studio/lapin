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

## Coordination: opening voids and clash checks

Every opening in a wall, floor or roof can be a **void** of its own: a solid as large as the opening, through the whole thickness of its host, finishes included. Clash programs (Navisworks, Solibri, BIMcollab) test MEP against such voids with "completely inside": a pipe, duct or cable tray wholly inside a void passes, one that sticks out of it, or crosses a wall where there is none, is a clash. Lapin runs the same check itself.

**Manage → Clash check…** runs the check only when asked: nothing runs it by itself, neither an edit nor **Manage → Check**. Its **Settings** tab sets what the voids are made for (wall, door, window, floor and roof openings), how much bigger or smaller than their openings they are (**Void offset**), and the **Clearance** an MEP element must keep from a void's sides. **Run clash check** goes once through every pipe, duct and cable carrier — Lapin's runs and the segments of an imported MEP model alike, and an MEP model's own provisions for voids — and keeps what it found as the project's **clash report**, with the date it ran, until it is run again. When the model changes after that, the report says it is **out of date**. Its problems are grouped by kind and numbered, each number kept from one run to the next:

- **No opening**: it goes through the host where there is none;
- **Not cut: load-bearing or cut order**: no opening, because the runs' own openings (below) do not cut that host;
- **Opening too small**: it goes through an opening but stands out of it, the clearance included; **Short by** says by how many millimetres on the worst side;
- **Empty void** (with **Empty voids** ticked in **Settings**): a wall or floor opening nothing goes through (a stairwell is never one).

![The clash report: no opening, an opening too small and an empty void, each numbered](shots/mep-voids-check.webp)

Each problem is **To fix**, **Resolved** or **Ignored**, with a comment. A click on a row shows the pair in 3D, both selected (**alone in 3D** hides the rest for now); the arrows go to the previous and the next one. **Mark in view** (also **View → Clash marks**) draws each open problem in the plan, section or elevation in front as a red cloud with its number, on the screen only.

![The plan with the clashes marked: red clouds with their numbers](shots/mep-voids-clouds.webp)

**Make opening** cuts a bare opening round the element in the wall (round for a pipe or a cable, rectangular for a duct) or in the floor, its sides the clearance away and its size rounded up as the cut order's MEP rule says; **Enlarge opening** grows the opening that is too small. **Fix selected** does it for every ticked row, **Ignore** and **Reopen** set their status; each is one step to undo. The report then says it is out of date: run it again, and what is fixed is listed **Resolved** (**show resolved**). Its line then says at which run it was found resolved and how it is now: the opening it goes through and the run's size in it (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), or that the run no longer goes through; each run after that updates the sizes.

![Run again after the fixes: the two clashes resolved](shots/mep-voids-fixed.webp)

**BCF** turns the open clashes into remarks of type **Clash** with their viewpoints (**Manage → Model remarks…**) and saves them as a BCF file; run again, it updates the same remarks. **CSV** and **PDF** save the report as a list. **Manage → Check** only reads the report: its open clashes are warnings, and so is a report out of date.

### Runs that cut their own openings

The cut order (**Manage → Cut order**) holds MEP runs too, over walls, floors and roofs. With **Runs cut walls, floors and roofs** on, a run that crosses a wall, a floor or a roof cuts its own opening there: round for a pipe or a cable going square through, rectangular for a duct or a run at a slant, as large as the run's outside (its insulation, a gas pipe's sleeve) and the **Clearance** round it, rounded up to the step, never under the **Smallest opening**. The opening is not kept apart: it moves and grows with the run, and goes when the run goes or no longer crosses. It is a void like any opening (drawn, listed, checked), and the IFC export writes it as an **IfcOpeningElement** with **Lapin_ProvisionOpening** (and as a provision for void with **Opening voids** ticked), so a check run again finds nothing to report. A wall, floor or roof that ranks over the runs in the cut order (by its class or its material) is not cut, nor a load-bearing one (LoadBearing in its Pset_…Common) unless **cut them too** is ticked: the clash check reports them as **Not cut**. A demolished run cuts nothing, and where a run already passes through an opening none is cut. A system can say otherwise in **MEP → Systems…** (**Runs cut walls, floors and roofs**: as the cut order, yes, no). It is on in a new project; a project made before keeps it off until it is switched on.

In a curved wall the opening is found where the run meets the arc, its width measured along it; a rectangular one has radial sides, as a door's or a window's. In a roof it is cut in the slope the run passes through: upright for a pipe standing up (a vent), drawn out along its travel for one at a slant, as in a floor; the purlins and the ridge beam under the slope are not cut, and the clash check reports a run through them. Moving or resizing a run builds again only the walls, floors and roofs it crossed or crosses now, however large the model. Lapin's own IFC read back does not take these openings again when their runs come too (the runs cut them again, never twice); without their run (left out of the import, or a project whose runs cut nothing) each comes in as an opening of its host, so nothing is lost.

![MEP runs in the cut order](shots/mep-voids-cut-order.webp)

The **Voids** tab lists every void with its mark (V1, V2… storey by storey), its host, the door or window in it, its size and depth; a click selects its opening.

![The voids listed with their marks and sizes](shots/mep-voids-list.webp)

**View → Opening voids** with the 3D view in front draws them in 3D, translucent violet with their marks; a click on one selects its opening.

![The voids in the inner wall and the floor, with the pipes through them](shots/mep-voids-3d.webp)

With a plan, section or elevation in front, **View → Opening voids** draws them in that view instead, as a coordination drawing shows provisions for voids: cut where the view cuts them (a wall's opening through the wall's thickness, a floor's through the slab), seen where it sees them (a floor opening below the plan's cut, a wall's opening on the face an elevation looks at), dashed when a wall's opening lies under the cut or a floor's above it; each crossed corner to corner, with its mark and, under it, its size and a wall opening's bottom level (**400×200 +0.90**, **Ø200**). The engine draws them with the rest of the drawing, so sheets, the PDF and DXF (layer **VOIDS**) print them as the screen shows them.

![The plan with the voids: cut through the walls, the floor opening seen, each crossed and marked](shots/mep-voids-plan.webp)

A void's label stands beside it across its wall, clear of what is already drawn there: a door's swing, the door's or window's marker, other labels. Where both sides of the wall are taken it moves along the wall past the opening, else further out.

![A door's void: its label clear of the swing and of the door's marker](shots/void-label.webp)

To move a label, click it (or the void): that selects the opening, whose label shows a square grip to move it and a round one to turn it. Ctrl/⌘-click a grip to put the label back where it stands by default, or level again. A moved label is kept by that view, along and across the wall from the void's middle, so it follows the opening when the wall moves; sheets and PDF print it where you put it.

![The void's label moved by its grip](shots/void-label-moved.webp)

It is a setting of the view, also in its properties (**View → View settings**): **Opening voids**, and **Void sizes** for the size under the mark. Views draw none by default; the **Coordination** view style (**Manage → View styles**, chosen as the view's **Style**) draws them in every view that takes it. Their lines are the **Voids** category: their pens and colours in **Manage → Object styles** (cut, projection, hidden, overhead lines), shown or hidden per view in **View → Visibility / Graphics…**.

To hand the voids to a clash program, tick **Opening voids** in **File → Export IFC or Fragments…** (or **Export 3D…**). In IFC each void is an **IfcBuildingElementProxy** of type **PROVISIONFORVOID** with **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter for a round one, Depth) and Lapin's **Lapin_OpeningVoid** (its mark, kind, host, opening and offset), related to its host; the openings themselves stay IfcOpeningElements as always. Each void keeps its GlobalId from one export to the next, so the clash program follows it. In a mesh export they are a group of their own, **Opening voids**. Without the box ticked, the exports are as they were.

## MEP Pro

The engineering on the network — the demand each run carries and the size that takes it, the checks (open ends, tight bends, drainage falls, overloaded sources), the laying rules by material (copper at 45° steps, PVC turning 45° at most), radiators proposed under windows, routes suggested around the walls, a run's profile and a system's isometric — is in the **Lapin MEP** edition, which adds the **Engineering** and **Assist** groups to this tab.
