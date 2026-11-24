/**
 * The site's elements in the inspector, each in its own terms: an earthwork
 * (a platform's level, a ditch's section), a path's courses and kerbs, a
 * retaining wall's section and ground sides, a planting's mix and density.
 * Lengths in the project's unit.
 */

import { ProfileButton } from './ProfileButton';
import { useEffect, useMemo, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { notify, useUi } from '../../app/store';
import { isDitch } from './prefs';
import { t, useLang } from '../../i18n';
import { groupedSpecies, speciesName, type Species } from './species';
import { VEGETATION_MODES } from '../core/viewStyles';
import { formatLength, lengthText } from '../../engine/unitsStore';
import { LengthField, NumberField } from '../../shell/cells';
import { MaterialPicker } from '../core/MaterialPicker';
import { segmentRows } from './retainingWall';
import { openPortal } from './portalTools';
import { linkToAxis, openTunnel, portalAtEnd, relinkTunnel, unlinkTunnel } from './tunnelTools';
import { axisSourceOf, TUNNEL_PRESET_LABELS } from './tunnel';
import { pathTypeArgs, TypeOptions, useHardscapeTypeEdit } from './hardscapeTools';

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
        <ProfileButton id={node.id} />
        <dt>Top width</dt><dd><span className="num">{lengthText((typeof node.props.bottomWidth === 'number' ? node.props.bottomWidth : 0.4) + 2 * slope * (typeof node.props.depth === 'number' ? node.props.depth : 0.5))}</span></dd>
      </dl>
    );
  }
  const cutSlope = typeof node.props.cutSlope === 'number' ? node.props.cutSlope : 0;
  const fillSlope = typeof node.props.fillSlope === 'number' ? node.props.fillSlope : 0;
  const set = (args: Record<string, unknown>) => void execute('civil.updatePlatform', { id: node.id, ...args });
  return (
    <dl className="fields">
      <dt>Elevation</dt><dd><Mm value={node.props.elevation} onCommit={(v) => set({ elevation: v })} /></dd>
      <dt>Cut slope</dt><dd>1 : <NumberField value={cutSlope} min={0} max={10} onCommit={(v) => { if (v != null) set({ cutSlope: v }); }} /></dd>
      <dt>Fill slope</dt><dd>1 : <NumberField value={fillSlope} min={0} max={10} onCommit={(v) => { if (v != null) set({ fillSlope: v }); }} /></dd>
      {(cutSlope > 0 || fillSlope > 0) && <><dt /><dd><button type="button" className="btn compact" data-testid="platform-slopes-to-grading" disabled={!node.editable}
        title="Its slopes become a grading: a slope to the ground from its outline, at the same 1:N, which you can then change on its own"
        onClick={() => void execute('civil.platformSlopesToGrading', { id: node.id }).then((r) => {
          const made = r as { id?: string; notes?: string[] } | null;
          if (made?.id) { useUi.setState({ selected: [made.id] }); if (made.notes?.length) notify(made.notes[0]); }
        })}>Convert slopes to grading</button></dd></>}
    </dl>
  );
}

export function PathSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const set = (args: Record<string, unknown>) => void execute('civil.updatePath', { id: node.id, ...args });
  const mat = (key: string) => (typeof node.props[key] === 'string' ? nodes.get(node.props[key] as string)?.name ?? '' : '');
  // Its build-up from a hardscape type (shared, edited in one place) or its own two courses (docs/plan-site-grading.md, T4).
  const typeId = typeof node.props.hardscapeTypeId === 'string' ? node.props.hardscapeTypeId : '';
  const layers = Array.isArray(node.props.hardscapeLayers) ? (node.props.hardscapeLayers as { material: string; thickness: number }[]) : [];
  return (
    <dl className="fields">
      <Configure />
      <ProfileButton id={node.id} />
      <dt>Width</dt><dd><Mm value={node.props.width} fallback={1.5} min={200} onCommit={(v) => set({ width: v })} /></dd>
      <dt>Use a hardscape type</dt>
      <dd><input type="checkbox" checked={!!typeId} disabled={!node.editable} data-testid="path-use-type" title="Its layers from a hardscape type, shared and edited in one place; off, its own surface and base courses"
        onChange={(e) => set({ useHardscapeType: e.target.checked })} /></dd>
      {typeId ? <>
        <dt>Type</dt>
        <dd><select value={typeId} disabled={!node.editable} data-testid="path-type" onChange={(e) => set(pathTypeArgs(e.target.value))}><TypeOptions /></select>
          {' '}<button type="button" className="link small" onClick={() => { useHardscapeTypeEdit.setState({ id: typeId }); useUi.setState({ dialog: 'hardscapeTypes' }); }}>Edit types…</button></dd>
        <dt>Build-up</dt>
        <dd className="small" data-testid="path-layers">{layers.map((l, i) => <div key={i}><span>{l.material}</span> <span className="num muted">{formatLength(l.thickness)}</span></div>)}</dd>
      </> : <>
      <dt>Surface course</dt><dd><Mm value={node.props.surfaceThickness} fallback={0.06} min={0} onCommit={(v) => set({ surfaceThickness: v })} /></dd>
      <dt>Surface material</dt><dd><MaterialPicker value={mat('surfaceMaterial')} onChange={(m) => { if (m && m !== mat('surfaceMaterial')) set({ surfaceMaterial: m }); }} /></dd>
      <dt>Base course</dt><dd><Mm value={node.props.baseThickness} fallback={0.2} min={0} onCommit={(v) => set({ baseThickness: v })} /></dd>
      <dt>Base material</dt><dd><MaterialPicker value={mat('baseMaterial')} onChange={(m) => { if (m && m !== mat('baseMaterial')) set({ baseMaterial: m }); }} /></dd>
      </>}
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
    <>
    <dl className="fields">
      <Configure />
      <ProfileButton id={node.id} />
      {node.props.portal != null && typeof node.props.portal === 'object' && (
        <><dt /><dd><button type="button" className="btn compact" data-action="portal-configure" title="its preset, wings, section, free ends and IFC" onClick={() => openPortal(node)}>Portal configurator…</button></dd></>
      )}
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
      <BatterFields node={node} />
    </dl>
    <RetainingWallSegments node={node} />
    </>
  );
}

const num = (v: unknown) => (typeof v === 'number' ? v : null);

/**
 * A retaining wall's batters (n:1, empty upright), the back by its batter or by its thickness at the base, the footing's toe and heel
 * (empty: from the footing width) and the key under it (docs/plan-wing-walls.md, S1).
 */
function BatterFields({ node }: { node: NodeDto }) {
  const set = (args: Record<string, unknown>) => void execute('civil.updateRetainingWall', { id: node.id, ...args });
  const byBase = typeof node.props.baseThickness === 'number';
  const thickness = typeof node.props.thickness === 'number' ? node.props.thickness : 0.3;
  return (
    <>
      <dt>Front batter</dt><dd><NumberField value={num(node.props.frontBatter)} min={0} blank placeholder="upright" suffix=":1" onCommit={(v) => set({ frontBatter: v })} /></dd>
      <dt>Back face</dt>
      <dd><select value={byBase ? 'base' : 'batter'} onChange={(e) => set(e.target.value === 'base' ? { baseThickness: thickness + 0.2, backBatter: null } : { baseThickness: null })}>
        <option value="batter">by its batter</option><option value="base">by its thickness at the base</option></select></dd>
      {byBase
        ? <><dt>Thickness at the base</dt><dd><LengthField metres={num(node.props.baseThickness)} min={0.05} blank onCommit={(m) => set({ baseThickness: m })} /></dd></>
        : <><dt>Back batter</dt><dd><NumberField value={num(node.props.backBatter)} min={0} blank placeholder="upright" suffix=":1" onCommit={(v) => set({ backBatter: v })} /></dd></>}
      <dt>Toe</dt><dd><LengthField metres={num(node.props.toe)} min={0} blank placeholder="auto" title="from the footing width" onCommit={(m) => set({ toe: m })} /></dd>
      <dt>Heel</dt><dd><LengthField metres={num(node.props.heel)} min={0} blank placeholder="auto" title="from the footing width" onCommit={(m) => set({ heel: m })} /></dd>
      <dt>Key width</dt><dd><LengthField metres={num(node.props.keyWidth)} min={0} blank placeholder="no key" onCommit={(m) => set({ keyWidth: m })} /></dd>
      <dt>Key depth</dt><dd><LengthField metres={num(node.props.keyDepth)} min={0} blank placeholder="no key" onCommit={(m) => set({ keyDepth: m })} /></dd>
      {num(node.props.keyWidth) != null && num(node.props.keyDepth) != null
        && <><dt>Key from the toe</dt><dd><LengthField metres={num(node.props.keyAt)} min={0} blank placeholder="centred" onCommit={(m) => set({ keyAt: m })} /></dd></>}
    </>
  );
}

/**
 * The wall's segments, one by one: its length and turn (the path's), and what it has of its own (empty: the wall's): its crown at
 * its start and its end, its crown thickness, its batters, its footing level, and its warp (the face from one batter at its start
 * to another at its end, the warped wing).
 */
export function RetainingWallSegments({ node }: { node: NodeDto }) {
  const rows = segmentRows(node.props.path, node.props.segments);
  const [open, setOpen] = useState<number | null>(null);
  const set = (index: number, args: Record<string, unknown>) => void execute('civil.setRetainingWallSegment', { id: node.id, index, ...args });
  if (rows.length === 0) return null;
  return (
    <div className="layers" data-testid="wall-segments">
      <p className="eyebrow">Segments</p>
      {rows.map((r) => {
        const own = (k: string) => num(r.own[k]);
        return (
          <div key={r.index}>
            <button type="button" className="link small" onClick={() => setOpen(open === r.index ? null : r.index)}>
              <span data-no-i18n>{open === r.index ? '▾' : '▸'} </span><span>Segment</span>
              <span data-no-i18n> {r.index + 1} · {lengthText(r.length)} · {Math.round(r.turn * 10) / 10}°</span>
              {r.own.warp === true && <> · <span>warped</span></>}
            </button>
            {open === r.index && (
              <dl className="fields">
                <dt>Length</dt><dd><LengthField metres={r.length} min={0.05} onCommit={(m) => { if (m != null) set(r.index, { length: m }); }} /></dd>
                <dt>{r.index === 0 ? 'Direction from east' : 'Turn from the one before'}</dt>
                <dd><NumberField value={Math.round(r.turn * 100) / 100} min={-179} max={179} suffix="°" onCommit={(v) => { if (v != null) set(r.index, { angle: v }); }} /></dd>
                <dt>Crown at its start</dt><dd><LengthField metres={own('topStart')} blank placeholder="the wall's" onCommit={(m) => set(r.index, { topStart: m })} /></dd>
                <dt>Crown at its end</dt><dd><LengthField metres={own('topEnd')} blank placeholder="the wall's" onCommit={(m) => set(r.index, { topEnd: m })} /></dd>
                <dt>Thickness at the crown</dt><dd><LengthField metres={own('thickness')} min={0.05} blank placeholder="the wall's" onCommit={(m) => set(r.index, { thickness: m })} /></dd>
                <dt>Front batter</dt><dd><NumberField value={own('frontBatter')} min={0} blank placeholder="the wall's" suffix=":1" onCommit={(v) => set(r.index, { frontBatter: v })} /></dd>
                <dt>Back batter</dt><dd><NumberField value={own('backBatter')} min={0} blank placeholder="the wall's" suffix=":1" onCommit={(v) => set(r.index, { backBatter: v })} /></dd>
                <dt>Footing level</dt><dd><LengthField metres={own('footingLevel')} blank placeholder="the wall's" onCommit={(m) => set(r.index, { footingLevel: m })} /></dd>
                <dt>Warped</dt>
                <dd><label><input type="checkbox" checked={r.own.warp === true} onChange={(e) => set(r.index, { warp: e.target.checked })} /> <span className="muted small">the face turns to another batter at the end</span></label></dd>
                {r.own.warp === true && <>
                  <dt>Front batter at its end</dt><dd><NumberField value={own('frontBatterEnd')} min={0} blank placeholder="as at the start" suffix=":1" onCommit={(v) => set(r.index, { frontBatterEnd: v })} /></dd>
                  <dt>Back batter at its end</dt><dd><NumberField value={own('backBatterEnd')} min={0} blank placeholder="as at the start" suffix=":1" onCommit={(v) => set(r.index, { backBatterEnd: v })} /></dd>
                </>}
              </dl>
            )}
          </div>
        );
      })}
    </div>
  );
}

/**
 * A quick switch of how the 3D view ({3D}) draws the plants (decision 818): coloured or as a maquette, the {3D}'s own setting
 * (its view settings show it, and every other view sets its own there).
 */
function Vegetation3d() {
  const nodes = useSession((s) => s.nodes);
  const [mode, setMode] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    void query<{ resolved: { vegetation?: string } }>('view.style', { id: '3d' }).then((r) => { if (live && r) setMode(r.resolved.vegetation ?? 'coloured'); });
    return () => { live = false; };
  }, [nodes]);
  if (mode == null) return null;
  return (
    <>
      <dt title="How the 3D view draws the plants: each species in its colour, or as an architectural model's, neutral and faceted. Each view sets its own in its view settings.">Vegetation in 3D</dt>
      <dd>
        <select data-testid="planting-vegetation-3d" value={mode} onChange={(e) => void execute('view.setStyle', { id: '3d', vegetation: e.target.value })}>
          {VEGETATION_MODES.map(([k, l]) => <option key={k} value={k}>{l}</option>)}
        </select>
      </dd>
    </>
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
      <dl className="fields">
        <Vegetation3d />
      </dl>
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

/**
 * A tunnel lining (docs/plan-wing-walls.md, S4): its configurator (section, thicknesses, blocks, IFC), the corridor configurator
 * (its axis's curves, its red line, sections at stations), a portal at either end, and where its axis came from.
 */
export function TunnelLiningSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  useLang();
  const spec = (node.props.lining as Record<string, unknown> | undefined) ?? {};
  const preset = typeof spec.profileId === 'string' && spec.profileId ? t('Drawn section') : t(TUNNEL_PRESET_LABELS[String(spec.preset ?? 'horseshoe')] ?? String(spec.preset ?? ''));
  const source = axisSourceOf(node.props);
  const host = source ? nodes.get(source.id) : undefined;
  return (
    <dl className="fields">
      <dt>Section</dt><dd><span data-no-i18n>{preset}</span></dd>
      <dt /><dd><button type="button" className="btn compact" data-action="tunnel-configure" title="its section, thicknesses, blocks and IFC" onClick={() => openTunnel(node)}>Tunnel configurator…</button></dd>
      <Configure />
      {source && <><dt>Axis from</dt><dd><span data-no-i18n>{host?.name ?? '—'} · {lengthText(source.from)} → {lengthText(source.to)}</span></dd></>}
      <dt>Axis</dt>
      <dd data-testid="tunnel-link">
        {source?.linked
          ? <><span title="When that element's axis or red line changes, the lining is rebuilt along it">follows it</span>{' '}
            <button type="button" className="btn compact" data-action="tunnel-unlink" disabled={!node.editable} title="Keep the axis as it is now, a copy that no longer follows its source" onClick={() => void unlinkTunnel(node.id)}>Unlink</button></>
          : <><span className="muted">{source ? 'a copy' : 'its own'}</span>{' '}
            {source && host && <button type="button" className="btn compact" data-action="tunnel-relink" disabled={!node.editable} title="Follow that element again between the same stations" onClick={() => void relinkTunnel(node.id, source)}>Link again</button>}</>}
        {' '}<button type="button" className="btn compact" data-action="tunnel-link-axis" disabled={!node.editable} title="Click a road, a path, a wall or a line where the lining starts, then where it ends; it follows that element from then on" onClick={() => linkToAxis(node.id)}>Link to an axis…</button>
      </dd>
      {source?.note && <><dt /><dd className="muted small" data-testid="tunnel-link-note">{source.note}</dd></>}
      <dt>Portal</dt>
      <dd className="row" style={{ gap: 4 }}>
        <button type="button" className="btn compact" data-action="tunnel-portal-start" title="A portal at the lining's start, its face across the axis, the lining's opening through it" onClick={() => void portalAtEnd(node.id, 'start')}>At the start</button>
        <button type="button" className="btn compact" data-action="tunnel-portal-end" title="A portal at the lining's end, its face across the axis, the lining's opening through it" onClick={() => void portalAtEnd(node.id, 'end')}>At the end</button>
      </dd>
    </dl>
  );
}
