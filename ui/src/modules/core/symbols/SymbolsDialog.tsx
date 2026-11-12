/**
 * Manage → Symbols… (docs/plan-user-extensibility.md §3): the symbols by where they are kept — Lapin's own (built in, read only:
 * Duplicate makes one to change), this project's (they go with the file) and this computer's library (any project here takes
 * them) — by category, each drawn. For the one chosen: Place (the place tool), Edit (the symbol editor), Rename, Duplicate, Add to
 * the project or Save to this computer, Export as a .lapinsymbols file, Delete (offering to delete what is placed); New, and
 * Import… of a .lapinsymbols file, whose symbols the project already has under the same name ask what to do.
 */

import { useEffect, useMemo, useState } from 'react';
import { BookMarked, Copy, FileDown, FileUp, FolderInput, MapPin, Pencil, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { t as tr } from '../../../i18n';
import { pensToSvg } from '../documents/docModel';
import { TYPE_CHOICES } from '../LibraryChoices';
import { freeName } from '../titleBlocks/titleBlockModel';
import { SymbolEditor } from './SymbolEditor';
import { Thumb } from './Thumb';
import { createSymbol, deleteSymbol, exportSymbols, getSymbol, importSymbols, listSymbols, previewSymbol, readPack, updateSymbol, type PackSymbol, type SymList } from './symbols';
import {
  byCategory, forgetSymbol, symbolsDialog, keepSymbol, librarySymbols, normaliseDef, packText, parsePack, previewImage, renameSymbol, updateLibrarySymbol,
  type LibrarySymbol, type SymDef, type SymPreview, type SymSummary,
} from './symbolModel';
import { placeTool } from './symbolTools';

type Origin = 'builtin' | 'project' | 'library';
interface Row { id: string; name: string; origin: Origin; category: string; scale: string; description?: string; uses: number; preview?: SymPreview }

const GROUPS: { origin: Origin; label: string; empty: string }[] = [
  { origin: 'builtin', label: 'Built-in', empty: '' },
  { origin: 'project', label: 'This project', empty: 'None yet: start a new one, or duplicate one of Lapin\'s.' },
  { origin: 'library', label: 'This computer', empty: 'Symbols kept here are offered in every project on this computer.' },
];
const keyOf = (r: { origin: string; id: string }) => `${r.origin}:${r.id}`;

const STARTER: SymDef = {
  scale: 'annotation', category: 'other', attributes: [],
  items: [{ type: 'circle', c: [0, 0], r: 4, stroke: 0.25 }, { type: 'line', a: [0, 0], b: [4, 0], w: 0.25 }],
};

export { Thumb };

type Editing = { kind: 'project'; id: string; name: string; def: SymDef } | { kind: 'library'; id: string; name: string; def: SymDef } | { kind: 'new'; name: string; def: SymDef };

export function SymbolsDialog() {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<SymList>({ items: [], categories: [] });
  const [library, setLibrary] = useState<LibrarySymbol[]>(librarySymbols());
  const [libPreviews, setLibPreviews] = useState<Record<string, SymPreview>>({});
  const [selected, setSelected] = useState<string | null>(() => (symbolsDialog.open ? keyOf({ origin: symbolsDialog.open.startsWith('builtin:') ? 'builtin' : 'project', id: symbolsDialog.open }) : null));
  const [editing, setEditing] = useState<Editing | null>(null);
  const [renaming, setRenaming] = useState<string | null>(null);
  const [into, setInto] = useState<'project' | 'library'>('project');
  const [search, setSearch] = useState('');
  const [pack, setPack] = useState<{ text: string; symbols: PackSymbol[]; choices: Record<string, string> } | null>(null);
  useEffect(() => { void listSymbols(true).then(setList); }, [version]);
  useEffect(() => {
    // The editor, when another part of the interface asked for it (Properties → Edit symbol).
    const id = symbolsDialog.open, edit = symbolsDialog.edit;
    symbolsDialog.open = null; symbolsDialog.edit = false;
    if (id && edit) void getSymbol(id).then((r) => { if (r && !r.builtin) setEditing({ kind: 'project', id, name: r.name, def: r.def }); });
  }, []);
  // The library's symbols drawn by the engine, as the project's are.
  useEffect(() => {
    let live = true;
    void Promise.all(library.map(async (b) => [b.id, await previewSymbol({ def: normaliseDef(b.def) })] as const)).then((all) => {
      if (live) setLibPreviews(Object.fromEntries(all.flatMap(([id, p]) => (p ? [[id, p]] : []))));
    });
    return () => { live = false; };
  }, [library]);
  const reload = () => setLibrary(librarySymbols());
  const close = () => useUi.setState({ dialog: null });

  const rows: Row[] = useMemo(() => [
    ...list.items.map((s: SymSummary) => ({ id: s.id, name: s.name, origin: (s.builtin ? 'builtin' : 'project') as Origin, category: s.category, scale: s.scale, description: s.description, uses: s.uses, preview: s.preview })),
    ...library.map((b) => ({ id: b.id, name: b.name, origin: 'library' as Origin, category: b.def.category || 'other', scale: b.def.scale, description: b.def.description, uses: 0, preview: libPreviews[b.id] })),
  ], [list, library, libPreviews]);
  const shown = rows.filter((r) => !search || `${r.name} ${r.category}`.toLowerCase().includes(search.toLowerCase()));
  const current = rows.find((r) => keyOf(r) === selected) ?? null;
  const taken = rows.filter((r) => r.origin !== 'library').map((r) => r.name);
  useEffect(() => { setRenaming(null); }, [selected]);

  /** The definition of a symbol, wherever it is kept. */
  const defOf = async (r: Row): Promise<SymDef | null> => {
    if (r.origin === 'library') { const b = librarySymbols().find((x) => x.id === r.id); return b ? normaliseDef(b.def) : null; }
    return (await getSymbol(r.id))?.def ?? null;
  };

  const toProject = async (r: Row, name?: string) => {
    const made = r.origin === 'library'
      ? await (async () => { const def = await defOf(r); return def ? createSymbol({ name: name ?? freeName(r.name, taken), def }) : null; })()
      : await createSymbol({ from: r.id, name: name ?? freeName(`${r.name} (${tr('copy')})`, taken) });
    if (made?.id) { setSelected(keyOf({ origin: 'project', id: made.id })); notify('Symbol added to the project.'); }
    return made?.id ?? null;
  };

  const toLibrary = async (r: Row, name?: string) => {
    const def = await defOf(r);
    if (!def) return;
    const kept = keepSymbol(name ?? r.name, def);
    if (!kept) { notify("This computer's library is full: the symbol was not kept there."); return; }
    reload();
    setSelected(keyOf({ origin: 'library', id: kept.id }));
    notify("Symbol kept in this computer's library.");
  };

  const edit = async (r: Row) => {
    const def = await defOf(r);
    if (!def) return;
    setEditing({ kind: r.origin === 'library' ? 'library' : 'project', id: r.id, name: r.name, def });
  };

  const duplicate = async (r: Row) => {
    if (r.origin === 'library') { await toLibrary(r, `${r.name} (${tr('copy')})`); return; }
    const id = await toProject(r);
    if (id && r.origin === 'builtin') { const g = await getSymbol(id); if (g) setEditing({ kind: 'project', id, name: g.name, def: g.def }); }
  };

  const remove = async (r: Row) => {
    if (r.origin === 'library') {
      if (!window.confirm(tr('Delete this symbol from this computer\'s library?'))) return;
      forgetSymbol(r.id); reload(); setSelected(null);
      return;
    }
    if (r.origin !== 'project') return;
    if (r.uses > 0) {
      if (!window.confirm(`${tr('This symbol is placed')} ${r.uses} ${r.uses === 1 ? tr('time') : tr('times')}. ${tr('Delete the symbol and its placed copies too? Undo brings them back.')}`)) return;
      await deleteSymbol(r.id, true);
    } else {
      if (!window.confirm(tr('Delete this symbol from the project? Undo brings it back.'))) return;
      await deleteSymbol(r.id);
    }
    setSelected(null);
  };

  const rename = async (r: Row, name: string) => {
    const n = name.trim();
    setRenaming(null);
    if (!n || n === r.name) return;
    if (r.origin === 'project') await updateSymbol(r.id, { name: n });
    else if (r.origin === 'library') { renameSymbol(r.id, n); reload(); }
  };

  const exportOne = async (r: Row) => {
    let text: string, file: string;
    if (r.origin === 'library') { const def = await defOf(r); if (!def) return; text = packText([{ name: r.name, def }], r.name); file = `${safeFileName(r.name, 'symbol')}.lapinsymbols`; }
    else {
      const e = await exportSymbols([r.id], r.name);
      if (!e) return;
      text = e.text; file = `${safeFileName(r.name, 'symbol')}.lapinsymbols`;
    }
    const saved = await saveFile(new TextEncoder().encode(text), file, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };

  const importOne = async () => {
    const f = await pickFile('Symbol library (.lapinsymbols)', 'lapinsymbols');
    if (!f) return;
    const text = new TextDecoder().decode(f.bytes);
    if (into === 'library') {
      const entries = parsePack(text);
      if (!entries) { notify('That file is not a symbol library.'); return; }
      let kept = 0;
      for (const e of entries) if (keepSymbol(e.name, e.def)) kept++;
      reload();
      notify(kept === entries.length ? 'Symbols imported.' : "This computer's library is full: not every symbol was kept there.");
      return;
    }
    const read = await readPack(text);
    if (!read) { notify('That file is not a symbol library.'); return; }
    const conflicts = read.symbols.filter((s) => s.exists && !s.same);
    if (conflicts.length === 0) { const r = await importSymbols(text, {}); if (r) notify('Symbols imported.'); return; }
    setPack({ text, symbols: read.symbols, choices: Object.fromEntries(conflicts.map((c) => [c.name, 'both'])) });
  };

  const applyPack = async () => {
    if (!pack) return;
    const r = await importSymbols(pack.text, pack.choices);
    setPack(null);
    if (r) notify('Symbols imported.');
  };

  /** The editor's save: the project's symbol updated, the library's entry replaced, a new one made. */
  const save = async (name: string, def: SymDef): Promise<boolean> => {
    if (!editing) return false;
    if (def.items.length === 0) { notify('A symbol draws something: it needs one item at least.'); return false; }
    if (editing.kind === 'library') {
      if (!updateLibrarySymbol(editing.id, name, def)) { notify("This computer's library is full: the symbol was not kept there."); return false; }
      reload();
    } else if (editing.kind === 'project') {
      await updateSymbol(editing.id, { name, def });
      if (useSession.getState().error) return false;
    } else {
      const made = await createSymbol({ name: freeName(name, taken), def });
      if (!made?.id) return false;
      setEditing({ kind: 'project', id: made.id, name: made.name, def });
      setSelected(keyOf({ origin: 'project', id: made.id }));
    }
    notify('Symbol saved.');
    return true;
  };

  if (editing) return <SymbolEditor name={editing.name} def={editing.def} categories={list.categories} onSave={save} onClose={() => setEditing(null)} />;

  const place = async (r: Row) => {
    const id = r.origin === 'library' ? await toProject(r) : r.id;
    if (!id) return;
    useUi.setState({ dialog: null });
    await placeTool(id);
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates symbols-dialog" role="dialog" aria-label="Symbols" data-dialog="symbols" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Symbols</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row">
              <button type="button" className="btn" onClick={() => setEditing({ kind: 'new', name: freeName(tr('Symbol'), taken), def: STARTER })} data-field="sy-new" title="A new symbol, opened in the editor"><Plus size={13} />New</button>
              <button type="button" className="btn" onClick={() => void importOne()} data-field="sy-import" title="A symbol library's .lapinsymbols file, exported from Lapin on this or another computer"><FileUp size={13} />Import…</button>
              <select value={into} onChange={(e) => setInto(e.target.value as 'project' | 'library')} data-field="sy-import-where" aria-label="Import into">
                <option value="project">into this project</option>
                <option value="library">into this computer's library</option>
              </select>
            </div>
            <input type="search" placeholder={tr('Find a symbol')} value={search} onChange={(e) => setSearch(e.target.value)} data-field="sy-search" />
            {GROUPS.map((g) => {
              const items = shown.filter((r) => r.origin === g.origin);
              return (
                <section key={g.origin}>
                  <h4 className="eyebrow">{g.origin === 'library' ? <BookMarked size={12} /> : null} {g.label}</h4>
                  {items.length === 0 ? (g.empty && !search && <p className="muted small">{g.empty}</p>) : byCategory(items, list.categories).map(([cat, group]) => (
                    <div key={cat}>
                      <h5 className="muted small sy-category" data-no-i18n>{cat}</h5>
                      <ul className="doc-templates sy-list">
                        {group.map((r) => (
                          <li key={keyOf(r)} className={keyOf(r) === selected ? 'active' : undefined}>
                            <button type="button" data-field={`sy-item-${r.id}`} data-origin={r.origin} onClick={() => setSelected(keyOf(r))} onDoubleClick={() => r.origin !== 'builtin' && void edit(r)}>
                              <Thumb p={r.preview} />
                              <span className="sy-item-text">
                                <strong data-no-i18n>{r.name}</strong>
                                {r.uses > 0 && <span className="muted small">{`${r.uses} ${tr('placed')}`}</span>}
                              </span>
                            </button>
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </section>
              );
            })}
          </aside>
          <main className="tpl-detail">
            {pack ? (
              <>
                <h3 className="tpl-title">Symbols the project already has, drawn otherwise</h3>
                <table className="layer-table">
                  <thead><tr><th>Symbol</th><th /><th>
                    <select value="" title="The same choice for every symbol" onChange={(e) => { if (e.target.value) setPack({ ...pack, choices: Object.fromEntries(Object.keys(pack.choices).map((k) => [k, e.target.value])) }); }}>
                      <option value="">Apply to all…</option>
                      {TYPE_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                    </select>
                  </th></tr></thead>
                  <tbody>
                    {pack.symbols.filter((s) => s.exists && !s.same).map((s) => (
                      <tr key={s.name}>
                        <td data-no-i18n>{s.name}</td>
                        <td><Thumb p={s.preview} /></td>
                        <td><select value={pack.choices[s.name] ?? 'both'} data-choice-symbol={s.name} onChange={(e) => setPack({ ...pack, choices: { ...pack.choices, [s.name]: e.target.value } })}>
                          {TYPE_CHOICES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                        </select></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                <div className="row">
                  <button type="button" className="btn primary" onClick={() => void applyPack()} data-field="sy-import-apply">Import</button>
                  <button type="button" className="btn" onClick={() => setPack(null)}>Cancel</button>
                </div>
              </>
            ) : !current ? <p className="muted">Choose a symbol: place it, edit it, duplicate or export it. Draw your own in the editor, or from drafting with Save as symbol… (right-click).</p> : (
              <>
                {renaming != null ? (
                  <input className="tpl-title-input" autoFocus value={renaming} onChange={(e) => setRenaming(e.target.value)} data-field="sy-rename-input"
                    onBlur={() => void rename(current, renaming)} onKeyDown={(e) => { if (e.key === 'Enter') void rename(current, renaming); if (e.key === 'Escape') setRenaming(null); }} />
                ) : <h3 className="tpl-title" data-no-i18n>{current.name}</h3>}
                <p className="muted small">{`${current.origin === 'builtin' ? tr('Built-in') : current.origin === 'project' ? tr('This project') : tr('This computer')} · `}<span data-no-i18n>{current.category}</span>{` · ${current.scale === 'model' ? tr('Model size') : tr('Paper size')}`}</p>
                {current.description && <p className="small" data-no-i18n>{current.description}</p>}
                <div className="row wrap">
                  <button type="button" className="btn primary" onClick={() => void place(current)} data-field="sy-place" title="Place it on a plan, section, elevation or drafting view"><MapPin size={13} />Place on a view</button>
                  {current.origin !== 'builtin' && <button type="button" className="btn" onClick={() => void edit(current)} data-field="sy-edit"><Pencil size={13} />Edit…</button>}
                  {current.origin !== 'builtin' && <button type="button" className="btn" onClick={() => setRenaming(current.name)} data-field="sy-rename"><Pencil size={13} />Rename</button>}
                  <button type="button" className="btn" onClick={() => void duplicate(current)} data-field="sy-duplicate" title={current.origin === 'library' ? "A copy in this computer's library" : 'A copy in this project, to change'}><Copy size={13} />Duplicate</button>
                  {current.origin === 'library' && <button type="button" className="btn" onClick={() => void toProject(current)} data-field="sy-to-project"><FolderInput size={13} />Add to the project</button>}
                  {current.origin !== 'library' && <button type="button" className="btn" onClick={() => void toLibrary(current)} data-field="sy-to-library"><BookMarked size={13} />Save to this computer</button>}
                  <button type="button" className="btn" onClick={() => void exportOne(current)} data-field="sy-export" title="The symbol as a .lapinsymbols file, to import on another computer"><FileDown size={13} />Export…</button>
                  {current.origin !== 'builtin' && <button type="button" className="btn danger" onClick={() => void remove(current)} data-field="sy-delete"><Trash2 size={13} />Delete</button>}
                </div>
                <h4 className="eyebrow">Preview</h4>
                <div className="tb-preview sy-detail-preview" data-field="sy-preview" data-no-i18n dangerouslySetInnerHTML={{ __html: current.preview ? pensToSvg(previewImage(current.preview, 3)) : '' }} />
              </>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
