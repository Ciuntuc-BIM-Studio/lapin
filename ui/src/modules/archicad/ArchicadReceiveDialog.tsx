/**
 * File → Collaborate → Archicad: Receive… (docs/plan-archicad.md, A1): the elements of a running Archicad (Tapir) into the project.
 * The running Archicads are found by the engine; the survey says what a receive would do before it does it; Receive is one undo
 * step. Elements Lapin rebuilds within 1 mm or 0.5 % of Archicad's own body come in native, the rest as references; the report
 * says how many of each and why the rest stayed references.
 */

import { Fragment, useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi, notify } from '../../app/store';
import { backdropClose } from '../../shell/Dialog';
import { engine } from '../../engine/session';
import { begin, end } from '../../engine/ifcImport';
import { t } from '../../i18n';
import {
  countRows, DEFAULT_OPTIONS, GUIDE_URL, instanceLabel, instances, keptRows, lastPort, pickInstance, receive, receivedSummary, reportRows, survey,
  type ArchicadInstance, type ArchicadReceived, type ArchicadSurvey, type ReceiveOptions,
} from './archicad';

const messageOf = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function ArchicadReceiveDialog() {
  const close = () => useUi.setState({ dialog: null });
  const [list, setList] = useState<ArchicadInstance[] | null>(null);
  const [port, setPort] = useState<number | null>(null);
  const [looked, setLooked] = useState<ArchicadSurvey | null>(null);
  const [options, setOptions] = useState<ReceiveOptions>(DEFAULT_OPTIONS);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [received, setReceived] = useState<ArchicadReceived | null>(null);

  const find = () => {
    setList(null); setError(null); setLooked(null);
    instances().then((l) => { setList(l); setPort(pickInstance(l, lastPort())?.port ?? null); }).catch((e) => { setList([]); setError(messageOf(e)); });
  };
  useEffect(find, []);

  const chosen = list?.find((i) => i.port === port) ?? null;

  useEffect(() => {
    setLooked(null); setReceived(null);
    if (!chosen?.ok) return;
    let live = true;
    survey(chosen.port).then((s) => { if (live) setLooked(s); }).catch((e) => { if (live) setError(messageOf(e)); });
    return () => { live = false; };
  }, [chosen?.port, chosen?.ok]);

  const set = <K extends keyof ReceiveOptions>(k: K, v: ReceiveOptions[K]) => setOptions((o) => ({ ...o, [k]: v }));

  const doReceive = async () => {
    if (!chosen) return;
    setBusy(true); setError(null); setReceived(null);
    begin('Receiving from Archicad', 'Reading the model from Archicad', () => { void engine.call('cancel.long', {}).catch(() => undefined); });
    try {
      const r = await receive(chosen.port, { ...options, asCopy: options.asCopy && !!looked?.fromThisProject });
      setReceived(r);
      notify(receivedSummary(r));
    } catch (e) { setError(messageOf(e)); } finally { end(); setBusy(false); }
  };

  const kept = received ? keptRows(received) : [];
  const skipped = received ? countRows(received.skipped) : [];

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Receive from Archicad" data-testid="archicad-receive" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Receive from Archicad</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {list === null ? <p className="muted">Looking for Archicad…</p> : list.length === 0 ? (
            <div data-testid="archicad-none">
              <p>No Archicad found.</p>
              <p className="muted small">Open Archicad with the project, with the Tapir add-on installed (version 2.0 or newer). Lapin looks on ports 19723 to 19744.</p>
              <p className="small"><a href={GUIDE_URL} target="_blank" rel="noreferrer">How to set up the Archicad connector</a> <button type="button" className="btn" onClick={find}>Look again</button></p>
            </div>
          ) : (
            <>
              <dl className="fields">
                <dt>Archicad</dt>
                <dd><select className="archicad-instance" value={port ?? ''} onChange={(e) => setPort(Number(e.target.value))}>
                  {list.map((i) => <option key={i.port} value={i.port} data-no-i18n>{instanceLabel(i, t('Untitled project'))}</option>)}
                </select></dd>
              </dl>
              {chosen && !chosen.ok && <p className="error small" data-testid="archicad-problem" data-no-i18n>{chosen.problem ?? 'This Archicad cannot be read.'}</p>}
              {chosen?.ok && !looked && !error && <p className="muted small">Reading the model…</p>}
              {looked && (
                <div className="archicad-survey" data-testid="archicad-survey">
                  <dl className="fields small">
                    <dt>Elements</dt><dd data-no-i18n>{looked.elements}</dd>
                    <dt>Storeys</dt><dd data-no-i18n>{looked.storeys.map((s) => s.name).join(', ') || '-'}</dd>
                    {countRows(looked.native).length > 0 && <><dt>Candidates for native elements</dt><dd data-no-i18n>{countRows(looked.native).map(([k, n]) => `${k} ${n}`).join(', ')}</dd></>}
                    <dt>As references</dt><dd data-no-i18n>{looked.references}</dd>
                    <dt>To add</dt><dd data-no-i18n>{looked.adds}</dd>
                    <dt>To update</dt><dd data-no-i18n>{looked.updates}</dd>
                    <dt>To remove</dt><dd data-no-i18n>{looked.removes}</dd>
                  </dl>
                  {countRows(looked.byType).length > 0 && <p className="muted small">By type: <span data-no-i18n>{countRows(looked.byType).map(([k, n]) => `${k} ${n}`).join(', ')}</span></p>}
                  {countRows(looked.skipped).length > 0 && <p className="muted small">Left out: <span data-no-i18n>{countRows(looked.skipped).map(([k, n]) => `${k} ${n}`).join(', ')}</span></p>}
                  {looked.notes.map((n) => <p key={n} className="muted small" data-no-i18n>{n}</p>)}
                </div>
              )}
              <dl className="fields">
                <dt title="Archicad's stories become Lapin's storeys; the ones the project lacks are made from them, or the elements go on the project's storeys alone">Storeys</dt>
                <dd><select className="archicad-storeys" value={options.storeys} onChange={(e) => set('storeys', e.target.value as ReceiveOptions['storeys'])}>
                  <option value="create">Make the missing storeys</option>
                  <option value="existing">Only the project's storeys</option>
                </select></dd>
                <dt title="Where the coordinates start">Origin</dt>
                <dd><select className="archicad-origin" value={options.origin} onChange={(e) => set('origin', e.target.value as ReceiveOptions['origin'])}>
                  <option value="project">Archicad's project origin</option>
                  <option value="survey">Archicad's survey point</option>
                </select></dd>
                <dt>Recognition</dt>
                <dd><label><input type="checkbox" className="archicad-recognize" checked={options.recognize} onChange={(e) => set('recognize', e.target.checked)} /> <span>Recognise elements</span></label></dd>
                {looked?.fromThisProject && <><dt>Copy</dt><dd><label><input type="checkbox" className="archicad-copy" checked={options.asCopy} onChange={(e) => set('asCopy', e.target.checked)} /> <span>Receive as a copy</span></label></dd></>}
              </dl>
              {options.recognize && <p className="muted small">Walls, slabs, columns, beams, openings and zones become native Lapin elements where they match Archicad's own 3D body within 1 mm or 0.5 %; the rest come in as references.</p>}
              <p className="muted small">Receiving again updates the elements in place, keeping their GlobalIds, and removes what is gone from Archicad. One undo step.</p>
              <p className="muted small">To keep the received model on the server: receive it, then File → Collaborate → Save to server.</p>
              {received && (
                <div className="archicad-received" data-testid="archicad-received">
                  <p>{receivedSummary(received)}</p>
                  <dl className="fields small">
                    {reportRows(received).map((r) => <Fragment key={r.label}><dt>{r.label}</dt><dd data-no-i18n>{r.value}</dd></Fragment>)}
                  </dl>
                  {kept.length > 0 && <p className="muted small archicad-kept">Stayed references: <span data-no-i18n>{kept.map(([k, n]) => `${k} (${n})`).join('; ')}</span></p>}
                  {skipped.length > 0 && <p className="muted small">Left out: <span data-no-i18n>{skipped.map(([k, n]) => `${k} ${n}`).join(', ')}</span></p>}
                  {received.notes.map((n) => <p key={n} className="muted small" data-no-i18n>{n}</p>)}
                </div>
              )}
            </>
          )}
          {error && <p className="error small" data-testid="archicad-error" data-no-i18n>{error}</p>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>{received ? 'Close' : 'Cancel'}</button>
          <button type="button" className="btn primary" data-action="archicad-receive" disabled={busy || !chosen?.ok} onClick={() => void doReceive()}>{busy ? 'Receiving…' : 'Receive'}</button>
        </footer>
      </div>
    </div>
  );
}
