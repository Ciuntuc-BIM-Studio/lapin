/**
 * Python in Lapin (docs/plan-python-scripting.md, decision 489; docs/plan-python-notebook.md):
 * the open module `scripting`, in the free edition, through the modules' hooks only.
 * Registers `views.scripting` (opened beside the 3D/plan view or detached into a Tauri window,
 * like `views.graphs`), with CodeMirror lazy-loaded on first open.
 */

import { SquareTerminal } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { lazyPart } from '../../platform/lazyPart';
import { notify } from '../../app/store';
import { usePrefs } from '../../app/prefs';
import { openPanel, VIEW_KIND } from './store';
import { ScriptDock } from './ScriptDock';
import { addContentInstaller, contentInstallers } from '../../platform/extensions/runtime';
import { runExtension } from './scriptTools';

const ScriptView = lazyPart(() => import('./ScriptPanel').then((m) => m.ScriptPanel), { title: 'Script', inline: true });

// An installed script runs (its tool, else the whole script) from Extensions → Installed, after trust.
const scriptInstaller = contentInstallers().get('script');
if (scriptInstaller) addContentInstaller({ ...scriptInstaller, open: { label: 'Run script', run: (x) => void runExtension(x.id) } });

const extras = () => usePrefs.getState().developerExtras;

/** View → Script: opens the Python Notebook view beside the current view. */
function toggle(): void {
  if (!extras()) { notify('The Script panel is a developer extra: turn on Developer extras in Preferences (File → Preferences…, General).'); return; }
  void openPanel();
}

const scripting: LapinModule = {
  id: 'scripting',
  name: 'Scripting',
  summary: 'Python notebook in Lapin: code, Markdown and Ollama AI prompt cells with a live terminal, macro recorder, and Code → Graph conversion.',
  tabs: [{
    id: 'view', label: 'View', order: 90,
    groups: [{ label: 'Developer', shown: extras, tools: [{ id: 'scripting.panel', label: 'Script', icon: SquareTerminal, run: toggle }] }],
  }],
  views: { [VIEW_KIND]: { label: 'Script', component: ScriptView } },
  background: ScriptDock,
};

export default scripting;
