#!/usr/bin/env node
/**
 * Lapin's MCP bridge: lets an AI agent drive an open Lapin (desktop or
 * `pnpm dev`) through the Model Context Protocol, on stdio. Every Lapin tool
 * is an engine command with JSON parameters; the agent's edits go through
 * the same engine as the user's, show at once in the open window, and undo
 * one step each.
 *
 * It finds the engine as lapinbim does (docs/python.md, How a program finds
 * Lapin): LAPIN_PORT, else the host files the engines write
 * (<user-dir>/hosts/host-<pid>.json, <user-dir>/host.json), else the ports
 * 14912-15011 and 47810 probed, each confirmed by its /health ("engine":
 * "lapin"). Several open: LAPIN_PROJECT or LAPIN_PRODUCT says which. No
 * dependencies (Node 22).
 *
 *   claude mcp add lapin -- node /path/to/lapin-pro/scripts/lapin-mcp.mjs
 */

import { readFileSync, readdirSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { basename, dirname, isAbsolute, join, resolve } from 'node:path';
import { createInterface } from 'node:readline';

const VERSION = '0.1.0';

const processAlive = (pid) => { try { process.kill(pid, 0); return true; } catch (e) { return e.code === 'EPERM'; } };

/** The first port of an engine without one of its own (L A P I N), how many it tries, and pnpm dev's. */
const FIRST_PORT = 14912, PORT_COUNT = 100, DEV_PORT = 47810;

/** The live engines' host files (each engine's, then host.json), one per port; a file whose process is gone is skipped. */
function hostFiles(dirs) {
  const out = [], seen = new Set();
  for (const d of dirs) {
    const own = existsSync(join(d, 'hosts')) ? readdirSync(join(d, 'hosts')).filter((f) => /^host-.*\.json$/.test(f)).sort().map((f) => join(d, 'hosts', f)) : [];
    for (const f of [...own, join(d, 'host.json')]) {
      try {
        const h = JSON.parse(readFileSync(f, 'utf8'));
        if (!Number.isInteger(h.port) || seen.has(h.port)) continue;
        // A file left by an engine that is gone (killed, or its image ejected) names a dead port.
        if (h.pid && !processAlive(h.pid)) continue;
        seen.add(h.port);
        out.push(h);
      } catch { /* missing or unreadable: next */ }
    }
  }
  return out;
}

/** What the engine on a port says it is (GET /health); null when nothing answers there or it is not Lapin. */
async function identify(port, ms = 500) {
  try {
    const r = await fetch(`http://127.0.0.1:${port}/health`, { signal: AbortSignal.timeout(ms) });
    if (!r.ok) return null;
    const b = await r.json();
    if (b?.engine === 'lapin') return { ...b, port };
    // An engine from before engine.hello.
    if (b?.ok === true && typeof b.product === 'string' && Array.isArray(b.modules)) return { engine: 'lapin', product: b.product, port, project: null };
  } catch { /* closed, slow or not HTTP */ }
  return null;
}

/** Every live Lapin engine: the host files' and the probed ports', each confirmed (LAPIN_USER_DIR: that folder's only). */
async function findEngines() {
  const named = process.env.LAPIN_USER_DIR;
  const dirs = named ? [named] : [
    join(homedir(), 'Library', 'Application Support', 'ro.ciuntucbimstudio.lapin'),
    join(homedir(), '.local', 'share', 'Lapin'),
    join(homedir(), 'AppData', 'Local', 'Lapin'),
    process.env.LOCALAPPDATA && join(process.env.LOCALAPPDATA, 'Lapin'),
  ].filter(Boolean);
  const ports = hostFiles(dirs).map((h) => h.port);
  if (!named) for (let p = FIRST_PORT; p < FIRST_PORT + PORT_COUNT; p++) if (!ports.includes(p)) ports.push(p);
  if (!named && !ports.includes(DEV_PORT)) ports.push(DEV_PORT);
  return (await Promise.all(ports.map((p) => identify(p)))).filter(Boolean).sort((a, b) => a.port - b.port);
}

const describe = (e) => `port ${e.port}: ${e.product ?? '?'}, ${e.project || 'no project saved'}`;

/** LAPIN_PORT, else the one open engine's port, or the one LAPIN_PROJECT / LAPIN_PRODUCT names. */
async function enginePort() {
  if (process.env.LAPIN_PORT) return Number(process.env.LAPIN_PORT);
  const engines = await findEngines();
  if (!engines.length) return null;
  const project = process.env.LAPIN_PROJECT?.toLowerCase(), product = process.env.LAPIN_PRODUCT;
  const left = engines.filter((e) => (!product || e.product === product)
    && (!project || (e.project && (e.project.toLowerCase().includes(project) || basename(e.project).toLowerCase() === project))));
  if (left.length === 1) return left[0].port;
  if (!left.length) throw new Error(`No open Lapin matches LAPIN_PROJECT/LAPIN_PRODUCT. Open: ${engines.map(describe).join('; ')}.`);
  throw new Error(`${left.length} Lapins are open (${left.map(describe).join('; ')}): set LAPIN_PORT, LAPIN_PROJECT or LAPIN_PRODUCT in the bridge's environment.`);
}

/** The engine's binary frame: "LPF1", a uint32 header length, the JSON header (arrays left as references). */
function decode(buf) {
  const b = Buffer.from(buf);
  if (b.toString('latin1', 0, 4) !== 'LPF1') return JSON.parse(b.toString('utf8'));
  const n = b.readUInt32LE(4);
  return JSON.parse(b.toString('utf8', 8, 8 + n));
}

/**
 * The arguments each command and query reads (decision 489): the command catalogue made from the engine's source when
 * Lapin is built (scripts/command-catalogue.mjs), served by the engine itself as `session.catalogue`, so an installed
 * app, with no source beside it, has them too. An engine from before the catalogue: the repo's catalogue.json, when
 * this script is in a checkout. Keys read by helpers further than the catalogue looks are not listed; the command's
 * error then names what is missing. The same catalogue as the Python package's (docs/python.md).
 */
let catalogue = null;
async function argsOf() {
  if (catalogue) return catalogue;
  let c = null;
  try { c = await call('session.catalogue'); }
  catch {
    try { c = (await import('./command-catalogue.mjs')).readCatalogue(); } catch { /* no checkout beside the script */ }
  }
  const map = new Map();
  for (const [kind, list] of [['Command', c?.commands], ['Query', c?.queries]])
    for (const e of list ?? []) map.set(e.name, { kind, keys: (e.args ?? []).map((x) => x.name) });
  return (catalogue = map);
}
const withArgs = (args, name) => (args.get(name)?.keys.length ? `(${args.get(name).keys.join(', ')})` : '');

let ws = null;
let next = 1;
const pending = new Map();

async function connect() {
  if (ws && ws.readyState === WebSocket.OPEN) return ws;
  const port = await enginePort();
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
  ws.onclose = () => { ws = null; catalogue = null; for (const p of pending.values()) p.reject(new Error('The engine closed.')); pending.clear(); };
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

// ─── A document's pictures as SVG (as ui/src/modules/core/documents/docModel.ts pensToSvg makes them) ──

const SVG_U = 10; // SVG units a paper mm
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const num = (v) => (Math.round(v * SVG_U * 10) / 10).toString();

/**
 * A picture's pens (`document.images`) as a standalone SVG at its paper size, on white: the same drawing the interface
 * makes for a DOCX or ODT. The bridge has no rasteriser, so a document exported here carries its pictures as SVG only
 * (the engine puts a blank PNG beside each, which Word needs; Word 2016+ and LibreOffice show the SVG).
 */
function pensToSvg(img, image) {
  const out = [`<svg xmlns="http://www.w3.org/2000/svg" width="${img.w}mm" height="${img.h}mm" viewBox="0 0 ${num(img.w)} ${num(img.h)}">`,
    `<rect x="0" y="0" width="${num(img.w)}" height="${num(img.h)}" fill="#ffffff"/>`];
  for (const p of img.pens ?? []) {
    if (p.images?.length) {
      for (const im of p.images) {
        const href = image(im.id), c = im.corners;
        if (!href || c.length < 8) continue;
        out.push(`<image href="${esc(href)}" x="0" y="0" width="1" height="1" preserveAspectRatio="none" opacity="${im.opacity}" transform="matrix(${num(c[2] - c[0])} ${num(c[3] - c[1])} ${num(c[6] - c[0])} ${num(c[7] - c[1])} ${num(c[0])} ${num(c[1])})"/>`);
      }
      continue;
    }
    if (p.texts?.length) {
      out.push(`<g fill="${esc(p.stroke ?? '#000000')}">`);
      for (const t of p.texts) {
        const family = t.font === 'serif' ? 'Georgia, serif' : t.font === 'mono' ? 'Menlo, monospace' : 'Arial, Helvetica, sans-serif';
        const anchor = t.anchor === 'start' ? 'start' : t.anchor === 'end' ? 'end' : 'middle';
        const base = t.baseline === 'middle' ? 'central' : 'alphabetic';
        const rot = t.angle ? ` transform="rotate(${-t.angle} ${num(t.x)} ${num(t.y)})"` : '';
        out.push(`<text x="${num(t.x)}" y="${num(t.y)}" font-size="${num(t.height)}" font-family="${family}" font-weight="${t.bold ? 700 : 400}"${t.italic ? ' font-style="italic"' : ''} text-anchor="${anchor}" dominant-baseline="${base}"${rot}>${esc(t.text)}</text>`);
      }
      out.push('</g>');
      continue;
    }
    let d = '';
    for (const loop of p.loops ?? []) {
      for (let k = 0; k + 1 < loop.length; k += 2) d += `${k === 0 ? 'M' : 'L'}${num(loop[k])} ${num(loop[k + 1])}`;
      if (loop.length >= 2) d += 'Z';
    }
    const s = p.segments ?? [];
    for (let k = 0; k + 3 < s.length; k += 4) d += `M${num(s[k])} ${num(s[k + 1])}L${num(s[k + 2])} ${num(s[k + 3])}`;
    if (!d) continue;
    const stroke = p.stroke ? ` stroke="${esc(p.stroke)}" stroke-width="${num(Math.max(p.width, 0.05))}" stroke-linecap="round" stroke-linejoin="round"` : ' stroke="none"';
    const dash = p.stroke && p.dash?.length ? ` stroke-dasharray="${p.dash.map(num).join(' ')}"` : '';
    out.push(`<path d="${d}" fill="${p.fill ? esc(p.fill) : 'none'}" fill-rule="evenodd"${stroke}${dash}/>`);
  }
  out.push('</svg>');
  return out.join('');
}

/** A document's pictures for a DOCX or ODT: {key: {svg}}; an orthophoto's data read from the model when a picture has one. */
async function documentPictures(id) {
  const images = (await call('doc.query', { name: 'document.images', args: { id } })) ?? [];
  let nodes = null;
  if (images.some((i) => i.pens?.some((p) => p.images?.length))) nodes = new Map(((await call('doc.snapshot')).nodes ?? []).map((n) => [n.id, n]));
  const data = (imageId) => {
    const p = nodes?.get(imageId)?.props;
    return typeof p?.data === 'string' && typeof p?.mime === 'string' ? `data:${p.mime};base64,${p.data}` : null;
  };
  return Object.fromEntries(images.map((img) => [img.key, { svg: pensToSvg(img, data) }]));
}

/** A document written by the engine (`document.export`) to an absolute path on this computer. */
async function exportDocument({ id, format, path }) {
  const fmt = String(format ?? '').toLowerCase();
  if (!['docx', 'odt', 'pdf'].includes(fmt)) throw new Error('format is docx, odt or pdf.');
  if (typeof path !== 'string' || !path || !isAbsolute(path)) throw new Error('path must be an absolute file path, e.g. /Users/me/Desktop/memoriu.docx.');
  const images = fmt === 'pdf' ? undefined : await documentPictures(id);
  const r = await call('doc.query', { name: 'document.export', args: { id, format: fmt, ...(images ? { images } : {}) } });
  const bytes = Buffer.from(r.data, 'base64');
  const file = resolve(path);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, bytes);
  return { path: file, bytes: bytes.length, name: r.name, missing: r.missing ?? [] };
}

async function exportJg({ path, units, storeys, kinds, allWalls, footings }) {
  if (typeof path !== 'string' || !path || !isAbsolute(path)) throw new Error('path must be an absolute file path, e.g. /Users/me/Desktop/house.jg.json.');
  const args = { units: units === 'si' ? 'si' : 'contract', ...(Array.isArray(storeys) ? { storeys } : {}), ...(Array.isArray(kinds) ? { kinds } : {}), ...(allWalls ? { allWalls: true } : {}), ...(footings === 'pinned' ? { footings: 'pinned' } : {}) };
  const r = await call('doc.query', { name: 'jg.export', args });
  const bytes = Buffer.from(r.data, 'base64');
  const file = resolve(path);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, bytes);
  return { path: file, bytes: bytes.length, report: r.report };
}

const TOOLS = [
  {
    name: 'lapin_info',
    description: "What the open Lapin has: its product and modules, every command (name, the arguments it reads, label), the queries, and every class it builds with the fields each takes. Read this first.",
    inputSchema: { type: 'object', properties: {} },
    run: async () => {
      const i = await call('session.info');
      const args = await argsOf();
      return {
        product: i.product, modules: i.modules?.map((m) => m.id),
        commands: i.commands?.map((c) => `${c.name}${withArgs(args, c.name)} — ${c.label}`),
        queries: [...args].filter(([, d]) => d.kind === 'Query').map(([n]) => `${n}${withArgs(args, n)}`),
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
    name: 'lapin_documents',
    description: "The project's written documents (id, name, template, language, revision, status) and the templates a new one starts from (id, title, description). Make one with lapin_execute \"document.create\" {template, name?}; see docs/mcp.md, Documents.",
    inputSchema: { type: 'object', properties: {} },
    run: async () => ({
      documents: await call('doc.query', { name: 'document.list', args: {} }),
      templates: await call('doc.query', { name: 'document.templates', args: {} }),
    }),
  },
  {
    name: 'lapin_document_export',
    description: 'Write a document (from lapin_documents) as DOCX, ODT or PDF to an absolute path on this computer (folders made). Returns {path, bytes, name, missing}: missing lists the fields the project does not fill yet (written empty). DOCX and ODT carry their pictures as SVG.',
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', description: 'The document id' },
        format: { type: 'string', enum: ['docx', 'odt', 'pdf'] },
        path: { type: 'string', description: 'Absolute path of the file to write, e.g. /Users/me/Desktop/memoriu.pdf' },
      },
      required: ['id', 'format', 'path'],
    },
    run: exportDocument,
  },
  {
    name: 'lapin_export_jg',
    description: "Write the project's analytical model (frames, shells, nodes, supports, materials, sections) as JG Structures' exchange JSON (contract JG_Structures_v2_exchange 1.0.0; docs/jg-structures.md) to an absolute path on this computer. units: 'contract' (cm, kgf; the default) or 'si' (m, kN). Returns {path, bytes, report}: the report's counts, errors (the contract's checks), warnings, missing values for analysis and what was left out.",
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'Absolute path of the file to write, e.g. /Users/me/Desktop/house.jg.json' },
        units: { type: 'string', enum: ['contract', 'si'] },
        storeys: { type: 'array', items: { type: 'string' }, description: 'Storey ids to export (all when absent)' },
        kinds: { type: 'array', items: { type: 'string', enum: ['columns', 'beams', 'members', 'walls', 'slabs', 'roofs', 'supports'] } },
        allWalls: { type: 'boolean', description: 'Every wall, not only the load-bearing ones' },
        footings: { type: 'string', enum: ['fixed', 'pinned'], description: 'How column and wall feet on footings are held (fixed by default)' },
      },
      required: ['path'],
    },
    run: exportJg,
  },
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
