/**
 * Manage → Remarks: the remarks reviewers left on issued PDFs, read back
 * into the project (docs/plan-cde.md, stage 3). Read PDF remarks… picks a
 * file (the folder's manifest, the project's issues or the file name say
 * which sheet it is; else the user does), shows what was found and imports
 * it — the same file read again updates its remarks. On the desktop, Scan
 * the project folder lists the issued PDFs in Shared and Published changed
 * since their issue. The list is grouped by sheet with a status filter; a
 * click opens the sheet with the remark in front; each can be resolved,
 * closed, reopened or deleted.
 */

import { useState } from 'react';
import { FileSearch, FolderSearch, X } from 'lucide-react';
import { notify, useUi } from '../../../../app/store';
import { useSession } from '../../../../engine/session';
import { isDesktop } from '../../../../engine/files';
import { RemarkActions } from './RemarksOverlay';
import {
  filterRemarks, groupBySheet, importRemarks, initials, KIND_LABELS, pickPdf, readPdf, remarksOf, scanRemarks, showRemark,
  type PdfReadResult, type PdfSource, type Remark, type ScannedPdf, type StatusFilter,
} from './remarks';

const when = (iso: string | null) => { if (!iso) return ''; const d = new Date(iso); return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }); };
const FILTERS: [StatusFilter, string][] = [['open', 'Open'], ['resolved', 'Resolved'], ['closed', 'Closed'], ['all', 'All']];
const BY: Record<PdfReadResult['by'], string> = {
  issue: 'matched by the project\'s issue record', manifest: 'matched by the manifest beside the file', name: 'matched by the file name', chosen: 'the sheet you chose', none: 'no sheet matched: choose one',
};

export function RemarksDialog() {
  const nodes = useSession((s) => s.nodes);
  const [filter, setFilter] = useState<StatusFilter>('open');
  const [busy, setBusy] = useState(false);
  const [read, setRead] = useState<{ source: PdfSource; result: PdfReadResult } | null>(null);
  const [scan, setScan] = useState<ScannedPdf[] | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const sheets = [...nodes.values()].filter((n) => n.type === 'LapinSheet').sort((a, b) => String(a.props.number ?? '').localeCompare(String(b.props.number ?? ''), undefined, { numeric: true }));
  const all = remarksOf(nodes);
  const groups = groupBySheet(filterRemarks(all, filter), nodes);

  const fail = (e: unknown) => notify(e instanceof Error ? e.message : String(e));
  const readFile = async (source: PdfSource, sheetId?: string) => {
    setBusy(true);
    try { setRead({ source, result: await readPdf(source, sheetId) }); } catch (e) { fail(e); } finally { setBusy(false); }
  };
  const pick = async () => { const s = await pickPdf(); if (s) await readFile(s); };
  const doScan = async () => {
    setBusy(true);
    try { setScan(await scanRemarks()); } catch (e) { fail(e); } finally { setBusy(false); }
  };
  const doImport = async () => {
    if (!read) return;
    setBusy(true);
    try {
      const r = await importRemarks(read.result.remarks);
      notify(`${read.result.file}: ${r?.added ?? 0} remark(s) added, ${r?.updated ?? 0} updated.`);
      setRead(null);
      if (scan) setScan(scan.map((s) => (s.name === read.result.file ? { ...s, unread: false, read: new Date().toISOString() } : s)));
    } catch (e) { fail(e); } finally { setBusy(false); }
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Remarks" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Remarks on sheets</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body cde remarks-dialog">
          <p className="muted small">What a reviewer marked on an issued PDF — notes, text, highlights, pen, shapes, clouds, stamps — read back onto the sheet, at its place, with who and when. Reading the same file again updates its remarks. They show over the sheet on screen only; a sheet prints without them.</p>
          <div className="row">
            <button type="button" className="btn" disabled={busy} onClick={() => void pick()}><FileSearch size={13} /> Read PDF remarks…</button>
            {isDesktop() && <button type="button" className="btn" disabled={busy} title="The issued PDFs in Shared and Published changed since their issue" onClick={() => void doScan()}><FolderSearch size={13} /> Scan the project folder</button>}
            <span className="grow" />
            <span className="chips" role="radiogroup" aria-label="Status" data-i18n-ctx="status">
              {FILTERS.map(([id, label]) => <button key={id} type="button" role="radio" aria-checked={filter === id} className={`chip${filter === id ? ' on' : ''}`} onClick={() => setFilter(id)}>{label}</button>)}
            </span>
          </div>

          {scan && (
            <div className="remarks-scan">
              <h4>Issued PDFs in the project folder</h4>
              {scan.length === 0 && <p className="muted small">No issued PDF in Shared or Published. Issue first, then let the reviewers mark the files there.</p>}
              {scan.length > 0 && (
                <table className="layer-table versions">
                  <thead><tr><th>File</th><th>Sheet</th><th>Revision</th><th>Changed</th><th>Read</th><th /></tr></thead>
                  <tbody>
                    {scan.map((s) => (
                      <tr key={s.path} className={s.unread ? 'unread' : undefined}>
                        <td><code>{s.name}</code></td>
                        <td>{s.set ? 'the set' : `${s.sheetNumber ?? ''} · ${s.sheetName ?? ''}`}</td>
                        <td>{s.revision ?? '—'}</td>
                        <td>{when(s.modified)}{s.newer && <span className="cde-warn"> · after the issue</span>}</td>
                        <td className="muted small">{s.read ? when(s.read) : 'never'}</td>
                        <td className="row-tools"><button type="button" className="btn small" disabled={busy} onClick={() => void readFile({ path: s.path, name: s.name })}>Read</button></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}

          {read && (
            <div className="remarks-read" data-testid="remarks-read">
              <h4>{read.result.file}</h4>
              <p className="muted small">
                {read.result.pages} <span>page(s)</span> · {read.result.annotations} <span>annotation(s)</span> · <span>{BY[read.result.by]}</span>
                {read.result.revision ? ` · ${read.result.revision}` : ''}{read.result.skipped ? <> · {read.result.skipped} <span>left out (links, popups, forms)</span></> : null}
              </p>
              {read.result.sheets.length > 1 && (
                <ul className="small">{read.result.sheets.map((p) => <li key={p.page}><span>Page</span> {p.page}: {p.sheetId ? `${p.number ?? ''} · ${p.name ?? ''}` : <span>no sheet</span>} · {p.annotations} <span>annotation(s)</span></li>)}</ul>
              )}
              <label className="row"><span className="muted small loc-label">Sheet</span>
                <select value={read.result.sheets[0]?.sheetId ?? ''} disabled={busy} onChange={(e) => { if (e.target.value) void readFile(read.source, e.target.value); }}>
                  <option value="">{read.result.by === 'none' ? 'Choose the sheet…' : 'Another sheet…'}</option>
                  {sheets.map((s) => <option key={s.id} value={s.id}>{String(s.props.number ?? '')} · {s.name}</option>)}
                </select>
              </label>
              {read.result.warnings.map((w, i) => <p key={i} className="cde-warn small">{w}</p>)}
              {read.result.unplaced > 0 && <p className="cde-warn small">{read.result.unplaced} <span>annotation(s) on pages with no sheet.</span></p>}
              <div className="row">
                <span className="muted small">{read.result.remarks.length} <span>remark(s) to import</span></span>
                <span className="grow" />
                <button type="button" className="btn" disabled={busy} onClick={() => setRead(null)}>Cancel</button>
                <button type="button" className="btn primary" disabled={busy || read.result.remarks.length === 0} onClick={() => void doImport()}>Import</button>
              </div>
            </div>
          )}

          <h4>Remarks</h4>
          {all.length === 0 && <p className="muted">No remark yet. Read PDF remarks… takes a reviewer's annotations from an issued PDF.</p>}
          {all.length > 0 && groups.length === 0 && <p className="muted">No remark with this status.</p>}
          <div className="remarks-list" data-testid="remarks-list">
            {groups.map((g) => (
              <div key={g.sheetId} className="version-group">
                <span>{g.label} · {g.remarks.length}</span>
                {g.remarks.map((r) => <RemarkRow key={r.id} remark={r} />)}
              </div>
            ))}
          </div>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

function RemarkRow({ remark: r }: { remark: Remark }) {
  return (
    <div className={`remark-row ${r.status}`}>
      <span className="remark-initials" title={r.author ?? ''}>{initials(r.author)}</span>
      <div className="grow">
        <button type="button" className="link" title="Open the sheet with this remark in front" onClick={() => showRemark(r)}>{r.name}</button>
        <div className="muted small"><span>{KIND_LABELS[r.kind] ?? r.kind}</span> · {r.author ?? '—'}{r.date ? ` · ${when(r.date)}` : ''}{r.replies.length ? ` · ${r.replies.length} repl${r.replies.length === 1 ? 'y' : 'ies'}` : ''} · {r.source.file}</div>
        <RemarkActions remark={r} />
      </div>
    </div>
  );
}
