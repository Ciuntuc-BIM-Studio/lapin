/**
 * What a material says of itself (docs/plan-buildup-tags.md, material.setData): its description, its parameters
 * (IFC's standard names where there is one), how it is measured and its catalogue of thicknesses, the thicknesses its
 * maker makes it in. The catalogue is offered in the types' layer tables and a layer of another thickness is flagged.
 */

import type { ModelState } from '../../engine/model';

export type MeasureBy = 'area' | 'volume' | 'mass' | 'count';
export interface Measure { by: MeasureBy; thicknesses?: number[]; unit?: string; board?: [number, number] }
export interface MaterialParam { name: string; kind: string; value: unknown; pset?: string; options?: string[] }

/** The parameters the Data tab offers ready-made: λ, μ and density in IFC's own sets (SI values), the rest the user's. */
export const PARAM_PRESETS: { label: string; param: MaterialParam; unit: string }[] = [
  { label: 'Thermal conductivity λ', unit: 'W/(m·K)', param: { pset: 'Pset_MaterialThermal', name: 'ThermalConductivity', kind: 'number', value: 0 } },
  { label: 'Vapour resistance μ', unit: '', param: { pset: 'Pset_MaterialHygroscopic', name: 'UpperVaporResistanceFactor', kind: 'number', value: 0 } },
  { label: 'Density', unit: 'kg/m³', param: { pset: 'Pset_MaterialCommon', name: 'MassDensity', kind: 'number', value: 0 } },
  { label: 'Reaction to fire', unit: '', param: { name: 'Reaction to fire', kind: 'text', value: '' } },
  { label: 'Compressive strength', unit: 'MPa', param: { name: 'Compressive strength', kind: 'number', value: 0 } },
];

/** A parameter's key: its set and name, as the engine tells them apart. */
export const paramKey = (p: MaterialParam) => `${p.pset ?? ''}.${p.name.toLowerCase()}`;

/** The unit a standard parameter is held in (IFC's SI), for the label beside its value. */
export function unitOf(p: MaterialParam): string {
  return PARAM_PRESETS.find((x) => paramKey(x.param) === paramKey(p))?.unit ?? '';
}

/** A list of thicknesses as typed ("50, 80 100;150"): millimetres above zero, sorted, each once; null when one is not a number. */
export function parseThicknesses(text: string): number[] | null {
  const parts = text.split(/[\s,;]+/).filter(Boolean);
  const out: number[] = [];
  for (const p of parts) {
    const n = Number(p.replace(',', '.'));
    if (!Number.isFinite(n) || n <= 0) return null;
    out.push(Math.round(n * 10) / 10);
  }
  return [...new Set(out)].sort((a, b) => a - b);
}

export const formatThicknesses = (t: number[] | undefined) => (t ?? []).join(', ');

/** A stored measure, read; null for none (the takeoff as before). */
export function measureOf(props: Record<string, unknown> | undefined): Measure | null {
  const m = props?.measure as Partial<Measure> | undefined;
  return m && typeof m === 'object' && (m.by === 'area' || m.by === 'volume' || m.by === 'mass' || m.by === 'count') ? (m as Measure) : null;
}

/** A material's catalogue of thicknesses (mm) by its name; empty when it has none. */
export function catalogueOf(model: ModelState, materialName: string): number[] {
  const want = materialName.trim().toLowerCase();
  for (const n of model.nodes.values())
    if (n.type === 'IfcMaterial' && n.name.toLowerCase() === want) return measureOf(n.props)?.thicknesses ?? [];
  return [];
}

/** Whether a layer's thickness (metres) is one of the catalogue's (any, without one; a layer of no thickness too). */
export function inCatalogue(sizesMm: number[], metres: number): boolean {
  const mm = metres * 1000;
  return sizesMm.length === 0 || mm <= 1e-6 || sizesMm.some((s) => Math.abs(s - mm) < 0.05);
}
