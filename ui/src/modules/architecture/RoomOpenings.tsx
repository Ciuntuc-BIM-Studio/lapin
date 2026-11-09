/**
 * A room's doors and windows, chosen here by type and side (docs/rooms.md): off by default, beside placing them on walls,
 * which goes on as before. Each row makes an ordinary door or window in the wall on that side; the small plan numbers the
 * sides. Every change is sent whole, as one undo step.
 */

import { useEffect, useState } from 'react';
import { Trash2 } from 'lucide-react';
import { execute, query, useSession } from '../../engine/session';
import { formatLength } from '../../engine/unitsStore';
import type { NodeDto } from '../../engine/protocol';
import { LengthCell } from '../../shell/cells';

interface Side { index: number; a: [number, number]; b: [number, number]; length: number; wall?: string | null }
interface Item { key: string; kind: 'door' | 'window'; typeId?: string | null; side?: number | null; offset?: number | null; sill?: number | null; fill?: string | null; noWall?: boolean; fillName?: string | null; gone?: boolean }
interface Data { on: boolean; sides: Side[]; items: Item[] }

/** The room's outline with its sides numbered and a mark where each door or window goes. */
function SidesPlan({ data }: { data: Data }) {
  const pts = data.sides.flatMap((s) => [s.a, s.b]);
  if (pts.length === 0) return null;
  const xs = pts.map((p) => p[0]);
  const ys = pts.map((p) => p[1]);
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const size = Math.max(x1 - x0, y1 - y0, 1e-6);
  const pad = size * 0.14;
  const at = (x: number, y: number): [number, number] => [x, y0 + y1 - y];
  const view = `${x0 - pad} ${y0 - pad} ${x1 - x0 + 2 * pad} ${y1 - y0 + 2 * pad}`;
  return (
    <svg className="paper-card" data-testid="room-sides" viewBox={view} width="100%" height="120" style={{ display: 'block' }}>
      {data.sides.map((s) => {
        const [ax, ay] = at(s.a[0], s.a[1]);
        const [bx, by] = at(s.b[0], s.b[1]);
        // The number inside the room, clear of the marks on the side (the outline runs with the room on its left).
        const [nx, ny] = at((s.a[0] + s.b[0]) / 2 - ((s.b[1] - s.a[1]) / s.length) * size * 0.1, (s.a[1] + s.b[1]) / 2 + ((s.b[0] - s.a[0]) / s.length) * size * 0.1);
        return (
          <g key={s.index}>
            <line x1={ax} y1={ay} x2={bx} y2={by} stroke={s.wall ? '#222' : '#999'} strokeWidth={size * 0.012} strokeDasharray={s.wall ? undefined : `${size * 0.03}`} />
            <text x={nx} y={ny} dy={size * 0.025} fontSize={size * 0.075} textAnchor="middle" fill="#222" stroke="#fff" strokeWidth={size * 0.02} paintOrder="stroke">{s.index + 1}</text>
          </g>
        );
      })}
      {data.items.map((i) => {
        const s = data.sides.find((x) => x.index === i.side);
        if (!s) return null;
        const d = [(s.b[0] - s.a[0]) / s.length, (s.b[1] - s.a[1]) / s.length];
        const [mx, my] = [(s.a[0] + s.b[0]) / 2 + d[0] * (i.offset ?? 0), (s.a[1] + s.b[1]) / 2 + d[1] * (i.offset ?? 0)];
        const half = size * 0.05;
        const [ax, ay] = at(mx - d[0] * half, my - d[1] * half);
        const [bx, by] = at(mx + d[0] * half, my + d[1] * half);
        return <line key={i.key} x1={ax} y1={ay} x2={bx} y2={by} stroke={i.kind === 'door' ? '#b5442c' : '#2c6fb5'} strokeWidth={size * 0.035} />;
      })}
    </svg>
  );
}

export function RoomOpeningsField({ node }: { node: NodeDto }) {
  const nodes = useSession((x) => x.nodes);
  const [data, setData] = useState<Data | null>(null);
  useEffect(() => { void query<Data>('architecture.roomOpenings', { id: node.id }).then((d) => d && setData(d)); }, [nodes, node.id]);
  if (!data) return null;
  const types = (kind: Item['kind']) => [...nodes.values()].filter((n) => n.type === (kind === 'door' ? 'IfcDoorType' : 'IfcWindowType'));
  const send = (items: Item[], on = data.on) => { setData({ ...data, on, items }); void send0(items, on); };
  const send0 = (items: Item[], on: boolean) => execute('architecture.setRoomOpenings', { id: node.id, on, items: items.map(({ key, kind, typeId, side, offset, sill }) => ({ key, kind, typeId, side, offset, sill })) });
  const change = (key: string, patch: Partial<Item>) => send(data.items.map((i) => (i.key === key ? { ...i, ...patch } : i)));
  const add = (kind: Item['kind']) => {
    // On the first side with a wall and nothing on it yet, else on the first with a wall.
    const walled = data.sides.filter((s) => s.wall);
    const side = walled.find((s) => !data.items.some((i) => i.side === s.index)) ?? walled[0];
    send([...data.items, { key: crypto.randomUUID().slice(0, 8), kind, typeId: types(kind)[0]?.id ?? null, side: side?.index ?? 0, offset: 0 }]);
  };
  return (
    <div className="room-openings" data-testid="room-openings">
      <label className="row small" title="Doors and windows chosen here, by type and side. Placing them on the walls goes on as before.">
        <input type="checkbox" data-field="room-openings" checked={data.on} onChange={(e) => send(data.items, e.target.checked)} />
        Doors and windows from the room
      </label>
      {data.on && (
        <>
          <SidesPlan data={data} />
          {data.items.map((i) => (
            <dl key={i.key} className="fields" data-room-opening={i.kind}>
              <dt title={i.fillName ?? undefined}>{i.kind === 'door' ? 'Door' : 'Window'}</dt>
              <dd className="row">
                <select value={i.typeId ?? ''} onChange={(e) => change(i.key, { typeId: e.target.value })}>
                  {types(i.kind).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
                <button type="button" className="icon mini" title="Take it away" onClick={() => send(data.items.filter((x) => x.key !== i.key))}><Trash2 size={12} /></button>
              </dd>
              <dt>Side</dt>
              <dd>
                <select data-field="room-opening-side" value={i.side ?? ''} onChange={(e) => change(i.key, { side: Number(e.target.value), offset: 0 })}>
                  {data.sides.map((s) => <option key={s.index} value={s.index} disabled={!s.wall}>{`${s.index + 1}: ${s.wall ?? 'no wall'}, ${formatLength(s.length)}`}</option>)}
                </select>
              </dd>
              <dt title="From the middle of the side">Offset</dt>
              <dd><LengthCell metres={i.offset ?? 0} onCommit={(m) => change(i.key, { offset: m ?? 0 })} /></dd>
              {i.kind === 'window' && (
                <>
                  <dt title="Empty: as its type says">Sill</dt>
                  <dd><LengthCell metres={i.sill ?? null} placeholder="as its type" onCommit={(m) => change(i.key, { sill: m })} /></dd>
                </>
              )}
              {i.gone && <dd className="small warn">It was deleted: any change here puts it back.</dd>}
              {i.noWall && <dd className="small warn">No wall can take it here.</dd>}
            </dl>
          ))}
          <div className="row">
            <button type="button" className="btn small" data-add="door" onClick={() => add('door')}>Add door</button>
            <button type="button" className="btn small" data-add="window" onClick={() => add('window')}>Add window</button>
          </div>
        </>
      )}
    </div>
  );
}
