/**
 * Whether the model assistant exists here (docs/plan-assistant.md). It runs a
 * local model through Ollama on this computer, so it is the desktop app's
 * alone: the web build (the engine in a worker, VITE_ENGINE=worker) has none
 * of it — the constant below is false there at build time, and every way in
 * (the panel, the menu entry, the command) sits behind it, so the bundler
 * leaves the assistant's code out. A guest works in someone else's project:
 * no assistant either.
 */

/** Built in: false in the web build, decided when the bundle is made. */
export const ASSISTANT_BUILD: boolean = import.meta.env.VITE_ENGINE !== 'worker';

export interface GateFacts {
  /** This bundle has the assistant (ASSISTANT_BUILD). */
  build: boolean;
  /** Running in the desktop app (files.ts `isDesktop`, Tauri). */
  desktop: boolean;
  /** Joined someone else's session. */
  guest: boolean;
  /** `pnpm dev` in a browser with the assistant asked for (localStorage lapin.assistant.dev = 1): for trying it without the desktop shell. */
  devOverride?: boolean;
}

/** The rule: built in, not a guest, and the desktop app (or a development run that asks for it). */
export const gateOpen = (f: GateFacts): boolean => f.build && !f.guest && (f.desktop || f.devOverride === true);

/** The development override: only in a dev server, never in a build. */
export function devOverride(): boolean {
  if (!import.meta.env.DEV) return false;
  try { return localStorage.getItem('lapin.assistant.dev') === '1'; } catch { return false; }
}
