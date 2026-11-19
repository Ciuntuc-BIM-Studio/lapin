/**
 * Modify → Array (decision 448): copies of the selection in a row (a step along x, y and z), in a grid (columns × rows,
 * and levels; forum 90), round a centre (an angle filled) or along a path, any element alike: a wall, a column, a group,
 * an imported mesh's chair, and drafting (forum 90): a drafted shape, a text or a symbol on a plan, a section, an elevation
 * or a drafting view, in that view's own x and y. A row's or a grid's step is typed, or clicked in the view (ARRAYSTEP: two
 * points, a typed distance along the cursor's direction, the copies drawn live); a polar array's centre is typed or clicked
 * (ARRAYCENTRE, the copies drawn round the pointer). The dialog shows only the method's own fields and says by its field
 * what is wrong (forum 90; arrayDialog.ts). One undo step (element.array). ARRAY on the command line, and the Draft tab's
 * Modify panel, start it too.
 */
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ask, choiceField, lengthField, numberField, type Field } from '../../shell/AskBox';
import { cadLog, moved, registerCommand, shapesOf, startCommand, useCad, type Step } from '../../viewport/cad/session';
import type { Shape } from '../../viewport/cad/shapes';
import { rotatedShape } from '../../viewport/cad/rotate';
import { arrayFieldShown, checkArray, polarArgs, polarStep } from './arrayDialog';
import { arraySkipNotes, arrayTargets, gridOffsets, stepArgs, stepBetween, type ArrayPlace, type ArrayTargets, type StepJob, type V3 } from './arrayGrid';

type Vec2 = { x: number; y: number };
type PathRule = Record<string, unknown>;
interface Preview { places: { x: number; y: number; angle: number }[]; path?: [number, number][] }

/** The rule of the path array being set up (the dialog's answers), for the command line's two steps. */
let pending: { ids: string[]; rule: PathRule } | null = null;

/** The ghost of an arrangement: the path's line and a marker (a short line along the copy, a small circle) at each place. */
function ghost(p: Preview | null): Shape[] {
  if (!p) return [];
  const out: Shape[] = [];
  if (p.path && p.path.length >= 2) out.push({ type: 'polyline', pts: p.path.map(([x, y]) => ({ x, y })), closed: false });
  for (const pl of p.places) {
    const a = (pl.angle * Math.PI) / 180;
    out.push({ type: 'circle', c: { x: pl.x, y: pl.y }, r: 0.08 });
    out.push({ type: 'line', a: { x: pl.x, y: pl.y }, b: { x: pl.x + Math.cos(a) * 0.4, y: pl.y + Math.sin(a) * 0.4 } });
  }
  return out;
}

/** A live preview from the engine's own placement rule: asked at most every ~120 ms, the last answer drawn meanwhile. */
function livePreview(args: (cursor: Vec2) => PathRule | null): (cursor: Vec2) => Shape[] {
  let cache: Preview | null = null, key = '', timer: ReturnType<typeof setTimeout> | null = null, latest: PathRule | null = null;
  const fire = () => {
    timer = null;
    const a = latest; if (!a) return;
    const k = JSON.stringify(a);
    if (k === key) return;
    key = k;
    void query<Preview>('array.preview', a).then((r) => {
      cache = r;
      const c = useCad.getState().cursor;
      if (c) useCad.setState({ cursor: { ...c } });
    });
  };
  return (cursor) => {
    latest = args(cursor);
    if (!latest) { cache = null; key = ''; }
    else if (!timer) timer = setTimeout(fire, 120);
    return ghost(cache);
  };
}

/** The command line's steps of an array along a path: click the path, then the point of the selection that goes onto it. */
function pathStep(): Step {
  const job = pending;
  if (!job) return { prompt: 'Nothing to array', want: 'none', enter: () => null };
  const host = () => useCad.getState().host;
  const run = async (rule: PathRule) => {
    const r = await execute('element.array', { ids: job.ids, ...job.rule, ...rule });
    if (!r) cadLog(useSession.getState().error ?? 'The array could not be made.');
    pending = null;
    return null;
  };
  const base = (along: string): Step => ({
    prompt: 'Base point on the selection (Enter: it stays where it is)', want: 'point',
    preview: livePreview((c) => ({ ...job.rule, along, base: [c.x, c.y] })),
    point: (p) => run({ along, base: [p.x, p.y] }),
    enter: () => run({ along }),
  });
  const choose = (): Step => ({
    prompt: 'Click the path (a line, polyline, arc, wall or road)', want: 'select',
    preview: livePreview((c) => { const id = host()?.pickAny(c); return id && !job.ids.includes(id) ? { ...job.rule, along: id } : null; }),
    point: (p) => {
      const id = host()?.pickAny(p);
      if (!id || job.ids.includes(id)) { cadLog('Pick a line, polyline, arc, wall or road.'); return choose(); }
      return base(id);
    },
  });
  return choose();
}
registerCommand({ names: ['ARRAYPATH'], label: 'Array along a path', start: pathStep });

/** The row or grid being set up whose step is clicked (the dialog's answers), for ARRAYSTEP. */
let stepJob: StepJob | null = null;

/** The most copies the ghost draws (the array itself takes up to 1000). */
const GHOST_MOST = 400;

/**
 * ARRAYSTEP: the step of a row (or a grid's step between columns) from a first point to a second, then a grid's step
 * between rows from the same first point to a third; a typed distance goes along the cursor's direction (100 mm to the
 * next copy). The selection's outline is drawn at every place meanwhile.
 */
function stepStep(): Step {
  const job = stepJob;
  if (!job) return { prompt: 'Nothing to array', want: 'none', enter: () => null };
  const outline = shapesOf(job.ids);
  const ghostAt = (offsets: V3[]): Shape[] => offsets.slice(1, GHOST_MOST).flatMap((d) => outline.map((sh) => moved(sh, { x: d[0], y: d[1] })));
  const run = async (args: Record<string, unknown>) => {
    stepJob = null;
    const r = await execute('element.array', args);
    if (!r) cadLog(useSession.getState().error ?? 'The array could not be made.');
    return null;
  };
  const rowsStep = (a: { x: number; y: number }, step: { x: number; y: number }): Step => ({
    prompt: 'Third point: the step to the next row (or type the distance)', want: 'point', base: a,
    preview: (c) => {
      const row = stepBetween(a, c);
      return [{ type: 'line', a, b: { x: a.x + step.x, y: a.y + step.y } }, { type: 'line', a, b: c },
        ...ghostAt(gridOffsets(job.count, job.rows, 1, [step.x, step.y, 0], [row.x, row.y, 0]))];
    },
    point: (p) => run(stepArgs(job, step, stepBetween(a, p))),
  });
  const second = (a: { x: number; y: number }): Step => ({
    prompt: job.kind === 'grid' ? 'Second point: the step to the next column (or type the distance)' : 'Second point: the step to the next copy (or type the distance)',
    want: 'point', base: a,
    preview: (c) => {
      const d = stepBetween(a, c);
      return [{ type: 'line', a, b: c }, ...ghostAt(gridOffsets(job.count, 1, 1, [d.x, d.y, 0]))];
    },
    point: (p) => {
      const step = stepBetween(a, p);
      if (Math.hypot(step.x, step.y) < 1e-9) { cadLog('The step cannot be nothing: click a point away from the first.'); return second(a); }
      return job.kind === 'grid' && job.rows > 1 ? rowsStep(a, step) : run(stepArgs(job, step));
    },
  });
  return { prompt: 'First point of the step', want: 'point', point: (p) => second(p) };
}
registerCommand({ names: ['ARRAYSTEP'], label: 'Array by a clicked step', start: stepStep });

/** Where the array is made: the 3D view, a plan, or a section, an elevation or a drafting view (their own x and y). */
function arrayPlace(): ArrayPlace {
  if (useUi.getState().view === '3d') return '3d';
  const host = useCad.getState().host;
  return !host || host.isPlan?.() !== false ? 'plan' : 'view';
}

/**
 * What an array copies (forum 90): the selected elements and groups, and the drafted shapes, texts and symbols, by
 * element.array alike, so the copies of a drafted rectangle stay an arrangement to change in Properties as a wall's do.
 * What it cannot take where it is made is left out and said.
 */
export function arrayable(): ArrayTargets {
  return arrayTargets(useUi.getState().selected, useSession.getState().nodes, arrayPlace());
}

/** The polar array whose centre is clicked (the dialog's answers), for ARRAYCENTRE. */
let centreJob: { ids: string[]; answers: Record<string, string | number> } | null = null;

/** ARRAYCENTRE: the centre of a polar array clicked (or typed on the command line), the copies drawn round the pointer meanwhile. */
function centreStep(): Step {
  const job = centreJob;
  if (!job) return { prompt: 'Nothing to array', want: 'none', enter: () => null };
  const outline = shapesOf(job.ids);
  const count = Math.round(Number(job.answers.count)), step = polarStep(count, Number(job.answers.angle));
  return {
    prompt: 'Centre of the array (click, or type x,y)', want: 'point',
    preview: (c) => Array.from({ length: Math.min(count, GHOST_MOST) - 1 }, (_, i) => outline.map((sh) => rotatedShape(sh, c, step * (i + 1)))).flat(),
    point: async (p) => {
      centreJob = null;
      const r = await execute('element.array', polarArgs(job.ids, job.answers, p));
      if (!r) cadLog(useSession.getState().error ?? 'The array could not be made.');
      return null;
    },
  };
}
registerCommand({ names: ['ARRAYCENTRE', 'ARRAYCENTER'], label: 'Array round a clicked centre', start: centreStep });

/**
 * The dialog (forum 90): only the fields of the method chosen are shown, changing with it, and an answer that cannot make
 * an array is said by its field, the box kept open. A row's or a grid's step and a polar array's centre are typed or
 * clicked in the plan.
 */
export async function arraySelection(): Promise<void> {
  const targets = arrayable();
  const ids = targets.ids;
  const notes = arraySkipNotes(targets);
  if (!ids.length) { notify(notes[0] ?? 'Select what to array first, then Array.'); return; }
  for (const n of notes) notify(n);
  // Clicking a step, a centre or a path needs a plan (or a section, an elevation, a drafting view) open, not the 3D view.
  const inPlan = !!useCad.getState().host && arrayPlace() !== '3d';
  const fields: Field[] = [
    choiceField('kind', 'Kind', 'row', [{ value: 'row', label: 'In a row' }, { value: 'grid', label: 'In a grid (columns × rows)' }, { value: 'polar', label: 'Round a centre' }, { value: 'path', label: 'Along a path' }]),
    numberField('count', 'Copies (with the first)', 3),
    numberField('cols', 'Columns', 3), numberField('rows', 'Rows', 2), numberField('levels', 'Levels', 1),
    choiceField('by', 'Step given by', 'typed', [{ value: 'typed', label: 'Typing it (step x, y, z)' }, { value: 'click', label: 'Clicking two points in a plan' }]),
    lengthField('dx', 'Step x', 1), lengthField('dy', 'Step y', 0), lengthField('dz', 'Step z', 0),
    lengthField('rx', 'Row step x', 0), lengthField('ry', 'Row step y', 1), lengthField('lz', 'Level step z', 3),
    numberField('angle', 'Angle filled (°)', 360),
    choiceField('centreBy', 'Centre given by', inPlan ? 'click' : 'typed', [{ value: 'click', label: 'Clicking it in a plan' }, { value: 'typed', label: 'Typing it (centre x, y)' }]),
    lengthField('cx', 'Centre x', 0), lengthField('cy', 'Centre y', 0),
    lengthField('spacing', 'Spacing along the path (0: share out by the copies)', 0),
    lengthField('start', 'Start distance', 0), lengthField('end', 'End distance', 0), lengthField('offset', 'Offset from the path (+ left)', 0),
    choiceField('orient', 'Orientation', 'tangent', [{ value: 'tangent', label: 'Along the path' }, { value: 'fixed', label: 'Fixed' }]),
    choiceField('flip', 'Turn round', 'no', [{ value: 'no', label: 'No' }, { value: 'yes', label: 'Yes' }]),
  ].map((f) => ({ ...f, show: (v: Record<string, string>) => arrayFieldShown(f.key, v) }));
  const r = await ask('Array', fields, { check: checkArray });
  if (!r) return;
  if (r.kind === 'path') {
    const count = Number(r.count), spacing = Number(r.spacing);
    const rule: PathRule = { kind: 'path', orient: r.orient, flip: r.flip === 'yes', start: Number(r.start), end: Number(r.end), offset: Number(r.offset) };
    if (count >= 1) rule.count = count;
    if (spacing > 0) rule.spacing = spacing;
    if (!inPlan) { notify('Open a plan to click the path in.'); return; }
    pending = { ids, rule };
    await startCommand('ARRAYPATH');
    return;
  }
  if (r.kind === 'polar') {
    if (r.centreBy === 'click') {
      if (!inPlan) { notify('Open a plan to click the centre in.'); return; }
      centreJob = { ids, answers: r };
      await startCommand('ARRAYCENTRE');
      return;
    }
    await execute('element.array', polarArgs(ids, r, { x: Number(r.cx), y: Number(r.cy) }));
    return;
  }
  const job: StepJob = {
    ids, kind: r.kind === 'grid' ? 'grid' : 'row', count: Math.round(Number(r.kind === 'grid' ? r.cols : r.count)), rows: r.kind === 'grid' ? Math.max(1, Math.round(Number(r.rows))) : 1,
    levels: r.kind === 'grid' ? Math.max(1, Math.round(Number(r.levels))) : 1, dz: Number(r.dz), levelStep: [0, 0, Number(r.lz)],
  };
  if (r.by === 'click') {
    if (!inPlan) { notify('Open a plan to click the step in.'); return; }
    stepJob = job;
    await startCommand('ARRAYSTEP');
    return;
  }
  await execute('element.array', stepArgs(job, { x: Number(r.dx), y: Number(r.dy) }, { x: Number(r.rx), y: Number(r.ry) }));
}

// The command line's (and the Draft tab's) Array: the dialog, on the selection.
registerCommand({ names: ['ARRAY'], label: 'Array', start: () => { void arraySelection(); return null; } });
