/**
 * A graph's palette (docs/plan-visual-graphs.md §8 stage 2, decision 536): a small floating panel of the main window,
 * opened by the graph's tool in My tools, drawn from the graph's input nodes (palette.ts) with the same fields as a
 * script's palette (shell/AskField.tsx) — never the canvas. Changing a field evaluates the graph with it at once and
 * redraws the ghost in the views (graphs.evaluate with `values`: no undo step); letting go of it (a slider released, a
 * number typed and Enter, a choice or a box ticked, Use selection) writes it into the graph (graphs.set: one undo step),
 * so the canvas, Bake and a colleague opening the project have the same values. Bake makes the result, with the
 * palette's values, in one undo step. The Watch nodes and the nodes marked "Show in palette" show what the graph gives:
 * a value, a short list or a small table. Where the palette floats is kept on this computer. Works on the web too.
 */

import { useEffect, useMemo, useRef, useState, type PointerEvent } from 'react';
import { Flame, PencilRuler, Workflow, X } from 'lucide-react';
import { activeStorey, execute, query, useSession } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import { isGuest } from '../../engine/share';
import { t as tr, useLang } from '../../i18n';
import type { AskField, FieldState } from '../../shell/askFields';
import { Field, useFacts } from '../../shell/AskField';
import { scriptIcon } from '../../shell/toolIcons';
import { graphOf } from './graph';
import { changedValues, clampAt, paletteOf, shown, stateOf, toolOf, valueOf, type Palette } from './palette';
import { openGraph, openPanel, panelGhost, showGhost, useGraphs, type Evaluation } from './store';
import { togglePalette, usePalettes } from './tools';

const memoryKey = (id: string) => `lapin.graphPalette.${id}`;

function readAt(id: string): { x?: number; y?: number } {
  try { const v = JSON.parse(localStorage.getItem(memoryKey(id)) ?? 'null'); return v && typeof v === 'object' ? v : {}; } catch { return {}; }
}
function keepAt(id: string, at: { x: number; y: number }): void {
  try { localStorage.setItem(memoryKey(id), JSON.stringify(at)); } catch { /* this session only */ }
}

const win = () => ({ w: window.innerWidth, h: window.innerHeight });

/** The kinds of field written into the graph as soon as they change (a number or a text waits until it is let go of). */
const AT_ONCE = new Set<AskField['kind']>(['bool', 'choice', 'element', 'storey', 'type']);

/** The edits that still differ from the graph's own values (the others are written: dropped). */
function pending(p: Palette, edits: Record<string, FieldState>): Record<string, FieldState> {
  const out: Record<string, FieldState> = {};
  for (const { path, field } of p.inputs) {
    if (!(path in edits)) continue;
    const a = valueOf(field, edits[path]!);
    const b = valueOf(field, stateOf(field));
    if (!('value' in a) || !('value' in b) || JSON.stringify(a.value) !== JSON.stringify(b.value)) out[path] = edits[path]!;
  }
  return out;
}

function GraphPaletteWindow({ graphId, place }: { graphId: string; place: number }) {
  const node = useSession((s) => s.nodes.get(graphId));
  const version = useSession((s) => s.version);
  const specs = useGraphs((s) => s.specs);
  const lang = useLang((l) => l.lang);
  const graph = useMemo(() => graphOf(node?.props as Record<string, unknown> | undefined), [node]);
  const palette = useMemo(() => paletteOf(graph, (k) => specs?.get(k), tr), [graph, specs, lang]); // eslint-disable-line react-hooks/exhaustive-deps
  const tool = toolOf(graph, node?.name ?? '');
  const facts = useFacts();
  const [edits, setEdits] = useState<Record<string, FieldState>>({});
  const [wrong, setWrong] = useState<{ key: string; error: string } | null>(null);
  const [result, setResult] = useState<Evaluation | null>(null);
  const [busy, setBusy] = useState(false);
  const [ghostOn, setGhostOn] = useState(true);
  const [at, setAt] = useState(() => { const k = readAt(graphId); return clampAt(k.x ?? window.innerWidth - 320 - place * 24, k.y ?? 150 + place * 28, win()); });
  const drag = useRef<{ dx: number; dy: number } | null>(null);
  const box = useRef<HTMLElement>(null);
  const asked = useRef(0);

  // Written into the graph: the edits it now holds are dropped.
  useEffect(() => { setEdits((e) => { const p = pending(palette, e); return Object.keys(p).length === Object.keys(e).length ? e : p; }); }, [palette]);
  useEffect(() => { keepAt(graphId, at); }, [graphId, at]);
  // Closed: the panel's ghost again.
  useEffect(() => () => panelGhost(), []);

  const state = useMemo(() => ({ ...Object.fromEntries(palette.inputs.map((i) => [i.path, stateOf(i.field)])), ...edits }), [palette, edits]);
  const { values } = useMemo(() => changedValues(palette, state), [palette, state]);
  const valuesKey = JSON.stringify(values);
  const graphKey = JSON.stringify(graph);

  // The graph evaluated with the fields as they are, a moment after the last change (of a field, the graph or the model).
  useEffect(() => {
    const t = setTimeout(() => {
      const mine = ++asked.current;
      setBusy(true);
      void query<Evaluation>('graphs.evaluate', { id: graphId, values: JSON.parse(valuesKey), storey: activeStorey()?.id }).then((r) => {
        if (mine !== asked.current) return;
        setBusy(false);
        setResult(r);
        if (ghostOn) showGhost(r?.ghost ?? null);
      });
    }, 120);
    return () => clearTimeout(t);
  }, [graphId, valuesKey, graphKey, version, ghostOn]);

  /** The fields written into the graph (one undo step), or the first that does not read said. */
  const commit = (extra?: Record<string, FieldState>) => {
    const r = changedValues(palette, { ...state, ...extra });
    if (r.wrong) { setWrong(r.wrong); return; }
    setWrong(null);
    if (Object.keys(r.values).length > 0) void execute('graphs.set', { id: graphId, values: r.values });
  };

  const set = (f: AskField, v: FieldState) => {
    setEdits((e) => ({ ...e, [f.key]: v }));
    setGhostOn(true);
    if (wrong?.key === f.key) setWrong(null);
    if (AT_ONCE.has(f.kind)) commit({ [f.key]: v });
  };

  const bake = async () => {
    const r = changedValues(palette, state);
    if (r.wrong) { setWrong(r.wrong); return; }
    const made = await execute('graphs.bake', { id: graphId, storey: activeStorey()?.id, values: r.values });
    if (!made) return;
    setGhostOn(false);
    showGhost(null);
    notify('Baked: the elements are made. Undo takes them back in one step.');
  };

  const down = (e: PointerEvent<HTMLElement>) => {
    if ((e.target as HTMLElement).closest('button')) return;
    drag.current = { dx: e.clientX - at.x, dy: e.clientY - at.y };
    e.currentTarget.setPointerCapture(e.pointerId);
  };
  const move = (e: PointerEvent<HTMLElement>) => {
    if (!drag.current) return;
    const r = box.current?.getBoundingClientRect();
    setAt(clampAt(e.clientX - drag.current.dx, e.clientY - drag.current.dy, win(), r ? { w: r.width, h: 40 } : undefined));
  };
  const up = () => { drag.current = null; };

  if (!node) return null;
  const name = tool?.name ?? node.name;
  const Icon = tool?.icon ? scriptIcon(tool.icon, name) : Workflow;
  const failed = result ? Object.values(result.nodes).filter((n) => n.error).length : 0;
  return (
    <section ref={box} className="script-palette graph-palette" role="dialog" aria-label={name} data-graph-palette={node.name} style={{ left: at.x, top: at.y }}
      onKeyDown={(e) => e.stopPropagation()}>
      <header className="script-palette-head" onPointerDown={down} onPointerMove={move} onPointerUp={up} onPointerCancel={up} title={tool?.tooltip}>
        <span className="panel-title"><Icon size={14} /> <span data-no-i18n>{name}</span></span>
        <span className="graph-palette-head-buttons">
          <button type="button" className="icon" title="Open the graph in the Graphs panel" data-action="graph-palette-edit"
            onClick={() => { openGraph(graphId); openPanel(); }}><PencilRuler size={14} /></button>
          <button type="button" className="icon" onClick={() => togglePalette(graphId)} title="Close" aria-label="Close" data-action="palette-close"><X size={14} /></button>
        </span>
      </header>
      <div className="script-palette-body">
        {palette.inputs.length === 0 && palette.outputs.length === 0 && (
          <p className="muted small">This graph has no inputs yet: add Number, Choice, Storey or other input nodes in the Graphs panel, and they become its fields here.</p>
        )}
        {palette.inputs.map(({ path, field }) => (
          <Field key={path} f={field} value={state[path]!} facts={facts} wrong={wrong?.key === path} prefix={`graph-palette-${graphId}`}
            set={(v) => set(field, v)} onCommit={() => commit()} />
        ))}
        {wrong && <p className="error-text small" data-field="palette-error">{wrong.error}</p>}
        {palette.outputs.length > 0 && <hr />}
        {palette.outputs.map(({ path, node: n, label }) => {
          const r = result?.nodes[path];
          const ports = n.kind === 'watch' ? ['value'] : specs?.get(n.kind)?.outputs.map((p) => p.name) ?? Object.keys(r?.outputs ?? {});
          return (
            <div key={path} className="graph-palette-output" data-palette-output={path}>
              <span className="script-ask-label" data-no-i18n>{label}</span>
              {r?.error ? <span className="error-text small" title={r.error}>{r.error}</span>
                : <span className="graph-palette-values">{ports.map((p) => <Shown key={p} label={ports.length > 1 ? tr(specs?.get(n.kind)?.outputs.find((x) => x.name === p)?.label ?? p) : null} v={r?.outputs[p]} />)}</span>}
            </div>
          );
        })}
      </div>
      <footer className="graph-palette-foot">
        <span className={`small ${failed ? 'error-text' : 'muted'}`} data-field="graph-palette-status">
          {busy ? 'Evaluating…' : failed ? <><span data-no-i18n>{failed}</span> <span>failed</span></> : result ? <span data-no-i18n>{result.ms} ms</span> : ''}
        </span>
        <button type="button" className="btn primary" data-action="graph-palette-bake" title="Make what the graph previews, in one undo step" onClick={() => void bake()}>
          <Flame size={14} /> Bake</button>
      </footer>
    </section>
  );
}

/** An output's value: a value, a short list, or a small table, with how many in all when there are more. */
function Shown({ v, label }: { v: unknown; label: string | null }) {
  if (v === undefined) return <span className="muted">—</span>;
  const s = shown(v);
  const head = label ? <b data-no-i18n>{label} </b> : null;
  if (s.kind === 'value') return <span>{head}<span data-no-i18n>{s.text}</span></span>;
  if (s.kind === 'list') {
    return (
      <span>{head}<span data-no-i18n>{s.items.join(', ')}{s.total > s.items.length ? ` … (${s.total})` : ''}</span>
        {s.total === 0 && <span className="muted">empty</span>}</span>
    );
  }
  return (
    <span>{head}
      <table className="graph-palette-table"><tbody>{s.rows.map((r, i) => <tr key={i}>{r.map((c, j) => <td key={j} data-no-i18n>{c}</td>)}</tr>)}</tbody></table>
      {s.total > s.rows.length && <span className="muted small" data-no-i18n>… ({s.total})</span>}
    </span>
  );
}

/** The palettes open now (the main window's; none for a guest of a shared session). */
export function GraphPalettes() {
  const open = usePalettes((s) => s.open);
  const role = useUi((s) => s.windowRole);
  if (role === 'view' || isGuest()) return null;
  return <>{open.map((id, i) => <GraphPaletteWindow key={id} graphId={id} place={i} />)}</>;
}
