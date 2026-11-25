/**
 * Model Health Radar (docs/plan-ux-game-like.md, P10):
 * fast, pure client-side BIM integrity scanner flagging common modelling
 * hygiene issues (sliver walls < 50 mm, duplicate overlapping walls/columns,
 * elements missing a storey when storeys exist) with 1-click focus & select.
 */

import type { NodeDto } from '../engine/protocol';

export interface HealthIssue {
  id: string;
  nodeId: string;
  kind: 'sliver' | 'duplicate' | 'orphanStorey';
  label: string;
  detail: string;
}

const PHYSICAL = new Set(['IfcWall', 'IfcSlab', 'IfcColumn', 'IfcBeam', 'IfcFooting', 'IfcRoof', 'IfcStair', 'IfcRamp', 'IfcDoor', 'IfcWindow', 'IfcCurtainWall']);

function ptPair(n: NodeDto): [number, number, number, number] | null {
  const s = n.props.start, e = n.props.end;
  if (!Array.isArray(s) || !Array.isArray(e) || s.length < 2 || e.length < 2) return null;
  return [Number(s[0]), Number(s[1]), Number(e[0]), Number(e[1])];
}

function segKey(a: number, b: number, c: number, d: number, storey: string | null): string {
  const p1 = `${Math.round(a * 100)},${Math.round(b * 100)}`;
  const p2 = `${Math.round(c * 100)},${Math.round(d * 100)}`;
  return `${storey ?? ''}|${p1 < p2 ? `${p1}:${p2}` : `${p2}:${p1}`}`;
}

/** Scans the model's nodes for lightweight BIM hygiene issues. */
export function scanModelHealth(nodes: Map<string, NodeDto>): HealthIssue[] {
  const issues: HealthIssue[] = [];
  let hasStorey = false;
  for (const n of nodes.values()) if (n.type === 'IfcBuildingStorey') { hasStorey = true; break; }

  const seenWalls = new Map<string, string>();
  const seenCols = new Map<string, string>();

  for (const n of nodes.values()) {
    if (!n.editable || n.props.reference) continue;

    if (hasStorey && PHYSICAL.has(n.type) && !n.storeyId) {
      issues.push({ id: `orphan:${n.id}`, nodeId: n.id, kind: 'orphanStorey', label: n.name || n.type, detail: 'Not assigned to a level' });
    }

    if (n.type === 'IfcWall') {
      const seg = ptPair(n);
      if (seg) {
        const len = Math.hypot(seg[2] - seg[0], seg[3] - seg[1]);
        if (len > 0 && len < 0.05) {
          issues.push({ id: `sliver:${n.id}`, nodeId: n.id, kind: 'sliver', label: n.name || 'Wall', detail: `Very short wall (${Math.round(len * 1000)} mm)` });
        }
        const key = segKey(seg[0], seg[1], seg[2], seg[3], n.storeyId);
        if (seenWalls.has(key)) {
          issues.push({ id: `dup:${n.id}`, nodeId: n.id, kind: 'duplicate', label: n.name || 'Wall', detail: 'Overlaps another wall at the exact same coordinates' });
        } else seenWalls.set(key, n.id);
      }
    }

    if (n.type === 'IfcColumn') {
      const x = typeof n.props.x === 'number' ? n.props.x : null;
      const y = typeof n.props.y === 'number' ? n.props.y : null;
      if (x != null && y != null) {
        const key = `${n.storeyId ?? ''}|${Math.round(x * 100)},${Math.round(y * 100)}`;
        if (seenCols.has(key)) {
          issues.push({ id: `dup:${n.id}`, nodeId: n.id, kind: 'duplicate', label: n.name || 'Column', detail: 'Overlaps another column at the exact same point' });
        } else seenCols.set(key, n.id);
      }
    }
  }
  return issues;
}
