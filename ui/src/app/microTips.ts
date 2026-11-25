/**
 * Just-in-Time Micro-Tips (docs/plan-ux-game-like.md, P9):
 * gently surfaces a one-time shortcut hint when the user clicks the same
 * ribbon tool 3 times manually instead of using its keyboard shortcut.
 */

import { notify } from './store';

const SHOWN_KEY = 'lapin.tips.shown';
const THRESHOLD = 3;

const counts = new Map<string, number>();
const shownMemory = new Set<string>();

function readShown(): Set<string> {
  const out = new Set(shownMemory);
  try {
    const raw = localStorage.getItem(SHOWN_KEY);
    const arr = raw ? JSON.parse(raw) : [];
    if (Array.isArray(arr)) for (const x of arr) if (typeof x === 'string') out.add(x);
  } catch { /* session only */ }
  return out;
}

function markShown(id: string): void {
  shownMemory.add(id);
  const s = readShown();
  s.add(id);
  try { localStorage.setItem(SHOWN_KEY, JSON.stringify([...s])); } catch { /* session only */ }
}

/** Records a manual ribbon click; returns the tip string once when threshold is hit, else null. */
export function recordToolClick(toolId: string, label: string, shortcut: string | undefined): string | null {
  if (!shortcut) return null;
  const shown = readShown();
  if (shown.has(toolId)) return null;
  const next = (counts.get(toolId) ?? 0) + 1;
  counts.set(toolId, next);
  if (next < THRESHOLD) return null;
  markShown(toolId);
  const msg = `Tip: press ${shortcut} to start ${label} instantly.`;
  notify(msg);
  return msg;
}

export function resetMicroTips(): void {
  counts.clear();
  shownMemory.clear();
  try { localStorage.removeItem(SHOWN_KEY); } catch { /* session only */ }
}
