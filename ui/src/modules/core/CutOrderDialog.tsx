/**
 * The project's cut order: which class cuts which where elements meet (higher
 * cuts lower: a column through a wall, a floor over its head). Each class
 * shows its default and the project's value; an element can still set its
 * own priority in its properties.
 */

import { useEffect, useState } from 'react';
import { RotateCcw, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';

interface Row { class: string; label: string; default: number; priority: number }
interface MaterialRow { id: string; name: string; priority: number | null }

export function CutOrderDialog() {
  const nodes = useSession((s) => s.nodes);
  const [rows, setRows] = useState<Row[] | null>(null);
  const [materials, setMaterials] = useState<MaterialRow[]>([]);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    void query<Row[]>('cutOrder.list').then((r) => setRows(r ?? []));
    void query<MaterialRow[]>('cutOrder.materials').then((r) => setMaterials(r ?? []));
  }, [nodes]);
  const setMaterial = (id: string, value: number | null) => void execute('cutOrder.set', { materials: { [id]: value } });
  if (!rows) return null;
  const sorted = [...rows].sort((a, b) => b.priority - a.priority || a.label.localeCompare(b.label));
  const set = (cls: string, value: number | null) => void execute('cutOrder.set', { priorities: { [cls]: value } });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Cut order" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Cut order</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Where two elements meet, the one with the higher priority cuts the other. An element can set its own priority in its properties.</p>
          <table className="layer-table cut-order">
            <thead><tr><th>Class</th><th className="num">Default</th><th className="num">This project</th><th>Cuts</th><th /></tr></thead>
            <tbody>
              {sorted.map((r, i) => {
                const below = sorted.slice(i + 1).filter((o) => o.priority < r.priority).map((o) => o.label);
                return (
                  <tr key={r.class} className={r.priority !== r.default ? 'changed' : ''}>
                    <td title={r.class}>{r.label}</td>
                    <td className="num muted">{r.default}</td>
                    <td className="num">
                      <input type="number" min={0} max={100} step={1} defaultValue={r.priority} key={`${r.class}:${r.priority}`}
                        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
                        onBlur={(e) => { const v = Number(e.target.value); if (Number.isFinite(v) && v !== r.priority) set(r.class, Math.max(0, Math.min(100, Math.round(v)))); }} />
                    </td>
                    <td className="muted small">{below.length ? below.join(', ') : '—'}</td>
                    <td>{r.priority !== r.default && <button type="button" className="icon mini" title="Back to the default" onClick={() => set(r.class, null)}><RotateCcw size={11} /></button>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <p className="eyebrow">By material</p>
          <p className="muted small">A material given a priority ranks the elements made mainly of it (a wall's or a floor's core, a beam's or a column's material) over their class: concrete over masonry, whatever the class.</p>
          <table className="layer-table cut-order">
            <thead><tr><th>Material</th><th className="num">Priority</th><th /></tr></thead>
            <tbody>
              {materials.map((m) => (
                <tr key={m.id} className={m.priority != null ? 'changed' : ''}>
                  <td>{m.name}</td>
                  <td className="num">
                    <input type="number" min={0} max={100} step={1} placeholder="by class" defaultValue={m.priority ?? ''} key={`${m.id}:${m.priority}`}
                      onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
                      onBlur={(e) => {
                        const t = e.target.value.trim();
                        if (t === '') { if (m.priority != null) setMaterial(m.id, null); return; }
                        const v = Number(t);
                        if (Number.isFinite(v) && v !== m.priority) setMaterial(m.id, Math.max(0, Math.min(100, Math.round(v))));
                      }} />
                  </td>
                  <td>{m.priority != null && <button type="button" className="icon mini" title="Rank by class again" onClick={() => setMaterial(m.id, null)}><RotateCcw size={11} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={() => void execute('cutOrder.reset')}>Default order</button>
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
