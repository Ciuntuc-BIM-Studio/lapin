/**
 * The engine in a Web Worker (the web build): started once, with the engine
 * folder next to the page; its frames come back as transferred buffers.
 */

import type { EngineTransport } from './client';

export function workerTransport(engineFolder = new URL('engine/', document.baseURI).href): EngineTransport {
  return {
    retries: false,
    connect: ({ onOpen, onMessage, onClose }) => new Promise<void>((resolve, reject) => {
      const worker = new Worker(new URL('./worker/engine.worker.ts', import.meta.url), { type: 'module' });
      let up = false;
      worker.onmessage = (ev: MessageEvent<{ ready?: boolean; failed?: string; frame?: ArrayBuffer }>) => {
        if (ev.data.frame) onMessage(ev.data.frame);
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
