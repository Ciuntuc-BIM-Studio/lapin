/**
 * The Place catalogue (audit 2026-11-15, S15; docs/plan-user-extensibility.md §4.1): what can be put in a view, from every
 * module that offers some (the project's types, symbols with the blocks a DXF brought, components, parts, furniture, MEP
 * equipment), by source, with one search over all of them. A card starts placing it; each source's own dialog (Symbols…,
 * Families…, My furniture…) stays its editor, opened from here. The old entry points (Manage → Symbols…, Components, Studio →
 * Families…, Interior → My furniture…) open it on their source.
 */

import { useMemo } from 'react';
import { Search } from 'lucide-react';
import { useUi } from '../app/store';
import { placeSources } from '../platform/modules';
import type { PlaceEntry, PlaceSourceDef } from '../platform/module';
import { Dialog } from './Dialog';
import { t } from '../i18n';

export { openPlaceCatalog, usePlaceCatalog } from './placeCatalogStore';
import { usePlaceCatalog } from './placeCatalogStore';

/** Whether an entry answers the search: every word of it in its name, its group or its few words, case aside. */
export function matches(e: Pick<PlaceEntry, 'name' | 'group' | 'about'>, query: string): boolean {
  const words = query.toLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) return true;
  const text = `${e.name} ${e.group ?? ''} ${e.about ?? ''}`.toLowerCase();
  return words.every((w) => text.includes(w));
}

/** Entries by their group, in the order they come. */
export function grouped(entries: PlaceEntry[]): { group: string; entries: PlaceEntry[] }[] {
  const out: { group: string; entries: PlaceEntry[] }[] = [];
  for (const e of entries) {
    const g = e.group ?? '';
    const at = out.find((x) => x.group === g);
    if (at) at.entries.push(e); else out.push({ group: g, entries: [e] });
  }
  return out;
}

function SourceList({ source, query, heading }: { source: PlaceSourceDef; query: string; heading: boolean }) {
  const all = source.useEntries();
  const shown = useMemo(() => all.filter((e) => matches(e, query)), [all, query]);
  if (heading && !shown.length) return null;
  const place = (e: PlaceEntry) => { useUi.setState({ dialog: null }); void e.place(); };
  return (
    <section className="place-source" data-place-source={source.id}>
      {heading && <h3 className="eyebrow">{source.label}</h3>}
      {!shown.length && <p className="muted small">{all.length ? t('Nothing here answers the search.') : t('Nothing to place here yet.')}</p>}
      {grouped(shown).map((g) => (
        <div key={g.group}>
          {g.group && <div className="muted small place-group">{g.group}</div>}
          <div className="catalog-cards">
            {g.entries.map((e) => (
              <button type="button" key={e.key} className="catalog-card" data-place={e.key} title={e.about ? `${e.name} · ${e.about}` : e.name} onClick={() => place(e)}>
                <span className="catalog-thumb">{e.thumb}</span>
                <span className="catalog-text">
                  <span className="catalog-name" data-no-i18n>{e.name}</span>
                  {e.about && <span className="muted small" data-no-i18n>{e.about}</span>}
                </span>
              </button>
            ))}
          </div>
        </div>
      ))}
    </section>
  );
}

export function PlaceCatalogDialog() {
  const { source, query } = usePlaceCatalog();
  const sources = placeSources();
  const current = sources.find((s) => s.id === source);
  const close = () => useUi.setState({ dialog: null });
  const shown = current ? [current] : sources;
  return (
    <Dialog title="Place catalogue" onClose={close} className="wide place-catalog" dataDialog="placeCatalog" testId="place-catalog">
      <div className="row place-catalog-bar">
        <label className="row grow"><Search size={14} aria-hidden />
          <input type="search" autoFocus placeholder={t('Search everything to place')} value={query} data-field="place-search"
            onChange={(e) => usePlaceCatalog.setState({ query: e.target.value })} />
        </label>
        {current?.manage && <button type="button" className="btn small" data-action="place-manage" onClick={() => { close(); current.manage!.run(); }}>{current.manage.label}</button>}
      </div>
      <div className="catalog-kinds" role="tablist" aria-label="Sources">
        <button type="button" role="tab" aria-selected={!current} className={`kind-chip${!current ? ' on' : ''}`} data-source="all" onClick={() => usePlaceCatalog.setState({ source: 'all' })}>All</button>
        {sources.map((s) => (
          <button type="button" key={s.id} role="tab" aria-selected={s.id === current?.id} className={`kind-chip${s.id === current?.id ? ' on' : ''}`} data-source={s.id}
            onClick={() => usePlaceCatalog.setState({ source: s.id })}>{s.label}</button>
        ))}
      </div>
      <div className="place-catalog-list">
        {shown.map((s) => <SourceList key={s.id} source={s} query={query} heading={!current} />)}
      </div>
    </Dialog>
  );
}
