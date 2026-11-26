#!/usr/bin/env node
// Sends the model open in Archicad to an Atrium CDE project as a reference document of type `model`,
// without Lapin open (docs/plan-archicad.md, stage A3, decision 882).
//
//   ATRIUM_TOKEN=atrium_pat_… node scripts/archicad-to-atrium.mjs --project <id> [options]
//
// How: Archicad (with the Tapir add-on, 2.0 or newer) is asked over its JSON API on 127.0.0.1 to save an IFC file
// in the temp folder (Tapir's IFCFileOperation); the file goes to the server through the server's MCP endpoint,
// tool cde_reference_add, as the token's account. A personal access token is accepted on /mcp only, so this is the
// one route with one kind of token. Up to 12 MiB the file travels inside the call (base64); larger, the tool
// opens an upload session and the bytes go to its URL in chunks (lapin-remote's resumable upload).
//
// Then (A1b) Archicad's Project Info (Tapir's GetProjectInfoFields) is proposed to the project's information record with the
// tool cde_project_info_propose (source archicad, the reference document named): the server maps it with the table Lapin
// shares, and an owner applies it in the CDE's Project information tab. --no-project-info skips it.
//
// Node 22, no dependency. The token is read from the environment (ATRIUM_TOKEN), never from an argument and
// never written anywhere. Exit codes: 0 done, 1 failed, 2 wrong use, 3 Archicad or Tapir problem,
// 4 server problem (sign-in, rights, refusal).

import { createHash } from 'node:crypto';
import { createReadStream, mkdtempSync, rmSync, statSync, readFileSync, existsSync, openSync, readSync, closeSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join, resolve } from 'node:path';
import { createInterface } from 'node:readline/promises';
import { fileURLToPath } from 'node:url';

export const FIRST_PORT = 19723;
export const LAST_PORT = 19744;
export const MIN_TAPIR = [2, 0, 0];
export const INLINE_LIMIT = 12 * 1024 * 1024; // base64 grows it by a third; the endpoint takes 24 MiB, a file inline 16 MiB
export const DEFAULT_SERVER = 'https://cde.lapinbim.com';

export class Fail extends Error {
  constructor(message, code = 1) { super(message); this.exitCode = code; }
}

// ---------------------------------------------------------------- pure helpers

export const candidatePorts = () => Array.from({ length: LAST_PORT - FIRST_PORT + 1 }, (_, i) => FIRST_PORT + i);

export function parseVersion(text) {
  const m = /^(\d+)\.(\d+)(?:\.(\d+))?/.exec(String(text ?? '').trim());
  return m ? [Number(m[1]), Number(m[2]), Number(m[3] ?? 0)] : null;
}

export function versionAtLeast(have, need) {
  for (let i = 0; i < 3; i++) {
    if (have[i] !== need[i]) return have[i] > need[i];
  }
  return true;
}

const OPTIONS = {
  port: 'value', server: 'value', project: 'value', title: 'value', folder: 'value', discipline: 'value', type: 'value',
  translator: 'value', elements: 'value', out: 'value', phase: 'value', 'inline-limit': 'value',
  ifczip: 'flag', 'dry-run': 'flag', 'list-translators': 'flag', 'list-instances': 'flag', help: 'flag', 'no-project-info': 'flag',
};
const ELEMENTS = ['EntireProject', 'VisibleElementsOnAllStories', 'AllElementsOnCurrentStory', 'VisibleElementsOnCurrentStory', 'SelectedElementsOnly'];

export function parseArgs(argv) {
  const o = { server: DEFAULT_SERVER, type: 'model' };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (!a.startsWith('--')) throw new Fail(`Unexpected argument "${a}". Try --help.`, 2);
    let [name, inline] = a.slice(2).split(/=(.*)/s, 2);
    const kind = OPTIONS[name];
    if (!kind) throw new Fail(`Unknown option --${name}. Try --help.`, 2);
    if (kind === 'flag') {
      if (inline !== undefined) throw new Fail(`--${name} takes no value.`, 2);
      o[name] = true;
      continue;
    }
    const value = inline ?? argv[++i];
    if (value === undefined || (inline === undefined && value.startsWith('--'))) throw new Fail(`--${name} needs a value.`, 2);
    o[name] = value;
  }
  if (o.port !== undefined) {
    const p = Number(o.port);
    if (!Number.isInteger(p) || p < FIRST_PORT || p > LAST_PORT) throw new Fail(`--port must be between ${FIRST_PORT} and ${LAST_PORT}.`, 2);
    o.port = p;
  }
  if (o.elements !== undefined && !ELEMENTS.includes(o.elements)) throw new Fail(`--elements is one of: ${ELEMENTS.join(', ')}.`, 2);
  if (o.elements !== undefined && o.translator === undefined) throw new Fail('--elements needs --translator (Tapir takes it only together with a translator).', 2);
  o.server = normaliseServer(o.server);
  if (o['inline-limit'] !== undefined) {
    const n = Number(o['inline-limit']);
    if (!Number.isInteger(n) || n < 0 || n > 16 * 1024 * 1024) throw new Fail('--inline-limit is a number of bytes up to 16777216.', 2);
    o.inlineLimit = n;
  }
  if (o.project !== undefined && !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(o.project)) {
    throw new Fail('--project is the project\'s id (a UUID). Leave it out to pick from your projects.', 2);
  }
  return o;
}

export function normaliseServer(text) {
  let u;
  try { u = new URL(/^[a-z]+:\/\//i.test(text) ? text : `https://${text}`); } catch { throw new Fail(`"${text}" is not a server address.`, 2); }
  const local = ['127.0.0.1', 'localhost', '[::1]'].includes(u.hostname);
  if (u.protocol !== 'https:' && !(u.protocol === 'http:' && local)) throw new Fail('The server address must be https (plain http only for this computer).', 2);
  return u.origin + u.pathname.replace(/\/+$/, '');
}

export function fileNameFor(projectName, ext) {
  const base = (projectName ?? '').replace(/\.(pln|pla|ifc)$/i, '').replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').trim().replace(/^\.+/, '');
  return `${base || 'archicad-model'}.${ext}`;
}

export function mimeFor(name) {
  return /\.ifczip$/i.test(name) ? 'application/zip' : 'application/x-step';
}

/**
 * Tapir's GetProjectInfoFields answer as the MCP tool's `raw` takes it: `{id: {label, value}}`, the empty values left out (they never
 * propose anything).
 */
export function projectInfoRaw(fields) {
  const raw = {};
  for (const f of Array.isArray(fields) ? fields : []) {
    const id = typeof f?.projectInfoId === 'string' ? f.projectInfoId.trim() : '';
    const value = f?.projectInfoValue == null ? '' : String(f.projectInfoValue).trim();
    if (!id || id.length > 100 || !value) continue;
    raw[id] = f.projectInfoName ? { label: String(f.projectInfoName), value } : { value };
  }
  return raw;
}

/** The arguments of cde_project_info_propose for Archicad's Project Info, read from the reference document just added. */
export function proposeArgs(project, fields, referenceId) {
  return { project, source: 'archicad', raw: projectInfoRaw(fields), ...(referenceId ? { referenceId } : {}) };
}

/** The numbers an upload goes by: the byte ranges of its chunks, starting at the server's offset. */
export function* chunkRanges(size, offset, chunk) {
  for (let at = offset; at < size; at += chunk) yield [at, Math.min(size, at + chunk)];
}

// ---------------------------------------------------------------- Archicad

async function archicadPost(port, body, timeoutMs) {
  const res = await fetch(`http://127.0.0.1:${port}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), signal: AbortSignal.timeout(timeoutMs),
  });
  return res.json();
}

export async function probePort(port, post = archicadPost) {
  try {
    const r = await post(port, { command: 'API.IsAlive' }, 3000);
    return r?.succeeded === true && r.result?.isAlive === true;
  } catch { return false; }
}

export const BUSY = /ongoing user input|busy/i;

async function tapir(port, name, params = {}, timeoutMs = 600000, post = archicadPost, patience = 15) {
  // Archicad answers "ongoing user input" while a command, a dialog or a drag is under way: wait for the user to finish
  for (let attempt = 0; ; attempt++) {
    const r = await post(port, {
      command: 'API.ExecuteAddOnCommand',
      parameters: { addOnCommandId: { commandNamespace: 'TapirCommand', commandName: name }, addOnCommandParameters: params },
    }, timeoutMs);
    if (r?.succeeded !== true) {
      const e = r?.error ?? r?.result?.error;
      const message = e?.message ?? JSON.stringify(r).slice(0, 300);
      if (BUSY.test(message) && attempt < patience) {
        if (attempt === 0) console.error('Archicad is busy (a command or dialog is open); waiting for it…');
        await new Promise((ok) => setTimeout(ok, 2000));
        continue;
      }
      throw new Fail(`Archicad refused ${name}: ${message}`, 3);
    }
    const out = r.result?.addOnCommandResponse;
    if (out?.error) throw new Fail(`Tapir's ${name} failed: ${out.error.message ?? JSON.stringify(out.error)}`, 3);
    return out ?? {};
  }
}

async function describeInstance(port) {
  const inst = { port, tapir: null, project: null, untitled: null };
  try {
    inst.tapir = (await tapir(port, 'GetAddOnVersion', {}, 5000)).version ?? null;
  } catch (e) { inst.error = e.message; return inst; }
  try {
    const p = await tapir(port, 'GetProjectInfo', {}, 5000);
    inst.untitled = p.isUntitled ?? null;
    inst.project = p.projectName ?? (p.projectLocation ? basename(p.projectLocation) : null);
  } catch { /* the name is for the list only */ }
  return inst;
}

export async function findInstances() {
  const alive = (await Promise.all(candidatePorts().map(async (p) => ((await probePort(p)) ? p : null)))).filter(Boolean);
  return Promise.all(alive.map(describeInstance));
}

async function chooseInstance(opts) {
  if (opts.port !== undefined) {
    if (!(await probePort(opts.port))) throw new Fail(`No Archicad answers on port ${opts.port}.`, 3);
    return describeInstance(opts.port);
  }
  const found = await findInstances();
  if (found.length === 0) {
    throw new Fail(`No Archicad answers on 127.0.0.1, ports ${FIRST_PORT}–${LAST_PORT}. Start Archicad, open the project, and check that Tapir is installed (docs/user/archicad-to-atrium.md).`, 3);
  }
  const label = (i) => `port ${i.port}: ${i.project ?? (i.untitled ? 'untitled project' : 'project')}${i.tapir ? `, Tapir ${i.tapir}` : ', no Tapir'}`;
  if (found.length === 1) return found[0];
  if (process.stdin.isTTY && !opts['list-instances']) {
    console.error('Several Archicads are running:');
    found.forEach((i, n) => console.error(`  ${n + 1}) ${label(i)}`));
    const answer = (await ask('Which one? ')).trim();
    const pick = found[Number(answer) - 1];
    if (!pick) throw new Fail('No such choice.', 2);
    return pick;
  }
  throw new Fail(`Several Archicads are running; choose one with --port:\n${found.map((i) => '  ' + label(i)).join('\n')}`, 2);
}

function requireTapir(inst) {
  if (!inst.tapir && inst.error) throw new Fail(`Archicad on port ${inst.port} did not answer Tapir's version call: ${inst.error}`, 3);
  const v = parseVersion(inst.tapir);
  if (!v) throw new Fail(`Archicad on port ${inst.port} does not answer to Tapir. Install the Tapir add-on (version ${MIN_TAPIR.slice(0, 2).join('.')} or newer) from https://github.com/ENZYME-APD/tapir-archicad-automation/releases and restart Archicad.`, 3);
  if (!versionAtLeast(v, MIN_TAPIR)) throw new Fail(`Tapir ${inst.tapir} is too old; this needs ${MIN_TAPIR.slice(0, 2).join('.')} or newer (https://github.com/ENZYME-APD/tapir-archicad-automation/releases).`, 3);
}

async function exportIfc(inst, opts) {
  const translators = (await tapir(inst.port, 'GetIFCExportTranslators')).translators ?? [];
  if (opts['list-translators']) return { translators };
  if (opts.translator !== undefined && !translators.some((t) => t.name === opts.translator)) {
    throw new Fail(`Archicad has no IFC translator named "${opts.translator}". It has: ${translators.map((t) => t.name).join('; ')}.`, 2);
  }
  const ext = opts.ifczip ? 'ifczip' : 'ifc';
  const name = fileNameFor(inst.project, ext);
  let dir, path, temp = false;
  if (opts.out) {
    path = resolve(opts.out);
    dir = null;
  } else {
    dir = mkdtempSync(join(tmpdir(), 'archicad-to-atrium-'));
    path = join(dir, name);
    temp = true;
  }
  const params = { method: 'save', ifcFilePath: path, overwrite: true, fileType: ext };
  if (opts.translator !== undefined) {
    params.translatorName = opts.translator;
    if (opts.elements) params.elementsToExport = opts.elements;
  }
  try {
    await tapir(inst.port, 'IFCFileOperation', params);
  } catch (e) {
    if (dir) rmSync(dir, { recursive: true, force: true });
    if (e instanceof Fail && !BUSY.test(e.message)) e.message += ' (Archicad\'s demo mode, or a model that is not open, may refuse an export.)';
    throw e;
  }
  if (!existsSync(path) || statSync(path).size === 0) {
    if (dir) rmSync(dir, { recursive: true, force: true });
    throw new Fail('Archicad said it saved the file, but nothing is at ' + path + '.', 3);
  }
  return { path, name: opts.out ? basename(path) : name, dir, temp, translators };
}

// ---------------------------------------------------------------- files

export function sha256File(path) {
  return new Promise((ok, no) => {
    const h = createHash('sha256');
    createReadStream(path).on('data', (d) => h.update(d)).on('error', no).on('end', () => ok(h.digest('hex')));
  });
}

function readRange(path, from, to) {
  const fd = openSync(path, 'r');
  try {
    const buf = Buffer.alloc(to - from);
    let got = 0;
    while (got < buf.length) {
      const n = readSync(fd, buf, got, buf.length - got, from + got);
      if (n === 0) break;
      got += n;
    }
    return buf.subarray(0, got);
  } finally { closeSync(fd); }
}

// ---------------------------------------------------------------- the server's MCP endpoint

const MCP_VERSION = '2025-11-25';

export class McpClient {
  constructor(server, token, fetchImpl = fetch) {
    this.url = `${server}/mcp`; this.server = server; this.token = token; this.fetch = fetchImpl; this.id = 0; this.session = null;
  }

  headers() {
    const h = { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', Authorization: `Bearer ${this.token}`, 'MCP-Protocol-Version': MCP_VERSION };
    if (this.session) h['Mcp-Session-Id'] = this.session;
    return h;
  }

  async rpc(method, params, notification = false) {
    const body = { jsonrpc: '2.0', method, ...(params ? { params } : {}), ...(notification ? {} : { id: ++this.id }) };
    let res;
    try {
      res = await this.fetch(this.url, { method: 'POST', headers: this.headers(), body: JSON.stringify(body), signal: AbortSignal.timeout(120000) });
    } catch (e) { throw new Fail(`Cannot reach ${this.url}: ${e.cause?.message ?? e.message}`, 4); }
    if (res.status === 401) throw new Fail('The server refused the token (401): it is wrong, expired or revoked. Make a new one in the CDE\'s Access tokens panel.', 4);
    if (res.status === 429) throw new Fail('The server says too many requests (429); wait a minute and run it again.', 4);
    if (res.status === 403) throw new Fail(`The server refused the request (403): ${(await res.text()).slice(0, 200)}`, 4);
    if (notification) return null;
    if (!res.ok) throw new Fail(`The server answered ${res.status}: ${(await res.text()).slice(0, 300)}`, 4);
    const sid = res.headers.get('mcp-session-id');
    if (sid) this.session = sid;
    const text = await res.text();
    let json;
    try { json = JSON.parse(text); } catch { throw new Fail(`${this.url} did not answer with JSON; is it an Atrium server? (${text.slice(0, 100)})`, 4); }
    if (json.error) throw new Fail(`The server's MCP error ${json.error.code}: ${json.error.message}`, 4);
    return json.result;
  }

  async open() {
    await this.rpc('initialize', { protocolVersion: MCP_VERSION, capabilities: {}, clientInfo: { name: 'archicad-to-atrium', version: '1' } });
    await this.rpc('notifications/initialized', undefined, true);
  }

  async tool(name, args) {
    const r = await this.rpc('tools/call', { name, arguments: args });
    const data = r.structuredContent ?? tryJson(r.content?.[0]?.text);
    if (r.isError) {
      const e = data?.error;
      if (e?.code === 'scope') throw new Fail('This token may only read. Make a read+write token in the CDE\'s Access tokens panel.', 4);
      throw new Fail(`The server refused ${name}: ${e ? `${e.status ?? ''} ${e.code ?? ''} ${e.message ?? ''}`.trim() : (r.content?.[0]?.text ?? 'unknown')}`, 4);
    }
    return data;
  }
}

const tryJson = (t) => { try { return JSON.parse(t); } catch { return null; } };

async function uploadSession(mcp, upload, path, size, sha256) {
  const range = chunkRanges(size, upload.offset ?? 0, upload.chunk ?? 8 * 1024 * 1024);
  let sent = upload.offset ?? 0;
  for (const [from, to] of range) {
    for (let attempt = 1; ; attempt++) {
      const res = await fetch(upload.url, {
        method: 'PATCH',
        headers: { Authorization: `Bearer ${mcp.token}`, 'Content-Type': 'application/offset+octet-stream', 'Upload-Offset': String(from) },
        body: readRange(path, from, to),
      }).catch((e) => ({ ok: false, status: 0, text: async () => e.message }));
      if (res.ok) break;
      if (attempt >= 3 || res.status === 401 || res.status === 403 || res.status === 404) {
        throw new Fail(`The upload stopped at ${from} of ${size} bytes: ${res.status} ${(await res.text()).slice(0, 200)}`, 4);
      }
      await new Promise((r) => setTimeout(r, 1000 * attempt));
    }
    sent = to;
    process.stderr.write(`\r  uploaded ${(sent / 1048576).toFixed(1)} of ${(size / 1048576).toFixed(1)} MiB`);
  }
  process.stderr.write('\n');
  return sha256;
}

async function chooseProject(mcp, opts) {
  const info = await mcp.tool('cde_projects', {});
  const projects = (info.projects ?? []).filter((p) => p.role !== 'viewer');
  if (opts.project) {
    const mine = (info.projects ?? []).find((p) => p.id.toLowerCase() === opts.project.toLowerCase());
    if (!mine) throw new Fail('The token\'s account has no such project. Yours: ' + (info.projects ?? []).map((p) => `${p.name} (${p.id})`).join('; '), 4);
    if (mine.role === 'viewer') throw new Fail(`You are a viewer in "${mine.name}"; adding a document needs the editor or owner role.`, 4);
    return { ...mine, account: info.account, token: info.token };
  }
  if (projects.length === 0) throw new Fail('This account has no project it may add documents to.', 4);
  if (projects.length === 1) return { ...projects[0], account: info.account, token: info.token };
  if (process.stdin.isTTY) {
    console.error('Projects you may add documents to:');
    projects.forEach((p, n) => console.error(`  ${n + 1}) ${p.name} (${p.role})`));
    const pick = projects[Number((await ask('Which one? ')).trim()) - 1];
    if (!pick) throw new Fail('No such choice.', 2);
    return { ...pick, account: info.account, token: info.token };
  }
  throw new Fail(`Several projects; choose one with --project:\n${projects.map((p) => `  ${p.id}  ${p.name} (${p.role})`).join('\n')}`, 2);
}

async function ask(question) {
  const rl = createInterface({ input: process.stdin, output: process.stderr });
  try { return await rl.question(question); } finally { rl.close(); }
}

export async function sendToAtrium({ mcp, project, file, name, size, sha256, opts, inlineLimit = INLINE_LIMIT }) {
  const record = { project, title: opts.title ?? name.replace(/\.(ifczip|ifc)$/i, ''), type: opts.type };
  if (opts.folder) record.folder = opts.folder;
  if (opts.discipline) record.discipline = opts.discipline;
  if (opts.phase) record.phase = opts.phase.split(/[,;]/).map((s) => s.trim()).filter(Boolean);
  const mime = mimeFor(name);
  if (size <= inlineLimit) {
    return mcp.tool('cde_reference_add', { ...record, file: { name, content: readFileSync(file).toString('base64'), mime } });
  }
  const asked = { ...record, file: { name, sha256, size, mime } };
  let answer = await mcp.tool('cde_reference_add', asked);
  if (answer.uploadRequired) {
    await uploadSession(mcp, answer.upload, file, size, sha256);
    answer = await mcp.tool('cde_reference_add', asked);
  }
  if (answer.uploadRequired) throw new Fail('The server still wants the file after the upload; run the script again (the upload resumes).', 4);
  return answer;
}

/** Archicad's Project Info proposed to the project's information (A1b); a failure is said and leaves the document added. */
async function proposeProjectInfo(mcp, inst, project, referenceId) {
  let fields;
  try { fields = (await tapir(inst.port, 'GetProjectInfoFields', {}, 30000)).fields ?? []; } catch (e) {
    console.error(`  info     Archicad's Project Info was not read (${e.message}); nothing proposed.`);
    return;
  }
  const args = proposeArgs(project.id, fields, referenceId);
  if (Object.keys(args.raw).length === 0) { console.log('  info     Archicad\'s Project Info is empty: nothing proposed.'); return; }
  try {
    const p = await mcp.tool('cde_project_info_propose', args);
    if (p?.proposal === null) console.log('  info     Archicad\'s Project Info says nothing the project\'s information lacks: nothing proposed.');
    else console.log(`  info     project information proposed: #${p.number}, ${p.changes?.length ?? 0} changes, for an owner to apply (Project information tab)`);
  } catch (e) {
    console.error(`  info     the project information was not proposed: ${e.message}${/unknown tool|-32602|not found/i.test(e.message) ? ' (the server is older than A1b)' : ''}`);
  }
}

// ---------------------------------------------------------------- main

const HELP = `Sends the model open in Archicad to an Atrium CDE project as a reference document of type "model".

  ATRIUM_TOKEN=<personal access token, read+write> node scripts/archicad-to-atrium.mjs [options]

Archicad
  --port <n>            the Archicad to use (ports ${FIRST_PORT}-${LAST_PORT}); by default the only one running, else a list
  --list-instances      show the running Archicads and stop
  --translator <name>   the IFC export translator (default: the one Archicad's own Save dialog offers)
  --list-translators    show the translators and stop
  --elements <what>     with --translator: ${ELEMENTS.join(' | ')}
  --ifczip              save a compressed .ifczip (Archicad writes the zip)
  --out <file>          keep the IFC at this path instead of a temp file that is removed afterwards
Atrium
  --server <url>        default ${DEFAULT_SERVER}
  --project <id>        the project (default: your only one, else a list)
  --title <text>        the document's title (default: the file's name)
  --type <id>           the document type (default model)
  --folder <id>         the folder (default: the type's folder)
  --discipline <text>   e.g. A, S, MEP
  --phase <list>        e.g. "DTAC, PT"
  --no-project-info     do not propose Archicad's Project Info to the project's information
  --dry-run             export only; upload nothing

The token is read from ATRIUM_TOKEN only. Make it in the CDE's Access tokens panel (scope read+write).
Exit codes: 0 done, 1 failed, 2 wrong use, 3 Archicad or Tapir, 4 server.`;

async function main(argv) {
  const opts = parseArgs(argv);
  if (opts.help) { console.log(HELP); return 0; }
  const token = process.env.ATRIUM_TOKEN?.trim();
  if (!token && !opts['dry-run'] && !opts['list-instances'] && !opts['list-translators']) {
    throw new Fail('ATRIUM_TOKEN is not set. Make a personal access token (read+write) in the CDE\'s Access tokens panel and put it in the environment: export ATRIUM_TOKEN=… (never on the command line).', 2);
  }
  if (token && !/^atrium_pat_[A-Za-z0-9_-]{43}$/.test(token)) {
    throw new Fail('ATRIUM_TOKEN is not a personal access token (it starts with atrium_pat_).', 2);
  }

  if (opts['list-instances']) {
    const found = await findInstances();
    if (!found.length) throw new Fail('No Archicad answers.', 3);
    for (const i of found) console.log(`port ${i.port}  ${i.project ?? (i.untitled ? '(untitled)' : '?')}  Tapir ${i.tapir ?? 'missing'}`);
    return 0;
  }

  const inst = await chooseInstance(opts);
  requireTapir(inst);
  console.error(`Archicad on port ${inst.port}: ${inst.project ?? (inst.untitled ? 'untitled project' : 'project')}, Tapir ${inst.tapir}`);

  // The server first: a wrong token or project is found before Archicad is asked for a big export
  let mcp = null, project = null;
  if (!opts['dry-run'] && !opts['list-translators']) {
    mcp = new McpClient(opts.server, token);
    await mcp.open();
    project = await chooseProject(mcp, opts);
    if (!project.token?.scope?.includes('write')) throw new Fail('This token may only read. Make a read+write token in the CDE\'s Access tokens panel.', 4);
    console.error(`Server ${opts.server}, project "${project.name}" (${project.role}), as ${project.account?.name ?? '?'}`);
  }

  const made = await exportIfc(inst, opts);
  if (opts['list-translators']) {
    for (const t of made.translators) console.log(`${t.preview ? '* ' : '  '}${t.name}`);
    return 0;
  }
  try {
    const size = statSync(made.path).size;
    const sha256 = await sha256File(made.path);
    console.error(`Saved ${made.name}: ${size} bytes, sha256 ${sha256}${made.temp ? '' : `\n  at ${made.path}`}`);
    if (opts['dry-run']) { console.log(`dry run: ${made.name} ${size} bytes sha256 ${sha256}`); return 0; }

    const ref = await sendToAtrium({ mcp, project: project.id, file: made.path, name: made.name, size, sha256, opts, inlineLimit: opts.inlineLimit });
    console.log(`Added the reference document "${ref.title}"`);
    console.log(`  id       ${ref.id}`);
    console.log(`  type     ${ref.typeName ?? ref.type}${ref.folderPath ? `, in ${ref.folderPath}` : ''}`);
    console.log(`  file     ${made.name}, ${size} bytes, sha256 ${sha256}`);
    console.log(`  status   ${ref.status}`);
    console.log(`  open     ${opts.server}/  (project "${project.name}", Documents tab; ${project.id})`);
    for (const d of ref.duplicates ?? []) console.log(`  note     the same file is already in document ${d.id ?? d}${d.title ? ` ("${d.title}")` : ''}`);
    if (!opts['no-project-info']) await proposeProjectInfo(mcp, inst, project, ref.id);
    return 0;
  } finally {
    if (made.dir) rmSync(made.dir, { recursive: true, force: true });
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2)).then((c) => process.exit(c), (e) => {
    console.error(e instanceof Fail ? `Error: ${e.message}` : `Unexpected: ${e?.stack ?? e}`);
    process.exit(e instanceof Fail ? e.exitCode : 1);
  });
}
