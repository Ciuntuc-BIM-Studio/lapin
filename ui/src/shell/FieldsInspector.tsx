/**
 * The Parameters inspector (forum topic 84): every field an element's tags, live texts and formulas can read — its own (what
 * every element has, its attributes, what its module works out, its quantities, its parameters, the standard properties) and
 * its scopes' (type., storey., building., site., project., group., assembly., host., room., fromRoom., toRoom.) — each with its
 * token, its value now and what it is, searched by what is typed. A click writes the field where a formula or a text is being
 * edited (`onPick`), else copies `{token}` to the clipboard. The engine lists them (`fields.available`) as the model is now.
 */

import { useEffect, useMemo, useState } from 'react';
import { Braces, Copy, Search, X } from 'lucide-react';
import { create } from 'zustand';
import { query, useSession } from '../engine/session';
import { notify, useUi } from '../app/store';
import { filterGroups, pieceFor, type AvailableFields, type InsertMode } from './fieldsModel';

/** The fields the engine lists for an element, kept fresh as the model changes. */
export function useAvailableFields(id: string | null): AvailableFields | null {
  const version = useSession((s) => s.version);
  const [data, setData] = useState<AvailableFields | null>(null);
  useEffect(() => {
    if (!id) { setData(null); return; }
    let live = true;
    void query<AvailableFields>('fields.available', { id }).then((r) => { if (live) setData(r ?? null); });
    return () => { live = false; };
  }, [id, version]);
  return data;
}

async function copy(piece: string) {
  try { await navigator.clipboard.writeText(piece); notify('Copied to the clipboard.'); }
  catch { notify('The clipboard could not be written.'); }
}

export function FieldsInspector({ id, mode = 'text', onPick, autoFocus = true }: {
  id: string; mode?: InsertMode; onPick?: (piece: string) => void; autoFocus?: boolean;
}) {
  const data = useAvailableFields(id);
  const [typed, setTyped] = useState('');
  const groups = useMemo(() => filterGroups(data?.groups ?? [], typed), [data, typed]);
  return (
    <div className="fields-inspector" data-fields-inspector={id}>
      <label className="fields-inspector-search">
        <Search size={12} />
        <input autoFocus={autoFocus} placeholder="Search the fields" value={typed} data-field="fields-search" onChange={(e) => setTyped(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Escape' && typed) { e.stopPropagation(); setTyped(''); } }} />
      </label>
      {!data && <p className="muted small">Reading the fields…</p>}
      {data && groups.length === 0 && <p className="muted small">No field matches.</p>}
      {groups.map((g) => (
        <details key={g.id} open className="fields-inspector-group" data-fields-group={g.id}>
          <summary>
            <span className="eyebrow">{g.label}</span>
            {g.scope && g.entityName && <span className="muted small" data-no-i18n> · {g.entityName}</span>}
          </summary>
          <ul>
            {g.fields.map((f) => {
              const piece = pieceFor(f, mode);
              return (
                <li key={f.token}>
                  <button type="button" className="fields-inspector-row" data-field-token={f.token} title={f.description}
                    onClick={() => (onPick ? onPick(piece) : void copy(piece))}>
                    <code className="mono" data-no-i18n>{piece}</code>
                    <span className="fields-inspector-value" data-no-i18n>{f.value ?? ''}</span>
                    {onPick ? <Braces size={11} className="muted" /> : <Copy size={11} className="muted" />}
                  </button>
                </li>
              );
            })}
          </ul>
        </details>
      ))}
      {data && data.functions.length > 0 && (
        <p className="muted small">Functions a formula may call: <span data-no-i18n>{data.functions.join(', ')}</span></p>
      )}
    </div>
  );
}

/** The element the inspector dialog shows (the selection's when none was named). */
export const useInspectorTarget = create<{ id: string | null }>(() => ({ id: null }));

/** Opens the inspector dialog on an element (or a type). */
export function openFieldsInspector(id: string) {
  useInspectorTarget.setState({ id });
  useUi.setState({ dialog: 'fieldsInspector' });
}

/** The inspector as a dialog, from Properties: a click copies the field's token. */
export function FieldsInspectorDialog() {
  const named = useInspectorTarget((s) => s.id);
  const selection = useUi((s) => s.selection);
  const id = named ?? selection;
  const name = useSession((s) => (id ? s.nodes.get(id)?.name : undefined));
  const close = () => { useInspectorTarget.setState({ id: null }); useUi.setState({ dialog: null }); };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog fields-inspector-dialog" role="dialog" aria-label="Parameters inspector" onClick={(e) => e.stopPropagation()}
        onKeyDown={(e) => { if (e.key === 'Escape') close(); }}>
        <header className="dialog-head"><span className="panel-title">Parameters inspector{name ? <span className="muted small" data-no-i18n> · {name}</span> : null}</span><button type="button" className="icon" title="Close" onClick={close}><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">What its tags, texts and formulas can read: click a field to copy it, then paste it into a tag, a title block or a formula.</p>
          {id ? <FieldsInspector id={id} /> : <p className="muted">Select an element first.</p>}
        </div>
      </div>
    </div>
  );
}
