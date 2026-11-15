/**
 * The 3D overlays (a sketch, an axis drag, a move, the constraint marks) listen to the window's keys in the capture phase:
 * a key typed in a field or inside a dialog is never theirs (audit U8, I5).
 */
export function keyIsForField(e: Pick<KeyboardEvent, 'target'>, allow?: string): boolean {
  const el = e.target as HTMLElement | null;
  if (!el?.closest) return false;
  if (el.closest('[role="dialog"]')) return true;
  const field = el.closest('input, textarea, select, [contenteditable]');
  return !!field && !(allow && field.closest(allow));
}
