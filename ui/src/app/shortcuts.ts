/**
 * The keyboard shortcuts' registry: every command the ribbon, the quick tools
 * and the command palette know has an id, a name and zero or more bindings.
 * A binding is either
 *
 * - a chord: a key with or without modifiers ("Ctrl+Shift+S", "F3",
 *   "Delete", "Shift+Z", "1"); Ctrl stands for Cmd on a Mac; or
 * - a sequence: one to three letters typed one after the other while no
 *   field has the focus and the command line is empty ("WA" for Wall, as in
 *   Revit).
 *
 * The rule with the command line, whose text is typed anywhere in the
 * window: a sequence is matched against everything typed since the line was
 * empty, and only while no command is asking for input. When the letters are
 * a sequence and nothing longer starts with them, the command runs at once;
 * when a longer sequence or a command-line command starts with them too (DI
 * and DIMADD), it waits: typing on writes the longer one, Enter runs the
 * command line's, and after SEQUENCE_TIMEOUT with nothing more typed the
 * sequence runs. A bare key (a digit, +, −) acts only while nothing is
 * typed; a key in a field (a property, a dialog's input) is always the
 * field's.
 *
 * Bindings come from a preset (Lapin classic: the modules' own letters;
 * Revit-style) and the user's own changes over it, kept per user in the
 * browser's storage (the desktop app and the browser apart). This file is
 * pure: the commands themselves are listed in `commands.ts`.
 */

import { create } from 'zustand';

export type Binding = string;

export interface CommandDef {
  id: string;
  label: string;
  /** Where it is listed: the ribbon tab, or General, Views, Panels… */
  category: string;
  /** The ribbon group, shown in tooltips. */
  group?: string;
  /** The bindings of Lapin classic (the module's own letters, the app's chords). */
  defaults?: Binding[];
  /** The command-line command this is the same as (MOVE): its names never make the command's own sequence wait. */
  same?: string;
  /** Bindings that are part of the interface's own handling, shown, not changed (Escape cancels). */
  fixed?: Binding[];
  run: () => void;
}

export type PresetId = 'classic' | 'revit';

export const SEQUENCE_TIMEOUT = 800;

const MODS = ['Ctrl', 'Alt', 'Shift'] as const;
const NAMED: Record<string, string> = {
  esc: 'Escape', escape: 'Escape', del: 'Delete', delete: 'Delete', backspace: 'Backspace', tab: 'Tab', enter: 'Enter', return: 'Enter',
  space: 'Space', ' ': 'Space', up: 'ArrowUp', down: 'ArrowDown', left: 'ArrowLeft', right: 'ArrowRight',
  arrowup: 'ArrowUp', arrowdown: 'ArrowDown', arrowleft: 'ArrowLeft', arrowright: 'ArrowRight',
  plus: 'Plus', '+': 'Plus', minus: '-', home: 'Home', end: 'End', pageup: 'PageUp', pagedown: 'PageDown', insert: 'Insert',
};

/** A sequence: letters typed one after the other (no modifiers). */
export const isSequence = (b: Binding) => /^[A-Z]{1,3}$/.test(b);

/** A key that types a character: as a chord without Ctrl or Alt it acts only while nothing is typed. */
const printable = (key: string) => key.length === 1 || key === 'Plus' || key === 'Space';

/** Whether a chord is a bare printable key (1, +, Shift+Z): it waits for the command line to be empty. */
export function isBareKey(b: Binding): boolean {
  if (isSequence(b)) return false;
  const parts = b.split('+');
  const key = parts[parts.length - 1] || 'Plus';
  return printable(key) && !parts.includes('Ctrl') && !parts.includes('Alt');
}

/** A binding as typed by a user or found in a file ("ctrl+shift+s", "Cmd+S", "esc", "wa") in its one spelling, or null. */
export function normalizeBinding(text: string): Binding | null {
  const raw = text.trim();
  if (!raw) return null;
  if (/^[a-z]{1,3}$/i.test(raw) && !NAMED[raw.toLowerCase()]) return raw.toUpperCase();
  // "Ctrl++" ends in the plus key itself.
  const plusKey = raw.endsWith('++') || raw === '+';
  const parts = (plusKey ? raw.slice(0, raw === '+' ? 0 : -2) : raw).split('+').map((p) => p.trim()).filter(Boolean);
  if (plusKey) parts.push('Plus');
  const mods = new Set<string>();
  let key: string | null = null;
  for (const p of parts) {
    const l = p.toLowerCase();
    if (l === 'ctrl' || l === 'control' || l === 'cmd' || l === 'command' || l === 'meta' || l === '⌘') mods.add('Ctrl');
    else if (l === 'alt' || l === 'option' || l === 'opt' || l === '⌥') mods.add('Alt');
    else if (l === 'shift' || l === '⇧') mods.add('Shift');
    else if (key) return null;
    else {
      key = NAMED[l] ?? (/^f([1-9]|1[0-2])$/.test(l) ? l.toUpperCase() : p.length === 1 ? p.toUpperCase() : null);
      if (!key) return null;
    }
  }
  if (!key) return null;
  return [...MODS.filter((m) => mods.has(m)), key].join('+');
}

/** What a key press is as a chord, or null for a modifier alone. */
export function chordOf(e: { key: string; code?: string; ctrlKey: boolean; metaKey: boolean; altKey?: boolean; shiftKey: boolean }): Binding | null {
  if (['Control', 'Shift', 'Alt', 'Meta', 'CapsLock', 'Dead', 'Unidentified'].includes(e.key)) return null;
  const ctrl = e.ctrlKey || e.metaKey, alt = !!e.altKey;
  let key = NAMED[e.key.toLowerCase()] ?? e.key;
  // With Alt (a Mac types ∫ for Alt+B) or Ctrl, the key's place says which letter or digit it is.
  const code = e.code ?? '';
  if ((alt || ctrl) && /^Key[A-Z]$/.test(code)) key = code.slice(3);
  else if ((alt || ctrl) && /^Digit[0-9]$/.test(code)) key = code.slice(5);
  if (key.length === 1) key = key.toUpperCase();
  // A character that Shift made (+, <, !) is that character: Shift stays only with letters, digits and named keys.
  const shift = e.shiftKey && (key.length > 1 || /[A-Z0-9]/.test(key)) && key !== 'Plus';
  const mods = [ctrl && 'Ctrl', alt && 'Alt', shift && 'Shift'].filter(Boolean) as string[];
  return [...mods, key].join('+');
}

/** The modifiers held, as the capture shows them before the key comes ("Ctrl+Shift+…"); empty when none. */
export function heldOf(e: { ctrlKey: boolean; metaKey: boolean; altKey?: boolean; shiftKey: boolean }): string {
  const mods = [(e.ctrlKey || e.metaKey) && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift'].filter(Boolean) as string[];
  return mods.length ? `${mods.join('+')}+…` : '';
}

export type CaptureStep =
  | { kind: 'done'; binding: Binding | null }
  | { kind: 'letters'; letters: string }
  | { kind: 'held'; text: string }
  | null;

/**
 * One key press while a binding is listened for (Keyboard shortcuts → +): Esc
 * cancels, letters without a modifier make a sequence (Enter ends it, the
 * third ends it at once), anything else is a chord (Ctrl+Shift+W, Shift+W,
 * Alt+1, F4). A modifier alone is shown held and waits for its key (report
 * #40: Shift with a letter was taken for the letter alone).
 */
export function captureKey(letters: string, e: { key: string; code?: string; ctrlKey: boolean; metaKey: boolean; altKey?: boolean; shiftKey: boolean }): CaptureStep {
  if (e.key === 'Escape') return { kind: 'done', binding: null };
  if (e.key === 'Enter' && !e.ctrlKey && !e.metaKey && !e.altKey && !e.shiftKey) return { kind: 'done', binding: letters || null };
  if (e.key === 'Backspace' && letters) return { kind: 'letters', letters: letters.slice(0, -1) };
  if (/^[a-z]$/i.test(e.key) && !e.ctrlKey && !e.metaKey && !e.altKey && !e.shiftKey) {
    const next = (letters + e.key.toUpperCase()).slice(0, 3);
    return next.length === 3 ? { kind: 'done', binding: next } : { kind: 'letters', letters: next };
  }
  if (letters) return null;
  const chord = chordOf(e);
  if (!chord) return { kind: 'held', text: heldOf(e) };
  return { kind: 'done', binding: chord };
}

const MAC = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform ?? '');

/** A binding as the interface shows it: "Ctrl ⇧ S" (⌘⇧S on a Mac), "WA", "F3". */
export function formatBinding(b: Binding, mac = MAC): string {
  if (isSequence(b)) return b;
  const parts = b.split('+').map((p) => (p === '' ? 'Plus' : p));
  const names: Record<string, string> = mac
    ? { Ctrl: '⌘', Alt: '⌥', Shift: '⇧', Plus: '+', Escape: 'Esc', Delete: 'Del', Backspace: '⌫', ArrowUp: '↑', ArrowDown: '↓', ArrowLeft: '←', ArrowRight: '→', Enter: '↵' }
    : { Ctrl: 'Ctrl', Alt: 'Alt', Shift: '⇧', Plus: '+', Escape: 'Esc', Delete: 'Del', Backspace: '⌫', ArrowUp: '↑', ArrowDown: '↓', ArrowLeft: '←', ArrowRight: '→', Enter: '↵' };
  return parts.map((p) => names[p] ?? p).join(mac ? '' : ' ');
}

// ─── Presets ───────────────────────────────────────────────────────────────

/**
 * Revit's default keyboard shortcuts, where Lapin has the command (the
 * other commands keep Lapin's letters). An empty list takes a binding away
 * where Revit's letters mean something else.
 */
export const REVIT: Record<string, Binding[]> = {
  'architecture.wall': ['WA'], 'architecture.door': ['DR'], 'architecture.window': ['WN'], 'architecture.room': ['RM'],
  'architecture.slab': ['SB'], 'structure.column': ['CL'], 'structure.beam': ['BM'],
  'drafting.axis': ['GR'], 'level': ['LL'], 'drafting.dimension': ['DI'], 'drafting.text': ['TX'], 'cad.tag': ['TG'], 'cad.line': ['DL'],
  'modify.move': ['MV'], 'modify.copy': ['CO'], 'modify.rotate': ['RO'], 'modify.mirror': ['MM', 'DM'], 'modify.delete': ['DE'],
  'cad.trim': ['TR'], 'cad.offset': ['OF'], 'cad.scale': ['RE'],
  'modify.match': ['MA'], 'modify.similar': ['CS'],
  'view.layers': ['VV'], 'view.graphicsVg': ['VG'],
  'app.zoomFit': ['ZF', 'ZE'], 'app.zoomRegion': ['ZR'], 'app.zoomPrevious': ['ZP'], 'app.zoomOut': ['-', 'ZO'],
  'app.toggleProps': ['Ctrl+Shift+B', 'PP'], 'app.shortcuts': ['KS'],
  // Lapin's own letters that Revit gives to something else.
  'civil.corridor': ['CR'],
};

export const PRESETS: Record<PresetId, { label: string; patch: Record<string, Binding[]> }> = {
  classic: { label: 'Lapin classic', patch: {} },
  revit: { label: 'Revit-style', patch: REVIT },
};

export const DEFAULT_PRESET: PresetId = 'revit';

/** A preset's bindings for every command: the classic ones with the preset's changes. */
export function presetBindings(commands: readonly CommandDef[], preset: PresetId): Record<string, Binding[]> {
  const patch = PRESETS[preset]?.patch ?? {};
  const out: Record<string, Binding[]> = {};
  for (const c of commands) out[c.id] = [...(patch[c.id] ?? c.defaults ?? [])];
  return out;
}

export interface KeymapState {
  preset: PresetId;
  /** The user's own bindings, by command: they replace the preset's for that command. */
  overrides: Record<string, Binding[]>;
}

/** The bindings in force: the user's own, else the preset's. */
export function effectiveBindings(commands: readonly CommandDef[], s: KeymapState): Record<string, Binding[]> {
  const base = presetBindings(commands, s.preset);
  for (const c of commands) if (s.overrides[c.id]) base[c.id] = [...s.overrides[c.id]];
  return base;
}

/** The commands (in their order) that use a binding. */
export function usersOf(bindings: Record<string, Binding[]>, b: Binding, except?: string): string[] {
  return Object.keys(bindings).filter((id) => id !== except && bindings[id].includes(b));
}

/** Bindings used by more than one command, with who uses them. */
export function conflicts(bindings: Record<string, Binding[]>): Record<Binding, string[]> {
  const by: Record<Binding, string[]> = {};
  for (const [id, list] of Object.entries(bindings)) for (const b of list) (by[b] ??= []).push(id);
  return Object.fromEntries(Object.entries(by).filter(([, ids]) => ids.length > 1));
}

/** What the keyboard handler looks up: chords and sequences to the command they run (the first command wins a conflict), and the sequences that wait. */
export interface Keymap {
  chords: Record<Binding, string>;
  sequences: Record<Binding, string>;
  /** Sequences that something longer starts with (another sequence, a command-line command): they wait. */
  waits: Record<Binding, true>;
}

/**
 * The keymap from the bindings in force. `cli` lists the command line's
 * command names, each with the command it is (so MOVE's own names do not
 * make MV wait when MV is Move).
 */
export function resolveKeymap(commands: readonly CommandDef[], bindings: Record<string, Binding[]>, cli: readonly { name: string; command: string }[] = []): Keymap {
  const map: Keymap = { chords: {}, sequences: {}, waits: {} };
  for (const c of commands) for (const b of [...(c.fixed ?? []), ...(bindings[c.id] ?? [])]) {
    const into = isSequence(b) ? map.sequences : map.chords;
    if (!(b in into)) into[b] = c.id;
  }
  const same = new Map(commands.map((c) => [c.id, c.same]));
  for (const [seq, id] of Object.entries(map.sequences)) {
    const longer = Object.keys(map.sequences).some((o) => o.length > seq.length && o.startsWith(seq))
      || cli.some((n) => n.name.length > seq.length && n.name.startsWith(seq) && n.command !== same.get(id));
    if (longer) map.waits[seq] = true;
  }
  return map;
}

export type SequenceMatch = { kind: 'fire'; id: string } | { kind: 'wait'; id: string } | { kind: 'none' };

/** What the letters typed since the command line was empty mean. */
export function matchSequence(typed: string, map: Keymap): SequenceMatch {
  const seq = typed.toUpperCase();
  if (!/^[A-Z]{1,3}$/.test(seq)) return { kind: 'none' };
  const id = map.sequences[seq];
  if (!id) return { kind: 'none' };
  return map.waits[seq] ? { kind: 'wait', id } : { kind: 'fire', id };
}

/** A sequence that waits: it runs when its time is up and the command line still holds exactly its letters. */
export class PendingSequence {
  private armed: { id: string; typed: string; at: number } | null = null;
  constructor(private timeout = SEQUENCE_TIMEOUT) {}
  arm(id: string, typed: string, at: number): void { this.armed = { id, typed, at }; }
  clear(): void { this.armed = null; }
  /** The command to run now, or null (not yet, or the line changed since). */
  due(now: number, buffer: string, running: boolean): string | null {
    const a = this.armed;
    if (!a) return null;
    if (running || buffer !== a.typed) { this.armed = null; return null; }
    if (now - a.at < this.timeout) return null;
    this.armed = null;
    return a.id;
  }
  get delay(): number { return this.timeout; }
}

// ─── Changes and files ─────────────────────────────────────────────────────

/** A binding added to a command; with `reassign`, taken from the commands that had it. */
export function withBinding(commands: readonly CommandDef[], s: KeymapState, id: string, b: Binding, reassign = false): KeymapState {
  const now = effectiveBindings(commands, s);
  const overrides = { ...s.overrides };
  if (reassign) for (const other of usersOf(now, b, id)) overrides[other] = now[other].filter((x) => x !== b);
  if (!now[id]?.includes(b)) overrides[id] = [...(now[id] ?? []), b];
  return { ...s, overrides };
}

export function withoutBinding(commands: readonly CommandDef[], s: KeymapState, id: string, b: Binding): KeymapState {
  const now = effectiveBindings(commands, s);
  return { ...s, overrides: { ...s.overrides, [id]: (now[id] ?? []).filter((x) => x !== b) } };
}

export function resetCommand(s: KeymapState, id: string): KeymapState {
  const overrides = { ...s.overrides };
  delete overrides[id];
  return { ...s, overrides };
}

export const resetAll = (s: KeymapState): KeymapState => ({ ...s, overrides: {} });
/** Another preset: the user's own changes go, the preset's bindings come. */
export const withPreset = (preset: PresetId): KeymapState => ({ preset, overrides: {} });

const FORMAT = 'lapin-shortcuts';

export function exportKeymap(s: KeymapState): string {
  return JSON.stringify({ format: FORMAT, version: 1, preset: s.preset, overrides: s.overrides }, null, 2);
}

/** A keymap from a file; its bindings in their one spelling, the ones that are not bindings left out. Null: not a shortcuts file. */
export function importKeymap(text: string): KeymapState | null {
  let raw: unknown;
  try { raw = JSON.parse(text); } catch { return null; }
  const o = raw as { format?: unknown; preset?: unknown; overrides?: unknown };
  if (!o || typeof o !== 'object' || o.format !== FORMAT) return null;
  const preset: PresetId = o.preset === 'classic' || o.preset === 'revit' ? o.preset : DEFAULT_PRESET;
  const overrides: Record<string, Binding[]> = {};
  if (o.overrides && typeof o.overrides === 'object') {
    for (const [id, list] of Object.entries(o.overrides as Record<string, unknown>)) {
      if (!Array.isArray(list)) continue;
      overrides[id] = [...new Set(list.map((b) => (typeof b === 'string' ? normalizeBinding(b) : null)).filter((b): b is Binding => !!b))];
    }
  }
  return { preset, overrides };
}

// ─── The user's keymap, kept ───────────────────────────────────────────────

const KEY = 'lapin.shortcuts';

function load(): KeymapState {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) return importKeymap(raw) ?? withPreset(DEFAULT_PRESET);
  } catch { /* no storage: the preset */ }
  return withPreset(DEFAULT_PRESET);
}

export const useKeymap = create<KeymapState>(() => load());

export function setKeymap(s: KeymapState): void {
  useKeymap.setState(s, true);
  try { localStorage.setItem(KEY, exportKeymap(s)); } catch { /* kept for this session */ }
}
