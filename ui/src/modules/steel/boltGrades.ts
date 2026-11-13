/** The bolt grades a connection can be given: the active rule pack's (steel.boltGrades), the static list until it answers. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import { GRADES } from './nodes';

/** The list with the current value kept in it (a pack may have dropped the grade a node still holds). */
export function withCurrent(grades: string[], current?: string | null): string[] {
  return current && !grades.includes(current) ? [...grades, current] : grades;
}

export function useBoltGrades(current?: string | null): string[] {
  const version = useSession((s) => s.version);
  const [grades, setGrades] = useState<string[]>(GRADES);
  useEffect(() => {
    let live = true;
    void query<{ grades: string[] }>('steel.boltGrades', {}).then((r) => { if (live && r?.grades?.length) setGrades(r.grades); });
    return () => { live = false; };
  }, [version]);
  return withCurrent(grades, current);
}
