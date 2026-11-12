/** The engine's table import (docs/plan-user-extensibility.md §7.1): targets, a table read, the preview, the template, the import; and the dialog's opening. */

import { create } from 'zustand';
import { useUi } from '../../../app/store';
import { engine, execute, useSession } from '../../../engine/session';
import type { MapItem, TableTarget } from './tableModel';

export type TableSource = { text: string } | { data: string };
export interface TableRead { count: number; headerRow: number; rows: string[][]; mapping?: MapItem[] }
export interface PreviewRow { row: number; error: string | null; steps: string[] }
export interface TablePreview { rows: PreviewRow[]; ok: number; errors: number; steps: number }
export interface ImportOutcome { steps: number; rows: number; skipped: number; ids: string[] }

/** A query whose refusal comes back as a sentence rather than nothing. */
async function ask<T>(name: string, args: Record<string, unknown>): Promise<{ value: T | null; error: string | null }> {
  try { return { value: await engine.call<T>('doc.query', { name, args }), error: null }; } catch (e) { return { value: null, error: e instanceof Error ? e.message : String(e) }; }
}

export const tableTargets = async (): Promise<TableTarget[]> => (await ask<{ targets: TableTarget[] }>('tables.targets', {})).value?.targets ?? [];
export const readTable = (src: TableSource, target: string | null, headerRow?: number) =>
  ask<TableRead>('tables.read', { ...src, ...(target ? { target } : {}), ...(headerRow != null ? { headerRow } : {}) });
export const previewTable = (args: TableRequest) => ask<TablePreview>('tables.preview', args);
export const tableTemplate = async (target: string) => (await ask<{ text: string; file: string }>('tables.template', { target })).value;

export type TableRequest = TableSource & { target: string; headerRow: number; mapping: { column: number; key: string; unit?: string | null }[]; defaults?: Record<string, string> };

export async function importTable(args: TableRequest & { skipErrors?: boolean }): Promise<ImportOutcome | { error: string }> {
  const r = await execute('tables.import', args) as ImportOutcome | null;
  return r ?? { error: useSession.getState().error ?? 'The import failed.' };
}

/** What Import table… opens with: the target (none: the dialog asks) and its heading. */
export const tableDialog = create<{ target: string | null; title: string | null }>(() => ({ target: null, title: null }));

/** Opens Import table…, on a target when one is given (Import loads…, Import property values…). */
export function openImportTable(target?: string, title?: string): void {
  tableDialog.setState({ target: target ?? null, title: title ?? null });
  useUi.setState({ dialog: 'importTable' });
}
