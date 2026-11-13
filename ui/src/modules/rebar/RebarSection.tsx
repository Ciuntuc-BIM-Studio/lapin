/**
 * A bar in the inspector: its shape's parameters (mm), diameter, hooks, how it is seen, its array, what it measures;
 * a distribution: its step or zones, how many are drawn, the line they are drawn with.
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { LengthField, NumberField } from '../../shell/cells';
import { lengthText } from '../../engine/unitsStore';
import { LabelStyleFields, useRebarSettings, type LabelStylePatch } from './LabelStyleFields';
import { lapFromTo, overStock, splitRefusal, stockLap } from './split';
import { diameterChoices, useRebarRules } from './rebarRules';
import { useShapeLibrary } from './barShapes';
import { paramsOf, type ShapeDef } from './shapeModel';

export interface ShapeInfo { code: string; label: string; layer: string; diameter: number; freeVertices: boolean; hooks: boolean; params: { key: string; label: string; default: number; min: number }[] }
export interface RebarCatalog { shapes: ShapeInfo[]; diameters: number[]; steels: string[]; views: string[] }

export function useRebarCatalog(): RebarCatalog | null {
  const info = useSession((s) => s.info);
  return (info?.catalogs['rebar.shapes'] as unknown as RebarCatalog | undefined) ?? null;
}

/** A shape's length parameter, in the project's unit; `min` in metres. */
function Len({ label, value, min, onChange }: { label: string; value: unknown; min?: number; onChange: (m: number) => void }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <LengthField metres={typeof value === 'number' ? value : null} min={min} onCommit={(m) => { if (m != null) onChange(m); }} />
    </label>
  );
}

interface Described {
  length: number; trueLength: number; massPerMetre: number; mass: number; bendRadius: number; mandrel: number; label: string; positions?: number; step?: string; pieces?: number;
  /** A bar's cover its anchors stand off by and where it comes from (decision 226). */
  cover?: number; coverSource?: 'own' | 'host' | 'settings';
  /** A bar: whether it can be cut with a lap, the stock length it is measured against, the lap it would get, and whether it is over the stock (decision 227). */
  splittable?: boolean; stockLength?: number; lap?: number; overStock?: boolean;
}

/** What the engine says of the bar now: lengths, mass, bend radius. */
function useDescribed(id: string): Described | null {
  const nodes = useSession((s) => s.nodes);
  const [d, setD] = useState<Described | null>(null);
  useEffect(() => { let live = true; void query<Described>('rebar.describe', { id }).then((r) => { if (live) setD(r ?? null); }); return () => { live = false; }; }, [id, nodes]);
  return d;
}

type HookJson = { angle: number; length: number; flip?: boolean } | null;

function HookFields({ label, hook, diameter, onChange }: { label: string; hook: HookJson | undefined; diameter: number; onChange: (h: HookJson | undefined) => void }) {
  const angle = hook ? hook.angle : -1;
  return (
    <div className="row small" data-i18n-ctx="rebar">
      <span className="eyebrow">{label}</span>
      <select value={angle} onChange={(e) => {
        const a = Number(e.target.value);
        if (a < 0) onChange(undefined); else if (a === 0) onChange(null);
        else onChange({ angle: a, length: Math.round((a <= 90 ? 12 : a <= 135 ? 10 : 5) * diameter * 1000) / 1000 < 0.05 ? 0.05 : Math.round((a <= 90 ? 12 : a <= 135 ? 10 : 5) * diameter * 1000) / 1000, flip: hook?.flip });
      }}>
        <option value={-1}>As the shape</option><option value={0}>None</option><option value={90}>90°</option><option value={135}>135°</option><option value={180}>180°</option>
      </select>
      {hook && hook.angle > 0 && <>
        <LengthField style={{ width: 64 }} metres={hook.length} min={0} onCommit={(m) => { if (m != null) onChange({ ...hook, length: m }); }} />
        <label className="check small"><input type="checkbox" checked={!!hook.flip} onChange={(e) => onChange({ ...hook, flip: e.target.checked })} /> Other side</label>
      </>}
    </div>
  );
}

/**
 * The cover the bar's anchors stand off by (decision 226): its own in mm, or, empty, the effective one as a hint
 * (the host type's stirrup cover plus the stirrup, else the project's settings); Clear gives the bar's own back.
 */
function CoverField({ node, d, set }: { node: NodeDto; d: Described | null; set: (args: Record<string, unknown>) => void }) {
  const own = typeof node.props.cover === 'number' ? node.props.cover : null;
  return (
    <div className="row small" data-field="rebar-cover">
      <span className="eyebrow">Cover</span>
      <LengthField style={{ width: 64 }} metres={own} min={0} max={0.2} blank placeholder={d?.cover != null ? lengthText(d.cover) : ''}
        title="The cover the anchor grips stand off the bar by: drag an anchor onto the formwork face and the bar stops here inside it"
        onCommit={(m) => set({ cover: m })} />
      {own == null && d?.coverSource === 'host' && <span className="muted small">from the host</span>}
      {own == null && d?.coverSource === 'settings' && <span className="muted small">from the settings</span>}
      {own != null && <button type="button" className="link small" onClick={() => set({ cover: null })}>Clear</button>}
    </div>
  );
}

/** A bar's or a distribution's own label style, over the project's for its kind; its place (the balloon, the elbow, the string) is dragged by its grips. */
function LabelBlock({ node, kind, command }: { node: NodeDto; kind: 'bars' | 'runs'; command: string }) {
  const settings = useRebarSettings();
  const label = node.props.label as { at?: number[]; elbow?: number[]; offset?: number; style?: LabelStylePatch } | undefined;
  const placed = !!(label?.at || label?.elbow || label?.offset != null);
  return (
    <>
      <p className="eyebrow">Label</p>
      <LabelStyleFields style={label?.style} base={settings?.labels[kind]} onChange={(style) => void execute(command, { id: node.id, label: { style } })}
        onReset={() => void execute(command, { id: node.id, label: { style: null } })} testId="rebar-label" withLine={kind === 'runs'} />
      {placed && <button type="button" className="link small" onClick={() => void execute(command, { id: node.id, label: { at: null, elbow: null, offset: null } })}>Put the label back</button>}
    </>
  );
}

interface SplitPreview { first?: number; second?: number; lap?: number; from?: number; to?: number; length?: number; error?: string }

/** The lap zone the block starts with (mm): the project's lap ending at the bar's middle. */
function startZone(length: number, lap: number | undefined): { from: number; to: number } {
  const z = lapFromTo(length / 2, lap ?? 0);
  return { from: Math.round(z.from * 1000), to: Math.round(z.to * 1000) };
}

/**
 * A longitudinal bar cut in two with a lap (decision 227), the lap zone the engineer's: its two stations typed in mm from the bar's
 * start, `from` (where the second piece starts) and `to` (where the first piece ends), the lap beside them (editing it keeps `to`
 * and moves `from`); to begin with, the project's lap (50Ø unless set) ending at the bar's middle. The two lengths the engine
 * foresees, and the cut; a bar longer than the stock length is warned of, with a cut whose lap ends at the stock length, so the
 * first piece is exactly that long.
 */
function SplitBlock({ node, d }: { node: NodeDto; d: Described }) {
  const length = d.length;
  const [zone, setZone] = useState(() => startZone(length, d.lap));
  const [preview, setPreview] = useState<SplitPreview | null>(null);
  // A new bar selected, its length or the project's lap changed: the fields start over.
  useEffect(() => { setZone(startZone(length, d.lap)); }, [node.id, length, d.lap]);
  const { from, to } = zone;
  const lap = to - from;
  const refusal = splitRefusal(length, from / 1000, to / 1000);
  useEffect(() => {
    if (refusal) { setPreview(null); return; }
    let live = true;
    void query<SplitPreview>('rebar.splitPreview', { id: node.id, from: from / 1000, to: to / 1000 }).then((r) => { if (live) setPreview(r ?? null); });
    return () => { live = false; };
  }, [node.id, from, to, refusal]);
  const split = async (z: { from: number; to: number }) => {
    const r = await execute('rebar.split', { id: node.id, from: z.from, to: z.to }) as { first?: string; second?: string } | null;
    if (r?.first && r.second) useUi.setState({ selected: [r.first, r.second] });
  };
  const num = (e: React.ChangeEvent<HTMLInputElement>) => Math.max(0, Number(e.target.value) || 0);
  const stock = d.stockLength ?? 12;
  const over = d.overStock || overStock(length, stock);
  const atStock = stockLap(stock, lap / 1000);
  return (
    <div data-field="rebar-split" data-i18n-ctx="rebar">
      <p className="eyebrow">Split with a lap</p>
      {over && (
        <div className="row small">
          <span className="small" style={{ color: '#c2410c' }}>Longer than the stock length</span><span className="muted small" data-no-i18n>({stock} m)</span>
          {!splitRefusal(length, atStock.from, atStock.to) && <button type="button" className="link small" data-action="rebar-split-stock" onClick={() => void split(atStock)}>Cut at the stock length</button>}
        </div>
      )}
      <div className="row small">
        <label className="field-line"><span className="eyebrow">Lap from</span>
          <input className="num" type="number" step={10} min={0} max={Math.round(length * 1000)} value={from} data-field="rebar-split-from" onChange={(e) => setZone({ from: num(e), to })} />
        </label>
        <label className="field-line"><span className="eyebrow">to</span>
          <input className="num" type="number" step={10} min={0} max={Math.round(length * 1000)} value={to} data-field="rebar-split-to" onChange={(e) => setZone({ from, to: num(e) })} /><span className="muted small"> mm</span>
        </label>
        <label className="field-line"><span className="eyebrow">Lap</span>
          <input className="num" type="number" step={10} min={10} style={{ width: 64 }} value={lap} data-field="rebar-split-lap" onChange={(e) => setZone({ from: to - num(e), to })} /><span className="muted small"> mm</span>
        </label>
      </div>
      <p className="muted small">From the bar's start. The first bar ends at 'to', the second starts at 'from'.</p>
      <div className="row small">
        {refusal && <span className="muted small">{refusal}</span>}
        {!refusal && preview?.first != null && preview.second != null && (
          <span className="muted small"><span>Pieces</span><span data-no-i18n>: {Math.round(preview.first * 1000)} + {Math.round(preview.second * 1000)} mm</span></span>
        )}
        <button type="button" className="btn" disabled={!!refusal} data-action="rebar-split" onClick={() => void split({ from: from / 1000, to: to / 1000 })}>Split the bar</button>
      </div>
    </div>
  );
}

export function RebarSection({ node }: { node: NodeDto }) {
  const catalog = useRebarCatalog();
  const nodes = useSession((s) => s.nodes);
  const d = useDescribed(node.id);
  const rules = useRebarRules();
  const lib = useShapeLibrary();
  // A bar of one of the project's shapes (docs/plan-user-extensibility.md §6.3) keeps its definition: its parameters are the definition's.
  const userId = typeof node.props.shapeId === 'string' ? node.props.shapeId : null;
  const userDef = (node.props.shapeDef as unknown as ShapeDef | undefined) ?? lib?.shapes.find((x) => x.id === userId)?.def ?? null;
  const shape = userDef
    ? ({ code: String(node.props.shape), label: userDef.label, layer: userDef.layer ?? '', diameter: userDef.diameter ?? 0.012, freeVertices: false, hooks: true, params: paramsOf(userDef) } as ShapeInfo)
    : catalog?.shapes.find((s) => s.code === node.props.shape) ?? null;
  const params = (node.props.params as Record<string, number> | undefined) ?? {};
  const hooks = (node.props.hooks as { start?: HookJson; end?: HookJson } | undefined) ?? {};
  const diameter = typeof node.props.diameter === 'number' ? node.props.diameter : 0.012;
  const array = (node.props.array as { nx: number; dx: number; ny: number; dy: number } | undefined) ?? { nx: 1, dx: 0.1, ny: 1, dy: 0.1 };
  const set = (args: Record<string, unknown>) => void execute('rebar.update', { id: node.id, ...args });
  const setHook = (end: 'start' | 'end', h: HookJson | undefined) => set({ hooks: { ...hooks, [end]: h } });
  if (!catalog) return null;
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Shape</span>
        <select value={userId ? `@${userId}` : String(node.props.shape ?? 'straight')} disabled={!node.editable} data-field="rebar-shape"
          onChange={(e) => { const v = e.target.value; set(v.startsWith('@') ? { shapeId: v.slice(1) } : { shape: v }); }}>
          {catalog.shapes.map((s) => <option key={s.code} value={s.code}>{s.label}</option>)}
          {(lib?.shapes.length ?? 0) > 0 && <optgroup label="This project's shapes">{lib!.shapes.map((s) => <option key={s.id} value={`@${s.id}`} data-no-i18n>{s.name}</option>)}</optgroup>}
        </select>
      </label>
      {shape?.params.map((p) => <Len key={p.key} label={p.label} value={params[p.key] ?? p.default} min={p.min} onChange={(v) => set({ params: { [p.key]: v } })} />)}
      {shape && !shape.freeVertices && shape.code !== 'dot' && <CoverField node={node} d={d} set={set} />}
      <label className="field-line"><span className="eyebrow">Diameter</span>
        <select value={diameter} onChange={(e) => set({ diameter: Number(e.target.value) })}>
          {diameterChoices(rules, catalog.diameters, diameter).map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000 * 10) / 10}</option>)}
        </select>
      </label>
      <p className="eyebrow">Hooks</p>
      <HookFields label="Start" hook={hooks.start} diameter={diameter} onChange={(h) => setHook('start', h)} />
      <HookFields label="End" hook={hooks.end} diameter={diameter} onChange={(h) => setHook('end', h)} />
      <p className="eyebrow">Laid</p>
      <div className="row small">
        <label className="check small"><input type="checkbox" checked={!!node.props.mirrorX} onChange={(e) => set({ mirrorX: e.target.checked })} /> Mirror X</label>
        <label className="check small"><input type="checkbox" checked={!!node.props.mirrorY} onChange={(e) => set({ mirrorY: e.target.checked })} /> Mirror Y</label>
      </div>
      <div className="row small" data-field="rebar-array">
        <span className="eyebrow">Array</span>
        <NumberField style={{ width: 44 }} value={array.nx} min={1} max={200} integer onCommit={(n) => { if (n != null) set({ array: { ...array, nx: n } }); }} />
        <span>×</span>
        <LengthField style={{ width: 64 }} metres={array.dx} onCommit={(m) => { if (m != null) set({ array: { ...array, dx: m } }); }} />
        <span className="muted small">along x;</span>
        <NumberField style={{ width: 44 }} value={array.ny} min={1} max={200} integer onCommit={(n) => { if (n != null) set({ array: { ...array, ny: n } }); }} />
        <span>×</span>
        <LengthField style={{ width: 64 }} metres={array.dy} onCommit={(m) => { if (m != null) set({ array: { ...array, dy: m } }); }} />
        <span className="muted small">along y</span>
      </div>
      <p className="eyebrow">Across the drawing</p>
      {(() => {
        const across = (node.props.across as { count: number; mode: 'cover' | 'step'; step: number } | undefined) ?? { count: 1, mode: 'cover', step: 0.05 };
        const host = typeof node.props.host === 'string' ? nodes.get(node.props.host) : undefined;
        return (
          <div className="row small" data-field="rebar-across">
            <NumberField style={{ width: 44 }} value={across.count} min={1} max={40} integer onCommit={(n) => { if (n != null) set({ across: { ...across, count: n } }); }} />
            <span>×</span>
            <select value={across.mode} onChange={(e) => set({ across: { ...across, mode: e.target.value as 'cover' | 'step' } })}>
              <option value="cover">by the cover</option><option value="step">at a step</option>
            </select>
            {across.mode === 'step' && <LengthField style={{ width: 64 }} metres={across.step} min={0.005} onCommit={(m) => { if (m != null) set({ across: { ...across, step: m } }); }} />}
            {host && <span className="muted small">in <span data-no-i18n>{host.name}</span></span>}
          </div>
        );
      })()}
      {d && (
        <dl className="fields" data-field="rebar-describe">
          <dt>Label</dt><dd><span data-no-i18n>{d.label}</span></dd>
          <dt>Length</dt><dd><span className="num">{Math.round(d.length * 1000)}</span> <span className="muted">mm</span></dd>
          <dt>Developed</dt><dd><span className="num">{Math.round(d.trueLength * 1000)}</span> <span className="muted">mm</span></dd>
          <dt>Bend radius</dt><dd><span className="num">{Math.round(d.bendRadius * 1000)}</span> <span className="muted">mm</span></dd>
          <dt>Mandrel</dt><dd><span className="num">{Math.round(d.mandrel * 1000)}</span> <span className="muted">mm</span></dd>
          <dt>Mass</dt><dd><span className="num">{d.mass.toFixed(2)}</span> <span className="muted">kg</span></dd>
          <dt>Per metre</dt><dd><span className="num">{d.massPerMetre.toFixed(3)}</span> <span className="muted">kg/m</span></dd>
        </dl>
      )}
      {d?.splittable && node.editable !== false && <SplitBlock node={node} d={d} />}
      <LabelBlock node={node} kind="bars" command="rebar.update" />
      {typeof node.props.original === 'string' && <p className="muted small">A view of another bar: it is not counted in the schedule.</p>}
    </div>
  );
}

type Zone = { length: number; step: number };

export function RebarRunSection({ node }: { node: NodeDto }) {
  const catalog = useRebarCatalog();
  const d = useDescribed(node.id);
  const zones = (node.props.zones as Zone[] | undefined) ?? [];
  const rules = useRebarRules();
  const lib = useShapeLibrary();
  const userId = typeof node.props.shapeId === 'string' ? node.props.shapeId : null;
  const userDef = (node.props.shapeDef as unknown as ShapeDef | undefined) ?? lib?.shapes.find((x) => x.id === userId)?.def ?? null;
  const shape = userDef
    ? ({ code: String(node.props.shape), label: userDef.label, layer: userDef.layer ?? '', diameter: userDef.diameter ?? 0.008, freeVertices: false, hooks: true, params: paramsOf(userDef) } as ShapeInfo)
    : catalog?.shapes.find((s) => s.code === node.props.shape) ?? null;
  const params = (node.props.params as Record<string, number> | undefined) ?? {};
  const set = (args: Record<string, unknown>) => void execute('rebar.updateRun', { id: node.id, ...args });
  const setZones = (z: Zone[]) => set({ zones: z });
  if (!catalog) return null;
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Bar</span>
        <select value={userId ? `@${userId}` : String(node.props.shape ?? 'stirrup')} disabled={!node.editable}
          onChange={(e) => { const v = e.target.value; set(v.startsWith('@') ? { shapeId: v.slice(1) } : { shape: v }); }}>
          {catalog.shapes.filter((s) => s.code !== 'dot').map((s) => <option key={s.code} value={s.code}>{s.label}</option>)}
          {(lib?.shapes.length ?? 0) > 0 && <optgroup label="This project's shapes">{lib!.shapes.map((s) => <option key={s.id} value={`@${s.id}`} data-no-i18n>{s.name}</option>)}</optgroup>}
        </select>
      </label>
      {shape?.params.map((p) => <Len key={p.key} label={p.label} value={params[p.key] ?? p.default} min={p.min} onChange={(v) => set({ params: { [p.key]: v } })} />)}
      <label className="field-line"><span className="eyebrow">Diameter</span>
        <select value={typeof node.props.diameter === 'number' ? node.props.diameter : 0.008} onChange={(e) => set({ diameter: Number(e.target.value) })}>
          {diameterChoices(rules, catalog.diameters, typeof node.props.diameter === 'number' ? node.props.diameter : 0.008).map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000 * 10) / 10}</option>)}
        </select>
      </label>
      <p className="eyebrow">Zones</p>
      {zones.length === 0 && <p className="muted small">One step along the whole path. Zones put a closer step at the ends (a third each, for a seismic beam).</p>}
      {zones.map((z, i) => (
        <div key={i} className="row small">
          <LengthField style={{ width: 64 }} metres={z.length} min={0.001} onCommit={(m) => { if (m != null) setZones(zones.map((x, j) => (j === i ? { ...x, length: m } : x))); }} /><span className="muted small">at</span>
          <LengthField style={{ width: 64 }} metres={z.step} min={0.01} onCommit={(m) => { if (m != null) setZones(zones.map((x, j) => (j === i ? { ...x, step: m } : x))); }} />
          <button type="button" className="link small" onClick={() => setZones(zones.filter((_, j) => j !== i))}>Remove</button>
        </div>
      ))}
      <div className="row small">
        <button type="button" className="btn" onClick={() => setZones([...zones, { length: 1, step: typeof node.props.step === 'number' ? node.props.step : 0.15 }])}>Add zone</button>
        {zones.length === 0 && <button type="button" className="btn" data-action="rebar-thirds" onClick={() => {
          const step = typeof node.props.step === 'number' ? node.props.step : 0.15;
          const a = node.props.start as number[], b = node.props.end as number[];
          const len = a && b ? Math.hypot(b[0] - a[0], b[1] - a[1]) : 3;
          setZones([{ length: len / 3, step: step / 2 }, { length: len / 3, step }, { length: len / 3, step: step / 2 }]);
        }}>Thirds</button>}
      </div>
      {d && (
        <dl className="fields">
          <dt>Label</dt><dd><span data-no-i18n>{d.label}</span></dd>
          <dt>Positions</dt><dd><span className="num">{d.positions}</span></dd>
          <dt>Each</dt><dd><span className="num">{Math.round(d.length * 1000)}</span> <span className="muted">mm</span></dd>
          <dt>Mass</dt><dd><span className="num">{d.mass.toFixed(2)}</span> <span className="muted">kg</span></dd>
        </dl>
      )}
      <LabelBlock node={node} kind="runs" command="rebar.updateRun" />
    </div>
  );
}
