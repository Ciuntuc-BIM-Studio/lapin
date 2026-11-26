/**
 * Planning → Tasks from product × location… (docs/plan-4d5d.md, "Produs × Loc"): an activity ("Install doors"), a product
 * of the project's PBS and the locations (every storey by default) make one task a location under a summary, each with its
 * rule (the product × that location), linked finish-to-start in the locations' order as the generator links a step's
 * storeys, timed by a norm on their quantity when one is picked. The PBS itself: the classification chosen as the project's,
 * Lapin's indicative starter, or the user's own (Manage → Classifications…, its items imported from a table). One undo step.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { PendingDialog } from '../core/PendingDialog';
import { openImportTable } from '../core/tables/tables';
import { crossTasks, defaultLocations, locationGroups, productOptions, togglePicked, type LocationOption, type PbsItem } from './breakdown';
import type { NormRow } from './generator';
import { openGantt } from './schedule';

interface PbsDto { system: { id: string; name: string; source?: string | null } | null; chosen: boolean; systems: { id: string; name: string }[]; items?: PbsItem[]; unlabelled?: number }

function useAsk<T>(name: string, args: Record<string, unknown> = {}): T | null {
  const version = useSession((s) => s.version);
  const [v, setV] = useState<T | null>(null);
  useEffect(() => { let live = true; void query<T>(name, args).then((x) => { if (live) setV(x); }); return () => { live = false; }; }, [version]); // eslint-disable-line react-hooks/exhaustive-deps
  return v;
}

export function BreakdownDialog() {
  const pbs = useAsk<PbsDto>('planning.pbs');
  const options = useAsk<{ locations?: LocationOption[] }>('planning.ruleOptions');
  const gen = useAsk<{ norms: { rows: NormRow[] } }>('planning.generatorOptions');
  const [name, setName] = useState('');
  const [product, setProduct] = useState('');
  const [picked, setPicked] = useState<string[] | null>(null);
  const [norm, setNorm] = useState('');
  const [link, setLink] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const locations = useMemo(() => options?.locations ?? [], [options]);
  const chosen = picked ?? defaultLocations(locations);
  if (!pbs || !options) return <PendingDialog title="Tasks from product × location" />;
  const close = () => useUi.setState({ dialog: null });
  const items = pbs.items ?? [];
  const title = items.find((i) => i.code === product)?.title;
  const activity = name.trim() || (title ? `Install ${title.toLowerCase()}` : '');
  const tasks = crossTasks(activity, locations, chosen);
  const make = async () => {
    setError(null);
    try {
      await execute('planning.tasksFromBreakdown', { name: activity, products: product ? [product] : [], locations: chosen, link, ...(norm ? { norm } : {}) });
      openGantt();
      close();
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Tasks from product × location">
      <div className="dialog breakdown-dialog" data-testid="breakdown-dialog">
        <header className="dialog-head">
          <span className="panel-title">Tasks from product × location</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <label className="field-line"><span className="eyebrow">Product breakdown (PBS)</span>
            <span className="row">
              <select value={pbs.system?.id ?? ''} onChange={(e) => void execute('planning.setPbs', { system: e.target.value || null })} data-testid="pbs-system">
                {!pbs.system && <option value="">None</option>}
                {pbs.systems.map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
              </select>
              {!pbs.systems.some((s) => s.name === 'PBS') && <button type="button" className="btn small" onClick={() => void execute('planning.pbsStarter')} data-testid="pbs-starter-dialog" title="A few products by trade, Lapin's own codes, indicative: change them or import yours">Starter PBS</button>}
              {pbs.system && <button type="button" className="btn small" onClick={() => openImportTable('core.classification', 'Import classification items', { system: pbs.system!.name })} title="Your own codes and names from a CSV or Excel table (code, title, parent)">Import…</button>}
              <button type="button" className="link-button small" onClick={() => useUi.setState({ dialog: 'classifications' })}>Manage → Classifications…</button>
            </span>
          </label>
          {pbs.system?.source && <p className="muted small" data-no-i18n>{pbs.system.source}</p>}
          {pbs.unlabelled ? <p className="muted small">{`${pbs.unlabelled} elements carry no code of it: label their types (or them) in Properties → Classification.`}</p> : null}
          <label className="field-line"><span className="eyebrow">Product</span>
            <select value={product} onChange={(e) => setProduct(e.target.value)} data-testid="breakdown-product">
              <option value="">Any product</option>
              {productOptions(items).map((o) => <option key={o.code} value={o.code} data-no-i18n>{`${o.label} (${o.count})`}</option>)}
            </select>
          </label>
          <label className="field-line"><span className="eyebrow">Activity</span>
            <input value={name} placeholder={activity || 'Install doors'} onChange={(e) => setName(e.target.value)} data-testid="breakdown-activity" />
          </label>
          <div className="field-line"><span className="eyebrow">Locations (GBS)</span>
            <span className="chips" data-testid="breakdown-locations">
              {locationGroups(locations).map((g) => (
                <span key={g.head.id} className="chip-group">
                  {[g.head, ...g.rooms].map((l) => (
                    <button type="button" key={l.id} className={`chip${chosen.includes(l.id) ? ' on' : ''}`} onClick={() => setPicked(togglePicked(locations, chosen, l.id))} data-location={l.id}>
                      <span data-no-i18n>{l.name}</span>
                    </button>
                  ))}
                </span>
              ))}
            </span>
          </div>
          <label className="field-line"><span className="eyebrow">Norm</span>
            <select value={norm} onChange={(e) => setNorm(e.target.value)} data-testid="breakdown-norm" title="Each task's duration from its elements' quantity in the norm's unit">
              <option value="">None: 5 days each</option>
              {(gen?.norms.rows ?? []).map((n) => <option key={n.code} value={n.code} data-no-i18n>{`${n.code} ${n.name} (${n.unit})`}</option>)}
            </select>
          </label>
          <label className="check"><input type="checkbox" checked={link} onChange={(e) => setLink(e.target.checked)} />Link them finish-to-start, in this order</label>
          <ul className="muted small breakdown-list" data-testid="breakdown-tasks">
            {tasks.map((t) => <li key={t.location} data-no-i18n>{t.name}</li>)}
          </ul>
          {error && <p className="error small">{error}</p>}
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!activity || tasks.length === 0} onClick={() => void make()} data-testid="breakdown-make">
            {tasks.length === 1 ? 'Make 1 task' : `Make ${tasks.length} tasks`}
          </button>
        </footer>
      </div>
    </div>
  );
}
