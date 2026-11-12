/**
 * The macro recorder (docs/plan-python-scripting.md §5, stage 3; decision 489): each command the interface sends
 * (EngineClient's calls, caught where every tool's edit goes, the window's own) becomes one line of Python from the
 * command catalogue, as the lapinbim package names it: `architecture.addWall` → `lapin.architecture.add_wall(…)`.
 * An `edit.batch` is a `with lapin.batch("…"):` block. An Undo takes the last step recorded back out of the script and a
 * Redo puts it back, so the script holds what the window did in the end (and runs as one undo step in the panel, where
 * a script cannot undo); an Undo of what came before the recording is `lapin.undo()`. Queries are not recorded, nor
 * what a script does (its own connection's, never the window's), nor the panel's own commands (scripting.*).
 *
 * So a copied script works on a new project, what a command made is bound to a variable (`wall1 = …`, then `wall1.id`),
 * the storey it drew on is found by its name and elevation (`lapin.model.storey("Ground floor", 0.0)`), a type or a
 * resource by its class and name (`lapin.model.find("IfcWallType", "Brick 250")`); another element of the project is
 * `lapin.model["<id>"]`; the window's selection, as an argument, may be `lapin.selection()`.
 */

/** One call the window made, as the recorder hears it: the protocol method, a command's name and arguments, its result. */
export interface RecordedCall {
  method: string;
  params: Record<string, unknown>;
  result?: unknown;
}

/** What the recorder may ask of the model while it writes a line: the entity an id names, and the window's selection. */
export interface RecorderContext {
  node(id: string): { type: string; name: string; props: Record<string, unknown>; category?: string } | undefined;
  /** The commands the catalogue knows (so lapin.<ns>.<name> exists); null: every command is written so. */
  known: ((command: string) => boolean) | null;
  selection: readonly string[];
  /** The selection as an argument written `lapin.selection()` (else each element by its id). */
  useSelection: boolean;
}

const KEYWORDS = new Set(['False', 'None', 'True', 'and', 'as', 'assert', 'async', 'await', 'break', 'class', 'continue', 'def', 'del', 'elif', 'else', 'except',
  'finally', 'for', 'from', 'global', 'if', 'import', 'in', 'is', 'lambda', 'nonlocal', 'not', 'or', 'pass', 'raise', 'return', 'try', 'while', 'with', 'yield']);

/** The lapinbim module's own names: a namespace called so is reached as `lapin.ns("…")` (python/lapinbim/__init__.py). */
const RESERVED = new Set(['model', 'batch', 'step', 'selection', 'execute', 'query', 'call', 'undo', 'redo', 'save', 'info', 'quantities', 'snapshot', 'state', 'on',
  'catalogue', 'ns', 'command', 'connect', 'client', 'close', 'kernel', 'Lapin', 'LapinError', 'Batch', 'Deferred', 'Step', 'Element', 'Model', 'Obj']);

/** `addWall` → `add_wall` (lapinbim's snake: python/lapinbim/_names.py). */
export const snake = (name: string): string =>
  name.replace(/(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])/g, '_').replace(/[.-]/g, '_').toLowerCase();

/** `storey_id` → `storeyId`; a trailing `_` (`from_`) dropped (lapinbim's camel). */
export function camel(name: string): string {
  const n = name.endsWith('_') && !name.startsWith('_') ? name.replace(/_+$/, '') : name;
  const [head, ...rest] = n.split('_');
  return head + rest.map((p) => p.slice(0, 1).toUpperCase() + p.slice(1)).join('');
}

export const ident = (name: string): string => (KEYWORDS.has(name) ? `${name}_` : /^\d/.test(name) ? `_${name}` : name);

const isIdent = (s: string) => /^[A-Za-z_][A-Za-z0-9_]*$/.test(s);

/** A Python string literal (JSON's escapes are Python's). */
const str = (s: string) => JSON.stringify(s);

/** A number as Python writes it back: the shortest that reads the same; not-a-number and the infinities by name. */
function num(n: number): string {
  if (Number.isNaN(n)) return 'float("nan")';
  if (!Number.isFinite(n)) return n > 0 ? 'float("inf")' : '-float("inf")';
  // A point clicked in a view is a long fraction of a metre: a micrometre is enough, and reads better.
  return String(Number.isInteger(n) ? n : Math.round(n * 1e6) / 1e6);
}

/** What a command's result is named after: its name without the verb (`addWall` → `wall`, `createWallType` → `wall_type`). */
export function baseName(command: string): string {
  const last = command.slice(command.lastIndexOf('.') + 1);
  const bare = last.replace(/^(add|create|place|insert|new|make|draw|duplicate|copy)(?=[A-Z])/, '');
  const s = snake(bare.slice(0, 1).toLowerCase() + bare.slice(1)).replace(/[^a-z0-9_]/g, '_').replace(/^_+|_+$/g, '');
  return s && isIdent(s) && !KEYWORDS.has(s) ? s : 'result';
}

const INDENT = '    ';

/** One step recorded: its lines, and the lookups they bind ([id, variable]). */
interface Block { lines: string[]; found: [string, string][] }

/**
 * A recording: the lines so far, and what it knows to write the next ones (the variables bound to what earlier lines
 * made, the lookups of existing storeys and types).
 */
export class Recording {
  /** Each step recorded, as its lines (a batch is one step). */
  private blocks: Block[] = [];
  /** The steps the window undid, last first: a redo brings them back. */
  private undone: Block[] = [];
  /** The lookups the step being written binds (its storey, its type), forgotten with it when it is undone. */
  private binding: [string, string][] = [];
  private made = new Map<string, string>();
  private found = new Map<string, string>();
  private counts = new Map<string, number>();

  /** A fresh name for a variable: `wall1`, `wall2`… */
  private fresh(base: string): string {
    const n = (this.counts.get(base) ?? 0) + 1;
    this.counts.set(base, n);
    return `${base}${n}`;
  }

  /** The lines so far. */
  get lines(): string[] { return this.blocks.flatMap((b) => b.lines); }

  /** The whole script, ready to run here or from a terminal. */
  text(): string { return Recording.text(this.lines); }

  /** A recording's lines as the whole script: its head, the import, the lines. */
  static text(lines: readonly string[]): string {
    return [
      '# Recorded in Lapin (View → Script → Record): each line is a step done in the window.',
      '# Run it in the Script panel (one undo step), or from a terminal with Lapin open.',
      'import lapinbim as lapin',
      '',
      ...lines,
      '',
    ].join('\n');
  }

  /**
   * The call heard, as lines (none for what is not recorded); returns what it changed (empty: nothing). An Undo takes
   * back the last step recorded (a Redo brings it back), so the script holds what the window did in the end and runs as
   * one undo step in the panel; an Undo of what was there before the recording is written `lapin.undo()` (which only a
   * script outside a step can do: from a terminal).
   */
  add(call: RecordedCall, ctx: RecorderContext): string[] {
    if (call.method === 'doc.undo') {
      const last = this.blocks.pop();
      if (last) {
        for (const [id] of last.found) this.found.delete(id);
        this.undone.push(last);
        return last.lines;
      }
      this.blocks.push({ lines: ['lapin.undo()'], found: [] });
      return ['lapin.undo()'];
    }
    if (call.method === 'doc.redo') {
      const back = this.undone.pop() ?? { lines: ['lapin.redo()'], found: [] };
      for (const [id, v] of back.found) this.found.set(id, v);
      this.blocks.push(back);
      return back.lines;
    }
    this.binding = [];
    const block: string[] = [];
    if (call.method === 'doc.execute' && typeof call.params.command === 'string') {
      const command = call.params.command;
      const args = (call.params.args ?? {}) as Record<string, unknown>;
      if (command === 'edit.batch' && Array.isArray(args.steps)) {
        const steps = args.steps as { command?: unknown; args?: unknown }[];
        const results = Array.isArray(call.result) ? call.result : [];
        const pre: string[] = [];
        const body: string[] = [];
        steps.forEach((s, i) => {
          if (typeof s?.command !== 'string') return;
          body.push(...this.command(s.command, (s.args ?? {}) as Record<string, unknown>, results[i], ctx, pre).map((l) => INDENT + l));
        });
        if (!body.length) return [];
        block.push(...pre, `with lapin.batch(${typeof args.label === 'string' && args.label.trim() ? str(args.label) : ''}):`, ...body);
      } else {
        const pre: string[] = [];
        const own = this.command(command, args, call.result, ctx, pre);
        block.push(...pre, ...own);
      }
    }
    if (!block.length) { for (const [id] of this.binding) this.found.delete(id); return []; }
    // A new step ends what the window could redo.
    this.undone = [];
    this.blocks.push({ lines: block, found: this.binding });
    return block;
  }

  /** One command as one line (its lookups first, into `pre`), its result bound to a variable when it made something. */
  private command(command: string, args: Record<string, unknown>, result: unknown, ctx: RecorderContext, pre: string[]): string[] {
    const kw: string[] = [];
    const raw: string[] = [];
    for (const [k, v] of Object.entries(args)) {
      if (v === undefined) continue;
      const value = k === 'ids' && Array.isArray(v) ? this.ids(v, ctx, pre) : this.value(v, ctx, pre);
      const py = ident(snake(k));
      // A keyword where lapinbim names it back the engine's way; else (and a None, which a keyword would leave out) in the dict it sends as it is.
      if (v !== null && isIdent(py) && camel(py) === k) kw.push(`${py}=${value}`);
      else raw.push(`${str(k)}: ${value}`);
    }
    const params = [...(raw.length ? [`{${raw.join(', ')}}`] : []), ...kw].join(', ');
    const dot = command.indexOf('.');
    const ns = dot > 0 ? ident(snake(command.slice(0, dot))) : '';
    const name = dot > 0 ? ident(snake(command.slice(dot + 1))) : '';
    const typed = dot > 0 && isIdent(ns) && isIdent(name) && !name.startsWith('_') && (ctx.known?.(command) ?? true);
    const call = !typed ? `lapin.execute(${str(command)}${args && Object.keys(args).length ? `, ${this.dict(args, ctx, pre)}` : ''})`
      : RESERVED.has(ns) ? `lapin.ns(${str(command.slice(0, dot))}).${name}(${params})`
        : `lapin.${ns}.${name}(${params})`;
    const bound = this.bind(command, result);
    return [bound ? `${bound} = ${call}` : call];
  }

  /** What a command made, bound to a new variable: its id (`wall1.id`), or its ids (`walls1.ids[0]`). */
  private bind(command: string, result: unknown): string | null {
    if (!result || typeof result !== 'object' || Array.isArray(result)) return null;
    const r = result as Record<string, unknown>;
    if (typeof r.id === 'string') {
      const v = this.fresh(baseName(command));
      this.made.set(r.id, `${v}.id`);
      return v;
    }
    if (Array.isArray(r.ids) && r.ids.length && r.ids.every((x) => typeof x === 'string')) {
      const base = baseName(command);
      const v = this.fresh(base.endsWith('s') ? base : `${base}s`);
      (r.ids as string[]).forEach((id, i) => this.made.set(id, `${v}.ids[${i}]`));
      return v;
    }
    return null;
  }

  /** The arguments as one dict, for a command written by its engine name. */
  private dict(args: Record<string, unknown>, ctx: RecorderContext, pre: string[]): string {
    return `{${Object.entries(args).filter(([, v]) => v !== undefined).map(([k, v]) => `${str(k)}: ${k === 'ids' && Array.isArray(v) ? this.ids(v, ctx, pre) : this.value(v, ctx, pre)}`).join(', ')}}`;
  }

  /** An `ids` argument: the window's selection as `lapin.selection()` when it is that (and the option is on), else each id. */
  private ids(v: unknown[], ctx: RecorderContext, pre: string[]): string {
    const same = ctx.useSelection && ctx.selection.length > 0 && v.length === ctx.selection.length && v.every((x) => typeof x === 'string' && ctx.selection.includes(x));
    return same ? 'lapin.selection()' : this.value(v, ctx, pre);
  }

  /** A value as Python: an id another line made by its variable, an existing storey or type by a lookup, the rest literally. */
  private value(v: unknown, ctx: RecorderContext, pre: string[]): string {
    if (v === null || v === undefined) return 'None';
    if (typeof v === 'boolean') return v ? 'True' : 'False';
    if (typeof v === 'number') return num(v);
    if (typeof v === 'string') return this.made.get(v) ?? this.reference(v, ctx, pre) ?? str(v);
    if (Array.isArray(v)) return `[${v.map((x) => this.value(x, ctx, pre)).join(', ')}]`;
    if (typeof v === 'object') return `{${Object.entries(v as Record<string, unknown>).filter(([, x]) => x !== undefined).map(([k, x]) => `${str(k)}: ${this.value(x, ctx, pre)}`).join(', ')}}`;
    return str(String(v));
  }

  /** An entity of the project named by an argument: a storey by its name and elevation, a type or resource by its class and name, an element by its id. */
  private reference(id: string, ctx: RecorderContext, pre: string[]): string | null {
    const n = ctx.node(id);
    if (!n) return null;
    const known = this.found.get(id);
    if (known) return known;
    let lookup: string;
    let base: string;
    if (n.type === 'IfcBuildingStorey') {
      const elevation = Number(n.props.elevation ?? 0);
      lookup = `lapin.model.storey(${str(n.name)}, ${num(Number.isFinite(elevation) ? elevation : 0)})`;
      base = 'storey';
    } else if ((n.category === 'type' || n.category === 'resource' || /Type$/.test(n.type)) && n.name) {
      lookup = `lapin.model.find(${str(n.type)}, ${str(n.name)})`;
      base = baseName(`x.${n.type.replace(/^(Ifc|Lapin)/, '')}`);
    } else {
      return `lapin.model[${str(id)}]`;
    }
    const v = this.fresh(base);
    pre.push(`${v} = ${lookup}`);
    this.found.set(id, v);
    this.binding.push([id, v]);
    return v;
  }
}
