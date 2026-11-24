/** The style new corners get when walls are drawn: mitre (the default), or butt, remembered on this computer. */

import { create } from 'zustand';

export type JoinStyle = 'butt' | 'mitre';
const KEY = 'lapin.wallJoinStyle';

function stored(): JoinStyle {
  try { return localStorage.getItem(KEY) === 'butt' ? 'butt' : 'mitre'; } catch { return 'mitre'; }
}

/**
 * The line a wall is drawn on (decision 237): the core's centre (the axis), its exterior or interior face, a finish face, or an
 * offset from the core's centre (decision 685, `lineOffset` metres to the left as drawn).
 */
export type WallLine = 'core' | 'coreExterior' | 'coreInterior' | 'exterior' | 'interior' | 'offset';
const LINE_KEY = 'lapin.wallLine';
const OFFSET_KEY = 'lapin.wallLineOffset';
const REMEMBER_KEY = 'lapin.wallRememberLine';
const LINES: WallLine[] = ['core', 'coreExterior', 'coreInterior', 'exterior', 'interior', 'offset'];

function storedLine(): WallLine {
  try { const v = localStorage.getItem(LINE_KEY) as WallLine | null; return v && LINES.includes(v) ? v : 'core'; } catch { return 'core'; }
}
function storedOffset(): number {
  try { const v = Number(localStorage.getItem(OFFSET_KEY)); return Number.isFinite(v) ? v : 0; } catch { return 0; }
}
function storedRemember(): boolean {
  try { return localStorage.getItem(REMEMBER_KEY) === '1'; } catch { return false; }
}

/**
 * `remember`: the walls drawn keep the line they were drawn on as their reference line (forum topic 159; off: placed by it only, as before).
 * `flipped`: drawn on the core's centre, the walls come mirrored across it, their exterior to the left as drawn (Space, forum topic 238); for
 * this session only.
 */
export const useWallPrefs = create<{ joinStyle: JoinStyle; line: WallLine; lineOffset: number; remember: boolean; flipped: boolean }>(() => ({ joinStyle: stored(), line: storedLine(), lineOffset: storedOffset(), remember: storedRemember(), flipped: false }));

/** Whether the line is the core's centre (an offset of 0 too): it has no other side, so Space mirrors the wall across it instead (forum topic 238). */
export const onCoreCentre = (line: WallLine, lineOffset: number): boolean => line === 'core' || (line === 'offset' && Math.abs(lineOffset) < 1e-9);

/** Whether the walls drawn now come mirrored across their axis: flipped by Space while drawn on the core's centre. */
export const drawsFlipped = (p: { line: WallLine; lineOffset: number; flipped?: boolean }): boolean => !!p.flipped && onCoreCentre(p.line, p.lineOffset);

/** Space on the core's centre: the walls drawn next come mirrored across it (or back). */
export function toggleWallFlip(): void {
  useWallPrefs.setState((p) => ({ flipped: !p.flipped }));
}

export function setWallLine(line: WallLine): void {
  useWallPrefs.setState({ line });
  try { localStorage.setItem(LINE_KEY, line); } catch { /* the choice holds for this session */ }
}

export function setWallLineOffset(lineOffset: number): void {
  useWallPrefs.setState({ lineOffset });
  try { localStorage.setItem(OFFSET_KEY, String(lineOffset)); } catch { /* the choice holds for this session */ }
}

export function setRememberLine(remember: boolean): void {
  useWallPrefs.setState({ remember });
  try { localStorage.setItem(REMEMBER_KEY, remember ? '1' : '0'); } catch { /* the choice holds for this session */ }
}

/** What addWall is told of the line chosen: nothing for the core's centre (but `flip` when Space mirrored it), the line, and whether the wall keeps it. */
export function wallLineArgs(p: { line: WallLine; lineOffset: number; remember: boolean; flipped?: boolean }): Record<string, unknown> {
  if (onCoreCentre(p.line, p.lineOffset)) return drawsFlipped(p) ? { flip: true } : {};
  const reference = p.line === 'offset' ? { kind: 'offset', offset: p.lineOffset } : p.line;
  return { reference, ...(p.remember ? { rememberReference: true } : {}) };
}

export function setJoinStyle(joinStyle: JoinStyle): void {
  useWallPrefs.setState({ joinStyle });
  try { localStorage.setItem(KEY, joinStyle); } catch { /* the choice holds for this session */ }
}
