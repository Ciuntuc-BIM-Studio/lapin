/**
 * Graphics in this view… (decision 718, issue #88; Revit's Override Graphics in View → By Element): the selected elements'
 * cut hatch (pattern or none, scale, angle), cut fill, the hatch and fill of their faces seen in elevations, the hatch colour
 * and halftone, their cut and projection lines, or the whole element halftone, in the view in front or in every plan,
 * section and elevation. Over the materials' graphics and the view's Visibility / Graphics; Apply is one undo step,
 * Reset takes the elements back to them. From the properties panel and a view's right-click menu.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { backdropClose } from '../../shell/Dialog';
import { ColourField } from '../../shell/ColourField';
import { NumberField } from '../../shell/cells';
import { frontView } from '../../viewport/frontView';
import { PendingDialog } from './PendingDialog';
import { PatternOptions, listPatterns, type PatternInfo } from './hatchUnits';
import { useLineTypes } from './lineTypes/lineTypes';
import { patternChoices } from './lineTypes/lineTypeModel';
import { commonRow, elementGraphicsArgs, type ElementGraphicsRow, type LineRow, type Scope } from './elementGraphics';

/** The ISO 128 pen series (mm) a line can be drawn in. */
const WEIGHTS = [0.13, 0.18, 0.25, 0.35, 0.5, 0.7, 1.0];
const LINE_TYPES: [string, string][] = [['', 'as drawn'], ['continuous', 'continuous'], ['dashed', 'dashed'], ['hidden', 'hidden'], ['center', 'center'], ['dotted', 'dotted'], ['dashdot', 'dash dot']];

export function ElementGraphicsDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const close = () => useUi.setState({ dialog: null });
  const [ids] = useState<string[]>(() => { const ui = useUi.getState(); return ui.selected.length ? ui.selected : ui.selection ? [ui.selection] : []; });
  const [target, setTarget] = useState<string | null>(null);
  const [row, setRow] = useState<ElementGraphicsRow | null>(null);
  const [scope, setScope] = useState<Scope>('view');
  const [patterns, setPatterns] = useState<PatternInfo[]>([]);
  const lt = useLineTypes();

  useEffect(() => {
    if (!ids.length) { notify('Select the elements first.'); close(); return; }
    void frontView().then((id) => { if (!id) { notify('Open a plan, a section, an elevation or the 3D first.'); close(); } else setTarget(id); });
    void listPatterns().then((r) => r && setPatterns(r));
  }, []);
  useEffect(() => {
    if (!target) return;
    let live = true;
    void query<{ rows: Record<string, ElementGraphicsRow> }>('view.elementGraphicsOf', { id: target, ids }).then((r) => { if (live) setRow(commonRow(ids.map((id) => r?.rows?.[id]))); });
    return () => { live = false; };
  }, [target]);

  if (!target || !row) return <PendingDialog title="Graphics in this view" />;
  const put = (patch: Partial<ElementGraphicsRow>) => setRow({ ...row, ...patch });
  const putLine = (k: 'cutLine' | 'projectionLine', patch: Partial<LineRow>) => setRow({ ...row, [k]: { ...(row[k] ?? {}), ...patch } });
  const view = target === '3d' ? null : nodes.get(target);
  const apply = async (value: ElementGraphicsRow | null) => {
    const r = await execute('view.elementGraphics', elementGraphicsArgs(target, ids, value, scope));
    if (r) close();
  };

  /** A hatch's pattern, scale, angle and fill (the cut's or the seen faces'). */
  const hatchRows = (kind: 'cut' | 'surface') => {
    const [h, s, a, f] = kind === 'cut' ? (['cutHatch', 'cutHatchScale', 'cutHatchAngle', 'cutFill'] as const) : (['surfaceHatch', 'surfaceHatchScale', 'surfaceHatchAngle', 'surfaceFill'] as const);
    const fill = row[f];
    const fillMode = fill === 'none' ? 'none' : fill ? 'colour' : '';
    return <>
      <dt>Pattern</dt>
      <dd><select data-field={`${kind}-hatch`} value={row[h] ?? ''} onChange={(e) => put({ [h]: e.target.value || undefined })}>
        <option value="">as the material</option><option value="none">none</option><PatternOptions patterns={patterns} current={row[h] && row[h] !== 'none' ? row[h] : undefined} />
      </select></dd>
      <dt title="1: the pattern's own size; 2: twice as large">Scale</dt>
      <dd><NumberField testId={`${kind}-hatch-scale`} value={row[s]} min={0.01} placeholder="as the material" onCommit={(v) => put({ [s]: v ?? undefined })} /></dd>
      <dt>Angle</dt>
      <dd><NumberField testId={`${kind}-hatch-angle`} value={row[a]} suffix="°" placeholder="as the material" onCommit={(v) => put({ [a]: v ?? undefined })} /></dd>
      <dt>Fill</dt>
      <dd>
        <select data-field={`${kind}-fill`} value={fillMode} onChange={(e) => put({ [f]: e.target.value === 'none' ? 'none' : e.target.value === 'colour' ? (fill && fill !== 'none' ? fill : '#d0d0d0') : undefined })}>
          <option value="">as the material</option><option value="none">none</option><option value="colour">colour…</option>
        </select>
        {fillMode === 'colour' && <ColourField aria-label="Fill colour" value={fill ?? '#d0d0d0'} onChange={(x) => put({ [f]: x })} />}
      </dd>
    </>;
  };

  /** A line's colour, weight and type. */
  const lineRows = (k: 'cutLine' | 'projectionLine') => {
    const l = row[k] ?? {};
    return <>
      <dt>Colour</dt>
      <dd><ColourField aria-label="Line colour" value={l.color ?? '#000000'} onChange={(x) => putLine(k, { color: x })} />
        {l.color && <button type="button" className="icon mini" title="As drawn" aria-label="As drawn" onClick={() => putLine(k, { color: undefined })}><X size={10} /></button>}</dd>
      <dt>Weight</dt>
      <dd><select data-field={`${k}-weight`} value={l.weight ?? ''} onChange={(e) => putLine(k, { weight: e.target.value ? Number(e.target.value) : undefined })}>
        <option value="">as drawn</option>{WEIGHTS.map((w) => <option key={w} value={w}>{`${w} mm`}</option>)}
      </select></dd>
      <dt>Line type</dt>
      <dd><select data-field={`${k}-type`} value={l.lineType ?? ''} onChange={(e) => putLine(k, { lineType: e.target.value || undefined })}>
        {patternChoices(LINE_TYPES, lt, l.lineType).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
      </select></dd>
    </>;
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Graphics in this view" onMouseDown={backdropClose(close)}>
      <div className="dialog" data-dialog="elementGraphics">
        <header className="dialog-head">
          <span className="panel-title">Graphics in this view</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">
            {ids.length === 1 ? <span>For this element</span> : <><span>Elements selected:</span> <span data-no-i18n>{ids.length}</span></>}
            {' · '}{view ? <span data-no-i18n>{view.name}</span> : <span>3D</span>}.{' '}
            <span>Over its material (Manage → Materials) and the view's Visibility / Graphics; the others stay as they are.</span>
          </p>
          <h4 className="eyebrow">Cut (plans, sections)</h4>
          <dl className="fields">{hatchRows('cut')}</dl>
          <h4 className="eyebrow">Seen faces (elevations)</h4>
          <dl className="fields">{hatchRows('surface')}</dl>
          <h4 className="eyebrow">Hatch</h4>
          <dl className="fields">
            <dt>Hatch colour</dt>
            <dd><ColourField aria-label="Hatch colour" value={row.hatchColor ?? '#000000'} onChange={(x) => put({ hatchColor: x })} />
              {row.hatchColor && <button type="button" className="icon mini" title="As the material" aria-label="As the material" onClick={() => put({ hatchColor: undefined })}><X size={10} /></button>}</dd>
            <dt>Hatch halftone</dt>
            <dd><input type="checkbox" data-field="hatch-halftone" checked={!!row.hatchHalftone} onChange={(e) => put({ hatchHalftone: e.target.checked || undefined })} /></dd>
          </dl>
          <h4 className="eyebrow">Cut lines</h4>
          <dl className="fields">{lineRows('cutLine')}</dl>
          <h4 className="eyebrow">Projection lines</h4>
          <dl className="fields">{lineRows('projectionLine')}</dl>
          <dl className="fields">
            <dt title="Its lines and fills grey, as a halftone link">Halftone</dt>
            <dd><input type="checkbox" data-field="halftone" checked={!!row.halftone} onChange={(e) => put({ halftone: e.target.checked || undefined })} /></dd>
            <dt>Apply to</dt>
            <dd><select data-field="scope" value={scope} onChange={(e) => setScope(e.target.value as Scope)}>
              <option value="view">this view</option><option value="all">every plan, section and elevation</option>
            </select></dd>
          </dl>
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
        <footer className="dialog-foot row">
          <button type="button" className="btn" data-action="element-graphics-reset" title="Back to the material's and the view's graphics" onClick={() => void apply(null)}>Reset</button>
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-action="element-graphics-apply" onClick={() => void apply(row)}>Apply</button>
        </footer>
      </div>
    </div>
  );
}
