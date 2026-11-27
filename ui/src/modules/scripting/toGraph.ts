/**
 * Deterministic Python cell → Visual Graph (GraphJson format 2) converter
 * (docs/plan-python-notebook.md, Phase 3).
 *
 * Translates parametric Python cells into native C# Lapin.Graphs nodes (which run
 * without Python, preview as a 3D ghost, expose a palette, and work on the Web):
 *   - Scalar / point / list assignments   → `number`, `integer`, `text`, `bool`, `point`, `list` (with palette)
 *   - `range(start, stop, step)`          → `range`
 *   - Arithmetic expressions over vars    → `formula` (ports a, b, c, d)
 *   - `lapin.<mod>.<cmd>(...)` calls      → `command:<mod>.<cmd>` / `query:<mod>.<cmd>`
 *   - Simple `for v in <iterable>:` loops → Grasshopper-style list mapping on command inputs
 *
 * Unsupported statements (complex `if`, `while`, `def`, external libs) are reported by line
 * so the UI can offer an optional Ollama-assisted conversion validated via `graphs.evaluate`.
 */

import { GRAPH_FORMAT, LIST_ARG, type GraphJson, type GraphNode, type GraphWire } from '../graphs/graph';
import { cleanText } from '../../assistant/loop';
import { ollamaTransport } from './ai';
import { useAssistantSettings, defaultCodeModel, checkStatus, type OllamaChunk } from '../../assistant/ollama';
import { ident, snake, type Catalogue } from './logic';

export interface UnsupportedLine {
  line: number;
  text: string;
  reason: string;
}

export interface ConversionResult {
  graph: GraphJson;
  unsupported: UnsupportedLine[];
  convertedCommands: number;
}

interface Binding {
  nodeId: string;
  port: string;
}

/** Build a lookup from Python `ns.fn_name` → `{ kind: 'command:...' | 'query:...', args: Map<pyName, engineName> }`. */
function buildCallIndex(catalogue: Catalogue | null) {
  const index = new Map<string, { kind: string; args: Map<string, string> }>();
  if (!catalogue) return index;
  const register = (prefix: 'command' | 'query', name: string, args?: { name: string }[]) => {
    const [ns, ...rest] = name.split('.');
    if (!rest.length) return;
    const pyKey = `${ident(snake(ns))}.${ident(snake(rest.join('_')))}`;
    const argMap = new Map<string, string>();
    for (const a of args ?? []) {
      argMap.set(a.name, a.name);
      argMap.set(ident(snake(a.name)), a.name);
    }
    index.set(pyKey, { kind: `${prefix}:${name}`, args: argMap });
  };
  for (const c of catalogue.commands) register('command', c.name, c.args);
  for (const q of catalogue.queries) register('query', q.name, q.args);
  return index;
}

/** Split top-level comma-separated arguments respecting parentheses, brackets, and quotes. */
function splitTopLevel(text: string, sep = ','): string[] {
  const out: string[] = [];
  let cur = '';
  let depth = 0;
  let quote: '"' | "'" | null = null;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (quote) {
      cur += ch;
      if (ch === '\\' && i + 1 < text.length) { cur += text[++i]; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; cur += ch; continue; }
    if (ch === '(' || ch === '[' || ch === '{') { depth++; cur += ch; continue; }
    if (ch === ')' || ch === ']' || ch === '}') { depth = Math.max(0, depth - 1); cur += ch; continue; }
    if (ch === sep && depth === 0) { out.push(cur.trim()); cur = ''; continue; }
    cur += ch;
  }
  if (cur.trim()) out.push(cur.trim());
  return out;
}

/** Parse a literal Python value if possible. */
function parseLiteral(expr: string): { kind: 'integer' | 'number' | 'text' | 'bool' | 'point' | 'list'; value: unknown } | null {
  const s = expr.trim();
  if (/^-?\d+$/.test(s)) return { kind: 'integer', value: parseInt(s, 10) };
  if (/^-?(?:\d+\.\d*|\.\d+)(?:e[+-]?\d+)?$/i.test(s)) return { kind: 'number', value: parseFloat(s) };
  if (s === 'True' || s === 'False') return { kind: 'bool', value: s === 'True' };
  if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) {
    return { kind: 'text', value: s.slice(1, -1) };
  }
  // Tuple of 2 or 3 numbers → point
  if (s.startsWith('(') && s.endsWith(')')) {
    const parts = splitTopLevel(s.slice(1, -1));
    if (parts.length === 2 || parts.length === 3) {
      const nums = parts.map((p) => Number(p));
      if (nums.every((n) => Number.isFinite(n))) {
        return { kind: 'point', value: { x: nums[0], y: nums[1], z: nums[2] ?? 0 } };
      }
    }
  }
  // List literal of scalars
  if (s.startsWith('[') && s.endsWith(']')) {
    const parts = splitTopLevel(s.slice(1, -1));
    if (parts.length > 0) {
      const lits = parts.map(parseLiteral);
      if (lits.every((l) => l && (l.kind === 'integer' || l.kind === 'number' || l.kind === 'text'))) {
        const allNum = lits.every((l) => l!.kind === 'integer' || l!.kind === 'number');
        return { kind: 'list', value: { text: lits.map((l) => String(l!.value)).join('; '), mode: allNum ? 'number' : 'text' } };
      }
    }
  }
  return null;
}

/** Strip inline `# comment` outside string literals. */
function stripComment(line: string): string {
  let quote: '"' | "'" | null = null;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (quote) {
      if (ch === '\\') { i++; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; continue; }
    if (ch === '#') return line.slice(0, i);
  }
  return line;
}

/** Assign clean left-to-right topological (x, y) coordinates to nodes. */
function layoutNodes(nodes: GraphNode[], wires: GraphWire[]): void {
  const deps = new Map<string, string[]>();
  for (const n of nodes) deps.set(n.id, []);
  for (const w of wires) deps.get(w.to)?.push(w.from);

  const depth = new Map<string, number>();
  const visiting = new Set<string>();
  const getDepth = (id: string): number => {
    if (depth.has(id)) return depth.get(id)!;
    if (visiting.has(id)) return 0;
    visiting.add(id);
    const d = Math.max(0, ...(deps.get(id) ?? []).map((p) => getDepth(p) + 1));
    visiting.delete(id);
    depth.set(id, d);
    return d;
  };

  const columns = new Map<number, GraphNode[]>();
  for (const n of nodes) {
    const col = getDepth(n.id);
    const list = columns.get(col) ?? [];
    list.push(n);
    columns.set(col, list);
  }
  for (const [col, colNodes] of columns.entries()) {
    colNodes.forEach((n, row) => {
      n.x = 40 + col * 260;
      n.y = 40 + row * 130;
    });
  }
}

/**
 * Convert a Python code cell into a Lapin Visual Graph (`GraphJson` format 2).
 */
export function pythonToGraph(code: string, catalogue: Catalogue | null): ConversionResult {
  const nodes: GraphNode[] = [];
  const wires: GraphWire[] = [];
  const bindings = new Map<string, Binding>();
  const unsupported: UnsupportedLine[] = [];
  const callIndex = buildCallIndex(catalogue);
  let seq = 1;
  let convertedCommands = 0;

  const addNode = (partial: Omit<GraphNode, 'id' | 'x' | 'y'>): GraphNode => {
    const node: GraphNode = { id: `n${seq++}`, x: 0, y: 0, ...partial };
    nodes.push(node);
    return node;
  };

  const addWire = (from: string, out: string, to: string, input: string) => {
    if (!wires.some((w) => w.to === to && w.in === input)) {
      wires.push({ from, out, to, in: input });
    }
  };

  /** Resolve an expression into either a constant literal or a wired node output. */
  const resolveExpr = (expr: string, label?: string): { literal?: unknown; binding?: Binding } | null => {
    const s = expr.trim();
    if (!s) return null;

    // Direct variable reference
    if (/^[A-Za-z_]\w*$/.test(s) && bindings.has(s)) {
      return { binding: bindings.get(s)! };
    }

    // Literal value
    const lit = parseLiteral(s);
    if (lit) {
      if (label) {
        if (lit.kind === 'integer') {
          const n = addNode({ kind: 'integer', label, values: { value: lit.value }, palette: { label, show: true } });
          return { binding: { nodeId: n.id, port: 'value' } };
        }
        if (lit.kind === 'number') {
          const n = addNode({ kind: 'number', label, values: { value: lit.value }, palette: { label, show: true } });
          return { binding: { nodeId: n.id, port: 'value' } };
        }
        if (lit.kind === 'text') {
          const n = addNode({ kind: 'text', label, values: { value: lit.value }, palette: { label, show: true } });
          return { binding: { nodeId: n.id, port: 'value' } };
        }
        if (lit.kind === 'bool') {
          const n = addNode({ kind: 'bool', label, values: { value: lit.value }, palette: { label, show: true } });
          return { binding: { nodeId: n.id, port: 'value' } };
        }
        if (lit.kind === 'point') {
          const p = lit.value as { x: number; y: number; z: number };
          const n = addNode({ kind: 'point', label, values: { x: p.x, y: p.y, z: p.z } });
          return { binding: { nodeId: n.id, port: 'point' } };
        }
        if (lit.kind === 'list') {
          const l = lit.value as { text: string; mode: string };
          const n = addNode({ kind: 'list', label, values: { text: l.text, mode: l.mode } });
          return { binding: { nodeId: n.id, port: 'list' } };
        }
      }
      return { literal: lit.kind === 'point' ? { $pt: [(lit.value as { x: number; y: number; z: number }).x, (lit.value as { x: number; y: number; z: number }).y, (lit.value as { x: number; y: number; z: number }).z] } : lit.value };
    }

    // range(stop) | range(start, stop) | range(start, stop, step)
    const rangeMatch = /^range\((.*)\)$/.exec(s);
    if (rangeMatch) {
      const parts = splitTopLevel(rangeMatch[1]);
      if (parts.length >= 1 && parts.length <= 3) {
        const startExpr = parts.length === 1 ? '0' : parts[0];
        const endExpr = parts.length === 1 ? parts[0] : parts[1];
        const stepExpr = parts.length === 3 ? parts[2] : '1';
        const values: Record<string, unknown> = {};
        const n = addNode({ kind: 'range', ...(label ? { label } : {}), values });
        const bindArg = (port: string, e: string) => {
          const r = resolveExpr(e);
          if (r?.binding) addWire(r.binding.nodeId, r.binding.port, n.id, port);
          else if (r && r.literal !== undefined) values[port] = r.literal;
        };
        bindArg('start', startExpr);
        bindArg('end', endExpr);
        bindArg('step', stepExpr);
        return { binding: { nodeId: n.id, port: 'list' } };
      }
    }

    // lapin.selection()
    if (/^lapin\.selection\(\s*\)$/.test(s)) {
      const n = addNode({ kind: 'selection', ...(label ? { label } : {}) });
      return { binding: { nodeId: n.id, port: 'ids' } };
    }

    // lapin.<ns>.<cmd>(...)
    const callMatch = /^lapin\.([A-Za-z_]\w*)\.([A-Za-z_]\w*)\((.*)\)$/.exec(s);
    if (callMatch) {
      const [, ns, fn, rawArgs] = callMatch;
      const spec = callIndex.get(`${ns}.${fn}`);
      const kind = spec?.kind ?? `command:${ns}.${fn.replace(/_([a-z])/g, (_, ch: string) => ch.toUpperCase())}`;
      const values: Record<string, unknown> = {};
      const whole: string[] = [];
      const n = addNode({ kind, ...(label ? { label } : {}), values });
      convertedCommands++;

      const args = splitTopLevel(rawArgs);
      for (const arg of args) {
        const eq = arg.indexOf('=');
        if (eq < 0) continue;
        const rawKey = arg.slice(0, eq).trim();
        const rawVal = arg.slice(eq + 1).trim();
        const engineKey = spec?.args.get(rawKey) ?? rawKey.replace(/_([a-z])/g, (_, ch: string) => ch.toUpperCase());
        if (LIST_ARG.test(engineKey) && !whole.includes(engineKey)) whole.push(engineKey);
        const resolved = resolveExpr(rawVal);
        if (resolved?.binding) addWire(resolved.binding.nodeId, resolved.binding.port, n.id, engineKey);
        else if (resolved && resolved.literal !== undefined) values[engineKey] = resolved.literal;
      }
      if (whole.length) n.whole = whole;
      return { binding: { nodeId: n.id, port: kind.startsWith('command:') ? 'ids' : 'result' } };
    }

    // Arithmetic expression referencing known variables (e.g. `i * spacing`, `h + 0.5`)
    if (/^[0-9A-Za-z_+\-*/().\s]+$/.test(s) && /[+\-*/]/.test(s)) {
      const referenced = [...new Set(s.match(/\b[A-Za-z_]\w*\b/g) ?? [])].filter((v) => bindings.has(v));
      if (referenced.length > 0 && referenced.length <= 4) {
        const slots = ['a', 'b', 'c', 'd'];
        let rewritten = s;
        const n = addNode({ kind: 'formula', ...(label ? { label } : {}), values: {} });
        referenced.forEach((v, idx) => {
          const slot = slots[idx];
          rewritten = rewritten.replace(new RegExp(`\\b${v}\\b`, 'g'), slot);
          const b = bindings.get(v)!;
          addWire(b.nodeId, b.port, n.id, slot);
        });
        n.values = { expr: rewritten };
        return { binding: { nodeId: n.id, port: 'value' } };
      }
    }

    return null;
  };

  const lines = code.replace(/\r\n/g, '\n').split('\n');
  for (let i = 0; i < lines.length; i++) {
    const raw = stripComment(lines[i]).trim();
    const lineNo = i + 1;
    if (!raw) continue;
    // Ignored boilerplate
    if (/^(import\s|from\s|print\(|with\s+lapin\.(?:batch|step)\()/.test(raw)) continue;

    // Simple `for var in <iterable>:` → bind `var` to the iterable list node so commands map over it
    const forMatch = /^for\s+([A-Za-z_]\w*)\s+in\s+(.+):$/.exec(raw);
    if (forMatch) {
      const [, loopVar, iterExpr] = forMatch;
      const r = resolveExpr(iterExpr, loopVar);
      if (r?.binding) {
        bindings.set(loopVar, r.binding);
      } else {
        unsupported.push({ line: lineNo, text: raw, reason: 'Loop iterable could not be mapped to a graph list node.' });
      }
      continue;
    }

    // Assignment: `var = expr`
    const assignMatch = /^([A-Za-z_]\w*)\s*=\s*(.+)$/.exec(raw);
    if (assignMatch && !raw.includes('==')) {
      const [, varName, rhs] = assignMatch;
      const r = resolveExpr(rhs, varName);
      if (r?.binding) {
        bindings.set(varName, r.binding);
      } else {
        unsupported.push({ line: lineNo, text: raw, reason: 'Expression is too dynamic for deterministic graph conversion.' });
      }
      continue;
    }

    // Standalone `lapin.<ns>.<cmd>(...)`
    if (/^lapin\.[A-Za-z_]\w*\.[A-Za-z_]\w*\(.*\)$/.test(raw)) {
      const r = resolveExpr(raw);
      if (!r) unsupported.push({ line: lineNo, text: raw, reason: 'Command call could not be parsed.' });
      continue;
    }

    unsupported.push({ line: lineNo, text: raw, reason: 'Statement type is not directly representable as a native graph node.' });
  }

  layoutNodes(nodes, wires);
  return {
    graph: { format: GRAPH_FORMAT, nodes, wires },
    unsupported,
    convertedCommands,
  };
}

/**
 * Fallback: ask the local Ollama model to translate complex Python code into a valid Lapin GraphJson (format 2).
 */
export async function convertCellToGraphWithAi(code: string, catalogue: Catalogue | null, signal?: AbortSignal): Promise<GraphJson> {
  const settings = useAssistantSettings.getState();
  const transport = ollamaTransport();
  let model = settings.codeModel ?? settings.model;
  if (!model) {
    const status = await checkStatus(transport, settings.url, null);
    if (!status.running) throw new Error(status.error || 'Ollama is not running: start it with `ollama serve` or the Ollama app.');
    model = defaultCodeModel(status.models);
    if (!model) throw new Error('Ollama has no model: install one with `ollama pull qwen3-coder:30b`.');
  }

  const sampleCommands = (catalogue?.commands ?? [])
    .filter((c) => code.toLowerCase().includes(snake(c.name.split('.').pop() ?? '').replace(/_/g, '')) || code.includes(c.name.split('.')[0]))
    .slice(0, 20)
    .map((c) => `${c.name}(${(c.args ?? []).map((a) => a.name).join(', ')})`)
    .join('\n');

  const prompt = `Convert this Lapin Python code into a Lapin Visual Graph JSON (format 2).
Output ONLY valid JSON matching:
{"format": 2, "nodes": [{"id": "n1", "kind": "...", "x": 40, "y": 40, "values": {...}, "label": "..."}], "wires": [{"from": "n1", "out": "value", "to": "n2", "in": "..."}]}
Built-in node kinds: number (out: value), integer (out: value), text (out: value), bool (out: value), point (in: x,y,z; out: point), range (in: start,end,step; out: list), formula (in: a,b,c,d; values: {expr}; out: value), list (values: {text, mode}; out: list).
Command node kind: "command:<module.commandName>" (out: ids, result).
Relevant commands:
${sampleCommands}

Python code:
\`\`\`python
${code}
\`\`\``;

  let text = '';
  await transport.stream(
    settings.url,
    '/api/chat',
    { model, messages: [{ role: 'user', content: prompt }], stream: true, think: false, options: { temperature: 0.1, num_ctx: 16384 } },
    (c: OllamaChunk) => { if (c.message?.content) text += c.message.content; },
    signal,
  );

  const cleaned = cleanText(text).trim();
  const match = /\{[\s\S]*\}/.exec(cleaned);
  if (!match) throw new Error('Ollama\'s answer is not a graph\'s JSON.');
  const parsed = JSON.parse(match[0]) as Partial<GraphJson>;
  if (!Array.isArray(parsed.nodes) || !Array.isArray(parsed.wires)) {
    throw new Error('Ollama\'s graph has no nodes or no wires.');
  }
  const graph: GraphJson = { format: GRAPH_FORMAT, nodes: parsed.nodes, wires: parsed.wires };
  layoutNodes(graph.nodes, graph.wires);
  return graph;
}
