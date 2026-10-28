/**
 * The page stays still under the views (report #25: in the browser, a
 * trackpad's scroll over a view also rubber-banded the whole page, and a pinch
 * zoomed the page). The stylesheet takes the page's own scrolling and its
 * elastic overscroll away (theme.css); here the wheel over a view, and a pinch
 * anywhere, stop at the view: React's wheel listeners are passive, so they
 * cannot cancel what the browser does with the page. Safari reports a pinch as
 * gesture events rather than a wheel with Ctrl: it is turned into the wheel
 * the other browsers send, on the view under the fingers, so it zooms the view.
 * Panels that scroll (the browser, properties, dialogs) keep their wheel.
 */

/** What zooms with the wheel: the 2D views, sheets and schedules, the 3D canvas, the key plan. */
export const ZOOM_SURFACE = 'canvas, svg.plan-svg, [data-wheel-zoom]';

const surfaceOf = (t: EventTarget | null): Element | null =>
  t && typeof (t as Element).closest === 'function' ? (t as Element).closest(ZOOM_SURFACE) : null;

/** Whether the browser must be kept from scrolling or zooming the page for this wheel. */
export const cancelsWheel = (e: { ctrlKey: boolean; target: EventTarget | null }) => e.ctrlKey || !!surfaceOf(e.target);

/**
 * The Ctrl-wheel's deltaY that a pinch from scale `prev` to `next` stands
 * for: Chrome and Firefox report a pinch as a zoom by exp(−deltaY / 100).
 */
export const pinchDelta = (prev: number, next: number) => (prev > 0 && next > 0 ? -100 * Math.log(next / prev) : 0);

type GestureLike = Event & { scale?: number; clientX?: number; clientY?: number };

export function installPageGuards(doc: Document = document): () => void {
  const win = doc.defaultView ?? window;
  let lastCtrlWheel = -Infinity;
  let scale = 1;
  let at: { x: number; y: number } | null = null;
  // Where the pointer is, should a gesture not say where the fingers are.
  let pointer: { x: number; y: number } | null = null;

  const onWheel = (e: WheelEvent) => {
    if (e.ctrlKey && e.isTrusted) lastCtrlWheel = performance.now();
    if (cancelsWheel(e)) e.preventDefault();
  };
  const onGestureStart = (e: GestureLike) => {
    e.preventDefault();
    scale = e.scale ?? 1;
    at = e.clientX != null && e.clientY != null ? { x: e.clientX, y: e.clientY } : null;
  };
  const onGestureChange = (e: GestureLike) => {
    e.preventDefault();
    const next = e.scale ?? 1;
    const deltaY = pinchDelta(scale, next);
    scale = next;
    // A browser that already sent the pinch as a Ctrl-wheel has zoomed the view.
    if (performance.now() - lastCtrlWheel < 200 || !deltaY) return;
    const p = e.clientX != null && e.clientY != null ? { x: e.clientX, y: e.clientY } : at ?? pointer;
    if (!p) return;
    const target = surfaceOf(doc.elementFromPoint(p.x, p.y));
    if (!target) return;
    target.dispatchEvent(new WheelEvent('wheel', { bubbles: true, cancelable: true, ctrlKey: true, deltaY, deltaMode: 0, clientX: p.x, clientY: p.y }));
  };
  const onGestureEnd = (e: Event) => { e.preventDefault(); at = null; };

  const onPointer = (e: PointerEvent) => { pointer = { x: e.clientX, y: e.clientY }; };
  win.addEventListener('wheel', onWheel, { passive: false });
  win.addEventListener('pointermove', onPointer, { passive: true });
  doc.addEventListener('gesturestart', onGestureStart as EventListener, { passive: false });
  doc.addEventListener('gesturechange', onGestureChange as EventListener, { passive: false });
  doc.addEventListener('gestureend', onGestureEnd, { passive: false });
  return () => {
    win.removeEventListener('wheel', onWheel);
    win.removeEventListener('pointermove', onPointer);
    doc.removeEventListener('gesturestart', onGestureStart as EventListener);
    doc.removeEventListener('gesturechange', onGestureChange as EventListener);
    doc.removeEventListener('gestureend', onGestureEnd);
  };
}
