/**
 * `lapin.ui.ask`'s dialog (stage 4; decision 489): a script's question drawn by the window from its fields (ask.ts), never
 * from anything the script sends to run. It floats over the model without a backdrop, so an element field can take what
 * the user selects in the view meanwhile (Use selection). OK answers the values (checked first), Cancel or Escape
 * answers nothing; the script waits for it. What the script wrote (its title, labels, text, choices) is shown as it is.
 * Also here: a script tool's trust question (Run once / Trust / Cancel), for a project's or an installed script.
 */

import { useEffect, useState } from 'react';
import { ShieldAlert, SquareTerminal } from 'lucide-react';
import { useSession } from '../../engine/session';
import { collect, initialState, type AskQuestion, type FieldState } from '../../shell/askFields';
import { Field, useFacts } from '../../shell/AskField';
import { answer, answerToolTrust, useScriptTools } from './scriptTools';
import { trustQuestion, TRUST_WARNING } from './trust';

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
