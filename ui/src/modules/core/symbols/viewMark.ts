/**
 * A view's Mark in its properties (in-app report #167): what its markers write without one of its own, shown as the field's
 * placeholder. Lapin's own section head and elevation mark write the view's name; a symbol for the head (the section's own, else
 * the project's) writes its number on its sheet, as callouts and view titles do; empty, the mark is automatic as before.
 */

import type { SlotInfo, ViewMarkInfo } from './SlotPicker';

/** The slot of a view's marker in other views: a section's head, an elevation's mark; none for other views. */
export function headSlot(kind: unknown): string | null {
  return kind === 'section' ? 'sectionHead' : kind === 'elevation' ? 'elevationHead' : null;
}

/** What the view's markers write without a mark of its own; null: nothing yet (a symbol's number while it is on no sheet). */
export function automaticMark(kind: unknown, slots: SlotInfo[], mark: ViewMarkInfo | null): string | null {
  if (!mark) return null;
  const slot = headSlot(kind);
  const symbol = slot ? slots.find((s) => s.id === slot) : undefined;
  if (slot && !(symbol?.own || symbol?.project)) return mark.name;
  return mark.number || null;
}
