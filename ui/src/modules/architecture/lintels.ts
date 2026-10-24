/** A lintel rule, as wall types and the joinery configurator store it, and the rule a new wall type starts with. */

export interface Product { code: string; width: number; height: number; lengths: number[]; manufacturer?: string }
interface Bars { count: number; diameter: number }
export interface Rule {
  mode: 'none' | 'precast' | 'cast'; bearing: number; height: number;
  bearingEnd?: number | null; minWidth?: number | null; width?: number | null; placement?: string | null; pieces?: number | null; material?: string | null;
  product?: Product | null;
  reinforcement?: { top?: Bars | null; bottom?: Bars | null; stirrups?: { diameter: number; spacing: number } | null; cover?: number | null } | null;
  [k: string]: unknown;
}

export const DEFAULT_RULE: Rule = { mode: 'precast', bearing: 0.25, height: 0.25 };

