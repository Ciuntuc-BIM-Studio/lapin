/** The engine's side of families (studio.families, studio.familyPreview…), and what the dialogs share. */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { engine, execute, query, useSession } from '../../../engine/session';
import { normaliseFamily, type FamilyDef, type FamilyDto, type PreviewBody } from './familyModel';

export async function listFamilies(): Promise<FamilyDto[]> {
  const r = await query<{ families: (Omit<FamilyDto, 'def'> & { def: Partial<FamilyDef> })[] }>('studio.families');
  return (r?.families ?? []).map((f) => ({ ...f, def: normaliseFamily(f.def) }));
}

/** The families of the open project, kept fresh as the model changes. */
export function useFamilies(): { families: FamilyDto[]; loaded: boolean } {
  const version = useSession((s) => s.version);
  const connected = useSession((s) => s.connected);
  const [state, set] = useState<{ families: FamilyDto[]; loaded: boolean }>({ families: [], loaded: false });
  useEffect(() => {
    if (!connected) { set({ families: [], loaded: true }); return; }
    let live = true;
    void listFamilies().then((families) => { if (live) set({ families, loaded: true }); });
    return () => { live = false; };
  }, [version, connected]);
  return state;
}

/** A family's bodies for some values, or the sentence the engine gives when the definition does not read. */
export async function previewFamily(arg: { def: FamilyDef } | { id: string }, values?: Record<string, number>): Promise<{ bodies: PreviewBody[] } | { error: string }> {
  try {
    const r = await engine.call<{ bodies: PreviewBody[] }>('doc.query', { name: 'studio.familyPreview', args: { ...arg, ...(values && Object.keys(values).length ? { values } : {}) } });
    return { bodies: r?.bodies ?? [] };
  } catch (e) { return { error: e instanceof Error ? e.message : String(e) }; }
}

export const createFamily = (name: string, def: FamilyDef) => execute('studio.createFamily', { name, def }) as Promise<{ id: string } | null>;
export const updateFamily = (id: string, patch: { name?: string; def?: FamilyDef }) => execute('studio.updateFamily', { id, ...patch });
export const deleteFamily = (id: string) => execute('studio.deleteFamily', { id });
export const importFamily = (text: string) => execute('studio.importFamily', { text }) as Promise<{ id: string } | null>;
export const exportFamily = (id: string) => query<{ text: string; file: string }>('studio.exportFamily', { id });
export const setFamilyValues = (ids: string[], values: Record<string, number | null>) => execute('studio.setFamilyValues', { ids, values });

/** Where the Families dialog opens (Properties → Edit family, the place tool's „Edit…”). */
export const familiesDialog = { open: null as string | null };

/** A request for Make family from sketch…: the view and the drafted outlines. */
export const useFromSketch = create<{ request: { viewId: string; ids: string[] } | null }>(() => ({ request: null }));

/** What the Family place tool puts down: the family, the values typed (empty: the default), the turn and the mirror. */
export const usePlaceFamily = create<{ familyId: string | null; values: Record<string, string>; rotation: number; mirror: boolean }>(() => ({ familyId: null, values: {}, rotation: 0, mirror: false }));
