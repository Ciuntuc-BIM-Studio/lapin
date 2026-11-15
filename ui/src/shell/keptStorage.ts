/**
 * Keys the interface keeps in localStorage per project, graph or window (a graph's own values, a palette's place) would
 * grow without end (audit U13): `setCapped` writes one and forgets the least recently written beyond `cap` of its family.
 */

const ORDER = (family: string) => `${family}.__order`;

function readOrder(family: string): string[] {
  try { const v = JSON.parse(localStorage.getItem(ORDER(family)) ?? '[]'); return Array.isArray(v) ? v.filter((x): x is string => typeof x === 'string') : []; } catch { return []; }
}

/** The keys of a family that stay after writing `key`, newest first, and the ones to drop; apart from storage so it can be tested. */
export function touchOrder(order: readonly string[], key: string, cap: number): { keep: string[]; drop: string[] } {
  const all = [key, ...order.filter((k) => k !== key)];
  return { keep: all.slice(0, cap), drop: all.slice(cap) };
}

/** Writes `key` (its name starts with `family.`) and drops the oldest of the family beyond `cap`. */
export function setCapped(family: string, key: string, value: string, cap = 200): void {
  try {
    localStorage.setItem(key, value);
    const { keep, drop } = touchOrder(readOrder(family), key, cap);
    for (const k of drop) localStorage.removeItem(k);
    localStorage.setItem(ORDER(family), JSON.stringify(keep));
  } catch { /* this session only */ }
}

/** Removes `key` from its family. */
export function removeCapped(family: string, key: string): void {
  try {
    localStorage.removeItem(key);
    localStorage.setItem(ORDER(family), JSON.stringify(readOrder(family).filter((k) => k !== key)));
  } catch { /* nothing kept */ }
}
