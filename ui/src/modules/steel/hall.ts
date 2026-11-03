/**
 * The hall configurator's own logic (decision 270), kept apart from its dialog so it is tested: the bays as the user types
 * them ("6, 6, 7.5" or "5 × 6"), the braced bays, the hall a member belongs to, the preview's polygons fitted to a box, and
 * the summary's rows (each label a phrase the interface translates; the connections, their bolts and anchors when it has some).
 */

import type { ModelState } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import type { NodeParamsDto } from './nodes';

export interface HaunchDto { length: number; depth: number; kind?: 'cut' | 'plates'; web?: number; flange?: number; width?: number; own?: boolean }

/** A hall's spec as the engine keeps it on the hall (`HallSpec`). */
export interface HallAttrs {
  x?: number; y?: number; angle?: number;
  span?: number; bays?: number[]; eaves?: number; pitch?: number; roof?: 'duo' | 'mono';
  column?: unknown; rafter?: unknown; eavesHaunch?: HaunchDto | null; apexHaunch?: HaunchDto | null;
  gable?: 'frame' | 'posts'; postSpacing?: number; post?: unknown; gableRafter?: unknown;
  base?: 'pinned' | 'fixed'; purlinSpacing?: number; railSpacing?: number; bracing?: number[]; eavesBeam?: boolean;
  /** The frames' connections (decision 273): on or off, the anchors' kind, and each kind's parameters (none: by the profiles). */
  nodes?: boolean; anchor?: 'straight' | 'hooked' | 'plate';
  nodeParams?: { base?: NodeParamsDto; eaves?: NodeParamsDto; apex?: NodeParamsDto; post?: NodeParamsDto };
  style?: string;
}

export interface PreviewItem { role: 'column' | 'rafter' | 'haunch' | 'post' | 'node'; pts: number[] }
export interface HallSummary {
  frames: number; span: number; length: number; area: number; eaves: number; apex: number;
  columns: number; rafters: number; posts: number;
  weights: { column: number; rafter: number; haunch: number; post: number; node?: number; total: number };
  perArea: number;
  nodes?: number; plates?: number; bolts?: number; anchors?: number;
}
export interface HallPreview {
  problem?: string | null;
  attrs?: HallAttrs;
  frame?: PreviewItem[];
  frameNodes?: PreviewItem[];
  gable?: PreviewItem[];
  plan?: PreviewItem[];
  axes?: { tag: string; a: [number, number]; b: [number, number] }[];
  summary?: HallSummary;
}

const trim = (n: number) => String(Math.round(n * 1000) / 1000);

/** The bays as text: equal ones as "5 × 6", else listed. */
export function baysText(bays: number[] | undefined): string {
  if (!bays?.length) return '';
  return bays.every((b) => b === bays[0]) && bays.length > 1 ? `${bays.length} × ${trim(bays[0])}` : bays.map(trim).join(', ');
}

/** Bays typed: "n × b" (or "n x b", "n*b") for n equal ones, else a list by commas, spaces or semicolons (decimals with a point); null when not that. */
export function parseBays(text: string): number[] | null {
  const t = text.trim();
  const times = t.match(/^(\d+)\s*[×x*]\s*(\d+(?:[.,]\d+)?)$/i);
  if (times) {
    const n = Number(times[1]);
    const b = Number(times[2].replace(',', '.'));
    return n >= 1 && n <= 100 && b > 0 ? Array.from({ length: n }, () => b) : null;
  }
  const parts = t.split(/[,;\s]+/).filter(Boolean).map(Number);
  return parts.length > 0 && parts.every((b) => Number.isFinite(b) && b > 0) ? parts : null;
}

/** The braced bays typed ("1, 6"): whole numbers from 1, in order, once each; null when not that. */
export function parseBracing(text: string): number[] | null {
  if (!text.trim()) return [];
  const parts = text.split(/[,;\s]+/).filter(Boolean).map(Number);
  if (!parts.every((n) => Number.isInteger(n) && n >= 1)) return null;
  return [...new Set(parts)].sort((a, b) => a - b);
}

/** The hall a member, column or post belongs to (the hall aggregates its members). */
export function hallOf(model: ModelState, id: string | null | undefined): NodeDto | null {
  if (!id) return null;
  const node = model.nodes.get(id);
  if (node?.type === 'LapinSteelHall') return node;
  for (const r of model.relations.values())
    if (r.class === 'IfcRelAggregates' && r.related.includes(id)) {
      const h = model.nodes.get(r.relating);
      if (h?.type === 'LapinSteelHall') return h;
    }
  return null;
}

/** Items' points (x, y pairs) fitted into a W × H box with a margin, y up: the scale and the mapping. */
export function fit(items: PreviewItem[], extra: [number, number][], w: number, h: number, pad: number) {
  const xs: number[] = [];
  const ys: number[] = [];
  for (const it of items) for (let i = 0; i + 1 < it.pts.length; i += 2) { xs.push(it.pts[i]); ys.push(it.pts[i + 1]); }
  for (const [x, y] of extra) { xs.push(x); ys.push(y); }
  if (!xs.length) return null;
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const k = Math.min((w - 2 * pad) / Math.max(1e-6, x1 - x0), (h - 2 * pad) / Math.max(1e-6, y1 - y0));
  return {
    k,
    X: (x: number) => (w - (x1 - x0) * k) / 2 + (x - x0) * k,
    Y: (y: number) => (h + (y1 - y0) * k) / 2 - (y - y0) * k,
  };
}

/** The summary's rows: [label, value], the weights in kg (and t over a tonne). */
export function summaryRows(s: HallSummary, length: (m: number) => string): [string, string][] {
  const kg = (v: number) => (v >= 1000 ? `${(v / 1000).toFixed(2)} t` : `${Math.round(v)} kg`);
  const rows: [string, string][] = [
    ['Frames', String(s.frames)],
    ['Span', length(s.span)],
    ['Length', length(s.length)],
    ['Eaves height', length(s.eaves)],
    ['Apex height', length(s.apex)],
    ['Columns', `${s.columns} · ${kg(s.weights.column)}`],
    ['Rafters', `${s.rafters} · ${kg(s.weights.rafter)}`],
    ['Haunches', kg(s.weights.haunch)],
  ];
  if (s.posts > 0) rows.push(['Gable posts', `${s.posts} · ${kg(s.weights.post)}`]);
  if (s.nodes) rows.push(['Connections', `${s.nodes} · ${kg(s.weights.node ?? 0)}`], ['Bolts', String(s.bolts ?? 0)], ['Anchors', String(s.anchors ?? 0)]);
  rows.push(['Total steel', kg(s.weights.total)], ['Per m² of floor', `${s.perArea.toFixed(1)} kg/m²`]);
  return rows;
}
