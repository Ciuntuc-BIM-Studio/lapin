/**
 * The project's own roof panel profiles (docs/plan-roof-panels.md §3, stage S7): asked of the engine (`roofpanels.projectProfiles`)
 * as the model changes, and the commands that make, change and take them out, each one undo step. Lapin's own profiles stay
 * read-only: a change to one is a copy in the project.
 */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import { ask, runOk as run } from '../core/LibraryChoices';
import type { ProfileLibraryDto } from './roofpanels';

/** The profiles a roof can name, Lapin's and the project's, asked again as the model changes (null until the first answer). */
export function useProfileLibrary(): ProfileLibraryDto | null {
  const version = useSession((s) => s.version);
  const [lib, setLib] = useState<ProfileLibraryDto | null>(null);
  useEffect(() => {
    let live = true;
    // A quiet read: asked on every change, so a refusal is not a notice (a product without the module has none).
    void query<ProfileLibraryDto>('roofpanels.projectProfiles').then((r) => { if (live && r) setLib((was) => (was && JSON.stringify(was) === JSON.stringify(r) ? was : r)); });
    return () => { live = false; };
  }, [version]);
  return lib;
}

export const duplicateProfile = (from: string, name?: string) => run<{ id: string; profile: string }>('roofpanels.duplicateProfile', name ? { from, name } : { from });
export const updateProfile = (id: string, def: Record<string, unknown>) => run<unknown>('roofpanels.updateProfile', { id, def });
export const deleteProfile = (id: string) => run<unknown>('roofpanels.deleteProfile', { id });
export const exportProfiles = (ids?: string[]) => ask<{ text: string; file: string }>('roofpanels.exportProfiles', ids ? { ids } : {});

/** The extension of a firm's file of roof panel profiles. */
export const PROFILES_EXT = 'lapinpanels';
