/** The Grid dialog's spacings (report #68): typed as lengths in the project's units, laid out as the axes the engine makes. */

import { parseLengths } from '../core/format';

export interface GridSpec { x: number[]; y: number[]; extend: number; angle: number; origin: { x: number; y: number } }

/** The spacings typed in a field ("6000 6000 4500", "3*6000", "20' 18'"): metres, or null when one does not read. A count times a length repeats it. */
export function readSpacings(text: string): number[] | null {
  const out: number[] = [];
  for (const piece of text.split(/[;\s]+(?=\d+\s*[*x×])|;/).flatMap((p) => p.trim() ? [p.trim()] : [])) {
    const m = /^(\d+)\s*[*x×]\s*(.+)$/.exec(piece);
    if (m) {
      const one = parseLengths(m[2]);
      if (!one || one.length !== 1 || Number(m[1]) < 1 || Number(m[1]) > 200) return null;
      for (let i = 0; i < Number(m[1]); i++) out.push(one[0]);
      continue;
    }
    const list = parseLengths(piece);
    if (!list) return null;
    out.push(...list);
  }
  return out.every((v) => v > 0) ? out : null;
}

/** The axes' lines as the engine lays them (for the dialog's preview): across x at each station, along at each y station, each past the outer ones by `extend`. */
export function gridLines(s: GridSpec): { a: { x: number; y: number }; b: { x: number; y: number }; across: boolean }[] {
  const r = s.angle * Math.PI / 180, u = { x: Math.cos(r), y: Math.sin(r) }, v = { x: -Math.sin(r), y: Math.cos(r) };
  const w = (p: number, q: number) => ({ x: s.origin.x + u.x * p + v.x * q, y: s.origin.y + u.y * p + v.y * q });
  const stations = (list: number[]) => list.reduce<number[]>((acc, d) => [...acc, acc[acc.length - 1] + d], [0]);
  const width = s.x.reduce((a, b) => a + b, 0), depth = s.y.reduce((a, b) => a + b, 0);
  const out: { a: { x: number; y: number }; b: { x: number; y: number }; across: boolean }[] = [];
  if (s.x.length || !s.y.length) for (const p of stations(s.x)) out.push({ a: w(p, -s.extend), b: w(p, depth + s.extend), across: true });
  if (s.y.length || !s.x.length) for (const q of stations(s.y)) out.push({ a: w(-s.extend, q), b: w(width + s.extend, q), across: false });
  return out;
}

/** Selected axes (grid#tag) after a renumbering (forum #75): each one's new tag, the other selections as they were. */
export function renumberedSelection(selected: string[], gridId: string, tags: Record<string, string>): string[] {
  const hash = `${gridId}#`;
  return selected.map((s) => (s.startsWith(hash) && tags[s.slice(hash.length)] ? hash + tags[s.slice(hash.length)] : s));
}

/** The selected axes of one grid (grid#tag): that grid and their tags, or null when none or several grids are selected. */
export function selectedAxes(selected: string[]): { gridId: string; tags: string[] } | null {
  const axes = selected.filter((s) => s.indexOf('#') > 0);
  if (axes.length === 0) return null;
  const gridId = axes[0].slice(0, axes[0].indexOf('#'));
  if (axes.some((s) => !s.startsWith(`${gridId}#`))) return null;
  return { gridId, tags: axes.map((s) => s.slice(gridId.length + 1)) };
}

/**
 * Whether the axes named run up the plan (the family spaced along x, numbered left to right) rather than across it, as the
 * engine tells them apart (Grids.AlongX); null when none of them is in the grid. Renumber axes names the selected axes'
 * directions by it (issue #64).
 */
export function runUpThePlan(axes: { tag: string; a: { x: number; y: number }; b: { x: number; y: number } }[], tags: string[]): boolean | null {
  const first = axes.find((x) => tags.includes(x.tag));
  return first ? Math.abs(first.b.y - first.a.y) >= Math.abs(first.b.x - first.a.x) : null;
}

/** The label after `tag`, as the engine counts (Grids.Next): 1 → 2, 9 → 10, A → B, Z → AA, A1 → A2. */
export function nextLabel(tag: string): string {
  const m = /^(.*?)(\d+)$/.exec(tag);
  if (m) return m[1] + String(Number(m[2]) + 1);
  if (!tag) return '1';
  const chars = [...tag];
  for (let i = chars.length - 1; i >= 0; i--) {
    const c = chars[i];
    if ((c >= 'A' && c < 'Z') || (c >= 'a' && c < 'z')) { chars[i] = String.fromCharCode(c.charCodeAt(0) + 1); return chars.join(''); }
    if (c === 'Z' || c === 'z') { chars[i] = c === 'Z' ? 'A' : 'a'; continue; }
    return `${tag}1`;
  }
  return (tag[0] === tag[0].toUpperCase() ? 'A' : 'a') + chars.join('');
}

/**
 * Axis grid's first label when a family's counting is switched (issue #64): counting down from the default (1, A), the
 * first becomes the label that ends the family on it (4 axes: 4, D); counting up again from that, the default comes back.
 * A label the user typed is kept.
 */
export function firstForCounting(first: string, fallback: string, count: number, descending: boolean): string {
  let top = fallback;
  for (let i = 1; i < count; i++) top = nextLabel(top);
  if (descending) return first.trim() === fallback ? top : first;
  return first.trim() === top ? fallback : first;
}
