/** A rebar set in the inspector (decision 730): its layer, direction, diameter, how it is laid, cover, legs, lapping, what a plan draws, its mark and partition, and what it comes to. */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { lengthText } from '../../engine/unitsStore';
import { PartitionInput } from './partitions';
import { LabelBlock, MarkPrefixField, useRebarCatalog } from './RebarSection';
import { diameterChoices, useRebarRules } from './rebarRules';
import { directionChoice } from './setOptions';

interface SetDescribed {
  layers: string[]; wall: boolean; mark: string; cover: number; bars: number; pieces: number; step: string;
  lengths: { shape: string; length: number; count: number }[]; total: number; mass: number; label: string; error?: string;
}

function useSetDescribed(id: string): SetDescribed | null {
  const nodes = useSession((s) => s.nodes);
  const [d, setD] = useState<SetDescribed | null>(null);
  useEffect(() => { let live = true; void query<SetDescribed>('rebar.describeSet', { id }).then((r) => { if (live) setD(r ?? null); }); return () => { live = false; }; }, [id, nodes]);
  return d;
}

export function RebarSetSection({ node }: { node: NodeDto }) {
  const catalog = useRebarCatalog();
  const rules = useRebarRules();
  const d = useSetDescribed(node.id);
  const p = node.props;
  const set = (args: Record<string, unknown>) => void execute('rebar.updateSet', { id: node.id, ...args });
  const wall = d?.wall ?? false;
  const angle = typeof p.angle === 'number' ? p.angle : 0;
  const dir = directionChoice(angle, wall);
  const diameter = typeof p.diameter === 'number' ? p.diameter : 0.012;
  const mode = p.mode === 'count' ? 'count' : 'spacing';
  const cover = typeof p.cover === 'number' ? p.cover : null;
  const [mark, setMark] = useState(typeof p.mark === 'string' ? p.mark : '');
  useEffect(() => setMark(typeof p.mark === 'string' ? p.mark : ''), [p.mark]);
  if (!catalog) return null;
  const layers = d?.layers ?? (typeof p.layer === 'string' ? [p.layer] : []);
  return (
    <div className="layers" data-field="rebar-set">
      <label className="field-line"><span className="eyebrow">Layer</span>
        <select value={String(p.layer ?? '')} disabled={!node.editable} data-field="rebar-set-layer" onChange={(e) => set({ layer: e.target.value })}>
          {(layers.includes(String(p.layer)) ? layers : [...layers, String(p.layer ?? '')]).filter(Boolean).map((l) => <option key={l} value={l} data-no-i18n>{l}</option>)}
        </select>
      </label>
      <label className="field-line"><span className="eyebrow">Direction</span>
        <select value={dir} data-field="rebar-set-direction" onChange={(e) => {
          const v = e.target.value;
          if (v === 'x' || v === 'horizontal') set({ angle: 0 }); else if (v === 'y' || v === 'vertical') set({ angle: 90 }); else set({ angle: angle || 30 });
        }}>
          {wall ? <><option value="horizontal">Horizontal</option><option value="vertical">Vertical</option></>
            : <><option value="x" data-no-i18n>x</option><option value="y" data-no-i18n>y</option><option value="angle">At an angle</option></>}
        </select>
        {dir === 'angle' && <NumberField style={{ width: 56 }} value={angle} min={-180} max={180} suffix="°" onCommit={(n) => { if (n != null) set({ angle: n }); }} />}
      </label>
      <label className="field-line"><span className="eyebrow">Diameter</span>
        <select value={diameter} onChange={(e) => set({ diameter: Number(e.target.value) })}>
          {diameterChoices(rules, catalog.diameters, diameter).map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000 * 10) / 10}</option>)}
        </select>
      </label>
      <label className="field-line"><span className="eyebrow">Laid by</span>
        <select value={mode} onChange={(e) => set({ mode: e.target.value })}><option value="spacing">Spacing</option><option value="count">Count</option></select>
        {mode === 'spacing'
          ? <LengthField style={{ width: 64 }} metres={typeof p.step === 'number' ? p.step : 0.15} min={0.03} max={2} onCommit={(m) => { if (m != null) set({ step: m }); }} />
          : <NumberField style={{ width: 56 }} value={typeof p.count === 'number' ? p.count : 5} min={1} max={500} integer onCommit={(n) => { if (n != null) set({ count: n }); }} />}
      </label>
      <div className="row small" data-field="rebar-set-cover">
        <span className="eyebrow">Cover</span>
        <LengthField style={{ width: 64 }} metres={cover} min={0} max={0.2} blank placeholder={d ? lengthText(d.cover) : ''} title="Empty: the cover of the settings" onCommit={(m) => set({ cover: m })} />
        {cover == null && <span className="muted small">from the settings</span>}
      </div>
      <label className="field-line"><span className="eyebrow">Legs</span>
        <LengthField style={{ width: 64 }} metres={typeof p.legs === 'number' ? p.legs : 0} min={0} max={2} title="Bent ends turned into the host, making a U bar; 0: straight bars" onCommit={(m) => { if (m != null) set({ legs: m }); }} />
      </label>
      <label className="check small"><input type="checkbox" checked={p.splice !== false} onChange={(e) => set({ splice: e.target.checked })} /> Lapped at the stock length</label>
      <label className="field-line"><span className="eyebrow">Shown</span>
        <select value={String(p.show ?? 'one')} title="How many bars a plan draws" onChange={(e) => set({ show: e.target.value })}>
          <option value="all">All bars</option><option value="ends">The ends</option><option value="one">One bar</option>
        </select>
      </label>
      <label className="field-line"><span className="eyebrow">Mark</span>
        <input className="cell" type="text" value={mark} data-no-i18n style={{ width: 80 }} placeholder={d?.mark ?? ''} onChange={(e) => setMark(e.target.value)}
          onBlur={() => { if (mark.trim() !== (typeof p.mark === 'string' ? p.mark : '')) set({ mark: mark.trim() }); }}
          onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); e.stopPropagation(); }} />
      </label>
      <MarkPrefixField node={node} command="rebar.updateSet" />
      <label className="field-line"><span className="eyebrow">Partition</span>
        <PartitionInput value={typeof p.partition === 'string' ? p.partition : ''} onCommit={(v) => set({ partition: v })} testId="rebar-set-partition" />
      </label>
      {d?.error && <p className="muted small" data-no-i18n>{d.error}</p>}
      {d && (
        <dl className="fields" data-field="rebar-set-describe">
          <dt>Label</dt><dd><span data-no-i18n>{d.label}</span></dd>
          <dt>Bars</dt><dd><span className="num">{d.bars}</span></dd>
          <dt>Pieces</dt><dd><span className="num">{d.pieces}</span></dd>
          <dt>Spacing</dt><dd><span className="num" data-no-i18n>{d.step}</span></dd>
          <dt>Lengths</dt><dd>{d.lengths.map((l, i) => <div key={i} className="num" data-no-i18n>{l.count} × {Math.round(l.length * 1000)} mm</div>)}</dd>
          <dt>Total length</dt><dd><span className="num">{d.total.toFixed(2)}</span> <span className="muted">m</span></dd>
          <dt>Mass</dt><dd><span className="num">{d.mass.toFixed(2)}</span> <span className="muted">kg</span></dd>
        </dl>
      )}
      <LabelBlock node={node} kind="sets" command="rebar.updateSet" />
    </div>
  );
}
