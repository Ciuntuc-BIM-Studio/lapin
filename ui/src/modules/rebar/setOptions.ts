/**
 * The rebar set tools' options (decision 730): the layer, the direction, the diameter and the spacing a new set is made with,
 * kept for the session. Left on "Default" a field is not sent, so the engine's own default (a slab's first layer, a second
 * layer crossing the first) applies.
 */

import { create } from 'zustand';

export interface SetOptions {
  /** B1 B2 T1 T2 (slab, footing) or E1 E2 I1 I2 (wall); '' the engine's default. */
  layer: string;
  /** 'x' | 'y' | 'horizontal' | 'vertical'; '' the engine's default. */
  direction: '' | 'x' | 'y' | 'horizontal' | 'vertical';
  /** Metres; 0: the engine's default. */
  diameter: number;
  /** Metres; 0: the engine's default. */
  step: number;
}

export const useSetOptions = create<SetOptions>(() => ({ layer: '', direction: '', diameter: 0, step: 0 }));

export const SET_LAYERS = ['B1', 'B2', 'T1', 'T2', 'E1', 'E2', 'I1', 'I2'];

/** What `rebar.addSet` is given for the options: only what was chosen. */
export function setArgs(o: SetOptions): Record<string, unknown> {
  const a: Record<string, unknown> = {};
  if (o.layer) a.layer = o.layer;
  if (o.direction) a.direction = o.direction;
  if (o.diameter > 0) a.diameter = o.diameter;
  if (o.step > 0) a.step = o.step;
  return a;
}

/** A set's direction as the section shows it: a slab's x (0°), y (90°) or an angle; a wall's horizontal (0°) or vertical (90°). */
export function directionChoice(angle: number, wall: boolean): string {
  const a = ((Math.round(angle * 1000) / 1000) % 180 + 180) % 180;
  if (a === 0) return wall ? 'horizontal' : 'x';
  if (a === 90) return wall ? 'vertical' : 'y';
  return 'angle';
}
