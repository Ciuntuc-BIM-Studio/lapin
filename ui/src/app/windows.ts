/**
 * Detached view windows (desktop): a view's tab opened in a window of its
 * own, to put on a second screen. The window is this same interface started
 * with `?pane=` (what to show), one pane, no ribbon; it connects to the same
 * engine, so the model is one. What the windows share (the storey, the
 * selection, the tool, the theme…) travels between them as Tauri events;
 * the view's own state (its zoom, the camera) stays with its window.
 */

import { brand } from '../platform/brand';
import { emptyPane, useUi, type Pane, type UiState } from './store';
import { isDesktop } from '../engine/files';
import { useSectionBox } from '../viewport/sectionBox';

const SHARED = ['storeyId', 'selected', 'selection', 'tool', 'theme', 'visualStyle', 'fog', 'refPlanes', 'planDrawing', 'wallTypeId', 'doorTypeId', 'windowTypeId', 'slabTypeId', 'roofStyle', 'keyPlan'] as const;
type Shared = Pick<UiState, (typeof SHARED)[number]>;

/** The pane a window was opened for (`?pane=…`), null for the main window. */
export function paneFromUrl(): Pane | null {
  try {
    const q = new URLSearchParams(window.location.search).get('pane');
    return q ? { ...emptyPane(), ...(JSON.parse(q) as Partial<Pane>) } : null;
  } catch { return null; }
}

let windows = 0;

/** A pane in a window of its own. */
export async function openViewWindow(pane: Pane, title = 'View', at?: { x: number; y: number }): Promise<void> {
  if (!isDesktop()) return;
  const { WebviewWindow } = await import('@tauri-apps/api/webviewWindow');
  const label = `view-${Date.now().toString(36)}-${++windows}`;
  // A tab dragged out of the window (decision 445) opens where it was dropped: its title bar under the pointer.
  const place = at ? { x: Math.max(0, Math.round(at.x - 160)), y: Math.max(0, Math.round(at.y - 16)) } : {};
  const w = new WebviewWindow(label, { url: `index.html?pane=${encodeURIComponent(JSON.stringify(pane))}`, title: `${brand.name} · ${title}`, width: 1100, height: 760, ...place,
    // As the main window (tauri.conf.json): HTML5 drag and drop left to the page, so a view drops onto a sheet here too (forum 206).
    dragDropEnabled: false });
  await new Promise<void>((resolve) => {
    void w.once('tauri://created', () => resolve());
    void w.once('tauri://error', (e) => { useUi.setState({ notice: `The window could not open: ${String(e.payload)}` }); resolve(); });
  });
}

type BoxState = Pick<ReturnType<typeof useSectionBox.getState>, 'box' | 'hidden'>;

/**
 * The section box is one thing for every window of the app (a view opened in a window of its own has no ribbon of its
 * own to set it from, and cuts by the box of the main one): its changes go out through `send`, the ones that come in
 * through the returned `apply`, which does not send them back.
 */
export function mirrorSectionBox(send: (s: BoxState) => void): { apply: (s: BoxState) => void; current: () => BoxState; stop: () => void } {
  let applying = false;
  const current = (): BoxState => { const { box, hidden } = useSectionBox.getState(); return { box, hidden }; };
  const stop = useSectionBox.subscribe((s, before) => { if (!applying && (s.box !== before.box || s.hidden !== before.hidden)) send({ box: s.box, hidden: s.hidden }); });
  const apply = (s: BoxState) => { applying = true; try { useSectionBox.setState({ box: s.box, hidden: s.hidden }); } finally { applying = false; } };
  return { apply, current, stop };
}

/** Keep the shared state in step between the windows of this app. */
export async function syncWindows(): Promise<void> {
  if (!isDesktop()) return;
  const { emit, listen } = await import('@tauri-apps/api/event');
  const { getCurrentWindow } = await import('@tauri-apps/api/window');
  const me = getCurrentWindow().label;
  const shared = (): Partial<Shared> => { const s = useUi.getState(); const out: Partial<Shared> = {}; for (const k of SHARED) (out as Record<string, unknown>)[k] = s[k]; return out; };
  let applying = false;
  await listen<{ from: string; patch: Partial<Shared> }>('ui-shared', (e) => {
    if (e.payload.from === me) return;
    applying = true;
    try { useUi.setState(e.payload.patch); } finally { applying = false; }
  });
  let prev = useUi.getState();
  useUi.subscribe((s) => {
    const before = prev;
    prev = s;
    if (applying) return;
    const patch: Partial<Shared> = {};
    for (const k of SHARED) if (s[k] !== before[k]) (patch as Record<string, unknown>)[k] = s[k];
    if (Object.keys(patch).length) void emit('ui-shared', { from: me, patch });
  });
  const boxes = mirrorSectionBox((b) => void emit('section-box', { from: me, ...b }));
  await listen<{ from: string } & BoxState>('section-box', (e) => { if (e.payload.from !== me) boxes.apply({ box: e.payload.box, hidden: e.payload.hidden }); });
  // A new window asks the main one where things stand.
  if (me === 'main') await listen('ui-hello', () => { void emit('ui-shared', { from: me, patch: shared() }); void emit('section-box', { from: me, ...boxes.current() }); });
  else void emit('ui-hello', { from: me });
}
