/**
 * How a dimension style writes an opening's height under a dimension line across it (decision 338; forum topic 229):
 * each choice's format, as the engine has it (Standards.OpeningFormat), and the format filled (OpeningHeights.Fill), so the
 * Drafting standards dialog shows the format a preset stands for and what a typed one writes. The engine draws it.
 */

/** Each choice's format; custom without a text of its own is the stacked one. */
export function openingFormat(choice: string, text?: string | null): string | null {
  switch (choice) {
    case 'number': return '{h}';
    case 'h': return 'h={h}';
    case 'hp': return 'h={h}|p={p}';
    case 'slash': return '{h}[/{p}]';
    case 'line': return 'h={h}[, p={p}]';
    case 'custom': return text?.trim() ? text.trim() : 'h={h}|p={p}';
    default: return null;
  }
}

/**
 * A format filled: {h} the height, {p} the sill, | a new row; a part in [ ] is written only when there is a sill, and a row
 * naming the sill outside brackets is left out without one. Empty rows are dropped.
 */
export function fillOpening(format: string, h: string, sill: string | null): string[] {
  const values = (s: string) => s.split('{h}').join(h).split('{p}').join(sill ?? '');
  const rows: string[] = [];
  for (const row of format.split('|')) {
    let out = '';
    let keep = true;
    let i = 0;
    while (i < row.length) {
      const open = row.indexOf('[', i);
      const close = open < 0 ? -1 : row.indexOf(']', open + 1);
      const plain = close < 0 ? row.slice(i) : row.slice(i, open);
      if (sill == null && plain.includes('{p}')) keep = false;
      out += values(plain);
      if (close < 0) break;
      const part = row.slice(open + 1, close);
      if (sill != null || !part.includes('{p}')) out += values(part);
      i = close + 1;
    }
    if (keep && out.trim()) rows.push(out.trim());
  }
  return rows;
}
