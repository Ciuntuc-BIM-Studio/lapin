/**
 * `lapin.ui.ask`'s dialog (stage 4; decision 489): a script's question drawn by the window from its fields (ask.ts), never
 * from anything the script sends to run. It floats over the model without a backdrop, so an element field can take what
 * the user selects in the view meanwhile (Use selection). OK answers the values (checked first), Cancel or Escape
 * answers nothing; the script waits for it. What the script wrote (its title, labels, text, choices) is shown as it is.
 * Also here: a script tool's trust question (Run once / Trust / Cancel), for a project's or an installed script.
 */

import { useEffect, useMemo, useState } from 'react';
import { MousePointerClick, ShieldAlert, SquareTerminal } from 'lucide-react';
import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { collect, elementsOf, fromSelection, initialState, storeysOf, typesOf, type AskField, type AskQuestion, type FieldState, type ModelFacts } from './ask';
import { answer, answerToolTrust, useScriptTools } from './scriptTools';
import { trustQuestion, TRUST_WARNING } from './trust';

/** What the fields know of the model (also a palette's, PaletteWindow.tsx). */
export function useFacts(): ModelFacts {
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const selection = useUi((s) => s.selection);
  const storey = useUi((s) => s.storeyId);
  return useMemo(() => ({
    nodes: nodes as ModelFacts['nodes'],
    selection: selected.length ? selected : selection ? [selection] : [],
    storey,
  }), [nodes, selected, selection, storey]);
}

/** One field drawn by the window (a question's, a palette's: `prefix` keeps their ids apart). */
export function Field({ f, value, set, facts, wrong, prefix = 'ask' }: { f: AskField; value: FieldState; set: (v: FieldState) => void; facts: ModelFacts; wrong: boolean; prefix?: string }) {
  const id = `${prefix}-${f.key}`;
  const label = <label htmlFor={id} className="script-ask-label" data-no-i18n>{f.label}</label>;
  const cls = wrong ? ' wrong' : '';
  switch (f.kind) {
    case 'number':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <span className="script-ask-number">
            <input id={id} type="text" inputMode="decimal" value={String(value)} onChange={(e) => set(e.target.value)} spellCheck={false} />
            {f.unit && <span className="muted" data-no-i18n>{f.unit}</span>}
          </span>
        </div>
      );
    case 'text':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          {f.multiline
            ? <textarea id={id} rows={3} value={String(value)} onChange={(e) => set(e.target.value)} />
            : <input id={id} type="text" value={String(value)} onChange={(e) => set(e.target.value)} />}
        </div>
      );
    case 'bool':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>
          <span />
          <label className="check"><input id={id} type="checkbox" checked={value === true} onChange={(e) => set(e.target.checked)} /> <span data-no-i18n>{f.label}</span></label>
        </div>
      );
    case 'choice':
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <select id={id} value={String(value)} onChange={(e) => set(Number(e.target.value))} data-no-i18n>
            {f.choices!.map((c, i) => <option key={i} value={i}>{c.label}</option>)}
          </select>
        </div>
      );
    case 'storey':
    case 'type': {
      const list = f.kind === 'storey' ? storeysOf(facts) : typesOf(f.cls, facts);
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <select id={id} value={String(value)} onChange={(e) => set(e.target.value)}>
            {(f.kind === 'type' && f.required === false || !list.length) && <option value="">—</option>}
            {list.map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name || x.id}</option>)}
          </select>
        </div>
      );
    }
    case 'element': {
      const ids = Array.isArray(value) ? value : [];
      const list = elementsOf(f, facts);
      const take = () => set(fromSelection(f, facts));
      const named = ids.map((i) => facts.nodes.get(i)?.name || i);
      return (
        <div className={`script-ask-row${cls}`} data-ask-field={f.key}>{label}
          <span className="script-ask-element">
            {!f.multiple && list.length > 0
              ? (
                <select id={id} value={ids[0] ?? ''} onChange={(e) => set(e.target.value ? [e.target.value] : [])}>
                  <option value="">—</option>
                  {list.map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name}</option>)}
                </select>
              )
              : <span id={id} className="script-ask-picked" title={named.join(', ')}>
                  {ids.length === 0 ? <span className="muted">Nothing selected</span>
                    : ids.length === 1 ? <span data-no-i18n>{named[0]}</span>
                      : <span>{`${ids.length} elements`}</span>}
                </span>}
            <button type="button" className="btn" onClick={take} data-action="ask-use-selection" title="Select in the model, then take the selection">
              <MousePointerClick size={13} /> Use selection</button>
          </span>
        </div>
      );
    }
  }
}

function Question({ q }: { q: AskQuestion }) {
  const facts = useFacts();
  const [state, setState] = useState<Record<string, FieldState>>(() => initialState(q.fields, facts));
  const [wrong, setWrong] = useState<{ key: string; error: string } | null>(null);
  const ok = () => {
    const r = collect(q.fields, state);
    if ('error' in r) { setWrong(r); return; }
    void answer(q.ask, r.values);
  };
  const cancel = () => void answer(q.ask, null);
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); cancel(); } };
    window.addEventListener('keydown', key, true);
    return () => window.removeEventListener('keydown', key, true);
  });
  return (
    <section className="dialog script-ask" role="dialog" aria-label={q.title} data-dialog="script-ask"
      onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter' && !(e.target instanceof HTMLTextAreaElement)) { e.preventDefault(); ok(); } }}>
      <header className="dialog-head"><span className="panel-title"><SquareTerminal size={14} /> <span data-no-i18n>{q.title}</span></span></header>
      <div className="dialog-body script-ask-body">
        {q.text && <p className="small" data-no-i18n>{q.text}</p>}
        {q.fields.map((f) => (
          <Field key={f.key} f={f} value={state[f.key]} facts={facts} wrong={wrong?.key === f.key}
            set={(v) => { setState((s) => ({ ...s, [f.key]: v })); if (wrong?.key === f.key) setWrong(null); }} />
        ))}
        {wrong && <p className="error-text small" data-field="ask-error">{wrong.error}</p>}
        <p className="muted small">Asked by a script. It waits for your answer.</p>
      </div>
      <footer className="dialog-foot">
        <span className="grow" />
        <button type="button" className="btn" data-action="ask-cancel" onClick={cancel}>Cancel</button>
        <button type="button" className="btn primary" data-action="ask-ok" onClick={ok}>{q.ok ? <span data-no-i18n>{q.ok}</span> : 'OK'}</button>
      </footer>
    </section>
  );
}

function ToolTrust() {
  const a = useScriptTools((s) => s.trustAsk);
  const path = useSession((s) => s.doc?.path ?? null);
  if (!a) return null;
  const project = path ? path.split(/[\\/]/).pop() ?? null : null;
  const question = a.tool.source.kind === 'extension' ? `„${a.tool.source.label}” was installed from Lapin Extensions.` : trustQuestion(project, a.tool.source.label);
  return (
    <div className="dialog-backdrop" role="presentation">
      <section className="dialog script-tool-trust" role="alertdialog" aria-label="Trust this script?" data-dialog="script-tool-trust">
        <header className="dialog-head"><span className="panel-title"><ShieldAlert size={14} /> <span>Trust this script?</span></span></header>
        <div className="dialog-body">
          <p><b>{question}</b></p>
          <p className="small">{TRUST_WARNING}</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" data-action="script-trust-cancel" onClick={() => void answerToolTrust('cancel')}>Cancel</button>
          <button type="button" className="btn" data-action="script-trust-once" onClick={() => void answerToolTrust('once')}>Run once</button>
          <button type="button" className="btn primary" data-action="script-trust" onClick={() => void answerToolTrust('trust')}>Trust</button>
        </footer>
      </section>
    </div>
  );
}

/** The first question waiting (keyed by it, so the next starts afresh), and a tool's trust question. */
export function ScriptDialogs() {
  const q = useScriptTools((s) => s.questions[0] ?? null);
  return (
    <>
      {q && <Question key={q.ask} q={q} />}
      <ToolTrust />
    </>
  );
}
