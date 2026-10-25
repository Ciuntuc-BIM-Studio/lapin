/** A terrain in the inspector: its points, what stands in it, the brush strokes on it, its analysis with the legend, its spot levels and parcel, a new survey or a LandXML of it. */

import { useEffect, useState } from 'react';
import { FileDown, FileUp } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { SURVEY, downloadLandXml, pickText } from './survey';

interface Legend { kind: string; area: number; bands: { key: string; label: string; color: string; area: number }[] }

const KINDS: [string, string][] = [['none', 'none'], ['slope', 'slopes'], ['cutfill', 'cut and fill'], ['elevation', 'elevations']];

export function TerrainSection({ node }: { node: NodeDto }) {
  const count = Array.isArray(node.props.points) ? node.props.points.length : 0;
  const strokes = Array.isArray(node.props.sculpts) ? node.props.sculpts.length : 0;
  const spots = Array.isArray(node.props.spots) ? node.props.spots.length : 0;
  const parcel = Array.isArray(node.props.parcel);
  const analysis = typeof node.props.analysis === 'string' ? node.props.analysis : 'none';
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
        <dt>Points</dt><dd>{count}</dd>
        <dt>Platforms, ditches, paths</dt><dd>{pads}</dd>
        <dt>Brush strokes</dt>
        <dd>{strokes}{strokes > 0 && <> <button type="button" className="link small" onClick={() => void execute('civil.clearSculpt', { id: node.id, last: true })}>undo last</button> · <button type="button" className="link small" onClick={() => void execute('civil.clearSculpt', { id: node.id })}>clear</button></>}</dd>
        <dt>Spot levels</dt>
        <dd>{spots}{spots > 0 && <> <button type="button" className="link small" onClick={() => void execute('civil.removeSpot', { id: node.id, all: true })}>clear</button></>}</dd>
        <dt>Parcel</dt>
        <dd>{parcel ? <>drawn <button type="button" className="link small" onClick={() => void execute('civil.setParcel', { id: node.id })}>remove</button></> : <span className="muted">none</span>}</dd>
        <dt>Analysis</dt>
        <dd><select value={analysis} disabled={!node.editable} onChange={(e) => void execute('civil.updateTerrain', { id: node.id, analysis: e.target.value })}>
          {KINDS.map(([k, label]) => <option key={k} value={k}>{label}</option>)}
        </select></dd>
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
        <button type="button" onClick={() => void downloadLandXml([node.id])}><FileDown size={13} /> LandXML</button>
      </div>
    </div>
  );
}
