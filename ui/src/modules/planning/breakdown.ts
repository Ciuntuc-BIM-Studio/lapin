/**
 * Product × Location (docs/plan-4d5d.md, "Produs × Loc"; the consultant's ABS / PBS / GBS), apart from React so it can be
 * tested: a rule's product (a code of the project's PBS, the node and what is under it) and locations (the building, its
 * storeys, their rooms) read and changed, said in a few words, and the tasks a cross-product would make.
 */

import type { TaskRule } from './gantt';

/** A product of a rule: a code of the project's PBS, or of a system named. */
export type ProductPick = string | { system: string; code: string };

/** An item of the PBS as the engine counts it: its code, title, parent, depth in the tree and how many elements it labels (its own and those under it). */
export interface PbsItem { code: string; title: string; parent?: string | null; depth: number; count: number }

/** A place of the GBS: the building, a storey, a room (on its storey); how many elements stand there (rooms: worked out when picked). */
export interface LocationOption { id: string; name: string; level: 'building' | 'storey' | 'room'; count?: number; storey?: string }

export const codeOf = (p: ProductPick): string => (typeof p === 'string' ? p : p.code);

/** The codes a rule's products name. */
export function productCodes(rule: TaskRule | undefined): string[] {
  return (rule?.products ?? []).map(codeOf);
}

/** A rule with its product set to one code (null: any product, the key gone). */
export function setProduct(rule: TaskRule | undefined, code: string | null): TaskRule {
  const next: TaskRule = { ...(rule ?? {}) };
  if (code) next.products = [code]; else delete next.products;
  return next;
}

/** A rule with a location switched on or off; none left: anywhere, the key gone. */
export function toggleLocation(rule: TaskRule | undefined, id: string): TaskRule {
  const next: TaskRule = { ...(rule ?? {}) };
  const now = new Set(next.locations ?? []);
  if (now.has(id)) now.delete(id); else now.add(id);
  if (now.size) next.locations = [...now]; else delete next.locations;
  return next;
}

/** A product in words: its code and title ("40.20 Doors"), the code alone when the PBS has no such item. */
export function productLabel(items: readonly PbsItem[], code: string): string {
  const i = items.find((x) => x.code === code);
  return i?.title ? `${i.code} ${i.title}` : code;
}

/** The PBS as the picker lists it: each item indented by its depth, with its count. */
export function productOptions(items: readonly PbsItem[]): { code: string; label: string; count: number }[] {
  return items.map((i) => ({ code: i.code, label: `${' '.repeat(Math.max(0, i.depth))}${i.code} ${i.title}`.trimEnd(), count: i.count }));
}

/** The locations grouped as the picker shows them: the buildings, then each storey with its rooms under it. */
export function locationGroups(locations: readonly LocationOption[]): { head: LocationOption; rooms: LocationOption[] }[] {
  const groups: { head: LocationOption; rooms: LocationOption[] }[] = [];
  const byStorey = new Map<string, { head: LocationOption; rooms: LocationOption[] }>();
  for (const l of locations) {
    if (l.level === 'room') {
      const g = l.storey ? byStorey.get(l.storey) : undefined;
      if (g) g.rooms.push(l); else groups.push({ head: l, rooms: [] });
      continue;
    }
    const g = { head: l, rooms: [] as LocationOption[] };
    groups.push(g);
    if (l.level === 'storey') byStorey.set(l.id, g);
  }
  return groups;
}

/** A rule's product × location in words ("40.20 Doors × Ground floor, Floor 2"); null when it names neither. */
export function breakdownSummary(rule: TaskRule | undefined, items: readonly PbsItem[], locations: readonly LocationOption[]): string | null {
  const codes = productCodes(rule);
  const where = rule?.locations ?? [];
  if (!codes.length && !where.length) return null;
  const product = codes.length ? codes.map((c) => productLabel(items, c)).join(', ') : 'any product';
  const place = where.length ? where.map((id) => locations.find((l) => l.id === id)?.name ?? id).join(', ') : 'anywhere';
  return `${product} × ${place}`;
}

/** The tasks a cross-product makes, in order: one a location, named "<activity> · <location>". */
export function crossTasks(activity: string, locations: readonly LocationOption[], picked: readonly string[]): { location: string; name: string }[] {
  const name = activity.trim();
  return picked.map((id) => locations.find((l) => l.id === id)).filter((l): l is LocationOption => !!l)
    .map((l) => ({ location: l.id, name: `${name} · ${l.name}` }));
}

/** The locations a cross-product starts on: every storey, in their order. */
export function defaultLocations(locations: readonly LocationOption[]): string[] {
  return locations.filter((l) => l.level === 'storey').map((l) => l.id);
}

/** A list of locations with one switched, kept in the model's order (the order the tasks are linked in). */
export function togglePicked(locations: readonly LocationOption[], picked: readonly string[], id: string): string[] {
  const set = new Set(picked);
  if (set.has(id)) set.delete(id); else set.add(id);
  return locations.filter((l) => set.has(l.id)).map((l) => l.id);
}

/** The elements without a task by product, as the notice says it ("40.20 Doors 3, no code 2"); "" when there is no PBS. */
export function byProductText(groups: readonly { code: string; title: string; count: number }[] | undefined): string {
  if (!groups?.length) return '';
  return [...groups].sort((a, b) => b.count - a.count || a.code.localeCompare(b.code))
    .map((g) => (g.code ? `${g.code}${g.title ? ` ${g.title}` : ''} ${g.count}` : `no code ${g.count}`)).join(', ');
}
