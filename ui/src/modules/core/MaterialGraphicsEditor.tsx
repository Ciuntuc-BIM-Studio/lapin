/**
 * The material graphics configurator (docs/plan-material-graphics.md, stage 0b, decision 678). One editor for Manage → Materials
 * and for the popover beside a layer's swatch: per kind of view (cut in a section, cut in a plan, elevation) the hatch (pattern,
 * scale, angle, alignment, colour, weight, line type, opacity, halftone), the background (a fill and a second pattern) and the
 * lines (the cut's outline and the separator between layers), each field with its way back to the default and the inherited
 * value as its placeholder, and the engine's own preview at a scale. Every change is `material.update`, one undo step, for the
 * material or for all of a multi-selection (`ids`).
 */

import { useEffect, useRef, useState, type ReactNode } from 'react';
import { RotateCcw } from 'lucide-react';
import { create } from 'zustand';
import { t } from '../../i18n';
import { ColourField } from '../../shell/ColourField';
import { execute } from '../../engine/session';
import { HatchSwatch } from './HatchSwatch';
import { PatternOptions, UNITS_LABEL, UnitsBadge, effectiveUnits, scaleLabel, unitsOf, type HatchUnits } from './hatchUnits';
import { openHatchPatterns } from './hatchEditorOpen';
import { useLineTypes } from './lineTypes/lineTypes';
import { MaterialGraphicsPreview } from './MaterialGraphicsPreview';
import { MaterialScaleSteps } from './MaterialScaleSteps';
import {
  ISO_WEIGHTS, fieldPatch, groupPatch, inheritedText, isSet, localNumber, parseNumber, parseOpacity, parseWeight, stepsOf, stepsPatch, swatchFill, swatchInk, updateArgs,
  type Field, type GroupField, type MaterialKind, type MaterialRow, type OwnGraphics, type SampleLayout,
} from './materialGraphicsModel';

export type HatchPattern = { name: string; description: string; solid: boolean; project: boolean; units?: HatchUnits };

export const MATERIAL_KINDS: { kind: MaterialKind; label: string }[] = [{ kind: 'section', label: 'Cut (section)' }, { kind: 'plan', label: 'Cut (plan)' }, { kind: 'elevation', label: 'Elevation' }];

/** The preview's scale and layout, kept between openings. */
export const useGraphicsPrefs = create<{ scale: number; layout: SampleLayout }>(() => ({ scale: 50, layout: 'strip' }));

/** A row of the configurator: its label, the control, and the way back to the default when the field is set. */
export function Row({ label, title, set, onReset, children, testId }: { label: string; title?: string; set: boolean; onReset: () => void; children: ReactNode; testId?: string }) {
  return (
    <div className={`gfx-row${set ? ' own' : ''}`} title={title} data-testid={testId}>
      <span className="gfx-label small">{label}</span>
      <span className="gfx-control">{children}</span>
      {set ? <button type="button" className="icon mini" title="Back to the default" aria-label="Back to the default" onClick={onReset}><RotateCcw size={11} /></button> : <span className="gfx-nobtn" />}
    </div>
  );
}

/** A number typed, committed when the field is left; empty goes back to the default; the inherited value shows as the placeholder. */
export function NumberInput({ id, value, placeholder, parse, onCommit, list, field }: { id: string; value: number | undefined; placeholder: string; parse: (t: string) => number | null; onCommit: (v: number | undefined) => void; list?: string; field?: string }) {
  return (
    <input className="num" data-field={field} key={`${id}:${value ?? ''}`} defaultValue={value === undefined ? '' : localNumber(value)} placeholder={placeholder} list={list} inputMode="decimal"
      onBlur={(e) => {
        const text = e.target.value;
        if (text.trim() === '') { if (value !== undefined) onCommit(undefined); return; }
        const v = parse(text);
        if (v === null) { e.target.value = value === undefined ? '' : localNumber(value); return; }
        if (v !== value) onCommit(v);
      }}
      onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
  );
}

/** A percentage: the slider moves the preview (a draft) and the value goes to the engine when let go. */
export function OpacityInput({ value, inherited, onDraft, onCommit, field }: { value: number | undefined; inherited: number; onDraft: (v: number | undefined) => void; onCommit: (v: number) => void; field?: string }) {
  const [live, setLive] = useState<number | null>(null);
  const shown = live ?? value ?? inherited;
  const done = () => { if (live !== null) { const v = live; setLive(null); onCommit(v); } };
  return (
    <span className="gfx-opacity">
      <input type="range" min={0} max={100} step={1} data-field={field} value={shown}
        onChange={(e) => { const v = Number(e.target.value); setLive(v); onDraft(v); }}
        onPointerUp={done} onKeyUp={done} onBlur={done} />
      <input className="num" key={`${value ?? ''}${live ?? ''}`} defaultValue={shown} inputMode="numeric" aria-label="Opacity, per cent"
        onBlur={(e) => { const v = parseOpacity(e.target.value); if (v !== null && v !== value) onCommit(v); else e.target.value = String(shown); }} />
      <span className="small muted">%</span>
    </span>
  );
}

/** A colour: automatic until one is chosen (a button showing what it is now), then the colour field. */
export function ColourControl({ value, auto, onSet }: { value: string | undefined; auto: string; onSet: (v: string) => void }) {
  if (value === undefined) {
    return <button type="button" className="gfx-auto" title="Automatic: click to choose a colour" onClick={() => onSet(auto)}><span className="swatch" style={{ background: auto }} />Automatic</button>;
  }
  return <ColourField value={value} onChange={(v) => onSet(v.slice(0, 7))} />;
}

export function LineTypeSelect({ value, onSet, field }: { value: string | undefined; onSet: (v: string) => void; field?: string }) {
  const lt = useLineTypes();
  const names = [...(lt?.builtin ?? []).map((b) => b.name), ...(lt?.own ?? []).map((o) => o.name)];
  const shown = value ?? '';
  const all = value && !names.some((n) => n.toLowerCase() === value.toLowerCase()) ? [...names, value] : names;
  return (
    <select data-field={field} value={all.find((n) => n.toLowerCase() === shown.toLowerCase()) ?? ''} onChange={(e) => e.target.value && onSet(e.target.value)}>
      <option value="">default</option>
      {all.map((n) => <option key={n} value={n}>{n}</option>)}
    </select>
  );
}

const Section = ({ title, open, children, name }: { title: string; open: boolean; children: ReactNode; name: string }) => (
  <details className="gfx-section" open={open} data-section={name}><summary>{title}</summary>{children}</details>
);

/**
 * `m`: the material shown (its values); `targets`: the materials a change goes to (a multi-selection: all of them, one step),
 * else `m` alone. `compact`: the popover's size: only the hatch open at first.
 */
export function MaterialGraphicsEditor({ m, hatches, targets, compact = false }: { m: MaterialRow; hatches: HatchPattern[]; targets?: MaterialRow[]; compact?: boolean }) {
  const [kind, setKind] = useState<MaterialKind>('section');
  const [draft, setDraft] = useState<Record<string, unknown>>({});
  const [activeStep, setActiveStep] = useState<number | null>(null);
  const prefs = useGraphicsPrefs();
  const ids = (targets && targets.length ? targets : [m]).map((x) => x.id);
  const own = m.own[kind] as OwnGraphics | undefined;
  const r = m[kind];
  const lastId = useRef(m.id);
  useEffect(() => { if (lastId.current !== m.id) { lastId.current = m.id; setDraft({}); } }, [m.id]);
  useEffect(() => setDraft({}), [kind]);

  const send = (patch: Record<string, unknown> | null, k: MaterialKind = kind) => void execute('material.update', updateArgs(ids, k, patch)).then(() => setDraft({}));
  const setField = (field: Field, value: unknown) => send(fieldPatch(field, value));
  const setIn = (group: GroupField, key: string, value: unknown) => send(groupPatch(own, group, key, value));
  const has = (field: Field) => isSet(own, field);
  const hatchValue = own && 'hatch' in own ? (own.hatch ?? '') : '*';
  // The hatch shown came from the name: none of its own (a plan's, none of the section's either). An elevation's guess is the facade's.
  const guessed = hatchValue === '*' && !(kind === 'plan' && m.own.section && 'hatch' in m.own.section);
  const fillMode = own?.fill === 'material' ? 'material' : own?.fill ? 'colour' : '';
  const bg = own?.backgroundHatch;
  const patternOptions = <PatternOptions patterns={hatches} />;
  const patternNamed = (name: string | null | undefined) => hatches.find((h) => h.name === name);
  // The units the hatch is drawn in: the material's own choice, else its pattern's (the section's for a plan with none of its own).
  const shownPattern = patternNamed(hatchValue === '*' ? r.hatch : hatchValue);
  const units = effectiveUnits(own?.hatchUnits, shownPattern);
  const bgUnits = effectiveUnits(undefined, patternNamed(bg?.hatch));
  const lineRows = (group: 'cutLine' | 'separatorLine', prefix: string) => {
    const l = own?.[group] ?? {};
    return (
      <>
        <Row label="Colour" set={l.color !== undefined} onReset={() => setIn(group, 'color', undefined)}>
          <ColourControl value={l.color} auto={r[group]?.color ?? '#000000'} onSet={(v) => setIn(group, 'color', v)} />
        </Row>
        <Row label="Weight, mm" title="Paper millimetres: 0.13 · 0.18 · 0.25 · 0.35 · 0.5 · 0.7 · 1.0, or any value from 0.05 to 5" set={l.weight !== undefined} onReset={() => setIn(group, 'weight', undefined)}>
          <NumberInput id={`${m.id}${kind}${group}w`} value={l.weight} placeholder={r[group]?.weight != null ? localNumber(r[group]!.weight!) : 'Object styles'} parse={parseWeight} list="gfx-iso-weights" onCommit={(v) => setIn(group, 'weight', v)} field={`${prefix}-weight`} />
        </Row>
        <Row label="Line type" set={l.lineType !== undefined} onReset={() => setIn(group, 'lineType', undefined)}>
          <LineTypeSelect value={l.lineType} onSet={(v) => setIn(group, 'lineType', v)} field={`${prefix}-linetype`} />
        </Row>
      </>
    );
  };

  return (
    <div className="gfx-editor" data-testid="material-graphics" data-compact={compact || undefined}>
      <datalist id="gfx-iso-weights">{ISO_WEIGHTS.map((w) => <option key={w} value={localNumber(w)} />)}</datalist>
      <div className="gfx-kinds" role="tablist">
        {MATERIAL_KINDS.map(({ kind: k, label }) => {
          const x = m[k];
          const ink = swatchInk(x, '#000000');
          return (
            <button key={k} type="button" role="tab" aria-selected={kind === k} data-kind={k} className={`tab gfx-kind${kind === k ? ' on' : ''}`} onClick={() => setKind(k)}>
              <HatchSwatch hatch={x.hatch} scale={x.hatchScale} fill={swatchFill(x, m.color)} ink={ink.color} inkOpacity={ink.opacity} size={22} membrane={k !== 'elevation' && x.membrane} />
              <span data-i18n-ctx={k === 'elevation' ? 'facade' : undefined}>{label}</span>
            </button>
          );
        })}
        {kind !== 'section' && m.own[kind] && <button type="button" className="icon mini" title="As the section" onClick={() => send(null)}><RotateCcw size={11} /></button>}
      </div>
      {ids.length > 1 && <p className="small gfx-multi" data-testid="material-graphics-multi">{`Changes go to all ${ids.length} selected materials.`}</p>}
      <MaterialGraphicsPreview id={m.id} kind={kind} draft={draft} scale={prefs.scale} layout={prefs.layout} width={compact ? 300 : 340}
        onScale={(scale) => useGraphicsPrefs.setState({ scale })} onLayout={(layout) => useGraphicsPrefs.setState({ layout })} onStep={setActiveStep} version={m} />

      <Section title="Hatch" name="hatch" open>
        <Row label="Pattern" set={hatchValue !== '*'} onReset={() => setField('hatch', undefined)}>
          <select data-field={`material-hatch-${kind}`} value={hatchValue} onChange={(e) => (e.target.value === '*' ? setField('hatch', undefined) : setField('hatch', e.target.value || null))}>
            <option value="*">{t('from the name')}{guessed && r.hatch ? ` (${r.hatch})` : ''}</option>
            <option value="">none</option>
            {patternOptions}
          </select>
          <UnitsBadge units={unitsOf(shownPattern)} />
          {!compact && <button type="button" className="link small" data-testid="edit-pattern" onClick={() => openHatchPatterns({ select: shownPattern?.name ?? null, fresh: !shownPattern, fromMaterial: m.name })}>New / Edit pattern…</button>}
          {shownPattern && !shownPattern.solid && <HatchSwatch hatch={shownPattern.name} scale={own?.hatchScale ?? r.hatchScale} size={22} />}
        </Row>
        <Row label="Units" title="Pattern's own: as the pattern says. Drafting: the spacing is paper millimetres, the same on every scale. Real size: it is the real object's millimetres, so it follows the view's scale" set={has('hatchUnits')} onReset={() => setField('hatchUnits', undefined)}>
          <select data-field={`material-hatch-units-${kind}`} value={own?.hatchUnits ?? ''} onChange={(e) => setField('hatchUnits', e.target.value || undefined)}>
            <option value="">pattern's own</option>
            <option value="paper">{UNITS_LABEL.paper}</option>
            <option value="model">{UNITS_LABEL.model}</option>
          </select>
        </Row>
        <Row label={scaleLabel(units)} title={units === 'model' ? 'Real size: 1 is the pattern at the real object\'s size; 2 twice as large' : 'A factor on the pattern\'s spacing on paper'} set={has('hatchScale')} onReset={() => setField('hatchScale', undefined)}>
          <NumberInput id={`${m.id}${kind}s`} value={own?.hatchScale} placeholder={inheritedText(r, 'hatchScale')} parse={(t) => { const v = parseNumber(t); return v > 0 ? v : null; }} onCommit={(v) => setField('hatchScale', v)} />
        </Row>
        <Row label="Angle" set={has('hatchAngle')} onReset={() => setField('hatchAngle', undefined)}>
          <NumberInput id={`${m.id}${kind}a`} value={own?.hatchAngle} placeholder={inheritedText(r, 'hatchAngle') || '0'} parse={(t) => { const v = parseNumber(t); return Number.isFinite(v) ? v : null; }} onCommit={(v) => setField('hatchAngle', v)} />
        </Row>
        <Row label="Align" title="Where the hatch starts and turns: from the view's origin, or from each band it fills (along the band's longest edge)" set={has('hatchAlign')} onReset={() => setField('hatchAlign', undefined)}>
          <select value={r.hatchAlign ?? 'view'} onChange={(e) => setField('hatchAlign', e.target.value)}><option value="view">view</option><option value="layer">layer</option></select>
        </Row>
        <Row label="Colour" title="The colour of the hatch; automatic: the category colour" set={has('hatchColor')} onReset={() => setField('hatchColor', undefined)}>
          <ColourControl value={own?.hatchColor} auto={r.hatchColor ?? '#000000'} onSet={(v) => setField('hatchColor', v)} />
        </Row>
        <Row label="Weight, mm" title="The weight of the hatch lines on paper: 0.13 · 0.18 · 0.25 · 0.35 · 0.5 · 0.7 · 1.0, or any value from 0.05 to 5; it wins over the pen" set={has('hatchWeight')} onReset={() => setField('hatchWeight', undefined)}>
          <NumberInput id={`${m.id}${kind}hw`} value={own?.hatchWeight} placeholder={r.hatchWeight != null ? localNumber(r.hatchWeight) : 'from the pen'} parse={parseWeight} list="gfx-iso-weights" onCommit={(v) => setField('hatchWeight', v)} field={`material-hatch-weight-${kind}`} />
        </Row>
        <Row label="Pen" title="A pen of the pen table (1 to 16), when no weight is set" set={has('hatchPen')} onReset={() => setField('hatchPen', undefined)}>
          <NumberInput id={`${m.id}${kind}p`} value={own?.hatchPen} placeholder={inheritedText(r, 'hatchPen') || '—'} parse={(t) => { const v = parseNumber(t); return v >= 1 && v <= 16 ? v : null; }} onCommit={(v) => setField('hatchPen', v)} />
        </Row>
        <Row label="Line type" set={has('hatchLineType')} onReset={() => setField('hatchLineType', undefined)}>
          <LineTypeSelect value={own?.hatchLineType} onSet={(v) => setField('hatchLineType', v)} field={`material-hatch-linetype-${kind}`} />
        </Row>
        <Row label="Opacity" set={has('hatchOpacity')} onReset={() => setField('hatchOpacity', undefined)}>
          <OpacityInput value={own?.hatchOpacity} inherited={r.hatchOpacity ?? 100} onDraft={(v) => setDraft((d) => ({ ...d, hatchOpacity: v }))} onCommit={(v) => setField('hatchOpacity', v)} />
        </Row>
        <Row label="Halftone" title="The hatch in a lighter tone, as a background" set={has('halftone')} onReset={() => setField('halftone', undefined)}>
          <input type="checkbox" checked={own?.halftone ?? r.halftone ?? false} onChange={(e) => setField('halftone', e.target.checked)} />
        </Row>
      </Section>

      <Section title="Background" name="background" open={!compact}>
        <Row label="Fill" set={fillMode !== ''} onReset={() => setField('fill', undefined)}>
          <select data-field={`material-fill-${kind}`} value={fillMode} onChange={(e) => (e.target.value === '' ? setField('fill', undefined) : setField('fill', e.target.value === 'material' ? 'material' : (r.fill && r.fill !== 'material' ? r.fill : '#ffffff')))}>
            <option value="">none</option><option value="material">material colour</option><option value="colour">colour…</option>
          </select>
          {fillMode === 'colour' && <ColourField value={own?.fill ?? '#ffffff'} onChange={(v) => setField('fill', v.slice(0, 7))} />}
        </Row>
        {fillMode !== '' && (
          <Row label="Fill opacity" set={has('fillOpacity')} onReset={() => setField('fillOpacity', undefined)}>
            <OpacityInput value={own?.fillOpacity} inherited={r.fillOpacity ?? 100} onDraft={(v) => setDraft((d) => ({ ...d, fillOpacity: v }))} onCommit={(v) => setField('fillOpacity', v)} />
          </Row>
        )}
        <Row label="Second pattern" title="A second hatch under the first (a background and a foreground, as in Revit)" set={bg !== undefined} onReset={() => setField('backgroundHatch', undefined)}>
          <select data-field={`material-bg-hatch-${kind}`} value={bg?.hatch ?? ''} onChange={(e) => (e.target.value ? setIn('backgroundHatch', 'hatch', e.target.value) : setField('backgroundHatch', undefined))}>
            <option value="">none</option>
            {patternOptions}
          </select>
        </Row>
        {bg && (
          <>
            <Row label={scaleLabel(bgUnits)} set={bg.hatchScale !== undefined} onReset={() => setIn('backgroundHatch', 'hatchScale', undefined)}>
              <NumberInput id={`${m.id}${kind}bs`} value={bg.hatchScale} placeholder={localNumber(own?.hatchScale ?? r.hatchScale)} parse={(t) => { const v = parseNumber(t); return v > 0 ? v : null; }} onCommit={(v) => setIn('backgroundHatch', 'hatchScale', v)} />
            </Row>
            <Row label="Angle" set={bg.hatchAngle !== undefined} onReset={() => setIn('backgroundHatch', 'hatchAngle', undefined)}>
              <NumberInput id={`${m.id}${kind}ba`} value={bg.hatchAngle} placeholder="0" parse={(t) => { const v = parseNumber(t); return Number.isFinite(v) ? v : null; }} onCommit={(v) => setIn('backgroundHatch', 'hatchAngle', v)} />
            </Row>
            <Row label="Colour" set={bg.hatchColor !== undefined} onReset={() => setIn('backgroundHatch', 'hatchColor', undefined)}>
              <ColourControl value={bg.hatchColor} auto={r.hatchColor ?? '#000000'} onSet={(v) => setIn('backgroundHatch', 'hatchColor', v)} />
            </Row>
            <Row label="Weight, mm" set={bg.hatchWeight !== undefined} onReset={() => setIn('backgroundHatch', 'hatchWeight', undefined)}>
              <NumberInput id={`${m.id}${kind}bw`} value={bg.hatchWeight} placeholder="from the pen" parse={parseWeight} list="gfx-iso-weights" onCommit={(v) => setIn('backgroundHatch', 'hatchWeight', v)} />
            </Row>
          </>
        )}
      </Section>

      <Section title="Lines" name="lines" open={!compact}>
        <span className="eyebrow">Cut outline</span>
        {lineRows('cutLine', 'material-cutline')}
        <span className="eyebrow">Separator lines</span>
        {lineRows('separatorLine', 'material-separator')}
      </Section>

      <Section title="By scale" name="by-scale" open={!compact && stepsOf(own).length > 0}>
        <MaterialScaleSteps kind={kind} materialId={m.id} steps={stepsOf(own)} r={r} hatches={hatches} activeIndex={activeStep}
          onSteps={(steps) => send(stepsPatch(steps))} onPreviewAt={(scale) => useGraphicsPrefs.setState({ scale })} />
      </Section>
    </div>
  );
}
