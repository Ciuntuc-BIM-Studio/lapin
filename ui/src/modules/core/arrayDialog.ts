/**
 * The Array dialog's rules (forum 90), kept apart from the tool so they are tested alone: which fields each method shows
 * (in a row, in a grid, round a centre, along a path), and what is wrong with the answers, said by the field to change.
 */

export type ArrayKind = 'row' | 'grid' | 'polar' | 'path';

/** The most items an array makes, the first among them (the engine's Arrays.Most). */
export const ARRAY_MOST = 1000;

type Answers = Record<string, string | number | undefined>;

/** The fields each method shows, after Kind; a step, a grid's row step and a centre only when typed, not clicked. */
export function arrayFieldShown(key: string, v: Answers): boolean {
  const kind = String(v.kind ?? 'row') as ArrayKind;
  const typedStep = v.by !== 'click';
  const typedCentre = v.centreBy !== 'click';
  switch (key) {
    case 'kind': return true;
    case 'count': return kind !== 'grid';
    case 'cols': case 'rows': case 'levels': return kind === 'grid';
    case 'by': return kind === 'row' || kind === 'grid';
    case 'dx': case 'dy': return (kind === 'row' || kind === 'grid') && typedStep;
    case 'dz': return kind === 'row' || kind === 'grid';
    case 'rx': case 'ry': return kind === 'grid' && typedStep;
    case 'lz': return kind === 'grid' && Number(v.levels) > 1;
    case 'angle': case 'centreBy': return kind === 'polar';
    case 'cx': case 'cy': return kind === 'polar' && typedCentre;
    case 'spacing': case 'start': case 'end': case 'offset': case 'orient': case 'flip': return kind === 'path';
    default: return false;
  }
}

const num = (x: unknown) => Number(x);
const whole = (x: unknown) => Number.isInteger(num(x));

/**
 * What is wrong with the answers, by the field to change (its key) and why; null when they make an array. A step clicked
 * in the plan or a centre clicked is not checked here (the clicks give it).
 */
export function checkArray(v: Answers): { key: string; message: string } | null {
  const kind = String(v.kind ?? 'row') as ArrayKind;
  const nothing = (...xs: unknown[]) => xs.every((x) => !(Math.abs(num(x)) > 1e-9));
  if (kind === 'grid') {
    for (const key of ['cols', 'rows', 'levels'])
      if (!(num(v[key]) >= 1) || !whole(v[key])) return { key, message: 'A whole number, 1 or more.' };
    const total = num(v.cols) * num(v.rows) * num(v.levels);
    if (!(total >= 2 && total <= ARRAY_MOST)) return { key: 'cols', message: 'Columns × rows × levels must make 2 to 1000 items, the first among them.' };
    if (v.by !== 'click') {
      if (num(v.cols) > 1 && nothing(v.dx, v.dy, v.dz)) return { key: 'dx', message: 'Give the step from one column to the next (step x, y or z).' };
      if (num(v.rows) > 1 && nothing(v.rx, v.ry)) return { key: 'ry', message: 'Give the step from one row to the next (row step x or y).' };
    }
    if (num(v.levels) > 1 && nothing(v.lz)) return { key: 'lz', message: 'Give the height from one level to the next.' };
    return null;
  }
  if (kind === 'path') {
    const spacing = num(v.spacing);
    if (!(spacing >= 0)) return { key: 'spacing', message: 'A length, 0 or more (0: the copies shared out along the path).' };
    if (spacing > 0 && num(v.count) === 0) return null;
    if (!whole(v.count) || !(num(v.count) >= 2 && num(v.count) <= ARRAY_MOST)) return { key: 'count', message: 'A whole number from 2 to 1000, the first among them (or 0 with a spacing).' };
    return null;
  }
  if (!whole(v.count) || !(num(v.count) >= 2 && num(v.count) <= ARRAY_MOST)) return { key: 'count', message: 'A whole number from 2 to 1000, the first among them.' };
  if (kind === 'polar') {
    const a = num(v.angle);
    if (!(Math.abs(a) > 1e-9 && Math.abs(a) <= 360)) return { key: 'angle', message: 'An angle up to 360°, not 0 (negative: clockwise).' };
    return null;
  }
  if (v.by !== 'click' && nothing(v.dx, v.dy, v.dz)) return { key: 'dx', message: 'Give the step from one copy to the next (step x, y or z).' };
  return null;
}

/** The element.array arguments of a polar array: its copies round the centre, the angle filled. */
export const polarArgs = (ids: string[], v: Answers, centre: { x: number; y: number }) =>
  ({ ids, kind: 'polar', count: Math.round(num(v.count)), angle: num(v.angle), center: [centre.x, centre.y] });

/** The turn (°) from one copy of a polar array to the next, as the engine shares it: a full turn by the count, a part by one less. */
export const polarStep = (count: number, angle: number): number =>
  Math.abs(Math.abs(angle) - 360) < 1e-9 ? angle / count : angle / Math.max(1, count - 1);
