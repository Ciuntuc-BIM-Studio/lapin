/**
 * Hardscapes (site grading T4, docs/plan-site-grading.md; forum 230 §3): the tool that draws one on an outline (arcs, a
 * rectangle, a circle), with its type and whether it is cut into the terrain; the slope arrow; the inspector's section (type,
 * top level / sloped / by its corners, terrain, kerb along all or chosen edges with drops); the kerb's own section; the plan
 * grips of its outline and its top's height grips in 3D.
 */

import { create } from 'zustand';
import { ArrowDownRight, Grid2x2 } from 'lucide-react';
import type { Grip, Grip3D, GripView, RefLine, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ofClass } from '../../engine/model';
import { promptText } from '../../shell/prompts';
import { NumberField } from '../../shell/cells';
import { PlainShapeBar, toggleOutlineArcs } from '../../viewport/outlineShape';
import { curvedOutlineGrips, outlineRef, toPairs } from '../../viewport/gripKit';
import { arcsOf, fromPairs } from '../../viewport/contour';
import { formatLength, lengthText, levelUnit, parseLength, parseLevel } from '../../engine/unitsStore';
import { MaterialPicker } from '../core/MaterialPicker';
import { Mm } from './SiteSections';
import { HARDSCAPE, HARDSCAPE_TYPE, KERB, KERB_SHAPES, TOP_MODES, cornersOf, gradeText, hardscapeAt, kerbDrops, kerbEdges, parseGrade, raisedCorners, topGrips, withEdge, type TopMode } from './hardscape';

interface Preset { key: string; name: string; pavement: string; layers: { material: string; thickness: number }[] }

/** The ready-made types the engine ships (indicative). */
export const usePresets = (): Preset[] => (useSession((s) => s.info?.catalogs['civil.hardscapeTypes']) as unknown as Preset[] | undefined) ?? [];

/** The type the types dialog opens on. */
export const useHardscapeTypeEdit = create<{ id: string | null }>(() => ({ id: null }));

/** What the Hardscape tool lays: a type of the project's (its id) or a ready-made one (`preset:key`), cut into the terrain or alone, with a kerb all round or not. */
export const useHardscapeTool = create<{ type: string; standalone: boolean; kerb: boolean }>(() => ({ type: 'preset:pavers', standalone: false, kerb: false }));

/** The type picker: the project's types, then the ready-made ones not in it yet. */
function TypeOptions() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const types = ofClass({ nodes, relations }, HARDSCAPE_TYPE);
  const presets = usePresets().filter((p) => !types.some((t) => t.props.preset === p.key));
  return (
    <>
      {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
      {presets.length > 0 && <optgroup label="Ready-made (indicative)">
        {presets.map((p) => <option key={p.key} value={`preset:${p.key}`}>{p.name}</option>)}
      </optgroup>}
    </>
  );
}

const typeArgs = (v: string): Record<string, unknown> => (v.startsWith('preset:') ? { type: v.slice(7) } : { typeId: v });

function HardscapeBar() {
  const s = useHardscapeTool();
  return (
    <>
      <PlainShapeBar />
      <select className="cad-layer" value={s.type} data-testid="hardscape-type" title="The hardscape's type: its layers, top down" onChange={(e) => useHardscapeTool.setState({ type: e.target.value })}>
        <TypeOptions />
      </select>
      <select className={`cad-layer${s.standalone ? ' on' : ''}`} value={s.standalone ? 'alone' : 'terrain'} data-testid="hardscape-host" title="Cut into the terrain under it, its edges the ground's breaklines; or standing alone"
        onChange={(e) => useHardscapeTool.setState({ standalone: e.target.value === 'alone' })}>
        <option value="terrain">Cut into the terrain</option>
        <option value="alone">Standalone</option>
      </select>
      <label className="cad-layer" title="A kerb all round its edges (choose the edges and drops in Properties)">
        <input type="checkbox" checked={s.kerb} data-testid="hardscape-kerb" onChange={(e) => useHardscapeTool.setState({ kerb: e.target.checked })} /> Kerb
      </label>
    </>
  );
}

/** A hardscape on an outline (Space: arcs), level at the ground's height in its middle; its type, top and kerb set in Properties. */
export const hardscape: ToolDef = {
  id: 'civil.hardscape', label: 'Hardscape', icon: Grid2x2, shortcut: 'HS',
  gesture: { kind: 'polygon', shapes: true },
  options: HardscapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs }, ctx) => {
    if (points.length < 3) return;
    const s = useHardscapeTool.getState();
    await ctx.execute('civil.addHardscape', {
      storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}), ...typeArgs(s.type),
      ...(s.standalone ? { standalone: true } : {}), ...(s.kerb ? { kerb: { all: true } } : {}),
    });
  },
};

/** A hardscape's slope drawn as an arrow, from its high end to its low end, over it; the grade asked. */
export const hardscapeSlope: ToolDef = {
  id: 'civil.hardscapeSlope', label: 'Slope arrow', icon: ArrowDownRight,
  gesture: { kind: 'twoPoints', hint: 'click the high end, then the low end, over a hardscape · Esc cancels' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const [a, b] = points;
    const h = hardscapeAt(useSession.getState().nodes.values(), a, useUi.getState().selected);
    if (!h) { notify('Start the arrow on a hardscape.'); return; }
    const answer = promptText('Its grade (2 %, 1:50):', '2 %');
    if (answer == null) return;
    const grade = parseGrade(answer);
    if (grade == null) { notify('That grade does not read: type 2 %, 1:50 or 0.02.'); return; }
    await ctx.execute('civil.updateHardscape', { id: h.id, arrow: { from: [a.x, a.y], to: [b.x, b.y], grade } });
  },
};

/** The plan grips of its outline (its corners, edges, arcs), in a plan only: in 3D they would stand on the storey's level, not on its top. */
export function hardscapeGrips(n: NodeDto, view?: GripView): Grip[] {
  if (!view?.view) return [];
  return curvedOutlineGrips(n.id, fromPairs(n.props.contour), n.props.arcs, (pts, arcs) => ({ command: 'civil.updateHardscape', args: { id: n.id, points: toPairs(pts), arcs } }), 'civil');
}

export function hardscapeRefs(n: NodeDto, view?: GripView): RefLine[] {
  const pts = fromPairs(n.props.contour);
  return view?.view && pts.length >= 3 ? [outlineRef(pts, arcsOf(n.props.arcs, pts.length))] : [];
}

/** Its top's height grips in 3D: one per corner by its corners, else one for its elevation; dragged, or clicked to type it. */
export function hardscapeGrips3d(n: NodeDto): Grip3D[] {
  return topGrips(n).map((g) => ({
    key: g.key, kind: 'height' as const, at: g.at, dir: { x: 0, y: 0, z: 1 },
    title: g.corner == null ? 'The hardscape\'s elevation: drag up or down, or click to type it' : 'The corner\'s height: drag up or down, or click to type it',
    step: (by: number) => g.corner == null
      ? { command: 'civil.updateHardscape', args: { id: n.id, elevation: Math.round((g.at.z + by) * 1e6) / 1e6 } }
      : { command: 'civil.updateHardscape', args: { id: n.id, cornerZ: raisedCorners(n, g.corner, by) } },
    click: () => {
      const answer = promptText('The elevation ({unit}, ±0.00 = 0):', formatLength(g.at.z, levelUnit()));
      const z = answer == null ? null : parseLevel(answer);
      if (z == null || !Number.isFinite(z)) return null;
      return g.corner == null ? { command: 'civil.updateHardscape', args: { id: n.id, elevation: z } }
        : { command: 'civil.updateHardscape', args: { id: n.id, cornerZ: raisedCorners(n, g.corner, z - g.at.z) } };
    },
  }));
}

/** A grade's field: what it shows, read when it is committed. */
function GradeField({ value, onCommit }: { value: number; onCommit: (g: number) => void }) {
  return (
    <input type="text" key={value} defaultValue={gradeText(value)} data-testid="hardscape-grade" style={{ width: '6em' }} aria-label="Slope"
      onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
      onBlur={(e) => { const g = parseGrade(e.target.value); if (g == null) { notify('That grade does not read: type 2 %, 1:50 or 0.02.'); e.target.value = gradeText(value); return; } if (Math.abs(g - value) > 1e-9) onCommit(g); }} />
  );
}

/** The hardscape in Properties: its type, its top, its terrain, its kerb. */
export function HardscapeSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const set = (args: Record<string, unknown>) => void execute('civil.updateHardscape', { id: node.id, ...args });
  const mode = (typeof node.props.topMode === 'string' ? node.props.topMode : 'level') as TopMode;
  const corners = cornersOf(node.props.contour);
  const topZ = Array.isArray(node.props.topZ) ? (node.props.topZ as number[]) : [];
  const own = Array.isArray(node.props.cornerZ) ? (node.props.cornerZ as (number | null)[]) : [];
  const layers = Array.isArray(node.props.layers) ? (node.props.layers as { material: string; thickness: number }[]) : [];
  const terrain = typeof node.props.terrainId === 'string' ? nodes.get(node.props.terrainId) : undefined;
  const typeId = typeof node.props.typeId === 'string' ? node.props.typeId : '';
  const grade = typeof node.props.grade === 'number' ? node.props.grade : 0;
  const direction = typeof node.props.slopeDirection === 'number' ? node.props.slopeDirection : 0;
  return (
    <div className="layers" data-testid="hardscape-section">
      <dl className="fields">
        <dt>Type</dt>
        <dd><select value={typeId} disabled={!node.editable} data-testid="hardscape-section-type" onChange={(e) => set(typeArgs(e.target.value))}><TypeOptions /></select>
          {' '}<button type="button" className="link small" onClick={() => { useHardscapeTypeEdit.setState({ id: typeId || null }); useUi.setState({ dialog: 'hardscapeTypes' }); }}>Edit types…</button></dd>
        <dt>Build-up</dt>
        <dd className="small">{layers.map((l, i) => <div key={i}><span>{l.material}</span> <span className="num muted">{formatLength(l.thickness)}</span></div>)}</dd>
        <dt>Top surface</dt>
        <dd><select value={mode} disabled={!node.editable} data-testid="hardscape-top" onChange={(e) => set({ topMode: e.target.value })}>
          {TOP_MODES.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
        </select></dd>
        {mode !== 'corners' && <><dt>{mode === 'slope' ? 'Elevation at its origin' : 'Elevation'}</dt><dd><Mm value={node.props.elevation} onCommit={(v) => set({ elevation: v })} /></dd></>}
        {mode === 'slope' && <>
          <dt>Slope</dt><dd><GradeField value={grade} onCommit={(g) => set({ grade: g })} /></dd>
          <dt>Falls towards</dt><dd><NumberField value={Math.round(direction * 100) / 100} min={-360} max={360} onCommit={(v) => { if (v != null) set({ direction: v }); }} /> ° <span className="muted small">from +x, anticlockwise</span></dd>
        </>}
        {mode === 'corners' && <>
          <dt>Corners</dt>
          <dd>
            <table className="layer-edit small" data-testid="hardscape-corners"><tbody>
              {corners.map((_, i) => (
                <tr key={i}><td className="muted">{i + 1}</td>
                  <td><input type="text" key={`${i}:${topZ[i]}`} defaultValue={topZ[i] != null ? formatLength(topZ[i], levelUnit()) : ''} data-testid={`hardscape-z${i}`} style={{ width: '6em', color: own[i] != null ? undefined : 'var(--muted)' }} aria-label="Corner elevation"
                    onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
                    onBlur={(e) => { const z = parseLevel(e.target.value); if (z == null || !Number.isFinite(z) || Math.abs(z - (topZ[i] ?? 0)) < 1e-9) return; set({ cornerZ: topZ.map((v, k) => (k === i ? z : v)) }); }} /></td></tr>
              ))}
            </tbody></table>
          </dd>
        </>}
        {typeof node.props.topNote === 'string' && <><dt /><dd className="muted small">{node.props.topNote}</dd></>}
        <dt>Terrain</dt>
        <dd>{terrain ? <>cut into {terrain.name} <button type="button" className="link small" disabled={!node.editable} onClick={() => set({ standalone: true })}>standalone</button></>
          : <><span className="muted">standalone</span>{ofClass(useSession.getState(), 'IfcGeographicElement').some((t) => t.props.predefinedType === 'TERRAIN') && <> <button type="button" className="link small" disabled={!node.editable}
            onClick={() => { const t = ofClass(useSession.getState(), 'IfcGeographicElement').find((x) => x.props.predefinedType === 'TERRAIN'); if (t) set({ terrainId: t.id }); }}>cut into the terrain</button></>}</>}</dd>
        <dt>Area</dt><dd><span className="num">{typeof node.props.area === 'number' ? `${node.props.area.toFixed(2)} m²` : ''}</span></dd>
      </dl>
      <KerbFields host={node} />
    </div>
  );
}

/** The kerb of a hardscape: on or off, its shape or a profile of the project's, its sizes and material, each edge on or off and dropped. */
export function KerbFields({ host }: { host: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const kerb = typeof host.props.kerbId === 'string' ? nodes.get(host.props.kerbId) : undefined;
  const set = (args: Record<string, unknown>) => void execute('civil.setHardscapeKerb', { id: host.id, ...args });
  const n = cornersOf(host.props.contour).length;
  const lengths = Array.isArray(host.props.edgeLengths) ? (host.props.edgeLengths as number[]) : [];
  const profiles = [...nodes.values()].filter((p) => p.type === 'LapinProfile' && p.props.use === 'kerb');
  if (!kerb) return (
    <dl className="fields"><dt>Kerb</dt><dd><input type="checkbox" checked={false} disabled={!host.editable} data-testid="hardscape-kerb-on" onChange={() => set({ all: true })} /> <span className="muted small">none</span></dd></dl>
  );
  const edges = kerbEdges(n, kerb.props.edges);
  const drops = kerbDrops(n, kerb.props.drops);
  const material = typeof kerb.props.material === 'string' ? nodes.get(kerb.props.material)?.name ?? '' : '';
  const shape = typeof kerb.props.profileId === 'string' ? `profile:${kerb.props.profileId}` : typeof kerb.props.shape === 'string' ? kerb.props.shape : 'upright';
  return (
    <>
    <dl className="fields" data-testid="kerb-fields">
      <dt>Kerb</dt><dd><input type="checkbox" checked disabled={!host.editable} data-testid="hardscape-kerb-on" onChange={() => set({ on: false })} /></dd>
      <dt>Shape</dt>
      <dd><select value={shape} data-testid="kerb-shape" onChange={(e) => set(e.target.value.startsWith('profile:') ? { profileId: e.target.value.slice(8) } : { profileId: '', shape: e.target.value })}>
        {KERB_SHAPES.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
        {profiles.length > 0 && <optgroup label="Profiles">{profiles.map((p) => <option key={p.id} value={`profile:${p.id}`}>{p.name}</option>)}</optgroup>}
      </select></dd>
      <dt title="Its height above the paving">Above the paving</dt><dd><Mm value={kerb.props.height} fallback={0.12} min={0} onCommit={(v) => set({ height: v })} /></dd>
      <dt>Width</dt><dd><Mm value={kerb.props.width} fallback={0.15} min={20} onCommit={(v) => set({ width: v })} /></dd>
      <dt title="How deep it goes below the paving's top">Below the paving</dt><dd><Mm value={kerb.props.depth} fallback={0.25} min={0} onCommit={(v) => set({ depth: v })} /></dd>
      <dt>Material</dt><dd><MaterialPicker value={material} onChange={(m) => { if (m && m !== material) set({ material: m }); }} /></dd>
    </dl>
    <p className="eyebrow" style={{ margin: '8px 0 2px' }}>Kerb edges</p>
        <table className="kerb-edges small" style={{ width: '100%' }} data-testid="kerb-edges">
          <thead><tr><th>Edge</th><th>Kerb</th><th title="Where the kerb drops (a crossing): its reveal above the paving on that edge; empty: its full height">Dropped to</th></tr></thead>
          <tbody>
            {edges.map((on, i) => (
              <tr key={i}>
                <td className="muted">{i + 1} <span className="num">{lengths[i] != null ? lengthText(lengths[i]) : ''}</span></td>
                <td><input type="checkbox" checked={on} data-testid={`kerb-edge${i}`} onChange={(e) => set({ edges: withEdge(edges, i, e.target.checked) })} /></td>
                <td><input type="text" key={`${i}:${drops[i]}`} defaultValue={drops[i] != null ? formatLength(drops[i]!) : ''} placeholder="—" disabled={!on} data-testid={`kerb-drop${i}`} style={{ width: '5em' }} aria-label="Reveal where the kerb drops"
                  onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
                  onBlur={(e) => { const t = e.target.value.trim(); const v = t === '' ? null : parseLength(t); if (t !== '' && (v == null || !Number.isFinite(v))) return; if (v === drops[i]) return; set({ drop: { edge: i, reveal: v } }); }} /></td>
              </tr>
            ))}
          </tbody>
        </table>
    </>
  );
}

/** A kerb selected: its hardscape's kerb fields, and the way back to the hardscape. */
export function KerbSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const host = typeof node.props.hardscape === 'string' ? nodes.get(node.props.hardscape) : undefined;
  if (!host) return <p className="muted small">Its hardscape is not in the project.</p>;
  return (
    <div className="layers">
      <dl className="fields"><dt>Hardscape</dt><dd>{host.name} <button type="button" className="link small" onClick={() => useUi.setState({ selected: [host.id] })}>select</button></dd></dl>
      <KerbFields host={host} />
    </div>
  );
}

/** Create similar: the Hardscape tool with the element's type. */
export const similarHardscape = (n: NodeDto) => {
  if (typeof n.props.typeId === 'string') useHardscapeTool.setState({ type: n.props.typeId });
  return n;
};

export { HARDSCAPE, KERB };
