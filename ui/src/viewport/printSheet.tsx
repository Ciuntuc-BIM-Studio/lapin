/**
 * Printing a sheet (decision 236): its paper as the engine composes it (the same pens as the PDF), drawn as vector SVG at the
 * paper's real size into a page the print dialog sees alone, the page sized to the sheet (A1 landscape: 841 × 594 mm), so a
 * plotter takes it at 1:1 and a smaller printer fits it to its paper. The desktop's print goes through Tauri's webview print
 * on macOS (window.print there is Tauri's); the browser's is its own.
 */

import { flushSync } from 'react-dom';
import { createRoot, type Root } from 'react-dom/client';
import { DrawingLayer } from './DrawingLayer';
import type { PenDto } from './drawing';

/** Drawing units per paper mm: finer than the SVG's one decimal. */
const U = 10;
let root: Root | null = null;

function host(): HTMLElement {
  let el = document.getElementById('lapin-print');
  if (!el) {
    el = document.createElement('div');
    el.id = 'lapin-print';
    document.body.appendChild(el);
    root = createRoot(el);
  }
  return el;
}

function pageStyle(w: number, h: number) {
  let style = document.getElementById('lapin-print-page') as HTMLStyleElement | null;
  if (!style) {
    style = document.createElement('style');
    style.id = 'lapin-print-page';
    document.head.appendChild(style);
  }
  // On the screen it is never seen; printing, it is all there is, at the paper's size, without margins.
  style.textContent = `
#lapin-print { display: none; }
@media print {
  @page { size: ${w}mm ${h}mm; margin: 0; }
  html, body { margin: 0 !important; padding: 0 !important; background: #fff !important; }
  body > *:not(#lapin-print) { display: none !important; }
  #lapin-print { display: block; }
  #lapin-print svg { display: block; width: ${w}mm; height: ${h}mm; }
}`;
}

/** Prints a sheet's composed paper: w × h mm and its pens. */
export function printSheet(sheet: { w: number; h: number; pens: PenDto[] }, title: string) {
  const el = host();
  pageStyle(sheet.w, sheet.h);
  flushSync(() => {
    root!.render(
      <svg xmlns="http://www.w3.org/2000/svg" viewBox={`0 0 ${sheet.w * U} ${sheet.h * U}`} aria-label={title}>
        <rect x={0} y={0} width={sheet.w * U} height={sheet.h * U} fill="#ffffff" />
        <DrawingLayer drawing={{ pens: sheet.pens, scale: 1000 }} toScreen={(p) => ({ x: p.x * U, y: p.y * U })} pxPerMetre={U} />
      </svg>,
    );
  });
  void el.offsetHeight;
  const before = document.title;
  document.title = title;
  // The pictures (an orthophoto) are loaded before the dialog takes the page.
  const images = [...el.querySelectorAll('image')].map((img) => new Promise<void>((done) => {
    const src = img.getAttribute('href') ?? '';
    if (!src) { done(); return; }
    const probe = new Image();
    probe.onload = probe.onerror = () => done();
    probe.src = src;
  }));
  void Promise.all(images).then(() => {
    window.print();
    setTimeout(() => { document.title = before; }, 1000);
  });
}
