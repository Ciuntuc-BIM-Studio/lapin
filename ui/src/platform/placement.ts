/**
 * Asking the modules whether a door or window fits where the cursor is, before the click (LapinModule.placementCheck):
 * a throttled question per cursor position, the latest answer kept with the request it belongs to, and the point the
 * placement snaps to when the cursor is near the place a module offers. Pure but for the small store.
 */

import { create } from 'zustand';
import type { NodeDto } from '../engine/protocol';
import { arcOf, pointAt, project, type Arc } from '../viewport/arc';
import type { PlacementRequest, PlacementVerdict, Vec2 } from './module';
import { translate, type Lang } from '../i18n';
import { wrapText } from '../viewport/wrapText';

/** How far (m) from the offered place the cursor may be for the placement to snap to it. */
export const SNAP_REACH = 1;
const THROTTLE_MS = 120;

export type PlacementCheck = (req: PlacementRequest) => Promise<PlacementVerdict | null> | PlacementVerdict | null;

/** The request for a host and a cursor point: where along the host's axis it falls (null when the host has no axis). */
export function requestFor(host: NodeDto, at: Vec2, what: { kind: 'door' | 'window'; width: number; typeId: string | null }): { req: PlacementRequest; arc: Arc } | null {
  const arc = arcOf(host.props);
  if (!arc) return null;
  return { req: { ...what, host, along: project(arc, at) }, arc };
}

/**
 * A refusal's reason as the plan writes it by the cursor: in the interface's language (a module's sentence goes through
 * its phrases and patterns like any text; it cannot once wrapped into lines), then wrapped. No lines when it fits.
 */
export function reasonLines(verdict: PlacementVerdict | null, lang?: Lang, width = 56): string[] {
  if (!verdict || verdict.ok || !verdict.message) return [];
  return wrapText(translate(verdict.message, lang) ?? verdict.message, width);
}

/** Requests that differ by less than a centimetre along the host are the same question. */
export const placementKey = (r: PlacementRequest): string => `${r.kind}|${r.host.id}|${Math.round(r.along * 100)}|${r.width}|${r.typeId ?? ''}`;

/** The point the placement goes to: the offered place when the verdict refuses and it is within reach, on the cursor's side of the host; else the cursor. */
export function placedAt(arc: Arc, at: Vec2, verdict: PlacementVerdict | null): { point: Vec2; snapped: boolean } {
  const to = verdict && !verdict.ok ? verdict.snapAlong : undefined;
  if (to == null) return { point: at, snapped: false };
  const here = project(arc, at);
  if (Math.abs(to - here) > SNAP_REACH) return { point: at, snapped: false };
  const q = pointAt(arc, here), p = pointAt(arc, to);
  return { point: { x: p.x + at.x - q.x, y: p.y + at.y - q.y }, snapped: true };
}

export const usePlacement = create<{ key: string; verdict: PlacementVerdict | null }>(() => ({ key: '', verdict: null }));

/** The answer kept for this request, or null while it is still being asked. */
export const verdictFor = (req: PlacementRequest): PlacementVerdict | null => {
  const s = usePlacement.getState();
  return s.key === placementKey(req) ? s.verdict : null;
};

let latest = '';
let last = 0;
let timer: ReturnType<typeof setTimeout> | null = null;

/** Asks the checks (the first that answers wins), at most once in a while; the latest question is the one answered. */
export function askPlacement(req: PlacementRequest, checks: PlacementCheck[]): void {
  const key = placementKey(req);
  if (key === latest) return;
  latest = key;
  const go = async () => {
    timer = null;
    last = Date.now();
    let v: PlacementVerdict | null = null;
    try { for (const c of checks) { v = await c(req); if (v) break; } } catch { v = null; }
    if (latest === key) usePlacement.setState({ key, verdict: v });
  };
  if (timer) clearTimeout(timer);
  const wait = Math.max(0, THROTTLE_MS - (Date.now() - last));
  if (wait === 0) void go(); else timer = setTimeout(() => void go(), wait);
}

/**
 * The door or window being placed as the cursor has it now (its host, where along it, how wide), for what the plan draws
 * with it (its temporary dimensions, decision 511); null when the cursor is over no host.
 */
export const usePlacing = create<{ req: PlacementRequest | null }>(() => ({ req: null }));
export function setPlacing(req: PlacementRequest | null): void {
  const was = usePlacing.getState().req;
  if (was === req || (was && req && placementKey(was) === placementKey(req))) return;
  usePlacing.setState({ req });
}

/** Forgets what was asked (the tool was left). */
export function resetPlacement(): void {
  setPlacing(null);
  latest = '';
  if (timer) { clearTimeout(timer); timer = null; }
  usePlacement.setState({ key: '', verdict: null });
}
