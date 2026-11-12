/**
 * The Script panel's place in the workspace (the module's background): drawn while it is open and Developer extras are
 * on, fetched the first time. With Developer extras on it also tells the engine's host what the window has selected,
 * as it changes, so a script's lapin.selection() reads it (stage 3).
 * Stage 4, with or without Developer extras: the scripts' tools kept in My tools (scriptTools.ts), the questions scripts ask
 * (lapin.ui.ask) and a tool's trust question (AskDialog.tsx).
 */

import { useEffect } from 'react';
import { lazyPart } from '../../platform/lazyPart';
import { usePrefs } from '../../app/prefs';
import { useUi } from '../../app/store';
import { engine, engineInBrowser } from '../../engine/session';
import { forgetSentSelection, tellSelection, useScripting } from './store';
import { startScriptTools } from './scriptTools';
import { ScriptDialogs } from './AskDialog';

const ScriptPanel = lazyPart(() => import('./ScriptPanel').then((m) => m.ScriptPanel), { title: 'Script', inline: true });

export function ScriptDock() {
  const open = useScripting((s) => s.open);
  const on = usePrefs((s) => s.developerExtras);
  useEffect(() => { startScriptTools(); }, []);
  useEffect(() => {
    if (!on || engineInBrowser) return;
    let timer: ReturnType<typeof setTimeout> | null = null;
    const soon = () => { if (!timer) timer = setTimeout(() => { timer = null; tellSelection(); }, 100); };
    soon();
    const off = useUi.subscribe((s, was) => { if (s.selected !== was.selected || s.selection !== was.selection) soon(); });
    const back = engine.onStatus((connected) => { if (connected) { forgetSentSelection(); soon(); } });
    return () => { off(); back(); if (timer) clearTimeout(timer); };
  }, [on]);
  if (engineInBrowser) return on && open ? <ScriptPanel /> : null;
  return <>{on && open && <ScriptPanel />}<ScriptDialogs /></>;
}
