/**
 * The casework configurator (docs/plan-furniture.md, Configuratorul flexibil, stage 1; decision 475): a preset to start
 * from, the modern styles applied as patches, the construction (boards, base, top, fronts and handles) and the carcasses
 * side by side — each its width, height and zones stacked from the bottom (front, doors, drawers by ratio, a flap,
 * shelves, a rail, inner drawers) — added, split, moved and taken out. Every change is one step on the type
 * (furniture.updateType), and the preview beside it, with the cut list, is the engine's.
 */

import { ChevronLeft, ChevronRight, ChevronUp, Plus, SplitSquareHorizontal, Trash2 } from 'lucide-react';
import { useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { CHOICE_LABELS, type FurnitureKind, type FurnitureSpec } from './furniture';
import { ParamField } from './ParamField';
import {
  SECTIONS, addCarcass, addZone, fitWidth, formatRatios, laidWidths, moveCarcass, paramApplies, parseRatios, removeCarcass, removeZone, setCarcassField,
  setZoneField, splitCarcass, zoneFieldsFor, type Carcass, type CaseworkCatalog, type Zone,
} from './casework';
import type { UnitField } from './kitchenUnits';

const mm = (m: number) => Math.round(m * 1000);

function ZoneInput({ f, value, onCommit }: { f: UnitField; value: unknown; onCommit: (v: unknown) => void }) {
  switch (f.kind) {
    case 'choice': return <select value={String(value ?? f.choices?.[0] ?? '')} onChange={(e) => onCommit(e.target.value)}>{(f.choices ?? []).map((c) => <option key={c} value={c}>{CHOICE_LABELS[c] ?? c}</option>)}</select>;
    case 'number': return <NumberField value={typeof value === 'number' ? value : null} placeholder="–" min={f.min ?? undefined} max={f.max ?? undefined} onCommit={(v) => onCommit(v ?? undefined)} />;
    case 'length': return <LengthField metres={typeof value === 'number' ? value : null} placeholder="a share" min={f.min ?? undefined} max={f.max ?? undefined} onCommit={(v) => onCommit(v ?? undefined)} />;
    case 'bool': return <input type="checkbox" checked={value === true} onChange={(e) => onCommit(e.target.checked)} />;
    case 'ratios':
      return (
        <input className="casework-ratios" key={formatRatios(value)} defaultValue={formatRatios(value)} placeholder="1:1:2" title="The drawers' heights by ratio, from the top (empty: all alike)"
          onBlur={(e) => { const t = e.target.value.trim(); if (!t) onCommit(undefined); else { const r = parseRatios(t); if (r) onCommit(r); } }} />
      );
    default: return null;
  }
}

function ZoneRow({ z, j, fields, onField, onRemove, removable }: { z: Zone; j: number; fields: UnitField[]; onField: (key: string, v: unknown) => void; onRemove: () => void; removable: boolean }) {
  return (
    <div className="casework-zone" data-zone={j}>
      <span className="muted small" data-no-i18n>{j + 1}</span>
      {zoneFieldsFor(z, fields).map((f) => (
        <label key={f.key} className="kitchen-unit-field" data-zone-field={f.key}>
          <span className="muted small">{f.label}</span>
          <ZoneInput f={f} value={z[f.key]} onCommit={(v) => onField(f.key, v)} />
        </label>
      ))}
      <span className="grow" />
      <button type="button" className="icon mini" title="Take the zone out" disabled={!removable} onClick={onRemove}><Trash2 size={11} /></button>
    </div>
  );
}

/** A casework type's configurator; <code>update</code> sends the spec's patch. */
export function CaseworkEditor({ spec, kind, update }: { spec: FurnitureSpec; kind: FurnitureKind; update: (patch: Record<string, unknown>) => void }) {
  const cat = useSession((s) => s.info?.catalogs['furniture.casework']) as unknown as CaseworkCatalog | undefined;
  if (!cat?.presets) return null;
  const bed = (spec.form ?? kind.defaults.form) === 'bed';
  const list = ((spec.carcasses as Carcass[] | undefined) ?? (kind.defaults.carcasses as Carcass[] | undefined) ?? []);
  const width = Number(spec.width ?? kind.width);
  const widths = laidWidths(list, width);
  const fit = fitWidth(list, width);
  const set = (l: Carcass[]) => update({ carcasses: l });
  const param = (key: string) => kind.params.find((p) => p.key === key);
  return (
    <div className="casework-editor" data-testid="casework-editor">
      <div className="row" style={{ gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
        <span className="eyebrow">Preset</span>
        <select value={String(spec.preset ?? kind.defaults.preset ?? '')} data-field="casework-preset" title="Starts the type again from the preset's values (its materials stay)"
          onChange={(e) => { const p = cat.presets.find((x) => x.key === e.target.value); if (p) update(p.spec); }}>
          {cat.presets.map((p) => <option key={p.key} value={p.key}>{p.label}</option>)}
        </select>
      </div>
      <div className="casework-styles" data-testid="casework-styles">
        <span className="eyebrow">Styles</span>
        {cat.styles.map((s) => (
          <button key={s.key} type="button" className="btn small" data-style={s.key} title="Applied over the type's values, one step" onClick={() => update(s.patch)}>{s.label}</button>
        ))}
      </div>
      {SECTIONS.filter((sec) => !!sec.bed === bed).map((sec) => (
        <div key={sec.label} className="casework-section" data-section={sec.label}>
          <p className="eyebrow">{sec.label}</p>
          <dl className="fields">
            {sec.keys.map((k) => param(k)).filter((p) => p && paramApplies(p.key, spec, kind.defaults)).map((p) => (
              <div key={p!.key} style={{ display: 'contents' }} data-param={p!.key}>
                <dt>{p!.label}</dt>
                <dd><ParamField p={p!} value={spec[p!.key] ?? kind.defaults[p!.key]} onCommit={(v) => update({ [p!.key]: v })} /></dd>
              </div>
            ))}
          </dl>
        </div>
      ))}
      {!bed && (
        <div className="casework-carcasses" data-testid="casework-carcasses">
          <div className="row" style={{ gap: 6, alignItems: 'center' }}>
            <span className="eyebrow">Carcasses</span>
            <span className="muted small" data-no-i18n>{mm(widths.reduce((a, w) => a + w, 0))} / {mm(width)} mm</span>
            <span className="grow" />
            {fit != null && <button type="button" className="btn small" data-action="casework-fit" title="The piece as wide as its carcasses" onClick={() => update({ width: fit })}>Fit width</button>}
            <button type="button" className="btn small" data-action="casework-add-carcass" onClick={() => set(addCarcass(list))}><Plus size={13} /> Carcass</button>
          </div>
          <p className="muted small">From the left as seen from the front. A carcass without a width shares what the others leave; its zones as they stand, the top one first, a divider between each two.</p>
          {list.map((c, i) => {
            const gap = c.kind === 'gap';
            const zones = c.zones ?? [{ front: 'doors' }];
            return (
              <div key={i} className="casework-carcass" data-carcass={i}>
                <div className="kitchen-unit">
                  <span className="muted small" data-no-i18n>{i + 1}</span>
                  <select value={gap ? 'gap' : 'box'} data-field="carcass-kind" onChange={(e) => set(setCarcassField(list, i, 'kind', e.target.value))}>
                    <option value="box">{CHOICE_LABELS.box}</option>
                    <option value="gap">{CHOICE_LABELS.gap}</option>
                  </select>
                  <label className="kitchen-unit-field"><span className="muted small">Width</span>
                    <LengthField testId="carcass-width" metres={typeof c.width === 'number' ? c.width : null} placeholder={String(mm(widths[i] ?? 0))} min={0.1} max={6} onCommit={(v) => set(setCarcassField(list, i, 'width', v ?? undefined))} />
                  </label>
                  {!gap && (
                    <label className="kitchen-unit-field"><span className="muted small">Height</span>
                      <LengthField metres={typeof c.height === 'number' ? c.height : null} placeholder="full" min={0.1} max={3} onCommit={(v) => set(setCarcassField(list, i, 'height', v ?? undefined))} />
                    </label>
                  )}
                  <span className="grow" />
                  {!gap && <button type="button" className="icon mini" title="Add a zone over the top one" data-action="casework-add-zone" onClick={() => set(addZone(list, i))}><ChevronUp size={12} /></button>}
                  <button type="button" className="icon mini" title="Split it in two" data-action="casework-split" onClick={() => set(splitCarcass(list, i))}><SplitSquareHorizontal size={12} /></button>
                  <button type="button" className="icon mini" title="Move left" disabled={i === 0} onClick={() => set(moveCarcass(list, i, -1))}><ChevronLeft size={12} /></button>
                  <button type="button" className="icon mini" title="Move right" disabled={i === list.length - 1} onClick={() => set(moveCarcass(list, i, 1))}><ChevronRight size={12} /></button>
                  <button type="button" className="icon mini" title="Take it out" disabled={list.length < 2} onClick={() => set(removeCarcass(list, i))}><Trash2 size={11} /></button>
                </div>
                {!gap && [...zones.keys()].reverse().map((j) => (
                  <ZoneRow key={j} z={zones[j]} j={j} fields={cat.zoneFields} removable={zones.length > 1}
                    onField={(key, v) => set(setZoneField(list, i, j, key, v, cat.zoneFields))} onRemove={() => set(removeZone(list, i, j))} />
                ))}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
