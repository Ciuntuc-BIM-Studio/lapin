/**
 * Quick 3D Camera Bookmarks (docs/plan-ux-game-like.md, P7):
 * Ctrl+Shift+1..4 saves the current 3D camera pose into slot 1–4;
 * Shift+1..4 flies smoothly to that saved pose.
 */

import { create } from 'zustand';
import { useCamera, type CameraPose } from '../viewport/camera';
import { notify, useUi } from './store';

export const BOOKMARK_SLOTS = [1, 2, 3, 4] as const;
export type BookmarkSlot = (typeof BOOKMARK_SLOTS)[number];

export const useCameraBookmarks = create<{ slots: Partial<Record<BookmarkSlot, CameraPose>> }>(() => ({ slots: {} }));

export function saveCameraBookmark(slot: BookmarkSlot): boolean {
  const c = useCamera.getState();
  const pose: CameraPose = { azimuth: c.azimuth, pitch: c.pitch, distance: c.distance, target: { ...c.target } };
  useCameraBookmarks.setState((s) => ({ slots: { ...s.slots, [slot]: pose } }));
  notify(`Camera bookmark ${slot} saved (Shift+${slot} to jump back).`);
  return true;
}

export function gotoCameraBookmark(slot: BookmarkSlot): boolean {
  const pose = useCameraBookmarks.getState().slots[slot];
  if (!pose) {
    notify(`Camera bookmark ${slot} is empty — press Ctrl+Shift+${slot} in 3D to save the current angle.`);
    return false;
  }
  if (useUi.getState().view !== '3d') useUi.setState({ view: '3d' });
  useCamera.getState().api?.orbitTo(pose, true);
  return true;
}
