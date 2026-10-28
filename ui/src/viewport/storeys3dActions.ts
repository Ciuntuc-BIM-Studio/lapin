/** Setting the storeys a 3D view shows (storeys3d.ts): from the ribbon, the browser's level menu and the 3D view's chip. */

import { useUi } from '../app/store';
import { execute } from '../engine/session';
import type { Storeys3d } from './storeys3d';

/** The 3D view in front's id as the engine names it ({3D}: "3d"). */
export const front3d = () => useUi.getState().view3dId ?? '3d';

/** Sets the 3D view in front's storeys shown (a patch), bringing the 3D in front when it is not. */
export function setStoreys3d(patch: Partial<Storeys3d>, viewId: string = front3d()): Promise<unknown> {
  if (useUi.getState().view !== '3d') useUi.setState({ view: '3d' });
  return execute('view.setStoreys', { id: viewId, ...patch });
}

/** Every storey again, uncut. */
export const showAllStoreys3d = (viewId?: string) => setStoreys3d({ mode: 'all', cut: null, unplaced: true }, viewId);

/** A level's right-click: it becomes the active storey, and the 3D shows it alone or with those below (following the active storey after). */
export function showLevelIn3d(storeyId: string, mode: 'active' | 'below'): Promise<unknown> {
  useUi.setState({ storeyId });
  return setStoreys3d({ mode });
}
