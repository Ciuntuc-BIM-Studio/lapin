/**
 * Detached view windows (desktop): a view's tab opened in a window of its
 * own, to put on a second screen. The window is this same interface started
 * with `?pane=` (what to show), one pane, no ribbon; it connects to the same
 * engine, so the model is one. What the windows share (the storey, the
 * selection, the tool, the theme…) travels between them as Tauri events;
 * the view's own state (its zoom, the camera) stays with its window.
 */

import { emptyPane, useUi, type Pane, type UiState } from './store';
import { isDesktop } from '../engine/files';

const SHARED = ['storeyId', 'selected', 'selection', 'tool', 'theme', 'visualStyle', 'planDrawing', 'wallTypeId', 'doorTypeId', 'windowTypeId', 'slabTypeId', 'roofStyle', 'keyPlan'] as const;
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
export async function openViewWindow(pane: Pane, title = 'View'): Promise<void> {
  if (!isDesktop()) return;
  const { WebviewWindow } = await import('@tauri-apps/api/webviewWindow');
  const label = `view-${Date.now().toString(36)}-${++windows}`;
  const w = new WebviewWindow(label, { url: `index.html?pane=${encodeURIComponent(JSON.stringify(pane))}`, title: `Lapin · ${title}`, width: 1100, height: 760 });
  await new Promise<void>((resolve) => {
    void w.once('tauri://created', () => resolve());
    void w.once('tauri://error', (e) => { useUi.setState({ notice: `The window could not open: ${String(e.payload)}` }); resolve(); });
  });
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
  // A new window asks the main one where things stand.
  if (me === 'main') await listen('ui-hello', () => void emit('ui-shared', { from: me, patch: shared() }));
  else void emit('ui-hello', { from: me });
}
