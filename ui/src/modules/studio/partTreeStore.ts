import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import type { PartTree } from './partTreeModel';

/** The tree of the part being edited, asked again as the model changes. */
export function usePartTree(id: string | null): PartTree | null {
  const version = useSession((s) => s.version);
  const [tree, setTree] = useState<PartTree | null>(null);
  useEffect(() => { setTree(null); }, [id]);
  useEffect(() => {
    if (!id) return;
    let live = true;
    void query<PartTree>('studio.partTree', { id }).then((r) => { if (live && r) setTree(r); });
    return () => { live = false; };
  }, [id, version]);
  return tree;
}
