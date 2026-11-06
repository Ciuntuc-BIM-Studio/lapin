/**
 * What the user tucked away at the bottom of the views, kept on this computer: the quick tools bar, hidden apart in the
 * model's views (plan, 3D, sections) and in the documents (sheets, schedules, the Gantt, the estimate), so hiding it
 * over a schedule leaves it on the plan; and a schedule's table editor, folded into a strip so only the result shows.
 */

import { create } from 'zustand';

export type BarScope = 'model' | 'docs';
interface Bars { quickToolsHidden: Record<BarScope, boolean>; scheduleEditor: boolean }

const KEY = 'lapin.bars';
const DEFAULTS: Bars = { quickToolsHidden: { model: false, docs: false }, scheduleEditor: true };

function read(): Bars {
  try {
    const s = JSON.parse(localStorage.getItem(KEY) ?? 'null') as Partial<Bars> | null;
    return { quickToolsHidden: { ...DEFAULTS.quickToolsHidden, ...(s?.quickToolsHidden ?? {}) }, scheduleEditor: s?.scheduleEditor ?? DEFAULTS.scheduleEditor };
  } catch { return DEFAULTS; }
}

export const useBars = create<Bars>(() => read());

function save(next: Bars) {
  useBars.setState(next, true);
  try { localStorage.setItem(KEY, JSON.stringify(next)); } catch { /* kept for this session */ }
}

/** The documents' views, where the quick tools are hidden apart. */
export const DOC_VIEWS = new Set(['sheet', 'schedule', 'gantt', 'estimate', 'costTime']);
export const scopeOf = (view: string | null | undefined): BarScope => (view && DOC_VIEWS.has(view) ? 'docs' : 'model');

export function setQuickToolsHidden(scope: BarScope, hidden: boolean) {
  const s = useBars.getState();
  save({ ...s, quickToolsHidden: { ...s.quickToolsHidden, [scope]: hidden } });
}

export function setScheduleEditor(open: boolean) { save({ ...useBars.getState(), scheduleEditor: open }); }
