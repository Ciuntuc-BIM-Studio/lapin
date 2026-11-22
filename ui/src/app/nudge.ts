/**
 * Nudging (forum topic 212): the arrow keys move what is selected by a small step, as Revit's do, in a plan, a section,
 * an elevation, a drawing and the 3D view. The pure part: which way an arrow goes, how far, and when the keys are the
 * nudge's. The running of it (the move command, the view's state) is viewport/nudge.ts.
 *
 * - Keys: an arrow nudges by the small step, Shift+arrow by ten times as much; Ctrl (⌘ on a Mac) with an arrow does the
 *   same, for those who reach for it (the forum's suggestion). All eight are commands in the shortcuts' registry.
 * - Step: follows the zoom (about four pixels, rounded to 1, 2 or 5 × 10ⁿ mm, from 1 mm to 1 m), so it is fine when
 *   zoomed in and coarse when zoomed out; or one fixed step from File → Preferences. Shift is ten times either.
 * - Direction: along the screen's axes; in a plan with a UCS set, along the UCS's axes (the axis nearest to the arrow's
 *   way on screen); in 3D along the UCS's axes, or the world's with none, the nearest to the camera's.
 */

import type { Vec2 } from '../platform/module';

export type NudgeDir = 'left' | 'right' | 'up' | 'down';

/** The commands: id, label (translated), the arrow, whether it is the big step. */
export const NUDGES: readonly { id: string; label: string; dir: NudgeDir; big: boolean; key: string }[] = [
  { id: 'modify.nudgeLeft', label: 'Nudge left', dir: 'left', big: false, key: 'ArrowLeft' },
  { id: 'modify.nudgeRight', label: 'Nudge right', dir: 'right', big: false, key: 'ArrowRight' },
  { id: 'modify.nudgeUp', label: 'Nudge up', dir: 'up', big: false, key: 'ArrowUp' },
  { id: 'modify.nudgeDown', label: 'Nudge down', dir: 'down', big: false, key: 'ArrowDown' },
  { id: 'modify.nudgeLeftFar', label: 'Nudge left, ten times as far', dir: 'left', big: true, key: 'ArrowLeft' },
  { id: 'modify.nudgeRightFar', label: 'Nudge right, ten times as far', dir: 'right', big: true, key: 'ArrowRight' },
  { id: 'modify.nudgeUpFar', label: 'Nudge up, ten times as far', dir: 'up', big: true, key: 'ArrowUp' },
  { id: 'modify.nudgeDownFar', label: 'Nudge down, ten times as far', dir: 'down', big: true, key: 'ArrowDown' },
];

export const isNudgeCommand = (id: string | undefined): boolean => !!id && id.startsWith('modify.nudge');

/** The Shift step's factor over the small one. */
export const BIG_FACTOR = 10;
/** How far the small step goes on screen when it follows the zoom (pixels). */
export const STEP_PIXELS = 4;
export const MIN_STEP = 0.001;
export const MAX_STEP = 1;

/** A length (metres) rounded to the nearest 1, 2 or 5 × 10ⁿ (on a log scale), within [MIN_STEP, MAX_STEP]. */
export function niceStep(m: number): number {
  if (!(m > 0) || !Number.isFinite(m)) return MIN_STEP;
  const e = Math.floor(Math.log10(m));
  const base = 10 ** e;
  const f = m / base;
  const pick = f < Math.sqrt(2) ? 1 : f < Math.sqrt(10) ? 2 : f < Math.sqrt(50) ? 5 : 10;
  return Math.min(MAX_STEP, Math.max(MIN_STEP, Math.round(pick * base * 1e6) / 1e6));
}

/**
 * How far an arrow moves, metres: `fixed` (the preference; 0 or null: follow the zoom) or about four pixels at `pxPerMetre`;
 * ten times that for the big step.
 */
export function nudgeDistance(pxPerMetre: number, big: boolean, fixed: number | null | undefined): number {
  const small = fixed && fixed > 0 ? fixed : niceStep(STEP_PIXELS / Math.max(pxPerMetre, 1e-9));
  return Math.round(small * (big ? BIG_FACTOR : 1) * 1e6) / 1e6;
}

/** The fixed steps the preferences offer, metres (0: follows the zoom). */
export const NUDGE_STEPS: readonly number[] = [0, 0.001, 0.005, 0.01, 0.05, 0.1];

/** Where an arrow points on the screen, as a plan vector when the screen is not turned (x right, y up). */
const ARROW: Record<NudgeDir, Vec2> = { left: { x: -1, y: 0 }, right: { x: 1, y: 0 }, up: { x: 0, y: 1 }, down: { x: 0, y: -1 } };

const rotate = (v: Vec2, a: number): Vec2 => ({ x: v.x * Math.cos(a) - v.y * Math.sin(a), y: v.x * Math.sin(a) + v.y * Math.cos(a) });
const clean = (v: Vec2): Vec2 => ({ x: Math.abs(v.x) < 1e-12 ? 0 : v.x, y: Math.abs(v.y) < 1e-12 ? 0 : v.y });

/**
 * The unit direction of an arrow in a plan. `turn` is how far the plan is turned on screen (radians, PlanView's rot: 0 north
 * up); the arrow's way on screen is then turned back into the plan. With a UCS (its X axis's angle in degrees) the direction
 * is the UCS's axis nearest to that way (or `snap` alone: the world's axes, the 3D view's choice when it has no UCS).
 */
export function planDirection(dir: NudgeDir, turn: number, ucsAngle: number | null, snap = false): Vec2 {
  const way = rotate(ARROW[dir], -turn);
  if (ucsAngle == null && !snap) return clean(way);
  const a = ((ucsAngle ?? 0) * Math.PI) / 180;
  // The way in the UCS's own coordinates: its larger part is the axis, its sign the direction.
  const q = rotate(way, -a);
  const along = Math.abs(q.x) >= Math.abs(q.y) ? { x: Math.sign(q.x), y: 0 } : { x: 0, y: Math.sign(q.y) };
  return clean(rotate(along, a));
}

/** The direction of an arrow in a section, an elevation or a drawing: the view's own, x to the right, y up. */
export const viewDirection = (dir: NudgeDir): Vec2 => ({ ...ARROW[dir] });

/**
 * The 3D view's arrow as a way on the plan from the camera's azimuth (radians; it looks toward (cos, sin)): right is to the
 * camera's right, up is away from the camera. Turned back the same way a plan's screen is, then snapped to the axes.
 */
export function cameraDirection(dir: NudgeDir, azimuth: number, ucsAngle: number | null): Vec2 {
  // Screen right is the look direction turned a quarter to the right: a plan turned by (azimuth − 90°) shows it up.
  return planDirection(dir, Math.PI / 2 - azimuth, ucsAngle, true);
}

/** What `nudgeReady` needs to know. */
export interface NudgeState {
  screen: 'start' | 'workspace' | 'join';
  dialog: string | null;
  /** The view in the active pane. */
  view: string;
  selected: number;
  /** A tool is active (drawing, the select tool does not count). */
  tool: string | null;
  /** A command waits for input, something is typed, or a segment's length is being typed. */
  typing: boolean;
  /** Walking or flying through the model. */
  walking: boolean;
  /** A drag, a 3D move or a sketch's work plane holds the pointer. */
  gesture: boolean;
  /** The view has a command host (plan, section, elevation, drawing) or is the 3D model view. */
  host: boolean;
}

/** The views whose selection the arrows nudge; a sheet's arrows move its views' boxes (SheetView), a schedule's are its own. */
export const NUDGE_VIEWS: readonly string[] = ['plan', 'drawing', '3d'];

/** Whether the arrow keys are the nudge's now: a selection to move, in a view that moves it, with nothing else asking for the keys. */
export function nudgeReady(s: NudgeState): boolean {
  return s.screen === 'workspace' && !s.dialog && s.selected > 0 && NUDGE_VIEWS.includes(s.view) && !s.tool && !s.typing && !s.walking && !s.gesture && s.host;
}

/** Whether a key press aimed at this element is the element's own (a field, a tree, a list, a menu, a slider…): the arrows are not the nudge's. */
export const OWN_KEYS = 'input, select, textarea, [contenteditable], [role="tree"], [role="listbox"], [role="grid"], [role="menu"], [role="menubar"], [role="tablist"], [role="slider"], [role="spinbutton"], [role="combobox"], [role="radiogroup"], [role="treegrid"]';

/**
 * Nudges that come while an earlier one is still being made (a key held down) are added together, so a held key is one move
 * of the sum, not one undo step for every repeat. A nudge on other elements, or in another frame, is its own.
 */
export interface Pending { ids: readonly string[]; by: [number, number]; frame: unknown }

export function addPending(queue: Pending[], next: Pending): Pending[] {
  const last = queue[queue.length - 1];
  const same = last && last.ids.length === next.ids.length && last.ids.every((id, i) => id === next.ids[i]) && JSON.stringify(last.frame) === JSON.stringify(next.frame);
  if (!same) return [...queue, next];
  return [...queue.slice(0, -1), { ...last, by: [Math.round((last.by[0] + next.by[0]) * 1e6) / 1e6, Math.round((last.by[1] + next.by[1]) * 1e6) / 1e6] }];
}
