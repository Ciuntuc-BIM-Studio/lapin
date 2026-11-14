/**
 * A type's Duplicate, Move up and Move down (forum #71), the same in every type dialog: the copy is the engine's
 * (types.duplicate: a type of its own, „Name (2)”, listed after the original), the order the project's for the type's
 * class (types.move), read back by every type list and picker (ofClass). Each is one undo step.
 */

import { ArrowDown, ArrowUp, Copy } from 'lucide-react';
import { execute } from '../engine/session';

export function TypeListActions({ id, onDuplicated, first, last }: { id: string | null; onDuplicated?: (id: string) => void; first?: boolean; last?: boolean }) {
  if (!id) return null;
  return (
    <span className="row type-list-actions" data-type-actions>
      <button type="button" className="btn small" data-type-duplicate title="A copy of this type, with its own layers, to change" onClick={() => void (execute('types.duplicate', { id }) as Promise<{ id?: string } | null>).then((r) => { if (r?.id) onDuplicated?.(r.id); })}><Copy size={13} /> Duplicate</button>
      <button type="button" className="icon mini" data-type-up title="Move the type up its list" disabled={first} onClick={() => void execute('types.move', { id, by: -1 })}><ArrowUp size={12} /></button>
      <button type="button" className="icon mini" data-type-down title="Move the type down its list" disabled={last} onClick={() => void execute('types.move', { id, by: 1 })}><ArrowDown size={12} /></button>
    </span>
  );
}

/** Whether a type is first or last of its list, for its Move up and Move down. */
export const endsOf = (types: { id: string }[], id: string | null) => ({ first: !id || types[0]?.id === id, last: !id || types[types.length - 1]?.id === id });
