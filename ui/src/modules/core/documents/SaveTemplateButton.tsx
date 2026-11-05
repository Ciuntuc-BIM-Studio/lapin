/**
 * Save as template… (docs/plan-documents.md, stage 4): the open document's shape (its page and style, blocks, declared inputs,
 * language and type) kept as a template under a title and a description, in this project (it goes with the file), in this
 * computer's library (any project here takes it), or both; a new one, or one of the same place replaced when chosen.
 */

import { useEffect, useState } from 'react';
import { BookmarkPlus, X } from 'lucide-react';
import { notify } from '../../../app/store';
import { engineTemplates, listDocuments, saveTemplate, templateJson } from './documents';
import { freeId, keepTemplate, libraryTemplates } from './templateLibrary';
import type { DocTemplate } from './docModel';

type Where = 'project' | 'library' | 'both';

function SaveTemplateForm({ documentId, onClose, onSaved }: { documentId: string; onClose: () => void; onSaved?: () => void }) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [where, setWhere] = useState<Where>('project');
  const [replace, setReplace] = useState('');
  const [engine, setEngine] = useState<DocTemplate[]>([]);
  const [busy, setBusy] = useState(false);
  const library = libraryTemplates();

  useEffect(() => {
    void listDocuments().then((docs) => setTitle((t) => t || (docs.find((d) => d.id === documentId)?.name ?? '')));
    void engineTemplates().then(setEngine);
  }, [documentId]);

  const project = engine.filter((t) => t.origin === 'project');
  // What may be replaced: the project's (into the project, or both) or the library's.
  const replaceable = where === 'library' ? library.map((t) => ({ id: t.id, title: t.title })) : project.map((t) => ({ id: t.id, title: t.title }));
  useEffect(() => { if (replace && !replaceable.some((t) => t.id === replace)) setReplace(''); }, [where]); // eslint-disable-line react-hooks/exhaustive-deps

  const go = async () => {
    const name = title.trim();
    if (!name || busy) return;
    setBusy(true);
    try {
      let saved = 0, full = false;
      if (where !== 'library') {
        const tid = await saveTemplate(documentId, name, description.trim(), replace || undefined);
        if (!tid) return;
        saved++;
        if (where === 'both') {
          const whole = await templateJson({ templateId: tid });
          if (whole) { const r = keepTemplate(whole, true); if (r === 'kept') saved++; else full = true; }
        }
      } else {
        const taken = [...engine.map((t) => t.id), ...library.map((t) => t.id)];
        const whole = await templateJson({ id: documentId, title: name, description: description.trim(), newId: replace || freeId(name, taken) });
        if (!whole) return;
        const r = keepTemplate(whole, Boolean(replace));
        if (r === 'kept') saved++; else full = true;
      }
      if (full) notify("This computer's library is full: the template was not kept there.");
      if (saved) { notify(where === 'library' ? 'Template kept in this computer\'s library.' : 'Template saved.'); onSaved?.(); onClose(); }
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="dialog-backdrop" onClick={(e) => { e.stopPropagation(); onClose(); }}>
      <div className="dialog tpl-save" role="dialog" aria-label="Save as template" data-dialog="save-template" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Save as template</span><button type="button" className="icon" onClick={onClose} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">The document's text, layout, inputs and style become a template; the project's data is filled again in each document made from it.</p>
          <label className="doc-info-row"><span className="muted small">Title</span><input value={title} onChange={(e) => setTitle(e.target.value)} data-field="tpl-save-title" autoFocus /></label>
          <label className="doc-info-row"><span className="muted small">Description</span><input value={description} onChange={(e) => setDescription(e.target.value)} data-field="tpl-save-description" /></label>
          <label className="doc-info-row"><span className="muted small">Keep it in</span>
            <select value={where} onChange={(e) => setWhere(e.target.value as Where)} data-field="tpl-save-where">
              <option value="project">This project</option>
              <option value="library">This computer's library</option>
              <option value="both">Both</option>
            </select>
          </label>
          <label className="doc-info-row"><span className="muted small">Save as</span>
            <select value={replace} onChange={(e) => setReplace(e.target.value)} data-field="tpl-save-replace">
              <option value="">A new template</option>
              {replaceable.map((t) => <option key={t.id} value={t.id} data-no-i18n>{t.title}</option>)}
            </select>
          </label>
          {replace && <p className="muted small">The template chosen is replaced; documents already made from it keep their text until Update from template.</p>}
          <div className="row">
            <span className="grow" />
            <button type="button" className="btn" onClick={onClose}>Cancel</button>
            <button type="button" className="btn primary" disabled={!title.trim() || busy} onClick={() => void go()} data-field="tpl-save-go">Save</button>
          </div>
        </div>
      </div>
    </div>
  );
}

/** The button, to place beside a document's other actions; its form opens over the dialog. */
export function SaveTemplateButton({ documentId, onSaved }: { documentId: string; onSaved?: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" className="btn" data-field="doc-save-template" title="Keep this document's text, layout and style as a template" onClick={() => setOpen(true)}>
        <BookmarkPlus size={13} /> Save as template…
      </button>
      {open && <SaveTemplateForm documentId={documentId} onClose={() => setOpen(false)} onSaved={onSaved} />}
    </>
  );
}
