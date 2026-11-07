/**
 * A configurator's "Apply a style…" select that remembers (report #111): the style a type was given stays chosen in it,
 * "<Style> (modified)" once one of the type's fields differs from what the style sets, and the placeholder only while no
 * style was applied; picking the style again while modified applies it again. The type keeps the style's id in its spec
 * (the caller's `value`); the caller says whether it is `modified`, `sameAs` comparing the style's fields only, so the
 * engine's own extra fields and a null against a missing field do not count as changes.
 */

import { t, useLang } from '../i18n';

export interface StyleChoice { id: string; label: string; title?: string; /** A name the user gave (the project's own style): not translated. */ own?: boolean }
export interface StyleGroup { label?: string; items: StyleChoice[] }

export function StyleSelect({ groups, value, modified, placeholder = 'Apply a style…', title, field, onApply }: {
  groups: StyleGroup[];
  /** The style applied (its id); null or undefined: none yet. */
  value?: string | null;
  /** Some field differs from the style's. */
  modified?: boolean;
  placeholder?: string;
  title?: string;
  field?: string;
  onApply: (id: string) => void;
}) {
  useLang((s) => s.lang); // the chosen style's name, put into the language here (its option is not walked by the translator)
  const all = groups.flatMap((g) => g.items);
  const current = value ? all.find((s) => s.id === value) ?? null : null;
  const shown = (s: StyleChoice) => (s.own ? s.label : t(s.label));
  // Modified, the style's state stands first and the style itself stays in its list, picked to apply it again; a style gone
  // from the lists (a project style deleted) leaves the placeholder.
  const MODIFIED = '\u0000modified';
  const option = (s: StyleChoice) => <option key={s.id} value={s.id} title={s.title} data-no-i18n={s.own ? '' : undefined}>{s.label}</option>;
  return (
    <select data-field={field} value={!current ? '' : modified ? MODIFIED : current.id} title={title} data-style-state={!current ? 'none' : modified ? 'modified' : 'applied'}
      onChange={(e) => { if (e.target.value && e.target.value !== MODIFIED) onApply(e.target.value); }}>
      {!current && <option value="">{placeholder}</option>}
      {current && modified && <option value={MODIFIED} data-no-i18n>{`${shown(current)} ${t('(modified)')}`}</option>}
      {groups.map((g, i) => g.label
        ? (g.items.length > 0 && <optgroup key={i} label={t(g.label)}>{g.items.map(option)}</optgroup>)
        : g.items.map(option))}
    </select>
  );
}

/**
 * Whether `actual` still has what `expected` (a style's fields) sets: objects key by key over the expected's keys,
 * arrays item by item, numbers within a micrometre; null, undefined and a missing field are the same.
 */
export function sameAs(expected: unknown, actual: unknown): boolean {
  if (expected == null || actual == null) return expected == null && actual == null;
  if (typeof expected === 'number' && typeof actual === 'number') return Math.abs(expected - actual) < 1e-6;
  if (Array.isArray(expected)) return Array.isArray(actual) && expected.length === actual.length && expected.every((x, i) => sameAs(x, actual[i]));
  if (typeof expected === 'object') {
    if (typeof actual !== 'object' || Array.isArray(actual)) return false;
    const a = actual as Record<string, unknown>;
    return Object.entries(expected as Record<string, unknown>).every(([k, v]) => sameAs(v, a[k]));
  }
  return expected === actual;
}
