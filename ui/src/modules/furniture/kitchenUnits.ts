/**
 * A kitchen's own units (a tester's report #76, decision 472): the base line (base units, sinks, hobs, cookers,
 * dishwashers, fridges, tall units, fillers, gaps) and the wall line (wall units, hoods, gaps), as the engine's catalog
 * `furniture.kitchenUnits` describes them. These are the configurator's pure steps over a list; the engine checks
 * every list it is given and builds it.
 */

export interface UnitField {
  key: string; label: string; kind: 'length' | 'number' | 'choice' | 'bool';
  min?: number | null; max?: number | null; choices?: string[] | null; when?: { key: string; values: string[] } | null;
}
export interface UnitType {
  key: string; label: string; line: 'base' | 'upper'; min: number; max: number; tall: boolean;
  sizes?: number[] | null; fields: UnitField[]; defaults: Record<string, unknown>;
}
export interface UnitsCatalog { steps: number[]; typical: KitchenUnit[]; types: UnitType[] }
export type KitchenUnit = Record<string, unknown> & { type: string; width: number };

/** The types of a line. */
export const typesOf = (cat: UnitsCatalog | undefined, upper: boolean): UnitType[] =>
  (cat?.types ?? []).filter((t) => (t.line === 'upper') === upper);

export const findType = (cat: UnitsCatalog | undefined, key: string, upper: boolean): UnitType | undefined =>
  typesOf(cat, upper).find((t) => t.key === key);

/** The units' total width, in metres. */
export const unitsTotal = (list: KitchenUnit[]): number => Math.round(list.reduce((a, u) => a + Number(u.width ?? 0), 0) * 10000) / 10000;

/** The widths offered first: an appliance's sizes, else the standard steps within the type's range. */
export function widthOptions(t: UnitType | undefined, steps: number[]): number[] {
  if (!t) return [];
  if (t.sizes?.length) return t.sizes.filter((w) => w >= t.min - 1e-9 && w <= t.max + 1e-9);
  return steps.filter((w) => w >= t.min - 1e-9 && w <= t.max + 1e-9);
}

/** Whether only the sizes are allowed (a dishwasher, a hob, a cooker), not any width between. */
export const fixedSizes = (t: UnitType | undefined): boolean => !!t && ['dishwasher', 'hob', 'cooker'].includes(t.key);

/** A unit's fields that apply to it now (the drawers only with a front of drawers, a fridge's height only when it is tall…). */
export function fieldsFor(u: KitchenUnit, t: UnitType | undefined): UnitField[] {
  if (!t) return [];
  return t.fields.filter((f) => !f.when || f.when.values.includes(String(u[f.when.key] ?? t.defaults[f.when.key] ?? '')));
}

/** What a field is when the unit does not set it, as the engine builds it: a unit over 600 has two doors, a single door hinged on the left, three drawers. */
export function valueOf(u: KitchenUnit, f: UnitField, t: UnitType | undefined): unknown {
  if (u[f.key] !== undefined) return u[f.key];
  if (t?.defaults[f.key] !== undefined) return t.defaults[f.key];
  switch (f.key) {
    case 'doors': return Number(u.width) > 0.6 + 1e-6 ? 2 : 1;
    case 'hand': return 'left';
    case 'drawers': return 3;
    default: return undefined;
  }
}

/** A new unit of a type, from its defaults. */
export const newUnit = (t: UnitType): KitchenUnit => ({ ...(t.defaults as KitchenUnit), type: t.key });

/** The list with a unit of the type added after <code>at</code> (at the end without one). */
export function addUnit(list: KitchenUnit[], t: UnitType, at?: number): KitchenUnit[] {
  const i = at == null ? list.length : at + 1;
  return [...list.slice(0, i), newUnit(t), ...list.slice(i)];
}

export const removeUnit = (list: KitchenUnit[], i: number): KitchenUnit[] => list.filter((_, k) => k !== i);

/** The unit moved one place to the left (−1) or the right (+1); at an end it stays. */
export function moveUnit(list: KitchenUnit[], i: number, dir: -1 | 1): KitchenUnit[] {
  const j = i + dir;
  if (j < 0 || j >= list.length) return list;
  const o = [...list];
  [o[i], o[j]] = [o[j], o[i]];
  return o;
}

/**
 * A unit's field set. Its type changed, it starts from the new type's defaults, keeping its width when the new type
 * takes it; a field set to its type's default is left out (the list stays short). The fields that no longer apply are dropped.
 */
export function setField(list: KitchenUnit[], i: number, key: string, value: unknown, cat: UnitsCatalog | undefined, upper: boolean): KitchenUnit[] {
  const u = list[i];
  if (!u) return list;
  let next: KitchenUnit;
  if (key === 'type') {
    const t = findType(cat, String(value), upper);
    if (!t) return list;
    next = newUnit(t);
    const w = Number(u.width);
    const fits = fixedSizes(t) ? (t.sizes ?? []).some((x) => Math.abs(x - w) < 1e-6) : w >= t.min - 1e-9 && w <= t.max + 1e-9;
    if (fits) next.width = w;
  } else {
    next = { ...u, [key]: value };
    const t = findType(cat, u.type, upper);
    if (t) for (const k of Object.keys(next)) if (k !== 'type' && k !== 'width' && !fieldsFor(next, t).some((f) => f.key === k)) delete next[k];
  }
  return list.map((x, k) => (k === i ? next : x));
}
