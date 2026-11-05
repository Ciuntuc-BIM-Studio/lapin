/** A reference plane in the inspector: open it to draw on, move it along its normal, turn it about the vertical. Its place and angles are the fields above. */

import { FolderOpen, MoveVertical, RotateCw } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { useUi } from '../../app/store';
import { t } from '../../i18n';
import { parseLength, unitName } from '../../engine/unitsStore';

const ask = (what: string, length = false): number | null => {
  const v = window.prompt(length ? `${t(what)} [${unitName()}]` : t(what))?.trim();
  if (!v) return null;
  const n = length ? parseLength(v) : Number(v.replace(',', '.'));
  return n != null && Number.isFinite(n) && n !== 0 ? n : null;
};

export function RefPlaneSection({ node }: { node: NodeDto }) {
  if (node.props.kind !== 'plane') return null;
  return (
    <dl className="fields">
      <dt />
      <dd><button type="button" onClick={() => useUi.setState({ view: 'drawing', drawingViewId: node.id })}><FolderOpen size={13} /> Open the plane</button></dd>
      <dt />
      <dd><button type="button" onClick={() => { const d = ask('Move along its normal (negative: away from its viewer)', true); if (d != null) void execute('view.movePlane', { id: node.id, along: d }); }}><MoveVertical size={13} /> Move along its normal…</button></dd>
      <dt />
      <dd><button type="button" onClick={() => { const a = ask('Turn about the vertical through its origin (°, counter-clockwise)'); if (a != null) void execute('view.movePlane', { id: node.id, turn: a }); }}><RotateCw size={13} /> Turn about the vertical…</button></dd>
      <dd className="muted small">What is drawn on the plane, and the solids made from it, move and turn with it.</dd>
    </dl>
  );
}
