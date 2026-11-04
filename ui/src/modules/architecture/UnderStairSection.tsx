/**
 * The space under a stair (a tester's report, #71) in the inspector: on a stair, its spaces (each opened by a click) and
 * Space under the stair… for a new one; on a space, what fills it, its stair, and Configure… to open the configurator.
 * Where it stands (the flight, the open side, from and to, the depth) are its fields above.
 */

import { Armchair, Settings2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { openUnderStair } from './UnderStairDialog';

export function UnderStairLinks({ stairId }: { stairId: string }) {
  const spaces = useSession((s) => [...s.nodes.values()].filter((n) => n.type === 'LapinUnderStair' && n.props.host === stairId));
  return (
    <div className="pad under-stair-links">
      {spaces.map((n) => (
        <button key={n.id} type="button" className="link small" data-no-i18n onClick={() => useUi.setState({ selection: n.id, selected: [n.id] })}>{n.name}</button>
      ))}
      <button type="button" className="btn" data-field="under-stair-open" onClick={() => openUnderStair(stairId)}><Armchair size={14} />Space under the stair…</button>
    </div>
  );
}

export function UnderStairSection({ node }: { node: NodeDto }) {
  const stairId = typeof node.props.host === 'string' ? node.props.host : null;
  const stair = useSession((s) => (stairId ? s.nodes.get(stairId) : undefined));
  const spec = node.props.spec as { name?: string } | undefined;
  return (
    <div className="layers">
      <dl className="fields">
        <dt>Style</dt><dd data-no-i18n>{spec?.name ?? '—'}</dd>
        <dt>Stair</dt><dd>{stair ? <button type="button" className="link small" data-no-i18n onClick={() => useUi.setState({ selection: stair.id, selected: [stair.id] })}>{stair.name}</button> : '—'}</dd>
      </dl>
      {stairId && <button type="button" className="btn" data-field="under-stair-configure" onClick={() => openUnderStair(stairId, node.id)}><Settings2 size={14} />Configure…</button>}
    </div>
  );
}
