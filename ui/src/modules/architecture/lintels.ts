/** A lintel rule, as wall types and the joinery configurator store it, and the rule a new wall type starts with. */

export interface Product { code: string; width: number; height: number; lengths: number[]; manufacturer?: string }
/** A beam type a precast lintel can be (report #135), as `architecture.lintelTypes` lists them. */
export interface LintelType { id: string; name: string; width: number; height: number; lengths: number[]; mark?: string; code?: string; manufacturer?: string; bearing?: number; material?: string }
interface Bars { count: number; diameter: number }
export interface Rule {
  mode: 'none' | 'precast' | 'cast'; bearing: number; height: number;
  bearingEnd?: number | null; minWidth?: number | null; width?: number | null; placement?: string | null; pieces?: number | null; material?: string | null;
  product?: Product | null;
  reinforcement?: { top?: Bars | null; bottom?: Bars | null; stirrups?: { diameter: number; spacing: number } | null; cover?: number | null } | null;
  /** A beam type as the product: its section, lengths, material and bearing (unless `ownBearing`). */
  beamType?: string | null; ownBearing?: boolean | null;
  [k: string]: unknown;
}

export const DEFAULT_RULE: Rule = { mode: 'precast', bearing: 0.25, height: 0.25 };

/** The product a beam type stands for in the rule: a copy kept in case the type goes, the engine reading the type itself. */
export function productOf(t: LintelType): Product {
  return { code: t.name, width: t.width, height: t.height, lengths: t.lengths, ...(t.manufacturer ? { manufacturer: t.manufacturer } : {}) };
}

/** The rule with a beam type chosen (null: none); its bearing the type's again unless the rule keeps its own. */
export function withBeamType(rule: Rule, t: LintelType | null): Rule {
  if (!t) return { ...rule, beamType: null, ownBearing: null, product: null };
  return { ...rule, beamType: t.id, product: productOf(t), ownBearing: rule.ownBearing ?? null };
}

/** The bearing the rule's lintels take: its own, or its beam type's default when it has one and the rule no bearing of its own. */
export function effectiveBearing(rule: Rule, types: LintelType[]): number {
  const t = rule.beamType ? types.find((x) => x.id === rule.beamType) : undefined;
  return t && !rule.ownBearing && t.bearing != null ? t.bearing : rule.bearing;
}

/** The older catalogue's products not already among the beam types (the same code): one list, as the importer makes it. */
export function legacyProducts(products: Product[], types: LintelType[]): Product[] {
  const codes = new Set(types.flatMap((t) => [t.code, t.name].filter((c): c is string => !!c).map((c) => c.toLowerCase())));
  return products.filter((p) => !codes.has(p.code.toLowerCase()));
}
