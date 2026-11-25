/**
 * The selected reference document (docs/plan-cde-references.md §5): its metadata form (the fields of its type included), its files
 * (save a copy, detach, attach more, a preview), what it supersedes or is superseded by and what it relates to, Suggest (the
 * number, dates and issuer read from the text, each with its evidence, for the user to confirm), Withdraw with a reason, and
 * Delete. A certificate also has the Checklist tab.
 */

import { confirmAction, promptText } from '../../../shell/prompts';
import { useRef, useState } from 'react';
import { Ban, Download, Paperclip, Replace, Sparkles, Trash2, Unlink } from 'lucide-react';
import { notify } from '../../../app/store';
import { saveFile } from '../../../engine/files';
import { formatSize } from '../format';
import { today } from './expiry';
import {
  attachFiles, badgeOf, deleteRef, detachFile, fileKind, fileOf, flattenFolders, nameIn, readFiles, splitList, suggestFor, supersedeRef, updateRef, withdrawRef,
  type FolderTree, type RefDoc, type RefFile, type Suggestion, type TypeList,
} from './references';
import { StatusBadge } from './StatusBadge';
import { ReferencePreview } from './ReferencePreview';
import { ReferenceChecklist } from './ReferenceChecklist';

type Suggested = 'type' | 'number' | 'date' | 'issuer' | 'validUntil';
const SUGGESTED: [Suggested, string][] = [['type', 'Type'], ['number', 'Number'], ['date', 'Date'], ['issuer', 'Issuer'], ['validUntil', 'Valid until']];

export function ReferenceDetail({ doc, docs, types, tree, lang, phases, guest, onSelect, onChanged }: {
  doc: RefDoc; docs: RefDoc[]; types: TypeList; tree: FolderTree; lang: string; phases: string[]; guest: boolean; onSelect: (id: string) => void; onChanged: () => void;
}) {
  const type = types.types.find((x) => x.id === doc.type);
  const [tab, setTab] = useState<'details' | 'checklist'>('details');
  const [open, setOpen] = useState<string | null>(doc.files[0]?.name ?? null);
  const [suggest, setSuggest] = useState<{ s: Suggestion; on: Set<string> } | null>(null);
  const [busy, setBusy] = useState(false);
  const attach = useRef<HTMLInputElement>(null);
  const folders = flattenFolders(tree.folders);
  const b = badgeOf(doc, today());
  const live = doc.status !== 'withdrawn' && doc.status !== 'superseded';
  const save = (patch: Record<string, unknown>) => { void updateRef(doc.id, patch).then(onChanged); };
  const titleOf = (id: string) => docs.find((d) => d.id === id)?.title ?? id;
  const readonly = guest;
  const openFile = doc.files.find((f) => f.name === open) ?? null;

  const text = (key: 'title' | 'issuer' | 'number' | 'discipline', label: string, list?: string[]) => (
    <label className="refs-field"><span className="muted small">{label}</span>
      <input key={`${doc.id}:${key}:${doc[key] ?? ''}`} defaultValue={doc[key] ?? ''} disabled={readonly} list={list ? `refs-${key}` : undefined} aria-label={label}
        onBlur={(e) => { if (e.target.value.trim() !== (doc[key] ?? '') && (key !== 'title' || e.target.value.trim())) save({ [key]: e.target.value.trim() }); }} />
      {list && <datalist id={`refs-${key}`}>{list.map((x) => <option key={x} value={x} />)}</datalist>}
    </label>
  );
  const date = (key: 'date' | 'validFrom' | 'validUntil', label: string) => (
    <label className="refs-field"><span className="muted small">{label}</span>
      <input type="date" key={`${doc.id}:${key}:${doc[key] ?? ''}`} defaultValue={doc[key] ?? ''} disabled={readonly} aria-label={label} onChange={(e) => { if (e.target.value !== (doc[key] ?? '')) save({ [key]: e.target.value }); }} />
    </label>
  );

  const ask = async () => {
    setBusy(true);
    try {
      const s = await suggestFor(doc.id, open ?? undefined);
      if (!s) { notify('No suggestion could be read from this file.'); return; }
      const on = new Set<string>(SUGGESTED.map(([k]) => k as string).filter((k) => s[k as Suggested] != null && String(s[k as Suggested]) !== String(doc[k as keyof RefDoc] ?? '')));
      setSuggest({ s, on });
    } finally { setBusy(false); }
  };
  const applySuggestion = () => {
    if (!suggest) return;
    const patch: Record<string, unknown> = {};
    for (const [k] of SUGGESTED) if (suggest.on.has(k)) patch[k] = suggest.s[k];
    setSuggest(null);
    if (Object.keys(patch).length) save(patch);
  };
  /** What a suggested value was read from: the validity's words come from the months when no date was written. */
  const evidenceOf = (k: Suggested) => suggest?.s.evidence?.[k] ?? (k === 'validUntil' ? suggest?.s.evidence?.validityMonths : undefined);

  const saveCopy = async (f: RefFile) => {
    const r = await fileOf(doc.id, f.name);
    if (!r) { notify('The file could not be read.'); return; }
    await saveFile(Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0)), r.name, r.mime ?? 'application/octet-stream');
  };
  const detach = (f: RefFile) => {
    if (confirmAction('Take the file {name} off this document? The document and its other files stay.', { name: f.name })) void detachFile(doc.id, f.name).then(onChanged);
  };
  const attachPicked = async (files: File[]) => {
    if (!files.length) return;
    const r = await attachFiles(doc.id, await readFiles(files));
    r?.warnings.forEach((w) => notify(w));
    onChanged();
  };
  const withdraw = () => {
    const reason = promptText('Why is this document withdrawn?')?.trim();
    if (reason) void withdrawRef(doc.id, reason).then(onChanged);
  };
  const remove = () => {
    if (confirmAction('Delete {title} for good? Its record and its files leave the project; withdrawing keeps them. This is for a file added by mistake.', { title: doc.title })) void deleteRef(doc.id).then(onChanged);
  };
  const others = docs.filter((d) => d.id !== doc.id && d.status !== 'withdrawn');
  const relates = (doc.relatesTo ?? []).filter((id) => docs.some((d) => d.id === id));
  const found = SUGGESTED.filter(([k]) => suggest?.s[k] != null);

  return (
    <div className="refs-doc" data-testid="refs-detail">
      {type?.checklist && (
        <div className="chips" role="tablist" aria-label="Document">
          <button type="button" role="tab" aria-selected={tab === 'details'} className={`chip${tab === 'details' ? ' on' : ''}`} data-testid="refs-tab-details" onClick={() => setTab('details')}>Details</button>
          <button type="button" role="tab" aria-selected={tab === 'checklist'} className={`chip${tab === 'checklist' ? ' on' : ''}`} data-testid="refs-tab-checklist" onClick={() => setTab('checklist')}>Checklist</button>
        </div>
      )}
      {tab === 'checklist' && type?.checklist
        ? <ReferenceChecklist doc={doc} docs={docs} tree={tree} guest={guest} onSelect={onSelect} onChanged={onChanged} />
        : (
          <>
            <div className="row"><StatusBadge label={b.label} tone={b.tone} expiry={b.expiry} />
              {doc.status === 'withdrawn' && doc.reason && <span className="muted small">{doc.reason}</span>}
              <span className="grow" />
              {!guest && live && <button type="button" className="btn small" data-testid="refs-suggest" disabled={busy || doc.files.length === 0} title="Read the number, the dates and the issuer from the file's text; you confirm them" onClick={() => void ask()}><Sparkles size={12} /> Suggest</button>}
            </div>

            {suggest && (
              <div className="refs-suggest" data-testid="refs-suggestion">
                {found.length === 0 && <p className="muted small">{suggest.s.warning ?? 'Nothing found in the text.'}</p>}
                {found.map(([k, label]) => (
                  <label key={k} className="refs-sug">
                    <input type="checkbox" checked={suggest.on.has(k)} onChange={(e) => setSuggest((p) => { if (!p) return p; const on = new Set(p.on); if (e.target.checked) on.add(k); else on.delete(k); return { ...p, on }; })} />
                    <span className="small"><b>{label}</b>: {k === 'type' ? nameIn(types.types.find((x) => x.id === suggest.s.type)?.name, lang) || String(suggest.s.type) : String(suggest.s[k])}
                      {k === 'validUntil' && suggest.s.validUntilIndicative ? <span className="muted"> · <span>indicative: the type's usual validity</span></span> : null}</span>
                    {evidenceOf(k) && <q className="muted small refs-evidence">{evidenceOf(k)}</q>}
                  </label>
                ))}
                <div className="row"><span className="grow" /><button type="button" className="btn small" onClick={() => setSuggest(null)}>Cancel</button><button type="button" className="btn small primary" onClick={applySuggestion}>Apply</button></div>
              </div>
            )}

            <div className="refs-form">
              {text('title', 'Title')}
              <label className="refs-field"><span className="muted small">Type</span>
                <select value={doc.type ?? ''} disabled={readonly} aria-label="Type" onChange={(e) => save({ type: e.target.value })}>
                  <option value="" />
                  {types.types.map((x) => <option key={x.id} value={x.id}>{nameIn(x.name, lang)}</option>)}
                </select>
              </label>
              {text('issuer', 'Issuer', types.issuers)}
              {text('number', 'Number')}
              <div className="refs-two">{date('date', 'Date')}{date('validFrom', 'Valid from')}</div>
              {date('validUntil', 'Valid until')}
              <label className="refs-field"><span className="muted small">Phases</span>
                <input key={`${doc.id}:phase:${(doc.phase ?? []).join()}`} defaultValue={(doc.phase ?? []).join(', ')} disabled={readonly} list="refs-phases" aria-label="Phases" placeholder="SF, DTAC, PT"
                  onBlur={(e) => { if (splitList(e.target.value).join() !== (doc.phase ?? []).join()) save({ phase: splitList(e.target.value) }); }} />
                <datalist id="refs-phases">{phases.map((p) => <option key={p} value={p} />)}</datalist>
              </label>
              {text('discipline', 'Role (discipline)', ['A', 'S', 'E', 'M', 'P', 'C'])}
              <label className="refs-field"><span className="muted small">Folder</span>
                <select value={doc.folder ?? ''} disabled={readonly} aria-label="Folder" onChange={(e) => save({ folder: e.target.value })}>
                  <option value="" />
                  {folders.map((f) => <option key={f.id} value={f.id}>{' '.repeat(f.depth)}{f.text}</option>)}
                </select>
              </label>
              {live && (
                <label className="refs-field"><span className="muted small">Status</span>
                  <select value={doc.status === 'requested' ? 'requested' : 'received'} disabled={readonly} aria-label="Status" onChange={(e) => save({ status: e.target.value })}>
                    <option value="requested">Requested</option>
                    <option value="received">Received</option>
                  </select>
                </label>
              )}
              {(type?.fields ?? []).map((f) => (
                <label key={f.key} className="refs-field"><span className="muted small">{nameIn(f.label, lang)}</span>
                  <input key={`${doc.id}:f:${f.key}:${doc.fields?.[f.key] ?? ''}`} defaultValue={doc.fields?.[f.key] ?? ''} disabled={readonly} aria-label={nameIn(f.label, lang)}
                    onBlur={(e) => { if (e.target.value !== (doc.fields?.[f.key] ?? '')) save({ fields: { [f.key]: e.target.value } }); }} />
                </label>
              ))}
            </div>

            <h4>Files</h4>
            {doc.files.length === 0 && <p className="muted small">{doc.status === 'requested' ? 'Asked for, not received: attach the file when it comes.' : 'No file.'}</p>}
            <ul className="refs-files">
              {doc.files.map((f) => (
                <li key={f.name} className={open === f.name ? 'on' : ''}>
                  <button type="button" className="link" onClick={() => setOpen(f.name)} title="Preview"><code>{f.name}</code></button>
                  <span className="muted small"> {formatSize(f.size)}</span>
                  {!f.held && <span className="muted small" title="Above the size limit kept in the project file: its hash is kept, the file is on the server or in the project folder"> · <span>not kept in the project file</span></span>}
                  <span className="grow" />
                  <button type="button" className="icon mini" title="Save a copy…" onClick={() => void saveCopy(f)}><Download size={12} /></button>
                  {!readonly && <button type="button" className="icon mini" title="Take the file off this document" onClick={() => detach(f)}><Unlink size={12} /></button>}
                </li>
              ))}
            </ul>
            {!readonly && live && (
              <>
                <button type="button" className="btn small" onClick={() => attach.current?.click()}><Paperclip size={12} /> Attach files…</button>
                <input ref={attach} type="file" multiple hidden onChange={(e) => { const f = [...(e.target.files ?? [])]; e.target.value = ''; void attachPicked(f); }} />
              </>
            )}
            {openFile && <ReferencePreview key={`${doc.id}:${openFile.name}`} doc={doc} file={openFile} kind={fileKind(openFile.name, openFile.mime)} />}

            <h4>Links</h4>
            {doc.supersededBy && <p className="small"><span>Superseded by</span> <button type="button" className="link" onClick={() => onSelect(doc.supersededBy!)}>{titleOf(doc.supersededBy)}</button></p>}
            {(doc.supersedes ?? []).map((id) => <p key={id} className="small"><span>Supersedes</span> <button type="button" className="link" onClick={() => onSelect(id)}>{titleOf(id)}</button></p>)}
            {!readonly && live && (
              <label className="refs-field"><span className="muted small"><Replace size={11} /> <span>Superseded by…</span></span>
                <select value="" aria-label="Superseded by" onChange={(e) => { if (e.target.value) void supersedeRef(doc.id, e.target.value).then(onChanged); }}>
                  <option value="">—</option>
                  {others.filter((d) => d.status !== 'superseded').map((d) => <option key={d.id} value={d.id}>{d.title}</option>)}
                </select>
              </label>
            )}
            <div className="refs-related">
              <span className="muted small">Related documents</span>
              {relates.map((id) => (
                <span key={id} className="refs-chip"><button type="button" className="link" onClick={() => onSelect(id)}>{titleOf(id)}</button>
                  {!readonly && <button type="button" className="icon mini" title="Remove the link" onClick={() => save({ relatesTo: (doc.relatesTo ?? []).filter((x) => x !== id) })}><Unlink size={10} /></button>}</span>
              ))}
              {!readonly && (
                <select value="" aria-label="Relates to" onChange={(e) => { if (e.target.value) save({ relatesTo: [...(doc.relatesTo ?? []), e.target.value] }); }}>
                  <option value="">+ …</option>
                  {others.filter((d) => !(doc.relatesTo ?? []).includes(d.id)).map((d) => <option key={d.id} value={d.id}>{d.title}</option>)}
                </select>
              )}
            </div>

            {!readonly && (
              <div className="row refs-end">
                {live && <button type="button" className="btn small" data-testid="refs-withdraw" onClick={withdraw}><Ban size={12} /> Withdraw…</button>}
                <span className="grow" />
                <button type="button" className="btn small danger" data-testid="refs-delete" onClick={remove}><Trash2 size={12} /> Delete…</button>
              </div>
            )}
            {doc.addedAt && <p className="muted small"><span>Added</span> {doc.addedAt.slice(0, 10)}{doc.addedBy ? ` · ${doc.addedBy}` : ''}</p>}
          </>
        )}
    </div>
  );
}
