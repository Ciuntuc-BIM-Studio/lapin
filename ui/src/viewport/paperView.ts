/**
 * A sheet's paper as a view (decision 620): `paper:<sheetId>`, drawn in the drawing pane at 1:1, its coordinates the
 * paper's in metres from its lower-left corner (a typed length in mm is paper mm). The Draft and Annotate tools draw on it
 * as on any view; the sheet, its PDF, Print and DXF print what is drawn there.
 */

import type { UiState } from '../app/store';

export const PAPER_PREFIX = 'paper:';

export const isPaperView = (id: string | null | undefined): boolean => !!id && id.startsWith(PAPER_PREFIX);

/** The sheet a paper view is of; null for any other view. */
export const sheetOfPaper = (id: string | null | undefined): string | null => (isPaperView(id) ? id!.slice(PAPER_PREFIX.length) : null);

export const paperOf = (sheetId: string): string => PAPER_PREFIX + sheetId;

/** Drawing on a sheet: its paper in the drawing pane. */
export const openPaper = (sheetId: string): Partial<UiState> => ({ view: 'drawing', drawingViewId: paperOf(sheetId) });

/** Back from the paper to the sheet, its views placed and arranged there. */
export const openSheetOf = (paperId: string): Partial<UiState> | null => {
  const sheetId = sheetOfPaper(paperId);
  return sheetId ? { view: 'sheet', sheetId } : null;
};
