/**
 * The band of a reference model opened from the CDE web's Documents tab (docs/plan-cde-references.md R7): which document it is, that it
 * is read-only, and its IDS report beside the 3D, opened from the band. Drawn only while one is open; the report's view is fetched when
 * first shown.
 */

import { useState } from 'react';
import { ShieldCheck, X } from 'lucide-react';
import { useCde } from '../../../engine/cdeServer';
import { lazyPart } from '../../../platform/lazyPart';

const IdsReportView = lazyPart(() => import('../CdeIds').then((m) => m.IdsReportView), { inline: true });

export function ReferenceModelBand() {
  const r = useCde((s) => s.reference);
  const [open, setOpen] = useState(false);
  if (!r) return null;
  const n = r.ids?.summary;
  return (
    <div className="reference-band" data-cde-reference={r.document}>
      <div className="reference-band-line">
        <span className="chip">{r.withProject ? 'Linked, read-only' : 'Read-only'}</span>
        <span data-no-i18n>{r.title}</span> <span className="muted" data-no-i18n>{r.file}</span>
        {n ? <button type="button" className="link small" data-cde="reference-ids" onClick={() => setOpen(!open)}><ShieldCheck size={12} /> <b>IDS</b> <span data-no-i18n>{n.passed}</span> <span>passing</span> · <span data-no-i18n>{n.failed}</span> <span>failing</span></button>
          : <span className="muted small">No IDS report</span>}
        <button type="button" className="icon mini" title="Close" onClick={() => useCde.setState({ reference: null })}><X size={12} /></button>
      </div>
      {open && r.ids && <div className="reference-band-report"><IdsReportView ids={r.ids} /></div>}
    </div>
  );
}
