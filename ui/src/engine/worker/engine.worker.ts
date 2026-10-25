/// <reference lib="webworker" />
/**
 * The engine in a Web Worker (the web build, docs/plan-browser-engine.md):
 * the desktop's engine compiled to WebAssembly, started from engine/ next to
 * the page. The page posts `{ start: <engine folder URL> }`, then
 * `{ call: <JSON text> }` for each message of the protocol; the worker posts
 * back `{ ready }` or `{ failed }`, then every frame (`{ frame }`, its buffer
 * transferred): a call's diff first, then its reply, as the desktop's engine
 * sends them.
 *
 * The recovery journals live in the browser's own storage (the origin
 * private file system, folder "recovery"). The engine writes them
 * synchronously; the worker keeps them in memory, read at start, and writes
 * each line through to storage in order. A journal another tab is writing is
 * held by its lock ("lapin-journal:<name>") and counts as in use.
 */

interface Exports { Start(): string; Call(message: string): Uint8Array }
interface Journal { lines: string[]; at: number }

const scope = self as unknown as DedicatedWorkerGlobalScope;
const journals = new Map<string, Journal>();
const mine = new Set<string>();
const releases = new Map<string, () => void>();
let othersHeld = new Set<string>();
let engine: Exports | null = null;

// ─── The journals' storage ───────────────────────────────────────────────

let dir: FileSystemDirectoryHandle | null = null;
let queue: Promise<void> = Promise.resolve();
/** Storage work, one after another (a line is never written before the one before it). */
const later = (job: () => Promise<void>) => { queue = queue.then(job).catch((e) => console.warn('[lapin] journal storage:', e)); };

async function loadJournals(): Promise<void> {
  try {
    dir = await (await navigator.storage.getDirectory()).getDirectoryHandle('recovery', { create: true });
    for await (const [name, handle] of (dir as unknown as AsyncIterable<[string, FileSystemHandle]>)) {
      if (handle.kind !== 'file' || !name.endsWith('.jsonl')) continue;
      const file = await (handle as FileSystemFileHandle).getFile();
      journals.set(name, { lines: (await file.text()).split('\n').filter((l) => l.length > 0), at: file.lastModified });
    }
    const held = await navigator.locks.query();
    othersHeld = new Set((held.held ?? []).map((l) => l.name ?? '').filter((n) => n.startsWith('lapin-journal:')).map((n) => n.slice('lapin-journal:'.length)));
  } catch (e) {
    // No storage (a private window that refuses it): journals live as long as the tab.
    console.warn('[lapin] no storage for the recovery journals:', e);
    dir = null;
  }
}

function hold(name: string): void {
  mine.add(name);
  void navigator.locks?.request(`lapin-journal:${name}`, () => new Promise<void>((release) => releases.set(name, release)));
}

function letGo(name: string): void {
  mine.delete(name);
  releases.get(name)?.();
  releases.delete(name);
}

const journalModule = {
  create(name: string) {
    journals.set(name, { lines: [], at: Date.now() });
    hold(name);
    later(async () => { await dir?.getFileHandle(name, { create: true }); });
  },
  open(name: string) { hold(name); },
  close(name: string) { letGo(name); },
  write(name: string, line: string) {
    const j = journals.get(name) ?? { lines: [], at: 0 };
    j.lines.push(line);
    j.at = Date.now();
    journals.set(name, j);
    const text = j.lines.join('\n') + '\n';
    later(async () => {
      if (!dir) return;
      const w = await (await dir.getFileHandle(name, { create: true })).createWritable();
      await w.write(text);
      await w.close();
    });
  },
  names: () => [...journals.keys()],
  inUse: (name: string) => mine.has(name) || othersHeld.has(name),
  lines: (name: string) => journals.get(name)?.lines ?? [],
  lastWrite: (name: string) => journals.get(name)?.at ?? 0,
  remove(name: string) {
    journals.delete(name);
    later(async () => { await dir?.removeEntry(name).catch(() => undefined); });
  },
};

// ─── The engine ──────────────────────────────────────────────────────────

/** A frame to the page, its buffer handed over (no copy on the way). */
function post(bytes: Uint8Array): void {
  const copy = bytes.slice();
  scope.postMessage({ frame: copy.buffer }, [copy.buffer]);
}

/** Where the start has got to, for the page to show (and for a start that hangs, to tell where). */
const step = (s: string) => scope.postMessage({ step: s });

async function start(base: string): Promise<void> {
  step('storage');
  await loadJournals();
  step('runtime');
  // The runtime's own main thread, run in this worker (.NET calls it a sidecar): said before dotnet.js loads, else it
  // takes a worker with a message handler for one of its helper threads and waits for orders that never come.
  (globalThis as unknown as { dotnetSidecar: boolean }).dotnetSidecar = true;
  const { dotnet } = await import(/* @vite-ignore */ `${base}_framework/dotnet.js`);
  const runtime = await dotnet.create();
  step('engine');
  runtime.setModuleImports('lapin-host', { frame: post });
  runtime.setModuleImports('lapin-journal', journalModule);
  await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
  const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  engine = exports.Lapin.Browser.Engine as Exports;
  engine.Start();
}

scope.onmessage = async (ev: MessageEvent<{ start?: string; call?: string }>) => {
  if (ev.data.start) {
    try {
      await start(ev.data.start);
      scope.postMessage({ ready: true });
    } catch (e) {
      scope.postMessage({ failed: e instanceof Error ? e.message : String(e) });
    }
    return;
  }
  if (ev.data.call && engine) post(engine.Call(ev.data.call));
};
