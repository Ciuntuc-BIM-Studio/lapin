#!/usr/bin/env node
/**
 * The command catalogue (docs/python.md, docs/mcp.md; decision 489): every engine command and query with the
 * arguments its handler reads, found in the engine's source, written as engine/src/Platform/Lapin.Protocol/catalogue.json.
 * The engine embeds it and serves it as `session.catalogue` (with each command's label and module as the running engine
 * has them), so the MCP bridge and the Python package (python/lapinbim) know the arguments in an installed app too,
 * where there is no source.
 *
 * What it reads, per handler (`r.Command("name", "Label", …)` / `r.Query("name", …)`, up to the next one or the end of
 * the registering method): every `a["key"]`, `P(a, "x", "y")`, `Number(a, "k")`, and the keys read by the helpers it hands
 * its `a` to (`Args.Ids(a)`, `WallsAlong.Add(ctx, a, …)`: any static method of the engine taking a JsonObject, two levels
 * deep). A key's type is guessed from how it is read ((string?) → str, Json.Number → float, TryGetValue<bool> → bool,
 * `as JsonArray` → list, `as JsonObject` → dict), else left out. Keys read further away are not seen; the command's error
 * then names what is missing.
 *
 * The Lapin.Protocol build runs it before compiling (Lapin.Protocol.csproj), so the embedded file follows the source.
 *
 *   node scripts/command-catalogue.mjs            write the file (only when it changed)
 *   node scripts/command-catalogue.mjs --check    fail when the file is not what the source makes
 *   node scripts/command-catalogue.mjs --print    write it to stdout
 */

import { existsSync, readFileSync, readdirSync, utimesSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
export const repoRoot = resolve(here, '..');
export const catalogueFile = join(repoRoot, 'engine/src/Platform/Lapin.Protocol/catalogue.json');

/** Every .cs file under `root` (bin and obj left out), with its text and the module whose folder holds it. */
function sources(root) {
  const out = [];
  const moduleOf = (dir) => {
    for (let d = dir; d.startsWith(root); d = dirname(d)) {
      const f = join(d, 'module.json');
      if (existsSync(f)) try { return JSON.parse(readFileSync(f, 'utf8')).id; } catch { break; }
    }
    return 'core';
  };
  const walk = (dir) => {
    for (const e of readdirSync(dir, { withFileTypes: true }).sort((x, y) => x.name.localeCompare(y.name))) {
      const f = join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== 'bin' && e.name !== 'obj') walk(f); continue; }
      if (e.name.endsWith('.cs')) out.push({ file: f, src: readFileSync(f, 'utf8'), module: moduleOf(dir) });
    }
  };
  if (existsSync(root)) walk(root);
  return out;
}

/** The body of a member starting at `at`: to the brace closing at its own indentation, or to the `;` ending an expression body. */
function bodyAt(src, at) {
  const lineStart = src.lastIndexOf('\n', at) + 1;
  const indent = /^ */.exec(src.slice(lineStart))[0];
  const arrow = src.indexOf('=>', at), brace = src.indexOf('{', at);
  if (arrow >= 0 && (brace < 0 || arrow < brace) && !src.slice(at, arrow).includes('\n' + indent + '{')) {
    const end = src.indexOf(';\n', arrow);
    return src.slice(at, end < 0 ? src.length : end);
  }
  const close = src.indexOf(`\n${indent}}`, at);
  return src.slice(at, close < 0 ? src.length : close);
}

/** A type's declaration at the start of a line (not the word in a comment). */
const TYPE_DECL = /^( *)(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file|unsafe) +)*(?:record +(?:struct|class) +|class +|struct +|record +|interface +)(\w+)/gm;

/** The type a member at `at`, indented by `indent`, belongs to: the last declared before it less indented. */
const ownerAt = (types, at, indent) => types.filter((c) => c.index < at && c[1].length < indent).at(-1)?.[2];

const ARG = (name) => new RegExp(`\\b${name}\\["(\\w+)"\\]`, 'g');

/** The type a key seems read as, from the text round one of its reads: str, float, int, bool, list, dict; null when unclear. */
function typeHint(before, after) {
  if (/\(string\??\)\s*$/.test(before)) return 'str';
  if (/\((double|float)\??\)\s*$/.test(before) || /Number\(\s*$/.test(before)) return 'float';
  if (/\((int|long)\??\)\s*$/.test(before)) return 'int';
  if (/\(bool\??\)\s*$/.test(before)) return 'bool';
  if (/(?:Try)?Point3?\(\s*$|StoredPoints3?\(\s*$/.test(before)) return 'list';
  const a = after.slice(0, 80);
  if (/^\s*(?:as|is(?: not)?) JsonArray/.test(a)) return 'list';
  if (/^\s*(?:as|is(?: not)?) JsonObject/.test(a)) return 'dict';
  if (/^[^;\n]{0,40}GetValue<bool>/.test(a)) return 'bool';
  if (/^[^;\n]{0,40}GetValue<(?:double|float)>/.test(a)) return 'float';
  if (/^[^;\n]{0,40}GetValue<(?:int|long)>/.test(a)) return 'int';
  if (/^[^;\n]{0,40}GetValue<string>/.test(a)) return 'str';
  return null;
}

/** The keys `body` reads from its JsonObject named `v`, each with the types it seems read as. */
function keysRead(body, v) {
  const keys = new Map();
  const add = (k, t) => { if (!keys.has(k)) keys.set(k, new Set()); if (t) keys.get(k).add(t); };
  for (const m of body.matchAll(ARG(v))) add(m[1], typeHint(body.slice(Math.max(0, m.index - 30), m.index), body.slice(m.index + m[0].length)));
  // P(a, "x", "y") and Args.Point(a, "x", "y"): two numbers; Number(a, "k"): one.
  for (const m of body.matchAll(new RegExp(`\\(${v}, "(\\w+)", "(\\w+)"`, 'g'))) { add(m[1], 'float'); add(m[2], 'float'); }
  for (const m of body.matchAll(new RegExp(`Number\\(${v}, "(\\w+)"\\)`, 'g'))) add(m[1], 'float');
  for (const m of body.matchAll(new RegExp(`\\(${v}, "(\\w+)"\\)`, 'g'))) add(m[1], null);
  return keys;
}

/** Every static method taking a JsonObject: "Class.Method" → [{param, body}]. */
function helperIndex(files) {
  const index = new Map();
  const decl = /^( +)(?:(?:public|internal|private|protected|static|readonly|unsafe|override|async|sealed|new)\s+)*static\s+[\w<>[\]?,.() ]+?\s(\w+)\(([^)]*\bJsonObject\??\s+(\w+)[^)]*)\)/gm;
  for (const { src } of files) {
    const classes = [...src.matchAll(TYPE_DECL)];
    for (const m of src.matchAll(decl)) {
      const owner = ownerAt(classes, m.index, m[1].length);
      if (!owner) continue;
      const key = `${owner}.${m[2]}`;
      if (!index.has(key)) index.set(key, []);
      index.get(key).push({ param: m[4], body: bodyAt(src, m.index), owner });
    }
  }
  return index;
}

/** The keys a handler body reads through `v` and through the helpers it hands `v` to, `depth` levels deep. */
function keysWithHelpers(body, v, owner, index, depth = 2, seen = new Set()) {
  const keys = keysRead(body, v);
  if (depth <= 0) return keys;
  const merge = (more) => { for (const [k, t] of more) { if (!keys.has(k)) keys.set(k, new Set()); for (const x of t) keys.get(k).add(x); } };
  // Qualified (Args.Ids(a), WallsAlong.Add(ctx, a, …)) and unqualified calls to a helper of the same class.
  for (const m of body.matchAll(new RegExp(`(?:\\b(\\w+)\\.)?\\b(\\w+)\\((?:\\w+, )?${v}\\b\\s*[,)]`, 'g'))) {
    const name = `${m[1] ?? owner}.${m[2]}`;
    if (seen.has(name) || !index.has(name)) continue;
    seen.add(name);
    for (const h of index.get(name)) merge(keysWithHelpers(h.body, h.param, h.owner, index, depth - 1, seen));
  }
  return keys;
}

const typeOf = (set) => (set.size === 1 ? [...set][0] : null);

/** The names a loop just before `at` gives its variable `v`: the first string of each tuple (or each string) of its `new[] { … }`. */
function loopNames(src, at, v) {
  const before = src.slice(Math.max(0, at - 400), at);
  const loop = [...before.matchAll(new RegExp(`foreach \\(var (?:\\(([\\w, ]+)\\)|(\\w+)) in new(?:\\[\\])? ?\\{([^}]*)\\}\\)`, 'g'))].at(-1);
  if (!loop) return [];
  const vars = (loop[1] ?? loop[2]).split(',').map((x) => x.trim());
  if (vars[0] !== v) return [];
  return [...loop[3].matchAll(loop[1] ? /\(\s*"([\w.-]+)"/g : /"([\w.-]+)"/g)].map((x) => x[1]);
}

/**
 * The catalogue the source makes: {format, commands: [{name, module, label?, args: [{name, type?}]}], queries: [...]},
 * each list sorted by name, a command's arguments in the order its handler reads them.
 */
export function catalogueFromSource(root = join(repoRoot, 'engine/src')) {
  const files = sources(root);
  const index = helperIndex(files);
  const commands = new Map(), queries = new Map();
  for (const { src, module } of files) {
    // A name given as a literal, or as the variable of a loop over literals just above
    // (`foreach (var (command, cls) in new[] { ("updateScaffoldType", …), … }) r.Command(command, "Change type", …)`).
    const marks = [...src.matchAll(/\.(Command|Query)\((?:"([\w.-]+)"|([a-z]\w*)),\s*/g)].filter((m) => m[2] || loopNames(src, m.index, m[3]).length);
    if (!marks.length) continue;
    const classes = [...src.matchAll(TYPE_DECL)];
    marks.forEach((m, i) => {
      let body = src.slice(m.index, marks[i + 1]?.index ?? src.length);
      const end = body.search(/\n {4}\}\n/); // the registering method's end
      if (end > 0) body = body.slice(0, end);
      // The handler's JsonObject: (ctx, a) => … or a method group; most handlers name it a.
      const param = /\(\s*\w+\s*,\s*(\w+)\s*\)\s*=>/.exec(body)?.[1] ?? 'a';
      const owner = ownerAt(classes, m.index, /^ */.exec(src.slice(src.lastIndexOf('\n', m.index) + 1))[0].length) ?? '';
      const keys = keysWithHelpers(body, param, owner, index);
      // A method group (`r.Command("x", "X", Run)`): the method's own keys.
      const group = m[1] === 'Command' ? /^"(?:[^"\\]|\\.)*",\s*([\w.]+)\s*\)/.exec(body.slice(m[0].length)) : /^([\w.]+)\s*\)/.exec(body.slice(m[0].length));
      if (group && !keys.size) {
        const [cls, meth] = group[1].includes('.') ? group[1].split('.').slice(-2) : [owner, group[1]];
        for (const h of index.get(`${cls}.${meth}`) ?? []) for (const [k, t] of keysWithHelpers(h.body, h.param, h.owner, index)) keys.set(k, t);
      }
      const names = (m[2] ? [m[2]] : loopNames(src, m.index, m[3])).map((n) => (n.includes('.') ? n : `${module}.${n}`));
      const label = m[1] === 'Command' ? /^"((?:[^"\\]|\\.)*)"/.exec(body.slice(m[0].length))?.[1] : undefined;
      for (const name of names) {
        const entry = { name, module, ...(label !== undefined ? { label: JSON.parse(`"${label}"`) } : {}), args: [...keys].map(([k, t]) => (typeOf(t) ? { name: k, type: typeOf(t) } : { name: k })) };
        (m[1] === 'Command' ? commands : queries).set(name, entry);
      }
    });
  }
  const sorted = (map) => [...map.values()].sort((x, y) => (x.name < y.name ? -1 : x.name > y.name ? 1 : 0));
  return { format: 1, commands: sorted(commands), queries: sorted(queries) };
}

/** The catalogue as the file holds it: one entry a line, so a change shows as the lines it changes. */
export function catalogueText(c) {
  const list = (xs) => xs.map((x) => `    ${JSON.stringify(x)}`).join(',\n');
  return `{\n  "format": ${c.format},\n  "commands": [\n${list(c.commands)}\n  ],\n  "queries": [\n${list(c.queries)}\n  ]\n}\n`;
}

/** The catalogue as written: from the repo's file when it is there, else made from the source; null when neither is. */
export function readCatalogue() {
  if (existsSync(catalogueFile)) try { return JSON.parse(readFileSync(catalogueFile, 'utf8')); } catch { /* made below */ }
  const c = catalogueFromSource();
  return c.commands.length ? c : null;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const text = catalogueText(catalogueFromSource());
  const now = existsSync(catalogueFile) ? readFileSync(catalogueFile, 'utf8') : null;
  if (process.argv.includes('--print')) process.stdout.write(text);
  else if (process.argv.includes('--check')) {
    if (now !== text) { console.error(`${catalogueFile} is not what the engine's source makes: run node scripts/command-catalogue.mjs`); process.exit(1); }
    console.log('command catalogue up to date');
  } else if (now !== text) {
    writeFileSync(catalogueFile, text);
    const c = JSON.parse(text);
    console.log(`command catalogue: ${c.commands.length} commands, ${c.queries.length} queries → ${catalogueFile}`);
  } else {
    // Unchanged: its time moved on, so the build's incremental check does not run this again for nothing.
    const t = new Date();
    utimesSync(catalogueFile, t, t);
  }
}
