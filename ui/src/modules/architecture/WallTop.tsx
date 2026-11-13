/**
 * A wall's top (the forum's topic #45): where it goes up to — the storey above (a new wall's default), a storey of the
 * project's, or a height of its own (unconnected) — and its offset; the height it comes to, worked out by the engine on
 * every ask (architecture.wallTop). A change reaches every selected wall in one step (architecture.setWallTop). Also the
 * wall tool's option (WallTopOption) and a wall type's default (WallTypeTop).
 */

import { useEffect, useState } from 'react';
import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { storeys } from '../../engine/model';
import { lengthText } from '../../engine/unitsStore';
import { LengthField } from '../../shell/cells';
import { sameKind } from '../../shell/params';

const ABOVE = 'above';
const UNCONNECTED = 'unconnected';

export interface WallTopNow { top: string | null; topOffset: number; height: number; base: number; level: number; storey: string | null; problem: string | null }

const TOP_TIP = 'Where the wall goes up to: the storey above its own (on the topmost storey, that storey\'s top), a storey of the project, or a height of its own. Its height follows the storeys when they move';

/** The storeys a top may go to: every storey of the project, lowest first. */
function StoreyOptions() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  return <>{storeys({ nodes, relations }).map((st) => <option key={st.id} value={st.id}>{st.name}</option>)}</>;
}

/** The Top, Top offset and the height it comes to, in a wall's Wall section. */
export function WallTopFields({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [now, setNow] = useState<WallTopNow | null>(null);
  useEffect(() => {
    let live = true;
    void query<WallTopNow>('architecture.wallTop', { id: node.id }).then((r) => { if (live) setNow(r); });
    return () => { live = false; };
  }, [node.id, nodes]);
  const top = typeof node.props.top === 'string' && node.props.top ? node.props.top : UNCONNECTED;
  const set = (args: Record<string, unknown>) => void execute('architecture.setWallTop', { ids: sameKind(node), ...args });
  const offset = typeof node.props.topOffset === 'number' ? node.props.topOffset : 0;
  const goesTo = now?.storey ? nodes.get(now.storey)?.name : undefined;
  return (
    <dl className="fields">
      <dt title={TOP_TIP}>Top</dt>
      <dd>
        <select data-testid="wall-top" value={top} disabled={!node.editable} title={TOP_TIP} onChange={(e) => set({ top: e.target.value === UNCONNECTED ? null : e.target.value })}>
          <option value={ABOVE}>Storey above</option>
          <StoreyOptions />
          <option value={UNCONNECTED}>Unconnected height</option>
        </select>
      </dd>
      {top !== UNCONNECTED && <>
        <dt title="Added to the level the top goes to (negative: below it, under a floor's structure)">Top offset</dt>
        <dd><LengthField metres={offset} testId="wall-top-offset" onCommit={(m) => set({ topOffset: m ?? 0 })} /></dd>
        <dt title="From its base to its top, worked out from the storeys">Height</dt>
        <dd><span className="num" data-testid="wall-top-height">{now ? lengthText(now.height) : '—'}</span> <span className="muted small">{goesTo ? <>goes up to {goesTo}</> : top === ABOVE ? 'to its storey\'s top' : ''}</span></dd>
      </>}
      {now?.problem && <><dt /><dd className="error-text small">{now.problem}</dd></>}
    </dl>
  );
}

/** The wall tool's top for new walls, on this computer for this session: as the type says, the storey above, a storey, or a height. */
export const useWallTopPrefs = create<{ top: string; offset: number; height: number }>(() => ({ top: 'type', offset: 0, height: 3 }));

/** The addWall arguments for the wall tool's top (none: as the type says). */
export function wallTopArgs(): Record<string, unknown> {
  const { top, offset, height } = useWallTopPrefs.getState();
  if (top === 'type') return {};
  if (top === UNCONNECTED) return { top: null, height };
  return { top, topOffset: offset };
}

/** The wall tool's Top option in the command line's bar. */
export function WallTopOption() {
  const { top, offset, height } = useWallTopPrefs();
  return (
    <>
      <select className={`cad-layer${top !== 'type' ? ' on' : ''}`} value={top} data-testid="wall-tool-top" title={TOP_TIP} onChange={(e) => useWallTopPrefs.setState({ top: e.target.value })}>
        <option value="type">Top: as the type</option>
        <option value={ABOVE}>Top: storey above</option>
        <StoreyOptions />
        <option value={UNCONNECTED}>Top: unconnected height</option>
      </select>
      {top === UNCONNECTED
        ? <LengthField className="cad-length" metres={height} min={0.01} title="Height" testId="wall-tool-height" onCommit={(m) => { if (m != null && m > 0) useWallTopPrefs.setState({ height: m }); }} />
        : top !== 'type' && <LengthField className="cad-length" metres={offset} title="Top offset" testId="wall-tool-top-offset" onCommit={(m) => useWallTopPrefs.setState({ offset: m ?? 0 })} />}
    </>
  );
}

/** A wall type's top for the walls drawn with it, saved at once (architecture.setWallTypeTop). */
export function WallTypeTop({ typeId }: { typeId: string }) {
  const type = useSession((s) => s.nodes.get(typeId));
  if (!type) return null;
  const top = type.props.defaultTop === UNCONNECTED ? UNCONNECTED : ABOVE;
  const set = (args: Record<string, unknown>) => void execute('architecture.setWallTypeTop', { id: typeId, ...args });
  return (
    <div className="row wall-type-top" data-testid="wall-type-top">
      <span className="eyebrow">Wall top</span>
      <select value={top} title="Where new walls of this type go up to" onChange={(e) => set({ top: e.target.value })}>
        <option value={ABOVE}>Storey above</option>
        <option value={UNCONNECTED}>Unconnected height</option>
      </select>
      {top === ABOVE
        ? <label className="row small">Offset <LengthField metres={typeof type.props.defaultTopOffset === 'number' ? type.props.defaultTopOffset : 0} testId="wall-type-top-offset" onCommit={(m) => set({ topOffset: m ?? 0 })} /></label>
        : <label className="row small">Height <LengthField metres={typeof type.props.defaultHeight === 'number' ? type.props.defaultHeight : 3} min={0.01} testId="wall-type-height" onCommit={(m) => set({ height: m })} /></label>}
      <span className="muted small">for new walls; each wall's own in its properties</span>
    </div>
  );
}
