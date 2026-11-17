/**
 * Which extension lines a dimension leaves out (issue #66, as AutoCAD's dimension styles): none, a string's first, its
 * last, both. A dimension style says it for its dimensions; a dimension's own choice (its `extSuppress`) goes before.
 */
export const EXT_SUPPRESS = ['none', 'first', 'second', 'both'] as const;
export type ExtSuppress = (typeof EXT_SUPPRESS)[number];

/** How each choice reads, in the standards' table and in a dimension's properties. */
export const EXT_SUPPRESS_LABELS: Record<string, string> = { none: 'all shown', first: 'first off', second: 'second off', both: 'both off' };

/** A dimension's own choice as stored: one of the choices, or null (empty: as its style says). */
export const extSuppressAttr = (value: string): ExtSuppress | null => ((EXT_SUPPRESS as readonly string[]).includes(value) ? (value as ExtSuppress) : null);
