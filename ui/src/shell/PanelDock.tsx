/**
 * Moving the side panels with the mouse or a finger (decision 754, a forum request): drag a panel by its header; the
 * zones show where it would land (docked on a side of the views, above or below the other panel, or floating over the
 * view), the drop commits it and Escape cancels. A floating panel is a window: dragged by its header, resized from
 * its corner, and docked back with its button. The pure rules are in app/panels.ts (`dropZone`, `dropChange`,
 * `clampFloat`); View → Panels stays the keyboard way (the four layouts, and float or dock each panel).
 */

import { useEffect, type ReactNode } from 'react';
import { Dock } from 'lucide-react';
import { clampFloat, dockPanel, dropZone, setFloatRect, toggleFloat, useDrag, usePanels, type Box, type DropZone, type FloatRect, type PanelId } from '../app/panels';

const THRESHOLD = 5;
const win = () => ({ w: window.innerWidth, h: window.innerHeight });
const box = (el: Element | null): Box | null => { const r = el?.getBoundingClientRect(); return r && r.width > 0 ? { left: r.left, top: r.top, right: r.right, bottom: r.bottom } : null; };
const NAME: Record<PanelId, string> = { browser: 'Project browser', props: 'Properties' };
const key = (panel: PanelId) => (panel === 'browser' ? 'browserFloat' : 'propsFloat');

/** The rectangles the zones are made from: the views, and the open docked panels. */
function layoutBoxes(): { views: Box | null; panels: Partial<Record<PanelId, Box>> } {
  const panels: Partial<Record<PanelId, Box>> = {};
  for (const id of ['browser', 'props'] as const) { const b = box(document.querySelector(`.side-col [data-panel-id="${id}"]`)); if (b) panels[id] = b; }
  return { views: box(document.querySelector('.body > .views')), panels };
}

const isHandle = (t: Element) => !!t.closest('.panel-head, .side-tabs') && !t.closest('button, input, select, textarea, a, [role=tab], [contenteditable]');

/** The pointer-down of a panel's wrapper: a drag begins when it moves a few pixels from the header. */
export function beginPanelDrag(e: React.PointerEvent, panel: PanelId): void {
  if (e.button !== 0 || !isHandle(e.target as Element)) return;
  const x0 = e.clientX, y0 = e.clientY;
  const s0 = usePanels.getState();
  const floating = panel === 'browser' ? s0.browserFloat : s0.propsFloat;
  const from = (e.currentTarget as Element).getBoundingClientRect();
  const dx = x0 - from.left, dy = y0 - from.top;
  let started = false;
  let zone: DropZone | null = null;
  const place = (x: number, y: number): FloatRect => ({ x: x - dx, y: y - dy, w: floating?.w ?? from.width, h: floating?.h ?? Math.min(from.height, 480) });
  const end = () => {
    window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up);
    window.removeEventListener('pointercancel', cancel); window.removeEventListener('keydown', onKey, true);
    document.body.classList.remove('panel-dragging');
    useDrag.setState({ drag: null });
  };
  const move = (ev: PointerEvent) => {
    if (!started) { if (Math.hypot(ev.clientX - x0, ev.clientY - y0) < THRESHOLD) return; started = true; document.body.classList.add('panel-dragging'); }
    const { views, panels } = layoutBoxes();
    zone = views ? dropZone(ev.clientX, ev.clientY, views, panels, panel) : null;
    if (floating) usePanels.setState({ [key(panel)]: clampFloat(place(ev.clientX, ev.clientY), win()) });
    useDrag.setState({ drag: { panel, x: ev.clientX, y: ev.clientY, zone } });
  };
  const up = (ev: PointerEvent) => {
    const wasStarted = started;
    end();
    if (!wasStarted) return;
    const at = clampFloat(place(ev.clientX, ev.clientY), win());
    if (zone && (zone.kind !== 'float' || !floating)) dockPanel(panel, zone, at);
    else if (floating) setFloatRect(panel, at, win());
  };
  const cancel = () => { if (started && floating) usePanels.setState({ [key(panel)]: floating }); end(); };
  const onKey = (ev: KeyboardEvent) => { if (ev.key === 'Escape') { ev.preventDefault(); ev.stopPropagation(); cancel(); } };
  window.addEventListener('pointermove', move);
  window.addEventListener('pointerup', up);
  window.addEventListener('pointercancel', cancel);
  window.addEventListener('keydown', onKey, true);
}

/** The zone's highlight while dragging, and a label by the pointer. */
export function DockOverlay() {
  const drag = useDrag((s) => s.drag);
  if (!drag) return null;
  const { views, panels } = layoutBoxes();
  let hl: Box | null = null;
  const z = drag.zone;
  if (z?.kind === 'stack') { const b = panels[z.target]; if (b) { const mid = (b.top + b.bottom) / 2; hl = z.where === 'above' ? { ...b, bottom: mid } : { ...b, top: mid }; } }
  else if (z?.kind === 'edge' && views) { const e = Math.min(160, (views.right - views.left) / 5); hl = z.side === 'left' ? { ...views, right: views.left + e } : { ...views, left: views.right - e }; }
  return (
    <div className="dock-overlay" data-testid="dock-overlay" data-zone={z ? z.kind : 'none'} aria-hidden>
      {hl && <div className="dock-zone" style={{ left: hl.left, top: hl.top, width: hl.right - hl.left, height: hl.bottom - hl.top }} />}
      <div className="dock-ghost" style={{ left: drag.x + 12, top: drag.y + 12 }}>{NAME[drag.panel]}</div>
    </div>
  );
}

/** A panel floating over the view: moved by its header, resized from its corner, docked back with its button. */
export function FloatingPanel({ panel, children }: { panel: PanelId; children: ReactNode }) {
  const rect = usePanels((s) => (panel === 'browser' ? s.browserFloat : s.propsFloat));
  const open = usePanels((s) => (panel === 'browser' ? s.browserOpen : s.propsOpen));
  if (!rect || !open) return null;
  const resize = (e: React.PointerEvent) => {
    e.stopPropagation(); e.preventDefault();
    const x0 = e.clientX, y0 = e.clientY;
    const move = (ev: PointerEvent) => usePanels.setState({ [key(panel)]: clampFloat({ ...rect, w: rect.w + ev.clientX - x0, h: rect.h + ev.clientY - y0 }, win()) });
    const up = () => {
      window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up);
      const cur = usePanels.getState()[key(panel)];
      if (cur) setFloatRect(panel, cur, win());
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };
  return (
    <div className={`side float-panel ${panel === 'browser' ? 'browser-side' : 'props-side'}`} data-panel-id={panel} data-testid={`float-${panel}`}
      style={{ left: rect.x, top: rect.y, width: rect.w, height: rect.h }} onPointerDown={(e) => beginPanelDrag(e, panel)}>
      <button type="button" className="icon mini panel-fold" title="Dock the panel" onClick={() => toggleFloat(panel, win())}><Dock size={13} /></button>
      {children}
      <div className="float-resize" onPointerDown={resize} role="separator" />
    </div>
  );
}

/** Keep floating panels inside the window when it is resized (and when the page opens in a smaller one). */
export function useKeepFloatsInWindow(): void {
  useEffect(() => {
    const fit = () => {
      const s = usePanels.getState();
      const patch: Record<string, FloatRect> = {};
      if (s.browserFloat) patch.browserFloat = clampFloat(s.browserFloat, win());
      if (s.propsFloat) patch.propsFloat = clampFloat(s.propsFloat, win());
      if (Object.keys(patch).length) usePanels.setState(patch);
    };
    fit();
    window.addEventListener('resize', fit);
    return () => window.removeEventListener('resize', fit);
  }, []);
}
