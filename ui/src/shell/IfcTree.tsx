/**
 * The IFC tab of the browser: the model as the classic IFC explorer shows it
 * (storey → class → type → element), with one level more under an element:
 * its construction (the representation the file gave an imported element,
 * Lapin's own parameters for a native one), its openings and their fills.
 * A click selects; a storey's row makes it the current level.
 */

import { useMemo, useState } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { select, useUi } from '../app/store';
import { useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';
import { describeItem, ifcTree, ownParameters, type TreeElement } from './ifcTreeModel';

const KEY = 'lapin.ifcTree.open';
function loadOpen(): Set<string> {
  try { return new Set(JSON.parse(localStorage.getItem(KEY) ?? '[]') as string[]); } catch { return new Set(); }
}

export function IfcTree() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const selected = useUi((s) => s.selected);
  const storeyId = useUi((s) => s.storeyId);
  const [open, setOpen] = useState<Set<string>>(loadOpen);
  const [filter, setFilter] = useState('');
  const tree = useMemo(() => ifcTree({ nodes, relations }), [nodes, relations]);
  const toggle = (id: string) => {
    const next = new Set(open);
    if (next.has(id)) next.delete(id); else next.add(id);
    setOpen(next);
    try { localStorage.setItem(KEY, JSON.stringify([...next])); } catch { /* a convenience only */ }
  };
  const q = filter.trim().toLowerCase();
  const matches = (n: NodeDto) => !q || n.name.toLowerCase().includes(q) || n.type.toLowerCase().includes(q) || n.id.toLowerCase().includes(q);

  const Fold = ({ id, label, count, on, onPick, depth, children, force }: { id: string; label: React.ReactNode; count?: number; on?: boolean; onPick?: () => void; depth: number; children: React.ReactNode; force?: boolean }) => {
    const isOpen = force || open.has(id);
    return (
      <div className="ifc-node">
        <div className={`ifc-row${on ? ' on' : ''}`} style={{ paddingLeft: 8 + depth * 14 }}>
          <button type="button" className="icon mini fold" title={isOpen ? 'Collapse' : 'Expand'} onClick={() => toggle(id)}>{isOpen ? <ChevronDown size={12} /> : <ChevronRight size={12} />}</button>
          <button type="button" className="ifc-label" onClick={onPick ?? (() => toggle(id))}>{label}</button>
          {count != null && <span className="count">{count}</span>}
        </div>
        {isOpen && children}
      </div>
    );
  };

  const Leaf = ({ label, depth, on, onPick, muted }: { label: React.ReactNode; depth: number; on?: boolean; onPick?: () => void; muted?: boolean }) => (
    <div className={`ifc-row leaf${on ? ' on' : ''}${muted ? ' muted' : ''}`} style={{ paddingLeft: 8 + depth * 14 + 18 }}>
      <button type="button" className="ifc-label" onClick={onPick} disabled={!onPick}>{label}</button>
    </div>
  );

  const Element = ({ e, depth }: { e: TreeElement; depth: number }) => {
    const n = e.node;
    const imported = e.shape.length > 0;
    const params = imported ? [] : ownParameters(n);
    return (
      <Fold id={'e:' + n.id} depth={depth} on={selected.includes(n.id)} onPick={() => select(n.id)} label={<>{n.name}<span className="ifc-id">{n.id.slice(0, 6)}</span></>} force={!!q && !open.has('e:' + n.id) ? false : undefined}>
        {imported && (
          <Fold id={'s:' + n.id} depth={depth + 1} label={<span className="ifc-kind">Construction (IFC)</span>} count={e.shape.length}>
            {e.shape.map((item, i) => <Leaf key={i} depth={depth + 2} label={describeItem(item)} muted />)}
          </Fold>
        )}
        {!imported && params.length > 0 && (
          <Fold id={'p:' + n.id} depth={depth + 1} label={<span className="ifc-kind">Construction (Lapin)</span>} count={params.length}>
            {params.map(([k, v]) => <Leaf key={k} depth={depth + 2} label={<><span className="ifc-key">{k}</span> {v}</>} muted />)}
          </Fold>
        )}
        {e.openings.length > 0 && (
          <Fold id={'o:' + n.id} depth={depth + 1} label={<span className="ifc-kind">Openings</span>} count={e.openings.length}>
            {e.openings.map(({ opening, fill }) => (
              <Leaf key={opening.id} depth={depth + 2} on={selected.includes(fill?.id ?? opening.id)} onPick={() => select(fill?.id ?? opening.id)}
                label={fill ? <>{fill.type.replace(/^Ifc/, '')} · {fill.name}</> : <>Opening · {opening.name}</>} />
            ))}
          </Fold>
        )}
      </Fold>
    );
  };

  return (
    <div className="ifc-tree">
      <input className="filter" placeholder="Filter by name, class or id" value={filter} onChange={(e) => setFilter(e.target.value)} />
      {tree.map((st) => {
        const sid = 'st:' + (st.node?.id ?? 'none');
        return (
          <Fold key={sid} id={sid} depth={0} count={st.count} on={!!st.node && st.node.id === storeyId}
            onPick={st.node ? () => useUi.setState({ storeyId: st.node!.id, selection: st.node!.id, selected: [st.node!.id] }) : undefined}
            label={<strong>{st.name}</strong>} force={!!q || undefined}>
            {st.classes.map((c) => {
              const visibleTypes = c.types.map((t) => ({ ...t, elements: t.elements.filter((e) => matches(e.node) || (t.node && matches(t.node))) })).filter((t) => t.elements.length > 0);
              if (visibleTypes.length === 0) return null;
              const cid = `${sid}:${c.cls}`;
              return (
                <Fold key={cid} id={cid} depth={1} count={c.count} label={c.cls} force={!!q || undefined}>
                  {visibleTypes.map((t) => (
                    <Fold key={t.id} id={`${cid}:${t.id}`} depth={2} count={t.elements.length} on={!!t.node && selected.includes(t.node.id)}
                      onPick={t.node ? () => select(t.node!.id) : undefined} label={<span className="ifc-type">{t.name}</span>} force={!!q || undefined}>
                      {t.elements.map((e) => <Element key={e.node.id} e={e} depth={3} />)}
                    </Fold>
                  ))}
                </Fold>
              );
            })}
          </Fold>
        );
      })}
    </div>
  );
}
