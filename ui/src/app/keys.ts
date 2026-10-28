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
import { useUi } from './store';
import { hotbarTools } from '../platform/modules';
import { cancel, submit, useCad } from '../viewport/cad/session';
import { liveCommit, useLive } from '../viewport/liveInput';
import { chordOf, isBareKey, matchSequence, PendingSequence, type Keymap } from './shortcuts';
import { keymap, runCommand } from './commands';

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
  /** Whether a command ran before (Enter or Space repeats it). */
  hasLast: boolean;
  /** How many hotbar slots there are. */
  hotbar: number;
  /** The bindings in force (shortcuts.ts). */
  keymap: Keymap;
  /** A segment being drawn from a base: its length and angle can be typed (the focused field's text). */
  live?: { active: boolean; text: string; typed: boolean };
}

export type KeyIntent =
  | { kind: 'command'; id: string }
  /** Letters that are a sequence something longer starts with: typed, and run if nothing more comes. */
  | { kind: 'pending'; id: string; buffer: string }
  | { kind: 'closeDialog' } | { kind: 'cancel' } | { kind: 'submit' } | { kind: 'backspace' }
  | { kind: 'type'; buffer: string }
  | { kind: 'liveType'; key: string } | { kind: 'liveBack' } | { kind: 'liveFocus'; field?: 'len' | 'ang' } | { kind: 'liveClear' } | { kind: 'liveCommit' };

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
  // Drawing a segment, nothing on the command line: its length and angle are typed at the cursor.
  if (s.live?.active && !s.buffer) {
    if (e.key === 'Tab') return { kind: 'liveFocus' };
    if (e.key === '<') return { kind: 'liveFocus', field: 'ang' };
    if (e.key === 'Enter' && s.live.typed) return { kind: 'liveCommit' };
    if (e.key === 'Escape' && s.live.typed) return { kind: 'liveClear' };
    if (e.key === 'Backspace' && s.live.text) return { kind: 'liveBack' };
    if (/^[0-9.,+]$/.test(e.key) || (e.key === '-' && s.live.text)) return { kind: 'liveType', key: e.key };
    if (/^[mcd]$/i.test(e.key) && /\d$/.test(s.live.text)) return { kind: 'liveType', key: e.key.toLowerCase() };
  }
  const typing = s.buffer.length > 0 || !!s.step;
  if (e.key === 'Escape') return s.step || s.buffer ? { kind: 'cancel' } : null;
  if (e.key === 'Enter' || (e.key === ' ' && (typing || s.hasLast) && s.step?.want !== 'text')) {
    // Plan gestures (a floor's outline) take their own Enter.
    if (!typing && !s.hasLast) return null;
    if (!typing && s.tool) return null;
    return { kind: 'submit' };
  }
  if (e.key === 'Backspace' && s.buffer) return { kind: 'backspace' };
  // Delete (or Backspace with nothing typed): the selection, when there is one.
  if (bound === 'app.delete') return s.selected.length && !s.buffer ? { kind: 'command', id: bound } : null;
  // Named keys (Tab, the arrows…) and the bare keys (1–9, +, −, Shift+Z): only while nothing is typed.
  if (bound && !typing && (isBareKey(chord) || e.key.length > 1)) {
    const slot = /^hotbar\.(\d)$/.exec(bound);
    if (slot && Number(slot[1]) > s.hotbar) return null;
    return { kind: 'command', id: bound };
  }
  // A text step takes any character; otherwise letters, digits and the coordinate signs.
  const allowed = s.step?.want === 'text' ? e.key.length === 1 : /^[\w@<.,\-+]$/.test(e.key);
  if (!allowed) return null;
  const buffer = s.buffer + e.key;
  // Letters that are a shortcut's sequence start its command (when no command is running).
  if (!s.step) {
    const m = matchSequence(buffer, s.keymap);
    if (m.kind === 'fire') return { kind: 'command', id: m.id };
    if (m.kind === 'wait') return { kind: 'pending', id: m.id, buffer };
  }
  return { kind: 'type', buffer };
}

function snapshot(): KeySnapshot {
  const ui = useUi.getState();
  const cad = useCad.getState();
  return {
    screen: ui.screen, dialog: ui.dialog, tool: ui.tool, selected: ui.selected,
    buffer: cad.buffer, step: cad.step ? { want: cad.step.want } : null, hasLast: !!cad.last,
    hotbar: hotbarTools().length, keymap: keymap(),
    live: (() => { const l = useLive.getState(); return { active: !!l.owner, text: l[l.focus], typed: !!(l.len || l.ang) }; })(),
  };
}

const pending = new PendingSequence();

function carryOut(intent: KeyIntent): void {
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
    case 'closeDialog': useUi.setState({ dialog: null }); break;
    case 'cancel': cancel(); break;
    case 'submit': void submit(useCad.getState().buffer); break;
    case 'backspace': useCad.setState((s) => ({ buffer: s.buffer.slice(0, -1) })); break;
    case 'type': useCad.setState({ buffer: intent.buffer }); break;
    case 'liveType': useLive.setState((l) => ({ [l.focus]: l[l.focus] + intent.key })); break;
    case 'liveBack': useLive.setState((l) => ({ [l.focus]: l[l.focus].slice(0, -1) })); break;
    case 'liveFocus': useLive.setState((l) => ({ focus: intent.field ?? (l.focus === 'len' ? 'ang' : 'len') })); break;
    case 'liveClear': useLive.setState({ len: '', ang: '' }); break;
    case 'liveCommit': liveCommit(); break;
  }
}

export function useShortcuts(): void {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      // A view or a panel that took the key first (Tab over a shared grip) keeps it.
      if (e.defaultPrevented) return;
      const target = e.target as HTMLElement | null;
      const press: KeyPress = {
        key: e.key, code: e.code, ctrlKey: e.ctrlKey, metaKey: e.metaKey, shiftKey: e.shiftKey, altKey: e.altKey,
        inField: !!target?.closest?.('input, select, textarea'),
        inDialog: !!target?.closest?.('[role="dialog"]'),
      };
      const intent = keyIntent(press, snapshot());
      if (!intent) return;
      e.preventDefault();
      carryOut(intent);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
}
