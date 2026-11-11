/**
 * Furniture from models (docs/plan-furniture.md, Configuratorul flexibil, stage 2; decision 477): the engine's catalog
 * `furniture.models` — CC0 meshes stretched by zones, their colour variants per role and the looks — and what the
 * types dialog does with it: the gallery by group, a preview's address, a look's patch, the size kept in a model's range.
 */

export interface FurnitureModelInfo {
  id: string; name: string; group: string; predefined: string; objectType?: string | null;
  /** Width, depth, height (metres): the size a new type starts with, the least and the most it may take. */
  size: number[]; min: number[]; max: number[];
  mount: number; against: boolean; roles: string[];
  /** Each axis's stretch zone (fractions of its extent), or null: the axis scales whole. */
  zones: ([number, number] | null)[];
  thumbnail: string; licence: string; source: string;
}
export interface Swatch { name: string; color?: string | null }
export interface ModelLook { key: string; label: string; materials: Record<string, string> }
export interface ModelsCatalog { models: FurnitureModelInfo[]; swatches: Record<string, Swatch[]>; looks: ModelLook[] }

/** The models by group, in the catalog's order. */
export function byGroup(models: FurnitureModelInfo[]): [string, FurnitureModelInfo[]][] {
  const o = new Map<string, FurnitureModelInfo[]>();
  for (const m of models) o.set(m.group, [...(o.get(m.group) ?? []), m]);
  return [...o];
}

/**
 * A model's preview: a file the interface serves (ui/public), fetched when its picture is first shown — never in the
 * first load — beside the app on the desktop and the web build alike.
 */
export const thumbnailUrl = (m: FurnitureModelInfo, base = import.meta.env.BASE_URL ?? '/') => `${base.endsWith('/') ? base : base + '/'}${m.thumbnail}`;

/** A look's materials for the roles the model has (the others are left out). */
export function lookPatch(look: ModelLook, roles: string[]): Record<string, string> {
  return Object.fromEntries(Object.entries(look.materials).filter(([role]) => roles.includes(role)));
}

/** Whether a type's materials are a look's (for the roles the model has). */
export function isLook(look: ModelLook, roles: string[], materials: Record<string, string> | undefined): boolean {
  const p = lookPatch(look, roles);
  return Object.keys(p).length > 0 && Object.entries(p).every(([r, n]) => materials?.[r] === n);
}

/** A size on an axis (0 width, 1 depth, 2 height) kept between the model's least and most, as the engine keeps it. */
export const clampAxis = (m: FurnitureModelInfo, axis: number, v: number) => Math.min(m.max[axis], Math.max(m.min[axis], v));

/** The axes that stretch by a zone (the ends keep their size); the others scale whole. */
export const zonedAxes = (m: FurnitureModelInfo): ('Width' | 'Depth' | 'Height')[] =>
  (['Width', 'Depth', 'Height'] as const).filter((_, i) => !!m.zones[i]);
