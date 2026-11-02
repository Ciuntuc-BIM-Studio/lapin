/**
 * The site's elements in the inspector, each in its own terms: an earthwork
 * (a platform's level, a ditch's section), a path's courses and kerbs, a
 * retaining wall's section and ground sides, a planting's mix and density.
 * Lengths in the project's unit.
 */

import { useMemo } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { isDitch } from './prefs';
import { t, useLang } from '../../i18n';
import { groupedSpecies, speciesName, type Species } from './species';
import { lengthText } from '../../engine/unitsStore';
import { LengthField, NumberField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';

/** Opens the corridor configurator on the element selected. */
export function Configure() {
  return <><dt /><dd><button type="button" className="btn compact" title="section, profile, stations" onClick={() => useUi.setState({ dialog: 'corridor' })}>Configurator…</button></dd></>;
}

/**
 * A length (the shared field: the project's unit, one commit on blur or
 * Enter; empty puts the value back). `value` in metres, the fallback shown
 * when it has none; `min` in mm, as the site's sections (and the railway's)
 * give it.
 */
export function Mm({ value, fallback = 0, onCommit, min }: { value: unknown; fallback?: number; onCommit: (m: number) => void; min?: number }) {
  return <LengthField metres={typeof value === 'number' ? value : fallback} min={min != null ? min / 1000 : undefined} onCommit={(m) => { if (m != null) onCommit(m); }} />;
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
        <dt>Side slope</dt><dd>1 : <NumberField value={slope} min={0} max={10} onCommit={(v) => { if (v != null) set({ slope: v }); }} /></dd>
        <Configure />
        <dt>Top width</dt><dd><span className="num">{lengthText((typeof node.props.bottomWidth === 'number' ? node.props.bottomWidth : 0.4) + 2 * slope * (typeof node.props.depth === 'number' ? node.props.depth : 0.5))}</span></dd>
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
      <dt>Surface material</dt><dd><MaterialPicker value={mat('surfaceMaterial')} onChange={(m) => { if (m && m !== mat('surfaceMaterial')) set({ surfaceMaterial: m }); }} /></dd>
      <dt>Base course</dt><dd><Mm value={node.props.baseThickness} fallback={0.2} min={0} onCommit={(v) => set({ baseThickness: v })} /></dd>
      <dt>Base material</dt><dd><MaterialPicker value={mat('baseMaterial')} onChange={(m) => { if (m && m !== mat('baseMaterial')) set({ baseMaterial: m }); }} /></dd>
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

export function PlantingSection({ node }: { node: NodeDto }) {
  const info = useSession((s) => s.info);
  // Names and the picker's groups follow the language (an option's text and a group's label are left by the translator).
  const lang = useLang((s) => s.lang);
  const catalog = (info?.catalogs['civil.plants'] as unknown as Species[] | undefined) ?? [];
  const species = useMemo(() => (Array.isArray(node.props.species) ? (node.props.species as unknown as Species[]) : []), [node.props.species]);
  const zone = Array.isArray(node.props.contour);
  const set = (args: Record<string, unknown>) => void execute('civil.updatePlanting', { id: node.id, ...args });
  const setSpecies = (next: Species[]) => set({ species: next });
  // An old project's species has no Latin or Romanian name of its own: the library's, by its name.
  const named = (s: Species): Species => ({ ...catalog.find((c) => c.name === s.name), ...s });
  const add = (name: string) => { const c = catalog.find((s) => s.name === name); if (c) setSpecies([...species, { ...c, share: 1 }]); };
  return (
    <div className="layers">
      {zone && <dl className="fields">
        <dt>Plants per 100 m²</dt><dd><NumberField value={typeof node.props.density === 'number' ? node.props.density : 3} min={0.1} onCommit={(v) => { if (v != null) set({ density: v }); }} /></dd>
        <dt>Seed</dt><dd><NumberField value={typeof node.props.seed === 'number' ? node.props.seed : 1} integer onCommit={(v) => { if (v != null) set({ seed: v }); }} /> <button type="button" className="link small" onClick={() => set({ seed: Math.floor(Math.random() * 100000) })}>shuffle</button></dd>
      </dl>}
      <p className="eyebrow">{zone ? 'Species · share' : 'Species'}</p>
      <table className="panel-grid species">
        <tbody>
          {species.map((s, i) => (
            <tr key={i}>
              <td><span className="swatch" style={{ background: s.color }} /> <span data-no-i18n>{speciesName(named(s), lang)}</span> {named(s).latin && <i className="muted small" data-no-i18n>{named(s).latin}</i>} <span className="muted small" data-no-i18n>{Math.round(s.height * 10) / 10} m · ⌀ {Math.round(s.crown * 10) / 10} m</span></td>
              {zone && <td><NumberField value={s.share} min={0} style={{ width: 56 }} onCommit={(v) => { if (v != null) setSpecies(species.map((x, k) => (k === i ? { ...x, share: v } : x))); }} /></td>}
              <td><button type="button" className="icon" title="Remove" disabled={species.length <= 1} onClick={() => setSpecies(species.filter((_, k) => k !== i))}>×</button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <select value="" onChange={(e) => { if (e.target.value) add(e.target.value); }}>
        <option value="">Add a species…</option>
        {groupedSpecies(catalog).map(([group, list]) => (
          <optgroup key={group} label={t(group)}>
            {list.map((c) => <option key={c.name} value={c.name} data-no-i18n>{speciesName(c, lang)}{c.latin ? ` (${c.latin})` : ''}</option>)}
          </optgroup>
        ))}
      </select>
    </div>
  );
}
