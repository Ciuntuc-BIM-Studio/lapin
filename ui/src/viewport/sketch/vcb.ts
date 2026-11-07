/**
 * The value typed while sketching in 3D, as SketchUp's Measurements box
 * (decision 374): typed at any time, no box clicked first, Enter applies it.
 * `1200` a length (the line's next segment along the way the cursor points, a
 * circle's radius), `1200,600` a rectangle's width and height (also `1200x600`
 * or `1200;600`), `r300` a radius, `<30` an angle from the red axis (or the
 * last segment); each length in the project's unit unless it carries its own.
 */

export type Vcb =
  | { kind: 'length'; value: number }
  | { kind: 'pair'; a: number; b: number }
  | { kind: 'radius'; value: number }
  | { kind: 'angle'; deg: number }
  | { kind: 'lengthAngle'; value: number; deg: number };

/** What a typed text means, or null while it means nothing yet (an empty or half-typed value). */
export function parseVcb(text: string, parse: (s: string) => number | null): Vcb | null {
  const t = text.trim();
  if (!t) return null;
  const r = /^r\s*(.+)$/i.exec(t);
  if (r) { const v = parse(r[1].trim()); return v != null && v > 0 ? { kind: 'radius', value: v } : null; }
  const ang = /^<\s*(-?\d+(?:[.,]\d+)?)\s*°?$/.exec(t);
  if (ang) return { kind: 'angle', deg: Number(ang[1].replace(',', '.')) };
  const la = /^(.+?)\s*<\s*(-?\d+(?:[.,]\d+)?)\s*°?$/.exec(t);
  if (la) { const v = parse(la[1].trim()); return v != null && v !== 0 ? { kind: 'lengthAngle', value: v, deg: Number(la[2].replace(',', '.')) } : null; }
  // A pair: two values with a comma (or ; x × *) between them, as SketchUp's (the decimal mark is a point there).
  const parts = t.split(/\s*[,;×*]\s*|\s*(?<=[\d.'"a-z])x(?=[\s\d.])\s*/i).filter(Boolean);
  if (parts.length === 2) {
    const a = parse(parts[0]), b = parse(parts[1]);
    return a != null && b != null && a !== 0 && b !== 0 ? { kind: 'pair', a, b } : null;
  }
  if (parts.length > 2) return null;
  const v = parse(parts.length === 1 ? parts[0] : t);
  if (v != null && v !== 0) return { kind: 'length', value: v };
  return null;
}
