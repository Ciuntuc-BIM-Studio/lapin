/**
 * Delete on a sheet (forum topic 91): with views active on the paper the key takes those off the sheet (one undo step), never
 * the sheet itself; a sheet is deleted from the project browser. The sheet pane registers what is active; the global Delete
 * command asks first.
 */

let handler: (() => boolean) | null = null;
/** The active sheet pane's: true when it took the key. Null when the pane goes. */
export function setSheetDelete(h: (() => boolean) | null): void { handler = h; }
/** Delete pressed: true when a sheet's active views took it. */
export function takeSheetDelete(): boolean { return handler ? handler() : false; }

/** Whether Delete belongs to the active views: some are active, and the selection is nothing or the sheet itself (a selected symbol, a wall elsewhere keep the key). */
export function deleteTakesViewports(active: readonly string[], selected: readonly string[], sheetId: string): boolean {
  return active.length > 0 && selected.every((id) => id === sheetId);
}

/** The commands that take views off a sheet: one edit, so one undo step. */
export function removeSteps(sheetId: string, viewportIds: readonly string[]): { command: string; args: Record<string, unknown> }[] {
  return [...new Set(viewportIds)].map((viewportId) => ({ command: 'sheet.removeViewport', args: { id: sheetId, viewportId } }));
}
