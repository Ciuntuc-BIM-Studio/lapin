/**
 * The stair configurator: the project's stair types, one being edited on
 * three tabs (the shape, the rules, the construction with the finish and the
 * railing), a preview in plan and in section worked out by the engine for a
 * height to try, and the checks against the rules. Lengths in the project's unit.
 * Ramps are made here too (decision 252): a type of kind "ramp" climbs by its
 * slope, in flights no longer than its longest run, with landings between.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { ofClass, relatedOf } from '../../engine/model';
import { formatLength, lengthText } from '../../engine/unitsStore';
import { LengthField, NumberField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';
import { PendingDialog } from '../core/PendingDialog';
import { ChecksList, percent, type StairNumbers } from './StairSection';
import { DrawingLayer } from '../../viewport/DrawingLayer';
import type { DrawingDto } from '../../viewport/drawing';
import { deleteType } from '../core/deleteType';

interface Rules { set: string; riserMin: number; riserMax: number; riserTarget: number; treadMin: number; treadMax: number; treadTarget: number; stepMin: number; stepMax: number; minWidth: number; maxRisers: number; headroom: number; maxSlope: number; minRisers?: number }
interface Spec {
  kind: string; name: string; shape: string; turn: string; winders: number; width: number; gap: number; landingLength: number;
  /** A U turned by winders (report #109): the second corner's winders (null: as the first's) and the straight treads between the corners. */
  windersEnd?: number | null; between?: number;
  rules: Rules;
  construction: { kind: string; waist: number; landingThickness: number; concrete: string; landingBeamWidth?: number | null; landingBeamDepth?: number | null };
  finish: { treadThickness: number; treadMaterial: string; nosing: number };
  railing: { sides: string; height: number; barSize: number; postSize: number; postSpacing: number; style?: string; profile?: string; balusterSpacing?: number; inset?: number; extension?: number; color?: string | null };
  slope: number;
  /** A rise of its own (entrance steps); null: up to the storey above. Climbed from below the storey's level up to it, or from the level up. */
  rise?: number | null; riseFrom?: string;
  /** A ramp's longest flight before a landing (0: no limit). */
  maxRun?: number;
  /** A spiral's or curved stair's inner radius, the degrees it turns through (null: as the risers and tread make it), a split stair's side flights' width. */
  innerRadius?: number; sweep?: number | null; sideWidth?: number | null;
  /** What a plan writes on it (report #46), and how it draws the part over the plan's cut and the break line (report #143). */
  symbols?: Symbols | null;
}
interface Symbols { numbers: boolean; label: boolean; up: boolean; aboveCut?: 'dashed' | 'hidden' | 'whole'; break?: 'zigzag' | 'double' }
const SYMBOLS: Symbols = { numbers: false, label: false, up: true, aboveCut: 'dashed', break: 'zigzag' };
interface Defaults { spec: Spec; ramp?: Spec; rampShapes?: string[]; rules: Record<string, Rules>; shapes: string[]; turns: string[]; sides: string[]; constructions?: string[]; railingStyles?: string[]; library?: { key: string; spec: Spec }[] }

const SHAPES: Record<string, string> = { straight: 'Straight', 'straight-landing': 'Straight with landing', l: 'L shape', u: 'U shape (dog-leg with no gap)', spiral: 'Spiral', curved: 'Curved', split: 'Split (bifurcated)' };
const RAMP_SHAPES: Record<string, string> = { straight: 'Straight', 'straight-landing': 'Straight, with landings', l: 'L shape', u: 'Switchback (back and forth)' };
const RAMP_CONSTRUCTIONS: Record<string, string> = { rc: 'A sloping slab of reinforced concrete', solid: 'Solid, cast on the ground' };
const CONSTRUCTIONS: Record<string, string> = { rc: 'Waist slab under the steps', solid: 'Solid, cast on the ground', treads: 'Open treads (cantilevered, floating)', stringers: 'Treads on stringers' };
const RAILINGS: Record<string, string> = { bar: 'Posts and a handrail', balusters: 'Balusters', glass: 'Glass panels', cable: 'Cables', panel: 'Solid balustrade' };

/** A labelled length (the shared field: the project's unit, one commit on blur or Enter). */
function Mm({ label, value, onChange }: { label: string; value: number; onChange: (m: number) => void; step?: number }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <LengthField metres={value} onCommit={(m) => { if (m != null) onChange(m); }} />
    </label>
  );
}

/** A labelled whole number (risers, winders): the shared field. */
function Count({ label, value, min, max, after, onChange }: { label: string; value: number; min: number; max: number; after?: string; onChange: (n: number) => void }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <NumberField value={value} integer min={min} max={max} onCommit={(n) => { if (n != null) onChange(n); }} />{after && <span className="muted small"> {after}</span>}
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

/** The project's rules for a stair set (docs/plan-user-extensibility.md §5.4): the rule pack in use, over Lapin's. */
const projectRules = (set: string) => query<Omit<Rules, 'set'>>('architecture.stairRules', { set }).then((r) => (r ? { ...r, set } as Rules : null));

export function StairTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = [...ofClass(model, 'IfcStairType'), ...ofClass(model, 'IfcRampType')];
  const defaults = info?.catalogs['architecture.stairDefaults'] as unknown as Defaults | undefined;
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  const [spec, setSpec] = useState<Spec | null>(null);
  const [tab, setTab] = useState<'shape' | 'rules' | 'construction'>('shape');
  const [height, setHeight] = useState(3.0);
  const [preview, setPreview] = useState<StairNumbers | null>(null);
  const [drawn, setDrawn] = useState<{ plan: DrawingDto; section: DrawingDto } | null>(null);
  // Bumped by New stair type (or New ramp type), so the editor reloads the default even when no type was picked.
  const [freshTick, setFreshTick] = useState(0);
  const [freshKind, setFreshKind] = useState<'stair' | 'ramp'>('stair');
  const modelRef = useRef(model);
  modelRef.current = model;

  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as Spec | undefined) ?? (freshKind === 'ramp' ? defaults?.ramp : defaults?.spec) ?? null;
    setSpec(s ? structuredClone(s) : null);
    // A new type starts from the project's rules for its set (the rule pack in use), not the catalog's.
    if (!t && s) {
      let live = true;
      void projectRules(s.rules.set).then((r) => { if (live && r) setSpec((cur) => (cur && cur.rules.set === r.set ? { ...cur, rules: r } : cur)); });
      return () => { live = false; };
    }
  }, [editing, defaults, freshTick, freshKind]);

  useEffect(() => {
    if (!spec) return;
    let live = true;
    void query<StairNumbers>('architecture.stairPreview', { spec, height }).then((p) => { if (live) setPreview(p); });
    // The drawings take a moment (a session of their own): asked for once the spec has settled.
    const t = setTimeout(() => {
      void query<{ plan: DrawingDto; section: DrawingDto }>('architecture.stairDrawing', { spec, height }).then((d) => { if (live) setDrawn(d); });
    }, 250);
    return () => { live = false; clearTimeout(t); };
  }, [spec, height]);

  if (!spec || !defaults) return <PendingDialog title="Stair types" />;
  const set = (patch: Partial<Spec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const fresh = (kind: 'stair' | 'ramp') => { setEditing(null); setFreshKind(kind); setFreshTick((n) => n + 1); };
  const ramp = spec.kind === 'ramp';
  const placeWith = (id: string) => useUi.setState(ramp ? { rampTypeId: id } : { stairTypeId: id });
  const save = async () => {
    if (editing) await execute('architecture.updateStairType', { id: editing, spec });
    else {
      const r = await execute('architecture.createStairType', { spec }) as { id?: string } | null;
      if (r?.id) { setEditing(r.id); placeWith(r.id); }
    }
  };

  const W = 480;

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Stair types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Stair and ramp types</span>
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
            <li><button type="button" className="tree-item" onClick={() => fresh('stair')}><Plus size={12} /> New stair type</button></li>
            <li><button type="button" className="tree-item" data-action="new-ramp-type" onClick={() => fresh('ramp')}><Plus size={12} /> New ramp type</button></li>
            {/* Ready-made types the project has no copy of yet: Entrance steps. */}
            {(defaults.library ?? []).filter((l) => !types.some((t) => t.props.library === l.key)).map((l) => (
              <li key={l.key}>
                <button type="button" className="tree-item" title="Add this ready-made type to the project"
                  onClick={() => void execute('architecture.stairTypeFromLibrary', { key: l.key }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) setEditing(id); })}>
                  <Plus size={12} /> {l.spec.name} <span className="muted small">· library</span>
                </button>
              </li>
            ))}
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
              <div className="tabs-row small stair-tabs">
                {(['shape', 'rules', 'construction'] as const).map((t) => (
                  <button type="button" key={t} className={`tab${tab === t ? ' on' : ''}`} onClick={() => setTab(t)}>{t === 'shape' ? 'Shape' : t === 'rules' ? 'Rules' : 'Construction'}</button>
                ))}
              </div>
              {tab === 'shape' && ramp && <>
                <label className="field-line"><span className="eyebrow">Shape</span>
                  <select data-field="ramp-shape" value={spec.shape} onChange={(e) => set({ shape: e.target.value })}>{(defaults.rampShapes ?? Object.keys(RAMP_SHAPES)).map((s) => <option key={s} value={s}>{RAMP_SHAPES[s] ?? s}</option>)}</select>
                </label>
                <Mm label="Width" value={spec.width} onChange={(v) => set({ width: Math.max(0.5, v) })} />
                {spec.shape === 'u' && <Mm label="Gap between flights" value={spec.gap} onChange={(v) => set({ gap: Math.max(0, v) })} />}
                <label className="field-line"><span className="eyebrow">Slope</span>
                  <NumberField testId="ramp-type-slope" value={Math.round(spec.slope * 1000) / 10} min={0.5} max={50} suffix="%" onCommit={(n) => { if (n != null) set({ slope: n / 100 }); }} />
                </label>
                <p className="muted small">{`1 : ${Math.round(1 / spec.slope)}`} · {'the run in plan is the height over the slope'}</p>
                <Mm label="Longest flight" value={spec.maxRun ?? 9} onChange={(v) => set({ maxRun: Math.max(0, v) })} />
                <p className="muted small">Longer, the ramp is split into flights with landings between (0: no limit).</p>
                {spec.shape !== 'straight' && spec.shape !== 'l' && <Mm label="Landing length" value={spec.landingLength} onChange={(v) => set({ landingLength: Math.max(0, v) })} />}
              </>}
              {tab === 'shape' && !ramp && <>
                <label className="field-line"><span className="eyebrow">Shape</span>
                  <select value={spec.shape} onChange={(e) => set({ shape: e.target.value })}>{defaults.shapes.map((s) => <option key={s} value={s}>{SHAPES[s] ?? s}</option>)}</select>
                </label>
                {(spec.shape === 'l' || spec.shape === 'u') && <>
                  <label className="field-line"><span className="eyebrow">Turn made as</span>
                    <select value={spec.turn} onChange={(e) => set({ turn: e.target.value })}>{defaults.turns.map((t) => <option key={t} value={t}>{t}</option>)}</select>
                  </label>
                  {spec.turn === 'winders' && spec.shape === 'l' && <Count label="Winders per quarter turn" min={1} max={6} value={spec.winders} onChange={(winders) => set({ winders })} />}
                  {spec.turn === 'winders' && spec.shape === 'u' && <>
                    <Count label="Winders at the first corner" min={1} max={6} value={spec.winders} onChange={(winders) => set({ winders })} />
                    <Count label="Winders at the second corner" min={1} max={6} value={spec.windersEnd ?? spec.winders} onChange={(windersEnd) => set({ windersEnd })} />
                    <Count label="Straight treads between the corners" min={0} max={12} value={spec.between ?? 0} onChange={(between) => set({ between })} />
                    {((spec.between ?? 0) > 0 || (spec.windersEnd ?? spec.winders) !== spec.winders) && <p className="muted small">Each corner turns on its own; the well between the flights is as wide as the straight treads.</p>}
                  </>}
                </>}
                {(spec.shape === 'split' || (spec.shape === 'u' && !(spec.turn === 'winders' && ((spec.between ?? 0) > 0 || (spec.windersEnd ?? spec.winders) !== spec.winders)))) && <Mm label="Gap between flights" value={spec.gap} onChange={(v) => set({ gap: Math.max(0, v) })} />}
                <Mm label={spec.shape === 'split' ? 'Width of the main flight' : 'Width'} value={spec.width} onChange={(v) => set({ width: Math.max(0.5, v) })} />
                {spec.shape === 'split' && <Mm label="Width of the side flights" value={spec.sideWidth ?? Math.max(spec.rules.minWidth, Math.round(spec.width * 0.6 / 0.05) * 0.05)} onChange={(v) => set({ sideWidth: Math.max(0.5, v) })} />}
                {(spec.shape === 'spiral' || spec.shape === 'curved') && <>
                  <Mm label={spec.shape === 'spiral' ? 'Pole radius' : 'Inner radius'} value={spec.innerRadius ?? 0.1} onChange={(v) => set({ innerRadius: Math.max(0.03, v) })} />
                  <label className="field-line"><span className="eyebrow">Turns through (°)</span>
                    <NumberField testId="sweep" placeholder="as the tread makes it" value={spec.sweep ?? null} blank min={1} onCommit={(n) => set({ sweep: n })} /></label>
                  <p className="muted small">Empty: as the risers and the tread make it. Given: the tread follows from it.</p>
                </>}
                {spec.shape !== 'straight' && spec.shape !== 'spiral' && spec.shape !== 'curved' && <Mm label="Landing length" value={spec.landingLength} onChange={(v) => set({ landingLength: Math.max(0, v) })} />}
                {spec.shape !== 'straight' && spec.shape !== 'spiral' && spec.shape !== 'curved' && spec.landingLength === 0 && <p className="muted small">Landing length 0: as the width.</p>}
                <p className="eyebrow">In plan</p>
                {(['numbers', 'label', 'up'] as const).map((k) => (
                  <label key={k} className="row small">
                    <input type="checkbox" data-field={`symbols-${k}`} checked={(spec.symbols ?? SYMBOLS)[k]}
                      onChange={(e) => set({ symbols: { ...SYMBOLS, ...spec.symbols, [k]: e.target.checked } })} />
                    {k === 'numbers' ? 'Number the steps' : k === 'label' ? 'Label: risers, riser and tread, width' : '"UP" at the foot of the arrow'}
                  </label>
                ))}
                {/* Report #143: a plan cuts the stair at its cut plane, with a break line; what lies over the cut is dashed, left out, or the stair drawn whole. */}
                <label className="field-line"><span className="eyebrow">Over the cut plane</span>
                  <select data-field="symbols-above-cut" value={spec.symbols?.aboveCut ?? 'dashed'} onChange={(e) => set({ symbols: { ...SYMBOLS, ...spec.symbols, aboveCut: e.target.value as Symbols['aboveCut'] } })}>
                    <option value="dashed">Dashed</option><option value="hidden">Not drawn</option><option value="whole">Whole stair, no cut</option>
                  </select>
                </label>
                {(spec.symbols?.aboveCut ?? 'dashed') !== 'whole' && <label className="field-line"><span className="eyebrow">Break line</span>
                  <select data-field="symbols-break" value={spec.symbols?.break ?? 'zigzag'} onChange={(e) => set({ symbols: { ...SYMBOLS, ...spec.symbols, break: e.target.value as Symbols['break'] } })}>
                    <option value="zigzag">Zigzag</option><option value="double">Two parallel lines</option>
                  </select>
                </label>}
              </>}
              {tab === 'shape' && <>
                {/* How high it climbs: up to the storey above, or any height, from below the storey's level or up from it. */}
                <p className="eyebrow">Climbs</p>
                <label className="field-line"><span className="eyebrow">Up to</span>
                  <select data-field="stair-climbs" value={spec.rise == null ? 'storey' : 'own'} onChange={(e) => set({ rise: e.target.value === 'own' ? (spec.rise ?? 0.45) : null })}>
                    <option value="storey">The storey above</option><option value="own">A height of its own</option>
                  </select>
                </label>
                {spec.rise != null && <>
                  <Mm label="Rise" value={spec.rise} onChange={(v) => set({ rise: Math.max(0.01, v) })} />
                  <label className="field-line"><span className="eyebrow">Starts</span>
                    <select data-field="stair-rise-from" value={spec.riseFrom ?? 'below'} onChange={(e) => set({ riseFrom: e.target.value })}>
                      <option value="below">Up to its storey's level, from below (an entrance)</option>
                      <option value="level">From its storey's level up (a terrace, a platform)</option>
                    </select>
                  </label>
                  <p className="muted small">Each one placed may climb another height: its Rise in the properties.</p>
                </>}
              </>}
              {tab === 'rules' && ramp && <>
                <label className="field-line"><span className="eyebrow">Rule set</span>
                  <select value={spec.rules.set} onChange={(e) => { const next = e.target.value; void projectRules(next).then((r) => set({ rules: r ?? structuredClone(defaults.rules[next] ?? spec.rules) })); }}>
                    <option value="ramp">ramp</option><option value="public">public</option><option value="residential">residential</option>
                  </select>
                </label>
                <label className="field-line"><span className="eyebrow">Steepest slope</span>
                  <NumberField value={Math.round(spec.rules.maxSlope * 1000) / 10} min={0.5} max={50} suffix="%" onCommit={(n) => { if (n != null) set({ rules: { ...spec.rules, maxSlope: n / 100 } }); }} />
                </label>
                <Mm label="Minimum width" value={spec.rules.minWidth} onChange={(v) => set({ rules: { ...spec.rules, minWidth: v } })} />
                <Mm label="Headroom" value={spec.rules.headroom} onChange={(v) => set({ rules: { ...spec.rules, headroom: v } })} />
              </>}
              {tab === 'rules' && !ramp && <>
                <label className="field-line"><span className="eyebrow">Rule set</span>
                  <select value={spec.rules.set} onChange={(e) => { const next = e.target.value; void projectRules(next).then((r) => set({ rules: r ?? structuredClone(defaults.rules[next] ?? spec.rules) })); }}>
                    <option value="residential">residential</option><option value="public">public</option><option value="entrance">entrance steps</option>
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
                <Count label="Risers in a flight" min={1} max={30} after="at least" value={spec.rules.minRisers ?? 3} onChange={(minRisers) => set({ rules: { ...spec.rules, minRisers } })} />
                <Count label="Risers in a flight" min={1} max={30} after="at most" value={spec.rules.maxRisers} onChange={(maxRisers) => set({ rules: { ...spec.rules, maxRisers } })} />
                <Mm label="Headroom" value={spec.rules.headroom} onChange={(v) => set({ rules: { ...spec.rules, headroom: v } })} />
              </>}
              {tab === 'construction' && <>
                <p className="eyebrow">{spec.construction.kind === 'treads' || spec.construction.kind === 'stringers' ? 'Open treads' : 'Reinforced concrete, monolithic'}</p>
                <label className="field-line"><span className="eyebrow">Built as</span>
                  <select value={spec.construction.kind} onChange={(e) => set({ construction: { ...spec.construction, kind: e.target.value } })}>
                    {ramp
                      ? Object.entries(RAMP_CONSTRUCTIONS).map(([k, label]) => <option key={k} value={k}>{label}</option>)
                      : (defaults.constructions ?? ['rc']).map((k) => <option key={k} value={k}>{CONSTRUCTIONS[k] ?? k}</option>)}
                  </select>
                </label>
                {spec.construction.kind !== 'solid' && <Mm label={ramp ? 'Slab thickness' : spec.construction.kind === 'treads' || spec.construction.kind === 'stringers' ? 'Tread thickness' : 'Waist'} value={spec.construction.waist} onChange={(v) => set({ construction: { ...spec.construction, waist: Math.max(0.05, v) } })} />}
                <Mm label="Landing slab" value={spec.construction.landingThickness} onChange={(v) => set({ construction: { ...spec.construction, landingThickness: Math.max(0.05, v) } })} />
                <label className="field-line"><span className="eyebrow">Concrete</span><MaterialPicker value={spec.construction.concrete} onChange={(concrete) => set({ construction: { ...spec.construction, concrete } })} /></label>
                <p className="eyebrow">{ramp ? 'Finish' : 'Tread finish'}</p>
                <Mm label="Thickness" value={spec.finish.treadThickness} onChange={(v) => set({ finish: { ...spec.finish, treadThickness: Math.max(0, v) } })} />
                <label className="field-line"><span className="eyebrow">Material</span><MaterialPicker value={spec.finish.treadMaterial} onChange={(treadMaterial) => set({ finish: { ...spec.finish, treadMaterial } })} /></label>
                {!ramp && <Mm label="Nosing" value={spec.finish.nosing} onChange={(v) => set({ finish: { ...spec.finish, nosing: Math.max(0, v) } })} />}
                <p className="eyebrow">Railing</p>
                <label className="field-line"><span className="eyebrow">Sides</span>
                  <select value={spec.railing.sides} onChange={(e) => set({ railing: { ...spec.railing, sides: e.target.value } })}>{defaults.sides.map((s) => <option key={s} value={s}>{s}</option>)}</select>
                </label>
                <label className="field-line"><span className="eyebrow">Design</span>
                  <select data-field="railing-style" value={spec.railing.style ?? 'bar'} onChange={(e) => set({ railing: { ...spec.railing, style: e.target.value } })}>
                    {(defaults.railingStyles ?? ['bar']).map((k) => <option key={k} value={k}>{RAILINGS[k] ?? k}</option>)}
                  </select>
                </label>
                <label className="field-line"><span className="eyebrow">Handrail</span>
                  <select value={spec.railing.profile ?? 'square'} onChange={(e) => set({ railing: { ...spec.railing, profile: e.target.value } })}>
                    <option value="square">Square</option><option value="round">Round</option>
                  </select>
                </label>
                <Mm label="Height" value={spec.railing.height} onChange={(v) => set({ railing: { ...spec.railing, height: Math.max(0.3, v) } })} />
                <Mm label="Bar" value={spec.railing.barSize} onChange={(v) => set({ railing: { ...spec.railing, barSize: Math.max(0.01, v) } })} />
                <Mm label="Post" value={spec.railing.postSize} onChange={(v) => set({ railing: { ...spec.railing, postSize: Math.max(0.01, v) } })} />
                {(spec.railing.style ?? 'bar') !== 'panel' && (spec.railing.style ?? 'bar') !== 'balusters' && <Mm label="Post spacing" value={spec.railing.postSpacing} onChange={(v) => set({ railing: { ...spec.railing, postSpacing: Math.max(0.3, v) } })} />}
                {spec.railing.style === 'balusters' && <Mm label="Baluster spacing" value={spec.railing.balusterSpacing ?? 0.11} onChange={(v) => set({ railing: { ...spec.railing, balusterSpacing: Math.max(0.05, v) } })} />}
                <Mm label="In from the edge" value={spec.railing.inset ?? 0.05} onChange={(v) => set({ railing: { ...spec.railing, inset: Math.max(0, v) } })} />
                <Mm label="Runs on past the ends" value={spec.railing.extension ?? 0} onChange={(v) => set({ railing: { ...spec.railing, extension: Math.max(0, v) } })} />
                <label className="field-line"><span className="eyebrow">Colour</span><input type="color" value={spec.railing.color ?? '#4a4a4a'} onChange={(e) => set({ railing: { ...spec.railing, color: e.target.value } })} /></label>
              </>}
            </div>
            <div className="joinery-preview stair-preview">
              {spec.rise != null
                ? <p className="small caption">Preview at its own rise of <b>{formatLength(spec.rise)}</b></p>
                : <label className="field-line"><span className="eyebrow">Preview at a height of</span><LengthField metres={height} min={0.3} onCommit={(m) => { if (m != null) setHeight(m); }} /></label>}
              <Drawn drawing={drawn?.plan ?? null} width={W} label="Plan" />
              <Drawn drawing={drawn?.section ?? null} width={W} label="Section along the first flight" />
              {preview && preview.kind === 'ramp' && (
                <p className="small caption">
                  <b>{percent(preview.slope ?? 0)}</b> · {preview.flights.map((x) => lengthText(x)).join(' + ')} · run {lengthText(preview.run)} · {preview.predefinedType}
                </p>
              )}
              {preview && preview.kind !== 'ramp' && (
                <p className="small caption">
                  <b>{preview.risers}</b> risers · <b>{formatLength(preview.riserHeight)}</b> / <b>{lengthText(preview.tread)}</b> · 2h+b {lengthText(preview.step)}
                  · {preview.flights.join(' + ')} {preview.winders ? `+ ${preview.winders} winders` : ''} · run {lengthText(preview.run)} · {preview.predefinedType}
                </p>
              )}
              {preview && <ChecksList checks={preview.checks} />}
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while stairs or ramps use it)" onClick={() => void deleteType(editing).then((ok) => { if (ok) setEditing(null); })}>Delete</button>}
              {editing && <button type="button" className="btn" onClick={() => placeWith(editing)}>Place with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
