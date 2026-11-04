/**
 * Manage → Openings vs MEP (a tester's request, decision 289): which openings
 * are voids (walls, doors, windows, floors, roofs) and how much bigger, the
 * clearance a pipe or duct must keep inside one, then the check: every MEP
 * element crossing a wall, floor or roof listed with what it goes through (no
 * opening, an opening too small and by how much, or one it fits), a click
 * showing the pair in 3D; the clashes made remarks (BCF) in one step. The
 * Voids tab lists the voids with their marks and sizes, and View → Opening
 * voids draws them in 3D. The IFC export writes them on request.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { Eye, MessageSquareText, Play, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { useCamera } from '../../../viewport/camera';
import { author } from '../remarks/remarks';
import {
  clashCount, clashRemarks, DEFAULT_SETTINGS, KIND_LABELS, loadSettings, loadVoids, metresToMm, mmToMetres, pairOf, runCheck, saveSettings, shortBy, shown, sizeOf,
  STATUS_LABELS, toggleKind, VOID_KINDS, type ClashCheck, type ClashResult, type CoordinationSettings, type VoidDto,
} from './coordination';

/** The pair in the 3D view: selected, the camera looking at where they meet. */
async function showPair(r: ClashResult) {
  const ids = pairOf(r);
  useUi.setState({ view: '3d', selected: ids, selection: ids[0] ?? null });
  for (let i = 0; i < 40; i++) {
    const api = useCamera.getState().api;
    if (api) { api.orbitTo({ target: { x: r.centre[0], y: r.centre[1], z: r.centre[2] }, distance: 4 }); return; }
    await new Promise((res) => setTimeout(res, 50));
  }
}

export function CoordinationDialog() {
  const nodes = useSession((s) => s.nodes);
  const voidsOn = useUi((s) => s.openingVoids);
  const [tab, setTab] = useState<'check' | 'voids'>('check');
  const [settings, setSettings] = useState<CoordinationSettings>(DEFAULT_SETTINGS);
  const [offset, setOffset] = useState('0');
  const [clearance, setClearance] = useState('0');
  const [check, setCheck] = useState<ClashCheck | null | undefined>(undefined);
  const [voids, setVoids] = useState<VoidDto[] | null>(null);
  const [withOk, setWithOk] = useState(false);
  const [busy, setBusy] = useState(false);
  const asked = useRef(0);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => {
    void loadSettings().then((s) => { setSettings(s); setOffset(metresToMm(s.offset)); setClearance(metresToMm(s.clearance)); });
  }, []);
  const off = mmToMetres(offset);
  const clear = mmToMetres(clearance, 0, 500);
  const valid = off != null && clear != null;

  // The settings as typed, kept in the project when they changed (one step), before what reads them runs.
  const keep = useCallback(async (): Promise<boolean> => {
    if (off == null || clear == null) return false;
    const next = { ...settings, offset: off, clearance: clear };
    const now = await loadSettings();
    if (JSON.stringify(now) !== JSON.stringify({ ...now, ...next })) await saveSettings(next);
    setSettings(next);
    return true;
  }, [settings, off, clear]);

  const run = useCallback(async () => {
    const n = ++asked.current;
    setBusy(true);
    if (!(await keep())) { setBusy(false); return; }
    const [c, v] = await Promise.all([runCheck(), loadVoids()]);
    if (n !== asked.current) return;
    setCheck(c);
    setVoids(v);
    setBusy(false);
  }, [keep]);
  // The voids listed when the dialog opens and the model changes; the check is run when asked.
  useEffect(() => { void loadVoids().then(setVoids); }, [nodes]);

  const remarks = async () => {
    setBusy(true);
    const r = await clashRemarks(author());
    setBusy(false);
    if (r) notify(`Remarks: ${r.added} added, ${r.updated} updated (Manage → Model remarks…).`);
  };

  const rows = check ? shown(check.results, withOk) : [];
  const listed = voids?.filter((v) => settings.kinds.includes(v.kind)) ?? null;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide coordination" role="dialog" aria-label="Openings vs MEP" data-testid="coordination-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Openings vs MEP</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body cde">
          <p className="muted small">Every opening in a wall, floor or roof as a solid void, and every pipe, duct or cable carrier that crosses a wall, floor or roof checked against them: it must lie wholly inside a void, keeping the clearance on every side.</p>
          <dl className="fields coordination-settings">
            <dt>Voids for</dt>
            <dd className="coordination-kinds">
              {VOID_KINDS.map((k) => (
                <label key={k} className="check-line"><input type="checkbox" data-void-kind={k} style={{ width: 'auto', marginRight: 6 }} checked={settings.kinds.includes(k)} onChange={() => setSettings({ ...settings, kinds: toggleKind(settings.kinds, k) })} /> <span>{KIND_LABELS[k]}</span></label>
              ))}
            </dd>
            <dt title="Each void's sides moved out by this much (negative: in), in the 3D view and the exports">Void offset (mm)</dt>
            <dd><input data-testid="void-offset" inputMode="decimal" value={offset} onChange={(e) => setOffset(e.target.value)} /></dd>
            <dt title="How far an MEP element must stay from a void's sides">Clearance (mm)</dt>
            <dd><input data-testid="void-clearance" inputMode="decimal" value={clearance} onChange={(e) => setClearance(e.target.value)} /></dd>
            <dt>Empty voids</dt>
            <dd><label><input type="checkbox" data-testid="void-empty" style={{ width: 'auto', marginRight: 6 }} checked={settings.empty} onChange={(e) => setSettings({ ...settings, empty: e.target.checked })} /> report wall and floor openings nothing goes through</label></dd>
            <dt>3D view</dt>
            <dd><label><input type="checkbox" data-testid="void-show" style={{ width: 'auto', marginRight: 6 }} checked={voidsOn} onChange={(e) => { void keep(); useUi.setState({ openingVoids: e.target.checked }); }} /> show the voids (View → Opening voids)</label></dd>
          </dl>
          <div className="row tabs-row">
            <button type="button" className={`btn${tab === 'check' ? ' active' : ''}`} data-coordination-tab="check" onClick={() => setTab('check')}>Clashes</button>
            <button type="button" className={`btn${tab === 'voids' ? ' active' : ''}`} data-coordination-tab="voids" onClick={() => setTab('voids')}>Voids{listed ? ` (${listed.length})` : ''}</button>
          </div>
          {tab === 'check' && (
            <div className="coordination-results" data-testid="clash-results">
              {check === undefined && !busy && <p className="muted">Run the check to see what crosses walls, floors and roofs.</p>}
              {busy && <p className="muted">Checking…</p>}
              {check === null && !busy && <p className="error-text" role="alert">The check could not run: run it again.</p>}
              {check && !busy && !check.available && <p className="error-text" role="alert">The check needs the boolean library (manifold), missing here.</p>}
              {check && !busy && (
                <>
                  <p className="small coordination-counts" data-testid="clash-counts">
                    <span className="cde-block">{check.counts.missing}</span> <span>no opening</span> · <span className="cde-warn">{check.counts.tooSmall}</span> <span>too small</span> · <span>{check.counts.ok}</span> <span>fit</span>
                    {settings.empty && <> · <span>{check.counts.empty}</span> <span>empty</span></>}
                    <label className="check-line" style={{ marginLeft: 12 }}><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={withOk} onChange={(e) => setWithOk(e.target.checked)} /> show what fits</label>
                  </p>
                  {rows.length === 0 ? <p className="cde-ok">Nothing to fix.</p> : (
                    <table className="schedule-table coordination-table">
                      <thead><tr><th>Status</th><th>MEP element</th><th>Host</th><th>Void</th><th title="How far the element stands out of the void, clearance included">Short by (mm)</th></tr></thead>
                      <tbody>
                        {rows.map((r) => (
                          <tr key={r.key} data-clash-status={r.status} className={`clash-${r.status}`} onClick={() => void showPair(r)} title={r.message}>
                            <td><span className={r.status === 'missing' ? 'cde-block' : r.status === 'tooSmall' ? 'cde-warn' : undefined}>{STATUS_LABELS[r.status]}</span></td>
                            <td>{r.mep ?? ''}</td>
                            <td>{r.host ?? ''}</td>
                            <td>{r.mark ?? ''}</td>
                            <td className="num">{shortBy(r)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </>
              )}
            </div>
          )}
          {tab === 'voids' && (
            <div className="coordination-results" data-testid="void-list">
              {listed === null ? <p className="muted">Reading the openings…</p> : listed.length === 0 ? <p className="muted">No opening of these kinds.</p> : (
                <table className="schedule-table coordination-table">
                  <thead><tr><th>Mark</th><th>Kind</th><th>Host</th><th>Door or window</th><th>Size (mm)</th><th>Depth (mm)</th></tr></thead>
                  <tbody>
                    {listed.map((v) => (
                      <tr key={v.key} data-void-mark={v.mark} onClick={() => useUi.setState({ selected: [v.selectId], selection: v.selectId })}>
                        <td>{v.mark}</td><td>{KIND_LABELS[v.kind]}</td><td>{v.host ?? ''}</td><td>{v.fill ?? ''}</td><td className="num">{sizeOf(v)}</td><td className="num">{Math.round(v.depth * 1000)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn primary" data-testid="clash-run" disabled={busy || !valid} onClick={() => void run()}><Play size={13} /> Run check</button>
          <button type="button" className="btn" data-testid="clash-remarks" disabled={busy || clashCount(check ?? null) === 0} onClick={() => void remarks()} title="Each clash a remark of type Clash with its viewpoint, for BCF (run again: the same remarks updated)"><MessageSquareText size={13} /> Make remarks</button>
          <button type="button" className="btn" onClick={() => { void keep(); useUi.setState({ openingVoids: true, view: '3d', dialog: null }); }}><Eye size={13} /> Voids in 3D</button>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
