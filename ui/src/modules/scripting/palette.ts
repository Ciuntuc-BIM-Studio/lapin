/**
 * A script's palette (`lapin.ui.palette`, decision 528), its logic: the rows a script registered (python/lapinbim/_ui.py,
 * said back by the kernel after a Run from the Script panel) read into what the window draws itself — headings, texts,
 * separators, fields of the kinds `lapin.ui.ask` has (ask.ts reads them), buttons alone or side by side — never anything
 * of the script's to run; and what a palette remembers on this computer (where it floats, what its fields hold). Kept
 * apart from what draws it (PaletteWindow.tsx), for the tests.
 */

import { readField, type AskField, type FieldState } from './ask';

export interface PaletteButton { name: string; icon?: string; tooltip?: string; call: string | null }

export type PaletteRow =
  | { row: 'heading'; text: string }
  | { row: 'text'; text: string }
  | { row: 'separator' }
  | { row: 'field'; field: AskField }
  | { row: 'buttons'; buttons: PaletteButton[] };

/** The most of each a palette keeps (as python/lapinbim/_ui.py allows), and the most buttons side by side. */
export const PALETTE_ROWS = 200, PALETTE_FIELDS = 64, PALETTE_BUTTONS = 50, ROW_BUTTONS = 8;

const PY_NAME = /^[A-Za-z_]\w{0,99}$/;
const str = (v: unknown, max: number): string | undefined => (typeof v === 'string' && v.trim() ? v.trim().slice(0, max) : undefined);

function readButton(raw: unknown, seen: Set<string>): PaletteButton | null {
  if (!raw || typeof raw !== 'object') return null;
  const b = raw as Record<string, unknown>;
  const name = str(b.name, 120);
  if (!name || seen.has(name)) return null;
  seen.add(name);
  const out: PaletteButton = { name, call: typeof b.call === 'string' && PY_NAME.test(b.call) ? b.call : null };
  const icon = str(b.icon, 40); if (icon) out.icon = icon;
  const tooltip = str(b.tooltip, 300); if (tooltip) out.tooltip = tooltip;
  return out;
}

/** A palette's rows as registered, read so that only what the window draws is kept (a button stands once, a field's key once). */
export function readRows(raw: unknown): PaletteRow[] {
  if (!Array.isArray(raw)) return [];
  const rows: PaletteRow[] = [];
  const keys = new Set<string>(), names = new Set<string>();
  for (const r of raw.slice(0, PALETTE_ROWS)) {
    if (!r || typeof r !== 'object') continue;
    const o = r as Record<string, unknown>;
    switch (o.row) {
      case 'heading':
      case 'text': {
        const text = str(o.text, 2000);
        if (text) rows.push({ row: o.row, text });
        break;
      }
      case 'separator':
        if (rows.length && rows[rows.length - 1].row !== 'separator') rows.push({ row: 'separator' });
        break;
      case 'field': {
        if (keys.size >= PALETTE_FIELDS) break;
        const field = readField(o.field, keys);
        if (field) rows.push({ row: 'field', field });
        break;
      }
      case 'button':
      case 'buttons': {
        const list = (o.row === 'button' ? [o] : Array.isArray(o.buttons) ? o.buttons.slice(0, ROW_BUTTONS) : [])
          .map((b) => (names.size < PALETTE_BUTTONS ? readButton(b, names) : null)).filter((b): b is PaletteButton => !!b);
        if (list.length) rows.push({ row: 'buttons', buttons: list });
        break;
      }
    }
  }
  return rows;
}

/** A palette's fields, in order. */
export const fieldsOf = (rows: readonly PaletteRow[]): AskField[] => rows.flatMap((r) => (r.row === 'field' ? [r.field] : []));

/** What a palette remembers on this computer: where it floats, what its fields hold. */
export interface PaletteMemory { x?: number; y?: number; state?: Record<string, FieldState> }

/** A palette's memory as kept, read so that anything unreadable is left out; only the fields it has now, of the shape their kind holds. */
export function readMemory(stored: unknown, fields: readonly AskField[]): PaletteMemory {
  if (!stored || typeof stored !== 'object') return {};
  const o = stored as Record<string, unknown>;
  const out: PaletteMemory = {};
  if (typeof o.x === 'number' && Number.isFinite(o.x)) out.x = o.x;
  if (typeof o.y === 'number' && Number.isFinite(o.y)) out.y = o.y;
  if (o.state && typeof o.state === 'object') {
    const s = o.state as Record<string, unknown>;
    const state: Record<string, FieldState> = {};
    for (const f of fields) {
      const v = s[f.key];
      const fits = f.kind === 'bool' ? typeof v === 'boolean'
        : f.kind === 'choice' ? typeof v === 'number' && Number.isInteger(v) && v >= 0 && v < (f.choices?.length ?? 0)
          : f.kind === 'element' ? Array.isArray(v) && v.every((x) => typeof x === 'string')
            : typeof v === 'string';
      if (fits) state[f.key] = v as FieldState;
    }
    out.state = state;
  }
  return out;
}

/** Where a palette floats, kept inside the window (its header reachable). */
export function clampAt(x: number, y: number, win: { w: number; h: number }, size = { w: 280, h: 40 }): { x: number; y: number } {
  return { x: Math.max(0, Math.min(x, win.w - Math.min(size.w, win.w))), y: Math.max(0, Math.min(y, win.h - size.h)) };
}
