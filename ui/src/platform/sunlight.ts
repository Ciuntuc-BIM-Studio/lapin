/**
 * The 3D view's sun, as a module asks for it (the urban module's sun study):
 * the direction towards the sun in the project's coordinates (x east of the
 * plan's +X, z up), and whether it casts shadows. Null: the view's own fixed
 * light, no shadows. The view only obeys it; where the sun is, for which
 * place, day and hour, is the module's to say.
 */

import { create } from 'zustand';

export interface Sunlight {
  /** Towards the sun, a unit vector; z below 0: the sun is down (night light). */
  dir: [number, number, number] | null;
  /** What the sun says of itself, shown on the view: "21 June 12:00 · 68.9° high, 180° azimuth". */
  label: string | null;
}

export const useSunlight = create<Sunlight>(() => ({ dir: null, label: null }));

export const setSunlight = (patch: Partial<Sunlight>): void => useSunlight.setState(patch);
