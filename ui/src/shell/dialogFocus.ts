/** A dialog that is not the store's (`useUi.dialog`) takes the focus when it opens, so the keys are its own and not the model's behind it. */

import { useEffect, useRef } from 'react';

/** A ref for the dialog's role="dialog" element (with tabIndex={-1}): focused on mount unless something in it already is. */
export function useDialogFocus<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  useEffect(() => {
    const el = ref.current;
    if (el && !el.contains(document.activeElement)) el.focus({ preventScroll: true });
  }, []);
  return ref;
}

/** Whether an Escape belongs to a field (a text being typed, a select) and not to the dialog around it. */
export const escapeIsForField = (e: Pick<KeyboardEvent, 'target'>): boolean => !!(e.target as HTMLElement | null)?.closest?.('input, textarea, select, [contenteditable]');

/**
 * Escape closes a dialog of its own (not the store's): not while a field is being typed in (the written reply is not
 * lost), not when something took the key first (`defaultPrevented`), and the key goes no further, so it does not also
 * cancel the tool behind the dialog.
 */
export function useEscapeClose(onClose: () => void): void {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape' || e.defaultPrevented || e.isComposing || escapeIsForField(e)) return;
      e.preventDefault();
      e.stopPropagation();
      onClose();
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [onClose]);
}
