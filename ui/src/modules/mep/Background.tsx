/** Mounted while the workspace is open: a Connect left half-done is dropped when the tool changes. */

import { useEffect } from 'react';
import { useUi } from '../../app/store';
import { setMep, useMep } from './prefs';

export function Background() {
  const tool = useUi((s) => s.tool);
  useEffect(() => { if (tool !== 'mep.connect' && useMep.getState().pending) setMep({ pending: null }); }, [tool]);
  return null;
}
