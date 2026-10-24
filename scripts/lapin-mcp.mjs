#!/usr/bin/env node
/**
 * Lapin's MCP bridge: lets an AI agent drive an open Lapin (desktop or
 * `pnpm dev`) through the Model Context Protocol, on stdio. Every Lapin tool
 * is an engine command with JSON parameters; the agent's edits go through
 * the same engine as the user's, show at once in the open window, and undo
 * one step each.
 *
 * It finds the engine through the port file the engine writes
 * (<user-dir>/host.json), or LAPIN_PORT. No dependencies (Node 22).
 *
 *   claude mcp add lapin -- node /path/to/lapin-pro/scripts/lapin-mcp.mjs
 */

import { readFileSync, existsSync, readdirSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createInterface } from 'node:readline';

const VERSION = '0.1.0';

/** Where an open engine left its port: LAPIN_PORT, else the newest host.json of the known user folders. */
function enginePort() {
  if (process.env.LAPIN_PORT) return Number(process.env.LAPIN_PORT);
  const dirs = [
    process.env.LAPIN_USER_DIR,
    join(homedir(), 'Library', 'Application Support', 'ro.ciuntucbimstudio.lapin'),
    join(homedir(), '.local', 'share', 'Lapin'),
    join(homedir(), 'AppData', 'Local', 'Lapin'),
    process.env.LOCALAPPDATA && join(process.env.LOCALAPPDATA, 'Lapin'),
  ].filter(Boolean);
  for (const d of dirs) {
    const f = join(d, 'host.json');
    if (existsSync(f)) {
      try { return JSON.parse(readFileSync(f, 'utf8')).port; } catch { /* next */ }
    }
  }
  return null;
}

/** The engine's binary frame: "LPF1", a uint32 header length, the JSON header (arrays left as references). */
function decode(buf) {
  const b = Buffer.from(buf);
  if (b.toString('latin1', 0, 4) !== 'LPF1') return JSON.parse(b.toString('utf8'));
  const n = b.readUInt32LE(4);
  return JSON.parse(b.toString('utf8', 8, 8 + n));
}

/**
 * The arguments each command and query reads, found in the engine's source
 * (every `a["key"]` and `P(a, "x", "y")` of its handler), so they never drift
 * from the code. Keys read by helpers the handler calls are not seen; the
 * command's error then names what is missing. Empty when the source is not
 * beside this script (a packaged app).
 */
function argsFromSource() {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', 'engine', 'src');
  const out = new Map();
  if (!existsSync(root)) return out;
  const moduleOf = (dir) => {
    for (let d = dir; d.startsWith(root); d = dirname(d)) {
      const f = join(d, 'module.json');
      if (existsSync(f)) try { return JSON.parse(readFileSync(f, 'utf8')).id; } catch { break; }
    }
    return 'core';
  };
  const walk = (dir) => {
    for (const e of readdirSync(dir, { withFileTypes: true })) {
      const f = join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== 'bin' && e.name !== 'obj') walk(f); continue; }
      if (!e.name.endsWith('.cs')) continue;
      const src = readFileSync(f, 'utf8');
      const marks = [...src.matchAll(/\.(Command|Query)\("([\w.]+)"/g)];
      if (!marks.length) continue;
      const mod = moduleOf(dir);
      marks.forEach((m, i) => {
        let body = src.slice(m.index, marks[i + 1]?.index ?? src.length);
        const end = body.search(/\n {4}\}\n/); // the registering method's end
        if (end > 0) body = body.slice(0, end);
        const keys = new Set();
        for (const k of body.matchAll(/\ba\["(\w+)"\]/g)) keys.add(k[1]);
        for (const k of body.matchAll(/\(a, "(\w+)", "(\w+)"/g)) { keys.add(k[1]); keys.add(k[2]); }
        for (const k of body.matchAll(/\(a, "(\w+)"\)/g)) keys.add(k[1]);
        const name = m[2].includes('.') ? m[2] : `${mod}.${m[2]}`;
        out.set(name, { kind: m[1], keys: [...keys] });
      });
    }
  };
  walk(root);
  return out;
}

const ARGS = argsFromSource();
const withArgs = (name) => (ARGS.get(name)?.keys.length ? `(${ARGS.get(name).keys.join(', ')})` : '');

let ws = null;
let next = 1;
const pending = new Map();

async function connect() {
  if (ws && ws.readyState === WebSocket.OPEN) return ws;
  const port = enginePort();
  if (!port) throw new Error('No open Lapin found: start the app (or pnpm dev), or set LAPIN_PORT.');
  ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  ws.binaryType = 'arraybuffer';
  await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = () => reject(new Error(`Lapin's engine did not answer on port ${port}.`)); });
  ws.onmessage = (ev) => {
    const msg = decode(ev.data);
    if (msg.event) return; // diffs are for the window
    const p = pending.get(msg.id);
    if (!p) return;
    pending.delete(msg.id);
    if (msg.error) p.reject(new Error(msg.error)); else p.resolve(msg.result);
  };
  ws.onclose = () => { ws = null; for (const p of pending.values()) p.reject(new Error('The engine closed.')); pending.clear(); };
  return ws;
}

async function call(method, params = {}) {
  const sock = await connect();
  const id = next++;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    sock.send(JSON.stringify({ id, method, params }));
  });
}

const TOOLS = [
  {
    name: 'lapin_info',
    description: "What the open Lapin has: its product and modules, every command (name, the arguments it reads, label), the queries, and every class it builds with the fields each takes. Read this first.",
    inputSchema: { type: 'object', properties: {} },
    run: async () => {
      const i = await call('session.info');
      return {
        product: i.product, modules: i.modules?.map((m) => m.id),
        commands: i.commands?.map((c) => `${c.name}${withArgs(c.name)} — ${c.label}`),
        queries: [...ARGS].filter(([, d]) => d.kind === 'Query').map(([n]) => `${n}${withArgs(n)}`),
        classes: i.nodeTypes?.map((t) => ({ class: t.type, label: t.label, module: t.module, fields: t.fields?.map((f) => `${f.key} (${f.kind})`) })),
      };
    },
  },
  {
    name: 'lapin_model',
    description: 'The model as it is: every entity (id, IFC class, name, storey, attributes), optionally only some classes. Large models: ask for classes.',
    inputSchema: { type: 'object', properties: { classes: { type: 'array', items: { type: 'string' }, description: 'IFC classes to list, e.g. ["IfcWall", "IfcBuildingStorey"]' } } },
    run: async ({ classes }) => {
      const s = await call('doc.snapshot');
      const nodes = (s.nodes ?? []).filter((n) => !classes?.length || classes.includes(n.type));
      return nodes.map((n) => ({ id: n.id, class: n.type, name: n.name, storey: n.storeyId, attrs: n.props }));
    },
  },
  {
    name: 'lapin_execute',
    description: 'Run one engine command (one undo step), e.g. command "architecture.addWall" with args {"x1":0,"y1":0,"x2":5,"y2":0} (metres). Commands and their classes come from lapin_info. Returns what the command returns (often the new id).',
    inputSchema: { type: 'object', properties: { command: { type: 'string' }, args: { type: 'object' } }, required: ['command'] },
    run: async ({ command, args }) => call('doc.execute', { command, args: args ?? {} }),
  },
  {
    name: 'lapin_query',
    description: 'Ask the engine without changing anything, e.g. "view.drawing" {"id": viewId}, "schedule.csv" {"id"}, "civil.elevationAt" {"x","y"}.',
    inputSchema: { type: 'object', properties: { name: { type: 'string' }, args: { type: 'object' } }, required: ['name'] },
    run: async ({ name, args }) => call('doc.query', { name, args: args ?? {} }),
  },
  {
    name: 'lapin_quantities',
    description: "An element's quantity sets (lengths, areas, volumes; its layers').",
    inputSchema: { type: 'object', properties: { id: { type: 'string' } }, required: ['id'] },
    run: async ({ id }) => call('quantities.of', { id }),
  },
  { name: 'lapin_undo', description: 'Undo the last step.', inputSchema: { type: 'object', properties: {} }, run: async () => { await call('doc.undo'); return 'undone'; } },
  { name: 'lapin_redo', description: 'Redo the step undone.', inputSchema: { type: 'object', properties: {} }, run: async () => { await call('doc.redo'); return 'redone'; } },
  {
    name: 'lapin_save',
    description: 'Save the project (to its own file, or to a .lapin path given).',
    inputSchema: { type: 'object', properties: { path: { type: 'string' } } },
    run: async ({ path }) => { await call('project.save', path ? { path } : {}); return 'saved'; },
  },
];

function reply(id, result) { process.stdout.write(`${JSON.stringify({ jsonrpc: '2.0', id, result })}\n`); }
function fail(id, code, message) { process.stdout.write(`${JSON.stringify({ jsonrpc: '2.0', id, error: { code, message } })}\n`); }

async function handle(msg) {
  const { id, method, params } = msg;
  switch (method) {
    case 'initialize':
      return reply(id, {
        protocolVersion: params?.protocolVersion ?? '2025-06-18',
        capabilities: { tools: {} },
        serverInfo: { name: 'lapin', version: VERSION },
        instructions: 'Drive the open Lapin (a BIM modeller on IFC). Call lapin_info first; lengths are metres; every lapin_execute is one undo step the user sees at once.',
      });
    case 'ping': return reply(id, {});
    case 'tools/list': return reply(id, { tools: TOOLS.map(({ run: _, ...t }) => t) });
    case 'tools/call': {
      const tool = TOOLS.find((t) => t.name === params?.name);
      if (!tool) return fail(id, -32602, `Unknown tool ${params?.name}`);
      try {
        const result = await tool.run(params.arguments ?? {});
        return reply(id, { content: [{ type: 'text', text: typeof result === 'string' ? result : JSON.stringify(result ?? null, null, 1) }] });
      } catch (e) {
        return reply(id, { content: [{ type: 'text', text: e instanceof Error ? e.message : String(e) }], isError: true });
      }
    }
    default:
      if (id !== undefined && !method?.startsWith('notifications/')) fail(id, -32601, `Unknown method ${method}`);
  }
}

// One message at a time, in order: an agent's query sees the edit it sent before.
let queue = Promise.resolve();
createInterface({ input: process.stdin }).on('line', (line) => {
  if (!line.trim()) return;
  let msg;
  try { msg = JSON.parse(line); } catch { return; }
  queue = queue.then(() => handle(msg)).catch(() => undefined);
});
