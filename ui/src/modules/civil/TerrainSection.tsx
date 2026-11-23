/** A terrain in the inspector: its points, what stands in it, the brush strokes on it, its analysis with the legend, its spot levels and parcel, a new survey or a LandXML of it. */

import { useEffect, useState } from 'react';
import { FileDown, FileUp } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { SURVEY, downloadLandXml, pickText } from './survey';
import { notify, useUi } from '../../app/store';
import { redrawGrips, setPicked, useTerrainEdit } from './prefs';
import { MAX_POINT_GRIPS, outlinePointIndices, terrainControls, type PointGripMode } from './pointGrips';
import { formatLength, levelUnit, parseLevel } from '../../engine/unitsStore';

interface Legend { kind: string; area: number; bands: { key: string; label: string; color: string; area: number }[] }

const KINDS: [string, string][] = [['none', 'none'], ['slope', 'slopes'], ['cutfill', 'cut and fill'], ['elevation', 'elevations']];
const GRIP_MODES: [PointGripMode, string][] = [['all', 'all points'], ['cage', 'controls only'], ['pick', 'choose controls']];
const GRID_STEPS: [number, string][]= [[0, 'off'], [2, '2 m'], [5, '5 m'], [10, '10 m']];

export function TerrainSection({ node }: { node: NodeDto }) {
  const count = Array.isArray(node.props.points) ? node.props.points.length : 0;
  const strokes = Array.isArray(node.props.sculpts) ? node.props.sculpts.length : 0;
  const spots = Array.isArray(node.props.spots) ? node.props.spots.length : 0;
  const slopes = Array.isArray(node.props.slopes) ? node.props.slopes.length : 0;
  const subregions = Array.isArray(node.props.subregions) ? node.props.subregions.length : 0;
  const parcel = Array.isArray(node.props.parcel);
  const boundary = Array.isArray(node.props.boundary);
  const hasSnapshot = Array.isArray(node.props.existingPoints);
  const cutFillGrid = typeof node.props.cutFillGrid === 'number' ? node.props.cutFillGrid : 0;
  const analysis = typeof node.props.analysis === 'string' ? node.props.analysis : 'none';
  const editPoints = useTerrainEdit((s) => s.editPoints);
  const setEditPoints = useTerrainEdit((s) => s.setEditPoints);
  // The 3D view draws its grips when the selection changes: the same selection again shows or hides the points' grips now.
  const toggleEdit = (on: boolean) => { setEditPoints(on); useUi.setState({ selected: [...useUi.getState().selected] }); };
  // The control cage (issue #113): which points get grips, and the spacing "every … m" takes controls at.
  const gripMode = useTerrainEdit((s) => s.gripMode);
  const showOn = (m: PointGripMode) => { useTerrainEdit.getState().setGripMode(m); useUi.setState({ selected: [...useUi.getState().selected] }); };
  const controls = terrainControls(node).size;
  const [cageStep, setCageStep] = useState(10);
  // Points picked (T1): their elevation typed, absolute, all at once.
  const picked = useTerrainEdit((s) => (s.picked?.id === node.id ? s.picked.indices : null));
  const outlinePoints = node.props.outlinePoints === true;
  const relations = useSession((s) => s.relations);
  const nodes = useSession((s) => s.nodes);
  const pads = [...relations.values()].filter((r) => r.class === 'IfcRelVoidsElement' && r.relating === node.id).reduce((n, r) => n + r.related.length, 0);
  const [legend, setLegend] = useState<Legend | null>(null);
  useEffect(() => {
    if (analysis === 'none') { setLegend(null); return; }
    let live = true;
    void query<Legend>('civil.analysis', { id: node.id }).then((l) => { if (live) setLegend(l); });
    return () => { live = false; };
  }, [node.id, analysis, nodes]);
  return (
    <div className="layers">
      <dl className="fields">
        <dt>Points</dt>
        <dd>{count} <button type="button" className="link small" onClick={() => toggleEdit(!editPoints)}>{editPoints ? 'done editing 3D' : 'edit in 3D'}</button>
          {editPoints && <div className="muted small">{gripMode === 'cage'
            ? (controls > 0 ? 'Drag a control up or down: the free points around it follow smoothly.' : 'No controls yet: choose them, or take one every few metres or along the boundary.')
            : gripMode === 'pick' ? 'Click a point\'s grip to make it a control, or a free point again.'
            : count > MAX_POINT_GRIPS
              ? `Grips on the ${MAX_POINT_GRIPS} points nearest the 3D view's centre: move the view to reach the others.`
              : 'Drag a point\'s grip up or down in the 3D view, or click it to pick it and type its elevation.'}</div>}
          {editPoints && gripMode === 'all' && node.editable && <PickedPoints node={node} picked={picked ?? []} />}
        </dd>
        <dt>Control cage</dt>
        <dd data-testid="terrain-cage">
          <span>{controls}</span> <span>controls</span>
          {editPoints && <> <select value={gripMode} aria-label="Grips on" onChange={(e) => showOn(e.target.value as PointGripMode)}>
            {GRIP_MODES.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
          </select></>}
          {node.editable && <div className="row small" style={{ flexWrap: 'wrap', gap: '2px 6px', width: '100%' }}>
            <button type="button" className="link small" onClick={() => void execute('civil.setTerrainControls', { id: node.id, spacing: cageStep })}>every</button>
            <input type="number" min={0.5} step={1} value={cageStep} style={{ width: '5.5em', flex: 'none' }} aria-label="Control spacing" onChange={(e) => setCageStep(Math.max(0.5, Number(e.target.value) || 10))} /> <span>m</span>
            {' · '}<button type="button" className="link small" onClick={() => void execute('civil.setTerrainControls', { id: node.id, boundary: true })}>along the boundary</button>
            {controls > 0 && <>{' · '}<button type="button" className="link small" onClick={() => void execute('civil.setTerrainControls', { id: node.id, clear: true })}>clear</button></>}
          </div>}
        </dd>
        <dt>Platforms, ditches, paths</dt><dd>{pads}</dd>
        <dt>Brush strokes</dt>
        <dd>{strokes}{strokes > 0 && <> <button type="button" className="link small" onClick={() => void execute('civil.clearSculpt', { id: node.id, last: true })}>undo last</button> · <button type="button" className="link small" onClick={() => void execute('civil.clearSculpt', { id: node.id })}>clear</button></>}</dd>
        <dt>Spot levels</dt>
        <dd>{spots}{spots > 0 && <> <button type="button" className="link small" onClick={() => void execute('civil.removeSpot', { id: node.id, all: true })}>clear</button></>}</dd>
        <dt>Slope tags</dt>
        <dd>{slopes}{slopes > 0 && <> <button type="button" className="link small" onClick={() => void execute('civil.removeSlope', { id: node.id, all: true })}>clear</button></>}</dd>
        <dt>Subregions</dt>
        <dd>{subregions}{subregions > 0 && <> <button type="button" className="link small" onClick={() => void execute('civil.removeSubregion', { id: node.id, all: true })}>clear</button></>}</dd>
        <dt>Boundary</dt>
        <dd>
          {boundary ? <>clipped <button type="button" className="link small" onClick={() => void execute('civil.setBoundary', { id: node.id })}>remove</button>
            {node.editable && !outlinePoints && <>{' · '}<button type="button" className="link small" data-testid="terrain-outline-points" title="Make each vertex of the boundary a point of the terrain, at the ground's height there, so it has a grip and an elevation to type"
              onClick={() => void execute('civil.outlineToPoints', { id: node.id }).then((r) => { if ((r as { moved?: number } | null)?.moved) notify('The outline\'s points take the surveyed heights, and the brush strokes work on them now: the ground moved a little along the outline.'); })}>outline as points</button></>}
            {outlinePoints && <div className="muted small">Its outline's vertices are its points.</div>}</>
            : <><span className="muted">convex hull</span>{parcel && <> <button type="button" className="link small" onClick={() => void execute('civil.setBoundary', { id: node.id, fromParcel: true })}>from parcel</button></>}</>}
        </dd>
        <dt>Parcel</dt>
        <dd>{parcel ? <>drawn <button type="button" className="link small" onClick={() => void execute('civil.setParcel', { id: node.id })}>remove</button></> : <span className="muted">none</span>}</dd>
        <dt>Analysis</dt>
        <dd><select value={analysis} disabled={!node.editable} onChange={(e) => void execute('civil.updateTerrain', { id: node.id, analysis: e.target.value })}>
          {KINDS.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
        </select></dd>
        <dt>Cut/fill grid</dt>
        <dd><select value={cutFillGrid} disabled={!node.editable} onChange={(e) => void execute('civil.updateTerrain', { id: node.id, cutFillGrid: Number(e.target.value) })}>
          {GRID_STEPS.map(([v, label]) => <option key={v} value={v}>{label}</option>)}
        </select></dd>
        <dt>Graded region</dt>
        <dd>
          {hasSnapshot
            ? <button type="button" className="link small" onClick={() => void execute('civil.snapshotExisting', { id: node.id, reset: true })}>reset to survey</button>
            : <button type="button" className="link small" onClick={() => void execute('civil.snapshotExisting', { id: node.id })}>snapshot existing</button>}
        </dd>
      </dl>
      {legend && (
        <ul className="stair-checks legend">
          {legend.bands.map((b) => (
            <li key={b.key} className="ok">
              <span><span className="swatch" style={{ background: b.color }} /> {b.label}</span>
              <span className="num">{b.area.toFixed(1)} m²</span>
              <span className="muted small">{legend.area > 0 ? `${Math.round((b.area / legend.area) * 100)} %` : ''}</span>
            </li>
          ))}
        </ul>
      )}
      <div className="row">
        <button type="button" disabled={!node.editable} onClick={() => void pickText(SURVEY).then((f) => f && execute('civil.updateTerrain', { id: node.id, text: f.text }))}>
          <FileUp size={13} /> Replace the points…
        </button>
        <button type="button" onClick={() => void downloadLandXml([node.id]).catch((e) => notify(e instanceof Error ? e.message : String(e)))}><FileDown size={13} /> LandXML</button>
      </div>
    </div>
  );
}

/** The points picked in the 3D view (or in plan, from a point's right-click): how many, their elevation typed, absolute, as one undo step. */
function PickedPoints({ node, picked }: { node: NodeDto; picked: number[] }) {
  const datum = typeof node.props.datum === 'number' ? node.props.datum : 0;
  const pts = Array.isArray(node.props.points) ? (node.props.points as number[][]) : [];
  const first = picked.length ? pts[picked[0]] : undefined;
  const same = first && picked.every((i) => pts[i] && Math.abs(pts[i][2] - first[2]) < 1e-6);
  const shown = same && first ? formatLength(first[2] - datum, levelUnit()) : '';
  const [text, setText] = useState(shown);
  useEffect(() => setText(shown), [shown, picked.length]);
  const set = () => {
    const z = parseLevel(text);
    if (z == null || !Number.isFinite(z)) { notify('That elevation does not read: type a number in the project\'s unit.'); return; }
    void execute('civil.setTerrainPoints', { id: node.id, indices: picked, z });
  };
  const allOutline = useTerrainEdit((s) => s.allOutline);
  const outline = outlinePointIndices(node, allOutline);
  return (
    <div className="row small" data-testid="terrain-picked" style={{ flexWrap: 'wrap', gap: '2px 6px', width: '100%' }}>
      {picked.length > 0 ? <>
        <span>{picked.length}</span> <span>picked</span>
        <input type="text" value={text} placeholder={same ? '' : 'several'} style={{ width: '6em', flex: 'none' }} aria-label="Elevation of the points picked" data-testid="terrain-picked-z"
          onChange={(e) => setText(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') set(); }} />
        <button type="button" className="link small" onClick={set}>set elevation</button>
        {' · '}<button type="button" className="link small" onClick={() => setPicked(node.id, [])}>let go</button>
      </> : <span className="muted">No point picked.</span>}
      {outline.length > 0 && <>{' · '}<button type="button" className="link small" onClick={() => setPicked(node.id, outline)}>pick the outline's</button></>}
      {node.props.outlinePoints === true && <label className="small" title="Grips on every vertex along the outline's arcs too; off, on its corners only, the arcs following them" style={{ width: '100%' }}>
        <input type="checkbox" checked={allOutline} data-testid="terrain-all-outline" onChange={(e) => { useTerrainEdit.setState({ allOutline: e.target.checked }); redrawGrips(); }} /> all outline points
      </label>}
    </div>
  );
}
