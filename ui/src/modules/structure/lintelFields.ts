/** A beam type's stock lengths as the Structure types dialog types them (report #135): millimetres, spaces or | between. */

export function formatLengths(lengths: number[]): string {
  return lengths.map((l) => String(Math.round(l * 10000) / 10)).join(' | ');
}

/** Metres, sorted, each once; null when one is not a positive number. Empty: none (cut to length). */
export function parseLengths(text: string): number[] | null {
  const parts = text.split(/[|/;\s]+/).map((x) => x.trim()).filter(Boolean);
  const o: number[] = [];
  for (const p of parts) {
    const v = Number(p.replace(',', '.'));
    if (!(v > 0)) return null;
    o.push(v / 1000);
  }
  return [...new Set(o)].sort((a, b) => a - b);
}
