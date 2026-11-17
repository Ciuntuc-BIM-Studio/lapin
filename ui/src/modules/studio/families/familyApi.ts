/** The engine's side of parts (studio.parts, studio.partPreview…; a family is a part), and what the dialogs share. */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { engine, execute, query, useSession } from '../../../engine/session';
import { normaliseFamily, type FamilyDef, type FamilyDto, type PreviewBody } from './familyModel';

export async function listFamilies(): Promise<FamilyDto[]> {
  const r = await query<{ parts: (Omit<FamilyDto, 'def'> & { def: Partial<FamilyDef> | null })[] }>('studio.parts');
  return (r?.parts ?? []).map((f) => ({ ...f, raw: f.def as Record<string, unknown> | null, def: normaliseFamily(f.def) }));
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
    const r = await engine.call<{ bodies: PreviewBody[] }>('doc.query', { name: 'studio.partPreview', args: { ...arg, ...(values && Object.keys(values).length ? { values } : {}) } });
    return { bodies: r?.bodies ?? [] };
  } catch (e) { return { error: e instanceof Error ? e.message : String(e) }; }
}

export const createPart = (name: string, def: FamilyDef) => execute('studio.createPart', { name, def, constrain: true }) as Promise<{ id: string } | null>;
export const updatePart = (id: string, patch: { name?: string; def?: FamilyDef }) => execute('studio.updatePart', { id, ...patch });
export const deletePart = (id: string) => execute('studio.deletePart', { id });
export const exportPart = (id: string) => query<{ text: string; file: string }>('studio.exportPart', { id });
export const setPartValues = (ids: string[], values: Record<string, number | null>) => execute('studio.setPartValues', { ids, values });

/** The part the Parts dialog opens on (Properties → Parts…, the place tool's „Edit…”). */
export const partsDialog = { open: null as string | null };

/** A request for Make part from sketch…: the view and the drafted outlines. */
export const useFromSketch = create<{ request: { viewId: string; ids: string[] } | null }>(() => ({ request: null }));

/** What the Part place tool puts down: the family, the values typed (empty: the default), the turn and the mirror. */
export const usePlaceFamily = create<{ familyId: string | null; values: Record<string, string>; rotation: number; mirror: boolean }>(() => ({ familyId: null, values: {}, rotation: 0, mirror: false }));
