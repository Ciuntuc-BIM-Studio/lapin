/**
 * The build-up tag type (docs/plan-buildup-tags.md, stage 2): a header, a line per layer, a footer; the layers' order,
 * whether layers of no thickness are written, one leader or one per layer, a bullet, the width the lines wrap at. The
 * engine fills and draws it (BuildupText, BuildupTags); these are the editor's own helpers.
 */

export interface BuildupEntry {
  id: string; name: string; shape?: string; template?: string; header?: string; footer?: string; order?: string;
  zeroLayers?: boolean; leaders?: string; bullet?: string; width?: number; unit?: string; textStyle?: string;
  leaderShape?: string; underline?: string; extension?: number;
}

/** What a tag type becomes when its shape is made a build-up: the tester's note (EXTERIOR, a line per layer, INTERIOR). */
export const BUILDUP_DEFAULTS = {
  template: '{description}[, λ = {λ} W/mK]: {thickness} mm', header: 'EXTERIOR', footer: 'INTERIOR', order: 'out-in', zeroLayers: true,
  leaders: 'one', bullet: 'bullet', width: 80, unit: 'mm', classes: 'IfcWall, IfcSlab, IfcRoof, IfcCovering, LapinRoomFinish',
};

/** The layer fields a line reads (the engine's BuildupText.LayerFields), with what each writes. */
export const LAYER_FIELDS: [string, string][] = [
  ['description', 'Description (else the name)'], ['material', 'Material'], ['thickness', 'Thickness'], ['note', 'Layer note'],
  ['function', 'Function (core, finish…)'], ['index', 'Number'], ['category', 'Category'],
];

/** The element's fields a line or the header may read too. */
export const ELEMENT_FIELDS: [string, string][] = [['name', 'Name'], ['type', 'Type'], ['mark', 'Mark']];

/** The symbols the engine reads for the standard properties, by the property each stands for. */
const SYMBOLS: Record<string, string> = {
  'Pset_MaterialThermal.ThermalConductivity': 'λ',
  'Pset_MaterialHygroscopic.UpperVaporResistanceFactor': 'μ',
  'Pset_MaterialHygroscopic.LowerVaporResistanceFactor': 'μ',
  'Pset_MaterialCommon.MassDensity': 'ρ',
};

interface ParamLike { name?: unknown; pset?: unknown }

/**
 * The material parameters a line may read, found on the project's materials: each by its symbol where the engine has
 * one (λ, μ, ρ), else by its name, each once, in the order first met.
 */
export function materialFields(materials: { params?: unknown }[]): string[] {
  const out: string[] = [];
  for (const m of materials) {
    if (!Array.isArray(m.params)) continue;
    for (const p of m.params as ParamLike[]) {
      if (typeof p?.name !== 'string' || !p.name.trim()) continue;
      const key = typeof p.pset === 'string' && p.pset ? `${p.pset}.${p.name.trim()}` : p.name.trim();
      const field = SYMBOLS[key] ?? p.name.trim();
      if (!out.includes(field)) out.push(field);
    }
  }
  return out;
}

/** A field put into a template where the caret is (replacing what is selected); the template and where the caret goes after it. */
export function insertField(template: string, start: number, end: number, field: string): { text: string; caret: number } {
  const s = Math.max(0, Math.min(start, template.length)), e = Math.max(s, Math.min(end, template.length));
  const piece = `{${field}}`;
  return { text: template.slice(0, s) + piece + template.slice(e), caret: s + piece.length };
}

/**
 * What is selected made an optional part, [ … ], dropped where a field in it is empty; nothing selected, a part for the
 * field given ("[, λ = {λ}]"). The template and where the caret goes after it.
 */
export function wrapOptional(template: string, start: number, end: number, field?: string): { text: string; caret: number } {
  const s = Math.max(0, Math.min(start, template.length)), e = Math.max(s, Math.min(end, template.length));
  const inner = e > s ? template.slice(s, e) : field ? `, ${field} = {${field}}` : '';
  const piece = `[${inner}]`;
  return { text: template.slice(0, s) + piece + template.slice(e), caret: s + piece.length - (inner ? 0 : 1) };
}

/** Whether a template's brackets close: every [ has its ], every { its }, escaped ones (\[) aside. */
export function balanced(template: string): boolean {
  let square = 0, brace = 0;
  for (let i = 0; i < template.length; i++) {
    const c = template[i];
    if (c === '\\') { i++; continue; }
    if (c === '[') square++;
    else if (c === ']') { if (--square < 0) return false; }
    else if (c === '{') { if (brace++ > 0) return false; }
    else if (c === '}') { if (--brace < 0) return false; }
  }
  return square === 0 && brace === 0;
}

/** How the order reads for the sample shown: across a wall, or through a floor. */
export function orderLabels(stack: boolean): [string, string][] {
  return stack
    ? [['out-in', 'top → bottom'], ['in-out', 'bottom → top']]
    : [['out-in', 'exterior → interior'], ['in-out', 'interior → exterior']];
}

/** A width typed (paper mm): a number from 10 to 1000, else null. */
export function parseWidth(text: string): number | null {
  const n = Number(text.trim().replace(',', '.'));
  return Number.isFinite(n) && n >= 10 && n <= 1000 ? Math.round(n * 10) / 10 : null;
}
