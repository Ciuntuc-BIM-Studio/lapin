/**
 * Walk in 3D in one click (the Walk quick view, a Flow extra; the radial menu's Walk): inside the active storey's first
 * room, else at the 3D camera's target (docs/plan-ux-game-like.md).
 */

import { activeStorey, useSession } from '../engine/session';
import { num } from '../engine/model';
import { useCamera } from '../viewport/camera';
import { EYE_HEIGHT, exitWalk, useWalk, walkInto } from '../viewport/walk';
import { useUi } from './store';
import { noteMissionEvent } from './onboarding';

/** Start first-person walk in 3D: inside the first room of the active storey if one exists, else at the 3D camera target. */
export function toggleQuickWalk(): void {
  if (useWalk.getState().start) { exitWalk(); return; }
  const { nodes, render } = useSession.getState();
  const storey = activeStorey();
  const elev = num(storey ?? undefined, 'elevation');
  let start = null;
  for (const n of nodes.values()) {
    if (n.type !== 'IfcSpace' || (storey && n.storeyId !== storey.id)) continue;
    const item = render.get(n.id);
    const outline = item?.plan.find((p) => p.kind === 'room')?.points;
    if (!item || !outline) continue;
    let floor = Infinity;
    for (let k = 2; k < item.positions.length; k += 3) floor = Math.min(floor, item.positions[k]);
    start = walkInto(n.name, outline, item.plan.filter((p) => p.kind === 'roomIsland').map((p) => p.points), Number.isFinite(floor) ? floor : elev);
    if (start) break;
  }
  if (!start) {
    const cam = useCamera.getState();
    start = { eye: { x: cam.target.x, y: cam.target.y, z: elev + EYE_HEIGHT }, yaw: cam.azimuth + Math.PI, pitch: 0, name: storey?.name ?? '3D' };
  }
  useWalk.setState({ start });
  noteMissionEvent('walk');
  useUi.setState({ view: '3d' });
}
