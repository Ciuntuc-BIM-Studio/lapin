/**
 * Manage → Clash check… (decisions 289 and 293): openings vs MEP run on demand. Run clash check runs the engine's check
 * once and keeps its report in the project (its date, the model's state, the counts) until it is run again; the report
 * says when the model has changed since. Its problems are grouped by kind (no opening, not cut by the cut order, an
 * opening too small, an empty void), each open, fixed or ignored with a comment; a click shows the pair in 3D (alone
 * when asked), previous and next go through them, and the view in front marks them with red clouds. Make opening and
 * Enlarge opening fix them (one undo step), a run again marks what is fixed. Out as BCF remarks, CSV or PDF. The Voids
 * tab lists the openings' voids; Settings holds which openings are voids, their offset, the clearance, empty voids.
 */

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ChevronLeft, ChevronRight, Cloud, Download, Eye, FileText, Hammer, MessageSquareText, Play, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { saveFile } from '../../../engine/files';
import { useCamera } from '../../../viewport/camera';
import { isolateIds, resetTemporary } from '../../../viewport/temporary';
import { Cell } from '../../../shell/cells';
import { author, exportBcf } from '../remarks/remarks';
import {
  DEFAULT_SETTINGS, fixItems, fixOf, groupItems, itemPair, itemShortBy, ITEM_KIND_LABELS, KIND_LABELS, loadReport, loadSettings, loadVoids, metresToMm, mmToMetres,
  reportDate, reportFile, reportRemarks, runClashCheck, saveSettings, setItems, sizeOf, STATE_LABELS, step, toggleClashMarks, toggleKind, VOID_KINDS,
  type ClashItem, type ClashReport, type CoordinationSettings, type VoidDto,
} from './coordination';

/** The item in the 3D view: the pair selected (alone when asked), the camera looking at where they meet. */
async function showItem(i: ClashItem, alone: boolean) {
  const ids = itemPair(i);
  useUi.setState({ view: '3d', selected: ids, selection: ids[0] ?? null });
  if (alone) isolateIds(ids);
  for (let k = 0; k < 40; k++) {
    const api = useCamera.getState().api;
    if (api) { api.orbitTo({ target: { x: i.centre[0], y: i.centre[1], z: i.centre[2] }, distance: 4 }); return; }
    await new Promise((res) => setTimeout(res, 50));
  }
}

const toBytes = (b64: string) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));

export function CoordinationDialog() {
  const nodes = useSession((s) => s.nodes);
  const version = useSession((s) => s.version);
  const voidsOn = useUi((s) => s.openingVoids);
  const [tab, setTab] = useState<'report' | 'voids' | 'settings'>('report');
  const [settings, setSettings] = useState<CoordinationSettings>(DEFAULT_SETTINGS);
  const [offset, setOffset] = useState('0');
  const [clearance, setClearance] = useState('0');
  const [report, setReport] = useState<ClashReport | null | undefined>(undefined);
  const [voids, setVoids] = useState<VoidDto[] | null>(null);
  const [show, setShow] = useState({ fixed: false, ignored: false });
  const [picked, setPicked] = useState<Set<number>>(new Set());
  const [current, setCurrent] = useState<number | null>(null);
  const [alone, setAlone] = useState(false);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => {
    void loadSettings().then((s) => { setSettings(s); setOffset(metresToMm(s.offset)); setClearance(metresToMm(s.clearance)); });
  }, []);
  // The report as kept, read again when the model changes (it says then whether it is out of date); never run here.
  useEffect(() => { void loadReport().then((r) => setReport(r ?? null)); }, [version]);
  useEffect(() => { void loadVoids().then(setVoids); }, [nodes]);
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

  const run = async () => {
    setBusy(true);
    if (await keep()) {
      const r = await runClashCheck();
      if (r) { setReport(r); setPicked(new Set()); setTab('report'); }
    }
    setBusy(false);
  };

  const groups = useMemo(() => (report ? groupItems(report.items, show) : []), [report, show]);
  const byNumber = useMemo(() => new Map((report?.items ?? []).map((i) => [i.n, i])), [report]);
  const go = (n: number | null) => {
    if (n == null) return;
    setCurrent(n);
    const i = byNumber.get(n);
    if (i) void showItem(i, alone);
  };
  const fix = async (numbers: number[]) => {
    if (numbers.length === 0) return;
    setBusy(true);
    const r = await fixItems(numbers);
    setBusy(false);
    if (!r) return;
    if (r.failed.length) notify(r.failed.map((f) => `${f.n}: ${f.message}`).join(' · '));
    else notify('Openings made: one step to undo. Run the check again to confirm.');
    setPicked(new Set());
  };
  const ignore = (numbers: number[], state: 'open' | 'ignored') => { if (numbers.length) void setItems(numbers, { state }); setPicked(new Set()); };
  const bcf = async () => {
    setBusy(true);
    const r = await reportRemarks(author());
    setBusy(false);
    if (r?.ids.length) await exportBcf({ ids: r.ids });
    else notify('No open clash to send.');
  };
  const file = async (format: 'csv' | 'pdf') => {
    const r = await reportFile(format);
    if (r) await saveFile(toBytes(r.data), r.name, format === 'pdf' ? 'application/pdf' : 'text/csv');
  };
  const marks = async () => {
    const on = await toggleClashMarks();
    if (on == null) notify('Open a plan, section or elevation to mark the clashes in.');
  };

  const listed = voids?.filter((v) => settings.kinds.includes(v.kind)) ?? null;
  const open = report?.counts.open ?? 0;
  const pickedList = [...picked];
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide coordination" role="dialog" aria-label="Clash check" data-testid="coordination-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Clash check</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body cde">
          <p className="muted small">Every pipe, duct or cable carrier that crosses a wall, floor or roof must lie wholly inside an opening, keeping the clearance on every side. The check runs only when asked; its report stays in the project until it runs again.</p>
          <div className="row tabs-row">
            <button type="button" className={`btn${tab === 'report' ? ' active' : ''}`} data-coordination-tab="report" onClick={() => setTab('report')}>Report</button>
            <button type="button" className={`btn${tab === 'voids' ? ' active' : ''}`} data-coordination-tab="voids" onClick={() => setTab('voids')}>Voids{listed ? ` (${listed.length})` : ''}</button>
            <button type="button" className={`btn${tab === 'settings' ? ' active' : ''}`} data-coordination-tab="settings" onClick={() => setTab('settings')}>Settings</button>
          </div>
          {tab === 'report' && (
            <div data-testid="clash-report">
              {report === undefined && <p className="muted">Reading the report…</p>}
              {report === null && !busy && <p className="muted" data-testid="clash-none">No clash check has been run yet: Run clash check runs it once and keeps its report here.</p>}
              {busy && <p className="muted">Checking…</p>}
              {report && (
                <>
                  <p className="small coordination-counts" data-testid="clash-counts">
                    <span>Run on</span> <span data-testid="clash-date">{reportDate(report.date)}</span> ·
                    <span className="cde-block">{report.counts.missing ?? 0}</span> <span>no opening</span> ·
                    <span className="cde-block">{report.counts.held ?? 0}</span> <span>not cut</span> ·
                    <span className="cde-warn">{report.counts.tooSmall ?? 0}</span> <span>too small</span> ·
                    <span>{report.counts.empty ?? 0}</span> <span>empty</span> ·
                    <span className="cde-ok">{report.counts.fixed ?? 0}</span> <span>resolved</span> ·
                    <span>{report.counts.ignored ?? 0}</span> <span>ignored</span>
                  </p>
                  {report.stale && <p className="error-text" role="status" data-testid="clash-stale">Out of date: the model has changed since this run. Run the check again.</p>}
                  <div className="row coordination-tools">
                    <button type="button" className="btn small" data-testid="clash-prev" disabled={groups.length === 0} onClick={() => go(step(groups, current, -1))} title="Previous clash"><ChevronLeft size={13} /></button>
                    <button type="button" className="btn small" data-testid="clash-next" disabled={groups.length === 0} onClick={() => go(step(groups, current, 1))} title="Next clash"><ChevronRight size={13} /></button>
                    <label className="check-line"><input type="checkbox" data-testid="clash-alone" style={{ width: 'auto', marginRight: 6 }} checked={alone} onChange={(e) => { setAlone(e.target.checked); if (!e.target.checked) resetTemporary(); }} /> <span>alone in 3D</span></label>
                    <label className="check-line"><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={show.fixed} onChange={(e) => setShow({ ...show, fixed: e.target.checked })} /> <span>show resolved</span></label>
                    <label className="check-line"><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={show.ignored} onChange={(e) => setShow({ ...show, ignored: e.target.checked })} /> <span>show ignored</span></label>
                    <span className="grow" />
                    <button type="button" className="btn small" data-testid="clash-fix-picked" disabled={busy || pickedList.length === 0} onClick={() => void fix(pickedList)} title="An opening made or enlarged round each selected clash, with the clearance (one step)"><Hammer size={12} /> Fix selected</button>
                    <button type="button" className="btn small" data-testid="clash-ignore-picked" disabled={busy || pickedList.length === 0} onClick={() => ignore(pickedList, 'ignored')}>Ignore</button>
                    <button type="button" className="btn small" disabled={busy || pickedList.length === 0} onClick={() => ignore(pickedList, 'open')}>Reopen</button>
                  </div>
                  {groups.length === 0 ? <p className="cde-ok">Nothing to fix.</p> : (
                    <div className="coordination-results">
                      {groups.map((g) => (
                        <div key={g.kind} data-clash-group={g.kind}>
                          <p className="eyebrow">{ITEM_KIND_LABELS[g.kind]} · {g.items.length}</p>
                          <table className="schedule-table coordination-table">
                            <thead><tr><th /><th>#</th><th>Status</th><th>MEP element</th><th>Host</th><th>Void</th><th title="How far the element stands out of the void, clearance included">Short by (mm)</th><th>Comments</th><th /></tr></thead>
                            <tbody>
                              {g.items.map((i) => {
                                const how = fixOf(i);
                                return (
                                  <tr key={i.key} data-clash-n={i.n} data-clash-state={i.state} className={`clash-${i.kind}${current === i.n ? ' current' : ''}`} onClick={() => go(i.n)} title={i.message}>
                                    <td onClick={(e) => e.stopPropagation()}><input type="checkbox" style={{ width: 'auto' }} checked={picked.has(i.n)} onChange={(e) => { const s = new Set(picked); if (e.target.checked) s.add(i.n); else s.delete(i.n); setPicked(s); }} /></td>
                                    <td className="num"><b>{i.n}</b></td>
                                    <td><span className={i.state === 'open' ? (i.kind === 'tooSmall' ? 'cde-warn' : 'cde-block') : i.state === 'fixed' ? 'cde-ok' : undefined}>{STATE_LABELS[i.state]}</span></td>
                                    <td>{i.mep ?? ''}</td>
                                    <td>{i.host ?? ''}</td>
                                    <td>{i.mark ?? ''}</td>
                                    <td className="num">{itemShortBy(i)}</td>
                                    <td onClick={(e) => e.stopPropagation()}><Cell value={i.comment ?? ''} placeholder="—" testId={`clash-comment-${i.n}`} onCommit={(t) => { if (t.trim() !== (i.comment ?? '')) void setItems([i.n], { comment: t }); }} /></td>
                                    <td onClick={(e) => e.stopPropagation()}>
                                      {i.state === 'open' && how && <button type="button" className="link" data-clash-fix={i.n} disabled={busy} onClick={() => void fix([i.n])}>{how === 'enlarge' ? 'Enlarge opening' : 'Make opening'}</button>}
                                      {i.state === 'open' && <button type="button" className="link" data-clash-ignore={i.n} onClick={() => ignore([i.n], 'ignored')}>Ignore</button>}
                                      {i.state === 'ignored' && <button type="button" className="link" onClick={() => ignore([i.n], 'open')}>Reopen</button>}
                                    </td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                        </div>
                      ))}
                    </div>
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
          {tab === 'settings' && (
            <dl className="fields coordination-settings" data-testid="clash-settings">
              <dt>Voids for</dt>
              <dd className="coordination-kinds">
                {VOID_KINDS.map((k) => (
                  <label key={k} className="check-line"><input type="checkbox" data-void-kind={k} style={{ width: 'auto', marginRight: 6 }} checked={settings.kinds.includes(k)} onChange={() => setSettings({ ...settings, kinds: toggleKind(settings.kinds, k) })} /> <span>{KIND_LABELS[k]}</span></label>
                ))}
              </dd>
              <dt title="Each void's sides moved out by this much (negative: in), in the 3D view and the exports">Void offset (mm)</dt>
              <dd><input data-testid="void-offset" inputMode="decimal" value={offset} onChange={(e) => setOffset(e.target.value)} onBlur={() => void keep()} /></dd>
              <dt title="How far an MEP element must stay from a void's sides">Clearance (mm)</dt>
              <dd><input data-testid="void-clearance" inputMode="decimal" value={clearance} onChange={(e) => setClearance(e.target.value)} onBlur={() => void keep()} /></dd>
              <dt>Empty voids</dt>
              <dd><label><input type="checkbox" data-testid="void-empty" style={{ width: 'auto', marginRight: 6 }} checked={settings.empty} onChange={(e) => setSettings({ ...settings, empty: e.target.checked })} /> report wall and floor openings nothing goes through</label></dd>
              <dt>3D view</dt>
              <dd><label><input type="checkbox" data-testid="void-show" style={{ width: 'auto', marginRight: 6 }} checked={voidsOn} onChange={(e) => { void keep(); useUi.setState({ openingVoids: e.target.checked }); }} /> show the voids (View → Opening voids)</label></dd>
              <dt>Cut order</dt>
              <dd><button type="button" className="link" data-testid="clash-cut-order" onClick={() => useUi.setState({ dialog: 'cutOrder' })}>Runs that cut walls and floors (Manage → Cut order)…</button></dd>
            </dl>
          )}
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn primary" data-testid="clash-run" disabled={busy || !valid} onClick={() => void run()}><Play size={13} /> Run clash check</button>
          <button type="button" className="btn" data-testid="clash-marks" onClick={() => void marks()} title="The view in front marks the open clashes with red clouds and their numbers, on the screen only (View → Clash marks)"><Cloud size={13} /> Mark in view</button>
          <button type="button" className="btn" data-testid="clash-bcf" disabled={busy || !report || open === 0} onClick={() => void bcf()} title="Each open clash a remark of type Clash with its viewpoint, sent as a BCF file (run again: the same remarks updated)"><MessageSquareText size={13} /> BCF</button>
          <button type="button" className="btn" data-testid="clash-csv" disabled={!report} onClick={() => void file('csv')}><Download size={13} /> CSV</button>
          <button type="button" className="btn" data-testid="clash-pdf" disabled={!report} onClick={() => void file('pdf')}><FileText size={13} /> PDF</button>
          <button type="button" className="btn" onClick={() => { void keep(); useUi.setState({ openingVoids: true, view: '3d', dialog: null }); }}><Eye size={13} /> Voids in 3D</button>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}
