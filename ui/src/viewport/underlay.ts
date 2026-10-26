/**
 * The underlay of a plan: another storey's drawing under this one's, faded,
 * not picked or edited, but snapped to (a wall over the one below). Chosen
 * per storey shown and remembered in this browser; not part of the project.
 */

import { create } from 'zustand';

const KEY = 'lapin.underlay';

function read(): Record<string, string> {
  try { return JSON.parse(localStorage.getItem(KEY) ?? '{}') as Record<string, string>; } catch { return {}; }
}

export const useUnderlay = create<{ of: Record<string, string> }>(() => ({ of: read() }));

/** Shows <paramref name="under"/> beneath the plan of <paramref name="storeyId"/> (null: none). */
export function setUnderlay(storeyId: string, under: string | null): void {
  const of = { ...useUnderlay.getState().of };
  if (under) of[storeyId] = under; else delete of[storeyId];
  useUnderlay.setState({ of });
  try { localStorage.setItem(KEY, JSON.stringify(of)); } catch { /* the choice lasts this session */ }
}
