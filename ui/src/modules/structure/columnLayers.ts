/**
 * A column type's finish layers (decision 719, forum topic 169): plaster, fireproofing boards, insulation laid round its core,
 * outward, each a material and a thickness, all round or on some faces (the section's own axes: x along its width). As the
 * type keeps them (`finishLayers`, materials by id) and as the structure types dialog edits them (materials by name).
 */

import type { NodeDto } from '../../engine/protocol';

export const FACES = ['+x', '-x', '+y', '-y'] as const;
export type Face = (typeof FACES)[number];

/** A layer as edited: its material's name, its thickness (m), the faces it covers (all four: all round). */
export interface Wrap { material: string; thickness: number; faces: Face[] }

interface Stored { material?: string; thickness?: number; faces?: string[] }

/** A type's layers for the dialog: each material's name from its id. */
export function wrapsOf(type: NodeDto | null | undefined, nodes: ReadonlyMap<string, NodeDto>): Wrap[] {
  const raw = type?.props.finishLayers;
  if (!Array.isArray(raw)) return [];
  return (raw as Stored[]).filter((l) => typeof l?.material === 'string' && typeof l.thickness === 'number').map((l) => ({
    material: nodes.get(l.material!)?.name ?? l.material!,
    thickness: l.thickness!,
    faces: Array.isArray(l.faces) && l.faces.length ? FACES.filter((f) => l.faces!.includes(f)) : [...FACES],
  }));
}

/** The layers as the engine takes them (`structure.createColumnType`, `structure.updateType`): faces left out when all round. */
export function wrapsArg(wraps: Wrap[]): { material: string; thickness: number; faces?: Face[] }[] {
  return wraps.filter((w) => w.material.trim() && w.thickness > 0 && w.faces.length).map((w) => ({
    material: w.material.trim(), thickness: w.thickness, ...(w.faces.length < FACES.length ? { faces: w.faces } : {}),
  }));
}

/** A face turned on or off for a layer, the others kept in order; never none (the last one stays). */
export function toggleFace(w: Wrap, f: Face): Wrap {
  const on = w.faces.includes(f);
  if (on && w.faces.length === 1) return w;
  return { ...w, faces: FACES.filter((x) => (x === f ? !on : w.faces.includes(x))) };
}

/** How far the layers reach out on each face (m): what they add to the column's size. */
export function reach(wraps: Wrap[]): Record<Face, number> {
  const o = { '+x': 0, '-x': 0, '+y': 0, '-y': 0 } as Record<Face, number>;
  for (const w of wraps) for (const f of w.faces) o[f] += w.thickness;
  return o;
}
