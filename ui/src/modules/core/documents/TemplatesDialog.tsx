/**
 * Manage → Document templates… (docs/plan-documents.md, stage 4): the templates by where they are kept — Lapin's own (built in,
 * changed only on a duplicate), this project's (they go with the file) and this computer's library (any project here takes
 * them). For the one chosen: Rename, Delete, Duplicate into the project or the library, Copy to the other place, Export as a
 * .json file; Import… reads one into the project or the library, so a company passes its templates from one computer to
 * another. A project or library template's Style (font, PDF font, size, headings' colour, logo) is set here too.
 */

import { useEffect, useMemo, useState } from 'react';
import { BookMarked, Copy, FileDown, FileUp, FolderInput, Pencil, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { t as tr } from '../../../i18n';
import { StyleForm } from './DocumentStyleForm';
import { deleteTemplate, engineTemplates, importTemplate, renameTemplate, setDocumentStyle, templateJson } from './documents';
import {
  changeLibraryTemplate, forgetTemplate, freeId, isTemplateJson, keepTemplate, libraryTemplates, summaryOf, withStyle,
  type DocStyle, type TemplateJson, type TemplateOrigin,
} from './templateLibrary';
import type { DocTemplate } from './docModel';

const GROUPS: { origin: TemplateOrigin; label: string; empty: string }[] = [
  { origin: 'builtin', label: 'Built-in', empty: '' },
  { origin: 'project', label: 'This project', empty: 'None yet: save a document as a template, or duplicate one.' },
  { origin: 'library', label: 'This computer', empty: 'Templates kept here are offered in every project on this computer.' },
];

const keyOf = (t: { origin?: TemplateOrigin; id: string }) => `${t.origin ?? 'builtin'}:${t.id}`;

export function TemplatesDialog() {
  const version = useSession((s) => s.version);
  const [engine, setEngine] = useState<DocTemplate[]>([]);
  const [library, setLibrary] = useState<TemplateJson[]>(libraryTemplates());
  const [selected, setSelected] = useState<string | null>(null);
  const [renaming, setRenaming] = useState<string | null>(null);
  const [into, setInto] = useState<'project' | 'library'>('project');
  const [style, setStyle] = useState<DocStyle | null>(null);
  useEffect(() => { void engineTemplates().then(setEngine); }, [version]);
  const reload = () => setLibrary(libraryTemplates());
  const close = () => useUi.setState({ dialog: null });

  const all: DocTemplate[] = useMemo(() => [...engine.map((t) => ({ ...t, origin: t.origin ?? 'builtin' as const })), ...library.map(summaryOf)], [engine, library]);
  const current = all.find((t) => keyOf(t) === selected) ?? null;
  const taken = () => [...engine.map((t) => t.id), ...libraryTemplates().map((t) => t.id)];

  // The chosen template's style: the engine's for a project one, the library's own.
  useEffect(() => {
    setRenaming(null);
    if (!current || current.origin === 'builtin') { setStyle(null); return; }
    if (current.origin === 'library') { setStyle(library.find((t) => t.id === current.id)?.page?.style ?? {}); return; }
    void templateJson({ templateId: current.id }).then((j) => setStyle((j?.page?.style as DocStyle | undefined) ?? {}));
  }, [selected, version, library]); // eslint-disable-line react-hooks/exhaustive-deps

  /** A template whole, wherever it is kept (the library's without its day). */
  const whole = async (t: DocTemplate): Promise<TemplateJson | null> => {
    if (t.origin === 'library') {
      const kept = libraryTemplates().find((x) => x.id === t.id);
      if (!kept) return null;
      const { added: _added, ...rest } = kept;
      return rest;
    }
    return templateJson({ templateId: t.id });
  };

  const toLibrary = async (t: DocTemplate, title?: string) => {
    const json = await whole(t);
    if (!json) return;
    const copy = title ? { ...json, id: freeId(title, taken()), title } : json;
    let r = keepTemplate(copy);
    if (r === 'exists') {
      if (!window.confirm(tr('This computer\'s library has a template with this id. Replace it?'))) return;
      r = keepTemplate(copy, true);
    }
    if (r === 'full') { notify("This computer's library is full: the template was not kept there."); return; }
    reload();
    setSelected(keyOf({ origin: 'library', id: copy.id }));
    notify('Template kept in this computer\'s library.');
  };

  const toProject = async (t: DocTemplate, title?: string) => {
    const json = await whole(t);
    if (!json) return;
    const exists = !title && engine.some((x) => x.origin === 'project' && x.id === json.id);
    const overwrite = exists && window.confirm(tr('The project has a template with this id. Replace it? (Cancel keeps both.)'));
    const r = await importTemplate(json, { overwrite, ...(title ? { title } : {}) });
    if (r?.templateId) { setSelected(keyOf({ origin: 'project', id: r.templateId })); notify('Template added to the project.'); }
  };

  const remove = async (t: DocTemplate) => {
    if (t.origin === 'project') {
      if (!window.confirm(tr('Delete this template from the project? Undo brings it back; documents made from it keep their text.'))) return;
      await deleteTemplate(t.id);
    } else if (t.origin === 'library') {
      if (!window.confirm(tr('Delete this template from this computer\'s library?'))) return;
      forgetTemplate(t.id);
      reload();
    }
    setSelected(null);
  };

  const rename = async (t: DocTemplate, title: string) => {
    const name = title.trim();
    setRenaming(null);
    if (!name || name === t.title) return;
    if (t.origin === 'project') await renameTemplate(t.id, name);
    else if (t.origin === 'library') { changeLibraryTemplate(t.id, (x) => ({ ...x, title: name })); reload(); }
  };

  const exportOne = async (t: DocTemplate) => {
    const json = await whole(t);
    if (!json) return;
    const saved = await saveFile(new TextEncoder().encode(JSON.stringify(json, null, 2) + '\n'), `${safeFileName(t.title, t.id)}.json`, 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };

  const importOne = async () => {
    const f = await pickFile('Document template (JSON)', 'json');
    if (!f) return;
    let json: unknown;
    try { json = JSON.parse(new TextDecoder().decode(f.bytes)); } catch { notify('That file is not a document template.'); return; }
    if (!isTemplateJson(json)) { notify('That file is not a document template.'); return; }
    if (into === 'library') {
      let kept: TemplateJson = json;
      let r = keepTemplate(kept);
      if (r === 'exists') {
        if (window.confirm(tr('This computer\'s library has a template with this id. Replace it?'))) r = keepTemplate(kept, true);
        else { kept = { ...json, id: freeId(json.title || json.id, taken()) }; r = keepTemplate(kept); }
      }
      if (r === 'full') { notify("This computer's library is full: the template was not kept there."); return; }
      reload();
      setSelected(keyOf({ origin: 'library', id: kept.id }));
      notify('Template imported.');
    } else {
      const exists = engine.some((x) => x.origin === 'project' && x.id === json.id);
      const overwrite = exists && window.confirm(tr('The project has a template with this id. Replace it? (Cancel keeps both.)'));
      const r = await importTemplate(json, { overwrite });
      if (r?.templateId) { setSelected(keyOf({ origin: 'project', id: r.templateId })); notify('Template imported.'); }
    }
  };

  const changeStyle = (t: DocTemplate, s: DocStyle | null) => {
    setStyle(s ?? {});
    if (t.origin === 'project') void setDocumentStyle({ templateId: t.id }, s);
    else if (t.origin === 'library') {
      if (!changeLibraryTemplate(t.id, (x) => ({ ...x, page: withStyle(x.page, s) }))) notify("This computer's library is full: the style was not kept.");
      reload();
    }
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates" role="dialog" aria-label="Document templates" data-dialog="document-templates" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Document templates</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row">
              <button type="button" className="btn" onClick={() => void importOne()} data-field="tpl-import" title="A template's .json file, exported from Lapin on this or another computer"><FileUp size={13} />Import…</button>
              <select value={into} onChange={(e) => setInto(e.target.value as 'project' | 'library')} data-field="tpl-import-where" aria-label="Import into">
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
                          <button type="button" data-field={`tpl-item-${t.id}`} data-origin={t.origin} onClick={() => setSelected(keyOf(t))}>
                            <strong data-no-i18n>{t.title}</strong>
                            {t.description && <span className="muted small" data-no-i18n>{t.description}</span>}
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              );
            })}
          </aside>
          <main className="tpl-detail">
            {!current ? <p className="muted">Choose a template: rename it, duplicate it, export it, or set its company style.</p> : (
              <>
                {renaming != null ? (
                  <input className="tpl-title-input" autoFocus value={renaming} onChange={(e) => setRenaming(e.target.value)} data-field="tpl-rename-input"
                    onBlur={() => void rename(current, renaming)} onKeyDown={(e) => { if (e.key === 'Enter') void rename(current, renaming); if (e.key === 'Escape') setRenaming(null); }} />
                ) : <h3 className="tpl-title" data-no-i18n>{current.title}</h3>}
                <p className="muted small">
                  <span>{current.origin === 'builtin' ? 'Built-in' : current.origin === 'project' ? 'This project' : 'This computer'}</span>
                  <span data-no-i18n>{` · ${current.lang} · ${current.isoType} · ${current.id}`}</span>
                </p>
                {current.description && <p className="small" data-no-i18n>{current.description}</p>}
                <div className="row wrap">
                  {current.origin !== 'builtin' && <button type="button" className="btn" onClick={() => setRenaming(current.title)} data-field="tpl-rename"><Pencil size={13} />Rename</button>}
                  <button type="button" className="btn" onClick={() => void toProject(current, `${current.title} (${tr('copy')})`)} data-field="tpl-duplicate" title="A copy in this project, to change"><Copy size={13} />Duplicate into the project</button>
                  <button type="button" className="btn" onClick={() => void toLibrary(current, `${current.title} (${tr('copy')})`)} data-field="tpl-duplicate-library" title="A copy in this computer's library, to change"><Copy size={13} />Duplicate into the library</button>
                  {current.origin === 'project' && <button type="button" className="btn" onClick={() => void toLibrary(current)} data-field="tpl-to-library"><BookMarked size={13} />Copy to the library</button>}
                  {current.origin === 'library' && <button type="button" className="btn" onClick={() => void toProject(current)} data-field="tpl-to-project"><FolderInput size={13} />Copy to the project</button>}
                  <button type="button" className="btn" onClick={() => void exportOne(current)} data-field="tpl-export" title="The template as a .json file, to import on another computer"><FileDown size={13} />Export…</button>
                  {current.origin !== 'builtin' && <button type="button" className="btn danger" onClick={() => void remove(current)} data-field="tpl-delete"><Trash2 size={13} />Delete</button>}
                </div>
                <h4 className="eyebrow">Style</h4>
                {current.origin === 'builtin'
                  ? <p className="muted small">Lapin's own templates keep Lapin's look: duplicate one to give it your company's style.</p>
                  : style && <StyleForm key={keyOf(current)} style={style} onChange={(s) => changeStyle(current, s)} />}
              </>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
