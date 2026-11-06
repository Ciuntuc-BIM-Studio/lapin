/**
 * File → Export JG Structures (JSON)… (docs/jg-structures.md): the analytical model of the project — columns, beams and
 * steel members as frames, load-bearing walls and floors as shells on shared nodes, the footings' supports — in the exchange
 * file of JG Structures, a structural analysis program (contract JG_Structures_v2_exchange 1.0.0). The storeys and kinds
 * chosen, the units (the contract's cm and kgf, or SI), every wall or only the load-bearing ones, how the feet on footings are
 * held (fixed by default, or pinned); the engine's preview
 * counts what goes in and checks it against the contract's rules before it is written, and lists what analysis still needs.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { engine, query, useSession } from '../../engine/session';
import { projectTitle, saveFile } from '../../engine/files';
import { LONG_TIMEOUT_MS } from '../../engine/client';

interface JgSurvey { storeys: { id: string; name: string; elevation: number }[]; kinds: Record<string, number>; footings?: 'fixed' | 'pinned' }
interface JgMissing { entity: string; id: string; name: string; fields: string[] }
interface JgReport {
  units: string;
  counts: Record<string, number>;
  errors: string[]; warnings: string[]; missing: JgMissing[];
  leftOut: { id: string; name: string; ifcClass: string; reason: string }[];
  ready: boolean;
}

const KINDS: [string, string][] = [['columns', 'Columns'], ['beams', 'Beams'], ['members', 'Steel members'], ['walls', 'Walls'], ['slabs', 'Floors'], ['roofs', 'Roofs (decks, timber framing)'], ['supports', 'Supports (footings, column bases)']];
const COUNTS: [string, string][] = [['nodes', 'Nodes'], ['frames', 'Frame elements'], ['shells', 'Shell elements'], ['supports', 'Supported nodes'], ['sections', 'Sections'], ['materials', 'Materials'], ['stories', 'Storeys'], ['grids', 'Grids']];

export function JgExportDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const [survey, setSurvey] = useState<JgSurvey | null>(null);
  const [offStoreys, setOffStoreys] = useState<Set<string>>(new Set());
  const [offKinds, setOffKinds] = useState<Set<string>>(new Set());
  const [units, setUnits] = useState<'contract' | 'si'>('contract');
  const [allWalls, setAllWalls] = useState(false);
  const [footings, setFootings] = useState<'fixed' | 'pinned'>('fixed');
  const [report, setReport] = useState<JgReport | null>(null);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { void query<JgSurvey>('jg.survey').then((s) => { setSurvey(s); if (s?.footings) setFootings(s.footings); }); }, []);
  const args = useMemo(() => ({
    units,
    ...(allWalls ? { allWalls: true } : {}),
    ...(footings === 'pinned' ? { footings } : {}),
    ...(survey && offStoreys.size ? { storeys: survey.storeys.filter((s) => !offStoreys.has(s.id)).map((s) => s.id) } : {}),
    ...(offKinds.size ? { kinds: KINDS.map(([k]) => k).filter((k) => !offKinds.has(k)) } : {}),
  }), [units, allWalls, footings, survey, offStoreys, offKinds]);
  useEffect(() => {
    if (!survey) return;
    let live = true;
    void query<JgReport>('jg.preview', args).then((r) => { if (live) setReport(r); });
    return () => { live = false; };
  }, [args, survey]);
  const toggle = (set: Set<string>, key: string, put: (s: Set<string>) => void) => {
    const next = new Set(set);
    if (next.has(key)) next.delete(key); else next.add(key);
    put(next);
  };
  const run = async () => {
    setBusy(true);
    try {
      const r = await engine.call<{ name: string; data: string; report: JgReport }>('doc.query', { name: 'jg.export', args }, { timeoutMs: LONG_TIMEOUT_MS });
      const bytes = Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0));
      const saved = await saveFile(bytes, `${projectTitle(path)}.jg.json`, 'application/json');
      if (saved) { notify(`Exported ${saved}.`); close(); }
    } catch (e) {
      notify(e instanceof Error ? e.message : String(e));
    }
    setBusy(false);
  };
  const empty = !report || (report.counts.frames ?? 0) + (report.counts.shells ?? 0) === 0;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog import-ifc jg-export" role="dialog" aria-label="Export JG Structures" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title">Export JG Structures</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body exchange-options">
          <p className="muted small">The analytical model for structural analysis: frames on the members' axes, shells on the cores of load-bearing walls and floors, on shared nodes.</p>
        </div>
        <div className="dialog-body split">
          <div>
            <strong>Storeys</strong>
            {survey ? survey.storeys.map((s) => (
              <label key={s.id} className="check-line"><input type="checkbox" checked={!offStoreys.has(s.id)} onChange={() => toggle(offStoreys, s.id, setOffStoreys)} /> {s.name}</label>
            )) : <p className="muted">Reading the project…</p>}
          </div>
          <div>
            <strong>What to export</strong>
            {KINDS.map(([k, label]) => (
              <label key={k} className="check-line" data-testid={`jg-kind-${k}`}>
                <input type="checkbox" checked={!offKinds.has(k)} onChange={() => toggle(offKinds, k, setOffKinds)} /> {label} <span className="muted small">{survey?.kinds[k] ?? 0}</span>
              </label>
            ))}
          </div>
        </div>
        <div className="dialog-body exchange-options">
          <dl className="fields">
            <dt title="The contract declares its units once: cm and kgf; SI is Lapin's proposal for the contract's next version">Units</dt>
            <dd><select value={units} onChange={(e) => setUnits(e.target.value as 'contract' | 'si')}>
              <option value="contract">cm, kgf (the contract's)</option>
              <option value="si">m, kN (SI)</option>
            </select></dd>
            <dt title="Pset_WallCommon.LoadBearing on the wall or its type">Walls</dt>
            <dd><label><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={allWalls} onChange={(e) => setAllWalls(e.target.checked)} /> every wall, not only the load-bearing ones</label></dd>
            <dt title="How the column and wall feet standing on a footing are held; a steel column base keeps its connection's own">Feet on footings</dt>
            <dd><select data-testid="jg-footings" value={footings} onChange={(e) => setFootings(e.target.value as 'fixed' | 'pinned')}>
              <option value="fixed">fixed (the default)</option>
              <option value="pinned">pinned</option>
            </select></dd>
          </dl>
        </div>
        <div className="dialog-body jg-report" data-testid="jg-report">
          {!report ? <p className="muted">Checking…</p> : (
            <>
              <div className="jg-counts">
                {COUNTS.map(([k, label]) => <span key={k} className="jg-count"><strong>{report.counts[k] ?? 0}</strong> <span className="muted small">{label}</span></span>)}
              </div>
              <p className={report.errors.length ? 'jg-bad' : report.ready ? 'jg-good' : 'jg-warn'}>
                {report.errors.length ? 'The file does not pass the contract’s checks.' : report.ready ? 'Valid, and every value a linear analysis needs is there.' : 'Valid. Some values analysis needs are missing (null in the file):'}
              </p>
              {report.errors.length > 0 && <ul className="jg-list jg-bad">{report.errors.slice(0, 12).map((e) => <li key={e}>{e}</li>)}</ul>}
              {report.missing.length > 0 && (
                <ul className="jg-list">{report.missing.slice(0, 12).map((m) => (
                  <li key={`${m.entity}:${m.id}`} data-entity={m.entity}><strong data-no-i18n>{m.name}</strong> <code>{m.fields.join(', ')}</code></li>
                ))}</ul>
              )}
              {report.warnings.length > 0 && (
                <>
                  <p className="muted small">Warnings</p>
                  <ul className="jg-list">{report.warnings.slice(0, 8).map((w) => <li key={w}>{w}</li>)}</ul>
                </>
              )}
              {report.leftOut.length > 0 && <p className="muted small" title={report.leftOut.slice(0, 20).map((x) => `${x.name}: ${x.reason}`).join('\n')}>Left out: {report.leftOut.length}</p>}
            </>
          )}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy || !report || report.errors.length > 0 || empty} onClick={() => void run()}>{busy ? 'Exporting…' : 'Export'}</button>
        </footer>
      </div>
    </div>
  );
}
