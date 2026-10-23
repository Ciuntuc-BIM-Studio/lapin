/**
 * The keyboard: Ctrl/Cmd shortcuts (undo, redo, new, open, save), Delete for
 * the selection, 1–9 for the hotbar, and Revit's two-letter shortcuts (WA
 * for Wall…) typed within a second.
 */

import { useEffect } from 'react';
import { select, useUi } from './store';
import { activate, allTools, hotbarTools } from '../platform/modules';
import { execute, redo, undo } from '../engine/session';
import { newProject, openProject, saveProject } from '../engine/files';

export function useShortcuts(): void {
  useEffect(() => {
    let typed = '';
    let at = 0;
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement)?.closest('input, select, textarea')) return;
      if (useUi.getState().screen !== 'workspace') return;
      const mod = e.ctrlKey || e.metaKey;
      const k = e.key.toLowerCase();
      if (mod) {
        const act = ({ z: e.shiftKey ? redo : undo, y: redo, n: newProject, o: () => openProject(), s: () => saveProject(e.shiftKey) } as Record<string, () => unknown>)[k];
        if (act) { e.preventDefault(); void act(); }
        return;
      }
      if (e.key === 'Delete' || e.key === 'Backspace') {
        const id = useUi.getState().selection;
        if (id) { e.preventDefault(); void execute('element.delete', { id }); select(null); }
        return;
      }
      const n = Number(e.key);
      if (n >= 1 && n <= 9) { const t = hotbarTools()[n - 1]; if (t) activate(t); return; }
      if (!/^[a-z]$/.test(k)) return;
      const now = performance.now();
      typed = now - at < 1000 ? (typed + k).slice(-2) : k;
      at = now;
      if (typed.length === 2) {
        const t = allTools().find((x) => x.shortcut?.toLowerCase() === typed);
        if (t) { activate(t); typed = ''; }
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
}
