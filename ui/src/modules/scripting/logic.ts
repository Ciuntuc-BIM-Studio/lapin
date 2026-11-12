/**
 * The Script panel's logic, kept apart from what draws it (docs/plan-python-scripting.md, stage 2; decision 489):
 * the engine's script.output events folded into the console's lines, the names of the user's scripts, the console's
 * history, and what the editor completes after `lapin.` from the command catalogue (session.catalogue), named as the
 * lapinbim package names them (python/lapinbim/_names.py: `architecture.addWall` is `lapin.architecture.add_wall`).
 */

/** One event of the engine's kernel (engine/src/Host/Lapin.Host/ScriptHost.cs, python/lapinbim/kernel.py). */
export interface ScriptEvent {
  type: 'start' | 'out' | 'err' | 'done' | 'exit' | 'ready' | string;
  id?: number;
  text?: string;
  name?: string;
  console?: boolean;
  ok?: boolean;
  interrupted?: boolean;
  killed?: boolean;
  kept?: boolean;
  error?: string | null;
  line?: number | null;
  traceback?: string | null;
  seconds?: number;
  code?: number;
}

/**
 * A line of the console: what was typed, what the script printed, its errors, and how each run ended. `own`: Lapin's
 * own words (how a run ended, the engine's refusals), translated as the interface is; the rest is the script's, shown
 * as it is.
 */
export interface ConsoleLine { kind: 'in' | 'out' | 'err' | 'info' | 'ok'; text: string; run?: number; own?: boolean }

/** A line of Lapin's own. */
export const own = (kind: ConsoleLine['kind'], text: string, run?: number): ConsoleLine => ({ kind, text, own: true, ...(run !== undefined ? { run } : {}) });

/** The most lines the console keeps (the oldest go). */
export const MAX_LINES = 2000;

/** What a failed run adds after its error: nothing it did is kept (but a killed Python, whose message says it already). */
export const NOTHING_KEPT = 'Nothing the script did is kept.';

/** The console after one event of the kernel. */
export function foldOutput(lines: readonly ConsoleLine[], e: ScriptEvent): ConsoleLine[] {
  const add = (...ls: ConsoleLine[]): ConsoleLine[] => {
    let next = [...lines];
    for (const l of ls) {
      const last = next[next.length - 1];
      // A print comes in pieces ("a", "\n"): joined into the line they make.
      if ((l.kind === 'out' || l.kind === 'err') && !l.own && last && !last.own && last.kind === l.kind && last.run === l.run && !last.text.endsWith('\n'))
        next = [...next.slice(0, -1), { ...last, text: last.text + l.text }];
      else next.push(l);
    }
    return next.length > MAX_LINES ? next.slice(next.length - MAX_LINES) : next;
  };
  switch (e.type) {
    case 'out': return e.text ? add({ kind: 'out', text: e.text, run: e.id }) : [...lines];
    case 'err': return e.text ? add({ kind: 'err', text: e.text, run: e.id }) : [...lines];
    case 'done': {
      if (e.ok) return e.console ? [...lines] : add(own('ok', doneText(e), e.id));
      // A traceback is the script's; a message without one (Stopped., the engine's refusal, a Python that ended) is Lapin's.
      const trace = e.traceback?.trim();
      const first: ConsoleLine = trace ? { kind: 'err', text: trace, run: e.id } : own('err', e.error || 'The script failed.', e.id);
      return e.killed ? add(first) : add(first, own('err', NOTHING_KEPT, e.id));
    }
    case 'exit': return add(own('info', 'Python ended; the next Run starts it again.'));
    default: return [...lines];
  }
}

/** How a run that ended well is told: kept as one undo step, or changing nothing. */
export function doneText(e: ScriptEvent): string {
  const time = typeof e.seconds === 'number' ? ` in ${e.seconds < 10 ? e.seconds.toFixed(2) : Math.round(e.seconds)} s` : '';
  return e.kept === false ? `Done${time}.` : `Done${time}: one undo step.`;
}


/** The undo step's name for a script: its file name without .py, or the console's. */
export const stepName = (file: string | null, console: boolean): string =>
  console ? 'Python console' : (file ?? 'Script').replace(/\.py$/i, '') || 'Script';

/** A name the engine keeps a script under (ScriptHost.Name): a file name ending .py, no folder, not hidden. */
export function validScriptName(name: string): boolean {
  // Control characters are what a file name cannot hold.
  // eslint-disable-next-line no-control-regex
  return /^[^/\\:*?"<>|\x00-\x1f.][^/\\:*?"<>|\x00-\x1f]{0,118}\.py$/.test(name.trim());
}

/** What the user typed as a name made into one: .py added, the characters a file name cannot have taken out. */
export function scriptName(typed: string): string | null {
  // eslint-disable-next-line no-control-regex
  const base = typed.trim().replace(/\.py$/i, '').replace(/[/\\:*?"<>|\x00-\x1f]/g, ' ').replace(/^\.+/, '').replace(/\s+/g, ' ').trim();
  if (!base) return null;
  const name = `${base.slice(0, 116)}.py`;
  return validScriptName(name) ? name : null;
}

/** The console's history: the lines run before, newest last; Up and Down walk it. */
export interface History { items: string[]; at: number | null }

export const remember = (h: History, line: string): History =>
  !line.trim() ? h : { items: [...h.items.filter((x) => x !== line), line].slice(-200), at: null };

/** One step back (-1, Up) or forward (+1, Down) through the history: the line to show, the history after. */
export function walk(h: History, dir: -1 | 1, draft: string): { text: string; history: History } {
  if (!h.items.length) return { text: draft, history: h };
  const at = h.at ?? h.items.length;
  const next = Math.max(0, Math.min(h.items.length, at + dir));
  return { text: next === h.items.length ? '' : h.items[next], history: { ...h, at: next === h.items.length ? null : next } };
}

// ------------------------------------------------------------ completion from the catalogue

/** session.catalogue (engine/src/Platform/Lapin.Protocol/CommandCatalogue.cs). */
export interface Catalogue {
  commands: { name: string; label?: string; module?: string; args?: { name: string; type?: string }[] }[];
  queries: { name: string; module?: string; args?: { name: string; type?: string }[] }[];
}

const KEYWORDS = new Set(['False', 'None', 'True', 'and', 'as', 'assert', 'async', 'await', 'break', 'class', 'continue', 'def', 'del', 'elif', 'else', 'except',
  'finally', 'for', 'from', 'global', 'if', 'import', 'in', 'is', 'lambda', 'nonlocal', 'not', 'or', 'pass', 'raise', 'return', 'try', 'while', 'with', 'yield']);

/** `addWall` → `add_wall`, `exportIFC` → `export_ifc`, `cutOrder` → `cut_order`; dots and dashes become `_` (lapinbim's snake). */
export const snake = (name: string): string =>
  name.replace(/(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])/g, '_').replace(/[.-]/g, '_').toLowerCase();

/** A Python identifier for a name: a keyword gets `_`, a leading digit `_` before it (lapinbim's ident). */
export const ident = (name: string): string => (KEYWORDS.has(name) ? `${name}_` : /^\d/.test(name) ? `_${name}` : name);

/** One completion: what is put in, what kind it is, and what it says beside it. */
export interface Completion { label: string; type: 'namespace' | 'function' | 'variable'; detail?: string; info?: string; apply?: string }

/** What `lapin.` offers itself, beside the namespaces (python/lapinbim/__init__.py). */
const TOP: [string, string][] = [
  ['execute', 'execute(command, args): a command by its engine name'], ['query', 'query(name, args): a question that changes nothing'],
  ['undo', 'undo(): one step back'], ['redo', 'redo(): one step forward'], ['save', 'save(path=None)'], ['info', 'info(): the product, modules, commands'],
  ['batch', 'with lapin.batch(label): one undo step for many'], ['step', 'with lapin.step(label): one undo step, each command answered at once'],
  ['selection', 'selection(): what the window has selected'], ['model','the model: lapin.model.walls, lapin.model[id]'], ['on', 'on("diff", f): every change'],
  ['quantities', 'quantities(id)'], ['snapshot', 'snapshot()'], ['state', 'state(): undo label, dirty, path'], ['catalogue', 'catalogue()'], ['call', 'call(method, params)'],
];

/** The namespaces of the catalogue and the calls in each, as Python names them. */
export function namespaces(c: Catalogue): Map<string, Completion[]> {
  const o = new Map<string, Completion[]>();
  const add = (ns: string, item: Completion) => { const list = o.get(ns) ?? []; if (!list.some((x) => x.label === item.label)) list.push(item); o.set(ns, list); };
  for (const cmd of c.commands) {
    const [prefix, ...rest] = cmd.name.split('.');
    if (!rest.length) continue;
    const args = (cmd.args ?? []).map((a) => ident(snake(a.name)));
    add(ident(snake(prefix)), { label: ident(snake(rest.join('.'))), type: 'function', detail: `(${args.map((a) => `${a}=…`).join(', ')})`, info: `${cmd.label ?? cmd.name} · ${cmd.name}: one undo step`, apply: `${ident(snake(rest.join('.')))}(` });
  }
  for (const q of c.queries) {
    const [prefix, ...rest] = q.name.split('.');
    if (!rest.length) continue;
    const ns = ident(snake(prefix));
    let name = ident(snake(rest.join('.')));
    if (c.commands.some((x) => x.name === q.name)) name += '_query';
    const args = (q.args ?? []).map((a) => ident(snake(a.name)));
    add(ns, { label: name, type: 'function', detail: `(${args.map((a) => `${a}=…`).join(', ')})`, info: `${q.name}: a query, changes nothing`, apply: `${name}(` });
  }
  for (const list of o.values()) list.sort((a, b) => a.label.localeCompare(b.label));
  return o;
}

/**
 * What to complete before the cursor: `lapin.` and a part of a name → the namespaces and the package's own calls;
 * `lapin.architecture.` and a part → that namespace's calls. Null when the text before the cursor is not one of those.
 * `from` is where the part being typed begins (to be replaced).
 */
export function complete(spaces: Map<string, Completion[]>, before: string): { from: number; options: Completion[] } | null {
  const m = /(?:^|[^\w.])lapin\.(?:(\w+)\.)?(\w*)$/.exec(before);
  if (!m) return null;
  const [, ns, part] = m;
  const from = before.length - part.length;
  const starts = (x: Completion) => x.label.startsWith(part);
  if (ns) {
    const list = spaces.get(ns);
    return list ? { from, options: list.filter(starts) } : null;
  }
  const top: Completion[] = [
    ...TOP.map(([label, info]): Completion => ({ label, type: label === 'model' ? 'variable' : 'function', info })),
    ...[...spaces.keys()].sort().map((label): Completion => ({ label, type: 'namespace', info: `${spaces.get(label)!.length} commands and queries` })),
  ];
  return { from, options: top.filter(starts) };
}
