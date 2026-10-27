import { installRecentErrors } from './shell/support';
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

// A detached view window (desktop): one pane, straight into the workspace, no save prompt (the main window has it).
const detached = paneFromUrl();
if (detached) useUi.setState({ screen: 'workspace', windowRole: 'view', panes: [detached], pane: 0, ...detached });
void connect().then(() => { if (!detached) void openStartupFile(); });
if (!detached) void guardWindowClose();
// A newer build of the desktop app, looked for once the app has settled.
if (!detached) setTimeout(() => void checkForUpdates(), 8000);
void syncWindows();
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
// The interface in the language chosen (English, or Romanian).
startTranslator(document.getElementById('root')!);

installRecentErrors();
