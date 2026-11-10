/**
 * Pinned elements (decision 449): Modify → Pin and Unpin, and the Pinned box in an element's properties. A pinned element
 * is not moved, turned, mirrored, sent to another storey nor deleted, and shows no grips; its fields still change.
 */
import { notify, useUi } from '../../app/store';
import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';

export const isPinned = (n?: NodeDto | null): boolean => n?.props.pinned === true;

export async function setPinned(ids: string[], pinned: boolean): Promise<void> {
  if (!ids.length) { notify(pinned ? 'Select what to pin first.' : 'Select what to unpin first.'); return; }
  await execute('element.pin', { ids, pinned });
}

export const pinSelection = (pinned: boolean) => setPinned(useUi.getState().selected, pinned);
