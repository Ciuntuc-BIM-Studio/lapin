/**
 * Context-aware Radial Pie Menu (docs/plan-ux-game-like.md, P5):
 * opens at the cursor on Alt+Q (or Commands… → Radial menu) with 8 ergonomic
 * sectors adapted to whether an element is selected or the canvas is empty.
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { Box, Copy, CopyCheck, Eye, EyeOff, Footprints, Maximize, Move, RotateCw, Ruler, Trash2, X } from 'lucide-react';
import { useUi } from '../app/store';
import { runCommand } from '../app/commands';
import { activate, findTool } from '../platform/modules';
import { hideSelection, isolateSelection } from '../viewport/temporary';
import { startCommand } from '../viewport/cad/session';
import { useSession } from '../engine/session';
import { deleteElements } from '../engine/dependents';
import { SectionIcon } from '../icons';

export interface PieSector {
  id: string;
  label: string;
  shortcut?: string;
  icon: typeof Move;
  run: () => void;
}

let lastPointer = { x: 400, y: 300 };

export const usePieMenu = create<{ open: boolean; x: number; y: number }>(() => ({ open: false, x: 400, y: 300 }));

export function openPieMenu(x = lastPointer.x, y = lastPointer.y): void {
  const pad = 132;
  const cx = typeof window !== 'undefined' ? Math.max(pad, Math.min(window.innerWidth - pad, x)) : x;
  const cy = typeof window !== 'undefined' ? Math.max(pad, Math.min(window.innerHeight - pad, y)) : y;
  usePieMenu.setState({ open: true, x: cx, y: cy });
}

export function closePieMenu(): void {
  usePieMenu.setState({ open: false });
}

export function togglePieMenu(): void {
  if (usePieMenu.getState().open) closePieMenu();
  else openPieMenu();
}

/** Tracks cursor position lightly so Alt+Q opens the pie menu right under the pointer. */
export function usePiePointer(): void {
  useEffect(() => {
    const onMove = (e: PointerEvent) => { lastPointer = { x: e.clientX, y: e.clientY }; };
    window.addEventListener('pointermove', onMove, { passive: true });
    return () => window.removeEventListener('pointermove', onMove);
  }, []);
}

function runTool(id: string): void {
  const t = findTool(id);
  if (t) activate(t);
}

/** Pure sector builder (8 sectors around 360°), adapted to selection state. */
export function pieSectors(hasSelection: boolean): PieSector[] {
  if (hasSelection) {
    return [
      { id: 'move', label: 'Move', shortcut: 'Ctrl+D', icon: Move, run: () => void startCommand('MOVE') },
      { id: 'copy', label: 'Copy', shortcut: 'Ctrl+Shift+D', icon: Copy, run: () => void startCommand('COPY') },
      { id: 'rotate', label: 'Rotate', shortcut: 'Ctrl+E', icon: RotateCw, run: () => void startCommand('ROTATE') },
      { id: 'similar', label: 'Create similar', shortcut: 'Alt+I', icon: CopyCheck, run: () => runCommand('app.eyedropper') },
      { id: 'isolate', label: 'Isolate', shortcut: 'HI', icon: Eye, run: isolateSelection },
      { id: 'hide', label: 'Hide', shortcut: 'HH', icon: EyeOff, run: hideSelection },
      { id: 'zoomSel', label: 'Zoom selection', shortcut: 'ZS', icon: Maximize, run: () => runCommand('app.zoomSelection') },
      { id: 'delete', label: 'Delete', shortcut: 'Del', icon: Trash2, run: () => {
        const nodes = useSession.getState().nodes;
        const ids = useUi.getState().selected.filter((id) => nodes.get(id)?.type !== 'LapinView');
        if (ids.length) { void deleteElements(ids); useUi.setState({ selection: null, selected: [] }); }
      } },
    ];
  }
  return [
    { id: 'wall', label: 'Wall', shortcut: 'WA', icon: Box, run: () => runTool('architecture.wall') },
    { id: 'door', label: 'Door', shortcut: 'DR', icon: Box, run: () => runTool('architecture.door') },
    { id: 'window', label: 'Window', shortcut: 'WN', icon: Box, run: () => runTool('architecture.window') },
    { id: 'slab', label: 'Floor', shortcut: 'SB', icon: Box, run: () => runTool('architecture.slab') },
    { id: 'measure', label: 'Measure', icon: Ruler, run: () => runTool('measure') },
    { id: 'section', label: 'Section', icon: SectionIcon as typeof Move, run: () => runTool('view.section') },
    { id: 'walk', label: 'Walk in 3D', icon: Footprints, run: () => { const el = document.querySelector<HTMLButtonElement>('[data-testid="quick-view.walk"]'); el?.click(); } },
    { id: 'zoomFit', label: 'Zoom fit', shortcut: 'ZE', icon: Maximize, run: () => runCommand('app.zoomFit') },
  ];
}

const RADIUS = 88;

export function PieMenu() {
  const { open, x, y } = usePieMenu();
  const hasSelection = useUi((s) => s.selected.length > 0);
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); closePieMenu(); } };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [open]);

  if (!open) return null;
  const sectors = pieSectors(hasSelection);
  return (
    <div className="pie-backdrop" role="presentation" onPointerDown={(e) => { if (e.target === e.currentTarget) closePieMenu(); }}>
      <div className="pie-ring" role="menu" aria-label="Radial menu" data-testid="pie-menu" style={{ left: x, top: y }}>
        <button type="button" className="pie-center" title="Close (Esc)" aria-label="Close" onClick={closePieMenu}><X size={14} /></button>
        {sectors.map((s, i) => {
          const angle = -Math.PI / 2 + (i * 2 * Math.PI) / sectors.length;
          const dx = Math.round(Math.cos(angle) * RADIUS);
          const dy = Math.round(Math.sin(angle) * RADIUS);
          const Icon = s.icon;
          return (
            <button key={s.id} type="button" role="menuitem" className="pie-sector" data-pie={s.id}
              style={{ transform: `translate(${dx}px, ${dy}px)` }}
              onClick={() => { closePieMenu(); s.run(); }}>
              <Icon size={15} strokeWidth={1.6} />
              <span className="pie-label">{s.label}</span>
              {s.shortcut && <kbd data-no-i18n>{s.shortcut}</kbd>}
            </button>
          );
        })}
      </div>
    </div>
  );
}
