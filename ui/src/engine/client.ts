/**
 * The connection to the local Lapin engine: request/response over one
 * WebSocket, plus the diffs the engine pushes after every change. Calls go
 * out as JSON text; everything comes back as binary frames (frame.ts).
 * Reconnects by itself: the engine may still be starting when the window opens.
 */

import { decodeFrame } from './frame';
import type { Diff } from './protocol';

interface Message { id?: number; result?: unknown; error?: string | null; event?: string; data?: unknown }

export class EngineClient {
  private ws: WebSocket | null = null;
  private seq = 0;
  private pending = new Map<number, { resolve: (v: unknown) => void; reject: (e: Error) => void }>();
  private diffListeners = new Set<(d: Diff) => void>();
  private statusListeners = new Set<(connected: boolean) => void>();
  private closed = false;

  constructor(private readonly resolveUrl: () => Promise<string>) {}

  async start(): Promise<void> {
    this.closed = false;
    while (!this.closed) {
      try {
        await this.open(await this.resolveUrl());
        return;
      } catch {
        await new Promise((r) => setTimeout(r, 700));
      }
    }
  }

  private open(url: string): Promise<void> {
    return new Promise<void>((resolve, reject) => {
      const ws = new WebSocket(url);
      ws.binaryType = 'arraybuffer';
      ws.onopen = () => { this.ws = ws; this.statusListeners.forEach((l) => l(true)); resolve(); };
      ws.onerror = () => reject(new Error('engine unreachable'));
      ws.onclose = () => {
        if (this.ws !== ws) return;
        this.ws = null;
        for (const p of this.pending.values()) p.reject(new Error('The connection to the engine closed.'));
        this.pending.clear();
        this.statusListeners.forEach((l) => l(false));
        if (!this.closed) setTimeout(() => void this.start(), 700);
      };
      ws.onmessage = (ev) => this.receive(ev.data instanceof ArrayBuffer ? decodeFrame<Message>(ev.data) : JSON.parse(String(ev.data)) as Message);
    });
  }

  private receive(msg: Message): void {
    if (msg.event === 'diff') { this.diffListeners.forEach((l) => l(msg.data as Diff)); return; }
    if (typeof msg.id !== 'number') return;
    const p = this.pending.get(msg.id);
    if (!p) return;
    this.pending.delete(msg.id);
    if (msg.error) p.reject(new Error(msg.error)); else p.resolve(msg.result);
  }

  call<T = unknown>(method: string, params: Record<string, unknown> = {}): Promise<T> {
    const ws = this.ws;
    if (!ws || ws.readyState !== WebSocket.OPEN) return Promise.reject(new Error('The engine is not connected.'));
    const id = ++this.seq;
    return new Promise<T>((resolve, reject) => {
      this.pending.set(id, { resolve: resolve as (v: unknown) => void, reject });
      ws.send(JSON.stringify({ id, method, params }));
    });
  }

  onDiff(l: (d: Diff) => void): () => void { this.diffListeners.add(l); return () => this.diffListeners.delete(l); }
  onStatus(l: (c: boolean) => void): () => void { this.statusListeners.add(l); return () => this.statusListeners.delete(l); }
  stop(): void { this.closed = true; this.ws?.close(); }
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
