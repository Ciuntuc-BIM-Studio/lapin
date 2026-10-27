/**
 * My patterns: the .pat files a user has imported, kept on this computer (the
 * app's or the browser's storage), so they can be brought into any project.
 * A project keeps the patterns it uses either way; this is the user's own
 * collection beside it.
 */

const KEY = 'lapin.myPatterns';

export interface MyPatternFile { name: string; text: string; names: string[] }

export function myPatterns(): MyPatternFile[] {
  try { return JSON.parse(localStorage.getItem(KEY) ?? '[]') as MyPatternFile[]; } catch { return []; }
}

/** The pattern names a .pat file defines (its *NAME lines). */
export function patternNames(text: string): string[] {
  return [...text.matchAll(/^\*\s*([^,\r\n]+)/gm)].map((m) => m[1].trim().toUpperCase());
}

/** Keeps a file in the collection (a file of the same name is replaced). */
export function keepPatterns(name: string, text: string): void {
  const list = myPatterns().filter((f) => f.name !== name);
  list.push({ name, text, names: patternNames(text) });
  try { localStorage.setItem(KEY, JSON.stringify(list)); } catch { /* full or blocked: the project still has them */ }
}

export function forgetPatterns(name: string): void {
  try { localStorage.setItem(KEY, JSON.stringify(myPatterns().filter((f) => f.name !== name))); } catch { /* nothing kept */ }
}
