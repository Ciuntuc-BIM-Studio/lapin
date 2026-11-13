/** The engine's furniture models of the project's own (docs/plan-user-extensibility.md §3.5): the list, kept fresh, and the commands. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import { runOk as run } from '../core/LibraryChoices';
import { toBase64 } from '../../engine/files';
import type { UserModel, Zones } from './models';

export const listUserModels = () => query<{ models: UserModel[] }>('furniture.models').then((r) => r?.models ?? []);

export function useUserModels(): UserModel[] {
  const version = useSession((s) => s.version);
  const [list, setList] = useState<UserModel[]>([]);
  useEffect(() => { let live = true; void listUserModels().then((r) => { if (live) setList((was) => (JSON.stringify(was) === JSON.stringify(r) ? was : r)); }); return () => { live = false; }; }, [version]);
  return list;
}

export interface ModelSettings {
  name?: string; licence?: string; source?: string; group?: string; predefined?: string; size?: number[]; min?: number[]; max?: number[];
  zones?: Record<string, [number, number]>; slots?: Record<string, string>; mount?: number; against?: boolean; seats?: boolean;
}

export const importModel = (bytes: Uint8Array, s: ModelSettings & { name: string; licence: string }) =>
  run<{ id: string; model: string; natural: number[]; materials: string[] }>('furniture.importModel', { data: toBase64(bytes), ...s });
export const updateModel = (id: string, s: ModelSettings) => run<{ model: string }>('furniture.updateModel', { id, ...s });
export const deleteModel = (id: string) => run<unknown>('furniture.deleteModel', { id });
export type { Zones };
