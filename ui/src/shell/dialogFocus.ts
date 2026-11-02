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
