/** The drafting standards as a library file (docs/plan-user-extensibility.md §3.7): what a read of a .lapinstandards file says, and the choices for what differs. */

export interface PackEntry { kind: string; id: string; name: string; exists: boolean; same: boolean }
export interface StandardsPack { name?: string | null; entries: PackEntry[]; options: boolean }

/** The kinds of entry a file carries, as the engine names them. */
export const KIND_LABELS: Record<string, string> = {
  text: 'Text style', dim: 'Dimension style', axis: 'Grid axis style', tag: 'Tag type', layer: 'Layer', layerGroup: 'Layer group',
  lineType: 'Line type', pattern: 'Hatch pattern', symbol: 'Symbol',
};

export const kindLabel = (kind: string): string => KIND_LABELS[kind] ?? kind;

/** The key the engine's `choices` use for an entry. */
export const entryKey = (e: PackEntry): string => `${e.kind}:${e.id}`;

/** The entries the project has under the same name but made otherwise: the ones that ask. */
export const conflictsOf = (p: StandardsPack): PackEntry[] => p.entries.filter((e) => e.exists && !e.same);

/** Each conflict at the engine's default (update). */
export const defaultStandardsChoices = (p: StandardsPack): Record<string, string> => Object.fromEntries(conflictsOf(p).map((e) => [entryKey(e), 'update']));

/** The same choice for every conflict. */
export const allChoices = (p: StandardsPack, choice: string): Record<string, string> => Object.fromEntries(conflictsOf(p).map((e) => [entryKey(e), choice]));

/** How many entries are new, the same, or differ. */
export function summarise(p: StandardsPack): { added: number; same: number; differ: number } {
  return {
    added: p.entries.filter((e) => !e.exists).length,
    same: p.entries.filter((e) => e.exists && e.same).length,
    differ: conflictsOf(p).length,
  };
}
