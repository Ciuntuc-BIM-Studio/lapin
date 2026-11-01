/**
 * A railing of its own (decision 238): drawn along a balcony's edge, a terrace or a gallery (Architecture → Railing, a
 * polyline, Enter finishes), in the stair railings' designs; its design, height and sizes in its properties, its line by
 * its vertices' grips (Ctrl/⌘-click an edge's middle to add one, a vertex to take it out).
 */

import type { NodeDto } from '../../engine/protocol';
import type { Grip, ToolDef, Vec2 } from '../../platform/module';
import { RailingIcon } from '../../icons';
import { pt } from '../../viewport/gripKit';

export const railing: ToolDef = {
  id: 'architecture.railing', label: 'Railing', icon: RailingIcon, shortcut: 'RL',
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { await ctx.execute('architecture.addRailing', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) }); },
};

export function railingGrips(node: NodeDto): Grip[] {
  const pts = ((node.props.points as unknown[] | undefined) ?? []).map(pt).filter((p): p is Vec2 => p !== null);
  if (pts.length < 2) return [];
  const send = (next: Vec2[]) => ({ command: 'architecture.updateRailing', args: { id: node.id, points: next.map((p) => [p.x, p.y]) } });
  const grips: Grip[] = pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex', at: p, title: 'Drag: the railing\'s point',
    step: (to) => send(pts.map((q, k) => (k === i ? to : q))),
    remove: pts.length > 2 ? () => send(pts.filter((_, k) => k !== i)) : undefined,
  }));
  for (let i = 0; i + 1 < pts.length; i++) {
    const mid = { x: (pts[i].x + pts[i + 1].x) / 2, y: (pts[i].y + pts[i + 1].y) / 2 };
    grips.push({
      key: `m${i}`, kind: 'edgeMid', at: mid, title: 'Drag: move the segment',
      step: (to) => { const d = { x: to.x - mid.x, y: to.y - mid.y }; return send(pts.map((q, k) => (k === i || k === i + 1 ? { x: q.x + d.x, y: q.y + d.y } : q))); },
      insert: () => send([...pts.slice(0, i + 1), mid, ...pts.slice(i + 1)]),
    });
  }
  return grips;
}
