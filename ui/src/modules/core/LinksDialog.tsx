/**
 * Manage → Links: the files linked into the project (IFC, DXF), read-only.
 * Each is shown or hidden, drawn halftone, read again from its file, unlinked
 * (what it brought goes), converted to native (what it brought stays, as the
 * project's own) or opened on its own in another instance of the app (the
 * desktop), to edit there. The 3D follows: a hidden link's elements are not
 * drawn, a halftone link's are faded.
 */

import { useEffect, useState } from 'react';
import { ExternalLink, Feather, MapPin, RefreshCw, Trash2, Unlink, Wand2, X } from 'lucide-react';
import { fileName, openOptimize } from '../../engine/ifcOptimize';
import { notify, useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { isDesktop } from '../../engine/files';
import { setLooks } from '../../platform/looks';
import type { NodeDto } from '../../engine/protocol';
import { Cell } from '../../shell/cells';
import { CLOUD_MODES, CLOUD_UNITS } from '../../engine/files';
import { query } from '../../engine/session';
import { isCloudLink, setCloudStatus, statusText, usePointClouds } from '../../viewport/pointClouds';

const COLOR_MODES = [
  { value: 'rgb', label: 'RGB' }, { value: 'intensity', label: 'Intensity' }, { value: 'elevation', label: 'Elevation' }, { value: 'classification', label: 'Classification' },
];

/** Where a cloud stands: by the map, origin to origin or centred, its unit, moved (x, y, z) and turned (°), asked then sent as one step. */
export async function placeCloud(l: NodeDto): Promise<void> {
  const p = (l.props.placement ?? {}) as { mode?: string; units?: string; offset?: number[]; rotation?: number };
  const { ask, choiceField, lengthField, numberField } = await import('../../shell/AskBox');
  const off = Array.isArray(p.offset) ? p.offset : [0, 0, 0];
  const r = await ask(`Place the point cloud ${l.name}`, [
    choiceField('mode', 'Placed', p.mode ?? 'origin', CLOUD_MODES.filter((m) => m.value !== 'auto')),
    choiceField('units', 'Its unit', p.units ?? 'm', CLOUD_UNITS.filter((u) => u.value !== 'auto')),
    lengthField('x', 'Moved x', off[0] ?? 0), lengthField('y', 'Moved y', off[1] ?? 0), lengthField('z', 'Moved z', off[2] ?? 0),
    numberField('rotation', 'Turned (°)', p.rotation ?? 0),
  ]);
  if (!r) return;
  await execute('pointcloud.place', { id: l.id, mode: r.mode, units: r.units, offset: [Number(r.x), Number(r.y), Number(r.z)], rotation: Number(r.rotation) });
  const check = await query<{ warning?: string | null }>('pointcloud.check', { id: l.id });
  if (check?.warning) notify(check.warning);
}

const isLink = (n: NodeDto) => n.type === 'LapinLink';

export function LinksDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [busy, setBusy] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const links = [...nodes.values()].filter(isLink).sort((a, b) => a.name.localeCompare(b.name));
  const status = usePointClouds((st) => st.status);
  const cloudIds = links.filter(isCloudLink).map((l) => l.id).join(',');
  // The clouds' indexes, asked again while any is being made (the 3D view reports them too when it is open).
  useEffect(() => {
    if (!cloudIds) return;
    let stop = false;
    const tick = async () => {
      let busy = false;
      for (const id of cloudIds.split(',')) {
        const st = await query<{ state: string; stage?: string; done?: number; error?: string }>('pointcloud.status', { id });
        if (stop) return;
        if (st) setCloudStatus(id, st);
        if (st?.state === 'indexing') busy = true;
      }
      if (busy && !stop) setTimeout(() => void tick(), 700);
    };
    void tick();
    return () => { stop = true; };
  }, [cloudIds]);
  const counts = new Map<string, number>();
  for (const n of nodes.values()) if (typeof n.props.linkId === 'string' && n.storeyId) counts.set(n.props.linkId, (counts.get(n.props.linkId) ?? 0) + 1);
  const run = async (id: string, what: () => Promise<unknown>) => {
    setBusy(id);
    try { await what(); } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(null); }
  };
  const openSeparately = async (path: string) => {
    const { invoke } = await import('@tauri-apps/api/core');
    await invoke('open_separately', { path });
    notify('Opening the file in another Lapin window, on its own, to edit there.');
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Links" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Links</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {links.length === 0 && <p className="muted">Nothing is linked. File → Link IFC…, or Import DXF… with „As a link”, brings a file in read-only; it is listed here.</p>}
          {links.length > 0 && (
            <table className="layer-table">
              <thead><tr><th>Name</th><th>Kind</th><th>File</th><th>Elements</th><th title="Drawn in every view">Shown</th><th title="Drawn grey, faded in 3D">Halftone</th><th title="A linked DXF's drawing on its view's plane in the 3D view">3D</th><th /></tr></thead>
              <tbody>
                {links.map((l) => {
                  const path = typeof l.props.path === 'string' ? l.props.path : null;
                  const file = typeof l.props.file === 'string' ? l.props.file : '';
                  const cloud = isCloudLink(l);
                  const info = (l.props.cloud ?? {}) as { points?: number; format?: string };
                  return (
                    <tr key={l.id} className={busy === l.id ? 'muted' : undefined}>
                      <td><Cell value={l.name} onCommit={(t) => { const v = t.trim(); if (!v) return false; if (v !== l.name) void execute('link.update', { id: l.id, name: v }); }} /></td>
                      <td>{cloud ? <span title={info.format}>Point cloud</span> : String(l.props.kind ?? '').toUpperCase()}</td>
                      <td title={path ?? 'linked from the browser: its bytes were read once'}>{file}{!path && <span className="muted small"> (no path)</span>}
                        {cloud && <div className="muted small" data-cloud-status={status[l.id]?.state ?? 'waiting'}>{statusText(status[l.id])}</div>}
                        {cloud && (
                          <div className="row-tools">
                            <select aria-label="Colours" title="What colours its points" value={String(l.props.colorMode ?? 'rgb')} onChange={(e) => void execute('pointcloud.update', { id: l.id, colorMode: e.target.value })}>
                              {COLOR_MODES.map((m) => <option key={m.value} value={m.value}>{m.label}</option>)}
                            </select>
                            <label className="small" title="Point size (px)" style={{ marginLeft: 8 }}>Size <input type="number" min={1} max={12} step={1} style={{ width: 44 }} value={Number(l.props.pointSize ?? 2)} onChange={(e) => void execute('pointcloud.update', { id: l.id, pointSize: Number(e.target.value) })} /></label>
                          </div>
                        )}
                      </td>
                      <td className="num" title={cloud ? 'points' : undefined}>{cloud ? (info.points ?? 0).toLocaleString() : counts.get(l.id) ?? 0}</td>
                      <td><input type="checkbox" checked={l.props.visible !== false} onChange={(e) => void execute('link.update', { id: l.id, visible: e.target.checked })} /></td>
                      <td><input type="checkbox" checked={l.props.halftone === true} onChange={(e) => void execute('link.update', { id: l.id, halftone: e.target.checked })} /></td>
                      <td>{(l.props.kind === 'dxf' || cloud) && <input type="checkbox" data-link="in3d" title="Its drawing on its view's plane in the 3D view" checked={l.props.in3d !== false} onChange={(e) => void execute('link.update', { id: l.id, in3d: e.target.checked })} />}</td>
                      <td className="row-tools">
                        {cloud && <button type="button" className="icon mini" data-link="place" title="Where it stands: by the map, origin to origin or centred, its unit, moved and turned" disabled={busy != null} onClick={() => void run(l.id, () => placeCloud(l))}><MapPin size={12} /></button>}
                        {path && cloud && <button type="button" className="icon mini" title="Read the file again (indexed again if it changed)" disabled={busy != null} onClick={() => void run(l.id, () => execute('pointcloud.reload', { id: l.id }))}><RefreshCw size={12} /></button>}
                        {path && !cloud && <button type="button" className="icon mini" title="Read the file again (what the link holds is replaced)" disabled={busy != null} onClick={() => void run(l.id, () => engine.call('project.reloadLink', { id: l.id }, { timeoutMs: 600_000 }).then(() => notify(`${l.name} read again.`)))}><RefreshCw size={12} /></button>}
                        {!cloud && <button type="button" className="icon mini" title="Convert to native: what it brought stays, editable, as the project's own" disabled={busy != null} onClick={() => void run(l.id, () => execute('link.convert', { id: l.id }))}><Wand2 size={12} /></button>}
                        {path && isDesktop() && l.props.kind === 'ifc' && <button type="button" className="icon mini" data-link="optimize" title="Optimize the source file…" disabled={busy != null} onClick={() => openOptimize([{ name: fileName(path), path }])}><Feather size={12} /></button>}
                        {path && isDesktop() && !cloud && <button type="button" className="icon mini" title="Open on its own in another Lapin window, to edit there" disabled={busy != null} onClick={() => void run(l.id, () => openSeparately(path))}><ExternalLink size={12} /></button>}
                        <button type="button" className="icon mini" title="Unlink: the link and everything it brought go" disabled={busy != null}
                          onClick={() => { if (window.confirm(`Unlink ${l.name}? Everything it brought in goes (undo brings it back).`)) void run(l.id, () => execute('link.unlink', { id: l.id })); }}><Unlink size={12} /></button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
          {error && <p className="error-text" role="alert">{error}</p>}
          <p className="muted small"><Trash2 size={11} /> What a link brought cannot be edited or deleted on its own: unlink it, convert it, or open it on its own.</p>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** The 3D follows the links: a hidden link's elements are not drawn, a halftone link's are faded. */
export function LinksLook() {
  const nodes = useSession((s) => s.nodes);
  useEffect(() => {
    const looks: Record<string, { hidden?: boolean; opacity?: number }> = {};
    for (const n of nodes.values()) {
      if (typeof n.props.linkId !== 'string') continue;
      const link = nodes.get(n.props.linkId);
      if (!link || link.props.visible === false) looks[n.id] = { hidden: true };
      else if (link.props.halftone === true) looks[n.id] = { opacity: 0.35 };
    }
    setLooks('links', looks);
  }, [nodes]);
  return null;
}
