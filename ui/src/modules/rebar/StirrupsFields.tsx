/**
 * A beam's or a column's stirrups, on its type, in the Structure types dialog: on or off, their diameter and cover,
 * one step throughout or three zones each with its share of the length and its step. Saved as they are changed.
 */

import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { useRebarCatalog } from './RebarSection';
import { diameterChoices, useRebarRules } from './rebarRules';
import { LabelStyleFields, useRebarSettings, type LabelStylePatch } from './LabelStyleFields';
import { PartitionInput } from './partitions';

interface Zone { share: number; step: number }
interface Spec { enabled: boolean; diameter: number; cover: number; mode: 'uniform' | 'zones'; step: number; zones: Zone[]; mark: number; pattern?: 'single' | 'diamond' | 'cross'; label?: LabelStylePatch }
interface ColumnBars { enabled: boolean; perFace: number; diameter: number; lap: number; mark: number; top: 'auto' | 'starter' | 'hooks'; label?: LabelStylePatch }
const COLUMN_BARS: ColumnBars = { enabled: false, perFace: 3, diameter: 0.016, lap: 1.0, mark: 0, top: 'auto' };

const DEFAULT: Spec = { enabled: false, diameter: 0.008, cover: 0.025, mode: 'uniform', step: 0.15, zones: [{ share: 1 / 3, step: 0.1 }, { share: 1 / 3, step: 0.2 }, { share: 1 / 3, step: 0.1 }], mark: 0 };
export function StirrupsFields({ type, kind }: { type: NodeDto; kind: string }) {
  const catalog = useRebarCatalog();
  const rules = useRebarRules();
  const settings = useRebarSettings();
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
            {diameterChoices(rules, catalog.diameters, s.diameter).filter((x) => x <= 0.02 || x === s.diameter).map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000 * 10) / 10}</option>)}
          </select>
        </label>
        <label className="field-line"><span className="eyebrow">Cover</span><LengthField metres={s.cover} min={0} max={0.2} onCommit={(m) => { if (m != null) set({ cover: m }); }} /></label>
        <label className="field-line"><span className="eyebrow">Laid</span>
          <select value={s.mode} onChange={(e) => set({ mode: e.target.value as Spec['mode'] })}><option value="uniform">One step throughout</option><option value="zones">Three zones</option></select>
        </label>
        {s.mode === 'uniform'
          ? <label className="field-line"><span className="eyebrow">Step</span><LengthField metres={s.step} min={0.01} onCommit={(m) => { if (m != null) set({ step: m }); }} /></label>
          : zones.map((z, i) => (
            <div key={i} className="row small">
              <span className="eyebrow">{i === 0 ? 'Start' : i === 1 ? 'Middle' : 'End'}</span>
              <NumberField style={{ width: 48 }} value={Math.round(z.share * 100)} min={0} max={100} onCommit={(n) => { if (n != null) setZone(i, { share: n / 100 }); }} /><span className="muted small">% of the length at</span>
              <LengthField style={{ width: 64 }} metres={z.step} min={0.01} onCommit={(m) => { if (m != null) setZone(i, { step: m }); }} />
            </div>
          ))}
        {kind === 'column' && <label className="field-line"><span className="eyebrow">Arrangement</span>
          <select value={s.pattern ?? 'single'} onChange={(e) => set({ pattern: e.target.value as Spec['pattern'] })}>
            <option value="single">Round the perimeter only</option><option value="diamond">With a diamond inside</option><option value="cross">With two inside, one each way</option>
          </select>
        </label>}
        <label className="field-line"><span className="eyebrow">Mark</span><NumberField value={s.mark || null} min={1} integer onCommit={(n) => { if (n != null) set({ mark: n }); }} /></label>
        <label className="field-line"><span className="eyebrow">Partition</span><PartitionInput value={String(type.props.stirrupsPartition ?? '')} testId="type-stirrups-partition" onCommit={(partition) => void execute('rebar.setTypeStirrups', { id: type.id, partition })} /></label>
        <p className="muted small">Drawn in sections as they lie to the axis, in structural plans and elevations, and counted in the bar schedule. The longitudinal bars are drawn by hand (Rebar 2D).</p>
        <p className="eyebrow">Label</p>
        <LabelStyleFields style={s.label} base={settings?.labels.stirrups} onChange={(label) => set({ label })} onReset={() => void execute('rebar.setTypeStirrups', { id: type.id, label: null })} testId="type-stirrups-label" withLine />
      </>}
      {kind === 'column' && <ColumnBarsFields type={type} />}
    </div>
  );
}

/** A column type's longitudinal bars: so many on each face, their diameter, how they end at the top (decision 232), the starter bars past it. */
function ColumnBarsFields({ type }: { type: NodeDto }) {
  const catalog = useRebarCatalog();
  const rules = useRebarRules();
  const settings = useRebarSettings();
  const b: ColumnBars = { ...COLUMN_BARS, ...((type.props.bars as Partial<ColumnBars> | undefined) ?? {}) };
  const set = (patch: Partial<ColumnBars>) => void execute('rebar.setColumnBars', { id: type.id, ...patch });
  if (!catalog) return null;
  return (
    <div className="layers" data-field="column-bars">
      <label className="field-line check"><input type="checkbox" checked={b.enabled} onChange={(e) => set({ enabled: e.target.checked })} /><span>Longitudinal bars on this type</span></label>
      {b.enabled && <>
        <div className="row small">
          <span className="eyebrow">Each face</span>
          <NumberField style={{ width: 44 }} value={b.perFace} min={2} max={20} integer onCommit={(n) => { if (n != null) set({ perFace: n }); }} />
          <span>×</span>
          <select value={b.diameter} onChange={(e) => set({ diameter: Number(e.target.value) })}>{diameterChoices(rules, catalog.diameters, b.diameter).map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000 * 10) / 10}</option>)}</select>
          {b.mark ? <span className="muted small">mark {b.mark}</span> : null}
        </div>
        <label className="field-line"><span className="eyebrow">At the top</span>
          <select value={b.top} data-field="column-bars-top" onChange={(e) => set({ top: e.target.value as ColumnBars['top'] })}>
            <option value="auto">By the column above</option><option value="starter">Starter bars past the top</option><option value="hooks">90° hooks inside the top</option>
          </select>
        </label>
        <label className="field-line"><span className="eyebrow">Partition</span><PartitionInput value={String(type.props.columnBarsPartition ?? '')} testId="column-bars-partition" onCommit={(partition) => void execute('rebar.setColumnBars', { id: type.id, partition })} /></label>
        {b.top !== 'hooks' && <label className="field-line"><span className="eyebrow">Starter bars past the top</span><LengthField metres={b.lap} min={0} max={5} onCommit={(m) => { if (m != null) set({ lap: m }); }} /></label>}
        <p className="muted small">The corners shared, the rest of each face spread evenly, inside the stirrups; from the base to the top and on by the lap, storey by storey. By the column above: where none stands on a column (the top storey), its bars end under the top with a 90° hook inwards.</p>
        <p className="eyebrow">Label</p>
        <LabelStyleFields style={b.label} base={settings?.labels.columnBars} onChange={(label) => set({ label })} onReset={() => void execute('rebar.setColumnBars', { id: type.id, label: null })} testId="column-bars-label" />
      </>}
    </div>
  );
}
