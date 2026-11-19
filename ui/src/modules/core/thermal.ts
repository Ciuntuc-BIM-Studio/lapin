/** Thermal resistance and U-value (EN ISO 6946): what the engine's `thermal.of` and `thermal.settings` return, and how the interface shows it. */

export interface ThermalLayer {
  index: number; name: string; material: string | null; materialId: string | null; thickness: number;
  lambda: number | null; source: 'material' | 'indicative' | 'air' | 'none' | 'skipped'; as: string | null; r: number | null;
}

export interface Thermal {
  id: string; kind: 'wall' | 'floor' | 'roof'; case: string; caseOwn: string | null; from: 'type' | 'element';
  rsi: number; rse: number; layers: ThermalLayer[]; rLayers: number; rTotal: number; u: number | null; complete: boolean;
  missing: string[]; typed: number | null; effective: number | null; indicative: boolean; notes: string[];
}

export interface ThermalCase { id: string; label: string; flow: 'horizontal' | 'upward' | 'downward'; rsi: number; rse: number; defaultRsi: number; defaultRse: number; own: boolean }
export interface ThermalSettings { indicative: boolean; cases: ThermalCase[] }

/** The classes the section and the command work on. */
export const THERMAL_CLASSES = ['IfcWall', 'IfcSlab', 'IfcRoof', 'IfcWallType', 'IfcSlabType'];

/** U in W/m²K, three decimals. */
export const fmtU = (u: number) => u.toFixed(3);
/** R in m²K/W, three decimals. */
export const fmtR = (r: number) => r.toFixed(3);
/** λ in W/mK: up to three decimals, trailing zeros dropped (0.04, 2.3, 0.135). */
export const fmtLambda = (l: number) => String(Number(l.toFixed(3)));
/** A thickness in metres as millimetres, one decimal at most (10, 15, 7.5). */
export const fmtMm = (m: number) => String(Math.round(m * 10000) / 10);

/** A surface resistance typed in a box: a number of 0 or more (a comma is a decimal point); null when it is not one. */
export function parseResistance(text: string): number | null {
  const s = text.trim().replace(',', '.');
  if (!/^\d+(\.\d+)?$|^\.\d+$/.test(s)) return null;
  const v = Number(s);
  return Number.isFinite(v) && v < 10 ? v : null;
}

/** The case a node falls under, for the select: its own when set, else '' (by its kind). */
export const caseChoice = (t: Thermal) => t.caseOwn ?? '';

/** The label of the case a node is worked out with. */
export const caseLabel = (cases: ThermalCase[], id: string) => cases.find((c) => c.id === id)?.label ?? id;

/** Whether a layer's λ is Lapin's own guess by name. */
export const isIndicative = (l: ThermalLayer) => l.source === 'indicative';
