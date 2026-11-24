/**
 * The roof panel layout as the interface reads it (docs/plan-roof-panels.md, S2): what a roof says it is covered with
 * (`panels` on the IfcRoof), the built-in profiles (the catalogue `roofpanels.profiles`) and the layout the engine works out
 * (the query `roofpanels.layout`), with the small models the Panels section and the Panel layout dialog draw from. Nothing
 * here lays panels: the engine does, on every ask.
 */

import type { NodeDto } from '../../engine/protocol';

export interface PanelColour { code: string; name: string; rgb?: string | null }

export interface PanelProfileDto {
  id: string; name: string; maker: string; kind: string;
  grossWidth: number; usefulWidth: number; slopeLap: number; module: number; joints: string;
  onOrder: boolean; minLength: number; maxLength: number; lengths?: number[] | null; maxLengths: number[];
  colors: PanelColour[]; minPitch: number; minPitchHipValley: number; reuseOffcuts: boolean; notes: string; source: string;
}

/** What a roof names: its profile, colour and a made-to-order profile's longest panel. */
export interface PanelChoice { profile: string; color?: string | null; maxLength?: number | null }

export type Pt = [number, number];

export interface FaceDto { id: string; plane: number; pitch: number; eave: number; area: number; coverage: number; slopeLength: number; outline: Pt[]; planOutline: Pt[] }
export interface EdgeDto { kind: string; faces: string[]; length: number; lap: number; pieces: number; joints: number; a: number[]; b: number[] }
export interface PanelDto {
  id: string; face: string; column: number; row: number; length: number; buy: number; x: number; y: number;
  trimmedLength: number; grossArea: number; coverage: number; edges: string[];
  donor?: string | null; shift?: number | null; donatesTo?: string | null; plan: Pt[]; net: Pt[];
}
export interface LayoutDto {
  roofId: string; profile: PanelProfileDto; color: string | null; stock: number[];
  faces: FaceDto[]; edges: EdgeDto[]; panels: PanelDto[];
  purchase: Record<string, number>; installed: Record<string, number>;
  totals: { roofArea: number; coverage: number; sheetArea: number; panels: number; bought: number; reused: number; lengths: number; boughtLength: number; edgeLengths: Record<string, number> };
  warnings: string[];
}
export type LayoutAnswer = LayoutDto | { problem: string };

export const isLayout = (a: LayoutAnswer | null | undefined): a is LayoutDto => !!a && !('problem' in a);

/** The catalogue's profiles, as the session's info holds them. */
export function profilesOf(catalogs: Record<string, unknown> | undefined): PanelProfileDto[] {
  const c = catalogs?.['roofpanels.profiles'];
  return Array.isArray(c) ? (c as PanelProfileDto[]) : [];
}

/** What the roof names (its `panels`), or null. */
export function choiceOf(node: NodeDto | undefined): PanelChoice | null {
  const p = node?.props.panels as { profile?: unknown; color?: unknown; maxLength?: unknown } | undefined;
  if (!p || typeof p.profile !== 'string' || !p.profile) return null;
  return {
    profile: p.profile,
    color: typeof p.color === 'string' ? p.color : null,
    maxLength: typeof p.maxLength === 'number' ? p.maxLength : null,
  };
}

/**
 * The arguments of `roofpanels.setRoofPanels` for a change of the roof's choice (one undo step): another profile drops a
 * colour or longest panel it does not have; null takes the panels off.
 */
export function setArgs(roofId: string, choice: PanelChoice | null, profiles: PanelProfileDto[]): Record<string, unknown> {
  if (!choice) return { id: roofId, profile: null };
  const p = profiles.find((x) => x.id === choice.profile);
  const args: Record<string, unknown> = { id: roofId, profile: choice.profile };
  if (choice.color && (!p || p.colors.some((c) => c.code === choice.color))) args.color = choice.color;
  if (choice.maxLength && (!p || p.maxLengths.includes(choice.maxLength))) args.maxLength = choice.maxLength;
  return args;
}

/** The panels to buy by length, the longest first: each its count and metres. */
export function purchaseRows(l: Pick<LayoutDto, 'purchase'>): { length: number; count: number; metres: number }[] {
  return Object.entries(l.purchase)
    .map(([k, n]) => ({ length: Number(k), count: n, metres: (Number(k) * n) / 1000 }))
    .sort((a, b) => b.length - a.length);
}

/** One line of what to buy: 4350 mm × 20, 2600 mm × 8. */
export const purchaseText = (l: Pick<LayoutDto, 'purchase'>) => purchaseRows(l).map((r) => `${r.length} mm × ${r.count}`).join(', ');

/** Each roof plane's totals: its panels, those bought and reused, the sheet bought for it (m²). */
export function faceTotals(l: LayoutDto): { id: string; pitch: number; area: number; panels: number; bought: number; reused: number; sheet: number }[] {
  return l.faces.map((f) => {
    const mine = l.panels.filter((p) => p.face === f.id);
    const bought = mine.filter((p) => p.buy);
    return {
      id: f.id, pitch: f.pitch, area: f.area, panels: mine.length, bought: bought.length, reused: mine.length - bought.length,
      sheet: bought.reduce((s, p) => s + (p.length * l.profile.grossWidth) / 1e6, 0),
    };
  });
}

/** The edges by kind: how many, how long, how many accessory pieces; in the kinds' own order. */
export const EDGE_ORDER = ['eave', 'verge', 'ridge', 'hip', 'valley', 'topEdge', 'concaveBreak', 'convexBreak', 'wallTop', 'wallSide'];
export function edgeRows(l: Pick<LayoutDto, 'edges'>): { kind: string; count: number; length: number; pieces: number }[] {
  const by = new Map<string, { kind: string; count: number; length: number; pieces: number }>();
  for (const e of l.edges) {
    const r = by.get(e.kind) ?? { kind: e.kind, count: 0, length: 0, pieces: 0 };
    r.count++; r.length += e.length; r.pieces += e.pieces;
    by.set(e.kind, r);
  }
  const rank = (k: string) => (EDGE_ORDER.indexOf(k) + 99) % 99;
  return [...by.values()].sort((a, b) => rank(a.kind) - rank(b.kind));
}

/** The edge kinds as the interface names them. */
export const EDGE_LABEL: Record<string, string> = {
  eave: 'Eaves', verge: 'Verges', ridge: 'Ridges', hip: 'Hips', valley: 'Valleys', topEdge: 'Top edges',
  concaveBreak: 'Concave breaks', convexBreak: 'Convex breaks', wallTop: 'Wall tops', wallSide: 'Wall sides',
};

/** The edges' colours in the layout (by kind, as the app draws them). */
export const EDGE_COLOUR: Record<string, string> = {
  eave: '#2f6fb3', verge: '#7a4fb0', ridge: '#c0392b', hip: '#d35400', valley: '#16a085', topEdge: '#8e44ad',
  concaveBreak: '#27ae60', convexBreak: '#2c3e50', wallTop: '#7f8c8d', wallSide: '#95a5a6',
};

/** A reused offcut's colour, as the 3D view and the app show it. */
export const REUSED = '#e07b39';

/** A colour per panel length, the shortest lightest: blues and greens, never the offcut's orange. */
const LENGTH_PALETTE = ['#cfe3f2', '#a9cde8', '#82b6dc', '#5b9fd0', '#3f86bd', '#2d6c9e', '#7fc4a5', '#55ad87', '#3a9070', '#2b7059', '#b7b2d9', '#8f88c4'];
export function lengthColours(lengths: Iterable<number>): Map<number, string> {
  const sorted = [...new Set(lengths)].sort((a, b) => a - b);
  return new Map(sorted.map((n, i) => [n, LENGTH_PALETTE[i % LENGTH_PALETTE.length]]));
}

export const centroid = (pts: Pt[]): Pt => {
  if (pts.length === 0) return [0, 0];
  let x = 0, y = 0;
  for (const p of pts) { x += p[0]; y += p[1]; }
  return [x / pts.length, y / pts.length];
};

/** Each reused offcut's link: from the middle of the panel it is cut from to where it lies, in plan. */
export function reuseLinks(l: Pick<LayoutDto, 'panels'>): { donor: string; recipient: string; shift: number; from: Pt; to: Pt }[] {
  const by = new Map(l.panels.map((p) => [p.id, p]));
  return l.panels.filter((p) => p.donor && by.has(p.donor)).map((p) => ({
    donor: p.donor!, recipient: p.id, shift: p.shift ?? 0, from: centroid(by.get(p.donor!)!.plan), to: centroid(p.plan),
  }));
}

/** The box round points. */
export function bounds(pts: Iterable<Pt>): { x0: number; y0: number; x1: number; y1: number } {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (const [x, y] of pts) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
  return Number.isFinite(x0) ? { x0, y0, x1, y1 } : { x0: 0, y0: 0, x1: 1, y1: 1 };
}

/**
 * The roof planes laid flat for the dialog (each in true size, u along its eave, v up its slope): side by side in rows no
 * wider than about twice the widest, <paramref name="gap"/> metres apart; each plane's offset to add to its points.
 */
export function laidFlat(faces: Pick<FaceDto, 'id' | 'outline'>[], gap = 1): { id: string; dx: number; dy: number }[] {
  const boxes = faces.map((f) => ({ id: f.id, b: bounds(f.outline) }));
  const widest = Math.max(1, ...boxes.map((x) => x.b.x1 - x.b.x0));
  const rowWidth = Math.max(widest * 2, 1);
  const out: { id: string; dx: number; dy: number }[] = [];
  let x = 0, y = 0, rowHeight = 0;
  for (const { id, b } of boxes) {
    const w = b.x1 - b.x0, h = b.y1 - b.y0;
    if (x > 0 && x + w > rowWidth) { x = 0; y -= rowHeight + gap; rowHeight = 0; }
    // Rows go down the page: the next row's top under this one's bottom.
    out.push({ id, dx: x - b.x0, dy: y - b.y1 });
    x += w + gap;
    rowHeight = Math.max(rowHeight, h);
  }
  return out;
}

/** A hovered panel in a line: its code, length, and whether it is bought or which panel it is cut from. */
export function panelLine(p: PanelDto): { code: string; length: number; donor: string | null; gives: string | null } {
  return { code: p.id, length: p.length, donor: p.donor ?? null, gives: p.donatesTo ?? null };
}
