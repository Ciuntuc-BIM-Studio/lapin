/** The Script panel's place in the workspace (the module's background): drawn while it is open and Developer extras are on, fetched the first time. */

import { lazyPart } from '../../platform/lazyPart';
import { usePrefs } from '../../app/prefs';
import { useScripting } from './store';

const ScriptPanel = lazyPart(() => import('./ScriptPanel').then((m) => m.ScriptPanel), { title: 'Script', inline: true });

export function ScriptDock() {
  const open = useScripting((s) => s.open);
  const on = usePrefs((s) => s.developerExtras);
  return on && open ? <ScriptPanel /> : null;
}
