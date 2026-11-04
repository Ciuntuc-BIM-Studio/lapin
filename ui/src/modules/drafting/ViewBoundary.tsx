/**
 * View boundaries (decision 236): a named rectangle drawn on a plan, section or elevation (Annotate → View boundary), so a
 * sheet's viewport shows exactly that part of the view instead of being cropped blind on the sheet. Its corners and edges are
 * dragged in the view; its name, unique in the project, is set here; the sheets it is on follow it.
 */

import { useEffect, useState } from 'react';
import { SquareDashed } from 'lucide-react';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, ToolDef, Vec2 } from '../../platform/module';
import { pt } from '../../viewport/gripKit';
import { lengthText } from '../../engine/unitsStore';

export const viewBoundary: ToolDef = {
  id: 'drafting.viewBoundary', label: 'View boundary', icon: SquareDashed, shortcut: 'VB',
  gesture: { kind: 'rectangle' },
  complete: async ({ points: [a, b] }, ctx) => {
    const name = window.prompt('Name of the view boundary (unique in the project; empty: one is given)', '');
    if (name === null) return;
    await ctx.execute('view.addBoundary', { viewId: ctx.viewId, a: [a.x, a.y], b: [b.x, b.y], ...(name.trim() ? { name: name.trim() } : {}) });
  },
};

/** Its corners and its edges' middles to drag, and its middle to move it whole. */
export function boundaryGrips(node: NodeDto): Grip[] {
  const a = pt(node.props.min), b = pt(node.props.max);
  if (!a || !b) return [];
  const view = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  const [x0, x1, y0, y1] = [Math.min(a.x, b.x), Math.max(a.x, b.x), Math.min(a.y, b.y), Math.max(a.y, b.y)];
  const set = (min: Vec2, max: Vec2) => ({ command: 'view.updateBoundary', args: { id: node.id, min: [min.x, min.y], max: [max.x, max.y] } });
  const corner = (key: string, at: Vec2, move: (to: Vec2) => [Vec2, Vec2]): Grip => ({ key, kind: 'vertex', at, view, title: 'Drag: the corner', step: (to) => set(...move(to)) });
  const edge = (key: string, at: Vec2, move: (to: Vec2) => [Vec2, Vec2]): Grip => ({ key, kind: 'edgeMid', at, view, title: 'Drag: the edge', step: (to) => set(...move(to)) });
  const mid = { x: (x0 + x1) / 2, y: (y0 + y1) / 2 };
  return [
    corner('sw', { x: x0, y: y0 }, (to) => [to, { x: x1, y: y1 }]),
    corner('se', { x: x1, y: y0 }, (to) => [{ x: x0, y: to.y }, { x: to.x, y: y1 }]),
    corner('ne', { x: x1, y: y1 }, (to) => [{ x: x0, y: y0 }, to]),
    corner('nw', { x: x0, y: y1 }, (to) => [{ x: to.x, y: y0 }, { x: x1, y: to.y }]),
    edge('s', { x: mid.x, y: y0 }, (to) => [{ x: x0, y: to.y }, { x: x1, y: y1 }]),
    edge('e', { x: x1, y: mid.y }, (to) => [{ x: x0, y: y0 }, { x: to.x, y: y1 }]),
    edge('n', { x: mid.x, y: y1 }, (to) => [{ x: x0, y: y0 }, { x: x1, y: to.y }]),
    edge('w', { x: x0, y: mid.y }, (to) => [{ x: to.x, y: y0 }, { x: x1, y: y1 }]),
    { key: 'mid', kind: 'move', at: mid, view, title: 'Drag: move the boundary', step: (to) => set({ x: x0 + to.x - mid.x, y: y0 + to.y - mid.y }, { x: x1 + to.x - mid.x, y: y1 + to.y - mid.y }) },
  ];
}

/** Its name, and the sheets it is on. */
export function BoundarySection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [name, setName] = useState(node.name);
  useEffect(() => setName(node.name), [node.name]);
  const a = pt(node.props.min), b = pt(node.props.max);
  const sheets = [...nodes.values()].filter((n) => n.type === 'LapinSheet' && Array.isArray(n.props.viewports) && (n.props.viewports as { boundary?: string }[]).some((v) => v.boundary === node.id));
  const commit = () => { if (name.trim() && name.trim() !== node.name) void execute('view.updateBoundary', { id: node.id, name: name.trim() }); else setName(node.name); };
  return (
    <dl className="fields">
      <dt>Name</dt>
      <dd><input className="cell" value={name} disabled={!node.editable} onChange={(e) => setName(e.target.value)} onBlur={commit} onKeyDown={(e) => { if (e.key === 'Enter') commit(); }} /></dd>
      {a && b && <><dt>Size</dt><dd className="num">{`${lengthText(Math.abs(b.x - a.x))} × ${lengthText(Math.abs(b.y - a.y))}`}</dd></>}
      <dt>On sheets</dt>
      <dd>{sheets.length ? sheets.map((s) => `${String(s.props.number ?? '')} ${s.name}`.trim()).join(', ') : '—'}</dd>
    </dl>
  );
}
