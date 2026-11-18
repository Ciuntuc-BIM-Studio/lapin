/**
 * My patterns: the .pat files a user has imported, kept on this computer (the
 * app's or the browser's storage), so they can be brought into any project.
 * A project keeps the patterns it uses either way; this is the user's own
 * collection beside it.
 */

const KEY = 'lapin.myPatterns';

export interface MyPatternFile { name: string; text: string; names: string[]; /** The patterns that say `;%TYPE=MODEL` (real size), by name. */ model?: string[] }

export function myPatterns(): MyPatternFile[] {
  try { return JSON.parse(localStorage.getItem(KEY) ?? '[]') as MyPatternFile[]; } catch { return []; }
}

/** The pattern names a .pat file defines (its *NAME lines). */
function patternNames(text: string): string[] {
  return [...text.matchAll(/^\*\s*([^,\r\n]+)/gm)].map((m) => m[1].trim().toUpperCase());
}

/**
 * The patterns of a .pat text that are real size: a `;%TYPE=MODEL` comment line (AutoCAD's own extension) before the pattern's
 * `*NAME` line, or anywhere before the file's first pattern for all of them, makes it so; `;%TYPE=PAPER` turns it back.
 */
export function modelPatternNames(text: string): string[] {
  const out: string[] = [];
  let units: 'model' | 'paper' = 'paper';
  for (const line of text.split(/\r?\n/)) {
    const t = line.trim();
    const type = /^;\s*%\s*TYPE\s*=\s*(\w+)/i.exec(t);
    if (type) { units = type[1].toUpperCase() === 'MODEL' ? 'model' : 'paper'; continue; }
    const name = /^\*\s*([^,\r\n]+)/.exec(t);
    if (name && units === 'model') out.push(name[1].trim().toUpperCase());
  }
  return out;
}

/** Keeps a file in the collection (a file of the same name is replaced). */
export function keepPatterns(name: string, text: string): void {
  const list = myPatterns().filter((f) => f.name !== name);
  const model = modelPatternNames(text);
  list.push({ name, text, names: patternNames(text), ...(model.length ? { model } : {}) });
  try { localStorage.setItem(KEY, JSON.stringify(list)); } catch { /* full or blocked: the project still has them */ }
}

export function forgetPatterns(name: string): void {
  try { localStorage.setItem(KEY, JSON.stringify(myPatterns().filter((f) => f.name !== name))); } catch { /* nothing kept */ }
}
