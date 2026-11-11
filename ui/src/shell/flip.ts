/**
 * Smooth reordering (decision 479): after each draw, every element marked `data-flip="<key>"` inside the container that has moved
 * since the draw before slides from where it was to where it is, over 160 ms (the FLIP technique: the move is undone with a
 * transform, then let go). Nothing is done for a user who asks for reduced motion, or for an element that is new.
 */

import { useLayoutEffect, useRef, type RefObject } from 'react';

export function useFlip(host: RefObject<HTMLElement | null>, signature: string): void {
  const before = useRef(new Map<string, DOMRect>());
  useLayoutEffect(() => {
    const el = host.current;
    if (!el) return;
    const reduce = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    const now = new Map<string, DOMRect>();
    for (const c of el.querySelectorAll<HTMLElement>('[data-flip]')) {
      const key = c.dataset.flip!;
      const rect = c.getBoundingClientRect();
      now.set(key, rect);
      const was = before.current.get(key);
      if (reduce || !was || typeof c.animate !== 'function') continue;
      const dx = was.left - rect.left, dy = was.top - rect.top;
      if (Math.abs(dx) < 1 && Math.abs(dy) < 1) continue;
      c.animate([{ transform: `translate(${dx}px, ${dy}px)` }, { transform: 'none' }], { duration: 160, easing: 'ease-out' });
    }
    before.current = now;
  }, [host, signature]);
}
