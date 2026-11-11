/**
 * My furniture library (decision 476): Save to my library… keeps a project's furniture type with a name, a category, tags,
 * an article code and an indicative price of the user's own, its thumbnail drawn from the engine's preview; My furniture…
 * lists the library (searched, by category and tag, as a grid of thumbnails) to insert a type into the open project or to
 * place it at once, to rename it, change its tags, copy, delete or export it, to add it to a project template, and to import a
 * library file. See library.ts for where it is kept.
 */

import { useEffect, useMemo, useState } from 'react';
import { Copy, Download, FileUp, LayoutTemplate, MousePointerClick, Pencil, Plus, Tags, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { engineInBrowser, useSession } from '../../engine/session';
import { download, pickFile, pickPath, toBase64 } from '../../engine/files';
import { addFurnitureToTemplate, refreshTemplates, useTemplates } from '../../engine/templates';
import { specOf, TYPE_CLASS } from './furniture';
import {
  CATEGORIES, CATEGORY_LABELS, allTags, deleteEntry, duplicateEntry, exportEntries, filterEntries, importLibrary, parseTags, refreshLibrary,
  guessCategory, saveToLibrary, takeEntry, thumbnailOf, updateEntry, useFurnitureLibrary, useSaveToLibrary, type Category, type LibraryEntry,
} from './library';

const FILTERS = [{ name: 'Lapin furniture library', extensions: ['lapinlib'] }];
const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const close = () => useUi.setState({ dialog: null });
const fileName = (name: string) => name.replace(/[\\/:*?"<>|]/g, '_').trim() || 'My furniture';

/** Entries out as a .lapinlib file: a save dialog on the desktop, a download in the browser. */
async function exportTo(ids: string[] | null, name: string) {
  try {
    if (engineInBrowser) {
      const r = await exportEntries(ids);
      if (r.data) download(Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0)), `${fileName(name)}.lapinlib`, 'application/json');
      return;
    }
    const path = await pickPath('save', `${fileName(name)}.lapinlib`, FILTERS);
    if (!path) return;
    await exportEntries(ids, path.endsWith('.lapinlib') ? path : `${path}.lapinlib`);
    notify(`Exported ${path.split(/[\\/]/).pop()}.`);
  } catch (e) { notify(message(e)); }
}

function Card({ e, templates }: { e: LibraryEntry; templates: { id: string; name: string }[] }) {
  const [template, setTemplate] = useState('');
  const rename = async () => {
    const name = window.prompt('The furniture’s new name:', e.name)?.trim();
    if (name && name !== e.name) await updateEntry(e.id, { name }).catch((err) => notify(message(err)));
  };
  const retag = async () => {
    const text = window.prompt('Its tags, separated by commas:', e.tags.join(', '));
    if (text != null) await updateEntry(e.id, { tags: parseTags(text) }).catch((err) => notify(message(err)));
  };
  const remove = async () => {
    if (window.confirm(`Delete „${e.name}” from your library? Projects that have it keep it.`)) await deleteEntry(e.id).catch((err) => notify(message(err)));
  };
  const toTemplate = async () => {
    if (!template) return;
    try {
      await addFurnitureToTemplate(template, [e.type]);
      notify('Added to the template: projects made from it have this type.');
      setTemplate('');
    } catch (err) { notify(message(err)); }
  };
  return (
    <div className="furniture-card" data-library-entry={e.name}>
      <div className="furniture-thumb">{e.thumbnail ? <img src={e.thumbnail} alt="" /> : <span className="muted small">—</span>}</div>
      <div className="furniture-card-body">
        <span className="furniture-card-name" data-no-i18n title={e.name}>{e.name}</span>
        <span className="muted small"><span>{CATEGORY_LABELS[e.category] ?? e.category}</span>{e.article && <span data-no-i18n> · {e.article}</span>}{e.price != null && <span data-no-i18n> · {e.price.toLocaleString()}</span>}</span>
        {!!e.tags.length && <span className="furniture-tags" data-no-i18n>{e.tags.map((t) => <span key={t} className="chip small">{t}</span>)}</span>}
      </div>
      <div className="row furniture-card-actions">
        <button type="button" className="btn small" data-action="library-insert" title="Into the open project’s furniture types" onClick={() => void takeEntry(e, false)}><Plus size={12} /> Insert</button>
        <button type="button" className="btn small primary" data-action="library-place" title="Insert it and start placing it" onClick={() => void takeEntry(e, true)}><MousePointerClick size={12} /> Place</button>
        <span className="grow" />
        <button type="button" className="icon mini" title="Rename" onClick={() => void rename()}><Pencil size={12} /></button>
        <button type="button" className="icon mini" title="Tags" onClick={() => void retag()}><Tags size={12} /></button>
        <button type="button" className="icon mini" title="Duplicate" onClick={() => void duplicateEntry(e.id).catch((err) => notify(message(err)))}><Copy size={12} /></button>
        <button type="button" className="icon mini" title="Export as a .lapinlib file" onClick={() => void exportTo([e.id], e.name)}><Download size={12} /></button>
        <button type="button" className="icon mini" title="Delete" onClick={() => void remove()}><Trash2 size={12} /></button>
      </div>
      {!!templates.length && (
        <div className="row small furniture-card-template">
          <LayoutTemplate size={12} />
          <select value={template} data-field="library-template" title="Add to a template" onChange={(ev) => setTemplate(ev.target.value)}>
            <option value="">Add to a template…</option>
            {templates.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.name}</option>)}
          </select>
          {template && <button type="button" className="btn small" onClick={() => void toTemplate()}>Add</button>}
        </div>
      )}
    </div>
  );
}

/** Interior → My furniture…: the library. */
export function FurnitureLibraryDialog() {
  const { entries, read } = useFurnitureLibrary();
  const connected = useSession((s) => s.connected);
  const templates = useTemplates((s) => s.list);
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [tag, setTag] = useState('');
  useEffect(() => { if (connected) { void refreshLibrary(); void refreshTemplates(); } }, [connected]);
  const shown = useMemo(() => filterEntries(entries, { search, category: category || null, tag: tag || null }), [entries, search, category, tag]);
  const tags = useMemo(() => allTags(entries), [entries]);
  const importIt = async () => {
    try {
      let file: { path?: string; data?: string };
      if (engineInBrowser) {
        const picked = await pickFile('Lapin furniture library', 'lapinlib');
        if (!picked) return;
        file = { data: toBase64(picked.bytes) };
      } else {
        const path = await pickPath('open', undefined, FILTERS);
        if (!path) return;
        file = { path };
      }
      const r = await importLibrary(file);
      notify(`${r.added.length} added, ${r.skipped} already in your library.`);
    } catch (e) { notify(message(e)); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="My furniture">
      <div className="dialog wide" data-dialog="furniture-library" data-i18n-ctx="furniture">
        <header className="dialog-head">
          <span className="panel-title">My furniture</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="row" style={{ gap: 6, flexWrap: 'wrap' }}>
            <input type="search" placeholder="Search" value={search} data-field="library-search" onChange={(e) => setSearch(e.target.value)} />
            <select value={category} data-field="library-category" onChange={(e) => setCategory(e.target.value)}>
              <option value="">Every category</option>
              {CATEGORIES.map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
            </select>
            {!!tags.length && (
              <select value={tag} data-field="library-tag" onChange={(e) => setTag(e.target.value)}>
                <option value="">Every tag</option>
                {tags.map((t) => <option key={t} value={t} data-no-i18n>{t}</option>)}
              </select>
            )}
            <span className="grow" />
            <button type="button" className="btn small" data-action="library-import" onClick={() => void importIt()}><FileUp size={13} /> Import…</button>
            <button type="button" className="btn small" disabled={!entries.length} onClick={() => void exportTo(null, 'My furniture')}><Download size={13} /> Export all…</button>
          </div>
          <div className="furniture-library" data-testid="furniture-library">
            {shown.map((e) => <Card key={e.id} e={e} templates={templates} />)}
          </div>
          {read && !entries.length && <p className="muted small">Your library is empty. In Furniture types, Save to my library… keeps a type here for every project.</p>}
          {read && !!entries.length && !shown.length && <p className="muted small">Nothing matches.</p>}
          <p className="muted small">{engineInBrowser ? 'Kept in this browser. Export it to keep a file, or to take it to the desktop app.' : 'Kept on this computer, in your Lapin folder. Export it to take it to another computer or the browser.'}</p>
        </div>
      </div>
    </div>
  );
}

/** Furniture types → Save to my library…: the type with a name, a category, tags, an article code and a price. */
export function SaveToLibraryDialog() {
  const typeId = useSaveToLibrary((s) => s.typeId);
  const type = useSession((s) => (typeId ? s.nodes.get(typeId) : undefined));
  const [name, setName] = useState(type?.name ?? '');
  const [category, setCategory] = useState<Category>(() => guessCategory(String(specOf(type).kind ?? ''), String(specOf(type).preset ?? '')));
  const [tags, setTags] = useState('');
  const [article, setArticle] = useState('');
  const [price, setPrice] = useState('');
  const [thumb, setThumb] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);
  useEffect(() => { let live = true; if (type) void thumbnailOf(specOf(type)).then((t) => { if (live) setThumb(t); }); return () => { live = false; }; }, [type]);
  const back = () => useUi.setState({ dialog: 'furnitureTypes' });
  if (!type || type.type !== TYPE_CLASS) return null;
  const save = async () => {
    const p = price.trim() ? Number(price.replace(',', '.')) : null;
    if (p != null && (!Number.isFinite(p) || p < 0)) { notify('A price is a number, 0 or more.'); return; }
    setBusy(true);
    try {
      await saveToLibrary(type.id, { name: name.trim(), category, tags: parseTags(tags), article: article.trim() || undefined, price: p, thumbnail: thumb });
      notify('Saved to your library: Interior → My furniture… puts it into any project.');
      back();
    } catch (e) { notify(message(e)); setBusy(false); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Save to my library" onMouseDown={(e) => { if (e.target === e.currentTarget) back(); }}>
      <div className="dialog" data-dialog="furniture-save-library" data-i18n-ctx="furniture" style={{ width: 480 }}>
        <header className="dialog-head">
          <span className="panel-title">Save to my library</span>
          <button type="button" className="icon" onClick={back} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <div className="row furniture-save" style={{ gap: 10, alignItems: 'flex-start' }}>
            <div className="furniture-thumb">{thumb ? <img src={thumb} alt="" /> : <span className="muted small">…</span>}</div>
            <dl className="fields grow">
              <dt>Name</dt>
              <dd><input type="text" value={name} autoFocus data-field="library-name" data-no-i18n onChange={(e) => setName(e.target.value)} /></dd>
              <dt>Category</dt>
              <dd>
                <select value={category} data-field="library-save-category" onChange={(e) => setCategory(e.target.value as Category)}>
                  {CATEGORIES.map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
                </select>
              </dd>
              <dt>Tags</dt>
              <dd><input type="text" value={tags} placeholder="oak, modern" data-field="library-tags" data-no-i18n onChange={(e) => setTags(e.target.value)} /></dd>
              <dt>Article code</dt>
              <dd><input type="text" value={article} data-field="library-article" data-no-i18n onChange={(e) => setArticle(e.target.value)} /></dd>
              <dt>Price (indicative)</dt>
              <dd><input type="text" inputMode="decimal" value={price} data-field="library-price" data-no-i18n onChange={(e) => setPrice(e.target.value)} /></dd>
            </dl>
          </div>
          <p className="muted small">The type goes with its materials. Inserted into a project, a material the project has by name is used, the others are made; an identical type already there is reused. The price is yours, indicative.</p>
        </div>
        <footer className="dialog-foot"><span className="grow" />
          <button type="button" className="btn" onClick={back}>Cancel</button>
          <button type="button" className="btn primary" data-action="library-save" disabled={!name.trim() || busy} onClick={() => void save()}>Save</button>
        </footer>
      </div>
    </div>
  );
}

