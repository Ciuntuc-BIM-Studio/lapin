import { installRecentErrors } from './shell/support';
import { installPageGuards } from './app/pageGuards';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { connect } from './engine/session';
import { guardWindowClose, openStartupFile } from './engine/files';
import './theme.css';
import { startTranslator } from './i18n';
import { useUi } from './app/store';
import { paneFromUrl, syncWindows } from './app/windows';
import { checkForUpdates } from './app/updates';
import { isGuest, savedName } from './engine/share';

// A detached view window (desktop): one pane, straight into the workspace, no save prompt (the main window has it).
const detached = paneFromUrl();
if (detached) useUi.setState({ screen: 'workspace', windowRole: 'view', panes: [detached], pane: 0, ...detached });
if (isGuest()) {
  // A guest of a shared session (docs/plan-multiplayer.md): straight into the host's project, once their name is known.
  if (savedName()) { useUi.setState({ screen: 'workspace' }); void connect(); } else useUi.setState({ screen: 'join' });
} else {
  void connect().then(() => { if (!detached) void openStartupFile(); });
  if (!detached) void guardWindowClose();
}
// A newer build of the desktop app, looked for once the app has settled.
if (!detached) setTimeout(() => void checkForUpdates(), 8000);
void syncWindows();
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
// The interface in the language chosen (English, Romanian or Spanish).
startTranslator(document.getElementById('root')!);

installRecentErrors();
// The page stays still under the views: the wheel and a pinch zoom the view, not the page.
installPageGuards();
