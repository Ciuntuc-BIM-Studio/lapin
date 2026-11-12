/**
 * Python in Lapin (docs/plan-python-scripting.md, stage 2; decision 489): the open module `scripting`, in the free
 * edition, through the modules' hooks only. Its engine half is the host's script.* methods (Lapin.Host/ScriptHost.cs:
 * the user's own Python, the lapinbim kernel, one undo step per Run); this half is the Script panel, behind
 * Preferences → Developer extras (off by default: then nothing of it shows). The panel and CodeMirror are fetched the
 * first time it opens.
 *
 * Stage 4: a script's tools in My tools (its header's `tool=`, or `lapin.ui.tool`), its questions as dialogs of the
 * window's own (`lapin.ui.ask`), and a script installed from Lapin Extensions run from Installed → Run or its tool,
 * after trust. Those work with Developer extras off too (scriptTools.ts, the module's background).
 */

import { SquareTerminal } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { notify } from '../../app/store';
import { usePrefs } from '../../app/prefs';
import { closePanel, openPanel, useScripting } from './store';
import { ScriptDock } from './ScriptDock';
import { addContentInstaller, contentInstallers } from '../../platform/extensions/runtime';
import { runExtension } from './scriptTools';

// An installed script runs (its tool, else the whole script) from Extensions → Installed, after trust.
const scriptInstaller = contentInstallers().get('script');
if (scriptInstaller) addContentInstaller({ ...scriptInstaller, open: { label: 'Run script', run: (x) => void runExtension(x.id) } });

const extras = () => usePrefs.getState().developerExtras;

/** View → Script: the panel opened, or closed when it is open. */
function toggle(): void {
  if (!extras()) { notify('The Script panel is a developer extra: turn on Developer extras in Preferences (File → Preferences…, General).'); return; }
  if (useScripting.getState().open) closePanel(); else void openPanel();
}

const scripting: LapinModule = {
  id: 'scripting',
  name: 'Scripting',
  summary: 'Python in Lapin: a console and an editor that run your own Python (3.11 or newer) with the lapinbim package; each Run is one undo step. A developer extra.',
  tabs: [{
    // No tab of its own: a group on View, shown with Developer extras on.
    id: 'view', label: 'View', order: 90,
    groups: [{ label: 'Developer', shown: extras, tools: [{ id: 'scripting.panel', label: 'Script', icon: SquareTerminal, run: toggle }] }],
  }],
  background: ScriptDock,
};

export default scripting;
