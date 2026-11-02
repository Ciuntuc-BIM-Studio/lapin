/** A pad footing's steps in one field: L × W × H in the project's unit, a semicolon between steps, bottom up. */

import { formatLength, parseLength, type Unit } from '../../engine/unitsStore';

export type Step = { length: number; width: number; height: number };

export const formatSteps = (steps: Step[], u?: Unit) => steps.map((x) => `${formatLength(x.length, u)} × ${formatLength(x.width, u)} × ${formatLength(x.height, u)}`).join('; ');

/** The steps typed (each length in the project's unit or its own); null when one does not read or is not more than nothing. */
export function parseSteps(text: string, u?: Unit): Step[] | null {
  const out: Step[] = [];
  for (const piece of text.split(';').map((s) => s.trim()).filter(Boolean)) {
    const n = piece.split(/\s*[×x]\s*/i).map((s) => parseLength(s, u));
    if (n.length !== 3 || n.some((v) => v == null || !(v > 0))) return null;
    out.push({ length: n[0]!, width: n[1]!, height: n[2]! });
  }
  return out;
}
