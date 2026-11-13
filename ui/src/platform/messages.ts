/**
 * The messages console's model (decision 538): a log of rows (an error, a warning or an info, with a time, a source and
 * the text; a row may carry a target the place knows how to focus), kept per place (the graph editor, the Script panel).
 * The pure parts (adding with collapsed repeats, replacing a group of rows, filtering, the plain text) are here; the strip
 * that shows them is shell/MessageStrip.tsx.
 */

import { create, type UseBoundStore, type StoreApi } from 'zustand';

export type MessageKind = 'error' | 'warning' | 'info';

export interface MessageInput {
  kind: MessageKind;
  /** What it is about: a node's label, a script's name, "Bake". Shown as it is. */
  source: string;
  text: string;
  /** The source (a node's label, a script's name) is the user's own words: not translated. */
  sourcePlain?: boolean;
  /** The text is a script's (a Python error): not translated. */
  textPlain?: boolean;
  /** What a click focuses (the place's own: a node id, a script and its line). */
  target?: unknown;
  /** Rows of one group are replaced together (the graph's last evaluation). */
  group?: string;
}

export interface Message extends MessageInput {
  id: number;
  time: number;
  /** How many times in a row it came (a repeat is counted, not listed again). */
  runs: number;
}

export const MAX_MESSAGES = 500;

export const keyOf = (m: MessageInput): string => `${m.kind}\u0000${m.source}\u0000${m.text}\u0000${JSON.stringify(m.target ?? null)}`;

const capped = (list: Message[], cap: number): Message[] => (list.length > cap ? list.slice(list.length - cap) : list);

/** Rows added at the end; a row equal to the one before it (same kind, source, text and target) only counts again and takes the new time. */
export function addMessages(list: readonly Message[], inputs: readonly MessageInput[], now: number, nextId: number, cap = MAX_MESSAGES): { list: Message[]; nextId: number } {
  let out = [...list];
  let id = nextId;
  for (const m of inputs) {
    const last = out[out.length - 1];
    if (last && !m.group && !last.group && keyOf(last) === keyOf(m)) out[out.length - 1] = { ...last, runs: last.runs + 1, time: now };
    else out.push({ ...m, id: id++, time: now, runs: 1 });
  }
  out = capped(out, cap);
  return { list: out, nextId: id };
}

/**
 * The rows of a group replaced by the group's new ones, at the end (one evaluation's rows are the log's latest, the one
 * before is gone: an evaluation that runs on every change never floods it). A row the old set had (the same key) keeps its id
 * and time, so it is not told again; `fresh` are the rows that were not there.
 */
export function replaceGroup(list: readonly Message[], group: string, inputs: readonly MessageInput[], now: number, nextId: number, cap = MAX_MESSAGES): { list: Message[]; nextId: number; fresh: Message[] } {
  const old = new Map<string, Message>();
  for (const m of list) if (m.group === group && !old.has(keyOf(m))) old.set(keyOf(m), m);
  const rest = list.filter((m) => m.group !== group);
  let id = nextId;
  const fresh: Message[] = [];
  const made = inputs.map((m): Message => {
    const had = old.get(keyOf(m));
    if (had) return { ...m, group, id: had.id, time: had.time, runs: 1 };
    const row = { ...m, group, id: id++, time: now, runs: 1 };
    fresh.push(row);
    return row;
  });
  return { list: capped([...rest, ...made], cap), nextId: id, fresh };
}

export type MessageFilter = 'all' | 'errors' | 'warnings';

export function filterMessages(list: readonly Message[], filter: MessageFilter): Message[] {
  return filter === 'errors' ? list.filter((m) => m.kind === 'error') : filter === 'warnings' ? list.filter((m) => m.kind === 'warning') : [...list];
}

export const countsOf = (list: readonly Message[]): { errors: number; warnings: number; all: number } =>
  ({ errors: list.filter((m) => m.kind === 'error').length, warnings: list.filter((m) => m.kind === 'warning').length, all: list.length });

const two = (n: number) => String(n).padStart(2, '0');
export const clock = (t: number): string => { const d = new Date(t); return `${two(d.getHours())}:${two(d.getMinutes())}:${two(d.getSeconds())}`; };

/** The rows as plain text, one per line: `12:03:04  error    source: text (x3)`. */
export function messagesText(list: readonly Message[]): string {
  return list.map((m) => `${clock(m.time)}  ${m.kind.padEnd(7)}  ${m.source ? `${m.source}: ` : ''}${m.text}${m.runs > 1 ? ` (x${m.runs})` : ''}`).join('\n');
}

// ── the strip's remembered state, per place ──

export interface StripState { open: boolean; height: number; filter: MessageFilter }
export const DEFAULT_STRIP: StripState = { open: false, height: 150, filter: 'all' };
export const MIN_STRIP = 70;
export const MAX_STRIP = 600;

const storeKey = (place: string) => `lapin.messages.${place}`;

/** Reads what a place remembered; anything unreadable is the default. */
export function readStrip(raw: string | null): StripState {
  try {
    const v = raw ? JSON.parse(raw) as Partial<StripState> : {};
    return {
      open: v.open === true,
      height: typeof v.height === 'number' && isFinite(v.height) ? Math.max(MIN_STRIP, Math.min(MAX_STRIP, Math.round(v.height))) : DEFAULT_STRIP.height,
      filter: v.filter === 'errors' || v.filter === 'warnings' ? v.filter : 'all',
    };
  } catch { return { ...DEFAULT_STRIP }; }
}

export function loadStrip(place: string): StripState {
  try { return readStrip(localStorage.getItem(storeKey(place))); } catch { return { ...DEFAULT_STRIP }; }
}

export function saveStrip(place: string, s: StripState): void {
  try { localStorage.setItem(storeKey(place), JSON.stringify(s)); } catch { /* kept for this session */ }
}

// ── a place's log ──

export interface MessageLog extends StripState {
  place: string;
  list: Message[];
  nextId: number;
  /** The latest new error, for the screen readers' live region. */
  announce: string;
}

export interface MessageLogApi {
  use: UseBoundStore<StoreApi<MessageLog>>;
  add: (...inputs: MessageInput[]) => void;
  replace: (group: string, inputs: MessageInput[]) => void;
  clear: () => void;
  setStrip: (patch: Partial<StripState>) => void;
}

/** A place's log: its rows in memory (not kept between sessions), its strip's state remembered. */
export function createMessageLog(place: string, cap = MAX_MESSAGES): MessageLogApi {
  const use = create<MessageLog>(() => ({ place, ...loadStrip(place), list: [], nextId: 1, announce: '' }));
  const set = use.setState;
  const get = use.getState;
  return {
    use,
    add: (...inputs) => {
      const s = get();
      const r = addMessages(s.list, inputs, Date.now(), s.nextId, cap);
      const err = [...inputs].reverse().find((m) => m.kind === 'error');
      set({ list: r.list, nextId: r.nextId, ...(err ? { announce: `${err.source ? `${err.source}: ` : ''}${err.text}` } : {}) });
    },
    replace: (group, inputs) => {
      const s = get();
      const r = replaceGroup(s.list, group, inputs, Date.now(), s.nextId, cap);
      const err = [...r.fresh].reverse().find((m) => m.kind === 'error');
      set({ list: r.list, nextId: r.nextId, ...(err ? { announce: `${err.source ? `${err.source}: ` : ''}${err.text}` } : {}) });
    },
    clear: () => set({ list: [], announce: '' }),
    setStrip: (patch) => {
      set(patch);
      const s = get();
      saveStrip(place, { open: s.open, height: s.height, filter: s.filter });
    },
  };
}
