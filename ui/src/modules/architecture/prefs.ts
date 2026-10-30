/** The style new corners get when walls are drawn: mitre (the default), or butt, remembered on this computer. */

import { create } from 'zustand';

export type JoinStyle = 'butt' | 'mitre';
const KEY = 'lapin.wallJoinStyle';

function stored(): JoinStyle {
  try { return localStorage.getItem(KEY) === 'butt' ? 'butt' : 'mitre'; } catch { return 'mitre'; }
}

export const useWallPrefs = create<{ joinStyle: JoinStyle }>(() => ({ joinStyle: stored() }));

export function setJoinStyle(joinStyle: JoinStyle): void {
  useWallPrefs.setState({ joinStyle });
  try { localStorage.setItem(KEY, joinStyle); } catch { /* the choice holds for this session */ }
}
