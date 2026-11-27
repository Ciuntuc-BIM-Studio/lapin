/**
 * The Python Notebook view (`views.scripting`; docs/plan-python-notebook.md):
 * - Left (collapsible): Library (Mine + Project's `.py` scripts)
 * - Center: Jupyter-style notebook with Code (Python), Text (Markdown), and AI Prompt (Ollama) cells,
 *   per-cell execution (`Shift+Enter`), Run All (one undo step), Fix with AI, and Code → Visual Graph.
 * - Bottom (resizable): Persistent Terminal (global Console REPL + chronological output/errors + Recorder with Insert as cell).
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ArrowDown,
  ArrowUp,
  Circle,
  Code2,
  Copy,
  Eraser,
  ExternalLink,
  FilePlus,
  FileText,
  FolderInput,
  PanelBottom,
  PanelLeft,
  Play,
  PlayCircle,
  RefreshCw,
  RotateCcw,
  Save,
  ShieldOff,
  Sparkles,
  Square,
  SquareTerminal,
  Trash2,
  Wand2,
  Workflow,
  X,
} from 'lucide-react';
import { engineInBrowser, useSession } from '../../engine/session';
import { MessageStrip } from '../../shell/MessageStrip';
import { Markdown } from '../../assistant/Markdown';
import { t, useLang } from '../../i18n';
import { saveSettings, useAssistantSettings } from '../../assistant/ollama';
import { CodeEditor } from './CodeEditor';
import { namespaces, scriptName, walk, type Completion } from './logic';
import { trustQuestion, TRUST_WARNING } from './trust';
import { parseHeader } from './header';
import { forgetTrust, isTrusted } from './scriptTools';
import type { NotebookCell } from './notebook';
import {
  addCell,
  answerTrust,
  cellToGraph,
  cellToGraphWithOllama,
  clearCellOutputs,
  clearConsole,
  clearRecording,
  copyRecording,
  deleteCell,
  deleteFromProject,
  deleteScript,
  dirty,
  dismissGraphWarning,
  fixCellWithAi,
  follow,
  insertRecordingAsCell,
  listen,
  moveCellBy,
  newScript,
  openConvertedGraph,
  openProjectScript,
  openScript,
  projectScripts,
  recordedText,
  refreshFiles,
  refreshOllama,
  refreshStatus,
  restart,
  restartAndRunAll,
  reveal,
  run,
  runAllCells,
  runCell,
  runPromptCell,
  saveRecording,
  saveScript,
  saveToProject,
  scriptLog,
  setActiveCell,
  setHeight,
  showScriptAt,
  startRecording,
  stop,
  stopAi,
  stopRecording,
  updateCell,
  useScripting,
} from './store';

function Status() {
  const status = useScripting((s) => s.status);
  const running = useScripting((s) => s.runningName);
  const aiStatus = useScripting((s) => s.aiStatus);
  if (aiStatus) return <span className="script-status running" data-field="script-status"><Sparkles size={12} /> <span>{aiStatus}</span></span>;
  if (running) return <span className="script-status running" data-field="script-status"><span>Running…</span> <b data-no-i18n>{running}</b></span>;
  if (!status) return <span className="script-status muted">Looking for Python…</span>;
  if (status.ok) return <span className="script-status muted" title={status.python ?? ''} data-field="script-status">Python <span data-no-i18n>{status.version}</span></span>;
  return <span className="script-status error-text" data-field="script-status">{status.message}</span>;
}

function OllamaPicker() {
  const ollama = useScripting((s) => s.ollama);
  const codeModel = useAssistantSettings((s) => s.codeModel ?? s.model ?? '');
  if (engineInBrowser) return null;
  const models = ollama?.models ?? [];
  return (
    <div className="nb-ollama" title={ollama?.running ? "The Ollama model on this computer that writes the AI cells' Python" : (ollama?.error || 'Ollama is not running: start it with `ollama serve` or the Ollama app.')}>
      <Sparkles size={13} className={ollama?.running ? 'nb-ollama-on' : 'muted'} />
      {ollama?.running && models.length > 0 ? (
        <select
          className="nb-model-select"
          value={models.includes(codeModel) ? codeModel : models[0]}
          aria-label="Ollama model"
          data-no-i18n
          onChange={(e) => saveSettings({ codeModel: e.target.value })}
        >
          {models.map((m) => <option key={m} value={m}>{m}</option>)}
        </select>
      ) : (
        <span className="muted small">{ollama?.running ? 'No model' : 'Ollama offline'}</span>
      )}
      <button type="button" className="icon" title="Look for the Ollama models again" onClick={() => void refreshOllama()}><RefreshCw size={12} /></button>
    </div>
  );
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
        <button type="button" className="icon" title="New notebook" data-action="script-new" onClick={() => newScript()}><FilePlus size={14} /></button></div>
      <div className="script-files">
        {files.length === 0 && <p className="muted small">Saved scripts and notebooks show here: plain .py files in your folder.</p>}
        {files.map((f) => (
          <button type="button" key={f.name} className={`script-file${f.name === file ? ' on' : ''}`} data-script={f.name} title={f.name} onClick={() => void openScript(f.name)}>
            <span data-no-i18n>{f.name.replace(/\.py$/, '')}</span>
          </button>
        ))}
      </div>
      <div className="script-library-head"><span className="panel-title">Project&apos;s</span></div>
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
        <textarea rows={Math.min(5, draft.split('\n').length)} value={draft} spellCheck={false} data-field="script-console-input"
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
          ? <button type="button" className="btn" data-action="script-record-stop" onClick={stopRecording} title="Stop recording: the script is kept here to copy, save or insert as a cell"><Square size={13} /> Stop</button>
          : <button type="button" className="btn primary" data-action="script-record" onClick={() => void startRecording()} title="Record: each step you do in the window becomes a line of Python"><Circle size={13} /> Record</button>}
        <button type="button" className="btn" data-action="script-record-insert" disabled={!recorded.length} onClick={insertRecordingAsCell} title="Insert the recorded steps as a new code cell"><Code2 size={13} /> Insert as cell</button>
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
          ? <p className="muted small">{recording ? 'Recording: draw, change or delete something in the window.' : 'Record, then work in the window: each step becomes Python you can insert as a cell.'}</p>
          : <pre ref={out} className={`script-line out${recording ? ' live' : ''}`} data-no-i18n>{recordedText()}</pre>}
      </div>
    </>
  );
}

function TerminalPane() {
  const tab = useScripting((s) => s.tab);
  const recording = useScripting((s) => s.recording);
  const height = useScripting((s) => s.height);
  const drag = (e: React.PointerEvent) => {
    e.preventDefault();
    const y0 = e.clientY, h0 = height;
    const move = (ev: PointerEvent) => setHeight(h0 + (y0 - ev.clientY));
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };
  return (
    <div className="nb-terminal" style={{ height }}>
      <div className="nb-term-resize" onPointerDown={drag} title="Drag to resize" />
      <div className="script-console-head">
        <div className="script-tabs" role="tablist">
          <button type="button" role="tab" aria-selected={tab === 'console'} className={tab === 'console' ? 'on' : ''} data-action="script-tab-console" onClick={() => useScripting.setState({ tab: 'console' })}>Terminal</button>
          <button type="button" role="tab" aria-selected={tab === 'recorder'} className={tab === 'recorder' ? 'on' : ''} data-action="script-tab-recorder" onClick={() => useScripting.setState({ tab: 'recorder' })}>
            {recording && <span className="script-rec-dot" aria-hidden />}Recorder</button>
        </div>
        <div className="nb-term-actions">
          {tab === 'console' && <button type="button" className="icon" title="Clear the terminal" onClick={clearConsole}><Eraser size={14} /></button>}
          <button type="button" className="icon" title="Hide the terminal" onClick={() => useScripting.setState({ terminalOpen: false })}><X size={14} /></button>
        </div>
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

function CellCard({ cell, index, total, active, spaces }: {
  cell: NotebookCell;
  index: number;
  total: number;
  active: boolean;
  spaces: () => Map<string, Completion[]>;
}) {
  const running = useScripting((s) => s.running != null);
  const runningCellId = useScripting((s) => s.runningCellId);
  const aiBusyCellId = useScripting((s) => s.aiBusyCellId);
  const graphWarning = useScripting((s) => s.graphWarning);
  useLang((x) => x.lang);   // the placeholders (t) redrawn in a new language
  const [editingMd, setEditingMd] = useState(cell.kind === 'markdown' && !cell.source.trim());

  const isRunningThis = runningCellId === cell.id;
  const isAiBusyThis = aiBusyCellId === cell.id;
  const hasGraphWarning = graphWarning?.cellId === cell.id;

  const badge = cell.kind === 'code'
    ? (isRunningThis ? '[*]' : cell.execCount != null ? `[${cell.execCount}]${cell.stale ? '•' : ''}` : '[ ]')
    : cell.kind === 'prompt' ? 'AI' : 'MD';

  return (
    <div
      className={`nb-cell nb-cell-${cell.kind}${active ? ' active' : ''}${cell.lastError ? ' has-error' : ''}`}
      data-cell-id={cell.id}
      data-cell-kind={cell.kind}
      onMouseDown={() => setActiveCell(cell.id)}
    >
      <div className="nb-cell-bar">
        <span className="nb-badge" data-no-i18n>{badge}</span>
        <span className="nb-kind-label">{cell.kind === 'code' ? 'Python' : cell.kind === 'prompt' ? 'AI prompt → Python' : 'Markdown'}</span>
        <span className="grow" />

        {cell.kind === 'code' && (
          <>
            <button type="button" className="btn nb-btn-sm primary" disabled={running} onClick={() => void runCell(cell.id)} title="Run the cell (Shift+Enter): one undo step">
              <Play size={12} /> Run
            </button>
            <button type="button" className="btn nb-btn-sm" disabled={running || aiBusyCellId != null} onClick={() => void cellToGraph(cell.id)} title="Convert this Python cell into a visual graph">
              <Workflow size={12} /> To graph
            </button>
            {cell.lastError && !engineInBrowser && (
              <button type="button" className="btn nb-btn-sm" disabled={aiBusyCellId != null} onClick={() => void fixCellWithAi(cell.id)} title="Ask Ollama to fix this cell's error">
                <Wand2 size={12} /> Fix with AI
              </button>
            )}
          </>
        )}

        {cell.kind === 'prompt' && (
          isAiBusyThis ? (
            <button type="button" className="btn nb-btn-sm" onClick={stopAi}><Square size={12} /> Stop</button>
          ) : (
            <button type="button" className="btn nb-btn-sm primary" disabled={aiBusyCellId != null || !cell.source.trim()} onClick={() => void runPromptCell(cell.id)} title="Generate Python code into the cell below (Shift+Enter)">
              <Sparkles size={12} /> Generate
            </button>
          )
        )}

        {cell.kind === 'markdown' && (
          <button type="button" className="btn nb-btn-sm" onClick={() => setEditingMd((v) => !v)}>
            {editingMd ? 'Preview' : 'Edit'}
          </button>
        )}

        <button type="button" className="icon" disabled={index === 0} title="Move the cell up" onClick={() => moveCellBy(index, -1)}><ArrowUp size={13} /></button>
        <button type="button" className="icon" disabled={index === total - 1} title="Move the cell down" onClick={() => moveCellBy(index, 1)}><ArrowDown size={13} /></button>
        <button type="button" className="icon" title="Delete the cell" onClick={() => deleteCell(cell.id)}><Trash2 size={13} /></button>
      </div>

      <div className="nb-cell-body">
        {cell.kind === 'code' && (
          active ? (
            <CodeEditor
              value={cell.source}
              onChange={(v) => updateCell(cell.id, v)}
              onRun={() => void runCell(cell.id)}
              line={cell.errorLine ?? null}
              spaces={spaces}
              label={`Python cell ${index + 1}`}
            />
          ) : (
            <pre className="nb-code-static" {...(cell.source ? { 'data-no-i18n': '' } : {})} onClick={() => setActiveCell(cell.id)}>
              {cell.source || <span className="muted">Click to write Python…</span>}
            </pre>
          )
        )}

        {cell.kind === 'markdown' && (
          editingMd ? (
            <textarea
              className="nb-md-input"
              rows={Math.max(2, cell.source.split('\n').length + 1)}
              value={cell.source}
              placeholder={t('Notes in Markdown: # a heading, **bold**, lists, tables, ```code```…')}
              onChange={(e) => updateCell(cell.id, e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter' && e.shiftKey) { e.preventDefault(); setEditingMd(false); } }}
              onBlur={() => { if (cell.source.trim()) setEditingMd(false); }}
              autoFocus
            />
          ) : (
            <div className="nb-md-preview" onDoubleClick={() => setEditingMd(true)}>
              {cell.source.trim() ? <Markdown text={cell.source} /> : <p className="muted small">Double-click to write notes…</p>}
            </div>
          )
        )}

        {cell.kind === 'prompt' && (
          <textarea
            className="nb-prompt-input"
            rows={Math.max(2, Math.min(8, cell.source.split('\n').length + 1))}
            value={cell.source}
            placeholder={t('Say what the Python in the cell below should do (for example: six columns 4 m apart along X on the ground floor)…')}
            onChange={(e) => updateCell(cell.id, e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.shiftKey || e.metaKey || e.ctrlKey)) {
                e.preventDefault();
                void runPromptCell(cell.id);
              }
            }}
          />
        )}
      </div>

      {hasGraphWarning && graphWarning && (
        <div className="nb-graph-warn">
          <div className="nb-graph-warn-text">
            <b>Partial graph conversion:</b>{' '}
            <span>{`${graphWarning.unsupported.length} line(s) have no graph node, the first at line ${graphWarning.unsupported[0]?.line ?? 0}:`}</span>{' '}
            <code data-no-i18n>{graphWarning.unsupported[0]?.text}</code>
          </div>
          <div className="nb-graph-warn-actions">
            {graphWarning.graph.nodes.length > 0 && (
              <button type="button" className="btn nb-btn-sm" onClick={() => void openConvertedGraph(graphWarning.graph, `Cell ${index + 1}`)}>
                {`Open the partial graph (${graphWarning.graph.nodes.length} nodes)`}
              </button>
            )}
            {!engineInBrowser && (
              <button type="button" className="btn nb-btn-sm primary" disabled={aiBusyCellId != null} onClick={() => void cellToGraphWithOllama(cell.id)}>
                <Sparkles size={12} /> Convert with Ollama
              </button>
            )}
            <button type="button" className="icon" title="Close" onClick={dismissGraphWarning}><X size={13} /></button>
          </div>
        </div>
      )}

      {cell.kind === 'code' && cell.outputs && cell.outputs.length > 0 && (
        <div className="nb-cell-outputs">
          <div className="nb-cell-outputs-head">
            <span className="muted small">Output</span>
            <button type="button" className="icon" title="Clear the cell's output" onClick={() => clearCellOutputs(cell.id)}><Eraser size={12} /></button>
          </div>
          {cell.outputs.map((o, i) => (
            <pre key={i} className={`script-line ${o.kind}`} data-no-i18n>{o.text}</pre>
          ))}
        </div>
      )}
    </div>
  );
}

export function ScriptPanel() {
  const s = useScripting();
  const running = s.running != null;
  const spaces = useMemo(() => (s.catalogue ? namespaces(s.catalogue) : new Map<string, Completion[]>()), [s.catalogue]);
  const spacesRef = useRef(spaces);
  spacesRef.current = spaces;

  const nodes = useSession((x) => x.nodes);
  const project = s.projectId ? projectScripts(nodes).find((p) => p.id === s.projectId) ?? null : null;
  const shown = project?.name ?? s.file ?? '';
  const [name, setName] = useState(shown.replace(/\.py$/, ''));
  useEffect(() => { setName(shown.replace(/\.py$/, '')); }, [shown]);

  useEffect(() => {
    if (engineInBrowser) return;
    listen();
    void Promise.all([refreshStatus(), refreshFiles(), refreshOllama()]);
    const t = window.setInterval(() => void follow(), 2000);
    const back = () => { void follow(); void refreshStatus(); };
    window.addEventListener('focus', back);
    return () => { window.clearInterval(t); window.removeEventListener('focus', back); };
  }, []);

  const named = () => {
    const as = scriptName(name);
    if (!as) useScripting.setState({ lines: [...s.lines, { kind: 'err', text: 'Give the script a name first.', own: true }] });
    return as;
  };
  const save = async () => { const as = named(); if (as) await saveScript(as); };
  const keep = async () => { const as = named(); if (as) await saveToProject(as); };
  const changed = project ? project.code !== s.code || project.name !== scriptName(name) : dirty(s);
  const header = useMemo(() => parseHeader(s.code, shown || undefined), [s.code, shown]);
  const trusted = project ? isTrusted(project.sha256) || s.trust.trusted.has(project.sha256) : false;

  const keepKeys = (e: React.KeyboardEvent) => { e.stopPropagation(); };

  return (
    <section className="nb-view" aria-label="Python notebook" data-panel="script" data-field="script-panel" onKeyDown={keepKeys}>
      <div className="nb-head">
        <button type="button" className={`icon${s.libraryOpen ? ' on' : ''}`} title="Show or hide the library" onClick={() => useScripting.setState({ libraryOpen: !s.libraryOpen })}><PanelLeft size={14} /></button>
        <SquareTerminal size={14} />
        <input
          type="text"
          className="script-name"
          value={name}
          placeholder="Untitled"
          spellCheck={false}
          data-field="script-name"
          aria-label="Script name"
          onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') void (project ? keep() : save()); }}
        />
        <span className="muted small" title={project ? 'The project’s script' : undefined}>{project ? '◆' : ''}{changed ? '•' : ''}</span>

        {!engineInBrowser && (
          <>
            <button type="button" className="btn primary" data-action="script-run" disabled={running} onClick={() => void runAllCells()} title="Run all the code cells: one undo step">
              <PlayCircle size={13} /> Run all
            </button>
            <button type="button" className="btn" data-action="script-stop" disabled={!running && !s.aiBusyCellId} onClick={() => { if (s.aiBusyCellId) stopAi(); else void stop(); }} title="Stop: nothing the script did is kept">
              <Square size={13} /> Stop
            </button>
            <button type="button" className="btn" data-action="script-restart" disabled={running} onClick={() => void restart()} title="Restart Python: the notebook’s variables are forgotten">
              <RotateCcw size={13} /> Restart
            </button>
            <button type="button" className="btn" disabled={running} onClick={() => void restartAndRunAll()} title="Restart Python and run all cells from top to bottom">
              Restart and run all
            </button>
          </>
        )}

        <span className="grow" />
        <OllamaPicker />
        <Status />

        {header?.tool && <span className="muted small script-tool-name" title="Its tool in My tools (the script's header)" data-field="script-tool"><span>Tool:</span> <b data-no-i18n>{header.tool}</b></span>}
        {project && trusted && (
          <button type="button" className="icon" data-action="script-untrust" title="Forget trust: this script asks again before it runs, and its tools leave My tools" onClick={() => void forgetTrust(project.sha256)}>
            <ShieldOff size={14} />
          </button>
        )}
        {!engineInBrowser && (
          <>
            <button type="button" className="icon" data-action="script-save" title="Save in Mine" onClick={() => void save()}><Save size={14} /></button>
            <button type="button" className="icon" data-action="script-save-project" title="Save in the project (one undo step; it goes with the file)" onClick={() => void keep()}><FolderInput size={14} /></button>
            <button type="button" className="icon" data-action="script-reveal" title="Open in external editor" disabled={!s.file} onClick={() => void reveal()}><ExternalLink size={14} /></button>
            <button type="button" className="icon" data-action="script-delete" title={project ? 'Delete from the project (Undo brings it back)' : 'Delete from Mine'} disabled={!s.file && !project} onClick={() => void (project ? deleteFromProject() : deleteScript())}><Trash2 size={14} /></button>
            <button type="button" className={`icon${s.terminalOpen ? ' on' : ''}`} title="Show or hide the terminal" onClick={() => useScripting.setState({ terminalOpen: !s.terminalOpen })}><PanelBottom size={14} /></button>
          </>
        )}
      </div>

      {engineInBrowser ? (
        <div className="script-browser-note">
          <p>Python runs in the desktop app.</p>
          <p className="muted small">Lapin for the desktop runs the notebook with your own Python (3.11 or newer) and its AI cells with your own Ollama; in the browser there is none. A graph made from a Python cell runs in both.</p>
        </div>
      ) : (
        <div className="nb-workspace">
          {s.libraryOpen && <Library />}
          <div className="nb-main">
            <Trust />
            <div className="nb-cells" data-field="notebook-cells">
              {s.cells.map((cell, idx) => (
                <CellCard
                  key={cell.id}
                  cell={cell}
                  index={idx}
                  total={s.cells.length}
                  active={s.activeCellId === cell.id}
                  spaces={() => spacesRef.current}
                />
              ))}
              <div className="nb-add-bar">
                <button type="button" className="btn" data-action="nb-add-code" onClick={() => addCell('code')}><Code2 size={13} /> + Code</button>
                <button type="button" className="btn" data-action="nb-add-markdown" onClick={() => addCell('markdown')}><FileText size={13} /> + Text</button>
                <button type="button" className="btn" data-action="nb-add-prompt" onClick={() => addCell('prompt')}><Sparkles size={13} /> + AI prompt</button>
              </div>
            </div>
            {s.terminalOpen && <TerminalPane />}
            <MessageStrip log={scriptLog} onOpen={(m) => void showScriptAt(m.target as { script?: string; line?: number } | undefined)} />
          </div>
        </div>
      )}
    </section>
  );
}
