/**
 * The Constraints tools (stage C3 of decision 520): one CAD command for each kind of sketch constraint and the smart
 * Dimension. Each asks for its picks in the 2D view (a point, an edge, a curve: the part nearest the click, lit as the cursor
 * moves), applies when the picks make a whole pattern of the kind, and stays on for the next, as FreeCAD's and Inventor's do;
 * Escape ends it. A dimension then asks for its value: a number (with its unit), `=formula`, Enter for what it measures now.
 */

import { ArrowRightLeft, ArrowUpDown, CornerDownRight, Crosshair, Dot, Equal, FlipHorizontal2, Lock, Menu, MoveHorizontal, MoveVertical, Spline, SquareDot } from 'lucide-react';
import type { ToolDef, Vec2 } from '../../platform/module';
import { execute, useSession } from '../../engine/session';
import { lengthText, parseLength } from '../../engine/unitsStore';
import { AngleIcon, DiameterIcon, DimensionIcon, RadiusIcon } from '../../icons';
import {
  candidatesAt, choose, dimensionFor, dimensionIsFinal, dimensionText, isComplete, isFinal, measure, parseDimensionInput, partLabel, pointOf, segmentOf, wanted,
  PATTERNS, type Candidate, type Ref, type RefKind,
} from '../../viewport/cad/constraints';
import { cadHost, cadLog, registerCommand, startCommand, useCad, type Step } from '../../viewport/cad/session';
import type { Shape } from '../../viewport/cad/shapes';

/** A pick made: the part, what it is, and the shape it is on (for measuring and lighting). */
interface Pick { ref: Ref; kind: RefKind; shape: Shape; at?: Vec2 }

interface ToolKind { kind: string; label: string; icon: ToolDef['icon']; command: string; shortcut: string }

/** The geometric constraints, in the ribbon's order. */
export const GEOMETRIC: ToolKind[] = [
  { kind: 'coincident', label: 'Coincident', icon: Dot, command: 'CNCOINCIDENT', shortcut: 'CNC' },
  { kind: 'pointOn', label: 'Point on object', icon: Crosshair, command: 'CNPOINTON', shortcut: 'CNO' },
  { kind: 'horizontal', label: 'Horizontal', icon: MoveHorizontal, command: 'CNHORIZONTAL', shortcut: 'CNH' },
  { kind: 'vertical', label: 'Vertical', icon: MoveVertical, command: 'CNVERTICAL', shortcut: 'CNV' },
  { kind: 'parallel', label: 'Parallel', icon: Menu, command: 'CNPARALLEL', shortcut: 'CNL' },
  { kind: 'perpendicular', label: 'Perpendicular', icon: CornerDownRight, command: 'CNPERPENDICULAR', shortcut: 'CNP' },
  { kind: 'tangent', label: 'Tangent', icon: Spline, command: 'CNTANGENT', shortcut: 'CNT' },
  { kind: 'equal', label: 'Equal', icon: Equal, command: 'CNEQUAL', shortcut: 'CNE' },
  { kind: 'symmetric', label: 'Symmetric', icon: FlipHorizontal2, command: 'CNSYMMETRIC', shortcut: 'CNS' },
  { kind: 'midpoint', label: 'Midpoint', icon: SquareDot, command: 'CNMIDPOINT', shortcut: 'CNM' },
  { kind: 'fix', label: 'Fix', icon: Lock, command: 'CNFIX', shortcut: 'CNF' },
];
/** The dimensions: the smart one first, then each kind by itself. */
export const DIMENSIONS: ToolKind[] = [
  { kind: 'dimension', label: 'Dimension', icon: DimensionIcon, command: 'CDIM', shortcut: 'CDI' },
  { kind: 'distanceX', label: 'Horizontal distance', icon: ArrowRightLeft, command: 'CNDISTX', shortcut: 'CDX' },
  { kind: 'distanceY', label: 'Vertical distance', icon: ArrowUpDown, command: 'CNDISTY', shortcut: 'CDY' },
  { kind: 'radius', label: 'Radius', icon: RadiusIcon, command: 'CNRADIUS', shortcut: 'CDR' },
  { kind: 'diameter', label: 'Diameter', icon: DiameterIcon, command: 'CNDIAMETER', shortcut: 'CDD' },
  { kind: 'angle', label: 'Angle', icon: AngleIcon, command: 'CNANGLE', shortcut: 'CDA' },
];

const label = (kind: string) => [...GEOMETRIC, ...DIMENSIONS].find((t) => t.kind === kind)?.label ?? kind;

/** Why a pick was not taken, in the command line. */
const refuse = (kind: string, picks: readonly Pick[]) => cadLog(`${label(kind)} takes ${wanted(kind, picks.map((p) => p.kind)) || 'nothing more'}: nothing like that is drafted there.`);

/** Every shape within reach of a click, each with the parts the click may mean. */
function foundAt(at: Vec2): { shape: string; candidates: Candidate[]; geometry: Shape }[] {
  const host = cadHost();
  if (!host) return [];
  const tol = 8 / Math.max(host.pxPerMetre?.() ?? 100, 1e-6);
  const hits = host.pickDrafts?.(at) ?? (host.pickDraft(at) ? [{ ...host.pickDraft(at)!, d: 0 }] : []);
  return hits.map((h) => ({ shape: h.id, candidates: candidatesAt(h.shape, at, tol), geometry: h.shape }));
}

/** What a click at `at` would pick for `kind` after `picks`, or null. */
function pickAt(kind: string, picks: readonly Pick[], at: Vec2, allowed?: (kinds: RefKind[]) => boolean): Pick | null {
  const found = foundAt(at);
  const done = picks.map((p) => ({ ref: p.ref, kind: p.kind }));
  for (const f of found) for (const c of f.candidates) {
    if (done.some((p) => p.ref.shape === f.shape && p.ref.part === c.part)) continue;
    const kinds = [...done.map((d) => d.kind), c.kind];
    const fits = PATTERNS[kind] ? choose(kind, done, [{ shape: f.shape, candidates: [c] }]) : allowed?.(kinds) ? { ref: { shape: f.shape, part: c.part }, kind: c.kind, at: c.at } : null;
    if (fits) return { ref: fits.ref, kind: fits.kind, shape: f.geometry, at: c.at };
  }
  return null;
}

/** What a pick looks like lit: a ring on a point, the edge, the curve, the polyline. */
function lit(p: Pick, scale: number): Shape[] {
  if (p.kind === 'point') { const at = pointOf(p.shape, p.ref.part); return at ? [{ type: 'circle', c: at, r: 5 / scale }] : []; }
  if (p.kind === 'line') { const s = segmentOf(p.shape, p.ref.part); return s ? [{ type: 'line', a: s[0], b: s[1] }] : []; }
  return [p.shape];
}

/** Where a click means: the aimed cursor the preview lit (snapped as the view snaps it), else the raw click. */
const where = (raw: Vec2): Vec2 => useCad.getState().cursor ?? raw;

const scaleOf = () => Math.max(cadHost()?.pxPerMetre?.() ?? 100, 1e-6);

/** Applies a constraint; its refusal (a conflict, a name) goes to the command line too. */
async function apply(kind: string, picks: readonly Pick[], extra: { value?: number; formula?: string; driving?: boolean } = {}): Promise<boolean> {
  const host = cadHost();
  if (!host) return false;
  const r = await execute('studio.addConstraint', { viewId: host.viewId, kind, refs: picks.map((p) => p.ref), ...extra });
  if (!r) { const e = useSession.getState().error; if (e) cadLog(e); return false; }
  return true;
}

/** The step every constraint tool is: pick, pick, apply, again. */
function constrain(kind: string): Step {
  const picks: Pick[] = [];
  const next = (): Step => ({
    prompt: `${label(kind)}: pick ${wanted(kind, picks.map((p) => p.kind))} (Esc ends)`, want: 'pick',
    preview: (c) => {
      const s = scaleOf(), h = pickAt(kind, picks, c);
      return [...picks, ...(h ? [h] : [])].flatMap((p) => lit(p, s));
    },
    labels: (c) => { const h = pickAt(kind, picks, c); return h ? [{ at: c, text: partLabel(h.kind, h.ref.part) }] : []; },
    pick: async (_hit, at) => {
      const p = pickAt(kind, picks, where(at));
      if (!p) { refuse(kind, picks); return next(); }
      picks.push(p);
      if (isFinal(kind, picks.map((x) => x.kind))) { await apply(kind, picks); return constrain(kind); }
      return next();
    },
    // A pattern that may still grow but is whole (a point's horizontal distance): Enter takes it.
    enter: async () => { if (picks.length && isComplete(kind, picks.map((x) => x.kind))) { await apply(kind, picks); return constrain(kind); } return null; },
  });
  return next();
}

const lenText = (m: number) => lengthText(m);

/** The value a dimension asks for, once its picks are whole. */
function valueStep(kind: string, picks: Pick[], alternatives: string[], smart = false): Step {
  const geometry = picks.map((p) => ({ shape: p.shape, part: p.ref.part }));
  const measured = measure(kind, geometry);
  const shown = measured == null ? '?' : dimensionText({ kind, value: measured, driving: true }, lenText);
  const alt = alternatives.map((k) => ({ distanceX: 'X', distanceY: 'Y', diameter: 'D', radius: 'R' } as Record<string, string>)[k]).filter(Boolean).join('/');
  const make = async (extra: { value?: number; formula?: string; driving?: boolean }) => { await apply(kind, picks, extra); return smart ? dimension() : dimensionOf(kind); };
  const again = (k: string): Step => valueStep(k, picks, [kind, ...alternatives].filter((x) => x !== k), smart);
  return {
    prompt: `${label(kind)} ${shown}: type a value, =formula, or Enter to keep it [Driven${alt ? `/${alt}` : ''}]`, want: 'text',
    text: async (t) => {
      const word = t.trim().toUpperCase();
      const swap = { X: 'distanceX', Y: 'distanceY', D: 'diameter', R: 'radius' }[word];
      if (swap && (alternatives.includes(swap) || swap === kind)) return again(swap);
      if (word === 'DRIVEN' || word === 'REF') return measured == null ? null : make({ value: measured, driving: false });
      const r = parseDimensionInput(t, kind, parseLength);
      if ('error' in r) { cadLog(r.error); return valueStep(kind, picks, alternatives, smart); }
      return make('formula' in r ? { formula: r.formula } : { value: r.value });
    },
    enter: () => (measured == null ? null : make({ value: measured })),
    preview: () => picks.flatMap((p) => lit(p, scaleOf())),
  };
}

/** The smart Dimension (FreeCAD's, Inventor's): what is picked says which dimension it is. */
function dimension(): Step {
  const picks: Pick[] = [];
  const kinds = () => picks.map((p) => p.kind);
  const fine = (k: RefKind[]) => (k.length === 1 ? k[0] !== 'shape' : dimensionFor(k) != null);
  const done = (): Step => { const d = dimensionFor(kinds()); return d ? valueStep(d.kind, picks, d.alternatives, true) : dimension(); };
  const next = (): Step => ({
    prompt: picks.length ? 'Dimension: pick a second point or line, or Enter for the length (Esc ends)' : 'Dimension: pick a point, a line or a circle (Esc ends)', want: 'pick',
    preview: (c) => { const s = scaleOf(), h = pickAt('dimension', picks, c, fine); return [...picks, ...(h ? [h] : [])].flatMap((p) => lit(p, s)); },
    labels: (c) => { const h = pickAt('dimension', picks, c, fine); return h ? [{ at: c, text: partLabel(h.kind, h.ref.part) }] : []; },
    pick: (_hit, at) => {
      const p = pickAt('dimension', picks, where(at), fine);
      if (!p) { cadLog('Dimension takes a point, a line, a circle or an arc: nothing like that is drafted there.'); return next(); }
      picks.push(p);
      return dimensionIsFinal(kinds()) ? done() : next();
    },
    enter: () => (picks.length && dimensionFor(kinds()) ? done() : null),
  });
  return next();
}

/** A dimension of one kind: its own picks, then the value. */
function dimensionOf(kind: string): Step {
  const picks: Pick[] = [];
  const kindsOf = () => picks.map((p) => p.kind);
  const next = (): Step => ({
    prompt: `${label(kind)}: pick ${wanted(kind, kindsOf())} (Esc ends)`, want: 'pick',
    preview: (c) => { const s = scaleOf(), h = pickAt(kind, picks, c); return [...picks, ...(h ? [h] : [])].flatMap((p) => lit(p, s)); },
    labels: (c) => { const h = pickAt(kind, picks, c); return h ? [{ at: c, text: partLabel(h.kind, h.ref.part) }] : []; },
    pick: (_hit, at) => {
      const p = pickAt(kind, picks, where(at));
      if (!p) { refuse(kind, picks); return next(); }
      picks.push(p);
      return isFinal(kind, kindsOf()) ? valueStep(kind, picks, []) : next();
    },
    enter: () => (picks.length && isComplete(kind, kindsOf()) ? valueStep(kind, picks, []) : null),
  });
  return next();
}

registerCommand({ names: ['CDIM', 'SKDIM'], label: 'Dimension', start: dimension });
for (const t of DIMENSIONS.slice(1)) registerCommand({ names: [t.command], label: t.label, start: () => dimensionOf(t.kind) });
for (const t of GEOMETRIC) registerCommand({ names: [t.command], label: t.label, start: () => constrain(t.kind) });

/** The ribbon's tools. */
export const constraintTool = (t: ToolKind): ToolDef => ({ id: `studio.constrain.${t.kind}`, label: t.label, icon: t.icon, shortcut: t.shortcut, run: () => void startCommand(t.command) });
/** Whether a constraint command is running (the overlay lights the sketch's state, and glyphs stop taking clicks). */
export const constraintCommandOn = (): boolean => { const c = useCad.getState().command; return !!c && (c === 'CDIM' || c.startsWith('CN')); };
