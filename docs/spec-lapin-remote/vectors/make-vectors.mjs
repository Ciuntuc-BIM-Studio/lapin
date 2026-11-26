#!/usr/bin/env node
// The conformance vectors of lapin-remote 1.0 (docs/spec-lapin-remote.md), made from the definitions below.
//
//   node make-vectors.mjs          writes every vector file beside this script and prints the ids
//   node make-vectors.mjs --check  makes them in memory and fails when a file on disk differs
//
// No dependencies: Node's crypto and zlib only. The canonical JSON (RFC 8785, JCS) is the small function `jcs` below:
// ECMAScript's JSON.stringify serialises strings and numbers exactly as JCS asks, and JavaScript's default string sort
// compares UTF-16 code units, which is JCS's key order.

import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const check = process.argv.includes('--check');
const files = new Map(); // relative path -> Buffer

// ---------------------------------------------------------------- canonical JSON (RFC 8785)

function jcs(v) {
  if (v === null || typeof v === 'boolean') return JSON.stringify(v);
  if (typeof v === 'number') {
    if (!Number.isFinite(v)) throw new Error('JCS: NaN and Infinity are not JSON');
    return JSON.stringify(v); // ECMAScript Number serialisation, -0 as 0
  }
  if (typeof v === 'string') {
    if (/\p{Surrogate}/u.test(v)) throw new Error('JCS: a lone surrogate is not valid');
    return JSON.stringify(v);
  }
  if (Array.isArray(v)) return '[' + v.map(jcs).join(',') + ']';
  const keys = Object.keys(v).sort(); // UTF-16 code units
  return '{' + keys.map(k => jcs(k) + ':' + jcs(v[k])).join(',') + '}';
}

const sha = b => createHash('sha256').update(b).digest('hex');
const canon = o => Buffer.from(jcs(o), 'utf8');
const put = (path, bytes) => files.set(path, Buffer.isBuffer(bytes) ? bytes : Buffer.from(bytes, 'utf8'));
const putJson = (path, o) => put(path, JSON.stringify(o, null, 2) + '\n'); // readable files (not objects)

// ---------------------------------------------------------------- blobs

const blobDefs = [
  ['empty.bin', Buffer.alloc(0), 'zero bytes'],
  ['hello.txt', Buffer.from('Hello, Lapin!\n', 'utf8'), 'ASCII text with a final LF'],
  ['unicode.txt', Buffer.from('Pereții etajului 2 — varianta B ✓\n', 'utf8'), 'UTF-8 text with multi-byte characters'],
  ['document.json', Buffer.from('{"views":[{"id":"v1","name":"Level 0"}]}', 'utf8'), 'a project/document.json'],
  ['walls.arrow', Buffer.from(Array.from({ length: 256 }, (_, i) => i)), 'bytes 0x00..0xFF (stands in for an Arrow table)'],
  ['relations.arrow', Buffer.concat([Buffer.from('ARROW1\0\0', 'latin1'), Buffer.from(Array.from({ length: 56 }, (_, i) => (i * 37 + 11) & 0xff))]), '64 binary bytes (stands in for model/relations.arrow)'],
  ['hello.py', Buffer.from('print("Hello, Lapin!")\n', 'utf8'), 'a project script'],
  ['issue.pdf', Buffer.from('%PDF-1.7\n% lapin-remote vector stub\n%%EOF\n', 'utf8'), 'an issued file (a stub PDF)'],
  ['missing.py', Buffer.from('print("never uploaded")\n', 'utf8'), 'named by tree T4, never uploaded (the 422 case)'],
];
const blob = {};
for (const [name, bytes, what] of blobDefs) {
  put(`blobs/${name}`, bytes);
  blob[name] = { id: sha(bytes), size: bytes.length, what };
}
const B = n => blob[n].id;
const S = n => blob[n].size;

// ---------------------------------------------------------------- trees (the manifest without "foreign")

const part = (path, name, module) => (module ? { path, sha256: B(name), size: S(name), module } : { path, sha256: B(name), size: S(name) });
const manifest = (parts, classes, modules) => ({ format: 'lapin-project', version: 3, app: '0.1.1612', product: 'lapin', modules, parts, classes });
const core = { id: 'core', schemaVersion: 1 };
const scripting = { id: 'scripting', schemaVersion: 1 };

const trees = {
  T1: manifest([part('project/document.json', 'document.json'), part('model/relations.arrow', 'relations.arrow')], {}, [core]),
  T2: manifest([
    part('project/document.json', 'document.json'),
    part('model/core/IfcWall/_.arrow', 'walls.arrow', 'core'),
    part('model/relations.arrow', 'relations.arrow'),
    part('scripts/hello.py', 'hello.py', 'scripting'),
  ], { IfcWall: 'core' }, [core, scripting]),
  T3: manifest([
    part('project/document.json', 'document.json'),
    part('model/relations.arrow', 'relations.arrow'),
    part('scripts/hello.py', 'hello.py', 'scripting'),
  ], {}, [core, scripting]),
  T4: manifest([
    part('project/document.json', 'document.json'),
    part('model/core/IfcWall/_.arrow', 'walls.arrow', 'core'),
    part('model/relations.arrow', 'relations.arrow'),
    part('scripts/hello.py', 'hello.py', 'scripting'),
    part('scripts/missing.py', 'missing.py', 'scripting'),
  ], { IfcWall: 'core' }, [core, scripting]),
};

// ---------------------------------------------------------------- commits and tags

const mihai = { id: 'atrium:5d2c8a4e-1f3b-4c6d-9e7a-0b1c2d3e4f50', name: 'Mihai Ionescu' };
const ana = { id: 'atrium:a17e9c3b-6d42-4f8a-b5c1-7e2d9f0a3b64', name: 'Ana Popescu' };
const objects = {}; // name -> { kind, value, bytes, id }
function obj(name, kind, value) {
  const bytes = canon(value);
  objects[name] = { kind, value, bytes, id: sha(bytes) };
  put(`objects/${name}.json`, bytes);
  return objects[name].id;
}
for (const [k, t] of Object.entries(trees)) obj(k, 'tree', t);
const T = k => objects[k].id;

const commit = (tree, parents, author, at, message, entities, extra = {}) => ({
  format: 'lapin-commit', v: 1, tree, parents, author, at, message, app: '0.1.1612', product: 'lapin', entities, ...extra,
});
const c1 = obj('c1', 'commit', commit(T('T1'), [], mihai, '2026-11-23T10:00:00Z', 'Initial model', 0));
const c2 = obj('c2', 'commit', commit(T('T2'), [c1], mihai, '2026-11-23T10:42:00Z', 'Pereții etajului 0, varianta B', 4));
const c3 = obj('c3', 'commit', commit(T('T3'), [c1], ana, '2026-11-23T10:45:30Z', 'Schedule script', 0));
const c4 = obj('c4', 'commit', commit(T('T4'), [c2], mihai, '2026-11-23T11:05:00Z', 'Second script', 4));
// A commit from a later 1.x writer: fields this reader does not know, kept and hashed like the others.
const cFuture = obj('commit-future', 'commit', commit(T('T2'), [c1], mihai, '2026-11-23T10:42:00.250Z', 'From a newer Lapin', 4,
  { 'x-review': { zeta: 1, alpha: [true, null, 1.5, 'ș'] }, Zone: 'B' }));

const tagIssue = obj('tag-issue', 'tag', {
  format: 'lapin-tag', v: 1, commit: c2, kind: 'issue', number: 1,
  issue: '3vB2YO$MX4xv5uCqZZG05x', revision: 'P01', status: 'S2', container: 'Shared',
  files: [{ name: 'PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf', sha256: B('issue.pdf'), size: S('issue.pdf') }],
  author: mihai, at: '2026-11-23T10:50:00Z', note: 'For coordination',
});
const tagVersion = obj('tag-version', 'tag', {
  format: 'lapin-tag', v: 1, commit: c2, kind: 'version', number: 3, name: 'Version 3',
  author: mihai, at: '2026-11-23T10:43:00Z', note: 'Before the review',
});

// ---------------------------------------------------------------- canonicalisation vectors (input -> expected bytes)

const canonCases = [
  ['keys-order', '{\n  "b": 2, "a": 1, "B": 0, "\\u20ac": "euro", "\\r": "cr", "1": "one",\n  "\\ud83d\\ude00": "emoji (U+1F600, D83D DE00)", "\\ufb33": "U+FB33 sorts after the emoji in UTF-16 order"\n}\n'],
  ['numbers', '[1.0, 1e21, 1e-7, -0, 0.1, 100, 123456789012345678, 9007199254740991, 4.50, 2e0, -1.5E3]'],
  ['strings', '["\\u0041", "\\/", "\\u2028", "\\u001f", "\\t", "quote\\"back\\\\slash", "Pereții", "\\u00e9 vs e\\u0301"]'],
  ['nested', '{ "z": [ {"y": null, "x": true} , [] , {} ], "a": { "c": false, "b": "" } }'],
];
for (const [name, input] of canonCases) {
  put(`canonical/${name}.input.json`, input);
  put(`canonical/${name}.expected.json`, jcs(JSON.parse(input)));
}

// ---------------------------------------------------------------- HTTP sessions

const P = '0b6f1c2e-5a7d-4c1e-9f3a-2d8e6b4a1c90';
const base = '/lapin-remote/1.0';
const proj = `${base}/projects/${P}`;
const auth = who => ({ authorization: `Bearer test-access-token-${who}` });
const json = { 'content-type': 'application/json; charset=utf-8' };
const octet = { 'content-type': 'application/octet-stream' };
const err = (code, message, extra = {}) => ({ code, message, ...extra });
const limits = { maxObject: 2147483648, chunk: 16777216, maxBatch: 10000, maxPush: 4294967296, maxPack: 4294967296, maxCommitsPage: 1000 };
const objFile = n => `../../objects/${n}.json`;
const blobFile = n => `../../blobs/${n}`;
const objBody = n => ({ bodyFile: objFile(n) });

function session(dir, steps) {
  steps.forEach((s, i) => {
    const n = String(i + 1).padStart(2, '0');
    putJson(`session/${dir}/${n}-${s.name}.json`, { step: i + 1, title: s.title, request: s.request, response: s.response, ...(s.capture ? { capture: s.capture } : {}), ...(s.opaque ? { opaque: s.opaque } : {}) });
  });
}

const ref = (name, target, by, opaque = true) => ({ name, target, updatedAt: '2026-11-23T10:00:05Z', updatedBy: by });
const refsBefore = { refs: [ref('refs/heads/main', c1, { id: mihai.id, name: mihai.name })] };
const refsAfter = { refs: [
  ref('refs/heads/main', c2, { id: mihai.id, name: mihai.name }),
  ref('refs/tags/issue/1-P01', tagIssue, { id: mihai.id, name: mihai.name }),
] };
const refsOpaque = r => r.refs.map((_, i) => `/response/body/refs/${i}/updatedAt`);

// The push: Mihai (owner) has c2 on main, with the issue tag issue/1-P01; the server has c1 on main.
// An earlier, interrupted push already uploaded issue.pdf, so the server says it is not missing.
const closure = [c2, T('T2'), B('walls.arrow'), B('hello.py'), tagIssue, B('issue.pdf')];
session('push', [
  { name: 'capabilities', title: 'The client reads what the server supports (anonymous).',
    request: { method: 'GET', path: `${base}/capabilities`, headers: {} },
    response: { status: 200, headers: json, body: {
      protocol: 'lapin-remote', version: '1.0', majors: [1],
      objects: { commit: [1], tag: [1], tree: [3] },
      limits, encodings: ['gzip'],
      features: ['uploads', 'pack', 'package', 'viewer', 'range', 'projects.create'],
      auth: { foundation: '/foundation/1.1' },
    } } },
  { name: 'project', title: 'The project, the caller\'s role and the project\'s rules.',
    request: { method: 'GET', path: proj, headers: auth('mihai') },
    response: { status: 200, headers: json, body: { id: P, name: 'Bloc P+4', defaultRef: 'refs/heads/main', role: 'owner', rules: { main: 'editor', issueTags: 'owner' }, limits } } },
  { name: 'refs', title: '1. Fetch the refs: main is c1, as last seen.',
    request: { method: 'GET', path: `${proj}/refs`, headers: auth('mihai') },
    response: { status: 200, headers: { ...json, etag: '"r-7"' }, body: refsBefore },
    opaque: ['/response/headers/etag', ...refsOpaque(refsBefore)] },
  { name: 'missing', title: '2-3. The closure (c2, its tree, the new parts, the tag, the issued file) against objects/missing.',
    request: { method: 'POST', path: `${proj}/objects/missing`, headers: { ...auth('mihai'), ...json }, body: { hashes: closure } },
    response: { status: 200, headers: json, body: { missing: closure.filter(h => h !== B('issue.pdf')) } } },
  { name: 'put-tree', title: '4. Upload: the tree T2 in one PUT.',
    request: { method: 'PUT', path: `${proj}/objects/${T('T2')}`, headers: { ...auth('mihai'), ...octet }, ...objBody('T2') },
    response: { status: 201, headers: { etag: `"${T('T2')}"` }, body: null } },
  { name: 'put-blob', title: '4. Upload: scripts/hello.py.',
    request: { method: 'PUT', path: `${proj}/objects/${B('hello.py')}`, headers: { ...auth('mihai'), ...octet }, bodyFile: blobFile('hello.py') },
    response: { status: 201, headers: { etag: `"${B('hello.py')}"` }, body: null } },
  { name: 'put-existing', title: '4. A PUT of an object the server has already: 200, nothing written.',
    request: { method: 'PUT', path: `${proj}/objects/${B('issue.pdf')}`, headers: { ...auth('mihai'), ...octet }, bodyFile: blobFile('issue.pdf') },
    response: { status: 200, headers: { etag: `"${B('issue.pdf')}"` }, body: null } },
  { name: 'upload-start', title: '4. A resumable upload (any size may use it; above `chunk` it must): walls.arrow, 256 bytes.',
    request: { method: 'POST', path: `${proj}/uploads`, headers: { ...auth('mihai'), ...json }, body: { sha256: B('walls.arrow'), size: 256 } },
    response: { status: 201, headers: { ...json, location: `${proj}/uploads/{upload}` }, body: { id: '{upload}', offset: 0, chunk: 16777216, expiresAt: '2026-11-24T10:51:00Z' } },
    capture: { upload: '/response/body/id' }, opaque: ['/response/body/id', '/response/body/expiresAt', '/response/headers/location'] },
  { name: 'upload-chunk-1', title: '4. The first chunk, bytes 0..99.',
    request: { method: 'PATCH', path: `${proj}/uploads/{upload}`, headers: { ...auth('mihai'), 'content-type': 'application/offset+octet-stream', 'upload-offset': '0', 'content-length': '100' }, bodyFile: blobFile('walls.arrow'), bodyRange: { offset: 0, length: 100 } },
    response: { status: 204, headers: { 'upload-offset': '100' }, body: null } },
  { name: 'upload-head', title: '4. The connection broke; the client asks where to resume.',
    request: { method: 'HEAD', path: `${proj}/uploads/{upload}`, headers: auth('mihai') },
    response: { status: 200, headers: { 'upload-offset': '100', 'upload-length': '256', 'cache-control': 'private, no-store' }, body: null } },
  { name: 'upload-chunk-2', title: '4. The last chunk, bytes 100..255: the server checks the hash and the object exists.',
    request: { method: 'PATCH', path: `${proj}/uploads/{upload}`, headers: { ...auth('mihai'), 'content-type': 'application/offset+octet-stream', 'upload-offset': '100', 'content-length': '156' }, bodyFile: blobFile('walls.arrow'), bodyRange: { offset: 100, length: 156 } },
    response: { status: 201, headers: { ...json, 'upload-offset': '256' }, body: { sha256: B('walls.arrow'), size: 256 } } },
  { name: 'put-tag', title: '4. The tag object.',
    request: { method: 'PUT', path: `${proj}/objects/${tagIssue}`, headers: { ...auth('mihai'), ...octet }, ...objBody('tag-issue') },
    response: { status: 201, headers: { etag: `"${tagIssue}"` }, body: null } },
  { name: 'put-commit', title: '4. The commit last (any order is valid; last is the client\'s habit).',
    request: { method: 'PUT', path: `${proj}/objects/${c2}`, headers: { ...auth('mihai'), ...octet }, ...objBody('c2') },
    response: { status: 201, headers: { etag: `"${c2}"` }, body: null } },
  { name: 'update-ok', title: '5. refs/update, compare-and-swap: main c1 -> c2 and the new issue tag, all or none.',
    request: { method: 'POST', path: `${proj}/refs/update`, headers: { ...auth('mihai'), ...json }, body: { updates: [
      { name: 'refs/heads/main', old: c1, new: c2 },
      { name: 'refs/tags/issue/1-P01', old: null, new: tagIssue },
    ] } },
    response: { status: 200, headers: { ...json, etag: '"r-8"' }, body: refsAfter },
    opaque: ['/response/headers/etag', ...refsOpaque(refsAfter)] },
]);

// The conflict: Ana (editor) committed c3 on c1, saw main at c1, and pushes after Mihai.
session('push-conflict', [
  { name: 'missing', title: 'Ana\'s closure: c3 and T3 (hello.py is on the server since Mihai\'s push).',
    request: { method: 'POST', path: `${proj}/objects/missing`, headers: { ...auth('ana'), ...json }, body: { hashes: [c3, T('T3'), B('hello.py')] } },
    response: { status: 200, headers: json, body: { missing: [c3, T('T3')] } } },
  { name: 'put-tree', title: 'T3.',
    request: { method: 'PUT', path: `${proj}/objects/${T('T3')}`, headers: { ...auth('ana'), ...octet }, ...objBody('T3') },
    response: { status: 201, headers: { etag: `"${T('T3')}"` }, body: null } },
  { name: 'put-commit', title: 'c3.',
    request: { method: 'PUT', path: `${proj}/objects/${c3}`, headers: { ...auth('ana'), ...octet }, ...objBody('c3') },
    response: { status: 201, headers: { etag: `"${c3}"` }, body: null } },
  { name: 'update-409', title: 'main is no longer c1: 409 with the current value; nothing changed. Ana pulls (S1: keeps her work as a wip branch).',
    request: { method: 'POST', path: `${proj}/refs/update`, headers: { ...auth('ana'), ...json }, body: { updates: [{ name: 'refs/heads/main', old: c1, new: c3 }] } },
    response: { status: 409, headers: json, body: err('ref_moved', 'refs/heads/main has moved: fetch and pull first.', { refs: [{ name: 'refs/heads/main', expected: c1, current: c2, updatedAt: '2026-11-23T10:51:10Z', updatedBy: { id: mihai.id, name: mihai.name } }] }) },
    opaque: ['/response/body/message', '/response/body/refs/0/updatedAt'] },
  { name: 'update-wip', title: 'Keep my work as a branch: a new ref (old null) on wip/<author>/<name>.',
    request: { method: 'POST', path: `${proj}/refs/update`, headers: { ...auth('ana'), ...json }, body: { updates: [{ name: 'refs/heads/wip/ana-popescu/2026-11-23-1046', old: null, new: c3 }] } },
    response: { status: 200, headers: { ...json, etag: '"r-9"' }, body: { refs: [
      ref('refs/heads/main', c2, { id: mihai.id, name: mihai.name }),
      ref('refs/heads/wip/ana-popescu/2026-11-23-1046', c3, { id: ana.id, name: ana.name }),
      ref('refs/tags/issue/1-P01', tagIssue, { id: mihai.id, name: mihai.name }),
    ] } },
    opaque: ['/response/headers/etag', '/response/body/refs/0/updatedAt', '/response/body/refs/1/updatedAt', '/response/body/refs/2/updatedAt'] },
]);

// Fetch: a viewer (reader) who knows c1 only, after Mihai's push (run before push-conflict, so no wip ref yet).
session('fetch', [
  { name: 'refs', title: 'The refs, with the ETag of the last fetch: changed, so 200.',
    request: { method: 'GET', path: `${proj}/refs`, headers: { ...auth('viewer'), 'if-none-match': '"r-7"' } },
    response: { status: 200, headers: { ...json, etag: '"r-8"' }, body: refsAfter },
    opaque: ['/response/headers/etag', ...refsOpaque(refsAfter)] },
  { name: 'refs-unchanged', title: 'The same again: 304, no body.',
    request: { method: 'GET', path: `${proj}/refs`, headers: { ...auth('viewer'), 'if-none-match': '"r-8"' } },
    response: { status: 304, headers: { etag: '"r-8"' }, body: null }, opaque: ['/response/headers/etag'] },
  { name: 'commits', title: 'The commits from the new head back to what the client has (stop).',
    request: { method: 'GET', path: `${proj}/commits?from=${c2}&stop=${c1}&limit=100`, headers: auth('viewer') },
    response: { status: 200, headers: json, body: { commits: [{ id: c2, commit: objects.c2.value }], more: false } } },
  { name: 'pack', title: 'The objects the client lacks, as a zip whose entries are named by id (entries listed, not the zip bytes).',
    request: { method: 'POST', path: `${proj}/objects/pack`, headers: { ...auth('viewer'), ...json }, body: { hashes: [tagIssue, T('T2'), B('walls.arrow'), B('hello.py'), B('issue.pdf')] } },
    response: { status: 200, headers: { 'content-type': 'application/zip' }, bodyZipEntries: [
      { name: tagIssue, file: objFile('tag-issue') }, { name: T('T2'), file: objFile('T2') },
      { name: B('walls.arrow'), file: blobFile('walls.arrow') }, { name: B('hello.py'), file: blobFile('hello.py') }, { name: B('issue.pdf'), file: blobFile('issue.pdf') },
    ] } },
  { name: 'object', title: 'One object, with its ETag.',
    request: { method: 'GET', path: `${proj}/objects/${B('hello.py')}`, headers: auth('viewer') },
    response: { status: 200, headers: { ...octet, etag: `"${B('hello.py')}"`, 'cache-control': 'private, no-store' }, bodyFile: blobFile('hello.py') } },
  { name: 'object-404', title: 'An object the project does not have.',
    request: { method: 'GET', path: `${proj}/objects/${B('missing.py')}`, headers: auth('viewer') },
    response: { status: 404, headers: json, body: err('object_missing', 'The object does not exist in this project.') }, opaque: ['/response/body/message'] },
]);

// The errors.
const hello = blob['hello.txt'];
session('errors', [
  { name: 'bad-hash', title: 'A PUT whose bytes do not hash to the id in the path: 400, nothing stored.',
    request: { method: 'PUT', path: `${proj}/objects/${B('hello.py')}`, headers: { ...auth('mihai'), ...octet }, bodyFile: blobFile('hello.txt') },
    response: { status: 400, headers: json, body: err('hash_mismatch', 'The bytes do not hash to the id.', { expected: B('hello.py'), actual: hello.id }) }, opaque: ['/response/body/message'] },
  { name: 'put-commit-c4', title: 'c4 and T4 are uploaded, but not scripts/missing.py.',
    request: { method: 'PUT', path: `${proj}/objects/${c4}`, headers: { ...auth('mihai'), ...octet }, ...objBody('c4') },
    response: { status: 201, headers: { etag: `"${c4}"` }, body: null } },
  { name: 'put-tree-t4', title: 'T4.',
    request: { method: 'PUT', path: `${proj}/objects/${T('T4')}`, headers: { ...auth('mihai'), ...octet }, ...objBody('T4') },
    response: { status: 201, headers: { etag: `"${T('T4')}"` }, body: null } },
  { name: 'update-422', title: 'The closure is not complete: 422 with what is missing; no ref moved.',
    request: { method: 'POST', path: `${proj}/refs/update`, headers: { ...auth('mihai'), ...json }, body: { updates: [{ name: 'refs/heads/main', old: c2, new: c4 }] } },
    response: { status: 422, headers: json, body: err('object_missing', '1 object named by the update is missing.', { missing: [{ id: B('missing.py'), kind: 'blob', from: T('T4'), path: 'scripts/missing.py', size: S('missing.py') }] }) },
    opaque: ['/response/body/message'] },
  { name: 'viewer-403', title: 'A viewer (reader) may not push.',
    request: { method: 'POST', path: `${proj}/refs/update`, headers: { ...auth('viewer'), ...json }, body: { updates: [{ name: 'refs/heads/main', old: c2, new: c4 }] } },
    response: { status: 403, headers: json, body: err('forbidden', 'Your role in this project (viewer) cannot update refs/heads/main.') }, opaque: ['/response/body/message'] },
  { name: 'no-token-401', title: 'No token: 401 with the Bearer challenge.',
    request: { method: 'GET', path: `${proj}/refs`, headers: {} },
    response: { status: 401, headers: { ...json, 'www-authenticate': 'Bearer' }, body: err('unauthorized', 'Sign in first.') }, opaque: ['/response/body/message'] },
  { name: 'not-member-404', title: 'A signed-in user who is not a member: 404, never 403.',
    request: { method: 'GET', path: `${proj}/refs`, headers: auth('stranger') },
    response: { status: 404, headers: json, body: err('not_found', 'The project does not exist, or is not yours to see.') }, opaque: ['/response/body/message'] },
  { name: 'too-large-413', title: 'A single PUT above the chunk size: 413; use /uploads.',
    request: { method: 'PUT', path: `${proj}/objects/${'0'.repeat(64)}`, headers: { ...auth('mihai'), ...octet, 'content-length': '16777217' }, bodyNote: '16777217 bytes, not included' },
    response: { status: 413, headers: json, body: err('too_large', 'An object above 16777216 bytes goes through /uploads.', { limit: 16777216 }) }, opaque: ['/response/body/message'] },
  { name: 'batch-400', title: 'objects/missing with more than maxBatch hashes: 400.',
    request: { method: 'POST', path: `${proj}/objects/missing`, headers: { ...auth('mihai'), ...json }, bodyNote: '{"hashes": [10001 ids]}' },
    response: { status: 400, headers: json, body: err('batch_too_large', 'At most 10000 hashes per request.', { limit: 10000 }) }, opaque: ['/response/body/message'] },
  { name: 'upload-start', title: 'A resumable upload of missing.py (24 bytes), never finished: the object does not exist until its last chunk.',
    request: { method: 'POST', path: `${proj}/uploads`, headers: { ...auth('mihai'), ...json }, body: { sha256: B('missing.py'), size: S('missing.py') } },
    response: { status: 201, headers: { ...json, location: `${proj}/uploads/{upload}` }, body: { id: '{upload}', offset: 0, chunk: 16777216, expiresAt: '2026-11-24T11:10:00Z' } },
    capture: { upload: '/response/body/id' }, opaque: ['/response/body/id', '/response/body/expiresAt', '/response/headers/location'] },
  { name: 'upload-chunk', title: 'Bytes 0..9.',
    request: { method: 'PATCH', path: `${proj}/uploads/{upload}`, headers: { ...auth('mihai'), 'content-type': 'application/offset+octet-stream', 'upload-offset': '0', 'content-length': '10' }, bodyFile: blobFile('missing.py'), bodyRange: { offset: 0, length: 10 } },
    response: { status: 204, headers: { 'upload-offset': '10' }, body: null } },
  { name: 'upload-offset-409', title: 'The same chunk again, at offset 0: 409 with the server\'s offset; nothing appended.',
    request: { method: 'PATCH', path: `${proj}/uploads/{upload}`, headers: { ...auth('mihai'), 'content-type': 'application/offset+octet-stream', 'upload-offset': '0', 'content-length': '10' }, bodyFile: blobFile('missing.py'), bodyRange: { offset: 0, length: 10 } },
    response: { status: 409, headers: { ...json, 'upload-offset': '10' }, body: err('offset_mismatch', 'The upload is at 10.', { offset: 10 }) }, opaque: ['/response/body/message'] },
  { name: 'issue-tag-immutable', title: 'An issue tag never moves, even for an owner: 422.',
    request: { method: 'POST', path: `${proj}/refs/update`, headers: { ...auth('mihai'), ...json }, body: { updates: [{ name: 'refs/tags/issue/1-P01', old: tagIssue, new: tagVersion }] } },
    response: { status: 422, headers: json, body: err('tag_immutable', 'An issue tag cannot be moved.', { name: 'refs/tags/issue/1-P01' }) }, opaque: ['/response/body/message'] },
]);

// ---------------------------------------------------------------- folder remote: the append-only ref log and a fork

const entry = (name, seq, old, nw, author, at) => {
  const value = { format: 'lapin-ref', v: 1, name, seq, old, new: nw, author, at };
  const key = author.id.replace(':', '-');
  return { file: `refs/heads/main/${String(seq).padStart(8, '0')}-${nw ?? '0'.repeat(64)}-${key}.ref`, content: jcs(value) };
};
const localMihai = { id: 'local:9f2b6c1d0e8a4b7c', name: 'Mihai Ionescu' };
const localAna = { id: 'local:3a7d5e9f1b2c4d6e', name: 'Ana Popescu' };
putJson('folder/fork-detection.json', {
  title: 'Two computers on a synced folder both extended seq 1 (old c1): the earlier `at` wins; the later stays reachable and its author makes it a wip branch.',
  project: P,
  entries: [
    entry('refs/heads/main', 1, null, c1, localMihai, '2026-11-23T10:00:05Z'),
    entry('refs/heads/main', 2, c1, c2, localMihai, '2026-11-23T10:51:10Z'),
    entry('refs/heads/main', 2, c1, c3, localAna, '2026-11-23T10:51:40Z'),
  ],
  expected: {
    head: c2, seq: 2,
    chain: [`refs/heads/main/00000001-${c1}-local-9f2b6c1d0e8a4b7c.ref`, `refs/heads/main/00000002-${c2}-local-9f2b6c1d0e8a4b7c.ref`],
    detached: [{ file: `refs/heads/main/00000002-${c3}-local-3a7d5e9f1b2c4d6e.ref`, new: c3, author: localAna, reason: 'fork' }],
    reachable: [c1, c2, c3],
    loserAction: 'the client whose install id is local:3a7d5e9f1b2c4d6e writes refs/heads/wip/<author>/<name> = c3 (old null) and tells its user to pull',
  },
});
putJson('folder/layout.json', {
  title: 'The files of a folder remote after the push session (objects listed, not their gzip bytes).',
  files: {
    'lapin-remote.json': jcs({ format: 'lapin-remote', v: 1 }),
    [`${P}/project.json`]: jcs({ format: 'lapin-remote-project', v: 1, id: P, name: 'Bloc P+4', defaultRef: 'refs/heads/main', createdAt: '2026-11-23T09:59:00Z' }),
  },
  objects: [B('document.json'), B('relations.arrow'), T('T1'), T('T2'), B('walls.arrow'), B('hello.py'), B('issue.pdf')].map(id => `${P}/objects/${id.slice(0, 2)}/${id}.gz`),
  commits: [c1, c2, tagIssue].map(id => `${P}/commits/${id}.json`),
  refs: [entry('refs/heads/main', 1, null, c1, localMihai, '2026-11-23T10:00:05Z').file, entry('refs/heads/main', 2, c1, c2, localMihai, '2026-11-23T10:51:10Z').file,
    `refs/tags/issue/1-P01/00000001-${tagIssue}-local-9f2b6c1d0e8a4b7c.ref`].map(f => `${P}/${f}`),
});

// ---------------------------------------------------------------- tree paths (§4.2)

// The names that are never parts of a project state (§4.2, B14), compared without case on the first segment, and the generic
// rules. `references` was added with the reference documents (docs/plan-cde-references.md, decision 878).
putJson('paths/tree-paths.json', {
  title: 'Paths a tree may name (§4.2): valid ones, and refused ones with why. The first segment is compared without case.',
  never: ['manifest.json', 'versions', 'issues', 'references', 'history', 'derived', 'journal'],
  valid: ['model/core/IfcWall/_.arrow', 'model/relations.arrow', 'project/document.json', 'scripts/hello.py', 'dag/graph.json',
    'reference/notes.txt', 'models/references.json', 'model/core/LapinReference/_.arrow'],
  refused: [
    { path: 'manifest.json', why: 'never a part' },
    { path: 'versions/index.json', why: 'never a part' },
    { path: 'issues/blobs/' + '0'.repeat(64), why: 'never a part' },
    { path: 'references/index.json', why: 'never a part' },
    { path: 'references/blobs/' + 'a'.repeat(64), why: 'never a part' },
    { path: 'References/blobs/x', why: 'never a part (without case)' },
    { path: 'history/objects/x', why: 'never a part' },
    { path: 'derived/x', why: 'never a part' },
    { path: 'journal/x', why: 'never a part' },
    { path: '', why: 'empty' },
    { path: 'a//b', why: 'an empty segment' },
    { path: '../a', why: 'a dot segment' },
    { path: 'a/./b', why: 'a dot segment' },
    { path: 'a\\b', why: 'a backslash' },
  ],
});

// ---------------------------------------------------------------- the index of ids

const ids = {
  blobs: Object.fromEntries(Object.entries(blob).map(([k, v]) => [k, v])),
  objects: Object.fromEntries(Object.entries(objects).map(([k, v]) => [k, { kind: v.kind, id: v.id, size: v.bytes.length, file: `objects/${k}.json` }])),
  project: P,
};
putJson('ids.json', ids);

// ---------------------------------------------------------------- write or check

let bad = 0;
for (const [rel, bytes] of files) {
  const path = join(here, rel);
  if (check) {
    if (!existsSync(path) || !readFileSync(path).equals(bytes)) { console.error(`differs: ${rel}`); bad++; }
  } else {
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, bytes);
  }
}
// The objects' files must hash to their ids as written (no editor may add a final newline).
for (const [k, v] of Object.entries(objects)) if (sha(files.get(`objects/${k}.json`)) !== v.id) { console.error(`id: ${k}`); bad++; }
if (bad) { console.error(`${bad} vector file(s) differ.`); process.exit(1); }
if (!check) {
  for (const [k, v] of Object.entries(blob)) console.log(`blob   ${k.padEnd(16)} ${v.id}  ${v.size}`);
  for (const [k, v] of Object.entries(objects)) console.log(`${v.kind.padEnd(6)} ${k.padEnd(16)} ${v.id}  ${v.bytes.length}`);
}
console.log(check ? `ok: ${files.size} files match.` : `wrote ${files.size} files.`);
