/**
 * Visual graphs (docs/plan-visual-graphs.md, decision 527): the graph as the engine keeps it (format 1: nodes and wires),
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
  /** Format 2: how the node shows in the graph's palette (palette.ts's NodePalette). */
  palette?: Record<string, unknown>;
  /** Format 2, a group: its nodes and wires, and its open ports (an inner node's port each, named node.port on the group). */
  graph?: { nodes: GraphNode[]; wires: GraphWire[] };
  inputs?: GroupPort[];
  outputs?: GroupPort[];
}

export interface GroupPort { node: string; port: string; label?: string }

export interface GraphWire { from: string; out: string; to: string; in: string }

/** A graph as the engine keeps it: format 2 (stage 2: palette settings, the tool, groups); a format-1 graph reads the same. */
export interface GraphJson { format: 1 | 2; nodes: GraphNode[]; wires: GraphWire[]; tool?: Record<string, unknown> }

export const GRAPH_FORMAT = 2;
export const GROUP = 'group';

export const emptyGraph = (): GraphJson => ({ format: 2, nodes: [], wires: [] });

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

/** The arguments the commands read as lists by their name (the engine's Evaluator.ListArgument): ids, points, pts, shapes, every …Ids. */
export const LIST_ARG = /^(ids|points|pts|shapes)$|.Ids$/;

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
    // An argument the catalogue could not type but the commands read as a list (ids, points…): a list too, as the engine takes it.
    const type = a.name === 'storeyId' ? 'storey' : !a.type && LIST_ARG.test(a.name) ? 'list' : portType(a.type);
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

/** A fresh id for a node of this graph, unique in its groups too (so a group expanded never meets one): n1, n2… */
export function newNodeId(graph: { nodes: GraphNode[] }, prefix = 'n'): string {
  let max = 0;
  const walk = (nodes: GraphNode[]) => {
    for (const n of nodes) {
      const m = new RegExp(`^${prefix}(\\d+)$`).exec(n.id);
      if (m) max = Math.max(max, Number(m[1]));
      if (n.graph) walk(n.graph.nodes);
    }
  };
  walk(graph.nodes);
  return `${prefix}${max + 1}`;
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
export function connect(graph: GraphJson, specs: (kind: string, node?: GraphNode) => NodeSpec | undefined, wire: GraphWire): { graph: GraphJson } | { refused: string } {
  const from = graph.nodes.find((n) => n.id === wire.from);
  const to = graph.nodes.find((n) => n.id === wire.to);
  if (!from || !to) return { refused: 'No such node.' };
  if (wouldCycle(graph, wire.from, wire.to)) return { refused: 'A node cannot feed itself, even through others.' };
  const out = specs(from.kind, from)?.outputs.find((p) => p.name === wire.out);
  const inp = specs(to.kind, to)?.inputs.find((p) => p.name === wire.in);
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

/** The graph as the engine keeps it, read from an entity's props (an unreadable one is empty); written as format 2. */
export function graphOf(props: Record<string, unknown> | undefined): GraphJson {
  const g = props?.graph as Partial<GraphJson> | undefined;
  if (!g || !Array.isArray(g.nodes)) return emptyGraph();
  const out: GraphJson = { format: 2, nodes: g.nodes as GraphNode[], wires: Array.isArray(g.wires) ? (g.wires as GraphWire[]) : [] };
  if (g.tool && typeof g.tool === 'object') out.tool = g.tool;
  return out;
}

/** A node's palette settings changed (an undefined or empty one taken out; none left: no palette). */
export function setPalette(graph: GraphJson, id: string, change: Record<string, unknown>): GraphJson {
  return {
    ...graph,
    nodes: graph.nodes.map((n) => {
      if (n.id !== id) return n;
      const palette: Record<string, unknown> = { ...(n.palette ?? {}) };
      for (const [k, v] of Object.entries(change)) if (v === undefined || v === null || v === '') delete palette[k]; else palette[k] = v;
      const next: GraphNode = { ...n, palette };
      if (Object.keys(palette).length === 0) delete next.palette;
      return next;
    }),
  };
}

/** The graph's tool settings changed (show, name, icon, tooltip, group); none left: no tool. */
export function setTool(graph: GraphJson, change: Record<string, unknown>): GraphJson {
  const tool: Record<string, unknown> = { ...(graph.tool ?? {}) };
  for (const [k, v] of Object.entries(change)) if (v === undefined || v === null || v === '') delete tool[k]; else tool[k] = v;
  const next: GraphJson = { ...graph, tool };
  if (Object.keys(tool).length === 0) delete next.tool;
  return next;
}

// ── groups: a selection collapsed into one node, and expanded back (decision 536) ──

/** A group's port's name on the group: its inner node's id and port. */
export const groupPortName = (p: GroupPort) => `${p.node}.${p.port}`;

/** A group's spec: its open ports, each named and typed as the inner node's port (its own label when it has one). */
export function groupSpec(node: GraphNode, specOf: (kind: string, node?: GraphNode) => NodeSpec): NodeSpec {
  const inner = node.graph?.nodes ?? [];
  const port = (p: GroupPort, side: 'inputs' | 'outputs'): PortSpec => {
    const n = inner.find((x) => x.id === p.node);
    const ps = n ? specOf(n.kind, n)[side].find((x) => x.name === p.port) : undefined;
    const owner = n ? (n.label ?? specOf(n.kind, n).label) : p.node;
    return { name: groupPortName(p), label: p.label ?? (ps ? `${ps.label} (${owner})` : p.port), type: ps?.type ?? 'any', ...(ps?.whole ? { whole: true } : {}) };
  };
  return {
    kind: GROUP, label: 'Group', group: 'Groups', about: 'Nodes collapsed into one: its inputs and outputs are their ports wired from and to the rest of the graph.',
    inputs: (node.inputs ?? []).map((p) => port(p, 'inputs')), outputs: (node.outputs ?? []).map((p) => port(p, 'outputs')),
  };
}

/**
 * The nodes <ids> collapsed into one group: placed where the first of them stood, holding them and the wires between
 * them; a wire from outside into one of them becomes the group's input (one per inner port, so a value wired into two of
 * them is still two inputs only when it goes to two ports), a wire from one of them to outside the group's output. Null
 * when fewer than one node is chosen.
 */
export function collapse(graph: GraphJson, ids: ReadonlySet<string>, label = 'Group'): { graph: GraphJson; id: string } | null {
  const inside = graph.nodes.filter((n) => ids.has(n.id));
  if (inside.length === 0) return null;
  const id = newNodeId(graph, 'g');
  const innerWires = graph.wires.filter((w) => ids.has(w.from) && ids.has(w.to));
  const incoming = graph.wires.filter((w) => !ids.has(w.from) && ids.has(w.to));
  const outgoing = graph.wires.filter((w) => ids.has(w.from) && !ids.has(w.to));
  const inputs: GroupPort[] = [];
  for (const w of incoming) if (!inputs.some((p) => p.node === w.to && p.port === w.in)) inputs.push({ node: w.to, port: w.in });
  const outputs: GroupPort[] = [];
  for (const w of outgoing) if (!outputs.some((p) => p.node === w.from && p.port === w.out)) outputs.push({ node: w.from, port: w.out });
  const x = Math.min(...inside.map((n) => n.x));
  const y = Math.min(...inside.map((n) => n.y));
  const group: GraphNode = { id, kind: GROUP, x, y, label, graph: { nodes: inside, wires: innerWires }, inputs, outputs };
  const at = graph.nodes.findIndex((n) => ids.has(n.id));
  const nodes = graph.nodes.filter((n) => !ids.has(n.id));
  nodes.splice(Math.min(at, nodes.length), 0, group);
  const wires = [
    ...graph.wires.filter((w) => !ids.has(w.from) && !ids.has(w.to)),
    ...incoming.map((w) => ({ ...w, to: id, in: `${w.to}.${w.in}` })),
    ...outgoing.map((w) => ({ ...w, from: id, out: `${w.from}.${w.out}` })),
  ];
  return { graph: { ...graph, nodes, wires }, id };
}

/**
 * A group expanded back: its nodes put where it stands (moved with it since it was made), their wires, and the group's
 * wires taken to the inner ports they name. An inner node whose id the graph has since given to another is renamed.
 */
export function expand(graph: GraphJson, id: string): { graph: GraphJson; ids: string[] } | null {
  const group = graph.nodes.find((n) => n.id === id && n.kind === GROUP && n.graph);
  if (!group?.graph) return null;
  const others = graph.nodes.filter((n) => n.id !== id);
  const taken = new Set(allIds({ nodes: others }));
  const rename = new Map<string, string>();
  let scratch: { nodes: GraphNode[] } = { nodes: [...others, ...group.graph.nodes] };
  for (const n of group.graph.nodes) {
    if (!taken.has(n.id)) { taken.add(n.id); continue; }
    const fresh = newNodeId(scratch);
    rename.set(n.id, fresh);
    taken.add(fresh);
    scratch = { nodes: [...scratch.nodes, { ...n, id: fresh }] };
  }
  const to = (n: string) => rename.get(n) ?? n;
  const x0 = Math.min(...group.graph.nodes.map((n) => n.x));
  const y0 = Math.min(...group.graph.nodes.map((n) => n.y));
  const inner = group.graph.nodes.map((n) => ({ ...n, id: to(n.id), x: n.x - x0 + group.x, y: n.y - y0 + group.y }));
  const port = (list: GroupPort[] | undefined, name: string) => list?.find((p) => groupPortName(p) === name);
  const wires: GraphWire[] = [];
  for (const w of graph.wires) {
    if (w.to === id) { const p = port(group.inputs, w.in); if (p) wires.push({ ...w, to: to(p.node), in: p.port }); continue; }
    if (w.from === id) { const p = port(group.outputs, w.out); if (p) wires.push({ ...w, from: to(p.node), out: p.port }); continue; }
    wires.push(w);
  }
  wires.push(...group.graph.wires.map((w) => ({ ...w, from: to(w.from), to: to(w.to) })));
  const at = graph.nodes.indexOf(group);
  const nodes = [...graph.nodes.slice(0, at), ...inner, ...graph.nodes.slice(at + 1)];
  return { graph: { ...graph, nodes, wires }, ids: inner.map((n) => n.id) };
}

/** Every node's id, in groups too. */
function allIds(graph: { nodes: GraphNode[] }): string[] {
  return graph.nodes.flatMap((n) => [n.id, ...(n.graph ? allIds(n.graph) : [])]);
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
export function fitView(graph: GraphJson, specs: (kind: string, node?: GraphNode) => NodeSpec, width: number, height: number, margin = 24): GraphView {
  if (graph.nodes.length === 0 || width <= 0 || height <= 0) return { x: -40, y: -40, zoom: 1 };
  const x0 = Math.min(...graph.nodes.map((n) => n.x)) - margin;
  const y0 = Math.min(...graph.nodes.map((n) => n.y)) - margin;
  const x1 = Math.max(...graph.nodes.map((n) => n.x + NODE_W)) + margin;
  const y1 = Math.max(...graph.nodes.map((n) => n.y + nodeHeight(specs(n.kind, n)))) + margin;
  const zoom = Math.max(0.25, Math.min(1, width / (x1 - x0), height / (y1 - y0)));
  return { zoom, x: x0 - (width / zoom - (x1 - x0)) / 2, y: y0 - (height / zoom - (y1 - y0)) / 2 };
}

/** The nodes inside a rectangle of the graph (a marquee). */
export function nodesIn(graph: GraphJson, specs: (kind: string, node?: GraphNode) => NodeSpec, a: { x: number; y: number }, b: { x: number; y: number }): string[] {
  const [x0, x1] = [Math.min(a.x, b.x), Math.max(a.x, b.x)];
  const [y0, y1] = [Math.min(a.y, b.y), Math.max(a.y, b.y)];
  return graph.nodes.filter((n) => n.x < x1 && n.x + NODE_W > x0 && n.y < y1 && n.y + nodeHeight(specs(n.kind, n)) > y0).map((n) => n.id);
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

/**
 * A constant as a node's input row shows it, in the room between its label and <right> (valueEnd: up to the output's label
 * on the same row, else to the node's edge): a storey by its name (none: the view's, nothing shown), picked elements by their count, the rest as
 * showValue gives it, cut with … to fit, or nothing when there is no room.
 */
export function rowValue(v: unknown, type: PortType, label: string, storeyName: (id: string) => string | undefined, right = valueEnd()): string {
  const text = type === 'storey' ? (typeof v === 'string' && v ? storeyName(v) ?? '?' : '')
    : type === 'element' && Array.isArray(v) ? String(v.length)
    : showValue(v);
  const room = Math.floor((right - 10 - label.length * 6 - 6) / 5.5);
  if (room < 2 || !text) return '';
  return text.length > room ? `${text.slice(0, room - 1)}…` : text;
}

/** A number as the inspector writes it, as the nodes show it: a decimal point, whatever the browser's language. */
export function numberText(v: unknown): string {
  return typeof v === 'number' && Number.isFinite(v) ? String(Math.round(v * 1e9) / 1e9) : '';
}

/** Where a row's constant ends: before the label of the output on the same row (if any), else at the node's right edge. */
export function valueEnd(outLabel?: string): number {
  return outLabel ? NODE_W - 10 - outLabel.length * 6 - 8 : NODE_W - 10;
}
