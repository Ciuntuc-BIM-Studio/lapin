import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { connect } from './engine/session';
import './theme.css';

void connect();
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
