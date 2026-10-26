/** The project's view styles (docs/view-styles.md), as the engine lists them, read again whenever the model changes. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';

export type LayerMode = 'all' | 'noFinishes' | 'core';
export interface ViewStyle { id: string; name: string; discipline: string | null; layers: LayerMode; joins: boolean; overhead: boolean }

export function useViewStyles(): ViewStyle[] {
  const nodes = useSession((s) => s.nodes);
  const [styles, setStyles] = useState<ViewStyle[]>([]);
  useEffect(() => { void query<ViewStyle[]>('viewStyle.list').then((r) => setStyles(r ?? [])); }, [nodes]);
  return styles;
}
