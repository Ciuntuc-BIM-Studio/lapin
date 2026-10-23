import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { connect } from './engine/session';
import { guardWindowClose } from './engine/files';
import './theme.css';

void connect();
void guardWindowClose();
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
