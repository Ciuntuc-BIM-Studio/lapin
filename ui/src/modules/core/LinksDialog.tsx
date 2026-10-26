/**
 * Manage → Links: the files linked into the project (IFC, DXF), read-only.
 * Each is shown or hidden, drawn halftone, read again from its file, unlinked
 * (what it brought goes), converted to native (what it brought stays, as the
 * project's own) or opened on its own in another instance of the app (the
 * desktop), to edit there. The 3D follows: a hidden link's elements are not
 * drawn, a halftone link's are faded.
 */

import { useEffect, useState } from 'react';
import { ExternalLink, RefreshCw, Trash2, Unlink, Wand2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { isDesktop } from '../../engine/files';
import { setLooks } from '../../platform/looks';
import type { NodeDto } from '../../engine/protocol';

const isLink = (n: NodeDto) => n.type === 'LapinLink';

export function LinksDialog() {
  const nodes = useSession((s) => s.nodes);
  const [busy, setBusy] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const links = [...nodes.values()].filter(isLink).sort((a, b) => a.name.localeCompare(b.name));
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
              <thead><tr><th>Name</th><th>Kind</th><th>File</th><th>Elements</th><th title="Drawn in every view">Shown</th><th title="Drawn grey, faded in 3D">Halftone</th><th /></tr></thead>
              <tbody>
                {links.map((l) => {
                  const path = typeof l.props.path === 'string' ? l.props.path : null;
                  const file = typeof l.props.file === 'string' ? l.props.file : '';
                  return (
                    <tr key={l.id} className={busy === l.id ? 'muted' : undefined}>
                      <td><input defaultValue={l.name} onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== l.name) void execute('link.update', { id: l.id, name: v }); }} /></td>
                      <td>{String(l.props.kind ?? '').toUpperCase()}</td>
                      <td title={path ?? 'linked from the browser: its bytes were read once'}>{file}{!path && <span className="muted small"> (no path)</span>}</td>
                      <td className="num">{counts.get(l.id) ?? 0}</td>
                      <td><input type="checkbox" checked={l.props.visible !== false} onChange={(e) => void execute('link.update', { id: l.id, visible: e.target.checked })} /></td>
                      <td><input type="checkbox" checked={l.props.halftone === true} onChange={(e) => void execute('link.update', { id: l.id, halftone: e.target.checked })} /></td>
                      <td className="row-tools">
                        {path && <button type="button" className="icon mini" title="Read the file again (what the link holds is replaced)" disabled={busy != null} onClick={() => void run(l.id, () => engine.call('project.reloadLink', { id: l.id }, { timeoutMs: 600_000 }).then(() => notify(`${l.name} read again.`)))}><RefreshCw size={12} /></button>}
                        <button type="button" className="icon mini" title="Convert to native: what it brought stays, editable, as the project's own" disabled={busy != null} onClick={() => void run(l.id, () => execute('link.convert', { id: l.id }))}><Wand2 size={12} /></button>
                        {path && isDesktop() && <button type="button" className="icon mini" title="Open on its own in another Lapin window, to edit there" disabled={busy != null} onClick={() => void run(l.id, () => openSeparately(path))}><ExternalLink size={12} /></button>}
                        <button type="button" className="icon mini" title="Unlink: the link and everything it brought go" disabled={busy != null}
                          onClick={() => { if (window.confirm(`Unlink ${l.name}? Everything it brought in goes (undo brings it back).`)) void run(l.id, () => execute('link.unlink', { id: l.id })); }}><Unlink size={12} /></button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
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
