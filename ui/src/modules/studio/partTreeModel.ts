/** The part's tree as `studio.partTree` answers it, and what the panel shows of it (pure, tested in editMode.test.ts). */

export type PartPlane = 'xy' | 'xz' | 'yz';
export type PartParamKind = 'length' | 'number' | 'angle' | 'bool';

export interface TreeSketch { id: string; role?: string; viewId?: string; plane: string; shapeIds: string[]; frame?: unknown; points?: unknown[]; name?: string; missing?: boolean }
export interface TreeStep { index: number; kind: string; label: string; suppressed?: boolean; when?: string | number; sketch?: string; feature?: number }
export interface TreeParam {
  key: string; label: string; kind: PartParamKind; default: number; value: number; instance: boolean; published: boolean;
  unit?: string; group?: string; description?: string;
}
export interface PartTree {
  id: string; name: string; kind: 'formula' | 'recipe'; partTypeId?: string; planes: PartPlane[];
  sketches: TreeSketch[]; steps: TreeStep[]; params: TreeParam[];
}

/** The origin's three planes as the panel names them. */
export const PLANE_LABELS: Record<PartPlane, string> = { xy: 'Top plane (xy)', xz: 'Front plane (xz)', yz: 'Right plane (yz)' };

export interface SketchRow { id: string; label: string; plane: string; opens: 'view' | 'body' | 'none'; missing: boolean }
export interface StepRow {
  index: number; label: string; kind: string; suppressed: boolean; when: string | null;
  /** The feature's own index (updateFeature, moveFeature, removeFeature); null for a step the part is made by (its operation, its bodies). */
  feature: number | null;
  canUp: boolean; canDown: boolean; canRemove: boolean; canToggle: boolean;
}
export interface ParamRow { key: string; label: string; kind: PartParamKind; value: number; default: number; unit: string; changed: boolean; instance: boolean; published: boolean; group: string }

/** A recipe sketch opens on its view; a formula part's body opens in the part's editor; one with neither is only listed. */
export function sketchRows(tree: PartTree): SketchRow[] {
  return tree.sketches.map((s) => ({
    id: s.id,
    label: s.name ?? s.role ?? s.id,
    plane: s.plane,
    opens: tree.kind === 'formula' ? 'body' : s.viewId && !s.missing ? 'view' : 'none',
    missing: !!s.missing,
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
      feature: f,
      canUp: f != null && f > first, canDown: f != null && f < last, canRemove: f != null, canToggle: f != null,
    };
  });
}

export const paramRows = (tree: PartTree): ParamRow[] => tree.params.map((p) => ({
  key: p.key, label: p.label, kind: p.kind, value: p.value, default: p.default, unit: p.unit ?? (p.kind === 'angle' ? '°' : ''),
  changed: Math.abs(p.value - p.default) > 1e-9, instance: p.instance, published: p.published, group: p.group ?? '',
}));

/** The origin plane's corners (metres) in the part's frame: a square of `half` round the origin; x then y of the plane, z up. */
export function planeCorners(plane: PartPlane, half: number): [number, number, number][] {
  const c: [number, number][] = [[-half, -half], [half, -half], [half, half], [-half, half]];
  return c.map(([a, b]) => (plane === 'xy' ? [a, b, 0] : plane === 'xz' ? [a, 0, b] : [0, a, b]));
}
