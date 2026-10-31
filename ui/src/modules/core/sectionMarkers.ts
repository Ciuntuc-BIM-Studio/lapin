/** What View → Sections… shows (docs/sections.md, decision 223): the sections' markers a view shows, grouped under their layers. */

export interface SectionMarker { id: string; name: string; layer: string; shown: boolean; hiddenIn: number }
export interface MarkerLayer { name: string; shown: boolean }
export interface SectionMarkers { sections: SectionMarker[]; layers: MarkerLayer[] }

/** The sections under their layers, in the layers' order (a layer with no section stays, empty), each layer's sections by name. */
export function groupByLayer(d: SectionMarkers): { layer: MarkerLayer; sections: SectionMarker[] }[] {
  const groups = d.layers.map((layer) => ({ layer, sections: [] as SectionMarker[] }));
  for (const s of d.sections) {
    let g = groups.find((x) => x.layer.name === s.layer);
    if (!g) { g = { layer: { name: s.layer, shown: true }, sections: [] }; groups.push(g); }
    g.sections.push(s);
  }
  for (const g of groups) g.sections.sort((a, b) => a.name.localeCompare(b.name));
  return groups;
}

/** How many views hide the section, as the dialog says it; nothing when none does. */
export const hiddenInText = (n: number): string => (n <= 0 ? '' : n === 1 ? 'hidden in 1 view' : `hidden in ${n} views`);
