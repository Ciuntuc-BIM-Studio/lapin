#!/usr/bin/env node
// Every engine command and query the interface calls by a literal name must exist in the engine as
// that kind (a command run with execute, a query asked with query): a call to a name that is missing,
// or of the other kind, answers "Unknown command" in the desktop and in the browser alike (decision 434).
// Also run by scripts/check-web.mjs, and by the UI's tests (ui/src/engine/commandNames.test.ts).
//   node scripts/check-commands.mjs [id]  (exit 1 listing the calls that cannot be answered)
import { readdirSync, readFileSync, statSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

function walk(dir, out = [], skip = /(^|\/)(bin|obj|node_modules|artifacts|\.git)$/) {
  if (!existsSync(dir)) return out;
  for (const n of readdirSync(dir)) {
    const p = join(dir, n);
    if (skip.test(p)) continue;
    const s = statSync(p);
    if (s.isDirectory()) walk(p, out, skip); else out.push(p);
  }
  return out;
}

/** Engine side: module id → { commands, queries }, from the module folders' sources. */
export function engineNames() {
  const mods = new Map();
  const add = (id, kind, name) => { if (!mods.has(id)) mods.set(id, { command: new Set(), query: new Set() }); mods.get(id)[kind].add(name); };
  const folders = [];
  for (const f of walk(join(root, 'engine/src'))) if (f.endsWith('module.json')) {
    const id = JSON.parse(readFileSync(f, 'utf8')).id;
    folders.push([dirname(f), id]);
  }
  folders.push([join(root, 'engine/src/Platform/Lapin.Platform'), 'core']);
  for (const [dir, id] of folders) {
    for (const f of walk(dir)) {
      if (!f.endsWith('.cs')) continue;
      const src = readFileSync(f, 'utf8');
      // Commands registered in a loop over a table of names: any quoted name in a tuple counts as a command of the kind it is called as.
      for (const m of src.matchAll(/\(\s*"([a-z][A-Za-z0-9]+)"\s*,\s*[A-Z]\w*\.\w+\s*\)/g)) add(id, 'command', m[1]);
      for (const m of src.matchAll(/\b[A-Za-z_]\w*\.(Command|Query|Override|OverrideQuery)\(\s*"([^"]+)"/g)) {
        const kind = m[1] === 'Command' || m[1] === 'Override' ? 'command' : 'query';
        add(id, kind, m[2].startsWith(id + '.') ? m[2].slice(id.length + 1) : m[2]);
      }
    }
  }
  return mods;
}

/** Interface side: the literal names called, with where. */
export function uiCalls() {
  const calls = [];
  for (const f of walk(join(root, 'ui/src'))) {
    if (!/\.(ts|tsx)$/.test(f) || /\.test\./.test(f)) continue;
    const src = readFileSync(f, 'utf8');
    for (const m of src.matchAll(/\b(execute|query)(?:<[^>()]*>)?\(\s*['"`]([a-z][A-Za-z0-9-]*)\.([A-Za-z0-9_]+)['"`]/g)) {
      calls.push({ kind: m[1] === 'execute' ? 'command' : 'query', mod: m[2], name: m[3], file: f.slice(root.length + 1), line: src.slice(0, m.index).split('\n').length });
    }
  }
  return calls;
}

export function unanswered() {
  const mods = engineNames();
  const bad = [];
  for (const c of uiCalls()) {
    const m = mods.get(c.mod);
    if (!m) continue; // an edition's module not in this repo (private siblings)
    if (m[c.kind].has(c.name)) continue;
    const other = c.kind === 'command' ? 'query' : 'command';
    bad.push({ ...c, why: m[other].has(c.name) ? `is a ${other}, called as a ${c.kind}` : `no such ${c.kind}` });
  }
  return bad;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const bad = unanswered();
  for (const b of bad) console.error(`${b.file}:${b.line}  ${b.mod}.${b.name}: ${b.why}`);
  console.log(bad.length ? `check-commands: ${bad.length} call(s) the engine cannot answer.` : `check-commands: every command and query the interface calls exists (${uiCalls().length} calls).`);
  process.exit(bad.length ? 1 : 0);
}
