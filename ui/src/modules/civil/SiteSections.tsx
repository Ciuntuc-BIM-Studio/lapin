/**
 * The site's elements in the inspector, each in its own terms: an earthwork
 * (a platform's level, a ditch's section), a path's courses and kerbs, a
 * retaining wall's section and ground sides, a planting's mix and density.
 * Lengths in mm.
 */

import { useEffect, useMemo, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { isDitch } from './prefs';

/** Opens the corridor configurator on the element selected. */
function Configure() {
  return <><dt /><dd><button type="button" className="btn compact" onClick={() => useUi.setState({ dialog: 'corridor' })}>Configurator…</button> <span className="muted small">section, profile, stations</span></dd></>;
}

const mm = (v: unknown, d = 0) => (typeof v === 'number' ? Math.round(v * 1000) : Math.round(d * 1000));

/** A length in mm committed on Enter or blur (empty: the fallback goes back). */
function Mm({ value, fallback = 0, onCommit, min }: { value: unknown; fallback?: number; onCommit: (m: number) => void; min?: number }) {
  const shown = String(mm(value, fallback));
  const [text, setText] = useState(shown);
  useEffect(() => setText(shown), [shown]);
  const done = () => { const n = Number(text.replace(',', '.')); if (text.trim() !== '' && Number.isFinite(n) && text !== shown) onCommit(Math.max(min ?? -Infinity, n) / 1000); else setText(shown); };
  return <><input className="num" value={text} onChange={(e) => setText(e.target.value)} onBlur={done} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} /> <span className="muted">mm</span></>;
}

/** A platform (its level) or a ditch (its depth, bottom and side slope). */
export function EarthworkSection({ node }: { node: NodeDto }) {
  if (isDitch(node)) {
    const set = (args: Record<string, unknown>) => void execute('civil.updateDitch', { id: node.id, ...args });
    const slope = typeof node.props.slope === 'number' ? node.props.slope : 1;
    return (
      <dl className="fields">
        <dt>Depth under the ground</dt><dd><Mm value={node.props.depth} fallback={0.5} min={50} onCommit={(v) => set({ depth: v })} /></dd>
        <dt>Bottom width</dt><dd><Mm value={node.props.bottomWidth} fallback={0.4} min={50} onCommit={(v) => set({ bottomWidth: v })} /></dd>
        <dt>Side slope</dt><dd>1 : <input className="num" type="number" step={0.25} min={0} max={10} value={slope} onChange={(e) => set({ slope: Math.max(0, Number(e.target.value) || 0) })} /></dd>
        <Configure />
        <dt>Top width</dt><dd><span className="num">{mm((typeof node.props.bottomWidth === 'number' ? node.props.bottomWidth : 0.4) + 2 * slope * (typeof node.props.depth === 'number' ? node.props.depth : 0.5))} mm</span></dd>
      </dl>
    );
  }
  return (
    <dl className="fields">
      <dt>Elevation</dt><dd><Mm value={node.props.elevation} onCommit={(v) => void execute('civil.updatePlatform', { id: node.id, elevation: v })} /></dd>
    </dl>
  );
}

export function PathSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const set = (args: Record<string, unknown>) => void execute('civil.updatePath', { id: node.id, ...args });
  const mat = (key: string) => (typeof node.props[key] === 'string' ? nodes.get(node.props[key] as string)?.name ?? '' : '');
  return (
    <dl className="fields">
      <Configure />
      <dt>Width</dt><dd><Mm value={node.props.width} fallback={1.5} min={200} onCommit={(v) => set({ width: v })} /></dd>
      <dt>Surface course</dt><dd><Mm value={node.props.surfaceThickness} fallback={0.06} min={0} onCommit={(v) => set({ surfaceThickness: v })} /></dd>
      <dt>Surface material</dt><dd><input defaultValue={mat('surfaceMaterial')} key={mat('surfaceMaterial')} onBlur={(e) => { if (e.target.value.trim() && e.target.value !== mat('surfaceMaterial')) set({ surfaceMaterial: e.target.value.trim() }); }} /></dd>
      <dt>Base course</dt><dd><Mm value={node.props.baseThickness} fallback={0.2} min={0} onCommit={(v) => set({ baseThickness: v })} /></dd>
      <dt>Base material</dt><dd><input defaultValue={mat('baseMaterial')} key={mat('baseMaterial')} onBlur={(e) => { if (e.target.value.trim() && e.target.value !== mat('baseMaterial')) set({ baseMaterial: e.target.value.trim() }); }} /></dd>
      <dt>Kerbs</dt><dd><input type="checkbox" checked={node.props.kerb === true} onChange={(e) => set({ kerb: e.target.checked })} /></dd>
      {node.props.kerb === true && <>
        <dt>Kerb height</dt><dd><Mm value={node.props.kerbHeight} fallback={0.1} min={20} onCommit={(v) => set({ kerbHeight: v })} /></dd>
        <dt>Kerb width</dt><dd><Mm value={node.props.kerbWidth} fallback={0.1} min={20} onCommit={(v) => set({ kerbWidth: v })} /></dd>
      </>}
    </dl>
  );
}

export function RetainingWallSection({ node }: { node: NodeDto }) {
  const set = (args: Record<string, unknown>) => void execute('civil.updateRetainingWall', { id: node.id, ...args });
  const fixedTop = typeof node.props.topElevation === 'number';
  return (
    <dl className="fields">
      <Configure />
      <dt>Thickness</dt><dd><Mm value={node.props.thickness} fallback={0.3} min={100} onCommit={(v) => set({ thickness: v })} /></dd>
      <dt>High ground on the</dt>
      <dd><select value={(node.props.side as string | undefined) ?? 'left'} onChange={(e) => set({ side: e.target.value })}><option value="left">left</option><option value="right">right</option></select> <span className="muted small">of the way it was drawn</span></dd>
      <dt>Crown</dt>
      <dd><select value={fixedTop ? 'absolute' : 'above'} onChange={(e) => set({ topElevation: e.target.value === 'absolute' ? (typeof node.props.height === 'number' ? node.props.height : 1) : null })}>
        <option value="above">over the high ground</option><option value="absolute">at a fixed elevation</option></select></dd>
      {fixedTop
        ? <><dt>Crown elevation</dt><dd><Mm value={node.props.topElevation} onCommit={(v) => set({ topElevation: v })} /></dd></>
        : <><dt>Height over the ground</dt><dd><Mm value={node.props.height} fallback={1} min={0} onCommit={(v) => set({ height: v })} /></dd></>}
      <dt>Embedment</dt><dd><Mm value={node.props.embed} fallback={0.3} min={0} onCommit={(v) => set({ embed: v })} /></dd>
      <dt>Footing width</dt><dd><Mm value={node.props.footingWidth} fallback={0.8} min={100} onCommit={(v) => set({ footingWidth: v })} /></dd>
      <dt>Footing thickness</dt><dd><Mm value={node.props.footingThickness} fallback={0.3} min={0} onCommit={(v) => set({ footingThickness: v })} /></dd>
    </dl>
  );
}

interface Species { name: string; type: string; height: number; crown: number; color: string; share: number }

export function PlantingSection({ node }: { node: NodeDto }) {
  const info = useSession((s) => s.info);
  const catalog = (info?.catalogs['civil.plants'] as unknown as Species[] | undefined) ?? [];
  const species = useMemo(() => (Array.isArray(node.props.species) ? (node.props.species as unknown as Species[]) : []), [node.props.species]);
  const zone = Array.isArray(node.props.contour);
  const set = (args: Record<string, unknown>) => void execute('civil.updatePlanting', { id: node.id, ...args });
  const setSpecies = (next: Species[]) => set({ species: next });
  const add = (name: string) => { const c = catalog.find((s) => s.name === name); if (c) setSpecies([...species, { ...c, share: 1 }]); };
  return (
    <div className="layers">
      {zone && <dl className="fields">
        <dt>Plants per 100 m²</dt><dd><input className="num" type="number" step={0.5} min={0.1} value={typeof node.props.density === 'number' ? node.props.density : 3} onChange={(e) => set({ density: Math.max(0.1, Number(e.target.value) || 0.1) })} /></dd>
        <dt>Seed</dt><dd><input className="num" type="number" value={typeof node.props.seed === 'number' ? node.props.seed : 1} onChange={(e) => set({ seed: Number(e.target.value) || 1 })} /> <button type="button" className="link small" onClick={() => set({ seed: Math.floor(Math.random() * 100000) })}>shuffle</button></dd>
      </dl>}
      <p className="eyebrow">Species{zone ? ' · share' : ''}</p>
      <table className="panel-grid species">
        <tbody>
          {species.map((s, i) => (
            <tr key={i}>
              <td><span className="swatch" style={{ background: s.color }} /> {s.name} <span className="muted small">{s.type} · {Math.round(s.height * 10) / 10} m</span></td>
              {zone && <td><input className="num" type="number" step={0.1} min={0} value={s.share} onChange={(e) => setSpecies(species.map((x, k) => (k === i ? { ...x, share: Math.max(0, Number(e.target.value) || 0) } : x)))} /></td>}
              <td><button type="button" className="icon" title="Remove" disabled={species.length <= 1} onClick={() => setSpecies(species.filter((_, k) => k !== i))}>×</button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <select value="" onChange={(e) => { if (e.target.value) add(e.target.value); }}>
        <option value="">Add a species…</option>
        {catalog.map((c) => <option key={c.name} value={c.name}>{c.name} · {c.type}</option>)}
      </select>
    </div>
  );
}
