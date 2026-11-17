/**
 * The shared sketch canvas (audit S11, docs/plan-sketch-canvas.md): every 2D drawing board is a view drawn on with the CAD
 * session (viewport/cad/*: the Draft commands, the object snaps, the constraint and dimension tools, the Enter/Esc rule), shown
 * flat (a plan, a section, a reference plane's drawing) or over the 3D view on its plane (Draft3D.tsx). A **board** is what a
 * module lends that canvas while one of its sketches is edited there: which view it is, what it is called, which tools it offers,
 * whether it takes constraints, what Finish writes back and what Cancel drops. The bar over the view (SketchBar) is the same for
 * every board.
 *
 * Modules register a resolver (`registerBoard`): given a view, the board it is, or null. A view no resolver claims is Draft on a
 * plan, a section or a sheet: the same canvas, with no bar.
 */

import type { ComponentType } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { zoomHost } from '../zoom';

/** A tool on the bar: a command-line command (LINE, TRIM…) or a constraint command (CNCOINCIDENT…). */
export interface BoardTool { command: string; label: string; group: 'draw' | 'edit' | 'constrain' | 'dimension' }

export interface Board {
  /** Whose sketch it is: `outline` (a floor's…), `part` (a part's own), `face` (Studio's sketch on a face or a plane). */
  kind: string;
  /** The view the canvas draws on (its drafts are the board's shapes while it is open). */
  viewId: string;
  /** "Sketch of Floor 1". */
  title: string;
  hint?: string;
  /** A note beside the title (a part's: the values shown are its defaults). */
  note?: string;
  /** Draft commands the board offers on its bar (all of them stay in the ribbon and the command line). */
  tools: string[];
  /** The board takes sketch constraints and dimensions (drawing records them; the constraint tools are on its bar). */
  constraints: boolean;
  /** Writes the board back (one undo step), and leaves it. */
  finish(): Promise<void>;
  /** Drops what the board holds, and leaves it. */
  cancel(): Promise<void>;
  /** Esc with nothing in progress (no command, no selection) in 3D: leaves the board keeping its work; none in 2D. */
  leave?(): void;
  /**
   * The board's side form beside the canvas (audit S11 stage 2): what its editor had besides drawing (a profile's name, material
   * and library, the shapes to start from, a table of its points). Shown flat, over the view's right side.
   */
  panel?: ComponentType<{ board: Board }>;
}

/** Where to go back to when a board is left (the view it was opened from), per board view. */
const returns = new Map<string, Record<string, unknown>>();

/** Remembers the interface's place before a board opened (its view, the plan or drawing it showed, a dialog to reopen). */
export function rememberReturn(viewId: string, state: Record<string, unknown>): void { if (!returns.has(viewId)) returns.set(viewId, state); }

/** The place a board was opened from, taken (null when unknown: a project reopened with the board open). */
export function takeReturn(viewId: string): Record<string, unknown> | null {
  const r = returns.get(viewId) ?? null;
  returns.delete(viewId);
  return r;
}

/** The Draft commands a board drawing texts and hatches too offers (a symbol's, a title block's). */
export const ANNOTATE_TOOLS = ['TEXT', 'HATCH'] as const;

/** The Draft commands every board offers, in the bar's order (the drafting tab's own names). */
export const DRAW_TOOLS = ['LINE', 'PLINE', 'RECTANGLE', 'CIRCLE', 'ARC', 'ELLIPSE'] as const;
export const EDIT_TOOLS = ['OFFSET', 'TRIM', 'EXTEND', 'FILLET', 'CHAMFER', 'ERASE'] as const;
export const SKETCH_TOOLS: string[] = [...DRAW_TOOLS, ...EDIT_TOOLS];

type Resolver = (viewId: string, nodes: ReadonlyMap<string, NodeDto>) => Board | null;
const resolvers: Resolver[] = [];

/** A module's boards: called with each view the canvas shows. */
export function registerBoard(r: Resolver): void { if (!resolvers.includes(r)) resolvers.push(r); }

/** The board a view is, or null (plain Draft). */
export function boardOf(viewId: string | null | undefined, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  if (!viewId) return null;
  for (const r of resolvers) {
    const b = r(viewId, nodes);
    if (b) return b;
  }
  return null;
}

/** Whether a view is a board's (its drafts are a sketch: drawing records constraints there even before it has any). */
export const isBoardView = (viewId: string, nodes: ReadonlyMap<string, NodeDto>): boolean => !!boardOf(viewId, nodes)?.constraints;

/**
 * What Esc does on a board with no command running (the CAD session's Enter/Esc rule takes a running command first): the
 * selection goes first; then, over the 3D view, the board is left keeping its work (`leave`); flat, nothing (the plan is the
 * board's until Finish or Cancel). Enter with no command running: Finish in 3D; flat, Enter keeps repeating the last command.
 */
export function boardKey(key: string, o: { in3d: boolean; selected: boolean; board: Pick<Board, 'leave'> }): 'deselect' | 'leave' | 'finish' | null {
  if (key === 'Escape') return o.selected ? 'deselect' : o.in3d && o.board.leave ? 'leave' : null;
  if (key === 'Enter') return o.in3d ? 'finish' : null;
  return null;
}

/**
 * Frames a board's shapes once its view is shown (the view's own fit frames the model it draws, which a board's sketch may lie
 * outside of, or which may be empty): the box from (x0, y0) to (x1, y1) in the view's coordinates, with room left over it for the bar
 * and on its right for the side form. Waits for the view's zoom to replace the one shown when the board was opened.
 */
export function frameBoard(boxOf: () => [number, number, number, number] | null, opts: { panel?: boolean } = {}): void {
  const before = zoomHost();
  let tries = 0;
  const tick = () => {
    const host = zoomHost();
    const box = boxOf();
    if (host && host !== before && box) {
      const [x0, y0, x1, y1] = box;
      const w = Math.max(x1 - x0, 1e-3), h = Math.max(y1 - y0, 1e-3);
      host.zoomWindow({ x: x0 - w * 0.12, y: y0 - h * 0.15 }, { x: x1 + w * (opts.panel ? 0.95 : 0.12), y: y1 + h * 0.9 });
      return;
    }
    if (++tries < 30) setTimeout(tick, 100);
  };
  setTimeout(tick, 100);
}
