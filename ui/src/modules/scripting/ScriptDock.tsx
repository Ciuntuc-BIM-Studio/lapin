/**
 * The scripting module's background: keeps script tools in My tools, relays the
 * window's selection to the engine's host for `lapin.selection()`, and renders
 * script dialogs (`lapin.ui.ask`) and palettes (`lapin.ui.palette`).
 */

import { useEffect } from 'react';
import { useUi } from '../../app/store';
import { engine, engineInBrowser } from '../../engine/session';
import { forgetSentSelection, tellSelection } from './store';
import { startScriptTools } from './scriptTools';
import { ScriptDialogs } from './AskDialog';
import { ScriptPalettes } from './PaletteWindow';

export function ScriptDock() {
  useEffect(() => { startScriptTools(); }, []);
  useEffect(() => {
    if (engineInBrowser) return;
    let timer: ReturnType<typeof setTimeout> | null = null;
    const soon = () => { if (!timer) timer = setTimeout(() => { timer = null; tellSelection(); }, 100); };
    soon();
    const off = useUi.subscribe((s, was) => { if (s.selected !== was.selected || s.selection !== was.selection) soon(); });
    const back = engine.onStatus((connected) => { if (connected) { forgetSentSelection(); soon(); } });
    return () => { off(); back(); if (timer) clearTimeout(timer); };
  }, []);
  if (engineInBrowser) return null;
  return <><ScriptDialogs /><ScriptPalettes /></>;
}
