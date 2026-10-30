/**
 * Help → Messages (docs/decisions-pending.md 177): the reports sent with
 * "I'd like a reply", each with its thread and a box to answer; the settings
 * below (nickname, stop replies, forget me, use on another device).
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useLang } from '../i18n';
import {
  adoptCode, forgetMe, markSeen, refresh, sendMessage, setActive, setNickname, unreadOf, useReplies, validSecret,
  type InboxReport,
} from './replies';

const when = (at: string, lang: string) => { try { return new Date(at).toLocaleString(lang === 'en' ? undefined : lang, { dateStyle: 'medium', timeStyle: 'short' }); } catch { return at; } };

export function MessagesDialog({ onClose }: { onClose: () => void }) {
  const { settings, reports, loaded } = useReplies();
  const lang = useLang((s) => s.lang);
  const [chosen, setChosen] = useState<number | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  // Opened: read the inbox now (a choice of the user's, so even when replies are stopped).
  useEffect(() => {
    if (!settings) return;
    setBusy(true);
    refresh().then(() => setProblem(null), (e) => setProblem(e instanceof Error ? e.message : String(e))).finally(() => setBusy(false));
  }, [settings?.secret]); // eslint-disable-line react-hooks/exhaustive-deps

  const report = useMemo(() => reports.find((r) => r.number === chosen) ?? reports[0] ?? null, [reports, chosen]);
  useEffect(() => { if (report && unreadOf(report) > 0) void markSeen(report); }, [report]);

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Messages">
      <div className="dialog wide messages">
        <header className="dialog-head">
          <span className="panel-title">Messages</span>
          <button type="button" className="icon" onClick={onClose} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          {!settings && <p className="muted">No replies here: tick “I'd like a reply” when you report an issue, or use the code from another device below.</p>}
          {settings && loaded && !reports.length && <p className="muted">No reports with replies yet.</p>}
          {settings && !loaded && busy && <p className="muted">Loading…</p>}
          {problem && <p className="error-text" role="alert">{problem}</p>}
          {reports.length > 0 && (
            <div className="messages-split">
              <ul className="messages-list">
                {reports.map((r) => (
                  <li key={r.number}>
                    <button type="button" className={r.number === report?.number ? 'on' : ''} onClick={() => setChosen(r.number)}>
                      <span className="messages-title"><span data-no-i18n>#{r.number} {r.title}</span>{unreadOf(r) > 0 && <span className="badge" data-no-i18n>{unreadOf(r)}</span>}</span>
                      <span className="muted small"><span data-no-i18n>{when(r.receivedAt, lang)}</span> · <span data-i18n-ctx="report">{r.status === 'closed' ? 'Closed' : 'Open'}</span></span>
                    </button>
                  </li>
                ))}
              </ul>
              {report && <Thread report={report} lang={lang} />}
            </div>
          )}
          <div className="messages-settings">
            <button type="button" className="link" onClick={() => setSettingsOpen(!settingsOpen)}>{settingsOpen ? 'Hide the settings' : 'Settings'}</button>
            {settingsOpen && <ReplySettingsPanel />}
          </div>
        </div>
      </div>
    </div>
  );
}

function Thread({ report, lang }: { report: InboxReport; lang: string }) {
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const send = async () => {
    setBusy(true); setProblem(null);
    try { await sendMessage(report.number, text); setText(''); } catch (e) { setProblem(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  return (
    <div className="messages-thread">
      <div className="message user">
        <span className="muted small">You · <span data-no-i18n>{when(report.receivedAt, lang)}</span></span>
        <p data-no-i18n>{report.description}</p>
      </div>
      {report.messages.map((m, i) => (
        <div key={i} className={`message ${m.from}`}>
          <span className="muted small">{m.from === 'lapin' ? 'Lapin' : 'You'} · <span data-no-i18n>{when(m.at, lang)}</span></span>
          <p data-no-i18n>{m.text}</p>
        </div>
      ))}
      {!report.messages.length && <p className="muted small">No reply yet. You will see it here.</p>}
      <label className="field-line"><span className="eyebrow">Your answer</span><textarea rows={3} maxLength={4000} value={text} onChange={(e) => setText(e.target.value)} /></label>
      {report.status === 'closed' && <p className="muted small">This report is closed; answering opens it again.</p>}
      {problem && <p className="error-text" role="alert">{problem}</p>}
      <div className="row"><span className="grow" /><button type="button" className="btn primary" disabled={busy || !text.trim()} onClick={() => void send()}>{busy ? 'Sending…' : 'Send'}</button></div>
    </div>
  );
}

function ReplySettingsPanel() {
  const settings = useReplies((s) => s.settings);
  const [name, setName] = useState(settings?.nickname ?? '');
  const [code, setCode] = useState('');
  const [showCode, setShowCode] = useState(false);
  const [confirmForget, setConfirmForget] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);
  const run = async (f: () => Promise<unknown>, done?: string) => {
    setProblem(null); setNote(null);
    try { await f(); if (done) setNote(done); } catch (e) { setProblem(e instanceof Error ? e.message : String(e)); }
  };
  return (
    <div className="messages-settings-body">
      {settings && (
        <>
          <div className="row">
            <label className="field-line grow"><span className="eyebrow">Nickname</span><input value={name} maxLength={40} onChange={(e) => setName(e.target.value)} placeholder="How we greet you (optional)" /></label>
            <button type="button" className="btn" disabled={name.trim() === settings.nickname} onClick={() => void run(() => setNickname(name), 'Nickname saved.')}>Save</button>
          </div>
          <div className="row">
            {settings.active
              ? <button type="button" className="btn" onClick={() => { setActive(false); setNote('Lapin no longer asks for replies. Your key is kept: start again any time.'); }}>Stop replies</button>
              : <button type="button" className="btn" onClick={() => { setActive(true); setNote(null); }}>Start replies again</button>}
            <span className="muted small">Stopping keeps your key and your reports; Lapin just stops asking for news.</span>
          </div>
          <div className="row">
            {!confirmForget
              ? <button type="button" className="btn" onClick={() => setConfirmForget(true)}>Forget me…</button>
              : <>
                <button type="button" className="btn danger" onClick={() => void run(forgetMe)}>Yes, forget me</button>
                <button type="button" className="btn" onClick={() => setConfirmForget(false)}>Cancel</button>
              </>}
          </div>
          {confirmForget && <p className="muted small">Forget me removes your nickname and your key from all your reports on our server, and deletes the key from this device. The reports and the messages in them stay, with no name attached; you can no longer read or answer them.</p>}
          <h4>Use on another device</h4>
          <p className="muted small">This code is your key: whoever has it reads and answers your messages. Keep it like a password.</p>
          {showCode
            ? <div className="row"><input className="grow" readOnly value={settings.secret} onFocus={(e) => e.target.select()} data-no-i18n />
              <button type="button" className="btn" onClick={() => void navigator.clipboard?.writeText(settings.secret).then(() => setNote('Code copied.'), () => undefined)}>Copy</button></div>
            : <button type="button" className="btn" onClick={() => setShowCode(true)}>Show my code</button>}
        </>
      )}
      <div className="row">
        <label className="field-line grow"><span className="eyebrow">A code from another device</span><input value={code} onChange={(e) => setCode(e.target.value)} placeholder="Paste the code" /></label>
        <button type="button" className="btn" disabled={!validSecret(code.trim())} onClick={() => void run(async () => { if (await adoptCode(code)) setCode(''); }, 'This device now uses that key.')}>Use this code</button>
      </div>
      {settings && code.trim() && <p className="muted small">Using another code replaces this device's key: keep a copy of it first if you still need it.</p>}
      {note && <p className="muted small" role="status">{note}</p>}
      {problem && <p className="error-text" role="alert">{problem}</p>}
    </div>
  );
}
