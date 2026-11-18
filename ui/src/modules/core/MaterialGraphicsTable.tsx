/**
 * A material's 2D graphics, one editor per place it is drawn: cut in a section, cut in a plan, seen in an elevation (report #76,
 * decision 678). The Materials dialog's 2D graphics tab and a layer table's hatch button (forum #30, decision 493) both edit it
 * here, with the same command (material.update, one undo step each change). The editor itself is MaterialGraphicsEditor; this
 * keeps the names the rest of the interface knows it by.
 */

export { MATERIAL_KINDS, type HatchPattern } from './MaterialGraphicsEditor';
export { MaterialGraphicsEditor as MaterialGraphicsTable } from './MaterialGraphicsEditor';
export type { MaterialKind, MaterialRow, OwnGraphics, ResolvedGraphics } from './materialGraphicsModel';
