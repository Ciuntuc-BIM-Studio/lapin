/**
 * Material keys (forum topic 255): how a material's key is written from its number (the project's format, Drafting standards →
 * Project), as the engine writes it (MaterialKeys), for the dialogs' previews, and what standards.options is given for a format.
 */

/** The format a project takes when it sets none: the number alone. */
export const DEFAULT_KEY_FORMAT = '{n}';

const SLOT = /\{n(?::(0{1,4}))?\}/g;

/** Whether a format holds its number once ({n} or {n:00}), in 24 characters at most. */
export function isKeyFormat(text: string): boolean {
  const t = text.trim();
  return t.length > 0 && t.length <= 24 && !t.includes('|') && (t.match(SLOT) ?? []).length === 1;
}

/** A number written by a format: F{n} with 3 is F3, M-{n:00} with 3 is M-03. */
export function writeKey(format: string, n: number): string {
  return format.replace(SLOT, (_, zeros?: string) => (zeros ? String(n).padStart(zeros.length, '0') : String(n)));
}

/** What standards.options is given for a format typed: null for none or the default (so the project keeps no value of its own). */
export const keyFormatArg = (text: string): string | null => (text.trim() === '' || text.trim() === DEFAULT_KEY_FORMAT ? null : text.trim());

/** A row of material.keys: a material's key in effect, its number, whether that number is stored, its own code. */
export interface MaterialKeyRow { id: string; name: string; key: string; number: number; stored: boolean; own?: string | null }
