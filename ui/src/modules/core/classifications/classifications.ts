/** The engine's classifications and shared parameters (docs/plan-user-extensibility.md §7.3): reads kept fresh, and the commands. */

import { useEffect, useState } from 'react';
import { useSession } from '../../../engine/session';
import { ask, runOk as run } from '../LibraryChoices';
import type { ClassItem, ClassSystem, ParamDef } from './classModel';

export const listSystems = () => ask<{ systems: ClassSystem[] }>('classification.list', {}).then((r) => r?.systems ?? []);
export const systemItems = (id: string, args: { parent?: string | null; search?: string } = {}) =>
  ask<{ items: ClassItem[]; count: number }>('classification.items', { id, ...args });
export const exportSystem = (id: string) => ask<{ text: string; file: string }>('classification.export', { id });
export const createSystem = (args: { name: string; source?: string; edition?: string }) => run<{ id: string }>('classification.create', args);
export const updateSystem = (args: { id: string; name?: string; source?: string; edition?: string }) => run<unknown>('classification.update', args);
export const deleteSystem = (id: string) => run<unknown>('classification.delete', { id });
export const assignCode = (args: { ids: string[]; system: string; code: string; add?: boolean; title?: string }) => run<unknown>('classification.assign', args);
export const unassignCode = (ids: string[], system: string) => run<unknown>('classification.unassign', { ids, system });

export const listDefs = () => ask<{ defs: ParamDef[] }>('params.defs', {}).then((r) => r?.defs ?? []);
export const setDef = (def: Partial<ParamDef> & { name: string; kind: string }) => run<{ id: string }>('params.setDef', { def });
export const removeDef = (id: string) => run<unknown>('params.removeDef', { id });
export const exportDefs = () => ask<{ text: string; file: string }>('params.exportDefs', {});
export const importDefs = (text: string, keep?: boolean) => run<{ added: number; updated: number }>('params.importDefs', { text, ...(keep ? { keep } : {}) });

/** The classification systems of the project, kept fresh. */
export function useSystems(): ClassSystem[] {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<ClassSystem[]>([]);
  useEffect(() => { let live = true; void listSystems().then((r) => { if (live) setList((was) => (JSON.stringify(was) === JSON.stringify(r) ? was : r)); }); return () => { live = false; }; }, [version]);
  return list;
}

/** The project's shared parameter definitions, kept fresh. */
export function useParamDefs(): ParamDef[] {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<ParamDef[]>([]);
  useEffect(() => { let live = true; void listDefs().then((r) => { if (live) setList((was) => (JSON.stringify(was) === JSON.stringify(r) ? was : r)); }); return () => { live = false; }; }, [version]);
  return list;
}
