/**
 * A long engine call shown while it runs (an IFC read or imported): what it
 * does, its stage, a bar when the engine says how far along it is (moving
 * on its own when it does not: the browser's engine runs in one go), and the
 * time so far, and Cancel when the call can be stopped (an SDNF import, until
 * it builds the bodies). Over everything, so nothing is clicked meanwhile.
 */

import { useEffect, useState } from 'react';
import { cancellable, useIfcImport } from '../engine/ifcImport';

const seconds = (ms: number) => (ms < 60_000 ? `${Math.floor(ms / 1000)} s` : `${Math.floor(ms / 60_000)} min ${Math.floor((ms % 60_000) / 1000)} s`);

export function BusyOverlay() {
  const busy = useIfcImport((s) => s.busy);
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    if (!busy) return;
    const t = setInterval(() => setNow(Date.now()), 250);
    return () => clearInterval(t);
  }, [busy]);
  if (!busy) return null;
  const known = busy.total > 0;
  const share = known ? Math.min(1, busy.done / busy.total) : null;
  return (
    <div className="busy-backdrop" role="alertdialog" aria-live="polite" aria-label="Working">
      <div className="busy-card" data-testid="busy">
        <div className="busy-title" data-no-i18n>{busy.title}</div>
        <div className="busy-stage">
          <span>{busy.stage}</span>
          {known && <span className="num muted" data-no-i18n> {busy.done.toLocaleString()} / {busy.total.toLocaleString()}</span>}
        </div>
        <div className={`busy-bar${known ? '' : ' indeterminate'}`}>
          <span style={share != null ? { width: `${Math.round(share * 100)}%` } : undefined} />
        </div>
        <div className="busy-time muted small" data-no-i18n>{seconds(Math.max(0, now - busy.started))}</div>
        {cancellable(busy) && <div className="row"><span className="grow" /><button type="button" className="btn" data-action="busy-cancel" onClick={busy.cancel}>Cancel</button></div>}
      </div>
    </div>
  );
}
