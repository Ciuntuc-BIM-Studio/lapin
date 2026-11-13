/**
 * Manage → Title blocks… (docs/plan-title-blocks.md, decision 340): the title blocks by where they are kept — Lapin's own (built
 * in, read only: Duplicate makes one to change), this project's (they go with the file) and this computer's library (any
 * project here takes them). For the one chosen: a preview with the project's values, Edit (the designer), Set as project
 * default, Rename, Duplicate into the project or the library, Copy to the other place, Export as .json (its images inside),
 * Delete; New and Import… on top.
 */

import { useEffect, useMemo, useState } from 'react';
import { BookMarked, Copy, FileDown, FileUp, FolderInput, Pencil, Plus, Star, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { t as tr } from '../../../i18n';
import { pensToSvg } from '../documents/docModel';
import { TitleBlockDesigner } from './TitleBlockDesigner';
import { FirmFolderFiles } from '../FirmFolderFiles';
import {
  createTitleBlock, deleteTitleBlock, exportTitleBlock, importTitleBlock, listTitleBlocks, previewTitleBlock, setDefaultTitleBlock, updateTitleBlock,
  type TbList,
} from './titleBlocks';
import { forgetBlock, freeName, isTitleBlockFile, keepBlock, libraryBlocks, normalise, renameBlock, type LibraryBlock, type TbFile, type TbSummary } from './titleBlockModel';

const GROUPS: { origin: TbSummary['origin']; label: string; empty: string }[] = [
  { origin: 'builtin', label: 'Built-in', empty: '' },
  { origin: 'project', label: 'This project', empty: 'None yet: start a new one, or duplicate one of Lapin\'s.' },
  { origin: 'library', label: 'This computer', empty: 'Title blocks kept here are offered in every project on this computer.' },
];

const keyOf = (t: { origin: string; id: string }) => `${t.origin}:${t.id}`;

/** Which title block the dialog opens on (a sheet's "Edit…" asks for its own). */
export const titleBlocksDialog = { open: null as string | null };

export function TitleBlocksDialog() {
  const version = useSession((s) => s.version);
  const nodes = useSession((s) => s.nodes);
  const [list, setList] = useState<TbList>({ items: [], default: '' });
  const [library, setLibrary] = useState<LibraryBlock[]>(libraryBlocks());
  const [selected, setSelected] = useState<string | null>(() => (titleBlocksDialog.open ? keyOf({ origin: titleBlocksDialog.open.startsWith('builtin:') ? 'builtin' : 'project', id: titleBlocksDialog.open }) : null));
  const [editing, setEditing] = useState<string | null>(null);
  const [renaming, setRenaming] = useState<string | null>(null);
  const [into, setInto] = useState<'project' | 'library'>('project');
  const [preview, setPreview] = useState<string | null>(null);
  useEffect(() => { void listTitleBlocks().then(setList); }, [version]);
  useEffect(() => { titleBlocksDialog.open = null; }, []);
  const reload = () => setLibrary(libraryBlocks());
  const close = () => useUi.setState({ dialog: null });

  const all: TbSummary[] = useMemo(() => [
    ...list.items.map((t) => ({ ...t, origin: t.builtin ? 'builtin' as const : 'project' as const })),
    ...library.map((b) => ({ id: b.id, name: b.name, builtin: false, origin: 'library' as const })),
  ], [list, library]);
  const current = all.find((t) => keyOf(t) === selected) ?? null;

  // The chosen one drawn with the project's values (the first sheet's), its images from the project.
  useEffect(() => {
    setRenaming(null);
    setPreview(null);
    if (!current) return;
    let live = true;
    const kept = current.origin === 'library' ? library.find((b) => b.id === current.id) : null;
    void previewTitleBlock(kept ? { template: normalise(kept.template) } : { id: current.id }).then((r) => {
      if (!live || !r) return;
      setPreview(pensToSvg(r, (id) => { const n = nodes.get(id); return typeof n?.props.data === 'string' ? `data:${n.props.mime};base64,${n.props.data}` : null; }));
    });
    return () => { live = false; };
  }, [selected, version, library]); // eslint-disable-line react-hooks/exhaustive-deps

  /** A title block whole, wherever it is kept. */
  const whole = async (t: TbSummary): Promise<TbFile | null> => {
    if (t.origin === 'library') { const b = libraryBlocks().find((x) => x.id === t.id); return b ? { format: 'lapin.titleBlock', version: 1, name: b.name, template: b.template } : null; }
    return exportTitleBlock(t.id);
  };

  const toProject = async (t: TbSummary, name?: string) => {
    const file = await whole(t);
    if (!file) return;
    const id = await importTitleBlock(file, name);
    if (id) { setSelected(keyOf({ origin: 'project', id })); notify('Title block added to the project.'); }
  };

  const toLibrary = async (t: TbSummary, name?: string) => {
    const file = await whole(t);
    if (!file) return;
    const kept = keepBlock({ ...file, name: name ?? file.name });
    if (!kept) { notify("This computer's library is full: the title block was not kept there."); return; }
    reload();
    setSelected(keyOf({ origin: 'library', id: kept.id }));
    notify("Title block kept in this computer's library.");
  };

  const newOne = async () => {
    const id = await createTitleBlock({ name: freeName(tr('Title block'), all.map((t) => t.name)) });
    if (id) { setSelected(keyOf({ origin: 'project', id })); setEditing(id); }
  };

  const remove = async (t: TbSummary) => {
    if (t.origin === 'project') {
      if (!window.confirm(tr('Delete this title block from the project? Its sheets take the project\'s default; Undo brings it back.'))) return;
      await deleteTitleBlock(t.id);
    } else if (t.origin === 'library') {
      if (!window.confirm(tr('Delete this title block from this computer\'s library?'))) return;
      forgetBlock(t.id);
      reload();
    }
    setSelected(null);
  };

  const rename = async (t: TbSummary, name: string) => {
    const n = name.trim();
    setRenaming(null);
    if (!n || n === t.name) return;
    if (t.origin === 'project') await updateTitleBlock(t.id, { name: n });
    else if (t.origin === 'library') { renameBlock(t.id, n); reload(); }
  };

  const exportOne = async (t: TbSummary) => {
    const file = await whole(t);
    if (!file) return;
    const saved = await saveFile(new TextEncoder().encode(JSON.stringify(file, null, 2) + '\n'), `${safeFileName(t.name, 'title-block')}.json`, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };

  const importOne = async (given?: { bytes: Uint8Array }) => {
    const f = given ?? await pickFile('Title block (JSON)', 'json');
    if (!f) return;
    let json: unknown;
    try { json = JSON.parse(new TextDecoder().decode(f.bytes)); } catch { notify('That file is not a title block.'); return; }
    if (!isTitleBlockFile(json)) { notify('That file is not a title block.'); return; }
    if (into === 'library') {
      const kept = keepBlock(json);
      if (!kept) { notify("This computer's library is full: the title block was not kept there."); return; }
      reload();
      setSelected(keyOf({ origin: 'library', id: kept.id }));
    } else {
      const id = await importTitleBlock(json);
      if (id) setSelected(keyOf({ origin: 'project', id }));
    }
    notify('Title block imported.');
  };

  if (editing) return <TitleBlockDesigner id={editing} onClose={() => setEditing(null)} />;

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates title-blocks" role="dialog" aria-label="Title blocks" data-dialog="title-blocks" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Title blocks</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row">
              <button type="button" className="btn" onClick={() => void newOne()} data-field="tb-new" title="An empty title block in this project, opened in the designer"><Plus size={13} />New</button>
              <button type="button" className="btn" onClick={() => void importOne()} data-field="tb-import" title="A title block's .json file, exported from Lapin on this or another computer"><FileUp size={13} />Import…</button>
              <select value={into} onChange={(e) => setInto(e.target.value as 'project' | 'library')} data-field="tb-import-where" aria-label="Import into">
                <option value="project">into this project</option>
                <option value="library">into this computer's library</option>
              </select>
            </div>
            {GROUPS.map((g) => {
              const items = all.filter((t) => t.origin === g.origin);
              return (
                <section key={g.origin}>
                  <h4 className="eyebrow">{g.origin === 'library' ? <BookMarked size={12} /> : null} {g.label}</h4>
                  {items.length === 0 ? (g.empty && <p className="muted small">{g.empty}</p>) : (
                    <ul className="doc-templates">
                      {items.map((t) => (
                        <li key={keyOf(t)} className={keyOf(t) === selected ? 'active' : undefined}>
                          <button type="button" data-field={`tb-item-${t.id}`} data-origin={t.origin} onClick={() => setSelected(keyOf(t))} onDoubleClick={() => t.origin === 'project' && setEditing(t.id)}>
                            <strong data-no-i18n>{t.name}</strong>
                            {t.default && <span className="muted small"><Star size={10} /> Project default</span>}
                            {t.origin !== 'library' && (t.uses ?? 0) > 0 && <span className="muted small">{`${t.uses} ${t.uses === 1 ? tr('sheet') : tr('sheets')}`}</span>}
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              );
            })}
            <FirmFolderFiles ext="lapintitleblock" onPick={(f) => importOne(f)}
              save={{ disabled: !current, get: async () => { if (!current) { notify('Choose a title block to save.'); return null; } const file = await whole(current); return file ? { text: JSON.stringify(file, null, 2) + '\n', name: current.name } : null; } }} />
          </aside>
          <main className="tpl-detail">
            {!current ? <p className="muted">Choose a title block: edit it, make it the project's default, duplicate or export it. A sheet chooses its own in its properties.</p> : (
              <>
                {renaming != null ? (
                  <input className="tpl-title-input" autoFocus value={renaming} onChange={(e) => setRenaming(e.target.value)} data-field="tb-rename-input"
                    onBlur={() => void rename(current, renaming)} onKeyDown={(e) => { if (e.key === 'Enter') void rename(current, renaming); if (e.key === 'Escape') setRenaming(null); }} />
                ) : <h3 className="tpl-title" data-no-i18n>{current.name}</h3>}
                <p className="muted small">{current.origin === 'builtin' ? 'Built-in' : current.origin === 'project' ? 'This project' : 'This computer'}</p>
                {current.description && <p className="small">{current.description}</p>}
                <div className="row wrap">
                  {current.origin === 'project' && <button type="button" className="btn primary" onClick={() => setEditing(current.id)} data-field="tb-edit"><Pencil size={13} />Edit…</button>}
                  {current.origin !== 'library' && !current.default && <button type="button" className="btn" onClick={() => void setDefaultTitleBlock(current.id)} data-field="tb-default" title="Sheets that do not choose their own use it"><Star size={13} />Set as project default</button>}
                  {current.origin !== 'builtin' && <button type="button" className="btn" onClick={() => setRenaming(current.name)} data-field="tb-rename"><Pencil size={13} />Rename</button>}
                  <button type="button" className="btn" onClick={() => void toProject(current, freeName(`${current.name} (${tr('copy')})`, all.map((t) => t.name)))} data-field="tb-duplicate" title="A copy in this project, to change"><Copy size={13} />Duplicate into the project</button>
                  <button type="button" className="btn" onClick={() => void toLibrary(current, `${current.name} (${tr('copy')})`)} data-field="tb-duplicate-library" title="A copy in this computer's library"><Copy size={13} />Duplicate into the library</button>
                  {current.origin === 'project' && <button type="button" className="btn" onClick={() => void toLibrary(current)} data-field="tb-to-library"><BookMarked size={13} />Copy to the library</button>}
                  {current.origin === 'library' && <button type="button" className="btn" onClick={() => void toProject(current)} data-field="tb-to-project"><FolderInput size={13} />Copy to the project</button>}
                  <button type="button" className="btn" onClick={() => void exportOne(current)} data-field="tb-export" title="The title block as a .json file, its images inside, to import on another computer"><FileDown size={13} />Export…</button>
                  {current.origin !== 'builtin' && <button type="button" className="btn danger" onClick={() => void remove(current)} data-field="tb-delete"><Trash2 size={13} />Delete</button>}
                </div>
                <h4 className="eyebrow">Preview, with this project's values</h4>
                <div className="tb-preview" data-field="tb-preview" data-no-i18n dangerouslySetInnerHTML={{ __html: preview ?? '' }} />
              </>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
