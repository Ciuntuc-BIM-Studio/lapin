/**
 * Room colours (View → Room colours, a plan's properties, a room's): the
 * quick configurator of the project's colour schemes. Pick or make a scheme,
 * what it colours by (function, name, department…), then a colour and/or a
 * hatch for each value the rooms have; "Fill missing colours" gives every
 * value without one a colour of the palette. The plan in front shows the
 * scheme chosen for it; a colour legend lists what it uses.
 */

import { useEffect, useMemo, useState } from 'react';
import { Copy, Paintbrush, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { frontView } from '../../viewport/frontView';
import { HatchSwatch } from '../core/HatchSwatch';
import { activate } from '../../platform/modules';
import { useSchemes, type SchemeEntry } from './rooms';
import { colorLegend } from './roomTools';

const area = (m2: number) => `${m2.toFixed(2)} m²`;

export function RoomColorsDialog() {
  const nodes = useSession((s) => s.nodes);
  const uiView = useUi((x) => x.view);
  const [viewId, setViewId] = useState<string | null>(null);
  const [s] = useSchemes(viewId);
  const [picked, setPicked] = useState<string | null>(null);
  const [all, setAll] = useState(false);
  const [hatches, setHatches] = useState<string[]>([]);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => { if (uiView === 'plan') void frontView().then(setViewId); }, [uiView]);
  useEffect(() => { void query<{ name: string }[]>('hatch.list').then((r) => r && setHatches(r.map((h) => h.name))); }, []);

  const view = viewId ? nodes.get(viewId) : undefined;
  const scheme = s?.schemes.find((x) => x.id === (picked ?? s.viewScheme ?? s.schemes[0]?.id)) ?? null;
  const values = useMemo(() => (scheme && s ? s.values[scheme.by] ?? [] : []), [s, scheme]);
  // Every entry of the scheme, then the values the rooms have that it does not list yet.
  const rows = useMemo(() => {
    if (!scheme) return [];
    const listed = scheme.entries.map((e) => ({ entry: e as SchemeEntry | null, value: e.value, used: values.find((v) => v.value.toLowerCase() === e.value.toLowerCase()) }));
    const extra = values.filter((v) => !scheme.entries.some((e) => e.value.toLowerCase() === v.value.toLowerCase())).map((v) => ({ entry: null, value: v.value, used: v }));
    // The values the rooms have first; the scheme's other entries after them, on request.
    return [...listed.filter((r) => r.used), ...extra, ...listed.filter((r) => !r.used)];
  }, [scheme, values]);
  const unused = rows.filter((r) => !r.used).length;
  const shown = all || rows.length === unused ? rows : rows.filter((r) => r.used);
  if (!s) return null;

  /** One value's look changed (null clears a field). */
  const set = (value: string, patch: Record<string, string | number | null>) => scheme && void execute('architecture.setColorSchemeEntry', { id: scheme.id, value, ...patch });
  const showInView = (id: string | null) => viewId && void execute('architecture.viewColorScheme', { viewId, schemeId: id });
  const create = async (from?: string) => {
    const r = await execute('architecture.createColorScheme', from ? { from } : { by: scheme?.by ?? 'function' }) as { id?: string } | null;
    if (r?.id) setPicked(r.id);
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide room-colors" role="dialog" aria-label="Room colours" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Room colours</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="row vv-set">
            <span className="eyebrow">Scheme</span>
            <select value={scheme?.id ?? ''} onChange={(e) => setPicked(e.target.value)}>
              {s.schemes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
            <button type="button" className="btn" title="A new scheme, its values filled from the rooms" onClick={() => void create()}><Plus size={13} />New</button>
            {scheme && <button type="button" className="btn" title="A copy of this scheme" onClick={() => void create(scheme.id)}><Copy size={13} />Duplicate</button>}
            {scheme && <button type="button" className="btn" title="Delete the scheme: the views that show it fill no rooms"
              onClick={() => { if (window.confirm(`Delete the colour scheme ${scheme.name}?`)) { void execute('architecture.deleteColorScheme', { id: scheme.id }); setPicked(null); } }}><Trash2 size={13} />Delete</button>}
          </div>
          {scheme && (
            <div className="room-colors-head">
              <label>Name
                <input key={scheme.id + scheme.name} defaultValue={scheme.name} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== scheme.name) void execute('architecture.updateColorScheme', { id: scheme.id, name: v }); }} />
              </label>
              <label title="The rooms' field the colours go by">Colour by
                <select value={scheme.by} onChange={(e) => void execute('architecture.updateColorScheme', { id: scheme.id, by: e.target.value, fill: true })}>
                  {s.fields.map((f) => <option key={f.key} value={f.key}>{f.label}</option>)}
                </select>
              </label>
              {view ? (
                <label title="The plan in front fills its rooms with the scheme it names; none by default">This plan shows
                  <select value={s.viewScheme ?? ''} onChange={(e) => showInView(e.target.value || null)}>
                    <option value="">no room colours</option>
                    {s.schemes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                  </select>
                  {s.viewScheme !== scheme.id && <button type="button" className="btn small" onClick={() => showInView(scheme.id)}>Show this scheme</button>}
                </label>
              ) : <span className="muted small">Open a plan to show room colours in it.</span>}
            </div>
          )}
          {scheme && (
            <table className="layer-table room-colors-table">
              <thead><tr><th>Value</th><th title="Rooms with this value">Rooms</th><th>Area</th><th>Colour</th><th>Hatch</th><th title="Hatch scale">Scale</th><th title="Hatch colour">Ink</th><th>Looks</th><th /></tr></thead>
              <tbody>
                {rows.length === 0 && <tr><td colSpan={9} className="muted">No room has a value for this field yet. Give rooms a {s.fields.find((f) => f.key === scheme.by)?.label.toLowerCase() ?? scheme.by} in their properties.</td></tr>}
                {shown.map(({ entry, value, used }) => (
                  <tr key={value} className={used ? undefined : 'unused'}>
                    <td className="value">{value}</td>
                    <td className="num">{used?.count ?? 0}</td>
                    <td className="num">{used ? area(used.area) : ''}</td>
                    <td>
                      <span className="pick">
                        <input type="color" value={entry?.color ?? '#ffffff'} onChange={(e) => set(value, { color: e.target.value })} />
                        {entry?.color && <button type="button" className="icon mini" title="No fill colour" onClick={() => set(value, { color: null })}><X size={10} /></button>}
                      </span>
                    </td>
                    <td>
                      <select value={entry?.hatch ?? ''} onChange={(e) => set(value, { hatch: e.target.value || null })}>
                        <option value="">none</option>
                        {hatches.map((h) => <option key={h}>{h}</option>)}
                      </select>
                    </td>
                    <td><input className="num narrow" type="number" min={0.1} step={0.1} disabled={!entry?.hatch} value={entry?.hatchScale ?? 1}
                      onChange={(e) => { const v = Number(e.target.value); if (v > 0) set(value, { hatchScale: v }); }} /></td>
                    <td><input type="color" disabled={!entry?.hatch} value={entry?.hatchColor ?? '#555555'} onChange={(e) => set(value, { hatchColor: e.target.value })} /></td>
                    <td><HatchSwatch hatch={entry?.hatch ?? null} scale={entry?.hatchScale ?? 1} fill={entry?.color ?? (entry?.hatch ? '#ffffff' : null)} size={28} /></td>
                    <td>{entry && <button type="button" className="icon mini" title="Take the value out of the scheme" onClick={() => void execute('architecture.setColorSchemeEntry', { id: scheme.id, value, remove: true })}><Trash2 size={11} /></button>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {scheme && unused > 0 && unused < rows.length && (
            <label className="row small muted"><input type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />Also the values no room has yet</label>
          )}
        </div>
        <footer className="dialog-foot">
          {scheme && <button type="button" className="btn" title="Every value the rooms have gets an entry, and every entry without a look a colour of the palette"
            onClick={() => void (execute('architecture.fillColorScheme', { id: scheme.id }) as Promise<{ added?: number } | null>).then((r) => r && notify(r.added ? `${r.added} value(s) added.` : 'Every value has its colour.'))}><Paintbrush size={13} />Fill missing colours</button>}
          {scheme && view && <button type="button" className="btn" title="Click where the legend goes on the plan" onClick={() => { if (s.viewScheme !== scheme.id) showInView(scheme.id); close(); activate(colorLegend); }}>Place a legend</button>}
          <span className="grow" />
          <button type="button" className="btn primary" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
