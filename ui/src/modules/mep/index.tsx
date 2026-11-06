/**
 * MEP: the building's services drawn as schematics on the plan — systems
 * with their defaults, nodes with ports placed by role, runs between them
 * that snap to ports and to other runs, connections made and undone by hand,
 * a system placed whole and shaped in its edit mode (docs/plan-mep.md). Tags
 * come from Annotate's TAG, as for any element; a plan of the MEP discipline
 * is made from here. The engineering on the network (checks, sizing, the
 * assistants) is mep-pro's, which adds its groups to this tab.
 */

import { Box, Container, CookingPot, Cylinder, Filter, Fuel, GitBranchPlus, Heater, ShieldCheck, Waypoints } from 'lucide-react';
import { AlarmSmoke, BellRing, CloudDrizzle, Cog, Droplet, FireExtinguisher, GitMerge, Hand, PanelTop, RefreshCw, Siren, ThermometerSun } from 'lucide-react';
import { AirVent, ArrowDownToLine, Ban, Bath, CircleDot, Droplets, Fan, Flame, Gauge, GitFork, Lightbulb, Link2, Milestone, PencilLine, Route, SquarePen, Waves, MoveVertical, Plug, ScanSearch, ShowerHead, Spline, Thermometer, ToggleLeft, Toilet, Unlink, Utensils, Wind, Workflow, Wrench, Zap, Map as MapIcon } from 'lucide-react';
import type { Icon } from '../../icons';
import type { Grip, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { activeStorey, ensureStorey, execute, useSession } from '../../engine/session';
import { bypass, onLeg, runNear, squareOff } from './route';
import { PICKABLE, RUN_CLASSES, endSpec, isNode, isRun, nearerEnd, nearestRun, networkQuery, openPortsQuery, runPoints, systemFor, systemMaterial } from './network';
import { runSystem, schedule } from './tools';
import { mepHooks } from './hooks';
import { setMep, useMep } from './prefs';
import { MepSection } from './MepSection';
import { SystemsDialog } from './SystemsDialog';
import { editSystem, editedSystem, toggleEdit } from './edit';
import { Background } from './Background';
import { mainArgs } from './gas';
import { LengthField } from '../../shell/cells';
import { fanGroup } from './fans/tools';
import { FanSection } from './fans/FanSection';
import { FanTypesDialog } from './fans/FanTypesDialog';
import { CLASS as FAN_CLASS, fanGrips } from './fans/fans';

/** A role as its tool places it: its id in the engine's catalogue, the services it takes ("*": any), whether it sits on a run. */
interface RoleTool { id: string; label: string; icon: Icon; services: string[]; inline?: boolean; shortcut?: string }

const ROLES: RoleTool[] = [
  { id: 'boiler', label: 'Boiler', icon: Flame, services: ['heating'], shortcut: 'BO' },
  { id: 'radiator', label: 'Radiator', icon: Thermometer, services: ['heating'], shortcut: 'RA' },
  { id: 'manifold', label: 'Manifold', icon: GitFork, services: ['heating'] },
  { id: 'waterMeter', label: 'Water meter', icon: Gauge, services: ['coldWater'], inline: true },
  { id: 'basin', label: 'Wash basin', icon: Droplets, services: ['coldWater', 'hotWater', 'drainage'] },
  { id: 'sink', label: 'Kitchen sink', icon: Utensils, services: ['coldWater', 'hotWater', 'drainage'] },
  { id: 'wc', label: 'WC', icon: Toilet, services: ['coldWater', 'drainage'] },
  { id: 'shower', label: 'Shower', icon: ShowerHead, services: ['coldWater', 'hotWater', 'drainage'] },
  { id: 'bath', label: 'Bath', icon: Bath, services: ['coldWater', 'hotWater', 'drainage'] },
  { id: 'stack', label: 'Stack', icon: ArrowDownToLine, services: ['drainage'] },
  { id: 'cleanout', label: 'Cleanout', icon: Wrench, services: ['drainage'], inline: true },
  { id: 'panel', label: 'Consumer unit', icon: Zap, services: ['electrical'] },
  { id: 'outlet', label: 'Outlet', icon: Plug, services: ['electrical'] },
  { id: 'light', label: 'Light', icon: Lightbulb, services: ['electrical'] },
  { id: 'switch', label: 'Switch', icon: ToggleLeft, services: ['electrical'], inline: true },
  { id: 'fan', label: 'Fan', icon: Fan, services: ['supplyAir', 'extractAir'] },
  { id: 'diffuser', label: 'Diffuser', icon: Wind, services: ['supplyAir'] },
  { id: 'grille', label: 'Extract grille', icon: AirVent, services: ['extractAir'] },
  { id: 'valve', label: 'Valve', icon: CircleDot, services: ['*'], inline: true },
  { id: 'rise', label: 'Rise / drop', icon: MoveVertical, services: ['*'] },
  { id: 'cap', label: 'Cap', icon: Ban, services: ['*'] },
  // Gas: a residence's installation, from the meter box to the appliances; the street's tapping tee.
  { id: 'meterBox', label: 'Meter box', icon: Box, services: ['gas'] },
  { id: 'regulator', label: 'Gas regulator', icon: Gauge, services: ['gas'], inline: true },
  { id: 'gasMeter', label: 'Gas meter', icon: Gauge, services: ['gas'], inline: true },
  { id: 'gasShutoff', label: 'Main gas shut-off valve', icon: ShieldCheck, services: ['gas'], inline: true },
  { id: 'gasTap', label: 'Appliance gas valve', icon: CircleDot, services: ['gas'], inline: true },
  { id: 'flexConnector', label: 'Flexible connector', icon: Waypoints, services: ['gas', 'fuelOil'], inline: true },
  { id: 'gasBoiler', label: 'Gas boiler', icon: Flame, services: ['gas', 'heating'] },
  { id: 'cooker', label: 'Gas cooker', icon: CookingPot, services: ['gas'] },
  { id: 'waterHeater', label: 'Gas water heater', icon: Heater, services: ['gas'] },
  { id: 'tappingTee', label: 'Tapping tee', icon: GitBranchPlus, services: ['gas'], inline: true },
  // Fuel oil: the tank, its fill point and vent cap, the filter, the oil boiler.
  { id: 'oilTank', label: 'Fuel oil tank', icon: Cylinder, services: ['fuelOil'] },
  { id: 'fillPoint', label: 'Fill point', icon: Fuel, services: ['fuelOil'] },
  { id: 'ventCap', label: 'Vent cap', icon: Wind, services: ['fuelOil'] },
  { id: 'oilFilter', label: 'Oil filter', icon: Filter, services: ['fuelOil'], inline: true },
  { id: 'oilBoiler', label: 'Oil boiler', icon: Container, services: ['fuelOil', 'heating'] },
  // Fire protection (report #89): the detection loop's panel and devices, each placed joining the loop's open end; the hydrants' water.
  { id: 'firePanel', label: 'Fire alarm control panel', icon: PanelTop, services: ['fireAlarm'] },
  { id: 'smokeDetector', label: 'Smoke detector', icon: AlarmSmoke, services: ['fireAlarm'], shortcut: 'SD' },
  { id: 'heatDetector', label: 'Heat detector', icon: ThermometerSun, services: ['fireAlarm'], shortcut: 'HD' },
  { id: 'callPoint', label: 'Manual call point', icon: Hand, services: ['fireAlarm'], shortcut: 'MC' },
  { id: 'sounder', label: 'Sounder', icon: BellRing, services: ['fireAlarm'] },
  { id: 'sounderBeacon', label: 'Sounder beacon', icon: Siren, services: ['fireAlarm'] },
  { id: 'fireTank', label: 'Fire water reserve', icon: Cylinder, services: ['fireWater'] },
  { id: 'firePump', label: 'Fire pump set', icon: Cog, services: ['fireWater'] },
  { id: 'hydrant', label: 'Internal hydrant', icon: FireExtinguisher, services: ['fireWater'], shortcut: 'HY' },
  { id: 'externalHydrant', label: 'External hydrant', icon: Droplet, services: ['fireWater'] },
  { id: 'breechingInlet', label: 'Fire brigade inlet', icon: GitMerge, services: ['fireWater'] },
  { id: 'sprinkler', label: 'Sprinkler', icon: CloudDrizzle, services: ['fireWater'] },
];
const role = (id: string) => ROLES.find((r) => r.id === id)!;

/** A node placed at a click, in the system that takes its role; an inline fitting dropped on a run of that system splits it. */
const nodeTool = (r: RoleTool): ToolDef => ({
  id: `mep.node.${r.id}`, label: r.label, icon: r.icon, shortcut: r.shortcut,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    const systemId = await systemFor(r.services);
    if (!systemId) return;
    // An endpoint joins the system's network by a run of its own (an inline fitting sits on a run instead).
    const args: Record<string, unknown> = { systemId, role: r.id, storeyId: ctx.storeyId, x: p.x, y: p.y, connect: !r.inline };
    if (r.inline) {
      const net = (await networkQuery(systemId))?.[0];
      const on = net ? nearestRun(net.runs, p, 0.3) : null;
      if (on) args.onRunId = on.id;
    }
    await ctx.execute('mep.placeNode', args);
  },
});

/** A run along a polyline (Enter or a click on the last point finishes it); an end on an open port or on a run of the system connects there. */
const run: ToolDef = {
  id: 'mep.run', label: 'Run', icon: Spline, shortcut: 'RN',
  // Laid as the system's material allows when its rules are known (mep-pro: copper at 45° steps, PVC turning 45° at most, PEX-AL-PEX freely).
  gesture: {
    kind: 'polyline',
    constrain: (placed, aimed) => {
      const m = systemMaterial(editedSystem() ?? useMep.getState().systemId);
      return m ? mepHooks.nextPoint(m.service, m.material, placed, aimed) : aimed;
    },
  },
  complete: async ({ points }, ctx) => {
    const systemId = await runSystem();
    if (!systemId || points.length < 2) return;
    const start = await endSpec(systemId, points[0]);
    const end = await endSpec(systemId, points[points.length - 1]);
    const { pair, plane } = useMep.getState();
    await ctx.execute('mep.drawRun', {
      systemId, storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]),
      ...(start ? { start } : {}), ...(end ? { end } : {}), ...(pair != null ? { pair } : {}), ...(plane ? { plane } : {}),
    });
  },
};

/** The class fields a run and a node have (one class may hold both: a stack is an IfcPipeSegment). */
const RUN_FIELDS = ['size', 'height', 'elevation', 'plane', 'pair', 'separation', 'slope', 'material', 'cover', 'warningTape'];
const NODE_FIELDS = ['role', 'x', 'y', 'rotation', 'elevation', 'width', 'depth', 'height', 'duty', 'toElevation', 'consumption', 'flue', 'placement', 'capacity', 'coverage', 'hoseLength', 'jetLength'];

/** Connect: a run (its end nearer the click), then the node or run it joins. */
const connect: ToolDef = {
  id: 'mep.connect', label: 'Connect', icon: Link2, shortcut: 'CN',
  gesture: { kind: 'pick', types: PICKABLE },
  complete: async ({ points: [p], entity }) => {
    const pending = useMep.getState().pending;
    if (!entity) return;
    if (!pending) {
      if (!isRun(entity)) { notify('Pick a run first.'); return; }
      const end = nearerEnd(entity, p);
      setMep({ pending: { runId: entity.id, end } });
      notify(`${entity.name}, ${end}: now pick the node or run it connects to.`);
      return;
    }
    setMep({ pending: null });
    if (entity.id === pending.runId) return;
    const to = isRun(entity) ? { runId: entity.id, at: [p.x, p.y] } : { nodeId: entity.id };
    await execute('mep.connect', { runId: pending.runId, end: pending.end, to });
  },
};

/** Disconnect: a run's end nearer the click is freed. */
const disconnect: ToolDef = {
  id: 'mep.disconnect', label: 'Disconnect', icon: Unlink,
  gesture: { kind: 'pick', types: RUN_CLASSES },
  complete: async ({ points: [p], entity }) => { if (entity && isRun(entity)) await execute('mep.disconnect', { runId: entity.id, end: nearerEnd(entity, p) }); },
};

/** A system placed whole from a click: its source there, an endpoint beside it, the run between them; then it opens for editing. */
const newSystem = (service: string, label: string, icon: Icon): ToolDef => ({
  id: `mep.new.${service}`, label, icon,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    const r = await ctx.execute('mep.placeSystem', { service, storeyId: ctx.storeyId, x: p.x, y: p.y }) as { id: string; endpointId: string } | null;
    if (!r?.id) return;
    useUi.setState({ tool: null });
    editSystem(r.id);
    useUi.setState({ selected: [r.endpointId], selection: r.endpointId });
    notify('Move the source and the endpoint where they go; add endpoints from the inspector (+) or the tools; Done ends the edit.');
  },
});
const newSystems: ToolDef[] = [
  newSystem('heating', 'Heating', Flame), newSystem('coldWater', 'Water', Droplets), newSystem('hotWater', 'Hot water', Waves),
  newSystem('drainage', 'Drainage', ArrowDownToLine), newSystem('electrical', 'Electrical', Zap), newSystem('supplyAir', 'Ventilation', Wind),
  newSystem('gas', 'Gas', Flame), newSystem('fuelOil', 'Fuel oil', Fuel),
  newSystem('fireAlarm', 'Fire alarm', AlarmSmoke), newSystem('fireWater', 'Fire hydrants', FireExtinguisher),
];
const edit: ToolDef = { id: 'mep.edit', label: 'Edit system', icon: SquarePen, shortcut: 'ES', run: () => toggleEdit(useMep.getState().systemId) };

/** A handle on a run: click the run where it goes, then drag it. */
const addHandle: ToolDef = {
  id: 'mep.addHandle', label: 'Add handle', icon: Milestone,
  gesture: { kind: 'pick', types: RUN_CLASSES },
  complete: async ({ points: [p], entity }) => {
    if (!entity || !isRun(entity)) return;
    await execute('mep.addHandle', { runId: entity.id, x: p.x, y: p.y });
    useUi.setState({ selected: [entity.id], selection: entity.id });
  },
};

/** How near a run a click must fall to take it (m). */
const ON_RUN = 0.3;
const runAt = (p: Vec2) => runNear(useSession.getState().nodes, useUi.getState().selected, p, ON_RUN);

/**
 * Detour: a click on a run's leg, a second on the same leg (kept on it), then
 * the side and distance (square off the leg; the distance can be typed): the
 * span goes out, along and back round what is in the way.
 */
const detour: ToolDef = {
  id: 'mep.detour', label: 'Detour', icon: Route, shortcut: 'DU',
  gesture: {
    kind: 'threePoints',
    constrain: (placed, aimed) => {
      const on = runAt(placed[0]);
      if (!on) return aimed;
      return placed.length === 1 ? onLeg(on.leg, aimed) : squareOff(on.leg, onLeg(on.leg, placed[1]), aimed);
    },
    preview: (placed, aimed) => {
      const on = runAt(placed[0]);
      if (!on) return null;
      if (placed.length === 1) return [onLeg(on.leg, placed[0]), onLeg(on.leg, aimed)];
      return [on.leg.a, ...bypass(on.leg, placed[0], placed[1], aimed), on.leg.b];
    },
  },
  complete: async ({ points: [from, to, side] }) => {
    const on = runAt(from);
    if (!on) { notify('Start the detour on a run.'); return; }
    await execute('mep.detour', { id: on.run.id, from: [from.x, from.y], to: [to.x, to.y], side: [side.x, side.y] });
    useUi.setState({ selected: [on.run.id], selection: on.run.id });
  },
};

/** The run a redrawn path is for: the one selected, else the one the path starts on. */
const redrawTarget = (first: Vec2): NodeDto | null => {
  const nodes = useSession.getState().nodes;
  const sel = useUi.getState().selected.map((id) => nodes.get(id)).filter((n): n is NodeDto => !!n && isRun(n));
  return sel.length === 1 ? sel[0] : runAt(first)?.run ?? null;
};

/** Redraw path: the selected run (or the one the path starts on) takes a new path; its system, size and connections stay, its ends on their ports. */
const redraw: ToolDef = {
  id: 'mep.redrawRun', label: 'Redraw path', icon: PencilLine, shortcut: 'RP',
  gesture: {
    kind: 'polyline',
    constrain: (placed, aimed) => {
      const target = redrawTarget(placed[0]);
      const m = systemMaterial(typeof target?.props.systemId === 'string' ? target.props.systemId : null);
      return m ? mepHooks.nextPoint(m.service, m.material, placed, aimed) : aimed;
    },
  },
  complete: async ({ points }) => {
    if (points.length < 2) return;
    const target = redrawTarget(points[0]);
    if (!target) { notify('Select the run first, or start the path on it.'); return; }
    await execute('mep.redrawRun', { id: target.id, points: points.map((p) => [p.x, p.y]) });
    useUi.setState({ selected: [target.id], selection: target.id });
  },
};

const systems: ToolDef = { id: 'mep.systems', label: 'Systems…', icon: Workflow, run: () => useUi.setState({ dialog: 'mepSystems' }) };

/** What is left open in every system: selected, and counted. */
const openPorts: ToolDef = {
  id: 'mep.openPorts', label: 'Open ports', icon: ScanSearch,
  run: () => void openPortsQuery().then((items) => {
    const ids = [...new Set((items ?? []).map((i) => i.id))];
    if (ids.length === 0) { notify('No open ports.'); return; }
    useUi.setState({ selected: ids, selection: ids[0] });
    notify(`${items!.length} open port(s) on ${ids.length} element(s).`);
  }),
};

/** A plan of the MEP discipline for the storey shown, opened in the pane (it lands in the browser's MEP set). */
const mepPlan: ToolDef = {
  id: 'mep.plan', label: 'MEP plan', icon: MapIcon,
  run: () => void ensureStorey().then(async (storeyId) => {
    if (!storeyId) return;
    const storey = activeStorey();
    const r = await execute('view.create', { kind: 'plan', storeyId, discipline: 'MEP', name: `${storey?.name ?? 'Plan'} – MEP` }) as { id: string } | null;
    if (r?.id) useUi.setState({ view: 'plan', storeyId, planViewId: r.id });
  }),
};

/** A gas main's offset to the left of its line and its cover, chosen in the command line's bar while the tool is on. */
function MainOptions() {
  const { mainOffset, mainDepth } = useMep();
  return (
    <span className="row" data-gas-main-options>
      <span className="muted small">Offset from the line</span><LengthField metres={mainOffset} onCommit={(v) => { if (v != null) setMep({ mainOffset: v }); }} />
      <span className="muted small">Depth (cover)</span><LengthField metres={mainDepth} min={0} onCommit={(v) => { if (v != null) setMep({ mainDepth: v }); }} />
    </span>
  );
}

/** A gas main along a road, an alignment, a drafted line or a wall: click it; the main lies at the offset to its left and the cover under the ground. */
const gasMain: ToolDef = {
  id: 'mep.gasMain', label: 'Gas main along…', icon: Waypoints, shortcut: 'GM',
  gesture: { kind: 'pick', types: ['*'] },
  options: MainOptions,
  complete: async ({ entity }, ctx) => {
    if (!entity) return;
    const { mainOffset, mainDepth } = useMep.getState();
    const r = await ctx.execute('mep.mainAlong', mainArgs({ alongId: entity.id }, mainOffset, mainDepth, ctx.storeyId)) as { id: string; systemId: string } | null;
    if (r?.id) { setMep({ systemId: r.systemId }); useUi.setState({ selected: [r.id], selection: r.id }); }
  },
};

/** A gas main drawn point by point (Enter finishes), at the cover under the ground. */
const gasMainDrawn: ToolDef = {
  id: 'mep.gasMainDrawn', label: 'Gas main', icon: Spline,
  gesture: { kind: 'polyline' },
  options: MainOptions,
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const { mainDepth } = useMep.getState();
    const r = await ctx.execute('mep.mainAlong', mainArgs({ points }, 0, mainDepth, ctx.storeyId)) as { id: string; systemId: string } | null;
    if (r?.id) { setMep({ systemId: r.systemId }); useUi.setState({ selected: [r.id], selection: r.id }); }
  },
};

/** Service connection: the gas main, then the meter box; a tapping tee, the service pipe at its cover, the riser into the box. */
const serviceConnection: ToolDef = {
  id: 'mep.serviceConnection', label: 'Service connection', icon: GitBranchPlus, shortcut: 'GS',
  gesture: { kind: 'pick', types: PICKABLE },
  complete: async ({ points: [p], entity }) => {
    if (!entity) return;
    const main = useMep.getState().pendingMain;
    if (!main) {
      if (!isRun(entity)) { notify('Pick the gas main first.'); return; }
      setMep({ pendingMain: entity.id });
      notify('Now pick the meter box.');
      return;
    }
    setMep({ pendingMain: null });
    if (!isNode(entity)) { notify('Pick the meter box.'); return; }
    const r = await execute('mep.serviceConnection', { mainId: main, toNodeId: entity.id, at: [p.x, p.y] }) as { id: string } | null;
    if (r?.id) useUi.setState({ selected: [r.id], selection: r.id });
  },
};

const gasSchedule = schedule('mep.scheduleGas', 'Gas and fuel oil bill of materials', 'mep-gas');
const fireSchedule = schedule('mep.scheduleFire', 'Fire protection bill of materials', 'mep-fire');

/** The fire detection loop closed back to its panel's return: the system being edited or the current one when it is a fire alarm, else the only one. */
const closeLoop: ToolDef = {
  id: 'mep.closeLoop', label: 'Close the loop', icon: RefreshCw,
  run: () => {
    const nodes = useSession.getState().nodes;
    const fire = [...nodes.values()].filter((n) => n.type === 'IfcDistributionSystem' && n.props.service === 'fireAlarm');
    const pick = [editedSystem(), useMep.getState().systemId].find((id) => id && fire.some((s) => s.id === id)) ?? (fire.length === 1 ? fire[0].id : null);
    if (!pick) { notify(fire.length ? 'Edit the fire alarm system whose loop to close (Edit system).' : 'Make a fire alarm system first (New system → Fire alarm).'); return; }
    void execute('mep.closeLoop', { systemId: pick }).then((r) => { if (r) notify('The loop is closed back to the panel.'); });
  },
};
const equipmentSchedule = schedule('mep.scheduleEquipment', 'Equipment list', 'mep-equipment');
const runSchedule = schedule('mep.scheduleRuns', 'Run list', 'mep-runs');

/** A node: its point to move, a handle 0.6 m out along its rotation to turn it. */
function nodeGrips(node: NodeDto): Grip[] {
  const at: Vec2 = { x: Number(node.props.x ?? 0), y: Number(node.props.y ?? 0) };
  const rotation = Number(node.props.rotation ?? 0);
  const rad = (rotation * Math.PI) / 180;
  const handle = { x: at.x + Math.cos(rad) * 0.6, y: at.y + Math.sin(rad) * 0.6 };
  return [
    { key: 'at', kind: 'move', at, step: (p) => ({ command: 'element.transform', args: { ids: [node.id], op: 'move', by: [p.x - at.x, p.y - at.y] } }) },
    { key: 'rotate', kind: 'rotate', at: handle, step: (p) => ({ command: 'element.transform', args: { ids: [node.id], op: 'rotate', center: [at.x, at.y], angle: Math.round(((Math.atan2(p.y - at.y, p.x - at.x) * 180) / Math.PI - rotation) * 100) / 100 } }) },
  ];
}

/** A run: its vertices to drag (Ctrl-click takes one out), its legs' middles to slide a leg (Ctrl-click adds a vertex there). */
function runGrips(node: NodeDto): Grip[] {
  const pts = runPoints(node);
  const save = (next: Vec2[]) => ({ command: 'mep.setRunPoints', args: { id: node.id, points: next.map((q) => [q.x, q.y]) } });
  const grips: Grip[] = pts.map((q, i) => ({
    key: `v${i}`, kind: 'vertex', at: q, title: 'Drag the handle · Ctrl-click takes it out',
    step: (p) => save(pts.map((x, k) => (k === i ? p : x))),
    remove: pts.length > 2 ? () => save(pts.filter((_, k) => k !== i)) : undefined,
  }));
  for (let i = 0; i + 1 < pts.length; i++) {
    const a = pts[i], b = pts[i + 1];
    const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    grips.push({
      key: `e${i}`, kind: 'edgeMid', at: mid, title: 'Drag: slide the leg · Ctrl-click adds a handle here',
      // Slid square to itself (the engine keeps the drag's part across the leg); a jog where it meets an end or a leg in line.
      step: (p) => ({ command: 'mep.slideLeg', args: { id: node.id, leg: i, by: [p.x - mid.x, p.y - mid.y] } }),
      insert: () => save([...pts.slice(0, i + 1), mid, ...pts.slice(i + 1)]),
    });
  }
  return grips;
}

const grips = (n: NodeDto): Grip[] => (isRun(n) ? runGrips(n) : isNode(n) ? nodeGrips(n) : []);
const references = (n: NodeDto): RefLine[] => (isRun(n) ? [{ points: runPoints(n), closed: false, kind: 'axis' }] : []);
const byClass = <T,>(f: (n: NodeDto) => T): Record<string, (n: NodeDto) => T> => Object.fromEntries(PICKABLE.map((c) => [c, f]));

const mep: LapinModule = {
  id: 'mep',
  name: 'MEP',
  summary: 'Heating, water, drainage, ventilation, electrical, gas and fire protection systems drawn as schematics on the plan.',
  tabs: [{
    id: 'mep', label: 'MEP', order: 45,
    groups: [
      { label: 'New system', tools: [], small: newSystems },
      { label: 'System', tools: [edit], small: [systems, mepPlan, openPorts, equipmentSchedule, runSchedule, gasSchedule, fireSchedule] },
      { label: 'Runs', tools: [run], small: [connect, disconnect, addHandle, detour, redraw] },
      { label: 'Heating', tools: [], small: [nodeTool(role('boiler')), nodeTool(role('radiator')), nodeTool(role('manifold'))] },
      { label: 'Plumbing', tools: [], small: [nodeTool(role('basin')), nodeTool(role('wc')), nodeTool(role('sink')), nodeTool(role('shower')), nodeTool(role('bath')), nodeTool(role('waterMeter')), nodeTool(role('stack')), nodeTool(role('cleanout'))] },
      { label: 'Electrical', tools: [], small: [nodeTool(role('panel')), nodeTool(role('outlet')), nodeTool(role('light')), nodeTool(role('switch'))] },
      { label: 'Air', tools: [], small: [nodeTool(role('fan')), nodeTool(role('diffuser')), nodeTool(role('grille'))] },
      // Exhaust fans and ventilators (report #55): elements of their own, configured by type.
      fanGroup,
      { label: 'Fittings', tools: [], small: [nodeTool(role('valve')), nodeTool(role('rise')), nodeTool(role('cap'))] },
      { label: 'Gas', tools: [], small: [nodeTool(role('meterBox')), nodeTool(role('regulator')), nodeTool(role('gasMeter')), nodeTool(role('gasShutoff')), nodeTool(role('gasTap')), nodeTool(role('flexConnector')), nodeTool(role('gasBoiler')), nodeTool(role('cooker')), nodeTool(role('waterHeater'))] },
      { label: 'Street', tools: [], small: [gasMain, gasMainDrawn, serviceConnection, nodeTool(role('tappingTee'))] },
      { label: 'Fuel oil', tools: [], small: [nodeTool(role('oilTank')), nodeTool(role('fillPoint')), nodeTool(role('ventCap')), nodeTool(role('oilFilter')), nodeTool(role('oilBoiler'))] },
      { label: 'Fire alarm', tools: [], small: [nodeTool(role('firePanel')), nodeTool(role('smokeDetector')), nodeTool(role('heatDetector')), nodeTool(role('callPoint')), nodeTool(role('sounder')), nodeTool(role('sounderBeacon')), closeLoop] },
      { label: 'Fire hydrants', tools: [], small: [nodeTool(role('fireTank')), nodeTool(role('firePump')), nodeTool(role('hydrant')), nodeTool(role('externalHydrant')), nodeTool(role('breechingInlet')), nodeTool(role('sprinkler'))] },
    ],
  }],
  hotbar: [run, nodeTool(role('radiator')), nodeTool(role('valve'))],
  propertySections: [
    { id: 'mep', label: 'MEP', classes: PICKABLE, component: MepSection, fieldKeys: (n) => (isRun(n) ? RUN_FIELDS : isNode(n) ? NODE_FIELDS : null) },
    { id: 'fan', label: 'Exhaust fan', classes: [FAN_CLASS], component: FanSection },
  ],
  dialogs: { mepSystems: SystemsDialog, fanTypes: FanTypesDialog },
  background: Background,
  grips: { ...byClass(grips), [FAN_CLASS]: fanGrips },
  setType: { [FAN_CLASS]: (ids, typeId) => execute('mep.setFanType', { ids, typeId }) },
  references: byClass(references),
};

export default mep;
