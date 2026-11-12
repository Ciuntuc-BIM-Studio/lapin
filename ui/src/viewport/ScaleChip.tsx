/**
 * The view's corner controls: its scale, picked there (forum #30, decision 492: the project's standard scales, one
 * typed, or Drafting standards → Project for the scales new views take), its partial structure display (decision 500),
 * and how big it shows against paper, which a click brings to paper size (ZA).
 */

import { useEffect, useState } from 'react';
import { notify } from '../app/store';
import { execute, query, useSession } from '../engine/session';
import { STRUCTURE_MODES, styleStructure, type LayerMode, type ViewStyle } from '../modules/core/viewStyles';
import { structureCommand } from './viewStructure';
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

interface StyleRead { style: ViewStyle; resolved: ViewStyle; overrides: string[] }

/**
 * The view's partial structure display (decision 500), beside its scale: the entire model, without the finishes, the cores
 * only, the load-bearing elements' cores only; changed as its settings change it (one undo step), its style's for a view of
 * its kind picked to take the style's again. None to change while there is no view.
 */
export function StructurePicker({ viewId, kind }: { viewId: string | null; kind?: string | null }) {
  const nodes = useSession((s) => s.nodes);
  const [read, setRead] = useState<{ id: string; state: StyleRead } | null>(null);
  useEffect(() => {
    if (!viewId) return;
    let live = true;
    void query<StyleRead>('view.style', { id: viewId }).then((r) => { if (live && r) setRead({ id: viewId, state: r }); });
    return () => { live = false; };
  }, [viewId, nodes]);
  const state = read && read.id === viewId ? read.state : null;
  const current: LayerMode = state?.resolved.layers ?? 'all';
  const byStyle = state ? styleStructure(state.style, kind ?? 'plan') : current;
  const pick = (next: LayerMode) => {
    if (!viewId) return;
    const c = structureCommand(viewId, next, current, byStyle);
    if (c) void execute(c.command, c.args);
  };
  return (
    <select className="chip scale-pick structure-pick" data-testid="view-structure" aria-label="Partial structure display" value={current} disabled={!viewId || !state}
      title="Partial structure display: which layers of walls, floors and roofs the view draws (one undo step); the style's for this kind of view is marked ·" onChange={(e) => pick(e.target.value as LayerMode)}>
      {STRUCTURE_MODES.map(([k, l, d]) => <option key={k} value={k} title={d}>{k === byStyle ? `${t(l)} ·` : l}</option>)}
    </select>
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
