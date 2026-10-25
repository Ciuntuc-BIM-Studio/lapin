/**
 * The stair configurator: the project's stair types, one being edited on
 * three tabs (the shape, the rules, the construction with the finish and the
 * railing), a preview in plan and in section worked out by the engine for a
 * height to try, and the checks against the rules. Lengths in mm.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, engine, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';
import { ChecksList, type StairNumbers } from './StairSection';
import { DrawingLayer } from '../../viewport/DrawingLayer';
import type { DrawingDto } from '../../viewport/drawing';

interface Rules { set: string; riserMin: number; riserMax: number; riserTarget: number; treadMin: number; treadMax: number; treadTarget: number; stepMin: number; stepMax: number; minWidth: number; maxRisers: number; headroom: number; maxSlope: number }
interface Spec {
  kind: string; name: string; shape: string; turn: string; winders: number; width: number; gap: number; landingLength: number;
  rules: Rules;
  construction: { kind: string; waist: number; landingThickness: number; concrete: string; landingBeamWidth?: number | null; landingBeamDepth?: number | null };
  finish: { treadThickness: number; treadMaterial: string; nosing: number };
  railing: { sides: string; height: number; barSize: number; postSize: number; postSpacing: number };
  slope: number;
}
interface Defaults { spec: Spec; rules: Record<string, Rules>; shapes: string[]; turns: string[]; sides: string[] }

const SHAPES: Record<string, string> = { straight: 'Straight', 'straight-landing': 'Straight with landing', l: 'L shape', u: 'U shape' };

function Mm({ label, value, onChange, step = 5 }: { label: string; value: number; onChange: (m: number) => void; step?: number }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <input type="number" step={step} value={Math.round(value * 1000)} onChange={(e) => { const n = Number(e.target.value); if (Number.isFinite(n)) onChange(n / 1000); }} /><span className="muted small"> mm</span>
    </label>
  );
}

/** A view's drawing fitted into a box, as the generator made it: pens with their widths, dashes and hatches at the view's scale. */
function Drawn({ drawing, width, label }: { drawing: DrawingDto | null; width: number; label: string }) {
  const pad = 12;
  const b = drawing?.bounds ?? [0, 0, 1, 1];
  const bw = Math.max(b[2] - b[0], 0.5), bh = Math.max(b[3] - b[1], 0.5);
  const k = (width - 2 * pad) / bw;
  const height = Math.max(120, Math.round(bh * k + 2 * pad));
  const toScreen = (p: { x: number; y: number }) => ({ x: pad + (p.x - b[0]) * k, y: height - pad - (p.y - b[1]) * k });
  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} aria-label={label}>
      {drawing && drawing.pens.length > 0 ? <DrawingLayer drawing={drawing} toScreen={toScreen} pxPerMetre={k} /> : <text x={width / 2} y={height / 2} textAnchor="middle" fontSize={11} fill="#888">{label}…</text>}
    </svg>
  );
}

export function StairTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcStairType');
  const defaults = info?.catalogs['architecture.stairDefaults'] as unknown as Defaults | undefined;
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  const [spec, setSpec] = useState<Spec | null>(null);
  const [tab, setTab] = useState<'shape' | 'rules' | 'construction'>('shape');
  const [height, setHeight] = useState(3.0);
  const [preview, setPreview] = useState<StairNumbers | null>(null);
  const [drawn, setDrawn] = useState<{ plan: DrawingDto; section: DrawingDto } | null>(null);
  const modelRef = useRef(model);
  modelRef.current = model;

  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as Spec | undefined) ?? defaults?.spec ?? null;
    setSpec(s ? structuredClone(s) : null);
  }, [editing, defaults]);

  useEffect(() => {
    if (!spec) return;
    let live = true;
    void engine.call<StairNumbers>('doc.query', { name: 'architecture.stairPreview', args: { spec, height } }).then((p) => { if (live) setPreview(p); }).catch(() => undefined);
    // The drawings take a moment (a session of their own): asked for once the spec has settled.
    const t = setTimeout(() => {
      void engine.call<{ plan: DrawingDto; section: DrawingDto }>('doc.query', { name: 'architecture.stairDrawing', args: { spec, height } }).then((d) => { if (live) setDrawn(d); }).catch(() => undefined);
    }, 250);
    return () => { live = false; clearTimeout(t); };
  }, [spec, height]);

  if (!spec || !defaults) return null;
  const set = (patch: Partial<Spec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const fresh = () => { setEditing(null); setSpec(structuredClone(defaults.spec)); };
  const save = async () => {
    if (editing) await execute('architecture.updateStairType', { id: editing, spec });
    else {
      const r = await execute('architecture.createStairType', { spec }) as { id?: string } | null;
      if (r?.id) { setEditing(r.id); useUi.setState({ stairTypeId: r.id }); }
    }
  };

  const W = 480;

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Stair types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Stair types</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}>
                <button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>
                  {t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span>
                </button>
              </li>
            ))}
            <li><button type="button" className="tree-item" onClick={fresh}><Plus size={12} /> New stair type</button></li>
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
              <div className="tabs-row small stair-tabs">
                {(['shape', 'rules', 'construction'] as const).map((t) => (
                  <button type="button" key={t} className={`tab${tab === t ? ' on' : ''}`} onClick={() => setTab(t)}>{t === 'shape' ? 'Shape' : t === 'rules' ? 'Rules' : 'Construction'}</button>
                ))}
              </div>
              {tab === 'shape' && <>
                <label className="field-line"><span className="eyebrow">Shape</span>
                  <select value={spec.shape} onChange={(e) => set({ shape: e.target.value })}>{defaults.shapes.map((s) => <option key={s} value={s}>{SHAPES[s] ?? s}</option>)}</select>
                </label>
                {(spec.shape === 'l' || spec.shape === 'u') && <>
                  <label className="field-line"><span className="eyebrow">Turn made as</span>
                    <select value={spec.turn} onChange={(e) => set({ turn: e.target.value })}>{defaults.turns.map((t) => <option key={t} value={t}>{t}</option>)}</select>
                  </label>
                  {spec.turn === 'winders' && <label className="field-line"><span className="eyebrow">Winders per quarter turn</span><input type="number" min={1} max={6} value={spec.winders} onChange={(e) => set({ winders: Number(e.target.value) || 3 })} /></label>}
                </>}
                {spec.shape === 'u' && <Mm label="Gap between flights" value={spec.gap} onChange={(v) => set({ gap: Math.max(0, v) })} />}
                <Mm label="Width" value={spec.width} onChange={(v) => set({ width: Math.max(0.5, v) })} />
                {spec.shape !== 'straight' && <Mm label="Landing length" value={spec.landingLength} onChange={(v) => set({ landingLength: Math.max(0, v) })} />}
                {spec.shape !== 'straight' && spec.landingLength === 0 && <p className="muted small">Landing length 0: as the width.</p>}
              </>}
              {tab === 'rules' && <>
                <label className="field-line"><span className="eyebrow">Rule set</span>
                  <select value={spec.rules.set} onChange={(e) => set({ rules: structuredClone(defaults.rules[e.target.value] ?? spec.rules) })}>
                    <option value="residential">residential</option><option value="public">public</option>
                  </select>
                </label>
                <p className="eyebrow">Riser · min, target, max</p>
                <div className="row">
                  <Mm label="min" value={spec.rules.riserMin} onChange={(v) => set({ rules: { ...spec.rules, riserMin: v } })} />
                  <Mm label="target" value={spec.rules.riserTarget} onChange={(v) => set({ rules: { ...spec.rules, riserTarget: v } })} />
                  <Mm label="max" value={spec.rules.riserMax} onChange={(v) => set({ rules: { ...spec.rules, riserMax: v } })} />
                </div>
                <p className="eyebrow">Tread · min, target, max</p>
                <div className="row">
                  <Mm label="min" value={spec.rules.treadMin} onChange={(v) => set({ rules: { ...spec.rules, treadMin: v } })} />
                  <Mm label="target" value={spec.rules.treadTarget} onChange={(v) => set({ rules: { ...spec.rules, treadTarget: v } })} />
                  <Mm label="max" value={spec.rules.treadMax} onChange={(v) => set({ rules: { ...spec.rules, treadMax: v } })} />
                </div>
                <p className="eyebrow">Step rule 2h + b</p>
                <div className="row">
                  <Mm label="min" value={spec.rules.stepMin} onChange={(v) => set({ rules: { ...spec.rules, stepMin: v } })} />
                  <Mm label="max" value={spec.rules.stepMax} onChange={(v) => set({ rules: { ...spec.rules, stepMax: v } })} />
                </div>
                <Mm label="Minimum width" value={spec.rules.minWidth} onChange={(v) => set({ rules: { ...spec.rules, minWidth: v } })} />
                <label className="field-line"><span className="eyebrow">Risers in a flight</span><input type="number" min={3} max={30} value={spec.rules.maxRisers} onChange={(e) => set({ rules: { ...spec.rules, maxRisers: Number(e.target.value) || 18 } })} /><span className="muted small"> at most</span></label>
                <Mm label="Headroom" value={spec.rules.headroom} onChange={(v) => set({ rules: { ...spec.rules, headroom: v } })} />
              </>}
              {tab === 'construction' && <>
                <p className="eyebrow">Reinforced concrete, monolithic</p>
                <Mm label="Waist" value={spec.construction.waist} onChange={(v) => set({ construction: { ...spec.construction, waist: Math.max(0.05, v) } })} />
                <Mm label="Landing slab" value={spec.construction.landingThickness} onChange={(v) => set({ construction: { ...spec.construction, landingThickness: Math.max(0.05, v) } })} />
                <label className="field-line"><span className="eyebrow">Concrete</span><input value={spec.construction.concrete} onChange={(e) => set({ construction: { ...spec.construction, concrete: e.target.value } })} /></label>
                <p className="eyebrow">Tread finish</p>
                <Mm label="Thickness" value={spec.finish.treadThickness} onChange={(v) => set({ finish: { ...spec.finish, treadThickness: Math.max(0, v) } })} />
                <label className="field-line"><span className="eyebrow">Material</span><input value={spec.finish.treadMaterial} onChange={(e) => set({ finish: { ...spec.finish, treadMaterial: e.target.value } })} /></label>
                <Mm label="Nosing" value={spec.finish.nosing} onChange={(v) => set({ finish: { ...spec.finish, nosing: Math.max(0, v) } })} />
                <p className="eyebrow">Railing</p>
                <label className="field-line"><span className="eyebrow">Sides</span>
                  <select value={spec.railing.sides} onChange={(e) => set({ railing: { ...spec.railing, sides: e.target.value } })}>{defaults.sides.map((s) => <option key={s} value={s}>{s}</option>)}</select>
                </label>
                <Mm label="Height" value={spec.railing.height} onChange={(v) => set({ railing: { ...spec.railing, height: Math.max(0.3, v) } })} />
                <Mm label="Bar" value={spec.railing.barSize} onChange={(v) => set({ railing: { ...spec.railing, barSize: Math.max(0.01, v) } })} />
                <Mm label="Post" value={spec.railing.postSize} onChange={(v) => set({ railing: { ...spec.railing, postSize: Math.max(0.01, v) } })} />
                <Mm label="Post spacing" value={spec.railing.postSpacing} onChange={(v) => set({ railing: { ...spec.railing, postSpacing: Math.max(0.3, v) } })} step={50} />
              </>}
            </div>
            <div className="joinery-preview stair-preview">
              <label className="field-line"><span className="eyebrow">Preview at a height of</span><input type="number" step={10} value={Math.round(height * 1000)} onChange={(e) => { const n = Number(e.target.value); if (n > 300) setHeight(n / 1000); }} /><span className="muted small"> mm</span></label>
              <Drawn drawing={drawn?.plan ?? null} width={W} label="Plan" />
              <Drawn drawing={drawn?.section ?? null} width={W} label="Section along the first flight" />
              {preview && (
                <p className="small caption">
                  <b>{preview.risers}</b> risers · <b>{Math.round(preview.riserHeight * 1000)}</b> / <b>{Math.round(preview.tread * 1000)}</b> mm · 2h+b {Math.round(preview.step * 1000)} mm
                  · {preview.flights.join(' + ')} {preview.winders ? `+ ${preview.winders} winders` : ''} · run {Math.round(preview.run * 1000)} mm · {preview.predefinedType}
                </p>
              )}
              {preview && <ChecksList checks={preview.checks} />}
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while stairs use it)" onClick={() => void execute('element.delete', { id: editing }).then((r) => { if (r !== null) setEditing(null); })}>Delete</button>}
              {editing && <button type="button" className="btn" onClick={() => useUi.setState({ stairTypeId: editing })}>Place with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
