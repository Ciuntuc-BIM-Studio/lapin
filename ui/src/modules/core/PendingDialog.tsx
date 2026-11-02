/**
 * A dialog's frame while what it shows is on its way, or could not be read:
 * the backdrop, the title, Close and "Reading…" (or the error). A dialog
 * never draws nothing while it is open, so the keys always have a dialog on
 * the screen to belong to, and Close is always there.
 */

import { X } from 'lucide-react';
import { useUi } from '../../app/store';

export function PendingDialog({ title, error, onClose }: { title: string; error?: string | null; onClose?: () => void }) {
  const close = onClose ?? (() => useUi.setState({ dialog: null }));
  return (
    <div className="dialog-backdrop" role="dialog" aria-label={title}>
      <div className="dialog">
        <header className="dialog-head">
          <span className="panel-title">{title}</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          {error ? <p className="error-text" role="alert">{error}</p> : <p className="muted">Reading…</p>}
        </div>
      </div>
    </div>
  );
}
