/**
 * Ask Lapin (Help → Ask Lapin, F1): a question typed in your own words gets
 * the answer from the FAQ (help/faq.ts) in the interface's language, with a
 * button to where it is; a question it cannot answer goes to us as a report,
 * and its answer comes back here.
 */

import { useEffect, useRef, useState } from 'react';
import { MessageCircleQuestion, Send, X } from 'lucide-react';
import { useUi } from '../app/store';
import { activate, findTool } from '../platform/modules';
import { openStandards } from '../modules/core/standardsTab';
import { useLang } from '../i18n';
import { FAQ, ask, type Entry } from './faq';

interface Message { from: 'you' | 'lapin'; text?: string; entry?: Entry; related?: Entry[]; unknown?: string }

/** The questions offered before anything is asked. */
const STARTERS = ['units', 'copy-move', 'import-dwg-pdf', 'roof-gables', 'levels', 'sheets-pdf'];

export function AskDialog({ onClose, onReport }: { onClose: () => void; onReport: (question: string) => void }) {
  const ro = useLang((s) => s.lang) === 'ro';
  const [text, setText] = useState('');
  const [messages, setMessages] = useState<Message[]>([]);
  const end = useRef<HTMLDivElement>(null);
  useEffect(() => { end.current?.scrollIntoView({ block: 'end' }); }, [messages]);

  const title = (e: Entry) => (ro ? e.qRo[0] : e.q[0]);
  const show = (e: Entry, asked?: string) => setMessages((m) => [...m, { from: 'you', text: asked ?? title(e) }, { from: 'lapin', entry: e }]);
  const send = () => {
    const q = text.trim();
    if (!q) return;
    setText('');
    const hits = ask(q);
    setMessages((m) => [...m, { from: 'you', text: q }, hits.length
      ? { from: 'lapin', entry: hits[0].entry, related: hits.slice(1, 3).map((h) => h.entry) }
      : { from: 'lapin', unknown: q }]);
  };
  const run = (e: Entry) => {
    const a = e.action?.run;
    if (!a) return;
    if (a.kind === 'report') { onReport(''); return; }
    onClose();
    if (a.kind === 'tool') { const t = findTool(a.id); if (t) activate(t); }
    else if (a.kind === 'tab') useUi.setState({ tab: a.id });
    else if (a.kind === 'standards') openStandards('project');
    else useUi.setState({ dialog: a.id });
  };

  return (
    <div className="dialog-backdrop" onClick={onClose}>
      <div className="dialog ask-dialog" role="dialog" aria-label="Ask Lapin" data-no-i18n onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head">
          <span className="panel-title"><MessageCircleQuestion size={15} /> {ro ? 'Întreabă Lapin' : 'Ask Lapin'}</span>
          <button type="button" className="icon" onClick={onClose} title={ro ? 'Închide' : 'Close'}><X size={15} /></button>
        </header>
        <div className="dialog-body ask-body">
          <div className="ask-msg lapin">
            <p>{ro ? 'Scrie o întrebare cu cuvintele tale, de exemplu „cum schimb unitățile”. Sau alege una:' : 'Type a question in your own words, for example “how do I change the units”. Or pick one:'}</p>
            <div className="ask-chips">
              {STARTERS.map((id) => FAQ.find((e) => e.id === id)).filter((e): e is Entry => !!e).map((e) => (
                <button key={e.id} type="button" className="chip" onClick={() => show(e)}>{title(e)}</button>
              ))}
            </div>
          </div>
          {messages.map((m, i) => m.from === 'you' ? (
            <div key={i} className="ask-msg you">{m.text}</div>
          ) : m.entry ? (
            <div key={i} className="ask-msg lapin">
              <p className="ask-title">{title(m.entry)}</p>
              <p>{ro ? m.entry.aRo : m.entry.a}</p>
              {m.entry.action && <button type="button" className="btn compact" onClick={() => run(m.entry!)}>{ro ? m.entry.action.labelRo : m.entry.action.label}</button>}
              {(m.related?.length ?? 0) > 0 && (
                <div className="ask-chips"><span className="muted small">{ro ? 'Poate și:' : 'Or perhaps:'}</span>
                  {m.related!.map((e) => <button key={e.id} type="button" className="chip" onClick={() => show(e)}>{title(e)}</button>)}
                </div>
              )}
            </div>
          ) : (
            <div key={i} className="ask-msg lapin">
              <p>{ro ? 'Nu am încă un răspuns la asta. Trimite-ne întrebarea: îți răspundem și o adăugăm aici.' : "I don't have an answer to that yet. Send us the question: we'll answer it and add it here."}</p>
              <button type="button" className="btn compact" onClick={() => onReport(m.unknown ?? '')}>{ro ? 'Trimite întrebarea' : 'Send the question'}</button>
            </div>
          ))}
          <div ref={end} />
        </div>
        <footer className="dialog-foot ask-foot">
          <input className="grow" autoFocus value={text} placeholder={ro ? 'Întrebarea ta…' : 'Your question…'} aria-label={ro ? 'Întrebarea ta' : 'Your question'}
            onChange={(e) => setText(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') send(); }} />
          <button type="button" className="btn primary" onClick={send} disabled={!text.trim()}><Send size={13} /> {ro ? 'Întreabă' : 'Ask'}</button>
        </footer>
      </div>
    </div>
  );
}
