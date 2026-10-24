/**
 * Studio (the old Lapin's): solids from sketches drawn on a plan or a
 * section, an extrusion, a sweep or a revolution, each following its sketch;
 * live booleans between elements. EXTRUDE, SWEEP and REVOLVE are also
 * command-line commands.
 */

import { Box, Eye, PenTool, RefreshCcw, SquareMinus, SquarePlus, SquareSplitHorizontal, Unlink, Waypoints } from 'lucide-react';
import type { Grip, LapinModule, RefLine, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { gripsOf } from '../../platform/modules';
import { fromJson } from '../../viewport/cad/shapes';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { editSketch } from './sketch';
import { FeatureCard } from './FeatureCard';
import { cadLog, registerCommand, startCommand, type Step } from '../../viewport/cad/session';

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
 * typed value. Corners are mitred; a closed path makes one ring.
 */
const PLACES: Record<string, string> = { C: 'centre', R: 'right', L: 'left', O: 'outside', I: 'inside' };
function sweep(): Step {
  let place = 'centre';
  let gap = 0;
  const pickProfile = (path: { id: string }): Step => ({
    prompt: `Pick the closed profile [Centre/Right/Left/Outside/Inside: now ${place}; type a gap from the path: now ${gap} m]`, want: 'pick',
    keywords: Object.fromEntries(Object.entries(PLACES).map(([k, v]) => [k, () => { place = v; return pickProfile(path); }])),
    value: (v) => { gap = v; return pickProfile(path); },
    pick: async (profile) => { await execute('studio.sweep', { profileId: profile.id, pathId: path.id, place, placeOffset: gap }); return null; },
  });
  return { prompt: 'Pick the path (a line, polyline, arc or circle)', want: 'pick', pick: (path) => pickProfile(path) };
}

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
      { label: 'Solids', tools: [cmd('extrude', 'Extrude', Box, 'EXTRUDE'), cmd('sweep', 'Sweep', Waypoints, 'SWEEP'), cmd('revolve', 'Revolve', RefreshCcw, 'REVOLVE'), { id: 'studio.editSketch', label: 'Edit sketch', icon: PenTool, run: () => void editSketch() }] },
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
  propertySections: [{ id: 'studioFeature', label: 'Studio', classes: SOLID_CLASSES, component: FeatureCard }],
  grips: Object.fromEntries(SOLID_CLASSES.map((c) => [c, solidGrips])),
  references: Object.fromEntries(SOLID_CLASSES.map((c) => [c, solidRefs])),
};

export default studio;
