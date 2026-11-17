/**
 * The tag rules of the drafting standards (issue #72): per class and in their order, a condition (a formula over the element's
 * fields, as {= …} reads them) and the tag type it gives; the first rule of a class that holds wins. The engine keeps them in one
 * list (`tagRules`); the Tags tab shows them grouped by class, and moving one up or down swaps it with its class's neighbour.
 */

export interface TagRuleDto { id: string; class: string; when: string; tagType: string }

/** The classes the rules are offered for, beside those the tag types name. */
export const RULE_CLASSES = ['IfcDoor', 'IfcWindow', 'IfcSpace', 'IfcWall', 'IfcSlab', 'IfcColumn', 'IfcBeam'];

/** The classes to offer: the usual ones, then any a tag type or a rule names, each once. */
export function ruleClasses(types: { classes?: unknown }[], rules: TagRuleDto[]): string[] {
  const named = types.flatMap((t) => (Array.isArray(t.classes) ? t.classes : typeof t.classes === 'string' ? t.classes.split(',') : []))
    .map((c) => String(c).trim()).filter(Boolean);
  return [...new Set([...RULE_CLASSES, ...named, ...rules.map((r) => r.class)])];
}

/** The rules by class, the classes in the order their first rule stands, each class's rules in theirs. */
export function rulesByClass(rules: TagRuleDto[]): [string, TagRuleDto[]][] {
  const groups = new Map<string, TagRuleDto[]>();
  for (const r of rules) groups.set(r.class, [...(groups.get(r.class) ?? []), r]);
  return [...groups.entries()];
}

/**
 * Where a rule goes in the whole list when moved up (by −1) or down (by 1) among its class's rules: the index of the neighbour
 * it changes places with; null when it is already first (or last) of its class, or not in the list.
 */
export function moveIndex(rules: TagRuleDto[], id: string, by: -1 | 1): number | null {
  const at = rules.findIndex((r) => r.id === id);
  if (at < 0) return null;
  for (let i = at + by; i >= 0 && i < rules.length; i += by) if (rules[i].class === rules[at].class) return i;
  return null;
}

/** A condition as typed: trimmed, and its {= …} or a leading = taken off (the engine reads it so too); empty when nothing is left. */
export function conditionOf(text: string): string {
  let t = text.trim();
  if (t.startsWith('{=') && t.endsWith('}')) t = t.slice(2, -1);
  else if (t.startsWith('=')) t = t.slice(1);
  return t.trim();
}
