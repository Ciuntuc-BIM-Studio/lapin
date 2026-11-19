/**
 * A straight wall's temporary dimensions in plan (decision 693): across it, from its reference (the line it keeps, else its axis)
 * to the nearest parallel wall or grid axis on each side that runs beside it; along it, from each free end to the first wall its
 * line runs into beyond that end. The measuring is shared with columns and beams since decision 733 (viewport/lineDims.ts,
 * where the user's dimension reference chooses the ends); this keeps the walls' names. Pure.
 */

import { lineDims, type AxisLine, type DimOptions, type DimLineInfo, type DimPick, type Host, type Joins, type LineDim } from '../../viewport/lineDims';

export { commitAcross, commitEnd, framePoint, frameOf, hostOf } from '../../viewport/lineDims';
export type { AxisLine, Host } from '../../viewport/lineDims';

/** A wall as the engine gives it: its axis, its faces and core faces (left of the axis), what is joined at its ends and along it. */
export type WallLineInfo = DimLineInfo & { joins: Joins };
export type WallDimPick = DimPick;
export type WallDim = LineDim;

/** The host's dimensions: across to each side's nearest parallel wall or axis, along from each free end. */
export const wallDims = (h: Host, walls: DimLineInfo[], axes: AxisLine[], picks: Record<string, DimPick>, o: DimOptions = {}): WallDim[] =>
  lineDims(h, walls, axes, picks, o);
