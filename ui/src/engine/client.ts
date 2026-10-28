/**
 * The connection to the local Lapin engine: request/response over one
 * WebSocket, plus the diffs the engine pushes after every change. Calls go
 * out as JSON text; everything comes back as binary frames (frame.ts).
 * Reconnects by itself: the engine may still be starting when the window opens.
 * A call that gets no answer in time fails, so a stalled engine never leaves
 * the interface hanging on a promise.
 */

import { decodeFrame } from './frame';
import type { Diff } from './protocol';

interface Message { id?: number; result?: unknown; error?: string | null; event?: string; data?: unknown }

export interface CallOptions {
  /** How long to wait for the answer, ms. Default 30 s; long work (opening, saving, exporting) passes more. */
  timeoutMs?: number;
}

/** The default wait for an answer; a stall beyond it is reported, not sat out. */
export const DEFAULT_TIMEOUT_MS = 30_000;
/** Opening, saving, importing and exporting may legitimately take long. */
export const LONG_TIMEOUT_MS = 10 * 60_000;

/** An open connection to the engine: messages go out as JSON text. */
export interface EngineLink { send(text: string): void; close(): void }

/**
 * How the interface reaches the engine: the local engine over a WebSocket
 * (the desktop app and pnpm dev), or the engine in a Web Worker (the web
 * build). Frames come back to `onMessage`; `onClose` when the link drops.
 */
export interface EngineTransport {
  /** Calls `onOpen` as soon as the link is up (before the promise settles), then resolves. */
  connect(handlers: { onOpen(link: EngineLink): void; onMessage(data: ArrayBuffer | string): void; onClose(): void }): Promise<void>;
  /** False: one failed start is final (an engine in a worker that would not load will not load the next time either). */
  readonly retries?: boolean;
}

/** The local engine, over a WebSocket at the URL given (resolved at each connection: the engine may move ports). */
export function webSocketTransport(resolveUrl: () => Promise<string>): EngineTransport {
  return {
    connect: ({ onOpen, onMessage, onClose }) => resolveUrl().then((url) => new Promise<void>((resolve, reject) => {
      const ws = new WebSocket(url);
      ws.binaryType = 'arraybuffer';
      let open = false;
      ws.onopen = () => { open = true; onOpen({ send: (t) => ws.send(t), close: () => ws.close() }); resolve(); };
      ws.onerror = () => { if (!open) reject(new Error('engine unreachable')); };
      ws.onclose = () => { if (open) onClose(); else reject(new Error('engine unreachable')); };
      ws.onmessage = (ev) => onMessage(ev.data as ArrayBuffer | string);
    })),
  };
}

export class EngineClient {
  private link: EngineLink | null = null;
  private seq = 0;
  private pending = new Map<number, { resolve: (v: unknown) => void; reject: (e: Error) => void; timer: ReturnType<typeof setTimeout> }>();
  private diffListeners = new Set<(d: Diff) => void>();
  private eventListeners = new Map<string, Set<(data: unknown) => void>>();
  private statusListeners = new Set<(connected: boolean) => void>();
  private closed = false;
  private readonly transport: EngineTransport;

  /** By the engine's WebSocket URL (resolved at each connection), or by a transport of its own (the engine in a worker). */
  constructor(source: (() => Promise<string>) | EngineTransport, private readonly retryMs = 700) {
    this.transport = typeof source === 'function' ? webSocketTransport(source) : source;
  }

  async start(): Promise<void> {
    this.closed = false;
    while (!this.closed) {
      try {
        await this.open();
        return;
      } catch (e) {
        if (this.transport.retries === false) { this.statusListeners.forEach((l) => l(false)); throw e; }
        await new Promise((r) => setTimeout(r, this.retryMs));
      }
    }
  }

  private async open(): Promise<void> {
    let link: EngineLink | null = null;
    await this.transport.connect({
      onOpen: (l) => { link = l; this.link = l; this.statusListeners.forEach((s) => s(true)); },
      onMessage: (data) => this.receive(data instanceof ArrayBuffer ? decodeFrame<Message>(data) : JSON.parse(String(data)) as Message),
      onClose: () => {
        if (this.link !== link) return;
        this.link = null;
        this.failAll(new Error('The connection to the engine closed.'));
        this.statusListeners.forEach((l) => l(false));
        if (!this.closed && this.transport.retries !== false) setTimeout(() => void this.start(), this.retryMs);
      },
    });
  }

  private failAll(error: Error): void {
    for (const p of this.pending.values()) { clearTimeout(p.timer); p.reject(error); }
    this.pending.clear();
  }

  private receive(msg: Message): void {
    if (msg.event === 'diff') { this.diffListeners.forEach((l) => l(msg.data as Diff)); return; }
    if (msg.event) { this.eventListeners.get(msg.event)?.forEach((l) => l(msg.data)); return; }
    if (typeof msg.id !== 'number') return;
    const p = this.pending.get(msg.id);
    if (!p) return;
    this.pending.delete(msg.id);
    clearTimeout(p.timer);
    if (msg.error) p.reject(new Error(msg.error)); else p.resolve(msg.result);
  }

  call<T = unknown>(method: string, params: Record<string, unknown> = {}, options: CallOptions = {}): Promise<T> {
    const link = this.link;
    if (!link) return Promise.reject(new Error('The engine is not connected.'));
    const id = ++this.seq;
    const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    return new Promise<T>((resolve, reject) => {
      const timer = setTimeout(() => {
        if (!this.pending.delete(id)) return;
        reject(new Error(`The engine did not answer ${method} within ${Math.round(timeoutMs / 1000)} s.`));
      }, timeoutMs);
      this.pending.set(id, { resolve: resolve as (v: unknown) => void, reject, timer });
      link.send(JSON.stringify({ id, method, params }));
    });
  }

  /** A call that wants no answer (presence): sent if connected, else dropped. */
  notify(method: string, params: Record<string, unknown> = {}): void {
    this.link?.send(JSON.stringify({ method, params }));
  }

  /** The engine's other events (presence, the sharing's state, a shared session ended). */
  onEvent(name: string, l: (data: unknown) => void): () => void {
    let set = this.eventListeners.get(name);
    if (!set) this.eventListeners.set(name, set = new Set());
    set.add(l);
    return () => set!.delete(l);
  }

  /** How many calls are waiting for an answer. */
  get waiting(): number { return this.pending.size; }

  onDiff(l: (d: Diff) => void): () => void { this.diffListeners.add(l); return () => this.diffListeners.delete(l); }
  onStatus(l: (c: boolean) => void): () => void { this.statusListeners.add(l); return () => this.statusListeners.delete(l); }
  stop(): void { this.closed = true; this.link?.close(); }
}

/** Inside the desktop app the shell knows the engine's port; in a browser (pnpm dev) it is the dev port. */
export async function engineUrl(): Promise<string> {
  const w = window as unknown as { __TAURI_INTERNALS__?: unknown };
  if (w.__TAURI_INTERNALS__) {
    const { invoke } = await import('@tauri-apps/api/core');
    return await invoke<string>('engine_url');
  }
  return (import.meta.env.VITE_ENGINE_URL as string | undefined) ?? 'ws://127.0.0.1:47810/ws';
}
