/**
 * A role's symbol as the engine draws it alone (MepGlyphs: paper mm, y up, its insertion point at 0, 0): lines, fills and
 * texts, as a small SVG for the symbol sets' table and the legend's preview.
 */

export interface Glyph { pts: number[]; closed: boolean; heavy?: boolean; fill?: boolean; text?: string; h?: number; angle?: number }

/** The box of what a glyph draws (mm): its points, a text's by its height. */
export function glyphBounds(g: Glyph[]): [number, number, number, number] | null {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (const it of g) {
    for (let i = 0; i + 1 < it.pts.length; i += 2) {
      x0 = Math.min(x0, it.pts[i]); x1 = Math.max(x1, it.pts[i]); y0 = Math.min(y0, it.pts[i + 1]); y1 = Math.max(y1, it.pts[i + 1]);
    }
    if (it.text && it.pts.length >= 2) { x1 = Math.max(x1, it.pts[0] + (it.h ?? 2.5) * 0.7 * it.text.length); y1 = Math.max(y1, it.pts[1] + (it.h ?? 2.5)); }
  }
  return Number.isFinite(x0) ? [x0, y0, x1, y1] : null;
}

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]!));

/** The glyph as an SVG (y flipped to the screen's), its lines in the current colour, heavy ones thicker. */
export function glyphSvg(g: Glyph[], size = 28): string {
  const b = glyphBounds(g);
  if (!b) return '';
  const pad = 0.8;
  const [x0, y0, x1, y1] = [b[0] - pad, b[1] - pad, b[2] + pad, b[3] + pad];
  const w = Math.max(x1 - x0, 0.1), h = Math.max(y1 - y0, 0.1);
  const p = (x: number, y: number) => `${(x - x0).toFixed(3)},${(y1 - y).toFixed(3)}`;
  const parts = g.map((it) => {
    if (it.text) return it.pts.length >= 2 ? `<text x="${(it.pts[0] - x0).toFixed(3)}" y="${(y1 - it.pts[1]).toFixed(3)}" font-size="${(it.h ?? 2.5).toFixed(2)}" fill="currentColor">${esc(it.text)}</text>` : '';
    const pts: string[] = [];
    for (let i = 0; i + 1 < it.pts.length; i += 2) pts.push(p(it.pts[i], it.pts[i + 1]));
    const tag = it.closed ? 'polygon' : 'polyline';
    return it.fill ? `<polygon points="${pts.join(' ')}" fill="currentColor" stroke="none"/>` : `<${tag} points="${pts.join(' ')}" fill="none" stroke="currentColor" stroke-width="${it.heavy ? 0.5 : 0.25}" stroke-linejoin="round"/>`;
  });
  const scale = size / Math.max(w, h);
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${(w * scale).toFixed(1)}" height="${(h * scale).toFixed(1)}" viewBox="0 0 ${w.toFixed(3)} ${h.toFixed(3)}">${parts.join('')}</svg>`;
}
