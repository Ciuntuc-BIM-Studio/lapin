/**
 * Preselection and selection colours (forum topic 97). What the cursor is over lights up before a click (the preselection),
 * and what is selected shows in its own colour. Both are the user's: the preselection on or off, each colour chosen or left
 * as the theme's accent (null; today's look). Kept on this computer like the other interface preferences (`lapin.highlight`),
 * applied to the 2D views through CSS variables on the page's root (`--hl-select`, `--hl-pre`, and their washes) and to the 3D
 * view through {@link selectionColour} and {@link preselectionColour}.
 */

import { create } from 'zustand';

export interface Highlight {
  /** The element under the cursor lights up before a click. */
  preselect: boolean;
  /** #rrggbb, or null: the theme's accent. */
  preselectColour: string | null;
  selectColour: string | null;
}

export const DEFAULT_HIGHLIGHT: Highlight = { preselect: true, preselectColour: null, selectColour: null };

/** What 3D has always drawn the selection and the element under the cursor in (the paper theme's accent). */
export const ACCENT_3D = '#b5532d';

const KEY = 'lapin.highlight';
const HEX = /^#[0-9a-f]{6}$/i;
const colour = (v: unknown): string | null => (typeof v === 'string' && HEX.test(v) ? v.toLowerCase() : null);

/** What was kept, anything unreadable at its default. */
export function readHighlight(stored: unknown): Highlight {
  const o = (stored && typeof stored === 'object' ? stored : {}) as Partial<Record<keyof Highlight, unknown>>;
  return { preselect: o.preselect !== false, preselectColour: colour(o.preselectColour), selectColour: colour(o.selectColour) };
}

function read(): Highlight {
  try { return readHighlight(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return readHighlight(null); }
}

export const useHighlight = create<Highlight>(() => read());

/** The CSS variables a setting asks of the page's root (a colour not chosen sets none: the theme's accent stays). */
export function highlightVars(h: Highlight): Record<string, string> {
  const v: Record<string, string> = {};
  if (h.selectColour) { v['--hl-select'] = h.selectColour; v['--hl-select-wash'] = `color-mix(in srgb, ${h.selectColour} 18%, transparent)`; }
  if (h.preselectColour) v['--hl-pre'] = h.preselectColour;
  return v;
}

const VARS = ['--hl-select', '--hl-select-wash', '--hl-pre'];

/** Puts the settings on the page: the variables, and a class that turns the preselection's CSS off. */
export function applyHighlight(h: Highlight = useHighlight.getState()): void {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;
  const vars = highlightVars(h);
  for (const k of VARS) { if (vars[k]) root.style.setProperty(k, vars[k]); else root.style.removeProperty(k); }
  root.classList.toggle('no-preselect', !h.preselect);
}

export function setHighlight(patch: Partial<Highlight>): void {
  useHighlight.setState(readHighlight({ ...useHighlight.getState(), ...patch }));
  try { localStorage.setItem(KEY, JSON.stringify(useHighlight.getState())); } catch { /* kept for this session */ }
  applyHighlight();
}

/** The 3D view's colours: the selection's, and the preselection's (its own, else the selection's). */
export const selectionColour = (h: Highlight = useHighlight.getState()): string => h.selectColour ?? ACCENT_3D;
export const preselectionColour = (h: Highlight = useHighlight.getState()): string => h.preselectColour ?? selectionColour(h);

applyHighlight();
