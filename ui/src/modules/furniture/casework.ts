/**
 * Casework (docs/plan-furniture.md, Configuratorul flexibil, stage 1; decision 475): furniture of boards, one or more
 * carcasses side by side, each with its zones stacked from the bottom, as the engine's catalog `furniture.casework`
 * describes them (its presets, styles and the fields of a zone and of a carcass). These are the configurator's pure
 * steps over the list of carcasses; the engine checks every list it is given and builds it.
 */

import type { UnitField } from './kitchenUnits';

export type Zone = Record<string, unknown> & { front: string };
export type Carcass = Record<string, unknown> & { kind?: 'box' | 'gap'; width?: number; height?: number; zones?: Zone[] };
export interface CaseworkPreset { key: string; label: string; predefined: string; objectType?: string | null; spec: Record<string, unknown> }
export interface CaseworkStyle { key: string; label: string; patch: Record<string, unknown> }
export interface CaseworkCatalog { presets: CaseworkPreset[]; styles: CaseworkStyle[]; zoneFields: UnitField[]; carcassFields: UnitField[] }

/** The parameters of a casework type, by the section of the configurator they are edited in. */
export const SECTIONS: { label: string; keys: string[]; bed?: boolean }[] = [
  { label: 'Construction', keys: ['carcassBoard', 'backBoard', 'shelfBoard', 'frontBoard', 'topOn', 'back'] },
  { label: 'Base', keys: ['base', 'plinthHeight', 'plinthRecess', 'legs', 'legHeight', 'mount'] },
  { label: 'Top', keys: ['top', 'topThickness', 'overhang', 'basin'] },
  { label: 'Fronts', keys: ['frontStyle', 'handles', 'sliding'] },
  { label: 'Bed', keys: ['mattressHeight', 'bedDrawers', 'drawersOn', 'handles', 'carcassBoard'], bed: true },
];

/** Whether a parameter applies to the type's other values (the plinth's height only on a plinth, the legs only on legs…). */
export function paramApplies(key: string, spec: Record<string, unknown>, defaults: Record<string, unknown>): boolean {
  const v = (k: string) => spec[k] ?? defaults[k];
  switch (key) {
    case 'plinthHeight': case 'plinthRecess': return v('base') === 'plinth';
    case 'legs': case 'legHeight': return v('base') === 'legs';
    case 'mount': return v('base') === 'floating';
    case 'topThickness': case 'overhang': return v('top') !== 'none';
    default: return true;
  }
}

const isAuto = (c: Carcass) => typeof c.width !== 'number';

/** The carcasses' widths as the engine lays them: given, or a share of what the given ones leave of the piece's width (at least 100 mm). */
export function laidWidths(list: Carcass[], width: number): number[] {
  const fixed = list.reduce((a, c) => a + (isAuto(c) ? 0 : Number(c.width)), 0);
  const autos = list.filter(isAuto).length;
  const auto = autos ? Math.max(0.1, (width - fixed) / autos) : 0;
  return list.map((c) => Math.round((isAuto(c) ? auto : Number(c.width)) * 10000) / 10000);
}

/** The width the piece needs for its carcasses: the given ones' and 100 mm at least for each of the others; null when they all share it. */
export function fitWidth(list: Carcass[], width: number): number | null {
  if (!list.length || list.every(isAuto)) return null;
  const need = laidWidths(list, width).reduce((a, w) => a + w, 0);
  return Math.abs(need - width) < 1e-6 ? null : Math.round(need * 10000) / 10000;
}

export const newCarcass = (): Carcass => ({ zones: [{ front: 'doors' }] });

export function addCarcass(list: Carcass[], at?: number): Carcass[] {
  const i = at == null ? list.length : at + 1;
  return [...list.slice(0, i), newCarcass(), ...list.slice(i)];
}

export const removeCarcass = (list: Carcass[], i: number): Carcass[] => list.filter((_, k) => k !== i);

export function moveCarcass(list: Carcass[], i: number, dir: -1 | 1): Carcass[] {
  const j = i + dir;
  if (j < 0 || j >= list.length) return list;
  const o = [...list];
  [o[i], o[j]] = [o[j], o[i]];
  return o;
}

/** A carcass split in two side by side, each half its width (or both sharing, as it did) and a copy of its zones. */
export function splitCarcass(list: Carcass[], i: number): Carcass[] {
  const c = list[i];
  if (!c) return list;
  const half = (): Carcass => {
    const o: Carcass = JSON.parse(JSON.stringify(c));
    if (!isAuto(c)) o.width = Math.round((Number(c.width) / 2) * 10000) / 10000;
    return o;
  };
  return [...list.slice(0, i), half(), half(), ...list.slice(i + 1)];
}

/** A carcass's field set; empty (undefined) takes it off (a share of the width, the piece's height). A knee space has no zones or height. */
export function setCarcassField(list: Carcass[], i: number, key: string, value: unknown): Carcass[] {
  const c = list[i];
  if (!c) return list;
  const next: Carcass = { ...c, [key]: value };
  if (value === undefined || value === null) delete next[key];
  if (key === 'kind') {
    if (value === 'gap') { delete next.zones; delete next.height; next.kind = 'gap'; }
    else { delete next.kind; next.zones = c.zones?.length ? c.zones : [{ front: 'doors' }]; }
  }
  return list.map((x, k) => (k === i ? next : x));
}

/** A zone's fields that apply to it now (the drawers only with a front of drawers…). */
export function zoneFieldsFor(z: Zone, fields: UnitField[]): UnitField[] {
  return fields.filter((f) => !f.when || f.when.values.includes(String(z[f.when.key] ?? '')));
}

const zonesOf = (c: Carcass): Zone[] => c.zones ?? [{ front: 'doors' }];
const withZones = (list: Carcass[], i: number, zones: Zone[]): Carcass[] => list.map((x, k) => (k === i ? { ...x, zones } : x));

/** A zone added over the carcass's top one (or over <code>at</code>), open with a shelf. */
export function addZone(list: Carcass[], i: number, at?: number): Carcass[] {
  const c = list[i];
  if (!c || c.kind === 'gap') return list;
  const zones = zonesOf(c);
  const j = at == null ? zones.length : at + 1;
  return withZones(list, i, [...zones.slice(0, j), { front: 'open', shelves: 1 }, ...zones.slice(j)]);
}

/** A zone taken out; the last one stays (a carcass has one at least). */
export function removeZone(list: Carcass[], i: number, j: number): Carcass[] {
  const c = list[i];
  if (!c) return list;
  const zones = zonesOf(c);
  return zones.length < 2 ? list : withZones(list, i, zones.filter((_, k) => k !== j));
}

/**
 * A zone's field set. Its front changed, the fields that no longer apply are dropped (a door's hand on a zone of
 * drawers); the drawers' ratios dropped when their count changes and they no longer match; undefined takes a field off.
 */
export function setZoneField(list: Carcass[], i: number, j: number, key: string, value: unknown, fields: UnitField[]): Carcass[] {
  const c = list[i];
  if (!c) return list;
  const zones = zonesOf(c);
  const z = zones[j];
  if (!z) return list;
  const next: Zone = { ...z, [key]: value };
  if (value === undefined || value === null || value === '') delete next[key];
  const applies = zoneFieldsFor(next, fields).map((f) => f.key);
  for (const k of Object.keys(next)) if (k !== 'front' && fields.some((f) => f.key === k) && !applies.includes(k)) delete next[k];
  if (Array.isArray(next.ratios) && next.ratios.length !== Number(next.drawers ?? 3)) delete next.ratios;
  return withZones(list, i, zones.map((x, k) => (k === j ? next : x)));
}

/** Drawer ratios typed as "1:1:2" (or with commas or spaces): the numbers, or null when one is not a positive number. */
export function parseRatios(text: string): number[] | null {
  const parts = text.split(/[\s:;,/]+/).filter(Boolean);
  if (!parts.length) return null;
  const o = parts.map((p) => Number(p.replace(',', '.')));
  return o.every((x) => Number.isFinite(x) && x > 0) ? o : null;
}

export const formatRatios = (r: unknown): string => (Array.isArray(r) ? r.join(':') : '');

/** A board as the preview lists it. */
export interface BoardDto { part: string; carcass: number; role: string; length: number; width: number; thickness: number; grain: string; front: boolean }

/** The preview's boards grouped as the cut list groups them: a row per part, carcass and size, the boards before the fronts. */
export function cutListRows(boards: BoardDto[]): { part: string; carcass: number; size: string; role: string; count: number; front: boolean }[] {
  const mm = (v: number) => Math.round(v * 1000);
  const rows = new Map<string, { part: string; carcass: number; size: string; role: string; count: number; front: boolean }>();
  for (const front of [false, true])
    for (const b of boards.filter((x) => x.front === front)) {
      const size = `${mm(b.length)}×${mm(b.width)}×${mm(b.thickness)}`;
      const key = `${front}|${b.part}|${b.carcass}|${b.role}|${size}`;
      const r = rows.get(key);
      if (r) r.count += 1; else rows.set(key, { part: b.part, carcass: b.carcass, size, role: b.role, count: 1, front });
    }
  return [...rows.values()];
}
