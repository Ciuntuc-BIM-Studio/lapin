/**
 * An urbanism certificate's checklist (docs/plan-cde-references.md §3.3): the permits it asks for, ticked from the indicative list
 * (never a list of what the law requires) or typed, each linked to the document that answers it, and each item's state: missing,
 * requested (a requested document stands for it), received, or expired. "Request" makes the requested document and links it.
 */

import { useEffect, useState } from 'react';
import { FilePlus2, Link2, Plus, Trash2, Unlink } from 'lucide-react';
import { useLang } from '../../../i18n';
import { useRefAdapter } from './adapter';
import {
  answerCandidates, CHECKLIST_STATES, nameIn, requiresOf,
  type Checklist, type ChecklistItem, type FolderTree, type RefDoc,
} from './references';

export function ReferenceChecklist({ doc, docs, tree, guest, onSelect, onChanged }: {
  doc: RefDoc; docs: RefDoc[]; tree: FolderTree; guest: boolean; onSelect: (id: string) => void; onChanged: () => void;
}) {
  const ui = useLang((s) => s.lang);
  const adapter = useRefAdapter();
  const [list, setList] = useState<Checklist | null>(null);
  const [typed, setTyped] = useState('');
  // Read again whenever the documents change (a linked one received, expired, withdrawn).
  const stamp = docs.map((d) => `${d.id}:${d.status}:${d.number ?? ''}:${d.validUntil ?? ''}`).join('|') + (doc.requires ?? []).map((r) => `${r.key}:${r.document ?? ''}`).join('|');
  useEffect(() => { let live = true; void adapter.checklist(doc.id).then((r) => { if (live) setList(r); }); return () => { live = false; }; }, [doc.id, stamp, adapter]);
  void tree;

  if (!list) return <p className="muted small">Reading…</p>;
  const write = (items: ChecklistItem[]) => { void adapter.update(doc.id, { requires: requiresOf(items) }).then(onChanged); };
  const link = (item: ChecklistItem, document?: string) => write(list.items.map((i) => (i.key === item.key ? { ...i, document } : i)));
  const add = (key: string, label: ChecklistItem['label']) => write([...list.items, { key, label, text: '', state: 'missing' }]);
  const addTyped = () => {
    const text = typed.trim();
    if (!text) return;
    let key = `custom-${text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'item'}`;
    while (list.items.some((i) => i.key === key)) key += '-2';
    setTyped('');
    add(key, { en: text, ro: text });
  };
  const request = async (item: ChecklistItem) => {
    const option = list.options.find((o) => o.key === item.key) ?? undefined;
    const r = await adapter.add({ title: item.text || nameIn(item.label, ui), ...(option?.folder ? { folder: option.folder } : {}), relatesTo: [doc.id] });
    if (r) link(item, r.id);
  };
  const fine = (item: ChecklistItem) => [item.number && `${item.number}`, item.date, item.issuer, item.validUntil && `→ ${item.validUntil}`].filter(Boolean).join(' · ');

  return (
    <div className="refs-check" data-testid="refs-checklist">
      <p className="muted small">The permits and studies this certificate asks for. The list is indicative and yours to change: tick what it names, add the others, and link each to the document that answers it.</p>
      <p className="small" data-testid="refs-check-summary">
        {list.items.length === 0 ? <span className="muted">Nothing ticked yet.</span> : list.complete ? <b>Every item is received.</b> : <><b>{list.missing}</b> <span>still missing or expired</span></>}
      </p>
      <ul className="refs-items">
        {list.items.map((item) => {
          const st = CHECKLIST_STATES[item.state];
          const answered = item.document ? docs.find((d) => d.id === item.document) : undefined;
          return (
            <li key={item.key} data-item={item.key} data-state={item.state}>
              <div className="row">
                <span className={`refs-badge ${st.tone}`}>{st.label}</span>
                <span className="grow">{item.text || nameIn(item.label, ui)}</span>
                {!guest && <button type="button" className="icon mini" title="Take this item off the list" onClick={() => write(list.items.filter((i) => i.key !== item.key))}><Trash2 size={11} /></button>}
              </div>
              <div className="row small">
                <Link2 size={11} />
                {answered
                  ? <button type="button" className="link" onClick={() => onSelect(answered.id)}>{answered.title}</button>
                  : <span className="muted">No document linked</span>}
                {fine(item) && <span className="muted"> · {fine(item)}</span>}
                <span className="grow" />
                {!guest && (
                  <select value={item.document ?? ''} aria-label="Answered by" onChange={(e) => link(item, e.target.value || undefined)}>
                    <option value="">{item.document ? '— (unlink)' : 'Link a document…'}</option>
                    {answerCandidates(docs, doc.id, list.options.find((o) => o.key === item.key)?.folder).map((d) => <option key={d.id} value={d.id}>{d.title}</option>)}
                  </select>
                )}
                {!guest && item.document && <button type="button" className="icon mini" title="Unlink the document" onClick={() => link(item, undefined)}><Unlink size={11} /></button>}
                {!guest && !item.document && <button type="button" className="btn small" data-testid="refs-request" title="Make a requested document for this item and link it" onClick={() => void request(item)}><FilePlus2 size={11} /> Request</button>}
              </div>
            </li>
          );
        })}
      </ul>
      {!guest && (
        <>
          <h4>Add to the list</h4>
          <div className="refs-options">
            {list.options.map((o) => <button type="button" key={o.key} className="chip pick" data-option={o.key} onClick={() => add(o.key, o.label)}><Plus size={10} /> {o.text ?? nameIn(o.label, ui)}</button>)}
          </div>
          <div className="row">
            <input value={typed} placeholder="Another permit or study" aria-label="Another permit or study" onChange={(e) => setTyped(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') addTyped(); }} />
            <button type="button" className="btn small" disabled={!typed.trim()} onClick={addTyped}>Add</button>
          </div>
        </>
      )}
    </div>
  );
}
