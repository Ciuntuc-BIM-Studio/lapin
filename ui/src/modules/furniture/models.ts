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

// ─── The user's own models (docs/plan-user-extensibility.md §3.5, decision 512) ──────────────────────────────────────────

/** The roles a model's materials may be given (the engine's), in the order the picker lists them. */
export const MODEL_ROLES = ['upholstery', 'accent', 'body', 'top', 'legs', 'metal', 'glass', 'shade', 'foliage', 'pot'] as const;

export type Axis = 'x' | 'y' | 'z';
export const AXES: Axis[] = ['x', 'y', 'z'];
/** Each axis's stretch zone as fractions of the model's extent (the ends keep their size); an axis left out scales whole. */
export type Zones = Partial<Record<Axis, [number, number]>>;

/** A model of the project's own, as `furniture.models` gives it. */
export interface UserModel {
  id: string; name: string; code: string; natural: number[]; size?: number[]; min?: number[]; max?: number[]; zones?: Zones; slots?: Record<string, string>;
  mount?: number; against?: boolean; seats?: boolean; licence?: string; source?: string; group?: string; predefined?: string; types: number;
}

const FALLBACK_GROUP = "My models";

/** A user's model in the shape the catalog's models have, so the gallery, the roles and the size limits serve both: its id is its code (what a type's `model` holds). */
export function userModelInfo(u: UserModel): FurnitureModelInfo {
  const natural = u.natural?.length === 3 ? u.natural : [1, 1, 1];
  const roles = [...new Set(Object.values(u.slots ?? {}))];
  return {
    id: u.code, name: u.name, group: u.group || FALLBACK_GROUP, predefined: u.predefined || 'USERDEFINED', objectType: u.name,
    size: u.size?.length === 3 ? u.size : natural, min: u.min?.length === 3 ? u.min : natural.map((x) => x * 0.25), max: u.max?.length === 3 ? u.max : natural.map((x) => x * 4),
    mount: u.mount ?? 0, against: u.against ?? true, roles: roles.length ? roles : ['body'],
    zones: AXES.map((a) => u.zones?.[a] ?? null), thumbnail: '', licence: u.licence ?? '', source: u.source ?? 'own',
  };
}

/** The catalog with the user's models after Lapin's (an id the catalog has is not added twice). */
export function mergeModels(cat: ModelsCatalog | undefined, users: UserModel[]): ModelsCatalog | undefined {
  if (!cat) return cat;
  if (users.length === 0) return cat;
  const have = new Set(cat.models.map((m) => m.id));
  return { ...cat, models: [...cat.models, ...users.map(userModelInfo).filter((m) => !have.has(m.id))] };
}

/** A zone's ends kept inside 0 to 1 and in order: the first not above the second (the one just moved gives way). */
export function setZoneEnd(zones: Zones, axis: Axis, end: 0 | 1, value: number): Zones {
  const v = Math.min(1, Math.max(0, value));
  const cur = zones[axis] ?? [0.3, 0.7];
  const next: [number, number] = end === 0 ? [Math.min(v, cur[1]), cur[1]] : [cur[0], Math.max(v, cur[0])];
  return { ...zones, [axis]: next };
}

/** An axis made to stretch by a zone (the middle third to start), or back to scaling whole. */
export function toggleZone(zones: Zones, axis: Axis, on: boolean): Zones {
  const { [axis]: _gone, ...rest } = zones;
  return on ? { ...rest, [axis]: zones[axis] ?? [1 / 3, 2 / 3] } : rest;
}

/** A zone as the sliders show it: percents of the extent. */
export const zonePercent = (z: [number, number] | undefined): [number, number] | null => (z ? [Math.round(z[0] * 100), Math.round(z[1] * 100)] : null);

/** What the update command is given for the zones: only the axes that stretch, each rounded to a thousandth. */
export const zonesArg = (zones: Zones): Record<string, [number, number]> =>
  Object.fromEntries(AXES.filter((a) => zones[a]).map((a) => [a, [Math.round(zones[a]![0] * 1000) / 1000, Math.round(zones[a]![1] * 1000) / 1000]]));

/** Materials of the file listed with the role each is given: the ones the model has, else "body". */
export const slotRows = (materials: string[], slots: Record<string, string> | undefined): [string, string][] => materials.map((m) => [m, slots?.[m] ?? 'body']);
