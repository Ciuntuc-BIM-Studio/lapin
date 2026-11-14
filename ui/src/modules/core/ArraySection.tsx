/**
 * An array in the inspector (decision 481): an arrangement kept beside its copies. Any copy shows its rule (the count,
 * the step, a grid's columns, rows and levels, the angle, the path…) to change, and selecting the array or exploding it
 * into plain copies.
 */

import { Blocks, MousePointer2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { LengthCell, NumberField } from '../../shell/cells';

interface Item { ids: string[]; at?: number[] }

const itemsOf = (n: NodeDto): Item[] => (Array.isArray(n.props.items) ? (n.props.items as unknown as Item[]) : []);

/** The array arrangement an element belongs to (or the arrangement itself). */
export function arrayOf(node: NodeDto, nodes: Map<string, NodeDto>): NodeDto | null {
  if (node.type === 'LapinArray') return node;
  for (const n of nodes.values()) if (n.type === 'LapinArray' && itemsOf(n).some((i) => i.ids?.includes(node.id))) return n;
  return null;
}

export function ArraySection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const arr = arrayOf(node, nodes);
  if (!arr) return null;
  const p = arr.props;
  const items = itemsOf(arr);
  const index = items.findIndex((i) => i.ids?.includes(node.id));
  const live = items.filter((i) => i.ids?.length).length;
  const kind = String(p.kind ?? 'row');
  const set = (args: Record<string, unknown>) => void execute('array.update', { id: arr.id, ...args });
  const vec = (key: string, i: number): number => (Array.isArray(p[key]) ? Number((p[key] as number[])[i] ?? 0) : 0);
  const setVec = (key: string, i: number, m: number | null) => {
    if (m == null) return;
    const v = [0, 1, 2].map((k) => (k === i ? m : vec(key, k)));
    set({ [key]: v });
  };
  const num = (key: string) => (typeof p[key] === 'number' ? (p[key] as number) : null);
  const alongNode = typeof p.along === 'string' ? nodes.get(p.along) : undefined;
  const all = items.flatMap((i) => i.ids ?? []);
  const select = () => useUi.setState({ selected: all, selection: all[all.length - 1] ?? null });
  return (
    <>
      <p className="pad" data-testid="array-title"><span>Array</span> <span data-no-i18n>„{arr.name}”</span>{index >= 0 && <> · <span>{`item ${index + 1} of ${items.length}`}</span></>}</p>
      <dl className="fields">
        <dt>Kind</dt>
        <dd className="ro">{kind === 'path' ? 'Along a path' : kind === 'polar' ? 'Round a centre' : kind === 'grid' ? 'In a grid' : 'In a row'}</dd>
        {kind !== 'grid' && <>
          <dt>Copies</dt>
          <dd><NumberField value={live} onCommit={(n) => { if (n != null && n >= 1) set({ count: Math.round(n) }); }} /></dd>
        </>}
        {kind === 'grid' && <>
          <dt>Columns</dt><dd><NumberField value={num('count') ?? 1} onCommit={(n) => { if (n != null && n >= 1) set({ count: Math.round(n) }); }} /></dd>
          <dt>Rows</dt><dd><NumberField value={num('rows') ?? 1} onCommit={(n) => { if (n != null && n >= 1) set({ rows: Math.round(n) }); }} /></dd>
          <dt>Levels</dt><dd><NumberField value={num('levels') ?? 1} onCommit={(n) => { if (n != null && n >= 1) set({ levels: Math.round(n), ...(n > 1 && !Array.isArray(p.levelStep) ? { levelStep: [0, 0, 3] } : {}) }); }} /></dd>
          <dt>Step x</dt><dd><LengthCell metres={vec('step', 0)} onCommit={(m) => setVec('step', 0, m)} /></dd>
          <dt>Step y</dt><dd><LengthCell metres={vec('step', 1)} onCommit={(m) => setVec('step', 1, m)} /></dd>
          <dt>Step z</dt><dd><LengthCell metres={vec('step', 2)} onCommit={(m) => setVec('step', 2, m)} /></dd>
          <dt>Row step x</dt><dd><LengthCell metres={vec('rowStep', 0)} onCommit={(m) => setVec('rowStep', 0, m)} /></dd>
          <dt>Row step y</dt><dd><LengthCell metres={vec('rowStep', 1)} onCommit={(m) => setVec('rowStep', 1, m)} /></dd>
          <dt>Level step z</dt><dd><LengthCell metres={vec('levelStep', 2)} onCommit={(m) => setVec('levelStep', 2, m)} /></dd>
        </>}
        {kind === 'row' && <>
          <dt>Step x</dt><dd><LengthCell metres={vec('step', 0)} onCommit={(m) => setVec('step', 0, m)} /></dd>
          <dt>Step y</dt><dd><LengthCell metres={vec('step', 1)} onCommit={(m) => setVec('step', 1, m)} /></dd>
          <dt>Step z</dt><dd><LengthCell metres={vec('step', 2)} onCommit={(m) => setVec('step', 2, m)} /></dd>
        </>}
        {kind === 'polar' && <>
          <dt>Angle filled (°)</dt><dd><NumberField value={num('angle')} onCommit={(n) => { if (n != null) set({ angle: n }); }} /></dd>
          <dt>Centre x</dt><dd><LengthCell metres={Array.isArray(p.center) ? Number((p.center as number[])[0]) : 0} onCommit={(m) => { if (m != null) set({ center: [m, Array.isArray(p.center) ? Number((p.center as number[])[1]) : 0] }); }} /></dd>
          <dt>Centre y</dt><dd><LengthCell metres={Array.isArray(p.center) ? Number((p.center as number[])[1]) : 0} onCommit={(m) => { if (m != null) set({ center: [Array.isArray(p.center) ? Number((p.center as number[])[0]) : 0, m] }); }} /></dd>
        </>}
        {kind === 'path' && <>
          <dt>Path</dt><dd className="ro">{alongNode ? <span data-no-i18n>{alongNode.name}</span> : 'Drawn points'}</dd>
          <dt>Spacing</dt><dd><LengthCell metres={num('spacing')} placeholder="shared out" onCommit={(m) => set({ spacing: m && m > 0 ? m : null })} /></dd>
          <dt>Start distance</dt><dd><LengthCell metres={num('start') ?? 0} onCommit={(m) => { if (m != null) set({ start: m }); }} /></dd>
          <dt>End distance</dt><dd><LengthCell metres={num('end') ?? 0} onCommit={(m) => { if (m != null) set({ end: m }); }} /></dd>
          <dt>Offset from the path (+ left)</dt><dd><LengthCell metres={num('offset') ?? 0} onCommit={(m) => { if (m != null) set({ offset: m }); }} /></dd>
          <dt>Orientation</dt>
          <dd><select value={String(p.orient ?? 'tangent')} onChange={(e) => set({ orient: e.target.value })}>
            <option value="tangent">Along the path</option><option value="fixed">Fixed</option>
          </select></dd>
          <dt>Turn round</dt>
          <dd><input type="checkbox" checked={p.flip === true} onChange={(e) => set({ flip: e.target.checked })} /></dd>
        </>}
      </dl>
      <div className="pad row-wrap">
        <button type="button" className="btn" data-action="array-select" onClick={select}><MousePointer2 size={14} />Select the array</button>
        <button type="button" className="btn" data-action="array-explode" title="The copies stay as plain elements" onClick={() => void execute('array.explode', { id: arr.id }).then((r) => {
          const ids = (r as { ids?: string[] } | null)?.ids ?? [];
          if (ids.length) useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
        })}><Blocks size={14} />Explode</button>
      </div>
    </>
  );
}
