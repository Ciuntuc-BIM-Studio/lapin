/** The project's material graphics sets as the engine lists them: one shared copy, read again once per change of the model. */

import { useEffect } from 'react';
import { create } from 'zustand';
import { query, useSession } from '../../engine/session';
import type { GraphicsSetList } from './materialGraphicsSetsModel';

const useSets = create<{ list: GraphicsSetList; asked: unknown }>(() => ({ list: { sets: [], categories: [] }, asked: null }));

export function useMaterialGraphicsSets(): GraphicsSetList {
  const nodes = useSession((s) => s.nodes);
  useEffect(() => {
    if (useSets.getState().asked === nodes) return;
    useSets.setState({ asked: nodes });
    void query<GraphicsSetList>('materialGraphicsSet.list').then((r) => { if (useSets.getState().asked === nodes && r) useSets.setState({ list: r }); });
  }, [nodes]);
  return useSets((s) => s.list);
}
