/**
 * A bar in the inspector: its shape's parameters (mm), diameter, hooks, how it is seen, its array, what it measures;
 * a distribution: its step or zones, how many are drawn, the line they are drawn with.
 */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';

export interface ShapeInfo { code: string; label: string; layer: string; diameter: number; freeVertices: boolean; hooks: boolean; params: { key: string; label: string; default: number; min: number }[] }
export interface RebarCatalog { shapes: ShapeInfo[]; diameters: number[]; steels: string[]; views: string[] }

export function useRebarCatalog(): RebarCatalog | null {
  const info = useSession((s) => s.info);
  return (info?.catalogs['rebar.shapes'] as unknown as RebarCatalog | undefined) ?? null;
}

const mm = (m: unknown) => (typeof m === 'number' ? Math.round(m * 1000) : '');

function Mm({ label, value, min, onChange, step = 1 }: { label: string; value: unknown; min?: number; onChange: (m: number) => void; step?: number }) {
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <input className="num" type="number" step={step} value={mm(value)} onChange={(e) => { const n = Number(e.target.value); if (Number.isFinite(n) && (min === undefined || n >= min)) onChange(n / 1000); }} /><span className="muted small"> mm</span>
    </label>
  );
}

interface Described { length: number; trueLength: number; massPerMetre: number; mass: number; bendRadius: number; mandrel: number; label: string; positions?: number; step?: string; pieces?: number }

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
        <input className="num" type="number" style={{ width: 56 }} value={Math.round(hook.length * 1000)} onChange={(e) => onChange({ ...hook, length: Math.max(0, Number(e.target.value)) / 1000 })} /><span className="muted small">mm</span>
        <label className="check small"><input type="checkbox" checked={!!hook.flip} onChange={(e) => onChange({ ...hook, flip: e.target.checked })} /> Other side</label>
      </>}
    </div>
  );
}

export function RebarSection({ node }: { node: NodeDto }) {
  const catalog = useRebarCatalog();
  const d = useDescribed(node.id);
  const shape = catalog?.shapes.find((s) => s.code === node.props.shape) ?? null;
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
        <select value={String(node.props.shape ?? 'straight')} disabled={!node.editable} onChange={(e) => set({ shape: e.target.value })}>
          {catalog.shapes.map((s) => <option key={s.code} value={s.code}>{s.label}</option>)}
        </select>
      </label>
      {shape?.params.map((p) => <Mm key={p.key} label={p.label} value={params[p.key] ?? p.default} min={p.min * 1000} onChange={(v) => set({ params: { [p.key]: v } })} />)}
      <label className="field-line"><span className="eyebrow">Diameter</span>
        <select value={diameter} onChange={(e) => set({ diameter: Number(e.target.value) })}>
          {catalog.diameters.map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000)}</option>)}
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
        <input className="num" type="number" min={1} max={200} style={{ width: 44 }} value={array.nx} onChange={(e) => set({ array: { ...array, nx: Math.max(1, Number(e.target.value) || 1) } })} />
        <span>×</span>
        <input className="num" type="number" style={{ width: 56 }} value={Math.round(array.dx * 1000)} onChange={(e) => set({ array: { ...array, dx: Number(e.target.value) / 1000 } })} />
        <span className="muted small">mm along x;</span>
        <input className="num" type="number" min={1} max={200} style={{ width: 44 }} value={array.ny} onChange={(e) => set({ array: { ...array, ny: Math.max(1, Number(e.target.value) || 1) } })} />
        <span>×</span>
        <input className="num" type="number" style={{ width: 56 }} value={Math.round(array.dy * 1000)} onChange={(e) => set({ array: { ...array, dy: Number(e.target.value) / 1000 } })} />
        <span className="muted small">mm along y</span>
      </div>
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
      {typeof node.props.original === 'string' && <p className="muted small">A view of another bar: it is not counted in the schedule.</p>}
    </div>
  );
}

type Zone = { length: number; step: number };

export function RebarRunSection({ node }: { node: NodeDto }) {
  const catalog = useRebarCatalog();
  const d = useDescribed(node.id);
  const zones = (node.props.zones as Zone[] | undefined) ?? [];
  const shape = catalog?.shapes.find((s) => s.code === node.props.shape) ?? null;
  const params = (node.props.params as Record<string, number> | undefined) ?? {};
  const set = (args: Record<string, unknown>) => void execute('rebar.updateRun', { id: node.id, ...args });
  const setZones = (z: Zone[]) => set({ zones: z });
  if (!catalog) return null;
  return (
    <div className="layers">
      <label className="type-pick">
        <span className="eyebrow">Bar</span>
        <select value={String(node.props.shape ?? 'stirrup')} disabled={!node.editable} onChange={(e) => set({ shape: e.target.value })}>
          {catalog.shapes.filter((s) => s.code !== 'dot').map((s) => <option key={s.code} value={s.code}>{s.label}</option>)}
        </select>
      </label>
      {shape?.params.map((p) => <Mm key={p.key} label={p.label} value={params[p.key] ?? p.default} min={p.min * 1000} onChange={(v) => set({ params: { [p.key]: v } })} />)}
      <label className="field-line"><span className="eyebrow">Diameter</span>
        <select value={typeof node.props.diameter === 'number' ? node.props.diameter : 0.008} onChange={(e) => set({ diameter: Number(e.target.value) })}>
          {catalog.diameters.map((x) => <option key={x} value={x}>Ø{Math.round(x * 1000)}</option>)}
        </select>
      </label>
      <p className="eyebrow">Zones</p>
      {zones.length === 0 && <p className="muted small">One step along the whole path. Zones put a closer step at the ends (a third each, for a seismic beam).</p>}
      {zones.map((z, i) => (
        <div key={i} className="row small">
          <input className="num" type="number" style={{ width: 64 }} value={Math.round(z.length * 1000)} onChange={(e) => setZones(zones.map((x, j) => (j === i ? { ...x, length: Math.max(1, Number(e.target.value)) / 1000 } : x)))} /><span className="muted small">mm at</span>
          <input className="num" type="number" style={{ width: 56 }} value={Math.round(z.step * 1000)} onChange={(e) => setZones(zones.map((x, j) => (j === i ? { ...x, step: Math.max(10, Number(e.target.value)) / 1000 } : x)))} /><span className="muted small">mm</span>
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
    </div>
  );
}
