/**
 * The space under a stair (a tester's report, #71), apart from React: what the configurator keeps as its draft (a style
 * picked, the fields changed on top of it, where it stands), the arguments it sends the engine, and how it fits the
 * engine's elevation and plan into its drawings.
 */

/** What fills the space, as the engine's UnderStairSpec has it (metres). */
export interface UnderStairSpec {
  name: string; front: 'doors' | 'panel' | 'open'; doorWidth: number; shelves: boolean; shelfSpacing: number;
  bookcase: boolean; bookcaseWidth: number; feature: 'none' | 'desk' | 'bench' | 'mudroom' | 'playhouse' | 'dogbed'; featureWidth: number;
  benchHeight: number; cushion: boolean; deskHeight: number; light: boolean; hookHeight: number; hookSpacing: number;
  openingWidth: number; openingHeight: number; window: boolean; thickness: number; carcass: string; fronts: string; fabric: string;
}

/** Where it stands under its stair: the flight (from 1), the open side, from and to along the flight, how deep. */
export interface UnderStairPlace { side?: 'left' | 'right' | null; flight?: number | null; start?: number | null; end?: number | null; depth?: number | null }

export type Role = 'stair' | 'carcass' | 'front' | 'fabric' | 'metal' | 'light';
export interface PreviewItem { role: Role; key?: string; pts: number[] }
export interface UnderStairPreview {
  problem?: string | null;
  spec?: UnderStairSpec;
  flights?: number;
  elevation?: PreviewItem[];
  plan?: PreviewItem[];
  space?: { start: number; end: number; run: number; depth: number; width: number; low: number; high: number; side: 'left' | 'right'; flight: number };
  parts?: { key: string; name: string; role: Role; count: number }[];
  notes?: string[];
}

/** The configurator's draft: a style picked (ready-made or the project's), the spec's fields changed by hand, the placement's. */
export interface Draft { style?: string; spec: Partial<UnderStairSpec>; place: UnderStairPlace }

export const emptyDraft = (): Draft => ({ spec: {}, place: {} });

export const isDirty = (d: Draft) => Boolean(d.style) || Object.keys(d.spec).length > 0 || Object.keys(d.place).length > 0;

/**
 * What the engine is asked with: a style and the fields changed over it; else the whole spec as it stands (the element's or
 * the last preview's) with the changes, so a field left alone keeps its value; and the placement's fields changed.
 */
export function draftArgs(d: Draft, current: UnderStairSpec | undefined): Record<string, unknown> {
  const spec = d.style ? d.spec : current ? { ...current, ...d.spec } : d.spec;
  return { ...(d.style ? { style: d.style } : {}), ...(Object.keys(spec).length ? { spec } : {}), ...d.place };
}

/** A value of the spec as the draft shows it: changed by hand, else as the preview resolved it. */
export const specValue = <K extends keyof UnderStairSpec>(d: Draft, preview: UnderStairPreview | null, k: K): UnderStairSpec[K] | undefined =>
  (k in d.spec ? d.spec[k] : preview?.spec?.[k]) as UnderStairSpec[K] | undefined;

/** Which fields a feature asks for. */
export function featureFields(feature: UnderStairSpec['feature']): (keyof UnderStairSpec)[] {
  switch (feature) {
    case 'desk': return ['featureWidth', 'deskHeight', 'light'];
    case 'bench': return ['featureWidth', 'benchHeight', 'cushion', 'light'];
    case 'mudroom': return ['featureWidth', 'benchHeight', 'cushion', 'hookHeight', 'hookSpacing'];
    case 'playhouse': return ['featureWidth', 'openingWidth', 'openingHeight', 'window'];
    case 'dogbed': return ['featureWidth', 'openingWidth', 'openingHeight'];
    default: return [];
  }
}

/** Fits drawings' points (x y pairs) into a w × h box with a margin, y up. */
export function fit(items: { pts: number[] }[], w: number, h: number, pad: number) {
  const xs: number[] = [];
  const ys: number[] = [];
  for (const it of items) for (let i = 0; i + 1 < it.pts.length; i += 2) { xs.push(it.pts[i]); ys.push(it.pts[i + 1]); }
  if (!xs.length) return null;
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const k = Math.min((w - 2 * pad) / Math.max(1e-6, x1 - x0), (h - 2 * pad) / Math.max(1e-6, y1 - y0));
  return { k, X: (x: number) => (w - (x1 - x0) * k) / 2 + (x - x0) * k, Y: (y: number) => (h + (y1 - y0) * k) / 2 - (y - y0) * k };
}

/** A part's pieces in a line ("Shelves × 6"), the doors counted as one line. */
export function partLines(parts: NonNullable<UnderStairPreview['parts']>): [string, number][] {
  const out = new Map<string, number>();
  for (const p of parts) {
    const name = p.key.startsWith('nook-door:') && p.key !== 'nook-door:play' ? 'Doors' : p.name;
    out.set(name, (out.get(name) ?? 0) + p.count);
  }
  return [...out];
}
