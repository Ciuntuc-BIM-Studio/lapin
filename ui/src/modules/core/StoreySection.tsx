/**
 * A storey in the inspector: its base level and its top level, either above
 * or below ±0.00; its height is what lies between (shown, not typed). Moving
 * the base keeps the top where it is.
 */

import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { LengthCell } from '../../shell/cells';

export function StoreySection({ node }: { node: NodeDto }) {
  const base = typeof node.props.elevation === 'number' ? node.props.elevation : 0;
  const height = typeof node.props.height === 'number' ? node.props.height : 3;
  const top = base + height;
  const set = (args: Record<string, unknown>) => void execute('storey.update', { id: node.id, ...args });
  return (
    <dl className="fields">
      <dt title="elevation">Base level</dt><dd><LengthCell metres={base} onCommit={(m) => { if (m != null) set({ elevation: m, keepTop: true }); }} /></dd>
      <dt title="top">Top level</dt><dd><LengthCell metres={top} onCommit={(m) => { if (m != null) set({ top: m }); }} /></dd>
      <dt title="height">Height</dt><dd className="ro"><LengthCell metres={height} onCommit={(m) => { if (m != null) set({ height: m }); }} /></dd>
    </dl>
  );
}
