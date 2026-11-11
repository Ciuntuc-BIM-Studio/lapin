/**
 * The hall configurator's own logic (decision 270), kept apart from its dialog so it is tested: the bays as the user types
 * them ("6, 6, 7.5" or "5 × 6"), the braced bays, the hall a member belongs to, the preview's polygons fitted to a box, and
 * the summary's rows (each label a phrase the interface translates; the connections, their bolts and anchors when it has some;
 * the secondary steel by kind, decision 275), and the profiles each kind of secondary member is offered.
 */

import type { ModelState } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import type { NodeParamsDto } from './nodes';
import { formatLength, parseLength, unit, type Unit } from '../../engine/unitsStore';

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
  nodeParams?: { base?: NodeParamsDto; eaves?: NodeParamsDto; apex?: NodeParamsDto; post?: NodeParamsDto; cleat?: NodeParamsDto; gusset?: NodeParamsDto };
  /** The secondary steel (decision 275): on or off, the purlins (profile, overhang, a bay or continuous), the rails, the bracing's section and planes, the eaves beam's profile, the fly braces. */
  secondary?: boolean; purlin?: unknown; purlinOverhang?: number; purlinRun?: 'bay' | 'continuous';
  rail?: unknown; railBottom?: number; brace?: unknown; roofBracing?: boolean; wallBracing?: boolean; eavesBeamProfile?: unknown; flyBraces?: boolean;
  style?: string;
}

export type PreviewRole = 'column' | 'rafter' | 'haunch' | 'post' | 'node' | 'purlin' | 'rail' | 'eaves' | 'brace' | 'fly';
export interface PreviewItem { role: PreviewRole; pts: number[] }
export interface HallSummary {
  frames: number; span: number; length: number; area: number; eaves: number; apex: number;
  columns: number; rafters: number; posts: number;
  weights: { column: number; rafter: number; haunch: number; post: number; node?: number; purlin?: number; rail?: number; eaves?: number; brace?: number; fly?: number; total: number };
  perArea: number;
  nodes?: number; plates?: number; bolts?: number; anchors?: number;
  purlins?: number; rails?: number; eavesBeams?: number; braces?: number; flyBraces?: number;
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

/**
 * One bay typed, in the project's unit or with its own (6000, 6m, 600cm, 20'); a bare number too small to be a bay in that unit
 * (under a metre: "6" in millimetres) is read as metres, as bays were typed before the unit was shown (tester's question 7).
 */
function bayLength(text: string, u: Unit): number | null {
  const v = parseLength(text, u);
  if (v == null || !(v > 0)) return null;
  if (v < 1 && /^\d+(?:\.\d+)?$/.test(text.trim())) return Number(text.trim());
  return v;
}

/** The bays as text in the project's unit (no label): equal ones as "5 × 6000", else listed. */
export function baysText(bays: number[] | undefined, u: Unit = unit()): string {
  if (!bays?.length) return '';
  return bays.every((b) => b === bays[0]) && bays.length > 1 ? `${bays.length} × ${formatLength(bays[0], u)}` : bays.map((b) => formatLength(b, u)).join(', ');
}

/** Bays typed: "n × b" (or "n x b", "n*b") for n equal ones, else a list by commas or semicolons (spaces too, but not in imperial); in metres, null when not that. */
export function parseBays(text: string, u: Unit = unit()): number[] | null {
  const t = text.trim();
  const times = t.match(/^(\d+)\s*[×x*]\s*(.+)$/i);
  if (times) {
    const n = Number(times[1]);
    const b = bayLength(times[2], u);
    return n >= 1 && n <= 100 && b != null ? Array.from({ length: n }, () => b) : null;
  }
  const imperial = u === 'ft-in' || u === 'in' || /['"″′]/.test(t);
  const parts = t.split(imperial ? /[,;]+/ : /[,;\s]+/).map((x) => x.trim()).filter(Boolean).map((x) => bayLength(x, u));
  return parts.length > 0 && parts.every((b) => b != null) ? parts as number[] : null;
}

/** The hall made `length` long with as many bays as it has, each equal (the Length field); null when not a length. */
export function baysForLength(bays: number[] | undefined, length: number | null): number[] | null {
  const n = Math.max(1, bays?.length ?? 1);
  if (length == null || !(length > 0)) return null;
  const each = Math.round(length / n * 10_000) / 10_000;
  return Array.from({ length: n }, () => each);
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
  // The secondary steel, each kind that has some.
  const secondary: [string, number | undefined, number | undefined][] = [
    ['Purlins', s.purlins, s.weights.purlin], ['Side rails', s.rails, s.weights.rail], ['Eaves beam', s.eavesBeams, s.weights.eaves],
    ['Bracing', s.braces, s.weights.brace], ['Fly braces', s.flyBraces, s.weights.fly],
  ];
  for (const [label, n, w] of secondary) if (n) rows.push([label, `${n} · ${kg(w ?? 0)}`]);
  if (s.nodes) rows.push(['Connections', `${s.nodes} · ${kg(s.weights.node ?? 0)}`], ['Bolts', String(s.bolts ?? 0)], ['Anchors', String(s.anchors ?? 0)]);
  rows.push(['Total steel', kg(s.weights.total)], ['Per m² of floor', `${s.perArea.toFixed(1)} kg/m²`]);
  return rows;
}

/** A profile as the catalogue lists it (its kind and name). */
interface Named { kind: string; name?: string }

/**
 * The profiles each kind of secondary member is offered: cold-formed (and channels, I) for purlins, rails and the eaves
 * beam; round bars, angles and tubes for the bracing. A round bar is a section of its own, named by its diameter.
 */
export function secondaryProfiles(kind: 'purlin' | 'rail' | 'eaves' | 'brace', catalogue: Named[]): string[] {
  if (kind === 'brace') return [...ROD_SIZES.map(rodName), ...catalogue.filter((p) => p.kind === 'L' || p.kind === 'CHS').map((p) => p.name ?? '')];
  const cold = catalogue.filter((p) => p.kind === 'Z' || p.kind === 'C' || p.kind === 'SIGMA');
  const hot = catalogue.filter((p) => p.kind === 'U' || (p.kind === 'I' && /^IPE (200|240)$/.test(p.name ?? '')) || p.kind === 'RHS');
  return [...cold, ...hot].map((p) => p.name ?? '');
}

/** Round bars offered for bracing, metres. */
export const ROD_SIZES = [0.016, 0.02, 0.024, 0.027, 0.03];
export const rodName = (d: number) => `Ø${Math.round(d * 1000)}`;

/** A secondary profile as the spec keeps it: a round bar as its section, any other by its catalogue name. */
export function secondaryValue(name: string): unknown {
  const rod = /^Ø(\d+)$/.exec(name);
  return rod ? { kind: 'circle', width: Number(rod[1]) / 1000, depth: Number(rod[1]) / 1000 } : name;
}

/** The name a spec's profile goes by in the pickers: a catalogue name, or a round bar's diameter. */
export function secondaryName(v: unknown): string {
  if (typeof v === 'string') return v;
  const s = v as { kind?: string; width?: number; name?: string } | undefined;
  if (s?.kind === 'circle' && s.width) return rodName(s.width);
  return s?.name ?? s?.kind ?? '';
}
