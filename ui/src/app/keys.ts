/**
 * The keyboard: Ctrl/Cmd shortcuts (undo, redo, new, open, save); typing goes
 * to the command line (CAD commands and coordinates, Enter or Space to run,
 * Escape to cancel), where Revit's two-letter shortcuts (WA for Wall…) start
 * their tool at once; Delete for the selection; 1–9 for the hotbar when
 * nothing is being typed; F3 object snap, F8 ortho, F10 polar tracking;
 * + and − zoom the 2D view in front, Shift+Z zooms to the selection.
 *
 * While a dialog is open the keys belong to it: Escape closes it, Ctrl/Cmd+S
 * still saves, and nothing else reaches the model (no Delete on the selection
 * behind it, no tool started by two letters). `keyIntent` decides, without
 * touching anything, so it can be tested; `useShortcuts` carries it out.
 */

import { useEffect } from 'react';
import { select, useUi } from './store';
import { activate, allTools, hotbarTools } from '../platform/modules';
import { execute, redo, undo } from '../engine/session';
import { newProject, openProject, saveProject } from '../engine/files';
import { cancel, submit, useCad } from '../viewport/cad/session';
import { zoomHost } from '../viewport/zoom';
import { liveCommit, useLive } from '../viewport/liveInput';

/** What `keyIntent` needs to know about the key press. */
export interface KeyPress {
  key: string;
  ctrlKey: boolean;
  metaKey: boolean;
  shiftKey: boolean;
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
  /** Two-letter shortcuts, lower case, to the tool they start. */
  shortcuts: Record<string, string>;
  /** A segment being drawn from a base: its length and angle can be typed (the focused field's text). */
  live?: { active: boolean; text: string; typed: boolean };
}

export type KeyIntent =
  | { kind: 'undo' } | { kind: 'redo' } | { kind: 'new' } | { kind: 'open' } | { kind: 'save'; as: boolean }
  | { kind: 'toggle'; what: 'osnap' | 'ortho' | 'polar' }
  | { kind: 'closeDialog' } | { kind: 'cancel' } | { kind: 'submit' } | { kind: 'backspace' }
  | { kind: 'delete'; ids: string[] } | { kind: 'hotbar'; slot: number }
  | { kind: 'type'; buffer: string } | { kind: 'tool'; id: string }
  | { kind: 'zoom'; factor: number } | { kind: 'zoomSelection' }
  | { kind: 'liveType'; key: string } | { kind: 'liveBack' } | { kind: 'liveFocus'; field?: 'len' | 'ang' } | { kind: 'liveClear' } | { kind: 'liveCommit' };

const TOGGLES: Record<string, 'osnap' | 'ortho' | 'polar'> = { F3: 'osnap', F8: 'ortho', F10: 'polar' };

/** What a key press means, given the interface's state; null when it means nothing here. */
export function keyIntent(e: KeyPress, s: KeySnapshot): KeyIntent | null {
  if (e.inField || s.screen !== 'workspace') return null;
  const mod = e.ctrlKey || e.metaKey;
  const k = e.key.toLowerCase();
  if (s.dialog || e.inDialog) {
    if (e.key === 'Escape') return s.dialog ? { kind: 'closeDialog' } : null;
    if (mod && k === 's') return { kind: 'save', as: e.shiftKey };
    return null;
  }
  if (mod) {
    switch (k) {
      case 'z': return e.shiftKey ? { kind: 'redo' } : { kind: 'undo' };
      case 'y': return { kind: 'redo' };
      case 'n': return { kind: 'new' };
      case 'o': return { kind: 'open' };
      case 's': return { kind: 'save', as: e.shiftKey };
      default: return null;
    }
  }
  if (TOGGLES[e.key]) return { kind: 'toggle', what: TOGGLES[e.key] };
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
  if (e.key === 'Delete' || e.key === 'Backspace') return s.selected.length ? { kind: 'delete', ids: s.selected } : null;
  const n = Number(e.key);
  if (!typing && e.key.length === 1 && n >= 1 && n <= 9) return n <= s.hotbar ? { kind: 'hotbar', slot: n } : null;
  // Zoom keys, when nothing is being typed (a − may otherwise start a coordinate).
  if (!typing && (e.key === '+' || e.key === '=')) return { kind: 'zoom', factor: 1.25 };
  if (!typing && e.key === '-') return { kind: 'zoom', factor: 0.8 };
  if (!typing && e.key === 'Z' && e.shiftKey) return { kind: 'zoomSelection' };
  // A text step takes any character; otherwise letters, digits and the coordinate signs.
  const allowed = s.step?.want === 'text' ? e.key.length === 1 : /^[\w@<.,\-+]$/.test(e.key);
  if (!allowed) return null;
  const buffer = s.buffer + e.key;
  // Two letters that are a tool's shortcut start it at once (when no command is running).
  if (!s.step && /^[a-z]{2}$/i.test(buffer) && s.shortcuts[buffer.toLowerCase()]) return { kind: 'tool', id: s.shortcuts[buffer.toLowerCase()] };
  return { kind: 'type', buffer };
}

function snapshot(): KeySnapshot {
  const ui = useUi.getState();
  const cad = useCad.getState();
  const shortcuts: Record<string, string> = {};
  for (const t of allTools()) if (t.shortcut) shortcuts[t.shortcut.toLowerCase()] = t.id;
  return {
    screen: ui.screen, dialog: ui.dialog, tool: ui.tool, selected: ui.selected,
    buffer: cad.buffer, step: cad.step ? { want: cad.step.want } : null, hasLast: !!cad.last,
    hotbar: hotbarTools().length, shortcuts,
    live: (() => { const l = useLive.getState(); return { active: !!l.owner, text: l[l.focus], typed: !!(l.len || l.ang) }; })(),
  };
}

function carryOut(intent: KeyIntent): void {
  switch (intent.kind) {
    case 'undo': void undo(); break;
    case 'redo': void redo(); break;
    case 'new': void newProject(); break;
    case 'open': void openProject(); break;
    case 'save': void saveProject(intent.as); break;
    case 'toggle': useCad.setState((s) => ({ [intent.what]: !s[intent.what] })); break;
    case 'closeDialog': useUi.setState({ dialog: null }); break;
    case 'cancel': cancel(); break;
    case 'submit': void submit(useCad.getState().buffer); break;
    case 'backspace': useCad.setState((s) => ({ buffer: s.buffer.slice(0, -1) })); break;
    case 'delete': void execute('element.delete', { ids: intent.ids }); select(null); break;
    case 'hotbar': { const t = hotbarTools()[intent.slot - 1]; if (t) activate(t); break; }
    case 'type': useCad.setState({ buffer: intent.buffer }); break;
    case 'tool': { useCad.setState({ buffer: '' }); const t = allTools().find((x) => x.id === intent.id); if (t) activate(t); break; }
    case 'zoom': zoomHost()?.zoomBy(intent.factor); break;
    case 'liveType': useLive.setState((l) => ({ [l.focus]: l[l.focus] + intent.key })); break;
    case 'liveBack': useLive.setState((l) => ({ [l.focus]: l[l.focus].slice(0, -1) })); break;
    case 'liveFocus': useLive.setState((l) => ({ focus: intent.field ?? (l.focus === 'len' ? 'ang' : 'len') })); break;
    case 'liveClear': useLive.setState({ len: '', ang: '' }); break;
    case 'liveCommit': liveCommit(); break;
    case 'zoomSelection': zoomHost()?.fitSelection(); break;
  }
}

export function useShortcuts(): void {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      const press: KeyPress = {
        key: e.key, ctrlKey: e.ctrlKey, metaKey: e.metaKey, shiftKey: e.shiftKey,
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
