/**
 * A rebar label's style (decisions 221, 222): what the text says (its parts, as tokens), the mark's balloon, the text's direction,
 * the side of the bars it stands on and how far, the leader (none, one, one to each bar seen as a dot) and, for stirrups and
 * distributions, the dimension string they are marked with. One block serves the project's settings (a whole style), a type
 * (its own fields over the project's) and a bar (over the project's): a field left to inherit shows the base's value.
 */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';
import { NumberField } from '../../shell/cells';

export interface LabelStyle { parts: string[]; balloon: boolean; direction: string; side: string; offset: number; leader: 'none' | 'one' | 'each'; line: 'none' | 'string' }
export type LabelStylePatch = Partial<Record<keyof LabelStyle, LabelStyle[keyof LabelStyle] | null>>;
export type LabelKind = 'bars' | 'stirrups' | 'columnBars' | 'runs';
export interface RebarSettings {
  colorByDiameter: boolean; textHeight: number; balloon: number; steel: string; segmentLengths: boolean; cover: number; labels: Record<LabelKind, LabelStyle>;
  /** The length a bar is delivered in (m): 12 or 6 as a rule; a longer bar is split with a lap (decision 227). */
  stockLength: number;
  /** The lap a split bar overlaps by (m); null: 50Ø. */
  lapLength: number | null;
}

/** The project's rebar settings as the engine resolves them (each kind's label style whole), read again after every change. */
export function useRebarSettings(): RebarSettings | null {
  const nodes = useSession((s) => s.nodes);
  const [s, setS] = useState<RebarSettings | null>(null);
  useEffect(() => { let live = true; void query<RebarSettings>('rebar.settings', {}).then((r) => { if (live && r) setS(r); }); return () => { live = false; }; }, [nodes]);
  return s;
}

export const TOKENS = ['count', 'diameter', 'step', 'length', 'mark', 'steel', 'text:…'];

/** The parts typed as tokens; applied when the field is left or Enter pressed, so a half-typed token is not refused. */
function PartsInput({ value, inherited, onChange }: { value: string; inherited: boolean; onChange: (parts: string) => void }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const apply = () => { if (text.trim() !== value) onChange(text.trim()); };
  return <input className={inherited ? 'muted' : undefined} type="text" value={text} spellCheck={false} data-no-i18n onChange={(e) => setText(e.target.value)} onBlur={apply} onKeyDown={(e) => { if (e.key === 'Enter') apply(); }} />;
}

export function LabelStyleFields({ style, base, onChange, onReset, testId, withLine }: {
  /** The level's own fields (partial), or the whole style at the project's level. */
  style: LabelStylePatch | LabelStyle | undefined;
  /** What a field shows when this level leaves it: the level under it; none at the project's. */
  base?: LabelStyle;
  onChange: (patch: LabelStylePatch) => void;
  onReset?: () => void;
  testId?: string;
  /** A distribution's kind (stirrups on the type, distributions): the dimension string is offered. */
  withLine?: boolean;
}) {
  const own = (style ?? {}) as LabelStylePatch;
  const of = <K extends keyof LabelStyle>(k: K): LabelStyle[K] | undefined => (own[k] ?? base?.[k]) as LabelStyle[K] | undefined;
  const has = (k: keyof LabelStyle) => own[k] != null;
  const direction = of('direction') ?? 'horizontal';
  const angle = direction.startsWith('angle:') ? Number(direction.slice(6)) : 0;
  const dirKind = direction.startsWith('angle:') ? 'angle' : direction;
  const owned = Object.keys(own).filter((k) => own[k as keyof LabelStyle] != null).length;
  return (
    <div className="layers" data-field={testId ?? 'rebar-label-style'} data-i18n-ctx="rebar-label">
      <label className="field-line"><span className="eyebrow">Says</span>
        <PartsInput value={(of('parts') ?? []).join(' ')} inherited={!!base && !has('parts')} onChange={(parts) => onChange({ parts: parts ? parts.split(/\s+/) : null })} />
      </label>
      <p className="muted small">Parts in order: count, diameter, step, length, mark, steel, text:… — count diameter length gives 3Ø16 L=6000.</p>
      <div className="row small">
        <label className="check small"><input type="checkbox" checked={of('balloon') ?? true} onChange={(e) => onChange({ balloon: e.target.checked })} /> Balloon</label>
        <label className="field-line"><span className="eyebrow">Leader</span>
          <select value={of('leader') ?? 'one'} onChange={(e) => onChange({ leader: e.target.value })}>
            <option value="none">None</option><option value="one">One line</option><option value="each">A line to each bar</option>
          </select>
        </label>
      </div>
      {withLine && <label className="field-line"><span className="eyebrow">Marked by</span>
        <select value={of('line') ?? 'string'} onChange={(e) => onChange({ line: e.target.value })}>
          <option value="string">A dimension string</option><option value="none">The text alone</option>
        </select>
      </label>}
      <label className="field-line"><span className="eyebrow">Text runs</span>
        <select value={dirKind} onChange={(e) => onChange({ direction: e.target.value === 'angle' ? `angle:${angle || 30}` : e.target.value })}>
          <option value="horizontal">Horizontal</option><option value="along">Along the bars</option><option value="angle">At an angle</option>
        </select>
        {dirKind === 'angle' && <><NumberField style={{ width: 56 }} value={angle} min={-180} max={180} suffix="°" onCommit={(n) => onChange({ direction: `angle:${n ?? 0}` })} /></>}
      </label>
      <label className="field-line"><span className="eyebrow">Stands</span>
        <select value={of('side') ?? 'auto'} onChange={(e) => onChange({ side: e.target.value })}>
          <option value="auto">Where it fits</option><option value="above">Above the bars</option><option value="below">Below the bars</option><option value="left">Left of the bars</option><option value="right">Right of the bars</option>
        </select>
        <NumberField style={{ width: 56 }} value={of('offset') ?? 4} min={0} max={100} onCommit={(n) => { if (n != null) onChange({ offset: n }); }} /><span className="muted small">mm off them</span>
      </label>
      {onReset && owned > 0 && <button type="button" className="link small" onClick={onReset}>Back to the project's style</button>}
    </div>
  );
}
