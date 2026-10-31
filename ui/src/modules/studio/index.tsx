/**
 * Studio (the old Lapin's): solids from sketches drawn on a plan or a
 * section, an extrusion, a sweep or a revolution, each following its sketch;
 * live booleans between elements. EXTRUDE, SWEEP and REVOLVE are also
 * command-line commands.
 */

import { Box, BrickWall, Container, Cylinder, Eye, Frame, Layers2, PenTool, RefreshCcw, SquareMinus, SquarePlus, SquareSplitHorizontal, SquareSquare, SquareStack, Unlink, Waypoints } from 'lucide-react';
import type { Grip, LapinModule, RefLine, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { gripsOf } from '../../platform/modules';
import { fromJson } from '../../viewport/cad/shapes';
import { notify, useUi } from '../../app/store';
import { activeStorey, execute, useSession } from '../../engine/session';
import { editSketch } from './sketch';
import { FeatureCard } from './FeatureCard';
import { RefPlaneSection } from './RefPlaneSection';
import { COMMAND, DEFAULT_JUSTIFY, JUSTIFY, frontViewId, makesNative, nativeArgs, shapeFits, solidArgs, solidFieldKeys, threePoints, viewOf, type ThickChoice, type ThickOp } from './thick';
import { cadLog, registerCommand, startCommand, type Step } from '../../viewport/cad/session';

/** The thickness and height last used, the next command's defaults. */
const last = { thickness: 0.2, height: 3 };

/**
 * RING, BOX and TWALL: pick the drafted shape, then (on a plan, native walls of the current wall type by default: their
 * height; a Studio solid, Solid keyword, or off a plan) the thickness and the height. Keywords set where the thickness lies
 * (Inside/Outside/Centre, or Left/Right/Centre), a box's Bottom and Top, and Native or Solid on a plan.
 */
function thick(op: ThickOp): Step {
  const words = JUSTIFY[op];
  let justify = DEFAULT_JUSTIFY[op];
  let solid = false;
  let bottom = false;
  let top = false;
  const what = op === 'ring' ? 'a circle, an arc or a closed outline' : op === 'box' ? 'a rectangle, a closed polyline or a circle' : 'a line, a polyline or an arc';
  const choice = (thickness: number, height: number): ThickChoice => ({ op, justify, thickness, height, bottom, top });
  const run = async (command: string, args: Record<string, unknown>, done: string) => {
    const r = await execute(command, args) as { id?: string; ids?: string[] } | null;
    if (r) { cadLog(done); useUi.setState({ selected: r.ids ?? (r.id ? [r.id] : []), selection: r.ids?.[0] ?? r.id ?? null }); }
    return null;
  };
  const heightStep = (go: (h: number) => Promise<null>, prompt: string): Step => ({
    prompt: `${prompt} <${last.height}>`, want: 'none',
    value: (h) => { if (h !== 0) last.height = h; return go(h); },
    enter: () => go(last.height),
  });
  const pick = (): Step => ({
    prompt: `Pick ${what} [${Object.entries(words).map(([k, v]) => `${k}${v.slice(k.length)}`).join('/')}: ${justify}${op === 'box' ? `; Bottom ${bottom ? 'on' : 'off'}; Top ${top ? 'on' : 'off'}` : ''}; on a plan Native/Solid: ${solid ? 'solid' : 'native'}]`,
    want: 'pick',
    keywords: {
      ...Object.fromEntries(Object.entries(words).map(([k, v]) => [k, () => { justify = v; return pick(); }])),
      ...(op === 'box' ? { B: () => { bottom = !bottom; return pick(); }, T: () => { top = !top; return pick(); } } : {}),
      N: () => { solid = false; return pick(); },
      S: () => { solid = true; return pick(); },
    },
    pick: (hit) => {
      const nodes = useSession.getState().nodes;
      const draft = nodes.get(hit.id);
      const shape = draft?.props.shape as { type?: string; closed?: boolean } | undefined;
      if (!shapeFits(op, shape)) { cadLog(`That is not ${what}.`); return pick(); }
      const view = viewOf(typeof draft?.props.viewId === 'string' ? draft.props.viewId : undefined, nodes);
      if (makesNative(view.kind, solid)) {
        const typeId = useUi.getState().wallTypeId;
        return heightStep((h) => run('architecture.wallsAlong', nativeArgs(shape, view.storeyId ?? activeStorey()?.id ?? null, typeId, choice(last.thickness, h)), 'Walls laid along the shape, joined at its corners.'), 'Wall height (m)');
      }
      return {
        prompt: `Thickness (m) <${last.thickness}>`, want: 'none',
        value: (t) => { if (t > 0) last.thickness = t; return heightStep((h) => run(COMMAND[op], solidArgs(hit.id, choice(last.thickness, h)), 'Made: it follows the shape and its plane.'), 'Height (m; negative: away from you)'); },
        enter: () => heightStep((h) => run(COMMAND[op], solidArgs(hit.id, choice(last.thickness, h)), 'Made: it follows the shape and its plane.'), 'Height (m; negative: away from you)'),
      };
    },
  });
  return pick();
}
registerCommand({ names: ['RING', 'RNG'], label: 'Ring', start: () => thick('ring') });
registerCommand({ names: ['BOX', 'BOXWALLS'], label: 'Box', start: () => thick('box') });
registerCommand({ names: ['TWALL', 'TW'], label: 'Wall strip', start: () => thick('wall') });

/** The new reference plane's view opened, to draw on. */
const openPlane = (r: unknown) => {
  const id = (r as { id?: string } | null)?.id;
  if (id) { useUi.setState({ view: 'drawing', drawingViewId: id, selection: id, selected: [id] }); notify('The reference plane is open: draw on it with Draft, then make solids on it with Studio.'); }
};

/** REFPLANE: a plane parallel to the view in front, a distance toward you (away when negative). */
function refPlane(): Step | null {
  const viewId = frontViewId(useUi.getState(), activeStorey()?.id ?? null);
  if (!viewId) { cadLog('Open a plan, a section, an elevation or a reference plane first.'); return null; }
  const make = async (offset: number) => { openPlane(await execute('view.create', { kind: 'plane', fromView: viewId, offset })); return null; };
  return { prompt: 'Offset from this view\'s plane (m; negative: away from you) <0>', want: 'none', value: make, enter: () => make(0) };
}
registerCommand({ names: ['REFPLANE', 'RPLANE'], label: 'Reference plane', start: refPlane });

/** A reference plane on the face picked in 3D: through the point, facing out of the face. */
const planeOnFace: ToolDef = {
  id: 'studio.planeOnFace', label: 'Plane on face', icon: SquareSquare,
  gesture: { kind: 'face', types: ['*'] },
  complete: async ({ entity, face }) => {
    if (!face?.normal || face.point.length < 3) { notify('Pick a face in the 3D view.'); return; }
    openPlane(await execute('view.create', { kind: 'plane', point: face.point, normal: face.normal, ...(entity ? { elementId: entity.id } : {}) }));
  },
};

/** A reference plane through three points picked on the model in 3D. */
let picked: number[][] = [];
const planeBy3: ToolDef = {
  id: 'studio.planeBy3', label: 'Plane by 3 points', icon: Frame,
  gesture: { kind: 'face', types: ['*'] },
  complete: async ({ face }) => {
    if (!face || face.point.length < 3) { notify('Pick the points in the 3D view, on the model.'); return; }
    picked = [...picked, face.point].slice(-3);
    if (picked.length < 3) { notify(picked.length === 1 ? 'First point picked: pick the second.' : 'Second point picked: pick the third.'); return; }
    const problem = threePoints(picked);
    const pts = picked;
    picked = [];
    if (problem) { notify(problem); return; }
    openPlane(await execute('view.create', { kind: 'plane', points: pts }));
  },
};

const CLASSES = ['PROXY', 'MEMBER', 'PLATE', 'COVERING', 'RAILING', 'STAIR', 'RAMP', 'CHIMNEY', 'FURNITURE', 'WALL', 'SLAB', 'COLUMN', 'BEAM', 'ROOF', 'FOOTING'];

/** EXTRUDE: pick a closed outline, then a depth (a class word at any step: SLAB, WALL…). */
function extrude(): Step {
  let cls = 'PROXY';
  const kw = (next: () => Step) => Object.fromEntries(CLASSES.map((c) => [c, () => { cls = c; cadLog(`Class ${c}.`); return next(); }]));
  const pick = (): Step => ({
    prompt: `Pick a closed outline [class ${cls}]`, want: 'pick', keywords: kw(pick),
    pick: (hit) => {
      const depth = (): Step => ({
        prompt: `Depth (m; negative: away from you) [class ${cls}] <1>`, want: 'none', keywords: kw(depth),
        value: async (d) => { await execute('studio.extrude', { profileId: hit.id, depth: d, class: cls }); return null; },
        enter: async () => { await execute('studio.extrude', { profileId: hit.id, depth: 1, class: cls }); return null; },
      });
      return depth();
    },
  });
  return pick();
}

/**
 * SWEEP: pick the path, then the closed profile that travels along it. Where
 * the profile sits on the path is a keyword (Centre, Right, Left, Outside,
 * Inside: outside a closed outline for a pavement), the gap from the path a
 * typed value; End picks a profile it becomes on the way, Rail a second rail
 * it stretches to. Corners are mitred; a closed path makes one ring. With
 * shapes selected that carry roles (path, profile, end profile, rail), it runs
 * on them at once.
 */
const PLACES: Record<string, string> = { C: 'centre', R: 'right', L: 'left', O: 'outside', I: 'inside' };
function byRole(): Record<string, string> {
  const nodes = useSession.getState().nodes;
  const out: Record<string, string> = {};
  for (const id of useUi.getState().selected) { const r = nodes.get(id)?.props.role; if (typeof r === 'string' && !out[r]) out[r] = id; }
  return out;
}
function sweep(): Step | null | Promise<null> {
  const roles = byRole();
  if (roles.path && roles.profile) {
    return execute('studio.sweep', { profileId: roles.profile, pathId: roles.path, ...(roles.endProfile ? { endProfileId: roles.endProfile } : {}), ...(roles.rail ? { railId: roles.rail } : {}) })
      .then((r) => { if (r) cadLog('Swept the selected profile along the selected path.'); return null; });
  }
  let place = 'centre';
  let gap = 0;
  let endId: string | null = null;
  let railId: string | null = null;
  const run = async (path: { id: string }, profile: { id: string }) => {
    await execute('studio.sweep', { profileId: profile.id, pathId: path.id, place, placeOffset: gap, ...(endId ? { endProfileId: endId } : {}), ...(railId ? { railId } : {}) });
    return null;
  };
  const pickProfile = (path: { id: string }): Step => ({
    prompt: `Pick the closed profile [Centre/Right/Left/Outside/Inside: ${place}; gap ${gap} m; End profile${endId ? ' ✓' : ''}; Rail${railId ? ' ✓' : ''}]`, want: 'pick',
    keywords: {
      ...Object.fromEntries(Object.entries(PLACES).map(([k, v]) => [k, () => { place = v; return pickProfile(path); }])),
      E: () => ({ prompt: 'Pick the profile it ends with', want: 'pick', pick: (h) => { endId = h.id; railId = null; return pickProfile(path); } }),
      RA: () => ({ prompt: 'Pick the second rail', want: 'pick', pick: (h) => { railId = h.id; endId = null; return pickProfile(path); } }),
    },
    value: (v) => { gap = v; return pickProfile(path); },
    pick: (profile) => run(path, profile),
  });
  return { prompt: 'Pick the path (a line, polyline, arc or circle)', want: 'pick', pick: (path) => pickProfile(path) };
}

/** LOFT: through the closed shapes selected, in the order they were selected (each on its own view: a plan of another level, a section further on). */
function loft(): null | Promise<null> {
  const nodes = useSession.getState().nodes;
  const ids = useUi.getState().selected.filter((id) => nodes.get(id)?.props.predefinedType === 'DRAFT');
  if (ids.length < 2) { cadLog('Select two closed shapes at least, one on each view (a plan of each level, or sections), in order; then LOFT.'); return null; }
  return execute('studio.loft', { profileId: ids[0], profileIds: ids }).then((r) => { if (r) cadLog(`Lofted through ${ids.length} profiles.`); return null; });
}
registerCommand({ names: ['LOFT', 'LO'], label: 'Loft', start: loft });

/** REVOLVE: pick the closed profile, then the axis line, then the angle (Enter: all the way round). */
function revolve(): Step {
  return {
    prompt: 'Pick the closed profile', want: 'pick',
    pick: (profile) => ({
      prompt: 'Pick the axis (a line)', want: 'pick',
      pick: (axis) => ({
        prompt: 'Angle (degrees) <360>', want: 'none',
        value: async (a) => { await execute('studio.revolve', { profileId: profile.id, axisId: axis.id, angle: a }); return null; },
        enter: async () => { await execute('studio.revolve', { profileId: profile.id, axisId: axis.id, angle: 360 }); return null; },
      }),
    }),
  };
}

registerCommand({ names: ['EXTRUDE', 'EXT', 'EXTRUSION'], label: 'Extrude', start: extrude });
registerCommand({ names: ['SWEEP', 'SW'], label: 'Sweep', start: sweep });
registerCommand({ names: ['REVOLVE', 'REV'], label: 'Revolve', start: revolve });

/** The selection as a boolean: the first selected is the target, the others its tools. */
const boolean = (op: 'subtract' | 'union' | 'intersect'): ToolDef['run'] => () => {
  const [target, ...tools] = useUi.getState().selected;
  if (!target || !tools.length) { notify('Select the target first, then Shift-click the elements to combine with it.'); return; }
  void execute('studio.boolean', { target, tools, op }).then((r) => {
    if (!r) return;
    notify(op === 'subtract' ? `${tools.length} element(s) subtracted from the first selected. Both stay editable: the cut follows them.`
      : op === 'union' ? 'United into the first selected element. The others stay editable: Show operands, edit them, and the union follows.'
      : 'Only what the elements share is left, in the first selected element. The others stay editable through Show operands.');
  });
};

const cmd = (id: string, label: string, icon: ToolDef['icon'], command: string): ToolDef => ({ id: `studio.${id}`, label, icon, run: () => void startCommand(command) });

const SOLID_CLASSES = ['IfcBuildingElementProxy', 'IfcMember', 'IfcPlate', 'IfcCovering', 'IfcRailing', 'IfcStair', 'IfcRamp', 'IfcChimney', 'IfcFurnishingElement'];
/** The drafted shapes a solid is made from: its profile, and its path or axis. */
const sources = (n: NodeDto): NodeDto[] => {
  const nodes = useSession.getState().nodes;
  return [n.props.profile, n.props.guide].filter((x): x is string => typeof x === 'string').map((id) => nodes.get(id)).filter((x): x is NodeDto => !!x);
};
/** A selected solid shows the grips of its sketch: dragging them reshapes the sketch, and the solid follows. */
const solidGrips = (n: NodeDto): Grip[] => (n.props.operation ? sources(n).flatMap((d) => gripsOf(d)) : []);
/** And the sketch itself, in its view. */
const solidRefs = (n: NodeDto): RefLine[] => (n.props.operation ? sources(n).flatMap((d) => {
  const sh = d.props.shape ? fromJson(d.props.shape as Record<string, unknown>) : null;
  const view = typeof d.props.viewId === 'string' ? d.props.viewId : undefined;
  if (!sh) return [];
  if (sh.type === 'line') return [{ points: [sh.a, sh.b], closed: false, kind: 'sketch' as const, view }];
  if (sh.type === 'polyline') return [{ points: sh.pts, closed: sh.closed, kind: 'sketch' as const, view }];
  if (sh.type === 'circle') return [{ points: Array.from({ length: 48 }, (_, i) => ({ x: sh.c.x + Math.cos((i * Math.PI) / 24) * sh.r, y: sh.c.y + Math.sin((i * Math.PI) / 24) * sh.r })), closed: true, kind: 'sketch' as const, view }];
  const sweep = (((sh.end - sh.start) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) || 2 * Math.PI;
  return [{ points: Array.from({ length: 25 }, (_, i) => ({ x: sh.c.x + Math.cos(sh.start + (sweep * i) / 24) * sh.r, y: sh.c.y + Math.sin(sh.start + (sweep * i) / 24) * sh.r })), closed: false, kind: 'sketch' as const, view }];
}) : []);

const studio: LapinModule = {
  id: 'studio',
  name: 'Studio',
  summary: 'Free-form solids from sketches (extrude, sweep, revolve) and live booleans.',
  tabs: [{
    id: 'studio', label: 'Studio', order: 50,
    groups: [
      { label: 'Solids', tools: [cmd('extrude', 'Extrude', Box, 'EXTRUDE'), cmd('sweep', 'Sweep', Waypoints, 'SWEEP'), cmd('revolve', 'Revolve', RefreshCcw, 'REVOLVE'), cmd('loft', 'Loft', Layers2, 'LOFT'), { id: 'studio.editSketch', label: 'Edit sketch', icon: PenTool, run: () => void editSketch() }] },
      { label: 'Thick', tools: [cmd('ring', 'Ring', Cylinder, 'RING'), cmd('box', 'Box', Container, 'BOX'), cmd('wallStrip', 'Wall strip', BrickWall, 'TWALL')] },
      { label: 'Work planes', tools: [cmd('refPlane', 'Plane from view', SquareStack, 'REFPLANE'), planeOnFace, planeBy3] },
      {
        label: 'Boolean',
        tools: [
          { id: 'studio.subtract', label: 'Subtract', icon: SquareMinus, run: boolean('subtract') },
          { id: 'studio.union', label: 'Union', icon: SquarePlus, run: boolean('union') },
          { id: 'studio.intersect', label: 'Intersect', icon: SquareSplitHorizontal, run: boolean('intersect') },
        ],
        small: [
          { id: 'studio.operands', label: 'Show operands', icon: Eye, run: () => { const id = useUi.getState().selection; if (id) void execute('studio.operands', { id }); else notify('Select a united or intersected element: its operands are shown for editing, or hidden again.'); } },
          { id: 'studio.removeBoolean', label: 'Remove operation', icon: Unlink, run: () => { const ids = useUi.getState().selected; if (!ids.length) { notify('Select the elements whose operation goes.'); return; } void execute('studio.removeBoolean', { ids }).then((r) => r && notify((r as { removed: number }).removed ? 'Operation removed: the elements are separate again.' : 'The selected elements take part in no boolean operation.')); } },
        ],
      },
    ],
  }],
  propertySections: [
    { id: 'studioFeature', label: 'Studio', classes: SOLID_CLASSES, component: FeatureCard, fieldKeys: solidFieldKeys },
    { id: 'studioRefPlane', label: 'Reference plane', classes: ['LapinView'], component: RefPlaneSection },
  ],
  grips: Object.fromEntries(SOLID_CLASSES.map((c) => [c, solidGrips])),
  references: Object.fromEntries(SOLID_CLASSES.map((c) => [c, solidRefs])),
};

export default studio;
