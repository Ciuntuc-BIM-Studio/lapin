/**
 * The material graphics' "By scale" section (docs/plan-material-graphics.md, stage 2): per kind of view a list of steps, each a
 * range of scales and the fields it changes over the material's own (the rest stays inherited). The first step that covers
 * the scale wins, so the order matters. Every change is one `material.update` with the whole list (one undo step).
 */

import { useState } from 'react';
import { ArrowDown, ArrowUp, Copy, Eye, Plus, Trash2, Wand2 } from 'lucide-react';
import { t } from '../../i18n';
import { ColourField } from '../../shell/ColourField';
import { ColourControl, LineTypeSelect, NumberInput, OpacityInput, Row, type HatchPattern } from './MaterialGraphicsEditor';
import { PatternOptions } from './hatchUnits';
import {
  MAX_STEPS, SCALE_PRESETS, addStep, duplicateStep, moveStep, localNumber, newStep, parseNumber, parseScaleDenominator, parseWeight, rangeLabel, removeStep,
  sampleScale, setStepField, setStepGroup, setStepRange, simplifySteps, stepFields, stepsProblem,
  type Field, type GroupField, type MaterialKind, type ResolvedGraphics, type ScaleStep,
} from './materialGraphicsModel';

/** An end of a step's range: a whole scale denominator, empty for open. */
function ScaleInput({ id, label, value, onCommit, testId }: { id: string; label: string; value: number | undefined; onCommit: (v: number | undefined) => void; testId: string }) {
  return (
    <label className="gfx-scale-end small">
      <span>{label}</span>1:
      <input className="num" data-testid={testId} key={`${id}:${value ?? ''}`} defaultValue={value ?? ''} placeholder="open" list="gfx-scale-presets" inputMode="numeric"
        onBlur={(e) => {
          if (e.target.value.trim() === '') { if (value !== undefined) onCommit(undefined); return; }
          const v = parseScaleDenominator(e.target.value);
          if (v === null) { e.target.value = value === undefined ? '' : String(value); return; }
          if (v !== value) onCommit(v);
        }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
    </label>
  );
}

const sub = (step: ScaleStep, group: 'cutLine' | 'separatorLine' | 'backgroundHatch') => (step[group] ?? {}) as Record<string, string | number | undefined>;

function StepFields({ step, i, r, hatches, apply, idKey }: { step: ScaleStep; i: number; r: ResolvedGraphics; hatches: HatchPattern[]; apply: (next: (s: ScaleStep[]) => ScaleStep[]) => void; idKey: string }) {
  const has = (f: Field) => f in step && (step as Record<string, unknown>)[f] !== undefined;
  const set = (f: Field, v: unknown) => apply((s) => setStepField(s, i, f, v));
  const grp = (g: GroupField, k: string, v: unknown) => apply((s) => setStepGroup(s, i, g, k, v));
  const fillMode = !has('fill') ? '*' : step.fill === null ? '' : step.fill === 'material' ? 'material' : 'colour';
  const hatchValue = has('hatch') ? (step.hatch ?? '') : '*';
  const num = (id: string, value: number | undefined, placeholder: string, parse: (t: string) => number | null, commit: (v: number | undefined) => void, extra: object = {}) => (
    <NumberInput id={`${idKey}${id}`} value={value} placeholder={placeholder} parse={parse} onCommit={commit} {...extra} />
  );
  const lineRows = (g: 'cutLine' | 'separatorLine') => {
    const l = sub(step, g);
    return (
      <>
        <Row label="Colour" set={l.color !== undefined} onReset={() => grp(g, 'color', undefined)}>
          <ColourControl value={l.color as string | undefined} auto={r[g]?.color ?? '#000000'} onSet={(v) => grp(g, 'color', v)} />
        </Row>
        <Row label="Weight, mm" set={l.weight !== undefined} onReset={() => grp(g, 'weight', undefined)}>
          {num(`${g}w`, l.weight as number | undefined, r[g]?.weight != null ? localNumber(r[g]!.weight!) : 'Object styles', parseWeight, (v) => grp(g, 'weight', v))}
        </Row>
        <Row label="Line type" set={l.lineType !== undefined} onReset={() => grp(g, 'lineType', undefined)}>
          <LineTypeSelect value={l.lineType as string | undefined} onSet={(v) => grp(g, 'lineType', v)} />
        </Row>
      </>
    );
  };
  return (
    <div className="gfx-step-fields">
      <Row label="Pattern" set={hatchValue !== '*'} onReset={() => set('hatch', undefined)}>
        <select data-field="step-hatch" value={hatchValue} onChange={(e) => (e.target.value === '*' ? set('hatch', undefined) : set('hatch', e.target.value || null))}>
          <option value="*">{`as the material${r.hatch ? ` (${r.hatch})` : ''}`}</option>
          <option value="">none</option>
          <PatternOptions patterns={hatches} />
        </select>
      </Row>
      <Row label="Units" set={has('hatchUnits')} onReset={() => set('hatchUnits', undefined)}>
        <select value={step.hatchUnits ?? ''} onChange={(e) => set('hatchUnits', e.target.value || undefined)}>
          <option value="">as the material</option><option value="paper">Drafting</option><option value="model">Real size</option>
        </select>
      </Row>
      <Row label="Scale (×)" set={has('hatchScale')} onReset={() => set('hatchScale', undefined)}>
        {num('s', step.hatchScale, localNumber(r.hatchScale), (x) => { const v = parseNumber(x); return v > 0 ? v : null; }, (v) => set('hatchScale', v))}
      </Row>
      <Row label="Angle" set={has('hatchAngle')} onReset={() => set('hatchAngle', undefined)}>
        {num('a', step.hatchAngle, localNumber(r.hatchAngle ?? 0), (x) => { const v = parseNumber(x); return Number.isFinite(v) ? v : null; }, (v) => set('hatchAngle', v))}
      </Row>
      <Row label="Align" set={has('hatchAlign')} onReset={() => set('hatchAlign', undefined)}>
        <select value={step.hatchAlign ?? ''} onChange={(e) => set('hatchAlign', e.target.value || undefined)}><option value="">as the material</option><option value="view">view</option><option value="layer">layer</option></select>
      </Row>
      <Row label="Colour" set={has('hatchColor')} onReset={() => set('hatchColor', undefined)}>
        <ColourControl value={step.hatchColor} auto={r.hatchColor ?? '#000000'} onSet={(v) => set('hatchColor', v)} />
      </Row>
      <Row label="Weight, mm" set={has('hatchWeight')} onReset={() => set('hatchWeight', undefined)}>
        {num('hw', step.hatchWeight, r.hatchWeight != null ? localNumber(r.hatchWeight) : 'from the pen', parseWeight, (v) => set('hatchWeight', v), { list: 'gfx-iso-weights' })}
      </Row>
      <Row label="Pen" set={has('hatchPen')} onReset={() => set('hatchPen', undefined)}>
        {num('p', step.hatchPen, String(r.hatchPen ?? '—'), (x) => { const v = parseNumber(x); return v >= 1 && v <= 16 ? v : null; }, (v) => set('hatchPen', v))}
      </Row>
      <Row label="Line type" set={has('hatchLineType')} onReset={() => set('hatchLineType', undefined)}>
        <LineTypeSelect value={step.hatchLineType} onSet={(v) => set('hatchLineType', v)} />
      </Row>
      <Row label="Opacity" set={has('hatchOpacity')} onReset={() => set('hatchOpacity', undefined)}>
        <OpacityInput value={step.hatchOpacity} inherited={r.hatchOpacity ?? 100} onDraft={() => undefined} onCommit={(v) => set('hatchOpacity', v)} />
      </Row>
      <Row label="Halftone" set={has('halftone')} onReset={() => set('halftone', undefined)}>
        <input type="checkbox" checked={step.halftone ?? r.halftone ?? false} onChange={(e) => set('halftone', e.target.checked)} />
      </Row>
      <Row label="Fill" set={fillMode !== '*'} onReset={() => set('fill', undefined)}>
        <select data-field="step-fill" value={fillMode} onChange={(e) => {
          const v = e.target.value;
          if (v === '*') set('fill', undefined); else if (v === '') set('fill', null); else set('fill', v === 'material' ? 'material' : (typeof step.fill === 'string' && step.fill !== 'material' ? step.fill : '#b3b3b3'));
        }}>
          <option value="*">as the material</option><option value="">none</option><option value="material">material colour</option><option value="colour">colour…</option>
        </select>
        {fillMode === 'colour' && <ColourField value={step.fill ?? '#b3b3b3'} onChange={(v) => set('fill', v.slice(0, 7))} />}
      </Row>
      {fillMode !== '*' && fillMode !== '' && (
        <Row label="Fill opacity" set={has('fillOpacity')} onReset={() => set('fillOpacity', undefined)}>
          <OpacityInput value={step.fillOpacity} inherited={r.fillOpacity ?? 100} onDraft={() => undefined} onCommit={(v) => set('fillOpacity', v)} />
        </Row>
      )}
      <Row label="Second pattern" set={has('backgroundHatch')} onReset={() => set('backgroundHatch', undefined)}>
        <select value={String(sub(step, 'backgroundHatch').hatch ?? '')} onChange={(e) => (e.target.value ? grp('backgroundHatch', 'hatch', e.target.value) : set('backgroundHatch', undefined))}>
          <option value="">as the material</option>
          <PatternOptions patterns={hatches} />
        </select>
      </Row>
      <span className="eyebrow">Cut outline</span>
      {lineRows('cutLine')}
      <span className="eyebrow">Separator lines</span>
      {lineRows('separatorLine')}
    </div>
  );
}

/**
 * `steps`: the kind's steps (raw, as stored); `onSteps` sends the whole list; `activeIndex`: the step the preview scale uses
 * (the engine's `resolved.step`), `onPreviewAt`: asks the preview to show a scale.
 */
export function MaterialScaleSteps({ kind, materialId, steps, r, hatches, activeIndex, onSteps, onPreviewAt }: {
  kind: MaterialKind; materialId: string; steps: ScaleStep[]; r: ResolvedGraphics; hatches: HatchPattern[]; activeIndex: number | null;
  onSteps: (steps: ScaleStep[]) => void; onPreviewAt: (scale: number) => void;
}) {
  const [problem, setProblem] = useState<{ index: number; text: string } | null>(null);
  const [open, setOpen] = useState<number | null>(null);
  // A change is checked as the engine would, and only a valid list goes to it.
  const apply = (next: (s: ScaleStep[]) => ScaleStep[]) => {
    const list = next(steps);
    const p = stepsProblem(list);
    setProblem(p);
    if (!p) onSteps(list);
  };
  return (
    <div className="gfx-bytscale" data-testid="material-by-scale">
      <datalist id="gfx-scale-presets">{SCALE_PRESETS.map((s) => <option key={s} value={s}>{`1:${s}`}</option>)}</datalist>
      <p className="small muted gfx-bs-help">The first step that covers the scale wins; it changes only the fields it sets, the rest is the material's own. “From 1:100” is 1:100 and smaller scales, “to 1:50” is 1:50 and larger.</p>
      {steps.map((step, i) => {
        const key = `${materialId}${kind}${i}`;
        const fields = stepFields(step);
        return (
          <div key={key} className={`gfx-step${activeIndex === i ? ' active' : ''}`} data-testid="material-step" data-active={activeIndex === i || undefined}>
            <div className="gfx-step-head">
              <span className="small gfx-step-no" title="Steps are tried in this order">{i + 1}</span>
              <ScaleInput id={key} label="from" value={step.from} testId="step-from" onCommit={(v) => apply((s) => setStepRange(s, i, 'from', v))} />
              <ScaleInput id={key} label="to" value={step.to} testId="step-to" onCommit={(v) => apply((s) => setStepRange(s, i, 'to', v))} />
              {activeIndex === i && <span className="small gfx-step-applies" data-testid="step-applies">Applies at the preview scale</span>}
              <span className="grow" />
              <button type="button" className="icon mini" title="Show the preview at this step's scale" aria-label="Show the preview at this step's scale" onClick={() => onPreviewAt(sampleScale(step))}><Eye size={12} /></button>
              <button type="button" className="icon mini" title="Earlier: tried before the others" aria-label="Move up" disabled={i === 0} onClick={() => apply((s) => moveStep(s, i, -1))}><ArrowUp size={12} /></button>
              <button type="button" className="icon mini" title="Later: tried after the others" aria-label="Move down" disabled={i === steps.length - 1} onClick={() => apply((s) => moveStep(s, i, 1))}><ArrowDown size={12} /></button>
              <button type="button" className="icon mini" title="Duplicate the step" aria-label="Duplicate the step" disabled={steps.length >= MAX_STEPS} onClick={() => apply((s) => duplicateStep(s, i))}><Copy size={12} /></button>
              <button type="button" className="icon mini" title="Remove the step" aria-label="Remove the step" onClick={() => apply((s) => removeStep(s, i))}><Trash2 size={12} /></button>
            </div>
            <div className="small muted gfx-step-sum" title={rangeLabel(step)}>{fields.join(' · ')}</div>
            {problem?.index === i && <p className="small gfx-step-problem" role="alert" data-testid="step-problem">{t(problem.text)}</p>}
            <details open={open === i} onToggle={(e) => { if ((e.currentTarget as HTMLDetailsElement).open) setOpen(i); else if (open === i) setOpen(null); }}>
              <summary className="small">Fields of the step</summary>
              {open === i && <StepFields step={step} i={i} r={r} hatches={hatches} apply={apply} idKey={key} />}
            </details>
          </div>
        );
      })}
      {problem && problem.index >= steps.length && <p className="small gfx-step-problem" role="alert">{t(problem.text)}</p>}
      <div className="gfx-bs-actions">
        <button type="button" className="link small" data-testid="step-add" disabled={steps.length >= MAX_STEPS} onClick={() => { apply((s) => addStep(s, newStep(r))); setOpen(steps.length); }}><Plus size={11} /> Add a step</button>
        <button type="button" className="link small" data-testid="step-simplify" title="From 1:100 a plain grey fill with no hatch, from 1:200 solid; edit them after" disabled={steps.length + 2 > MAX_STEPS} onClick={() => apply((s) => [...s, ...simplifySteps()])}><Wand2 size={11} /> Simplify at small scales</button>
      </div>
    </div>
  );
}
