/** The aligned and linear dimension tool's own parts: what it remembers, how its direction is chosen, how a string previews, and its bar. */

import { create } from 'zustand';
import type { Vec2 } from '../../platform/module';
import { finishStep, useCad } from '../../viewport/cad/session';
import { useLive } from '../../viewport/liveInput';
import { formatLength } from '../../engine/unitsStore';

/** `continuous`: points go on as a string (remembered); `orient`: the linear dimension's direction, once chosen; `forced`: Tab chose it; `count`: the string's points so far. */
interface DimTool { continuous: boolean; orient: 'h' | 'v' | null; forced: boolean; count: number }

const KEY = 'lapin.dimension.continuous';
const stored = (): boolean => { try { return localStorage.getItem(KEY) === '1'; } catch { return false; } };
export const useDimTool = create<DimTool>(() => ({ continuous: stored(), orient: null, forced: false, count: 0 }));

/** Continuous or single, kept for the next time. */
export function setContinuous(on: boolean): void {
  useDimTool.setState({ continuous: on });
  try { localStorage.setItem(KEY, on ? '1' : '0'); } catch { /* kept for this session */ }
}

/**
 * A linear dimension's direction from where its line is (the cursor against the middle of the two points): horizontal
 * when it is above or below, vertical when beside. Once chosen it holds until the cursor is clearly on the other side
 * (past 60° towards it from the horizontal, or 30° from it), and Tab's choice (`forced`) holds for good.
 */
export function pickDirection(a: Vec2, b: Vec2, at: Vec2, prev: 'h' | 'v' | null, forced = false): 'h' | 'v' {
  if (prev && forced) return prev;
  const dx = Math.abs(at.x - (a.x + b.x) / 2), dy = Math.abs(at.y - (a.y + b.y) / 2);
  // On the middle itself there is no side to read: keep what was.
  if (prev && Math.hypot(dx, dy) < 1e-9) return prev;
  const angle = (Math.atan2(dy, dx) * 180) / Math.PI; // 0: beside, 90: above or below
  if (!prev) return angle >= 45 ? 'h' : 'v';
  if (prev === 'h') return angle < 30 ? 'v' : 'h';
  return angle > 60 ? 'h' : 'v';
}

/** A click on the string's last point again (within `tol`) finishes it, as a double-click does. */
export const isSamePoint = (p: Vec2, q: Vec2, tol: number): boolean => Math.hypot(p.x - q.x, p.y - q.y) <= tol;

/** The values a string shows while it is drawn: each segment's length along its line, in the project's unit, at the segment's middle on the line. */
export function segmentLabels(pts: Vec2[], at: Vec2, u: Vec2): { at: Vec2; text: string }[] {
  const n = { x: -u.y, y: u.x };
  const onLine = pts.map((p) => { const o = (at.x - p.x) * n.x + (at.y - p.y) * n.y; return { x: p.x + n.x * o, y: p.y + n.y * o }; });
  const out: { at: Vec2; text: string }[] = [];
  for (let i = 0; i + 1 < onLine.length; i++) {
    const len = Math.abs((onLine[i + 1].x - onLine[i].x) * u.x + (onLine[i + 1].y - onLine[i].y) * u.y);
    out.push({ at: { x: (onLine[i].x + onLine[i + 1].x) / 2, y: (onLine[i].y + onLine[i + 1].y) / 2 }, text: formatLength(len) });
  }
  return out;
}

/** Tab flips a linear dimension's direction, and holds it. */
export function flipOrient(): void {
  const { orient } = useDimTool.getState();
  useDimTool.setState({ orient: orient === 'h' ? 'v' : 'h', forced: true });
}

if (typeof window !== 'undefined') {
  window.addEventListener('keydown', (e) => {
    if (e.key !== 'Tab' || e.ctrlKey || e.metaKey || e.altKey) return;
    const cad = useCad.getState();
    const live = useLive.getState();
    if (cad.command !== 'DIMLINEAR' || !cad.step?.base || useDimTool.getState().orient === null || live.len || live.ang) return;
    if ((e.target as HTMLElement | null)?.closest?.('input, select, textarea')) return;
    e.preventDefault();
    e.stopPropagation();
    flipOrient();
  }, true);
}

/** DIM's and DLI's bar: continuous or single, a linear dimension's direction (Tab), and the finish button of a string. */
export function DimensionOptions() {
  const { continuous, orient, count } = useDimTool();
  const command = useCad((s) => s.command);
  return (
    <>
      <label className={`cad-toggle${continuous ? ' on' : ''}`} data-dim-continuous title="Off: one dimension at a time. On: points go on as a string, each measured from the last (C)">
        <input type="checkbox" checked={continuous} onChange={(e) => setContinuous(e.target.checked)} /> Continuous
      </label>
      {command === 'DIMLINEAR' && orient && (
        <button type="button" className="cad-toggle" data-dim-orient={orient} title="Tab flips it" onClick={flipOrient}>
          {orient === 'h' ? 'Horizontal · Tab' : 'Vertical · Tab'}
        </button>
      )}
      {continuous && count >= 2 && (
        <button type="button" className="cad-toggle on" data-dim-finish title="Make the string (Enter, right-click, or the last point again)" onClick={() => void finishStep()}>{`Finish string (${count} points)`}</button>
      )}
    </>
  );
}
