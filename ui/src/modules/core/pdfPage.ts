/**
 * A page of a PDF drawn into a canvas with pdf.js (Apache-2.0), for the checks' findings drawn over it (CdeChecks.tsx). Loaded only
 * when a page is first shown, so the web build's first load never carries it. The PDF is someone else's: scripts are never evaluated
 * (isEvalSupported: false), no font or map is fetched, and the parsing runs in pdf.js's own worker, made by the bundler from this
 * origin (the CSP's worker-src 'self').
 */

import { GlobalWorkerOptions, getDocument, type PDFDocumentProxy } from 'pdfjs-dist';

let port: Worker | null = null;
function worker(): void {
  if (port) return;
  port = new Worker(new URL('pdfjs-dist/build/pdf.worker.min.mjs', import.meta.url), { type: 'module' });
  GlobalWorkerOptions.workerPort = port;
}

/** A PDF opened from its bytes (a copy: pdf.js takes the buffer over). */
export async function openPdf(bytes: Uint8Array): Promise<PDFDocumentProxy> {
  worker();
  return getDocument({ data: bytes.slice(), isEvalSupported: false, verbosity: 0, disableAutoFetch: true, stopAtErrors: false, enableXfa: false }).promise;
}

/**
 * One page (1-based) drawn at `scale` (pdf.js's viewport is in points, so 1 is 72 dpi), sharp on the screen's pixel ratio: its size in
 * CSS pixels. The page's own rotation is applied, as the checks measure it.
 */
export async function drawPage(doc: PDFDocumentProxy, page: number, canvas: HTMLCanvasElement, scale: number): Promise<{ width: number; height: number }> {
  const p = await doc.getPage(page);
  const viewport = p.getViewport({ scale });
  const ratio = Math.min(window.devicePixelRatio || 1, 2);
  canvas.width = Math.floor(viewport.width * ratio);
  canvas.height = Math.floor(viewport.height * ratio);
  canvas.style.width = `${viewport.width}px`;
  canvas.style.height = `${viewport.height}px`;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('No canvas.');
  await p.render({ canvasContext: ctx, viewport, transform: ratio === 1 ? undefined : [ratio, 0, 0, ratio, 0, 0] }).promise;
  return { width: viewport.width, height: viewport.height };
}
