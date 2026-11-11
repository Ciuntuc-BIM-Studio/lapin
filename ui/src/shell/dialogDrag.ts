/**
 * Every dialog moves by its title bar (report #140): pressed and dragged with a mouse, a pen or a finger, kept inside
 * the window, its place remembered for the session per dialog (so it opens where it was left), a double click on the
 * title putting it back in the middle. One listener for the whole window: a dialog is any `.dialog` with a
 * `.dialog-head`, so the ones modules add are covered without a call of their own. The dialog moves by the CSS
 * `translate` property, its layout (centred by its backdrop) left as it is, and its focus untouched.
 */

export interface Box { left: number; top: number; right: number; bottom: number }
export interface Offset { x: number; y: number }

/** Where a dialog whose untranslated box is `base` may be moved to in a window of `win`: wholly inside, or, when it is larger, centred on that axis. */
export function clampOffset(base: Box, win: { w: number; h: number }, want: Offset): Offset {
  const axis = (lo: number, hi: number, max: number, v: number) => {
    const min = -lo, top = max - hi;
    if (hi - lo >= max) return (min + top) / 2;
    return Math.min(Math.max(v, min), top);
  };
  return { x: axis(base.left, base.right, win.w, want.x), y: axis(base.top, base.bottom, win.h, want.y) };
}

/** What in a title bar keeps its own click: the buttons, fields and links. */
const OWN = 'button, input, select, textarea, a, label, summary, [role="button"], [contenteditable="true"]';

const remembered = new Map<string, Offset>();

/** A dialog's key: its data-dialog, else its classes and its title. */
function keyOf(dialog: HTMLElement): string {
  const named = dialog.dataset.dialog;
  if (named) return named;
  const head = dialog.querySelector('.dialog-head');
  return `${[...dialog.classList].sort().join('.')}|${(head?.textContent ?? '').trim().slice(0, 60)}`;
}

const offsetOf = (el: HTMLElement): Offset => ({ x: Number(el.dataset.dragX ?? 0), y: Number(el.dataset.dragY ?? 0) });

function place(dialog: HTMLElement, o: Offset) {
  dialog.dataset.dragX = String(o.x);
  dialog.dataset.dragY = String(o.y);
  dialog.style.translate = o.x || o.y ? `${o.x}px ${o.y}px` : '';
}

/** The dialog's box as laid out, without the move. */
function baseBox(dialog: HTMLElement): Box {
  const r = dialog.getBoundingClientRect();
  const o = offsetOf(dialog);
  return { left: r.left - o.x, top: r.top - o.y, right: r.right - o.x, bottom: r.bottom - o.y };
}

const windowSize = () => ({ w: window.innerWidth, h: window.innerHeight });

/** A dialog just opened: where it was left, if it was. */
function restore(dialog: HTMLElement) {
  if (dialog.dataset.dragKey) return;
  const key = keyOf(dialog);
  dialog.dataset.dragKey = key;
  const was = remembered.get(key);
  if (was) place(dialog, clampOffset(baseBox(dialog), windowSize(), was));
}

/** Start listening (once per window); returns the way to stop. */
export function installDialogDrag(): () => void {
  let drag: { dialog: HTMLElement; head: HTMLElement; id: number; sx: number; sy: number; from: Offset; base: Box } | null = null;

  const headOf = (e: Event) => {
    const t = e.target as Element | null;
    if (!t || !t.closest) return null;
    const head = t.closest<HTMLElement>('.dialog-head');
    const dialog = head?.closest<HTMLElement>('.dialog');
    if (!head || !dialog || t.closest(OWN)) return null;
    return { head, dialog };
  };

  const down = (e: PointerEvent) => {
    if (e.button !== 0 || !e.isPrimary) return;
    const hit = headOf(e);
    if (!hit) return;
    restore(hit.dialog);
    drag = { ...hit, id: e.pointerId, sx: e.clientX, sy: e.clientY, from: offsetOf(hit.dialog), base: baseBox(hit.dialog) };
    try { hit.head.setPointerCapture(e.pointerId); } catch { /* a synthetic pointer */ }
  };
  const move = (e: PointerEvent) => {
    if (!drag || e.pointerId !== drag.id) return;
    place(drag.dialog, clampOffset(drag.base, windowSize(), { x: drag.from.x + e.clientX - drag.sx, y: drag.from.y + e.clientY - drag.sy }));
  };
  const end = (e: PointerEvent) => {
    if (!drag || e.pointerId !== drag.id) return;
    remembered.set(drag.dialog.dataset.dragKey ?? keyOf(drag.dialog), offsetOf(drag.dialog));
    try { drag.head.releasePointerCapture(e.pointerId); } catch { /* already released */ }
    drag = null;
  };
  const centre = (e: MouseEvent) => {
    const hit = headOf(e);
    if (!hit) return;
    restore(hit.dialog);
    remembered.delete(hit.dialog.dataset.dragKey ?? keyOf(hit.dialog));
    place(hit.dialog, { x: 0, y: 0 });
  };
  const resize = () => {
    for (const d of document.querySelectorAll<HTMLElement>('.dialog[data-drag-key]')) {
      const o = offsetOf(d);
      if (o.x || o.y) place(d, clampOffset(baseBox(d), windowSize(), o));
    }
  };
  // Dialogs opened later find their place again.
  const seen = new MutationObserver((records) => {
    for (const r of records) for (const n of r.addedNodes) {
      if (!(n instanceof HTMLElement)) continue;
      if (n.classList.contains('dialog')) restore(n);
      n.querySelectorAll<HTMLElement>('.dialog').forEach(restore);
    }
  });

  document.addEventListener('pointerdown', down);
  document.addEventListener('pointermove', move);
  document.addEventListener('pointerup', end);
  document.addEventListener('pointercancel', end);
  document.addEventListener('dblclick', centre);
  window.addEventListener('resize', resize);
  seen.observe(document.body, { childList: true, subtree: true });
  return () => {
    document.removeEventListener('pointerdown', down);
    document.removeEventListener('pointermove', move);
    document.removeEventListener('pointerup', end);
    document.removeEventListener('pointercancel', end);
    document.removeEventListener('dblclick', centre);
    window.removeEventListener('resize', resize);
    seen.disconnect();
  };
}
