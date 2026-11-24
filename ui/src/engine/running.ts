/**
 * What the engine is working on, by name (app report #187): an edit past the usual wait is said with the command's readable
 * name ("Still working on: Import IFC…"), so one does not press the same button again; and the same edit asked again while
 * the first has run a while is not sent twice (it is said to be still running instead).
 */

/** The commands' readable names (the command catalogue's labels, the same the Undo button shows), once fetched. */
let labels: Map<string, string> | null = null;

/** Keeps the catalogue's labels (`session.catalogue`'s commands). */
export function setCommandLabels(commands: readonly { name: string; label?: string | null }[] | null | undefined): void {
  labels = new Map((commands ?? []).filter((c) => c.label).map((c) => [c.name, c.label!]));
}

export const haveCommandLabels = () => labels !== null;

/** "wall.moveVertex" → "Move vertex": the readable name when the catalogue has none. */
export function humanize(name: string): string {
  const last = name.split('.').pop() || name;
  const words = last.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ').trim().toLowerCase();
  return words ? words[0].toUpperCase() + words.slice(1) : name;
}

/** A command's readable name: the catalogue's label, else its name made readable. */
export function commandLabel(command: string): string {
  return labels?.get(command) ?? humanize(command);
}

/** What an engine call does, by name: a command's label, Undo, Redo, else the method made readable. */
export function callLabel(method: string, params: Record<string, unknown> | undefined): string {
  if (method === 'doc.execute' && typeof params?.command === 'string') return commandLabel(params.command);
  if (method === 'doc.undo') return 'Undo';
  if (method === 'doc.redo') return 'Redo';
  if (method === 'doc.query' && typeof params?.name === 'string') return humanize(params.name);
  return humanize(method);
}

/** The notice of an edit past the usual wait, naming it. */
export const stillWorking = (label: string) => `Still working on: ${label}…`;
/** Said when the same edit is asked again while it runs. */
export const stillRunning = (label: string) => `${label} is still running: its result shows when it lands.`;

/** An edit sent again this soon is taken as meant (a quick second press); later, while the first still runs, as a repeat. */
export const REPEAT_AFTER_MS = 2_000;

/** The edits on their way: what they are (command and arguments) → when the first was sent and how many are out. */
const inFlight = new Map<string, { since: number; count: number }>();

function keyOf(command: string, args: Record<string, unknown>): string {
  try { return `${command}\u0000${JSON.stringify(args)}`; } catch { return `${command}\u0000?`; }
}

/**
 * Marks an edit as sent, unless the same one (command and arguments) has been on its way for REPEAT_AFTER_MS or more: then
 * null, and it is not to be sent. Otherwise what to call when it lands.
 */
export function begin(command: string, args: Record<string, unknown>, now = Date.now()): (() => void) | null {
  const key = keyOf(command, args);
  const out = inFlight.get(key);
  if (out && now - out.since >= REPEAT_AFTER_MS) return null;
  if (out) out.count++; else inFlight.set(key, { since: now, count: 1 });
  let ended = false;
  return () => {
    if (ended) return;
    ended = true;
    const o = inFlight.get(key);
    if (!o) return;
    if (--o.count <= 0) inFlight.delete(key);
  };
}

/** The slow calls, in the order they became slow: the notice names the newest still running. */
export class SlowCalls {
  private list: string[] = [];
  /** A call became slow (true) or landed (false): the notice to show now (null: none). */
  change(label: string, slow: boolean): string | null {
    if (slow) this.list.push(label);
    else { const i = this.list.lastIndexOf(label); if (i >= 0) this.list.splice(i, 1); }
    const newest = this.list[this.list.length - 1];
    return newest ? stillWorking(newest) : null;
  }
}

/** Forgets every edit on its way (tests; a connection that closed failed them all). */
export function resetRunning(): void { inFlight.clear(); }
