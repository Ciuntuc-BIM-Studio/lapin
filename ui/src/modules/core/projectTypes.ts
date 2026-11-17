/**
 * File → Import types from a project… (issue #74): what the engine read out of another .lapin (`library.projectTypes`: its
 * types with the materials each needs, and what each is to the open project) and the import's arguments
 * (`library.importProjectTypes`). The rules are the type packs' (a .lapintypes): materials by name, a type of the same name
 * kept beside as „Name (2)” by default; a type the project has under the very GlobalId is that type, replaced in place by default.
 */

import type { LibraryChoiceState, MaterialConflict, TypeConflict } from './LibraryChoices';

export type TypeStatus = 'new' | 'same' | 'name' | 'id';

export interface SourceType {
  ref: string; class: string; label: string; name: string; count: number; materials: string[];
  status: TypeStatus; exists: boolean; differs?: string[];
}

/** What `library.projectTypes` answers: the source project's name, its types as a pack's text, and what differs from the open project. */
export interface ProjectTypesRead { name: string | null; text: string; types: SourceType[]; materials: MaterialConflict[]; conflicts: number }

/** The source file, picked by File → Import types from a project… and read by its dialog: a path on the desktop, bytes in a browser. */
export const projectTypesFile: { current: { name: string; path?: string; data?: string } | null } = { current: null };

/** The types to choose for: those the project has (by GlobalId or by name) made otherwise. */
export const typeConflicts = (types: SourceType[]): TypeConflict[] =>
  types.filter((t) => (t.status === 'name' || t.status === 'id') && (t.differs?.length ?? 0) > 0)
    .map((t) => ({ key: t.ref, name: t.name, label: t.label, differs: t.differs! }));

/** Every choice at its default: a material kept as the project's; a type of the same GlobalId replaced, one of the same name kept beside. */
export function defaultProjectChoices(read: Pick<ProjectTypesRead, 'types' | 'materials'>): LibraryChoiceState {
  return {
    materials: Object.fromEntries(read.materials.map((m) => [m.name, 'keep'])),
    types: Object.fromEntries(typeConflicts(read.types).map((t) => [t.key, read.types.find((x) => x.ref === t.key)?.status === 'id' ? 'update' : 'both'])),
  };
}

/** The import's arguments: the chosen types, the choices for them and for the materials. */
export function importArgs(read: Pick<ProjectTypesRead, 'text'>, chosen: Set<string>, choices: LibraryChoiceState): Record<string, unknown> {
  return {
    text: read.text,
    refs: [...chosen],
    materials: choices.materials,
    types: Object.fromEntries(Object.entries(choices.types).filter(([ref]) => chosen.has(ref))),
  };
}

/** The types by kind, then name. */
export const byKind = (list: SourceType[]): SourceType[] => [...list].sort((a, b) => a.label.localeCompare(b.label) || a.name.localeCompare(b.name));
