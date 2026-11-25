/**
 * The project's notations (decision 864): a domain's symbols, prefixes and suffixes as templates with {…} tokens, Lapin's
 * coded texts the default, country presets (indicative) and the user's own over them. The engine resolves and draws them
 * (`notation.describe`, `notation.set`); this is what the settings dialogs read and send.
 */

import { useEffect, useState } from 'react';
import { execute, query, useSession } from '../engine/session';

export interface NotationKey { key: string; label: string; kind: 'template' | 'symbol'; default: string; value: string; own: boolean; tokens: string[]; example: string }
export interface NotationPreset { id: string; label: string; source: string; indicative: boolean }
export interface NotationDomain { id: string; label: string; preset: string | null; presets: NotationPreset[]; keys: NotationKey[] }

/** The symbols offered beside free text: the diameter's in Europe (Ø, ⌀, φ), the UK's grades (H, T, Y, R), the US's bar size (#). */
export const SYMBOLS = ['Ø', '⌀', 'φ', 'ϕ', 'H', 'T', 'Y', 'R', 'B', '#'];

/** A domain's notation as the project has it, read again after every change. */
export function useNotation(domain: string): NotationDomain | null {
  const nodes = useSession((s) => s.nodes);
  const [d, setD] = useState<NotationDomain | null>(null);
  useEffect(() => {
    let live = true;
    void query<NotationDomain[]>('notation.describe', { domain }).then((r) => { if (live && r) setD(r[0] ?? null); });
    return () => { live = false; };
  }, [nodes, domain]);
  return d;
}

/** What `notation.set` is sent for a change: a preset, one key's text (empty: back to the preset's or Lapin's), or everything back. */
export function notationPatch(domain: string, change: { preset: string | null } | { key: string; text: string } | { reset: true }): Record<string, unknown> {
  if ('reset' in change) return { domain, reset: true };
  if ('preset' in change) return { domain, preset: change.preset };
  return { domain, values: { [change.key]: change.text === '' ? null : change.text } };
}

export function setNotation(domain: string, change: Parameters<typeof notationPatch>[1]): Promise<unknown> {
  return execute('notation.set', notationPatch(domain, change));
}

/** The tokens a key reads, as the hint writes them: {d} {symbol}. */
export function tokenHint(k: NotationKey): string {
  return k.tokens.map((t) => `{${t}}`).join(' ');
}

/** The keys grouped as the dialog shows them: the texts on drawings first, the schedule's after. */
export function groupKeys(keys: NotationKey[]): { drawing: NotationKey[]; schedule: NotationKey[] } {
  const schedule = keys.filter((k) => k.key.startsWith('heading.') || k.key.startsWith('schedule.'));
  return { drawing: keys.filter((k) => !schedule.includes(k)), schedule };
}
