/** A roof's edges as the configurator and the inspector edit them: one entry per side of its outline, null for "as the roof". */

import type { NodeDto } from '../../engine/protocol';
import type { Grip, Vec2 } from '../../platform/module';
import { anticlockwise, fromPairs } from '../../viewport/contour';

export type EdgeKind = 'eave' | 'gable';
export type Edge = { kind?: EdgeKind; pitch?: number; overhang?: number; gutter?: boolean } | null;

/**
 * The edges with side `i` changed by `patch` (null: back to the roof's own):
 * one entry for each of `count` sides; a field set to undefined goes, and an
 * edge left with no field of its own becomes null.
 */
export function mergeEdge(edges: readonly Edge[], count: number, i: number, patch: Partial<NonNullable<Edge>> | null): Edge[] {
  const next: Edge[] = Array.from({ length: count }, (_, k) => edges[k] ?? null);
  const merged: Record<string, unknown> | null = patch === null ? null : { ...(next[i] ?? {}), ...patch };
  if (merged) for (const key of Object.keys(merged)) if (merged[key] === undefined) delete merged[key];
  next[i] = merged && Object.keys(merged).length ? (merged as NonNullable<Edge>) : null;
  return next;
}

function shedLowEdge(outline: readonly Vec2[], ridgeDirection: string): number {
  const n = outline.length;
  const edgeVec = (i: number) => {
    const a = outline[i], b = outline[(i + 1) % n];
    return { x: b.x - a.x, y: b.y - a.y };
  };
  let best = 0;
  if (ridgeDirection !== 'x' && ridgeDirection !== 'y') {
    let bestLen = -1;
    for (let i = 0; i < n; i++) {
      const d = edgeVec(i);
      const len = Math.hypot(d.x, d.y);
      if (len > bestLen) { bestLen = len; best = i; }
    }
    return best;
  }
  const alongX = ridgeDirection === 'x';
  const along: number[] = [];
  for (let i = 0; i < n; i++) {
    const d = edgeVec(i);
    if (alongX ? Math.abs(d.x) > Math.abs(d.y) : Math.abs(d.y) > Math.abs(d.x)) along.push(i);
  }
  if (!along.length) {
    let bestLen = -1;
    for (let i = 0; i < n; i++) {
      const d = edgeVec(i);
      const len = Math.hypot(d.x, d.y);
      if (len > bestLen) { bestLen = len; best = i; }
    }
    return best;
  }
  const span = (i: number) => Math.abs(alongX ? edgeVec(i).x : edgeVec(i).y);
  const longest = Math.max(...along.map(span));
  const candidates = along.filter((i) => span(i) >= longest - 1e-6);
  let minCoord = Infinity;
  for (const i of candidates) {
    const d = edgeVec(i);
    const midCoord = alongX ? outline[i].y + d.y / 2 : outline[i].x + d.x / 2;
    if (midCoord < minCoord) { minCoord = midCoord; best = i; }
  }
  return best;
}

function acrossEdge(outline: readonly Vec2[], i: number): number {
  const n = outline.length;
  const a = outline[i];
  const next = outline[(i + 1) % n];
  const ux = next.x - a.x, uy = next.y - a.y;
  const uLen = Math.hypot(ux, uy) || 1;
  const nx = ux / uLen, ny = uy / uLen;
  const sin10 = Math.sin((10 * Math.PI) / 180);
  let best = i;
  let far = 1e-6;
  for (let j = 0; j < n; j++) {
    if (j === i) continue;
    const dx = outline[(j + 1) % n].x - outline[j].x;
    const dy = outline[(j + 1) % n].y - outline[j].y;
    const dLen = Math.hypot(dx, dy);
    if (dLen < 1e-9 || Math.abs(nx * (dy / dLen) - ny * (dx / dLen)) > sin10) continue;
    const mx = outline[j].x + dx * 0.5 - a.x;
    const my = outline[j].y + dy * 0.5 - a.y;
    const off = Math.abs(nx * my - ny * mx);
    if (off > far) { far = off; best = j; }
  }
  return best;
}

/**
 * Each edge's automatic kind for the roof's shape, matching the engine's `Roof.Kinds`:
 * hip/mansard -> all eaves; shed -> one low eave, gables elsewhere; gable -> short convex ends.
 */
export function resolveRoofEdgeKinds(
  shape: string,
  outline: Vec2[],
  ridgeDirection = 'auto',
  lowEdge = -1,
  shedFlip = false,
): EdgeKind[] {
  const n = outline.length;
  const kinds: EdgeKind[] = Array(n).fill('eave');
  if (n < 3) return kinds;
  const len = (i: number) => Math.hypot(outline[(i + 1) % n].x - outline[i].x, outline[(i + 1) % n].y - outline[i].y);
  if (shape === 'shed') {
    let low = lowEdge >= 0 && lowEdge < n ? lowEdge : shedLowEdge(outline, ridgeDirection);
    if (shedFlip) low = acrossEdge(outline, low);
    for (let i = 0; i < n; i++) if (i !== low) kinds[i] = 'gable';
    return kinds;
  }
  if (shape === 'gable' || shape === 'barrel') {
    const ccw = anticlockwise(outline);
    const convex = (v: number) => {
      const a = outline[(v + n - 1) % n], b = outline[v], c = outline[(v + 1) % n];
      return (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x) > 0 === ccw;
    };
    if (n === 4 && (ridgeDirection === 'x' || ridgeDirection === 'y')) {
      for (let i = 0; i < n; i++) {
        const d = { x: outline[(i + 1) % n].x - outline[i].x, y: outline[(i + 1) % n].y - outline[i].y };
        const alongY = Math.abs(d.y) > Math.abs(d.x);
        if (alongY === (ridgeDirection === 'x')) kinds[i] = 'gable';
      }
      return kinds;
    }
    for (let i = 0; i < n; i++) {
      if (convex(i) && convex((i + 1) % n) && len(i) <= len((i + n - 1) % n) + 1e-9 && len(i) <= len((i + 1) % n) + 1e-9) {
        kinds[i] = 'gable';
      }
    }
    if (kinds.every((k): boolean => k === 'gable')) for (let i = 1; i < n; i += 2) kinds[i] = 'eave';
  }
  return kinds;
}

/** Next edge kind override when clicking an edge indicator: toggles between eave and gable, returning undefined when back to auto. */
export function nextRoofEdgeKind(autoKind: EdgeKind, overrideKind?: EdgeKind): EdgeKind | undefined {
  const current = overrideKind ?? autoKind;
  const target: EdgeKind = current === 'eave' ? 'gable' : 'eave';
  return target === autoKind ? undefined : target;
}

/**
 * Interactive slope/gable indicators along each boundary edge of a selected roof (omitted for flat and barrel roofs).
 * Clicking an indicator toggles that edge between slope-defining (`eave`) and vertical end (`gable`) as one undo step.
 */
export function roofSlopeGrips(node: NodeDto): Grip[] {
  const shape = typeof node.props.shape === 'string' ? node.props.shape : 'hip';
  if (shape === 'flat' || shape === 'barrel') return [];
  const pts = fromPairs(node.props.contour);
  if (pts.length < 3) return [];
  const edges = Array.isArray(node.props.edges) ? (node.props.edges as Edge[]) : [];
  const ridgeDirection = typeof node.props.ridgeDirection === 'string' ? node.props.ridgeDirection : 'auto';
  const lowEdge = typeof node.props.lowEdge === 'number' ? node.props.lowEdge : -1;
  const shedFlip = Boolean(node.props.shedFlip);
  const defaultPitch = typeof node.props.pitch === 'number' ? node.props.pitch : 30;
  const autoKinds = resolveRoofEdgeKinds(shape, pts, ridgeDirection, lowEdge, shedFlip);
  const inwardSign = anticlockwise(pts) ? 1 : -1;

  const grips: Grip[] = [];
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i], b = pts[(i + 1) % pts.length];
    const dx = b.x - a.x, dy = b.y - a.y;
    const edgeLen = Math.hypot(dx, dy);
    if (edgeLen < 1e-6) continue;
    const ux = dx / edgeLen, uy = dy / edgeLen;
    const overrideKind = edges[i]?.kind;
    const resolvedKind: EdgeKind = overrideKind ?? autoKinds[i];
    const pitch = typeof edges[i]?.pitch === 'number' ? edges[i]!.pitch! : defaultPitch;
    const pitchText = `${Math.round(pitch * 10) / 10}°`;
    const nextKind = nextRoofEdgeKind(autoKinds[i], overrideKind);
    const nextLabel = nextKind === undefined ? `auto (${autoKinds[i]})` : nextKind;
    grips.push({
      key: `slope:${i}`,
      kind: 'slope',
      at: { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 },
      dir: { x: -uy * inwardSign + 0, y: ux * inwardSign + 0 },
      label: resolvedKind === 'eave' ? pitchText : 'G',
      slope: { kind: resolvedKind, override: overrideKind !== undefined },
      title: resolvedKind === 'eave'
        ? `Edge ${i + 1} · Eave (${pitchText}${overrideKind ? ', override' : ', auto'}) · Click to set ${nextLabel}`
        : `Edge ${i + 1} · Gable (${overrideKind ? 'override' : 'auto'}) · Click to set ${nextLabel}`,
      click: () => ({
        command: 'architecture.updateRoof',
        args: { id: node.id, edges: mergeEdge(edges, pts.length, i, { kind: nextKind }) },
      }),
    });
  }
  return grips;
}
