/** The part's tree as `studio.partTree` answers it, and what the panel shows of it (pure, tested in editMode.test.ts). */

export type PartPlane = 'xy' | 'xz' | 'yz';
export type PartParamKind = 'length' | 'number' | 'angle' | 'bool' | 'text' | 'integer';

export interface TreeFrame { origin: number[]; right: number[]; up: number[]; normal: number[] }
export interface TreeSketch {
  id: string; role?: string; viewId?: string; plane: string; shapeIds: string[]; frame?: TreeFrame; points?: unknown[]; name?: string; missing?: boolean;
  /** A sketch of the part's own (`studio.constrainPart`, `studio.addPartSketch`), edited in place: its shapes (solved for this object, `{id, type, …}`), constraints and how it stands. */
  local?: boolean; shapes?: Record<string, unknown>[]; constraints?: unknown[]; constraintCount?: number; status?: string; dof?: number;
  freeShapes?: { shape: string; part: string }[]; redundant?: string[]; conflicting?: string[]; problem?: string;
  /** The names of the bodies drawn from it. */
  bodies?: string[];
  /**
   * The sketch as the part stores it (solved with the part's defaults): what editing the sketch edits, the definition. Its shapes,
   * constraints, how it stands and its frame for the defaults; the same as the fields above when the object's values are the defaults.
   */
  stored?: StoredSketch;
}
export interface StoredSketch {
  shapes?: Record<string, unknown>[]; constraints?: unknown[]; constraintCount?: number; status?: string; dof?: number;
  freeShapes?: { shape: string; part: string }[]; redundant?: string[]; conflicting?: string[]; problem?: string; frame?: TreeFrame;
}
export interface TreeStep { index: number; kind: string; label: string; suppressed?: boolean; when?: string | number; sketch?: string; feature?: number }
export interface TreeParam {
  key: string; label: string; kind: PartParamKind; default: number; value: number; instance: boolean; published: boolean;
  unit?: string; group?: string; description?: string; formula?: string; derived?: boolean; min?: number; max?: number;
}
export interface PartTree {
  id: string; name: string; kind: 'formula' | 'recipe'; partTypeId?: string; planes: PartPlane[];
  sketches: TreeSketch[]; steps: TreeStep[]; params: TreeParam[];
}

/** The origin's three planes as the panel names them. */
export const PLANE_LABELS: Record<PartPlane, string> = { xy: 'Top plane (xy)', xz: 'Front plane (xz)', yz: 'Right plane (yz)' };

export interface SketchRow { id: string; label: string; viewId?: string; plane: string; opens: 'sketch' | 'view' | 'body' | 'none'; missing: boolean; local: boolean; bodies: string[]; dof: number | null }
export interface StepRow {
  index: number; label: string; kind: string; suppressed: boolean; when: string | null;
  /** The local sketch a body step is drawn from. */
  sketch: string | null;
  /** The feature's own index (updateFeature, moveFeature, removeFeature); null for a step the part is made by (its operation, its bodies). */
  feature: number | null;
  canUp: boolean; canDown: boolean; canRemove: boolean; canToggle: boolean;
}
export interface ParamRow { key: string; label: string; kind: PartParamKind; value: number; default: number; unit: string; changed: boolean; instance: boolean; published: boolean; group: string; derived: boolean; formula: string }

/** A recipe sketch opens on its view; a formula part's body opens in the part's editor; one with neither is only listed. */
export function sketchRows(tree: PartTree): SketchRow[] {
  return tree.sketches.map((s) => ({
    id: s.id,
    label: s.name ?? s.role ?? s.id,
    viewId: s.viewId,
    plane: s.plane,
    opens: s.local ? 'sketch' : tree.kind === 'formula' ? 'body' : s.viewId && !s.missing ? 'view' : 'none',
    missing: !!s.missing,
    local: !!s.local,
    bodies: s.bodies ?? [],
    dof: s.local && typeof s.dof === 'number' ? s.dof : null,
  }));
}

/** The steps in order; a feature may move within the features only, never past its operation or its bodies. */
export function stepRows(tree: PartTree): StepRow[] {
  const features = tree.steps.filter((s) => s.feature != null);
  const first = features[0]?.feature ?? 0;
  const last = features.length ? (features[features.length - 1].feature ?? 0) : 0;
  return tree.steps.map((s) => {
    const f = s.feature ?? null;
    return {
      index: s.index, label: s.label, kind: s.kind, suppressed: !!s.suppressed,
      when: s.when == null || s.when === '' ? null : String(s.when),
      sketch: s.sketch ?? null,
      feature: f,
      canUp: f != null && f > first, canDown: f != null && f < last, canRemove: f != null, canToggle: f != null,
    };
  });
}

export const paramRows = (tree: PartTree): ParamRow[] => tree.params.map((p) => ({
  key: p.key, label: p.label, kind: p.kind, value: p.value, default: p.default, unit: p.unit ?? (p.kind === 'angle' ? '°' : ''),
  changed: Math.abs(p.value - p.default) > 1e-9, instance: p.instance, published: p.published, group: p.group ?? '', derived: !!p.derived || !!p.formula, formula: p.formula ?? '',
}));

/** The origin plane's corners (metres) in the part's frame: a square of `half` round the origin; x then y of the plane, z up. */
export function planeCorners(plane: PartPlane, half: number): [number, number, number][] {
  const c: [number, number][] = [[-half, -half], [half, -half], [half, half], [-half, half]];
  return c.map(([a, b]) => (plane === 'xy' ? [a, b, 0] : plane === 'xz' ? [a, 0, b] : [0, a, b]));
}

/** Whether *Constrain part* is offered: a formula part with a body still made of corners (`points`), not of a sketch of its own. */
export const canConstrain = (tree: PartTree | null | undefined): boolean => !!tree && tree.kind === 'formula' && tree.sketches.some((s) => !s.local && Array.isArray(s.points));

/** The planes a new sketch of the part may lie on, as the picker offers them. */
export const SKETCH_PLANES: PartPlane[] = ['xy', 'xz', 'yz'];

/** The local sketches: the ones edited in place. */
export const localSketches = (tree: PartTree | null | undefined): TreeSketch[] => (tree?.sketches ?? []).filter((s) => s.local);

/** The body steps drawn from a sketch (`sketch` is a local sketch's id), with the sketch they are drawn from. */
export const sketchBodySteps = (tree: PartTree): { step: TreeStep; sketch: TreeSketch }[] =>
  tree.steps.flatMap((step) => { const sketch = step.sketch ? tree.sketches.find((s) => s.local && s.id === step.sketch) : undefined; return sketch ? [{ step, sketch }] : []; });

/** A depth typed in the panel: a number in metres, or a formula of the parameters (`=H - 0.04` or `H - 0.04`); null when empty. */
export function depthInput(text: string, length: (s: string) => number | null): number | string | null {
  const t = text.trim();
  if (!t) return null;
  if (t.startsWith('=')) return t.slice(1).trim() || null;
  const n = length(t);
  return n != null && Number.isFinite(n) ? n : t;
}
