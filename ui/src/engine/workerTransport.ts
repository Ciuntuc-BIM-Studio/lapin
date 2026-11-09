/**
 * The engine in a Web Worker (the web build): started once, with the engine
 * folder next to the page; its frames come back as transferred buffers. A
 * lazy part the engine fetches the first time it needs it (worker/engine.worker.ts)
 * is said to `onPart`: loading, loaded, or failed.
 */

import type { EngineTransport } from './client';

export type PartState = 'loading' | 'loaded' | 'failed';

/**
 * A lazy part of the engine that could not be fetched. The runtime remembers an assembly it could not load for as long as
 * it runs, so only a new page brings the part; the project can still be saved, and the recovery journal keeps the work.
 */
export const ENGINE_PART_FAILED = 'This part of Lapin could not be downloaded. Check the connection, save your work and reload the page.';

export function workerTransport(engineFolder = new URL('engine/', document.baseURI).href, onPart?: (name: string, state: PartState) => void): EngineTransport {
  let partFailed = false;
  return {
    retries: false,
    // What the engine answers each time it reaches for the part that did not come is the runtime's own text.
    explain: (error) => (partFailed && /could not load|assembly|typeload|method not found/i.test(error) ? ENGINE_PART_FAILED : error),
    connect: ({ onOpen, onMessage, onClose }) => new Promise<void>((resolve, reject) => {
      const worker = new Worker(new URL('./worker/engine.worker.ts', import.meta.url), { type: 'module' });
      let up = false;
      worker.onmessage = (ev: MessageEvent<{ ready?: boolean; failed?: string; frame?: ArrayBuffer; part?: string; state?: PartState }>) => {
        if (ev.data.frame) onMessage(ev.data.frame);
        else if (ev.data.part && ev.data.state) { partFailed ||= ev.data.state === 'failed'; onPart?.(ev.data.part, ev.data.state); }
        else if (ev.data.ready) { up = true; onOpen({ send: (text) => worker.postMessage({ call: text }), close: () => worker.terminate() }); resolve(); }
        else if (ev.data.failed !== undefined) { worker.terminate(); reject(new Error(`The engine could not start: ${ev.data.failed}`)); }
      };
      worker.onerror = (e) => {
        if (up) onClose(); else reject(new Error(`The engine could not start: ${e.message}`));
      };
      worker.postMessage({ start: engineFolder });
    }),
  };
}
