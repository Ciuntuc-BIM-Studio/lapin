/**
 * Classifications, keynotes and shared parameters, the pure side (docs/plan-user-extensibility.md §7.3): a system the user brings
 * (Uniclass, OmniClass, CCI, the office's own), the codes an element or a type carries (`classification`: [{system, code}], the
 * system by its id), the fields a tag reads (`{classification.code}`, `{classification.title}`, `{classification.<System>}`), and
 * the shared parameter definitions a parameter may be made from.
 */

export interface ClassSystem { id: string; name: string; source?: string | null; edition?: string | null; items: number; uses: number }
export interface ClassItem { code: string; title: string; parent: string | null; children: boolean }
export interface ClassRef { system: string; code: string }
export interface ParamDef { id: string; name: string; kind: string; pset?: string | null; options?: string[] | null; classes?: string[] | null; description?: string | null }

/** The codes a node carries, as {system, code} (anything else in the attribute left out). */
export function refsOf(value: unknown): ClassRef[] {
  if (!Array.isArray(value)) return [];
  return value.flatMap((v) => (v && typeof v === 'object' && typeof (v as ClassRef).system === 'string' && typeof (v as ClassRef).code === 'string' ? [{ system: (v as ClassRef).system, code: (v as ClassRef).code }] : []));
}

/** The code a node has in a system (the system's id or its name), or null. */
export function codeIn(refs: ClassRef[], systems: ClassSystem[], system: string): string | null {
  const id = systems.find((s) => s.id === system || s.name.toLowerCase() === system.toLowerCase())?.id ?? system;
  return refs.find((r) => r.system === id)?.code ?? null;
}

/** The fields a tag may read for the systems the project has: the first code, its title, then each system's own code. */
export function classificationFields(systems: ClassSystem[]): [string, string][] {
  return [['classification.code', 'Code (the first)'], ['classification.title', 'Title (of the first)'], ...systems.map((s) => [`classification.${s.name}`, s.name] as [string, string])];
}

/** What an element's codes read as in one line: "Uniclass Ss_25_10 · CCI 21". */
export function refsText(refs: ClassRef[], systems: ClassSystem[]): string {
  return refs.map((r) => `${systems.find((s) => s.id === r.system)?.name ?? '?'} ${r.code}`).join(' · ');
}

/** A free name among the systems (a number after it when it is taken). */
export function freeSystemName(base: string, systems: ClassSystem[]): string {
  const taken = new Set(systems.map((s) => s.name.toLowerCase()));
  if (!taken.has(base.toLowerCase())) return base;
  for (let n = 2; ; n++) if (!taken.has(`${base} ${n}`.toLowerCase())) return `${base} ${n}`;
}

/** The kinds a shared parameter has, as a parameter's. */
export const PARAM_KINDS = ['text', 'number', 'length', 'area', 'angle', 'bool', 'choice'] as const;

/** A definition's choices typed as a list (one per comma); empty entries dropped. */
export const parseOptions = (text: string): string[] => text.split(',').map((x) => x.trim()).filter(Boolean);

/** The definitions to suggest as a parameter's name is typed (those whose name starts with, then contains, the text). */
export function suggestDefs(defs: ParamDef[], text: string): ParamDef[] {
  const t = text.trim().toLowerCase();
  if (!t) return defs;
  return [...defs.filter((d) => d.name.toLowerCase().startsWith(t)), ...defs.filter((d) => !d.name.toLowerCase().startsWith(t) && d.name.toLowerCase().includes(t))];
}

/** The definition a typed name is (case aside). */
export const defNamed = (defs: ParamDef[], name: string): ParamDef | undefined => defs.find((d) => d.name.toLowerCase() === name.trim().toLowerCase());
