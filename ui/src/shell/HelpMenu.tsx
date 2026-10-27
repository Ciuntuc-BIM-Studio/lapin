/**
 * Ask Me (the rabbit beside the ?) and the help menu (?): Ask Lapin (the FAQ, also on F1), the user guide
 * (lapin.ciuntucbimstudio.ro/docs, docs/user), report an issue (it
 * reaches Ciuntuc BIM Studio with the app's last errors attached), the
 * community on Skool, the site, updates (the desktop app), the version.
 */

import { useEffect, useRef, useState } from 'react';
import { HelpCircle, X } from 'lucide-react';
import { notify } from '../app/store';
import { useSession } from '../engine/session';
import { RECENT, openLink, sendReport } from './support';
import { isDesktop } from '../engine/files';
import { checkForUpdates } from '../app/updates';
import { setLang, useLang } from '../i18n';
import { AskDialog } from '../help/AskDialog';
import { BunnyIcon } from '../icons';


/** An image shrunk to at most 1600 px on its longer side, as a JPEG data URL. */
async function shrink(file: File): Promise<string> {
  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, 1600 / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(bitmap.width * scale);
  canvas.height = Math.round(bitmap.height * scale);
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  return canvas.toDataURL('image/jpeg', 0.85);
}

/** What a report starts with: a question Ask Lapin could not answer comes in as one. */
interface ReportStart { kind: 'bug' | 'idea' | 'question'; title: string; description: string }

function ReportDialog({ onClose, start }: { onClose: () => void; start?: ReportStart }) {
  const [kind, setKind] = useState<'bug' | 'idea' | 'question'>(start?.kind ?? 'bug');
  const [title, setTitle] = useState(start?.title ?? '');
  const [description, setDescription] = useState(start?.description ?? '');
  const [expected, setExpected] = useState('');
  const [email, setEmail] = useState('');
  const [screenshot, setScreenshot] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const send = async () => {
    setBusy(true); setProblem(null);
    try {
      const number = await sendReport({
        kind, title, description, expected: expected || undefined, email: email || undefined, consent: Boolean(email), screenshot: screenshot ?? undefined,
        website: '', errors: [...RECENT], screen: `${window.innerWidth}×${window.innerHeight} @${window.devicePixelRatio}`,
      });
      notify(`Thank you: report #${number} is on its way.`);
      onClose();
    } catch (e) {
      setProblem(e instanceof Error ? e.message : String(e));
    } finally { setBusy(false); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Report an issue">
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">Report an issue</span>
          <button type="button" className="icon" onClick={onClose} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body report">
          <div className="tabs-inline">
            {(['bug', 'idea', 'question'] as const).map((k) => <button key={k} type="button" className={`tab-chip${kind === k ? ' on' : ''}`} onClick={() => setKind(k)}>{k === 'bug' ? 'Something is wrong' : k === 'idea' ? 'An idea' : 'A question'}</button>)}
          </div>
          <label className="field-line"><span className="eyebrow">Title</span><input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="In a few words" /></label>
          <label className="field-line"><span className="eyebrow">What happened</span><textarea rows={5} value={description} onChange={(e) => setDescription(e.target.value)} /></label>
          {kind === 'bug' && <label className="field-line"><span className="eyebrow">What you expected</span><textarea rows={2} value={expected} onChange={(e) => setExpected(e.target.value)} /></label>}
          <label className="field-line"><span className="eyebrow">E-mail (optional)</span><input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></label>
          <label className="field-line"><span className="eyebrow">Screenshot (optional)</span>
            <input type="file" accept="image/png,image/jpeg" onChange={(e) => { const f = e.target.files?.[0]; if (f) void shrink(f).then(setScreenshot); else setScreenshot(null); }} /></label>
          <p className="muted small">The app's version and its last {RECENT.length} error message(s) go with the report.</p>
          {problem && <p className="error-text" role="alert">{problem}</p>}
          <div className="row">
            <span className="grow" />
            <button type="button" className="btn" onClick={onClose}>Cancel</button>
            <button type="button" className="btn primary" disabled={busy || !title.trim() || !description.trim()} onClick={() => void send()}>{busy ? 'Sending…' : 'Send'}</button>
          </div>
        </div>
      </div>
    </div>
  );
}

export function HelpMenu() {
  const [open, setOpen] = useState(false);
  const [report, setReport] = useState<ReportStart | true | false>(false);
  const [asking, setAsking] = useState(false);
  const info = useSession((s) => s.info);
  // F1: Ask Lapin, from anywhere.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'F1') { e.preventDefault(); setAsking(true); } };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const away = (e: MouseEvent) => { if (!ref.current?.contains(e.target as Node)) setOpen(false); };
    window.addEventListener('mousedown', away);
    return () => window.removeEventListener('mousedown', away);
  }, [open]);
  return (
    <>
    <button type="button" className="ask-me" data-no-i18n title="Ask Lapin how to do something (F1)" onClick={() => setAsking(true)}><BunnyIcon size={17} />Ask Me</button>
    <div className="help-menu" ref={ref}>
       <button type="button" className="icon" title="Help: ask Lapin (F1), report an issue, community" onClick={() => setOpen(!open)}><HelpCircle size={15} /></button>
      {open && (
        <div className="menu-pop" role="menu">
          <button type="button" role="menuitem" onClick={() => { setOpen(false); setAsking(true); }}>Ask Lapin…<kbd>F1</kbd></button>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); openLink('guide'); }}>User guide</button>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); setReport(true); }}>Report an issue…</button>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); openLink('community'); }}>Community (Skool)</button>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); openLink('site'); }}>ciuntucbimstudio.ro</button>
          {isDesktop() && <button type="button" role="menuitem" onClick={() => { setOpen(false); void checkForUpdates(true); }}>Check for updates…</button>}
          <button type="button" role="menuitem" data-no-i18n onClick={() => { setOpen(false); setLang(useLang.getState().lang === 'ro' ? 'en' : 'ro'); }}>
            {useLang.getState().lang === 'ro' ? 'Interfața: română → English' : 'Interface: English → română'}
          </button>
          <span className="muted small menu-note">Lapin {info?.engineVersion ?? ''}</span>
        </div>
      )}
      {asking && <AskDialog onClose={() => setAsking(false)}
        onReport={(q) => { setAsking(false); setReport(q ? { kind: 'question', title: q.length > 80 ? `${q.slice(0, 77)}…` : q, description: q } : true); }} />}
      {report && <ReportDialog onClose={() => setReport(false)} start={report === true ? undefined : report} />}
    </div>
    </>
  );
}

