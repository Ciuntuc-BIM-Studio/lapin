/**
 * A picture laid on a 2D view (a point cloud's slice, an orthophoto) drawn only where the screen sees it (decision 399).
 * A slice of an airborne scan is hundreds of metres wide: zoomed in on a building, the whole picture laid on the screen
 * would be tens of thousands of pixels a side for the browser to lay out and paint (suspected in the renderer stuck at
 * full CPU of 08.11.2026). The picture's unit square is mapped onto the screen by its corners; what the screen holds of it
 * is cut out, so the browser never draws more than about the screen.
 */

export interface Pt { x: number; y: number }

/** The part of the picture's unit square the screen sees: u along its top edge, v down its left edge, 0–1. */
export interface Crop { u0: number; v0: number; u1: number; v1: number }

/**
 * What of a picture to draw on a screen of `w` × `h` pixels, the picture's top-left, top-right and bottom-left corners
 * given on the screen: `'all'` when it is small enough to draw whole (at most `whole` times the screen's area), null when
 * none of it is on the screen, else the part seen (with a margin of `margin` pixels, so a small pan shows no edge).
 */
export function visibleCrop(tl: Pt, tr: Pt, bl: Pt, w: number, h: number, margin = 64, whole = 4): Crop | 'all' | null {
  const ax = tr.x - tl.x, ay = tr.y - tl.y, bx = bl.x - tl.x, by = bl.y - tl.y;
  const det = ax * by - ay * bx;
  if (!Number.isFinite(det) || Math.abs(det) < 1e-9 || w <= 0 || h <= 0) return 'all';
  // The picture's area on the screen against the screen's.
  if (Math.abs(det) <= whole * w * h) {
    // Small: whole, unless it lies off the screen altogether.
    const xs = [tl.x, tr.x, bl.x, tr.x + bx], ys = [tl.y, tr.y, bl.y, tr.y + by];
    if (Math.max(...xs) < -margin || Math.min(...xs) > w + margin || Math.max(...ys) < -margin || Math.min(...ys) > h + margin) return null;
    return 'all';
  }
  // The screen's corners (with the margin) taken back into the picture's square.
  const inv = (p: Pt) => {
    const dx = p.x - tl.x, dy = p.y - tl.y;
    return { u: (dx * by - dy * bx) / det, v: (ax * dy - ay * dx) / det };
  };
  const cs = [inv({ x: -margin, y: -margin }), inv({ x: w + margin, y: -margin }), inv({ x: w + margin, y: h + margin }), inv({ x: -margin, y: h + margin })];
  const u0 = Math.max(0, Math.min(...cs.map((c) => c.u))), u1 = Math.min(1, Math.max(...cs.map((c) => c.u)));
  const v0 = Math.max(0, Math.min(...cs.map((c) => c.v))), v1 = Math.min(1, Math.max(...cs.map((c) => c.v)));
  if (u1 <= u0 || v1 <= v0) return null;
  return { u0, v0, u1, v1 };
}

/** An object URL a picture's bytes are drawn from, made once a picture (a data: URL a megabyte long would be parsed again on every render). */
const urls = new Map<string, { data: string; url: string }>();
const MAX_URLS = 32;

export function imageUrl(id: string, data: string, mime: string): string {
  const hit = urls.get(id);
  if (hit && hit.data === data) { urls.delete(id); urls.set(id, hit); return hit.url; }
  if (hit) { URL.revokeObjectURL?.(hit.url); urls.delete(id); }
  let url: string;
  try {
    const bin = atob(data);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    url = URL.createObjectURL(new Blob([bytes], { type: mime }));
  } catch {
    return `data:${mime};base64,${data}`;
  }
  urls.set(id, { data, url });
  while (urls.size > MAX_URLS) {
    const [k, v] = urls.entries().next().value!;
    URL.revokeObjectURL?.(v.url);
    urls.delete(k);
  }
  return url;
}
