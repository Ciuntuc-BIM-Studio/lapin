/**
 * A floor type of beam and block (report #134): its preset, the joist's section and spacing, the infill block, the topping,
 * the span and the bearing, the mesh; the depth derived; beside them the engine's own layout of a sample floor in plan and
 * two bays in section. The user's own catalogue comes in from a CSV as floor types (architecture.importBeamBlockTypes).
 */

import { useEffect, useState } from 'react';
import { FileDown, FileUp } from 'lucide-react';
import { notify } from '../../app/store';
import { pickFile, saveFile } from '../../engine/files';
import { execute, query } from '../../engine/session';
import { LengthField } from '../../shell/cells';
import { lengthText } from '../../engine/unitsStore';
import { MaterialPicker } from '../core/MaterialPicker';
import { beamBlockDepth, beamBlockOf, editBeamBlock, svgPoints, type BeamBlockSpec } from './beamBlock';

interface Preview {
  problem?: string; depth: number; outline: number[]; hole: number[]; joists: number[][]; blocks: { whole: boolean; pts: number[] }[];
  section: { role: string; pts: number[] }[]; counts: { joists: number; blocks: number; cutBlocks: number }; warnings: string[];
}

function Plan({ p }: { p: Preview }) {
  const width = 300, height = 260, pad = 10;
  const k = Math.min((width - 2 * pad) / 5, (height - 2 * pad) / 4.2);
  const X = (x: number) => pad + x * k;
  const Y = (y: number) => height - pad - y * k;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-label="Beam and block plan" data-testid="beam-block-plan">
      <polygon points={svgPoints(p.outline, X, Y)} fill="#eeeeee" stroke="#555" strokeWidth={0.8} />
      {p.blocks.map((b, i) => <polygon key={i} points={svgPoints(b.pts, X, Y)} fill={b.whole ? '#d9825b' : '#f0b89c'} stroke="#7a3a1e" strokeWidth={0.3} />)}
      {p.joists.map((j, i) => <polygon key={i} points={svgPoints(j, X, Y)} fill="#8f8f8f" stroke="#333" strokeWidth={0.4} />)}
      <polygon points={svgPoints(p.hole, X, Y)} fill="#ffffff" stroke="#555" strokeDasharray="3 2" strokeWidth={0.6} />
    </svg>
  );
}

function Section({ p, spacing }: { p: Preview; spacing: number }) {
  const width = 300, height = 120, pad = 10;
  const w = 3 * spacing;
  const k = Math.min((width - 2 * pad) / w, (height - 2 * pad) / p.depth);
  const X = (x: number) => pad + (x + spacing / 2) * k;
  const Y = (y: number) => height - pad - y * k;
  const fill: Record<string, string> = { concrete: '#c9c9c9', block: '#d9825b', joist: '#8f8f8f' };
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-label="Beam and block section" data-testid="beam-block-section">
      {p.section.map((s, i) => <polygon key={i} points={svgPoints(s.pts, X, Y)} fill={fill[s.role] ?? '#ccc'} stroke="#333" strokeWidth={0.6} />)}
    </svg>
  );
}

function Size({ label, value, onChange, title }: { label: string; value: number; onChange: (m: number) => void; title?: string }) {
  return (
    <label className="field-line" title={title}><span className="eyebrow">{label}</span>
      <LengthField metres={value} onCommit={(m) => { if (m != null && m >= 0) onChange(m); }} />
    </label>
  );
}

export function BeamBlockFields({ spec, onChange, onImported }: { spec: BeamBlockSpec; onChange: (s: BeamBlockSpec) => void; onImported?: (names: string[]) => void }) {
  const [presets, setPresets] = useState<BeamBlockSpec[]>([]);
  const [template, setTemplate] = useState('');
  const [preview, setPreview] = useState<Preview | null>(null);
  useEffect(() => {
    void query<{ presets: unknown[]; csvTemplate: string }>('architecture.beamBlockPresets').then((r) => {
      if (!r) return;
      setPresets(r.presets.map(beamBlockOf).filter((x): x is BeamBlockSpec => x != null));
      setTemplate(r.csvTemplate);
    });
  }, []);
  useEffect(() => {
    let live = true;
    void query<Preview>('architecture.beamBlockPreview', { spec }).then((p) => { if (live) setPreview(p); });
    return () => { live = false; };
  }, [spec]);
  const set = (patch: Partial<BeamBlockSpec>) => onChange(editBeamBlock(spec, patch));
  const bring = async () => {
    const file = await pickFile('Beam and block catalogue (CSV)', ['csv', 'txt']);
    if (!file) return;
    const r = await execute('architecture.importBeamBlockTypes', { csv: new TextDecoder().decode(file.bytes) }) as { names?: string[] } | null;
    if (r?.names?.length) { notify('The catalogue was imported: one floor type a row, those of the same name changed, in one step to undo.'); onImported?.(r.names); }
  };
  const template$ = async () => {
    if (!template) return;
    await saveFile(new TextEncoder().encode(`﻿${template}`), 'Beam and block catalogue.csv', 'text/csv');
  };
  return (
    <div className="beam-block" data-field="beam-block">
      <div className="row" style={{ gap: 16, alignItems: 'flex-start', flexWrap: 'wrap' }}>
        <div style={{ minWidth: 260 }}>
          <label className="field-line"><span className="eyebrow">Preset</span>
            <select value={presets.some((p) => p.preset === spec.preset) ? spec.preset : ''} data-testid="beam-block-preset"
              onChange={(e) => { const p = presets.find((x) => x.preset === e.target.value); if (p) onChange({ ...p, direction: spec.direction, angle: spec.angle }); }}>
              <option value="">{spec.preset || 'Own sizes'}</option>
              {presets.map((p) => <option key={p.preset} value={p.preset}>{p.preset}</option>)}
            </select>
          </label>
          <p className="muted small">The presets are indicative: check the maker's tables and change any size.</p>
          <p className="eyebrow">Joist</p>
          <Size label="Width" value={spec.joistWidth} onChange={(v) => set({ joistWidth: v })} title="The joist's foot, across" />
          <Size label="Height" value={spec.joistHeight} onChange={(v) => set({ joistHeight: v })} />
          <Size label="Foot height" value={spec.flangeHeight} onChange={(v) => set({ flangeHeight: v })} title="The foot the blocks rest on" />
          <Size label="Web width" value={spec.webWidth} onChange={(v) => set({ webWidth: v })} />
          <Size label="Spacing" value={spec.spacing} onChange={(v) => set({ spacing: v })} title="Axis to axis: the block's width and the joist's" />
          <label className="field-line"><span className="eyebrow">Material</span><MaterialPicker value={spec.joistMaterial} onChange={(m) => set({ joistMaterial: m })} /></label>
          <p className="eyebrow">Infill block</p>
          <Size label="Height" value={spec.blockHeight} onChange={(v) => set({ blockHeight: v })} />
          <Size label="Length" value={spec.blockLength} onChange={(v) => set({ blockLength: v })} title="Along the joists" />
          <label className="field-line"><span className="eyebrow">Material</span><MaterialPicker value={spec.blockMaterial} onChange={(m) => set({ blockMaterial: m })} /></label>
          <p className="eyebrow">Topping</p>
          <Size label="Thickness" value={spec.topping} onChange={(v) => set({ topping: v })} />
          <label className="field-line"><span className="eyebrow">Material</span><MaterialPicker value={spec.toppingMaterial} onChange={(m) => set({ toppingMaterial: m })} /></label>
          <label className="field-line" title="The mesh in the topping: its name, measured by the floor's area"><span className="eyebrow">Mesh</span>
            <input value={spec.mesh} placeholder="none" onChange={(e) => set({ mesh: e.target.value })} /></label>
          <p className="eyebrow">Span</p>
          <label className="field-line"><span className="eyebrow">Joists</span>
            <select value={spec.direction} data-testid="beam-block-direction" onChange={(e) => set({ direction: e.target.value as BeamBlockSpec['direction'] })}>
              <option value="auto">the short way</option><option value="x">along x</option><option value="y">along y</option><option value="angle">at an angle</option>
            </select>
          </label>
          {spec.direction === 'angle' && (
            <label className="field-line"><span className="eyebrow">Angle (°)</span>
              <input className="num" type="number" value={spec.angle} onChange={(e) => { const v = Number(e.target.value); if (Number.isFinite(v)) set({ angle: v }); }} /></label>
          )}
          <Size label="Bearing" value={spec.bearing} onChange={(v) => set({ bearing: v })} title="How far the joists go into the supports past the floor's edge" />
          <p className="small"><span className="eyebrow">Depth</span> <strong data-testid="beam-block-depth">{lengthText(beamBlockDepth(spec))}</strong> <span className="muted">(block and topping: the core)</span></p>
        </div>
        <div>
          {preview && !preview.problem && (
            <>
              <Plan p={preview} />
              <Section p={preview} spacing={spec.spacing} />
              <p className="muted small" data-testid="beam-block-counts">{`${preview.counts.joists} joists · ${preview.counts.blocks} whole blocks · ${preview.counts.cutBlocks} cut`}</p>
              {preview.warnings.map((w, i) => <p key={i} className="error-text">{w}</p>)}
            </>
          )}
          {preview?.problem && <p className="error-text" role="alert">{preview.problem}</p>}
          <div className="row" style={{ gap: 6 }}>
            <button type="button" className="btn small" title="Floor types from your catalogue: one row a system, sizes in millimetres" onClick={() => void bring()}><FileUp size={13} /> Import catalogue…</button>
            <button type="button" className="btn small" title="A CSV with the columns the import reads, and one row" onClick={() => void template$()}><FileDown size={13} /> CSV template</button>
          </div>
        </div>
      </div>
    </div>
  );
}
