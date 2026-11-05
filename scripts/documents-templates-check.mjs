#!/usr/bin/env node
// Checks the built-in document templates (engine/src/Platform/Lapin.Platform/Documents/Templates/*.json)
// against the format of docs/plan-documents.md: blocks, fields, collections, inputs and table sources.
// Usage: node scripts/documents-templates-check.mjs [file.json…]
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const dir = join(root, 'engine/src/Platform/Lapin.Platform/Documents/Templates');

const FIELDS = new Set([
  'project.name', 'project.number', 'project.phase', 'project.client', 'project.clientAddress',
  'project.address', 'project.cadastral', 'project.landBook', 'project.designer', 'project.designerAddress',
  'project.chiefArchitect', 'project.author', 'project.checkedBy', 'site.crs', 'date.today',
  'cde.project', 'cde.originator', 'cde.lastIssue', 'cde.revision', 'cde.status',
  'counts.storeys', 'areas.built', 'areas.gross', 'areas.usable', 'areas.basement', 'height.max',
  'levels.text', 'counts.doors', 'counts.windows', 'counts.rooms',
]);
const PAGE_FIELDS = new Set(['page', 'pages']);

const COLLECTIONS = {
  storeys: { prefix: 'storey', fields: ['id', 'name', 'elevation', 'height', 'area', 'usable'] },
  sheets: { prefix: 'sheet', fields: ['number', 'title', 'scale', 'revision', 'status'] },
  documents: { prefix: 'doc', fields: ['id', 'name', 'revision', 'status', 'template'] },
  rooms: { prefix: 'room', fields: ['id', 'storeyId', 'storey', 'number', 'name', 'function', 'area', 'perimeter', 'height', 'floor', 'walls', 'ceiling'] },
  wallTypes: { prefix: 'wallType', fields: ['name', 'thickness', 'layers'] },
  slabTypes: { prefix: 'slabType', fields: ['name', 'thickness', 'layers'] },
  roofs: { prefix: 'roof', fields: ['name', 'shape', 'pitch', 'covering'] },
  materials: { prefix: 'material', fields: ['name', 'category'] },
  joineryTypes: { prefix: 'joinery', fields: ['mark', 'name', 'kind', 'width', 'height', 'count'] },
};

const TABLE_SOURCES = new Set(['kind:all', 'kind:door', 'kind:window', 'kind:lintel', 'kind:rooms',
  'kind:roomFinishes', 'kind:skirtings', 'kind:types', 'kind:roofTimber', 'kind:formwork', 'kind:scaffold']);

const ISO_TYPES = new Set(['RP', 'SP', 'SH']);
const PAGE_SIZES = new Set(['A4', 'A3', 'Letter']);
const ORIENTATIONS = new Set(['portrait', 'landscape']);

function check(file) {
  const errors = [];
  const err = (where, msg) => errors.push(`${where}: ${msg}`);
  let t;
  try { t = JSON.parse(readFileSync(file, 'utf8')); } catch (e) { return [`JSON: ${e.message}`]; }

  for (const k of ['id', 'title', 'description', 'lang', 'isoType', 'page', 'inputs', 'blocks'])
    if (!(k in t)) err('template', `missing "${k}"`);
  for (const k of ['id', 'title', 'description', 'lang'])
    if (k in t && (typeof t[k] !== 'string' || !t[k].trim())) err('template', `"${k}" must be a non-empty string`);
  if (t.id && t.id !== basename(file, '.json')) err('template', `id "${t.id}" differs from the file name`);
  if (t.isoType && !ISO_TYPES.has(t.isoType)) err('template', `isoType "${t.isoType}" not one of ${[...ISO_TYPES]}`);

  // {{key}} references in a text, with the repeat scopes enclosing it (prefix → collection).
  const refs = (where, text, scopes, allowPage = false) => {
    if (typeof text !== 'string') { err(where, 'text must be a string'); return; }
    const opened = (text.match(/\{\{/g) || []).length, closed = (text.match(/\}\}/g) || []).length;
    if (opened !== closed) err(where, `unbalanced braces in "${text}"`);
    for (const m of text.matchAll(/\{\{\s*([^}]*?)\s*\}\}/g)) checkKey(where, m[1], scopes, allowPage);
  };
  const checkKey = (where, key, scopes, allowPage = false) => {
    if (FIELDS.has(key)) return;
    if (PAGE_FIELDS.has(key)) { if (!allowPage) err(where, `{{${key}}} only in the header or footer`); return; }
    const dot = key.indexOf('.');
    const prefix = dot < 0 ? key : key.slice(0, dot), field = dot < 0 ? '' : key.slice(dot + 1);
    const scope = scopes.find(s => COLLECTIONS[s].prefix === prefix);
    if (!scope) { err(where, `unknown field {{${key}}}`); return; }
    if (!COLLECTIONS[scope].fields.includes(field)) err(where, `{{${key}}}: "${field}" is not a field of ${scope}`);
  };

  // page
  const p = t.page;
  if (p && typeof p === 'object') {
    if (!PAGE_SIZES.has(p.size)) err('page', `size "${p.size}"`);
    if (!ORIENTATIONS.has(p.orientation)) err('page', `orientation "${p.orientation}"`);
    if (!Array.isArray(p.margins) || p.margins.length !== 4 || !p.margins.every(n => typeof n === 'number' && n >= 0))
      err('page', 'margins must be four numbers [top, right, bottom, left] in mm');
    for (const k of ['header', 'footer']) if (k in p) refs(`page.${k}`, p[k], [], true);
  } else if (p !== undefined) err('page', 'must be an object');

  // inputs
  const inputKeys = new Set(), usedInputs = new Set();
  if (Array.isArray(t.inputs)) {
    t.inputs.forEach((inp, i) => {
      const w = `inputs[${i}]`;
      if (typeof inp.key !== 'string' || !/^[A-Za-z][A-Za-z0-9_]*$/.test(inp.key)) err(w, `bad key "${inp.key}"`);
      else if (inputKeys.has(inp.key)) err(w, `duplicate key "${inp.key}"`);
      else inputKeys.add(inp.key);
      if (typeof inp.label !== 'string' || !inp.label) err(w, 'missing label');
      if ('multiline' in inp && typeof inp.multiline !== 'boolean') err(w, 'multiline must be a boolean');
      if ('default' in inp) refs(`${w}.default`, inp.default, []);
      if ('group' in inp && typeof inp.group !== 'string') err(w, 'group must be a string');
    });
  } else if (t.inputs !== undefined) err('inputs', 'must be an array');

  // blocks
  const walk = (blocks, path, scopes) => {
    if (!Array.isArray(blocks)) { err(path, 'blocks must be an array'); return; }
    blocks.forEach((b, i) => {
      const w = `${path}[${i}]${b && b.kind ? ` (${b.kind})` : ''}`;
      if (!b || typeof b !== 'object') { err(w, 'not an object'); return; }
      switch (b.kind) {
        case 'heading':
          if (![1, 2, 3].includes(b.level)) err(w, `level ${b.level}`);
          refs(w, b.text, scopes); break;
        case 'paragraph':
          refs(w, b.text, scopes); break;
        case 'list':
          if (typeof b.ordered !== 'boolean') err(w, 'ordered must be a boolean');
          if (!Array.isArray(b.items) || !b.items.length) err(w, 'items must be a non-empty array');
          else b.items.forEach((s, j) => refs(`${w}.items[${j}]`, s, scopes));
          break;
        case 'fields':
          if (!Array.isArray(b.rows) || !b.rows.length) { err(w, 'rows must be a non-empty array'); break; }
          b.rows.forEach((r, j) => {
            if (!Array.isArray(r) || r.length !== 2) err(`${w}.rows[${j}]`, 'a row is [label, value]');
            else r.forEach(s => refs(`${w}.rows[${j}]`, s, scopes));
          });
          break;
        case 'table':
          if (typeof b.source !== 'string' || !(TABLE_SOURCES.has(b.source) || /^schedule:.+/.test(b.source)))
            err(w, `table source "${b.source}"`);
          if ('title' in b) refs(w, b.title, scopes);
          if ('columns' in b && !(Array.isArray(b.columns) && b.columns.every(c => typeof c === 'string'))) err(w, 'columns must be strings');
          if ('total' in b && typeof b.total !== 'boolean') err(w, 'total must be a boolean');
          break;
        case 'image':
          err(w, 'images need a view id; a template has none'); break;
        case 'repeat': {
          const c = COLLECTIONS[b.over];
          if (!c) { err(w, `unknown collection "${b.over}"`); break; }
          if (scopes.includes(b.over)) err(w, `repeat over "${b.over}" nested in itself`);
          if ('where' in b) {
            if (!b.where || typeof b.where !== 'object' || Array.isArray(b.where)) err(w, 'where must be an object');
            else for (const [f, v] of Object.entries(b.where)) {
              if (!c.fields.includes(f)) err(`${w}.where`, `"${f}" is not a field of ${b.over}`);
              refs(`${w}.where.${f}`, v, scopes);   // the value is read in the enclosing scope
            }
          }
          walk(b.blocks, `${w}.blocks`, [...scopes, b.over]);
          if (b.empty !== undefined) walk(b.empty, `${w}.empty`, scopes);   // shown when nothing matches, in the enclosing scope
          break;
        }
        case 'if':
          if (typeof b.field !== 'string') err(w, 'missing field');
          else checkKey(w, b.field, scopes);
          walk(b.blocks, `${w}.blocks`, scopes);
          if (b.else !== undefined) walk(b.else, `${w}.else`, scopes);
          break;
        case 'input':
          if (!inputKeys.has(b.key)) err(w, `input "${b.key}" not declared in inputs`);
          usedInputs.add(b.key); break;
        case 'toc':
        case 'pageBreak':
          break;
        default:
          err(w, `unknown block kind "${b.kind}"`);
      }
    });
  };
  walk(t.blocks, 'blocks', []);
  for (const k of inputKeys) if (!usedInputs.has(k)) err('inputs', `"${k}" declared but never used`);
  return errors;
}

const files = process.argv.length > 2
  ? process.argv.slice(2)
  : readdirSync(dir).filter(f => f.endsWith('.json')).sort().map(f => join(dir, f));
if (!files.length) { console.error(`No templates in ${dir}`); process.exit(1); }

let failed = 0;
for (const f of files) {
  const errors = check(f);
  if (errors.length) { failed++; console.log(`FAIL ${basename(f)}`); for (const e of errors) console.log(`  ${e}`); }
  else console.log(`ok   ${basename(f)}`);
}
console.log(failed ? `${failed} of ${files.length} templates failed` : `${files.length} templates ok`);
process.exit(failed ? 1 : 0);
