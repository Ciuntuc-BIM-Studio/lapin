/**
 * Ask Me (the rabbit beside the ?) and the help menu (?): Ask Lapin (the FAQ, also on F1), the user guide
 * (lapin.ciuntucbimstudio.ro/docs, docs/user), report an issue (it
 * reaches Ciuntuc BIM Studio with the app's last errors attached; a reply
 * only when asked for, read in Messages: replies.ts), the
 * community on Skool, the site, updates (the desktop app), About Lapin
 * (open source, MIT, helpware), the version.
 */

import { useEffect, useRef, useState } from 'react';
import { HelpCircle, X } from 'lucide-react';
import { notify } from '../app/store';
import { useSession } from '../engine/session';
import { RECENT, openLink, reportParams, sendReport } from './support';
import { contactFor, useReplies } from './replies';
import { MessagesDialog } from './MessagesDialog';
import { isGuest } from '../engine/share';
import { isDesktop } from '../engine/files';
import { checkForUpdates } from '../app/updates';
import { LANGUAGES, setLang, useLang } from '../i18n';
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
  // A reply is asked for, never assumed: off by default, the nickname remembered from the last time.
  const [reply, setReply] = useState(false);
  const [nickname, setNickname] = useState(() => useReplies.getState().settings?.nickname ?? '');
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const send = async () => {
    setBusy(true); setProblem(null);
    try {
      const contact = await contactFor(reply, nickname);
      const number = await sendReport(reportParams({ kind, title, description, expected, email, screenshot }, RECENT, `${window.innerWidth}×${window.innerHeight} @${window.devicePixelRatio}`, contact));
      notify(reply ? `Thank you: report #${number} is on its way. Replies will appear in Help → Messages.` : `Thank you: report #${number} is on its way.`);
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
            <input type="file" accept="image/png,image/jpeg" onChange={(e) => {
              const f = e.target.files?.[0];
              setProblem(null);
              // A file the browser cannot decode (not really a PNG or JPEG) is said here, not left as an error.
              if (f) void shrink(f).then(setScreenshot, () => { setScreenshot(null); setProblem(`${f.name} could not be read as a picture: attach a PNG or JPEG screenshot (or send the report without one).`); });
              else setScreenshot(null);
            }} /></label>
          {!isGuest() && <label className="check-line"><input type="checkbox" checked={reply} onChange={(e) => setReply(e.target.checked)} /><span>I'd like a reply</span></label>}
          {reply && <>
            <label className="field-line"><span className="eyebrow">Nickname (optional)</span><input value={nickname} maxLength={40} onChange={(e) => setNickname(e.target.value)} placeholder="How we greet you" /></label>
            <p className="muted small reply-note">We keep your nickname and the report. Replies show only in Lapin (Help → Messages); delete them any time; kept 12 months after the last message.</p>
          </>}
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
  const [about, setAbout] = useState(false);
  const [messages, setMessages] = useState(false);
  // Messages: offered once this device has a key (a reply was asked for once), never to a guest of a shared session.
  const hasKey = useReplies((s) => s.settings !== null) && !isGuest();
  const unread = useReplies((s) => s.unread);
  const info = useSession((s) => s.info);
  const lang = useLang((s) => s.lang);
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
    <button type="button" className="ask-me" title="Ask Lapin how to do something (F1)" onClick={() => setAsking(true)}><BunnyIcon size={17} /><span data-no-i18n>Ask Me</span></button>
    <div className="help-menu" ref={ref}>
       <button type="button" className="icon" title="Help: ask Lapin (F1), report an issue, community" onClick={() => setOpen(!open)}><HelpCircle size={15} />{hasKey && unread > 0 && <span className="badge" data-no-i18n>{unread}</span>}</button>
      {open && (
        <div className="menu-pop" role="menu">
          <button type="button" role="menuitem" onClick={() => { setOpen(false); setAsking(true); }}>Ask Lapin…<kbd>F1</kbd></button>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); openLink('guide'); }}>User guide</button>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); setReport(true); }}>Report an issue…</button>
          {hasKey && <button type="button" role="menuitem" onClick={() => { setOpen(false); setMessages(true); }}><span>Messages…</span>{unread > 0 && <span className="badge" data-no-i18n>{unread}</span>}</button>}
          <button type="button" role="menuitem" onClick={() => { setOpen(false); openLink('community'); }}>Community (Skool)</button>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); openLink('site'); }}>ciuntucbimstudio.ro</button>
          {isDesktop() && <button type="button" role="menuitem" onClick={() => { setOpen(false); void checkForUpdates(true); }}>Check for updates…</button>}
          <div className="menu-langs" role="group" aria-label="Language" data-no-i18n>
            {LANGUAGES.map((l) => (
              <button key={l.id} type="button" role="menuitemradio" aria-checked={lang === l.id} className={lang === l.id ? 'on' : ''}
                onClick={() => { setOpen(false); setLang(l.id); }}>{l.name}</button>
            ))}
          </div>
          <button type="button" role="menuitem" onClick={() => { setOpen(false); setAbout(true); }}>About Lapin…</button>
          <span className="muted small menu-note">Lapin {info?.engineVersion ?? ''}</span>
        </div>
      )}
      {asking && <AskDialog onClose={() => setAsking(false)}
        onReport={(q) => { setAsking(false); setReport(q ? { kind: 'question', title: q.length > 80 ? `${q.slice(0, 77)}…` : q, description: q } : true); }} />}
      {messages && <MessagesDialog onClose={() => setMessages(false)} />}
      {about && <AboutDialog version={info?.engineVersion} onClose={() => setAbout(false)} />}
      {report && <ReportDialog onClose={() => setReport(false)} start={report === true ? undefined : report} />}
    </div>
    </>
  );
}

/**
 * About Lapin: its version, that it is open source (MIT) with a link to the
 * code, and helpware: if Lapin helps, we encourage (never require) helping
 * anyone who needs it, in any way; Lapin asks nothing and takes no donations.
 */
function AboutDialog({ version, onClose }: { version?: string; onClose: () => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="About Lapin">
      <div className="dialog about">
        <header className="dialog-head">
          <span className="panel-title">About Lapin</span>
          <button type="button" className="icon" onClick={onClose} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="about-name"><BunnyIcon size={22} /><b>Lapin</b> <span className="muted" data-no-i18n>{version ?? ''}</span></p>
          <p>A classic BIM modeller built natively on IFC, by Ciuntuc BIM Studio.</p>
          <p>Lapin is free and open source, under the MIT licence. <button type="button" className="link" onClick={() => openLink('source')}>Source code on GitHub</button></p>
          <h4>Helpware</h4>
          <p>If Lapin helps you in any way, we encourage you, without obliging you, to help anyone who needs help, in any way you can: your time, your skills, a hand to someone nearby, a gift to people or a cause you trust.</p>
          <p className="muted small">Lapin asks nothing for itself and takes no donations. This is a request, not a condition of the licence.</p>
        </div>
      </div>
    </div>
  );
}
