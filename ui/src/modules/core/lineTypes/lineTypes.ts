/** The engine's line types (docs/plan-user-extensibility.md §3.6): the list the project has, kept fresh, and the dashes the screen draws with. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../../engine/session';
import { ask, runOk as run } from '../LibraryChoices';
import { DASHES } from '../../../viewport/planDetail';
import type { LineTypesDto, ReadLineType } from './lineTypeModel';

/** The project's own line types' dashes added to the table the plan draws with (and the ones of removed types taken out). */
let own: string[] = [];
function share(lt: LineTypesDto): void {
  for (const n of own) delete DASHES[n];
  own = lt.own.map((o) => o.name);
  for (const o of lt.own) DASHES[o.name] = o.dashes;
}

export const lineTypes = () => query<LineTypesDto>('standards.lineTypes');

/** The line types of the project and Lapin's; refreshed whenever the project changes. */
export function useLineTypes(): LineTypesDto | null {
  const version = useSession((s) => s.version);
  const [lt, setLt] = useState<LineTypesDto | null>(null);
  useEffect(() => {
    let live = true;
    void lineTypes().then((r) => { if (live && r) { share(r); setLt((was) => (was && JSON.stringify(was) === JSON.stringify(r) ? was : r)); } });
    return () => { live = false; };
  }, [version]);
  return lt;
}

export const setLineType = (args: { name: string; dashes?: number[]; description?: string; newName?: string }) => run<{ name: string }>('standards.setLineType', args);
export const removeLineType = (name: string) => run<unknown>('standards.removeLineType', { name });
export const readLin = (text: string, units: 'mm' | 'in') => ask<{ lineTypes: ReadLineType[]; skipped: string[] }>('standards.readLin', { text, units });
export const importLin = (args: { text: string; units: 'mm' | 'in'; names?: string[]; choices?: Record<string, string>; all?: string }) =>
  run<{ added: number; updated: number; skipped: number }>('standards.importLin', args);
export const exportLin = (names?: string[]) => ask<{ text: string; file: string }>('standards.exportLin', names ? { names } : {});
