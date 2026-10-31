/**
 * Object Styles (as in Revit): each category's cut, projection, hidden,
 * overhead and beyond lines (a pen 1–16, a colour, a pattern), for the whole
 * project; the pen table (each pen's paper width at each scale); whether
 * plans, sections and elevations show hidden lines by default. A view's
 * Visibility / Graphics and an element's own override them.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';

interface Spec { pen?: number; color?: string; pattern?: string }
interface Dto {
  objectStyles: Record<string, Record<string, Spec>>;
  penTable: { scales: number[]; widths: number[][] };
  customTable: boolean;
  hiddenLines: Record<string, string>;
  levelMark?: { style: string; size: number };
  categories: string[]; roles: string[]; patterns: string[];
}

const label = (s: string) => s[0].toUpperCase() + s.slice(1);

export function ObjectStylesDialog() {
  const nodes = useSession((s) => s.nodes);
  const [d, setD] = useState<Dto | null>(null);
  const [tab, setTab] = useState<'styles' | 'pens' | 'views'>('styles');
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { void query<Dto>('view.objectStyles').then(setD); }, [nodes]);
  if (!d) return null;
  const set = (category: string, role: string, patch: Spec) => {
    const next: Spec = { ...(d.objectStyles[category]?.[role] ?? {}), ...patch };
    for (const k of Object.keys(next) as (keyof Spec)[]) if (next[k] === '' || next[k] == null || Number.isNaN(next[k])) delete next[k];
    void execute('view.objectStyle', { category, role, spec: Object.keys(next).length ? next : null });
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Object styles" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Object styles</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <div className="tabs-inline">
            <button type="button" className={tab === 'styles' ? 'on' : ''} onClick={() => setTab('styles')}>Categories</button>
            <button type="button" className={tab === 'pens' ? 'on' : ''} onClick={() => setTab('pens')}>Pen table</button>
            <button type="button" className={tab === 'views' ? 'on' : ''} onClick={() => setTab('views')}>Hidden lines</button>
          </div>
          {tab === 'styles' && (
            <table className="layer-table os-table">
              <thead><tr><th>Category</th>{d.roles.map((r) => <th key={r}>{label(r)}</th>)}</tr></thead>
              <tbody>
                {d.categories.map((c) => (
                  <tr key={c}>
                    <td>{label(c)}</td>
                    {d.roles.map((r) => {
                      const s = d.objectStyles[c]?.[r] ?? {};
                      return (
                        <td key={r} className="os-cell">
                          <select title="Pen" value={s.pen ?? ''} onChange={(e) => set(c, r, { pen: e.target.value ? Number(e.target.value) : undefined })}>
                            <option value="">pen —</option>{Array.from({ length: 16 }, (_, i) => <option key={i + 1} value={i + 1}>pen {i + 1}</option>)}
                          </select>
                          <select title="Pattern" value={s.pattern ?? ''} onChange={(e) => set(c, r, { pattern: e.target.value || undefined })}>
                            <option value="">—</option>{d.patterns.map((p) => <option key={p} value={p}>{p}</option>)}
                          </select>
                          <input type="color" title="Colour" value={s.color ?? '#000000'} onChange={(e) => set(c, r, { color: e.target.value })} />
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {tab === 'pens' && (
            <>
              <table className="layer-table">
                <thead><tr><th>Pen</th>{d.penTable.scales.map((s) => <th key={s}>1:{s} (mm)</th>)}</tr></thead>
                <tbody>
                  {d.penTable.widths.map((row, i) => (
                    <tr key={i}>
                      <td>{i + 1}</td>
                      {row.map((w, j) => (
                        <td key={j}><input type="number" step={0.01} min={0.01} max={10} defaultValue={w} key={`${i}:${j}:${w}`}
                          onBlur={(e) => { const v = Number(e.target.value); if (v > 0 && v !== w) void execute('view.penTable', { pen: i + 1, scale: d.penTable.scales[j], width: v }); }} /></td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
              {d.customTable && <button type="button" className="btn" onClick={() => void execute('view.penTable', { reset: true })}>Back to the ISO widths</button>}
            </>
          )}
          {tab === 'views' && (<>
            <table className="layer-table">
              <thead><tr><th>View</th><th>Hidden lines by default</th></tr></thead>
              <tbody>
                {['plan', 'section', 'elevation'].map((k) => (
                  <tr key={k}>
                    <td>{label(k)}s</td>
                    <td><select value={d.hiddenLines[k] ?? ''} onChange={(e) => void execute('view.objectStyle', { kind: k, hiddenLines: e.target.value || null })}>
                      <option value="">Lapin's default</option><option value="show">shown</option><option value="hide">hidden</option>
                    </select></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="eyebrow">Level marker</p>
            <div className="row small">
              <select value={d.levelMark?.style ?? 'open'} onChange={(e) => void execute('view.objectStyle', { levelMark: { style: e.target.value } })}>
                <option value="open">Open triangle</option><option value="half">Triangle, half black</option><option value="filled">Black triangle</option><option value="tick">Tick</option>
              </select>
              <input className="num" type="number" style={{ width: 56 }} min={1} max={20} step={0.5} value={d.levelMark?.size ?? 5} onChange={(e) => { const v = Number(e.target.value); if (v >= 1 && v <= 20) void execute('view.objectStyle', { levelMark: { size: v } }); }} />
              <span className="muted small">mm on paper, in sections and elevations</span>
            </div>
          </>)}
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
