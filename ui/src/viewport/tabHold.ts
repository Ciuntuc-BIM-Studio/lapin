/**
 * Holding Tab (issue 110, after the tap that cycles: pickCycle.ts, decision 672). A tap of Tab lights the next element under
 * the pointer as before; HOLDING it for `HOLD_MS` opens a small list that follows the pointer with every element under it. The
 * mouse wheel moves through the list (down: the next), the current one is lit in the view, and letting go of Tab takes it
 * exactly as a click would. The list is recomputed as the pointer moves. The model here is pure (the phases, the wheel, the
 * list's rows); the timer and the window's keys are `tabHoldDown`, `tabHoldUp` and `holdWheel`.
 *
 * Only pick gestures and selection hold a list: a view offers candidates (`CycleSource.ids`) only when nothing else is busy,
 * so a drag in progress (where Tab means autohold: app/autohold.ts) never opens one.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import { clearCycle, cycleSource, usePickCycle } from './pickCycle';

/** How long Tab is held before it is not a tap any more (ms). */
export const HOLD_MS = 250;

export type HoldPhase = 'idle' | 'pending' | 'open';

/** What a key-down of Tab does: `pass` leaves it to the Tab command as before; `swallow` is ours. */
export type DownVerdict = 'pass' | 'swallow';
/** What a key-up of Tab does: a tap runs the Tab command now; a take selects the current element; none: nothing. */
export type UpVerdict = 'tap' | 'take' | 'none';

/** Tab went down: whether it starts (or belongs to) a hold. A repeat with nothing pending is the browser's key repeat: it passes. */
export function downVerdict(phase: HoldPhase, repeat: boolean, candidates: number): DownVerdict {
  if (phase !== 'idle') return 'swallow';
  if (repeat) return 'pass';
  return candidates >= 2 ? 'swallow' : 'pass';
}

/** Tab came up: a tap before the threshold, a take once the list is open. */
export function upVerdict(phase: HoldPhase): UpVerdict {
  return phase === 'pending' ? 'tap' : phase === 'open' ? 'take' : 'none';
}

/** The index after a wheel notch (positive deltaY: down, the next one), going round; 0 for an empty list. */
export function wheelIndex(index: number, count: number, deltaY: number): number {
  if (count <= 0) return 0;
  const dir = deltaY > 0 ? 1 : deltaY < 0 ? -1 : 0;
  return (((index + dir) % count) + count) % count;
}

/** The list after the pointer moved: the lit element stays lit when it is still there, else the first one (what a click takes). */
export function refreshList(prev: { ids: string[]; index: number }, ids: string[]): { ids: string[]; index: number } {
  const lit = prev.ids[prev.index];
  const at = lit ? ids.indexOf(lit) : -1;
  return { ids, index: at >= 0 ? at : 0 };
}

export interface HoldRow { id: string; current: boolean }
/** The list's rows, the current one marked. */
export const holdRows = (ids: string[], index: number): HoldRow[] => ids.map((id, i) => ({ id, current: i === index }));

interface HoldStore { open: boolean; ids: string[]; index: number; key: string; client: { x: number; y: number } | null }
export const useTabHold = create<HoldStore>(() => ({ open: false, ids: [], index: 0, key: '', client: null }));

let phase: HoldPhase = 'idle';
let timer: ReturnType<typeof setTimeout> | null = null;
let client: { x: number; y: number } | null = null;
/** The window's pointer, in client pixels (where the list is drawn). */
export const holdPointer = (x: number, y: number): void => {
  client = { x, y };
  if (phase === 'open') useTabHold.setState({ client });
};
export const holdPhase = (): HoldPhase => phase;

const lit = (key: string, at: Vec2, ids: string[], index: number) => usePickCycle.setState({ cycle: { key, at, ids, index } });

function close(): void {
  phase = 'idle';
  if (timer) { clearTimeout(timer); timer = null; }
  if (useTabHold.getState().open) useTabHold.setState({ open: false, ids: [], index: 0 });
}

function openList(): void {
  timer = null;
  const s = cycleSource();
  const at = s?.at();
  const ids = s && at ? s.ids(at) : [];
  if (!s || !at || ids.length < 2) { phase = 'idle'; return; }
  phase = 'open';
  useTabHold.setState({ open: true, ids, index: 0, key: s.key, client });
  lit(s.key, at, ids, 0);
}

/** The pointer moved over the view (its handler calls this after it has set the pointer): the list follows it. */
export function holdRefresh(): void {
  if (phase !== 'open') return;
  const s = cycleSource();
  const at = s?.at();
  if (!s || !at) return;
  const now = refreshList(useTabHold.getState(), s.ids(at));
  useTabHold.setState({ ...now, key: s.key });
  if (now.ids.length > 1) lit(s.key, at, now.ids, now.index);
  else clearCycle();
}

/** Tab went down. */
export function tabHoldDown(repeat: boolean): DownVerdict {
  const s = cycleSource();
  const at = s?.at();
  const v = downVerdict(phase, repeat, phase === 'idle' && s && at ? s.ids(at).length : 0);
  if (v === 'swallow' && phase === 'idle') {
    phase = 'pending';
    timer = setTimeout(openList, HOLD_MS);
  }
  return v;
}

/** Tab came up: `tap` means the caller runs the Tab command now (the hold never began); `take` took the current element. */
export function tabHoldUp(): UpVerdict {
  const v = upVerdict(phase);
  const s = v === 'take' ? cycleSource() : null;
  close();
  s?.take?.();
  return v;
}

/** The wheel while the list is open: it moves through it (true: the wheel was ours). */
export function holdWheel(deltaY: number): boolean {
  if (phase !== 'open') return false;
  const h = useTabHold.getState();
  const index = wheelIndex(h.index, h.ids.length, deltaY);
  useTabHold.setState({ index });
  const s = cycleSource();
  const at = s?.at();
  if (s && at && h.ids.length > 1) lit(s.key, at, h.ids, index);
  return true;
}

/** The window lost focus: the list closes and takes nothing. */
export function holdCancel(): void { if (phase !== 'idle') { close(); clearCycle(); } }
