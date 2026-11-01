/** The imported types the Map imported types dialog lists (docs/spec-ifc.md, Î9), and how it names and orders them. */

export interface ImportedType { id: string; name: string; class: string; file?: string | null; sourceName: string; count: number; candidates: { id: string; name: string }[] }
export interface RememberedType { key: string; class: string; name: string; to: string; toName?: string | null }
/** What a type class holds, as the dialog names its rows. */
export const KINDS: Record<string, string> = {
  IfcWallType: 'Walls', IfcSlabType: 'Floors', IfcDoorType: 'Doors', IfcWindowType: 'Windows',
  IfcColumnType: 'Columns', IfcBeamType: 'Beams', IfcFootingType: 'Footings',
};

/** The rows in the order the dialog shows them: by kind, then by name. */
export function sortTypes(types: ImportedType[]): ImportedType[] {
  const order = Object.keys(KINDS);
  const rank = (c: string) => (order.indexOf(c) < 0 ? order.length : order.indexOf(c));
  return [...types].sort((a, b) => rank(a.class) - rank(b.class) || a.name.localeCompare(b.name));
}
