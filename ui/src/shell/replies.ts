/**
 * Replies to a report (docs/decisions-pending.md 177): anonymous by default;
 * a sender who ticks "I'd like a reply" gets a key without an account. On
 * the first opt-in this device makes a random secret (32 bytes, base64url),
 * kept here (localStorage, like the app's other settings; the desktop app and
 * the browser each have their own); a report carries only its id, the
 * SHA-256 of the secret, and the replies are read and answered with the
 * secret itself, in the body of a POST. The server never keeps the secret.
 *
 * Help → Messages lists the sender's reports and their threads; the app asks
 * for news at start and every 15 minutes, only once opted in, only when
 * online, never from a guest's window or a detached view, and quietly.
 */

import { create } from 'zustand';
import { notify, useUi } from '../app/store';
import { engine } from '../engine/session';
import { isGuest } from '../engine/share';
import { FEEDBACK_URL } from './support';

export interface ReplySettings {
  secret: string;
  nickname: string;
  /** Asking for news: false after "Stop replies" (the key is kept). */
  active: boolean;
  /** The unread replies already announced, so each is announced once. */
  notified: number;
}

export interface ThreadMessage { from: 'lapin' | 'user'; text: string; at: string }
export interface InboxReport {
  number: number; title: string; kind: string; receivedAt: string; status: 'open' | 'closed';
  description: string; messages: ThreadMessage[]; seen: number;
}

export const POLL_MINUTES = 15;
const KEY = 'lapin.replies';
type Store = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;
const storage = (): Store | null => { try { return globalThis.localStorage ?? null; } catch { return null; } };

/** A code that can be a key's secret: base64url, as the app makes it (43 characters) or a little longer. */
export const validSecret = (s: string): boolean => /^[A-Za-z0-9_-]{32,128}$/.test(s);

export function loadSettings(s: Store | null = storage()): ReplySettings | null {
  try {
    const v = JSON.parse(s?.getItem(KEY) ?? 'null') as Partial<ReplySettings> | null;
    if (!v || typeof v.secret !== 'string' || !validSecret(v.secret)) return null;
    return { secret: v.secret, nickname: typeof v.nickname === 'string' ? v.nickname : '', active: v.active !== false, notified: Number(v.notified) || 0 };
  } catch { return null; }
}

export function saveSettings(v: ReplySettings | null, s: Store | null = storage()): void {
  try { if (v) s?.setItem(KEY, JSON.stringify(v)); else s?.removeItem(KEY); } catch { /* kept for this session only */ }
}

const b64url = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

/** A new secret: 32 random bytes, base64url (43 characters). */
export const newSecret = (): string => b64url(crypto.getRandomValues(new Uint8Array(32)));

/** A key's id: the SHA-256, in hex, of its secret (the text's UTF-8 bytes), as the server computes it. */
export async function keyIdOf(secret: string): Promise<string> {
  const hash = new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(secret)));
  return [...hash].map((b) => b.toString(16).padStart(2, '0')).join('');
}

/** Lapin's messages the sender has not read in a report. */
export const unreadOf = (r: InboxReport): number => r.messages.slice(r.seen).filter((m) => m.from === 'lapin').length;
/** The badge: Lapin's messages unread in all the sender's reports. */
export const unreadCount = (reports: InboxReport[]): number => reports.reduce((n, r) => n + unreadOf(r), 0);

/** Whether the app asks for news now: opted in and not stopped, online, the main window of the computer's own user. */
export function shouldPoll(settings: ReplySettings | null, env: { online: boolean; guest: boolean; main: boolean }): boolean {
  return Boolean(settings?.active) && env.online && !env.guest && env.main;
}

const environment = () => ({
  online: typeof navigator === 'undefined' || navigator.onLine !== false,
  guest: isGuest(),
  main: useUi.getState().windowRole === 'main',
});

interface RepliesState {
  settings: ReplySettings | null;
  reports: InboxReport[];
  unread: number;
  /** The inbox was read at least once since the app started. */
  loaded: boolean;
}

export const useReplies = create<RepliesState>(() => ({ settings: loadSettings(), reports: [], unread: 0, loaded: false }));

function setSettings(settings: ReplySettings | null): void {
  saveSettings(settings);
  useReplies.setState({ settings });
}

/**
 * The contact a report carries when its sender asked for a reply: the
 * nickname and the key's id; the key is made on the first opt-in and kept.
 * Undefined for an anonymous report (nothing is made or kept).
 */
export async function contactFor(wanted: boolean, nickname: string): Promise<{ nickname?: string; keyId: string } | undefined> {
  if (!wanted) return undefined;
  const name = nickname.trim().slice(0, 40);
  const now = useReplies.getState().settings;
  const settings: ReplySettings = { secret: now?.secret ?? newSecret(), nickname: name, active: true, notified: now?.notified ?? 0 };
  setSettings(settings);
  return { ...(name ? { nickname: name } : {}), keyId: await keyIdOf(settings.secret) };
}

/**
 * One call to the replies' endpoints. The desktop's engine posts it (as it
 * does a report); in the browser the engine hands back the body and the page
 * posts it to the server it was served from.
 */
export async function messagesCall<T>(endpoint: 'inbox' | 'seen' | 'message' | 'nickname' | 'forget', args: Record<string, unknown>, post: typeof fetch = fetch): Promise<T> {
  const secret = useReplies.getState().settings?.secret;
  if (!secret) throw new Error('Replies are not on.');
  const r = await engine.call<{ send?: unknown; endpoint?: string } & T>('support.messages', { endpoint, secret, ...args });
  if (r?.send === undefined) return r as T;
  let res: Response;
  try {
    res = await post(`${FEEDBACK_URL}/${r.endpoint ?? endpoint}`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(r.send) });
  } catch (e) {
    throw new Error(`The messages could not be reached: ${e instanceof Error ? e.message : String(e)}`, { cause: e });
  }
  let answer: T & { error?: string };
  try { answer = await res.json(); } catch { throw new Error("The messages could not be reached: the server's answer was unreadable."); }
  if (!res.ok) throw new Error(answer.error ?? `The messages could not be reached (${res.status}).`);
  return answer;
}

/** Read the inbox now; the reports and the badge updated, a new reply announced once. Throws when it cannot. */
export async function refresh(post: typeof fetch = fetch): Promise<void> {
  const answer = await messagesCall<{ reports: InboxReport[]; nickname?: string }>('inbox', {}, post);
  const reports = Array.isArray(answer.reports) ? answer.reports : [];
  const unread = unreadCount(reports);
  useReplies.setState({ reports, unread, loaded: true });
  const settings = useReplies.getState().settings;
  if (!settings) return;
  if (unread > settings.notified) notify('A reply to your report has arrived: Help → Messages.');
  const nickname = settings.nickname || answer.nickname || '';
  if (unread !== settings.notified || nickname !== settings.nickname) setSettings({ ...settings, notified: unread, nickname });
}

/** Ask for news if the gate allows, quietly: a failure waits for the next time. */
export async function poll(post: typeof fetch = fetch): Promise<void> {
  if (!shouldPoll(useReplies.getState().settings, environment())) return;
  try { await refresh(post); } catch { /* offline, the server down: the next time */ }
}

let started = false;
/** Ask for news soon after start, every 15 minutes, and when the computer is back online. */
export function startReplies(): void {
  if (started || typeof window === 'undefined') return;
  started = true;
  setTimeout(() => void poll(), 5000);
  setInterval(() => void poll(), POLL_MINUTES * 60_000);
  window.addEventListener('online', () => void poll());
}

/** The sender has read a report's thread: the server keeps the marker, the badge drops. */
export async function markSeen(report: InboxReport): Promise<void> {
  if (report.seen >= report.messages.length) return;
  const upTo = report.messages.length;
  const reports = useReplies.getState().reports.map((r) => (r.number === report.number ? { ...r, seen: upTo } : r));
  const unread = unreadCount(reports);
  useReplies.setState({ reports, unread });
  const settings = useReplies.getState().settings;
  if (settings && settings.notified > unread) setSettings({ ...settings, notified: unread });
  await messagesCall('seen', { report: report.number, upTo }).catch(() => undefined);
}

/** The sender's answer on their report; the inbox read again. */
export async function sendMessage(report: number, text: string): Promise<void> {
  await messagesCall('message', { report, text });
  await refresh();
}

/** The name shown on the sender's reports, here and on the server. */
export async function setNickname(nickname: string): Promise<void> {
  const name = nickname.trim().slice(0, 40);
  await messagesCall('nickname', { nickname: name });
  const settings = useReplies.getState().settings;
  if (settings) setSettings({ ...settings, nickname: name });
}

/** Stop asking for news (the key stays, to start again), or start again. */
export function setActive(active: boolean): void {
  const settings = useReplies.getState().settings;
  if (!settings) return;
  setSettings({ ...settings, active });
  if (active) void poll();
}

/**
 * Forget me: the server removes the nickname and the key from all the
 * sender's reports (the reports and their messages stay, anonymous), then
 * the secret is deleted here. Nothing is deleted here if the server could
 * not be reached, so the sender can try again.
 */
export async function forgetMe(post: typeof fetch = fetch): Promise<void> {
  await messagesCall('forget', {}, post);
  setSettings(null);
  useReplies.setState({ reports: [], unread: 0, loaded: false });
}

/** Use another device's key here (its code pasted); the inbox read with it. False when the code is not a key. */
export async function adoptCode(code: string): Promise<boolean> {
  const secret = code.trim();
  if (!validSecret(secret)) return false;
  setSettings({ secret, nickname: '', active: true, notified: 0 });
  useReplies.setState({ reports: [], unread: 0, loaded: false });
  await refresh().catch(() => undefined);
  return true;
}
