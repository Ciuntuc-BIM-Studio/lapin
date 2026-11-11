/**
 * The kitchen configurator's units (a tester's report #76, decision 472): the main run's base line and the wall line
 * above it, each a list of units edited in place — the type, a width (any from 100 to 800 mm for a cabinet, the sizes
 * for an appliance), the front or the appliance's options, a wall unit's own height — moved, added and taken out.
 * Every change is one step on the type (furniture.updateType), and the preview beside it is the engine's.
 */

import { ChevronLeft, ChevronRight, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { CHOICE_LABELS, type FurnitureSpec } from './furniture';
import {
  addUnit, valueOf, fieldsFor, findType, fixedSizes, moveUnit, removeUnit, setField, typesOf, unitsTotal, widthOptions,
  type KitchenUnit, type UnitField, type UnitsCatalog,
} from './kitchenUnits';

const mm = (m: number) => Math.round(m * 1000);

function FieldInput({ f, value, onCommit }: { f: UnitField; value: unknown; onCommit: (v: unknown) => void }) {
  switch (f.kind) {
    case 'choice': return <select value={String(value ?? '')} onChange={(e) => onCommit(e.target.value)}>{(f.choices ?? []).map((c) => <option key={c} value={c}>{CHOICE_LABELS[c] ?? c}</option>)}</select>;
    case 'number': return <NumberField value={Number(value ?? f.min ?? 1)} min={f.min ?? undefined} max={f.max ?? undefined} onCommit={(v) => { if (v != null) onCommit(v); }} />;
    case 'length': return <LengthField metres={typeof value === 'number' ? value : null} placeholder="the run’s" min={f.min ?? undefined} max={f.max ?? undefined} onCommit={(v) => onCommit(v ?? undefined)} />;
    case 'bool': return <input type="checkbox" checked={value === true} onChange={(e) => onCommit(e.target.checked)} />;
    default: return null;
  }
}

function UnitsLine({ upper, list, cat, onChange }: { upper: boolean; list: KitchenUnit[]; cat: UnitsCatalog; onChange: (l: KitchenUnit[]) => void }) {
  const types = typesOf(cat, upper);
  const [adding, setAdding] = useState(types[0]?.key ?? '');
  const set = (i: number, key: string, v: unknown) => onChange(setField(list, i, key, v, cat, upper));
  return (
    <div className="kitchen-units-line" data-testid={upper ? 'kitchen-upper' : 'kitchen-units'}>
      <div className="row" style={{ gap: 6, alignItems: 'center' }}>
        <span className="eyebrow">{upper ? 'Wall line' : 'Base line'}</span>
        <span className="muted small" data-no-i18n>{list.length ? `${mm(unitsTotal(list))} mm` : ''}</span>
        <span className="grow" />
        {!upper && !list.length && (
          <button type="button" className="btn small" data-action="kitchen-typical" title="A sink, a dishwasher, drawers and a hob over an oven, to change" onClick={() => onChange(cat.typical)}>Typical run</button>
        )}
        <select value={adding} data-field={upper ? 'kitchen-add-upper' : 'kitchen-add-unit'} onChange={(e) => setAdding(e.target.value)}>
          {types.map((t) => <option key={t.key} value={t.key}>{t.label}</option>)}
        </select>
        <button type="button" className="btn small" data-action={upper ? 'kitchen-add-upper' : 'kitchen-add-unit'} onClick={() => { const t = findType(cat, adding, upper); if (t) onChange(addUnit(list, t)); }}>
          <Plus size={13} /> Add
        </button>
      </div>
      {!list.length && (
        <p className="muted small">
          {upper ? 'None listed: a wall unit over each base unit, a hood over the hob.' : 'None listed: the run is split evenly into units of the base unit width, with the sink and hob where the type puts them.'}
        </p>
      )}
      {list.map((u, i) => {
        const t = findType(cat, u.type, upper);
        const options = widthOptions(t, cat.steps);
        const w = Number(u.width);
        return (
          <div key={i} className="kitchen-unit" data-unit={i}>
            <span className="muted small" data-no-i18n>{i + 1}</span>
            <select value={u.type} data-field="unit-type" onChange={(e) => set(i, 'type', e.target.value)}>
              {types.map((x) => <option key={x.key} value={x.key}>{x.label}</option>)}
            </select>
            <span className="kitchen-unit-width">
              <select value={options.some((o) => Math.abs(o - w) < 1e-6) ? String(w) : ''} data-field="unit-width" title="Width" onChange={(e) => { if (e.target.value) set(i, 'width', Number(e.target.value)); }}>
                {!fixedSizes(t) && <option value="">{mm(w)}</option>}
                {options.map((o) => <option key={o} value={String(o)} data-no-i18n>{mm(o)}</option>)}
              </select>
              {!fixedSizes(t) && t && <LengthField testId="unit-width-any" metres={w} min={t.min} max={t.max} title={`Any width from ${mm(t.min)} to ${mm(t.max)} mm`} onCommit={(v) => { if (v != null) set(i, 'width', v); }} />}
            </span>
            {fieldsFor(u, t).map((f) => (
              <label key={f.key} className="kitchen-unit-field" data-unit-field={f.key}>
                <span className="muted small">{f.label}</span>
                <FieldInput f={f} value={f.kind === 'length' ? u[f.key] : valueOf(u, f, t)} onCommit={(v) => set(i, f.key, v)} />
              </label>
            ))}
            <span className="grow" />
            <button type="button" className="icon mini" title="Move left" disabled={i === 0} onClick={() => onChange(moveUnit(list, i, -1))}><ChevronLeft size={12} /></button>
            <button type="button" className="icon mini" title="Move right" disabled={i === list.length - 1} onClick={() => onChange(moveUnit(list, i, 1))}><ChevronRight size={12} /></button>
            <button type="button" className="icon mini" title="Take it out" onClick={() => onChange(removeUnit(list, i))}><Trash2 size={11} /></button>
          </div>
        );
      })}
    </div>
  );
}

/** A kitchen type's units, base line and wall line; <code>update</code> sends the spec's patch. */
export function KitchenUnitsEditor({ spec, update }: { spec: FurnitureSpec; update: (patch: Record<string, unknown>) => void }) {
  const cat = useSession((s) => s.info?.catalogs['furniture.kitchenUnits']) as unknown as UnitsCatalog | undefined;
  if (!cat?.types) return null;
  const base = (spec.units as KitchenUnit[] | undefined) ?? [];
  const upper = (spec.upper as KitchenUnit[] | undefined) ?? [];
  return (
    <div className="kitchen-units" data-testid="kitchen-units-editor">
      <p className="muted small">
        The main run’s units from the left as seen from the front, after a blind corner; what they leave of the run is split evenly. Cabinets take any width from 100 to 800 mm, appliances their sizes.
      </p>
      <UnitsLine upper={false} list={base} cat={cat} onChange={(l) => update({ units: l.length ? l : null })} />
      {spec.wallUnits !== false && <UnitsLine upper list={upper} cat={cat} onChange={(l) => update({ upper: l.length ? l : null })} />}
    </div>
  );
}
