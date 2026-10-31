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

/**
 * What a run of wheel events does, pan or zoom, decided once per run. A
 * mouse wheel zooms, as in every CAD program; so does a trackpad's pinch (the
 * browser reports it with Ctrl) and its two fingers moving up and down. Two
 * fingers moving sideways pan, and so does the wheel with Shift held. The
 * decision is taken on the run's first clear movement (2 px, the deltas
 * added up) and kept until the run pauses for `gap` ms: a Magic Mouse sends
 * both deltas at once, and deciding on each event made the view alternate
 * between zooming and panning. Null: too small to tell yet. (Telling a mouse
 * from a trackpad by whole-number deltas does not work: Chrome on macOS
 * reports a mouse wheel as 4.000244…, so the wheel used to pan instead of
 * zooming.)
 */
export function wheelGesture(gap = 250): (e: { deltaX: number; deltaY: number; deltaMode: number; ctrlKey: boolean; shiftKey: boolean }, now?: number) => 'pan' | 'zoom' | null {
  let last = -Infinity, mode: 'pan' | 'zoom' | null = null, ax = 0, ay = 0;
  return (e, now = performance.now()) => {
    if (now - last > gap) { mode = null; ax = ay = 0; }
    last = now;
    if (e.ctrlKey) return 'zoom';
    if (e.shiftKey) return 'pan';
    if (mode) return mode;
    ax += wheelPixels({ deltaY: e.deltaX, deltaMode: e.deltaMode }, 1000);
    ay += wheelPixels(e, 1000);
    if (Math.max(Math.abs(ax), Math.abs(ay)) < 2) return null;
    return (mode = ax !== 0 && Math.abs(ax) >= Math.abs(ay) ? 'pan' : 'zoom');
  };
}

/** A wheel's step in pixels, however the device reports it (pixels, lines or pages). */
export const wheelPixels = (e: { deltaY: number; deltaMode: number }, pageHeight: number) =>
  e.deltaMode === 1 ? e.deltaY * 16 : e.deltaMode === 2 ? e.deltaY * pageHeight : e.deltaY;

/**
 * The most one wheel event zooms without Ctrl, as the exponent: exp(0.2) is
 * 22%, about a notch. macOS accelerates a fast wheel to hundreds of pixels an
 * event, and a 3D mouse's push is emulated as a wheel with big steps: held to
 * a notch each, they zoom in several steps instead of one jump (report #50).
 */
export const WHEEL_STEP = 0.2;

/**
 * How much a wheel event zooms (the factor the view's scale is multiplied by),
 * in proportion to how far it turns, so a trackpad's many small events zoom
 * smoothly and a mouse's notch is one step, and no event more than WHEEL_STEP.
 * A pinch (Ctrl, as the browsers report it, Safari's gestures included:
 * pageGuards.ts) stands for a zoom by exp(−deltaY / 100), followed one to one
 * under the fingers; Ctrl with a mouse wheel (100 or 120 a notch) is held to
 * about a quarter a notch.
 */
export function wheelZoom(e: { deltaY: number; deltaMode: number; ctrlKey: boolean }, pageHeight: number): number {
  const px = wheelPixels(e, pageHeight);
  const k = e.ctrlKey ? Math.max(-0.25, Math.min(0.25, px * 0.01)) : Math.max(-WHEEL_STEP, Math.min(WHEEL_STEP, px * 0.0015));
  return Math.exp(-k);
}

/** The pixels at which three's orbit controls (0.95 to the hundredth of a pixel) zoom by WHEEL_STEP. */
const ORBIT_WHEEL_MAX = (WHEEL_STEP / -Math.log(0.95)) * 100;

/**
 * A wheel event held to WHEEL_STEP for three's orbit controls, which read it
 * next (a capturing listener on the canvas, added before them): its deltaY is
 * shadowed on the event when it says more. A pinch (Ctrl) is theirs to scale.
 */
export function clampWheel(e: WheelEvent): void {
  if (e.ctrlKey) return;
  const px = wheelPixels(e, (e.currentTarget as HTMLElement | null)?.clientHeight || 800);
  if (Math.abs(px) <= ORBIT_WHEEL_MAX) return;
  Object.defineProperty(e, 'deltaY', { value: Math.sign(px) * ORBIT_WHEEL_MAX, configurable: true });
  Object.defineProperty(e, 'deltaMode', { value: 0, configurable: true });
}

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
