/**
 * A point cloud scaled true and cropped (docs/plan-gaussian-splats.md, G2), for every cloud, splats or not.
 *
 * **Calibrate scale…** (on the cloud's row in Manage → Links, and on the 3D view's right-click near the cloud): two points
 * snapped on it (its points, a splat's centre), their true distance typed at the cursor or asked for after the second click,
 * in the project's unit; the engine multiplies the link's factor by the ratio, the first point staying where it was
 * (`pointcloud.calibrate`, one undo step), as Scale image does for a traced plan.
 *
 * **Crop**: the link's box (`pointcloud.crop`): what is outside is hidden everywhere. Its numbers are on the cloud's row in
 * Manage → Links, which this module opens on the row asked for.
 */

import { Ruler } from 'lucide-react';
import { create } from 'zustand';
import type { ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { activeStorey, engine, useSession } from '../../engine/session';
import { num } from '../../engine/model';
import { isCloudLink } from '../../viewport/pointClouds';

export const CALIBRATE_TOOL = 'pointcloud.calibrate';

/** The cloud being calibrated, and the row whose crop is open in Manage → Links. */
export const useCloudTools = create<{ calibrating: string | null; cropOpen: string | null }>(() => ({ calibrating: null, cropOpen: null }));

/** The tool started on a cloud: two points on it, then their true distance. */
export function startCalibrate(id: string): void {
  useCloudTools.setState({ calibrating: id });
  useUi.setState({ tool: CALIBRATE_TOOL, dialog: null });
  notify('Calibrate scale: click two points on the cloud a known distance apart and type their true distance (Esc leaves it as it is).');
}

/** Manage → Links opened on a cloud's crop. */
export function openCrop(id: string): void {
  useCloudTools.setState({ cropOpen: id });
  useUi.setState({ dialog: 'links' });
}

/** The calibration's arguments: the two points in the project (m, their heights the snaps' in 3D, the level's in a plan), the true distance. */
export function calibrateArgs(id: string, a: { x: number; y: number }, b: { x: number; y: number }, heights: readonly number[] | undefined, level: number, length: number) {
  const za = heights?.[0] ?? level, zb = heights?.[1] ?? level;
  return { id, a: [a.x, a.y, za], b: [b.x, b.y, zb], length };
}

export const calibrateTool: ToolDef = {
  id: CALIBRATE_TOOL, label: 'Calibrate scale', icon: Ruler,
  gesture: { kind: 'twoPoints', measure: true },
  inSpace: true,
  complete: async ({ points: [a, b], heights, typed }, ctx) => {
    if (!a || !b) return;
    const nodes = useSession.getState().nodes;
    const clouds = [...nodes.values()].filter(isCloudLink);
    const id = useCloudTools.getState().calibrating ?? (clouds.length === 1 ? clouds[0].id : null);
    if (!id || !nodes.has(id)) { notify('Start Calibrate scale from a cloud: its row in Manage → Links, or right-click near it in 3D.'); useUi.setState({ tool: null }); return; }
    const level = num(activeStorey() ?? undefined, 'elevation');
    const za = heights?.[0] ?? level, zb = heights?.[1] ?? level;
    const measuredNow = Math.hypot(b.x - a.x, b.y - a.y, zb - za);
    let length = typed ?? null;
    if (length == null) {
      const { askLength } = await import('../../shell/AskBox');
      length = await askLength('Calibrate scale', 'Their true distance', measuredNow);
    }
    if (length == null || !(length > 0)) return;
    try {
      const r = await ctx.execute('pointcloud.calibrate', calibrateArgs(id, a, b, heights, level, length)) as { scale?: number; warning?: string | null } | null;
      notify(`Scaled ×${(length / measuredNow).toFixed(4)}: its factor is now ${Number(r?.scale ?? 1).toFixed(6)}.${r?.warning ? ' ' + r.warning : ''}`);
    } catch (e) {
      notify(e instanceof Error ? e.message : String(e));
    }
    useCloudTools.setState({ calibrating: null });
    useUi.setState({ tool: null });
  },
};

/** The crop made round where the cloud is dense (Fit to cloud) or round all of it (Whole cloud): one undo step each. */
export async function fitCrop(id: string, whole = false): Promise<void> {
  await engine.call('doc.execute', { command: 'pointcloud.crop', args: whole ? { id, fit: true, whole: true } : { id, fit: true } });
}
