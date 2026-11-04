/**
 * Small formatters and helpers the core dialogs share: a date and time, a
 * file's size, a capitalised label, a storey's level, a set's toggle, the
 * export origins, the project's phases, and a list of lengths typed in one
 * field. One copy each, so every dialog shows them alike.
 */

import { useEffect, useState } from 'react';
import { query } from '../../engine/session';
import { formatLength, formatLevel, parseLength, type Unit } from '../../engine/unitsStore';

/** An ISO date and time as the reader's locale writes it (the text itself when it does not read; '' for none). */
export function when(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
}

/** A file's size: MB from one MB, kB from one kB, bytes below. */
export const formatSize = (bytes: number) => (bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : bytes >= 1024 ? `${Math.round(bytes / 1024)} kB` : `${bytes} B`);

/** A word with its first letter capitalised (a category, a role). */
export const label = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s);

/** A level (metres) as storeys show it, in the project's units: +3.00, −0.50; +10'-0" in feet and inches. */
export const level = (z: number, u?: Unit) => formatLevel(z, u);

/** The set of what is left out with `keys` ticked on (taken out of it) or off (put in). */
export function toggle(set: ReadonlySet<string>, keys: string[], on: boolean): Set<string> {
  const next = new Set(set);
  for (const k of keys) { if (on) next.delete(k); else next.add(k); }
  return next;
}

/** Where an export puts its origin. */
export const ORIGINS: [string, string][] = [['base', 'the project base point'], ['internal', 'the internal origin'], ['survey', 'the survey point (real coordinates)']];

/** The project's phases (id and name), read once when the dialog opens; empty without the phases module. */
export function usePhaseList(): { id: string; name: string }[] {
  const [phases, setPhases] = useState<{ id: string; name: string }[]>([]);
  useEffect(() => {
    let live = true;
    void query<{ phases: { id: string; name: string }[] }>('phases.settings').then((r) => { if (live) setPhases(r?.phases ?? []); });
    return () => { live = false; };
  }, []);
  return phases;
}

/** Lengths (metres) as one field shows them: separated by "; " (an imperial length has spaces of its own). */
export const formatLengths = (ms: readonly number[], u?: Unit) => ms.map((m) => formatLength(m, u)).join('; ');

/**
 * Lengths typed in one field, in the project's unit or their own: separated by
 * semicolons, or by spaces when no imperial mark is typed. Empty pieces are
 * skipped (a trailing separator adds nothing); null when one does not read.
 */
export function parseLengths(text: string, u?: Unit): number[] | null {
  const pieces = text.split(';').flatMap((p) => (/['"″′]/.test(p) ? [p] : p.split(/\s+/))).map((p) => p.trim()).filter(Boolean);
  const out: number[] = [];
  for (const p of pieces) {
    const v = parseLength(p, u);
    if (v == null || !Number.isFinite(v)) return null;
    out.push(v);
  }
  return out;
}
