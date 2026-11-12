/** The engine's symbols (docs/plan-user-extensibility.md §3): the list, one definition, previews, fields, the commands, the pack files. */

import { execute, query, useSession } from '../../../engine/session';
import { notify } from '../../../app/store';
import { normaliseDef, type SymDef, type SymPreview, type SymSummary } from './symbolModel';

export interface SymList { items: SymSummary[]; categories: string[] }
export interface SymField { key: string; label: string; group: string }
export interface SymFields { fields: SymField[]; functions: string[] }
export interface PackSymbol { name: string; category: string; scale: string; exists: boolean; same: boolean; preview?: SymPreview }
export interface ImportResult { added: number; updated: number; kept: number; skipped: number; ids: string[] }

export const listSymbols = (previews = true) => query<SymList>('symbols.list', { previews }).then((r) => r ?? { items: [], categories: [] });
export const getSymbol = (id: string) => query<{ id: string; name: string; builtin: boolean; def: SymDef; uses: number }>('symbols.get', { id })
  .then((r) => (r ? { ...r, def: normaliseDef(r.def) } : null));
export const previewSymbol = (args: { id: string } | { def: SymDef }, opts: { values?: Record<string, string>; flipX?: boolean; flipY?: boolean; angle?: number } = {}) =>
  query<SymPreview>('symbols.preview', { ...args, ...opts });
export const symbolFields = (args: { id: string } | { def: SymDef }) => query<SymFields>('symbols.fields', args);
export const evaluateText = (text: string, values: Record<string, string>) => query<{ text: string; unknown: string[] }>('symbols.evaluate', { text, values });
export const exportSymbols = (ids: string[], name?: string) => query<{ text: string; file: string }>('symbols.export', { ids, ...(name ? { name } : {}) });
export const readPack = (text: string) => query<{ symbols: PackSymbol[] }>('symbols.readPack', { text });

/** A command whose refusal the user reads (a notice). */
async function run<T>(command: string, args: Record<string, unknown>): Promise<T | null> {
  const r = await execute(command, args) as T | null;
  const err = useSession.getState().error;
  if (r == null && err) notify(err);
  return r;
}
export const createSymbol = (args: { name: string; def: SymDef } | { from: string; name?: string }) => run<{ id: string; name: string }>('symbols.create', args);
export const updateSymbol = (id: string, args: { name?: string; def?: SymDef }) => run<unknown>('symbols.update', { id, ...args });
export const deleteSymbol = (id: string, instances = false) => run<{ removed: number }>('symbols.delete', { id, ...(instances ? { instances: true } : {}) });
export const importSymbols = (text: string, choices: Record<string, string>, all?: string, names?: string[]) =>
  run<ImportResult>('symbols.importFrom', { text, choices, ...(all ? { all } : {}), ...(names ? { names } : {}) });
export const placeSymbol = (args: Record<string, unknown>) => run<{ id: string }>('drafting.placeSymbol', args);
export const setSymbol = (ids: string[], args: Record<string, unknown>) => execute('drafting.setSymbol', { ids, ...args });
export const flipSymbol = (ids: string[], axis: 'x' | 'y') => execute('drafting.flipSymbol', { ids, axis });
export const explodeSymbol = (ids: string[]) => run<{ ids: string[] }>('drafting.explodeSymbol', { ids });

/** The symbol the place tool uses, remembered across runs. */
const LAST = 'lapin.symbols.last';
export const lastSymbol = (): string | null => { try { return localStorage.getItem(LAST); } catch { return null; } };
export const rememberSymbol = (id: string) => { try { localStorage.setItem(LAST, id); } catch { /* not kept */ } };
