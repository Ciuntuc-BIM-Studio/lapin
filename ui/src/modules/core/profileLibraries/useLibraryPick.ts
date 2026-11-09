/**
 * A profile picked from the libraries for a new type (docs/plan-profile-libraries.md): the profile and its library, its
 * section as the type will keep it (from the engine, an outline for the kinds a section has not) and its mass, for the
 * dialog's preview.
 */

import { useEffect, useState } from 'react';
import { query } from '../../../engine/session';
import type { ProfileJson } from './model';
import type { SectionDto } from '../../steel/sections';

export function useLibraryPick() {
  const [picked, setPicked] = useState<{ profile: ProfileJson; library: string | null } | null>(null);
  const [section, setSection] = useState<SectionDto | null>(null);
  const [mass, setMass] = useState<number | null>(null);
  useEffect(() => {
    if (!picked) { setSection(null); setMass(null); return; }
    let live = true;
    void query<{ section: SectionDto; values: Record<string, { computed?: number; given?: number }> }>('profiles.properties', { profile: picked.profile }).then((r) => {
      if (!live || !r) return;
      setSection(r.section);
      setMass(r.values.mass?.given ?? r.values.mass?.computed ?? null);
    });
    return () => { live = false; };
  }, [picked]);
  return { picked, section, mass, pick: (profile: ProfileJson, library: string | null) => setPicked({ profile, library }) };
}
