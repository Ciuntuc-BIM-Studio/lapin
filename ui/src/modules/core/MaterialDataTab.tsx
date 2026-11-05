/**
 * The Materials dialog's Data tab (docs/plan-buildup-tags.md): what the material says of itself, written once and read
 * by every type it is in, the build-up tag, the takeoff, the estimate. Its description (several lines), its parameters
 * (a name, a kind, a value; λ, μ and density offered in IFC's standard sets), how the takeoff measures it and its
 * catalogue of thicknesses. Each change is one material.setData, one undo step.
 */

import { useEffect, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { Cell, LengthField } from '../../shell/cells';
import { parseNumber } from '../../shell/cellKeys';
import { formatThicknesses, measureOf, paramKey, parseThicknesses, PARAM_PRESETS, unitOf, type MaterialParam, type Measure, type MeasureBy } from './materialData';

const KINDS = ['text', 'number', 'length', 'area', 'angle', 'bool'] as const;
const MEASURES: { by: MeasureBy | ''; label: string }[] = [
  { by: '', label: 'as its elements are measured' },
  { by: 'area', label: 'area (m²), by thickness' },
  { by: 'volume', label: 'volume (m³)' },
  { by: 'mass', label: 'mass (kg)' },
  { by: 'count', label: 'count (pieces of a board)' },
];

export function MaterialDataTab({ node }: { node: NodeDto }) {
  const id = node.id;
  const params = (Array.isArray(node.props.params) ? node.props.params : []) as unknown as MaterialParam[];
  const measure = measureOf(node.props);
  const description = typeof node.props.description === 'string' ? node.props.description : '';
  const [text, setText] = useState(description);
  useEffect(() => setText(description), [id, description]);
  const [thick, setThick] = useState(formatThicknesses(measure?.thicknesses));
  useEffect(() => setThick(formatThicknesses(measure?.thicknesses)), [id, measure?.thicknesses?.join(',')]);
  const [name, setName] = useState('');
  const [kind, setKind] = useState<string>('text');

  const save = (patch: Record<string, unknown>) => void execute('material.setData', { id, ...patch });
  const setParams = (list: MaterialParam[]) => save({ params: list });
  const setValue = (p: MaterialParam, value: unknown) => setParams(params.map((x) => (paramKey(x) === paramKey(p) ? { ...x, value } : x)));
  const setMeasure = (m: Measure | null) => save({ measure: m });
  const add = (p: MaterialParam) => { if (!params.some((x) => paramKey(x) === paramKey(p))) setParams([...params, p]); };
  const presets = PARAM_PRESETS.filter((x) => !params.some((p) => paramKey(p) === paramKey(x.param)));

  return (
    <div className="material-data" data-testid="material-data">
      <label className="field-line"><span className="eyebrow">Description</span>
        <textarea data-field="mat-description" rows={3} value={text} placeholder="What the specification says of it (a build-up tag writes it in place of the name)"
          onChange={(e) => setText(e.target.value)} onBlur={() => { if (text !== description) save({ description: text.trim() ? text : null }); }} /></label>

      <span className="eyebrow">Parameters</span>
      {params.length > 0 && (
        <table className="layer-edit material-params">
          <thead><tr><th>Name</th><th>Value</th><th /></tr></thead>
          <tbody>
            {params.map((p) => (
              <tr key={paramKey(p)} data-param={p.name}>
                <td title={p.pset ? `${p.pset}.${p.name}` : `${p.kind}`}>{p.pset && <span className="muted small" data-no-i18n>{p.pset} · </span>}<span data-no-i18n>{p.name}</span></td>
                <td>
                  {p.kind === 'length' ? <LengthField metres={typeof p.value === 'number' ? p.value : null} blank onCommit={(m) => setValue(p, m)} />
                    : p.kind === 'bool' ? <input type="checkbox" checked={p.value === true} onChange={(e) => setValue(p, e.target.checked)} />
                    : p.kind === 'text' || p.kind === 'choice' ? <Cell value={String(p.value ?? '')} onCommit={(t) => setValue(p, t)} />
                    : <Cell className="num" value={p.value == null ? '' : String(p.value)} suffix={unitOf(p) || (p.kind === 'area' ? 'm²' : p.kind === 'angle' ? '°' : undefined)}
                        onCommit={(t) => { const n = parseNumber(t); if (n == null) return false; setValue(p, n); }} />}
                </td>
                <td className="row-tools"><button type="button" className="icon mini" title="Remove the parameter" onClick={() => setParams(params.filter((x) => paramKey(x) !== paramKey(p)))}><X size={11} /></button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="row">
        <input value={name} placeholder="New parameter" onChange={(e) => setName(e.target.value)} />
        <select value={kind} title="What kind of value it holds" onChange={(e) => setKind(e.target.value)}>{KINDS.map((k) => <option key={k} value={k}>{k}</option>)}</select>
        <button type="button" className="icon" data-field="mat-param-add" title="Add the parameter" disabled={!name.trim()}
          onClick={() => { add({ name: name.trim(), kind, value: kind === 'bool' ? false : kind === 'text' ? '' : 0 }); setName(''); }}><Plus size={14} /></button>
        {presets.length > 0 && (
          <select data-field="mat-param-preset" value="" title="A parameter a material often has; λ, μ and density go to IFC's own sets" onChange={(e) => {
            const hit = presets.find((x) => x.label === e.target.value);
            if (hit) add(hit.param);
          }}>
            <option value="">Add a usual one…</option>
            {presets.map((x) => <option key={x.label} value={x.label}>{x.label}</option>)}
          </select>
        )}
      </div>

      <div className="row">
        <label className="field-line" title="How Cost → Quantity takeoff (by material) and the estimate measure it; as its elements are measured when not set"><span className="eyebrow">Measured by</span>
          <select data-field="mat-measure-by" value={measure?.by ?? ''} onChange={(e) => {
            const by = e.target.value as MeasureBy | '';
            setMeasure(by ? { ...(measure ?? {}), by, ...(by === 'area' || by === 'count' ? {} : { thicknesses: undefined }) } as Measure : null);
          }}>
            {MEASURES.map((x) => <option key={x.by} value={x.by}>{x.label}</option>)}
          </select></label>
        {measure && (measure.by === 'area' || measure.by === 'count') && (
          <label className="field-line grow" title="The thicknesses its maker makes it in (mm): offered in the types' layer tables; a layer of another thickness is flagged"><span className="eyebrow">Catalogue thicknesses (mm)</span>
            <input data-field="mat-thicknesses" value={thick} placeholder="50, 80, 100, 150, 200" onChange={(e) => setThick(e.target.value)}
              onBlur={() => {
                const list = parseThicknesses(thick);
                if (list == null) { setThick(formatThicknesses(measure.thicknesses)); return; }
                if (list.join(',') !== (measure.thicknesses ?? []).join(',')) setMeasure({ ...measure, thicknesses: list.length ? list : undefined });
              }} /></label>
        )}
        {measure && (
          <label className="field-line" title="The unit it is bought in, for the estimate when its price has none"><span className="eyebrow">Unit</span>
            <input data-field="mat-unit" style={{ width: 64 }} defaultValue={measure.unit ?? ''} key={`${id}u${measure.unit ?? ''}`}
              onBlur={(e) => { const u = e.target.value.trim(); if (u !== (measure.unit ?? '')) setMeasure({ ...measure, unit: u || undefined }); }} /></label>
        )}
      </div>
      {measure?.by === 'count' && (
        <div className="row">
          <span className="small">Board</span>
          <LengthField metres={measure.board?.[0] ?? null} min={0} placeholder="length" title="Board length" onCommit={(v) => setMeasure({ ...measure, board: v != null ? [v, measure.board?.[1] ?? v] : undefined })} />
          <span className="small">×</span>
          <LengthField metres={measure.board?.[1] ?? null} min={0} placeholder="width" title="Board width" onCommit={(v) => setMeasure({ ...measure, board: v != null ? [measure.board?.[0] ?? v, v] : undefined })} />
        </div>
      )}
      {measure?.by === 'mass' && typeof node.props.measure === 'object' && !params.some((p) => /density/i.test(p.name)) && (
        <p className="muted small">Mass needs a density: add Density (kg/m³).</p>
      )}
      <p className="muted small">Written once here, read by every type the material is in: the build-up tag, the takeoff, the estimate. IFC carries the parameters, λ, μ and density in IFC's own sets.</p>
    </div>
  );
}
