/**
 * Modify → Array (decision 448): copies of the selection in a row (a step along x, y and z) or round a centre (an angle
 * filled), any element alike: a wall, a column, a group, an imported mesh's chair. One undo step (element.array).
 */
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ask, choiceField, lengthField, numberField } from '../../shell/AskBox';
import { cadLog, registerCommand, startCommand, useCad, type Step } from '../../viewport/cad/session';
import type { Shape } from '../../viewport/cad/shapes';

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

/** What an array copies: the selected elements and groups, not a drafted shape (its view's tools copy those). */
export function arrayable(): string[] {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => {
    const n = nodes.get(id);
    return !!n && (!!n.storeyId || n.type === 'IfcGroup') && n.props.predefinedType !== 'DRAFT';
  });
}

export async function arraySelection(): Promise<void> {
  const ids = arrayable();
  if (!ids.length) { notify('Select what to array first, then Array.'); return; }
  const r = await ask('Array', [
    choiceField('kind', 'Kind', 'row', [{ value: 'row', label: 'In a row' }, { value: 'polar', label: 'Round a centre' }, { value: 'path', label: 'Along a path' }]),
    numberField('count', 'Copies (with the first)', 3),
    lengthField('dx', 'Step x', 1), lengthField('dy', 'Step y', 0), lengthField('dz', 'Step z', 0),
    numberField('angle', 'Angle filled (°)', 360), lengthField('cx', 'Centre x', 0), lengthField('cy', 'Centre y', 0),
    lengthField('spacing', 'Spacing along the path (0: share out by the copies)', 0),
    lengthField('start', 'Start distance', 0), lengthField('end', 'End distance', 0), lengthField('offset', 'Offset from the path (+ left)', 0),
    choiceField('orient', 'Orientation', 'tangent', [{ value: 'tangent', label: 'Along the path' }, { value: 'fixed', label: 'Fixed' }]),
    choiceField('flip', 'Turn round', 'no', [{ value: 'no', label: 'No' }, { value: 'yes', label: 'Yes' }]),
  ]);
  if (!r) return;
  if (r.kind === 'path') {
    const count = Number(r.count), spacing = Number(r.spacing);
    if (!(count >= 1) && !(spacing > 0)) { notify('Give the copies or a spacing.'); return; }
    const rule: PathRule = { kind: 'path', orient: r.orient, flip: r.flip === 'yes', start: Number(r.start), end: Number(r.end), offset: Number(r.offset) };
    if (count >= 1) rule.count = count;
    if (spacing > 0) rule.spacing = spacing;
    if (!useCad.getState().host) { notify('Open a plan to click the path in.'); return; }
    pending = { ids, rule };
    await startCommand('ARRAYPATH');
    return;
  }
  if (r.kind === 'polar') await execute('element.array', { ids, kind: 'polar', count: Number(r.count), angle: Number(r.angle), center: [Number(r.cx), Number(r.cy)] });
  else await execute('element.array', { ids, count: Number(r.count), step: [Number(r.dx), Number(r.dy), Number(r.dz)] });
}
