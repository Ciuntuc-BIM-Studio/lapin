/**
 * Visibility / Graphics (VV, VG) for the view in front, 2D or 3D: by category
 * (shown, line colour and weight, fill, cut hatch) and by IFC class (shown);
 * the view's own settings or a named set of the project's, shared by every
 * view that takes it; a 2D view's hidden lines.
 */

import { useEffect, useMemo, useState } from 'react';
import { Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';

interface Row { hidden?: boolean; lineColor?: string; lineWeight?: string; fillColor?: string; cutHatch?: string }
interface Visibility { graphics?: { categories?: Record<string, Row>; classes?: Record<string, Row> } | null; graphicsSet?: string | null; sets: { id: string; name: string }[]; categories: string[] }

/** The ISO 128 pen series (mm), as the engine reads a category's weight; the older names still show when a project has them. */
const WEIGHTS = ['', '0.13', '0.18', '0.25', '0.35', '0.5', '0.7', '1.0'];
const LEGACY: Record<string, string> = { heavy: '0.7', medium: '0.5', light: '0.25', hairline: '0.13' };
const label = (s: string) => s[0].toUpperCase() + s.slice(1);

export function VisibilityDialog() {
  const nodes = useSession((s) => s.nodes);
  const [target, setTarget] = useState<string | null>(null);
  const [v, setV] = useState<Visibility | null>(null);
  const [hatches, setHatches] = useState<string[]>([]);
  const [tab, setTab] = useState<'categories' | 'classes'>('categories');
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => { void frontView().then((id) => { if (!id) { notify('Open a plan, a section, an elevation or the 3D first.'); close(); } else setTarget(id); }); }, []);
  useEffect(() => { if (target) void query<Visibility>('view.visibility', { id: target }).then(setV); }, [target, nodes]);
  useEffect(() => { void query<{ name: string }[]>('hatch.list').then((r) => r && setHatches(r.map((h) => h.name))); }, []);

  // The IFC classes of the elements on storeys.
  const classes = useMemo(() => [...new Set([...nodes.values()].filter((n) => n.storeyId).map((n) => n.type))].sort(), [nodes]);
  if (!target || !v) return null;
  const view = target === '3d' ? null : nodes.get(target);
  const row = (bucket: 'categories' | 'classes', key: string): Row => v.graphics?.[bucket]?.[key] ?? {};
  const set = (bucket: 'categories' | 'classes', key: string, patch: Row) => {
    const next: Row = { ...row(bucket, key), ...patch };
    for (const k of Object.keys(next) as (keyof Row)[]) if (next[k] === '' || next[k] === false || next[k] == null) delete next[k];
    void execute('view.graphics', { id: target, [bucket === 'categories' ? 'category' : 'class']: key, value: Object.keys(next).length ? next : null });
  };
  const setName = v.sets.find((s) => s.id === v.graphicsSet)?.name;

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Visibility / Graphics" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Visibility / Graphics · {target === '3d' ? '3D' : view?.name ?? 'View'}</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="row vv-set">
            <span className="eyebrow">Graphics</span>
            <select value={v.graphicsSet ?? ''} onChange={(e) => void execute('view.useGraphicsSet', { id: target, setId: e.target.value || null })}>
              <option value="">this view's own</option>
              {v.sets.map((s) => <option key={s.id} value={s.id}>set: {s.name}</option>)}
            </select>
            <button type="button" className="btn" onClick={() => { const name = window.prompt('The new set\'s name'); if (name?.trim()) void execute('view.saveGraphicsSet', { id: target, name: name.trim() }); }}>Save as a set…</button>
            {v.graphicsSet && <button type="button" className="btn" title="Its views keep its settings as their own" onClick={() => void execute('view.removeGraphicsSet', { setId: v.graphicsSet })}><Trash2 size={13} />Remove the set</button>}
            {setName && <span className="muted small">Changes here change „{setName}” in every view that takes it.</span>}
            {view && (
              <label className="row"><span className="muted small">Hidden lines</span>
                <select value={String(view.props.hiddenLines ?? 'auto')} onChange={(e) => void execute('element.update', { id: target, attrs: { hiddenLines: e.target.value } })}>
                  <option value="auto">by the view's kind</option><option value="show">shown</option><option value="hide">hidden</option>
                </select>
              </label>
            )}
          </div>
          <div className="tabs-inline">
            <button type="button" className={tab === 'categories' ? 'on' : ''} onClick={() => setTab('categories')}>Model categories</button>
            <button type="button" className={tab === 'classes' ? 'on' : ''} onClick={() => setTab('classes')}>IFC classes</button>
          </div>
          {tab === 'categories' ? (
            <table className="layer-table">
              <thead><tr><th>Shown</th><th>Category</th><th>Line colour</th><th>Line weight</th><th>Fill</th><th>Cut hatch</th></tr></thead>
              <tbody>
                {v.categories.map((c) => {
                  const r = row('categories', c);
                  return (
                    <tr key={c}>
                      <td><input type="checkbox" checked={!r.hidden} onChange={(e) => set('categories', c, { hidden: !e.target.checked })} /></td>
                      <td>{label(c)}</td>
                      <td><input type="color" value={r.lineColor ?? '#000000'} onChange={(e) => set('categories', c, { lineColor: e.target.value })} />{r.lineColor && <button type="button" className="icon mini" title="As the object styles" onClick={() => set('categories', c, { lineColor: '' })}><X size={10} /></button>}</td>
                      <td><select value={r.lineWeight ?? ''} onChange={(e) => set('categories', c, { lineWeight: e.target.value })}>{(r.lineWeight && !WEIGHTS.includes(r.lineWeight) ? [...WEIGHTS, r.lineWeight] : WEIGHTS).map((w) => <option key={w} value={w}>{w === '' ? 'as styled' : LEGACY[w] ? `${w} (${LEGACY[w]} mm)` : `${w} mm`}</option>)}</select></td>
                      <td><input type="color" value={r.fillColor ?? '#ffffff'} onChange={(e) => set('categories', c, { fillColor: e.target.value })} />{r.fillColor && <button type="button" className="icon mini" title="No fill of its own" onClick={() => set('categories', c, { fillColor: '' })}><X size={10} /></button>}</td>
                      <td><select value={r.cutHatch ?? ''} onChange={(e) => set('categories', c, { cutHatch: e.target.value })}><option value="">the materials'</option>{hatches.map((h) => <option key={h}>{h}</option>)}</select></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          ) : (
            <table className="layer-table">
              <thead><tr><th>Shown</th><th>IFC class</th></tr></thead>
              <tbody>
                {classes.map((c) => (
                  <tr key={c}>
                    <td><input type="checkbox" checked={!row('classes', c).hidden} onChange={(e) => set('classes', c, { hidden: !e.target.checked })} /></td>
                    <td>{c}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** The 3D leaves out what its Visibility / Graphics hides. */
export function Visibility3D() {
  const nodes = useSession((s) => s.nodes);
  const view3dId = useUi((s) => s.view3dId);
  useEffect(() => {
    let live = true;
    void import('../../platform/looks').then(({ setLooks }) =>
      query<string[]>('view.hidden3d', { id: view3dId ?? '3d' }).then((ids) => { if (live && ids) setLooks('visibility', Object.fromEntries(ids.map((id) => [id, { hidden: true }]))); }));
    return () => { live = false; };
  }, [nodes, view3dId]);
  return null;
}
