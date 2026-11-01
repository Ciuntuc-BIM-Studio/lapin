/** The style new corners get when walls are drawn: mitre (the default), or butt, remembered on this computer. */

import { create } from 'zustand';

export type JoinStyle = 'butt' | 'mitre';
const KEY = 'lapin.wallJoinStyle';

function stored(): JoinStyle {
  try { return localStorage.getItem(KEY) === 'butt' ? 'butt' : 'mitre'; } catch { return 'mitre'; }
}

/** The line a wall is drawn on (decision 237): the core's centre (the axis), its exterior or interior face, or a finish face. */
export type WallLine = 'core' | 'coreExterior' | 'coreInterior' | 'exterior' | 'interior';
const LINE_KEY = 'lapin.wallLine';
const LINES: WallLine[] = ['core', 'coreExterior', 'coreInterior', 'exterior', 'interior'];

function storedLine(): WallLine {
  try { const v = localStorage.getItem(LINE_KEY) as WallLine | null; return v && LINES.includes(v) ? v : 'core'; } catch { return 'core'; }
}

export const useWallPrefs = create<{ joinStyle: JoinStyle; line: WallLine }>(() => ({ joinStyle: stored(), line: storedLine() }));

export function setWallLine(line: WallLine): void {
  useWallPrefs.setState({ line });
  try { localStorage.setItem(LINE_KEY, line); } catch { /* the choice holds for this session */ }
}

export function setJoinStyle(joinStyle: JoinStyle): void {
  useWallPrefs.setState({ joinStyle });
  try { localStorage.setItem(KEY, joinStyle); } catch { /* the choice holds for this session */ }
}
