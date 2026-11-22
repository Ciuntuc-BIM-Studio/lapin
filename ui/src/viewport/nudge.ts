/**
 * Nudging, carried out (forum topic 212; the rules are app/nudge.ts): the arrow keys' commands move the selection by the
 * existing move, element.transform, so pinned elements stay (the engine says so), doors and windows slide along their wall,
 * joins are worked out again and the temporary dimensions follow the model. Each nudge is one undo step; the nudges of a
 * held key that come while one is still being made are added into the next, so holding a key is one step, not dozens.
 */

import { useUi } from '../app/store';
import { execute } from '../engine/session';
import { useCad } from './cad/session';
import { useLive } from './liveInput';
import { useWalk } from './walk';
import { useMove3d } from './moveIn3d';
import { carrying } from './moveTrace';
import { useWorkPlane } from './sketch/sketch3d';
import { useCamera } from './camera';
import { ucsOf, useLocation } from '../app/location';
import { usePrefs } from '../app/prefs';
import { addPending, cameraDirection, nudgeDistance, nudgeReady, planDirection, viewDirection, type NudgeDir, type NudgeState, type Pending } from '../app/nudge';
import type { Vec2 } from '../platform/module';

/** The 3D camera's pixels per metre at its target, about (the view's height is taken as 800 px). */
const camPxPerMetre = (): number => {
  const c = useCamera.getState();
  const span = (2 * c.distance * Math.tan(c.fov / 2)) / Math.max(c.zoom, 1e-6);
  return 800 / Math.max(span, 1e-6);
};

/** The state the keys' gate reads. */
export function nudgeState(): NudgeState {
  const ui = useUi.getState();
  const cad = useCad.getState();
  const live = useLive.getState();
  const model3d = ui.view === '3d' && !useWorkPlane.getState().plane;
  return {
    screen: ui.screen, dialog: ui.dialog, view: ui.view, selected: ui.selected.length, tool: ui.tool,
    typing: !!cad.step || cad.buffer.length > 0 || cad.takeAll || !!live.owner,
    walking: useWalk.getState().start != null,
    gesture: carrying() || useMove3d.getState().ids.length > 0 || (ui.view === '3d' && !model3d),
    host: model3d || (ui.view !== '3d' && !!cad.host),
  };
}

let queue: Pending[] = [];
let busy = false;

async function drain(): Promise<void> {
  if (busy) return;
  busy = true;
  try {
    while (queue.length) {
      const next = queue[0];
      queue = queue.slice(1);
      await execute('element.transform', { ids: [...next.ids], op: 'move', by: next.by, ...(next.frame ? { frame: next.frame } : {}) });
    }
  } finally { busy = false; }
}

/** A point of the selection as the view draws it (a developed section's frame depends on where along it the selection is). */
function anchor(ids: readonly string[]): Vec2 | undefined {
  const h = useCad.getState().host;
  for (const id of ids) {
    const s = h?.outlinesOf?.(id)?.[0] ?? h?.shapeOf(id);
    if (!s) continue;
    return s.type === 'line' ? s.a : s.type === 'polyline' ? s.pts[0] : s.c;
  }
  return undefined;
}

/** Nudge the selection one step the way the arrow points; false when the keys are not the nudge's now. */
export function nudgeSelection(dir: NudgeDir, big: boolean): boolean {
  if (!nudgeReady(nudgeState())) return false;
  const ui = useUi.getState();
  const ids = ui.selected;
  const ucs = ucsOf(useLocation.getState().loc);
  const ucsAngle = ucs ? ucs.angle : null;
  let v: Vec2, scale: number, frame: unknown;
  if (ui.view === '3d') {
    v = cameraDirection(dir, useCamera.getState().azimuth, ucsAngle);
    scale = camPxPerMetre();
  } else {
    const h = useCad.getState().host;
    if (!h) return false;
    scale = h.pxPerMetre?.() ?? 100;
    if (h.isPlan?.()) v = planDirection(dir, h.screenTurn?.() ?? 0, ucsAngle);
    else {
      v = viewDirection(dir);
      frame = h.frameAt?.(anchor(ids) ?? { x: 0, y: 0 }) ?? undefined;
    }
  }
  const d = nudgeDistance(scale, big, usePrefs.getState().nudgeStep);
  const r6 = (x: number) => Math.round(x * 1e6) / 1e6;
  queue = addPending(queue, { ids: [...ids], by: [r6(v.x * d), r6(v.y * d)], frame });
  void drain();
  return true;
}
