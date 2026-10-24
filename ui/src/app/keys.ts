/**
 * The keyboard: Ctrl/Cmd shortcuts (undo, redo, new, open, save); typing goes
 * to the command line (CAD commands and coordinates, Enter or Space to run,
 * Escape to cancel), where Revit's two-letter shortcuts (WA for Wall…) start
 * their tool at once; Delete for the selection; 1–9 for the hotbar when
 * nothing is being typed; F3 object snap, F8 ortho, F10 polar tracking.
 */

import { useEffect } from 'react';
import { select, useUi } from './store';
import { activate, allTools, hotbarTools } from '../platform/modules';
import { execute, redo, undo } from '../engine/session';
import { newProject, openProject, saveProject } from '../engine/files';
import { cancel, submit, useCad } from '../viewport/cad/session';

export function useShortcuts(): void {
  useEffect(() => {
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
      const cad = useCad.getState();
      const toggles: Record<string, () => void> = {
        F3: () => useCad.setState({ osnap: !cad.osnap }),
        F8: () => useCad.setState({ ortho: !cad.ortho }),
        F10: () => useCad.setState({ polar: !cad.polar }),
      };
      if (toggles[e.key]) { e.preventDefault(); toggles[e.key](); return; }
      const typing = cad.buffer.length > 0 || !!cad.step;
      if (e.key === 'Escape') { if (cad.step || cad.buffer) cancel(); return; }
      if (e.key === 'Enter' || (e.key === ' ' && (typing || !!cad.last) && cad.step?.want !== 'text')) {
        // Plan gestures (a floor's outline) take their own Enter.
        if (!typing && !cad.last) return;
        if (!typing && useUi.getState().tool) return;
        e.preventDefault();
        void submit(cad.buffer);
        return;
      }
      if (e.key === 'Backspace' && cad.buffer) { e.preventDefault(); useCad.setState({ buffer: cad.buffer.slice(0, -1) }); return; }
      if (e.key === 'Delete' || e.key === 'Backspace') {
        const ids = useUi.getState().selected;
        if (ids.length) { e.preventDefault(); void execute('element.delete', { ids }); select(null); }
        return;
      }
      const n = Number(e.key);
      if (!typing && n >= 1 && n <= 9) { const t = hotbarTools()[n - 1]; if (t) activate(t); return; }
      // A text step takes any character; otherwise letters, digits and the coordinate signs.
      const allowed = cad.step?.want === 'text' ? e.key.length === 1 : /^[\w@<.,\-+]$/.test(e.key);
      if (!allowed) return;
      e.preventDefault();
      const buffer = cad.buffer + e.key;
      useCad.setState({ buffer });
      // Two letters that are a tool's shortcut start it at once (when no command is running).
      if (!cad.step && /^[a-z]{2}$/i.test(buffer)) {
        const t = allTools().find((x) => x.shortcut?.toLowerCase() === buffer.toLowerCase());
        if (t) { useCad.setState({ buffer: '' }); activate(t); }
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
}
