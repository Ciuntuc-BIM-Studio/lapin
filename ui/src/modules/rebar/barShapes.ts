/** The engine's bar shapes (docs/plan-user-extensibility.md §6.3), the bending schedule by the standard and BVBS (§6.4). */

import { useEffect, useState } from 'react';
import { notify } from '../../app/store';
import { query, useSession } from '../../engine/session';
import { saveFile, safeFileName } from '../../engine/files';
import { ask, runOk as run } from '../core/LibraryChoices';
import type { ShapeDef, ShapeLibrary } from './shapeModel';

export const shapeLibrary = () => ask<ShapeLibrary>('rebar.shapeLibrary', {});
export const shapePreview = (args: { def: ShapeDef } | { id: string }, params?: Record<string, number>) =>
  ask<{ corners: number[][] }>('rebar.shapePreview', { ...args, ...(params ? { params } : {}) });
export const createShape = (def: ShapeDef) => run<{ id: string }>('rebar.createShape', { def });
export const updateShape = (id: string, def: ShapeDef) => run<unknown>('rebar.updateShape', { id, def });
export const deleteShape = (id: string) => run<unknown>('rebar.deleteShape', { id });
export const importShapes = (text: string, update?: boolean) => run<{ added: number; updated: number }>('rebar.importShapes', { text, ...(update ? { update } : {}) });
export const exportShapes = (ids?: string[]) => ask<{ text: string; file: string }>('rebar.exportShapes', ids ? { ids } : {});

/** The project's bar shapes, asked again as the model changes. */
export function useShapeLibrary(): ShapeLibrary | null {
  const version = useSession((s) => s.version);
  const [lib, setLib] = useState<ShapeLibrary | null>(null);
  useEffect(() => {
    let live = true;
    // A quiet read: the library is asked for on every change, so a refusal is not a notice.
    void query<ShapeLibrary>('rebar.shapeLibrary').then((r) => { if (live && r) setLib((was) => (was && JSON.stringify(was) === JSON.stringify(r) ? was : r)); });
    return () => { live = false; };
  }, [version]);
  return lib;
}

export interface Schedule { columns: string[]; rows: (string | number | null)[][] }
export const bendingSchedule = () => ask<Schedule>('rebar.bendingSchedule', {});
export const bvbs = (args: { plan?: string; index?: string } = {}) => ask<{ text: string; file: string; records: number; leftOut: number }>('rebar.exportBvbs', args);

/** A schedule as CSV (semicolons, as the engine's other CSV; a cell with a semicolon, a quote or a line break quoted). */
export function scheduleCsv(s: Schedule): string {
  const cell = (v: string | number | null) => { const t = v == null ? '' : String(v); return /[;"\n]/.test(t) ? `"${t.replace(/"/g, '""')}"` : t; };
  return [s.columns, ...s.rows].map((r) => r.map(cell).join(';')).join('\n') + '\n';
}

/** Export BVBS (.abs): saves the file and says how many records, and how many bars laid by types were left out. */
export async function exportBvbsFile(): Promise<void> {
  const r = await bvbs();
  if (!r) return;
  const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, 'bars.abs'), 'text/plain');
  if (!saved) return;
  notify(`Exported ${saved}: ${r.records} records${r.leftOut > 0 ? `, ${r.leftOut} bars laid by types left out (they have no drawn shape)` : ''}.`);
}
