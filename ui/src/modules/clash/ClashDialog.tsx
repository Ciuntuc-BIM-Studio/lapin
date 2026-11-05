/**
 * Manage → Clash detection (docs/plan-visualisation.md, stage 2): the project's clash tests on the left (on for Run all,
 * their counts, out of date once the model changed), and for the test picked its clashes, its settings, or the search
 * sets. Run runs the tests asked for once and keeps what they found; a clash clicked is shown in 3D (the pair selected,
 * a section box round where they meet, the camera looking across), given a status, someone who has it and notes, and
 * sent out as BCF remarks or a CSV list.
 */

import { useEffect, useMemo, useState } from 'react';
import { ChevronLeft, ChevronRight, Download, MessageSquareText, Play, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { saveFile } from '../../engine/files';
import { useCamera } from '../../viewport/camera';
import { useSectionBox } from '../../viewport/sectionBox';
import { isolateIds, resetTemporary } from '../../viewport/temporary';
import { Cell } from '../../shell/cells';
import { author, exportBcf } from '../core/remarks/remarks';
import {
  clashCsv, clashRemarks, deleteSet, deleteTest, FIELD_LABELS, framing, isList, loadClashes, loadMembers, loadResults, OP_LABELS, runClashes, saveSet, saveTest,
  seedClashes, setStatus, STATUS_LABELS, STATUSES, valuesOf, valuesText,
  type Clash, type ClashList, type ClashStatus, type ClashTest, type RuleField, type RuleOp, type SearchSet, type SetRule,
} from './clash';

/** A clash in the 3D view: the pair selected (alone when asked), a section box round where they meet, the camera looking at it. */
async function showClash(c: Clash, alone: boolean) {
  const ids = [c.a, c.b];
  useUi.setState({ view: '3d', selected: ids, selection: ids[0] });
  if (alone) isolateIds(ids);
  for (let k = 0; k < 40; k++) {
    const api = useCamera.getState().api;
    if (api) {
      const box = (id: string) => { const b = api.boxOf?.([id]); return b ? { min: [b.min.x, b.min.y, b.min.z] as [number, number, number], max: [b.max.x, b.max.y, b.max.z] as [number, number, number] } : null; };
      const f = framing(c.at, box(c.a), box(c.b), useCamera.getState().azimuth ?? 0);
      useSectionBox.setState({ box: f.box });
      api.orbitTo({ target: f.target, azimuth: f.azimuth, pitch: f.pitch, distance: f.distance });
      return;
    }
    await new Promise((res) => setTimeout(res, 50));
  }
}

const mm = (m: number) => `${Math.round(m * 1000)}`;
const day = (iso?: string) => (iso ? new Date(iso).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' }) : '');

export function ClashDialog() {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<ClashList | null>(null);
  const [testId, setTestId] = useState<string | null>(null);
  const [tab, setTab] = useState<'clashes' | 'test' | 'sets'>('clashes');
  const [clashes, setClashes] = useState<Clash[]>([]);
  const [showResolved, setShowResolved] = useState(false);
  const [current, setCurrent] = useState<number | null>(null);
  const [picked, setPicked] = useState<Set<number>>(new Set());
  const [alone, setAlone] = useState(false);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });

  // The tests and sets, read again as the model changes; a project with none gets the starting ones (one undo step).
  useEffect(() => {
    void loadClashes().then(async (l) => {
      if (l && l.tests.length === 0 && l.sets.length === 0 && version >= 0) { await seedClashes(); return; }
      setList(l);
      if (l && !l.tests.some((t) => t.id === testId)) setTestId(l.tests[0]?.id ?? null);
    });
  }, [version]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { if (testId) void loadResults(testId).then((r) => setClashes(r?.clashes ?? [])); else setClashes([]); }, [testId, version]);

  const test = list?.tests.find((t) => t.id === testId) ?? null;
  const shown = useMemo(() => clashes.filter((c) => showResolved || c.status !== 'resolved'), [clashes, showResolved]);
  const go = (n: number | null) => {
    if (n == null) return;
    setCurrent(n);
    const c = clashes.find((x) => x.n === n);
    if (c) void showClash(c, alone);
  };
  const stepTo = (dir: 1 | -1) => {
    if (!shown.length) return;
    const at = shown.findIndex((c) => c.n === current);
    go(shown[(at < 0 ? (dir > 0 ? 0 : shown.length - 1) : (at + dir + shown.length) % shown.length)].n);
  };
  const run = async (ids?: string[]) => {
    setBusy(true);
    const r = await runClashes(ids);
    setBusy(false);
    if (r) { const sum = (k: 'found' | 'new' | 'resolved') => r.reduce((n, x) => n + x[k], 0); notify(`${sum('found')} clashes found, ${sum('new')} new, ${sum('resolved')} resolved.`); }
  };
  const bcf = async () => {
    if (!test) return;
    setBusy(true);
    const r = await clashRemarks(test.id, picked.size ? [...picked] : undefined, author());
    setBusy(false);
    if (r?.ids.length) await exportBcf({ ids: r.ids });
    else notify('No open clash to send.');
  };
  const csv = async () => {
    if (!test) return;
    await saveFile(new TextEncoder().encode(clashCsv(test, clashes)), `${test.name}.csv`, 'text/csv');
  };
  const newTest = async () => {
    const a = list?.sets[0]?.id;
    if (!a) { notify('Make a search set first.'); setTab('sets'); return; }
    const r = await saveTest({ name: 'New test', setA: a, kind: 'hard', tolerance: 0.005 });
    if (r) { setTestId(r.id); setTab('test'); }
  };
  const status = (numbers: number[], s: ClashStatus) => { if (test && numbers.length) void setStatus(test.id, numbers, { status: s }); };

  return (
    <div className="dialog-backdrop clash-backdrop">
      <div className="dialog clash-dialog" role="dialog" aria-label="Clash detection" data-testid="clash-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Clash detection</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body clash-body">
          <section className="clash-main">
            <div className="row clash-test-row" data-testid="clash-tests">
              <select value={testId ?? ''} aria-label="Clash test" data-testid="clash-test" onChange={(e) => { setTestId(e.target.value); setCurrent(null); setPicked(new Set()); }}>
                {list?.tests.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
              <button type="button" className="btn small" data-testid="clash-new-test" onClick={() => void newTest()}><Plus size={12} /> New test</button>
            </div>
            {test && (
              <p className="small clash-counts">
                {test.lastRun ? <><span className="cde-block">{test.counts.new + test.counts.active}</span> <span>open</span> · <span>{test.counts.reviewed + test.counts.approved}</span> <span>reviewed</span> · <span className="cde-ok">{test.counts.resolved}</span> <span>resolved</span></> : <span className="muted">not run yet</span>}
              </p>
            )}
            <div className="row tabs-row">
              <button type="button" className={`btn${tab === 'clashes' ? ' active' : ''}`} data-clash-tab="clashes" onClick={() => setTab('clashes')}>Clashes</button>
              <button type="button" className={`btn${tab === 'test' ? ' active' : ''}`} data-clash-tab="test" onClick={() => setTab('test')}>Test</button>
              <button type="button" className={`btn${tab === 'sets' ? ' active' : ''}`} data-clash-tab="sets" onClick={() => setTab('sets')}>Search sets</button>
            </div>
            {tab === 'clashes' && test && (
              <ClashList test={test} clashes={shown} current={current} picked={picked} setPicked={setPicked} go={go} status={status}
                header={(
                  <div className="row coordination-tools">
                    <button type="button" className="btn small" disabled={!shown.length} onClick={() => stepTo(-1)} title="Previous clash"><ChevronLeft size={13} /></button>
                    <button type="button" className="btn small" data-testid="clash-next" disabled={!shown.length} onClick={() => stepTo(1)} title="Next clash"><ChevronRight size={13} /></button>
                    <label className="check-line"><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={alone} onChange={(e) => { setAlone(e.target.checked); if (!e.target.checked) resetTemporary(); }} /> <span>alone in 3D</span></label>
                    <label className="check-line"><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={showResolved} onChange={(e) => setShowResolved(e.target.checked)} /> <span>show resolved</span></label>
                    <span className="grow" />
                    {picked.size > 0 && <select value="" onChange={(e) => { status([...picked], e.target.value as ClashStatus); setPicked(new Set()); }} aria-label="Status of the selected clashes">
                      <option value="" disabled>Status…</option>
                      {STATUSES.map((s) => <option key={s} value={s}>{STATUS_LABELS[s]}</option>)}
                    </select>}
                  </div>
                )} />
            )}
            {tab === 'clashes' && !test && <p className="muted">No clash test yet.</p>}
            {tab === 'test' && test && list && <TestForm key={test.id} test={test} sets={list.sets} onDelete={() => { void deleteTest(test.id); setTestId(null); }} />}
            {tab === 'sets' && list && <SetsEditor sets={list.sets} />}
          </section>
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn primary" data-testid="clash-run-test" disabled={busy || !test} onClick={() => test && void run([test.id])}><Play size={13} /> Run test</button>
          <button type="button" className="btn" data-testid="clash-run-all" disabled={busy || !list?.tests.some((t) => t.enabled)} onClick={() => void run()}><Play size={13} /> Run all</button>
          <button type="button" className="btn" data-testid="clash-bcf" disabled={busy || !test || !clashes.length} onClick={() => void bcf()} title="Each open clash (or those selected) a remark of type Clash with its viewpoint, sent as a BCF file; sent again: the same remarks updated"><MessageSquareText size={13} /> BCF</button>
          <button type="button" className="btn" disabled={!test || !clashes.length} onClick={() => void csv()}><Download size={13} /> CSV</button>
          {busy && <span className="muted">Testing…</span>}
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Done</button>
        </footer>
      </div>
    </div>
  );
}

function ClashList({ test, clashes, current, picked, setPicked, go, status, header }: {
  test: ClashTest; clashes: Clash[]; current: number | null; picked: Set<number>; setPicked: (s: Set<number>) => void;
  go: (n: number) => void; status: (numbers: number[], s: ClashStatus) => void; header: React.ReactNode;
}) {
  return (
    <div data-testid="clash-results">
      {test.lastRun && <p className="small muted">{`Run on ${day(test.lastRun.date)} · ${test.lastRun.countA} × ${test.setB ? test.lastRun.countB : 'itself'} elements · ${test.lastRun.seconds} s`}</p>}
      {test.lastRun?.truncated && <p className="error-text small">Stopped at the limit of 10,000 clashes: tighten the sets or the tolerance.</p>}
      {test.stale && <p className="error-text" role="status">Out of date: the model has changed since this run. Run the test again.</p>}
      {header}
      {!test.lastRun ? <p className="muted">Run test runs it once and keeps what it finds here.</p> : clashes.length === 0 ? <p className="cde-ok">No clash.</p> : (
        <div className="coordination-results">
          <table className="schedule-table coordination-table">
            <thead><tr><th /><th>#</th><th>Status</th><th>Element A</th><th>Element B</th><th>{test.kind === 'hard' ? 'Depth (mm)' : 'Distance (mm)'}</th><th>Storey</th><th>Assigned to</th><th>Notes</th></tr></thead>
            <tbody>
              {clashes.map((c) => (
                <tr key={c.key} data-clash-n={c.n} data-clash-status={c.status} className={current === c.n ? 'current' : undefined} onClick={() => go(c.n)}>
                  <td onClick={(e) => e.stopPropagation()}><input type="checkbox" style={{ width: 'auto' }} checked={picked.has(c.n)} onChange={(e) => { const s = new Set(picked); if (e.target.checked) s.add(c.n); else s.delete(c.n); setPicked(s); }} /></td>
                  <td className="num"><b>{c.n}</b></td>
                  <td onClick={(e) => e.stopPropagation()}>
                    <select value={c.status} aria-label="Status" className={c.status === 'new' || c.status === 'active' ? 'cde-block' : c.status === 'resolved' ? 'cde-ok' : undefined} onChange={(e) => status([c.n], e.target.value as ClashStatus)}>
                      {STATUSES.map((s) => <option key={s} value={s}>{STATUS_LABELS[s]}</option>)}
                    </select>
                  </td>
                  <td title={c.aClass ?? ''}>{c.aName || c.aClass}{c.aLink ? <span className="muted small"> · {c.aLink}</span> : null}</td>
                  <td title={c.bClass ?? ''}>{c.bName || c.bClass}{c.bLink ? <span className="muted small"> · {c.bLink}</span> : null}</td>
                  <td className="num">{c.crossing || test.kind === 'hard' ? mm(c.d) : `${mm(c.d)}`}</td>
                  <td>{c.aStorey ?? c.bStorey ?? ''}</td>
                  <td onClick={(e) => e.stopPropagation()}><Cell value={c.assignedTo ?? ''} placeholder="—" onCommit={(t) => { if (t.trim() !== (c.assignedTo ?? '')) void setStatus(test.id, [c.n], { assignedTo: t }); }} /></td>
                  <td onClick={(e) => e.stopPropagation()}><Cell value={c.notes ?? ''} placeholder="—" onCommit={(t) => { if (t.trim() !== (c.notes ?? '')) void setStatus(test.id, [c.n], { notes: t }); }} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function TestForm({ test, sets, onDelete }: { test: ClashTest; sets: SearchSet[]; onDelete: () => void }) {
  const [tolerance, setTolerance] = useState(mm(test.tolerance));
  const keepTolerance = () => {
    const v = Number(tolerance.replace(',', '.'));
    if (Number.isFinite(v) && v >= 0 && v <= 2000 && Math.abs(v / 1000 - test.tolerance) > 1e-9) void saveTest({ id: test.id, tolerance: v / 1000 });
  };
  return (
    <dl className="fields clash-test-form" data-testid="clash-test-form">
      <dt>Name</dt><dd><Cell value={test.name} onCommit={(t) => { if (t.trim() && t !== test.name) void saveTest({ id: test.id, name: t.trim() }); }} /></dd>
      <dt>Set A</dt>
      <dd><select value={test.setA} onChange={(e) => void saveTest({ id: test.id, setA: e.target.value })}>{sets.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></dd>
      <dt>Set B</dt>
      <dd><select value={test.setB ?? ''} onChange={(e) => void saveTest({ id: test.id, setB: e.target.value || null })}>
        <option value="">Set A against itself</option>
        {sets.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
      </select></dd>
      <dt>Kind</dt>
      <dd><select value={test.kind} onChange={(e) => { const kind = e.target.value as ClashTest['kind']; void saveTest({ id: test.id, kind, tolerance: kind === 'hard' ? 0.005 : 0.05 }); setTolerance(kind === 'hard' ? '5' : '50'); }}>
        <option value="hard">Hard: elements crossing</option>
        <option value="clearance">Clearance: closer than a distance</option>
      </select></dd>
      <dt title={test.kind === 'hard' ? 'A crossing this deep or less is not a clash' : 'Elements closer than this clash'}>{test.kind === 'hard' ? 'Tolerance (mm)' : 'Distance (mm)'}</dt>
      <dd><input inputMode="decimal" value={tolerance} onChange={(e) => setTolerance(e.target.value)} onBlur={keepTolerance} /></dd>
      <dt>Run all</dt>
      <dd><label><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={test.enabled} onChange={(e) => void saveTest({ id: test.id, enabled: e.target.checked })} /> takes this test</label></dd>
      <dt />
      <dd><p className="muted small">Changing the sets or the kind starts the test's clashes afresh. Never tested: an element and what fills its openings, the parts of one assembly, elements joined or attached to each other.</p>
        <button type="button" className="btn small" onClick={onDelete}><Trash2 size={12} /> Delete test</button></dd>
    </dl>
  );
}

const FIELDS: RuleField[] = ['class', 'predefinedType', 'type', 'storey', 'material', 'name', 'link', 'parameter'];
const OPS: RuleOp[] = ['is', 'isOneOf', 'isNot', 'isNotAnyOf', 'contains', 'notContains', 'atLeast', 'atMost', 'between'];

function SetsEditor({ sets }: { sets: SearchSet[] }) {
  const [id, setId] = useState<string | null>(sets[0]?.id ?? null);
  const set = sets.find((s) => s.id === id) ?? null;
  const add = async () => {
    const r = await saveSet({ name: 'New set', matchAll: false, rules: [{ field: 'class', op: 'is', values: [''] }], groups: [] });
    if (r) setId(r.id);
  };
  return (
    <div className="clash-sets" data-testid="clash-sets">
      <div className="row">
        <select value={id ?? ''} onChange={(e) => setId(e.target.value)} aria-label="Search set">
          {sets.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.count ?? 0})</option>)}
        </select>
        <button type="button" className="btn small" data-testid="clash-new-set" onClick={() => void add()}><Plus size={12} /> New set</button>
        {set && <button type="button" className="btn small" onClick={() => void deleteSet(set.id)}><Trash2 size={12} /> Delete</button>}
      </div>
      {set && <SetForm key={set.id + JSON.stringify(set.rules)} set={set} />}
    </div>
  );
}

function SetForm({ set }: { set: SearchSet }) {
  const [rules, setRules] = useState<SetRule[]>(set.rules);
  const [texts, setTexts] = useState<string[]>(set.rules.map(valuesText));
  const [count, setCount] = useState<number | null>(set.count ?? null);
  const draft = (r = rules, t = texts) => r.map((x, i) => ({ ...x, values: valuesOf(t[i] ?? '', x.op) }));
  // How many elements the rules take as they are typed.
  useEffect(() => {
    const t = setTimeout(() => void loadMembers({ matchAll: set.matchAll, rules: draft(), groups: set.groups }).then((m) => setCount(m?.count ?? null)), 300);
    return () => clearTimeout(t);
  }, [rules, texts]); // eslint-disable-line react-hooks/exhaustive-deps
  const save = (r = rules, t = texts, matchAll = set.matchAll) => void saveSet({ id: set.id, name: set.name, matchAll, rules: draft(r, t), groups: set.groups });
  const patch = (i: number, p: Partial<SetRule>) => { const r = rules.map((x, k) => (k === i ? { ...x, ...p } : x)); setRules(r); save(r); };
  return (
    <div className="clash-set-form">
      <dl className="fields">
        <dt>Name</dt><dd><Cell value={set.name} onCommit={(t) => { if (t.trim() && t !== set.name) void saveSet({ ...set, name: t.trim() }); }} /></dd>
        <dt>Takes</dt>
        <dd><select value={set.matchAll ? 'all' : 'any'} onChange={(e) => save(rules, texts, e.target.value === 'all')}>
          <option value="any">elements meeting any rule</option>
          <option value="all">elements meeting every rule</option>
        </select> <span className="muted small" data-testid="clash-set-count">{count == null ? '…' : `${count} elements`}</span></dd>
      </dl>
      <table className="schedule-table clash-rules">
        <thead><tr><th>Field</th><th /><th>Operator</th><th>Value</th><th /></tr></thead>
        <tbody>
          {rules.map((r, i) => (
            <tr key={i}>
              <td><select value={r.field} onChange={(e) => patch(i, { field: e.target.value as RuleField })}>{FIELDS.map((f) => <option key={f} value={f}>{FIELD_LABELS[f]}</option>)}</select></td>
              <td>{r.field === 'parameter' && <input placeholder="name" value={r.parameter ?? ''} onChange={(e) => setRules(rules.map((x, k) => (k === i ? { ...x, parameter: e.target.value } : x)))} onBlur={() => save()} />}</td>
              <td><select value={r.op} onChange={(e) => patch(i, { op: e.target.value as RuleOp })}>{OPS.map((o) => <option key={o} value={o}>{OP_LABELS[o]}</option>)}</select></td>
              <td><input value={texts[i] ?? ''} placeholder={isList(r.op) ? 'values, by commas' : 'value'} onChange={(e) => setTexts(texts.map((x, k) => (k === i ? e.target.value : x)))} onBlur={() => save()} /></td>
              <td><button type="button" className="icon" title="Remove the rule" onClick={() => { const r2 = rules.filter((_, k) => k !== i), t2 = texts.filter((_, k) => k !== i); setRules(r2); setTexts(t2); save(r2, t2); }}><Trash2 size={12} /></button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" className="btn small" onClick={() => { setRules([...rules, { field: 'class', op: 'is', values: [''] }]); setTexts([...texts, '']); }}><Plus size={12} /> Rule</button>
      {set.groups.length > 0 && <p className="muted small">{`${set.groups.length} group(s) of rules kept as they are.`}</p>}
      <p className="muted small">A class is its IFC name (IfcWall) or its label (Wall). A "not" rule takes elements out. With no rule adding elements, the set holds everything.</p>
    </div>
  );
}
