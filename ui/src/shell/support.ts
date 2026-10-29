/** What the help menu reports with and links to: the app's last errors, the community and the site. */

import { notify } from '../app/store';
import { engine, engineInBrowser } from '../engine/session';
import { useLang } from '../i18n';

/** The app's last errors, newest last (at most 20). */
export const RECENT: string[] = [];
let installed = false;

/** Browser messages that are not errors of the app: a ResizeObserver whose callback resized something again (benign, the browser delivers it next frame). */
const BENIGN = [/ResizeObserver loop completed with undelivered notifications/i, /ResizeObserver loop limit exceeded/i];

/** Whether a message is noise a report need not carry. */
export const benignError = (text: string): boolean => BENIGN.some((re) => re.test(text));

/** Add an error to the recent ones, newest last (at most 20), leaving out the benign ones. */
export function keepRecentError(text: string, now = new Date()): void {
  if (benignError(text)) return;
  RECENT.push(`${now.toISOString().slice(11, 19)} ${text}`.slice(0, 400));
  if (RECENT.length > 20) RECENT.shift();
}

/** Keep the app's last errors (uncaught, rejected, console.error), for the next report. */
export function installRecentErrors(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;
  const keep = (text: string) => keepRecentError(text);
  window.addEventListener('error', (e) => keep(`error: ${e.message} @ ${e.filename}:${e.lineno}`));
  window.addEventListener('unhandledrejection', (e) => keep(`rejection: ${e.reason instanceof Error ? e.reason.message : String(e.reason)}`));
  const original = console.error.bind(console);
  console.error = (...args: unknown[]) => { keep(`console: ${args.map((a) => (a instanceof Error ? a.message : String(a))).join(' ')}`); original(...args); };
}

/** Open the community or the site in the system's browser (the engine does it). */
export const LINKS = { guide: 'https://lapin.ciuntucbimstudio.ro/docs/', guideRo: 'https://lapin.ciuntucbimstudio.ro/docs/ro/', community: 'https://www.skool.com/bubblebim-club-1376/about', site: 'https://ciuntucbimstudio.ro', source: 'https://github.com/Ciuntuc-BIM-Studio/lapin-pro' } as const;

/** Open the community or the site: in the system's browser (the engine does it on the desktop), in a new tab on the web. The guide opens in the interface's language (English or Romanian; English for the others). */
export const openLink = (which: keyof typeof LINKS) => {
  const link = which === 'guide' && useLang.getState().lang === 'ro' ? 'guideRo' : which;
  if (engineInBrowser) { window.open(LINKS[link], '_blank', 'noopener'); return; }
  void engine.call('support.open', { link }).catch(() => notify('The link could not be opened.'));
};

/** Where the web build posts reports: the Lapin server it is served from. */
const FEEDBACK_URL: string = import.meta.env.VITE_FEEDBACK_URL ?? '/api/feedback';

/**
 * Send a report; its number back. The desktop's engine posts it; in the
 * browser the engine hands back the same body (a worker cannot wait for the
 * network inside a call) and the page posts it.
 */
export async function sendReport(params: Record<string, unknown>, post: typeof fetch = fetch): Promise<number> {
  const r = await engine.call<{ number?: number; send?: unknown }>('support.report', { ...params, userAgent: navigator.userAgent });
  if (r.send === undefined) return r.number ?? 0;
  let res: Response;
  try {
    res = await post(FEEDBACK_URL, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(r.send) });
  } catch (e) {
    throw new Error(`The report could not be sent: ${e instanceof Error ? e.message : String(e)}`, { cause: e });
  }
  let answer: { number?: number; error?: string } = {};
  try { answer = await res.json(); } catch { if (res.ok) throw new Error("The report could not be sent: the server's answer was unreadable."); }
  if (!res.ok) throw new Error(answer.error ?? `The report could not be sent (${res.status}).`);
  return Number(answer.number ?? 0);
}
