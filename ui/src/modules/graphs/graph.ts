/**
 * Visual graphs (docs/plan-visual-graphs.md, decision 522): the graph as the engine keeps it (format 1: nodes and wires),
 * the nodes' specs (the engine's own nodes from graphs.nodes, the product's commands and queries from session.catalogue),
 * and the editing a canvas does to it — pure functions, so the canvas only draws and the engine evaluates.
 */

export type PortType = 'number' | 'integer' | 'text' | 'bool' | 'point' | 'geometry' | 'element' | 'storey' | 'list' | 'any';

export interface PortSpec {
  name: string;
  label: string;
  type: PortType;
  /** Takes a list as it is, never mapped over. */
  whole?: boolean;
  /** Set in the inspector only: no socket. */
  setting?: boolean;
  default?: unknown;
  choices?: string[];
}

export interface NodeSpec {
  kind: string;
  label: string;
  group: string;
  inputs: PortSpec[];
  outputs: PortSpec[];
  about?: string | null;
  /** command:… / query:…: the engine's name, searched too. */
  name?: string;
}

export interface GraphNode {
  id: string;
  kind: string;
  x: number;
  y: number;
  values?: Record<string, unknown>;
  whole?: string[];
  label?: string;
}

export interface GraphWire { from: string; out: string; to: string; in: string }

export interface GraphJson { format: 1; nodes: GraphNode[]; wires: GraphWire[] }

export const emptyGraph = (): GraphJson => ({ format: 1, nodes: [], wires: [] });

/** What graphs.nodes answers. */
export interface EngineNodes {
  nodes: NodeSpec[];
  commands: { name: string; label: string; module: string }[];
  queries: string[];
}

/** What session.catalogue answers (the part read here). */
export interface Catalogue {
  commands: { name: string; module: string; label: string; args: { name: string; type?: string }[] }[];
  queries: { name: string; module: string; args: { name: string; type?: string }[] }[];
}

/** The groups the catalogue's modules go under, by module id; another module's group is its id, capitalised. */
const MODULE_GROUPS: Record<string, string> = {
  core: 'Model', architecture: 'Architecture', structure: 'Structure', analysis: 'Structure', rebar: 'Structure', wood: 'Timber', steel: 'Steel',
  drafting: 'Drafting', studio: 'Studio', civil: 'Civil', sitework: 'Site works', transport: 'Transport', furniture: 'Furniture', mep: 'MEP',
  planning: 'Planning', cost: 'Cost', phases: 'Phases', clash: 'Coordination',
};

const groupOf = (module: string) => MODULE_GROUPS[module] ?? module.charAt(0).toUpperCase() + module.slice(1);

/** A catalogue argument's type as a port's. */
export function portType(t: string | undefined): PortType {
  switch (t) {
    case 'float': return 'number';
    case 'int': return 'integer';
    case 'str': return 'text';
    case 'bool': return 'bool';
    case 'list': case 'dict': return 'list';
    default: return 'any';
  }
}

/** Words for an argument's name: storeyId → Storey, baseOffset → Base offset. */
export function argLabel(name: string): string {
  const n = name.replace(/Ids?$/, (m) => (m === 'Ids' ? 's' : '')).replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return n.charAt(0).toUpperCase() + n.slice(1);
}

/**
 * A command's or query's arguments as ports: pairs xN, yN become one point port pt:N (a wall's Start and End, two points
 * rather than four numbers; the engine splits the point again), the others as they are.
 */
export function argPorts(args: { name: string; type?: string }[]): PortSpec[] {
  const names = new Set(args.map((a) => a.name));
  const pairs = [...names].map((n) => /^x(\d*)$/.exec(n)?.[1]).filter((k): k is string => k !== undefined && names.has(`y${k}`));
  const paired = new Set(pairs.flatMap((k) => [`x${k}`, `y${k}`]));
  const ports: PortSpec[] = [];
  const pointLabel = (k: string) => (pairs.length === 2 && k === pairs[0] ? 'Start' : pairs.length === 2 && k === pairs[1] ? 'End' : k ? `Point ${k}` : 'Point');
  for (const a of args) {
    const k = /^x(\d*)$/.exec(a.name)?.[1];
    if (k !== undefined && paired.has(a.name)) { ports.push({ name: `pt:${k}`, label: pointLabel(k), type: 'point' }); continue; }
    if (paired.has(a.name)) continue;
    const type = a.name === 'storeyId' ? 'storey' : portType(a.type);
    ports.push({ name: a.name, label: argLabel(a.name), type, ...(type === 'list' ? { whole: true } : {}) });
  }
  return ports;
}

/** Every node a graph may hold, by kind: the engine's own, then the commands and queries the product has and a graph may run. */
export function nodeSpecs(engine: EngineNodes, catalogue: Catalogue | null): Map<string, NodeSpec> {
  const specs = new Map<string, NodeSpec>();
  for (const n of engine.nodes) specs.set(n.kind, n);
  const args = new Map((catalogue?.commands ?? []).map((c) => [c.name, c.args]));
  for (const c of engine.commands) {
    specs.set(`command:${c.name}`, {
      kind: `command:${c.name}`, label: c.label, group: groupOf(c.module), name: c.name,
      inputs: argPorts(args.get(c.name) ?? []),
      outputs: [{ name: 'ids', label: 'Elements made', type: 'element' }, { name: 'result', label: 'Result', type: 'any' }],
    });
  }
  const qargs = new Map((catalogue?.queries ?? []).map((q) => [q.name, q]));
  for (const name of engine.queries) {
    const q = qargs.get(name);
    specs.set(`query:${name}`, {
      kind: `query:${name}`, label: argLabel(name.split('.').pop() ?? name), group: `${groupOf(q?.module ?? name.split('.')[0]!)} questions`, name,
      inputs: argPorts(q?.args ?? []), outputs: [{ name: 'result', label: 'Answer', type: 'any' }],
    });
  }
  return specs;
}

/** A spec for a kind this build does not know (a graph from another edition): its wires' ports, so it still draws. */
export function unknownSpec(node: GraphNode, graph: GraphJson): NodeSpec {
  const ins = [...new Set([...graph.wires.filter((w) => w.to === node.id).map((w) => w.in), ...Object.keys(node.values ?? {})])];
  const outs = [...new Set(graph.wires.filter((w) => w.from === node.id).map((w) => w.out))];
  return { kind: node.kind, label: node.kind.split(':').pop() ?? node.kind, group: '?', inputs: ins.map((n) => ({ name: n, label: n, type: 'any' })), outputs: outs.map((n) => ({ name: n, label: n, type: 'any' })) };
}

/** The nodes whose name, group or engine name hold every word typed, best first (a name starting so, then a word of it, then the rest). */
export function searchNodes(specs: Iterable<NodeSpec>, text: string, most = 40): NodeSpec[] {
  const words = text.toLowerCase().split(/\s+/).filter(Boolean);
  const all = [...specs];
  if (words.length === 0) return all.filter((s) => !s.kind.includes(':')).slice(0, most);
  const scored: [number, NodeSpec][] = [];
  for (const s of all) {
    const label = s.label.toLowerCase();
    const hay = `${label} ${s.group.toLowerCase()} ${(s.name ?? s.kind).toLowerCase()}`;
    if (!words.every((w) => hay.includes(w))) continue;
    const first = words[0]!;
    const score = (label.startsWith(first) ? 0 : label.split(/\s+/).some((w) => w.startsWith(first)) ? 1 : 2) + (s.kind.startsWith('query:') ? 0.5 : s.kind.includes(':') ? 0.2 : 0);
    scored.push([score, s]);
  }
  return scored.sort((a, b) => a[0] - b[0] || a[1].label.localeCompare(b[1].label)).slice(0, most).map((x) => x[1]);
}

/** Whether a wire may go from an output of one type into an input of another. */
export function fits(from: PortType, to: PortType): boolean {
  if (from === to || from === 'any' || to === 'any' || to === 'list') return true;
  const numbers: PortType[] = ['number', 'integer', 'bool'];
  const names: PortType[] = ['text', 'element', 'storey'];
  if (numbers.includes(from) && numbers.includes(to)) return true;
  if (names.includes(from) && names.includes(to)) return true;
  if (from === 'list') return true; // a list maps over the input
  if ((from === 'number' || from === 'integer') && to === 'text') return true;
  return false;
}

/** Whether a wire from → to would close a circle (to already feeds from). */
export function wouldCycle(graph: GraphJson, from: string, to: string): boolean {
  if (from === to) return true;
  const seen = new Set<string>();
  const stack = [to];
  while (stack.length > 0) {
    const n = stack.pop()!;
    if (n === from) return true;
    if (seen.has(n)) continue;
    seen.add(n);
    for (const w of graph.wires) if (w.from === n) stack.push(w.to);
  }
  return false;
}

/** A fresh id for a node of this graph: n1, n2… */
export function newNodeId(graph: GraphJson): string {
  let max = 0;
  for (const n of graph.nodes) { const m = /^n(\d+)$/.exec(n.id); if (m) max = Math.max(max, Number(m[1])); }
  return `n${max + 1}`;
}

/** A node of <spec> added at (x, y); a catalogue node notes which of its inputs take a list whole (the engine does not read the catalogue). */
export function addNode(graph: GraphJson, spec: NodeSpec, x: number, y: number, values?: Record<string, unknown>): { graph: GraphJson; id: string } {
  const id = newNodeId(graph);
  const node: GraphNode = { id, kind: spec.kind, x: Math.round(x), y: Math.round(y) };
  if (values && Object.keys(values).length > 0) node.values = values;
  if (spec.kind.includes(':')) {
    const whole = spec.inputs.filter((p) => p.whole).map((p) => p.name);
    if (whole.length > 0) node.whole = whole;
  }
  return { graph: { ...graph, nodes: [...graph.nodes, node] }, id };
}

/** Nodes moved by (dx, dy). */
export function moveNodes(graph: GraphJson, ids: ReadonlySet<string>, dx: number, dy: number): GraphJson {
  return { ...graph, nodes: graph.nodes.map((n) => (ids.has(n.id) ? { ...n, x: Math.round(n.x + dx), y: Math.round(n.y + dy) } : n)) };
}

/** Nodes taken out, with their wires. */
export function removeNodes(graph: GraphJson, ids: ReadonlySet<string>): GraphJson {
  return { ...graph, nodes: graph.nodes.filter((n) => !ids.has(n.id)), wires: graph.wires.filter((w) => !ids.has(w.from) && !ids.has(w.to)) };
}

export const wireKey = (w: GraphWire) => `${w.from}.${w.out}>${w.to}.${w.in}`;

export function removeWire(graph: GraphJson, key: string): GraphJson {
  return { ...graph, wires: graph.wires.filter((w) => wireKey(w) !== key) };
}

/**
 * A wire drawn: refused (with why) when it closes a circle or the types do not meet; an input holds one wire, so one
 * already on it is replaced.
 */
export function connect(graph: GraphJson, specs: (kind: string) => NodeSpec | undefined, wire: GraphWire): { graph: GraphJson } | { refused: string } {
  const from = graph.nodes.find((n) => n.id === wire.from);
  const to = graph.nodes.find((n) => n.id === wire.to);
  if (!from || !to) return { refused: 'No such node.' };
  if (wouldCycle(graph, wire.from, wire.to)) return { refused: 'A node cannot feed itself, even through others.' };
  const out = specs(from.kind)?.outputs.find((p) => p.name === wire.out);
  const inp = specs(to.kind)?.inputs.find((p) => p.name === wire.in);
  if (out && inp && !fits(out.type, inp.type)) return { refused: `A ${out.type} does not go into a ${inp.type}.` };
  const wires = graph.wires.filter((w) => !(w.to === wire.to && w.in === wire.in));
  return { graph: { ...graph, wires: [...wires, wire] } };
}

/** A node's constant set (null or '' takes it out: the port's default stands). */
export function setValue(graph: GraphJson, id: string, port: string, value: unknown): GraphJson {
  return {
    ...graph,
    nodes: graph.nodes.map((n) => {
      if (n.id !== id) return n;
      const values = { ...(n.values ?? {}) };
      if (value === null || value === undefined || value === '') delete values[port]; else values[port] = value;
      const next: GraphNode = { ...n, values };
      if (Object.keys(values).length === 0) delete next.values;
      return next;
    }),
  };
}

/** A node's own name in the graph ('' takes it back to its kind's). */
export function setLabel(graph: GraphJson, id: string, label: string): GraphJson {
  return { ...graph, nodes: graph.nodes.map((n) => { if (n.id !== id) return n; const next = { ...n }; if (label.trim()) next.label = label.trim().slice(0, 80); else delete next.label; return next; }) };
}

/** The graph as the engine keeps it, read from an entity's props (an unreadable one is empty). */
export function graphOf(props: Record<string, unknown> | undefined): GraphJson {
  const g = props?.graph as Partial<GraphJson> | undefined;
  if (!g || !Array.isArray(g.nodes)) return emptyGraph();
  return { format: 1, nodes: g.nodes as GraphNode[], wires: Array.isArray(g.wires) ? (g.wires as GraphWire[]) : [] };
}

// ── drawing ──

export const NODE_W = 184;
export const HEAD_H = 26;
export const ROW_H = 20;

/** The ports drawn as sockets (settings are in the inspector). */
export const sockets = (spec: NodeSpec) => ({ inputs: spec.inputs.filter((p) => !p.setting), outputs: spec.outputs });

/** A node's height: its head, then a row for each socket on its taller side, plus a line for its settings' values. */
export function nodeHeight(spec: NodeSpec): number {
  const s = sockets(spec);
  const settings = spec.inputs.some((p) => p.setting) ? 1 : 0;
  return HEAD_H + Math.max(1, s.inputs.length, s.outputs.length, settings) * ROW_H + 6;
}

/** Where a port's socket is, in graph coordinates. */
export function portAt(node: GraphNode, spec: NodeSpec, side: 'in' | 'out', port: string): { x: number; y: number } | null {
  const s = sockets(spec);
  const list = side === 'in' ? s.inputs : s.outputs;
  const i = list.findIndex((p) => p.name === port);
  if (i < 0) return null;
  return { x: node.x + (side === 'in' ? 0 : NODE_W), y: node.y + HEAD_H + i * ROW_H + ROW_H / 2 };
}

/** A wire's path: a horizontal S-curve from an output to an input. */
export function wirePath(a: { x: number; y: number }, b: { x: number; y: number }): string {
  const d = Math.max(40, Math.abs(b.x - a.x) / 2);
  return `M${a.x} ${a.y}C${a.x + d} ${a.y} ${b.x - d} ${b.y} ${b.x} ${b.y}`;
}

/** The colour of a port's type. */
export const PORT_COLOURS: Record<PortType, string> = {
  number: '#3b82f6', integer: '#2563eb', text: '#f59e0b', bool: '#ef4444', point: '#10b981', geometry: '#14b8a6',
  element: '#8b5cf6', storey: '#a855f7', list: '#64748b', any: '#94a3b8',
};

/** The view's transform: graph point → screen = (p − origin) · zoom. */
export interface GraphView { x: number; y: number; zoom: number }

/** The view zoomed by <factor> about a screen point, kept between 0.25× and 2.5×. */
export function zoomAt(v: GraphView, sx: number, sy: number, factor: number): GraphView {
  const zoom = Math.min(2.5, Math.max(0.25, v.zoom * factor));
  const gx = v.x + sx / v.zoom;
  const gy = v.y + sy / v.zoom;
  return { zoom, x: gx - sx / zoom, y: gy - sy / zoom };
}

/** The view that shows every node of the graph in a canvas of <width> × <height> pixels, never zoomed in past 1×. */
export function fitView(graph: GraphJson, specs: (kind: string) => NodeSpec, width: number, height: number, margin = 24): GraphView {
  if (graph.nodes.length === 0 || width <= 0 || height <= 0) return { x: -40, y: -40, zoom: 1 };
  const x0 = Math.min(...graph.nodes.map((n) => n.x)) - margin;
  const y0 = Math.min(...graph.nodes.map((n) => n.y)) - margin;
  const x1 = Math.max(...graph.nodes.map((n) => n.x + NODE_W)) + margin;
  const y1 = Math.max(...graph.nodes.map((n) => n.y + nodeHeight(specs(n.kind)))) + margin;
  const zoom = Math.max(0.25, Math.min(1, width / (x1 - x0), height / (y1 - y0)));
  return { zoom, x: x0 - (width / zoom - (x1 - x0)) / 2, y: y0 - (height / zoom - (y1 - y0)) / 2 };
}

/** The nodes inside a rectangle of the graph (a marquee). */
export function nodesIn(graph: GraphJson, specs: (kind: string) => NodeSpec, a: { x: number; y: number }, b: { x: number; y: number }): string[] {
  const [x0, x1] = [Math.min(a.x, b.x), Math.max(a.x, b.x)];
  const [y0, y1] = [Math.min(a.y, b.y), Math.max(a.y, b.y)];
  return graph.nodes.filter((n) => n.x < x1 && n.x + NODE_W > x0 && n.y < y1 && n.y + nodeHeight(specs(n.kind)) > y0).map((n) => n.id);
}

/** A value as a few words, for a node's footer and the inspector. */
export function showValue(v: unknown, depth = 0): string {
  if (v === null || v === undefined) return '—';
  if (typeof v === 'number') return Number.isInteger(v) ? String(v) : String(Math.round(v * 1000) / 1000);
  if (typeof v === 'string') return v.length > 40 ? `${v.slice(0, 40)}…` : v;
  if (typeof v === 'boolean') return v ? 'yes' : 'no';
  if (Array.isArray(v)) {
    if (depth > 0) return `[${v.length}]`;
    const more = v.find((x) => x && typeof x === 'object' && '$more' in (x as object)) as { $more?: number } | undefined;
    const items = v.filter((x) => !(x && typeof x === 'object' && '$more' in (x as object)));
    const n = items.length + (more?.$more ?? 0);
    return `${n} × [${items.slice(0, 4).map((x) => showValue(x, depth + 1)).join(', ')}${n > 4 ? ', …' : ''}]`;
  }
  if (typeof v === 'object') {
    const o = v as Record<string, unknown>;
    if (Array.isArray(o.$pt)) return `(${(o.$pt as number[]).map((x) => showValue(x, 1)).join(', ')})`;
    if (Array.isArray(o.$line)) return `${o.closed ? 'polygon' : 'line'} of ${(o.$line as unknown[]).length}`;
    if (typeof o.id === 'string') return o.id;
    return '{…}';
  }
  return String(v);
}
