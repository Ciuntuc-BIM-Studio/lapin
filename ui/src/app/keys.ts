/**
 * The keyboard. Which key runs which command is the shortcuts' registry's
 * (shortcuts.ts, commands.ts): chords (Ctrl+S, F3, Delete, 1–9) and the
 * letters typed while nothing is in a field (WA for Wall). What is not a
 * binding is the command line's: typing goes to it (CAD commands and
 * coordinates, Enter or Space to run, Escape to cancel); a sequence is
 * matched against what it holds since it was empty, and only while no
 * command asks for input (the rule is in shortcuts.ts). While a segment is
 * drawn its length and angle are typed at the cursor.
 *
 * While a dialog is open the keys belong to it: Escape closes it, Ctrl/Cmd+S
 * still saves, and nothing else reaches the model (no Delete on the selection
 * behind it, no tool started by two letters). `keyIntent` decides, without
 * touching anything, so it can be tested; `useShortcuts` carries it out.
 */

import { useEffect } from 'react';
import { select, useUi } from './store';
import { findTool, hotbarIds, useToolTick } from '../platform/modules';
import { reach } from './hotbar';
import { cancel, submit, useCad } from '../viewport/cad/session';
import { liveCommit, liveToggleMode, useLive } from '../viewport/liveInput';
import { chordOf, isBareKey, matchSequence, PendingSequence, type Keymap } from './shortcuts';
import { keymap, runCommand } from './commands';
import { useKeymap } from './shortcuts';
import { valueOwner } from './valueOwner';

/** What `keyIntent` needs to know about the key press. */
export interface KeyPress {
  key: string;
  code?: string;
  ctrlKey: boolean;
  metaKey: boolean;
  shiftKey: boolean;
  altKey?: boolean;
  /** The target is an input, select or textarea: the key is theirs. */
  inField: boolean;
  /** The target sits inside an element with role="dialog". */
  inDialog: boolean;
}

/** What `keyIntent` needs to know about the interface. */
export interface KeySnapshot {
  screen: 'start' | 'workspace' | 'join';
  /** The open dialog's id, if any. */
  dialog: string | null;
  /** The active tool, if any. */
  tool: string | null;
  selected: string[];
  /** The command line's text. */
  buffer: string;
  /** The running command's step: what it wants ("text" takes any character). */
  step: { want?: string } | null;
  /** Whether the active tool takes Space (a door's other hand). */
  toolSpace?: boolean;
  /** Whether a command ran before (Enter or Space repeats it). */
  hasLast: boolean;
  /** How far the number keys reach: up to the last quick slot holding a tool. */
  hotbar: number;
  /** The bindings in force (shortcuts.ts). */
  keymap: Keymap;
  /** Letters typed over the view start a tool (default true); off, they only go to the command line (decision 473). */
  letters?: boolean;
  /** The command line takes all typing (the ` key). */
  takeAll?: boolean;
  /** A segment being drawn from a base: its length and angle can be typed (the focused field's text). */
  live?: { active: boolean; text: string; typed: boolean; /** A rectangle tool: Space switches length/angle with width/height. */ rect?: boolean; mode?: 'polar' | 'rect' };
  /** A 3D tool waits for a typed value (valueOwner.ts): what is typed so far. */
  value?: { text: string } | null;
}

export type KeyIntent =
  | { kind: 'command'; id: string }
  /** Letters that are a sequence something longer starts with: typed, and run if nothing more comes. */
  | { kind: 'pending'; id: string; buffer: string }
  | { kind: 'toolSpace' }
  /** The ` key: the command line takes all typing, or gives it back. */
  | { kind: 'cmdMode' }
  | { kind: 'closeDialog' } | { kind: 'deselect' } | { kind: 'cancel' } | { kind: 'submit' } | { kind: 'backspace' }
  | { kind: 'type'; buffer: string }
  | { kind: 'valueType'; key: string } | { kind: 'valueBack' } | { kind: 'valueEnter' }
  | { kind: 'liveType'; key: string } | { kind: 'liveBack' } | { kind: 'liveFocus'; field?: 'len' | 'ang' } | { kind: 'liveClear' } | { kind: 'liveCommit' } | { kind: 'liveMode' };

/** The units a typed length may end in (parseLength reads them). */
const UNIT_SUFFIXES = ['mm', 'cm', 'dm', 'm', 'in', 'ft', "'", '"'];

/** Whether `key` goes on a live length as (part of) its unit: 2400m → 2400mm, 240c → 240cm, 3' → 3'6", 10f → 10ft. */
export function unitKey(text: string, key: string): boolean {
  if (key.length !== 1) return false;
  const m = /\d([a-z]*)$/i.exec(text);
  if (!m) return false;
  const suffix = (m[1] + key).toLowerCase();
  return UNIT_SUFFIXES.some((u) => u.startsWith(suffix));
}

/**
 * Whether a key goes on a value a 3D tool waits for (decision 378): digits, the decimal marks, '+', '-', the angle's '<',
 * CAD's '@', a pair's ';' or 'x' after a number, 'r' (a radius) to begin with, and a unit's letters after a number.
 */
export function valueKey(text: string, key: string): boolean {
  if (key.length !== 1) return false;
  if (/^[0-9.,+\-<@;*×'"]$/.test(key)) return true;
  if ((key === 'r' || key === 'R') && !text.trim()) return true;
  if ((key === 'x' || key === 'X') && /\d\s*$/.test(text)) return true;
  return unitKey(text, key);
}

/** The open dialog's id when one is on the screen: an id with nothing rendered (a dialog that drew nothing) does not hold the keys. */
export function openDialog(id: string | null, rendered: boolean): string | null {
  return id && rendered ? id : null;
}

/** The commands a chord still runs while a dialog is open. */
const IN_DIALOG = new Set(['app.save', 'app.saveAs']);

/** What a key press means, given the interface's state; null when it means nothing here. */
export function keyIntent(e: KeyPress, s: KeySnapshot): KeyIntent | null {
  if (e.inField || s.screen !== 'workspace') return null;
  const chord = chordOf(e);
  if (!chord) return null;
  const mod = e.ctrlKey || e.metaKey || !!e.altKey;
  const bound = s.keymap.chords[chord];
  if (s.dialog || e.inDialog) {
    if (e.key === 'Escape') return s.dialog ? { kind: 'closeDialog' } : null;
    return bound && IN_DIALOG.has(bound) ? { kind: 'command', id: bound } : null;
  }
  // Chords with Ctrl, Alt or Cmd, and the function keys: whatever is typed.
  if (mod || /^(Shift\+)?F\d+$/.test(chord)) return bound ? { kind: 'command', id: bound } : null;
  // The command line's key: it takes all typing (and gives it back), whatever is typed.
  if (bound === 'app.commandLine') return { kind: 'cmdMode' };
  const all = !!s.takeAll;
  // A 3D tool waiting for a value takes the value's keys, before the quick tools (1–9) and the shortcuts (not while the command line takes all).
  if (s.value && !s.buffer && !all) {
    if (e.key === 'Enter') return { kind: 'valueEnter' };
    if (e.key === 'Backspace' && s.value.text) return { kind: 'valueBack' };
    if (valueKey(s.value.text, e.key)) return { kind: 'valueType', key: e.key };
  }
  // Drawing a segment, nothing on the command line: its length and angle are typed at the cursor.
  if (s.live?.active && !s.buffer && !all) {
    if (e.key === 'Tab') return { kind: 'liveFocus' };
    if (e.key === '<') return { kind: 'liveFocus', field: 'ang' };
    // A rectangle: Space switches length/angle with width/height; in width/height a comma goes on to the height.
    if (e.key === ' ' && s.live.rect) return { kind: 'liveMode' };
    if (e.key === ',' && s.live.rect && s.live.mode === 'rect') return { kind: 'liveFocus', field: 'ang' };
    if (e.key === 'Enter' && s.live.typed) return { kind: 'liveCommit' };
    if (e.key === 'Escape' && s.live.typed) return { kind: 'liveClear' };
    if (e.key === 'Backspace' && s.live.text) return { kind: 'liveBack' };
    if (/^[0-9.,+]$/.test(e.key) || (e.key === '-' && (s.live.text || (s.live.rect && s.live.mode === 'rect')))) return { kind: 'liveType', key: e.key };
    if (unitKey(s.live.text, e.key)) return { kind: 'liveType', key: e.key.toLowerCase() };
  }
  const typing = s.buffer.length > 0 || !!s.step;
  // Esc: first the running command (or what is typed), then, with nothing on, the selection lets go (a drilled one steps up first: DrillCrumbs).
  if (e.key === 'Escape') return s.step || s.buffer || all ? { kind: 'cancel' } : !s.tool && s.selected.length ? { kind: 'deselect' } : null;
  if (e.key === ' ' && !typing && s.toolSpace) return { kind: 'toolSpace' };
  if (e.key === 'Enter' || (e.key === ' ' && (typing || s.hasLast) && s.step?.want !== 'text')) {
    // Plan gestures (a floor's outline) take their own Enter.
    // The command line given all typing: Enter with nothing typed gives it back.
    if (!typing && !s.hasLast) return all ? { kind: 'cancel' } : null;
    if (!typing && s.tool && !all) return null;
    return { kind: 'submit' };
  }
  if (e.key === 'Backspace' && s.buffer) return { kind: 'backspace' };
  if (all && e.key === 'Backspace') return null;
  // Delete (or Backspace with nothing typed): the selection, when there is one.
  if (bound === 'app.delete') return s.selected.length && !s.buffer ? { kind: 'command', id: bound } : null;
  // Named keys (Tab, the arrows…) and the bare keys (1–9, +, −, Shift+Z): only while nothing is typed.
  if (bound && !typing && (all ? e.key.length > 1 : isBareKey(chord) || e.key.length > 1)) {
    const slot = /^hotbar\.(\d)$/.exec(bound);
    if (slot && Number(slot[1]) > s.hotbar) return null;
    return { kind: 'command', id: bound };
  }
  // A text step takes any character; otherwise letters, digits and the coordinate signs.
  const allowed = s.step?.want === 'text' ? e.key.length === 1 : /^[\w@<.,\-+]$/.test(e.key);
  if (!allowed) return null;
  const buffer = s.buffer + e.key;
  // Letters that are a shortcut's sequence start its command (when no command is running).
  if (!s.step && !all && s.letters !== false) {
    const m = matchSequence(buffer, s.keymap);
    if (m.kind === 'fire') return { kind: 'command', id: m.id };
    if (m.kind === 'wait') return { kind: 'pending', id: m.id, buffer };
  }
  return { kind: 'type', buffer };
}

/** The tool at the start of a key press, before a view's own Esc handler ends it: that Esc ends the tool and does no more. */
let toolAtKeydown: string | null = null;

function snapshot(): KeySnapshot {
  const ui = useUi.getState();
  const cad = useCad.getState();
  return {
    screen: ui.screen, dialog: openDialog(ui.dialog, !!document.querySelector('[role="dialog"]')), tool: ui.tool ?? toolAtKeydown, selected: ui.selected, toolSpace: !!(ui.tool && findTool(ui.tool)?.space),
    buffer: cad.buffer, step: cad.step ? { want: cad.step.want } : null, hasLast: !!cad.last,
    hotbar: reach(hotbarIds()), keymap: keymap(), letters: useKeymap.getState().letters, takeAll: cad.takeAll,
    live: (() => { const l = useLive.getState(); return { active: !!l.owner, text: l[l.focus], typed: !!(l.len || l.ang), rect: !!l.rect, mode: l.mode }; })(),
    value: (() => { const o = valueOwner(); return o ? { text: o.text() } : null; })(),
  };
}

const pending = new PendingSequence();

// The command line given all typing keeps it while a command asks for input, and gives it back when the command ends.
useCad.subscribe((cur, prev) => { if (cur.takeAll && prev.step && !cur.step) useCad.setState({ takeAll: false }); });

export function carryOut(intent: KeyIntent): void {
  if (intent.kind !== 'pending') pending.clear();
  switch (intent.kind) {
    case 'command': useCad.setState({ buffer: '' }); runCommand(intent.id); break;
    case 'pending': {
      useCad.setState({ buffer: intent.buffer });
      pending.arm(intent.id, intent.buffer, Date.now());
      setTimeout(() => {
        const cad = useCad.getState();
        const id = pending.due(Date.now(), cad.buffer, !!cad.step);
        if (id) { useCad.setState({ buffer: '' }); runCommand(id); }
      }, pending.delay + 10);
      break;
    }
    case 'toolSpace': { const t = useUi.getState().tool; if (t) findTool(t)?.space?.(); useToolTick.setState((s) => ({ n: s.n + 1 })); break; }
    case 'closeDialog': useUi.setState({ dialog: null }); break;
    case 'deselect': select(null); break;
    case 'cancel': cancel(); useCad.setState({ takeAll: false }); break;
    case 'cmdMode': useCad.setState((c) => ({ takeAll: !c.takeAll })); break;
    case 'submit': {
      const had = !!useCad.getState().step;
      // The command line given all typing keeps it while a command asks for input; when what was typed ran and nothing asks for more, it is given back.
      void submit(useCad.getState().buffer).then(() => { if (!had && !useCad.getState().step) useCad.setState({ takeAll: false }); });
      break;
    }
    case 'backspace': useCad.setState((s) => ({ buffer: s.buffer.slice(0, -1) })); break;
    case 'type': useCad.setState({ buffer: intent.buffer }); break;
    case 'valueType': { const o = valueOwner(); if (o) o.set(o.text() + intent.key); break; }
    case 'valueBack': { const o = valueOwner(); if (o) o.set(o.text().slice(0, -1)); break; }
    case 'valueEnter': valueOwner()?.enter(); break;
    case 'liveType': useLive.setState((l) => ({ [l.focus]: l[l.focus] + intent.key })); break;
    case 'liveBack': useLive.setState((l) => ({ [l.focus]: l[l.focus].slice(0, -1) })); break;
    case 'liveFocus': useLive.setState((l) => ({ focus: intent.field ?? (l.focus === 'len' ? 'ang' : 'len') })); break;
    case 'liveClear': useLive.setState({ len: '', ang: '' }); break;
    case 'liveCommit': liveCommit(); break;
    case 'liveMode': liveToggleMode(); break;
  }
}

/** A key event as the window listener gets it (the fields read). */
export type KeyEventLike = Pick<KeyboardEvent, 'key' | 'code' | 'ctrlKey' | 'metaKey' | 'shiftKey' | 'altKey' | 'defaultPrevented' | 'target' | 'preventDefault'>;

/** The window's keydown: what the key means here, carried out (the state and the doing given for tests). */
export function handleKeydown(e: KeyEventLike, snap: () => KeySnapshot = snapshot, act: (i: KeyIntent) => void = carryOut): void {
  // A view or a panel that took the key first (Tab over a shared grip) keeps it.
  if (e.defaultPrevented) return;
  const target = e.target as HTMLElement | null;
  const press: KeyPress = {
    key: e.key, code: e.code, ctrlKey: e.ctrlKey, metaKey: e.metaKey, shiftKey: e.shiftKey, altKey: e.altKey,
    inField: !!target?.closest?.('input, select, textarea'),
    inDialog: !!target?.closest?.('[role="dialog"]'),
  };
  const intent = keyIntent(press, snap());
  if (!intent) return;
  e.preventDefault();
  act(intent);
}

export function useShortcuts(): void {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { handleKeydown(e); toolAtKeydown = null; };
    const first = () => { toolAtKeydown = useUi.getState().tool; };
    window.addEventListener('keydown', first, true);
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('keydown', first, true); window.removeEventListener('keydown', onKey); };
  }, []);
}
