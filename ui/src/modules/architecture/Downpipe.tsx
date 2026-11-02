/**
 * Downpipes as elements of their own (decision 235): placed by a click on a roof's gutter (Architecture → Downpipe), each
 * tied to its gutter at a station (its grip slides only along the gutter, in plans and sections), its route a chain of
 * segments with their lengths and angles from the vertical (towards the wall), the last one to the ground unless given, its
 * diameter and the shoe at its foot, all edited here.
 */

import { useEffect, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import type { Grip, GripView, ToolDef } from '../../platform/module';
import { DownpipeIcon } from '../../icons';
import { LengthField, NumberField } from '../../shell/cells';

export const downpipe: ToolDef = {
  id: 'architecture.downpipe', label: 'Downpipe', icon: DownpipeIcon,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('architecture.addDownpipe', { x: p.x, y: p.y }); },
};

type Leg = { length?: number; angle: number };

/** The station grip: where the downpipe leaves its gutter, sliding along it only (the gutter as the view sees it). */
export function downpipeGrips(node: NodeDto, view?: GripView): Grip[] {
  const m = view?.marks?.find((x) => x.id === `${node.id}:gutter`) ?? (view?.mark?.id === `${node.id}:gutter` ? view.mark : undefined);
  const d = m?.data as { a?: number[]; b?: number[]; outlet?: number[]; length?: number } | undefined;
  if (!d?.a || !d.b || !d.outlet || !d.length) return [];
  const [ax, ay] = d.a, [bx, by] = d.b;
  const len2 = (bx - ax) ** 2 + (by - ay) ** 2;
  if (len2 < 1e-6) return [];   // the gutter seen end on: nothing to slide along
  return [{
    key: 'station', kind: 'move', at: { x: d.outlet[0], y: d.outlet[1] }, view: view?.view, title: 'Drag: slide the downpipe along its gutter',
    step: (to) => {
      const t = Math.min(1, Math.max(0, ((to.x - ax) * (bx - ax) + (to.y - ay) * (by - ay)) / len2));
      return { command: 'architecture.updateDownpipe', args: { id: node.id, station: Math.round(t * d.length! * 1000) / 1000 } };
    },
  }];
}

/** Its station, diameter, shoe and route. */
export function DownpipeSection({ node }: { node: NodeDto }) {
  const route = (node.props.route as Leg[] | undefined) ?? [{ angle: 0 }];
  const [legs, setLegs] = useState<Leg[]>(route);
  useEffect(() => setLegs((node.props.route as Leg[] | undefined) ?? [{ angle: 0 }]), [node.props.route]);
  const send = (next: Leg[]) => { setLegs(next); void execute('architecture.updateDownpipe', { id: node.id, route: next }); };
  return (
    <div className="layers">
      <dl className="fields">
        <dt>Station</dt>
        <dd><LengthField metres={Number(node.props.station ?? 0)} min={0} readOnly={!node.editable} onCommit={(v) => { if (v != null) void execute('architecture.updateDownpipe', { id: node.id, station: v }); }} /></dd>
        <dt>Diameter</dt>
        <dd><LengthField metres={Number(node.props.diameter ?? 0.1)} min={0.01} readOnly={!node.editable} onCommit={(v) => { if (v != null) void execute('architecture.updateDownpipe', { id: node.id, diameter: v }); }} /></dd>
        <dt>Shoe at the foot</dt>
        <dd><input type="checkbox" checked={node.props.shoe !== 'False'} disabled={!node.editable} onChange={(e) => void execute('architecture.updateDownpipe', { id: node.id, shoe: e.target.checked })} /></dd>
      </dl>
      <table className="roof-edges downpipe-route">
        <thead><tr><th>#</th><th>Length</th><th>°</th><th /></tr></thead>
        <tbody>
          {legs.map((l, i) => (
            <tr key={i}>
              <td className="num">{i + 1}</td>
              <td>
                {i === legs.length - 1 && l.length === undefined
                  ? <button type="button" className="link small" title="Down to the ground; click to give it a length" onClick={() => send(legs.map((x, k) => (k === i ? { ...x, length: 1 } : x)))}>to the ground</button>
                  : <LengthField metres={l.length ?? 1} min={0.01} noUnit readOnly={!node.editable} onCommit={(v) => { if (v != null) send(legs.map((x, k) => (k === i ? { ...x, length: v } : x))); }} />}
              </td>
              <td><NumberField value={l.angle} readOnly={!node.editable} title="From the vertical, towards the wall" onCommit={(v) => { if (v != null) send(legs.map((x, k) => (k === i ? { ...x, angle: v } : x))); }} /></td>
              <td>{legs.length > 1 && <button type="button" className="icon mini" title="Remove the segment" onClick={() => send(legs.filter((_, k) => k !== i))}><Trash2 size={11} /></button>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="row">
        <button type="button" className="btn compact" disabled={!node.editable || legs.length >= 12} onClick={() => send([...legs.slice(0, -1), { length: 0.5, angle: 45 }, legs[legs.length - 1]])}><Plus size={12} /> Segment</button>
        {legs[legs.length - 1]?.length !== undefined && <button type="button" className="btn compact" onClick={() => send(legs.map((x, k) => (k === legs.length - 1 ? { angle: x.angle } : x)))}>Last to the ground</button>}
      </div>
    </div>
  );
}
