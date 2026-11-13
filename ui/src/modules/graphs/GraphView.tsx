/**
 * The Graphs panel (docs/plan-visual-graphs.md §5, decision 527): a view of the module's own, opened beside the model's
 * (split; on the desktop its tab goes into a window of its own). Above, the project's graphs (New, its name, Delete), the
 * preview switch and Bake. Left, the canvas (SVG, written here: no dependency): nodes with typed, coloured ports, wires
 * drawn thicker when they carry a list, double-click to add a node by searching, drag a node's head to move it (the
 * selection with it), from an output to an input to wire them, a box on the empty canvas to select, the wheel to zoom, the
 * middle button or Space to pan, Delete to take the selection out. Right, the inspector: the node's constants, its error,
 * what it gave. Every finished change is one graphs.save (one undo step); the engine evaluates the graph after each.
 */

import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Flame, Eye, EyeOff, Group, Maximize2, PanelsTopLeft, Plus, Trash2, Ungroup, Workflow } from 'lucide-react';
import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { t as tr, useLang } from '../../i18n';
import {
  addNode, collapse, connect, expand, fitView, groupSpec, moveNodes, nodeHeight, nodesIn, numberText, portAt, PORT_COLOURS, removeNodes, removeWire, rowValue, searchNodes, valueEnd, setLabel, setPalette, setTool, setValue, showValue,
  sockets, unknownSpec, wireKey, wirePath, zoomAt, GROUP, HEAD_H, NODE_W, ROW_H, type GraphJson, type GraphNode, type GraphView as ViewT, type NodeSpec, type PortSpec,
} from './graph';
import { isInput, GRAPHS_GROUP, type GraphTool, type NodePalette } from './palette';
import { togglePalette } from './tools';
import { isGuest } from '../../engine/share';
import { commitNumber, NO_COMMIT } from '../../shell/fieldCommit';
import { bake, currentGraph, GUEST_REFUSAL, deleteGraph, evaluate, loadSpecs, newGraph, openGraph, projectGraphs, renameGraph, saveGraph, setPreview, useGraphs } from './store';

type Drag =
  | { kind: 'move'; start: { x: number; y: number }; ids: Set<string>; moved: boolean }
  | { kind: 'wire'; from: string; out: string; at: { x: number; y: number } }
  | { kind: 'box'; a: { x: number; y: number }; b: { x: number; y: number } }
  | { kind: 'pan'; sx: number; sy: number; view: ViewT };

export function GraphView() {
  // A tab of the host's window shown to a guest (a pane they share): the graphs are the host's, so it says so and nothing more.
  if (isGuest()) return <div className="graphs-view" data-testid="graphs-view"><div className="graphs-empty muted"><Workflow size={28} /><p>{GUEST_REFUSAL}</p></div></div>;
  return <GraphPanel />;
}

function GraphPanel() {
  const graphId = useGraphs((s) => s.graphId);
  const nodes = useSession((s) => s.nodes);
  const graphs = useMemo(() => projectGraphs(nodes), [nodes]);
  useEffect(() => { void loadSpecs(); }, []);
  useEffect(() => { if (!graphId && graphs.length > 0) openGraph(graphs[0]!.id); else if (graphId && !nodes.has(graphId)) openGraph(graphs[0]?.id ?? null); }, [graphId, graphs, nodes]);
  return (
    <div className="graphs-view" data-testid="graphs-view">
      <Toolbar graphs={graphs} />
      {graphId ? <Editor key={graphId} /> : (
        <div className="graphs-empty muted">
          <Workflow size={28} />
          <p>A graph wires nodes (numbers, points, lists, the commands of the tools) into a recipe: Lapin shows what it would make as a ghost, and Bake makes it.</p>
          <button type="button" data-action="graph-new" onClick={() => void newGraph()}><Plus size={14} /> New graph</button>
        </div>
      )}
    </div>
  );
}

function Toolbar({ graphs }: { graphs: { id: string; name: string }[] }) {
  const graphId = useGraphs((s) => s.graphId);
  const preview = useGraphs((s) => s.preview);
  const busy = useGraphs((s) => s.busy);
  const result = useGraphs((s) => s.result);
  const current = graphs.find((g) => g.id === graphId);
  const [name, setName] = useState(current?.name ?? '');
  useEffect(() => setName(current?.name ?? ''), [current?.name]);
  const errors = result ? Object.values(result.nodes).filter((n) => n.error).length : 0;
  return (
    <div className="graphs-toolbar">
      <select value={graphId ?? ''} onChange={(e) => openGraph(e.target.value || null)} data-field="graph-pick" title="The project's graphs">
        {graphs.length === 0 && <option value="">No graphs</option>}
        {graphs.map((g) => <option key={g.id} value={g.id} data-no-i18n>{g.name}</option>)}
      </select>
      <button type="button" className="icon" title="New graph" data-action="graph-new" onClick={() => void newGraph()}><Plus size={14} /></button>
      {current && (
        <>
          <input className="graphs-name" value={name} onChange={(e) => setName(e.target.value)} data-field="graph-name" title="The graph's name"
            onBlur={() => { if (name.trim() && name !== current.name) void renameGraph(name); else setName(current.name); }}
            onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
          <button type="button" className="icon" title="Delete graph" data-action="graph-delete" onClick={() => void deleteGraph()}><Trash2 size={14} /></button>
          <span className="grow" />
          <span className={`graphs-status small ${errors ? 'error-text' : 'muted'}`} data-field="graph-status">
            {busy ? 'Evaluating…' : errors ? <><span data-no-i18n>{errors}</span> <span>failed</span></> : result ? <span data-no-i18n>{result.ms} ms</span> : ''}
          </span>
          <button type="button" className="icon" title="Open the graph's palette: its inputs as fields, Bake without the canvas" data-action="graph-palette" onClick={() => togglePalette(current.id)}>
            <PanelsTopLeft size={14} /></button>
          <button type="button" className={`icon${preview ? ' on' : ''}`} title={preview ? 'Hide the preview' : 'Show the preview in the views'} data-action="graph-preview" onClick={() => setPreview(!preview)}>
            {preview ? <Eye size={14} /> : <EyeOff size={14} />}
          </button>
          <button type="button" className="primary" data-action="graph-bake" title="Make what the graph previews, in one undo step" onClick={() => void bake()}><Flame size={14} /> Bake</button>
        </>
      )}
    </div>
  );
}

function Editor() {
  const specs = useGraphs((s) => s.specs);
  const result = useGraphs((s) => s.result);
  const graphId = useGraphs((s) => s.graphId);
  const node = useSession((s) => (graphId ? s.nodes.get(graphId) : undefined));
  const version = useSession((s) => s.version);
  const saved = useMemo(() => currentGraph(), [node]); // eslint-disable-line react-hooks/exhaustive-deps
  const [draft, setDraft] = useState<GraphJson | null>(null);
  const graph = draft ?? saved;
  const [view, setView] = useState<ViewT>({ x: -40, y: -40, zoom: 1 });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [wireSel, setWireSel] = useState<string | null>(null);
  const [drag, setDrag] = useState<Drag | null>(null);
  const [search, setSearch] = useState<{ sx: number; sy: number; gx: number; gy: number } | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [space, setSpace] = useState(false);
  const host = useRef<HTMLDivElement>(null);
  const fitted = useRef(false);
  // Panned or zoomed by hand: the panel's resizing no longer fits the graph again (Fit does, and starts over).
  const touched = useRef(false);
  const fitNow = useRef(() => {});
  fitNow.current = () => {
    if (!specs || !host.current) return;
    const r = host.current.getBoundingClientRect();
    if (r.width < 1 || r.height < 1) return;
    setView(fitView(graph, specOf, r.width, r.height));
  };
  const fit = () => { touched.current = false; fitNow.current(); host.current?.focus(); };
  // The whole graph in sight when it opens (once its nodes' sizes are known), and again as the panel is resized until the
  // user pans or zooms.
  useLayoutEffect(() => {
    if (fitted.current || !specs || !host.current) return;
    fitted.current = true;
    fitNow.current();
  }, [specs]);
  useEffect(() => {
    const el = host.current;
    if (!el || typeof ResizeObserver === 'undefined') return;
    const ro = new ResizeObserver(() => { if (fitted.current && !touched.current) fitNow.current(); });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  // Evaluated after each change of the graph, and of the model (a graph reads it), a moment after the last.
  const graphText = JSON.stringify(saved);
  useEffect(() => {
    const t = setTimeout(() => void evaluate(), 200);
    return () => clearTimeout(t);
  }, [graphText, version]);

  const specOf = (kind: string, node?: GraphNode): NodeSpec => {
    if (kind === GROUP && node) return groupSpec(node, (k, n) => specOf(k, n));
    return specs?.get(kind) ?? unknownSpec(node ?? graph.nodes.find((n) => n.kind === kind) ?? { id: '', kind, x: 0, y: 0 }, graph);
  };
  const commit = (g: GraphJson) => { setDraft(g); void saveGraph(g).finally(() => setDraft(null)); };

  const toGraph = (e: { clientX: number; clientY: number }) => {
    const r = host.current!.getBoundingClientRect();
    return { x: view.x + (e.clientX - r.left) / view.zoom, y: view.y + (e.clientY - r.top) / view.zoom };
  };

  const onBackgroundDown = (e: React.PointerEvent) => {
    host.current?.focus();
    setSearch(null);
    (e.currentTarget as Element).setPointerCapture?.(e.pointerId);
    if (e.button === 1 || e.button === 2 || space) { setDrag({ kind: 'pan', sx: e.clientX, sy: e.clientY, view }); return; }
    if (e.button !== 0) return;
    if (!e.shiftKey) { setSelected(new Set()); setWireSel(null); }
    const p = toGraph(e);
    setDrag({ kind: 'box', a: p, b: p });
  };

  const onMove = (e: React.PointerEvent) => {
    if (!drag) return;
    if (drag.kind === 'pan') { touched.current = true; setView({ ...drag.view, x: drag.view.x - (e.clientX - drag.sx) / view.zoom, y: drag.view.y - (e.clientY - drag.sy) / view.zoom }); return; }
    const p = toGraph(e);
    if (drag.kind === 'box') setDrag({ ...drag, b: p });
    else if (drag.kind === 'wire') setDrag({ ...drag, at: p });
    else if (drag.kind === 'move') {
      const dx = p.x - drag.start.x;
      const dy = p.y - drag.start.y;
      if (!drag.moved && Math.abs(dx) + Math.abs(dy) < 3 / view.zoom) return;
      setDraft(moveNodes(saved, drag.ids, dx, dy));
      if (!drag.moved) setDrag({ ...drag, moved: true });
    }
  };

  const onUp = (e: React.PointerEvent) => {
    if (!drag) return;
    if (drag.kind === 'box') {
      const ids = nodesIn(graph, specOf, drag.a, drag.b);
      if (ids.length > 0) setSelected((s) => new Set([...(e.shiftKey ? s : []), ...ids]));
    } else if (drag.kind === 'move') {
      if (drag.moved) { const p = toGraph(e); commit(moveNodes(saved, drag.ids, p.x - drag.start.x, p.y - drag.start.y)); }
    } else if (drag.kind === 'wire') {
      // Dropped on an input's socket (its data attributes), else nowhere.
      const el = document.elementFromPoint(e.clientX, e.clientY)?.closest('[data-port-in]') as HTMLElement | null;
      if (el) {
        const r = connect(saved, specOf, { from: drag.from, out: drag.out, to: el.dataset.node!, in: el.dataset.portIn! });
        if ('refused' in r) setRefusal(r.refused); else { setRefusal(null); commit(r.graph); }
      }
    }
    setDrag(null);
  };

  const onWheel = (e: React.WheelEvent) => {
    const r = host.current!.getBoundingClientRect();
    touched.current = true;
    setView((v) => zoomAt(v, e.clientX - r.left, e.clientY - r.top, Math.exp(-e.deltaY * 0.0015)));
  };

  const onKey = (e: React.KeyboardEvent) => {
    if ((e.target as HTMLElement).closest('input, select, textarea')) return;
    if (e.key === 'Delete' || e.key === 'Backspace') {
      e.preventDefault();
      e.stopPropagation();
      if (selected.size > 0) { commit(removeNodes(saved, selected)); setSelected(new Set()); }
      else if (wireSel) { commit(removeWire(saved, wireSel)); setWireSel(null); }
    } else if (e.key === 'Escape') { e.stopPropagation(); setSearch(null); setSelected(new Set()); setWireSel(null); }
    else if (e.key === ' ') { e.preventDefault(); e.stopPropagation(); setSpace(true); }
    else if ((e.key === 'f' || e.key === 'F' || e.key === 'Home') && !e.ctrlKey && !e.metaKey && !e.altKey) { e.preventDefault(); e.stopPropagation(); fit(); }
    else if ((e.key === 'g' || e.key === 'G') && !e.ctrlKey && !e.metaKey && !e.altKey && selected.size > 0) { e.preventDefault(); e.stopPropagation(); collapseSelected(); }
  };

  const onDouble = (e: React.MouseEvent) => {
    if ((e.target as Element).closest('.graph-node')) return;
    const r = host.current!.getBoundingClientRect();
    const g = toGraph(e);
    setSearch({ sx: e.clientX - r.left, sy: e.clientY - r.top, gx: g.x, gy: g.y });
  };

  const add = (spec: NodeSpec) => {
    if (!search) return;
    const r = addNode(saved, spec, search.gx, search.gy);
    setSearch(null);
    setSelected(new Set([r.id]));
    commit(r.graph);
  };

  const startMove = (e: React.PointerEvent, id: string) => {
    e.stopPropagation();
    if (e.button !== 0) return;
    host.current?.focus();
    host.current?.setPointerCapture?.(e.pointerId);
    let ids = selected;
    if (e.shiftKey) { ids = new Set(selected); if (ids.has(id)) ids.delete(id); else ids.add(id); setSelected(ids); return; }
    if (!selected.has(id)) { ids = new Set([id]); setSelected(ids); }
    setWireSel(null);
    setDrag({ kind: 'move', start: toGraph(e), ids, moved: false });
  };

  const startWire = (e: React.PointerEvent, id: string, out: string) => {
    e.stopPropagation();
    if (e.button !== 0) return;
    host.current?.setPointerCapture?.(e.pointerId);
    setDrag({ kind: 'wire', from: id, out, at: toGraph(e) });
  };

  const one = selected.size === 1 ? graph.nodes.find((n) => selected.has(n.id)) : undefined;

  /** The selection collapsed into one group (one undo step), the group selected. */
  function collapseSelected() {
    const r = collapse(saved, selected, tr('Group'));
    if (!r) return;
    setSelected(new Set([r.id]));
    commit(r.graph);
    host.current?.focus();
  }

  return (
    <div className="graphs-body">
      <div ref={host} className={`graphs-canvas${space || drag?.kind === 'pan' ? ' panning' : ''}`} tabIndex={0} data-testid="graphs-canvas"
        onPointerDown={onBackgroundDown} onPointerMove={onMove} onPointerUp={onUp} onWheel={onWheel} onDoubleClick={onDouble}
        onKeyDown={onKey} onKeyUp={(e) => { if (e.key === ' ') setSpace(false); }} onContextMenu={(e) => e.preventDefault()}>
        <svg width="100%" height="100%">
          <g transform={`scale(${view.zoom}) translate(${-view.x} ${-view.y})`}>
            {graph.wires.map((w) => {
              const from = graph.nodes.find((n) => n.id === w.from);
              const to = graph.nodes.find((n) => n.id === w.to);
              const a = from && portAt(from, specOf(from.kind, from), 'out', w.out);
              const b = to && portAt(to, specOf(to.kind, to), 'in', w.in);
              if (!a || !b) return null;
              const key = wireKey(w);
              const count = result?.nodes[w.from]?.counts?.[w.out];
              const type = specOf(from.kind, from).outputs.find((p) => p.name === w.out)?.type ?? 'any';
              return (
                <g key={key} className={`graph-wire${wireSel === key ? ' on' : ''}`} onPointerDown={(e) => { e.stopPropagation(); setWireSel(key); setSelected(new Set()); host.current?.focus(); }}>
                  <path d={wirePath(a, b)} className="hit" />
                  <path d={wirePath(a, b)} stroke={PORT_COLOURS[type]} strokeWidth={count !== undefined ? 4.5 : 2} fill="none" data-list={count !== undefined ? count : undefined} />
                  {count !== undefined && <text x={(a.x + b.x) / 2} y={(a.y + b.y) / 2 - 5} className="graph-wire-count" data-no-i18n>{count}</text>}
                </g>
              );
            })}
            {drag?.kind === 'wire' && (() => {
              const from = graph.nodes.find((n) => n.id === drag.from);
              const a = from && portAt(from, specOf(from.kind, from), 'out', drag.out);
              return a ? <path d={wirePath(a, drag.at)} className="graph-wire-draft" /> : null;
            })()}
            {graph.nodes.map((n) => (
              <NodeBox key={n.id} node={n} spec={specOf(n.kind, n)} on={selected.has(n.id)} result={result?.nodes[n.id]} wired={graph.wires}
                onHead={(e) => startMove(e, n.id)} onOut={(e, port) => startWire(e, n.id, port)} />
            ))}
            {drag?.kind === 'box' && (
              <rect className="graph-box" x={Math.min(drag.a.x, drag.b.x)} y={Math.min(drag.a.y, drag.b.y)} width={Math.abs(drag.b.x - drag.a.x)} height={Math.abs(drag.b.y - drag.a.y)} />
            )}
          </g>
        </svg>
        {selected.size > 0 && (
          <button type="button" className="icon graphs-collapse" title="Collapse the selected nodes into one group (G)" data-action="graph-collapse"
            onPointerDown={(e) => e.stopPropagation()} onClick={collapseSelected}><Group size={14} /></button>
        )}
        {graph.nodes.length > 0 && (
          <button type="button" className="icon graphs-fit" title="Fit the graph in the canvas (F)" data-action="graph-fit"
            onPointerDown={(e) => e.stopPropagation()} onClick={fit}><Maximize2 size={14} /></button>
        )}
        {graph.nodes.length === 0 && !search && <div className="graphs-hint muted">Double-click to add a node.</div>}
        {refusal && <div className="graphs-refusal error-text" onClick={() => setRefusal(null)}>{refusal}</div>}
        {search && specs && <Search x={search.sx} y={search.sy} specs={specs} onPick={add} onClose={() => setSearch(null)} />}
      </div>
      <Inspector key={one?.id ?? ''} graph={graph} id={one?.id ?? null} spec={one ? specOf(one.kind, one) : null} onChange={commit}
        onExpand={(gid) => { const r = expand(saved, gid); if (r) { setSelected(new Set(r.ids)); commit(r.graph); } }} />
    </div>
  );
}

function NodeBox({ node, spec, on, result, wired, onHead, onOut }: {
  node: GraphJson['nodes'][number]; spec: NodeSpec; on: boolean; result?: { error?: string; warning?: string; outputs: Record<string, unknown> };
  wired: GraphJson['wires']; onHead: (e: React.PointerEvent) => void; onOut: (e: React.PointerEvent, port: string) => void;
}) {
  useLang((l) => l.lang); // drawn again in another language: the SVG is not walked by the translator
  const sessionNodes = useSession((s) => s.nodes);
  const storeyName = (id: string) => sessionNodes.get(id)?.name;
  const s = sockets(spec);
  const h = nodeHeight(spec);
  const settings = spec.inputs.filter((p) => p.setting);
  const state = result?.error ? ' failed' : result?.warning ? ' warned' : '';
  const label = node.label ?? tr(spec.label);
  return (
    <g className={`graph-node${on ? ' on' : ''}${state}${node.kind === GROUP ? ' group' : ''}`} transform={`translate(${node.x} ${node.y})`} data-graph-node={node.id} data-kind={node.kind}>
      <title>{result?.error ?? result?.warning ?? spec.about ?? spec.name ?? label}</title>
      <rect className="body" width={NODE_W} height={h} rx={6} />
      <rect className="head" width={NODE_W} height={HEAD_H} rx={6} onPointerDown={onHead} />
      <text x={10} y={17} className="graph-node-title" onPointerDown={onHead} {...(node.label ? { 'data-no-i18n': '' } : {})}>{label}</text>
      {s.inputs.map((p, i) => {
        const y = HEAD_H + i * ROW_H + ROW_H / 2;
        const isWired = wired.some((w) => w.to === node.id && w.in === p.name);
        const constant = node.values?.[p.name];
        return (
          <g key={`i${p.name}`}>
            <circle cx={0} cy={y} r={5} fill={isWired ? PORT_COLOURS[p.type] : 'var(--panel, #fff)'} stroke={PORT_COLOURS[p.type]} strokeWidth={2} className="graph-port" data-node={node.id} data-port-in={p.name} />
            <text x={10} y={y + 4} className="graph-port-label">{tr(p.label)}</text>
            {!isWired && constant !== undefined && (() => {
              const out = s.outputs[i];
              const right = valueEnd(out ? tr(out.label) : undefined);
              return <text x={right} y={y + 4} className="graph-port-value" textAnchor="end" data-no-i18n>{rowValue(constant, p.type, tr(p.label), storeyName, right)}</text>;
            })()}
          </g>
        );
      })}
      {s.outputs.map((p, i) => {
        const y = HEAD_H + i * ROW_H + ROW_H / 2;
        return (
          <g key={`o${p.name}`}>
            <text x={NODE_W - 10} y={y + 4} className="graph-port-label" textAnchor="end">{tr(p.label)}</text>
            <circle cx={NODE_W} cy={y} r={5} fill={PORT_COLOURS[p.type]} className="graph-port out" data-node={node.id} data-port-out={p.name} onPointerDown={(e) => onOut(e, p.name)} />
          </g>
        );
      })}
      {settings.length > 0 && s.inputs.length === 0 && (
        <text x={10} y={HEAD_H + ROW_H / 2 + 4} className="graph-port-value" data-no-i18n>
          {settings.map((p) => { const v = node.values?.[p.name] ?? p.default; return p.type === 'storey' ? (typeof v === 'string' && v ? storeyName(v) ?? '?' : tr("The view's storey")) : showValue(v); }).join(' · ')}
        </text>
      )}
    </g>
  );
}

function Search({ x, y, specs, onPick, onClose }: { x: number; y: number; specs: Map<string, NodeSpec>; onPick: (s: NodeSpec) => void; onClose: () => void }) {
  const [text, setText] = useState('');
  const [at, setAt] = useState(0);
  const found = useMemo(() => searchNodes(specs.values(), text), [specs, text]);
  return (
    <div className="graphs-search" style={{ left: x, top: y }} onPointerDown={(e) => e.stopPropagation()} onDoubleClick={(e) => e.stopPropagation()} role="dialog" aria-label="Add a node">
      <input autoFocus placeholder="Search nodes: wall, range, point…" value={text} data-field="graph-search"
        onChange={(e) => { setText(e.target.value); setAt(0); }}
        onKeyDown={(e) => {
          if (e.key === 'ArrowDown') { e.preventDefault(); setAt((a) => Math.min(found.length - 1, a + 1)); }
          else if (e.key === 'ArrowUp') { e.preventDefault(); setAt((a) => Math.max(0, a - 1)); }
          else if (e.key === 'Enter' && found[at]) onPick(found[at]!);
          else if (e.key === 'Escape') onClose();
        }} />
      <div className="graphs-search-list">
        {found.map((s, i) => (
          <button type="button" key={s.kind} className={i === at ? 'on' : ''} onClick={() => onPick(s)} data-node-kind={s.kind} title={s.about ?? s.name ?? ''}>
            <span>{s.label}</span> <span className="muted small">{s.group}</span>
          </button>
        ))}
        {found.length === 0 && <p className="muted small">No node is called so.</p>}
      </div>
    </div>
  );
}

function Inspector({ graph, id, spec, onChange, onExpand }: { graph: GraphJson; id: string | null; spec: NodeSpec | null; onChange: (g: GraphJson) => void; onExpand: (id: string) => void }) {
  const result = useGraphs((s) => (id ? s.result?.nodes[id] : undefined));
  const node = id ? graph.nodes.find((n) => n.id === id) : undefined;
  if (!node || !spec) {
    return (
      <aside className="graphs-inspector" data-field="graph-inspector">
        <p className="muted small">Select a node to set its inputs and see what it gives.</p>
        <ToolSettings graph={graph} onChange={onChange} />
      </aside>
    );
  }
  const wired = new Set(graph.wires.filter((w) => w.to === node.id).map((w) => w.in));
  const group = node.kind === GROUP;
  return (
    <aside className="graphs-inspector" data-field="graph-inspector">
      <div className="panel-title">{spec.label}</div>
      {spec.name && <div className="muted small" data-no-i18n>{spec.name}</div>}
      {spec.about && <p className="muted small">{spec.about}</p>}
      <label className="field"><span>Name in the graph</span>
        <input defaultValue={node.label ?? ''} placeholder={spec.label} onBlur={(e) => { if ((e.target.value.trim() || undefined) !== node.label) onChange(setLabel(graph, node.id, e.target.value)); }} />
      </label>
      {group && (
        <>
          <p className="muted small"><span data-no-i18n>{node.graph?.nodes.length ?? 0}</span> <span>nodes inside</span></p>
          <button type="button" className="btn" data-action="graph-expand" onClick={() => onExpand(node.id)}><Ungroup size={14} /> Expand the group</button>
        </>
      )}
      {!group && spec.inputs.filter((p) => !wired.has(p.name)).map((p) => (
        <Field key={p.name} port={p} value={node.values?.[p.name]} onSet={(v) => onChange(setValue(graph, node.id, p.name, v))} />
      ))}
      {result?.error && <p className="error-text small" data-field="graph-error">{result.error}</p>}
      {result?.warning && <p className="warning-text small">{result.warning}</p>}
      {result && !result.error && spec.outputs.map((p) => (
        <div key={p.name} className="graphs-output small"><b>{p.label}</b> <span data-no-i18n>{showValue(result.outputs[p.name])}</span></div>
      ))}
      {!group && <PaletteSettings graph={graph} node={node} spec={spec} onChange={onChange} />}
    </aside>
  );
}

/** A number typed in the inspector's small fields (empty: none), committed when left. */
function SmallNumber({ label, value, onSet, field }: { label: string; value: unknown; onSet: (v: number | undefined) => void; field: string }) {
  return (
    <label className="field"><span>{label}</span>
      <input className="num" inputMode="decimal" defaultValue={numberText(value)} data-palette-setting={field}
        onBlur={(e) => {
          const t = e.target.value.trim();
          if (!t) { onSet(undefined); return; }
          const v = commitNumber(t, null);
          if (v === NO_COMMIT || v === null) e.target.value = numberText(value); else { e.target.value = numberText(v); onSet(v); }
        }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
    </label>
  );
}

/**
 * How the node shows in the graph's palette (decision 536): an input node's field (shown, its label, its order, and a
 * number's least, most and step: a least and a most give it a slider); another node's values shown or not (a Watch is
 * shown unless it is hidden).
 */
function PaletteSettings({ graph, node, spec, onChange }: { graph: GraphJson; node: GraphNode; spec: NodeSpec; onChange: (g: GraphJson) => void }) {
  const p = (node.palette ?? {}) as NodePalette;
  const change = (c: Record<string, unknown>) => onChange(setPalette(graph, node.id, c));
  if (isInput(node.kind)) {
    const numeric = node.kind === 'number' || node.kind === 'integer';
    return (
      <div className="graphs-section" data-field="graph-palette-settings">
        <span className="graphs-section-title">In the palette</span>
        <label className="field check"><input type="checkbox" checked={!p.hide} data-palette-setting="show" onChange={(e) => change({ hide: e.target.checked ? undefined : true })} /> <span>Show in the palette</span></label>
        {!p.hide && (
          <>
            <label className="field"><span>Label in the palette</span>
              <input defaultValue={p.label ?? ''} placeholder={node.label ?? tr(spec.label)} data-palette-setting="label" onBlur={(e) => { if ((e.target.value.trim() || undefined) !== p.label) change({ label: e.target.value.trim() || undefined }); }}
                onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
            </label>
            <SmallNumber label="Order" field="order" value={p.order} onSet={(v) => change({ order: v })} />
            {numeric && (
              <div className="graphs-limits">
                <SmallNumber label="Least" field="min" value={p.min} onSet={(v) => change({ min: v })} />
                <SmallNumber label="Most" field="max" value={p.max} onSet={(v) => change({ max: v })} />
                <SmallNumber label="Step" field="step" value={p.step} onSet={(v) => change({ step: v })} />
              </div>
            )}
          </>
        )}
      </div>
    );
  }
  const watch = node.kind === 'watch';
  const on = watch ? !p.hide : p.show === true;
  return (
    <div className="graphs-section" data-field="graph-palette-settings">
      <span className="graphs-section-title">In the palette</span>
      <label className="field check">
        <input type="checkbox" checked={on} data-palette-setting="show" onChange={(e) => change(watch ? { hide: e.target.checked ? undefined : true } : { show: e.target.checked ? true : undefined })} />
        <span>Show its values in the palette</span>
      </label>
      {on && (
        <label className="field"><span>Label in the palette</span>
          <input defaultValue={p.label ?? ''} placeholder={node.label ?? tr(spec.label)} onBlur={(e) => { if ((e.target.value.trim() || undefined) !== p.label) change({ label: e.target.value.trim() || undefined }); }}
            onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
        </label>
      )}
    </div>
  );
}

/** The graph as a tool of My tools (decision 536): shown or not, its button's name, icon and tooltip, its group. */
function ToolSettings({ graph, onChange }: { graph: GraphJson; onChange: (g: GraphJson) => void }) {
  const t = (graph.tool ?? {}) as GraphTool;
  const graphId = useGraphs((s) => s.graphId);
  const name = useSession((s) => (graphId ? s.nodes.get(graphId)?.name : undefined));
  const change = (c: Record<string, unknown>) => onChange(setTool(graph, c));
  const text = (key: keyof GraphTool, label: string, placeholder: string) => (
    <label className="field"><span>{label}</span>
      <input defaultValue={String(t[key] ?? '')} placeholder={placeholder} data-tool-setting={key}
        onBlur={(e) => { if ((e.target.value.trim() || undefined) !== t[key]) change({ [key]: e.target.value.trim() || undefined }); }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
    </label>
  );
  return (
    <div className="graphs-section" data-field="graph-tool-settings">
      <span className="graphs-section-title">The graph as a tool</span>
      <label className="field check"><input type="checkbox" checked={t.show === true} data-tool-setting="show" onChange={(e) => change({ show: e.target.checked ? true : undefined })} /> <span>Show in My tools</span></label>
      <p className="muted small">Its button opens the graph's palette: the input nodes as fields, the watched values, and Bake.</p>
      {t.show === true && (
        <>
          {text('name', "Button's name", name ?? '')}
          {text('icon', 'Icon', 'wall, grid, star, P…')}
          {text('tooltip', 'Tooltip', '')}
          {text('group', 'Group in My tools', tr(GRAPHS_GROUP))}
          <label className="field"><span>Palette values</span>
            <select value={t.values === 'own' ? 'own' : 'shared'} data-tool-setting="values"
              onChange={(e) => change({ values: e.target.value === 'own' ? 'own' : undefined })}>
              <option value="shared">Shared</option>
              <option value="own">Each user's own</option>
            </select>
          </label>
          <p className="muted small">{t.values === 'own'
            ? "The graph's values are the defaults: what each user changes in the palette stays on their computer, and Bake does not change the graph."
            : 'What the palette changes is written into the graph: everyone sees the same values.'}</p>
        </>
      )}
    </div>
  );
}

/** One constant: a number, a text, a yes/no, a choice, a point; anything else as JSON. */
function Field({ port, value, onSet }: { port: PortSpec; value: unknown; onSet: (v: unknown) => void }) {
  const shown = value ?? port.default;
  const nodes = useSession((s) => s.nodes);
  if (port.setting && port.type === 'element') {
    const ids = Array.isArray(value) ? (value as string[]) : [];
    return (
      <div className="field"><span>{port.label}</span>
        <span className="graphs-pick"><span data-no-i18n>{ids.length}</span>
          <button type="button" data-action="graph-use-selection" onClick={() => { const u = useUi.getState(); onSet(u.selected.length > 0 ? [...u.selected] : u.selection ? [u.selection] : []); }}>Use the selection</button>
        </span>
      </div>
    );
  }
  if (port.type === 'storey') {
    const storeys = [...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey').sort((a, b) => Number(a.props.elevation ?? 0) - Number(b.props.elevation ?? 0));
    return (
      <label className="field"><span>{port.label}</span>
        <select value={String(shown ?? '')} onChange={(e) => onSet(e.target.value || null)}>
          <option value="">The view's storey</option>
          {storeys.map((n) => <option key={n.id} value={n.id} data-no-i18n>{n.name}</option>)}
        </select>
      </label>
    );
  }
  if (port.choices) {
    return (
      <label className="field"><span>{port.label}</span>
        <select value={String(shown ?? '')} onChange={(e) => onSet(e.target.value)}>{port.choices.map((c) => <option key={c} value={c}>{c}</option>)}</select>
      </label>
    );
  }
  if (port.type === 'bool') return <label className="field check"><input type="checkbox" checked={Boolean(shown)} onChange={(e) => onSet(e.target.checked)} /> <span>{port.label}</span></label>;
  if (port.type === 'number' || port.type === 'integer') {
    return (
      <label className="field"><span>{port.label}</span>
        <input className="num" inputMode="decimal" defaultValue={numberText(shown)} data-port={port.name}
          onBlur={(e) => {
            const t = e.target.value;
            if (!t.trim()) { onSet(null); return; }
            const v = commitNumber(t, null, { integer: port.type === 'integer' });
            if (v === NO_COMMIT || v === null) e.target.value = numberText(shown); else { e.target.value = numberText(v); onSet(v); }
          }}
          onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
      </label>
    );
  }
  if (port.type === 'point') {
    const p = (value as { $pt?: number[] } | undefined)?.$pt ?? [0, 0, 0];
    return (
      <label className="field"><span>{port.label}</span>
        <span className="graphs-xyz">
          {[0, 1, 2].map((i) => (
            <input key={i} className="num" inputMode="decimal" defaultValue={numberText(p[i] ?? 0)} aria-label={['X', 'Y', 'Z'][i]}
              onBlur={(e) => {
                const v = e.target.value.trim() ? commitNumber(e.target.value, null) : 0;
                const n = v === NO_COMMIT || v === null ? p[i] ?? 0 : v;
                e.target.value = numberText(n);
                const q = [...p]; q[i] = n; onSet({ $pt: q });
              }} />
          ))}
        </span>
      </label>
    );
  }
  const text = shown === undefined || shown === null ? '' : typeof shown === 'string' ? shown : JSON.stringify(shown);
  return (
    <label className="field"><span>{port.label}</span>
      <input defaultValue={text} data-port={port.name}
        onBlur={(e) => {
          const t = e.target.value;
          if (port.type === 'text' || port.type === 'storey' || port.type === 'element') { onSet(t); return; }
          try { onSet(t.trim() === '' ? null : JSON.parse(t)); } catch { onSet(t); }
        }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
    </label>
  );
}
