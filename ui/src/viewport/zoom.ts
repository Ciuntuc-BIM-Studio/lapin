/**
 * Zoom in the 2D views (plans, sections, elevations, sheets): the commands
 * of the command line (Z window, ZE everything, ZS the selection, ZP the
 * zoom before, ZA the paper size), the + and − keys, the scale chip, the
 * views remembered for the session so a tab comes back as it was left.
 * Each view lends the active pane's host; the commands talk to it.
 */

import { create } from 'zustand';
import { useEffect, useRef } from 'react';
import type { Vec2 } from '../platform/module';
import { cadLog, registerCommand, type Step } from './cad/session';

/** A 2D view: its centre (metres) and pixels per metre. */
export interface View2 { cx: number; cy: number; scale: number }
export type Bounds = [number, number, number, number];

/** CSS pixels per millimetre: a screen is taken at 96 dpi (what the browser calls a pixel). */
export const PX_PER_MM = 96 / 25.4;
/** Pixels per metre at which a drawing at 1:scale shows paper-sized. */
export const actualScale = (scale: number) => (PX_PER_MM * 1000) / scale;

export interface ZoomHost {
  fitAll(): void;
  /** False when nothing is selected in the view. */
  fitSelection(): boolean;
  /** Two corners (view coordinates). */
  zoomWindow(a: Vec2, b: Vec2): void;
  /** The drawing at its paper size. */
  actual(): void;
  /** About the view's centre. */
  zoomBy(factor: number): void;
  /** The zoom before the last one; false when there is none. */
  previous(): boolean;
}

export const useZoom = create<{ host: ZoomHost | null }>(() => ({ host: null }));
export const zoomHost = () => useZoom.getState().host;

/** The view in front lends its zoom to the commands and keys while its pane is active. */
export function useZoomHost(active: boolean, host: ZoomHost): void {
  const ref = useRef(host);
  ref.current = host;
  useEffect(() => {
    if (!active) return;
    const proxy: ZoomHost = {
      fitAll: () => ref.current.fitAll(), fitSelection: () => ref.current.fitSelection(), zoomWindow: (a, b) => ref.current.zoomWindow(a, b),
      actual: () => ref.current.actual(), zoomBy: (f) => ref.current.zoomBy(f), previous: () => ref.current.previous(),
    };
    useZoom.setState({ host: proxy });
    return () => { if (useZoom.getState().host === proxy) useZoom.setState({ host: null }); };
  }, [active]);
}

// ─── Remembered views and the zoom history, per view key, for the session ──

const kept = new Map<string, unknown>();
export const recall = <T,>(key: string): T | undefined => kept.get(key) as T | undefined;
export const remember = (key: string, v: unknown): void => { kept.set(key, v); };

const history = new Map<string, unknown[]>();
/** The view before a zoom, kept for ZP (ten deep). */
export function pushHistory(key: string, v: unknown): void {
  const h = history.get(key) ?? [];
  h.push(v);
  history.set(key, h.slice(-10));
}
export function popHistory<T>(key: string): T | undefined {
  return history.get(key)?.pop() as T | undefined;
}

/** A wheel's step in pixels, however the device reports it (pixels, lines or pages). */
export const wheelPixels = (e: { deltaY: number; deltaMode: number }, pageHeight: number) =>
  e.deltaMode === 1 ? e.deltaY * 16 : e.deltaMode === 2 ? e.deltaY * pageHeight : e.deltaY;

/** The view framing some bounds in a box of pixels. */
export function framing(b: Bounds, size: { w: number; h: number }, pad = 0.85, min = 2, max = 4000): View2 {
  const w = Math.max(b[2] - b[0], 1e-3), h = Math.max(b[3] - b[1], 1e-3);
  const scale = Math.min(size.w / w, size.h / h) * pad;
  return { cx: (b[0] + b[2]) / 2, cy: (b[1] + b[3]) / 2, scale: Math.max(min, Math.min(scale, max)) };
}

export const boundsOf = (pts: Iterable<Vec2>): Bounds | null => {
  let b: Bounds | null = null;
  for (const p of pts) b = b ? [Math.min(b[0], p.x), Math.min(b[1], p.y), Math.max(b[2], p.x), Math.max(b[3], p.y)] : [p.x, p.y, p.x, p.y];
  return b;
};

/** A middle button pressed twice quickly: fit everything (as in CAD). */
export function middleDouble(): (e: { button: number }) => boolean {
  let last = -Infinity;
  return (e) => {
    if (e.button !== 1) return false;
    const now = performance.now();
    const twice = now - last < 350;
    last = twice ? -Infinity : now;
    return twice;
  };
}

// ─── Commands ──────────────────────────────────────────────────────────────

function withHost(f: (h: ZoomHost) => void): null {
  const h = zoomHost();
  if (!h) cadLog('Open a plan, section, elevation or sheet to zoom.');
  else f(h);
  return null;
}

function zoomWindow(): Step {
  return {
    prompt: 'Zoom: first corner [Extents/Selection/Previous/Actual size]', want: 'point',
    keywords: {
      E: () => withHost((h) => h.fitAll()),
      S: () => withHost((h) => { if (!h.fitSelection()) cadLog('Nothing is selected in this view.'); }),
      P: () => withHost((h) => { if (!h.previous()) cadLog('No zoom before this one.'); }),
      A: () => withHost((h) => h.actual()),
    },
    point: (a) => ({
      prompt: 'Other corner', want: 'point', base: a,
      preview: (c) => [{ type: 'polyline', pts: [a, { x: c.x, y: a.y }, c, { x: a.x, y: c.y }], closed: true }],
      point: (b) => withHost((h) => h.zoomWindow(a, b)),
    }),
  };
}

registerCommand({ names: ['ZOOM', 'Z', 'ZW'], label: 'Zoom window', start: zoomWindow });
registerCommand({ names: ['ZE', 'ZOOMEXTENTS', 'ZOOMALL'], label: 'Zoom everything', start: () => withHost((h) => h.fitAll()) });
registerCommand({ names: ['ZS', 'ZOOMSELECTION'], label: 'Zoom to the selection', start: () => withHost((h) => { if (!h.fitSelection()) cadLog('Nothing is selected in this view.'); }) });
registerCommand({ names: ['ZP', 'ZOOMPREVIOUS'], label: 'Zoom before', start: () => withHost((h) => { if (!h.previous()) cadLog('No zoom before this one.'); }) });
registerCommand({ names: ['ZA', 'ZOOMACTUAL', 'ZOOMPAPER'], label: 'Paper size', start: () => withHost((h) => h.actual()) });
registerCommand({ names: ['ZI', 'ZOOMIN'], label: 'Zoom in', start: () => withHost((h) => h.zoomBy(1.25)) });
registerCommand({ names: ['ZO', 'ZOOMOUT'], label: 'Zoom out', start: () => withHost((h) => h.zoomBy(0.8)) });
