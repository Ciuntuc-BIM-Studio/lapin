/** A terrain in the inspector: its points, what stands in it, the brush strokes on it, its analysis with the legend, its spot levels and parcel, a new survey or a LandXML of it. */

import { useEffect, useState } from 'react';
import { FileDown, FileUp } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { SURVEY, downloadLandXml, pickText } from './survey';
import { notify } from '../../app/store';
import { useTerrainEdit } from './prefs';

interface Legend { kind: string; area: number; bands: { key: string; label: string; color: string; area: number }[] }

const KINDS: [string, string][] = [['none', 'none'], ['slope', 'slopes'], ['cutfill', 'cut and fill'], ['elevation', 'elevations']];
const GRID_STEPS: [number, string][] = [[0, 'off'], [2, '2 m'], [5, '5 m'], [10, '10 m']];

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
        <dd>{count} <button type="button" className="link small" onClick={() => setEditPoints(!editPoints)}>{editPoints ? 'done editing 3D' : 'edit in 3D'}</button></dd>
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
          {boundary ? <>clipped <button type="button" className="link small" onClick={() => void execute('civil.setBoundary', { id: node.id })}>remove</button></>
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
