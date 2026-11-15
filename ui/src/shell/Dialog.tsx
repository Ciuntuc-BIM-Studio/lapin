/**
 * The dialog shell (audit U1, U2, I1): what every dialog does the same way.
 *  - the backdrop closes the dialog only when a press STARTS on it (a text selection dragged out of the dialog and released
 *    on the backdrop does not), see `backdropClose`;
 *  - focus goes into the dialog when it opens and back to where it was when it closes, and the dialog is modal for assistive
 *    technology (`useDialogLayer`, mounted once in the App, applies this to every `.dialog-backdrop`, old markup included);
 *  - Escape closes it, also from a field (app/keys.ts), unless the field took the key first.
 * `Dialog` is the markup with the existing class names (`dialog-backdrop`, `dialog`, `dialog-head`, `dialog-body`,
 * `dialog-foot`) so tests and guide scenes keep finding what they drive.
 */

import { useEffect, type HTMLAttributes, type MouseEvent as RMouseEvent, type ReactNode } from 'react';
import { X } from 'lucide-react';

/** The backdrop's press handler: closes when the press began on the backdrop itself. */
export const backdropClose = (close: () => void) => (e: RMouseEvent<HTMLElement>) => { if (e.target === e.currentTarget) close(); };

export function Dialog({ title, onClose, children, footer, className, backdropClassName, dataDialog, testId, ...rest }: {
  title: ReactNode; onClose: () => void; children: ReactNode; footer?: ReactNode; className?: string; backdropClassName?: string;
  dataDialog?: string; testId?: string;
} & Omit<HTMLAttributes<HTMLDivElement>, 'title' | 'onClick'>) {
  return (
    <div className={`dialog-backdrop${backdropClassName ? ` ${backdropClassName}` : ''}`} role="dialog" aria-modal="true" aria-label={typeof title === 'string' ? title : undefined}
      tabIndex={-1} data-dialog={dataDialog} onMouseDown={backdropClose(onClose)}>
      <div className={`dialog${className ? ` ${className}` : ''}`} data-testid={testId} {...rest}>
        <header className="dialog-head">
          <span className="panel-title">{title}</span>
          <button type="button" className="icon" onClick={onClose} title="Close" aria-label="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">{children}</div>
        {footer && <footer className="dialog-foot">{footer}</footer>}
      </div>
    </div>
  );
}

/** The backdrops now on the screen, newest last. */
const layers = () => [...document.querySelectorAll<HTMLElement>('.dialog-backdrop:not(.clash-backdrop):not(.palette-backdrop)')];

/**
 * Focus into a dialog that opens (unless something in it already has it), back to the element that had it when the last
 * dialog closes, and aria-modal on every backdrop. Mounted once, by the App.
 */
export function useDialogLayer(): void {
  useEffect(() => {
    let before: HTMLElement | null = null;
    let open = 0;
    const sync = () => {
      const all = layers();
      for (const el of all) if (!el.hasAttribute('aria-modal')) el.setAttribute('aria-modal', 'true');
      if (all.length && !open) {
        const a = document.activeElement as HTMLElement | null;
        before = a && a !== document.body && !a.closest('.dialog-backdrop') ? a : null;
      }
      const top = all[all.length - 1];
      if (top && !top.contains(document.activeElement)) {
        if (!top.hasAttribute('tabindex')) top.setAttribute('tabindex', '-1');
        top.focus({ preventScroll: true });
      }
      if (!all.length && open && before?.isConnected) { const b = before; before = null; b.focus({ preventScroll: true }); }
      if (!all.length) before = null;
      open = all.length;
    };
    const mo = new MutationObserver(() => sync());
    mo.observe(document.body, { childList: true, subtree: true });
    sync();
    return () => mo.disconnect();
  }, []);
}
