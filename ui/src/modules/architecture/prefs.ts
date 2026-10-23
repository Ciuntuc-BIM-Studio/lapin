/** The style new corners get when walls are drawn: butt or mitre, remembered on this computer (as in the old Lapin). */

import { create } from 'zustand';

export type JoinStyle = 'butt' | 'mitre';
const KEY = 'lapin.wallJoinStyle';

function stored(): JoinStyle {
  try { return localStorage.getItem(KEY) === 'mitre' ? 'mitre' : 'butt'; } catch { return 'butt'; }
}

export const useWallPrefs = create<{ joinStyle: JoinStyle }>(() => ({ joinStyle: stored() }));

export function setJoinStyle(joinStyle: JoinStyle): void {
  useWallPrefs.setState({ joinStyle });
  try { localStorage.setItem(KEY, joinStyle); } catch { /* the choice holds for this session */ }
}
