/**
 * The profile editor's state (site grading T3): which line it shows, what is picked on its graph, and what the plan marks. The
 * panel (ProfilePanel.tsx) and the plan's overlay (ProfilePlanOverlay.tsx) read it, so a station hovered or clicked in one is
 * marked in the other.
 */

import { create } from 'zustand';
import type { CrossData, ProfileData } from './profile';

export interface ProfileState {
  /** The line shown (a feature line, section line, hardscape, path, wall or ditch), null when the panel is closed. */
  lineId: string | null;
  tab: 'long' | 'cross';
  exaggeration: number;
  data: ProfileData | null;
  /** A station under the cursor, in the graph or in the plan. */
  hover: number | null;
  /** The station picked (the graph's cursor, the plan's mark). */
  station: number | null;
  point: number | null;
  segment: number | null;
  /** A typed grade keeps the start or the end of its segment. */
  keep: 'start' | 'end';
  /** Cross sections: the stations picked, the interval (0: none), the width each way and what the engine answered. */
  stations: number[];
  interval: number;
  width: number;
  cross: CrossData[];
  /** What the engine refused, shown in the panel. */
  message: string | null;
}

export const useProfile = create<ProfileState>(() => ({
  lineId: null, tab: 'long', exaggeration: 2, data: null, hover: null, station: null, point: null, segment: null, keep: 'start',
  stations: [], interval: 0, width: 10, cross: [], message: null,
}));

/** Opens the editor on a line. */
export function openProfile(id: string): void {
  const s = useProfile.getState();
  if (s.lineId === id) return;
  useProfile.setState({ lineId: id, data: null, hover: null, station: null, point: null, segment: null, stations: [], interval: 0, cross: [], message: null, tab: 'long' });
}

export function closeProfile(): void {
  useProfile.setState({ lineId: null, data: null, hover: null, station: null, point: null, segment: null, cross: [], message: null });
}
