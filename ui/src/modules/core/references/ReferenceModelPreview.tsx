/**
 * A reference model in the CDE web's Documents tab (docs/plan-cde-references.md §8, stage R7): what the server read of it on upload (its
 * IFC header), its IDS report when the project's IDS rule ran on it, and Open in viewer / Open with the project (referenceModel.ts).
 */

import { useEffect, useState } from 'react';
import { Box, Link2 } from 'lucide-react';
import { notify } from '../../../app/store';
import { lazyPart } from '../../../platform/lazyPart';
import { useSession } from '../../../engine/session';
import { useRefAdapter, type ModelInfo } from './adapter';
import { headerFacts, idsCounts, openReferenceModel } from './referenceModel';
import type { RefDoc, RefFile } from './references';

// The IDS report's view (CdeIds.tsx), fetched when a report is shown.
const IdsReportView = lazyPart(() => import('../CdeIds').then((m) => m.IdsReportView), { inline: true });

export function ServerModelPreview({ doc, file }: { doc: RefDoc; file: RefFile }) {
  const adapter = useRefAdapter();
  // The browser's engine opens it: the buttons wait for it to have started.
  const connected = useSession((s) => s.connected);
  const [info, setInfo] = useState<ModelInfo | null | undefined>(undefined);
  const [busy, setBusy] = useState(false);
  const [report, setReport] = useState(false);
  useEffect(() => {
    let live = true;
    setInfo(undefined);
    void adapter.model?.(doc.id, file.name).then((i) => { if (live) setInfo(i); });
    return () => { live = false; };
  }, [adapter, doc.id, file.name]);
  const open = async (withProject: boolean) => {
    if (busy || !adapter.project) return;
    setBusy(true);
    try {
      const data = await adapter.file(doc.id, file.name);
      if (!data) { notify('The file could not be read.'); return; }
      await openReferenceModel({ project: adapter.project, document: { id: doc.id, title: doc.title }, file: data, ids: info?.ids ?? null, withProject });
    } finally { setBusy(false); }
  };
  const facts = headerFacts(info?.ifc);
  const counts = idsCounts(info?.ids);
  const unreadable = info?.ifc?.readable === false;
  return (
    <div className="refs-preview" data-testid="refs-model">
      {info === undefined && <p className="muted small">Reading…</p>}
      {facts.length > 0 && <p className="small">{facts.map((f, i) => <span key={f.label}>{i > 0 && ' · '}<span className="muted">{f.label}</span> <span data-no-i18n>{f.value}</span></span>)}</p>}
      {unreadable && <p className="cde-warn small">The server could not read this file as IFC: it may not open.</p>}
      <p className="muted small">A model from another program: it opens in 3D read-only, alone or linked into the project's latest commit, and nothing goes back to the server.</p>
      <div className="cde-actions">
        <button type="button" className="btn small" data-testid="refs-viewer" disabled={busy || !connected} onClick={() => void open(false)}><Box size={12} /> Open in viewer</button>
        <button type="button" className="btn small" data-testid="refs-with-project" disabled={busy || !connected} title="The project's latest commit on the server, read-only, with this model linked into it" onClick={() => void open(true)}><Link2 size={12} /> Open with the project</button>
      </div>
      {info && !counts && <p className="muted small">No IDS report: the project's IDS rule is off, or it has no IDS file.</p>}
      {counts && info?.ids && <>
        <p className="small" data-cde-ids-summary={counts.failed}><b>IDS</b> <span data-no-i18n>{counts.passed}</span> <span>passing</span> · <span data-no-i18n>{counts.failed}</span> <span>failing</span>
          <button type="button" className="link small" onClick={() => setReport(!report)}>{report ? 'Hide the report' : 'Show the report'}</button></p>
        {report && <IdsReportView ids={info.ids} />}
      </>}
    </div>
  );
}
