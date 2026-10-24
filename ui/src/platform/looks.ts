/** How modules ask the views to show an element: hidden, or tinted (a phase's override). Keyed by entity id; absent: as it is. */

import { create } from 'zustand';

export interface Look { hidden?: boolean; color?: string; opacity?: number }

export const useLooks = create<{ looks: Record<string, Look> }>(() => ({ looks: {} }));
