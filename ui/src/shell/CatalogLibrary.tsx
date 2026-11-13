/**
 * The Catalog's „More sources” (docs/plan-user-extensibility.md §4.1): what can be brought into the project from Lapin's own
 * libraries, this computer, the firm's folders and the installed extensions, one list with a search, a kind and a source
 * filter, each entry with its picture where the library has one, where it comes from and its licence, and Add to project.
 */

import { useEffect, useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import { notify } from '../app/store';
import { execute, useSession } from '../engine/session';
import { hasModule } from '../platform/brand';
import { computerFiles, libraryKind, libraryKinds, useLibraryFiles } from '../platform/libraries';
import { useExtensions } from '../platform/extensions/runtime';
import { readFirmFiles, firmFoldersAvailable } from '../modules/core/firmFolders';
import { useLibraries } from '../modules/core/profileLibraries/store';
import { typePackRows, isUserPack } from '../modules/core/typeLibrary';
import { myPatterns } from '../modules/core/myPatterns';
import { bringPatterns } from '../modules/core/libraryKinds';
import { librarySymbols, normaliseDef, parsePack, type SymDef } from '../modules/core/symbols/symbolModel';
import { createSymbol, listSymbols, previewSymbol } from '../modules/core/symbols/symbols';
import { Thumb } from '../modules/core/symbols/Thumb';
import { t as tr } from '../i18n';
import { countByKind, fileEntry, filterEntries, SOURCES, symbolEntries, typeEntries, type CatalogEntry, type CatalogSource } from './catalogEntries';

const needProject = () => { if (!useSession.getState().connected) { notify('Open a project first.'); return false; } return true; };

/** Every entry of every source this edition has; what could not be read, said. */
export async function gatherCatalog(): Promise<{ entries: CatalogEntry[]; problems: string[] }> {
  const entries: CatalogEntry[] = [], problems: string[] = [];
  const installed = useExtensions.getState().installed;
  const extOf = (id?: string) => installed.find((x) => x.id === id);
  const edition = (ext: string) => { const k = libraryKind(ext); return !!k && (!k.module || hasModule(k.module)); };
  const withPreviews = async (list: CatalogEntry[], text: string): Promise<CatalogEntry[]> => {
    const defs = new Map((parsePack(text) ?? []).map((x) => [x.name, x.def]));
    return Promise.all(list.map(async (e) => ({ ...e, preview: defs.has(e.name) ? await previewSymbol({ def: defs.get(e.name)! }) : null })));
  };
  const keepEntries = (list: (CatalogEntry | { problem: string })[]) => { for (const e of list) if ('problem' in e) problems.push(e.problem); else entries.push(e); };
  const addSymbol = async (s: { name: string; def: SymDef }) => {
    if (!needProject()) return;
    const r = await createSymbol({ name: s.name, def: s.def });
    if (r) notify(`Symbol added to the project: ${s.name}.`);
  };

  // Lapin's own symbols (a copy in the project to change) and this computer's.
  if (edition('lapinsymbols')) {
    const received = new Set(installed.flatMap((x) => (x.kind === 'symbols' && Array.isArray(x.receipt.ids) ? (x.receipt.ids as string[]) : [])));
    const list = await listSymbols(true);
    for (const s of list.items.filter((i) => i.builtin)) {
      entries.push({ key: `builtin:lapinsymbols:${s.id}`, kind: 'lapinsymbols', name: tr(s.name), detail: s.category, source: 'builtin', preview: s.preview,
        add: async () => { if (!needProject()) return; if (await createSymbol({ from: s.id, name: s.name })) notify(`Symbol added to the project: ${s.name}.`); } });
    }
    for (const b of librarySymbols().filter((x) => !received.has(x.id))) {
      const def = normaliseDef(b.def);
      entries.push({ key: `computer:lapinsymbols:${b.id}`, kind: 'lapinsymbols', name: b.name, detail: def.category, source: 'computer', preview: await previewSymbol({ def }), add: () => addSymbol({ name: b.name, def }) });
    }
  }

  // Files on this computer: an extension's (its name and licence) or the user's.
  for (const f of await computerFiles()) {
    if (!edition(f.ext)) continue;
    const x = extOf(f.extension);
    const src = { source: (x ? 'extension' : 'computer') as CatalogSource, ...(x ? { from: x.name, license: x.manifest.license } : {}) };
    if (f.ext === 'lapinsymbols') entries.push(...await withPreviews(symbolEntries(f.text, src, addSymbol), f.text));
    else keepEntries([fileEntry({ kind: f.ext, name: f.name, text: f.text, ...src })]);
  }
  for (const f of myPatterns()) if (edition('pat')) keepEntries([fileEntry({ kind: 'pat', name: f.name, text: f.text, source: 'computer', file: f.name }, (t, file) => bringPatterns(t, file))]);

  // Type packs on this computer: the user's, and those of types extensions.
  if (edition('lapintypes')) {
    const addType = (text: string) => async (ref: string) => {
      if (!needProject()) return;
      const r = await execute('types.importFrom', { text, refs: [ref] }) as { created: number; reused: number } | null;
      if (r) notify(`Types added to the project: ${r.created} new, ${r.reused} already in it.`);
    };
    for (const row of await typePackRows.all()) {
      const x = isUserPack(row) ? undefined : extOf(row.id);
      entries.push(...typeEntries(row.text, { source: x ? 'extension' : 'computer', pack: row.name, ...(x ? { from: x.name, license: x.manifest.license } : {}) }, addType(row.text)));
    }
  }

  // The firm's folders.
  if (firmFoldersAvailable()) {
    const r = await readFirmFiles([]);
    problems.push(...r.problems);
    for (const f of r.files) {
      if (!edition(f.kind)) continue;
      const src = { source: 'firm' as CatalogSource, from: f.folder.split(/[\\/]/).filter(Boolean).pop() ?? f.folder };
      if (f.kind === 'lapinsymbols') entries.push(...await withPreviews(symbolEntries(f.text, src, addSymbol), f.text));
      else if (f.kind === 'lapintypes') entries.push(...typeEntries(f.text, { ...src, pack: f.name }, async (ref) => { if (needProject()) { const x = await execute('types.importFrom', { text: f.text, refs: [ref] }) as { created: number; reused: number } | null; if (x) notify(`Types added to the project: ${x.created} new, ${x.reused} already in it.`); } }));
      else keepEntries([fileEntry({ kind: f.kind, name: f.name, text: f.text, ...src, file: `${f.name}.${f.kind}` })]);
    }
  }
  return { entries, problems };
}

export function CatalogLibrary() {
  const files = useLibraryFiles((s) => s.version);
  const folders = useLibraries((s) => s.version);
  const installed = useExtensions((s) => s.installed);
  const projectVersion = useSession((s) => s.version);
  const [state, setState] = useState<{ entries: CatalogEntry[]; problems: string[]; loading: boolean }>({ entries: [], problems: [], loading: true });
  const [search, setSearch] = useState('');
  const [kind, setKind] = useState<string | null>(null);
  const [source, setSource] = useState<CatalogSource | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    void gatherCatalog().then((r) => { if (live) setState({ ...r, loading: false }); }).catch(() => { if (live) setState({ entries: [], problems: ['The libraries could not be read.'], loading: false }); });
    return () => { live = false; };
  }, [files, folders, installed, projectVersion]);
  const counts = useMemo(() => countByKind(state.entries), [state.entries]);
  const kinds = libraryKinds().filter((k) => counts[k.ext]);
  const shown = filterEntries(state.entries, { search, kind, source });
  const add = async (e: CatalogEntry) => { if (!e.add) return; setBusy(e.key); try { await e.add(); } finally { setBusy(null); } };
  return (
    <div className="catalog-library" data-testid="catalog-library">
      <input type="search" placeholder={tr('Find in the libraries')} value={search} onChange={(e) => setSearch(e.target.value)} data-field="catalog-search" />
      <div className="catalog-kinds" role="tablist" aria-label="Kind">
        <button type="button" className={`kind-chip${kind == null ? ' on' : ''}`} onClick={() => setKind(null)} data-kind="all">All</button>
        {kinds.map((k) => <button type="button" key={k.ext} className={`kind-chip${kind === k.ext ? ' on' : ''}`} data-kind={k.ext} onClick={() => setKind(k.ext)}>{tr(k.label)}<span className="muted small" data-no-i18n> {counts[k.ext]}</span></button>)}
      </div>
      <div className="catalog-kinds" role="tablist" aria-label="Source">
        <button type="button" className={`kind-chip${source == null ? ' on' : ''}`} onClick={() => setSource(null)}>Every source</button>
        {SOURCES.map((s) => <button type="button" key={s.id} className={`kind-chip${source === s.id ? ' on' : ''}`} data-source={s.id} onClick={() => setSource(s.id)}>{s.label}</button>)}
      </div>
      {state.loading && <p className="muted small pad">Reading the libraries…</p>}
      {!state.loading && shown.length === 0 && <p className="muted small pad">{state.entries.length === 0 ? 'Nothing here yet: save types and symbols to this computer or a firm folder, or install extensions.' : 'Nothing matches.'}</p>}
      <ul className="catalog-lib-list">
        {shown.map((e) => (
          <li key={e.key} className="catalog-lib-row" data-entry={e.name} data-source={e.source}>
            {e.preview !== undefined ? <Thumb p={e.preview} /> : <span className="sy-thumb" aria-hidden />}
            <span className="catalog-text">
              <span className="catalog-name" data-no-i18n>{e.name}</span>
              <span className="muted small" data-no-i18n>{[e.detail, `${tr(SOURCES.find((s) => s.id === e.source)!.label)}${e.from ? `: ${e.from}` : ''}`, e.license].filter(Boolean).join(' · ')}</span>
            </span>
            {e.add && <button type="button" className="icon" disabled={busy === e.key} data-action="catalog-add" title="Add to project (one step to undo)" aria-label="Add to project" onClick={() => void add(e)}><Plus size={15} /></button>}
          </li>
        ))}
      </ul>
      {state.problems.length > 0 && <p className="muted small pad" data-no-i18n>{state.problems[0]}</p>}
    </div>
  );
}
