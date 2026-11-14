/**
 * What a right-click offers for the dimension strings selected (forum topic 74): split each into single dimensions, join
 * several that go on from one another, and align several on one dimension line. Split and join are the engine's commands;
 * aligning moves each parallel string's line (`moveDimension`) onto the first one's, as one undo step.
 */

import type { NodeDto } from '../../engine/protocol';
import { useSession } from '../../engine/session';
import { notify, select, useUi } from '../../app/store';
import type { MenuItem } from '../../shell/ContextMenu';
import { pt } from '../../viewport/gripKit';
import { runSteps } from '../../viewport/grips';
import type { GripStep } from '../../platform/module';

/** A linear or aligned dimension string (not an angle, a radius…). */
export const isDimensionString = (n: NodeDto | undefined): n is NodeDto => n?.props.predefinedType === 'DIMENSION' && !n.props.dimKind;

function directionOf(n: NodeDto): { x: number; y: number } | null {
  const dir = pt(n.props.dir);
  const pts = (Array.isArray(n.props.pts) ? n.props.pts : [n.props.a, n.props.b]).map(pt).filter((p): p is { x: number; y: number } => !!p);
  const v = dir ?? (pts.length >= 2 ? { x: pts[pts.length - 1].x - pts[0].x, y: pts[pts.length - 1].y - pts[0].y } : null);
  if (!v) return null;
  const l = Math.hypot(v.x, v.y);
  return l > 1e-12 ? { x: v.x / l, y: v.y / l } : null;
}

/**
 * The moves that put the strings on the first one's dimension line, and how many could not go (not parallel to it).
 * A string already on the line is left alone.
 */
export function alignSteps(dims: NodeDto[]): { steps: GripStep[]; skipped: number } {
  const [first, ...rest] = dims;
  const at0 = pt(first?.props.at);
  const u0 = first ? directionOf(first) : null;
  if (!first || !at0 || !u0) return { steps: [], skipped: Math.max(0, dims.length - 1) };
  const steps: GripStep[] = [];
  let skipped = 0;
  for (const d of rest) {
    const u = directionOf(d), at = pt(d.props.at);
    if (!u || !at || Math.abs(u.x * u0.y - u.y * u0.x) > 1e-6) { skipped++; continue; }
    // How far this line stands from the first's, across the direction.
    const across = (at.x - at0.x) * -u0.y + (at.y - at0.y) * u0.x;
    if (Math.abs(across) < 1e-6) continue;
    steps.push({ command: 'drafting.moveDimension', args: { id: d.id, at: [at0.x, at0.y] } });
  }
  return { steps, skipped };
}

/** Split into single dimensions, Join selected dimensions, Align selected dimensions: those that apply to the selection. */
export function dimensionItems(): MenuItem[] {
  const { selected } = useUi.getState();
  const nodes = useSession.getState().nodes;
  const dims = selected.map((id) => nodes.get(id)).filter(isDimensionString);
  if (!dims.length || dims.length !== selected.length) return [];
  const items: MenuItem[] = [];
  const strings = dims.filter((d) => Array.isArray(d.props.pts) && d.props.pts.length > 2);
  if (strings.length) {
    items.push({
      label: 'Split into single dimensions',
      onSelect: () => void runSteps(strings.map((d): GripStep => ({ command: 'drafting.splitDimension', args: { id: d.id } }))).then((r) => {
        const first = strings[0].id;
        if (r !== undefined) select(first);
      }),
    });
  }
  if (dims.length > 1) {
    items.push({
      label: 'Join selected dimensions',
      onSelect: () => void runSteps([{ command: 'drafting.joinDimensions', args: { ids: dims.map((d) => d.id) } }]).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) select(id); }),
    });
    items.push({
      label: 'Align selected dimensions',
      onSelect: () => {
        const { steps, skipped } = alignSteps(dims);
        if (skipped) notify('Only dimensions parallel to the first one are aligned.');
        if (steps.length) void runSteps(steps);
      },
    });
  }
  return items;
}
