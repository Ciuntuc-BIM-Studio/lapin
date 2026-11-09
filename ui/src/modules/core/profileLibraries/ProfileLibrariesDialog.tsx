/**
 * Manage → Profile libraries… (docs/plan-profile-libraries.md): the libraries on the left — Lapin's own EN set, this
 * computer's, the project's copies — their profiles in the middle by family and kind with a search, the profile chosen on
 * the right drawn with its dimensions and its properties (Lapin's worked out, the file's beside them, the differences
 * marked). Import a CSV or XLSX table (its columns matched as the price lists' are); copy a profile into the project; a
 * copy its library has newer is marked and updated on request, the types made of it following.
 */

import { useEffect, useMemo, useState } from 'react';
import { BookMarked, Download, FileUp, Library, RefreshCw, Search, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { engine, execute, query, useSession } from '../../../engine/session';
import { LONG_TIMEOUT_MS } from '../../../engine/client';
import { pickFile, saveFile, safeFileName, toBase64 } from '../../../engine/files';
import {
  families, formatValue, KIND_LABELS, KINDS, massOf, MATERIAL_LABELS, naturalCompare, newerInLibrary, PROPS, search,
  type LibraryEntry, type LibraryJson, type ProfileJson, type ProjectProfilesDto, type Ring,
} from './model';
import { forgetOnComputer, keepOnComputer, useProfileLibraries } from './store';
import { SectionView } from './SectionView';
import { TableImport, type CheckDto, type ImportReport, type TableRead } from './TableImport';

interface PropsDto {
  values: Record<string, { computed?: number; given?: number; method?: string }>;
  checks: CheckDto[]; rings: Ring[]; density: number; problem?: string | null; profile: ProfileJson;
}

const ORIGIN_LABELS: Record<string, string> = { builtin: 'Built in', computer: 'On this computer', folder: 'Company folders', project: 'In the project' };

/** The properties' table: Lapin's value, the file's, and the difference where it is more than the check allows. */
function PropsTable({ dto }: { dto: PropsDto }) {
  const checked = new Map(dto.checks.map((c) => [c.key, c]));
  const rows = PROPS.filter((p) => dto.values[p.key]);
  const anyGiven = rows.some((p) => dto.values[p.key].given != null);
  return (
    <table className="profile-props" data-testid="profile-props">
      <thead><tr><th /><th className="num">Lapin</th>{anyGiven && <th className="num">File</th>}<th /></tr></thead>
      <tbody>
        {rows.map((p) => {
          const v = dto.values[p.key];
          const c = checked.get(p.key);
          return (
            <tr key={p.key} className={c ? 'flagged' : undefined} title={v.method}>
              <td>{p.label}</td>
              <td className="num" data-no-i18n>{formatValue(v.computed, p.factor)}</td>
              {anyGiven && <td className="num" data-no-i18n>{formatValue(v.given, p.factor)}</td>}
              <td className="muted small"><span data-no-i18n>{p.unit}</span>{c && <span className="tag warn" data-no-i18n>{` ${c.difference > 0 ? '+' : ''}${(c.difference * 100).toFixed(1)} %${c.unit ? ` (${c.unit}?)` : ''}`}</span>}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

export function ProfileLibrariesDialog() {
  const version = useSession((s) => s.version);
  const close = () => useUi.setState({ dialog: null });
  const { entries, problems, loading } = useProfileLibraries();
  const [project, setProject] = useState<ProjectProfilesDto>({ copies: [], legacy: [] });
  useEffect(() => { void query<ProjectProfilesDto>('profiles.project').then((r) => setProject(r ?? { copies: [], legacy: [] })); }, [version]);
  const projectLib: LibraryEntry = useMemo(() => ({
    key: 'project', origin: 'project', name: 'In the project',
    library: { name: 'In the project', profiles: project.copies.map((c) => c.profile).sort((a, b) => naturalCompare(a.name, b.name)) },
  }), [project]);
  const all = useMemo(() => [...entries, projectLib], [entries, projectLib]);
  const [libKey, setLibKey] = useState<string>('builtin');
  const lib = all.find((e) => e.key === libKey) ?? all[0];
  const [text, setText] = useState('');
  const [kind, setKind] = useState('');
  const [family, setFamily] = useState('');
  const [chosen, setChosen] = useState<{ key: string; name: string } | null>(null);
  const [dto, setDto] = useState<PropsDto | null>(null);
  const [table, setTable] = useState<{ name: string; bytes: Uint8Array; read: TableRead } | null>(null);
  const [report, setReport] = useState<ImportReport | null>(null);
  const [busy, setBusy] = useState(false);

  // The search runs over every library when text is typed, else over the one chosen.
  const scope = text.trim() ? all : lib ? [lib] : [];
  const found = useMemo(() => search(scope, text, { kind: kind || undefined, family: family || undefined }, 800), [scope, text, kind, family]);
  const fams = useMemo(() => (lib ? families(lib.library.profiles) : []), [lib]);
  const current = chosen ? all.find((e) => e.key === chosen.key)?.library.profiles.find((p) => p.name === chosen.name) ?? null : null;
  const currentEntry = chosen ? all.find((e) => e.key === chosen.key) ?? null : null;
  const copy = currentEntry?.origin === 'project' ? project.copies.find((c) => c.name === current?.name) ?? null : null;
  const newer = copy ? newerInLibrary(copy, entries) : null;

  useEffect(() => {
    if (!current) { setDto(null); return; }
    let live = true;
    void query<PropsDto>('profiles.properties', { profile: current }).then((r) => { if (live) setDto(r); });
    return () => { live = false; };
  }, [current]);

  const importTable = async () => {
    const f = await pickFile('Profile table (CSV, XLSX)', ['csv', 'xlsx', 'txt']);
    if (!f) return;
    try {
      const read = await engine.call<TableRead>('doc.query', { name: 'profiles.readTable', args: { data: toBase64(f.bytes) } });
      setTable({ name: f.name, bytes: f.bytes, read });
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const importIfc = async () => {
    const f = await pickFile('IFC file (a profile library, or any model)', ['ifc']);
    if (!f) return;
    setBusy(true);
    try {
      const r = await engine.call<LibraryJson & { skipped: number; notes: string[]; checks: CheckDto[]; checkCount: number }>('profiles.readIfc', { data: toBase64(f.bytes), name: f.name }, { timeoutMs: LONG_TIMEOUT_MS });
      const { skipped, notes, checks, checkCount, ...library } = r;
      await imported({ library, skipped, notes, checks, checkCount });
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  const exportIfc = async () => {
    if (!lib) return;
    try {
      const library = { name: lib.origin === 'project' ? 'Project profiles' : lib.name, material: lib.library.material ?? 'steel', profiles: lib.library.profiles };
      const r = await engine.call<{ name: string; data: string }>('profiles.exportIfc', { library }, { timeoutMs: LONG_TIMEOUT_MS });
      await saveFile(Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0)), `${safeFileName(library.name, 'profiles')}.ifc`);
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const imported = async (r: ImportReport) => {
    await keepOnComputer(r.library);
    setTable(null);
    setReport(r);
    setLibKey(`computer:${r.library.name}`);
    notify(`Profile library imported: ${r.library.profiles.length} profile(s), ${r.skipped} row(s) left out.`);
  };
  const copyIntoProject = async () => {
    if (!current || !currentEntry) return;
    const r = await execute('profiles.use', { profile: current, library: currentEntry.origin === 'project' ? copy?.library ?? null : currentEntry.name }) as { differs?: boolean } | null;
    if (r?.differs) notify('The project already has this profile from that library, as it was then: Update brings the library\'s.');
    else if (r) notify(`${current.name} is in the project: its types can be made of it.`);
  };
  const exportCsv = async () => {
    if (!lib) return;
    const csv = await query<string>('profiles.csv', { profiles: lib.library.profiles });
    if (csv != null) await saveFile(new TextEncoder().encode('﻿' + csv), `${safeFileName(lib.name, 'profiles')}.csv`, 'text/csv');
  };
  const update = async () => {
    if (!copy || !newer) return;
    setBusy(true);
    const r = await execute('profiles.update', { id: copy.id, profile: newer.profile }) as { types?: number } | null;
    setBusy(false);
    if (r) notify(`${copy.name} updated from ${newer.entry.name}: ${r.types ?? 0} type(s) follow it.`);
  };

  const groups = ['builtin', 'computer', 'folder', 'project'].map((o) => ({ origin: o, items: all.filter((e) => e.origin === o) }));
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide profile-libraries" role="dialog" aria-label="Profile libraries" data-dialog="profile-libraries" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title"><Library size={14} /> Profile libraries</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {table ? (
            <>
              <h4 className="eyebrow" data-no-i18n>{table.name}</h4>
              <TableImport file={table} read={table.read} onDone={(r) => void imported(r)} onCancel={() => setTable(null)} />
            </>
          ) : (
            <div className="profile-libraries-grid">
              <nav className="profile-libraries-list" data-testid="profile-libraries">
                {groups.map((g) => (
                  <div key={g.origin}>
                    <p className="eyebrow">{ORIGIN_LABELS[g.origin]}</p>
                    {g.items.length === 0 && <p className="muted small">{g.origin === 'computer' ? 'Libraries you import are kept here, for any project.' : g.origin === 'folder' ? 'None.' : 'No profile yet.'}</p>}
                    <ul className="plain-list">
                      {g.items.map((e) => (
                        <li key={e.key} className={`tree-item${e.key === lib?.key ? ' selected' : ''}`} onClick={() => { setLibKey(e.key); setFamily(''); }} data-library={e.name}>
                          <span data-no-i18n>{e.name}</span><span className="muted small" data-no-i18n>{` ${e.library.profiles.length}`}</span>
                          {e.origin === 'computer' && <button type="button" className="icon mini" title="Forget it on this computer (the projects keep their copies)" onClick={(ev) => { ev.stopPropagation(); void forgetOnComputer(e.name); }}><Trash2 size={11} /></button>}
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
                <div className="row wrap">
                  <button type="button" className="btn" onClick={() => void importTable()} data-testid="profile-import-table"><FileUp size={14} />Table…</button>
                  <button type="button" className="btn" disabled={busy} onClick={() => void importIfc()} data-testid="profile-import-ifc" title="An IFC file: a profile library, or any model (the profiles its elements use)"><FileUp size={14} />IFC…</button>
                </div>
                {loading && <p className="muted small">Reading the libraries…</p>}
                {problems.map((p, i) => <p key={i} className="error-text small" data-no-i18n>{p}</p>)}
              </nav>
              <section className="profile-libraries-profiles">
                <div className="row">
                  <Search size={13} />
                  <input type="search" value={text} onChange={(e) => setText(e.target.value)} placeholder="Search every library" data-testid="profile-search" className="grow" />
                </div>
                <div className="row">
                  <select value={kind} onChange={(e) => setKind(e.target.value)} title="Kind">
                    <option value="">Every kind</option>
                    {KINDS.map((k) => <option key={k} value={k}>{KIND_LABELS[k]}</option>)}
                  </select>
                  {!text.trim() && <select value={family} onChange={(e) => setFamily(e.target.value)} title="Family" data-testid="profile-family">
                    <option value="">Every family</option>
                    {fams.map((f) => <option key={f.family} value={f.family} data-no-i18n>{`${f.family || '—'} (${f.count})`}</option>)}
                  </select>}
                </div>
                <ul className="plain-list profile-list" data-testid="profile-list">
                  {found.map(({ entry, profile }) => {
                    const pc = entry.origin === 'project' ? project.copies.find((c) => c.name === profile.name) : null;
                    const isNewer = pc ? newerInLibrary(pc, entries) != null : false;
                    return (
                      <li key={`${entry.key}/${profile.name}`} className={`tree-item${chosen?.key === entry.key && chosen.name === profile.name ? ' selected' : ''}`} onClick={() => setChosen({ key: entry.key, name: profile.name })} data-profile={profile.name}>
                        <span data-no-i18n>{profile.name}</span>
                        <span className="muted small" data-no-i18n>{[profile.family, massOf(profile) != null ? `${massOf(profile)!.toFixed(1)} kg/m` : null, text.trim() ? entry.name : null].filter(Boolean).join(' · ')}</span>
                        {(profile.summary?.checks ?? 0) > 0 && <span className="tag warn" title="Some of the file's values differ from Lapin's">check</span>}
                        {isNewer && <span className="tag" title="Its library has another version of it">newer in library</span>}
                      </li>
                    );
                  })}
                  {found.length === 0 && <li className="muted small">No profile.</li>}
                </ul>
                <div className="row wrap">
                  {lib && lib.library.profiles.length > 0 && <>
                    <button type="button" className="btn" onClick={() => void exportCsv()} title="The library as a CSV Lapin reads back"><Download size={14} />CSV</button>
                    <button type="button" className="btn" onClick={() => void exportIfc()} data-testid="profile-export-ifc" title="The library as an IFC4 profile library file, for other programs"><Download size={14} />IFC</button>
                  </>}
                </div>
              </section>
              <section className="profile-libraries-detail" data-testid="profile-detail">
                {!current ? <p className="muted">Choose a profile to see it drawn with its properties.</p> : (
                  <>
                    <h4 className="eyebrow" data-no-i18n>{current.name}</h4>
                    <p className="muted small">
                      <span>{KIND_LABELS[current.kind] ?? current.kind}</span>
                      {current.family && <span data-no-i18n>{` · ${current.family}`}</span>}
                      {current.standard && <span data-no-i18n>{` · ${current.standard}`}</span>}
                      {' · '}<span>{MATERIAL_LABELS[current.material ?? 'steel'] ?? current.material}</span>
                      {copy?.library && <><span className="muted"> · from </span><span data-no-i18n>{copy.library}</span></>}
                    </p>
                    {dto && <SectionView profile={current} rings={dto.rings} centroid={dto.values.cx && dto.values.cy ? [dto.values.cx.computed ?? 0, dto.values.cy.computed ?? 0] : null} />}
                    {dto?.problem && <p className="error-text small" data-no-i18n>{dto.problem}</p>}
                    {dto && <PropsTable dto={dto} />}
                    {dto && dto.checks.length > 0 && <p className="muted small">The file's values marked differ from Lapin's by more than 2 % (area, mass), 3 % (inertias, moduli) or 10 % (It, Iw): check them, or the column's unit.</p>}
                    <div className="row wrap">
                      {currentEntry?.origin !== 'project' && <button type="button" className="btn primary" onClick={() => void copyIntoProject()} data-testid="profile-use"><BookMarked size={14} />Copy into the project</button>}
                      {copy && newer && <button type="button" className="btn primary" disabled={busy} onClick={() => void update()} data-testid="profile-update" title={`From ${newer.entry.name}`}><RefreshCw size={14} />Update from the library</button>}
                      {copy && <button type="button" className="btn" disabled={copy.types.length > 0} title={copy.types.length ? 'Its types use it: change their profile first' : 'Take it out of the project'} onClick={() => void execute('profiles.remove', { id: copy.id }).then(() => setChosen(null))}><Trash2 size={14} />Remove</button>}
                    </div>
                    {copy && <p className="muted small"><span>Types made of it:</span> <span data-no-i18n>{copy.types.length}</span></p>}
                  </>
                )}
              </section>
            </div>
          )}
          {report && !table && (
            <div className="profile-report" data-testid="profile-report">
              <p className="small"><span data-no-i18n>{report.library.name}</span>: <span data-no-i18n>{report.library.profiles.length}</span> <span>profile(s)</span>, <span data-no-i18n>{report.skipped}</span> <span>row(s) left out</span>, <span data-no-i18n>{report.checkCount}</span> <span>value(s) to check</span>.</p>
              {report.notes.map((n, i) => <p key={i} className="muted small" data-no-i18n>{n}</p>)}
              {report.checks.length > 0 && (
                <ul className="plain-list small check-list">
                  {report.checks.slice(0, 30).map((c, i) => (
                    <li key={i} data-no-i18n>{`${c.profile} · ${c.key}: ${(c.difference * 100).toFixed(1)} %${c.unit ? ` (${c.unit})` : ''}`}</li>
                  ))}
                </ul>
              )}
              <button type="button" className="btn" onClick={() => setReport(null)}>Close</button>
            </div>
          )}
        </div>
        <footer className="dialog-foot"><span className="muted small">Lapin ships a small EN set; import your own libraries (tables, IFC) for the rest.</span><span className="grow" /><button type="button" className="btn" onClick={close}>Close</button></footer>
      </div>
    </div>
  );
}
