/**
 * The Script panel (View → Script, with Preferences → Developer extras on; decision 489): Python in Lapin.
 * Left, Mine: the user's own scripts (plain .py files in their folder). Middle, the editor (CodeMirror 6) with Run,
 * Stop and Restart; each Run is one undo step named after the script, and nothing of a run that fails or is stopped
 * is kept. Right, the console: what scripts print, their errors with their line, and a line to type Python in (Enter
 * runs it, Shift+Enter a new line, Up and Down its history). Python is the user's own (3.11 or newer), on the
 * desktop; in the browser the panel says that Python runs in the desktop app.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { ExternalLink, FilePlus, Play, RotateCcw, Save, Square, SquareTerminal, Trash2, X, Eraser } from 'lucide-react';
import { engineInBrowser } from '../../engine/session';
import { CodeEditor } from './CodeEditor';
import { namespaces, scriptName, walk, type Completion } from './logic';
import { clearConsole, closePanel, deleteScript, dirty, follow, newScript, openScript, refreshStatus, restart, reveal, run, saveScript, setHeight, stop, useScripting } from './store';

function Status() {
  const status = useScripting((s) => s.status);
  const running = useScripting((s) => s.runningName);
  if (running) return <span className="script-status running" data-field="script-status"><span>Running…</span> <b data-no-i18n>{running}</b></span>;
  if (!status) return <span className="script-status muted">Looking for Python…</span>;
  if (status.ok) return <span className="script-status muted" title={status.python ?? ''} data-field="script-status">Python <span data-no-i18n>{status.version}</span></span>;
  return <span className="script-status error-text" data-field="script-status">{status.message}</span>;
}

function Library() {
  const files = useScripting((s) => s.files);
  const file = useScripting((s) => s.file);
  return (
    <div className="script-library" data-field="script-library">
      <div className="script-library-head"><span className="panel-title">Mine</span>
        <button type="button" className="icon" title="New script" data-action="script-new" onClick={newScript}><FilePlus size={14} /></button></div>
      <div className="script-files">
        {files.length === 0 && <p className="muted small">Saved scripts show here: plain .py files in your folder.</p>}
        {files.map((f) => (
          <button type="button" key={f.name} className={`script-file${f.name === file ? ' on' : ''}`} data-script={f.name} title={f.name} onClick={() => void openScript(f.name)}>
            <span data-no-i18n>{f.name.replace(/\.py$/, '')}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

function Console() {
  const lines = useScripting((s) => s.lines);
  const running = useScripting((s) => s.running != null);
  const [draft, setDraft] = useState('');
  const out = useRef<HTMLDivElement>(null);
  useEffect(() => { out.current?.scrollTo({ top: out.current.scrollHeight }); }, [lines]);
  const key = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      if (running || !draft.trim()) return;
      void run(draft, true);
      setDraft('');
      return;
    }
    // Up on the first line and Down on the last walk the history.
    const t = e.currentTarget;
    if ((e.key === 'ArrowUp' && !t.value.slice(0, t.selectionStart).includes('\n')) || (e.key === 'ArrowDown' && !t.value.slice(t.selectionEnd).includes('\n'))) {
      const r = walk(useScripting.getState().history, e.key === 'ArrowUp' ? -1 : 1, draft);
      if (r.text === draft && r.history === useScripting.getState().history) return;
      e.preventDefault();
      useScripting.setState({ history: r.history });
      setDraft(r.text);
    }
  };
  return (
    <div className="script-console">
      <div className="script-console-head"><span className="panel-title">Console</span>
        <button type="button" className="icon" title="Clear the console" onClick={clearConsole}><Eraser size={14} /></button></div>
      <div className="script-output" ref={out} data-field="script-output" data-no-i18n>
        {lines.map((l, i) => <pre key={i} className={`script-line ${l.kind}`}>{l.kind === 'in' ? `>>> ${l.text.replace(/\n/g, '\n... ')}` : l.text}</pre>)}
      </div>
      <div className="script-input">
        <span className="script-prompt" aria-hidden>{'>>>'}</span>
        <textarea rows={Math.min(6, draft.split('\n').length)} value={draft} spellCheck={false} data-field="script-console-input"
          placeholder={running ? 'A script is running…' : 'Python: lapin.model.walls'} aria-label="Python console"
          onChange={(e) => setDraft(e.target.value)} onKeyDown={key} />
      </div>
    </div>
  );
}

function Editor() {
  const s = useScripting();
  const running = s.running != null;
  const catalogue = s.catalogue;
  const spaces = useMemo(() => (catalogue ? namespaces(catalogue) : new Map<string, Completion[]>()), [catalogue]);
  const spacesRef = useRef(spaces);
  spacesRef.current = spaces;
  const [name, setName] = useState((s.file ?? '').replace(/\.py$/, ''));
  useEffect(() => { setName((s.file ?? '').replace(/\.py$/, '')); }, [s.file]);
  const save = async () => {
    const as = scriptName(name);
    if (!as) { useScripting.setState({ lines: [...s.lines, { kind: 'err', text: 'Give the script a name first.' }] }); return; }
    await saveScript(as);
  };
  return (
    <div className="script-main">
      <div className="script-toolbar">
        <input type="text" className="script-name" value={name} placeholder="Untitled" spellCheck={false} data-field="script-name" aria-label="Script name"
          onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') void save(); }} />
        <span className="muted small">{dirty(s) ? '•' : ''}</span>
        <button type="button" className="btn primary" data-action="script-run" disabled={running} onClick={() => void run(s.code, false)} title="Run (Ctrl+Enter): one undo step">
          <Play size={13} /> Run</button>
        <button type="button" className="btn" data-action="script-stop" disabled={!running} onClick={() => void stop()} title="Stop: nothing the script did is kept">
          <Square size={13} /> Stop</button>
        <button type="button" className="btn" data-action="script-restart" onClick={() => void restart()} title="Restart Python: the console’s names are forgotten">
          <RotateCcw size={13} /> Restart</button>
        <span className="grow" />
        <button type="button" className="icon" data-action="script-save" title="Save in Mine" onClick={() => void save()}><Save size={14} /></button>
        <button type="button" className="icon" data-action="script-reveal" title="Open in external editor" disabled={!s.file} onClick={() => void reveal()}><ExternalLink size={14} /></button>
        <button type="button" className="icon" data-action="script-delete" title="Delete from Mine" disabled={!s.file} onClick={() => void deleteScript()}><Trash2 size={14} /></button>
      </div>
      <CodeEditor value={s.code} onChange={(code) => useScripting.setState({ code })} onRun={() => { if (useScripting.getState().running == null) void run(useScripting.getState().code, false); }}
        line={s.errorLine} spaces={() => spacesRef.current} label="Python script" />
    </div>
  );
}

export function ScriptPanel() {
  const height = useScripting((s) => s.height);
  // Typing in the panel is the panel's: the app's keys (a tool's letters, Delete), listened to on the window, wait outside it.
  // Stopped as React's own event (at the app's root, after the panel's handlers ran), not on the panel's element, which would hide the keys from React.
  const keep = (e: React.KeyboardEvent) => {
    e.stopPropagation();
    if (e.key === 'Escape' && !(e.target as HTMLElement)?.closest?.('.cm-editor, textarea, input')) closePanel();
  };
  // The file edited in an external editor comes back into the panel.
  useEffect(() => {
    if (engineInBrowser) return;
    const t = window.setInterval(() => void follow(), 2000);
    const back = () => { void follow(); void refreshStatus(); };
    window.addEventListener('focus', back);
    return () => { window.clearInterval(t); window.removeEventListener('focus', back); };
  }, []);
  const drag = (e: React.PointerEvent) => {
    e.preventDefault();
    const y0 = e.clientY, h0 = height;
    const move = (m: PointerEvent) => setHeight(h0 + (y0 - m.clientY));
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };
  return (
    <section className="script-panel" onKeyDown={keep} style={{ height }} data-panel="script" data-i18n-ctx="script" aria-label="Script">
      <div className="script-resize" onPointerDown={drag} title="Drag to resize" />
      <header className="script-head">
        <SquareTerminal size={15} /><span className="panel-title">Script</span>
        {!engineInBrowser && <Status />}
        <span className="grow" />
        <button type="button" className="icon" onClick={closePanel} title="Close"><X size={15} /></button>
      </header>
      {engineInBrowser
        ? <div className="script-web"><p>Python runs in the desktop app.</p><p className="muted small">Lapin for the desktop runs your own Python (3.11 or newer) in the Script panel; in the browser there is none.</p></div>
        : <div className="script-body"><Library /><Editor /><Console /></div>}
    </section>
  );
}
