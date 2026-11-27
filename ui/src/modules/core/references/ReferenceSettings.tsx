/**
 * Folders and types (docs/plan-cde-references.md §3.2): the project's own copy of the folder tree (rename, add, remove, order; the
 * engine refuses to remove a folder that holds documents and says so), the language the folders are named in on disk, the list
 * of document types with their default folder, validity and extra fields, the urbanism certificate's checklist offered (§3.3: the
 * permits a certificate may ask for, ticked on each certificate) and the size above which a file is not kept in the project file.
 * Every value shipped is indicative; the norms are the user's.
 */

import { confirmAction, promptText } from '../../../shell/prompts';
import { useEffect, useState } from 'react';
import { ArrowDown, ArrowUp, FolderPlus, Plus, Trash2 } from 'lucide-react';
import { execute } from '../../../engine/session';
import { useLang } from '../../../i18n';
import { addFolder, addOption, addTypeField, draftOf, moveOption, plainOptions, renameOption, moveFolder, removeFolder, renameFolder, renameType, sameTree, typeIdOf, type FolderDraft } from './folderEdit';
import { useRefAdapter } from './adapter';
import { nameIn, type ChecklistOption, type FolderTree, type RefType, type TypeList } from './references';

export function ReferenceSettings({ tree, types, guest, onDone }: { tree: FolderTree; types: TypeList; guest: boolean; onDone: () => void }) {
  const ui = useLang((s) => s.lang);
  const adapter = useRefAdapter();
  const { setFolders, setTypes } = adapter;
  const [tab, setTab] = useState<'folders' | 'types' | 'checklist'>('folders');
  const [draft, setDraft] = useState<FolderDraft[]>(() => draftOf(tree.folders));
  const [list, setList] = useState<RefType[]>(types.types);
  const saved = draftOf(tree.folders);
  useEffect(() => { setDraft(draftOf(tree.folders)); }, [tree]);
  useEffect(() => { setList(types.types); }, [types]);
  const lang = tree.language;
  const dirtyFolders = !sameTree(draft, saved);
  const dirtyTypes = JSON.stringify(list) !== JSON.stringify(types.types);
  const flat: { id: string; text: string; depth: number }[] = [];
  const names = (nodes: FolderDraft[], depth: number) => nodes.forEach((n) => { if (!n.derived) { flat.push({ id: n.id, text: n.name[lang] ?? n.name.en ?? n.id, depth }); names(n.children, depth + 1); } });
  names(draft, 0);

  const saveFolders = async () => { await setFolders({ folders: draft }); onDone(); };
  const changeType = (id: string, patch: Partial<RefType>) => setList((l) => l.map((t) => (t.id === id ? { ...t, ...patch } : t)));
  const saveTypes = async () => { await setTypes(list); onDone(); };
  const addType = () => {
    const name = promptText('Name of the new document type:')?.trim();
    if (name) setList((l) => [...l, { id: typeIdOf(name, l.map((t) => t.id)), name: { en: name, ro: name }, fields: [] }]);
  };
  const addTypeFieldTo = (id: string) => {
    const label = promptText('Name of the extra field:')?.trim();
    if (label) setList((l) => addTypeField(l, id, label, ui === 'ro' ? 'ro' : 'en'));
  };

  const rows = (nodes: FolderDraft[], depth: number): React.ReactNode => nodes.map((n, i) => (
    <div key={n.id}>
      <div className="refs-edit-row" style={{ paddingLeft: depth * 14 }}>
        {n.derived
          ? <span className="muted small grow" title="Shows what this project issued">{n.name[lang] ?? n.name.en} <span>(derived: holds no documents)</span></span>
          : <input className="grow" aria-label="Folder name" key={`${n.id}:${n.name[lang] ?? ''}`} defaultValue={n.name[lang] ?? n.name.en ?? ''} disabled={guest}
              onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== (n.name[lang] ?? n.name.en)) setDraft((d) => renameFolder(d, n.id, lang, v)); }} />}
        {!guest && !n.derived && <button type="button" className="icon mini" title="New subfolder" onClick={() => { const name = promptText('Name of the new folder:')?.trim(); if (name) setDraft((d) => addFolder(d, n.id, name)); }}><FolderPlus size={11} /></button>}
        {!guest && <button type="button" className="icon mini" title="Move up" disabled={i === 0} onClick={() => setDraft((d) => moveFolder(d, n.id, -1))}><ArrowUp size={11} /></button>}
        {!guest && <button type="button" className="icon mini" title="Move down" disabled={i === nodes.length - 1} onClick={() => setDraft((d) => moveFolder(d, n.id, 1))}><ArrowDown size={11} /></button>}
        {!guest && <button type="button" className="icon mini" title="Remove the folder: only an empty one can go" onClick={() => { if (confirmAction('Remove the folder {name} and its subfolders? A folder that holds documents stays: move them first.', { name: n.name[lang] ?? n.name.en ?? n.id })) setDraft((d) => removeFolder(d, n.id)); }}><Trash2 size={11} /></button>}
      </div>
      {rows(n.children, depth + 1)}
    </div>
  ));

  return (
    <div className="refs-panel" data-testid="refs-settings">
      <div className="chips" role="tablist" aria-label="Settings">
        <button type="button" role="tab" aria-selected={tab === 'folders'} className={`chip${tab === 'folders' ? ' on' : ''}`} onClick={() => setTab('folders')}>Folders</button>
        <button type="button" role="tab" aria-selected={tab === 'types'} className={`chip${tab === 'types' ? ' on' : ''}`} onClick={() => setTab('types')}>Types</button>
        <button type="button" role="tab" aria-selected={tab === 'checklist'} className={`chip${tab === 'checklist' ? ' on' : ''}`} data-testid="refs-settings-checklist" onClick={() => setTab('checklist')}>Checklist</button>
      </div>
      {tab === 'checklist' && <ChecklistOffered types={types} folders={flat} guest={guest} onDone={onDone} />}
      {tab === 'folders' && (
        <>
          <p className="muted small">The project's own copy of the folder tree. Documents point at a folder's id, so renaming one keeps them in it. The folders are named on disk in the language chosen here.</p>
          <label className="row small"><span className="muted loc-label">Folder names on disk</span>
            <select value={lang} disabled={guest} aria-label="Language of the folders" onChange={(e) => void setFolders({ language: e.target.value }).then(onDone)}>
              <option value="ro">Română</option><option value="en">English</option>
            </select>
          </label>
          <div className="refs-edit">{rows(draft, 0)}</div>
          {!guest && (
            <div className="row">
              <button type="button" className="btn small" onClick={() => { const name = promptText('Name of the new folder:')?.trim(); if (name) setDraft((d) => addFolder(d, null, name)); }}><FolderPlus size={12} /> New folder</button>
              <span className="grow" />
              <button type="button" className="btn small" disabled={!dirtyFolders} onClick={() => setDraft(saved)}>Revert</button>
              <button type="button" className="btn small primary" disabled={!dirtyFolders} data-testid="refs-save-folders" onClick={() => void saveFolders()}>Save folders</button>
            </div>
          )}
          {adapter.kind === 'engine' && <KeepLimit tree={tree} guest={guest} onDone={onDone} />}
        </>
      )}
      {tab === 'types' && (
        <>
          <p className="muted small">The kinds of document, each with its folder, its usual validity (indicative: used only to suggest a "valid until" the document does not write) and the extra fields it carries.</p>
          <table className="layer-table refs-types">
            <thead><tr><th>Name</th><th>Folder</th><th>Validity (months)</th><th>Checklist</th><th /></tr></thead>
            <tbody>
              {list.map((t) => (
                <tr key={t.id}>
                  <td>
                    <input aria-label="Type name" key={`${t.id}:${nameIn(t.name, ui)}`} defaultValue={nameIn(t.name, ui)} disabled={guest}
                      onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== nameIn(t.name, ui)) setList((l) => renameType(l, t.id, ui === 'ro' ? 'ro' : 'en', v)); }} />
                    <div className="small muted">{(t.fields ?? []).map((f) => (
                      <span key={f.key} className="refs-chip">{nameIn(f.label, ui)}{!guest && <button type="button" className="icon mini" title="Remove the field" onClick={() => changeType(t.id, { fields: t.fields.filter((x) => x.key !== f.key) })}><Trash2 size={9} /></button>}</span>
                    ))}{!guest && <button type="button" className="link" onClick={() => addTypeFieldTo(t.id)}><Plus size={10} /> <span>Extra field</span></button>}</div>
                  </td>
                  <td><select aria-label="Folder" value={t.folder ?? ''} disabled={guest} onChange={(e) => changeType(t.id, { folder: e.target.value || undefined })}>
                    <option value="" />{flat.map((f) => <option key={f.id} value={f.id}>{' '.repeat(f.depth)}{f.text}</option>)}</select></td>
                  <td><input type="number" min={1} max={600} aria-label="Validity (months)" className="refs-days" key={`${t.id}:${t.validityMonths ?? ''}`} defaultValue={t.validityMonths ?? ''} disabled={guest}
                    onBlur={(e) => { const v = Number(e.target.value); changeType(t.id, { validityMonths: v > 0 ? Math.min(600, Math.round(v)) : undefined }); }} /></td>
                  <td><input type="checkbox" aria-label="Checklist" checked={!!t.checklist} disabled={guest} onChange={(e) => changeType(t.id, { checklist: e.target.checked || undefined })} /></td>
                  <td>{!guest && <button type="button" className="icon mini" title="Remove the type: documents of it keep it in the list" onClick={() => setList((l) => l.filter((x) => x.id !== t.id))}><Trash2 size={11} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {!guest && (
            <div className="row">
              <button type="button" className="btn small" onClick={addType}><Plus size={12} /> New type</button>
              <span className="grow" />
              <button type="button" className="btn small" disabled={!dirtyTypes} onClick={() => setList(types.types)}>Revert</button>
              <button type="button" className="btn small primary" disabled={!dirtyTypes} data-testid="refs-save-types" onClick={() => void saveTypes()}>Save types</button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

/**
 * The urbanism certificate's checklist offered: the permits and studies a certificate may ask for, which each certificate's
 * Checklist tab ticks. Renaming an item keeps its key, so the certificates that ticked it keep it; taking one off the list leaves it
 * on the certificates that ticked it.
 */
function ChecklistOffered({ types, folders, guest, onDone }: { types: TypeList; folders: { id: string; text: string; depth: number }[]; guest: boolean; onDone: () => void }) {
  const ui = useLang((s) => s.lang);
  const lang = ui === 'ro' ? 'ro' : 'en';
  const adapter = useRefAdapter();
  const [items, setItems] = useState<ChecklistOption[]>(() => plainOptions(types.checklist ?? []));
  useEffect(() => { setItems(plainOptions(types.checklist ?? [])); }, [types]);
  const dirty = JSON.stringify(items) !== JSON.stringify(plainOptions(types.checklist ?? []));
  const add = () => { const label = promptText('Name of the permit or study:')?.trim(); if (label) setItems((l) => addOption(l, label, lang)); };
  const save = async () => { await adapter.setChecklist(plainOptions(items)); onDone(); };
  const defaults = async () => {
    if (!confirmAction('Take the indicative list again? The certificates keep the items they ticked.')) return;
    await adapter.setChecklist(null);
    onDone();
  };
  return (
    <>
      <p className="muted small">The permits and studies an urbanism certificate may ask for, offered on each certificate's Checklist tab to tick. The list shipped is indicative, never what the law requires: change it to what your certificates ask for. A certificate keeps the items it ticked whatever this list becomes.</p>
      <table className="layer-table refs-types" data-testid="refs-checklist-offered">
        <thead><tr><th>Permit or study</th><th>Folder</th><th /></tr></thead>
        <tbody>
          {items.map((o, i) => (
            <tr key={o.key} data-option={o.key}>
              <td><input aria-label="Permit or study" key={`${o.key}:${nameIn(o.label, ui)}`} defaultValue={nameIn(o.label, ui)} disabled={guest}
                onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== nameIn(o.label, ui)) setItems((l) => plainOptions(renameOption(l, o.key, lang, v))); }} /></td>
              <td><select aria-label="Folder" value={o.folder ?? ''} disabled={guest} title="Where a document requested for this item goes"
                onChange={(e) => setItems((l) => l.map((x) => (x.key === o.key ? (e.target.value ? { ...x, folder: e.target.value } : (({ folder: _f, ...rest }) => rest)(x)) : x)))}>
                <option value="" />{folders.map((f) => <option key={f.id} value={f.id}>{' '.repeat(f.depth)}{f.text}</option>)}</select></td>
              <td style={{ width: 1 }}>{!guest && <span style={{ display: 'inline-flex', gap: 2 }}>
                <button type="button" className="icon mini" title="Move up" disabled={i === 0} onClick={() => setItems((l) => moveOption(l, o.key, -1))}><ArrowUp size={11} /></button>
                <button type="button" className="icon mini" title="Move down" disabled={i === items.length - 1} onClick={() => setItems((l) => moveOption(l, o.key, 1))}><ArrowDown size={11} /></button>
                <button type="button" className="icon mini" title="Take the item off the list: the certificates that ticked it keep it" onClick={() => setItems((l) => l.filter((x) => x.key !== o.key))}><Trash2 size={11} /></button>
              </span>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {!guest && (
        <div className="row">
          <button type="button" className="btn small" data-testid="refs-checklist-add" onClick={add}><Plus size={12} /> New item</button>
          {adapter.kind === 'engine' && <button type="button" className="btn small" title="The indicative list Lapin ships, again" onClick={() => void defaults()}>Indicative list</button>}
          <span className="grow" />
          <button type="button" className="btn small" disabled={!dirty} onClick={() => setItems(plainOptions(types.checklist ?? []))}>Revert</button>
          <button type="button" className="btn small primary" disabled={!dirty} data-testid="refs-save-checklist" onClick={() => void save()}>Save checklist</button>
        </div>
      )}
    </>
  );
}

function KeepLimit({ tree, guest, onDone }: { tree: FolderTree; guest: boolean; onDone: () => void }) {
  const mb = Math.round(tree.keepLimit / 1_048_576);
  return (
    <label className="row small" title="A file above this size is not kept in the project file: its hash and size are, and the file stays on the server and in the project folder">
      <span className="muted loc-label">Largest file kept in the project file (MB)</span>
      <input type="number" min={0} className="refs-days" aria-label="Largest file kept (MB)" key={mb} defaultValue={mb} disabled={guest}
        onBlur={(e) => { const v = Number(e.target.value); if (v >= 0 && v !== mb) void execute('references.setKeepLimit', { bytes: Math.round(v * 1_048_576) }).then(onDone); }} />
    </label>
  );
}
