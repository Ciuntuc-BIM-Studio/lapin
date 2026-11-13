/**
 * A wall's, beam's or strip footing's ends joining automatically (the forum's topic #50, decision 546): two toggles, for
 * every selected one of them at once. An end turned off keeps the joins it has (the Join tools still make and take them off);
 * an imported element's ends start off, as the file has them.
 */

import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { autoJoinAt, isPathElement, type PathEnd } from './autoJoin';

export function AutoJoinSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const ids = [...new Set([node.id, ...selected])].filter((id) => { const n = nodes.get(id); return !!n && isPathElement(n.props); });
  const all = ids.map((id) => nodes.get(id)!);
  const state = (end: PathEnd) => {
    const on = all.filter((n) => autoJoinAt(n.props, end)).length;
    return { checked: on === all.length, mixed: on > 0 && on < all.length };
  };
  const toggle = (end: PathEnd, value: boolean) => void execute('element.setAutoJoin', { ids, [end]: value });
  const row = (end: PathEnd, label: string) => {
    const s = state(end);
    return <>
      <dt>{label}</dt>
      <dd><input type="checkbox" data-action={`auto-join-${end}`} checked={s.checked}
        ref={(el) => { if (el) el.indeterminate = s.mixed; }}
        onChange={(e) => toggle(end, e.target.checked)} /></dd>
    </>;
  };
  return (
    <dl className="fields" data-testid="auto-join">
      {row('start', 'Join automatically at start')}
      {row('end', 'Join automatically at end')}
    </dl>
  );
}
