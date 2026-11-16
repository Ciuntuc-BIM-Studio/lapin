/**
 * Enter and Esc while a tool's gesture is on, one rule for every view (audit 2026-11-15: polygons closed with Enter, chains with
 * Esc, the drafting commands with both). **Enter finishes**: it closes an outline (3 points or more), ends a polyline (2 or more),
 * ends a chain (a wall, a beam: the pieces drawn stay) and ends a pick-then-point gesture at its pick. **Esc cancels the step in
 * progress**: the points of what is not made yet go (what a chain already made stays), and with nothing in progress it leaves the
 * tool. The drafting commands follow the same rule (Enter finishes, Esc cancels: viewport/cad/session.ts).
 */
import type { Gesture } from '../platform/module';

export type GestureKeyAction = 'complete' | 'completePick' | 'clear' | 'exit' | null;

export function gestureKey(key: string, gesture: Gesture | undefined, points: number, picked: boolean): GestureKeyAction {
  if (!gesture) return null;
  if (key === 'Escape') return points ? 'clear' : 'exit';
  if (key !== 'Enter' || !points) return null;
  if (gesture.kind === 'pickThenPoint') return points === 1 && picked ? 'completePick' : null;
  if (gesture.kind === 'polygon') return points >= 3 ? 'complete' : null;
  if (gesture.kind === 'polyline') return points >= 2 ? 'complete' : null;
  // A chain: each piece is made as it is drawn; Enter ends it there (the next click starts a new one).
  if (gesture.kind === 'twoPoints' && gesture.repeat) return 'clear';
  return null;
}
