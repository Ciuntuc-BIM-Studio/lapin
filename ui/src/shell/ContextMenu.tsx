/**
 * The right-click menu: one for the whole window, opened at the cursor with
 * the entries the caller gives, kept on screen, closed by a choice, Esc, a
 * click elsewhere, the window losing focus or resizing.
 */

import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { create } from 'zustand';

export interface MenuItem {
  label: string;
  icon?: ReactNode;
  /** A tick before it (a toggle that is on). */
  checked?: boolean;
  disabled?: boolean;
  onSelect: () => void;
}

interface MenuState { at: { x: number; y: number } | null; items: MenuItem[] }

export const useContextMenu = create<MenuState>(() => ({ at: null, items: [] }));

/** Open the menu at a point of the window (client px); with no entries it stays shut. */
export function openContextMenu(x: number, y: number, items: MenuItem[]): void {
  useContextMenu.setState(items.length ? { at: { x, y }, items } : { at: null, items: [] });
}

export function closeContextMenu(): void {
  useContextMenu.setState({ at: null, items: [] });
}

/** Where a menu of `size` opened at `at` goes so it stays inside the window: flipped to the cursor's other side when it would run off, else pushed in. */
export function placeMenu(at: { x: number; y: number }, size: { w: number; h: number }, win: { w: number; h: number }, margin = 4): { x: number; y: number } {
  const fit = (p: number, s: number, max: number) => {
    if (p + s <= max - margin) return p;
    if (p - s >= margin) return p - s;
    return Math.max(margin, max - margin - s);
  };
  return { x: fit(at.x, size.w, win.w), y: fit(at.y, size.h, win.h) };
}

export function ContextMenuHost() {
  const { at, items } = useContextMenu();
  const ref = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!at || !el) { setPos(null); return; }
    setPos(placeMenu(at, { w: el.offsetWidth, h: el.offsetHeight }, { w: window.innerWidth, h: window.innerHeight }));
  }, [at, items]);

  useEffect(() => {
    if (!at) return;
    const away = (e: PointerEvent) => { if (!ref.current?.contains(e.target as Node)) closeContextMenu(); };
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); closeContextMenu(); } };
    window.addEventListener('pointerdown', away, true);
    window.addEventListener('keydown', key, true);
    window.addEventListener('blur', closeContextMenu);
    window.addEventListener('resize', closeContextMenu);
    return () => {
      window.removeEventListener('pointerdown', away, true);
      window.removeEventListener('keydown', key, true);
      window.removeEventListener('blur', closeContextMenu);
      window.removeEventListener('resize', closeContextMenu);
    };
  }, [at]);

  if (!at) return null;
  // Fixed on the window, inside the app's root, where the translator puts its entries into the language chosen.
  return (
    <div ref={ref} className="context-menu" role="menu" style={{ left: (pos ?? at).x, top: (pos ?? at).y, visibility: pos ? 'visible' : 'hidden' }}
      onContextMenu={(e) => e.preventDefault()}>
      {items.map((it, i) => (
        <button key={i} type="button" role="menuitem" className="context-item" disabled={it.disabled}
          onClick={() => { closeContextMenu(); it.onSelect(); }}>
          <span className="context-icon">{it.checked ? '✓' : it.icon}</span>
          <span>{it.label}</span>
        </button>
      ))}
    </div>
  );
}
