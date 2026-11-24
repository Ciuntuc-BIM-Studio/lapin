/**
 * The list Tab opens while it is held (issue 110, tabHold.ts): every element under the pointer, near the pointer, the current
 * one filled (and marked with an arrow and bold, so it does not rest on colour alone) and the others outlined. Not interactive:
 * the wheel and the key move it.
 */

import { useSession } from '../engine/session';
import { describeNode } from './pickCycle';
import { holdRows, useTabHold } from './tabHold';
import { t } from '../i18n';

export function TabHoldList() {
  const h = useTabHold();
  const nodes = useSession((s) => s.nodes);
  if (!h.open || !h.client) return null;
  const rows = holdRows(h.ids, h.index);
  return (
    <ul className="tab-hold" style={{ left: h.client.x + 16, top: h.client.y + 16 }} role="listbox" aria-label={t('Elements under the pointer')} data-testid="tab-hold">
      {rows.length === 0 && <li className="muted" role="option" aria-selected={false}><span className="tab-hold-mark" />Nothing under the pointer</li>}
      {rows.map((r) => (
        <li key={r.id} className={r.current ? 'on' : ''} role="option" aria-selected={r.current}>
          <span className="tab-hold-mark">{r.current ? '▶' : ''}</span>{describeNode(nodes.get(r.id.includes('#') ? r.id.slice(0, r.id.indexOf('#')) : r.id), r.id)}
        </li>
      ))}
    </ul>
  );
}
