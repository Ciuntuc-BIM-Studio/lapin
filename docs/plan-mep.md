# MEP as a system schematic: plan

Asked on 26.10.2026 (pending decision 133), after the analysis of the old Lapin's branch (`docs/mep-analiza-lapin-vechi.md`). The user's workflow, in their words: a system at the level of the building; the drawing is the logical MEP schematic of that system, with configurable nodes, from the source to the endpoints; values default, overridable per node or for the whole system; in the first phase the user draws the route and takes the point decisions (a symbol where a pipe rises or drops, by the 2D standard, each symbol mapped to a 3D object); engineering first, not architecture; later, nodes mapped to elements from imported libraries (sinks, radiators, baths). The module is `mep`, premium for now (M9).

## The model

Everything the old branch kept as JSON inside an `IfcElementAssembly` becomes entities and relationships of the model, as the rest of Lapin:

- **System** (`IfcDistributionSystem`, one per building and service: heating, cold water, hot water, drainage, ventilation supply, ventilation extract, electrical power): the **defaults** every node and run starts from — sizes by role, material, insulation, the elevation of runs on a storey (floor 0.2 m, ceiling 2.4 m, wall 1.1 m), the slope (drainage), the separation of a pair (heating, cold/hot), the symbol set.
- **Node** (an element of the system, contained in a storey, placed in plan): its **role** decides what it is in 3D and in IFC — source (boiler `IfcBoiler`, manifold `IfcPipeFitting/JUNCTION`, consumer unit, fan, water meter…), **endpoint** (radiator `IfcSpaceHeater`, fixture `IfcSanitaryTerminal`, outlet, light, diffuser…), **fitting** on the way (valve, meter, cleanout, reducer, `IfcPipeFitting`/`IfcValve`/`IfcFlowMeter`…), **junction** (a tee, made where a run leaves another), **rise/drop** (a vertical run to another elevation or storey: the riser), **cap** (an end left open on purpose). A node carries its **ports** as data (key, service, direction, position, size), its overrides, and, later, its library representation. Rotation and a mounting height.
- **Run** (`IfcPipeSegment`/`IfcDuctSegment`/`IfcCableSegment`, with its fittings aggregated): a polyline in plan from one node's port to another's, at an elevation (the system's, or its own), with a size, a material, an insulation; drawn orthogonally by default. A **pair** (supply/return, cold/hot) is one run with a separation, drawn once, two pipes generated; the pair can be opened into two single runs where they must diverge.
- **Connections** are `IfcRelConnectsPorts` between the ports of nodes and runs: made when a run is drawn onto a port, and by a Connect tool afterwards; a connection holds when either end moves (the run's last leg follows, orthogonally). What is left open is listed and marked.
- **Generated parts** (pipes, bends, tees, reducers, risers, sleeves, supports) are the run's geometry parts, keyed, so the IFC identities survive a change. Every part gets its ports, so the IFC network is connected from source to endpoint, not only at the drawn ends.

Dependencies as everywhere in the engine: a run reads the walls it crosses (sleeves), the nodes at its ends and its system; any of them changing rebuilds it.

## The drawing: the schematic on the plan

The plan of a storey is where the system is drawn, as its schematic: nodes are standard 2D symbols, runs are lines with the service's line type, and the point decisions are symbols too:

- **Rise and drop** (ISO 4067-1 for pipework, the arrow/ring conventions for ducts; IEC 60617 on the electrical plan): placed on a run, a rise node ends the run on this storey and starts its continuation on the storey or elevation it names; the 3D object is the riser between the two elevations, with its two bends. Drawn as the standard symbol (the ring with the dot or the cross, open or filled by direction), with the elevation it goes to.
- **Valves, meters, cleanouts, reducers**: standard symbols on the line; the 3D object the catalogue gives the role (a box of the nominal size for now).
- **Sources and endpoints**: their engineering symbols (radiator as a rectangle with its ports, basin, WC, boiler, consumer unit, outlet, light, fan, diffuser), sized by the node; the 3D object is a box of the right size with its ports, until a library element is mapped to it.
- **Per-system layers and line types**, the discipline switch per view (as the old plan had), tags with the planning mark, size and elevation; the legend.
- Elevations and sections draw the generated 3D, so a riser shows where it is.

The user **draws the run from the source toward the endpoints**: start on a port (or in the open), click the corners, finish on a port or a node; a run started on another run makes a junction there (mid-span allowed). Nodes are placed first or dropped onto a run afterwards (the run splits).

## Values: defaults and overrides

Three levels, each shown in the properties panel with where the value comes from: the **system** (sizes by role, elevation by plane, slope, materials, separation), the **node** (its own size, height, duty, the elevation its rise goes to), the **run** (size, elevation, material). Changing the system changes everything that does not override it.

## Libraries (later)

A node's role has a default representation (the box with ports). Later, an element from an imported library (an IFC or DXF link: a maker's radiator, sink, bath) is mapped to a node role or to one node: the library body replaces the box, its ports are taken from its `IfcDistributionPort`s when it has them, else placed by the user once per library element. The schematic symbol stays the role's.

## Stages

1. **The network core** (engine, tests): systems with defaults, nodes with ports, runs with pairs, connections that hold on moves, generated parts with ports, rise/drop nodes and risers across storeys, IFC export with systems and connected ports and a round trip. Heating and cold/hot water first; drainage with its slope; ventilation and electrical as roles of the same core.
2. **The schematic on the plan** (UI): the MEP tab, the system panel (defaults), the node tools by role with their symbols, the run tool (orthogonal, snapping to ports and runs), rise/drop placement, the Connect tool, the properties panel with the three levels, open ports listed and marked, the discipline switch and tags.
3. **Engineering** (from the old branch, as services on the core): open terminals, tight bends, the drainage fall check, water demand and bore, duct velocity, the schedules; the radiator proposals and route suggestions last, as assistants on the schematic.
4. **Libraries**: mapping nodes to linked elements.

## Where the module lives

`mep` is premium: its code does not belong in the repo that opens later. Default: a private repo `Ciuntuc-BIM-Studio/lapin-mep` checked out beside this one (as `lapin-plugins` is), holding `engine/src/Modules/Mep/Lapin.Mep` and `ui/src/modules/mep`; `scripts/product.mjs` learns to take a module from a sibling path named in the product (`products/lapin-mep.json`: `"external": { "mep": "../lapin-mep" }`), so the edition builds as any other and the public repo holds only that hook. The engine's module contract needs nothing new for this.

## Open questions (answered by default)

1. **Pairs:** drawn once as a pair with a separation (default), the pair openable into two runs; or always two runs?
2. **Rise target:** a rise node names the storey it goes to (default) and the continuation is drawn on that storey from the matching drop; or a free elevation on the same storey too (default: both allowed).
3. **Symbol standard:** ISO 4067 for pipework and ducts, IEC 60617 for electrical (default), with the Romanian STAS equivalents where they differ, as an option later.
4. **Equipment first or runs first:** both; a run may start in the open and be connected later (default), so the engineer can draw the trunk before the terminals exist.
5. **Where the module lives:** the private sibling repo (default), or a private folder inside this repo excluded from the public release?

## Progress

- **Stage 1 done (26.10.2026)**, in `lapin-mep` (private, beside this repo) against lapin-pro's platform: `Systems` (IfcDistributionSystem with `IfcRelServicesBuildings`, defaults by service), `Roles` (the catalogue of node roles with their ports), `Nodes` (IFC classes by role, ports in world space, box bodies and plan symbols, rise/drop with its riser), `Runs` (IfcPipe/Duct/CableSegment, single or paired, connections as `IfcRelConnectsPorts` relations that hold on moves, tees, splits, falls), `MepPlacement`; commands `mep.createSystem/updateSystem/placeNode/drawRun/connect/disconnect/setRunPoints`, queries `mep.systems/network/openPorts`, catalogs `roles` and `services`. The IFC export takes the network through `IIfcNetwork` (Lapin.Platform, `engine/src/Platform/Lapin.Platform/IfcNetwork.cs`), a service any module may register: the writer nests each element's ports (`IfcDistributionPort`, placed on the element, flow, PIPE/DUCT/CABLE, the system type) with `IfcRelNests`, writes `IfcRelConnectsPorts` for every connection and one `IfcDistributionSystem` per system with `IfcRelAssignsToGroup` and `IfcRelServicesBuildings`; the distribution classes are in the writer's class map with IFC4's enumerations, so a boiler stays an IfcBoiler. `MepNetwork` implements it: a node's ports from its role, a run's `start` and `end` at its centreline's ends and a `tee:<branch>` port per junction. Tests: `engine/tests/Lapin.Mep.Tests` (NetworkTests, IfcNetworkTests). The round trip back (IFC import recognising ports and systems) is left for when a foreign MEP IFC is on the table.
- **Stage 2 done (26.10.2026)**, `lapin-mep/ui/src/modules/mep`: the MEP tab (order 45, with Modify) — Systems… (the dialog: the systems with their service and counts, one current, renamed in place, its defaults edited: size, plane and the three elevations, slope, pair, separation, material, insulation, bend radius; new systems by service; how new runs are drawn: paired or single, in a plane, over the system's defaults), MEP plan (a plan view of the MEP discipline for the storey, opened in the pane, which lands in the browser's MEP set), Open ports (selects and counts what is left open); Run (a polyline; an end within 0.3 m of an open port connects to it, else to a run of the system passing there), Connect (a run's nearer end, then the node or run it joins) and Disconnect; node tools by role grouped by service (Heating, Water, Drainage, Electrical, Air, Fittings), each placing in the system that takes the role (the current one, else the first that does, else a new one of the service), an inline fitting dropped on a run splitting it. A node's ports are snap points in plan. Grips: a node's point and a rotation handle (through `element.transform`), a run's vertices (Ctrl-click removes one) and leg middles that slide a leg (Ctrl-click inserts a vertex), with the run drawn as a reference line. The inspector's MEP section shows the system (and opens the dialog), the role and duty, each port open or connected, a run's two ends with Disconnect; the class's generic fields are split between runs and nodes through `fieldKeys`, a new hook on `PropertySectionDef` (lapin-pro). Romanian phrases are added from the module (`i18n.ts`). Tags come from Annotate's TAG as for any element. Checked headless with the dev engine and vite (`LAPIN_PRODUCT=lapin-mep`): the tab, two nodes, a run and the dialog.
- Next: stage 3, the engineering services on the core (from the old branch), and the symbols per discipline convention when the user asks.
