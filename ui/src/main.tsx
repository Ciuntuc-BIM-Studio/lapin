import { installRecentErrors } from './shell/HelpMenu';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { connect } from './engine/session';
import { guardWindowClose } from './engine/files';
import './theme.css';
import { startTranslator } from './i18n';

void connect();
void guardWindowClose();
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
// The interface in the language chosen (English, or Romanian).
startTranslator(document.getElementById('root')!);

installRecentErrors();
