/**
 * Ollama, the local model server the assistant talks to (docs/plan-assistant.md):
 * its address (only this computer's), the models installed, a status check,
 * and the chat call, streamed. Nothing leaves the machine.
 *
 * The HTTP goes through a transport: `fetch` from the webview where Ollama's
 * default origins let it (tauri://localhost on macOS and Linux,
 * http://localhost:5180 under `pnpm dev:desktop`), else through the desktop
 * shell (`ollama_request`, a Rust command), because Windows' webview is
 * http://tauri.localhost, an origin Ollama refuses by default.
 */

import { create } from 'zustand';

export const DEFAULT_URL = 'http://127.0.0.1:11434';
/** The models measured on the assistant's two-step tool test, best first (qwen3-coder:30b 66 tok/s, gemma4:12b 26 tok/s; qwen3.8:27b failed it). */
export const PREFERRED_MODELS = ['qwen3-coder:30b', 'gemma4:12b'];

/** One message of an Ollama chat. */
export interface OllamaToolCall { id?: string; function: { name: string; arguments: Record<string, unknown> | string } }
export interface OllamaMessage { role: 'system' | 'user' | 'assistant' | 'tool'; content: string; tool_calls?: OllamaToolCall[]; tool_name?: string }
/** One line of a streamed /api/chat answer. */
export interface OllamaChunk {
  message?: { role?: string; content?: string; tool_calls?: OllamaToolCall[] };
  done?: boolean;
  error?: string;
  eval_count?: number;
  eval_duration?: number;
  prompt_eval_count?: number;
  total_duration?: number;
}

/**
 * The address as Ollama is reached: http on this computer only (127.0.0.1,
 * localhost or ::1, written as 127.0.0.1, which the desktop's content policy
 * allows), with its port; null for anything else.
 */
export function loopbackUrl(input: string): string | null {
  const text = input.trim() || DEFAULT_URL;
  let u: URL;
  try { u = new URL(/^[a-z]+:\/\//i.test(text) ? text : `http://${text}`); } catch { return null; }
  if (u.protocol !== 'http:') return null;
  if (!['127.0.0.1', 'localhost', '[::1]'].includes(u.hostname)) return null;
  if (u.username || u.password) return null;
  return `http://127.0.0.1:${u.port || '11434'}`;
}

/** The model to start with: the first preferred one installed, else the first installed. */
export function defaultModel(installed: string[]): string | null {
  for (const m of PREFERRED_MODELS) if (installed.includes(m)) return m;
  return installed[0] ?? null;
}

/** How the HTTP reaches Ollama. */
export interface OllamaTransport {
  /** A JSON request; the parsed answer. */
  json(base: string, path: string, body?: unknown): Promise<unknown>;
  /** A streamed request: each line of the answer, parsed, as it comes. */
  stream(base: string, path: string, body: unknown, onChunk: (c: OllamaChunk) => void, signal?: AbortSignal): Promise<void>;
}

/** Ollama refused or failed: said so, with its own words when it has some. */
export class OllamaError extends Error {}

const ERR_UNREACHABLE = 'Ollama is not running';

/** The transport over `fetch` (the webview, tests with a fake). */
export function fetchTransport(f: typeof fetch = (...a) => fetch(...a)): OllamaTransport {
  const fail = async (r: Response) => {
    let why = `${r.status}`;
    try { const j = await r.json() as { error?: string }; if (j.error) why = j.error; } catch { /* not JSON */ }
    return new OllamaError(why);
  };
  return {
    async json(base, path, body) {
      let r: Response;
      try { r = await f(base + path, body === undefined ? undefined : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); } catch { throw new OllamaError(ERR_UNREACHABLE); }
      if (!r.ok) throw await fail(r);
      return r.json();
    },
    async stream(base, path, body, onChunk, signal) {
      let r: Response;
      try { r = await f(base + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), signal }); } catch (e) {
        if (signal?.aborted) return;
        throw e instanceof OllamaError ? e : new OllamaError(ERR_UNREACHABLE);
      }
      if (!r.ok) throw await fail(r);
      if (!r.body) { for (const line of (await r.text()).split('\n')) if (line.trim()) onChunk(JSON.parse(line) as OllamaChunk); return; }
      const reader = r.body.getReader();
      const decoder = new TextDecoder();
      let rest = '';
      try {
        for (;;) {
          const { value, done } = await reader.read();
          if (done) break;
          rest += decoder.decode(value, { stream: true });
          let i: number;
          while ((i = rest.indexOf('\n')) >= 0) {
            const line = rest.slice(0, i).trim();
            rest = rest.slice(i + 1);
            if (line) onChunk(JSON.parse(line) as OllamaChunk);
          }
        }
      } catch (e) { if (signal?.aborted) return; throw e; }
      if (rest.trim()) onChunk(JSON.parse(rest) as OllamaChunk);
    },
  };
}

/** The transport through the desktop shell (Rust, `ollama_request`): for a webview whose origin Ollama refuses. */
export function shellTransport(): OllamaTransport {
  let next = 1;
  const call = async (base: string, path: string, body: unknown, onLine: (line: string) => void, signal?: AbortSignal) => {
    const { invoke, Channel } = await import('@tauri-apps/api/core');
    const port = Number(new URL(base).port || 11434);
    const id = next++;
    let ended: () => void = () => undefined;
    const end = new Promise<void>((r) => { ended = r; });
    const channel = new Channel<string>();
    channel.onmessage = (line) => { if (line === '\u0000end') ended(); else onLine(line); };
    const cancel = () => void invoke('ollama_cancel', { id });
    signal?.addEventListener('abort', cancel);
    try {
      const status = await invoke<number>('ollama_request', { port, method: body === undefined ? 'GET' : 'POST', path, body: body === undefined ? null : JSON.stringify(body), id, onLine: channel });
      await end;
      return status;
    } catch (e) {
      throw new OllamaError(String(e).includes('refused') ? ERR_UNREACHABLE : String(e));
    } finally { signal?.removeEventListener('abort', cancel); }
  };
  return {
    async json(base, path, body) {
      const lines: string[] = [];
      const status = await call(base, path, body, (l) => lines.push(l));
      const text = lines.join('\n');
      if (status >= 400) { let why = `${status}`; try { why = (JSON.parse(text) as { error?: string }).error ?? why; } catch { /* not JSON */ } throw new OllamaError(why); }
      return JSON.parse(text);
    },
    async stream(base, path, body, onChunk, signal) {
      let failed: string | null = null;
      const status = await call(base, path, body, (l) => { const c = JSON.parse(l) as OllamaChunk; if (c.error) failed = c.error; onChunk(c); }, signal);
      if (status >= 400) throw new OllamaError(failed ?? `${status}`);
    },
  };
}

/** The transport for this window: the shell where Ollama would refuse the webview's origin (Windows' http(s)://tauri.localhost), else fetch. */
export function transportFor(origin: string, desktop: boolean): 'shell' | 'fetch' {
  return desktop && /^https?:\/\/tauri\.localhost$/i.test(origin) ? 'shell' : 'fetch';
}

/** The models installed, as /api/tags lists them; embedding models (which cannot chat) left out. */
export async function listModels(t: OllamaTransport, base: string): Promise<string[]> {
  const r = await t.json(base, '/api/tags') as { models?: { name?: string; model?: string }[] };
  return (r.models ?? []).map((m) => m.name ?? m.model ?? '').filter((n) => n && !/embed/i.test(n)).sort((a, b) => a.localeCompare(b));
}

export interface OllamaStatus {
  running: boolean;
  models: string[];
  /** Whether the chosen model says it can call tools (/api/show's capabilities); null when unknown. */
  tools: boolean | null;
  error?: string;
}

/** Is Ollama there, what does it have, and can `model` use tools. */
export async function checkStatus(t: OllamaTransport, base: string, model: string | null): Promise<OllamaStatus> {
  let models: string[];
  try { models = await listModels(t, base); } catch (e) { return { running: false, models: [], tools: null, error: e instanceof Error ? e.message : String(e) }; }
  let tools: boolean | null = null;
  if (model && models.includes(model)) {
    try {
      const show = await t.json(base, '/api/show', { model }) as { capabilities?: string[] };
      if (Array.isArray(show.capabilities)) tools = show.capabilities.includes('tools');
    } catch { /* unknown */ }
  }
  return { running: true, models, tools };
}

// ─── Settings (this computer's: kept in the browser storage) ────────────────

export interface AssistantSettings { url: string; model: string | null }
const KEY = 'lapin.assistant';

function load(): AssistantSettings {
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) ?? '{}') as Partial<AssistantSettings>;
    return { url: loopbackUrl(raw.url ?? DEFAULT_URL) ?? DEFAULT_URL, model: typeof raw.model === 'string' ? raw.model : null };
  } catch { return { url: DEFAULT_URL, model: null }; }
}

export const useAssistantSettings = create<AssistantSettings>(() => load());

export function saveSettings(s: Partial<AssistantSettings>): void {
  useAssistantSettings.setState(s);
  try { localStorage.setItem(KEY, JSON.stringify(useAssistantSettings.getState())); } catch { /* a convenience */ }
}
