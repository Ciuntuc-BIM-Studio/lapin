/**
 * A group in the inspector (docs/groups.md): how many elements it holds, and opening it for editing,
 * ungrouping it or selecting its members one by one.
 */

import { Feather, PencilRuler, Ungroup, MousePointer2 } from 'lucide-react';
import { isDesktop } from '../../engine/files';
import { fileName, openOptimize } from '../../engine/ifcOptimize';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { membersOf, openGroup } from '../../platform/groups';

export function GroupSection({ node }: { node: NodeDto }) {
  const relations = useSession((s) => s.relations);
  const members = membersOf(node.id, relations);
  return (
    <>
      <dl className="fields">
        <dt>Elements</dt><dd className="ro">{members.length}</dd>
      </dl>
      <div className="pad row-wrap">
        <button type="button" className="btn" title="Double-click a member to do the same" onClick={() => openGroup(node.id)}><PencilRuler size={14} />Edit group</button>
        <button type="button" className="btn" onClick={() => void execute('group.ungroup', { ids: [node.id] }).then((r) => {
          const ids = (r as { ids?: string[] } | null)?.ids ?? [];
          useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
        })}><Ungroup size={14} />Ungroup</button>
        <button type="button" className="btn" onClick={() => useUi.setState({ selected: members, selection: members[members.length - 1] ?? null })}><MousePointer2 size={14} />Select members</button>
        {/* An imported IFC's group keeps its file: a lighter copy of it, its GlobalIds and properties the same (docs/ifc-optimize.md). */}
        {isDesktop() && typeof node.props.source === 'string' && /\.ifc(zip)?$/i.test(node.props.source) && (
          <button type="button" className="btn" data-group="optimize" title={node.props.source} onClick={() => openOptimize([{ name: fileName(node.props.source as string), path: node.props.source as string }])}><Feather size={14} />Optimize the source file…</button>
        )}
      </div>
    </>
  );
}
