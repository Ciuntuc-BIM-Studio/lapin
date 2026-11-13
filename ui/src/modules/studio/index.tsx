/**
 * Studio (the old Lapin's, grown into a geometry editor: docs/plan-studio-editor.md):
 * solids from sketches drawn on a plan, a section, an elevation or a work
 * plane (any face picked in 3D), an extrusion, a sweep, a loft or a
 * revolution, each following its sketch; primitives placed with a click;
 * the steps a solid takes (push/pull, chamfer, fillet, shell, split, arrays,
 * 3D placement); live booleans between elements. EXTRUDE, SWEEP, REVOLVE and
 * LOFT are also command-line commands.
 */

import {
  Boxes, PackagePlus, ArrowUpDown, ArrowUpToLine, ArrowRightToLine, Eraser, Keyboard, Rainbow, Ruler, Spline, CopyPlus, CornerUpRight, Slash, Redo2, ScanEye, SquarePen, CircleCheckBig, Circle, Hexagon, RectangleHorizontal, Blocks, Box, BrickWall, CircleDot, Cone, Container, Cylinder, Eye, FlipHorizontal2, Frame, Grid3x3, Layers2, Move3d, Octagon, PackageOpen, PenTool, Pyramid,
  RefreshCcw, Rotate3d, RotateCw, Scissors, Slice, SquareMinus, SquarePlus, SquareRoundCorner, SquareSplitHorizontal, SquareSquare, SquareStack, Torus, TriangleRight, Unlink, Waypoints,
} from 'lucide-react';
import type { Grip, LapinModule, RefLine, ToolDef, ToolInput } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { gripsOf } from '../../platform/modules';
import { fromJson } from '../../viewport/cad/shapes';
import { notify, useUi } from '../../app/store';
import { activeStorey, execute, query, useSession } from '../../engine/session';
import { ask, choiceField, lengthField, numberField } from '../../shell/AskBox';
import { formatLength, lengthText, unitName } from '../../engine/unitsStore';
import { editSketch } from './sketch';
import { FeatureCard, featureFieldKeys, takeUpTo } from './FeatureCard';
import { OperationsCard, hasTools } from './OperationsCard';
import { RefPlaneSection } from './RefPlaneSection';
import { COMMAND, DEFAULT_JUSTIFY, JUSTIFY, frontViewId, makesNative, nativeArgs, shapeFits, solidArgs, threePoints, viewOf, type ThickChoice, type ThickOp } from './thick';
import { cadLog, registerCommand, startCommand, type Step } from '../../viewport/cad/session';
import { useCamera, type Vec3 } from '../../viewport/camera';
import { XYZ, beginAxisDrag, middleOf } from '../../viewport/axisDrag';
import { beginFaceSketch, frameOf, roundOnFace, snapPoints, type FaceShape } from '../../viewport/faceSketch';
import { startDirect, type DirectTool } from '../../viewport/sketch/direct';
import { finishSketch, lookAtPlane, setWorkPlane, useWorkPlane } from '../../viewport/sketch/sketch3d';
import { frameOfView } from '../../viewport/workPlane';
import { cancel as cancelCommand } from '../../viewport/cad/session';
import { toggleSketchupKeys, useDisplay } from '../../app/display';
import './familyLibrary';
import { FamiliesDialog } from './families/FamiliesDialog';
import { FAMILY_CLASS, FamilyFromSketchDialog, FamilySection, placeFamilyTool, startFamilyFromSketch } from './families/FamilyTools';

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
    prompt: `${prompt} <${formatLength(last.height)}>`, want: 'none',
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
        return heightStep((h) => run('architecture.wallsAlong', nativeArgs(shape, view.storeyId ?? activeStorey()?.id ?? null, typeId, choice(last.thickness, h)), 'Walls laid along the shape, joined at its corners.'), `Wall height (${unitName()})`);
      }
      return {
        prompt: `Thickness (${unitName()}) <${formatLength(last.thickness)}>`, want: 'none',
        value: (t) => { if (t > 0) last.thickness = t; return heightStep((h) => run(COMMAND[op], solidArgs(hit.id, choice(last.thickness, h)), 'Made: it follows the shape and its plane.'), `Height (${unitName()}; negative: away from you)`); },
        enter: () => heightStep((h) => run(COMMAND[op], solidArgs(hit.id, choice(last.thickness, h)), 'Made: it follows the shape and its plane.'), `Height (${unitName()}; negative: away from you)`),
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
  const viewId = frontViewId(useUi.getState(), activeStorey()?.id ?? null, useSession.getState().nodes);
  if (!viewId) { cadLog('Open a plan, a section, an elevation or a reference plane first.'); return null; }
  const make = async (offset: number) => { openPlane(await execute('view.create', { kind: 'plane', fromView: viewId, offset })); return null; };
  return { prompt: `Offset from this view's plane (${unitName()}; negative: away from you) <0>`, want: 'none', value: make, enter: () => make(0) };
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
let threePicked: number[][] = [];
const planeBy3: ToolDef = {
  id: 'studio.planeBy3', label: 'Plane by 3 points', icon: Frame,
  gesture: { kind: 'face', types: ['*'] },
  complete: async ({ face }) => {
    if (!face || face.point.length < 3) { notify('Pick the points in the 3D view, on the model.'); return; }
    threePicked = [...threePicked, face.point].slice(-3);
    if (threePicked.length < 3) { notify(threePicked.length === 1 ? 'First point threePicked: pick the second.' : 'Second point threePicked: pick the third.'); return; }
    const problem = threePoints(threePicked);
    const pts = threePicked;
    threePicked = [];
    if (problem) { notify(problem); return; }
    openPlane(await execute('view.create', { kind: 'plane', points: pts }));
  },
};

/** A solid just made from a sketch, selected: it lights up (and shows its sketch's grips), so one sees at once what came of it. */
const selectMade = (r: unknown) => {
  const id = (r as { id?: string } | null)?.id;
  if (id) useUi.setState({ selected: [id], selection: id });
  return r;
};

const CLASSES = ['PROXY', 'MEMBER', 'PLATE', 'COVERING', 'RAILING', 'STAIR', 'RAMP', 'CHIMNEY', 'FURNITURE', 'WALL', 'SLAB', 'COLUMN', 'BEAM', 'ROOF', 'FOOTING'];

/** The elements selected that are not drafted shapes (what an extrusion made to cut cuts). */
const selectedElements = (): string[] => {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => { const n = nodes.get(id); return n && n.props.predefinedType !== 'DRAFT' && n.storeyId; });
};

/**
 * EXTRUDE: pick a closed outline, then a depth. At any step a class word (SLAB, WALL…), Symmetric (both ways
 * from the sketch), Taper (the sides lean in by an angle), Cut (the solid cuts the elements selected before
 * EXTRUDE and is not drawn itself).
 */
function extrude(): Step {
  let cls = 'PROXY';
  let symmetric = false;
  let taper = 0;
  const cut = selectedElements();
  let cutting = false;
  const opts = () => [`class ${cls}`, symmetric ? 'symmetric' : null, taper ? `taper ${taper}°` : null, cutting ? `cuts ${cut.length}` : null].filter(Boolean).join(', ');
  const kw = (next: () => Step): Record<string, () => Step> => ({
    ...Object.fromEntries(CLASSES.map((c) => [c, () => { cls = c; cadLog(`Class ${c}.`); return next(); }])),
    S: () => { symmetric = !symmetric; cadLog(symmetric ? 'Both ways from the sketch.' : 'One way from the sketch.'); return next(); },
    T: () => ({ prompt: 'Taper angle (°; positive leans in)', want: 'none', scalar: (v) => { taper = v; return next(); } }),
    CUT: () => {
      if (!cut.length) { cadLog('Select the elements to cut before EXTRUDE, then Cut.'); return next(); }
      cutting = !cutting; cadLog(cutting ? `It cuts the ${cut.length} element(s) selected, and is not drawn.` : 'It is a solid of its own.'); return next();
    },
  });
  const run = (profileId: string, depth: number) => execute('studio.extrude', {
    profileId, depth, class: cls, ...(symmetric ? { symmetric: true } : {}), ...(taper ? { taper } : {}), ...(cutting ? { cut } : {}),
  }).then((r) => (cutting ? r : selectMade(r)));
  const pick = (): Step => ({
    prompt: `Pick a closed outline [${opts()}; Symmetric/Taper${cut.length ? '/Cut' : ''}]`, want: 'pick', keywords: kw(pick),
    pick: (hit) => {
      const depth = (): Step => ({
        prompt: `Depth (${unitName()}; negative: away from you) [${opts()}] <${formatLength(1)}>`, want: 'none', keywords: kw(depth),
        value: async (d) => { await run(hit.id, d); return null; },
        enter: async () => { await run(hit.id, 1); return null; },
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
 * it stretches to; TWist turns the section along the way, SCale grows it,
 * RIse lifts it (a circle that rises is a helix, TUrns times round). Corners
 * are mitred; a closed path makes one ring. With shapes selected that carry
 * roles (path, profile, end profile, rail), it runs on them at once.
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
      .then((r) => { if (r) { selectMade(r); cadLog('Swept the selected profile along the selected path.'); } return null; });
  }
  let place = 'centre';
  let gap = 0;
  let endId: string | null = null;
  let railId: string | null = null;
  const shape: Record<string, number> = {};
  const run = async (path: { id: string }, profile: { id: string }) => {
    selectMade(await execute('studio.sweep', { profileId: profile.id, pathId: path.id, place, placeOffset: gap, ...shape, ...(endId ? { endProfileId: endId } : {}), ...(railId ? { railId } : {}) }));
    return null;
  };
  const number = (key: string, prompt: string, path: { id: string }): Step => ({ prompt, want: 'none', scalar: (v) => { shape[key] = v; return pickProfile(path); } });
  const length = (key: string, prompt: string, path: { id: string }): Step => ({ prompt, want: 'none', value: (v) => { shape[key] = v; return pickProfile(path); } });
  const extras = () => Object.entries(shape).map(([k, v]) => `${k} ${k === 'rise' ? lengthText(v) : v}`).join(', ');
  const pickProfile = (path: { id: string }): Step => ({
    prompt: `Pick the closed profile [Centre/Right/Left/Outside/Inside: ${place}; gap ${lengthText(gap)}; End profile${endId ? ' ✓' : ''}; RAil${railId ? ' ✓' : ''}; TWist/SCale/RIse/TUrns${extras() ? ': ' + extras() : ''}]`, want: 'pick',
    keywords: {
      ...Object.fromEntries(Object.entries(PLACES).map(([k, v]) => [k, () => { place = v; return pickProfile(path); }])),
      E: () => ({ prompt: 'Pick the profile it ends with', want: 'pick', pick: (h) => { endId = h.id; railId = null; return pickProfile(path); } }),
      RA: () => ({ prompt: 'Pick the second rail', want: 'pick', pick: (h) => { railId = h.id; endId = null; return pickProfile(path); } }),
      TW: () => number('twist', 'Twist by the end of the path (°)', path),
      SC: () => number('scaleEnd', 'Scale at the end (1: as drawn)', path),
      RI: () => length('rise', `Rise by the end of the path (${unitName()})`, path),
      TU: () => number('turns', 'Turns round a closed path that rises', path),
    },
    value: (v) => { gap = v; return pickProfile(path); },
    pick: (profile) => run(path, profile),
  });
  return { prompt: 'Pick the path (a line, polyline, arc or circle)', want: 'pick', pick: (path) => pickProfile(path) };
}

/**
 * LOFT: through the closed shapes selected, in the order they were selected (each on its own view: a plan of
 * another level, a section further on); a shape selected with the role rail bends it as a guide. Enter lofts
 * straight from one profile to the next, Smooth on a curve through them.
 */
function loft(): Step | null {
  const nodes = useSession.getState().nodes;
  const sel = useUi.getState().selected.filter((id) => nodes.get(id)?.props.predefinedType === 'DRAFT');
  const rail = sel.find((id) => nodes.get(id)?.props.role === 'rail');
  const ids = sel.filter((id) => id !== rail);
  if (ids.length < 2) { cadLog('Select two closed shapes at least, one on each view (a plan of each level, or sections), in order; a line with the role rail bends it; then LOFT.'); return null; }
  const run = (smooth: boolean) => execute('studio.loft', { profileId: ids[0], profileIds: ids, ...(rail ? { railId: rail } : {}), ...(smooth ? { smooth: true } : {}) })
    .then((r) => { if (r) selectMade(r); if (r) cadLog(`Lofted through ${ids.length} profiles${rail ? ' along the guide' : ''}${smooth ? ', smooth' : ''}.`); return null; });
  return { prompt: `Loft through ${ids.length} profiles${rail ? ' along the guide' : ''} [Smooth] <straight>`, want: 'none', keywords: { S: () => run(true) }, enter: () => run(false) };
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
        scalar: async (a) => { selectMade(await execute('studio.revolve', { profileId: profile.id, axisId: axis.id, angle: a })); return null; },
        enter: async () => { selectMade(await execute('studio.revolve', { profileId: profile.id, axisId: axis.id, angle: 360 })); return null; },
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
const isSolid = (n?: NodeDto) => !!n && SOLID_CLASSES.includes(n.type) && typeof n.props.operation === 'string';
/** The Studio solids selected. */
const selectedSolids = (): string[] => {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => isSolid(nodes.get(id)));
};

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
  const sweepAngle = (((sh.end - sh.start) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) || 2 * Math.PI;
  return [{ points: Array.from({ length: 25 }, (_, i) => ({ x: sh.c.x + Math.cos(sh.start + (sweepAngle * i) / 24) * sh.r, y: sh.c.y + Math.sin(sh.start + (sweepAngle * i) / 24) * sh.r })), closed: false, kind: 'sketch' as const, view }];
}) : []);

// ─── Tools on faces and edges, in 3D ───────────────────────────────────────

/** A face pick for the engine: the element, the point and the normal of the triangle hit (3D only). */
function picked(input: ToolInput): { id: string; point: number[]; normal: number[] } | null {
  const { entity, face } = input;
  if (!entity || !face?.normal) { notify('Pick the face in a 3D view: Studio works on the faces the body has.'); return null; }
  return { id: entity.id, point: face.point, normal: face.normal };
}
const faceGesture = (types: string[] = ['*']): ToolDef['gesture'] => ({ kind: 'face', types, query: 'studio.faceAt' });

const v3 = (a: number[]): Vec3 => ({ x: a[0], y: a[1], z: a[2] });
/** The flat face under a pick, as Studio names it (its plane's normal and its outline), or null with a word why. */
async function flatFace(p: { id: string; point: number[]; normal: number[] }): Promise<{ normal: Vec3; outline: Vec3[] } | null> {
  const face = await query<{ normal: number[]; outline: number[][] }>('studio.faceAt', p);
  if (!face || face.outline.length < 3) { notify('There is no flat face there.'); return null; }
  return { normal: v3(face.normal), outline: face.outline.map(v3) };
}

const PUSHED: Record<string, string> = {
  depth: 'The extrusion is deeper or shallower: its depth changed.',
  sketch: 'The side moved: the sketch\'s edge under it went with it.',
  step: 'Done as a step of the solid: it is in its Studio steps, and redone when the sketch changes.',
  solid: 'A solid stands on the face, in its material: edit its distance in its properties.',
  cut: 'The face is pushed in: a cut the element is less, kept live.',
};

/**
 * Push/pull: the face picked is taken by the mouse along its normal, shown as it will come out (green where
 * it adds, red where it takes away) with its distance beside it, which can be typed; a click or Enter applies.
 */
const pushPull: ToolDef = {
  id: 'studio.pushPull', label: 'Push/Pull', icon: ArrowUpDown, shortcut: 'PU',
  gesture: faceGesture(),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const face = await flatFace(p);
    if (!face) return;
    beginAxisDrag({
      title: 'Push/Pull', at: middleOf(face.outline), axes: [{ dir: face.normal }], preview: { kind: 'prism', outline: face.outline },
      hint: 'Out adds, in takes away',
      commit: async (d) => {
        const r = await execute('studio.pushPull', { ...p, distance: d }) as { how?: string } | null;
        if (r) notify(PUSHED[r.how ?? ''] ?? 'Pushed or pulled.');
      },
    });
  },
};

const ON_FACE: Record<string, string> = {
  solid: 'A solid stands on the face, from the shape drawn on it: Push/Pull its end, or change the sketch, and it follows.',
  cut: 'Cut into the face, from the shape drawn on it: Push/Pull the pocket\'s floor to make it deeper or shallower.',
};

/**
 * A rectangle, a circle or a polygon drawn right on a face in 3D (decision 272): the click on the face is its first point,
 * the shape follows the mouse on the face; done, it is pushed or pulled at once (out a solid on the face, in a cut into it),
 * and Esc or 0 leaves it on the face as a sketch, to extrude later.
 */
const onFace = (shape: FaceShape, label: string, icon: ToolDef['icon'], shortcut: string): ToolDef => ({
  id: `studio.${shape}OnFace`, label, icon, shortcut,
  gesture: faceGesture(),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const face = await flatFace(p);
    if (!face) return;
    const frame = frameOf(face.normal, face.outline);
    // The click is the first point: on the face's plane, at a corner or a middle close by, else to the centimetre.
    const at = v3(p.point);
    const reach = 0.03 * Math.max(...face.outline.map((q) => Math.hypot(q.x - at.x, q.y - at.y, q.z - at.z)), 0.1);
    const near = snapPoints(face.outline).find((s) => Math.hypot(s.at.x - at.x, s.at.y - at.y, s.at.z - at.z) < reach);
    beginFaceSketch({
      shape, pick: p, frame, outline: face.outline,
      done: (drawn) => {
        const args = 'center' in drawn
          ? { ...p, circle: { center: [drawn.center.x, drawn.center.y, drawn.center.z], radius: drawn.radius } }
          : { ...p, outline: drawn.outline.map((q) => [q.x, q.y, q.z]) };
        const keep = async () => { if (await execute('studio.onFace', args)) notify('The shape stays on the face as a sketch: extrude it later, or draw over it.'); };
        beginAxisDrag({
          title: 'Push/Pull', at: middleOf(drawn.outline), axes: [{ dir: face.normal }], preview: { kind: 'prism', outline: drawn.outline },
          hint: 'Out adds, in takes away', zero: true,
          cancel: () => void keep(),
          commit: async (d) => {
            if (Math.abs(d) < 1e-9) { await keep(); return; }
            const r = await execute('studio.onFace', { ...args, distance: d }) as { how?: string; id?: string } | null;
            if (!r) return;
            if (r.how === 'solid') selectMade(r);
            notify(ON_FACE[r.how ?? ''] ?? 'Done.');
          },
        });
      },
    }, near?.at ?? roundOnFace(frame, at));
  },
});

// ─── The 3D sketch (docs/plan-studio-sketch.md, decision 374) ──────────────

/**
 * Line, Rectangle, Circle: with a work plane active (Sketch on face), the command line's own command on it (the CAD
 * way); otherwise the direct tool, SketchUp's, drawing right on the face under the cursor (in the 3D view).
 */
const sketchTool = (direct: DirectTool, command: string, label: string, icon: ToolDef['icon'], shortcut: string): ToolDef => ({
  id: `studio.sketch.${direct}`, label, icon, shortcut,
  run: () => {
    if (useWorkPlane.getState().plane) { void startCommand(command); return; }
    useUi.setState({ view: '3d', tool: null });
    cancelCommand(true);
    startDirect(direct);
  },
});

/** A direct tool with no command line twin: the tape measure. */
const directTool = (direct: DirectTool, label: string, icon: ToolDef['icon'], shortcut: string): ToolDef => ({
  id: `studio.sketch.${direct}`, label, icon, shortcut,
  run: () => { useUi.setState({ view: '3d', tool: null }); cancelCommand(true); startDirect(direct); },
});

/** Erase guides: the tape measure's guide lines and points, all at once (one step). */
const eraseGuides: ToolDef = {
  id: 'studio.eraseGuides', label: 'Erase guides', icon: Eraser,
  run: () => { void execute('studio.clearGuides', {}).then((r) => { const n = (r as { removed?: number } | null)?.removed ?? 0; notify(n ? `${n} guide(s) erased.` : 'There are no guides.'); }); },
};

/**
 * Follow Me (stage C, decision 376): click the face to sweep (or a region of it), then the path: a drafted line, polyline,
 * arc or circle, or a face whose edges it follows round. One Studio solid, following both sketches.
 */
let followFrom: { id: string; point: number[]; normal: number[] } | null = null;
const followMe: ToolDef = {
  id: 'studio.followMe', label: 'Follow Me', icon: Spline, shortcut: 'FM',
  gesture: { kind: 'face', types: ['*'], query: 'studio.faceAt' },
  complete: async (input) => {
    const { entity, face } = input;
    if (!followFrom) {
      const p = picked(input);
      if (!p) return;
      followFrom = p;
      notify('Now the path: a drafted line (or polyline, arc, circle), or a face whose edges it follows round.');
      return;
    }
    const profile = followFrom;
    followFrom = null;
    const path = entity?.type === 'IfcAnnotation' ? { id: entity.id }
      : entity && face?.normal ? { face: { id: entity.id, point: face.point, normal: face.normal } } : null;
    if (!path) { notify('Pick the path in the 3D view: a drafted line or a face.'); return; }
    const r = await execute('studio.followMe', { profile, path });
    if (r) { selectMade(r); notify('Swept along the path: change the face\'s sketch or the path, and it follows.'); }
  },
};

/** SketchUp's one-letter shortcuts in the 3D view, switched on or off (kept for this computer). */
const sketchupKeys: ToolDef = {
  id: 'studio.sketchupKeys', label: 'SketchUp shortcuts', icon: Keyboard,
  run: () => { toggleSketchupKeys(); notify(useDisplay.getState().sketchupKeys ? 'SketchUp shortcuts on in the 3D view: L line, R rectangle, C circle, A arc, P push/pull, F offset, T tape, M move, E erase, Space select.' : 'SketchUp shortcuts off: the two-letter shortcuts only.'); },
};

/** A command line edit on the sketch's work plane in 3D (or the storey's level when none is active): TRIM, EXTEND, FILLET. */
const sketchCad = (id: string, command: string, label: string, icon: ToolDef['icon']): ToolDef => ({
  id: `studio.sketch.${id}`, label, icon,
  run: () => { useUi.setState({ view: '3d', tool: null }); void startCommand(command); },
});

/** Sketch on face: the face's plane (the same one the direct tools store on) made the active work plane, lit. */
const sketchOnFace: ToolDef = {
  id: 'studio.sketchOnFace', label: 'Sketch on face', icon: SquarePen, shortcut: 'KF',
  gesture: faceGesture(),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const face = await flatFace(p);
    if (!face) return;
    const r = await execute('studio.onFace', p) as { planeId?: string; name?: string } | null;
    if (!r?.planeId) return;
    useUi.setState({ tool: null });
    setWorkPlane({ planeId: r.planeId, name: r.name ?? 'Face', pick: p, outline: face.outline });
    notify('Sketching on the face: draw with Line, Rectangle, Circle or Arc (or the command line), then Finish sketch.');
  },
};

/** The reference plane selected made the active work plane (a plane of its own, not a face's). */
const sketchOnPlane: ToolDef = {
  id: 'studio.sketchOnPlane', label: 'Sketch on plane', icon: SquareSquare,
  run: () => {
    const sel = useUi.getState().selection;
    const n = sel ? useSession.getState().nodes.get(sel) : undefined;
    if (n?.type !== 'LapinView' || n.props.kind !== 'plane') { notify('Select a reference plane first (in the 3D view or the browser), or use Sketch on face.'); return; }
    useUi.setState({ view: '3d' });
    setWorkPlane({ planeId: n.id, name: n.name });
  },
};

/** The active work plane's frame, or the selected reference plane's. */
const activePlane = () => {
  const nodes = useSession.getState().nodes;
  const wp = useWorkPlane.getState().plane;
  const sel = useUi.getState().selection;
  const id = wp?.planeId ?? (sel && nodes.get(sel)?.props.kind === 'plane' ? sel : null);
  return id ? { frame: frameOfView(id, nodes), outline: wp?.outline } : null;
};

const finishSketchTool: ToolDef = {
  id: 'studio.finishSketch', label: 'Finish sketch', icon: CircleCheckBig, shortcut: 'KE',
  run: () => {
    const wp = useWorkPlane.getState().plane;
    if (!wp) { notify('No sketch is open: Sketch on face starts one.'); return; }
    const frame = frameOfView(wp.planeId, useSession.getState().nodes);
    void finishSketch(frame, wp.pick ? null : activeStorey()?.id ?? null, () => cancelCommand(true));
  },
};

const lookAtPlaneTool: ToolDef = {
  id: 'studio.lookAtPlane', label: 'Look at plane', icon: ScanEye, shortcut: 'KP',
  run: () => {
    const a = activePlane();
    if (!a?.frame) { notify('Sketch on a face, or select a reference plane, to look at it.'); return; }
    useUi.setState({ view: '3d' });
    lookAtPlane(a.frame.normal, a.outline?.length ? middleOf(a.outline) : a.frame.origin);
  },
};


/** Chamfer or fillet: the edge nearest the pick on the face picked (Shift: every edge round that face). */
const roundTool = (op: 'chamfer' | 'fillet'): ToolDef => ({
  id: `studio.${op}`, label: op === 'chamfer' ? 'Chamfer edge' : 'Fillet edge', icon: op === 'chamfer' ? Octagon : SquareRoundCorner, shortcut: op === 'chamfer' ? 'CE' : 'FE',
  gesture: faceGesture(SOLID_CLASSES),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const n = useSession.getState().nodes.get(p.id);
    if (!isSolid(n)) { notify('Chamfers and fillets go on Studio solids: make the element one first (Make Studio solid).'); return; }
    const r = await ask(op === 'chamfer' ? 'Chamfer' : 'Fillet', [
      lengthField('size', op === 'chamfer' ? 'Distance' : 'Radius', 0.05),
      choiceField('which', 'Edges', 'edge', [{ value: 'edge', label: 'the edge picked' }, { value: 'face', label: 'all round the face' }]),
    ]);
    if (!r || !(Number(r.size) > 0)) return;
    await execute('studio.addFeature', { id: p.id, feature: { op, [op === 'chamfer' ? 'distance' : 'radius']: r.size, point: p.point, normal: p.normal, ...(r.which === 'face' ? { face: true } : {}) } });
  },
});

/** Split or slice by a plane parallel to the face picked: the plane is dragged into the solid (or typed), shown where it cuts. */
const splitTool = (keep: 'split' | 'slice'): ToolDef => ({
  id: `studio.${keep}`, label: keep === 'split' ? 'Split' : 'Slice', icon: keep === 'split' ? Scissors : Slice,
  gesture: faceGesture(),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const target = selectedSolids()[0] ?? (isSolid(useSession.getState().nodes.get(p.id)) ? p.id : null);
    if (!target) { notify('Select the Studio solid first, then pick the face whose plane cuts it (a face of the solid itself, or of any element).'); return; }
    const face = await flatFace(p);
    if (!face) return;
    const into = { x: -face.normal.x, y: -face.normal.y, z: -face.normal.z };
    beginAxisDrag({
      title: keep === 'split' ? 'Split' : 'Slice', at: middleOf(face.outline), axes: [{ dir: into }], preview: { kind: 'plane', outline: face.outline },
      start: target === p.id ? 0.5 : 0, zero: true, hint: 'The plane, into the face',
      ...(keep === 'slice' ? { options: { label: 'Keep', value: 'back', values: [{ value: 'back', label: 'Keep behind' }, { value: 'front', label: 'Keep in front' }] } } : {}),
      commit: async (d, _axis, side) => {
        const point = [face.outline[0].x + into.x * d, face.outline[0].y + into.y * d, face.outline[0].z + into.z * d];
        const normal = [face.normal.x, face.normal.y, face.normal.z];
        if (keep === 'split') {
          const s = await execute('studio.split', { id: target, point, normal }) as { ids?: string[] } | null;
          if (s?.ids) { useUi.setState({ selected: s.ids, selection: s.ids[1] }); notify('Split in two: both keep the sketch, so changing it changes both.'); }
        } else await execute('studio.addFeature', { id: target, feature: { op: 'slice', point, normal, keep: side ?? 'back' } });
      },
    });
  },
});

/** Turned in 3D about the edge picked. */
const rotate3d: ToolDef = {
  id: 'studio.rotate3d', label: 'Rotate 3D', icon: Rotate3d,
  gesture: faceGesture(SOLID_CLASSES),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const r = await ask('Rotate about the edge picked', [numberField('angle', 'Angle (°)', 90)]);
    if (!r) return;
    await execute('studio.addFeature', { id: p.id, feature: { op: 'rotate', angle: r.angle, point: p.point, normal: p.normal } });
  },
};

/** Mirrored in the plane of the face picked (of the solid, or of any element). */
const mirror3d: ToolDef = {
  id: 'studio.mirror3d', label: 'Mirror 3D', icon: FlipHorizontal2,
  gesture: faceGesture(),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const target = selectedSolids()[0] ?? (isSolid(useSession.getState().nodes.get(p.id)) ? p.id : null);
    if (!target) { notify('Select the Studio solid first, then pick the face it is mirrored in.'); return; }
    await execute('studio.addFeature', { id: target, feature: { op: 'mirror', point: p.point, normal: p.normal, onId: p.id } });
  },
};

/** The selected Studio solids, each a step: a shell, an array, a move. */
const onSolids = (label: string, run: (ids: string[]) => Promise<void>): (() => void) => () => {
  const ids = selectedSolids();
  if (!ids.length) { notify(`Select the Studio solids first, then ${label}.`); return; }
  void run(ids);
};

const shell: ToolDef = {
  id: 'studio.shell', label: 'Shell', icon: PackageOpen,
  run: onSolids('Shell', async (ids) => {
    const r = await ask('Shell', [
      lengthField('t', 'Wall thickness', 0.05),
      choiceField('open', 'Left open', 'end', [{ value: 'end', label: 'at its end (the top of one drawn in plan)' }, { value: 'start', label: 'at its sketch' }, { value: 'both', label: 'at both ends' }, { value: 'none', label: 'nowhere (closed)' }]),
    ]);
    if (r && Number(r.t) > 0) await execute('studio.addFeature', { ids, feature: { op: 'shell', thickness: r.t, open: r.open } });
  }),
};

/**
 * The selection split: the Studio solids (which take a step of their own) and everything else Move moves (a reference, an
 * imported mesh's furniture, a group, a wall), which an array copies and Move 3D moves as elements (decision 448).
 */
const solidsAndOthers = (): { solids: string[]; others: string[] } => {
  const nodes = useSession.getState().nodes;
  const selected = useUi.getState().selected;
  return {
    solids: selected.filter((id) => isSolid(nodes.get(id))),
    others: selected.filter((id) => { const n = nodes.get(id); return !!n && !isSolid(n) && (!!n.storeyId || n.type === 'IfcGroup') && n.props.predefinedType !== 'DRAFT'; }),
  };
};

const array: ToolDef = {
  id: 'studio.array', label: 'Array', icon: Grid3x3,
  run: () => {
    const { solids, others } = solidsAndOthers();
    if (!solids.length && !others.length) { notify('Select what to array first, then Array.'); return; }
    void (async () => {
      const r = await ask('Array in a row', [numberField('count', 'Copies (with the first)', 3), lengthField('dx', 'Step x', 1), lengthField('dy', 'Step y', 0), lengthField('dz', 'Step z', 0)]);
      if (!r) return;
      if (solids.length) await execute('studio.addFeature', { ids: solids, feature: { op: 'array', count: r.count, step: [r.dx, r.dy, r.dz] } });
      if (others.length) await execute('element.array', { ids: others, count: r.count, step: [r.dx, r.dy, r.dz] });
    })();
  },
};

const polarArray: ToolDef = {
  id: 'studio.polarArray', label: 'Polar array', icon: RotateCw,
  run: () => {
    const { solids, others } = solidsAndOthers();
    if (!solids.length && !others.length) { notify('Select what to array first, then Polar array.'); return; }
    void (async () => {
      const r = await ask('Array round an upright', [numberField('count', 'Copies (with the first)', 6), numberField('angle', 'Angle filled (°)', 360), lengthField('cx', 'Centre x', 0), lengthField('cy', 'Centre y', 0)]);
      if (!r) return;
      if (solids.length) await execute('studio.addFeature', { ids: solids, feature: { op: 'array', kind: 'polar', count: r.count, angle: r.angle, centre: [r.cx, r.cy, 0], axis: [0, 0, 1] } });
      if (others.length) await execute('element.array', { ids: others, kind: 'polar', count: r.count, angle: r.angle, center: [r.cx, r.cy] });
    })();
  },
};

/**
 * Move 3D: arrows along x, y and z on the selection in the 3D view, one dragged (or a distance typed) moves it as a step;
 * elsewhere the three distances asked. A Studio solid takes a move step; anything else is moved as Move moves it, its
 * height by the z part.
 */
const move3d: ToolDef = {
  id: 'studio.move3d', label: 'Move 3D', icon: Move3d,
  run: () => {
    const { solids, others } = solidsAndOthers();
    const ids = [...solids, ...others];
    if (!ids.length) { notify('Select what to move first, then Move 3D.'); return; }
    const move = async (by: number[]) => {
      if (solids.length) await execute('studio.addFeature', { ids: solids, feature: { op: 'move', by } });
      if (others.length) await execute('element.transform', { ids: others, op: 'move', by: [by[0], by[1]], up: by[2] });
    };
    void (async () => {
      const box = useUi.getState().view === '3d' ? useCamera.getState().api?.boxOf?.(ids) : null;
      if (box) {
        const at = { x: (box.min.x + box.max.x) / 2, y: (box.min.y + box.max.y) / 2, z: (box.min.z + box.max.z) / 2 };
        beginAxisDrag({
          title: 'Move 3D', at, axes: XYZ, preview: { kind: 'box', min: box.min, max: box.max },
          commit: async (d, axis) => { await move([0, 1, 2].map((k) => (k === axis ? d : 0))); },
        });
        return;
      }
      const r = await ask('Move in 3D', [lengthField('dx', 'x', 0), lengthField('dy', 'y', 0), lengthField('dz', 'z (up)', 1)]);
      if (r) await move([Number(r.dx), Number(r.dy), Number(r.dz)]);
    })();
  },
};

/** The selected elements (walls, references, imported meshes…) made into Studio solids that take steps. */
const makeSolid: ToolDef = {
  id: 'studio.fromElement', label: 'Make Studio solid', icon: Blocks,
  run: () => {
    const nodes = useSession.getState().nodes;
    const ids = useUi.getState().selected.filter((id) => { const n = nodes.get(id); return n && n.storeyId && n.props.predefinedType !== 'DRAFT' && !isSolid(n); });
    if (!ids.length) { notify('Select the elements to make into Studio solids (a reference from IFC, an imported mesh, a wall…).'); return; }
    if (!window.confirm(`Make ${ids.length} element(s) into Studio solids? Each takes the element's place, name and IFC class, its body as it is now; what made it (a wall's type and layers, a reference's file) is not kept.`)) return;
    void execute('studio.fromElement', { ids }).then((r) => { const made = (r as { ids?: string[] } | null)?.ids; if (made?.length) { useUi.setState({ selected: made, selection: made[0] }); notify(`${made.length} Studio solid(s): push/pull, fillet, shell and the rest work on them now.`); } });
  },
};

/** An extrusion run up to the face picked (of another element): it follows that face as it moves. */
const upTo: ToolDef = {
  id: 'studio.upTo', label: 'Extrude up to face', icon: ArrowUpToLine,
  gesture: faceGesture(),
  complete: async (input) => {
    const p = picked(input);
    if (!p) return;
    const nodes = useSession.getState().nodes;
    const id = takeUpTo() ?? selectedSolids().find((x) => nodes.get(x)?.props.operation === 'extrude');
    if (!id) { notify('Select the extrusion first, then pick the face it runs up to.'); return; }
    if (id === p.id) { notify('Pick a face of another element.'); return; }
    if (await execute('studio.setUpTo', { id, upTo: p })) { useUi.setState({ tool: null, selection: id, selected: [id] }); notify('The extrusion runs up to that face, and follows it.'); }
  },
};

/** A primitive placed with a click (in plan or on the ground in 3D), on the active storey; its sizes are in its properties. */
const primitive = (shape: string, label: string, icon: ToolDef['icon']): ToolDef => ({
  id: `studio.primitive.${shape}`, label, icon,
  gesture: { kind: 'point' },
  complete: async ({ points }, ctx) => {
    const at = points[0];
    if (!at) return;
    const r = await ctx.execute('studio.primitive', { shape, at: [at.x, at.y], storeyId: ctx.storeyId || activeStorey()?.id }) as { id?: string } | null;
    if (r?.id) useUi.setState({ selection: r.id, selected: [r.id] });
  },
});

/** Families of the user's own (docs/plan-user-extensibility.md §5.6): the place tool, the dialog with the editor, and one made from drafted outlines. */
const familiesDialogTool: ToolDef = { id: 'studio.families', label: 'Families…', icon: Boxes, run: () => useUi.setState({ dialog: 'families' }) };
const familyFromSketch: ToolDef = { id: 'studio.familyFromSketch', label: 'Family from sketch…', icon: PackagePlus, run: startFamilyFromSketch };

const studio: LapinModule = {
  id: 'studio',
  name: 'Studio',
  summary: 'Free-form solids: sketches on any face, extrude, sweep, loft, revolve, primitives, push/pull, fillets, shells, arrays and live booleans.',
  tabs: [
    // Beside Save as symbol: closed drafting made a family.
    { id: 'draft', label: 'Draft', order: 10, groups: [{ label: 'Family', tools: [familyFromSketch] }] },
    {
    id: 'studio', label: 'Studio', order: 50,
    groups: [
      { label: 'Solids', tools: [cmd('extrude', 'Extrude', Box, 'EXTRUDE'), cmd('sweep', 'Sweep', Waypoints, 'SWEEP'), cmd('revolve', 'Revolve', RefreshCcw, 'REVOLVE'), cmd('loft', 'Loft', Layers2, 'LOFT'), { id: 'studio.editSketch', label: 'Edit sketch', icon: PenTool, run: () => void editSketch() }] },
      { label: 'Thick', tools: [cmd('ring', 'Ring', Cylinder, 'RING'), cmd('box', 'Box', Container, 'BOX'), cmd('wallStrip', 'Wall strip', BrickWall, 'TWALL')] },
      {
        label: 'Sketch',
        tools: [sketchTool('line', 'LINE', 'Line', Slash, 'KL'), sketchTool('rect', 'RECTANGLE', 'Rectangle', RectangleHorizontal, 'KR'), sketchTool('circle', 'CIRCLE', 'Circle', Circle, 'KC'),
          // Two arcs, each one thing whatever the work plane (decision 377): SketchUp's by its chord and bulge, CAD's ARC through three points.
          directTool('arc', '2-point arc', Rainbow, 'KA'),
          { id: 'studio.sketch.arc3', label: '3-point arc', icon: Redo2, run: () => { useUi.setState({ view: '3d', tool: null }); void startCommand('ARC'); } },
          sketchTool('offset', 'OFFSET', 'Offset', CopyPlus, 'KO')],
        small: [sketchOnFace, sketchOnPlane, finishSketchTool, lookAtPlaneTool, sketchCad('trim', 'TRIM', 'Trim', Scissors), sketchCad('extend', 'EXTEND', 'Extend', ArrowRightToLine), sketchCad('fillet', 'FILLET', 'Fillet', CornerUpRight),
          directTool('tape', 'Tape measure', Ruler, 'KM'), eraseGuides, followMe, sketchupKeys],
      },
      { label: 'On face', tools: [onFace('rect', 'Rectangle on face', RectangleHorizontal, 'FRE'), onFace('circle', 'Circle on face', Circle, 'FCI'), onFace('polygon', 'Polygon on face', Hexagon, 'FPL')] },
      { label: 'Work planes', tools: [cmd('refPlane', 'Plane from view', SquareStack, 'REFPLANE'), planeOnFace, planeBy3] },
      {
        label: 'Primitives',
        tools: [primitive('box', 'Cuboid', Box), primitive('cylinder', 'Cylinder', Cylinder)],
        small: [primitive('sphere', 'Sphere', CircleDot), primitive('cone', 'Cone', Cone), primitive('torus', 'Torus', Torus), primitive('wedge', 'Wedge', TriangleRight), primitive('pyramid', 'Pyramid', Pyramid)],
      },
      { label: 'Families', tools: [placeFamilyTool, familiesDialogTool], small: [familyFromSketch] },
      {
        label: 'Modify',
        tools: [pushPull, roundTool('fillet'), roundTool('chamfer'), shell],
        small: [upTo, splitTool('split'), splitTool('slice'), array, polarArray, rotate3d, mirror3d, move3d, makeSolid],
      },
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
  dialogs: { families: FamiliesDialog, familyFromSketch: FamilyFromSketchDialog },
  propertySections: [
    { id: 'studioFamily', label: 'Family', classes: [FAMILY_CLASS], component: FamilySection },
    { id: 'studioFeature', label: 'Studio', classes: SOLID_CLASSES, component: FeatureCard, fieldKeys: featureFieldKeys },
    { id: 'studioRefPlane', label: 'Reference plane', classes: ['LapinView'], component: RefPlaneSection },
    // Any element some solid cuts, unites or intersects: those solids listed, selectable even when never drawn.
    { id: 'studioOperations', label: 'Boolean', classes: ['*'], shows: hasTools, component: OperationsCard },
  ],
  grips: Object.fromEntries(SOLID_CLASSES.map((c) => [c, solidGrips])),
  references: Object.fromEntries(SOLID_CLASSES.map((c) => [c, solidRefs])),
};

export default studio;
