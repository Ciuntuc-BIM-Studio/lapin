/**
 * The property sets and quantity sets an element brought from an IFC file
 * (a reference's `properties`, a recognised element's `imported.properties`):
 * set → property → value as text, as the engine read them (Lapin.Ifc,
 * IfcModel.Properties). Shown read-only in the properties panel.
 */

import type { NodeDto } from '../engine/protocol';

export interface IfcPropertySet { name: string; props: [string, string][] }

export function importedSets(n: NodeDto): IfcPropertySet[] {
  const p = n.props as Record<string, unknown>;
  const raw = (p.properties ?? (p.imported as Record<string, unknown> | undefined)?.properties) as unknown;
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return [];
  const out: IfcPropertySet[] = [];
  for (const [name, set] of Object.entries(raw as Record<string, unknown>)) {
    if (!set || typeof set !== 'object' || Array.isArray(set)) continue;
    const props = Object.entries(set as Record<string, unknown>)
      .filter(([, v]) => v != null && typeof v !== 'object')
      .map(([k, v]) => [k, String(v)] as [string, string]);
    if (props.length) out.push({ name, props });
  }
  return out;
}

/** The sets with only the properties whose set, name or value has `q` in it (case aside); all of them for an empty query. */
export function filterSets(sets: IfcPropertySet[], q: string): IfcPropertySet[] {
  const t = q.trim().toLowerCase();
  if (!t) return sets;
  return sets
    .map((s) => (s.name.toLowerCase().includes(t) ? s : { name: s.name, props: s.props.filter(([k, v]) => k.toLowerCase().includes(t) || v.toLowerCase().includes(t)) }))
    .filter((s) => s.props.length > 0);
}
