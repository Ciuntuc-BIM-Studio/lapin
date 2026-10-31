/**
 * A beam's or a column's stirrups, on its type, in the Structure types dialog: on or off, their diameter and cover,
 * one step throughout or three zones each with its share of the length and its step. Saved as they are changed.
 */

import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { useRebarCatalog } from './RebarSection';

interface Zone { share: number; step: number }
interface Row { count: number; diameter: number; mark?: number }
interface Bars { enabled: boolean; top: Row; bottom: Row; side: Row; perFace: Row; extend: number }
const BARS: Bars = { enabled: false, top: { count: 2, diameter: 0.012 }, bottom: { count: 3, diameter: 0.016 }, side: { count: 0, diameter: 0.01 }, perFace: { count: 3, diameter: 0.016 }, extend: 0 };

/** A row's count and diameter. */
function RowFields({ label, row, diameters, min = 0, onChange }: { label: string; row: Row; diameters: number[]; min?: number; onChange: (r: Row) => void }) {
  return (
    <div className="row small">
      <span className="eyebrow">{label}</span>
      <input className="num" type="number" style={{ width: 48 }} min={min} max={40} value={row.count} onChange={(e) => onChange({ ...row, count: Math.max(min, Number(e.target.value) || 0) })} />
      <span>×</span>
      <select value={row.diameter} onChange={(e) => onChange({ ...row, diameter: Number(e.target.value) })}>{diameters.map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000)}</option>)}</select>
      {row.mark ? <span className="muted small">mark {row.mark}</span> : null}
    </div>
  );
}

/** A beam's or a column's longitudinal bars, on its type: the rows, their extension past the ends. */
export function BarsFields({ type, kind }: { type: NodeDto; kind: string }) {
  const catalog = useRebarCatalog();
  const section = type.props.section as { kind?: string } | undefined;
  const b: Bars = { ...BARS, ...((type.props.bars as Partial<Bars> | undefined) ?? {}) };
  const set = (patch: Partial<Bars>) => void execute('rebar.setTypeBars', { id: type.id, ...patch });
  if (!catalog || (kind !== 'beam' && kind !== 'column') || (section?.kind && section.kind !== 'rectangle')) return null;
  return (
    <div className="layers" data-field="type-bars">
      <label className="field-line check"><input type="checkbox" checked={b.enabled} onChange={(e) => set({ enabled: e.target.checked })} /><span>Longitudinal bars on this type</span></label>
      {b.enabled && <>
        {kind === 'beam' ? <>
          <RowFields label="Top row" row={b.top} diameters={catalog.diameters} onChange={(r) => set({ top: r })} />
          <RowFields label="Bottom row" row={b.bottom} diameters={catalog.diameters} onChange={(r) => set({ bottom: r })} />
          <RowFields label="Each side" row={b.side} diameters={catalog.diameters} onChange={(r) => set({ side: r })} />
        </> : <RowFields label="Each face" row={b.perFace} diameters={catalog.diameters} min={2} onChange={(r) => set({ perFace: r })} />}
        <label className="field-line"><span className="eyebrow">Past each end</span><input className="num" type="number" step={10} min={0} max={2000} value={Math.round(b.extend * 1000)} onChange={(e) => { const v = Number(e.target.value); if (v >= 0 && v <= 2000) set({ extend: v / 1000 }); }} /><span className="muted small"> mm</span></label>
        <p className="muted small">Laid inside the stirrups, the corners first; dots in a cross-section, lines along; each row a mark in the bar schedule.</p>
      </>}
    </div>
  );
}
interface Spec { enabled: boolean; diameter: number; cover: number; mode: 'uniform' | 'zones'; step: number; zones: Zone[]; mark: number }

const DEFAULT: Spec = { enabled: false, diameter: 0.008, cover: 0.025, mode: 'uniform', step: 0.15, zones: [{ share: 1 / 3, step: 0.1 }, { share: 1 / 3, step: 0.2 }, { share: 1 / 3, step: 0.1 }], mark: 0 };

export function StirrupsFields({ type, kind }: { type: NodeDto; kind: string }) {
  const catalog = useRebarCatalog();
  const section = type.props.section as { kind?: string } | undefined;
  const s: Spec = { ...DEFAULT, ...((type.props.stirrups as Partial<Spec> | undefined) ?? {}) };
  const set = (patch: Partial<Spec>) => void execute('rebar.setTypeStirrups', { id: type.id, ...patch });
  if (!catalog || (kind !== 'beam' && kind !== 'column')) return null;
  if (section?.kind && section.kind !== 'rectangle') return <p className="muted small">Stirrups are configured on rectangular sections.</p>;
  const zones = s.zones.length === 3 ? s.zones : DEFAULT.zones;
  const setZone = (i: number, patch: Partial<Zone>) => set({ zones: zones.map((z, j) => (j === i ? { ...z, ...patch } : z)) });
  return (
    <div className="layers" data-field="type-stirrups">
      <label className="field-line check"><input type="checkbox" checked={s.enabled} onChange={(e) => set({ enabled: e.target.checked })} /><span>Stirrups on this type</span></label>
      {s.enabled && <>
        <label className="field-line"><span className="eyebrow">Diameter</span>
          <select value={s.diameter} onChange={(e) => set({ diameter: Number(e.target.value) })}>
            {catalog.diameters.filter((x) => x <= 0.02).map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000)}</option>)}
          </select>
        </label>
        <label className="field-line"><span className="eyebrow">Cover</span><input className="num" type="number" step={5} min={0} max={200} value={Math.round(s.cover * 1000)} onChange={(e) => { const v = Number(e.target.value); if (v >= 0 && v <= 200) set({ cover: v / 1000 }); }} /><span className="muted small"> mm</span></label>
        <label className="field-line"><span className="eyebrow">Laid</span>
          <select value={s.mode} onChange={(e) => set({ mode: e.target.value as Spec['mode'] })}><option value="uniform">One step throughout</option><option value="zones">Three zones</option></select>
        </label>
        {s.mode === 'uniform'
          ? <label className="field-line"><span className="eyebrow">Step</span><input className="num" type="number" step={10} min={10} value={Math.round(s.step * 1000)} onChange={(e) => { const v = Number(e.target.value); if (v >= 10) set({ step: v / 1000 }); }} /><span className="muted small"> mm</span></label>
          : zones.map((z, i) => (
            <div key={i} className="row small">
              <span className="eyebrow">{i === 0 ? 'Start' : i === 1 ? 'Middle' : 'End'}</span>
              <input className="num" type="number" style={{ width: 48 }} min={0} max={100} value={Math.round(z.share * 100)} onChange={(e) => setZone(i, { share: Math.max(0, Number(e.target.value)) / 100 })} /><span className="muted small">% of the length at</span>
              <input className="num" type="number" style={{ width: 56 }} step={10} min={10} value={Math.round(z.step * 1000)} onChange={(e) => { const v = Number(e.target.value); if (v >= 10) setZone(i, { step: v / 1000 }); }} /><span className="muted small">mm</span>
            </div>
          ))}
        <label className="field-line"><span className="eyebrow">Mark</span><input className="num" type="number" min={1} value={s.mark || ''} onChange={(e) => { const v = Number(e.target.value); if (v >= 1) set({ mark: v }); }} /></label>
        <p className="muted small">Drawn in sections as they lie to the axis, in structural plans and elevations, and counted in the bar schedule. The longitudinal bars are drawn by hand (Rebar 2D).</p>
      </>}
    </div>
  );
}
