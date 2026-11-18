/** The "Small-scale cut" field of View settings and View styles: mode, the scale it starts at ("from 1:"), and the colour of a solid cut. */

import { ColourField } from '../../shell/ColourField';
import { DEFAULT_CUT_FROM, SMALL_SCALE_MODES, parseCutFrom, type SmallScaleCut, type SmallScaleCutMode } from './viewStyles';

/** `value`: what applies now; `onChange(part)`: one part changed (sent as `{smallScaleCut: part}`). */
export function SmallScaleCutControl({ value, onChange, testId }: { value: SmallScaleCut | null | undefined; onChange: (part: SmallScaleCut) => void; testId: string }) {
  const mode = value?.mode ?? 'asMaterial';
  const from = value?.from ?? DEFAULT_CUT_FROM;
  return (
    <span className="small-scale-cut" data-testid={testId}>
      <select data-testid={`${testId}-mode`} value={mode} onChange={(e) => onChange({ mode: e.target.value as SmallScaleCutMode })}>
        {SMALL_SCALE_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
      </select>
      {mode !== 'asMaterial' && (
        <>
          <label className="small" title="The scale it starts at: 1:200 and smaller scales">from 1:
            <input className="num" data-testid={`${testId}-from`} key={from} defaultValue={from} list="small-scale-cut-presets" inputMode="numeric"
              onBlur={(e) => { const v = parseCutFrom(e.target.value); if (v === null) { e.target.value = String(from); return; } if (v !== from) onChange({ from: v }); }}
              onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
          </label>
          <datalist id="small-scale-cut-presets">{[50, 100, 200, 500].map((s) => <option key={s} value={s} />)}</datalist>
        </>
      )}
      {mode === 'solid' && <ColourField value={value?.color ?? '#000000'} onChange={(v) => onChange({ color: v.slice(0, 7) })} />}
    </span>
  );
}
