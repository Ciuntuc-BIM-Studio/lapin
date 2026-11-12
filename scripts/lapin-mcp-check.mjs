#!/usr/bin/env node
/**
 * A check of the MCP bridge (scripts/lapin-mcp.mjs, docs/mcp.md) against an engine of its own: starts the built engine host
 * (engine/src/Host/Lapin.Host, Debug; `pnpm setup` or `dotnet build engine/Lapin.slnx` first) on a free port with a temporary
 * user folder, starts the bridge on stdio with LAPIN_PORT, and drives it as an MCP client would: initialize, tools/list,
 * a few walls, a plan with a view boundary, a document from memoriu-arhitectura with a picture of that boundary, then
 * lapin_document_export as PDF, DOCX and ODT. Checks each file's signature, the DOCX's SVG picture, and that a relative
 * path is refused. Never touches the engine of `pnpm dev`.
 *
 *   node scripts/lapin-mcp-check.mjs [out-dir]        (default: a temporary folder; the files are left there)
 */

import { spawn } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { inflateRawSync } from 'node:zlib';
import { resolveProduct } from './product.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const temp = mkdtempSync(join(tmpdir(), 'lapin-mcp-check-'));
const out = resolve(process.argv[2] ?? join(temp, 'out'));

const free = () => new Promise((done) => { const s = createServer(); s.listen(0, '127.0.0.1', () => { const { port } = s.address(); s.close(() => done(port)); }); });
const fail = (m) => { console.error(`FAIL ${m}`); process.exit(1); };
const children = [];
process.on('exit', () => { for (const c of children) c.kill('SIGTERM'); });

// The engine, on a port of its own.
const manifest = join(temp, 'product.json');
writeFileSync(manifest, JSON.stringify(resolveProduct(root, 'lapin'), null, 2));
const port = await free();
const host = spawn('dotnet', [join(root, 'engine/src/Host/Lapin.Host/bin/Debug/net10.0/Lapin.Host.dll'), '--product', manifest,
  '--modules-dir', join(root, 'engine/artifacts/modules'), '--user-dir', join(temp, 'user'), '--port', String(port), '--parent-pid', String(process.pid)]);
children.push(host);
await new Promise((done, reject) => {
  let seen = '';
  const look = (d) => { seen += d; if (seen.includes('LAPIN_HOST_READY')) done(); };
  host.stdout.on('data', look); host.stderr.on('data', look);
  host.on('exit', (code) => reject(new Error(`the engine stopped (${code}): ${seen.slice(-800)}`)));
  setTimeout(() => reject(new Error(`the engine did not start: ${seen.slice(-800)}`)), 90_000);
});

// The bridge, on stdio, as an MCP client starts it.
const bridge = spawn('node', [join(root, 'scripts/lapin-mcp.mjs')], { env: { ...process.env, LAPIN_PORT: String(port) } });
children.push(bridge);
let buffer = '', next = 1;
const waiting = new Map();
bridge.stdout.on('data', (d) => {
  buffer += d;
  for (let i; (i = buffer.indexOf('\n')) >= 0;) {
    const line = buffer.slice(0, i); buffer = buffer.slice(i + 1);
    if (!line.trim()) continue;
    const msg = JSON.parse(line);
    waiting.get(msg.id)?.(msg);
    waiting.delete(msg.id);
  }
});
bridge.stderr.on('data', (d) => process.stderr.write(d));
const rpc = (method, params) => new Promise((done) => { const id = next++; waiting.set(id, done); bridge.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`); });
async function tool(name, args = {}, { error = false } = {}) {
  const r = await rpc('tools/call', { name, arguments: args });
  const text = r.result?.content?.[0]?.text ?? '';
  if (!!r.result?.isError !== error) fail(`${name} ${JSON.stringify(args)}: ${error ? 'should have failed' : text}`);
  try { return JSON.parse(text); } catch { return text; }
}

const init = await rpc('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'lapin-mcp-check', version: '1' } });
console.log(`initialize: ${init.result.serverInfo.name} ${init.result.serverInfo.version}`);
const names = (await rpc('tools/list', {})).result.tools.map((t) => t.name);
console.log(`tools: ${names.join(', ')}`);
for (const t of ['lapin_documents', 'lapin_document_export']) if (!names.includes(t)) fail(`no ${t}`);
// The arguments come from the engine's own catalogue (session.catalogue), as in an installed app.
const info = await tool('lapin_info');
const addWall = info.commands.find((c) => c.startsWith('architecture.addWall('));
if (!addWall || !addWall.includes('x1') || !addWall.includes('storeyId')) fail(`lapin_info has no arguments for architecture.addWall: ${addWall}`);
if (!info.queries.some((q) => q.startsWith('document.preview'))) fail('lapin_info lists no queries');
console.log(`info: ${info.commands.length} commands, ${info.queries.length} queries; ${addWall}`);

// A small model: a 10 × 8 m ring of walls on the first storey, its plan, a boundary over one corner.
const storey = (await tool('lapin_model', { classes: ['IfcBuildingStorey'] }))[0].id;
for (const [x1, y1, x2, y2] of [[0, 0, 10, 0], [10, 0, 10, 8], [10, 8, 0, 8], [0, 8, 0, 0]])
  await tool('lapin_execute', { command: 'architecture.addWall', args: { storeyId: storey, x1, y1, x2, y2 } });
const plan = (await tool('lapin_execute', { command: 'view.create', args: { kind: 'plan', storeyId: storey, name: 'Plan', scale: 50 } })).id;
const boundary = (await tool('lapin_execute', { command: 'view.addBoundary', args: { viewId: plan, a: [-0.5, -0.5], b: [3.5, 2.5], name: 'Corner' } })).id;

// The assistant's flow (docs/mcp.md, Documents): templates, a document, its fields, its inputs, a picture, the export.
const { templates } = await tool('lapin_documents');
console.log(`templates: ${templates.map((t) => t.id).join(', ')}`);
if (!templates.some((t) => t.id === 'memoriu-arhitectura')) fail('no memoriu-arhitectura');
const doc = (await tool('lapin_execute', { command: 'document.create', args: { template: 'memoriu-arhitectura', name: 'Memoriu' } })).id;
await tool('lapin_execute', { command: 'project.setInfo', args: { client: 'Ionescu Maria', address: 'Str. Florilor 12, Huși' } });
const fields = await tool('lapin_query', { name: 'document.fields', args: { id: doc } });
console.log(`fields: ${fields.fields.length}, collections: ${fields.collections.length}, tables: ${fields.tables.length}`);
const entity = (await tool('lapin_model', { classes: ['LapinDocument'] })).find((n) => n.id === doc);
const blocks = [...entity.attrs.blocks, { kind: 'image', view: plan, boundary, caption: 'Corner of the plan' }];
await tool('lapin_execute', { command: 'document.update', args: { id: doc, blocks } });
const preview = await tool('lapin_query', { name: 'document.preview', args: { id: doc } });
const img = preview.blocks.find((b) => b.kind === 'image');
if (!img || Math.abs(img.w - 80) > 1e-3 || Math.abs(img.h - 60) > 1e-3) fail(`the boundary's picture is ${img?.w} × ${img?.h} mm, not 80 × 60`);
console.log(`picture: ${img.w} × ${img.h} mm (4 × 3 m at 1:50); still to complete: ${preview.missing.length}`);

await tool('lapin_document_export', { id: doc, format: 'pdf', path: 'relative.pdf' }, { error: true });
/** The names of a zip's entries, and one entry's bytes (central directory; stored or deflated). */
function zip(buf) {
  let e = buf.length - 22;
  while (e >= 0 && buf.readUInt32LE(e) !== 0x06054b50) e--;
  const entries = new Map();
  for (let p = buf.readUInt32LE(e + 16), n = buf.readUInt16LE(e + 10); n-- > 0;) {
    const [method, size, nameLen, extra, comment, local] = [buf.readUInt16LE(p + 10), buf.readUInt32LE(p + 20), buf.readUInt16LE(p + 28), buf.readUInt16LE(p + 30), buf.readUInt16LE(p + 32), buf.readUInt32LE(p + 42)];
    const name = buf.toString('utf8', p + 46, p + 46 + nameLen);
    const at = local + 30 + buf.readUInt16LE(local + 26) + buf.readUInt16LE(local + 28);
    entries.set(name, () => (method === 0 ? buf.subarray(at, at + size) : inflateRawSync(buf.subarray(at, at + size))));
    p += 46 + nameLen + extra + comment;
  }
  return entries;
}
for (const format of ['pdf', 'docx', 'odt']) {
  const r = await tool('lapin_document_export', { id: doc, format, path: join(out, 'nested', `memoriu.${format}`) });
  const bytes = readFileSync(r.path);
  if (bytes.length !== r.bytes) fail(`${format}: ${bytes.length} bytes on disk, ${r.bytes} said`);
  if (format === 'pdf' ? bytes.toString('latin1', 0, 5) !== '%PDF-' : bytes.readUInt32LE(0) !== 0x04034b50) fail(`${format}: not a ${format} file`);
  let pictures = '';
  if (format !== 'pdf') {
    const z = zip(bytes);
    const svgs = [...z.keys()].filter((n) => n.endsWith('.svg'));
    if (!svgs.length) fail(`${format}: no SVG picture in ${[...z.keys()].join(', ')}`);
    if (!z.get(svgs[0])().toString('utf8').startsWith('<svg')) fail(`${format}: ${svgs[0]} is not an SVG`);
    pictures = `, pictures ${svgs.join(', ')}`;
  }
  console.log(`${format}: ${r.path} (${r.bytes} bytes, ${r.missing.length} fields empty${pictures})`);
}
console.log('ok');
process.exit(0);
