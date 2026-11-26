/**
 * Product × Location in a task's rule (docs/plan-4d5d.md, "Produs × Loc"): the product picked in the project's PBS (a node
 * takes what is under it) and the locations in the GBS (the building, its storeys, their rooms; any of them), with how many
 * elements the pair takes now and, when the task has a norm, their quantity in its unit. Each pick is one step to undo.
 */

import { useEffect, useState } from 'react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import type { TaskDto, TaskRule } from './gantt';
import { locationGroups, productCodes, productOptions, setProduct, toggleLocation, type LocationOption, type PbsItem } from './breakdown';

export interface BreakdownOptions { products?: PbsItem[]; locations?: LocationOption[]; pbs?: { id: string; name: string } | null }

interface Preview { count: number; quantity?: number; unit?: string }

/** What a rule takes now (and its quantity in a norm's unit), asked again when the rule or the model changes. */
export function useRulePreview(rule: TaskRule | undefined, part: string | undefined, norm: string | undefined): Preview | null {
  const version = useSession((s) => s.version);
  const [p, setP] = useState<Preview | null>(null);
  const key = JSON.stringify([rule ?? null, part ?? null, norm ?? null]);
  useEffect(() => {
    let live = true;
    if (!rule) { setP(null); return; }
    void query<Preview>('planning.rulePreview', { rule, ...(part ? { part } : {}), ...(norm ? { norm } : {}) }).then((x) => { if (live) setP(x); });
    return () => { live = false; };
  }, [key, version]); // eslint-disable-line react-hooks/exhaustive-deps
  return p;
}

export function BreakdownPickers({ task, rule, options, onChange }: { task: TaskDto; rule: TaskRule; options: BreakdownOptions; onChange: (r: TaskRule) => void }) {
  const preview = useRulePreview(rule, task.part, task.norm);
  const items = options.products ?? [];
  const locations = options.locations ?? [];
  const code = productCodes(rule)[0] ?? '';
  const picked = new Set(rule.locations ?? []);
  const chip = (l: LocationOption) => (
    <button type="button" key={l.id} className={`chip${picked.has(l.id) ? ' on' : ''}`} onClick={() => onChange(toggleLocation(rule, l.id))} data-rule={`locations:${l.id}`}
      title={l.level === 'room' ? 'A room: what stands in it, and the doors and walls on its edge' : `${l.count ?? 0}`}>
      <span data-no-i18n>{l.name}</span>{l.count != null && <span className="muted small" data-no-i18n>{` ${l.count}`}</span>}
    </button>
  );
  return (
    <div className="rule-condition breakdown-pickers" data-testid="breakdown-pickers">
      <span className="eyebrow" title="The activity is the task; the product is what it builds, the location where">Product (PBS) × Location (GBS)</span>
      {!options.pbs ? (
        <span className="muted small">No product breakdown yet: <button type="button" className="link-button" onClick={() => void execute('planning.pbsStarter')} data-testid="pbs-starter">Make the starter PBS</button>
          {' · '}<button type="button" className="link-button" onClick={() => useUi.setState({ dialog: 'classifications' })}>Manage → Classifications…</button></span>
      ) : (
        <select value={code} onChange={(e) => onChange(setProduct(rule, e.target.value || null))} data-testid="rule-product" title={`Codes of ${options.pbs.name}: a node takes what is under it`}>
          <option value="">Any product</option>
          {productOptions(items).map((o) => <option key={o.code} value={o.code} data-no-i18n>{`${o.label} (${o.count})`}</option>)}
          {code && !items.some((i) => i.code === code) && <option value={code} data-no-i18n>{code}</option>}
        </select>
      )}
      <span className="chips" data-testid="rule-locations">
        {locationGroups(locations).map((g) => (
          <span key={g.head.id} className="chip-group">{chip(g.head)}{g.rooms.map(chip)}</span>
        ))}
      </span>
      {preview && (
        <span className="muted small" data-testid="rule-preview">
          {preview.count === 1 ? '1 element' : `${preview.count} elements`}
          {preview.quantity != null && <span data-no-i18n>{` · ${preview.quantity} ${preview.unit ?? ''}`}</span>}
        </span>
      )}
    </div>
  );
}
