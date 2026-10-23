/**
 * A lintel rule, as the wall types dialog and the configurator edit it:
 * none, precast (a catalogue length centred over the bearings) or cast in
 * place, with the bearing past each side and the height (mm). The
 * configurator adds "as the wall" (no rule of its own).
 */

import { useEffect, useState } from 'react';
import { mm } from '../../engine/model';

export interface Rule { mode: 'none' | 'precast' | 'cast'; bearing: number; height: number; [k: string]: unknown }

export const DEFAULT_RULE: Rule = { mode: 'precast', bearing: 0.25, height: 0.25 };

function MmInput({ value, onChange }: { value: number; onChange: (m: number) => void }) {
  const [text, setText] = useState(mm(value));
  useEffect(() => setText(mm(value)), [value]);
  return <input className="num" value={text} onChange={(e) => { setText(e.target.value); const v = Number(e.target.value.replace(',', '.')); if (Number.isFinite(v) && v >= 0) onChange(v / 1000); }} />;
}

/** `rule` null: as the wall (only when `inherit` is offered). */
export function LintelRuleEditor({ rule, onChange, inherit }: { rule: Rule | null; onChange: (r: Rule | null) => void; inherit?: boolean }) {
  const mode = rule?.mode ?? 'wall';
  return (
    <div className="row lintel-rule">
      <span className="eyebrow">Lintels</span>
      <select value={mode} onChange={(e) => onChange(e.target.value === 'wall' ? null : { ...(rule ?? DEFAULT_RULE), mode: e.target.value as Rule['mode'] })}>
        {inherit && <option value="wall">As the wall</option>}
        <option value="precast">Precast</option>
        <option value="cast">Cast in place</option>
        <option value="none">None</option>
      </select>
      {rule && rule.mode !== 'none' && <>
        <span className="muted small">bearing</span><MmInput value={rule.bearing} onChange={(v) => onChange({ ...rule, bearing: v })} />
        <span className="muted small">height</span><MmInput value={rule.height} onChange={(v) => onChange({ ...rule, height: v })} />
        <span className="muted small">mm</span>
      </>}
    </div>
  );
}
