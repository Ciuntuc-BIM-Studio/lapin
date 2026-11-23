/**
 * The Speckle connector's interface side (docs/plan-speckle.md, S1): the account, the projects and models, a send.
 *
 * The token never goes into a file Lapin writes: on the desktop the shell keeps it in the system's secure store
 * (macOS Keychain, Windows Credential Manager, the Secret Service on Linux: desktop/src-tauri, `secret_*`), under
 * `speckle:<server>`; the interface reads it from there when the dialog opens and hands it to the engine, which holds it
 * in memory for the session (`speckle.setAccount`). Only the server's address and the last project and model chosen are
 * remembered as settings. Outside the desktop app (the dev server in a browser) nothing is remembered: the token is asked
 * each session.
 */

import { create } from 'zustand';
import { engine } from '../../engine/session';
import { isDesktop } from '../../engine/files';
import { LONG_TIMEOUT_MS } from '../../engine/client';

export const DEFAULT_SERVER = 'https://app.speckle.systems';

export interface SpeckleAccount { server: string; name?: string | null; email?: string | null }
export interface SpeckleItem { id: string; name: string; role?: string | null; updatedAt?: string }
export interface SpeckleSent {
  projectId: string; modelId: string; versionId: string; url: string;
  elements: number; meshes: number; materials: number; triangles: number; objects: number; notes: string[];
}

/** What a send takes: the whole model, the model as the 3D view shows it, or the selection. */
export type SendWhat = 'model' | 'view' | 'selection';

const KEY = { server: 'lapin.speckle.server', project: 'lapin.speckle.project', model: 'lapin.speckle.model' };
const read = (k: string): string | null => { try { return localStorage.getItem(k); } catch { return null; } };
const write = (k: string, v: string | null) => { try { if (v == null) localStorage.removeItem(k); else localStorage.setItem(k, v); } catch { /* this session only */ } };

/** A server's address as Speckle takes it (the engine's SpeckleServer.ServerOf): http(s), no path, no trailing slash. */
export function serverOf(text: string | null | undefined): string {
  let s = (text ?? '').trim();
  if (!s) s = DEFAULT_SERVER;
  if (!s.includes('://')) s = `https://${s}`;
  try {
    const u = new URL(s);
    if (u.protocol !== 'http:' && u.protocol !== 'https:') return s;
    return `${u.protocol}//${u.host}`;
  } catch { return s; }
}

/** The secure store's key for a server's token. */
export const secretKey = (server: string) => `speckle:${serverOf(server)}`;

type Invoke = <T>(cmd: string, args?: Record<string, unknown>) => Promise<T>;
let invoker: Invoke | null = null;
async function shell(): Promise<Invoke | null> {
  if (!isDesktop()) return null;
  if (!invoker) invoker = (await import('@tauri-apps/api/core')).invoke as Invoke;
  return invoker;
}

/** Whether the token can be remembered here (the desktop app's secure store). */
export const canRemember = () => isDesktop();

export async function readToken(server: string): Promise<string | null> {
  const invoke = await shell();
  if (!invoke) return null;
  try { return await invoke<string | null>('secret_read', { key: secretKey(server) }); } catch { return null; }
}

async function keepToken(server: string, token: string): Promise<void> {
  const invoke = await shell();
  if (invoke) await invoke('secret_write', { key: secretKey(server), value: token });
}

async function forgetToken(server: string): Promise<void> {
  const invoke = await shell();
  if (invoke) await invoke('secret_delete', { key: secretKey(server) }).catch(() => undefined);
}

const ask = <T>(name: string, args: Record<string, unknown> = {}, long = false) =>
  engine.call<T>('doc.query', { name, args }, long ? { timeoutMs: LONG_TIMEOUT_MS } : undefined);

interface SpeckleState {
  account: SpeckleAccount | null;
  /** The server the dialog shows (the last one signed in to, else Speckle's own). */
  server: string;
  /** The last project and model sent to, chosen again next time. */
  project: string | null;
  model: string | null;
}

export const useSpeckle = create<SpeckleState>(() => ({
  account: null,
  server: read(KEY.server) ?? DEFAULT_SERVER,
  project: read(KEY.project),
  model: read(KEY.model),
}));

/** The account the engine holds, else signs in with the token kept for the server shown (desktop): null when there is none. */
export async function restore(): Promise<SpeckleAccount | null> {
  const held = await ask<SpeckleAccount[]>('speckle.accounts').catch(() => []);
  if (held.length > 0) { useSpeckle.setState({ account: held[0], server: held[0].server }); return held[0]; }
  const server = useSpeckle.getState().server;
  const token = await readToken(server);
  if (!token) return null;
  try { return await signIn(server, token, false); } catch { return null; }
}

/** Signs in (the token checked by the server); kept in the secure store when `remember` and on the desktop. */
export async function signIn(server: string, token: string, remember: boolean): Promise<SpeckleAccount> {
  const account = await ask<SpeckleAccount>('speckle.setAccount', { server: serverOf(server), token: token.trim() });
  if (remember) await keepToken(account.server, token.trim());
  write(KEY.server, account.server);
  useSpeckle.setState({ account, server: account.server });
  return account;
}

/** Signs out: the engine forgets the account, the secure store its token. */
export async function signOut(): Promise<void> {
  const server = useSpeckle.getState().account?.server ?? useSpeckle.getState().server;
  await ask('speckle.forgetAccount').catch(() => undefined);
  await forgetToken(server);
  useSpeckle.setState({ account: null });
}

export const listProjects = (search?: string) => ask<SpeckleItem[]>('speckle.projects', search ? { search } : {});
export const listModels = (projectId: string) => ask<SpeckleItem[]>('speckle.models', { projectId });

export interface SendArgs {
  projectId: string; modelId?: string | null; modelName?: string; message?: string; what: SendWhat;
  /** As the 3D view shows it: its id, what the interface does not draw, the planes it cuts by. */
  viewId?: string | null; hidden?: string[]; planes?: unknown[];
  ids?: string[];
}

/** The arguments of speckle.send for what the dialog chose (only those its `what` uses). */
export function sendArgs(a: SendArgs): Record<string, unknown> {
  const o: Record<string, unknown> = { projectId: a.projectId, what: a.what };
  if (a.modelId) o.modelId = a.modelId; else o.modelName = (a.modelName ?? '').trim();
  if (a.message?.trim()) o.message = a.message.trim();
  if (a.what === 'view') {
    o.viewId = a.viewId ?? '3d';
    if (a.hidden?.length) o.hidden = a.hidden;
    if (a.planes?.length) o.planes = a.planes;
  }
  if (a.what === 'selection') o.ids = a.ids ?? [];
  return o;
}

export async function send(a: SendArgs): Promise<SpeckleSent> {
  const sent = await ask<SpeckleSent>('speckle.send', sendArgs(a), true);
  write(KEY.project, sent.projectId);
  write(KEY.model, sent.modelId);
  useSpeckle.setState({ project: sent.projectId, model: sent.modelId });
  return sent;
}

/** A version's page in the system's browser (the engine opens only its own server's pages); a new tab outside the desktop. */
export async function openVersion(url: string): Promise<void> {
  if (!isDesktop()) { window.open(url, '_blank', 'noopener'); return; }
  await ask('speckle.openVersion', { url });
}
