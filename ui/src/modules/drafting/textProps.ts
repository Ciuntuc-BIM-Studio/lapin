/**
 * A drafted text's properties (forum topic 148): its style chosen from the project's text styles (by id; a name typed earlier
 * is matched by the engine too), its alignment, its width. Each change is one `element.update`, one undo step.
 */

export interface StyleEntry { id: string; name: string; height?: number }

/** The text's alignments, as the engine keeps them (`align`); left is the default and is kept as none. */
export const TEXT_ALIGNS = ['left', 'center', 'right'] as const;
export type TextAlign = (typeof TEXT_ALIGNS)[number];
export const TEXT_ALIGN_LABELS: Record<TextAlign, string> = { left: 'Left', center: 'Centre', right: 'Right' };

/** The alignment a text has: its own, else left. */
export function alignOf(v: unknown): TextAlign {
  return v === 'center' || v === 'right' ? v : 'left';
}

/** The `align` attribute for a choice: left (the default) as none. */
export function alignAttr(a: string): TextAlign | null {
  return a === 'center' || a === 'right' ? a : null;
}

/**
 * The style list's options and the one chosen: the project's styles by name, the chosen one by id; a style the text names
 * that the project no longer has (or a name typed before) stays in the list, so the select shows what it holds.
 */
export function styleOptions(styles: StyleEntry[], current: unknown): { value: string; options: { id: string; label: string }[] } {
  const options = styles.map((s) => ({ id: s.id, label: s.height != null ? `${s.name} (${s.height} mm)` : s.name }));
  if (typeof current !== 'string' || !current) return { value: '', options };
  const byId = styles.find((s) => s.id === current);
  if (byId) return { value: byId.id, options };
  const byName = styles.find((s) => s.name.trim().toLowerCase() === current.trim().toLowerCase());
  if (byName) return { value: byName.id, options };
  return { value: current, options: [...options, { id: current, label: current }] };
}
