/**
 * Manage → Documents… (docs/plan-documents.md, Interfața, etapa 1): the project's written documents. On the left the documents
 * and New from template (the templates with their descriptions); in the middle the document filled from the project, on its
 * pages, the empty fields marked; on the right its Inputs (the template's texts to write, by group, saved a moment after the
 * typing stops) and the Project information they share with every document, then To complete (a click shows the field's
 * place and where to fill it). Below: the exports (DOCX and ODT with the pictures the interface draws, PDF), Update from
 * template (the template's blocks again, the inputs kept), Rename, Duplicate and Delete. The preview follows every edit.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Copy, FileDown, FilePlus2, FileText, Pencil, RefreshCw, RotateCcw, Trash2, X } from 'lucide-react';
import { useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { t } from '../../../i18n';
import { PendingDialog } from '../PendingDialog';
import { DocumentPreview } from './DocumentPreview';
import { ProjectInfoForm, useProjectInfo } from './ProjectInfoForm';
import {
  createDocument, deleteDocument, documentPreview, documentTemplates, duplicateDocument, exportDocument, listDocuments, refreshDocument, updateDocument, useDocumentsDialog,
} from './documents';
import { groupInputs, isChanged, toComplete, type DocInput, type DocPreview, type DocSummary, type DocTemplate } from './docModel';

/** How long the typing rests before an input is saved. */
const SAVE_MS = 600;

/** The template's inputs, by group: a text area each, saved a moment after the typing stops (and when it is left). */
function InputsForm({ doc }: { doc: DocPreview }) {
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const pending = useRef<Record<string, string>>({});
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const id = doc.id;

  const flush = useCallback(() => {
    if (timer.current) { clearTimeout(timer.current); timer.current = null; }
    const sent = pending.current;
    if (Object.keys(sent).length === 0) return;
    pending.current = {};
    void updateDocument(id, { inputs: sent }).then(() => {
      // A draft goes once the engine has it (what was typed since stays).
      setDrafts((d) => { const n = { ...d }; for (const [k, v] of Object.entries(sent)) if (n[k] === v) delete n[k]; return n; });
    });
  }, [id]);
  // What is still typed is saved when the document changes or the dialog closes.
  useEffect(() => () => flush(), [flush]);
  useEffect(() => { setDrafts({}); }, [id]);

  const type = (key: string, v: string) => {
    setDrafts((d) => ({ ...d, [key]: v }));
    pending.current[key] = v;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(flush, SAVE_MS);
  };
  const reset = (i: DocInput) => {
    delete pending.current[i.key];
    setDrafts((d) => { const n = { ...d }; delete n[i.key]; return n; });
    void updateDocument(id, { inputs: { [i.key]: null } });
  };

  const groups = groupInputs(doc.inputs);
  if (groups.length === 0) return <p className="muted small" data-field="doc-inputs">This template has no texts to write: everything comes from the project.</p>;
  return (
    <div className="doc-inputs" data-field="doc-inputs">
      {groups.map((g) => (
        <section key={g.group}>
          {g.group && <h4 data-no-i18n="">{g.group}</h4>}
          {g.inputs.map((i) => {
            const value = drafts[i.key] ?? i.value ?? '';
            const changed = isChanged({ ...i, value });
            return (
              <label key={i.key} className="doc-input">
                <span className="doc-input-head">
                  <span className="small" data-no-i18n="">{i.label}</span>
                  {changed && i.default != null && <button type="button" className="link small" data-field={`doc-input-reset-${i.key}`} title="The template's text again" onClick={(e) => { e.preventDefault(); reset(i); }}><RotateCcw size={11} /> Reset to default</button>}
                </span>
                {i.multiline
                  ? <textarea value={value} rows={Math.min(8, Math.max(3, Math.ceil(value.length / 48)))} data-field={`input-${i.key}`} aria-label={i.label} onChange={(e) => type(i.key, e.target.value)} onBlur={flush} />
                  : <input value={value} data-field={`input-${i.key}`} aria-label={i.label} onChange={(e) => type(i.key, e.target.value)} onBlur={flush} />}
              </label>
            );
          })}
        </section>
      ))}
    </div>
  );
}

/** The pixels a paper mm takes in the preview: the page as wide as the middle column allows, within reason. */
function usePageScale(el: HTMLElement | null, pageW: number): number {
  const [k, setK] = useState(2.6);
  useEffect(() => {
    if (!el) return;
    const fit = () => setK(Math.max(1.6, Math.min(3.4, (el.clientWidth - 40) / Math.max(pageW, 1))));
    fit();
    const ro = new ResizeObserver(fit);
    ro.observe(el);
    return () => ro.disconnect();
  }, [el, pageW]);
  return k;
}

export function DocumentsDialog() {
  const version = useSession((s) => s.version);
  const chosen = useDocumentsDialog((s) => s.id);
  const [docs, setDocs] = useState<DocSummary[] | null>(null);
  const [templates, setTemplates] = useState<DocTemplate[]>([]);
  const [preview, setPreview] = useState<DocPreview | null>(null);
  const [tab, setTab] = useState<'inputs' | 'info'>('inputs');
  const [busy, setBusy] = useState<string | null>(null);
  const [centre, setCentre] = useState<HTMLDivElement | null>(null);
  const info = useProjectInfo();
  const close = () => useUi.setState({ dialog: null });
  const choose = (id: string | null) => useDocumentsDialog.setState({ id });

  useEffect(() => { void documentTemplates().then(setTemplates); }, []);
  useEffect(() => {
    let live = true;
    void listDocuments().then((r) => { if (live) setDocs(r); });
    return () => { live = false; };
  }, [version]);
  // The document shown: the one chosen while it is there, else the first.
  const current = docs?.find((d) => d.id === chosen) ?? docs?.[0] ?? null;
  const id = current?.id ?? null;
  useEffect(() => {
    if (!id) { setPreview(null); return; }
    let live = true;
    const tm = setTimeout(() => { void documentPreview(id).then((r) => { if (live) setPreview(r); }); }, 120);
    return () => { live = false; clearTimeout(tm); };
  }, [id, version]);
  const shown = preview && preview.id === id ? preview : null;
  const k = usePageScale(centre, shown?.page.w ?? 210);

  const missing = useMemo(() => (shown ? toComplete(shown.missing, shown.inputs, info?.fields ?? []) : []), [shown, info]);

  const make = async (template: string) => {
    const made = await createDocument(template);
    if (made) { choose(made); setTab('inputs'); }
  };
  const run = async (what: string, f: () => Promise<unknown>) => {
    setBusy(what);
    try { await f(); } finally { setBusy(null); }
  };
  const rename = () => {
    if (!current) return;
    const name = window.prompt(t('The document\'s name'), current.name)?.trim();
    if (name && name !== current.name) void updateDocument(current.id, { name });
  };
  const remove = () => {
    if (!current || !window.confirm(t('Delete this document? Undo brings it back.'))) return;
    void deleteDocument(current.id).then(() => choose(null));
  };
  const refresh = () => {
    if (!current || !window.confirm(t('Take the template\'s text and layout again? Your inputs are kept.'))) return;
    void refreshDocument(current.id);
  };
  const duplicate = async () => {
    if (!current) return;
    const made = await duplicateDocument(current.id);
    if (made) choose(made);
  };

  // A field to complete: its first place in the preview shown and marked, and the field to fill it focused.
  const goTo = (m: { key: string; where: 'inputs' | 'info' | 'model' }) => {
    const mark = centre?.querySelector(`.doc-sheet:not(.doc-measure) [data-missing="${CSS.escape(m.key)}"]`) as HTMLElement | null;
    if (mark) {
      mark.scrollIntoView({ block: 'center', behavior: 'smooth' });
      mark.classList.remove('flash');
      void mark.offsetWidth;
      mark.classList.add('flash');
    }
    if (m.where === 'model') return;
    setTab(m.where);
    const field = m.where === 'inputs' ? `input-${m.key.slice(6)}` : `info-${m.key.slice(8)}`;
    setTimeout(() => (document.querySelector(`.dialog.documents [data-field="${CSS.escape(field)}"]`) as HTMLElement | null)?.focus(), 50);
  };

  if (!docs) return <PendingDialog title="Documents" />;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog documents" role="dialog" aria-label="Documents" data-dialog="documents" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Documents</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body doc-body">
          <aside className="doc-side" data-field="doc-list">
            <h4>The project's documents</h4>
            {docs.length === 0 && <p className="muted small">No document yet: start one from a template below.</p>}
            <ul className="doc-docs">
              {docs.map((d) => (
                <li key={d.id} className={d.id === id ? 'current' : undefined}>
                  <button type="button" data-field={`doc-item-${d.id}`} onClick={() => choose(d.id)}>
                    <FileText size={13} />
                    <span className="doc-docs-text">
                      <span className="doc-docs-name" data-no-i18n="">{d.name}</span>
                      <span className="muted small"><span data-no-i18n="">{d.templateTitle ?? d.template ?? ''}</span>{d.revision ? <> · {d.revision}{d.status ? ` ${d.status}` : ''}</> : null}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
            <h4><FilePlus2 size={13} /> New from template</h4>
            <ul className="doc-templates">
              {templates.map((tp) => (
                <li key={tp.id}>
                  <button type="button" data-field={`doc-template-${tp.id}`} title="A new document from this template, filled from the project" onClick={() => void make(tp.id)}>
                    <span className="doc-docs-name" data-no-i18n="">{tp.title}</span>
                    <span className="muted small" data-no-i18n="">{tp.description}</span>
                  </button>
                </li>
              ))}
            </ul>
          </aside>
          <div className="doc-centre" ref={setCentre} data-field="doc-preview">
            {!current && <p className="muted doc-empty">Choose a template on the left: the document is filled from the project, and what it cannot fill is marked.</p>}
            {current && !shown && <p className="muted doc-empty">Reading…</p>}
            {shown && <DocumentPreview doc={shown} k={k} />}
          </div>
          <aside className="doc-side right">
            {current && shown && (
              <>
                <div className="tabs-inline" role="tablist">
                  <button type="button" role="tab" aria-selected={tab === 'inputs'} className={tab === 'inputs' ? 'on' : ''} data-field="doc-inputs-tab" onClick={() => setTab('inputs')}>Inputs</button>
                  <button type="button" role="tab" aria-selected={tab === 'info'} className={tab === 'info' ? 'on' : ''} data-field="doc-info-tab" onClick={() => setTab('info')}>Project information</button>
                </div>
                <div className="doc-tab">
                  {tab === 'inputs' ? <InputsForm doc={shown} /> : info ? <ProjectInfoForm info={info} /> : <p className="muted">Reading…</p>}
                </div>
                <div className="doc-missing-list" data-field="doc-missing">
                  <h4>To complete <span className="count">{missing.length}</span></h4>
                  {missing.length === 0
                    ? <p className="muted small">Every field is filled.</p>
                    : (
                      <ul>
                        {missing.map((m) => (
                          <li key={m.key}>
                            <button type="button" className="link small" data-field={`doc-missing-${m.key}`} onClick={() => goTo(m)}
                              title={m.where === 'inputs' ? 'Written in Inputs' : m.where === 'info' ? 'Written in Project information' : 'Comes from the model'}>
                              {m.kind === 'table' ? <span>Table</span> : m.kind === 'view' ? <span>View</span> : null}
                              <span data-no-i18n={m.where === 'inputs' || m.kind ? '' : undefined}>{m.label}</span>
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                </div>
              </>
            )}
          </aside>
        </div>
        <footer className="dialog-foot">
          {current && <>
            <button type="button" className="btn" disabled={busy != null} data-field="doc-export-docx" title="Word, with the project's pictures" onClick={() => void run('docx', () => exportDocument(current.id, 'docx'))}><FileDown size={13} /> {busy === 'docx' ? 'Exporting…' : 'Export DOCX'}</button>
            <button type="button" className="btn" disabled={busy != null} data-field="doc-export-odt" title="OpenDocument (LibreOffice), with the project's pictures" onClick={() => void run('odt', () => exportDocument(current.id, 'odt'))}><FileDown size={13} /> {busy === 'odt' ? 'Exporting…' : 'Export ODT'}</button>
            <button type="button" className="btn" disabled={busy != null} data-field="doc-export-pdf" title="The document as it prints" onClick={() => void run('pdf', () => exportDocument(current.id, 'pdf'))}><FileDown size={13} /> {busy === 'pdf' ? 'Exporting…' : 'Export PDF'}</button>
            <span className="grow" />
            <button type="button" className="btn" data-field="doc-refresh" title="The template's text and layout again, your inputs kept" onClick={refresh}><RefreshCw size={13} /> Update from template</button>
            <button type="button" className="btn" data-field="doc-rename" onClick={rename}><Pencil size={13} /> Rename</button>
            <button type="button" className="btn" data-field="doc-duplicate" onClick={() => void duplicate()}><Copy size={13} /> Duplicate</button>
            <button type="button" className="btn" data-field="doc-delete" onClick={remove}><Trash2 size={13} /> Delete</button>
          </>}
          {!current && <span className="grow" />}
          <button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}
