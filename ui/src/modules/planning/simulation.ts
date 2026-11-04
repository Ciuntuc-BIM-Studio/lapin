/**
 * The 4D simulation's arithmetic (docs/plan-4d5d.md, stage 4), apart from React so it can be tested. The engine sends the
 * timeline once a change (planning.timeline: each element's days built, each layer's when tasks take them apart, the days
 * demolished, a formwork's or scaffold's days standing); here it is read once into day numbers, and the state of every
 * element on any day is worked out without asking the engine again, then turned into the views' looks (platform/looks.ts):
 * not started hidden or a ghost, under way in its colour, built as it is, being demolished red, demolished gone, a temporary
 * only while it stands, a layered wall's layers not yet laid left out. The looks are shared objects, one per state (and per
 * mix of layer states), so a day that changes nothing changes no look, and the 3D restyles only what moved: nothing is built
 * again, so a large model plays smoothly. Playback: the day moves by the speed (days a second) as time passes, or steps a
 * day or a week. With progress and a baseline (stage 5): an element's days are the actual ones up to the status date, and,
 * measured against the baseline's days, one not built (or not started) when the baseline had it so is late, one under way
 * before the baseline had it start is ahead, each in its colour.
 */

import type { Look } from '../../platform/looks';
import { dateOf, dayOf } from './gantt';

export type SimState = 'planned' | 'active' | 'done' | 'existing' | 'demolishing' | 'gone' | 'temporary' | 'unlinked' | 'late' | 'ahead';

export interface SimLookDto {
  active: string; demolishing: string; temporary: string; planned: string;
  plannedMode: 'hide' | 'ghost'; unlinked: 'show' | 'hide'; layers: boolean;
  /** Against the baseline (stage 5): the colours of what is late and what is ahead, and whether the simulation compares at all. */
  late: string; ahead: string; compare: boolean;
}

export interface TimelineElementDto {
  id: string;
  build?: [string, string];
  /** Each layer's days by its part key (layer:0, layer:1…), when tasks take the layers apart. */
  layers?: Record<string, [string, string]>;
  demolish?: [string, string];
  temporary?: string;
  present?: [string, string];
  /** The days its tasks' baseline gave it (stage 5). */
  base?: [string, string];
}

export interface TimelineDto {
  start: string; finish: string; dataDate: string | null; look: SimLookDto; baseline?: boolean;
  elements: TimelineElementDto[];
  tasks: { id: string; name: string; start: string; finish: string; duration: number }[];
  stages: { chapters: string[]; months: string[] };
}

type Span = [number, number];

export interface SimElement { id: string; build: Span | null; layers: [string, Span][] | null; demolish: Span | null; present: Span | null; temporary: string | null; base?: Span | null }

export interface SimTimeline {
  start: number; finish: number; look: SimLookDto;
  /** Whether some element has baseline days to be compared with. */
  baseline: boolean;
  elements: SimElement[];
  ids: Set<string>;
  tasks: { id: string; name: string; start: number; finish: number; duration: number }[];
  stages: { chapters: number[]; months: number[] };
}

export const DEFAULT_LOOK: SimLookDto = {
  active: '#e8a33d', demolishing: '#d64545', temporary: '#3d8fd6', planned: '#b8c0c8', plannedMode: 'hide', unlinked: 'show', layers: true,
  late: '#c2255c', ahead: '#2f9e44', compare: true,
};

const span = (s: [string, string] | undefined): Span | null => (s ? [dayOf(s[0]), dayOf(s[1])] : null);

/** The timeline as day numbers. */
export function prepare(t: TimelineDto): SimTimeline {
  const elements = t.elements.map((e): SimElement => ({
    id: e.id, build: span(e.build), demolish: span(e.demolish), present: span(e.present), temporary: e.temporary ?? null, base: span(e.base),
    layers: e.layers ? Object.entries(e.layers).map(([k, s]) => [k, span(s)!] as [string, Span]) : null,
  }));
  return {
    start: dayOf(t.start), finish: dayOf(t.finish), look: { ...DEFAULT_LOOK, ...t.look }, baseline: elements.some((e) => !!e.base),
    elements, ids: new Set(elements.map((e) => e.id)),
    tasks: t.tasks.map((x) => ({ ...x, start: dayOf(x.start), finish: dayOf(x.finish) })),
    stages: { chapters: t.stages.chapters.map(dayOf), months: t.stages.months.map(dayOf) },
  };
}

const built = (s: Span | null, d: number): SimState => (!s ? 'unlinked' : d < s[0] ? 'planned' : d <= s[1] ? 'active' : 'done');

/** An element's state on a day (as the engine's Timeline.StateOf). */
export function stateOf(e: SimElement, d: number): SimState {
  if (e.present) return d >= e.present[0] && d <= e.present[1] ? 'temporary' : 'gone';
  if (e.demolish && (!e.build || d >= e.demolish[0])) return d < e.demolish[0] ? 'existing' : d <= e.demolish[1] ? 'demolishing' : 'gone';
  return built(e.build, d);
}

/**
 * An element's state on a day measured against its baseline (as the engine's Timeline.Compared): late when the baseline
 * had it started (or built) by then and it is not, ahead when it is under way or built before the baseline had it start.
 */
export function comparedOf(e: SimElement, d: number): SimState {
  const s = stateOf(e, d);
  if (!e.base || !e.build) return s;
  if ((s === 'planned' && d >= e.base[0]) || (s === 'active' && d > e.base[1])) return 'late';
  if ((s === 'active' || s === 'done') && d < e.base[0]) return 'ahead';
  return s;
}

/** The state the simulation shows: against the baseline when the look compares, else the element's own. */
export const shownOf = (e: SimElement, d: number, look: Pick<SimLookDto, 'compare'>): SimState => (look.compare ? comparedOf(e, d) : stateOf(e, d));

/** Each layer's state on a day (null: built in one piece, or demolished). */
export function layerStates(e: SimElement, d: number): [string, SimState][] | null {
  if (!e.layers || e.present || (e.demolish && d >= e.demolish[0])) return null;
  return e.layers.map(([k, s]) => [k, built(s, d)]);
}

/** The tasks under way on a day. */
export const tasksOn = (t: SimTimeline, d: number) => t.tasks.filter((x) => x.duration > 0 && d >= x.start && d <= x.finish);

// ── Looks ────────────────────────────────────────────────────────────────────

/** The looks of a simulation's colours, one object each, kept while the colours are the same. */
export interface LookKit { look: SimLookDto; byState: Partial<Record<SimState, Look>>; mixed: Map<string, Look> }

export function lookKit(look: SimLookDto): LookKit {
  const byState: Partial<Record<SimState, Look>> = {
    planned: look.plannedMode === 'ghost' ? { color: look.planned, opacity: 0.18 } : { hidden: true },
    active: { color: look.active },
    demolishing: { color: look.demolishing },
    temporary: { color: look.temporary },
    gone: { hidden: true },
    late: { color: look.late },
    ahead: { color: look.ahead },
  };
  if (look.unlinked === 'hide') byState.unlinked = { hidden: true };
  return { look, byState, mixed: new Map() };
}

/** A layered element's look from its layers' states: the element's, with the layers not yet laid hidden (one object per mix). */
function mixedLook(kit: KitLike, layers: [string, SimState][]): Look | undefined {
  const states = layers.map(([, s]) => s);
  if (states.every((s) => s === states[0])) return kit.byState[states[0]];
  const key = layers.map(([k, s]) => `${k}=${s}`).join(',');
  let l = kit.mixed.get(key);
  if (!l) {
    const parts: Record<string, Look> = {};
    for (const [k, s] of layers) {
      if (s === 'planned') parts[k] = { hidden: true };
      else if (s === 'active') parts[k] = { color: kit.look.active };
    }
    l = { parts };
    kit.mixed.set(key, l);
  }
  return l;
}

type KitLike = LookKit;

/**
 * The looks on a day: an element as it is (no look) when built, existing, or in no task (unless they are hidden); `others`,
 * the model's elements the timeline does not know (hidden only when the settings say so). The signature changes only when
 * some element's look does.
 */
export function looksAt(t: SimTimeline, d: number, kit: LookKit, others: Iterable<string> = []): { looks: Record<string, Look>; signature: string } {
  const looks: Record<string, Look> = {};
  const sig: string[] = [];
  for (const e of t.elements) {
    const s = shownOf(e, d, kit.look);
    const ls = kit.look.layers && (s === 'active' || s === 'done') ? layerStates(e, d) : null;
    const l = ls ? mixedLook(kit, ls) : kit.byState[s];
    if (!l) continue;
    looks[e.id] = l;
    sig.push(`${e.id}:${ls ? ls.map(([, x]) => x[0]).join('') : s}`);
  }
  const hideOthers = kit.byState.unlinked;
  if (hideOthers) for (const id of others) if (!t.ids.has(id)) { looks[id] = hideOthers; sig.push(`${id}:u`); }
  return { looks, signature: `${kit.look.plannedMode}|${sig.join(';')}` };
}

/** How many elements are in each state on a day (the legend's counts). */
export function counts(t: SimTimeline, d: number): Partial<Record<SimState, number>> {
  const o: Partial<Record<SimState, number>> = {};
  for (const e of t.elements) { const s = shownOf(e, d, t.look); o[s] = (o[s] ?? 0) + 1; }
  return o;
}

/** The legend: what each colour means, in the simulation's colours (the ghost only when not started shows as one; late and ahead with a baseline to compare with). */
export function legend(look: SimLookDto, baseline = false): { state: SimState; color: string; label: string }[] {
  const o: { state: SimState; color: string; label: string }[] = [
    { state: 'active', color: look.active, label: 'Under way' },
    { state: 'demolishing', color: look.demolishing, label: 'Being demolished' },
    { state: 'temporary', color: look.temporary, label: 'Formwork and scaffolding' },
  ];
  if (look.plannedMode === 'ghost') o.push({ state: 'planned', color: look.planned, label: 'Not started (ghost)' });
  if (baseline && look.compare) o.push({ state: 'late', color: look.late, label: 'Late on the baseline' }, { state: 'ahead', color: look.ahead, label: 'Ahead of the baseline' });
  return o;
}

// ── Playback ─────────────────────────────────────────────────────────────────

/** The speeds offered, days a second. */
export const SPEEDS = [1, 2, 5, 10, 30, 60] as const;

/** A day kept within the schedule. */
export const clampDay = (d: number, t: { start: number; finish: number }) => Math.max(t.start, Math.min(t.finish, Math.round(d)));

/**
 * The play head after some time: `pos` (a day, with its fraction) moved on by the speed; at the finish it stops there.
 * Returns the new position and whether playing goes on.
 */
export function advance(pos: number, ms: number, speed: number, t: { start: number; finish: number }): { pos: number; playing: boolean } {
  const next = pos + (Math.max(0, ms) / 1000) * speed;
  return next >= t.finish ? { pos: t.finish, playing: false } : { pos: Math.max(t.start, next), playing: true };
}

/** A step by a day or a week, forward (+1) or back (−1), kept within the schedule. */
export function step(d: number, unit: 'day' | 'week', dir: 1 | -1, t: { start: number; finish: number }): number {
  return clampDay(d + dir * (unit === 'week' ? 7 : 1), t);
}

/** The days a recording goes through: from one day to another by a step, the last one always in. */
export function frameDays(from: number, to: number, every: number): number[] {
  const o: number[] = [];
  const k = Math.max(1, Math.round(every));
  for (let d = from; d < to; d += k) o.push(d);
  o.push(to);
  return o;
}

/** A video's type the browser can record: WebM (VP9, VP8) first, MP4 where only that is offered (Safari, the macOS app); null: none. */
export function videoType(supported: (type: string) => boolean): { mime: string; ext: string } | null {
  for (const [mime, ext] of [['video/webm;codecs=vp9', 'webm'], ['video/webm;codecs=vp8', 'webm'], ['video/webm', 'webm'], ['video/mp4;codecs=avc1', 'mp4'], ['video/mp4', 'mp4']] as const)
    if (supported(mime)) return { mime, ext };
  return null;
}

/** A day as a date the engine reads (2027-03-14). */
export const isoOf = (d: number) => dateOf(d);

// ── Frames as a ZIP (where no video can be recorded) ─────────────────────────

const CRC = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; }
  return t;
})();

export function crc32(b: Uint8Array): number {
  let c = 0xffffffff;
  for (let i = 0; i < b.length; i++) c = CRC[(c ^ b[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/** Files in a ZIP, stored as they are (PNG frames are compressed already): the local headers, the files, the central directory. */
export function zipStore(files: { name: string; bytes: Uint8Array }[]): Uint8Array {
  const enc = new TextEncoder();
  const parts: Uint8Array[] = [];
  const central: Uint8Array[] = [];
  let offset = 0;
  const u16 = (v: DataView, at: number, x: number) => v.setUint16(at, x, true);
  const u32 = (v: DataView, at: number, x: number) => v.setUint32(at, x >>> 0, true);
  for (const f of files) {
    const name = enc.encode(f.name);
    const crc = crc32(f.bytes);
    const head = new Uint8Array(30 + name.length);
    const h = new DataView(head.buffer);
    u32(h, 0, 0x04034b50); u16(h, 4, 20); u16(h, 6, 0); u16(h, 8, 0); u16(h, 10, 0); u16(h, 12, 0x21);
    u32(h, 14, crc); u32(h, 18, f.bytes.length); u32(h, 22, f.bytes.length); u16(h, 26, name.length); u16(h, 28, 0);
    head.set(name, 30);
    const dir = new Uint8Array(46 + name.length);
    const c = new DataView(dir.buffer);
    u32(c, 0, 0x02014b50); u16(c, 4, 20); u16(c, 6, 20); u16(c, 8, 0); u16(c, 10, 0); u16(c, 12, 0); u16(c, 14, 0x21);
    u32(c, 16, crc); u32(c, 20, f.bytes.length); u32(c, 24, f.bytes.length); u16(c, 28, name.length);
    u32(c, 42, offset);
    dir.set(name, 46);
    parts.push(head, f.bytes);
    central.push(dir);
    offset += head.length + f.bytes.length;
  }
  const size = central.reduce((s, x) => s + x.length, 0);
  const end = new Uint8Array(22);
  const e = new DataView(end.buffer);
  u32(e, 0, 0x06054b50); u16(e, 8, files.length); u16(e, 10, files.length); u32(e, 12, size); u32(e, 16, offset);
  const all = [...parts, ...central, end];
  const out = new Uint8Array(all.reduce((s, x) => s + x.length, 0));
  let at = 0;
  for (const x of all) { out.set(x, at); at += x.length; }
  return out;
}
