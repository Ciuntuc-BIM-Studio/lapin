/**
 * The planting library as the inspector shows it (decision 234): each
 * species under its group, named in the interface's language (the Romanian
 * name when the interface is Romanian), its Latin name beside it.
 */

export interface Species { name: string; type: string; height: number; crown: number; color: string; share: number; latin?: string; ro?: string; family?: string }

/** The picker's groups, in their order. */
export const SPECIES_GROUPS = ['Fruit trees', 'Conifers', 'Deciduous trees', 'Shrubs', 'Ground cover'] as const;
export type SpeciesGroup = (typeof SPECIES_GROUPS)[number];

export function speciesGroup(s: Species): SpeciesGroup {
  if (s.type === 'grass' || s.family === 'ground') return 'Ground cover';
  if (s.type === 'bush') return 'Shrubs';
  if (s.family === 'fruit') return 'Fruit trees';
  if (s.family === 'fir' || s.family === 'pine') return 'Conifers';
  return 'Deciduous trees';
}

/** The species' name in a language: Romanian has the library's own, the others the English. */
export const speciesName = (s: Species, lang: string): string => (lang === 'ro' && s.ro ? s.ro : s.name);

/** The catalogue by group, the groups in their order, empty ones left out. */
export function groupedSpecies(catalog: Species[]): [SpeciesGroup, Species[]][] {
  return SPECIES_GROUPS.map((g) => [g, catalog.filter((s) => speciesGroup(s) === g)] as [SpeciesGroup, Species[]]).filter(([, list]) => list.length > 0);
}
