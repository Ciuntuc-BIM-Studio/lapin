/**
 * What works on an element as a live boolean, in its properties: the solids it is less (a face pushed into it, an
 * extrusion made to cut it) and those united or intersected with it, each selectable even when it is never drawn,
 * a pushed face with its distance.
 */

import { Fragment } from 'react';
import { MousePointerClick } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { formatLength, parseLength, unitLabel } from '../../engine/unitsStore';

/** The tools working on an element (ids), from its interference relations. */
export function toolsOf(id: string): string[] {
  const out: string[] = [];
  for (const r of useSession.getState().relations.values()) if (r.class === 'IfcRelInterferesElements' && r.relating === id) out.push(...r.related);
  return out;
}

export const hasTools = (node: NodeDto) => toolsOf(node.id).length > 0;

export function OperationsCard({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  useSession((s) => s.relations);
  const tools = toolsOf(node.id).map((id) => nodes.get(id)).filter((n): n is NodeDto => !!n);
  if (!tools.length) return null;
  return (
    <dl className="fields">
      {tools.map((t) => {
        const pushed = t.props.operation === 'prism';
        const d = Number(t.props.depth ?? 0);
        return (
          <Fragment key={t.id}>
            <dt title={t.props.void ? 'It cuts only, and is not drawn' : undefined}>{t.name}</dt>
            <dd>
              {pushed && (
                <input className="num" defaultValue={formatLength(d)} key={d} title="Distance (negative pushes in)"
                  onBlur={(e) => { const v = parseLength(e.target.value); if (v != null && Math.abs(v) > 1e-9 && Math.abs(v - d) > 1e-9) void execute('studio.setPush', { id: t.id, distance: v }); }}
                  onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
              )}
              {pushed && <span className="muted"> {unitLabel()} </span>}
              <button type="button" className="icon mini" title="Select it" onClick={() => useUi.setState({ selection: t.id, selected: [t.id] })}><MousePointerClick size={12} /></button>
            </dd>
          </Fragment>
        );
      })}
      <dt />
      <dd><button type="button" className="link" onClick={() => void execute('studio.removeBoolean', { ids: [node.id] })}>Remove operation</button></dd>
    </dl>
  );
}
