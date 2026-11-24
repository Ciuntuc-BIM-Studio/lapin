/** A submenu: an item that opens a menu beside it (File → Import ▸, Help → Language ▸). */

import { useRef, useState } from 'react';
import { ChevronRight } from 'lucide-react';

/**
 * A submenu of a menu (File → Import, Help → Language): it opens beside its item on hover, a click, Enter or the right arrow, and closes on the
 * left arrow or Escape (focus back on the item). It is placed by the item's box (fixed), so the menu's own scrolling does not clip it.
 */
export function MenuSub({ label, children, toLeft }: { label: string; children: React.ReactNode; /** Opens to the left of its item (a menu at the window's right edge). */ toLeft?: boolean }) {
  const [open, setOpen] = useState(false);
  const [at, setAt] = useState<React.CSSProperties>({});
  const item = useRef<HTMLButtonElement>(null);
  const box = useRef<HTMLDivElement>(null);
  const show = () => {
    const r = item.current?.getBoundingClientRect();
    if (r) {
      const top = Math.max(0, Math.min(r.top - 6, window.innerHeight - 320));
      setAt(toLeft ? { right: Math.max(0, window.innerWidth - r.left - 2), top } : { left: Math.min(r.right - 2, Math.max(0, window.innerWidth - 300)), top });
    }
    setOpen(true);
  };
  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowRight' || e.key === 'Enter' || e.key === ' ') {
      if (e.target !== item.current) return;
      e.preventDefault(); show();
      requestAnimationFrame(() => box.current?.querySelector<HTMLElement>('[role=menuitem]')?.focus());
    } else if (e.key === 'ArrowLeft' || e.key === 'Escape') {
      if (!open) return;
      e.preventDefault(); e.stopPropagation(); setOpen(false); item.current?.focus();
    } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      const inside = box.current?.contains(e.target as Node);
      if (!inside) return;
      const items = [...box.current!.querySelectorAll<HTMLElement>('[role=menuitem]:not(:disabled)')];
      const i = items.indexOf(e.target as HTMLElement);
      e.preventDefault();
      items[(i + (e.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length]?.focus();
    }
  };
  return (
    <div className="menu-sub" data-menu-sub-of={label} onMouseEnter={show} onMouseLeave={() => setOpen(false)} onKeyDown={onKey}>
      <button type="button" role="menuitem" data-menu-sub={label} ref={item} className={open ? 'open' : undefined} aria-haspopup="menu" aria-expanded={open} onClick={show}>{label}<ChevronRight size={13} aria-hidden /></button>
      {open && <div className="menu sub" role="menu" ref={box} style={at}>{children}</div>}
    </div>
  );
}
