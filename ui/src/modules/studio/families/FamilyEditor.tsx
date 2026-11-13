/**
 * The family editor (docs/plan-user-extensibility.md §5.6): a family's name, IFC class, parameters (key, label, kind, default) and
 * bodies (plane, corners, depth, offset, condition, material) — every corner, depth, offset and condition a number or a formula of
 * the parameters — with a live wireframe the engine builds, values to try, and the engine's own sentence when the family does not
 * read. The saving is the dialog's.
 */

import { useEffect, useMemo, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { t as tr } from '../../../i18n';
import { formatLength, parseLength } from '../../../engine/unitsStore';
import { FORMULA_FUNCTIONS } from '../../../shell/formulaModel';
import { MaterialPicker } from '../../core/MaterialPicker';
import { previewFamily } from './familyApi';
import {
  IFC_CLASSES, PARAM_KINDS, PARAM_KIND_LABELS, PLANES, PLANE_LABELS, addBody, addParam, addPoint, cellText, className, defaultFor, formulaNames, isFormula, keyProblem, parseCell, parseValue, removeBody,
  removeParam, removePoint, setBody, setBodyOptional, setOffset, setParam, setPoint, sizeOf, valuesOf, wireframe,
  type Cell, type FamilyDef, type PreviewBody,
} from './familyModel';

/** A cell that keeps what is typed (a half-written „0.” or „W -”) and tells the definition what it reads as at each key. */
export function CellInput({ value, onCommit, label, width = 64 }: { value: Cell | undefined; onCommit: (text: string) => void; label: string; width?: number }) {
  const [text, setText] = useState(cellText(value));
  useEffect(() => { if (parseCell(text) !== (value ?? 0)) setText(cellText(value)); }, [value]); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <input className={`mono fam-cell${isFormula(value) ? ' formula' : ''}`} style={{ width }} value={text} spellCheck={false} aria-label={label} data-no-i18n
      onChange={(e) => { setText(e.target.value); onCommit(e.target.value); }} />
  );
}

/** The wireframe, drawn from the engine's edges. */
export function Wireframe({ bodies, width = 300, height = 190 }: { bodies: PreviewBody[]; width?: number; height?: number }) {
  const w = useMemo(() => wireframe(bodies, width, height), [bodies, width, height]);
  return (
    <svg className="fam-wire" viewBox={`0 0 ${w.width} ${w.height}`} width={w.width} height={w.height} role="img" aria-label={tr('Preview of the family')} data-field="fam-preview">
      <g fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round">
        {w.lines.map((l, i) => <line key={i} x1={l[0]} y1={l[1]} x2={l[2]} y2={l[3]} />)}
      </g>
    </svg>
  );
}

export function FamilyEditor({ def, onChange }: { def: FamilyDef; onChange: (d: FamilyDef) => void }) {
  const [typed, setTyped] = useState<Record<string, string>>({});
  const [shown, setShown] = useState<{ bodies: PreviewBody[] } | { error: string } | null>(null);
  const values = useMemo(() => valuesOf(def, typed, (t) => parseLength(t)), [def, typed]);
  useEffect(() => {
    let live = true;
    const t = setTimeout(() => { void previewFamily({ def }, values).then((r) => { if (live) setShown(r); }); }, 220);
    return () => { live = false; clearTimeout(t); };
  }, [def, values]);
  const names = formulaNames(def);
  const bodies = shown && 'bodies' in shown ? shown.bodies : [];
  const size = sizeOf(bodies);
  const classes = IFC_CLASSES.includes(def.ifcClass) ? IFC_CLASSES : [def.ifcClass, ...IFC_CLASSES];

  return (
    <div className="fam-editor" data-field="family-editor">
      <div className="fam-top">
        <label className="fam-field"><span className="muted small">IFC class</span>
          <select value={def.ifcClass} data-field="fam-class" onChange={(e) => onChange({ ...def, ifcClass: e.target.value })}>
            {classes.map((c) => <option key={c} value={c} data-no-i18n>{className(c)} ({c})</option>)}
          </select></label>
        <label className="fam-field grow"><span className="muted small">Description</span>
          <input value={def.description ?? ''} data-field="fam-description" onChange={(e) => { const { description: _d, ...rest } = def; void _d; onChange(e.target.value ? { ...rest, description: e.target.value } : rest); }} /></label>
      </div>

      <section className="fam-sticky" aria-label={tr('Preview')}>
      <div className="fam-preview">
          <div className="fam-wire-box">
            <Wireframe bodies={bodies} />
            {shown && 'error' in shown && <p className="error-text small fam-error" data-field="fam-error" data-no-i18n>{shown.error}</p>}
          </div>
          <div className="fam-try">
            <p className="muted small">Try values: empty is the default.</p>
            {def.params.filter((p) => p.key).map((p) => (
              <label className="fam-try-row" key={p.key}><span data-no-i18n>{p.label}</span>
                {p.kind === 'bool'
                  ? <select value={typed[p.key] ?? ''} data-fam-try={p.key} onChange={(e) => setTyped({ ...typed, [p.key]: e.target.value })}>
                    <option value="">{tr(p.default ? 'Yes (default)' : 'No (default)')}</option><option value="yes">Yes</option><option value="no">No</option></select>
                  : <input className="mono" value={typed[p.key] ?? ''} placeholder={p.kind === 'length' ? formatLength(p.default) : String(p.default)} data-fam-try={p.key} data-no-i18n
                    aria-invalid={parseValue(typed[p.key] ?? '', p.kind, (t) => parseLength(t)) === 'bad'} onChange={(e) => setTyped({ ...typed, [p.key]: e.target.value })} />}
              </label>
            ))}
            {size && <p className="muted small" data-no-i18n>{`${formatLength(size[0])} × ${formatLength(size[1])} × ${formatLength(size[2])}`}</p>}
            {bodies.length > 0 && <p className="muted small" data-no-i18n>{bodies.map((b) => `${b.name}: ${Math.round(b.volume * 1e4) / 1e4} m³`).join(' · ')}</p>}
          </div>
        </div>
      </section>

      <h4 className="eyebrow">Parameters</h4>
      <table className="layer-table fam-table" data-field="fam-params">
        <thead><tr><th>Key</th><th>Label</th><th>Kind</th><th>Default</th><th /></tr></thead>
        <tbody>
          {def.params.map((p, i) => {
            const problem = keyProblem(def, i);
            return (
              <tr key={i}>
                <td><input className={`mono fam-cell${problem ? ' bad' : ''}`} style={{ width: 90 }} value={p.key} title={problem ? tr(problem) : undefined} data-no-i18n data-fam-key={i}
                  onChange={(e) => onChange(setParam(def, i, { key: e.target.value }))} /></td>
                <td><input value={p.label} style={{ width: 130 }} data-no-i18n onChange={(e) => onChange(setParam(def, i, { label: e.target.value }))} /></td>
                <td><select value={p.kind} onChange={(e) => { const kind = e.target.value as typeof p.kind; onChange(setParam(def, i, { kind, default: defaultFor(kind, p.default) })); }}>
                  {PARAM_KINDS.map((k) => <option key={k} value={k}>{PARAM_KIND_LABELS[k]}</option>)}</select></td>
                <td>{p.kind === 'bool'
                  ? <input type="checkbox" checked={!!p.default} aria-label={tr('Default')} onChange={(e) => onChange(setParam(def, i, { default: e.target.checked ? 1 : 0 }))} />
                  : <CellInput value={p.default} label={tr('Default')} onCommit={(t) => { const c = parseCell(t); onChange(setParam(def, i, { default: typeof c === 'number' ? c : p.default })); }} />}</td>
                <td><button type="button" className="icon mini" title="Remove the parameter" onClick={() => onChange(removeParam(def, i))}><X size={12} /></button></td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <div className="row"><button type="button" className="btn small" data-field="fam-add-param" onClick={() => onChange(addParam(def))}><Plus size={12} />Add a parameter</button>
        <span className="muted small">Lengths in metres, angles in degrees.</span></div>

      <h4 className="eyebrow">Bodies</h4>
      {def.bodies.map((b, bi) => (
        <section className="fam-body" key={bi} data-fam-body={bi}>
          <div className="row wrap">
            <input value={b.name} style={{ width: 140 }} aria-label={tr('Name')} data-no-i18n data-fam-body-name={bi} onChange={(e) => onChange(setBody(def, bi, { name: e.target.value }))} />
            <select value={b.plane} aria-label={tr('Plane')} data-fam-plane={bi} onChange={(e) => onChange(setBody(def, bi, { plane: e.target.value as typeof b.plane }))}>
              {PLANES.map((p) => <option key={p} value={p}>{PLANE_LABELS[p]}</option>)}</select>
            <span className="grow" />
            <button type="button" className="icon mini" title="Remove the body" disabled={def.bodies.length < 2} onClick={() => onChange(removeBody(def, bi))}><X size={12} /></button>
          </div>
          <div className="fam-body-grid">
            <div>
              <table className="layer-table fam-table">
                <thead><tr><th>#</th><th>x</th><th>y</th><th /></tr></thead>
                <tbody>
                  {b.points.map((pt, pi) => (
                    <tr key={pi}>
                      <td className="muted small">{pi + 1}</td>
                      <td><CellInput value={pt[0]} label={`x ${pi + 1}`} width={80} onCommit={(t) => onChange(setPoint(def, bi, pi, 0, t))} /></td>
                      <td><CellInput value={pt[1]} label={`y ${pi + 1}`} width={80} onCommit={(t) => onChange(setPoint(def, bi, pi, 1, t))} /></td>
                      <td className="nowrap">
                        <button type="button" className="icon mini" title="A corner after this one" onClick={() => onChange(addPoint(def, bi, pi))}><Plus size={11} /></button>
                        <button type="button" className="icon mini" title="Remove the corner" disabled={b.points.length < 4} onClick={() => onChange(removePoint(def, bi, pi))}><X size={11} /></button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <dl className="fields fam-fields">
              <dt>Depth</dt><dd><CellInput value={b.depth} label={tr('Depth')} width={110} onCommit={(t) => onChange(setBody(def, bi, { depth: parseCell(t) }))} /></dd>
              <dt>Offset x, y, z</dt>
              <dd className="row">{([0, 1, 2] as const).map((a) => <CellInput key={a} value={b.offset?.[a] ?? 0} label={`${tr('Offset')} ${'xyz'[a]}`} width={62} onCommit={(t) => onChange(setOffset(def, bi, a, t))} />)}</dd>
              <dt>Made when</dt>
              <dd><input className="mono fam-cell" style={{ width: 170 }} placeholder="HasShelf, W > 1.2" value={b.when ?? ''} spellCheck={false} data-no-i18n data-fam-when={bi}
                onChange={(e) => onChange(setBodyOptional(def, bi, 'when', e.target.value))} /></dd>
              <dt>Material</dt>
              <dd><MaterialPicker value={b.material ?? ''} allowEmpty placeholder="Grey" onChange={(m) => onChange(setBodyOptional(def, bi, 'material', m))} /></dd>
            </dl>
          </div>
        </section>
      ))}
      <div className="row"><button type="button" className="btn small" data-field="fam-add-body" onClick={() => onChange(addBody(def))}><Plus size={12} />Add a body</button></div>

      <details className="small param-formula-help fam-help">
        <summary>Formulas</summary>
        <p>A corner, a depth, an offset and a condition are a number or a formula of the parameters, like W - 0.04 or H / 2.</p>
        <p>Names you can use: <span data-no-i18n>{names.join(', ') || '—'}</span></p>
        <p>Functions: <span data-no-i18n>{FORMULA_FUNCTIONS.join(', ')}</span></p>
        <p>A body is made only when its condition is not 0: a yes/no parameter or a comparison. Decimals with a dot; arguments separated by , or ;.</p>
      </details>
    </div>
  );
}
