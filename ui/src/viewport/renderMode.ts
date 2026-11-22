/**
 * Rendering in the 3D view (docs/plan-render.md, S4): the path tracer's mode (on or off, how far it has got) and the
 * Render dialog's settings (this computer's, as the view's profile is: a project opens the same on a slower machine).
 * The 3D view does the work (Viewport3D.tsx with pathTraceView.ts); this is what the button, the corner readout and the
 * dialog share.
 */

import { create } from 'zustand';
import type { ResolutionChoice, TraceStats } from './pathTrace';

export interface RenderSettings {
  resolution: ResolutionChoice;
  /** The custom size, px. */
  width: number;
  height: number;
  /** When a picture is done: after so many samples per pixel, or after so many seconds. */
  limit: 'samples' | 'time';
  samples: number;
  seconds: number;
  denoise: boolean;
  /** Depth of field: the aperture (f-number) and the distance in focus (m, along the camera's axis). */
  dof: boolean;
  fStop: number;
  focus: number;
  /** The exposure (EV over the automatic one) and the white balance: the view's (null, 'view') or the picture's own. */
  ev: number | null;
  wb: 'view' | 'auto' | number;
}

export const DEFAULT_SETTINGS: RenderSettings = {
  resolution: 'window', width: 1920, height: 1080, limit: 'samples', samples: 256, seconds: 120,
  denoise: true, dof: false, fStop: 2.8, focus: 10, ev: null, wb: 'view',
};

/** The sample counts the dialog offers (64–4096, plan §S4). */
export const SAMPLE_STEPS = [64, 128, 256, 512, 1024, 2048, 4096] as const;
export const F_STOPS = [1.4, 2, 2.8, 4, 5.6, 8, 11, 16] as const;

const KEY = 'lapin.render.settings';

/** The settings as kept, each field checked (a bad or old value falls back to its default). */
export function readSettings(raw: unknown): RenderSettings {
  const v = (raw && typeof raw === 'object' ? raw : {}) as Partial<RenderSettings>;
  const num = (x: unknown, lo: number, hi: number, d: number) => (typeof x === 'number' && Number.isFinite(x) ? Math.min(hi, Math.max(lo, x)) : d);
  const d = DEFAULT_SETTINGS;
  return {
    resolution: (['window', '1080p', '4k', 'custom'] as const).includes(v.resolution as ResolutionChoice) ? v.resolution as ResolutionChoice : d.resolution,
    width: Math.round(num(v.width, 16, 8192, d.width)),
    height: Math.round(num(v.height, 16, 8192, d.height)),
    limit: v.limit === 'time' ? 'time' : 'samples',
    samples: Math.round(num(v.samples, 1, 16384, d.samples)),
    seconds: num(v.seconds, 5, 3600, d.seconds),
    denoise: typeof v.denoise === 'boolean' ? v.denoise : d.denoise,
    dof: typeof v.dof === 'boolean' ? v.dof : d.dof,
    fStop: num(v.fStop, 0.5, 64, d.fStop),
    focus: num(v.focus, 0.05, 10000, d.focus),
    ev: v.ev === null || v.ev === undefined ? null : num(v.ev, -6, 6, 0),
    wb: v.wb === 'auto' ? 'auto' : typeof v.wb === 'number' && v.wb >= 1000 && v.wb <= 12000 ? v.wb : 'view',
  };
}

function stored(): RenderSettings {
  try { return readSettings(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return { ...DEFAULT_SETTINGS }; }
}

export const useRenderSettings = create<RenderSettings>(() => stored());

export function setRenderSettings(patch: Partial<RenderSettings>): void {
  useRenderSettings.setState(readSettings({ ...useRenderSettings.getState(), ...patch }));
  try { localStorage.setItem(KEY, JSON.stringify(useRenderSettings.getState())); } catch { /* kept for the session */ }
}

export type RenderPhase = 'off' | 'loading' | 'building' | 'tracing' | 'done' | 'still' | 'denoising';

export interface RenderMode {
  on: boolean;
  phase: RenderPhase;
  samples: number;
  /** Seconds since the picture last started over. */
  seconds: number;
  stats: TraceStats | null;
  /** How long the last scene took to build (its BVH), ms. */
  buildMs: number;
  /** A click in the view sets the distance in focus. */
  pickingFocus: boolean;
  /** A still image being rendered: its size and how far (0–1). */
  still: { width: number; height: number; progress: number } | null;
  error: string | null;
}

export const useRenderMode = create<RenderMode>(() => ({ on: false, phase: 'off', samples: 0, seconds: 0, stats: null, buildMs: 0, pickingFocus: false, still: null, error: null }));

/** What the 3D view does for the dialog (set while a 3D view is open). */
export interface RenderApi {
  /** A still image at the settings' size, saved as a PNG; the file's name, or null (cancelled, failed). */
  saveImage(): Promise<string | null>;
  cancel(): void;
  /** A still image rendered into memory (the PNG's bytes), not saved: the guide's scenes and the tests take it. */
  still(): Promise<Uint8Array | null>;
}

export const useRenderApi = create<{ api: RenderApi | null; denoiser: 'oidn' | 'shader' }>(() => ({ api: null, denoiser: 'shader' }));

export function setRendering(on: boolean): void {
  useRenderMode.setState(on ? { on: true, phase: 'loading', samples: 0, seconds: 0, error: null } : { on: false, phase: 'off', samples: 0, seconds: 0, pickingFocus: false, still: null });
}

export const toggleRendering = (): void => setRendering(!useRenderMode.getState().on);

/** Whether a picture of so many samples, so long, is done by the settings. */
export function finished(s: Pick<RenderSettings, 'limit' | 'samples' | 'seconds'>, samples: number, seconds: number): boolean {
  return s.limit === 'time' ? seconds >= s.seconds : samples >= s.samples;
}

/** A picture's progress, 0–1, by the settings' limit. */
export function progressOf(s: Pick<RenderSettings, 'limit' | 'samples' | 'seconds'>, samples: number, seconds: number): number {
  return Math.min(1, s.limit === 'time' ? seconds / s.seconds : samples / Math.max(1, s.samples));
}

/** The readout: "128 samples · 0:42". */
export function readout(samples: number, seconds: number): string {
  const m = Math.floor(seconds / 60), s = Math.floor(seconds % 60);
  return `${Math.floor(samples)} samples · ${m}:${String(s).padStart(2, '0')}`;
}
