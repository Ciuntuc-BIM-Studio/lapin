/**
 * A floor's slopes in the inspector (docs/plan-floor-slopes.md, forum topic 157): how its build-up follows them (bent, or one
 * layer sculpted to falls with its minimum), its edge slopes, drains, level points and lines, its regions, what the engine
 * worked out (the top's range, a layer held at its minimum), and the tools that add more. Each change is one command.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { execute, query, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { notify } from '../../app/store';
import { LengthField } from '../../shell/cells';
import { formatLength } from '../../engine/unitsStore';
import { activate } from '../../platform/modules';
import { floorDrain, floorChannel, floorEdgeSlope, floorSlopeArrow, floorSlopeLine, floorSlopePoint, floorSlopeRegion, gradeText, itemCount, parseGrade, surfaceOf, type SlopeTop } from './floorSlopes';

interface Info {
  layers: { index: number; name: string; thickness: number; core: boolean }[];
  defaultLayer: number | null;
  top?: number;
  problems?: string[];
  pieces?: { region: number; how: 'bend' | 'layer'; layer: number | null; minThickness: number | null; min: number; max: number; grade: number | null }[];
}

/** A grade typed in place: what it shows, read when it is committed. */
function GradeInput({ value, onCommit }: { value: number; onCommit: (g: number) => void }) {
  return (
    <input type="text" key={value} defaultValue={gradeText(value)} style={{ width: '5.5em' }} aria-label="Slope" data-testid="floor-slope-grade"
      onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
      onBlur={(e) => { const g = parseGrade(e.target.value); if (g == null || !(g > 0)) { notify('That grade does not read: type 1.5 %, 1:50 or 0.015.'); e.target.value = gradeText(value); return; } if (Math.abs(g - value) > 1e-9) onCommit(g); }} />
  );
}

const Remove = ({ onClick }: { onClick: () => void }) => <button type="button" className="icon" title="Remove" onClick={onClick}><X size={12} /></button>;

/** One top's things (the floor's own, or a region's): its edge slopes, drains, points and lines, each editable in place. */
function Items({ id, top, region, editable }: { id: string; top: SlopeTop; region: number | null; editable: boolean }) {
  const at = region == null ? {} : { region };
  const update = (kind: string, index: number, args: Record<string, unknown>) => void execute('architecture.updateFloorSlope', { id, ...at, kind, index, ...args });
  const remove = (kind: string, index: number) => void execute('architecture.removeFloorSlope', { id, ...at, kind, index });
  const row = { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '4px 8px', padding: '3px 0', borderTop: '1px solid var(--line, #ddd)' } as const;
  return (
    <div className="small" data-testid={region == null ? 'floor-slope-items' : `floor-slope-items-${region}`}>
      {(top.edges ?? []).map((e, i) => (
        <div key={`e${i}`} style={row}>
          <span>Edge slope</span><span className="muted">{`#${e.edge + 1}`}</span>
          <GradeInput value={e.grade} onCommit={(g) => update('edge', i, { grade: g })} />
          <select value={e.fall} disabled={!editable} onChange={(x) => update('edge', i, { fall: x.target.value })}>
            <option value="in">falls away from it</option>
            <option value="out">falls towards it</option>
          </select>
          <span title="Where the slope stops: the offset it falls to, then level">
            <LengthField metres={e.until?.offset ?? null} placeholder="to the far side" style={{ width: '7em' }} onCommit={(m) => update('edge', i, { until: m == null ? null : { offset: m } })} />
          </span>
          {editable && <Remove onClick={() => remove('edge', i)} />}
        </div>
      ))}
      {(top.drains ?? []).map((d, i) => (
        <div key={`d${i}`} style={row}>
          <span>{d.to ? 'Channel drain' : 'Drain'}</span>{d.centre && <span className="muted">at the middle</span>}
          <span title="How far below the floor round it"><LengthField metres={d.depth} style={{ width: '5em' }} onCommit={(m) => { if (m != null && m > 0) update('drain', i, { depth: m }); }} /></span>
          {!d.to && <span title="The grate's size (a square at the drain's depth)"><LengthField metres={d.grate ?? null} placeholder="no grate" style={{ width: '6em' }} onCommit={(m) => update('drain', i, { grate: m ?? 0 })} /></span>}
          {editable && <Remove onClick={() => remove('drain', i)} />}
        </div>
      ))}
      {(top.points ?? []).map((p, i) => (
        <div key={`p${i}`} style={row}>
          <span>Level point</span><span className="muted">{p.level != null ? 'level' : 'offset'}</span>
          <LengthField metres={p.level ?? p.offset ?? 0} style={{ width: '6em' }} onCommit={(m) => { if (m != null) update('point', i, p.level != null ? { level: m } : { offset: m }); }} />
          {editable && <Remove onClick={() => remove('point', i)} />}
        </div>
      ))}
      {(top.lines ?? []).map((l, i) => (
        <div key={`l${i}`} style={row}>
          <span>Level line</span><span className="muted">{l.level ? 'levels' : 'offsets'}</span>
          <LengthField metres={(l.level ?? l.offset ?? [0, 0])[0]} style={{ width: '6em' }} onCommit={(m) => { if (m != null) update('line', i, l.level ? { level: [m, l.level[1]] } : { offset: [m, (l.offset ?? [0, 0])[1]] }); }} />
          <LengthField metres={(l.level ?? l.offset ?? [0, 0])[1]} style={{ width: '6em' }} onCommit={(m) => { if (m != null) update('line', i, l.level ? { level: [l.level[0], m] } : { offset: [(l.offset ?? [0, 0])[0], m] }); }} />
          {editable && <Remove onClick={() => remove('line', i)} />}
        </div>
      ))}
    </div>
  );
}

export function FloorSlopesSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const surface = surfaceOf(node);
  const [info, setInfo] = useState<Info | null>(null);
  useEffect(() => {
    let live = true;
    void query<Info>('architecture.floorSlope', { id: node.id }).then((r) => { if (live) setInfo(r); });
    return () => { live = false; };
  }, [node.id, nodes]);
  const set = (args: Record<string, unknown>) => void execute('architecture.setFloorSlope', { id: node.id, ...args });
  const main = info?.pieces?.find((p) => p.region === -1);
  const layers = (info?.layers ?? []).filter((l) => l.thickness > 0);
  const how = surface?.how ?? main?.how ?? 'layer';
  const tools = (
    <div data-testid="floor-slope-tools" style={{ display: 'flex', flexWrap: 'wrap', gap: '2px 12px', margin: '6px 0' }}>
      {[floorDrain, floorChannel, floorEdgeSlope, floorSlopePoint, floorSlopeLine, floorSlopeArrow, floorSlopeRegion].map((t) => (
        <button key={t.id} type="button" className="link small" style={{ whiteSpace: 'nowrap' }} disabled={!node.editable} onClick={() => activate(t)}>{t.label}</button>
      ))}
    </div>
  );
  if (!surface) {
    return (
      <div className="layers" data-testid="floor-slopes">
        <p className="muted small">Flat at its offset. Add a drain, an edge slope, level points or lines, or a slope arrow: the floor falls to them.</p>
        {tools}
      </div>
    );
  }
  const regions = surface.regions ?? [];
  return (
    <div className="layers" data-testid="floor-slopes">
      <dl className="fields">
        <dt>Made by</dt>
        <dd><select value={how} disabled={!node.editable} data-testid="floor-slope-how" onChange={(e) => set({ how: e.target.value })}>
          <option value="layer">Sculpting a layer</option>
          <option value="bend">Bending the whole floor</option>
        </select></dd>
        {how === 'layer' && <>
          <dt>Sculpted layer</dt>
          <dd><select value={surface.layer == null ? '' : String(surface.layer)} disabled={!node.editable} data-testid="floor-slope-layer"
            onChange={(e) => set({ layer: e.target.value === '' ? null : Number(e.target.value) })}>
            <option value="">Default</option>
            {layers.map((l) => <option key={l.index} value={String(l.index)}>{`${l.name} (${formatLength(l.thickness)})`}</option>)}
          </select>{surface.layer == null && <span className="muted small"> {layers.find((l) => l.index === info?.defaultLayer)?.name ?? ''}</span>}</dd>
          <dt>Minimum thickness</dt>
          <dd><LengthField metres={surface.minThickness ?? main?.minThickness ?? null} style={{ width: '6em' }} testId="floor-slope-min" onCommit={(m) => set({ minThickness: m })} /></dd>
        </>}
        <dt>Top</dt>
        <dd className="small" data-testid="floor-slope-range">
          {main ? `${formatLength(main.min)} … ${formatLength(main.max)}` : ''}{main?.grade ? ` · ${gradeText(main.grade)}` : ''}
        </dd>
      </dl>
      {itemCount(surface) > 0 && <Items id={node.id} top={surface} region={null} editable={node.editable} />}
      {regions.map((r, i) => (
        <div key={i} className="small" data-testid={`floor-slope-region-${i}`}>
          <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '4px 8px', paddingTop: 6 }}>
            <span>{r.roomId ? 'Room' : 'Region'}</span>
            <span>{r.roomId ? nodes.get(r.roomId)?.name ?? '' : String(i + 1)}</span>
            <span className="muted">offset</span>
            <LengthField metres={r.elevation ?? 0} style={{ width: '5.5em' }} onCommit={(m) => { if (m != null) set({ region: i, elevation: m }); }} />
            {node.editable && <Remove onClick={() => void execute('architecture.removeFloorSlopeRegion', { id: node.id, region: i })} />}
          </div>
          {itemCount(r) > 0 && <Items id={node.id} top={r} region={i} editable={node.editable} />}
        </div>
      ))}
      {(info?.problems ?? []).map((p, i) => <p key={i} className="warn small">{p}</p>)}
      {tools}
      <button type="button" className="link small" disabled={!node.editable} data-testid="floor-slope-reset" onClick={() => void execute('architecture.resetFloorSlope', { id: node.id })}>Make it flat again</button>
    </div>
  );
}
