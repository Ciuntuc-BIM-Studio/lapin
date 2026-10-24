/** What the help menu reports with and links to: the app's last errors, the community and the site. */

import { notify } from '../app/store';
import { engine } from '../engine/session';

/** The app's last errors, newest last (at most 20). */
export const RECENT: string[] = [];
let installed = false;

/** Keep the app's last errors (uncaught, rejected, console.error), for the next report. */
export function installRecentErrors(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  const keep = (text: string) => {
    RECENT.push(`${new Date().toISOString().slice(11, 19)} ${text}`.slice(0, 400));
    if (RECENT.length > 20) RECENT.shift();
  };
  window.addEventListener('error', (e) => keep(`error: ${e.message} @ ${e.filename}:${e.lineno}`));
  window.addEventListener('unhandledrejection', (e) => keep(`rejection: ${e.reason instanceof Error ? e.reason.message : String(e.reason)}`));
  const original = console.error.bind(console);
  console.error = (...args: unknown[]) => { keep(`console: ${args.map((a) => (a instanceof Error ? a.message : String(a))).join(' ')}`); original(...args); };
}

/** Open the community or the site in the system's browser (the engine does it). */
export const openLink = (link: 'community' | 'site') => void engine.call('support.open', { link }).catch(() => notify('The link could not be opened.'));
