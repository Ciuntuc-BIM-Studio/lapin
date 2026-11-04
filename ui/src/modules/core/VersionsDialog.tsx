/**
 * Manage → Versions: the project's versions, kept in its file
 * (docs/versions.md). A version is the project as it was when saved as one,
 * numbered, with a name and a note. From here one is saved, renamed, brought
 * up to the project as it is now (what it held kept as an earlier revision),
 * restored (one undo step), compared with another or with the project as it
 * is, exported as a file of its own, opened in another Lapin window (the
 * desktop), or deleted. Every change here writes the project's file, as a
 * save does.
 */

import { useCallback, useEffect, useState } from 'react';
import { Download, ExternalLink, GitCompareArrows, RefreshCw, RotateCcw, Trash2, X } from 'lucide-react';
import { notify, select, useUi } from '../../app/store';
import { engine, useSession } from '../../engine/session';
import { exportVersion, isDesktop, listVersions, openVersionSeparately, projectTitle, writeVersions, type ProjectVersion } from '../../engine/files';
import { LONG_TIMEOUT_MS } from '../../engine/client';
import { formatSize, when } from './format';
import { Cell } from '../../shell/cells';

interface Change { id: string; class: string; name: string; fields?: string[] }
interface Comparison {
  a: string; b: string; same: boolean;
  entities: { added: Change[]; removed: Change[]; changed: Change[] };
  relations: { added: number; removed: number; changed: number };
  parts: { added: string[]; removed: string[]; changed: string[]; same: number; areas: Record<string, number> };
  /** What the modules say changed beside the elements (the work schedule: its finish, tasks moved, durations changed). */
  sections?: DiffSection[];
}
interface DiffSection {
  key: string; title: string;
  facts: { label: string; from: string | null; to: string | null }[];
  rows: { id: string | null; name: string; what: string; from: string | null; to: string | null; days: number | null }[];
}

/** How a module's row says what changed. */
const WHAT: Record<string, string> = { added: 'Added', removed: 'Removed', moved: 'Moved', duration: 'Duration changed', finish: 'Finish moved', progress: 'Progress' };

const CURRENT = 'current';
/** A version's time, kept in UTC, shown in the user's own. */

export function VersionsDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const nodes = useSession((s) => s.nodes);
  const [versions, setVersions] = useState<ProjectVersion[] | null>(null);
  const [name, setName] = useState('');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [sides, setSides] = useState<{ a: string; b: string }>({ a: '', b: CURRENT });
  const [comparison, setComparison] = useState<Comparison | null>(null);
  const close = () => useUi.setState({ dialog: null });

  const refresh = useCallback(async () => {
    const list = await listVersions();
    setVersions(list);
    // A side whose version went (deleted, or another project opened) falls back to the latest one; the other side to the project as it is.
    const known = (side: string) => side === CURRENT || list.some((v) => String(v.number) === side);
    setSides((s) => ({ a: s.a && known(s.a) ? s.a : (list.length ? String(list[list.length - 1].number) : ''), b: known(s.b) ? s.b : CURRENT }));
  }, []);
  useEffect(() => { void refresh(); }, [refresh, path]);

  const run = async (what: () => Promise<unknown>, done?: string) => {
    if (busy) return;
    setBusy(true);
    try {
      const r = await what();
      if (r !== false && done) notify(done);
      await refresh();
    } catch (e) {
      notify(e instanceof Error ? e.message : String(e));
    } finally { setBusy(false); }
  };

  const save = () => run(async () => {
    const ok = await writeVersions('project.saveVersion', { name: name.trim(), note: note.trim() });
    if (ok) { setName(''); setNote(''); }
    return ok;
  }, 'Version saved with the project.');

  const compare = () => run(async () => {
    const side = (v: string) => (v === CURRENT ? CURRENT : Number(v));
    const r = await engine.call<Comparison>('project.compareVersions', { a: side(sides.a), b: side(sides.b) }, { timeoutMs: LONG_TIMEOUT_MS });
    setComparison(r);
  });

  const pick = (id: string) => { if (nodes.has(id)) { select(id); } else notify('This element is not in the project as it is now.'); };
  const label = (v: string) => (v === CURRENT ? 'Current project' : `${v}. ${versions?.find((x) => String(x.number) === v)?.name ?? ''}`);

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Versions" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Versions · {projectTitle(path)}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <div className="version-save">
            <input placeholder={`Version ${(versions?.length ?? 0) + 1}`} value={name} onChange={(e) => setName(e.target.value)} aria-label="Name" />
            <input placeholder="Note (optional)" value={note} onChange={(e) => setNote(e.target.value)} aria-label="Note" onKeyDown={(e) => { if (e.key === 'Enter') void save(); }} />
            <button type="button" className="btn primary" disabled={busy} onClick={() => void save()} title="The project as it is now, saved as a new version of its file">Save version</button>
          </div>
          {versions === null && <p className="muted">Reading the versions…</p>}
          {versions?.length === 0 && <p className="muted">No versions yet. A version is the project as it is when you save it as one; it stays in the project's file, with the parts it shares with the project kept once.</p>}
          {versions && versions.length > 0 && (
            <table className="layer-table versions">
              <thead><tr><th>#</th><th>Name</th><th>Saved</th><th>Note</th><th title="Entities in the version">Elements</th><th>Size</th><th /></tr></thead>
              <tbody>
                {versions.map((v) => (
                  <VersionRows key={v.number} v={v} busy={busy} path={path} title={projectTitle(path)} nodesKnown={nodes.size > 0}
                    onRename={(n) => run(() => writeVersions('project.updateVersion', { number: v.number, name: n }))}
                    onNote={(n) => run(() => writeVersions('project.updateVersion', { number: v.number, note: n }))}
                    onRestore={(rev) => { if (window.confirm(`Bring the project back to ${rev == null ? `version ${v.number}` : `an earlier state of version ${v.number}`}? What differs changes in one step (undo brings it back).`)) void run(() => engine.call('project.restoreVersion', { number: v.number, ...(rev == null ? {} : { revision: rev }) }, { timeoutMs: LONG_TIMEOUT_MS }), `Back at version ${v.number}.`); }}
                    onCompare={() => { setSides({ a: String(v.number), b: CURRENT }); setComparison(null); void run(async () => setComparison(await engine.call<Comparison>('project.compareVersions', { a: v.number, b: CURRENT }, { timeoutMs: LONG_TIMEOUT_MS }))); }}
                    onUpdate={() => { if (window.confirm(`Make version ${v.number} the project as it is now? What it holds stays as an earlier revision of it.`)) void run(() => writeVersions('project.updateVersion', { number: v.number, replace: true }), `Version ${v.number} brought to the project as it is now.`); }}
                    onExport={(rev) => void exportVersion(v.number, rev, `${projectTitle(path)} v${v.number}${rev == null ? '' : `.${rev + 1}`}.lapin`)}
                    onOpen={(rev) => void run(() => openVersionSeparately(v.number, rev))}
                    onDelete={() => { if (window.confirm(`Delete version ${v.number}? The parts only it kept go from the file.`)) void run(() => writeVersions('project.deleteVersion', { number: v.number }), `Version ${v.number} deleted.`); }}
                  />
                ))}
              </tbody>
            </table>
          )}
          {versions && versions.length > 0 && (
            <div className="version-compare">
              <div className="version-save">
                <select value={sides.a} onChange={(e) => setSides({ ...sides, a: e.target.value })} aria-label="Compare from">
                  {versions.map((v) => <option key={v.number} value={String(v.number)}>{v.number}. {v.name}</option>)}
                  <option value={CURRENT}>Current project</option>
                </select>
                <select value={sides.b} onChange={(e) => setSides({ ...sides, b: e.target.value })} aria-label="Compare to">
                  {versions.map((v) => <option key={v.number} value={String(v.number)}>{v.number}. {v.name}</option>)}
                  <option value={CURRENT}>Current project</option>
                </select>
                <button type="button" disabled={busy || !sides.a} onClick={() => void compare()}><GitCompareArrows size={13} /> Compare</button>
              </div>
              {comparison && (
                <div className="version-diff">
                  <p className="muted small">
                    {label(sides.a)} → {label(sides.b)}: {comparison.same ? 'the same.' : `${comparison.entities.added.length} added, ${comparison.entities.removed.length} removed, ${comparison.entities.changed.length} changed; relationships ${comparison.relations.added}/${comparison.relations.removed}/${comparison.relations.changed}; parts changed ${Object.entries(comparison.parts.areas).map(([k, n]) => `${k} ${n}`).join(', ') || 'none'}, ${comparison.parts.same} the same.`}
                  </p>
                  {(comparison.sections ?? []).map((sec) => <SectionDiff key={sec.key} section={sec} onPick={pick} />)}
                  {(['added', 'removed', 'changed'] as const).map((kind) => comparison.entities[kind].length > 0 && (
                    <ChangeList key={kind} title={kind === 'added' ? 'Added' : kind === 'removed' ? 'Removed' : 'Changed'} changes={comparison.entities[kind]} onPick={pick} />
                  ))}
                </div>
              )}
            </div>
          )}
          <p className="muted small">A version lives in the project's file; saving, renaming or deleting one writes the file, as Save does. Restoring changes the project in one undo step.</p>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

function VersionRows({ v, busy, path, title, nodesKnown, onRename, onNote, onRestore, onCompare, onUpdate, onExport, onOpen, onDelete }: {
  v: ProjectVersion; busy: boolean; path: string | null; title: string; nodesKnown: boolean;
  onRename: (name: string) => void; onNote: (note: string) => void; onRestore: (revision: number | null) => void; onCompare: () => void;
  onUpdate: () => void; onExport: (revision: number | null) => void; onOpen: (revision: number | null) => void; onDelete: () => void;
}) {
  const desktop = isDesktop() && Boolean(path);
  return (
    <>
      <tr className={busy ? 'muted' : undefined}>
        <td className="num">{v.number}</td>
        <td><Cell value={v.name} onCommit={(t) => { const n = t.trim(); if (!n) return false; if (n !== v.name) onRename(n); }} /></td>
        <td title={v.updatedAt !== v.at ? `updated ${when(v.updatedAt)}` : undefined}>{when(v.at)}</td>
        <td><Cell value={v.note ?? ''} placeholder="—" onCommit={(t) => { const n = t.trim(); if (n !== (v.note ?? '')) onNote(n); }} /></td>
        <td className="num">{v.entities}</td>
        <td className="num">{formatSize(v.size)}</td>
        <td className="row-tools">
          <button type="button" className="icon mini" title="Restore: the project back to this version, in one undo step" disabled={busy || !nodesKnown} onClick={() => onRestore(null)}><RotateCcw size={12} /></button>
          <button type="button" className="icon mini" title="Compare with the project as it is now" disabled={busy} onClick={onCompare}><GitCompareArrows size={12} /></button>
          <button type="button" className="icon mini" title="Update to current: this version becomes the project as it is now; what it held stays as an earlier revision" disabled={busy} onClick={onUpdate}><RefreshCw size={12} /></button>
          <button type="button" className="icon mini" title="Export as a .lapin file of its own" disabled={busy} onClick={() => onExport(null)}><Download size={12} /></button>
          {desktop && <button type="button" className="icon mini" title="Open separately, in another Lapin window" disabled={busy} onClick={() => onOpen(null)}><ExternalLink size={12} /></button>}
          <button type="button" className="icon mini" title="Delete this version" disabled={busy} onClick={onDelete}><Trash2 size={12} /></button>
        </td>
      </tr>
      {v.revisions.map((r, i) => (
        <tr key={`${v.number}.${i}`} className="version-revision">
          <td />
          <td className="muted">Earlier · {title} v{v.number}.{i + 1}</td>
          <td className="muted">{when(r.at)}</td>
          <td className="muted">{r.note ?? '—'}</td>
          <td className="num muted">{r.entities}</td>
          <td className="num muted">{formatSize(r.size)}</td>
          <td className="row-tools">
            <button type="button" className="icon mini" title="Restore this earlier state" disabled={busy || !nodesKnown} onClick={() => onRestore(i)}><RotateCcw size={12} /></button>
            <button type="button" className="icon mini" title="Export as a .lapin file of its own" disabled={busy} onClick={() => onExport(i)}><Download size={12} /></button>
            {desktop && <button type="button" className="icon mini" title="Open separately, in another Lapin window" disabled={busy} onClick={() => onOpen(i)}><ExternalLink size={12} /></button>}
          </td>
        </tr>
      ))}
    </>
  );
}

/** A module's comparison (the work schedule's): its facts from → to, then its rows; a click selects what the project still has. */
function SectionDiff({ section, onPick }: { section: DiffSection; onPick: (id: string) => void }) {
  const [open, setOpen] = useState(section.rows.length <= 40);
  return (
    <div className="version-changes" data-testid={`version-section-${section.key}`} data-i18n-ctx={section.key}>
      <button type="button" className="link" onClick={() => setOpen(!open)}>{section.title}</button>
      {section.facts.map((f) => (
        <div key={f.label} className="small"><span>{f.label}</span>: <span data-no-i18n>{`${f.from ?? '—'} → ${f.to ?? '—'}`}</span></div>
      ))}
      {open && section.rows.length > 0 && (
        <ul>
          {section.rows.map((r, i) => (
            <li key={`${r.id ?? r.name}:${r.what}:${i}`}>
              {r.id ? <button type="button" className="link" onClick={() => onPick(r.id!)} data-no-i18n>{r.name}</button> : <span data-no-i18n>{r.name}</span>}
              {' '}<span className="muted small">{WHAT[r.what] ?? r.what}</span>
              {(r.from || r.to) && <span className="muted small" data-no-i18n>{` ${r.from ?? ''}${r.from && r.to ? ' → ' : ''}${r.to ?? ''}`}</span>}
              {r.days != null && r.days !== 0 && <span className={`small${r.days > 0 ? ' error-text' : ''}`} data-no-i18n>{` (${r.days > 0 ? '+' : '−'}${Math.abs(r.days)} d)`}</span>}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** The entities of one kind of change, grouped by class; a click selects the element where the project still has it. */
function ChangeList({ title, changes, onPick }: { title: string; changes: Change[]; onPick: (id: string) => void }) {
  const [open, setOpen] = useState(changes.length <= 40);
  const groups = new Map<string, Change[]>();
  for (const c of changes) (groups.get(c.class) ?? groups.set(c.class, []).get(c.class)!).push(c);
  return (
    <div className="version-changes">
      <button type="button" className="link" onClick={() => setOpen(!open)}>{title} ({changes.length})</button>
      {open && [...groups.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([cls, list]) => (
        <div key={cls} className="version-group">
          <span className="muted">{cls.replace(/^Ifc/, '')} · {list.length}</span>
          <ul>
            {list.map((c) => (
              <li key={c.id}><button type="button" className="link" onClick={() => onPick(c.id)}>{c.name || c.id}</button>{c.fields?.length ? <span className="muted small"> {c.fields.join(', ')}</span> : null}</li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
