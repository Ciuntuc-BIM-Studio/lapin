/**
 * The view's corner controls: its scale, picked there (forum #30, decision 492: the project's standard scales, one
 * typed, or Drafting standards → Project for the scales new views take), and how big it shows against paper, which a
 * click brings to paper size (ZA).
 */

import { notify } from '../app/store';
import { execute } from '../engine/session';
import { useStandards } from '../engine/unitsStore';
import { t } from '../i18n';
import { openStandards } from '../modules/core/standardsTab';
import { parseScale, scaleCommand, scaleOptions } from './viewScale';
import { actualScale } from './zoom';

export function ScaleChip({ pxPerMetre, scale, onActual }: { pxPerMetre: number; scale: number; onActual: () => void }) {
  const pct = Math.round((pxPerMetre / actualScale(scale)) * 100);
  return (
    <button type="button" className="chip scale" title="Size on screen against paper · click for paper size (ZA)" onClick={onActual}>
      {pct} %
    </button>
  );
}

const CUSTOM = 'custom';
const DEFAULTS = 'defaults';

/** The view's scale, changed as its settings change it (one undo step); none to change while there is no view. */
export function ScalePicker({ viewId, scale }: { viewId: string | null; scale: number }) {
  const choices = useStandards((s) => s.scales);
  const apply = (next: number) => {
    if (!viewId || next === scale) return;
    const { command, args } = scaleCommand(viewId, next);
    void execute(command, args);
  };
  const pick = (value: string) => {
    if (value === DEFAULTS) { openStandards('project'); return; }
    if (value === CUSTOM) {
      const typed = window.prompt(t('Scale 1:'), String(scale));
      if (typed == null) return;
      const n = parseScale(typed);
      if (n == null) { notify('A scale is 1:1 to 1:10000.'); return; }
      apply(n);
      return;
    }
    apply(Number(value));
  };
  return (
    <select className="chip scale-pick" data-testid="view-scale" aria-label="View scale" value={String(scale)} disabled={!viewId}
      title="The view's scale: one of the project's standard scales, or your own (one undo step)" onChange={(e) => pick(e.target.value)}>
      {scaleOptions(choices, scale).map((s) => <option key={s} value={String(s)} data-no-i18n>1:{s}</option>)}
      <option value={CUSTOM}>Custom…</option>
      <option value={DEFAULTS}>Default scales…</option>
    </select>
  );
}
