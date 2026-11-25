/**
 * Quick 3D Camera Bookmarks (docs/plan-ux-game-like.md, P7):
 * Alt+Shift+1..4 saves the current 3D camera pose into slot 1–4;
 * Alt+1..4 flies smoothly to that saved pose (app/commands.ts binds and
 * says them, as the user has them bound).
 */

import { create } from 'zustand';
import { useCamera, type CameraPose } from '../viewport/camera';
import { useUi } from './store';

export const BOOKMARK_SLOTS = [1, 2, 3, 4] as const;
export type BookmarkSlot = (typeof BOOKMARK_SLOTS)[number];

export const useCameraBookmarks = create<{ slots: Partial<Record<BookmarkSlot, CameraPose>> }>(() => ({ slots: {} }));

export function saveCameraBookmark(slot: BookmarkSlot): boolean {
  const c = useCamera.getState();
  const pose: CameraPose = { azimuth: c.azimuth, pitch: c.pitch, distance: c.distance, target: { ...c.target } };
  useCameraBookmarks.setState((s) => ({ slots: { ...s.slots, [slot]: pose } }));
  return true;
}

export function gotoCameraBookmark(slot: BookmarkSlot): boolean {
  const pose = useCameraBookmarks.getState().slots[slot];
  if (!pose) return false;
  if (useUi.getState().view !== '3d') useUi.setState({ view: '3d' });
  useCamera.getState().api?.orbitTo(pose, true);
  return true;
}
