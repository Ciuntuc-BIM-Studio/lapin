/**
 * The Script panel (View → Script, with Preferences → Developer extras on; decision 489): Python in Lapin.
 * Left, the library: Mine (the user's own scripts, plain .py files in their folder) and the Project's (its LapinScript
 * entities, kept with the project, one undo step each change). Middle, the editor (CodeMirror 6) with Run, Stop and
 * Restart; each Run is one undo step named after the script, and nothing of a run that fails or is stopped is kept; a
 * project's script asks trust before its first run (or its first after its code changed). Right, the console (what
 * scripts print, their errors with their line, a line to type Python in: Enter runs it, Shift+Enter a new line, Up
 * and Down its history) and the recorder (Record / Stop: each step done in the window as a line of Python; Copy, Save as
 * script). Python is the user's own (3.11 or newer), on the desktop; in the browser the panel says that Python runs in
 * the desktop app.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Circle, Copy, ExternalLink, FilePlus, FolderInput, Play, RotateCcw, Save, ShieldOff, Square, SquareTerminal, Trash2, X, Eraser } from 'lucide-react';
import { engineInBrowser, useSession } from '../../engine/session';
import { CodeEditor } from './CodeEditor';
import { namespaces, scriptName, walk, type Completion } from './logic';
import { trustQuestion, TRUST_WARNING } from './trust';
import { parseHeader } from './header';
import { forgetTrust, isTrusted } from './scriptTools';
import {
  answerTrust, clearConsole, clearRecording, closePanel, copyRecording, deleteFromProject, deleteScript, dirty, follow, newScript, openProjectScript, openScript,
  projectScripts, recordedText, refreshStatus, restart, reveal, run, saveRecording, saveScript, saveToProject, setHeight, startRecording, stop, stopRecording, useScripting,
} from './store';

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
  const projectId = useScripting((s) => s.projectId);
  const nodes = useSession((s) => s.nodes);
  const project = useMemo(() => projectScripts(nodes), [nodes]);
  return (
    <div className="script-library" data-field="script-library">
      <div className="script-library-head"><span className="panel-title">Mine</span>
        <button type="button" className="icon" title="New script" data-action="script-new" onClick={() => newScript()}><FilePlus size={14} /></button></div>
      <div className="script-files">
        {files.length === 0 && <p className="muted small">Saved scripts show here: plain .py files in your folder.</p>}
        {files.map((f) => (
          <button type="button" key={f.name} className={`script-file${f.name === file ? ' on' : ''}`} data-script={f.name} title={f.name} onClick={() => void openScript(f.name)}>
            <span data-no-i18n>{f.name.replace(/\.py$/, '')}</span>
          </button>
        ))}
      </div>
      <div className="script-library-head"><span className="panel-title">Project's</span></div>
      <div className="script-files" data-field="script-project-library">
        {project.length === 0 && <p className="muted small">Scripts kept with the project show here; they never run as it opens.</p>}
        {project.map((p) => (
          <button type="button" key={p.id} className={`script-file${p.id === projectId ? ' on' : ''}`} data-project-script={p.name} title={p.name} onClick={() => openProjectScript(p.id)}>
            <span data-no-i18n>{p.name.replace(/\.py$/, '')}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

function Lines() {
  const lines = useScripting((s) => s.lines);
  const out = useRef<HTMLDivElement>(null);
  useEffect(() => { out.current?.scrollTo({ top: out.current.scrollHeight }); }, [lines]);
  return (
    <div className="script-output" ref={out} data-field="script-output">
      {/* Lapin's own lines are translated as the interface is; what a script prints or typed code is shown as it is. */}
      {lines.map((l, i) => <pre key={i} className={`script-line ${l.kind}`} {...(l.own ? {} : { 'data-no-i18n': '' })}>{l.kind === 'in' ? `>>> ${l.text.replace(/\n/g, '\n... ')}` : l.text}</pre>)}
    </div>
  );
}

function ConsoleTab() {
  const running = useScripting((s) => s.running != null);
  const [draft, setDraft] = useState('');
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
    <>
      <Lines />
      <div className="script-input">
        <span className="script-prompt" aria-hidden>{'>>>'}</span>
        <textarea rows={Math.min(6, draft.split('\n').length)} value={draft} spellCheck={false} data-field="script-console-input"
          placeholder={running ? 'A script is running…' : 'Python: lapin.model.walls'} aria-label="Python console"
          onChange={(e) => setDraft(e.target.value)} onKeyDown={key} />
      </div>
    </>
  );
}

function RecorderTab() {
  const recording = useScripting((s) => s.recording);
  const recorded = useScripting((s) => s.recorded);
  const useSelection = useScripting((s) => s.recordSelection);
  const out = useRef<HTMLPreElement>(null);
  useEffect(() => { out.current?.parentElement?.scrollTo({ top: out.current.parentElement.scrollHeight }); }, [recorded]);
  return (
    <>
      <div className="script-recorder-bar">
        {recording
          ? <button type="button" className="btn" data-action="script-record-stop" onClick={stopRecording} title="Stop recording: the script is kept here to copy or save"><Square size={13} /> Stop</button>
          : <button type="button" className="btn primary" data-action="script-record" onClick={() => void startRecording()} title="Record: each step you do in the window becomes a line of Python"><Circle size={13} /> Record</button>}
        <button type="button" className="btn" data-action="script-record-copy" disabled={!recorded.length} onClick={() => void copyRecording()} title="Copy the recorded script"><Copy size={13} /> Copy</button>
        <button type="button" className="btn" data-action="script-record-save" disabled={!recorded.length} onClick={() => void saveRecording()} title="Save the recorded script in Mine and open it in the editor"><Save size={13} /> Save as script</button>
        <span className="grow" />
        <label className="script-check" title="A command on the window's selection is written lapin.selection(), so the script works on what is selected when it runs">
          <input type="checkbox" checked={useSelection} onChange={(e) => useScripting.setState({ recordSelection: e.target.checked })} /> <span>Selection as lapin.selection()</span>
        </label>
        <button type="button" className="icon" title="Clear the recording" disabled={!recorded.length} onClick={clearRecording}><Eraser size={14} /></button>
      </div>
      <div className="script-output" data-field="script-recorder">
        {recorded.length === 0
          ? <p className="muted small">{recording ? 'Recording: draw, change or delete something in the window.' : 'Record, then work in the window: each step shows here as a line of Python, ready to run on another project.'}</p>
          : <pre ref={out} className={`script-line out${recording ? ' live' : ''}`} data-no-i18n>{recordedText()}</pre>}
      </div>
    </>
  );
}

function Console() {
  const tab = useScripting((s) => s.tab);
  const recording = useScripting((s) => s.recording);
  return (
    <div className="script-console">
      <div className="script-console-head">
        <div className="script-tabs" role="tablist">
          <button type="button" role="tab" aria-selected={tab === 'console'} className={tab === 'console' ? 'on' : ''} data-action="script-tab-console" onClick={() => useScripting.setState({ tab: 'console' })}>Console</button>
          <button type="button" role="tab" aria-selected={tab === 'recorder'} className={tab === 'recorder' ? 'on' : ''} data-action="script-tab-recorder" onClick={() => useScripting.setState({ tab: 'recorder' })}>
            {recording && <span className="script-rec-dot" aria-hidden />}Recorder</button>
        </div>
        {tab === 'console' && <button type="button" className="icon" title="Clear the console" onClick={clearConsole}><Eraser size={14} /></button>}
      </div>
      {tab === 'console' ? <ConsoleTab /> : <RecorderTab />}
    </div>
  );
}

function Trust() {
  const asking = useScripting((s) => s.asking);
  const path = useSession((s) => s.doc?.path ?? null);
  if (!asking) return null;
  const project = path ? path.split(/[\\/]/).pop() ?? null : null;
  return (
    <div className="script-trust" role="alertdialog" aria-label="Trust this script?" data-field="script-trust">
      <p><b>{trustQuestion(project, asking.name)}</b></p>
      <p className="small">{TRUST_WARNING}</p>
      <div className="script-trust-actions">
        <button type="button" className="btn" data-action="script-trust-cancel" onClick={() => void answerTrust('cancel')}>Cancel</button>
        <button type="button" className="btn" data-action="script-trust-once" onClick={() => void answerTrust('once')}>Run once</button>
        <button type="button" className="btn primary" data-action="script-trust" onClick={() => void answerTrust('trust')}>Trust</button>
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
  const nodes = useSession((x) => x.nodes);
  const project = s.projectId ? projectScripts(nodes).find((p) => p.id === s.projectId) ?? null : null;
  const shown = project?.name ?? s.file ?? '';
  const [name, setName] = useState(shown.replace(/\.py$/, ''));
  useEffect(() => { setName(shown.replace(/\.py$/, '')); }, [shown]);
  const named = () => {
    const as = scriptName(name);
    if (!as) useScripting.setState({ lines: [...s.lines, { kind: 'err', text: 'Give the script a name first.', own: true }] });
    return as;
  };
  const save = async () => { const as = named(); if (as) await saveScript(as); };
  const keep = async () => { const as = named(); if (as) await saveToProject(as); };
  const changed = project ? project.code !== s.code || project.name !== scriptName(name) : dirty(s);
  // Stage 4: the tool its header names (in My tools once saved), and trust in a project's script taken back.
  const header = useMemo(() => parseHeader(s.code, shown || undefined), [s.code, shown]);
  const trusted = project ? isTrusted(project.sha256) || s.trust.trusted.has(project.sha256) : false;
  return (
    <div className="script-main">
      <div className="script-toolbar">
        <input type="text" className="script-name" value={name} placeholder="Untitled" spellCheck={false} data-field="script-name" aria-label="Script name"
          onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') void (project ? keep() : save()); }} />
        <span className="muted small" title={project ? 'The project’s script' : undefined}>{project ? '◆' : ''}{changed ? '•' : ''}</span>
        <button type="button" className="btn primary" data-action="script-run" disabled={running} onClick={() => void run(s.code, false)} title="Run (Ctrl+Enter): one undo step">
          <Play size={13} /> Run</button>
        <button type="button" className="btn" data-action="script-stop" disabled={!running} onClick={() => void stop()} title="Stop: nothing the script did is kept">
          <Square size={13} /> Stop</button>
        <button type="button" className="btn" data-action="script-restart" onClick={() => void restart()} title="Restart Python: the console’s names are forgotten">
          <RotateCcw size={13} /> Restart</button>
        <span className="grow" />
        {header?.tool && <span className="muted small script-tool-name" title="Its tool in My tools (the script's header)" data-field="script-tool"><span>Tool:</span> <b data-no-i18n>{header.tool}</b></span>}
        {project && trusted && <button type="button" className="icon" data-action="script-untrust" title="Forget trust: this script asks again before it runs, and its tools leave My tools"
          onClick={() => void forgetTrust(project.sha256)}><ShieldOff size={14} /></button>}
        <button type="button" className="icon" data-action="script-save" title="Save in Mine" onClick={() => void save()}><Save size={14} /></button>
        <button type="button" className="icon" data-action="script-save-project" title="Save in the project (one undo step; it goes with the file)" onClick={() => void keep()}><FolderInput size={14} /></button>
        <button type="button" className="icon" data-action="script-reveal" title="Open in external editor" disabled={!s.file} onClick={() => void reveal()}><ExternalLink size={14} /></button>
        <button type="button" className="icon" data-action="script-delete" title={project ? 'Delete from the project (Undo brings it back)' : 'Delete from Mine'} disabled={!s.file && !project}
          onClick={() => void (project ? deleteFromProject() : deleteScript())}><Trash2 size={14} /></button>
      </div>
      <Trust />
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
