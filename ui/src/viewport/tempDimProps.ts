/**
 * The Temporary Dimension Properties (decision 738, Revit's dialog of that name): where the temporary dimensions of walls,
 * columns and beams measure from (centrelines, faces, centre of core, faces of core, or Lapin's reference line) and where a
 * door's or a window's do (centrelines or openings), kept for the user (app/prefs.ts) for every project. Opened from Annotate
 * and Manage → Temporary dimensions…, from a dimension dot's right click, and shown on Preferences → Grid and snaps.
 */

import { useUi } from '../app/store';
import type { DimRef, OpeningDimRef } from './lineDims';

export const TEMP_DIM_DIALOG = 'tempDimProperties';

export const openTempDimProperties = () => useUi.setState({ dialog: TEMP_DIM_DIALOG });

/** The two choices the dialog holds. */
export interface TempDimChoice { walls: DimRef; openings: OpeningDimRef }

/** What each choice means, said under it. */
export const DIM_REF_NOTES: Record<DimRef, string> = {
  axis: 'Axis to axis; a column\'s centre.',
  faces: 'Finish face to finish face.',
  coreCentre: 'The middle of the core layers: the axis unless the core is off it.',
  core: 'Core face to core face, the finish left out (the default).',
  reference: 'From the element\'s reference line to the other\'s near face (Lapin\'s first way).',
};
