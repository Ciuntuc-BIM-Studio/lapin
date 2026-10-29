/**
 * The Assistant panel (docs/plan-assistant.md), the desktop's only: a chat
 * with a local model (Ollama) that reads the project through a few tools,
 * makes short reports, and proposes property values the user reviews and
 * applies here. What the model writes is shown as it is (not translated).
 */

import { Fragment, useEffect, useRef, useState } from 'react';
import { Bot, Check, ChevronRight, Download, MessageSquarePlus, RefreshCw, Send, Settings2, Square, TriangleAlert, Wrench } from 'lucide-react';
import { applyChanges, newChat, rejectChanges, send, stop, toggleAll, toggleChange, useAssistant, type ChatEntry } from './store';
import { checkStatus, defaultModel, fetchTransport, loopbackUrl, saveSettings, shellTransport, transportFor, useAssistantSettings, type OllamaStatus } from './ollama';
import { tableCsv, type Table } from './tools';
import { cleanText } from './loop';
import { Markdown } from './Markdown';
import type { ChangeRow, Scalar } from './changeset';
import { isDesktop } from '../engine/files';
import { notify, useUi } from '../app/store';
import { t, useLang } from '../i18n';

const EXAMPLES = [
  'How many walls are there per storey, and their total length?',
  'Doors wider than 1 m: give them FireRating EI30.',
  'Which doors have no fire rating?',
];

function useStatus(): [OllamaStatus | null, () => void] {
  const { url, model } = useAssistantSettings();
  const [status, setStatus] = useState<OllamaStatus | null>(null);
  const check = () => {
    const t = transportFor(location.origin, isDesktop()) === 'shell' ? shellTransport() : fetchTransport();
    void checkStatus(t, url, model).then((s) => {
      setStatus(s);
      // No model chosen yet, or one no longer installed: the best one there is.
      if (s.running && (!model || !s.models.includes(model))) { const m = defaultModel(s.models); if (m && m !== model) saveSettings({ model: m }); }
    });
  };
  useEffect(check, [url, model]);
  return [status, check];
}

function Settings({ status, recheck }: { status: OllamaStatus | null; recheck: () => void }) {
  const { url, model } = useAssistantSettings();
  const [address, setAddress] = useState(url);
  const bad = loopbackUrl(address) === null;
  return (
    <div className="assistant-settings pad">
      <label className="assistant-field">
        <span>Ollama address</span>
        <input value={address} onChange={(e) => setAddress(e.target.value)} spellCheck={false}
          onBlur={() => { const u = loopbackUrl(address); if (u) { setAddress(u); saveSettings({ url: u }); } }}
          onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
      </label>
      {bad && <p className="assistant-warn">Only Ollama on this computer: http://127.0.0.1 and its port.</p>}
      <label className="assistant-field">
        <span>Model</span>
        <select value={model ?? ''} onChange={(e) => saveSettings({ model: e.target.value || null })} disabled={!status?.models.length}>
          {!status?.models.length && <option value="">—</option>}
          {status?.models.map((m) => <option key={m} value={m}>{m}</option>)}
        </select>
      </label>
      <p className="muted small">qwen3-coder:30b is the best tested (fast, reliable with tools); gemma4:12b runs on less memory. Install one with: ollama pull qwen3-coder:30b</p>
      <div className="assistant-status-row"><Status status={status} /><button type="button" className="icon mini" title="Check again" onClick={recheck}><RefreshCw size={12} /></button></div>
    </div>
  );
}

function Status({ status }: { status: OllamaStatus | null }) {
  if (!status) return <span className="muted small">Looking for Ollama…</span>;
  if (!status.running) return <span className="assistant-warn">Ollama is not running: start it with `ollama serve` or the Ollama app.</span>;
  if (!status.models.length) return <span className="assistant-warn">Ollama has no model: install one with `ollama pull qwen3-coder:30b`.</span>;
  if (status.tools === false) return <span className="assistant-warn">This model cannot use tools: choose another.</span>;
  return <span className="assistant-ok small">Ollama is running</span>;
}

function saveCsv(t: Table): void {
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([tableCsv(t)], { type: 'text/csv' }));
  a.download = `${t.title.replace(/[\\/:*?"<>|]+/g, '_') || 'report'}.csv`;
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 10_000);
}

const PREVIEW_ROWS = 12;

function TableCard({ table }: { table: Table }) {
  const [all, setAll] = useState(false);
  const rows = all ? table.rows : table.rows.slice(0, PREVIEW_ROWS);
  // The id column is for the model; the user reads names.
  const cols = table.columns.map((c, i) => [c, i] as const).filter(([c]) => c !== 'id');
  return (
    <div className="assistant-table">
      <div className="assistant-table-head">
        <span className="small">{table.title}</span>
        <span className="muted small">{table.rows.length} row(s)</span>
        <button type="button" className="icon mini" title="Export CSV" onClick={() => saveCsv(table)}><Download size={12} /></button>
      </div>
      <div className="assistant-table-scroll" data-no-i18n>
        <table>
          <thead><tr>{cols.map(([c]) => <th key={c}>{c}</th>)}</tr></thead>
          <tbody>{rows.map((r, i) => <tr key={i}>{cols.map(([c, j]) => <td key={c}>{r[j] === null ? '' : String(r[j])}</td>)}</tr>)}</tbody>
        </table>
      </div>
      {table.rows.length > PREVIEW_ROWS && <button type="button" className="link small" onClick={() => setAll(!all)}>{all ? 'Fewer rows' : 'All rows'}</button>}
    </div>
  );
}

function Entry({ e }: { e: ChatEntry }) {
  if (e.role === 'user') return <div className="assistant-msg user" data-no-i18n>{e.text}</div>;
  return (
    <div className="assistant-msg">
      {e.tools.length > 0 && (
        <ul className="assistant-tools">
          {e.tools.map((t, i) => <li key={i} className={t.error ? 'err' : undefined} title={`${t.name} ${t.args ?? ''}`}><Wrench size={11} /> {t.summary}</li>)}
        </ul>
      )}
      {e.text && <div className="assistant-text" data-no-i18n><Markdown text={cleanText(e.text)} /></div>}
      {e.tables.map((t, i) => <TableCard key={i} table={t} />)}
      {e.error && <p className="assistant-warn"><TriangleAlert size={12} /> {e.error}</p>}
      {e.seconds != null && <p className="muted small assistant-meta">{e.speed ? `${Math.round(e.speed)} tok/s · ` : ''}{e.seconds.toFixed(1)} s</p>}
    </div>
  );
}

const show = (v: Scalar | null) => (v === null ? '—' : typeof v === 'boolean' ? (v ? 'yes' : 'no') : String(v));

function Changeset({ rows }: { rows: ChangeRow[] }) {
  const accepted = rows.filter((r) => r.accepted).length;
  const select = (id: string) => useUi.setState({ selection: id, selected: [id] });
  return (
    <div className="assistant-changes" data-testid="assistant-changes">
      <div className="assistant-table-head">
        <span className="small strong">Proposed changes</span>
        <span className="muted small">{`${accepted} of ${rows.length} ticked`}</span>
      </div>
      <div className="assistant-table-scroll">
        <table>
          <thead><tr>
            <th><input type="checkbox" aria-label="Select all" checked={accepted === rows.length} onChange={(e) => toggleAll(e.target.checked)} /></th>
            <th>Element</th><th>Property</th>
          </tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.key}>
                <td><input type="checkbox" checked={r.accepted} onChange={() => toggleChange(r.key)} /></td>
                <td data-no-i18n><button type="button" className="link" title="Select it" onClick={() => select(r.id)}>{r.element}</button></td>
                <td data-no-i18n className="assistant-change">
                  <span className="muted small">{r.pset ?? 'Lapin_Parameters'}</span>
                  <span>{r.name}: <span className="muted">{show(r.old)}</span> <ChevronRight size={10} /> <strong>{show(r.value)}</strong></span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="assistant-actions">
        <button type="button" className="btn primary" disabled={!accepted} onClick={() => void applyChanges().then((ok) => { if (ok) notify(`${accepted} value(s) applied: one step to undo.`); })}><Check size={13} />Apply</button>
        <button type="button" className="btn" onClick={rejectChanges}>Reject</button>
      </div>
    </div>
  );
}

export function AssistantPanel() {
  const { entries, busy, changes } = useAssistant();
  useLang((s) => s.lang); // the placeholder (a textarea's, which the translator leaves) follows the language
  const model = useAssistantSettings((s) => s.model);
  const [status, recheck] = useStatus();
  const [settings, setSettings] = useState(false);
  const [text, setText] = useState('');
  const list = useRef<HTMLDivElement>(null);
  useEffect(() => { list.current?.scrollTo({ top: list.current.scrollHeight }); }, [entries, changes.length]);
  const ready = !!status?.running && !!model && status.models.includes(model);
  const ask = (q: string) => { if (!ready || busy) return; setText(''); void send(q); };
  // An example goes in the language the interface shows it in, so the answer comes in it too.
  const example = (q: string) => ask(t(q));
  return (
    <aside className="panel assistant" aria-label="Assistant" data-testid="assistant-panel">
      <div className="panel-head assistant-head">
        <span className="eyebrow">Assistant</span>
        <span className="panel-title" data-no-i18n>{model ?? '—'}</span>
        <span className="assistant-head-tools">
          <button type="button" className="icon mini" title="New chat" onClick={newChat} disabled={!entries.length}><MessageSquarePlus size={13} /></button>
          <button type="button" className={`icon mini${settings ? ' on' : ''}`} title="Assistant settings" onClick={() => setSettings(!settings)}><Settings2 size={13} /></button>
        </span>
      </div>
      {(settings || !ready) && <Settings status={status} recheck={recheck} />}
      <div className="assistant-list" ref={list}>
        {entries.length === 0 && (
          <div className="assistant-intro pad">
            <p className="muted small"><Bot size={13} /> A local model on this computer reads the project, answers and makes short reports. It proposes property values for you to apply; it never changes the geometry and never deletes anything.</p>
            {EXAMPLES.map((q) => <button type="button" key={q} className="assistant-example" disabled={!ready} onClick={() => example(q)}>{q}</button>)}
          </div>
        )}
        {entries.map((e, i) => <Fragment key={i}><Entry e={e} /></Fragment>)}
        {busy && entries[entries.length - 1]?.text === '' && <p className="muted small pad assistant-thinking">Thinking…</p>}
        {changes.length > 0 && <Changeset rows={changes} />}
      </div>
      <form className="assistant-input" onSubmit={(e) => { e.preventDefault(); ask(text); }}>
        <textarea value={text} rows={2} placeholder={t(ready ? 'Ask about the project' : 'Ollama is needed: see the settings above')} disabled={!ready}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); ask(text); } e.stopPropagation(); }} />
        {busy
          ? <button type="button" className="icon" title="Stop" onClick={stop}><Square size={14} /></button>
          : <button type="submit" className="icon" title="Send" disabled={!ready || !text.trim()}><Send size={14} /></button>}
      </form>
    </aside>
  );
}

export default AssistantPanel;
