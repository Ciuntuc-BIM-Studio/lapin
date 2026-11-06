/**
 * What a right-click on a plan or the 3D view offers for the selection: copy
 * it to other storeys, or move it to another (their dialogs).
 */

import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { copyable } from '../modules/core/CopyToStoreysDialog';
import type { MenuItem } from './ContextMenu';

export function selectionItems(): MenuItem[] {
  const { selected } = useUi.getState();
  if (!copyable(selected, useSession.getState().nodes).length) return [];
  return [
    { label: 'Copy to storeys…', onSelect: () => useUi.setState({ dialog: 'copyToStoreys' }) },
    { label: 'Move to storey…', onSelect: () => useUi.setState({ dialog: 'moveToStorey' }) },
  ];
}
