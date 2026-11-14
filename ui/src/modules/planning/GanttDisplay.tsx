/**
 * The Gantt's display by the office's fields (docs/plan-user-extensibility.md §12.4): Colour by a field with the colour of
 * each value, Group by a field (the rows of the window), and up to six field columns in the task table. Each change is one
 * `planning.updateSchedule {gantt}`; Reset takes the display off. The legend of the colours is drawn under the toolbar.
 */

import { ColourField } from '../../shell/ColourField';
import { useState } from 'react';
import { Braces, Plus, RotateCcw, Trash2, X } from 'lucide-react';
import { execute } from '../../engine/session';
import { useUi } from '../../app/store';
import { FieldPicker } from '../../shell/FieldPicker';
import type { GanttDisplay, LegendEntry } from './gantt';

export const MAX_FIELD_COLUMNS = 6;

/** The display as the engine takes it: nothing empty; null when nothing is left (the plain Gantt). */
export function cleanDisplay(g: GanttDisplay): GanttDisplay | null {
  const out: GanttDisplay = {};
  if (g.colourBy?.trim()) { out.colourBy = g.colourBy.trim(); if (g.colours && Object.keys(g.colours).length) out.colours = g.colours; }
  if (g.groupBy?.trim()) out.groupBy = g.groupBy.trim();
  const cols = (g.columns ?? []).map((c) => c.trim()).filter(Boolean).slice(0, MAX_FIELD_COLUMNS);
  if (cols.length) out.columns = cols;
  return Object.keys(out).length ? out : null;
}

const save = (g: GanttDisplay) => void execute('planning.updateSchedule', { gantt: cleanDisplay(g) });

/** A colour chosen in the box, kept when the box is left (the picker fires on every move of the mouse). */
function Swatch({ value, onCommit }: { value: string; onCommit: (hex: string) => void }) {
  const [v, setV] = useState(value);
  return <ColourField value={v} onChange={(c) => setV(c)} onCommit={(c) => c !== value && onCommit(c)} aria-label="Colour" />;
}

export function GanttDisplayPanel({ at, gantt, legend, onClose }: { at: { top: number; left: number }; gantt: GanttDisplay | null | undefined; legend: LegendEntry[]; onClose: () => void }) {
  const g = gantt ?? {};
  const cols = g.columns ?? [];
  const [adding, setAdding] = useState(false);
  return (
    <div className="gantt-display" style={{ top: at.top, left: at.left }} data-testid="gantt-display" role="dialog" aria-label="Display by fields">
      <div className="gantt-link-title"><span className="eyebrow">Display by fields</span><button type="button" className="icon mini" title="Close" onClick={onClose}><X size={12} /></button></div>
      <p className="muted small">Any field of the tasks' elements: the model's own, a file's property, or the office's field names.</p>
      <div className="gd-row"><span className="eyebrow">Colour by</span>
        <FieldPicker value={g.colourBy ?? ''} onPick={(t) => save({ ...g, colourBy: t, colours: {} })} placeholder="(none)" testId="gantt-colour-field" />
        {g.colourBy && <button type="button" className="icon mini" title="Take it off" onClick={() => save({ ...g, colourBy: undefined, colours: undefined })}><X size={11} /></button>}
      </div>
      {g.colourBy && <div className="gd-legend" data-testid="gantt-colours">
        {legend.map((e) => (
          <label key={e.value} className="gd-swatch" title={`${e.tasks}`}><Swatch key={e.colour} value={e.colour} onCommit={(hex) => save({ ...g, colours: { ...(g.colours ?? {}), [e.value]: hex } })} /><span data-no-i18n>{e.value}</span><span className="muted small" data-no-i18n>{e.tasks}</span></label>
        ))}
        {legend.length === 0 && <span className="muted small">No task has this field.</span>}
      </div>}
      <div className="gd-row"><span className="eyebrow" title="The rows of the window grouped by the value, each group under a header; the tasks that do the work only. The sheet keeps the WBS order.">Group by</span>
        <FieldPicker value={g.groupBy ?? ''} onPick={(t) => save({ ...g, groupBy: t })} placeholder="(none)" testId="gantt-group-field" />
        {g.groupBy && <button type="button" className="icon mini" title="Take it off" onClick={() => save({ ...g, groupBy: undefined })}><X size={11} /></button>}
      </div>
      <div className="gd-columns" data-testid="gantt-field-columns"><span className="eyebrow">Columns</span>
        {cols.map((c, i) => (
          <div className="gd-row" key={`${i}:${c}`}>
            <FieldPicker value={c} onPick={(t) => save({ ...g, columns: cols.map((x, j) => (j === i ? t : x)) })} testId={`gantt-column-${i}`} />
            <button type="button" className="icon mini" title="Remove" onClick={() => save({ ...g, columns: cols.filter((_, j) => j !== i) })}><Trash2 size={12} /></button>
          </div>
        ))}
        {adding && <div className="gd-row"><FieldPicker value="" onPick={(t) => { setAdding(false); save({ ...g, columns: [...cols, t] }); }} testId="gantt-column-new" /></div>}
        {cols.length < MAX_FIELD_COLUMNS && !adding && <button type="button" className="btn small" onClick={() => setAdding(true)} data-testid="gantt-add-column"><Plus size={12} />Column</button>}
        <span className="muted small">{`${cols.length} of ${MAX_FIELD_COLUMNS}`}</span>
      </div>
      <div className="gd-foot">
        <button type="button" className="link" onClick={() => useUi.setState({ dialog: 'fieldAliases' })} data-testid="gantt-field-names"><Braces size={12} />Field names…</button>
        <button type="button" className="link" disabled={!gantt} onClick={() => void execute('planning.updateSchedule', { gantt: null })} data-testid="gantt-display-reset"><RotateCcw size={12} />Reset</button>
      </div>
    </div>
  );
}

/** The legend under the toolbar: each value with its colour and how many tasks have it. */
export function GanttLegend({ legend }: { legend: LegendEntry[] }) {
  if (!legend.length) return null;
  return (
    <div className="gantt-legend" data-testid="gantt-legend">
      {legend.map((e) => <span key={e.value} title={`${e.tasks}`}><i style={{ background: e.colour }} /><span data-no-i18n>{e.value}</span></span>)}
    </div>
  );
}
