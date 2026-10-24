/** A dimension in the inspector: what it measures, a value typed to drive it, and, when it lost its element, freeing it. */

import { useEffect, useState } from 'react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';

export function DimensionSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const [d, setD] = useState<{ value: number; orphan: boolean; driving: boolean } | null>(null);
  useEffect(() => { void query<{ value: number; orphan: boolean; driving: boolean }>('drafting.dimension', { id: node.id }).then(setD); }, [node.id, nodes]);
  if (node.props.predefinedType !== 'DIMENSION' || !d) return null;
  const drive = (v: string) => { const n = Number(v.replace(',', '.')); if (n > 0 && Math.abs(n - d.value) > 1e-6) void execute('drafting.setDimensionValue', { id: node.id, value: n }); };
  return (
    <dl className="fields">
      <dt>Value (m)</dt>
      <dd><input key={d.value} defaultValue={d.value.toFixed(3)} disabled={!d.driving || d.orphan} title={d.driving ? 'Type a value: the element it measures moves (and, in a string, what follows)' : 'Neither end holds on an element'}
        onKeyDown={(e) => { if (e.key === 'Enter') drive((e.target as HTMLInputElement).value); }} onBlur={(e) => drive(e.target.value)} /></dd>
      {d.orphan && <>
        <dt />
        <dd className="error-text">It lost its element. <button type="button" onClick={() => void execute('drafting.freeDimensions', { ids: [node.id] })}>Free it</button></dd>
      </>}
    </dl>
  );
}
