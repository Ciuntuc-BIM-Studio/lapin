/**
 * A type's layer's own pens by scale (decision 758), under its row in Wall types: the cut outline and the lines between the
 * layers, for all scales and per range of scales, over the layer's material's (Manage → Materials). Edited with the type and
 * saved with it (one undo step).
 */

import { Plus, Trash2, X } from 'lucide-react';
import { t } from '../../i18n';
import { useStandards } from '../../engine/unitsStore';
import { ColourControl, NumberInput } from '../core/MaterialGraphicsEditor';
import { ISO_WEIGHTS, localNumber, parseScaleDenominator, parseWeight, MAX_STEPS } from '../core/materialGraphicsModel';
import { addPenStep, penOf, penStepProblem, penSteps, removePenStep, setPen, setPenStepRange, type LayerGraphics, type PenLineKey } from './layerPens';

export const LAYER_PENS_TIP = 'This layer\'s own lines in this type, over its material\'s: the cut outline and the lines between the layers, for all scales and for ranges of scales (as in ArchiCAD)';

function ScaleEnd({ id, value, onCommit, testId }: { id: string; value: number | undefined; onCommit: (v: number | undefined) => void; testId: string }) {
  return (
    <span className="gfx-scale-end small">1:
      <input className="num" data-testid={testId} key={`${id}:${value ?? ''}`} defaultValue={value ?? ''} placeholder="open" list="layer-pen-scales" inputMode="numeric"
        onBlur={(e) => {
          if (e.target.value.trim() === '') { if (value !== undefined) onCommit(undefined); return; }
          const v = parseScaleDenominator(e.target.value);
          if (v === null) { e.target.value = value === undefined ? '' : String(value); return; }
          if (v !== value) onCommit(v);
        }}
        onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
    </span>
  );
}

export function LayerPens({ id, graphics, onChange }: { id: string; graphics: LayerGraphics | null; onChange: (g: LayerGraphics | null) => void }) {
  const scales = useStandards((s) => s.scales);
  const steps = penSteps(graphics);
  const problem = penStepProblem(graphics);
  const cells = (step: number | null, key: string) => (['cutLine', 'separatorLine'] as PenLineKey[]).map((line) => {
    const w = penOf(graphics, step, line, 'weight') as number | undefined;
    const c = penOf(graphics, step, line, 'color') as string | undefined;
    const base = step === null ? undefined : penOf(graphics, null, line, 'weight') as number | undefined;
    return [
      <td key={`${line}w`} className="num">
        <NumberInput id={`${key}${line}w`} field={`${line}-weight`} value={w} placeholder={base != null ? localNumber(base) : 'material\'s'} list="layer-pen-weights" parse={parseWeight}
          onCommit={(v) => onChange(setPen(graphics, step, line, 'weight', v))} />
      </td>,
      <td key={`${line}c`}>
        <span className="layer-pen-colour">
          <ColourControl value={c} auto="#000000" onSet={(v) => onChange(setPen(graphics, step, line, 'color', v))} />
          {c !== undefined && <button type="button" className="icon mini" title="Automatic" aria-label="Automatic" onClick={() => onChange(setPen(graphics, step, line, 'color', undefined))}><X size={10} /></button>}
        </span>
      </td>,
    ];
  });
  return (
    <div className="layer-pens" data-testid="layer-pens">
      <datalist id="layer-pen-scales">{scales.map((s) => <option key={s} value={s}>{`1:${s}`}</option>)}</datalist>
      <datalist id="layer-pen-weights">{ISO_WEIGHTS.map((w) => <option key={w} value={localNumber(w)} />)}</datalist>
      <p className="muted small">{t('The first range that holds the view\'s scale wins over “All scales”; empty: the material\'s lines (Manage → Materials).')}</p>
      <table className="layer-pens-table">
        <thead>
          <tr><th>Scales</th><th colSpan={2}>Cut outline</th><th colSpan={2}>Separator lines</th><th /></tr>
          <tr className="muted small"><th /><th>Weight, mm</th><th>Colour</th><th>Weight, mm</th><th>Colour</th><th /></tr>
        </thead>
        <tbody>
          <tr data-testid="layer-pen-all"><td>All scales</td>{cells(null, `${id}a`)}<td /></tr>
          {steps.map((s, i) => (
            <tr key={i} data-testid="layer-pen-step" className={problem?.index === i ? 'bad' : undefined}>
              <td className="layer-pen-range">
                <span className="small">from</span><ScaleEnd id={`${id}${i}f`} value={s.from} testId="layer-pen-from" onCommit={(v) => onChange(setPenStepRange(graphics, i, 'from', v))} />
                <span className="small">to</span><ScaleEnd id={`${id}${i}t`} value={s.to} testId="layer-pen-to" onCommit={(v) => onChange(setPenStepRange(graphics, i, 'to', v))} />
              </td>
              {cells(i, `${id}${i}`)}
              <td className="row-tools"><button type="button" className="icon mini" title="Remove the range" aria-label="Remove the range" onClick={() => onChange(removePenStep(graphics, i))}><Trash2 size={12} /></button></td>
            </tr>
          ))}
        </tbody>
      </table>
      {problem && <p className="small error-text" role="alert" data-testid="layer-pen-problem">{t(problem.text)}</p>}
      <button type="button" className="link small" data-testid="layer-pen-add" disabled={steps.length >= MAX_STEPS} onClick={() => onChange(addPenStep(graphics, scales))}><Plus size={11} /> Add a range of scales</button>
    </div>
  );
}
