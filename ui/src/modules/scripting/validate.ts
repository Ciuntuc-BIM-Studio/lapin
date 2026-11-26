/**
 * Static pre-flight validator for generated `lapinbim` Python code (docs/plan-python-notebook.md):
 * checks every `lapin.<module>.<fn>(kw=...)` call against `session.catalogue` before the user
 * runs the cell, catching hallucinated command names and wrong keyword arguments so Ollama can
 * fix them in a single deterministic auto-repair turn.
 */

import { ident, snake, type Catalogue } from './logic';
import { formatPythonSignature } from './contextPack';

export interface ValidationIssue {
  line: number;
  call: string;
  kind: 'unknown_command' | 'unknown_arg';
  message: string;
  suggestion?: string;
}

/** Built-in top-level `lapin.*` attributes/namespaces that are not engine command namespaces. */
const BUILTIN_NS = new Set([
  'model', 'ui', 'execute', 'query', 'undo', 'redo', 'save', 'info', 'batch', 'step',
  'selection', 'selected', 'on', 'quantities', 'snapshot', 'state', 'catalogue', 'call',
  'create_body', 'set_body', 'global_id', 'measure_value', 'client', 'close',
]);

interface CommandEntry {
  pyKey: string;
  engineName: string;
  args: Set<string>;
  signature: string;
}

function buildIndex(catalogue: Catalogue): Map<string, CommandEntry> {
  const map = new Map<string, CommandEntry>();
  const add = (name: string, args?: { name: string; type?: string }[]) => {
    const [ns, ...rest] = name.split('.');
    if (!rest.length) return;
    const pyKey = `${ident(snake(ns))}.${ident(snake(rest.join('_')))}`;
    const argSet = new Set<string>();
    for (const a of args ?? []) {
      argSet.add(ident(snake(a.name)));
      argSet.add(a.name); // allow camelCase too if engine accepts it, or flag? Better: check snake_case strictly below
    }
    const snakeArgs = new Set((args ?? []).map((a) => ident(snake(a.name))));
    map.set(pyKey, { pyKey, engineName: name, args: snakeArgs, signature: formatPythonSignature(name, args) });
  };
  for (const c of catalogue.commands) add(c.name, c.args);
  for (const q of catalogue.queries) add(q.name, q.args);
  return map;
}

/** Simple similarity score (shared character bigrams) to suggest the closest real command or kwarg. */
function similarity(a: string, b: string): number {
  const x = a.toLowerCase();
  const y = b.toLowerCase();
  if (x === y) return 1;
  if (x.length < 2 || y.length < 2) return 0;
  const bigrams = new Map<string, number>();
  for (let i = 0; i < x.length - 1; i++) {
    const g = x.slice(i, i + 2);
    bigrams.set(g, (bigrams.get(g) ?? 0) + 1);
  }
  let overlap = 0;
  for (let i = 0; i < y.length - 1; i++) {
    const g = y.slice(i, i + 2);
    const n = bigrams.get(g) ?? 0;
    if (n > 0) { overlap++; bigrams.set(g, n - 1); }
  }
  return (2 * overlap) / (x.length + y.length - 2);
}

/** Extract top-level keyword argument names from a parenthesized argument list. */
function extractKwargs(argText: string): string[] {
  const keys: string[] = [];
  let depth = 0;
  let quote: '"' | "'" | null = null;
  let cur = '';
  const flush = () => {
    const trimmed = cur.trim();
    const m = /^([A-Za-z_]\w*)\s*=(?!=)/.exec(trimmed);
    if (m) keys.push(m[1]);
    cur = '';
  };
  for (let i = 0; i < argText.length; i++) {
    const ch = argText[i];
    if (quote) {
      cur += ch;
      if (ch === '\\' && i + 1 < argText.length) { cur += argText[++i]; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; cur += ch; continue; }
    if (ch === '(' || ch === '[' || ch === '{') { depth++; cur += ch; continue; }
    if (ch === ')' || ch === ']' || ch === '}') { depth = Math.max(0, depth - 1); cur += ch; continue; }
    if (ch === ',' && depth === 0) { flush(); continue; }
    cur += ch;
  }
  flush();
  return keys;
}

/** Find balanced closing `)` starting right after `(` at `openIdx`. */
function findCloseParen(text: string, openIdx: number): number {
  let depth = 1;
  let quote: '"' | "'" | null = null;
  for (let i = openIdx + 1; i < text.length; i++) {
    const ch = text[i];
    if (quote) {
      if (ch === '\\') { i++; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; continue; }
    if (ch === '(') depth++;
    else if (ch === ')') {
      depth--;
      if (depth === 0) return i;
    }
  }
  return -1;
}

/**
 * Validate all `lapin.<ns>.<fn>(...)` calls in `code` against `catalogue`.
 * Returns an empty array when every call and keyword argument matches the catalogue.
 */
export function validateLapinCode(code: string, catalogue: Catalogue | null): ValidationIssue[] {
  if (!catalogue || !code.trim()) return [];
  const index = buildIndex(catalogue);
  const allEntries = [...index.values()];
  const issues: ValidationIssue[] = [];

  // Strip full-line comments while preserving line numbers.
  const lines = code.replace(/\r\n/g, '\n').split('\n');
  const cleanText = lines.map((l) => (/^\s*#/.test(l) ? '' : l)).join('\n');

  const callRe = /\blapin\.([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s*\(/g;
  let m: RegExpExecArray | null;
  while ((m = callRe.exec(cleanText))) {
    const [full, ns, fn] = m;
    if (BUILTIN_NS.has(ns)) continue;
    const openIdx = m.index + full.length - 1;
    const closeIdx = findCloseParen(cleanText, openIdx);
    const line = cleanText.slice(0, m.index).split('\n').length;
    const pyKey = `${ns}.${fn}`;
    const entry = index.get(pyKey);

    if (!entry) {
      const closest = allEntries
        .map((e) => ({ e, s: similarity(pyKey, e.pyKey) }))
        .sort((a, b) => b.s - a.s)
        .slice(0, 3)
        .filter((x) => x.s >= 0.35)
        .map((x) => x.e.signature);
      issues.push({
        line,
        call: `lapin.${pyKey}`,
        kind: 'unknown_command',
        message: `Line ${line}: \`lapin.${pyKey}\` does not exist in Lapin's catalogue.`,
        ...(closest.length ? { suggestion: `Did you mean: ${closest.join(' | ')}` } : {}),
      });
      continue;
    }

    if (closeIdx > openIdx && entry.args.size > 0) {
      const argText = cleanText.slice(openIdx + 1, closeIdx);
      const kwargs = extractKwargs(argText);
      for (const kw of kwargs) {
        if (!entry.args.has(kw)) {
          const bestArg = [...entry.args]
            .map((a) => ({ a, s: similarity(kw, a) }))
            .sort((a, b) => b.s - a.s)[0];
          issues.push({
            line,
            call: `lapin.${pyKey}`,
            kind: 'unknown_arg',
            message: `Line ${line}: \`lapin.${pyKey}\` has no argument \`${kw}\`.`,
            suggestion: `${bestArg && bestArg.s >= 0.4 ? `Use \`${bestArg.a}\`. ` : ''}Valid signature: ${entry.signature}`,
          });
        }
      }
    }
  }

  return issues;
}

/** Format validation issues into a tight, deterministic auto-repair instruction for Ollama. */
export function formatRepairPrompt(code: string, issues: readonly ValidationIssue[]): string {
  const list = issues
    .slice(0, 8)
    .map((i) => `- ${i.message}${i.suggestion ? ` ${i.suggestion}` : ''}`)
    .join('\n');
  return `The Python code below has invalid Lapin API calls:\n${list}\n\nCode:\n\`\`\`python\n${code}\n\`\`\`\n\nFix ONLY these issues and output the corrected Python code.`;
}
