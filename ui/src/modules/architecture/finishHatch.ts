/**
 * A room finish's plan hatch (decision 299): how plans hatch its floor (a reflected ceiling plan its ceiling) — from the face
 * material's surface pattern (the default, kept as nothing on the type), a pattern of its own, or none. The engine draws it
 * (RoomFinishes.HatchOf, Rooms.Marks); this is the configurator's side.
 */

export type FinishHatchMode = 'material' | 'own' | 'none';
export interface FinishHatch { mode: FinishHatchMode; pattern?: string | null; scale?: number; angle?: number; color?: string | null }
export type HatchPart = 'floor' | 'ceiling';

const KEY = { floor: 'floorHatch', ceiling: 'ceilingHatch' } as const;

/** The part's hatch as the configurator shows it: the default (from its material) when the type keeps none. */
export function hatchOf(spec: { floorHatch?: FinishHatch | null; ceilingHatch?: FinishHatch | null }, part: HatchPart): Required<FinishHatch> {
  const h = spec[KEY[part]];
  return { mode: h?.mode ?? 'material', pattern: h?.pattern ?? null, scale: h?.scale ?? 1, angle: h?.angle ?? 0, color: h?.color ?? null };
}

/**
 * The spec's fields with the part's hatch changed: the default again kept as nothing (so a type reads as before); an own
 * hatch chosen without a pattern takes the one the material would have given, else diagonal lines.
 */
export function withHatch(spec: { floorHatch?: FinishHatch | null; ceilingHatch?: FinishHatch | null }, part: HatchPart, patch: Partial<FinishHatch>, suggested?: string | null): Record<string, FinishHatch | null> {
  const h = { ...hatchOf(spec, part), ...patch };
  if (h.mode === 'own' && !h.pattern) h.pattern = suggested || 'LINES45';
  if (h.mode !== 'own') { h.pattern = null; h.color = null; }
  const plain = h.mode === 'material' && h.scale === 1 && h.angle === 0;
  return { [KEY[part]]: plain ? null : h };
}
