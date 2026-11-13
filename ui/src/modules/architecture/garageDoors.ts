/**
 * Garage doors in the doors and windows configurator (forum topic #48, decision 530): a door type whose one panel opens as a
 * sectional overhead, roller, up-and-over or side-sliding sectional door. The engine builds and draws them; these are the
 * configurator's rules for switching between them and naming the common sizes (indicative).
 */

export const GARAGE_OPERATIONS = ['sectional-overhead', 'roller', 'up-and-over', 'sectional-side-left', 'sectional-side-right'] as const;
export type GarageOperation = (typeof GARAGE_OPERATIONS)[number];

/** A garage door's settings as the engine keeps them (JoineryGarage); a missing field is the engine's default. */
export interface Garage {
  sections?: number | null; finish?: 'ribbed' | 'flat' | 'v-groove' | 'glazed-top'; wicket?: boolean; wicketWidth?: number; wicketSide?: 'left' | 'centre' | 'right';
  track?: 'standard' | 'low-headroom' | 'high-lift'; headroom?: number | null; backroom?: number | null; sideroom?: number | null; lift?: number;
  box?: 'inside' | 'outside'; mechanism?: 'canopy' | 'retractable'; tracks?: boolean;
}

export const GARAGE_LABELS: Record<GarageOperation, string> = {
  'sectional-overhead': 'Sectional overhead (garage)',
  roller: 'Roller (garage)',
  'up-and-over': 'Up-and-over (garage)',
  'sectional-side-left': 'Side-sliding sectional, to the left (garage)',
  'sectional-side-right': 'Side-sliding sectional, to the right (garage)',
};

export const isGarage = (op: string): op is GarageOperation => (GARAGE_OPERATIONS as readonly string[]).includes(op);

interface PanelLike { col: number; row: number; operation: string }
interface SpecLike { kind: 'door' | 'window'; panels: PanelLike[] }

/** The garage door's operation of a type, or null when it is none. */
export function garageOf(spec: SpecLike): GarageOperation | null {
  if (spec.kind !== 'door') return null;
  const op = spec.panels.find((p) => isGarage(p.operation))?.operation;
  return op && isGarage(op) ? op : null;
}

/** The type as another kind of garage door: one panel filling the opening, its settings kept. */
export function withGarageKind<S extends SpecLike & { columns: number[]; rows: number[]; mullions?: unknown; meeting?: unknown }>(spec: S, op: GarageOperation): S {
  return { ...spec, columns: [1], rows: [1], mullions: null, meeting: null, panels: [{ col: 0, row: 0, operation: op }] };
}

/** A size as the list shows it, its region as a code every language reads: "2400 × 2100 · AU". */
export function sizeLabel(z: { region: string; width: number; height: number }): string {
  return `${Math.round(z.width * 1000)} × ${Math.round(z.height * 1000)} · ${z.region}`;
}

/** The name the dialog gives a garage door of a size, as it gives a door "Door 900×2100". */
export function garageName(op: GarageOperation, width: number, height: number): string {
  const what = op === 'sectional-overhead' ? 'Sectional garage door' : op === 'roller' ? 'Roller garage door' : op === 'up-and-over' ? 'Up-and-over garage door' : 'Side-sliding garage door';
  return `${what} ${Math.round(width * 1000)}×${Math.round(height * 1000)}`;
}
